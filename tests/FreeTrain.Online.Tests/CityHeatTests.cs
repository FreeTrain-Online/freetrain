using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class CityHeatTests
{
    static RailwayProfile Rail=>RailwayProfiles.Bundled;
    static IEnumerable<JsonElement> Cases(JsonDocument d,string group)=>d.RootElement.GetProperty("groups").GetProperty(group).GetProperty("cases").EnumerateArray();
    static JsonDocument Fixtures()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/city-fixtures.json")));
    static MixedSessionState Empty()
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Heat",new(16,16,16),2);
        var terrain=TerrainWorld.Empty(request,TerrainProfiles.Bundled);
        return new(new(MixedWorld.FromTerrain(Rail,terrain)),TerrainProvenance.From(request));
    }
    static MixedSessionState Read(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static MixedSessionState Replace(MixedSessionState s,CityState state,ClockState? clock=null)=>new(new(new MixedWorld(s.World.Profile,s.World.Terrain,
        s.World.Spatial.Capture(),s.World.Voxels,s.World.Slopes,s.World.Revision,city:new(s.World.Profile.City!,state))),s.Provenance,
        clock:clock??s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);
    [Fact]
    public void Every_independent_heat_trace_matches_exact_Single_buffers_and_public_power_truncation()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var row in Cases(fixtures,"heat"))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var size=input.GetProperty("size");var heat=CityHeatState.Empty(new(size[0].GetInt32(),size[1].GetInt32(),16));
            foreach(var rho in input.GetProperty("rho").EnumerateArray()) heat=heat with {Rho=heat.Rho.SetItem(heat.Index(rho[0].GetInt32(),rho[1].GetInt32()),rho[2].GetSingle())};
            var n=0;
            foreach(var action in input.GetProperty("actions").EnumerateArray())
            {
                if(action.GetProperty("kind").GetString()=="deposit")
                {
                    var p=action.GetProperty("hv");heat=CityHeat.Deposit(heat,new(UpdateFrequencyHours:action.GetProperty("frequency").GetInt32()),p[0].GetInt32(),p[1].GetInt32(),action.GetProperty("delta").GetSingle());
                }
                else heat=CityHeat.Diffuse(heat,new());
                var trace=expected.GetProperty("trace")[n++];var i=0;
                foreach(var p in input.GetProperty("probes").EnumerateArray())
                {
                    var at=heat.Index(p[0].GetInt32(),p[1].GetInt32());
                    Assert.Equal(trace.GetProperty("q")[i].GetSingle(),heat.Q[at]);
                    Assert.Equal(trace.GetProperty("back")[i++].GetSingle(),heat.Back[at]);
                }
            }
            var j=0;foreach(var p in input.GetProperty("publicReads").EnumerateArray())
                Assert.Equal(expected.GetProperty("publicValue")[j++].GetInt32(),CityHeat.Value(heat,new(),p[0].GetInt32(),p[1].GetInt32()));
            count++;
        }
        Assert.Equal(11,count);
    }
    [Fact]
    public void All_independent_HV_vectors_preserve_distinct_read_and_deposit_indexes_and_source_bounds()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var row in Cases(fixtures,"coordinates"))
        {
            var input=row.GetProperty("input");var xy=input.GetProperty("xy");var expected=row.GetProperty("expected").GetProperty("hv");
            var space=new WorldSpace(16,input.GetProperty("worldV").GetInt32(),16);var p=new Location(xy[0].GetInt32(),xy[1].GetInt32(),2);var hv=space.ToStorage(p);
            Assert.Equal(expected[0].GetInt32(),hv.H);Assert.Equal(expected[1].GetInt32(),hv.V);
            var heat=CityHeatState.Empty(space);
            if(hv.H<0||hv.V<0)Assert.Throws<ArgumentOutOfRangeException>(()=>CityHeat.Deposit(heat,new(),hv.H,hv.V,4));
            else
            {
                heat=CityHeat.Deposit(heat,new(),hv.H,hv.V,4);
                Assert.Equal(10,heat.Q[heat.Index(hv.H,hv.V)]);Assert.Equal(0,CityHeat.Value(heat,new(),hv.H,hv.V));
            }
            count++;
        }
        Assert.Equal(8,count);
    }
    [Fact]
    public void New_fields_timer_captured_interval_buffer_borders_and_worlds_continue_exactly_after_reopen()
    {
        var before=CityHeat.Enable(Empty());var initial=SaveCodec.Write(before);var state=before.World.City!.State;
        Assert.Equal(600,state.HeatCallback!.Interval);Assert.Equal(before.Clock.Time.AbsoluteMinute+600,state.HeatCallback.DueMinute);
        Assert.All(state.Heat.Q,v=>Assert.Equal(0,v));Assert.All(state.Heat.Back,v=>Assert.Equal(0,v));
        Assert.Equal(0,state.Heat.Rho[state.Heat.Index(0,0)]);Assert.Equal(0.8f,state.Heat.Rho[state.Heat.Index(16,16)]);
        var heat=CityHeat.Deposit(state.Heat,state.Settings,2,2,4);
        heat=heat with {Q=heat.Q.SetItem(heat.Index(0,0),3),Back=heat.Back.SetItem(heat.Index(0,0),7)};
        before=Replace(before,state with {Heat=heat,Settings=state.Settings with {UpdateFrequencyHours=3}});
        var reopened=Read(SaveCodec.Write(before));var isolated=CityHeat.Enable(Empty());
        for(var i=0;i<1200;i++)
        {
            before.AdvanceOneMinute();reopened.AdvanceOneMinute();
            if(i is 598 or 599 or 600 or 1199)Assert.Equal(SaveCodec.Write(before),SaveCodec.Write(reopened));
        }
        var after=before.World.City!.State;
        Assert.Equal(600,after.HeatCallback!.Interval);Assert.Equal(state.HeatCallback.Registration,after.HeatCallback.Registration);
        Assert.Equal(3,after.Heat.Q[heat.Index(0,0)]);Assert.Equal(7,after.Heat.Back[heat.Index(0,0)]);
        Assert.Equal(initial,SaveCodec.Write(isolated));Assert.Equal(JsonSerializer.Serialize(before.Random.Capture()),JsonSerializer.Serialize(reopened.Random.Capture()));
    }
    [Fact]
    public void Deposit_clamp_power_and_diffusion_failures_are_checked_without_mutating_prior_buffers()
    {
        var heat=CityHeatState.Empty(new(3,3,8));var settings=new CityDevelopmentSettings();
        var negative=CityHeat.Deposit(heat,settings,2,2,-4);Assert.Equal(-10,negative.Q[negative.Index(2,2)]);
        Assert.Throws<ArgumentException>(()=>CityHeat.Value(negative,settings,1,1));
        Assert.Equal(0,CityHeat.Diffuse(negative,settings).Q[negative.Index(2,2)]);
        Assert.Throws<ArgumentException>(()=>CityHeat.Deposit(heat,settings,0,0,float.MaxValue));
        Assert.Throws<ArgumentException>(()=>CityHeat.Deposit(heat,settings,0,0,float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(()=>CityHeat.Deposit(heat,settings,-1,0,1));
        var huge=heat with {Q=heat.Q.SetItem(heat.Index(2,2),float.MaxValue)};
        Assert.Throws<OverflowException>(()=>CityHeat.Value(huge,settings with {LandValuePower=1},1,1));
        Assert.Throws<ArgumentException>(()=>CityHeat.Diffuse(huge,settings with {Alpha=float.MaxValue}));
        Assert.All(heat.Q,v=>Assert.Equal(0,v));
        Assert.Equal(10,CityHeat.Value(heat with {Q=heat.Q.SetItem(heat.Index(1,1),8)},settings,0,0));
    }
    [Fact]
    public void Failed_diffusion_and_later_same_minute_consumer_restore_complete_save_and_random_queue()
    {
        var s=CityHeat.Enable(Empty());var city=s.World.City!.State;var minute=s.Clock.Time.AbsoluteMinute;
        var heat=city.Heat with {Q=city.Heat.Q.SetItem(city.Heat.Index(2,2),float.MaxValue)};
        var timer=city.HeatCallback! with {DueMinute=minute+1};
        s=Replace(s,city with {Heat=heat,Settings=city.Settings with {Alpha=float.MaxValue},HeatCallback=timer},new(minute,1,[timer]));
        var bytes=SaveCodec.Write(s);Assert.Throws<ArgumentException>(()=>s.AdvanceOneMinute());Assert.Equal(bytes,SaveCodec.Write(s));
        var later=new ScheduledAction(1,1,minute+1,CityHandlers.Development,60);
        s=Replace(s,city with {HeatCallback=timer,DevelopmentCallback=later},new(minute,2,[timer,later]));
        bytes=SaveCodec.Write(s);Assert.Throws<CityConsumerUnavailableException>(()=>s.AdvanceOneMinute());Assert.Equal(bytes,SaveCodec.Write(s));
    }
    [Fact]
    public void Actual_terrain_notifications_rescan_only_changed_voxels_and_preview_is_inert()
    {
        var s=CityHeat.Enable(Empty());var before=SaveCodec.Write(s);var p=new Location(10,10,2);
        var draft=MixedTerrain.Draft(s.World);var plan=draft.Preview(p,"raise");Assert.True(plan.Legal);Assert.Equal(before,SaveCodec.Write(s));
        draft.Apply(plan);var candidate=MixedTerrain.Replace(s.Construction,draft.Finish(),s.Clock.Time);
        var after=s.Successor(s.Revision,candidate,false);Assert.Equal(before,SaveCodec.Write(s));
        Assert.All(plan.Changes,c=>{
            var hv=after.World.Terrain.Space.ToStorage(c.Position);
            Assert.Equal(0.4f,after.World.City!.State.Heat.Rho[cityIndex(hv)]);
        });
        int cityIndex(StoragePosition hv)=>after.World.City!.State.Heat.Index(hv.H,hv.V);
        Assert.Equal(0.8f,after.World.City!.State.Heat.Rho[after.World.City.State.Heat.Index(15,15)]);
        Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Read(SaveCodec.Write(after))));
    }
    static MixedWorld Column(MixedWorld basis, IEnumerable<(int Z, RoadComponent? Road)> slots)
    {
        var traffic=slots.Select((s,i)=>new RailwayVoxel(new Location(10,10,s.Z),i+10,RailwayVoxelKind.Traffic,
            new TrafficComponents(Rail:new(Rail.Bindings.First(b=>b.Handler==RailwayHandler.Normal&&b.Variant==0).Reference,0),Road:s.Road))).ToImmutableArray();
        var entities=basis.Terrain.Spatial.Entities.AddRange(traffic.Select(v=>new SpatialEntity(v.OwnerId,0,basis.Profile.TrafficOwner,v.Position,
            Footprint.Box(new(new(0,0,0),1,1,1),true),true,false,0)));
        return new(basis.Profile,basis.Terrain,new(30,entities),traffic,[],basis.Revision,city:basis.City);
    }
    [Fact]
    public void Every_independent_conductivity_vector_scans_real_terrain_and_Traffic_in_source_order()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var row in Cases(fixtures,"conductivity"))
        {
            var input=row.GetProperty("input");var slots=input.GetProperty("column").EnumerateArray().ToArray();var s=CityHeat.Enable(Empty());
            var t=s.World.Terrain;
            if(slots.Any(x=>x.GetProperty("kind").GetString()=="mountain"))
            {
                var draft=MixedTerrain.Draft(s.World);draft.Apply(draft.Preview(new(10,10,2),"raise"));t=draft.Finish();
            }
            t=new(t.Profile,t.Name,new(16,16,16),input.GetProperty("water").GetInt32()+2,t.Cells,t.Spatial.Capture(),t.Occupants,t.Revision);
            var roads=Rail.City!.Roads.ToArray();var roadSlots=new List<(int,RoadComponent?)>();var n=0;
            foreach(var slot in slots.Where(x=>x.GetProperty("kind").GetString()=="traffic"))
            {
                RoadComponent? component=null;
                if(slot.TryGetProperty("road",out var road)&&road.ValueKind!=JsonValueKind.Null)
                {
                    roads[n]=roads[n] with {Style=road.GetProperty("major").GetInt32()>=2 ? "street":"footpath",
                        Sidewalk=road.GetProperty("pavement").GetBoolean()?"pavement":"shoulder"};
                    component=new(roads[n++].Reference,1);
                }
                roadSlots.Add((4+roadSlots.Count,component));
            }
            var city=new CityProfile(Rail.City.Packs,Rail.City.Bindings,Rail.City.Structures,roads,Rail.City.Ground);
            var rail=new RailwayProfile(t.Profile,Rail.Bindings,Rail.FleetProfile,Rail.Stations,city);
            var basis=new MixedWorld(rail,t,t.Spatial.Capture(),[],[],t.Revision,city:CityGraph.Empty(city,t.Space));
            Assert.Equal(row.GetProperty("expected").GetProperty("rho").GetSingle(),CityHeat.Conductivity(Column(basis,roadSlots),new(10,10,2)));count++;
        }
        Assert.Equal(9,count);
    }
    [Fact]
    public void Traffic_road_attach_notifies_but_detach_with_rail_and_outlook_changes_retain_stale_conductivity()
    {
        var empty=CityHeat.Enable(Empty()).World;var road=new RoadComponent(Rail.City!.Roads[0].Reference,1);
        var before=Column(empty,[(4,road)]);var hv=before.Terrain.Space.ToStorage(new(10,10,4));var i=before.City!.State.Heat.Index(hv.H,hv.V);
        // A retained Traffic's null road setter only invalidates its outlook.
        var voxel=before.Voxels.Single();var entity=before.Spatial.Entities.Single(e=>e.Id==voxel.OwnerId);
        var state=before.City.State with {Heat=before.City.State.Heat with {Rho=before.City.State.Heat.Rho.SetItem(i,0.123f)}};
        before=new(before.Profile,before.Terrain,before.Spatial.Capture(),before.Voxels,[],city:new(before.City.Profile,state));
        var removed=before.Replace(before.Revision,before.Terrain,before.Spatial.Capture() with {Entities=before.Spatial.Entities.Replace(entity,entity with {Revision=1})},
            [voxel with {Traffic=voxel.Traffic! with {Road=null}}],[]);
        Assert.Equal(0.123f,removed.City!.State.Heat.Rho[i]);
        var attached=removed.Replace(removed.Revision,removed.Terrain,removed.Spatial.Capture() with {Entities=removed.Spatial.Entities.Select(e=>e with {Revision=2}).ToImmutableArray()},[voxel],[]);
        Assert.Equal(CityHeat.Conductivity(attached,voxel.Position),attached.City!.State.Heat.Rho[i]);
        var gone=attached.Replace(attached.Revision,attached.Terrain,new(30,[]),[],[]);
        Assert.Equal(0.8f,gone.City!.State.Heat.Rho[i]);
    }
    [Fact]
    public void Service_reachable_fixture_emits_exact_diffusion_continuation_without_a_controlled_dispatcher()
    {
        var s=CityHeat.Enable(Empty());var world=CityHeat.Deposit(s.World,s.World.Terrain.Space.ToLocation(new(2,2,2)),4);
        s=s.Successor(s.Revision,new(world,s.Construction.Accounts),false);
        for(var i=0;i<599;i++)s.AdvanceOneMinute();
        var bytes=SaveCodec.Write(s);var path=Environment.GetEnvironmentVariable("FREETRAIN_CITY_HEAT_OUTPUT");
        if(path!=null)File.WriteAllBytes(path,bytes);
        var reopened=Read(bytes);s.AdvanceOneMinute();reopened.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));
        var heat=s.World.City!.State.Heat;
        Assert.Equal(3.7559995651245117f,heat.Q[heat.Index(2,2)]);
        Assert.Equal(1.5360000133514404f,heat.Q[heat.Index(2,1)]);
        if(path!=null)File.WriteAllText(path+".snapshot.json",JsonSerializer.Serialize<FreeTrain.Online.Contracts.WireMessage>(MixedWire.Snapshot(s,1),FreeTrain.Online.Contracts.WireJson.CreateOptions()));
    }

    [Fact]
    public void Construction_uses_actual_center_value_times_area_and_default_live_and_detached_consumers()
    {
        var s=CityHeat.Enable(Empty());var d=Rail.City!.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.SizeX>1&&d.SizeY>1);
        var structure=new CityStructure(1,0,d.Reference,new(8,8,2),d.MinimumHeight,false,null,[],[]);
        var center=new Location(8+d.SizeX/2,8+d.SizeY/2,2);var hv=s.World.Terrain.Space.ToStorage(center);
        var city=s.World.City!.State;
        s=Replace(s,city with {Heat=city.Heat with {Q=city.Heat.Q.SetItem(city.Heat.Index(hv.H+1,hv.V+1),1024)}});
        Assert.Equal(80L*d.SizeX*d.SizeY,CityHeat.TotalLandValue(s.World,structure));
        // No callback adapter is loaded from this save. Completion samples the
        // actual zero field and passes that result to the existing valuation.
        var restored=Read(SaveCodec.Write(CityPersistenceTests.Fixture(false)));
        var detached=CityConstruction.AdvanceOneMinute(new(restored.Construction,restored.Clock.Capture(),restored.Random.Capture()));
        restored.AdvanceOneMinute();
        var complete=restored.World.City!.State.Structures.Single(b=>b.Id>20);
        Assert.Equal(0,complete.Subsidiary!.TotalLandValue);
        Assert.Equal(JsonSerializer.Serialize(restored.World.City.State),JsonSerializer.Serialize(detached.Construction.World.City!.State));
    }

}
