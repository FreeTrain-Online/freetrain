using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;

namespace FreeTrain.Online.Tests;

public class StationLifecycleTests
{
    static int Int(JsonElement j,string key) => j.GetProperty(key).GetInt32();
    static string Snapshot(MixedSessionState s) => JsonSerializer.Serialize(new {
        s.Revision, WorldRevision=s.World.Revision, Spatial=s.World.Spatial.Capture(), s.World.Voxels,
        Graph=new {s.World.Stations!.Buildings,s.World.Stations.NextStationName,s.World.Stations.NextPlatformName,
            Platforms=s.World.Stations.Platforms.Select(p=>new {p.Id,p.Revision,p.Kind,p.Location,Direction=p.Direction.Index,p.Length,
                p.Name,p.Roof,p.Lane0,p.Lane1,p.Bell,p.HostId,p.Children,p.Yards,p.DayTimer})},
        Accounts=s.Construction.Accounts.Capture(), Clock=s.Clock.Capture(), Random=s.Random.Capture() });
    static MixedSessionState Session(long now=517, ConstructionAccounts? accounts=null, StationCallback? callback=null, bool authoring=false)
    {
        var w=World(Graph());
        return new(new(w,accounts),TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(64,24,8),0)),
            constructionMode:authoring,clock:new(now,0,[]),stationCallback:callback);
    }
    static MixedWorld Candidate(MixedSessionState s,StationGraph graph)
    {
        var shape=World(graph);
        return s.World.Replace(s.World.Revision,s.World.Terrain,
            shape.Spatial.Capture() with {NextEntityId=Math.Max(s.World.Spatial.NextEntityId,shape.Spatial.NextEntityId)},shape.Voxels,[],stations:graph);
    }
    static MixedSessionState Built(long now=517,StationCallback? callback=null,ConstructionAccounts? accounts=null)
    {
        var s=Session(now,accounts,callback);
        return StationLifecycle.Commit(s,Candidate(s,Graph([Building()],[Platform()])),
            new(StationBooking.BuildingPlacement(Definition).Charges.Concat(StationBooking.PlatformPlacement(4,2,2,0).Charges)),"station-place");
    }
    static MixedConstructionState ControlledTick(MixedSessionState s,string h) => StationLifecycle.Tick(s,h,g=>g);

    [Fact]
    public void All_nineteen_independent_cost_vectors_preserve_source_width_genres_and_no_spend_calls()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"costs"))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");
            ConstructionBooking Quote() => i.GetProperty("kind").GetString() switch {
                "platform" => StationBooking.PlatformPlacement(Int(i,"length"),Int(i,"z"),Int(i,"ground"),Int(i,"water")),
                "platformDaily" => StationBooking.PlatformDaily(Int(i,"length")),
                "demolish" => StationBooking.BuildingRemoval(Int(i,"x"),Int(i,"y")),
                "station" => StationBooking.BuildingPlacement(Definition with {Price=i.GetProperty("amount").GetInt64()}),
                "stationDaily" => StationBooking.BuildingDaily(Definition with {OperationCost=Int(i,"amount")}),
                "lane" => StationBooking.LaneChange,"passage" => StationBooking.PassageChange,"stairs" => StationBooking.StairChange,
                "platformRemove" => StationBooking.PlatformRemoval,_ => throw new Exception("Unknown cost vector.") };
            if(e.TryGetProperty("rejected",out _)) Assert.Throws<OverflowException>(()=>Quote());
            else
            {
                var q=Quote();Assert.Equal(e.GetProperty("amount").GetInt64(),q.Total);
                Assert.Equal(e.GetProperty("booking").GetBoolean(),q.Charges.Length==1);
                Assert.All(q.Charges,c=>Assert.Equal(e.GetProperty("genre").GetString()=="SUBSIDIARIES"?AccountGenre.Subsidiaries:AccountGenre.RailService,c.Genre));
            }
            count++;
        }
        Assert.Equal(19,count);
        Assert.Throws<ArgumentException>(()=>StationBooking.PlatformPlacement(0,2,2,0));
        Assert.Throws<ArgumentException>(()=>StationBooking.PlatformDaily(-1));
        Assert.Throws<ArgumentException>(()=>StationBooking.BuildingRemoval(1,0));
        Assert.Throws<OverflowException>(()=>StationBooking.PlatformPlacement(1,int.MaxValue,int.MinValue,0));
        Assert.Throws<OverflowException>(()=>StationBooking.PlatformPlacement(1,int.MinValue,0,0));
    }
    [Fact]
    public void All_bundled_station_prices_and_upkeep_are_definition_bound_and_quotes_are_pure()
    {
        using var f=Fixtures();var s=Session();var before=Snapshot(s);
        foreach(var row in Cases(f,"stations"))
        {
            var d=Profile.Definitions.Single(d=>d.Reference.ContributionId==row.GetProperty("id").GetString());
            var e=row.GetProperty("expected");
            Assert.Equal(e.GetProperty("price").GetInt64(),StationBooking.BuildingPlacement(d).Total);
            Assert.Equal(e.GetProperty("operationCost").GetInt64(),StationBooking.BuildingDaily(d).Total);
        }
        Assert.Equal(before,Snapshot(s));
        Assert.Single(StationBooking.PlatformPlacement(4,2,2,4).Charges);
        Assert.Empty(StationBooking.PlatformRemoval.Charges);
    }
    [Fact]
    public void Four_literal_timer_vectors_register_immediately_and_cancel_only_the_removed_owners()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"timers"))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var now=Int(i,"now");
            var neighbor=new ScheduledAction(0,0,now+10,"neighbor",0);
            var state=new ClockState(now,1,[neighbor]);var graph=Graph([Building()],[Platform()]);
            var next=StationLifecycle.Reconcile(null,graph,state);
            Assert.Null(graph.Building(1).HourTimer);Assert.Single(state.Pending);
            void Compare(JsonElement expected,StationSchedule schedule)
            {
                var timers=StationLifecycle.Timers(schedule.Graph).OrderBy(t=>t.Order).ToArray();
                var rows=expected.EnumerateArray().ToArray();Assert.Equal(rows.Length,timers.Length);
                for(var n=0;n<rows.Length;n++)
                {
                    var timer=timers[n];var r=rows[n];
                    var handler=r.GetProperty("handler").GetString() switch {"hour"=>"station/1/hour","day"=>"station/1/day",_=>"platform/2/day"};
                    Assert.Equal(handler,timer.HandlerId);Assert.Equal(Int(r,"due"),timer.DueMinute);
                    Assert.Equal(Int(r,"period"),timer.Interval);Assert.Equal(Int(r,"order"),timer.Order);
                    Assert.Contains(timer,schedule.Clock.Pending);
                }
                Assert.Contains(neighbor,schedule.Clock.Pending);
            }
            Compare(e.GetProperty("pending"),next);
            var cancel=i.GetProperty("cancel").EnumerateArray().Select(v=>v.GetString()).ToArray();
            var remaining=Graph(cancel.Contains("hour")?[]:next.Graph.Buildings,cancel.Contains("platform")?[]:next.Graph.Platforms);
            var removed=StationLifecycle.Reconcile(next.Graph,remaining,next.Clock);
            Compare(e.GetProperty("afterRemoval"),removed);Assert.Equal(next.Clock.NextOrder,removed.Clock.NextOrder);count++;
        }
        Assert.Equal(4,count);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void One_detached_successor_contains_immediate_geometry_booking_and_both_timer_owners(bool authoring)
    {
        var s=Session(authoring:authoring);var before=Snapshot(s);var candidate=Candidate(s,Graph([Building()],[Platform()]));
        var cost=new ConstructionBooking(StationBooking.BuildingPlacement(Definition).Charges.Concat(StationBooking.PlatformPlacement(4,2,2,0).Charges));
        var after=StationLifecycle.Commit(s,candidate,cost,"station-place");
        Assert.Equal(before,Snapshot(s));Assert.Equal(1,after.World.Revision);Assert.Equal(1,after.Revision);
        Assert.Equal(authoring,after.ConstructionMode);Assert.Equal(2,after.Construction.Accounts.Transactions.Length);
        Assert.All(after.Construction.Accounts.Transactions,t=>{Assert.Equal(1,t.WorldRevision);Assert.Equal(517,t.AbsoluteMinute);});
        Assert.Equal(3,after.Clock.Capture().Pending.Length);Assert.Empty(StationaryConstruction.Pending(after.World));
        Assert.Equal(540,after.World.Stations!.Building(1).HourTimer!.DueMinute);
        Assert.Equal(1440,after.World.Stations.Building(1).DayTimer!.DueMinute);
        Assert.Equal(1957,after.World.Stations.Platform(2).DayTimer!.DueMinute);
        Assert.Equal(JsonSerializer.Serialize(s.Random.Capture()),JsonSerializer.Serialize(after.Random.Capture()));
        Assert.Equal(Snapshot(after),Snapshot(after.Freeze()));
    }
    [Fact]
    public void Removal_cancels_station_and_platform_independently_with_exact_booking()
    {
        var s=Built();var g=s.World.Stations!;var before=Snapshot(s);
        var removed=StationLifecycle.Commit(s,Candidate(s,Graph(platforms:g.Platforms)),StationBooking.BuildingRemoval(1,1),"station-remove");
        Assert.Equal(before,Snapshot(s));Assert.Single(removed.Clock.Capture().Pending);
        Assert.Equal(g.Platform(2).DayTimer,removed.Clock.Capture().Pending[0]);
        Assert.Equal(AccountGenre.Subsidiaries,removed.Construction.Accounts.Transactions[^1].Genre);
        var candidate=removed.World;var graph=Graph();
        // Platform removal restores the existing Traffic/normal rails and retains IDs.
        var spatial=candidate.Spatial.Capture() with {Entities=candidate.Spatial.Entities.Select(e=>e with{Revision=e.Revision+1}).ToImmutableArray()};
        var world=candidate.Replace(candidate.Revision,candidate.Terrain,spatial,candidate.Voxels.Select(v=>v with{Traffic=v.Traffic! with{Platform=null}}),[],stations:graph);
        var empty=StationLifecycle.Commit(removed,world,StationBooking.PlatformRemoval,"platform-remove");
        Assert.Empty(empty.Clock.Capture().Pending);Assert.Equal(removed.Construction.Accounts.Capture(),empty.Construction.Accounts.Capture());
        Assert.Equal(4,empty.World.Voxels.Length);Assert.All(empty.World.Voxels,v=>Assert.Null(v.Traffic!.Platform));
    }
    [Fact]
    public void Equal_midnight_hour_day_and_platform_callbacks_repeat_in_FIFO_order_with_stable_cookies()
    {
        var trace=new List<string>();
        var s=Built(0,(session,handler)=>{trace.Add(handler);return ControlledTick(session,handler);});
        var registrations=s.Clock.Capture().Pending.ToDictionary(t=>t.HandlerId,t=>t.Registration);
        for(var minute=0;minute<1440;minute++) s.AdvanceOneMinute();
        Assert.Equal(new[]{"station/1/day","platform/2/day","station/1/hour"},trace.TakeLast(3));
        var charges=s.Construction.Accounts.Transactions.TakeLast(2).ToArray();
        Assert.Equal(new long[]{Definition.OperationCost,720_000},charges.Select(t=>t.Expenditure));
        Assert.All(charges,t=>Assert.Equal(1440,t.AbsoluteMinute));
        foreach(var timer in StationLifecycle.Timers(s.World.Stations))
        {
            Assert.Equal(registrations[timer.HandlerId],timer.Registration);Assert.Contains(timer,s.Clock.Capture().Pending);
            Assert.Equal(timer.HandlerId.EndsWith("/hour")?1500:2880,timer.DueMinute);
        }
        Assert.Equal(Snapshot(s),Snapshot(s.Freeze()));
    }
    [Fact]
    public void Relative_platform_upkeep_is_not_shifted_to_midnight()
    {
        var s=Built(callback:ControlledTick);
        for(var minute=517;minute<1956;minute++) s.AdvanceOneMinute();
        Assert.DoesNotContain(s.Construction.Accounts.Transactions,t=>t.Operation=="platform/2/day");
        s.AdvanceOneMinute();var last=s.Construction.Accounts.Transactions[^1];
        Assert.Equal("platform/2/day",last.Operation);Assert.Equal(1957,last.AbsoluteMinute);Assert.Equal(720_000,last.Expenditure);
        Assert.Equal(3397,s.World.Stations!.Platform(2).DayTimer!.DueMinute);
    }
    [Fact]
    public void Late_callback_failure_discards_earlier_same_minute_bookings_queue_geometry_and_random_draws()
    {
        var fail=false;
        var s=Built(0,(session,handler)=> {
            if(handler.EndsWith("/hour") && fail){session.Random["construction"].Next(100);throw new OverflowException("late callback");}
            return ControlledTick(session,handler);
        });
        for(var minute=0;minute<1439;minute++) s.AdvanceOneMinute();
        fail=true;
        var before=Snapshot(s);Assert.Throws<OverflowException>(()=>s.AdvanceOneMinute());Assert.Equal(before,Snapshot(s));
        fail=false;s.AdvanceOneMinute();Assert.Equal(1440,s.Clock.Time.AbsoluteMinute);
        Assert.Equal(new long[]{Definition.OperationCost,720_000},s.Construction.Accounts.Transactions.TakeLast(2).Select(t=>t.Expenditure));
    }
    [Fact]
    public void Unsupported_station_dispatch_and_bad_continuation_reject_without_publishing()
    {
        var s=Built(1439);var before=Snapshot(s);
        Assert.Throws<InvalidOperationException>(()=>s.AdvanceOneMinute());Assert.Equal(before,Snapshot(s));
        var bad=Built(1439,(session,_)=>new(session.World.Replace(session.World.Revision,session.World.Terrain,
            session.World.Spatial.Capture(),session.World.Voxels,session.World.Slopes),session.Construction.Accounts));
        before=Snapshot(bad);Assert.Throws<ArgumentException>(()=>bad.AdvanceOneMinute());Assert.Equal(before,Snapshot(bad));
    }
    [Fact]
    public void Queue_timer_mismatch_alignment_duplicate_unknown_and_rewrite_reject_at_boundaries()
    {
        var s=Built();var before=Snapshot(s);var clock=s.Clock.Capture();var g=s.World.Stations!;
        foreach(var bad in new[]{clock with{Pending=clock.Pending.RemoveAt(0)},clock with{Pending=clock.Pending.Add(clock.Pending[0])},
            clock with{Pending=clock.Pending.SetItem(0,clock.Pending[0] with{DueMinute=541})},
            clock with{Pending=clock.Pending.Add(new(clock.NextOrder,clock.NextOrder,600,"unknown",0)),NextOrder=clock.NextOrder+1}})
            Assert.Throws<ArgumentException>(()=>new MixedSessionState(s.Construction,s.Provenance,clock:bad,revision:s.Revision));
        var building=g.Building(1);var wrong=building with {HourTimer=building.HourTimer! with{DueMinute=541}};
        var graph=Graph([wrong],g.Platforms);
        Assert.Throws<ArgumentException>(()=>StationLifecycle.Validate(graph,clock with{Pending=clock.Pending.SetItem(0,wrong.HourTimer!)}));
        Assert.Throws<ArgumentException>(()=>StationLifecycle.Reconcile(g,Graph([building with{HourTimer=null,DayTimer=null}],g.Platforms),clock));
        Assert.Throws<ArgumentException>(()=>StationLifecycle.Reconcile(null,g,clock));
        Assert.Equal(before,Snapshot(s));
    }
    [Fact]
    public void Successor_cannot_drop_retained_timers_or_publish_new_inactive_owners()
    {
        var s=Built();var g=s.World.Stations!;var b=g.Building(1);
        var candidate=Candidate(s,Graph([b with{Revision=b.Revision+1,HourTimer=null,DayTimer=null}],g.Platforms));
        var clock=s.Clock.Capture() with {Pending=[g.Platform(2).DayTimer!]};
        var before=Snapshot(s);
        Assert.Throws<ArgumentException>(()=>s.Successor(s.Revision,new(candidate,s.Construction.Accounts),false,clock:clock));
        Assert.Equal(before,Snapshot(s));
        var empty=Session();candidate=Candidate(empty,Graph([Building()]));
        Assert.Throws<ArgumentException>(()=>empty.Successor(empty.Revision,new(candidate,empty.Construction.Accounts),false));
    }
    [Fact]
    public void Station_train_and_construction_share_one_FIFO_and_first_purchase_preserves_station_dispatch()
    {
        var trace=new List<string>();
        var s=Built(1400,(session,handler)=>{trace.Add(handler);return ControlledTick(session,handler);});
        var stock=RollingStockCatalog.Bundled.Trains.First();
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,1,1));
        var type=s.World.Profile.Bindings.First(b=>b.Stationary?.Height==1).Reference.ContributionId;
        var plan=StationaryConstruction.Preview(s.World,new(14,14,2),type);Assert.True(plan.Legal,plan.Reason);
        s=StationaryConstruction.Commit(s,plan);
        var f=s.Fleet!;var t=f.Trains[0];
        t=t with{Revision=t.Revision+1,State=TrainOperationalState.Moving,
            Cars=t.Cars.Select(c=>c with{Revision=c.Revision+1,State=new FleetCarState.Outside(new(0,0,2),Heading.East,100)}).ToImmutableArray()};
        var fleet=new FleetGraph(f.Profile,f.RootGroupId,[t],f.Groups,f.Controllers);
        var world=s.World.Replace(s.World.Revision,s.World.Terrain,s.World.Spatial.Capture(),s.World.Voxels,s.World.Slopes,fleet:fleet);
        var q=s.Clock.Capture();var site=q.Pending.Single(a=>a.HandlerId.StartsWith("construction/"));
        var pending=q.Pending.Select(a=>a==site?a with{DueMinute=1440}:a)
            .Append(new(q.NextOrder,q.NextOrder,1440,MixedSessionState.TrainHandler(t.Id),0)).ToImmutableArray();
        var rng=RandomStreams.Restore(s.Random.Capture());rng["construction"].Next(80);var expectedDraw=rng["construction"].Next(1000);
        TrainCallbackResult Wait(MixedSessionState session,long id)
        {
            trace.Add("train");Assert.Equal(2,session.World.Stations!.Building(1).Revision);
            Assert.Equal(expectedDraw,session.Random["construction"].Next(1000));
            return new(session.Construction,4);
        }
        s=new(new(world,s.Construction.Accounts),s.Provenance,clock:new(1439,q.NextOrder+1,pending),random:s.Random.Capture(),
            revision:world.Revision,trainCallback:Wait,stationCallback:s.StationDispatcher);
        s.AdvanceOneMinute();
        Assert.Equal(new[]{"station/1/hour","station/1/day","train"},trace);
        var after=s.Clock.Capture().Pending;
        Assert.Equal(5,after.Single(a=>a.HandlerId=="station/1/hour").Order);
        Assert.Equal(6,after.Single(a=>a.HandlerId=="station/1/day").Order);
        Assert.Equal(7,after.Single(a=>a.HandlerId==site.HandlerId).Order);
        Assert.Equal(8,after.Single(a=>a.HandlerId==MixedSessionState.TrainHandler(t.Id)).Order);
        Assert.Equal(5,after.Length);
    }
    [Fact]
    public void Late_booking_overflow_and_clock_range_or_capacity_failure_leave_every_original_subset_unchanged()
    {
        var s=Session(accounts:new(long.MinValue));var before=Snapshot(s);var candidate=Candidate(s,Graph([Building()],[Platform()]));
        Assert.Throws<OverflowException>(()=>StationLifecycle.Commit(s,candidate,ConstructionBooking.Spend(1),"overflow"));Assert.Equal(before,Snapshot(s));
        var atEnd=Session(GameTime.MaximumMinute-1);before=Snapshot(atEnd);
        Assert.Throws<ArgumentOutOfRangeException>(()=>StationLifecycle.Commit(atEnd,Candidate(atEnd,Graph([Building()])),ConstructionBooking.None,"range"));
        Assert.Equal(before,Snapshot(atEnd));
        var queue=Enumerable.Range(0,Clock.MaximumPending-1).Select(n=>new ScheduledAction(n,n,600,"neighbor/"+n,0)).ToImmutableArray();
        var state=new ClockState(517,queue.Length,queue);var encoded=JsonSerializer.Serialize(state);
        Assert.Throws<InvalidOperationException>(()=>StationLifecycle.Reconcile(null,Graph([Building()]),state));
        Assert.Equal(encoded,JsonSerializer.Serialize(state));
        Assert.Throws<OverflowException>(()=>StationLifecycle.Reconcile(null,Graph([Building()]),new(517,long.MaxValue-1,[])));
    }
}
