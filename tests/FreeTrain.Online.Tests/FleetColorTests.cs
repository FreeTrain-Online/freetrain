using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    static FleetColorTestRead ColorConfiguration(long revision=1,int template=1) => new(revision,
        new("exav-stable-bundled",$"{{243D9F53-67FB-432E-B985-3B9B696D8CE0}}-{template}",$"{{243D9F53-67FB-432E-B985-3B9B696D8CE0}}-{template}"),
        "#123456","#fedcba","#0080ff","#808080");

    [Fact]
    public async Task Color_override_is_world_local_saved_and_survives_movement_reversal_trading_groups_and_controller_edits()
    {
        using var host=FleetHost();using var other=FleetHost();var before=host.Snapshot();
        var config=ColorConfiguration();
        Assert.IsType<AcceptedMessage>(await FleetSend(host,new ConfigureFleetColorCommand(0,config)));
        Assert.Null(other.Snapshot().Mixed!.Fleet!.ColorTest);
        var after=host.Snapshot();Assert.Equal(config,after.Mixed!.Fleet!.ColorTest);
        Assert.Equal(before.Tick,after.Tick);Assert.Equal(before.Cash,after.Cash);Assert.Equal(before.Mixed!.Voxels,after.Mixed.Voxels);
        Assert.Equal(before.Mixed.Queue!.Pending,after.Mixed.Queue!.Pending);Assert.Equal(before.Mixed.Queue.NextOrder,after.Mixed.Queue.NextOrder);
        var bytes=await FleetSave(host);
        using var reopened=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(bytes,await FleetSave(reopened));
        var train=host.Snapshot().Mixed!.Fleet!.Trains[0];
        Assert.IsType<AcceptedMessage>(await FleetSend(host,new RenameFleetCommand(train.Id,train.Revision,"Visible name")));
        Assert.IsType<AcceptedMessage>(await FleetSend(host,ManagerCreate()));
        await FleetStep(host,12);Assert.Equal(config,host.Snapshot().Mixed!.Fleet!.ColorTest);
        var state=SaveCodec.ReadWorld(await FleetSave(host),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        var id=state.Fleet!.Trains[0].Id;
        state=FleetDeployment.Commit(state,FleetDeployment.PreviewReversal(state,id));
        state=FleetDeployment.Commit(state,FleetDeployment.PreviewWithdrawal(state,id));
        state=FleetTrading.Commit(state,FleetTrading.QuoteSale(state,id));
        var stock=RollingStockCatalog.Bundled.Trains.First();
        state=FleetTrading.Commit(state,FleetTrading.QuotePurchase(state,stock.Binding,stock.PermittedLengths[0],1));
        Assert.Equal(config,FleetWire.Read(state.Fleet)!.ColorTest);
    }

    [Fact]
    public async Task Color_override_rejects_observer_stale_invalid_templates_and_palettes_without_mutation()
    {
        using var h=FleetHost();var command=new ConfigureFleetColorCommand(0,ColorConfiguration());
        var before=await FleetSave(h);
        Assert.IsType<RejectedMessage>(await FleetSend(h,command,new("viewer",ParticipantRole.Observer,"viewer-connection")));
        foreach(var invalid in new[]{command.Configuration with {Template=new("other","x","x")},command.Configuration with {Body="#12345g"},command.Configuration with {Body="#123456\n"},command.Configuration with {Line1=null!},command.Configuration with {Revision=9}})
            Assert.IsType<RejectedMessage>(await FleetSend(h,command with {Configuration=invalid}));
        Assert.Equal(before,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,command));var committed=await FleetSave(h);
        ManagerRejected(await FleetSend(h,command),"staleRevision");Assert.Equal(committed,await FleetSave(h));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new ConfigureFleetColorCommand(1,ColorConfiguration(2,7))));
        Assert.Equal(ColorConfiguration(2,7),h.Snapshot().Mixed!.Fleet!.ColorTest);
    }

    [Fact]
    public void Color_test_two_purchased_deployed_trains_retain_real_zero_capacity_and_shared_override()
    {
        var state=OperationsLine(quantity:1);var stock=RollingStockCatalog.Bundled.Trains.Single(t=>t.Binding.ContributionId=="{719F0922-EA74-409C-B829-C4950201C226}");
        state=FleetTrading.Commit(state,FleetTrading.QuotePurchase(state,stock.Binding,1,2));
        state=Place(state,1,new(20,20,2));state=Place(state,2,new(25,20,2));
        var w=state.World;var f=state.Fleet!;
        var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains,f.Groups,f.Controllers,FleetWire.Color(ColorConfiguration()));
        state=state.Successor(state.Revision,new(w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.SpeedLimits,graph),state.Construction.Accounts),false);
        Assert.All(state.Fleet!.Trains.Skip(1),t=>Assert.Equal(0,f.Profile.Resolve(t.Definition).Quote(t.Cars.Length,1).Formation.NormalCapacity));
        OperationsBoundary(state);
        if(Environment.GetEnvironmentVariable("FREETRAIN_COLOR_FLEET_OUTPUT") is {} path)File.WriteAllBytes(path,SaveCodec.Write(state));
        Assert.Equal(SaveCodec.Write(state),SaveCodec.Write(Restore(SaveCodec.Write(state))));
    }
    [Fact]
    public async Task Color_override_can_initialize_before_stock_and_deduplicates_the_shared_command_receipt()
    {
        var empty=Empty();var state=Session(MixedWorld.FromTerrain(empty.Profile,empty.Terrain));
        using var h=new SessionHost(state,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var message=new CommandMessage("same-color-command",h.WorldEpoch,new ConfigureFleetColorCommand(0,ColorConfiguration()));
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var peer=network.Connect(FleetNow,FleetOwner)!;
        var first=network.Submit(peer,message,FleetNow);Assert.Same(first,network.Submit(peer,message,FleetNow));network.Tick(FleetNow);Assert.IsType<AcceptedMessage>(await first);
        var bytes=await FleetSave(h);
        var retry=network.Submit(peer,message,FleetNow);network.Tick(FleetNow);Assert.Equal(await first,await retry);Assert.Equal(bytes,await FleetSave(h));
        Assert.Empty(h.Snapshot().Mixed!.Fleet!.Trains);Assert.Equal(ColorConfiguration(),h.Snapshot().Mixed!.Fleet!.ColorTest);
        var stale=network.Submit(peer,new("stale-color-epoch",h.WorldEpoch+1,new ConfigureFleetColorCommand(1,ColorConfiguration(2))),FleetNow);network.Tick(FleetNow);ManagerRejected(await stale,"staleEpoch");
        var f=h.Snapshot().Mixed!.Fleet!;
        Assert.Throws<ArgumentException>(()=>FleetWire.Restore(f with {ColorTest=ColorConfiguration() with {Body="red"}},RollingStockCatalog.Bundled.FleetProfile));
    }

    [Fact]
    public void Information_follow_fixture_reaches_outside_zero_then_returns_with_source_head_identity()
    {
        var state=BorderRun();AdvanceTo(state,302);
        Assert.IsType<FleetCarState.Outside>(state.Fleet!.Trains[0].Head.State);
        var id=state.Fleet.Trains[0].Id;var head=state.Fleet.Trains[0].Head.Id;
        if(Environment.GetEnvironmentVariable("FREETRAIN_INFORMATION_OUTSIDE_OUTPUT") is {} path)File.WriteAllBytes(path,SaveCodec.Write(state));
        AdvanceTo(state,304);Assert.Equal(id,state.Fleet!.Trains[0].Id);Assert.Equal(head,state.Fleet.Trains[0].Head.Id);
        Assert.IsType<FleetCarState.Inside>(state.Fleet.Trains[0].Head.State);OperationsBoundary(state);
    }

}
