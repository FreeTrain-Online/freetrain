using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class FleetSessionTests
{
    static SessionHost Host()
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Stock",new(32,24,8),0);
        var w=new TerrainWorld(TerrainProfiles.Bundled,"Stock",new(32,24,8),0,
            Enumerable.Repeat(new TerrainCell(1,TerrainSurface.Cut),768),new(1,[]),[]);
        return new(new TerrainSessionState(w,TerrainProvenance.From(request)),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    }
    static FleetTradeQuery Query(SessionHost h,int length=3,int quantity=2)
    {
        var s=h.Snapshot();var b=RollingStockCatalog.Bundled.Trains[0].Binding;
        return new(s.WorldEpoch,s.Revision,s.Tick,s.ContentVersion,"buy",new(b.PackId,b.Version,b.CatalogHash,b.ContributionId),length,quantity,0);
    }
    static async Task<WireMessage> Send(SessionHost h,CommandPayload p,ParticipantContext? who=null)
    {var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,p));h.Pump(0);return await t;}
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task First_purchase_is_detached_atomic_and_publishes_only_revision_zero_new_members(bool mixed)
    {
        using var h=Host();
        if(mixed)Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(new(h.WorldEpoch,0,TerrainProfile.ContentVersion,new(10,10,1),new(14,10,1),"lay","source"))));
        var bytes=await Save(h);var before=h.Snapshot();var q=Query(h);var quote=h.PreviewFleetTrade(q);
        Assert.Equal(bytes,await Save(h));Assert.Equal(180_000_000,quote.Price);Assert.Equal(792,quote.NormalCapacity);
        Assert.Equal(396,quote.SeatedCapacity);Assert.Equal(1188,quote.PackedCapacity);
        Assert.IsType<AcceptedMessage>(await Send(h,new TradeFleetCommand(q,quote.Price)));
        var after=h.Snapshot();Assert.Equal(before.Tick,after.Tick);Assert.Equal(before.Paused,after.Paused);
        Assert.Equal(before.Cash-(mixed?quote.Price:quote.Price-ConstructionAccounts.DefaultInitialCash),after.Cash);
        Assert.Equal(2,after.Mixed!.Fleet!.Trains.Length);Assert.All(after.Mixed.Fleet.Groups,g=>Assert.Equal(0,g.Revision));
        Assert.All(after.Mixed.Fleet.Trains,t=>Assert.Equal(0,t.Revision));
        Assert.Single(after.Mixed.Accounts.Transactions,t=>t.Operation=="trainPurchase");
        var purchased=await Save(h);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeFleetCommand(q,quote.Price))).Code);
        Assert.Equal(purchased,await Save(h));
        var restored=SaveCodec.ReadWorld(purchased,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        using var reopened=new SessionHost(restored,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(purchased,await Save(reopened));
    }
    [Fact]
    public async Task Quotes_fence_clock_epoch_price_role_and_invalid_sizes_without_promotion()
    {
        using var h=Host();var q=Query(h);var quote=h.PreviewFleetTrade(q);var bytes=await Save(h);
        Assert.Throws<ArgumentException>(()=>h.PreviewFleetTrade(q with{Length=0}));
        Assert.Throws<ArgumentException>(()=>h.PreviewFleetTrade(q with{Definition=q.Definition! with{CatalogHash=new string('0',64)}}));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeFleetCommand(q,quote.Price+1))).Code);
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new TradeFleetCommand(q,quote.Price),observer)).Code);
        Assert.Equal(bytes,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new StepCommand()));
        Assert.Throws<InvalidOperationException>(()=>h.PreviewFleetTrade(q));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeFleetCommand(q,quote.Price))).Code);
        Assert.Null(h.Snapshot().Mixed);
    }
    [Fact]
    public async Task Rename_is_revision_fenced_and_sale_quotes_remove_only_selected_unplaced_stock()
    {
        using var h=Host();var q=Query(h);Assert.IsType<AcceptedMessage>(await Send(h,new TradeFleetCommand(q,h.PreviewFleetTrade(q).Price)));
        var train=h.Snapshot().Mixed!.Fleet!.Trains[0];
        Assert.IsType<AcceptedMessage>(await Send(h,new RenameFleetCommand(train.Id,train.Revision,"  Raicho  ")));
        Assert.Equal("Raicho",h.Snapshot().Mixed!.Fleet!.Trains[0].Name);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new RenameFleetCommand(train.Id,train.Revision,"overwrite"))).Code);
        Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await Send(h,new RenameFleetCommand(train.Id,train.Revision+1,"bad\nname"))).Code);
        var s=h.Snapshot();var sale=new FleetTradeQuery(s.WorldEpoch,s.Revision,s.Tick,s.ContentVersion,"sell",null,0,0,train.Id);
        var quote=h.PreviewFleetTrade(sale);Assert.Equal(45_000_000,quote.Price);
        Assert.IsType<AcceptedMessage>(await Send(h,new TradeFleetCommand(sale,quote.Price)));
        var after=h.Snapshot();Assert.Single(after.Mixed!.Fleet!.Trains);Assert.Equal(45_000_000,after.Mixed.Accounts.Transactions[^1].Revenue);
        Assert.Equal(s.Mixed!.NextEntityId,after.Mixed.NextEntityId);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeFleetCommand(sale,quote.Price))).Code);
    }
    [Fact]
    public async Task Browser_sale_boundary_rejects_placed_train_without_changing_callbacks_or_accounts()
    {
        var saved=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/fleet-v9.json"));
        var loaded=SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        using var h=new SessionHost(loaded,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var s=h.Snapshot();var train=s.Mixed!.Fleet!.Trains.First(t=>t.State!="unplaced");
        var q=new FleetTradeQuery(s.WorldEpoch,s.Revision,s.Tick,s.ContentVersion,"sell",null,0,0,train.Id);
        var before=await Save(h);Assert.Throws<ArgumentException>(()=>h.PreviewFleetTrade(q));
        Assert.Equal("invalidInput",Assert.IsType<RejectedMessage>(await Send(h,new TradeFleetCommand(q,0))).Code);
        Assert.Equal(before,await Save(h));
    }

    static FleetDeploymentQuery Deployment(SessionHost h,string action="place",bool reverse=false)
    {
        var s=h.Snapshot();return new(s.WorldEpoch,s.Revision,s.Tick,s.ContentVersion,s.Mixed!.Fleet!.Trains[0].Id,action,
            action=="place"?new PositionRead(14,10,1):null,reverse);
    }
    static async Task BuyAndBuild(SessionHost h)
    {
        Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(new(h.WorldEpoch,0,TerrainProfile.ContentVersion,new(12,10,1),new(20,10,1),"lay","source"))));
        var q=Query(h,3,1);Assert.IsType<AcceptedMessage>(await Send(h,new TradeFleetCommand(q,h.PreviewFleetTrade(q).Price)));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Deployment_commands_publish_exact_previews_withdraw_without_sale_and_continue_after_reopen(bool reverse)
    {
        using var h=Host();await BuyAndBuild(h);var before=await Save(h);var q=Deployment(h,reverse:reverse);
        var preview=h.PreviewFleetDeployment(q);Assert.True(preview.Legal);Assert.Equal(3,preview.Cars.Length);
        Assert.Equal(reverse,preview.IsReversed);Assert.Equal(before,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new DeployFleetCommand(q)));
        var s=h.Snapshot();var placed=s.Mixed!.Fleet!.Trains[0];Assert.Equal(reverse,placed.IsReversed);
        Assert.Equal(preview.Cars.Select(c=>(c.CarId,c.Location,c.Heading)),placed.Cars.Select(c=>(c.Id,c.State.Location!,c.State.Heading!.Value)));
        var bytes=await Save(h);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new DeployFleetCommand(q))).Code);
        Assert.Equal(bytes,await Save(h));
        using var reopened=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        // Actual production callback continues identically through the current codec.
        for(var i=0;i<4;i++){Assert.IsType<AcceptedMessage>(await Send(h,new StepCommand()));Assert.IsType<AcceptedMessage>(await Send(reopened,new StepCommand()));}
        Assert.Equal(await Save(h),await Save(reopened));
        var accounts=h.Snapshot().Mixed!.Accounts;var withdraw=Deployment(h,"withdraw");Assert.Empty(h.PreviewFleetDeployment(withdraw).Cars);
        Assert.IsType<AcceptedMessage>(await Send(h,new DeployFleetCommand(withdraw)));
        s=h.Snapshot();Assert.Equal("unplaced",s.Mixed!.Fleet!.Trains[0].State);
        Assert.All(s.Mixed.Voxels,v=>Assert.Null(v.Traffic?.Car));Assert.Empty(s.Mixed.Queue!.Pending);
        Assert.Equal(accounts.InitialCash,s.Mixed.Accounts.InitialCash);Assert.Equal(accounts.Transactions,s.Mixed.Accounts.Transactions);Assert.Equal(placed.Id,s.Mixed.Fleet.Trains[0].Id);
        Assert.IsType<AcceptedMessage>(await Send(h,new DeployFleetCommand(Deployment(h,reverse:!reverse))));
    }
    [Fact]
    public async Task Deployment_fences_peer_edits_clock_replacement_and_observer_without_partial_occupancy()
    {
        using var h=Host();await BuyAndBuild(h);var q=Deployment(h);var original=await Save(h);
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new DeployFleetCommand(q),observer)).Code);
        var bad=q with{Tail=new(14,11,1)};Assert.Equal("missingRail",h.PreviewFleetDeployment(bad).Reason);
        Assert.Equal("invalidPlacement",Assert.IsType<RejectedMessage>(await Send(h,new DeployFleetCommand(bad))).Code);
        Assert.Equal(original,await Save(h));
        await Send(h,new StepCommand());Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new DeployFleetCommand(q))).Code);
        q=Deployment(h);var train=h.Snapshot().Mixed!.Fleet!.Trains[0];await Send(h,new RenameFleetCommand(train.Id,train.Revision,"Peer edit"));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new DeployFleetCommand(q))).Code);
        q=Deployment(h);var replacement=h.LoadSave(original);h.Pump(0);await replacement;
        Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await Send(h,new DeployFleetCommand(q))).Code);
        Assert.All(h.Snapshot().Mixed!.Voxels,v=>Assert.Null(v.Traffic?.Car));
    }
}
