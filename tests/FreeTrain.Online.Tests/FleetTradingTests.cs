using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class FleetTradingTests
{
    static StockTrain Stock => RollingStockCatalog.Bundled.Trains.Single(t=>t.Binding.ContributionId=="{F3604C61-D0D5-4c9c-ADD6-405610FA6250}");
    static TrainCallbackResult Wait(MixedSessionState s,long id) => new(s.Construction,2);
    static MixedSessionState Session(MixedWorld? world=null, ConstructionAccounts? accounts=null, long minute=GameTime.StartMinute)
    {
        var w=world??GarageConstructionTests.World().EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
        var provenance=TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0));
        return new(new(w,accounts),provenance,clock:new(minute,0,[]),revision:w.Revision,trainCallback:Wait);
    }
    static MixedSessionState Buy(MixedSessionState s,int length=1,int quantity=1) =>
        FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,length,quantity));
    static MixedSessionState Decode(byte[] bytes) => SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve,Wait).Mixed!;

    [Fact]
    public void Batch_purchase_quotes_actual_capacity_and_commits_one_charge_with_shared_time_and_ordered_ids()
    {
        var s=Session(accounts:new(1)); var original=SaveCodec.Write(s);
        var p=FleetTrading.QuotePurchase(s,Stock.Binding,5,3);
        Assert.False(p.IsSale); Assert.Equal(450_000_000,p.Price); Assert.Equal(15,p.Purchase!.CarCount);
        Assert.Equal(1980,p.NormalCapacity); Assert.Equal(990,p.SeatedCapacity); Assert.Equal(2970,p.PackedCapacity);
        Assert.Equal(original,SaveCodec.Write(s)); // quoting and cancelling consume nothing
        var after=FleetTrading.Commit(s,p);
        Assert.Equal(3,after.Fleet!.Trains.Length);Assert.Equal(22,after.World.Spatial.NextEntityId);
        Assert.Empty(after.World.Spatial.Entities);Assert.Empty(after.Clock.Capture().Pending);
        Assert.Equal(new long[]{4,10,16},p.TrainIds);
        Assert.Equal(p.TrainIds,after.Fleet.Groups.Single(g=>g.Id==after.Fleet.RootGroupId).Members);
        Assert.All(after.Fleet.Trains,t=>{
            Assert.Equal(GameTime.StartMinute,t.PurchaseMinute);Assert.Equal(after.Fleet.Simple.Id,t.ControllerId);
            Assert.Equal(TrainOperationalState.Unplaced,t.State);Assert.False(t.IsReversed);
            Assert.Equal(new[]{0,1,2,3,4},t.Cars.Select(c=>c.OriginalIndex));
            Assert.All(t.Cars,c=>Assert.IsType<FleetCarState.Unplaced>(c.State));
        });
        var entry=Assert.Single(after.Construction.Accounts.Transactions);
        Assert.Equal(AccountGenre.RailService,entry.Genre);Assert.Equal(p.Price,entry.Expenditure);Assert.Equal(0,entry.Revenue);
        Assert.Equal(1,entry.TopUps);Assert.Equal(9_550_000_001,after.Construction.Accounts.Cash);
        Assert.Equal(original,SaveCodec.Write(s));Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Decode(SaveCodec.Write(after))));
        Assert.Throws<InvalidOperationException>(()=>FleetTrading.Commit(after,p));
    }

    [Fact]
    public void Every_bundled_definition_buys_exact_original_formation_including_single_tail_and_colored_stock()
    {
        var s=Session();
        foreach(var stock in RollingStockCatalog.Bundled.Trains)
            foreach(var length in new[]{1,5})
            {
                var p=FleetTrading.QuotePurchase(s,stock.Binding,length,1);s=FleetTrading.Commit(s,p);
                var t=s.Fleet!.Train(p.TrainIds[0]);
                Assert.Equal(stock.Create(length).Cars.Select(c=>c.Reference),t.Cars.Select(c=>c.Definition));
                Assert.Equal(stock.PricePerCar*length,p.Price);
            }
        Assert.Equal(12,s.Fleet!.Trains.Length);
    }

    [Fact]
    public void Explicit_mixed_patterns_enforce_declared_lengths_and_exact_quote_totals()
    {
        var w=GarageConstructionTests.World();var pack=w.Terrain.Profile.Packs[0];
        var cars=RollingStockCatalog.Bundled.Cars.Take(2).ToArray();
        var stock=new StockTrain(new(pack.Id,pack.Version,pack.CatalogHash,"test:pattern"),new("C","T","N","A","D"),
            100,2,1,100,0,null,null,null,[ImmutableArray.Create(cars[0],cars[1],cars[0])]);
        var s=Session(w.EnableFleet(new(w.Terrain.Profile.Packs,[stock])));
        var bytes=SaveCodec.Write(s);
        Assert.Throws<ArgumentException>(()=>FleetTrading.QuotePurchase(s,stock.Binding,2,1));
        var p=FleetTrading.QuotePurchase(s,stock.Binding,3,2);var after=FleetTrading.Commit(s,p);
        Assert.Equal(600,p.Price);Assert.Equal(6,p.Purchase!.CarCount);
        Assert.All(after.Fleet!.Trains,t=>Assert.Equal(new[]{cars[0].Reference,cars[1].Reference,cars[0].Reference},t.Cars.Select(c=>c.Definition)));
        Assert.Equal(bytes,SaveCodec.Write(s));
    }

    [Theory]
    [InlineData(0,1)][InlineData(101,1)][InlineData(1,0)][InlineData(1,101)][InlineData(100,11)]
    [InlineData(int.MaxValue,int.MaxValue)]
    public void Invalid_trade_sizes_leave_ids_inventory_and_accounts_unchanged(int length,int quantity)
    {
        var s=Session();var bytes=SaveCodec.Write(s);
        Assert.Throws<ArgumentException>(()=>FleetTrading.QuotePurchase(s,Stock.Binding,length,quantity));
        Assert.Equal(bytes,SaveCodec.Write(s));
    }

    [Fact]
    public void Invalid_identity_and_missing_inventory_reject_without_promoting_other_pack_stock()
    {
        var s=Session();var bytes=SaveCodec.Write(s);
        Assert.Throws<ArgumentException>(()=>FleetTrading.QuotePurchase(s,Stock.Binding with{CatalogHash=new string('0',64)},1,1));
        Assert.Throws<ArgumentException>(()=>FleetTrading.QuoteSale(s,123));
        Assert.Equal(bytes,SaveCodec.Write(s));
    }

    [Fact]
    public void Replays_competing_quotes_clock_pause_and_world_replacement_are_stale()
    {
        var s=Session();var a=FleetTrading.QuotePurchase(s,Stock.Binding,1,1);var b=FleetTrading.QuotePurchase(s,Stock.Binding,5,1);
        var after=FleetTrading.Commit(s,a);var bytes=SaveCodec.Write(after);
        Assert.Throws<InvalidOperationException>(()=>FleetTrading.Commit(after,a));
        Assert.Throws<InvalidOperationException>(()=>FleetTrading.Commit(after,b));
        Assert.Throws<InvalidOperationException>(()=>FleetTrading.Commit(Decode(SaveCodec.Write(s)),a));
        s.AdvanceOneMinute();Assert.Throws<InvalidOperationException>(()=>FleetTrading.Commit(s,a));
        s=Session();a=FleetTrading.QuotePurchase(s,Stock.Binding,1,1);s.SetPaused(false);
        Assert.Throws<InvalidOperationException>(()=>FleetTrading.Commit(s,a));
        Assert.Equal(bytes,SaveCodec.Write(after));
        var sale=FleetTrading.QuoteSale(after,after.Fleet!.Trains[0].Id);
        var sold=FleetTrading.Commit(after,sale);Assert.Empty(sold.Fleet!.Trains);
        Assert.Throws<InvalidOperationException>(()=>FleetTrading.Commit(sold,sale));
        Assert.Throws<ArgumentException>(()=>FleetTrading.QuoteSale(sold,sale.TrainIds[0]));
    }

    [Fact]
    public void Real_inventory_admission_reaches_128_trains_1024_cars_and_sale_never_rewinds_ids()
    {
        var s=Buy(Buy(Session(),8,64),8,64);var bytes=SaveCodec.Write(s);var next=s.World.Spatial.NextEntityId;
        Assert.Equal(128,s.Fleet!.Trains.Length);Assert.Equal(1024,s.Fleet.Trains.Sum(t=>t.Cars.Length));
        Assert.Throws<ArgumentException>(()=>FleetTrading.QuotePurchase(s,Stock.Binding,1,1));
        Assert.Equal(bytes,SaveCodec.Write(s));
        s=FleetTrading.Commit(s,FleetTrading.QuoteSale(s,s.Fleet.Trains[0].Id));
        Assert.Equal(next,s.World.Spatial.NextEntityId);
        var p=FleetTrading.QuotePurchase(s,Stock.Binding,8,1);s=FleetTrading.Commit(s,p);
        Assert.Equal(next,p.TrainIds[0]);Assert.Equal(128,s.Fleet!.Trains.Length);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Decode(SaveCodec.Write(s))));
    }

    [Theory]
    [InlineData("allocator")][InlineData("cash")][InlineData("journal")][InlineData("revision")]
    public void Overflow_or_journal_exhaustion_leaves_the_entire_source_boundary_intact(string cause)
    {
        var s=Session();
        if(cause=="allocator")
        {
            var w=s.World;
            s=Session(new(w.Profile,w.Terrain,new(long.MaxValue,[]),[],[],w.Revision,fleet:w.Fleet));
        }
        if(cause=="cash")s=Session(accounts:new(long.MinValue));
        if(cause=="revision")s=new(s.Construction,s.Provenance,revision:long.MaxValue);
        if(cause=="journal")
        {
            var accounts=new ConstructionAccounts().Book(new(Enumerable.Repeat(new AccountCharge(AccountGenre.RailService,0),8192)),1,new(GameTime.StartMinute),"full");
            s=Session(accounts:accounts);
        }
        var bytes=SaveCodec.Write(s);
        if(cause=="journal")Assert.Throws<ArgumentException>(()=>FleetTrading.QuotePurchase(s,Stock.Binding,1,1));
        else Assert.Throws<OverflowException>(()=>FleetTrading.QuotePurchase(s,Stock.Binding,1,1));
        Assert.Equal(bytes,SaveCodec.Write(s));
    }

    [Fact]
    public void Independent_SCN026_literals_book_exact_resale_revenue_or_no_receipt_when_date_missing()
    {
        using var fixtures=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/train-fixtures.json")));
        foreach(var row in fixtures.RootElement.GetProperty("groups").GetProperty("sales").GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");var age=input.GetProperty("ageDays");
            var w=GarageConstructionTests.World();var pack=w.Terrain.Profile.Packs[0];
            var stock=new StockTrain(new(pack.Id,pack.Version,pack.CatalogHash,"test:SCN026"),new("C","T","N","A","D"),
                input.GetProperty("price").GetInt32(),1,1,100,0,null,RollingStockCatalog.Bundled.Cars[0],null);
            var f=FleetGraph.Empty(new(w.Terrain.Profile.Packs,[stock]),1);
            var train=new FleetTrain(4,0,"SCN026",stock.Binding,f.RootGroupId,f.Simple.Id,
                [new(5,0,0,stock.Create(1).Cars[0].Reference,new FleetCarState.Unplaced())],PurchaseMinute:age.ValueKind==JsonValueKind.Null?null:0);
            f=new(f.Profile,f.RootGroupId,[train],f.Groups.Select(g=>g with{Members=[4]}),f.Controllers);
            w=new(w.Profile,w.Terrain,new(6,[]),[],[],1,fleet:f);
            var s=Session(w,new(input.GetProperty("cash").GetInt64()),age.ValueKind==JsonValueKind.Null?0:age.GetInt64()*GameTime.Day);
            var bytes=SaveCodec.Write(s);var plan=FleetTrading.QuoteSale(s,4);var after=FleetTrading.Commit(s,plan);
            Assert.Equal(expected.GetProperty("revenue").GetInt64(),plan.Price);
            Assert.Equal(expected.GetProperty("cash").GetInt64(),after.Construction.Accounts.Cash);
            Assert.Empty(after.Fleet!.Trains);Assert.Empty(after.Fleet.Groups[0].Members);Assert.Empty(after.Clock.Capture().Pending);
            if(age.ValueKind==JsonValueKind.Null)Assert.Empty(after.Construction.Accounts.Transactions);
            else
            {
                var entry=Assert.Single(after.Construction.Accounts.Transactions);
                Assert.Equal(plan.Price,entry.Revenue);Assert.Equal(0,entry.Expenditure);Assert.Equal(AccountGenre.RailService,entry.Genre);
            }
            Assert.Equal(bytes,SaveCodec.Write(s));
        }
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Placed_sale_clears_complete_consist_and_group_but_preserves_neighbor_construction_and_shared_controller(bool outside)
    {
        var s=Buy(Buy(Session(),2),1);var first=s.Fleet!.Trains[0];var neighbor=s.Fleet.Trains[1];var f=s.Fleet;
        var entities=s.World.Spatial.Entities.ToBuilder();var voxels=s.World.Voxels.ToBuilder();var next=s.World.Spatial.NextEntityId;
        var changed=f.Trains.Select(t=>t with{Revision=1,State=TrainOperationalState.Moving,
            Cars=t.Cars.Select(c=>{
                FleetCarState state;
                if(outside&&c.Id==first.Cars[1].Id)state=new FleetCarState.Outside(new(0,0,2),new(2),50);
                else
                {
                    var p=GarageConstructionTests.Start with{X=GarageConstructionTests.Start.X+(int)(c.Id-first.Head.Id)};
                    state=new FleetCarState.Inside(p,new(2));
                    entities.Add(new(next,0,s.World.Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0));
                    var rail=new RailComponent(s.World.Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Normal&&b.Variant==4).Reference,4);
                    voxels.Add(new(p,next++,RailwayVoxelKind.Traffic,new(rail,Car:new(c.Definition,t.Id,c.Id))));
                }
                return c with{Revision=1,State=state};
            }).ToImmutableArray()}).ToArray();
        // Place the sold train in a subgroup and share its controller with a neighbor.
        var groupId=next++;var provider=f.Profile.Providers.First(p=>p.Kind==FleetControllerKind.Timetable);var controllerId=next++;
        var controller=new FleetController(controllerId,0,"Shared",FleetControllerKind.Timetable,provider.Binding);
        changed=changed.Select(t=>t with{ControllerId=controllerId,GroupId=t.Id==first.Id?groupId:f.RootGroupId}).ToArray();
        var graph=new FleetGraph(f.Profile,f.RootGroupId,changed,
            [f.Groups[0] with{Revision=f.Groups[0].Revision+1,Members=[groupId,neighbor.Id]},new(groupId,0,"Subgroup",f.RootGroupId,f.Simple.Id,[first.Id])],f.Controllers.Add(controller));
        var w=s.World.Replace(s.World.Revision,s.World.Terrain,new(next,entities.ToImmutable()),voxels,s.World.Slopes,fleet:graph);
        s=new(new(w,s.Construction.Accounts),s.Provenance,clock:new(GameTime.StartMinute,2,
            [new(0,0,GameTime.StartMinute+1,MixedSessionState.TrainHandler(first.Id),0),new(1,1,GameTime.StartMinute+1,MixedSessionState.TrainHandler(neighbor.Id),0)]),revision:w.Revision,trainCallback:Wait);
        var type=w.Profile.Bindings.First(b=>b.Stationary?.Height==1).Reference.ContributionId;
        s=StationaryConstruction.Commit(s,StationaryConstruction.Preview(s.World,new(10,10,2),type));
        var original=SaveCodec.Write(s);var queue=s.Clock.Capture();var ids=s.World.Spatial.NextEntityId;
        var plan=FleetTrading.QuoteSale(s,first.Id);Assert.Equal(original,SaveCodec.Write(s));
        var after=FleetTrading.Commit(s,plan);
        Assert.Equal(30_000_000,plan.Price);Assert.Single(after.Fleet!.Trains);
        Assert.Empty(after.Fleet.Groups.Single(g=>g.Id==groupId).Members);
        Assert.Equal(controller,after.Fleet.Controllers.Single(c=>c.Id==controllerId));
        Assert.Equal(controllerId,after.Fleet.EffectiveController(neighbor.Id).Id);
        Assert.Equal(ids,after.World.Spatial.NextEntityId);
        Assert.Equal(queue.NextOrder,after.Clock.Capture().NextOrder);
        Assert.Equal(queue.Pending.Where(a=>a.HandlerId!=MixedSessionState.TrainHandler(first.Id)),after.Clock.Capture().Pending);
        foreach(var v in s.World.Voxels.Where(v=>v.Traffic?.Car?.TrainId==first.Id))
        {
            Assert.Null(after.World.At(v.Position)!.Traffic!.Car);
            Assert.Equal(v.Traffic! with{Car=null},after.World.At(v.Position)!.Traffic);
            Assert.Equal(s.World.Spatial.Entities.Single(e=>e.Id==v.OwnerId).Revision+1,after.World.Spatial.Entities.Single(e=>e.Id==v.OwnerId).Revision);
        }
        Assert.Equal(neighbor.Id,Assert.Single(after.World.Voxels.Where(v=>v.Traffic?.Car!=null)).Traffic!.Car!.TrainId);
        var bytes=SaveCodec.Write(after);var restored=Decode(bytes);Assert.Equal(bytes,SaveCodec.Write(restored));
        after.AdvanceOneMinute();restored.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(restored));
        Assert.Equal(original,SaveCodec.Write(s));
    }

    [Fact]
    public void Revenue_round_trips_save_snapshot_delta_and_rejected_journal_rewrites_preserve_state()
    {
        var before=Buy(Session());var after=FleetTrading.Commit(before,FleetTrading.QuoteSale(before,before.Fleet!.Trains[0].Id));
        var bytes=SaveCodec.Write(after);Assert.Equal(bytes,SaveCodec.Write(Decode(bytes)));
        var snapshot=MixedWire.Snapshot(after,1);Assert.Equal(15_000_000,snapshot.Mixed!.Accounts.Transactions[^1].Revenue);
        var wire=JsonSerializer.Serialize<WireMessage>(snapshot,WireJson.CreateOptions());
        Assert.Contains("\"revenue\":\"15000000\"",wire);
        var delta=StateDifference.Between(MixedWire.Snapshot(before,1),snapshot);
        Assert.Equal(snapshot.Mixed,delta.Mixed);MixedWire.ValidateTransition(before,after);
        var doc=JsonNode.Parse(bytes)!;doc["mixed"]!["accounts"]!["transactions"]![1]!["revenue"]="15000001";
        Assert.Throws<InvalidDataException>(()=>Decode(System.Text.Encoding.UTF8.GetBytes(doc.ToJsonString())));
        Assert.Equal(bytes,SaveCodec.Write(after));
    }

    [Fact]
    public void Real_sale_of_literal_reversed_inside_outside_fleet_matches_independently_authored_delta()
    {
        var before=Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/fleet-v9.json")));
        var quote=FleetTrading.QuoteSale(before,17);Assert.Equal(50_000_000,quote.Price);
        var after=FleetTrading.Commit(before,quote);
        var actual=StateDifference.Between(MixedWire.Snapshot(before,1),MixedWire.Snapshot(after,1));
        var expected=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/delta-fleet-sale.json"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected),JsonNode.Parse(JsonSerializer.Serialize<WireMessage>(actual,WireJson.CreateOptions()))));
        Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Decode(SaveCodec.Write(after))));
    }

    [Fact]
    public void Revenue_overflow_malformed_records_zero_receipts_and_negative_cash_keep_source_accounting_rules()
    {
        var accounts=new ConstructionAccounts(long.MaxValue);Assert.Throws<OverflowException>(()=>accounts.Earn(1,1,new(0),"overflow"));
        Assert.Empty(accounts.Transactions);Assert.Equal(long.MaxValue,accounts.Cash);
        var earned=new ConstructionAccounts(-20,new(100)).Earn(5,1,new(0),"earn");
        Assert.Equal(85,earned.Cash);Assert.Equal(1,earned.TopUps);Assert.Equal(5,earned.Transactions[0].Revenue);
        Assert.Equal(earned.Cash,ConstructionAccounts.Restore(earned.Capture()).Cash);
        foreach(var entry in new[]{earned.Transactions[0] with{Revenue=-1},earned.Transactions[0] with{Expenditure=1},earned.Transactions[0] with{TopUps=0}})
            Assert.Throws<ArgumentException>(()=>ConstructionAccounts.Restore(earned.Capture() with{Transactions=[entry]}));
        Assert.Single(new ConstructionAccounts().Earn(0,1,new(0),"zero").Transactions);
        var s=Buy(Session());s=Session(s.World,new(long.MaxValue));var bytes=SaveCodec.Write(s);
        Assert.Throws<OverflowException>(()=>FleetTrading.QuoteSale(s,s.Fleet!.Trains[0].Id));Assert.Equal(bytes,SaveCodec.Write(s));
    }
}
