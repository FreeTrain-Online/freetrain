using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class FleetMovementTests
{
    static StockTrain Stock => RollingStockCatalog.Bundled.Trains.First(t=>t.MinutesPerVoxel==2&&t.PricePerCar==30_000_000);
    static JsonDocument Fixtures()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/train-fixtures.json")));
    static IEnumerable<JsonElement> Cases(JsonDocument f,string group)=>f.RootElement.GetProperty("groups").GetProperty(group).GetProperty("cases").EnumerateArray();
    static int Int(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    static Location Point(JsonElement a,int offset=0)=>new(a[0].GetInt32()+offset,a[1].GetInt32()+offset,a[2].GetInt32());
    static FleetCarState Pose(JsonElement e,int offset=0)=>e.GetProperty("state").GetString()=="Outside"
        ?new FleetCarState.Outside(Point(e.GetProperty("location"),offset),new(Int(e,"heading")),Int(e,"countdown"))
        :new FleetCarState.Inside(Point(e.GetProperty("location"),offset),new(Int(e,"heading")));
    static MixedWorld Empty(int ground=2,int width=32,int height=48)
    {
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Movement",new(width,height,8),0,
            Enumerable.Repeat(new TerrainCell(ground,TerrainSurface.Cut),width*height),new(1,[]),[]);
        return MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain).EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
    }
    static MixedSessionState Session(MixedWorld w,ClockState? clock=null,ConstructionAccounts? accounts=null,TrainCallback? callback=null,long? revision=null)
        =>new(new(w,accounts),TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,
            new(w.Terrain.Space.HorizontalSize,w.Terrain.Space.VerticalSize,8),0)),clock:clock,
            revision:revision??w.Revision,trainCallback:callback);
    // Trusted literal topology, with actual registered subtype bindings and owned
    // purchased stock. Gameplay-built rails/placement have separate tests below.
    static MixedWorld Rail(MixedWorld w,Location p,int pattern)
    {
        var binding=w.Profile.Bindings.Single(b=>b.Handler==(pattern<12?RailwayHandler.Normal:RailwayHandler.Junction)&&b.Variant==pattern);
        var id=w.Spatial.NextEntityId;
        return new(w.Profile,w.Terrain,new(id+1,w.Spatial.Entities.Add(new(id,0,w.Profile.TrafficOwner,p,
            new([new(new(0,0,0),true)]),true,false,0))),w.Voxels.Add(new(p,id,RailwayVoxelKind.Traffic,new(new(binding.Reference,pattern)))),
            w.Slopes,w.Revision,fleet:w.Fleet);
    }
    static MixedSessionState Purchase(MixedWorld w,int length,int quantity=1)
    {
        var s=Session(w,new(100,0,[]));return FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,length,quantity));
    }
    static MixedWorld Install(MixedWorld w,FleetTrain train)
    {
        var f=w.Fleet!;var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(t=>t.Id==train.Id?train:t),f.Groups,f.Controllers);
        var slots=train.Cars.Where(c=>c.State is FleetCarState.Inside).ToDictionary(c=>((FleetCarState.Inside)c.State).Location);
        var voxels=w.Voxels.Select(v=>v.Traffic==null?v:v with{Traffic=v.Traffic with{Car=slots.TryGetValue(v.Position,out var c)
            ?new(c.Definition,train.Id,c.Id):v.Traffic.Car?.TrainId==train.Id?null:v.Traffic.Car}}).ToImmutableArray();
        var owners=voxels.Where(v=>v!=w.At(v.Position)).Select(v=>v.OwnerId).ToHashSet();
        return w.Replace(w.Revision,w.Terrain,new(w.Spatial.NextEntityId,w.Spatial.Entities.Select(e=>owners.Contains(e.Id)
            ?e with{Revision=e.Revision+1}:e).ToImmutableArray()),voxels,w.Slopes,w.SpeedLimits,graph);
    }
    static void AdvanceTo(MixedSessionState s,long minute){while(s.Clock.Time.AbsoluteMinute<minute)s.AdvanceOneMinute();}
    static MixedSessionState Restore(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static void AssertPose(FleetTrain train,JsonElement poses,int offset=0)
    {
        Assert.Equal(poses.GetArrayLength(),train.Cars.Length);
        for(var n=0;n<train.Cars.Length;n++)
        {
            Assert.Equal(Int(poses[n],"index"),train.Cars[n].OriginalIndex);
            Assert.Equal(Pose(poses[n],offset),train.Cars[n].State);
        }
    }
    static void AssertOccupancy(MixedSessionState s)
    {
        var cars=s.Fleet!.Trains.SelectMany(t=>t.Cars.Select(c=>(t,c))).Where(a=>a.c.State is FleetCarState.Inside).ToArray();
        Assert.Equal(cars.Length,s.World.Voxels.Count(v=>v.Traffic?.Car!=null));
        foreach(var (t,c) in cars)
            Assert.Equal(new CarComponent(c.Definition,t.Id,c.Id),s.World.At(((FleetCarState.Inside)c.State).Location)!.Traffic!.Car);
    }

    [Fact]
    public void Literal_inside_callbacks_shift_all_headings_curves_blocked_own_tail_and_single_reverse_retry()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"callbacks"))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");
            if(Int(i,"stop")!=0||i.GetProperty("junction").GetString()!="straight"||i.GetProperty("occupied").GetArrayLength()!=0)continue;
            var w=Empty();foreach(var r in i.GetProperty("rails").EnumerateArray())w=Rail(w,Point(r.GetProperty("location")),Int(r,"pattern"));
            // A real second train represents the literal queued neighbor.
            w=Rail(w,new(35,30,2),4);var s=Purchase(w,i.GetProperty("cars").GetArrayLength(),2);w=s.World;
            var train=s.Fleet!.Trains[0];var neighbor=s.Fleet.Trains[1];
            train=train with{Revision=train.Revision+1,State=TrainOperationalState.Moving,Passengers=Int(i,"passengers"),
                Forwarding=i.GetProperty("forwarding").GetBoolean(),StopCallCount=Int(i,"stopCallCount"),
                Cars=train.Cars.Select((c,n)=>c with{Revision=c.Revision+1,State=Pose(i.GetProperty("cars")[n])}).ToImmutableArray()};
            w=Install(w,train);
            // The neighbor has the same formation length; place its remaining
            // cars Outside so only one separate legal rail slot is needed.
            neighbor=neighbor with{Revision=1,State=TrainOperationalState.Moving,Cars=neighbor.Cars.Select((c,n)=>c with{Revision=1,
                State=n==0?new FleetCarState.Inside(new(35,30,2),new(2)):new FleetCarState.Outside(new(0,0,2),new(0),100)}).ToImmutableArray()};
            w=Install(w,neighbor);var head=(FleetCarState.Inside)train.Head.State;
            var limits=new SpeedLimits([head.Location],[],[]);w=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,limits,w.Fleet);
            s=Session(w,new(199,10,[new(8,8,200,MixedSessionState.TrainHandler(train.Id),0),new(9,9,204,MixedSessionState.TrainHandler(neighbor.Id),0)]),new(1_000_000));
            s.AdvanceOneMinute();var after=s.Fleet!.Train(train.Id);AssertPose(after,e.GetProperty("cars"));AssertOccupancy(s);
            Assert.Equal(e.GetProperty("isReversed").GetBoolean(),after.IsReversed);Assert.Equal(e.GetProperty("forwarding").GetBoolean(),after.Forwarding);
            Assert.Equal(e.GetProperty("state").GetString(),after.State.ToString());Assert.Equal(Int(e,"moveCount"),after.MoveCount);
            Assert.Equal(Int(e,"cash"),s.Construction.Accounts.Cash);Assert.Equal(0,after.StopCallCount);
            Assert.Equal(11,s.Clock.NextOrder);var queue=s.Clock.Capture().Pending;Assert.Equal(2,queue.Length);
            Assert.Equal(MixedSessionState.TrainHandler(neighbor.Id),queue[0].HandlerId);Assert.Equal(9,queue[0].Order);
            Assert.Equal(204,queue[1].DueMinute);Assert.Equal(10,queue[1].Order);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Restore(SaveCodec.Write(s))));count++;
        }
        Assert.Equal(13,count);
    }

    [Fact]
    public void Sixteen_literal_ramp_traces_use_real_four_direction_surface_underground_owners_and_both_travel_directions()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"ramps"))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");const int offset=16;
            var underground=i.GetProperty("patterns")[0].GetInt32()>=44;var w=Empty(underground?4:2);
            var patterns=i.GetProperty("patterns").EnumerateArray().Select(p=>p.GetInt32()).ToArray();
            var climb=SlopeRailwayConstruction.Pattern(patterns[0]).Climb;
            var index=Array.FindIndex(patterns,p=>SlopeRailwayConstruction.Pattern(p).Level==0);
            var build=SlopeRailwayConstruction.PreviewBuild(w,Point(i.GetProperty("locations")[index],offset),climb);
            Assert.True(build.Legal,build.Reason);w=SlopeRailwayConstruction.Apply(new(w),build,new(100)).World;
            foreach(var r in i.GetProperty("rails").EnumerateArray())
            {
                var p=Point(r.GetProperty("location"),offset);var pattern=Int(r,"pattern");
                if(pattern<28)w=Rail(w,p,pattern);else Assert.Equal(pattern,w.At(p)!.Traffic!.Rail!.Pattern);
            }
            var s=Purchase(w,2);var train=s.Fleet!.Trains[0];train=train with{Revision=1,State=TrainOperationalState.Moving,
                Cars=train.Cars.Select((c,n)=>c with{Revision=1,State=Pose(i.GetProperty("cars")[n],offset)}).ToImmutableArray()};
            w=Install(s.World,train);s=Session(w,new(199,10,[new(8,8,200,MixedSessionState.TrainHandler(train.Id),0)]),new(1_000_000));
            foreach(var step in e.GetProperty("steps").EnumerateArray())
            {
                var restored=Restore(SaveCodec.Write(s));var due=Assert.Single(s.Clock.Capture().Pending).DueMinute;
                var oldOwner=s.World.Spatial.Entities.Single(a=>a.Definition==w.Profile.SlopeOwner);
                AdvanceTo(s,due);AdvanceTo(restored,due);Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
                AssertPose(s.Fleet!.Train(train.Id),step.GetProperty("cars"),offset);AssertOccupancy(s);
                Assert.Equal(Int(step,"moveCount"),s.Fleet.Train(train.Id).MoveCount);Assert.Equal(Int(step,"cash"),s.Construction.Accounts.Cash);
                var q=Assert.Single(s.Clock.Capture().Pending);Assert.Equal(Int(step,"due"),q.DueMinute);Assert.Equal(Int(step,"order"),q.Order);
                Assert.Equal(oldOwner.Revision+1,s.World.Spatial.Entities.Single(a=>a.Id==oldOwner.Id).Revision);
            }
            count++;
        }
        Assert.Equal(16,count);
    }

    [Fact]
    public void Eight_literal_visible_exits_precede_destination_lookup_and_install_Outside100_with_native_delay()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"exits"))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var car=i.GetProperty("car");
            var p=Point(car.GetProperty("location"));var heading=new Heading(Int(car,"heading"));
            var pattern=NormalRailPatterns.Find(heading.Index,heading.Opposite.Index);
            var s=Purchase(Rail(Empty(),p,pattern),1);var t=s.Fleet!.Trains[0];
            t=t with{Revision=1,State=TrainOperationalState.Moving,Cars=[t.Head with{Revision=1,State=Pose(car)}]};
            var w=Install(s.World,t);w=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,new([p],[],[]),w.Fleet);
            s=Session(w,new(199,10,[new(8,8,200,MixedSessionState.TrainHandler(t.Id),0)]),new(1_000_000));s.AdvanceOneMinute();
            Assert.Equal(Pose(e.GetProperty("car")),s.Fleet!.Train(t.Id).Head.State);AssertOccupancy(s);
            Assert.Equal(Int(e,"due"),Assert.Single(s.Clock.Capture().Pending).DueMinute);
            var bytes=SaveCodec.Write(s);Assert.Equal(bytes,SaveCodec.Write(Restore(bytes)));
            // The next Outside callback has an explicit later-phase boundary,
            // rather than silently running incomplete exchange/return behavior.
            AdvanceTo(s,201);bytes=SaveCodec.Write(s);
            Assert.Equal("unboundOutsideMovement",Assert.Throws<InvalidOperationException>(()=>s.AdvanceOneMinute()).Message);
            Assert.Equal(bytes,SaveCodec.Write(s));count++;
        }
        Assert.Equal(8,count);
    }

    static MixedSessionState BuildRing(bool reverse=false,int quantity=1)
    {
        var state=new MixedConstructionState(Empty(width:64,height:64));
        // Eight source-normal directions, four cells per side. Source endpoint
        // attachment closes the ring with legal 45-degree bends.
        Location[] points=[new(24,20,2),new(24,16,2),new(28,12,2),new(32,12,2),new(36,16,2),
            new(36,20,2),new(32,24,2),new(28,24,2),new(24,20,2)];
        // Build a continuous route in two halves, using source sign-vector bends.
        foreach(var (from,to) in new[]{(points[0],points[4]),(points[4],points[8])})
        {
            // Individual segments overlap at endpoints; source converts those
            // straight endpoint ports into the corresponding normal curve.
            var first=Array.IndexOf(points,from);var end=from==points[0]?4:8;
            for(var n=first;n<end;n++)
            {
                var plan=NormalRailwayConstruction.PreviewBuild(state.World,points[n],points[n+1]);
                Assert.True(plan.Legal,plan.Reason);state=NormalRailwayConstruction.Apply(state,plan,new(100));
            }
        }
        var s=Session(state.World,new(100,0,[]),state.Accounts);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,3,quantity));
        for(var n=0;n<quantity;n++)s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,s.Fleet!.Trains[n].Id,
            n==0?new(24,20,2):new(36,16,2),reverse));
        return s;
    }

    [Fact]
    public async Task Committed_motion_history_keeps_every_coalesced_turn_and_resets_after_bounded_overflow_or_load()
    {
        var bytes=SaveCodec.Write(BuildRing());
        using var host=SessionComposition.CreateSavedHost(bytes);
        var baseline=host.Snapshot();
        var run=host.Enqueue(new("operator-1"),new("run-motion",host.WorldEpoch,new RunCommand()));
        host.Pump();Assert.IsType<AcceptedMessage>(await run);
        host.Pump(24);
        var read=host.Snapshot();var delta=StateDifference.Between(baseline,read);
        var motion=Assert.IsType<TrainMotionRead>(delta.TrainMotion);
        Assert.False(motion.Reset);Assert.Equal(12,motion.Sequence);Assert.Equal(12,motion.Steps.Length);
        Assert.Equal(Enumerable.Range(1,12).Select(n=>(long)n),motion.Steps.Select(s=>s.Sequence));
        foreach(var step in motion.Steps)
        {
            Assert.Equal(3,step.Cars.Length);Assert.Equal(2,step.Duration);
            foreach(var car in step.Cars)
            {
                var from=car.From.Position;var to=car.To.Position;
                Assert.Equal(from.Z,to.Z);Assert.InRange(Math.Abs(to.X-from.X),0,1);Assert.InRange(Math.Abs(to.Y-from.Y),0,1);
                Assert.NotEqual(from,to);Assert.Equal(car.From.Outgoing,car.To.Heading);
            }
        }
        var last=motion.Steps[^1];
        foreach(var car in read.Mixed!.Fleet!.Trains[0].Cars)
            Assert.Equal(car.State.Location,last.Cars.Single(c=>c.CarId==car.Id).To.Position);
        Assert.Empty(StateDifference.Between(read,read).TrainMotion!.Steps);
        host.Pump(256);host.Pump(256);
        Assert.InRange(host.Snapshot().TrainMotion!.Steps.Length,1,SessionHost.MaximumMotionSteps);
        var gap=StateDifference.Between(baseline,host.Snapshot()).TrainMotion!;
        Assert.True(gap.Reset);Assert.Empty(gap.Steps);
        var load=host.LoadSave(bytes);host.Pump();await load;
        Assert.Equal(0,host.Snapshot().TrainMotion!.Sequence);Assert.Empty(host.Snapshot().TrainMotion!.Steps);
    }

    [Fact]
    public async Task Failed_detached_minute_emits_no_motion_even_after_an_earlier_train_moved()
    {
        var s=BuildRing(quantity:2);var second=s.Fleet!.Trains[1].Id;
        TrainCallbackResult Callback(MixedSessionState minute,long id)
        {
            if(id==second)throw new InvalidOperationException("unboundHarborController");
            return FleetMovement.Tick(minute,id);
        }
        s=Session(s.World,s.Clock.Capture(),s.Construction.Accounts,Callback);
        using var host=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var before=host.Snapshot();
        var run=host.Enqueue(new("owner"),new("rollback-motion",host.WorldEpoch,new RunCommand()));
        host.Pump();Assert.IsType<AcceptedMessage>(await run);host.Pump(2);
        var after=host.Snapshot();Assert.True(after.Paused);
        Assert.Equal(before.Cash,after.Cash);Assert.Equal(before.Mixed!.Fleet!.Trains,after.Mixed!.Fleet!.Trains);
        Assert.Equal(0,after.TrainMotion!.Sequence);Assert.Empty(after.TrainMotion.Steps);
        Assert.Empty(StateDifference.Between(before,after).TrainMotion!.Steps);
    }

    [Fact]
    public async Task Unsupported_tunnel_and_Outside_steps_emit_ordered_reset_markers()
    {
        var s=SaveCodec.ReadWorld(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/fleet-v9.json")),
            TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        s=FleetTrading.Commit(s,FleetTrading.QuoteSale(s,17));
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,20,new(3,2,2)));
        using var host=SessionComposition.CreateSavedHost(SaveCodec.Write(s));
        var before=host.Snapshot();
        var run=host.Enqueue(new("operator-1"),new("unsupported-motion",host.WorldEpoch,new RunCommand()));
        host.Pump();Assert.IsType<AcceptedMessage>(await run);host.Pump(2);
        var motion=StateDifference.Between(before,host.Snapshot()).TrainMotion!;
        Assert.False(motion.Reset);Assert.NotEmpty(motion.Steps);
        Assert.All(motion.Steps,step=>{Assert.Equal(20,step.TrainId);Assert.Empty(step.Cars);});
    }

    [Fact]
    public async Task Shared_clock_rates_preserve_exact_ring_movement_accounts_random_queue_and_save()
    {
        var initial=SaveCodec.Write(BuildRing());
        byte[]? baseline=null;
        var now=new DateTimeOffset(2026,11,4,12,0,0,TimeSpan.Zero);
        foreach(var rate in new[]{0.125,0.25,0.5,1,2})
        {
            using var host=SessionComposition.CreateSavedHost(initial);
            async Task Send(CommandPayload payload)
            {
                var pending=host.Enqueue(new("operator-1"),new(Guid.NewGuid().ToString(),host.WorldEpoch,payload));
                host.Pump();Assert.IsType<AcceptedMessage>(await pending);
            }
            await Send(new SetPlaybackRateCommand(rate));host.PumpRealtime(now);
            await Send(new RunCommand());host.PumpRealtime(now);
            for(var i=1;i<=16/rate;i++)host.PumpRealtime(now.AddMilliseconds(i*50));
            await Send(new PauseCommand());
            var read=host.Snapshot();Assert.Equal(116,read.Tick);
            Assert.Equal(8,read.Mixed!.Fleet!.Trains[0].MoveCount);
            Assert.Equal(8,read.TrainMotion!.Steps.Length);
            var pendingSave=host.CaptureSave();host.Pump();var save=await pendingSave;
            if(baseline==null)baseline=save;else Assert.Equal(baseline,save);
        }
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Purchased_and_deployed_stock_runs_gameplay_built_ring_through_simple_junction_and_reopen(bool reverse)
    {
        var s=BuildRing(reverse);var id=s.Fleet!.Trains[0].Id;
        // Add a live branch at a straight with a neighbor on both sides. Simple
        // must keep the ring path; each follower inherits its old predecessor.
        var junction=NormalRailwayConstruction.PreviewBuild(s.World,new(24,17,2),new(26,15,2));
        Assert.True(junction.Legal,junction.Reason);var construction=NormalRailwayConstruction.Apply(s.Construction,junction,s.Clock.Time);
        s=s.Successor(s.Revision,construction,false);
        var output=Environment.GetEnvironmentVariable("FREETRAIN_MOVING_FLEET_OUTPUT");
        if(output!=null&&!reverse)await File.WriteAllBytesAsync(output,SaveCodec.Write(s));
        using var host=SessionComposition.CreateSavedHost(SaveCodec.Write(s));
        var restored=Restore(SaveCodec.Write(s));var initial=s.Fleet!.Train(id);var locations=new HashSet<Location>();
        var accounts=s.Construction.Accounts.Transactions.Length;
        // Independent clockwise ring oracle, authored from the eight endpoint
        // segments above. No production guide/path output supplies expectations.
        var route=new List<Location>();var position=new Location(24,20,2);
        foreach(var direction in new[]{0,1,2,3,4,5,6,7})
            for(var n=0;n<4;n++){route.Add(position);position=position.Move(new(direction));}
        var start=reverse?0:2;
        for(var move=1;move<=80;move++)
        {
            var due=s.Clock.Capture().Pending.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(id)).DueMinute;
            AdvanceTo(s,due);AdvanceTo(restored,due);
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));AssertOccupancy(s);
            var t=s.Fleet!.Train(id);Assert.Equal(move,t.MoveCount);Assert.Equal(TrainOperationalState.Moving,t.State);
            Assert.Equal(initial.Cars.Select(c=>c.Id),t.Cars.Select(c=>c.Id));Assert.Equal(initial.IsReversed,t.IsReversed);
            Assert.Equal(route[(start+(reverse?-move:move)+96)%32],((FleetCarState.Inside)t.Head.State).Location);
            locations.Add(((FleetCarState.Inside)t.Head.State).Location);
            if(move==40)restored=Restore(SaveCodec.Write(restored));
        }
        Assert.Equal(32,locations.Count);Assert.Equal(accounts+20,s.Construction.Accounts.Transactions.Length);
        // Startup uses the same production dispatcher and exact saved graph.
        Assert.NotNull(host.Snapshot().Mixed!.Fleet);
        for(var n=0;n<2;n++)
        {
            var command=host.Enqueue(new("operator-1"),new(Guid.NewGuid().ToString(),host.WorldEpoch,new StepCommand()));
            host.Pump(0);Assert.IsType<AcceptedMessage>(await command);
        }
        Assert.Equal(1,host.Snapshot().Mixed!.Fleet!.Trains[0].MoveCount);
    }

    [Fact]
    public void Foreign_occupancy_blocks_without_reversal_and_resumes_after_withdrawal()
    {
        var w=Empty();for(var x=18;x<=26;x++)w=Rail(w,new(x,20,2),4);
        var s=Purchase(w,2,2);var first=s.Fleet!.Trains[0].Id;var second=s.Fleet.Trains[1].Id;
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,first,new(18,20,2)));
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,second,new(20,20,2)));
        var t=s.Fleet!.Train(first);var accounts=s.Construction.Accounts.Transactions.Length;
        AdvanceTo(s,102);var blocked=s.Fleet!.Train(first);
        Assert.Equal(t.Cars.ToArray(),blocked.Cars.ToArray());Assert.Equal(0,blocked.MoveCount);
        Assert.Equal(TrainOperationalState.EmergencyStopping,blocked.State);Assert.False(blocked.IsReversed);
        Assert.Equal(accounts+1,s.Construction.Accounts.Transactions.Length); // only the neighbor moves
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewWithdrawal(s,second));AdvanceTo(s,104);
        Assert.Equal(1,s.Fleet!.Train(first).MoveCount);Assert.Equal(TrainOperationalState.Moving,s.Fleet.Train(first).State);
        AssertOccupancy(s);Assert.Single(s.Clock.Capture().Pending);
    }

    [Theory]
    [InlineData("tail-straight")][InlineData("tail-curve")]
    public void Junction_follower_reuses_literal_predecessor_route_even_when_current_simple_controller_would_choose_straight(string caseId)
    {
        using var f=Fixtures();var row=Cases(f,"guides").Single(r=>r.GetProperty("id").GetString()==caseId);
        var i=row.GetProperty("input");var outgoing=new Heading(Int(row.GetProperty("expected"),"outgoing"));
        var junction=Point(i.GetProperty("location"),16);var predecessor=Point(i.GetProperty("previous"),16);
        var target=predecessor.Move(outgoing);var straight=NormalRailPatterns.Find(outgoing.Index,outgoing.Opposite.Index);
        var w=Rail(Rail(Rail(Empty(),junction,Int(i,"pattern")),predecessor,straight),target,straight);
        var s=Purchase(w,2);var t=s.Fleet!.Trains[0];
        t=t with{Revision=1,State=TrainOperationalState.Moving,Cars=[
            t.Cars[0] with{Revision=1,State=new FleetCarState.Inside(predecessor,outgoing)},
            t.Cars[1] with{Revision=1,State=new FleetCarState.Inside(junction,new(Int(i,"incoming")))}]};
        s=Session(Install(s.World,t),new(100,1,[new(0,0,101,MixedSessionState.TrainHandler(t.Id),0)]));s.AdvanceOneMinute();
        var after=s.Fleet!.Train(t.Id);Assert.Equal(new FleetCarState.Inside(target,outgoing),after.Head.State);
        Assert.Equal(t.Head.State,after.Cars[1].State);Assert.Equal(t.Cars.Select(c=>c.Id),after.Cars.Select(c=>c.Id));AssertOccupancy(s);
    }

    [Fact]
    public void Present_destination_track_without_reciprocal_port_is_a_missing_candidate_and_reverses_once()
    {
        var p=new Location(24,24,2);var w=Rail(Rail(Rail(Empty(),p,4),p.Move(new(2)),10),p.Move(new(6)),4);
        var s=Purchase(w,1);var id=s.Fleet!.Trains[0].Id;
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,id,p));AdvanceTo(s,102);
        var t=s.Fleet!.Train(id);Assert.True(t.IsReversed);Assert.Equal(1,t.MoveCount);
        Assert.Equal(new FleetCarState.Inside(p.Move(new(6)),new(6)),t.Head.State);AssertOccupancy(s);
    }

    [Theory]
    [InlineData("late-fault")][InlineData("order")][InlineData("car")][InlineData("owner")][InlineData("moves")]
    public void Interrupted_move_or_later_callback_leaves_complete_consists_accounts_random_and_queue_unchanged(string fault)
    {
        var s=BuildRing(quantity:2);var id=s.Fleet!.Trains[0].Id;var second=s.Fleet.Trains[1].Id;
        if(fault is "car" or "moves")
        {
            var t=s.Fleet.Train(id);var changed=t with{Revision=t.Revision+1,MoveCount=fault=="moves"?int.MaxValue:t.MoveCount,
                Cars=fault=="car"?t.Cars.Select(c=>c with{Revision=long.MaxValue}).ToImmutableArray():t.Cars};
            var w=s.World;var f=w.Fleet!;
            var graph=new FleetGraph(f.Profile,f.RootGroupId,f.Trains.Select(a=>a.Id==id?changed:a),f.Groups,f.Controllers);
            s=Session(new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,graph),
                s.Clock.Capture(),s.Construction.Accounts);
        }
        if(fault=="owner")
        {
            var w=s.World;w=new(w.Profile,w.Terrain,new(w.Spatial.NextEntityId,w.Spatial.Entities.Select(e=>
                e with{Revision=long.MaxValue}).ToImmutableArray()),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet);
            s=Session(w,s.Clock.Capture(),s.Construction.Accounts);
        }
        if(fault=="order")s=Session(s.World,s.Clock.Capture() with{NextOrder=long.MaxValue},s.Construction.Accounts);
        if(fault=="late-fault")
        {
            TrainCallbackResult Callback(MixedSessionState state,long trainId)
            {
                state.Random["construction"].Next(100);
                if(trainId==second)throw new InvalidOperationException("Injected after first complete move");
                return FleetMovement.Tick(state,trainId);
            }
            s=Session(s.World,s.Clock.Capture(),s.Construction.Accounts,Callback);
        }
        AdvanceTo(s,101);var bytes=SaveCodec.Write(s);var world=s.World;
        if(fault=="late-fault")Assert.Throws<InvalidOperationException>(()=>s.AdvanceOneMinute());
        else Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());
        Assert.Same(world,s.World);Assert.Equal(bytes,SaveCodec.Write(s));AssertOccupancy(s);
    }

    [Fact]
    public async Task Unavailable_Outside_handler_rejects_step_and_pauses_run_without_faulting_the_host()
    {
        var s=SaveCodec.ReadWorld(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Saves/fleet-v9.json")),
            TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        // The literal inventory's deployed train has a source-bound controller
        // and an overflowing historical move counter. Remove it, deploy the
        // single owned stock on the tunnel, then exit on the next native move.
        s=FleetTrading.Commit(s,FleetTrading.QuoteSale(s,17));
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,20,new(3,2,2)));
        AdvanceTo(s,130083);Assert.IsType<FleetCarState.Outside>(s.Fleet!.Train(20).Head.State);
        AdvanceTo(s,130084);var bytes=SaveCodec.Write(s);
        using var h=SessionComposition.CreateSavedHost(bytes);
        var step=h.Enqueue(new("operator-1"),new("unavailable-step",h.WorldEpoch,new StepCommand()));h.Pump(0);
        Assert.Equal("operationUnavailable",Assert.IsType<RejectedMessage>(await step).Code);
        Assert.Equal(130084,h.Snapshot().Tick);Assert.True(h.Snapshot().Paused);
        var unchanged=h.CaptureSave();h.Pump(0);Assert.Equal(bytes,await unchanged);
        var run=h.Enqueue(new("operator-1"),new("unavailable-run",h.WorldEpoch,new RunCommand()));h.Pump(1);
        Assert.IsType<AcceptedMessage>(await run);Assert.True(h.Snapshot().Paused);Assert.Equal(130084,h.Snapshot().Tick);
        var save=h.CaptureSave();h.Pump(0);Assert.NotEmpty(await save);
    }
}
