using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class CityConstructionTests
{
    static JsonDocument Fixtures()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/city-fixtures.json")));
    static IEnumerable<JsonElement> Cases(JsonDocument d,string name)=>d.RootElement.GetProperty("groups").GetProperty(name).GetProperty("cases").EnumerateArray();
    static string Json(object x)=>JsonSerializer.Serialize(x);
    static CityStructureDefinition Definition=>RailwayProfiles.Bundled.City!.Structures.First(d=>d.Kind==CityStructureKind.Fixed && d.SizeX==1 && d.SizeY==1 && d.MinimumHeight==1 && d.Population!=null);
    static readonly Location P=new(12,12,2);
    static CityCandidate Empty(long minute=1980,ConstructionAccounts? accounts=null)
    {
        var t=new TerrainWorld(TerrainProfiles.Bundled,"Construction",new(40,32,16),0,
            Enumerable.Repeat(new TerrainCell(2),40*32),new(1,[]),[]);
        var city=CityGraph.Empty(RailwayProfiles.Bundled.City!,t.Space);
        var world=new MixedWorld(RailwayProfiles.Bundled,t,new(1,[]),[],[],city:city);
        return new(new(world,accounts),new(minute,0,[]),new RandomStreams(7).Capture());
    }
    static CityCandidate Start(CityCandidate s,CityConstructionCaller caller=CityConstructionCaller.CatalogPlayer,CityStructureDefinition? d=null,int? height=null)
    { d??=Definition;return CityConstruction.Start(s,P,d.Reference,height??d.MinimumHeight,caller); }
    static CityCandidate Script(CityCandidate s,IEnumerable<(int Bound,int Value)> draws)
    {
        var values=new int[55];var i=0;
        foreach(var (bound,value) in draws) values[i++]=(int)(((value+0.5)/bound)*int.MaxValue);
        Assert.True(i<21);
        var streams=s.Random.Streams.Select(r=>r.Id=="construction"?r with {State=new(0,values.ToImmutableArray())}:r).ToImmutableArray();
        return new(s.Construction,s.Clock,s.Random with {Streams=streams});
    }
    static CityCandidate Finish(CityCandidate s)
    {
        for(var i=0;i<15000 && s.Construction.World.City!.State.Structures.Any(b=>!b.Construction.IsEmpty);i++)
            s=CityConstruction.AdvanceOneMinute(s,(_,_)=>20);
        Assert.All(s.Construction.World.City!.State.Structures,b=>Assert.Empty(b.Construction));return s;
    }
    [Fact]
    public void All_16_independent_gate_vectors_and_five_random_traces_use_shared_production_kernel()
    {
        using var f=Fixtures();var gates=0;var traces=0;
        foreach(var row in Cases(f,"constructionGates"))
        {
            var i=row.GetProperty("input");int N(string k)=>i.GetProperty(k).GetInt32();
            int? Maybe(string k)=>i.GetProperty(k).ValueKind==JsonValueKind.Null?null:N(k);
            Assert.Equal(row.GetProperty("expected").GetProperty("proceed").GetBoolean(),
                ConstructionLifecycle.CanProceed(i.GetProperty("weekend").GetBoolean(),N("hour"),N("state"),Maybe("below"),N("belowMin"),Maybe("above"),N("aboveMin")));gates++;
        }
        foreach(var row in Cases(f,"constructionTraces"))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var calls=new Queue<JsonElement>(expected.GetProperty("random").EnumerateArray());
            int Draw(int bound){var c=calls.Dequeue();Assert.Equal("construction",c.GetProperty("stream").GetString());Assert.Equal(c.GetProperty("bound").GetInt32(),bound);return c.GetProperty("value").GetInt32();}
            var cell=ConstructionLifecycle.Initial(input.GetProperty("southwest").GetBoolean(),Draw);
            var now=input.GetProperty("start").GetInt64()+ConstructionLifecycle.Delay(Draw);
            var allowed=input.GetProperty("allowed").EnumerateArray().ToArray();var n=0;
            foreach(var e in expected.GetProperty("trace").EnumerateArray())
            {
                Assert.Equal(e.GetProperty("minute").GetInt64(),now);
                var name=e.GetProperty("state").GetString();if(name=="abandoned")break;
                if(allowed[n++].GetBoolean())cell=ConstructionLifecycle.Progress(cell,Draw);
                Assert.Equal(Array.IndexOf(new[]{"empty","bone1","bone2","bone3","walled"},name),cell.State);
                Assert.Equal(Array.IndexOf(new[]{"ground","woods","steel","hole","machine1","machine2","machine3"},e.GetProperty("ground").GetString()),cell.Ground);
                if(cell.State<4)now+=ConstructionLifecycle.Delay(Draw);
            }
            Assert.Empty(calls);traces++;
        }
        Assert.Equal(16,gates);Assert.Equal(5,traces);
    }
    [Fact]
    public void Detached_runtime_follows_literal_one_voxel_completion_and_cancels_subsidiary_timer_on_demolition()
    {
        var before=Script(Empty(),[(80,0),(2,1),(80,0),(80,0),(80,0)]);
        var baseline=Json(before.Random);var s=Start(before);Assert.Equal(baseline,Json(before.Random));
        Assert.Empty(s.Construction.World.City!.State.Producers);
        var site=Assert.Single(s.Construction.World.City.State.Structures);
        Assert.False(site.Owned);Assert.Equal(0,s.Construction.World.Spatial.Entities.Single().Value);
        Assert.Equal(2020,Assert.Single(s.Clock.Pending).DueMinute);
        foreach(var state in new[]{1,2,3,4})
        {
            for(var i=0;i<40;i++)s=CityConstruction.AdvanceOneMinute(s,(_,_)=>20);
            var b=Assert.Single(s.Construction.World.City!.State.Structures);
            if(state<4){Assert.Equal(state,b.Construction.Single().Cell.State);Assert.Empty(s.Construction.World.City.State.Producers);}
            else{Assert.Empty(b.Construction);Assert.True(b.Owned);Assert.Equal(2,b.Id);Assert.Equal(2,s.Construction.World.City.State.Producers.Single().OwnerId);Assert.Equal(20,b.Subsidiary!.TotalLandValue);}
        }
        Assert.Equal(2140,s.Clock.AbsoluteMinute);Assert.Single(s.Construction.Accounts.Transactions);
        Assert.Equal(Definition.UnitPrice,s.Construction.Accounts.Transactions[0].Expenditure);
        Assert.StartsWith("city/subsidiary/2",s.Clock.Pending.Single().HandlerId);
        var random=Json(s.Random);s=CityConstruction.Remove(s,2);
        Assert.Equal(random,Json(s.Random));Assert.Empty(s.Clock.Pending);Assert.Empty(s.Construction.World.City!.State.Producers);
        Assert.Equal(500_000,s.Construction.Accounts.Transactions[^1].Expenditure);
        for(var i=0;i<500;i++)s=CityConstruction.AdvanceOneMinute(s,(_,_)=>throw new Exception());
        Assert.Equal(random,Json(s.Random));Assert.Equal(2,s.Construction.Accounts.Transactions.Length);
    }
    [Theory]
    [InlineData(CityConstructionCaller.CatalogPlayer,true,true)]
    [InlineData(CityConstructionCaller.LegacyPlayer,true,false)]
    [InlineData(CityConstructionCaller.Automated,false,false)]
    public void Actual_callers_retain_ownership_booking_and_saved_continuation(CityConstructionCaller caller,bool owned,bool booked)
    {
        var s=Start(Empty(),caller);var w=s.Construction.World;
        var restoredGraph=new CityGraph(w.City!.Profile,JsonSerializer.Deserialize<CityState>(Json(w.City.State))!);
        var restoredWorld=new MixedWorld(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,
            w.SpeedLimits,w.Fleet,w.Stations,restoredGraph);
        var reopened=new CityCandidate(new(restoredWorld,ConstructionAccounts.Restore(
            JsonSerializer.Deserialize<ConstructionAccountState>(Json(s.Construction.Accounts.Capture()))!)),
            JsonSerializer.Deserialize<ClockState>(Json(s.Clock))!,JsonSerializer.Deserialize<RandomStreamsState>(Json(s.Random))!);
        Assert.Equal(booked?1:0,s.Construction.Accounts.Transactions.Length);
        s=Finish(s);reopened=Finish(reopened);
        Assert.Equal(Json(s.Construction.World.City!.State),Json(reopened.Construction.World.City!.State));
        Assert.Equal(Json(s.Clock),Json(reopened.Clock));Assert.Equal(Json(s.Random),Json(reopened.Random));
        Assert.Equal(owned,s.Construction.World.City.State.Structures.Single().Owned);
        Assert.Equal(booked?1:0,s.Construction.Accounts.Transactions.Length);
    }
    [Fact]
    public void Cancellation_removes_every_voxel_callback_without_refund_draw_or_delayed_completion()
    {
        var d=RailwayProfiles.Bundled.City!.Structures.First(d=>d.Kind==CityStructureKind.Variable);
        var s=Start(Empty(),d:d,height:d.MinimumHeight);var random=Json(s.Random);
        Assert.Equal(d.SizeX*d.SizeY*d.MinimumHeight,s.Clock.Pending.Length);
        s=CityConstruction.Remove(s,1);Assert.Empty(s.Clock.Pending);Assert.Empty(s.Construction.World.Spatial.Entities);
        Assert.Single(s.Construction.Accounts.Transactions);Assert.Equal(random,Json(s.Random));
        for(var i=0;i<500;i++)s=CityConstruction.AdvanceOneMinute(s,(_,_)=>throw new Exception());
        Assert.Equal(random,Json(s.Random));Assert.Empty(s.Construction.World.City!.State.Structures);
    }
    [Fact]
    public void Rejected_start_minute_and_demolition_preserve_accounts_allocator_clock_and_random()
    {
        var s=Empty(accounts:new(long.MinValue,new(1)));var saved=Json(s.Random);var clock=Json(s.Clock);
        Assert.Throws<OverflowException>(()=>Start(s));
        Assert.Equal(saved,Json(s.Random));Assert.Equal(clock,Json(s.Clock));Assert.Equal(1,s.Construction.World.Spatial.NextEntityId);
        Assert.Empty(s.Construction.Accounts.Transactions);
        s=Script(Empty(),[(80,0),(2,1),(80,0),(80,0),(80,0)]);s=Start(s);
        for(var i=0;i<159;i++)s=CityConstruction.AdvanceOneMinute(s,(_,_)=>20);
        saved=Json(s.Random);clock=Json(s.Clock);var world=s.Construction.World;
        Assert.Throws<OverflowException>(()=>CityConstruction.AdvanceOneMinute(s,(_,_)=>long.MaxValue));
        Assert.Same(world,s.Construction.World);Assert.Equal(saved,Json(s.Random));Assert.Equal(clock,Json(s.Clock));
        var state=s.Construction.World.City!.State;
        var callback=state.Structures.Single().Construction.Single().Callback!;
        Assert.Throws<ArgumentException>(()=>new CityCandidate(s.Construction,s.Clock with {Pending=[]},s.Random));
        Assert.Throws<ArgumentException>(()=>new CityCandidate(s.Construction,s.Clock with {NextOrder=callback.Order},s.Random));
    }
    [Fact]
    public void Existing_account_limit_and_zero_call_distinction_are_preserved()
    {
        var empty=Empty();var w=empty.Construction.World;
        var accounts=new ConstructionAccounts().Book(new(Enumerable.Repeat(new AccountCharge(AccountGenre.Subsidiaries,0),8192)),1,new(1980),"full");
        w=w.Replace(0,w.Terrain,w.Spatial.Capture(),[],[]);
        var s=new CityCandidate(new(w,accounts),empty.Clock,empty.Random);var random=Json(s.Random);
        Assert.Throws<ArgumentException>(()=>Start(s));Assert.Equal(random,Json(s.Random));
        var legacy=Start(s,CityConstructionCaller.LegacyPlayer);Assert.Equal(8192,legacy.Construction.Accounts.Transactions.Length);
        var zero=Definition with {UnitPrice=0};
        Assert.Single(CityConstruction.StartBooking(zero,1,CityConstructionCaller.CatalogPlayer).Charges);
        Assert.Empty(CityConstruction.StartBooking(zero,1,CityConstructionCaller.LegacyPlayer).Charges);
        Assert.Empty(CityConstruction.RemovalBooking(zero,true).Charges);
        Assert.Throws<OverflowException>(()=>CityConstruction.RemovalBooking(zero with {SizeX=5000},false));
    }
    [Fact]
    public void City_start_reserves_pending_accessory_completion_bookings_and_rejects_long_site_delays()
    {
        var source=StationaryConstructionTests.Empty();
        var binding=source.World.Profile.Bindings.First(b=>b.Handler==RailwayHandler.Stationary);
        source=StationaryConstruction.Commit(source,StationaryConstruction.Preview(source.World,GarageConstructionTests.Start,binding.Reference.ContributionId));
        var w=source.World;
        var graph=CityGraph.Empty(w.Profile.City!,w.Terrain.Space);
        w=w.Replace(w.Revision,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,city:graph);
        var accounts=new ConstructionAccounts().Book(new(Enumerable.Repeat(new AccountCharge(AccountGenre.Subsidiaries,0),8191)),w.Revision,source.Clock.Time,"full");
        var a=new CityCandidate(new(w,accounts),source.Clock.Capture(),source.Random.Capture());
        Assert.Throws<ArgumentException>(()=>Start(a));
        var b=Start(a,CityConstructionCaller.LegacyPlayer);Assert.Equal(8191,b.Construction.Accounts.Transactions.Length);
        Assert.Equal(source.Clock.Capture().Pending.Length+1,b.Clock.Pending.Length);
        var state=b.Construction.World.City!.State;var site=state.Structures.Single();
        var c=site.Construction.Single();var delayed=c.Callback! with {DueMinute=b.Clock.AbsoluteMinute+120};
        graph=new CityGraph(w.Profile.City!,state with {Structures=[site with {Revision=site.Revision+1,Construction=[c with {Callback=delayed}]}]});
        w=b.Construction.World;
        w=w.Replace(w.Revision,w.Terrain,new(w.Spatial.NextEntityId,w.Spatial.Entities.Where(e=>!w.City!.IsSpatialOwner(e.Id))
            .Append(graph.SpatialOwner(site.Id)).ToImmutableArray()),w.Voxels,w.Slopes,city:graph);
        Assert.Throws<ArgumentException>(()=>new CityCandidate(new(w,b.Construction.Accounts),
            b.Clock with {Pending=b.Clock.Pending.Replace(c.Callback!,delayed)},b.Random));
    }
    [Fact]
    public void Queue_admission_counts_existing_timers_and_rejects_the_whole_site_without_draws()
    {
        // Synthetic valid content at the existing world/content maxima; a heat
        // timer leaves only 65,535 slots for this 65,536-voxel site.
        var bundled=RailwayProfiles.Bundled;var original=bundled.City!;
        var definition=Definition with {SizeX=32,SizeY=32,MinimumHeight=64,MaximumHeight=64};
        var profile=new CityProfile(original.Packs,original.Bindings,
            original.Structures.Select(d=>d.Reference==definition.Reference?definition:d),original.Roads,original.Ground);
        var rail=new RailwayProfile(bundled.Terrain,bundled.Bindings,city:profile);
        var terrain=new TerrainWorld(bundled.Terrain,"Queue boundary",new(200,200,64),0,
            Enumerable.Repeat(new TerrainCell(0),200*200),new(1,[]),[]);
        var heat=new ScheduledAction(0,0,2580,CityHandlers.Heat,600);
        var graph=new CityGraph(profile,CityGraph.Empty(profile,terrain.Space).State with {HeatCallback=heat});
        var world=new MixedWorld(rail,terrain,new(1,[]),[],[],city:graph);
        var a=new CityCandidate(new(world),new(1980,1,[heat]),new RandomStreams(7).Capture());
        var clock=Json(a.Clock);var random=Json(a.Random);
        var error=Assert.Throws<ArgumentException>(()=>CityConstruction.Start(a,new(100,100,0),definition.Reference,64,CityConstructionCaller.CatalogPlayer));
        Assert.Contains("queue capacity",error.Message);Assert.Equal(clock,Json(a.Clock));Assert.Equal(random,Json(a.Random));
        Assert.Empty(a.Construction.Accounts.Transactions);Assert.Equal(1,a.Construction.World.Spatial.NextEntityId);
    }
    [Fact]
    public void Variable_completion_uses_selected_height_price_multiplier_and_no_completion_charge()
    {
        var d=RailwayProfiles.Bundled.City!.Structures.First(d=>d.Kind==CityStructureKind.Variable && d.Population!=null);
        var before=Empty();var s=Start(before,d:d,height:d.MinimumHeight);
        Assert.Equal(d.UnitPrice*d.MinimumHeight,s.Construction.Accounts.Transactions.Single().Expenditure);
        var construction=s.Random.Streams.Single(r=>r.Id=="time-length");
        Assert.Equal(Json(before.Random.Streams.Single(r=>r.Id=="time-length")),Json(construction));
        s=Finish(s);var graph=s.Construction.World.City!;var owner=graph.State.Structures.Single();
        Assert.Equal(d.UnitPrice*d.MinimumHeight,s.Construction.World.Spatial.Entities.Single().Value);
        Assert.Equal(d.Population!.Residents*d.MinimumHeight,graph.ProducerPopulation(owner.Id).Residents);
        Assert.Single(s.Construction.Accounts.Transactions);
        Assert.Equal((construction.State.Index+1)%55,s.Random.Streams.Single(r=>r.Id=="time-length").State.Index);
        Assert.InRange(s.Clock.Pending.Single().DueMinute-s.Clock.AbsoluteMinute,20160,40319);
    }
    [Fact]
    public void Literal_eight_voxel_constructor_order_binds_doors_delays_FIFO_and_only_construction_stream()
    {
        using var f=Fixtures();var row=Cases(f,"randomFlows").Single(c=>c.GetProperty("id").GetString()=="random-constructor-order");
        var calls=row.GetProperty("expected").GetProperty("random").EnumerateArray()
            .Select(c=>(c.GetProperty("bound").GetInt32(),c.GetProperty("value").GetInt32())).ToArray();
        var before=Script(Empty(),calls);
        var d=RailwayProfiles.Bundled.City!.Structures.First(d=>d.Kind==CityStructureKind.Fixed && d.SizeX==2 && d.SizeY==2 && d.MinimumHeight==2);
        var after=Start(before,d:d);var site=after.Construction.World.City!.State.Structures.Single();
        var expected=row.GetProperty("expected").GetProperty("result").EnumerateArray().ToArray();
        Assert.Equal(8,site.Construction.Length);
        for(var n=0;n<8;n++)
        {
            var e=expected[n];var c=site.Construction[n];var p=e.GetProperty("cell");
            Assert.Equal(new(p[0].GetInt32(),p[1].GetInt32(),p[2].GetInt32()),c.Offset);
            Assert.Equal(e.GetProperty("door").GetBoolean(),c.Cell.NeedsDoor);
            Assert.Equal(e.GetProperty("due").GetInt64()+1440,c.Callback!.DueMinute);
            Assert.Equal(n,c.Callback.Order);Assert.Equal(n,c.Callback.Registration);
        }
        foreach(var stream in before.Random.Streams.Where(r=>r.Id!="construction"))
            Assert.Equal(Json(stream),Json(after.Random.Streams.Single(r=>r.Id==stream.Id)));
        Assert.Equal(14,after.Random.Streams.Single(r=>r.Id=="construction").State.Index);
        var reopened=new CityCandidate(after.Construction,JsonSerializer.Deserialize<ClockState>(Json(after.Clock))!,
            JsonSerializer.Deserialize<RandomStreamsState>(Json(after.Random))!);
        var complete=Finish(after);var other=Finish(reopened);
        Assert.Equal(Json(complete.Construction.World.City!.State),Json(other.Construction.World.City!.State));
        Assert.Equal(Json(complete.Clock),Json(other.Clock));Assert.Equal(Json(complete.Random),Json(other.Random));
    }
    [Fact]
    public void A_later_equal_time_completion_failure_rolls_back_the_entire_detached_minute()
    {
        var a=Start(Empty());
        a=CityConstruction.Start(a,P with {X=P.X+2},Definition.Reference,1,CityConstructionCaller.Automated);
        var world=a.Construction.World;var state=world.City!.State;
        state=state with {Structures=state.Structures.Select(s=>s with {Revision=s.Revision+1,
            Construction=s.Construction.Select(c=>c with {Cell=c.Cell with {State=3},
                Callback=c.Callback! with {DueMinute=a.Clock.AbsoluteMinute+1}}).ToImmutableArray()}).ToImmutableArray()};
        var graph=new CityGraph(world.City.Profile,state);
        world=world.Replace(world.Revision,world.Terrain,new(world.Spatial.NextEntityId,
            graph.Identities.Select(i=>graph.SpatialOwner(i.Id)).ToImmutableArray()),[],[],city:graph);
        a=new(new(world,a.Construction.Accounts),a.Clock with {Pending=graph.Callbacks},a.Random);
        var clock=Json(a.Clock);var random=Json(a.Random);var original=a.Construction;var calls=new List<long>();
        Assert.Throws<OverflowException>(()=>CityConstruction.AdvanceOneMinute(a,(_,s)=>{calls.Add(s.Id);return s.Id==4?long.MaxValue:20;}));
        Assert.Equal(new long[]{3,4},calls);Assert.Same(original,a.Construction);
        Assert.Equal(clock,Json(a.Clock));Assert.Equal(random,Json(a.Random));
        calls.Clear();var b=CityConstruction.AdvanceOneMinute(a,(_,s)=>{calls.Add(s.Id);return 20;});
        Assert.Equal(new long[]{3,4},calls);Assert.Equal(new long[]{3,4},b.Construction.World.City!.State.Producers.Select(p=>p.OwnerId));
        Assert.Equal(2,b.Clock.Pending.Length);Assert.All(b.Clock.Pending,c=>Assert.StartsWith("city/subsidiary/",c.HandlerId));
        Assert.Single(b.Construction.Accounts.Transactions);
    }
    [Fact]
    public void All_four_literal_FIFO_cancellation_vectors_use_the_existing_clock()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"fifo"))
        {
            var i=row.GetProperty("input");var entries=i.GetProperty("entries").EnumerateArray().ToArray();var seen=new List<string>();
            var clock=new Clock(0,entries.ToDictionary(e=>e.GetProperty("id").GetString()!,e=>(Action)(()=>seen.Add(e.GetProperty("id").GetString()!))));
            var handles=entries.OrderBy(e=>e.GetProperty("registration").GetInt64()).ToDictionary(e=>e.GetProperty("id").GetString()!,e=>clock.ScheduleAt(e.GetProperty("id").GetString()!,e.GetProperty("due").GetInt64()));
            foreach(var id in i.GetProperty("cancel").EnumerateArray())clock.Unregister(handles[id.GetString()!]);
            var saved=clock.Capture();clock=Clock.Restore(saved,entries.ToDictionary(e=>e.GetProperty("id").GetString()!,e=>(Action)(()=>seen.Add(e.GetProperty("id").GetString()!))));
            var maximum=entries.Max(e=>e.GetProperty("due").GetInt64());for(var n=0;n<maximum;n++)clock.Advance();
            Assert.Equal(row.GetProperty("expected").GetProperty("order").EnumerateArray().Select(v=>v.GetString()),seen);count++;
        }
        Assert.Equal(4,count);
    }
}
