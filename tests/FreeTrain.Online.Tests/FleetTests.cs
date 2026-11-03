using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class FleetTests
{
    static FleetProfile Profile => RollingStockCatalog.Bundled.FleetProfile;
    static StockTrain Stock => RollingStockCatalog.Bundled.Trains.First(t => t.MinutesPerVoxel == 2 && t.PricePerCar == 30_000_000);
    static Location P => GarageConstructionTests.Start;
    static MixedWorld Empty() => GarageConstructionTests.World().EnableFleet(Profile);
    static FleetGraph Graph(FleetGraph f, IEnumerable<FleetTrain>? trains = null, IEnumerable<FleetGroup>? groups = null,
        IEnumerable<FleetController>? controllers = null) => new(f.Profile,f.RootGroupId,trains ?? f.Trains,groups ?? f.Groups,controllers ?? f.Controllers);
    static FleetTrain Train(long id, long carId, FleetGraph f, FleetCarState state, int length=1) => new(id,0,"Train",Stock.Binding,
        f.RootGroupId,f.Simple.Id,Enumerable.Range(0,length).Select(n => new FleetCar(carId+n,0,n,Stock.Create(length).Cars[n].Reference,state)).ToImmutableArray(),
        State:state is FleetCarState.Unplaced ? TrainOperationalState.Unplaced : TrainOperationalState.Moving);
    static MixedWorld Add(MixedWorld w, FleetCarState state, int length=1)
    {
        var f=w.Fleet!; var id=w.Spatial.NextEntityId; var t=Train(id,id+1,f,state,length);
        var root=f.Groups.Single(g => g.Id==f.RootGroupId);
        var next=Graph(f,f.Trains.Add(t),f.Groups.Select(g=>g.Id==root.Id ? g with {Revision=g.Revision+1,Members=g.Members.Add(t.Id)} : g));
        var entities=w.Spatial.Entities.ToBuilder();var voxels=w.Voxels.ToBuilder();var nextId=id+1+length;
        if(state is FleetCarState.Inside inside)
        {
            Assert.Equal(1,length);
            entities.Add(new(nextId,0,w.Profile.TrafficOwner,inside.Location,new([new(new(0,0,0),true)]),true,false,0));
            var pattern=inside.Heading.Index%4==0 ? 10 : 4;
            var rail=w.Profile.Bindings.Single(b=>b.Handler==RailwayHandler.Normal&&b.Variant==pattern).Reference;
            voxels.Add(new(inside.Location,nextId,RailwayVoxelKind.Traffic,new(new(rail,pattern),Car:new(t.Cars[0].Definition,t.Id,t.Cars[0].Id))));nextId++;
        }
        return w.Replace(w.Revision,w.Terrain,new(nextId,entities.ToImmutable()),voxels,w.Slopes,fleet:next);
    }
    static MixedWorld Change(MixedWorld w, FleetTrain t, bool clearSlots=false, SpeedLimits? limits=null)
    {
        var f=Graph(w.Fleet!,w.Fleet!.Trains.Select(old=>old.Id==t.Id?t:old));
        var voxels=w.Voxels.Select(v=>clearSlots&&v.Traffic?.Car?.TrainId==t.Id ? v with{Traffic=v.Traffic with{Car=null}}:v).ToArray();
        var changed=voxels.Where(v=>v!=w.At(v.Position)).Select(v=>v.OwnerId).ToHashSet();
        return w.Replace(w.Revision,w.Terrain,new(w.Spatial.NextEntityId,w.Spatial.Entities.Select(e=>changed.Contains(e.Id)?e with{Revision=e.Revision+1}:e).ToImmutableArray()),voxels,w.Slopes,limits,f);
    }
    static MixedSessionState Session(MixedWorld w, TrainCallback? callback=null, ClockState? clock=null)
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0);
        var actions=w.Fleet!.Trains.Where(t=>t.State!=TrainOperationalState.Unplaced)
            .Select((t,n)=>new ScheduledAction(n,n,3421,MixedSessionState.TrainHandler(t.Id),0)).ToImmutableArray();
        return new(new(w),TerrainProvenance.From(request),clock:clock??new(3420,actions.Length,actions),revision:w.Revision,trainCallback:callback);
    }
    static TrainCallbackResult Wait(MixedSessionState s,long id) => new(s.Construction,2);

    [Fact]
    public void Actual_stock_uses_shared_allocator_without_second_car_footprints_and_preserves_all_state()
    {
        var empty=Empty();Assert.Equal(4,empty.Spatial.NextEntityId);Assert.Empty(empty.Spatial.Entities);
        Assert.Equal(empty.Fleet!.Simple.Id,empty.Fleet.Groups[0].ControllerId);
        var world=Add(empty,new FleetCarState.Inside(P,new(2)));var t=world.Fleet!.Trains[0];
        Assert.Equal(4,t.Id);Assert.Equal(5,t.Head.Id);Assert.Equal(7,world.Spatial.NextEntityId);
        Assert.Equal(6,Assert.Single(world.Spatial.Entities).Id);Assert.Equal(t.Id,world.At(P)!.Traffic!.Car!.TrainId);
        Assert.Null(t.Predecessor(t.Head.Id));Assert.Throws<ArgumentException>(()=>t.Predecessor(-1));
        t=t with{Revision=1,PurchaseMinute=100,Forwarding=true,Passengers=150,MoveCount=17,StopCallCount=2,
            State=TrainOperationalState.StoppingAtStation,BoardingOrigin=new FleetCarState.Outside(new(0,0,2),new(0),51)};
        world=Change(world,t);var session=Session(world,Wait);var frozen=session.Freeze();
        Assert.Same(world,frozen.World);Assert.Equal(t,frozen.Fleet!.Train(t.Id));Assert.Single(frozen.Clock.Capture().Pending);
        Assert.Empty(empty.Fleet.Trains);Assert.Single(world.Spatial.Entities);
        Assert.NotEmpty(SaveCodec.Write(session));
        Assert.NotNull(MixedWire.Read(session.Construction).Fleet);
        Assert.NotNull(MixedWire.Snapshot(session,1).Mixed!.Queue);
        Assert.Throws<ArgumentException>(()=>Session(world,Wait,new(99,1,[new(0,0,100,MixedSessionState.TrainHandler(t.Id),0)])));
    }

    [Theory]
    [InlineData("duplicate-train")][InlineData("duplicate-car")][InlineData("wrong-index")][InlineData("wrong-type")]
    [InlineData("partial-unplaced")][InlineData("negative-counter")][InlineData("passengers")][InlineData("countdown")]
    [InlineData("dangling-controller")][InlineData("dangling-group")][InlineData("future-purchase")][InlineData("pack")]
    public void Invalid_consists_and_source_state_reject_before_publication(string bad)
    {
        var w=Add(Empty(),new FleetCarState.Unplaced(),2);var f=w.Fleet!;var t=f.Trains[0];
        var cars=t.Cars;var rows=f.Trains;
        switch(bad)
        {
            case "duplicate-train": rows=rows.Add(t);break;
            case "duplicate-car":t=t with{Cars=cars.SetItem(1,cars[1] with{Id=cars[0].Id})};break;
            case "wrong-index":t=t with{Cars=cars.SetItem(0,cars[0] with{OriginalIndex=1})};break;
            case "wrong-type":t=t with{Cars=cars.SetItem(0,cars[0] with{Definition=w.Profile.TrafficOwner})};break;
            case "partial-unplaced":t=t with{Cars=cars.SetItem(0,cars[0] with{State=new FleetCarState.Inside(P,new(2))})};break;
            case "negative-counter":t=t with{MoveCount=-1};break;
            case "passengers":t=t with{Passengers=int.MaxValue};break;
            case "countdown":t=t with{BoardingOrigin=new FleetCarState.Outside(P,new(2),101)};break;
            case "dangling-controller":t=t with{ControllerId=999};break;
            case "dangling-group":t=t with{GroupId=999};break;
            case "future-purchase":t=t with{PurchaseMinute=GameTime.MaximumMinute+1};break;
            case "pack":t=t with{Definition=t.Definition with{CatalogHash="different"}};break;
        }
        if(bad!="duplicate-train")rows=rows.SetItem(0,t);
        Assert.Throws<ArgumentException>(()=>Graph(f,rows));Assert.Equal(2,w.Fleet!.Trains[0].Cars.Length);
    }

    [Theory]
    [InlineData("missing-slot")][InlineData("orphan-slot")][InlineData("wrong-train")][InlineData("wrong-car")]
    [InlineData("wrong-definition")][InlineData("no-rail")][InlineData("wrong-location")][InlineData("outside-inside")]
    [InlineData("allocator")][InlineData("spatial-collision")]
    public void Bidirectional_fleet_and_traffic_or_allocator_corruption_rejects(string bad)
    {
        var w=Add(Empty(),new FleetCarState.Inside(P,new(2)));var f=w.Fleet!;var t=f.Trains[0];var v=w.Voxels[0];var spatial=w.Spatial.Capture();
        switch(bad)
        {
            case "missing-slot":v=v with{Traffic=v.Traffic! with{Car=null}};break;
            case "orphan-slot":t=t with{State=TrainOperationalState.Unplaced,Cars=t.Cars.Select(c=>c with{State=new FleetCarState.Unplaced()}).ToImmutableArray()};break;
            case "wrong-train":v=v with{Traffic=v.Traffic! with{Car=v.Traffic.Car! with{TrainId=999}}};break;
            case "wrong-car":v=v with{Traffic=v.Traffic! with{Car=v.Traffic.Car! with{CarId=999}}};break;
            case "wrong-definition":v=v with{Traffic=v.Traffic! with{Car=v.Traffic.Car! with{Definition=w.Profile.TrafficOwner}}};break;
            case "no-rail":v=v with{Traffic=v.Traffic! with{Rail=null}};break;
            case "wrong-location":t=t with{Cars=t.Cars.Select(c=>c with{State=new FleetCarState.Inside(P.Move(new(2)),new(2))}).ToImmutableArray()};break;
            case "outside-inside":t=t with{Cars=t.Cars.Select(c=>c with{State=new FleetCarState.Outside(P,new(2),100)}).ToImmutableArray()};break;
            case "allocator":spatial=spatial with{NextEntityId=t.Head.Id};break;
            case "spatial-collision":t=t with{Id=v.OwnerId};break;
        }
        Assert.Throws<ArgumentException>(()=>new MixedWorld(w.Profile,w.Terrain,spatial,[v],w.Slopes,w.Revision,fleet:Graph(f,[t],f.Groups.Select(g=>g with{Members=[t.Id]}))));
        Assert.Equal(w.Fleet!.Trains[0].Id,w.At(P)!.Traffic!.Car!.TrainId);
    }

    [Fact]
    public void Shared_controller_and_ordered_group_graph_resolves_null_delegation_and_root_fallbacks()
    {
        var w=Add(Add(Empty(),new FleetCarState.Unplaced()),new FleetCarState.Unplaced());var f=w.Fleet!;
        Assert.Equal(2,f.Profile.Providers.Length);
        var provider=f.Profile.Providers.Single(p=>p.Kind==FleetControllerKind.Timetable);
        var custom=new FleetController(20,0,"Shared",provider.Kind,provider.Binding);
        var delegation=f.Controllers.Single(c=>c.Kind==FleetControllerKind.Delegation);
        var root=f.Groups[0] with{Members=[22]};
        var a=new FleetGroup(22,0,"A",root.Id,custom.Id,[23]);
        var b=new FleetGroup(23,0,"B",a.Id,delegation.Id,f.Trains.Select(t=>t.Id).ToImmutableArray());
        var trains=f.Trains.Select(t=>t with{GroupId=b.Id,ControllerId=delegation.Id}).ToArray();
        var graph=Graph(f,trains,[root,a,b],f.Controllers.Add(custom));
        Assert.All(trains,t=>Assert.Equal(custom.Id,graph.EffectiveController(t.Id).Id));
        var direct=Graph(graph,trains.Select(t=>t with{ControllerId=custom.Id}));
        Assert.All(trains,t=>Assert.Equal(custom.Id,direct.EffectiveController(t.Id).Id));
        var nulled=Graph(graph,groups:[root,a,b with{ControllerId=null}]);
        Assert.All(trains,t=>Assert.Equal(f.Simple.Id,nulled.EffectiveController(t.Id).Id));
        var exhausted=Graph(graph,groups:[root with{ControllerId=delegation.Id},a with{ControllerId=delegation.Id},b]);
        Assert.All(trains,t=>Assert.Equal(f.Simple.Id,exhausted.EffectiveController(t.Id).Id));
        Assert.Throws<ArgumentException>(()=>Graph(graph,controllers:f.Controllers)); // referenced deletion
        Assert.Throws<ArgumentException>(()=>Graph(graph,groups:[root with{ParentId=b.Id},a,b]));
        Assert.Throws<ArgumentException>(()=>Graph(graph,groups:[root with{Members=[]},a,b]));
        Assert.Throws<ArgumentException>(()=>Graph(graph,groups:[root,a,b with{Members=b.Members.Add(b.Members[0])}]));
        Assert.Throws<ArgumentException>(()=>Graph(graph,groups:[root,a with{ParentId=b.Id,Members=[b.Id]},b with{Members=b.Members.Add(a.Id)}]));
        Assert.Throws<ArgumentException>(()=>Graph(graph,controllers:f.Controllers.Add(custom with{Provider=custom.Provider! with{CatalogHash="wrong"}})));
    }

    [Fact]
    public void Successors_preserve_identity_revisions_limits_and_inventory_across_construction_edits()
    {
        var w=Add(Empty(),new FleetCarState.Unplaced());var s=Session(w);var t=w.Fleet!.Trains[0];
        Assert.Throws<ArgumentException>(()=>Change(w,t with{Name="Changed"}));
        var changed=Change(w,t with{Revision=1,Name="Changed"});Assert.Equal("Changed",changed.Fleet!.Trains[0].Name);
        var plan=NormalRailwayConstruction.PreviewBuild(w,P,P.Move(new(2)));
        Assert.True(plan.Legal,plan.Reason);
        var edited=NormalRailwayConstruction.Apply(s.Construction,plan,s.Clock.Time);
        s=s.Successor(s.Revision,edited,false);Assert.Same(w.Fleet,s.Fleet);
        Assert.Equal(t.Id,s.Fleet!.Trains[0].Id);Assert.Equal(2,s.World.Spatial.Entities.Length);
        var noFleet=new MixedWorld(s.World.Profile,s.World.Terrain,s.World.Spatial.Capture(),s.World.Voxels,s.World.Slopes,s.World.Revision+1);
        Assert.Throws<ArgumentException>(()=>s.Successor(s.Revision,new(noFleet,s.Construction.Accounts),false));
        var invalid=Graph(s.Fleet!,[t with{Id=6}],s.Fleet!.Groups.Select(g=>g with{Members=[6]}));
        Assert.Throws<ArgumentException>(()=>s.World.Replace(s.World.Revision,s.World.Terrain,s.World.Spatial.Capture(),s.World.Voxels,s.World.Slopes,fleet:invalid));
        var replacing=t with{Revision=1,Cars=t.Cars.Select(c=>c with{Id=s.World.Spatial.NextEntityId,Revision=0}).ToImmutableArray()};
        Assert.Throws<ArgumentException>(()=>s.World.Replace(s.World.Revision,s.World.Terrain,
            new(s.World.Spatial.NextEntityId+1,s.World.Spatial.Entities),s.World.Voxels,s.World.Slopes,fleet:Graph(s.Fleet,[replacing])));
        var candidate=Change(s.World,t with{Revision=1,Name="Next"});
        Assert.Throws<ArgumentException>(()=>s.Successor(s.Revision,new(candidate,s.Construction.Accounts),false,
            clock:new(s.Clock.Time.AbsoluteMinute,1,[new(0,0,s.Clock.Time.AbsoluteMinute+1,"unknown/handler",0)])));
    }

    [Fact]
    public void Independent_reversal_and_turning_poses_keep_original_indices_and_current_predecessors()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/train-fixtures.json")));
        var count=0;
        foreach(var family in new[]{"reversals","turning"})
        foreach(var row in doc.RootElement.GetProperty("groups").GetProperty(family).GetProperty("cases").EnumerateArray())
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            foreach(var pose in new[]{input,expected})
            {
                var cars=pose.GetProperty("cars");var f=Empty().Fleet!;var formation=Stock.Create(cars.GetArrayLength());
                var members=cars.EnumerateArray().Select(c=>new FleetCar(10+c.GetProperty("index").GetInt32(),0,c.GetProperty("index").GetInt32(),
                    formation.Cars[c.GetProperty("index").GetInt32()].Reference,ReadPose(c))).ToImmutableArray();
                var t=new FleetTrain(8,0,"Fixture",Stock.Binding,f.RootGroupId,f.Simple.Id,members,
                    pose.GetProperty("isReversed").GetBoolean(),TrainOperationalState.Moving);
                var graph=Graph(f,[t],f.Groups.Select(g=>g with{Members=[t.Id]}));
                Assert.Null(t.Predecessor(t.Head.Id));
                for(var n=1;n<members.Length;n++)Assert.Equal(members[n-1].Id,graph.Train(t.Id).Predecessor(members[n].Id)!.Id);
                Assert.Equal(members.Select(c=>c.OriginalIndex),cars.EnumerateArray().Select(c=>c.GetProperty("index").GetInt32()));
                count++;
            }
        }
        Assert.Equal(120,count);
    }
    static FleetCarState ReadPose(JsonElement c)
    {
        var p=c.GetProperty("location");var h=new Heading(c.GetProperty("heading").GetInt32());var location=new Location(p[0].GetInt32(),p[1].GetInt32(),p[2].GetInt32());
        return c.GetProperty("state").GetString()=="Outside" ? new FleetCarState.Outside(location,h,c.GetProperty("countdown").GetInt32()) : new FleetCarState.Inside(location,h);
    }

    [Theory]
    [InlineData("missing")][InlineData("duplicate")][InlineData("repeated")][InlineData("unknown")][InlineData("unplaced")]
    [InlineData("due-now")][InlineData("no-engine")]
    public void Malformed_train_schedules_reject(string bad)
    {
        var w=Add(Empty(),new FleetCarState.Inside(P,new(2)));var handler=MixedSessionState.TrainHandler(w.Fleet!.Trains[0].Id);
        ImmutableArray<ScheduledAction> pending=[new(0,0,3421,handler,0)];
        switch(bad)
        {
            case "missing":pending=[];break;
            case "duplicate":pending=pending.Add(new(1,1,3422,handler,0));break;
            case "repeated":pending=[pending[0] with{Interval=1}];break;
            case "unknown":pending=[pending[0] with{HandlerId="train/999"}];break;
            case "unplaced":w=Add(Empty(),new FleetCarState.Unplaced());break;
            case "due-now":pending=[pending[0] with{DueMinute=3420}];break;
        }
        Assert.Throws<ArgumentException>(()=>Session(w,bad=="no-engine"?null:Wait,new(3420,2,pending)));
    }

    [Fact]
    public void Outside_only_train_owns_one_callback_and_withdrawal_cancels_only_its_own_action()
    {
        var w=Add(Add(Empty(),new FleetCarState.Outside(new(0,0,2),new(0),51)),new FleetCarState.Inside(P,new(2)));
        var s=Session(w,Wait);Assert.Equal(2,s.Clock.Capture().Pending.Length);
        var t=s.Fleet!.Trains[0];var next=Change(w,t with{Revision=1,State=TrainOperationalState.Unplaced,
            Cars=t.Cars.Select(c=>c with{Revision=1,State=new FleetCarState.Unplaced()}).ToImmutableArray()},true);
        var survivor=s.Clock.Capture().Pending[1];s=s.Successor(s.Revision,new(next,s.Construction.Accounts),false);
        Assert.Equal(survivor,Assert.Single(s.Clock.Capture().Pending));s.AdvanceOneMinute();
        Assert.Equal(3423,Assert.Single(s.Clock.Capture().Pending).DueMinute);
        Assert.IsType<FleetCarState.Inside>(s.Fleet!.Trains[1].Head.State);
    }

    [Fact]
    public void Train_and_construction_callbacks_coexist_freeze_and_continue_in_equal_time_order()
    {
        var w=Add(Add(Empty(),new FleetCarState.Inside(P,new(2))),new FleetCarState.Outside(new(0,0,2),new(0),100));
        var calls=new List<long>();
        TrainCallbackResult Callback(MixedSessionState s,long id){calls.Add(id);return new(s.Construction,4);}
        var s=Session(w,Callback);
        var type=w.Profile.Bindings.First(b=>b.Stationary?.Height==1).Reference.ContributionId;
        s=StationaryConstruction.Commit(s,StationaryConstruction.Preview(s.World,P.Move(new(4)),type));
        var site=Assert.Single(s.Clock.Capture().Pending.Where(a=>a.HandlerId.StartsWith("construction/")));
        var q=s.Clock.Capture();
        // Literal equal-time queue family: T then U, neighbor already queued for
        // now+4, followed by T/U retries in new registration order 3/4.
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/train-fixtures.json")));
        var literal=doc.RootElement.GetProperty("groups").GetProperty("queue").GetProperty("cases")[0];
        var expected=literal.GetProperty("expected");var due=3424L;
        q=q with{AbsoluteMinute=3419,NextOrder=3,Pending=[q.Pending[0] with{DueMinute=3420},q.Pending[1] with{DueMinute=3420},site with{DueMinute=due}]};
        s=Session(s.World,Callback,q);s.AdvanceOneMinute();
        Assert.Equal(s.Fleet!.Trains.Select(t=>t.Id),calls);
        var pending=s.Clock.Capture().Pending;
        Assert.Equal(new[]{site.HandlerId}.Concat(calls.Select(MixedSessionState.TrainHandler)),pending.Select(a=>a.HandlerId));
        Assert.Equal(expected.GetProperty("pending").EnumerateArray().Select(e=>e.GetProperty("order").GetInt64()),pending.Select(a=>a.Order));
        Assert.All(pending,a=>Assert.Equal(due,a.DueMinute));Assert.Equal(5,s.Clock.Capture().NextOrder);
        var frozen=SaveCodec.ReadWorld(SaveCodec.Write(s),TerrainProfiles.Resolve,RailwayProfiles.Resolve,Callback).Mixed!;
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(frozen));
        Assert.Equal(s.Clock.Capture().Pending.AsEnumerable(),frozen.Clock.Capture().Pending.AsEnumerable());
        for(var n=0;n<120;n++){s.AdvanceOneMinute();frozen.AdvanceOneMinute();}
        Assert.Equal(JsonSerializer.Serialize(s.Random.Capture()),JsonSerializer.Serialize(frozen.Random.Capture()));
        Assert.Equal(s.Clock.Capture().Pending.AsEnumerable(),frozen.Clock.Capture().Pending.AsEnumerable());
        Assert.Equal(s.World.Voxels.AsEnumerable(),frozen.World.Voxels.AsEnumerable());Assert.Equal(s.Construction.Accounts.Cash,frozen.Construction.Accounts.Cash);
    }

    [Fact]
    public void Actual_fleet_limits_apply_to_inventory_and_pack_mismatches_never_enter_world()
    {
        var w=Empty();var f=w.Fleet!;long next=w.Spatial.NextEntityId;
        var rows=Enumerable.Range(0,128).Select(_=>{var t=Train(next,next+1,f,new FleetCarState.Unplaced(),8);next+=9;return t;}).ToArray();
        var graph=Graph(f,rows,f.Groups.Select(g=>g with{Revision=1,Members=rows.Select(t=>t.Id).ToImmutableArray()}));
        var full=w.Replace(w.Revision,w.Terrain,new(next,[]),[],[],fleet:graph);
        Assert.Equal(1024,full.Fleet!.Trains.Sum(t=>t.Cars.Length));
        var state=Session(full);MixedWire.ValidateAdmission(state);
        var bytes=SaveCodec.Write(state);
        Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
        Assert.Throws<ArgumentException>(()=>Graph(f,rows.Append(Train(next,next+1,f,new FleetCarState.Unplaced()))));
        var extra=rows.ToArray();extra[0]=Train(rows[0].Id,next,f,new FleetCarState.Unplaced(),9);
        Assert.Throws<ArgumentException>(()=>Graph(f,extra));
        var profile=new FleetProfile(f.Profile.Packs.Add(new("unselected","1",new string('0',64))),f.Profile.Trains,f.Profile.Providers);
        Assert.Throws<ArgumentException>(()=>w.Replace(w.Revision,w.Terrain,new(w.Spatial.NextEntityId+3,[]),[],[],
            fleet:FleetGraph.Empty(profile,w.Spatial.NextEntityId)));
    }

    [Fact]
    public void Callback_can_publish_occupancy_fleet_and_accounting_then_cancel_its_own_continuation()
    {
        var w=Add(Empty(),new FleetCarState.Inside(P,new(2)));
        TrainCallbackResult Callback(MixedSessionState s,long id)
        {
            var t=s.Fleet!.Train(id);
            var candidate=Change(s.World,t with{Revision=t.Revision+1,State=TrainOperationalState.Unplaced,
                Cars=t.Cars.Select(c=>c with{Revision=c.Revision+1,State=new FleetCarState.Unplaced()}).ToImmutableArray()},true);
            var accounts=s.Construction.Accounts.Book(ConstructionBooking.Spend(20),candidate.Revision,s.Clock.Time,"controlledCallback");
            return new(new(candidate,accounts),null);
        }
        var s=Session(w,Callback);s.AdvanceOneMinute();Assert.Empty(s.Clock.Capture().Pending);
        Assert.Null(s.World.At(P)!.Traffic!.Car);Assert.NotNull(s.World.At(P)!.Traffic!.Rail);
        Assert.Equal(TrainOperationalState.Unplaced,s.Fleet!.Trains[0].State);
        Assert.Equal(20,Assert.Single(s.Construction.Accounts.Transactions).Expenditure);
        Assert.NotNull(w.At(P)!.Traffic!.Car);Assert.Same(s.World,s.Freeze().World);
    }

    [Fact]
    public void A_later_train_fault_discards_earlier_successful_construction_callback_in_same_minute()
    {
        var w=Add(Empty(),new FleetCarState.Inside(P,new(2)));
        TrainCallbackResult Fail(MixedSessionState s,long id) => throw new InvalidOperationException("Controlled late fault");
        var s=Session(w,Fail);
        var type=w.Profile.Bindings.First(b=>b.Stationary?.Height==1).Reference.ContributionId;
        s=StationaryConstruction.Commit(s,StationaryConstruction.Preview(s.World,P.Move(new(4)),type));
        var q=s.Clock.Capture();var train=q.Pending.Single(a=>a.HandlerId.StartsWith("train/"));var site=q.Pending.Single(a=>a.HandlerId.StartsWith("construction/"));
        s=Session(s.World,Fail,q with{Pending=[site with{Registration=0,Order=0,DueMinute=3421},train with{Registration=1,Order=1,DueMinute=3421}]});
        var before=s.World;var random=JsonSerializer.Serialize(s.Random.Capture());var revision=s.Revision;
        Assert.Throws<InvalidOperationException>(()=>s.AdvanceOneMinute());
        Assert.Same(before,s.World);Assert.Equal(revision,s.Revision);Assert.Equal(3420,s.Clock.Time.AbsoluteMinute);
        Assert.Equal(random,JsonSerializer.Serialize(s.Random.Capture()));Assert.Empty(s.Construction.Accounts.Transactions);
        Assert.Equal(2,s.Clock.Capture().Pending.Length);
    }

    [Theory]
    [InlineData("throw")][InlineData("missing-next")][InlineData("duplicate-next")][InlineData("overflow")][InlineData("bad-world")]
    public void Bad_train_callback_discards_entire_minute_clock_random_fleet_and_accounts(string bad)
    {
        var w=Add(Empty(),new FleetCarState.Inside(P,new(2)));
        TrainCallbackResult Callback(MixedSessionState s,long id)
        {
            s.Random["construction"].Next(100);
            if(bad=="throw")throw new InvalidOperationException("Controlled fault");
            if(bad=="duplicate-next")s.Clock.Schedule(MixedSessionState.TrainHandler(id),1);
            if(bad=="bad-world")return new(new(new MixedWorld(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision+1)),2);
            return new(s.Construction,bad=="missing-next"?null:bad=="overflow"?long.MaxValue:2);
        }
        var s=Session(w,Callback);var before=s.Clock.Capture();var random=JsonSerializer.Serialize(s.Random.Capture());var revision=s.Revision;
        Assert.ThrowsAny<Exception>(()=>s.AdvanceOneMinute());
        Assert.Same(w,s.World);Assert.Equal(revision,s.Revision);Assert.Equal(before.AbsoluteMinute,s.Clock.Time.AbsoluteMinute);Assert.Equal(before.NextOrder,s.Clock.Capture().NextOrder);Assert.Equal(before.Pending.AsEnumerable(),s.Clock.Capture().Pending.AsEnumerable());
        Assert.Equal(random,JsonSerializer.Serialize(s.Random.Capture()));Assert.Empty(s.Construction.Accounts.Transactions);
    }
}
