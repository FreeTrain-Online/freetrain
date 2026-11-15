using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class FixedCityConstructionTests
{
    static RailwayProfile Rail=>RailwayProfiles.Bundled;
    static CityProfile City=>Rail.City!;
    static readonly Location P=new(14,12,2);
    // Pinned bundled residential declaration: 1x1x1, ¥11m, 10 residents.
    static CityStructureDefinition House=>City.Structures.Single(d=>d.Reference.ContributionId=="{27D6D0C0-2B1A-4494-9257-14C208168075}");
    static CityCandidate Capture(MixedSessionState s)=>new(s.Construction,s.Clock.Capture(),s.Random.Capture());
    static MixedSessionState Empty(bool station=true,int water=0)
    {
        var t=new TerrainWorld(TerrainProfiles.Bundled,"Fixed city",new(40,32,16),water,
            Enumerable.Repeat(new TerrainCell(2),40*32),new(1,[]),[]);
        var graph=CityGraph.Empty(City,t.Space);
        // Actual heat input, independently gives trunc(pow(100,.3f))*10 = 30.
        graph=new(City,graph.State with {Heat=graph.State.Heat with {Q=graph.State.Heat.Q.Select(_=>100f).ToImmutableArray()}});
        var world=new MixedWorld(Rail,t,new(1,[]),[],[],city:graph);
        var s=new MixedSessionState(new(world),TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,t.Name,new(40,32,16),water)),
            clock:new(1980,0,[]),random:new RandomStreams(7).Capture());
        if(station) s=StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,new(12,12,2),StationStateTests.Definition.Reference));
        return s;
    }
    static MixedSessionState Start(MixedSessionState s,CityStructureDefinition? d=null,CityConstructionCaller caller=CityConstructionCaller.CatalogPlayer,Location? p=null) =>
        FixedCityConstruction.Commit(s,FixedCityConstruction.Preview(Capture(s),p??P,(d??House).Reference,caller));
    static MixedSessionState Reopen(MixedSessionState s)=>SaveCodec.ReadWorld(SaveCodec.Write(s),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static MixedSessionState Finish(MixedSessionState s)
    {
        for(var i=0;i<15000 && s.World.City!.State.Structures.Any(o=>!o.Construction.IsEmpty);i++) s.AdvanceOneMinute();
        Assert.All(s.World.City!.State.Structures,o=>Assert.Empty(o.Construction));return s;
    }
    static MixedSessionState WithState(MixedSessionState s,CityState state)
    {
        var graph=new CityGraph(City,state);var w=s.World;
        var entities=w.Spatial.Entities.Where(e=>!w.City!.IsSpatialOwner(e.Id)).Concat(graph.Identities.Select(i=>graph.SpatialOwner(i.Id))).ToImmutableArray();
        var next=Math.Max(w.Spatial.NextEntityId,entities.Select(e=>e.Id).DefaultIfEmpty().Max()+1);
        return new(new(new MixedWorld(Rail,w.Terrain,new(next,entities),w.Voxels,w.Slopes,w.Revision,w.SpeedLimits,w.Fleet,w.Stations,graph),s.Construction.Accounts),
            s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);
    }
    [Fact]
    public void Preview_retains_exact_candidate_without_booking_drawing_or_advertising_then_natural_completion_binds_real_demand()
    {
        var s=Empty();var bytes=SaveCodec.Write(s);var plan=FixedCityConstruction.Preview(Capture(s),P,House.Reference);
        Assert.Equal(bytes,SaveCodec.Write(s));Assert.Empty(plan.Candidate.Construction.World.City!.State.Producers);
        Assert.Empty(plan.Candidate.Construction.World.Stations!.Listeners);
        s=FixedCityConstruction.Commit(s,plan);Assert.Same(plan.Candidate.Construction,s.Construction);
        Assert.Equal(11_000_000,s.Construction.Accounts.Transactions.Last().Expenditure);
        var site=Assert.Single(s.World.City!.State.Structures);Assert.False(site.Owned);Assert.Null(site.Subsidiary);
        var restored=Reopen(s);
        for(var i=0;i<15000 && !s.World.City.State.Structures.Single().Construction.IsEmpty;i++)
        {
            s.AdvanceOneMinute();restored.AdvanceOneMinute();
            if(i%60==0)Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        }
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        var owner=Assert.Single(s.World.City.State.Structures);Assert.Empty(owner.Construction);Assert.NotEqual(site.Id,owner.Id);Assert.True(owner.Owned);
        Assert.Equal(30,owner.Subsidiary!.TotalLandValue);
        var listener=Assert.Single(s.World.Stations!.Listeners);var provider=listener.Provider!;
        Assert.Equal(owner.Id,listener.ProviderId);Assert.Equal("city-fixed",provider.Input);Assert.Equal(30,provider.LandValue);
        Assert.Equal((1,1),(provider.XYSize,provider.ZSize));Assert.Equal(10,provider.Population.Residents);
        Assert.Equal(new long[]{1},listener.Stations);Assert.Equal(new long[]{owner.Id},s.World.Stations.ListenerMemberships.Single().Providers);
        // January 2 is a source holiday: 40%; January 4 weekday: 80%.
        Assert.Equal(4,StationCatchment.Demand(s.World.Stations,1,new(1980)));
        Assert.Equal(8,StationCatchment.Demand(s.World.Stations,1,new(4860)));
        Assert.DoesNotContain(s.Clock.Capture().Pending,a=>a.HandlerId.StartsWith("city/construction/",StringComparison.Ordinal));
        Assert.Single(s.Clock.Capture().Pending.Where(a=>a.HandlerId==CityHandlers.Subsidiary(owner.Id)));
    }
    [Theory]
    [InlineData(CityConstructionCaller.CatalogPlayer,true,11_000_000)]
    [InlineData(CityConstructionCaller.LegacyPlayer,true,0)]
    [InlineData(CityConstructionCaller.Automated,false,0)]
    public void Caller_controls_ownership_and_source_start_spend(CityConstructionCaller caller,bool owned,long price)
    {
        var s=Start(Empty(false),caller:caller);Assert.Equal(price,s.Construction.Accounts.Transactions.Sum(t=>t.Expenditure));
        Assert.Equal(price==0?0:1,s.Construction.Accounts.Transactions.Length);
        s=Finish(s);Assert.Equal(owned,s.World.City!.State.Structures.Single().Owned);
        Assert.Single(s.World.Stations!.Listeners);Assert.Empty(s.World.Stations.Listeners.Single().Stations);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Selector_volume_only_and_legacy_ground_predicates_preserve_water_and_elevated_behavior(int water)
    {
        var s=Empty(false,water);var elevated=P with {Z=4};
        Assert.NotNull(FixedCityConstruction.Preview(Capture(s),elevated,House.Reference));
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),elevated,House.Reference,CityConstructionCaller.LegacyPlayer));
        // Water is a world plane, not an occupied SeaVoxel in ExAV.
        Assert.NotNull(FixedCityConstruction.Preview(Capture(s),P,House.Reference,CityConstructionCaller.LegacyPlayer));
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),P with {Z=1},House.Reference));
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),new(-100,0,2),House.Reference));
    }
    [Fact]
    public void Legacy_reclaims_actual_ground_provider_property_and_fence_in_one_successor_while_catalog_rejects()
    {
        var s=Empty();var ground=City.Ground.First(d=>d.Handler=="land-0d"&&d.Population!=null);
        var fence=City.Ground.First(d=>d.Handler=="fence"&&!City.Bindings.Single(b=>b.Reference==d.Reference).Implementation.EndsWith("FenceRemover",StringComparison.Ordinal));
        s=WithState(s,s.World.City!.State with {Ground=[new(2,0,ground.Reference,P,false)],Producers=[new(2,ground.Reference,1)],Fences=[new(2,P,0,fence.Reference)]});
        var attached=StationCatchment.Attach(s.World,2,s.World.City!.Provider(2,30));
        s=new(new(attached,s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:attached.Revision);
        var bytes=SaveCodec.Write(s);Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),P,House.Reference));Assert.Equal(bytes,SaveCodec.Write(s));
        var next=Start(s,caller:CityConstructionCaller.LegacyPlayer);
        Assert.Equal(s.World.Revision+1,next.World.Revision);Assert.Empty(next.World.City!.State.Ground);Assert.Empty(next.World.City.State.Producers);
        Assert.Empty(next.World.City.State.Fences);Assert.Empty(next.World.Stations!.Listeners);Assert.Empty(next.World.Stations.ListenerMemberships.Single().Providers);
        Assert.Equal(s.Construction.Accounts.Transactions.Length,next.Construction.Accounts.Transactions.Length);
        var property=WithState(Empty(false),Empty(false).World.City!.State with {Properties=[new(2,0,P)]});
        Assert.Empty(Start(property,caller:CityConstructionCaller.LegacyPlayer).World.City!.State.Properties);
    }
    [Fact]
    public void Automated_replacement_rejects_unowned_and_atomically_demolishes_owned_whole_producer_and_timer()
    {
        var s=Finish(Start(Empty()));var owner=s.World.City!.State.Structures.Single();var bytes=SaveCodec.Write(s);
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),P,House.Reference,CityConstructionCaller.LegacyPlayer));
        Assert.Equal(bytes,SaveCodec.Write(s));
        var next=Start(s,caller:CityConstructionCaller.Automated);
        Assert.Empty(next.World.Stations!.Listeners);Assert.Empty(next.World.Stations.ListenerMemberships.Single().Providers);
        Assert.Empty(next.World.City!.State.Producers);Assert.DoesNotContain(next.Clock.Capture().Pending,a=>a.HandlerId==CityHandlers.Subsidiary(owner.Id));
        Assert.Equal(500_000,next.Construction.Accounts.Transactions.Last().Expenditure);
        next=Finish(next);Assert.False(next.World.City!.State.Structures.Single().Owned);
        var original=SaveCodec.Write(next);Assert.Throws<ArgumentException>(()=>Start(next,caller:CityConstructionCaller.Automated));Assert.Equal(original,SaveCodec.Write(next));
    }
    [Fact]
    public void Upper_voxel_destruction_clears_entire_footprint_both_edges_subsidiary_and_callbacks_once()
    {
        var d=City.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.SizeX==2&&d.SizeY==2&&d.MinimumHeight==2);
        var s=Finish(Start(Empty(),d));var owner=s.World.City!.State.Structures.Single();var picked=new Location(P.X+1,P.Y+1,P.Z+1);
        var plan=FixedCityConstruction.PreviewRemoval(Capture(s),picked,owner.Id,owner.Revision);
        var next=FixedCityConstruction.Commit(s,plan);Assert.Empty(next.World.City!.State.Structures);Assert.Empty(next.World.City.State.Producers);
        Assert.Empty(next.World.Stations!.Listeners);Assert.Empty(next.World.Stations.ListenerMemberships.Single().Providers);
        Assert.All(d.Footprint(d.MinimumHeight).At(P),v=>Assert.Null(next.World.Spatial.At(v.Position).Entity));
        Assert.Equal(2_000_000,next.Construction.Accounts.Transactions.Last().Expenditure);
        Assert.DoesNotContain(next.Clock.Capture().Pending,a=>a.HandlerId==CityHandlers.Subsidiary(owner.Id));
        Assert.Equal(SaveCodec.Write(next),SaveCodec.Write(Reopen(next)));
        Assert.Throws<InvalidOperationException>(()=>FixedCityConstruction.Commit(next,plan));
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.PreviewRemoval(Capture(next),picked,owner.Id,owner.Revision));
    }
    [Fact]
    public void Invisible_upper_occupancy_cancellation_and_stale_plans_reject_without_effects()
    {
        var s=Empty(false);var upper=Start(s,p:P with {Z=3});var bytes=SaveCodec.Write(upper);
        var d=City.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.SizeX==1&&d.SizeY==1&&d.MinimumHeight==2);
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(upper),P,d.Reference));Assert.Equal(bytes,SaveCodec.Write(upper));
        var site=upper.World.City!.State.Structures.Single();var cancel=FixedCityConstruction.PreviewRemoval(Capture(upper),site.Location,site.Id,site.Revision);
        var removed=FixedCityConstruction.Commit(upper,cancel);Assert.Empty(removed.Clock.Capture().Pending);Assert.Empty(removed.World.City!.State.Structures);
        Assert.Equal(upper.Construction.Accounts.Transactions.Length,removed.Construction.Accounts.Transactions.Length);
        Assert.Equal(JsonSerializer.Serialize(upper.Random.Capture()),JsonSerializer.Serialize(removed.Random.Capture()));
        var plan=FixedCityConstruction.Preview(Capture(s),P,House.Reference);s.AdvanceOneMinute();
        Assert.Throws<InvalidOperationException>(()=>FixedCityConstruction.Commit(s,plan));
    }
    [Fact]
    public void No_population_definition_completes_without_advertising_a_provider()
    {
        var d=City.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.Population==null&&d.MinimumHeight==1);
        var s=Finish(Start(Empty(),d));Assert.Empty(s.World.City!.State.Producers);Assert.Empty(s.World.Stations!.Listeners);
    }
    [Fact]
    public void Failed_subsidiary_creation_after_fixed_listener_attachment_rolls_back_the_entire_minute()
    {
        var s=Start(Empty());
        s=new(s.Construction,s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision,
            cityLandValue:(_,_)=>throw new OverflowException("Injected late subsidiary valuation failure."));
        var failed=false;
        for(var i=0;i<15000;i++)
        {
            var before=SaveCodec.Write(s);
            try {s.AdvanceOneMinute();}
            catch(OverflowException)
            {
                Assert.Equal(before,SaveCodec.Write(s));Assert.Empty(s.World.Stations!.Listeners);
                Assert.Empty(s.World.City!.State.Producers);Assert.NotEmpty(s.World.City.State.Structures.Single().Construction);
                failed=true;break;
            }
        }
        Assert.True(failed);
    }
    [Fact]
    public void Actual_creek_reclamation_updates_terrain_and_city_in_one_successor_and_preserves_other_voxels()
    {
        var s=Empty(false);var draft=AuthoringTerrain.Plan(s.World.Terrain,P,P with {X=P.X+1},"placeCreek",null,s.Random,
            s.World.Spatial.NextEntityId,s.World.Spatial);
        var terrain=draft.Draft.Finish();Assert.Equal(2,terrain.Occupants.Length);
        var world=new MixedWorld(Rail,terrain,terrain.Spatial.Capture(),[],[],terrain.Revision,city:s.World.City);
        s=new(new(world),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:world.Revision);
        var next=Start(s,caller:CityConstructionCaller.LegacyPlayer);
        Assert.Equal(s.World.Revision+1,next.World.Revision);Assert.Equal(terrain.Revision+1,next.World.Terrain.Revision);
        Assert.Null(next.World.Terrain.OccupantAt(P));Assert.NotNull(next.World.Terrain.OccupantAt(P with {X=P.X+1}));
        Assert.Single(next.World.City!.State.Structures);Assert.Equal(SaveCodec.Write(next),SaveCodec.Write(Reopen(next)));
    }
    [Fact]
    public void Timed_families_and_manual_versus_automatic_content_flags_are_enforced_without_effects()
    {
        var s=Empty(false);var before=SaveCodec.Write(s);
        foreach(var kind in new[]{CityStructureKind.Half})
            Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),P,City.Structures.First(d=>d.Kind==kind).Reference));
        var manual=City.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.ComputerCannotBuild);
        Assert.NotNull(FixedCityConstruction.Preview(Capture(s),P,manual.Reference));
        Assert.Throws<ArgumentException>(()=>FixedCityConstruction.Preview(Capture(s),P,manual.Reference,CityConstructionCaller.Automated));
        Assert.Equal(before,SaveCodec.Write(s));
    }
    [Fact]
    public void Reclamation_books_whole_owners_once_in_source_XYZ_order_before_starting_the_successor()
    {
        var two=City.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.SizeX==2&&d.SizeY==1&&d.MinimumHeight==2&&!d.ComputerCannotBuild);
        var replacement=City.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.SizeX==2&&d.SizeY==2&&d.MinimumHeight==1&&!d.ComputerCannotBuild);
        var s=Finish(Start(Empty(),two,p:P with {Y=P.Y+1}));
        s=Finish(Start(s));var old=s.World.City!.State.Structures.Select(o=>o.Id).ToHashSet();
        var count=s.Construction.Accounts.Transactions.Length;
        var next=Start(s,replacement,CityConstructionCaller.Automated);
        Assert.Equal(new long[]{500_000,1_000_000},next.Construction.Accounts.Transactions.Skip(count).Select(t=>t.Expenditure));
        Assert.All(next.Construction.Accounts.Transactions.Skip(count),t=>Assert.Equal(next.World.Revision,t.WorldRevision));
        Assert.Empty(next.World.City!.State.Producers);Assert.Empty(next.World.Stations!.Listeners);
        Assert.DoesNotContain(next.Clock.Capture().Pending,a=>old.Any(id=>a.HandlerId==CityHandlers.Subsidiary(id)));
        Assert.DoesNotContain(next.World.Spatial.Entities,e=>old.Contains(e.Id));
    }
    [Fact]
    public void Natural_completion_fixture_uses_real_site_station_callbacks_and_exact_save_continuation()
    {
        var s=Start(Empty());byte[]? last=null;
        for(var i=0;i<15000&&!s.World.City!.State.Structures.Single().Construction.IsEmpty;i++)
        {last=SaveCodec.Write(s);s.AdvanceOneMinute();}
        Assert.Empty(s.World.City!.State.Structures.Single().Construction);Assert.NotNull(last);
        var reopened=SaveCodec.ReadWorld(last,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        var before=MixedWire.Snapshot(reopened,1);reopened.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));
        if(Environment.GetEnvironmentVariable("FREETRAIN_FIXED_CITY_OUTPUT") is {} path)
        {
            File.WriteAllBytes(path,last);var options=WireJson.CreateOptions();
            File.WriteAllText(path+".before.json",JsonSerializer.Serialize<WireMessage>(before,options));
            File.WriteAllText(path+".snapshot.json",JsonSerializer.Serialize<WireMessage>(MixedWire.Snapshot(s,1),options));
            File.WriteAllText(path+".delta.json",JsonSerializer.Serialize<WireMessage>(StateDifference.Between(before,MixedWire.Snapshot(s,1)),options));
        }
    }
}
