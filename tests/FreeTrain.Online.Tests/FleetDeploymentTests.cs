using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class FleetDeploymentTests
{
    static Location P => new(24,24,2);
    static StockTrain Stock => RollingStockCatalog.Bundled.Trains.First(t=>t.MinutesPerVoxel==2&&t.PricePerCar==30_000_000);
    static TrainCallbackResult Wait(MixedSessionState s,long id)=>new(s.Construction,2);
    static MixedWorld Empty(int size=64)
    {
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Deployment",new(size,size,8),0,
            Enumerable.Repeat(new TerrainCell(2,TerrainSurface.Cut),size*size),new(1,[]),[]);
        return MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain).EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
    }
    static MixedSessionState Session(MixedWorld w,TrainCallback? dispatcher=null,ClockState? clock=null,long? revision=null)
    {
        var provenance=TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,
            new(w.Terrain.Space.HorizontalSize,w.Terrain.Space.VerticalSize,8),0));
        return new(new(w),provenance,clock:clock,revision:revision??w.Revision,trainCallback:dispatcher??Wait);
    }
    static MixedWorld Rail(MixedWorld w,Location p,int pattern=4,RailwayHandler? special=null)
    {
        var handler=special??(pattern<12?RailwayHandler.Normal:RailwayHandler.Junction);
        var binding=w.Profile.Bindings.Single(b=>b.Handler==handler&&(special!=null||b.Variant==pattern));
        var id=w.Spatial.NextEntityId;
        var e=new SpatialEntity(id,0,w.Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0);
        return new(w.Profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(e)),w.Voxels.Add(new(p,id,RailwayVoxelKind.Traffic,
            new(new(binding.Reference,pattern)))),w.Slopes,w.Revision,fleet:w.Fleet);
    }
    static MixedSessionState Buy(MixedWorld w,int length=3,int quantity=1,StockTrain? stock=null)
    {
        var s=Session(w);return FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,(stock??Stock).Binding,length,quantity));
    }
    static MixedSessionState Place(MixedSessionState s,long id,Location p,bool reverse=false)=>
        FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,id,p,reverse));
    static MixedSessionState Decode(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve,Wait).Mixed!;
    static Location Point(JsonElement a)=>new(a[0].GetInt32(),a[1].GetInt32(),a[2].GetInt32());
    static MixedSessionState Change(MixedSessionState s,FleetTrain t)
    {
        var w=s.World;var f=s.Fleet!;var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(a=>a.Id==t.Id?t:a),f.Groups,f.Controllers);
        var world=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.SpeedLimits,graph);
        return s.Successor(s.Revision,new(world,s.Construction.Accounts),false);
    }

    [Fact]
    public void Independent_48_poses_cover_eight_headings_both_original_orientations_and_short_long_formations()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/train-placement-fixtures.json")));
        var count=0;var headings=new HashSet<int>();
        foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");var length=input.GetProperty("length").GetInt32();
            var w=Empty();var poses=expected.GetProperty("cars").EnumerateArray().ToArray();
            foreach(var pose in poses)w=Rail(w,Point(pose.GetProperty("location")),input.GetProperty("pattern").GetInt32());
            var s=Buy(w,length);var t=s.Fleet!.Trains[0];
            if(input.GetProperty("initialReversed").GetBoolean())
                s=Change(s,t with{Revision=t.Revision+1,IsReversed=true,Cars=t.Cars.Reverse().ToImmutableArray()});
            var bytes=SaveCodec.Write(s);var plan=FleetDeployment.PreviewPlacement(s,t.Id,Point(input.GetProperty("tail")),input.GetProperty("reverse").GetBoolean());
            Assert.Equal(bytes,SaveCodec.Write(s)); // discarded preview/cancellation is exact
            var after=FleetDeployment.Commit(s,plan);t=after.Fleet!.Train(t.Id);
            Assert.Equal(expected.GetProperty("isReversed").GetBoolean(),t.IsReversed);
            for(var n=0;n<length;n++)
            {
                var car=t.Cars[n];var state=Assert.IsType<FleetCarState.Inside>(car.State);var pose=poses[n];
                Assert.Equal(pose.GetProperty("originalIndex").GetInt32(),car.OriginalIndex);
                Assert.Equal(Point(pose.GetProperty("location")),state.Location);Assert.Equal(pose.GetProperty("heading").GetInt32(),state.Heading.Index);
                headings.Add(state.Heading.Index);Assert.Equal(car.Id,after.World.At(state.Location)!.Traffic!.Car!.CarId);
            }
            Assert.Equal(s.World.Spatial.NextEntityId,after.World.Spatial.NextEntityId);Assert.Same(s.Construction.Accounts,after.Construction.Accounts);
            var action=Assert.Single(after.Clock.Capture().Pending);Assert.Equal(s.Clock.Time.AbsoluteMinute+2,action.DueMinute);
            Assert.Equal(s.Clock.NextOrder,action.Order);Assert.Equal(0,action.Interval);
            Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Decode(SaveCodec.Write(after))));
            var withdrawn=FleetDeployment.Commit(after,FleetDeployment.PreviewWithdrawal(after,t.Id));
            Assert.Empty(withdrawn.Clock.Capture().Pending);Assert.All(withdrawn.World.Voxels,v=>Assert.Null(v.Traffic!.Car));
            Assert.Equal(t.IsReversed,withdrawn.Fleet!.Train(t.Id).IsReversed);count++;
        }
        Assert.Equal(48,count);Assert.Equal(Enumerable.Range(0,8),headings.Order());
    }

    [Fact]
    public void P502_literal_guides_use_exact_normal_and_junction_head_and_predecessor_decisions()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/train-fixtures.json")));
        foreach(var row in doc.RootElement.GetProperty("groups").GetProperty("guides").GetProperty("cases").EnumerateArray())
        {
            var i=row.GetProperty("input");var pattern=i.GetProperty("pattern").GetInt32();
            var previous=i.TryGetProperty("previous",out var p)&&p.ValueKind!=JsonValueKind.Null?(Location?)Point(p):null;
            var location=i.TryGetProperty("location",out var l)?(Location?)Point(l):null;
            var d=FleetRailGuidance.Guide(pattern<12?RailwayHandler.Normal:RailwayHandler.Junction,pattern,new(i.GetProperty("incoming").GetInt32()),
                i.TryGetProperty("controller",out var c)&&c.GetString()=="curve"?FleetJunctionRoute.Curve:FleetJunctionRoute.Straight,previous,location);
            Assert.Equal(row.GetProperty("expected").GetProperty("outgoing").GetInt32(),d.Index);
        }
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Curve_dir1_heading_and_reversal_use_guide_opposite_not_stored_heading_opposite(bool reverse)
    {
        // Tail curve [0,5], source d=0 (no opposite4); then straight N.
        var w=Rail(Rail(Empty(),P,11),P.Move(new(0)),10);var s=Buy(w,2);var id=s.Fleet!.Trains[0].Id;
        // Pattern11 is [0,5]; retain source dir1 even though reciprocal entry is absent.
        var plan=FleetDeployment.PreviewPlacement(s,id,P,reverse);var after=FleetDeployment.Commit(s,plan);
        var t=after.Fleet!.Train(id);Assert.Equal(reverse,t.IsReversed);
        Assert.Equal(reverse?P:P.Move(new(0)),((FleetCarState.Inside)t.Head.State).Location);
        Assert.All(t.Cars,c=>Assert.Equal(reverse?4:0,((FleetCarState.Inside)c.State).Heading.Index));
        Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Decode(SaveCodec.Write(after))));
        // A real bend: tail incoming E at the next curve [1,6] exits NE.
        w=Rail(Rail(Rail(Empty(),P),P.Move(new(2)),2),P.Move(new(2)).Move(new(1)),1);
        s=Buy(w);after=Place(s,s.Fleet!.Trains[0].Id,P,true);t=after.Fleet!.Trains[0];
        Assert.Equal(new[]{6,5,5},t.Cars.Select(c=>((FleetCarState.Inside)c.State).Heading.Index));
    }

    [Fact]
    public void Temporary_head_chooses_simple_straight_at_junction_and_never_installs_partial_occupancy()
    {
        var w=Rail(Rail(Rail(Empty(),P,19),P.Move(new(0)),19),P.Move(new(0)).Move(new(0)),10);
        var s=Buy(w);var bytes=SaveCodec.Write(s);var id=s.Fleet!.Trains[0].Id;
        var after=Place(s,id,P,true);Assert.Equal(bytes,SaveCodec.Write(s));
        Assert.All(after.Fleet!.Train(id).Cars,c=>Assert.Equal(4,((FleetCarState.Inside)c.State).Heading.Index));
        Assert.Equal(3,after.World.Voxels.Count(v=>v.Traffic!.Car!=null));
    }

    [Theory]
    [InlineData("missing")][InlineData("occupied")][InlineData("duplicate")][InlineData("heading")][InlineData("border")]
    public void Missing_occupied_loop_invalid_heading_and_stored_border_reject_without_mutation(string failure)
    {
        var w=Empty();var tail=P;
        if(failure=="duplicate")
        {
            // N -> NE -> E -> SE -> S -> SW -> W -> NW -> N closes after eight cells.
            var p=P;foreach(var pattern in new[]{8,0,3,6,9,11,2,5}){w=Rail(w,p,pattern);var d=new[]{0,1,2,3,4,5,6,7}[w.Voxels.Length-1];p=p.Move(new(d));}
        }
        else
        {
            if(failure=="border")tail=w.Terrain.Space.ToLocation(new(0,0,2));
            w=Rail(w,tail,10);
            if(failure!="missing"&&failure!="border")w=Rail(w,tail.Move(new(0)),failure=="heading"?4:10);
        }
        var s=Buy(w,failure=="duplicate"?9:2,failure=="occupied"?2:1);
        if(failure=="occupied")s=Place(s,s.Fleet!.Trains[1].Id,tail);
        var bytes=SaveCodec.Write(s);var reason=failure switch{"missing"=>"missingRail","occupied"=>"carOccupied","duplicate"=>"duplicateCell","heading"=>"invalidPlacementHeading",_=>"outside"};
        Assert.Equal(reason,Assert.Throws<InvalidOperationException>(()=>FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[0].Id,tail)).Message);
        Assert.Equal(bytes,SaveCodec.Write(s));
    }

    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Actual_slopes_keep_fixed_level_deployment_and_reject_missing_height_transition_atomically(int direction)
    {
        var d=new Heading(direction);var w=Empty();var b=P;
        var build=SlopeRailwayConstruction.PreviewBuild(w,b,d);Assert.True(build.Legal,build.Reason);
        w=SlopeRailwayConstruction.Apply(new(w),build,new(GameTime.StartMinute)).World;
        // rr.dir1 heads N/E, so choose low base for N/E slopes and high end for S/W slopes.
        var tail=direction<4?b:b.Move(d).Move(d).Move(d) with{Z=3};
        var s=Buy(w,2);var after=Place(s,s.Fleet!.Trains[0].Id,tail);
        Assert.All(after.Fleet!.Trains[0].Cars,c=>Assert.Equal(tail.Z,((FleetCarState.Inside)c.State).Location.Z));
        // A length-three source walk reaches a filler at fixed Z rather than climbing.
        s=Buy(w,3);var bytes=SaveCodec.Write(s);
        Assert.Equal("missingRail",Assert.Throws<InvalidOperationException>(()=>FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[0].Id,tail)).Message);
        Assert.Equal(bytes,SaveCodec.Write(s));
        Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Decode(SaveCodec.Write(after))));
    }

    [Fact]
    public void Withdrawal_retains_owned_stock_history_and_other_callbacks_and_replacement_resets_only_stop_counter()
    {
        var w=Empty();for(var i=0;i<4;i++)w=Rail(w,new(P.X+i,P.Y,2));
        var s=Buy(w,2,2);var a=s.Fleet!.Trains[0].Id;var b=s.Fleet.Trains[1].Id;
        s=Place(s,a,P);s=Place(s,b,new(P.X+2,P.Y,2));
        var site=s.World.Profile.Bindings.First(binding=>binding.Stationary?.Height==1).Reference.ContributionId;
        s=StationaryConstruction.Commit(s,StationaryConstruction.Preview(s.World,new(30,30,2),site));
        var t=s.Fleet!.Train(a);s=Change(s,t with{Revision=t.Revision+1,MoveCount=17,StopCallCount=3,Forwarding=true,Passengers=39,
            BoardingOrigin=new FleetCarState.Inside(P,new(2))});t=s.Fleet!.Train(a);
        var old=SaveCodec.Write(s);var neighbor=s.Clock.Capture().Pending.Single(e=>e.HandlerId==MixedSessionState.TrainHandler(b));
        var plan=FleetDeployment.PreviewWithdrawal(s,a);Assert.Equal(old,SaveCodec.Write(s));
        var after=FleetDeployment.Commit(s,plan);var withdrawn=after.Fleet!.Train(a);
        Assert.Equal(neighbor,after.Clock.Capture().Pending.Single(e=>e.HandlerId==MixedSessionState.TrainHandler(b)));
        Assert.Equal(s.Clock.Capture().Pending.Where(e=>e.HandlerId!=MixedSessionState.TrainHandler(a)),after.Clock.Capture().Pending);
        Assert.Equal(s.Clock.NextOrder,after.Clock.NextOrder);
        Assert.Equal(t.MoveCount,withdrawn.MoveCount);Assert.Equal(t.StopCallCount,withdrawn.StopCallCount);Assert.Equal(t.Passengers,withdrawn.Passengers);
        Assert.Equal(t.BoardingOrigin,withdrawn.BoardingOrigin);Assert.Equal(t.Forwarding,withdrawn.Forwarding);
        Assert.Equal(s.Fleet.Groups.ToArray(),after.Fleet.Groups.ToArray());Assert.Equal(s.Fleet.Controllers.ToArray(),after.Fleet.Controllers.ToArray());
        Assert.Same(s.Construction.Accounts,after.Construction.Accounts);Assert.Equal(s.World.Spatial.NextEntityId,after.World.Spatial.NextEntityId);
        var placed=Place(after,a,P);Assert.Equal(0,placed.Fleet!.Train(a).StopCallCount);Assert.Equal(17,placed.Fleet.Train(a).MoveCount);
        Assert.Equal(39,placed.Fleet.Train(a).Passengers);
        var restored=Decode(SaveCodec.Write(placed));placed.AdvanceOneMinute();restored.AdvanceOneMinute();
        placed.AdvanceOneMinute();restored.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(placed),SaveCodec.Write(restored));
    }

    [Fact]
    public void Outside_withdrawal_cancels_callback_without_occupancy_sale_or_inventory_deletion()
    {
        var s=Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/fleet-v10.json")));
        var before=s.Fleet!.Train(17);var after=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,17));
        Assert.All(after.Fleet!.Train(17).Cars,c=>Assert.IsType<FleetCarState.Unplaced>(c.State));
        Assert.Empty(after.Clock.Capture().Pending);Assert.Null(after.World.Voxels[0].Traffic!.Car);
        Assert.Equal(before.Passengers,after.Fleet.Train(17).Passengers);Assert.Equal(before.IsReversed,after.Fleet.Train(17).IsReversed);
        Assert.Equal(s.Fleet.Trains.Length,after.Fleet.Trains.Length);Assert.Same(s.Construction.Accounts,after.Construction.Accounts);
        Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Decode(SaveCodec.Write(after))));
    }

    [Theory]
    [InlineData("time")][InlineData("pause")][InlineData("queue")][InlineData("replacement")][InlineData("competing")]
    public void Stale_replacement_replay_pause_time_and_queue_cannot_publish_a_plan(string cause)
    {
        var s=Buy(Rail(Rail(Empty(),P),P.Move(new(2))),1,2);var id=s.Fleet!.Trains[0].Id;
        var other=s.Fleet.Trains[1].Id;
        if(cause=="queue")s=Place(s,other,P.Move(new(2)));
        var plan=FleetDeployment.PreviewPlacement(s,id,P);
        if(cause=="time")s.AdvanceOneMinute();
        if(cause=="pause")s.SetPaused(false);
        if(cause=="queue") {s.Clock.UnregisterOneShots(MixedSessionState.TrainHandler(other));s.Clock.Schedule(MixedSessionState.TrainHandler(other),2);}
        if(cause=="replacement")s=Decode(SaveCodec.Write(s));
        if(cause=="competing")s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,1,1));
        var bytes=SaveCodec.Write(s);Assert.Throws<InvalidOperationException>(()=>FleetDeployment.Commit(s,plan));Assert.Equal(bytes,SaveCodec.Write(s));
    }

    [Fact]
    public void Replays_and_duplicate_operations_reject_and_default_dispatcher_uses_native_first_delay()
    {
        var w=Rail(Empty(),P);var s=Buy(w,1);var id=s.Fleet!.Trains[0].Id;
        Assert.Throws<InvalidOperationException>(()=>FleetDeployment.PreviewWithdrawal(s,id));
        var plan=FleetDeployment.PreviewPlacement(s,id,P);var after=FleetDeployment.Commit(s,plan);
        Assert.Throws<InvalidOperationException>(()=>FleetDeployment.Commit(after,plan));
        Assert.Throws<InvalidOperationException>(()=>FleetDeployment.PreviewPlacement(after,id,P));
        var withdrawal=FleetDeployment.PreviewWithdrawal(after,id);var withdrawn=FleetDeployment.Commit(after,withdrawal);
        Assert.Throws<InvalidOperationException>(()=>FleetDeployment.Commit(withdrawn,withdrawal));
        var production=new MixedSessionState(s.Construction,s.Provenance,revision:s.Revision);
        Assert.Equal((TrainCallback)FleetMovement.Tick,production.TrainDispatcher);
        Assert.NotNull(FleetDeployment.PreviewPlacement(production,id,P));
        w=s.World;var limited=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,new([P],[],[]),w.Fleet);
        s=Session(limited);after=Place(s,id,P);Assert.Equal(2,Assert.Single(after.Clock.Capture().Pending).DueMinute-s.Clock.Time.AbsoluteMinute);
    }

    [Fact]
    public void Real_deployment_and_withdrawal_match_independent_complete_deltas_with_unchanged_tunnel_backup_and_neighbors()
    {
        var s=Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/fleet-v10.json")));
        foreach(var action in new[]{"placement","withdrawal"})
        {
            var after=action=="placement"?Place(s,20,new(3,2,2)):
                FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,20));
            var actual=StateDifference.Between(MixedWire.Snapshot(s,1),MixedWire.Snapshot(after,1));
            var expected=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures/delta-fleet-"+action+".json"));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected),JsonNode.Parse(JsonSerializer.Serialize<WireMessage>(actual,WireJson.CreateOptions()))),action);
            MixedWire.ValidateTransition(s,after);
            Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Decode(SaveCodec.Write(after))));s=after;
        }
    }

    [Theory]
    [InlineData("order")][InlineData("due")][InlineData("session")][InlineData("train")][InlineData("car")][InlineData("owner")]
    public void Exhausted_numeric_boundaries_leave_inventory_accounts_ids_occupancy_and_queue_intact(string boundary)
    {
        var s=Buy(Rail(Empty(),P),1);var w=s.World;var t=s.Fleet!.Trains[0];
        if(boundary is "train" or "car")
        {
            var changed=t with{Revision=boundary=="train"?long.MaxValue:t.Revision,
                Cars=boundary=="car"?t.Cars.Select(c=>c with{Revision=long.MaxValue}).ToImmutableArray():t.Cars};
            var f=s.Fleet!;var graph=new FleetGraph(f.Profile,f.RootGroupId,[changed],f.Groups,f.Controllers);
            s=Session(new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,fleet:graph));
        }
        if(boundary=="owner")
        {
            w=new(w.Profile,w.Terrain,new(w.Spatial.NextEntityId,w.Spatial.Entities.Select(e=>e with{Revision=long.MaxValue}).ToImmutableArray()),w.Voxels,w.Slopes,w.Revision,fleet:w.Fleet);
            s=Session(w);
        }
        if(boundary=="order")s=Session(w,clock:new(GameTime.StartMinute,long.MaxValue,[]));
        if(boundary=="due")s=Session(w,clock:new(GameTime.MaximumMinute,0,[]));
        if(boundary=="session")s=Session(w,revision:long.MaxValue);
        var bytes=SaveCodec.Write(s);
        if(boundary=="due")Assert.Throws<ArgumentException>(()=>FleetDeployment.PreviewPlacement(s,t.Id,P));
        else Assert.Throws<OverflowException>(()=>FleetDeployment.PreviewPlacement(s,t.Id,P));
        Assert.Equal(bytes,SaveCodec.Write(s));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Explicit_mixed_formation_preserves_named_car_types_and_all_special_rail_subtypes(bool reverse)
    {
        var w=Empty();var pack=w.Terrain.Profile.Packs[0];var source=RollingStockCatalog.Bundled.Cars.Take(3).ToImmutableArray();
        var stock=new StockTrain(new(pack.Id,pack.Version,pack.CatalogHash,"test:mixed"),new("C","T","N","A","D"),
            100,2,1,100,0,null,null,null,[source]);
        // Explicit test profile exercises generic pattern composition; no source catalog is changed.
        w=MixedWorld.FromTerrain(w.Profile,w.Terrain);w=w.EnableFleet(new(w.Terrain.Profile.Packs,[stock]));
        foreach(var special in new[]{RailwayHandler.Bridge,RailwayHandler.Girder,RailwayHandler.Garage})
        {
            var point=new Location(P.X+w.Voxels.Length,P.Y,2);w=Rail(w,point,4,special);
        }
        var s=Buy(w,3,stock:stock);var after=Place(s,s.Fleet!.Trains[0].Id,P,reverse);var train=after.Fleet!.Trains[0];
        Assert.Equal(reverse?source.Reverse().Select(c=>c.Reference):source.Select(c=>c.Reference),train.Cars.Select(c=>c.Definition));
        for(var n=0;n<3;n++)Assert.Equal(w.Voxels[n].Traffic!.Rail,after.World.Voxels[n].Traffic!.Rail);
        var withdrawn=FleetDeployment.Commit(after,FleetDeployment.PreviewWithdrawal(after,train.Id));
        Assert.All(withdrawn.World.Voxels,v=>Assert.Null(v.Traffic!.Car));Assert.Equal(3,withdrawn.Fleet!.Trains[0].Cars.Length);
    }

    [Fact]
    public void All_six_bundled_families_keep_original_types_and_schedule_their_own_native_first_delay()
    {
        foreach(var stock in RollingStockCatalog.Bundled.Trains)
        {
            var w=Empty();for(var n=0;n<5;n++)w=Rail(w,P with{X=P.X+n});
            var s=Buy(w,5,stock:stock);var after=Place(s,s.Fleet!.Trains[0].Id,P,true);
            Assert.Equal(stock.MinutesPerVoxel,Assert.Single(after.Clock.Capture().Pending).DueMinute-s.Clock.Time.AbsoluteMinute);
            Assert.Equal(stock.Create(5).Cars.Reverse().Select(c=>c.Reference),after.Fleet!.Trains[0].Cars.Select(c=>c.Definition));
            Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Decode(SaveCodec.Write(after))));
        }
    }
}
