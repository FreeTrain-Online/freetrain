using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class CityStateTests
{
    static RailwayProfile Rail => RailwayProfiles.Bundled;
    static CityProfile Profile => Rail.City!;
    static TerrainWorld Terrain() => new(TerrainProfiles.Bundled,"City state",new(80,32,64),0,
        Enumerable.Repeat(new TerrainCell(2),80*32),new(1,[]),[]);
    static CityState Empty(TerrainWorld? terrain=null) => CityGraph.Empty(Profile,(terrain ?? Terrain()).Space).State;
    static CityStructureDefinition Fixed => Profile.Structures.First(d=>d.Kind==CityStructureKind.Fixed && d.SizeX==1 && d.SizeY==1 && d.MinimumHeight==1);
    static CityStructure Complete(CityStructureDefinition? d=null,long id=1,Location? location=null,int? height=null,bool owned=true)
    {
        d ??= Fixed;
        return new(id,0,d.Reference,location ?? new(20,20,2),height ?? d.MinimumHeight,owned,new(123456789,20),[],[]);
    }
    static CityState WithStructures(params CityStructure[] structures)
    {
        var state=Empty();
        return state with {Structures=structures.ToImmutableArray(),Producers=structures.Where(s=>s.Construction.IsEmpty && Profile.Structure(s.Definition).Population!=null)
            .Select(s=>new CityProducer(s.Id,s.Definition,s.Height)).ToImmutableArray()};
    }
    static MixedWorld World(CityState state,MixedWorld? basis=null,IEnumerable<SpatialEntity>? entities=null,
        IEnumerable<RailwayVoxel>? voxels=null,long? next=null)
    {
        basis ??= MixedWorld.FromTerrain(Rail,Terrain());
        var graph=new CityGraph(Profile,state);
        var spatial=(entities ?? basis.Spatial.Entities.Concat(graph.Identities.Select(i=>graph.SpatialOwner(i.Id)))).ToImmutableArray();
        return new(Rail,basis.Terrain,new(next ?? spatial.Select(e=>e.Id).Concat(basis.Stations?.Identities.Select(i=>i.Id) ?? []).DefaultIfEmpty().Max()+1,spatial),
            voxels ?? basis.Voxels,basis.Slopes,basis.Revision,basis.SpeedLimits,basis.Fleet,basis.Stations,graph);
    }
    static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/city-fixtures.json")));
    static IEnumerable<JsonElement> Cases(JsonDocument doc,string group) => doc.RootElement.GetProperty("groups").GetProperty(group).GetProperty("cases").EnumerateArray();
    static Location Point(JsonElement a) => new(a[0].GetInt32(),a[1].GetInt32(),a[2].GetInt32());

    [Fact]
    public void Prepared_bindings_cover_all_reviewed_city_declarations_without_enabling_empty_or_P8_content()
    {
        Assert.Equal(209,Profile.Bindings.Length);Assert.Equal(129,Profile.Structures.Length);
        Assert.Equal(66,Profile.Ground.Length);Assert.Equal(3,Profile.Roads.Length);
        Assert.Equal(66,Profile.Structures.Count(d=>d.Kind==CityStructureKind.Fixed));
        Assert.Equal(55,Profile.Structures.Count(d=>d.Kind==CityStructureKind.Variable));
        Assert.Equal(8,Profile.Structures.Count(d=>d.Kind==CityStructureKind.Half));
        Assert.All(Profile.Bindings,b=>Assert.NotNull(PreparedContentCatalog.Bundled.FindRenderRecipe(b.Reference.PackId,b.Reference.RecipeId)));
        using var review=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/city-review.json")));
        var rows=review.RootElement.GetProperty("contributions").EnumerateArray().ToArray();
        Assert.Equal(rows.Select(r=>r.GetProperty("sourceContribution").GetProperty("contributionId").GetString()).Order(),
            Profile.Bindings.Select(b=>b.Reference.ContributionId).Order());
        foreach(var row in rows)
        {
            var source=row.GetProperty("sourceContribution");
            var b=Profile.Bindings.Single(b=>b.Reference.ContributionId==source.GetProperty("contributionId").GetString());
            Assert.Equal(source.GetProperty("contributionType").GetString(),b.SourceType);
            if(row.GetProperty("computerCannotBuild").ValueKind!=JsonValueKind.Null)
                Assert.Equal(row.GetProperty("computerCannotBuild").GetBoolean(),b.ComputerCannotBuild);
            if(row.GetProperty("playerCannotBuild").ValueKind!=JsonValueKind.Null)
                Assert.Equal(row.GetProperty("playerCannotBuild").GetBoolean(),b.PlayerCannotBuild);
            var impl=source.GetProperty("implementationBinding");
            if(impl.ValueKind!=JsonValueKind.Null) Assert.Equal(impl.GetProperty("name").GetString(),b.Implementation);
        }
        Assert.Throws<ArgumentException>(()=>Profile.Structure(Profile.Bindings.Single(b=>b.Reference.ContributionId=="null-contrib-0:0").Reference));
        Assert.Throws<ArgumentException>(()=>Profile.Structure(Profile.Bindings.Single(b=>b.SourceType=="specialStructure").Reference));
        Assert.Throws<ArgumentException>(()=>new RailwayProfile(Rail.Terrain,Rail.Bindings,city:new CityProfile(
            [Profile.Packs[0] with {Version="other"}],Profile.Bindings,Profile.Structures,Profile.Roads,Profile.Ground)));
        Assert.Throws<ArgumentException>(()=>new CityProfile(Profile.Packs,Profile.Bindings.Add(Profile.Bindings[0]),Profile.Structures,Profile.Roads,Profile.Ground));
        Assert.Throws<ArgumentException>(()=>Profile.Structure(Fixed.Reference with {PackId="missing"}));
        Assert.Throws<ArgumentException>(()=>new CityProfile(Profile.Packs,Profile.Bindings,
            Profile.Structures.SetItem(0,Profile.Structures[0] with {Reference=Profile.Roads[0].Reference}),Profile.Roads,Profile.Ground));
    }
    [Fact]
    public void All_225_literal_definition_cases_bind_prices_footprints_and_population_height_multipliers()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var row in Cases(fixtures,"definitions"))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var source=input.GetProperty("definition").GetString()!;var id=source[(source.LastIndexOf(':')+1)..];
            var d=Profile.Structures.Single(d=>d.Reference.ContributionId==id);var h=input.GetProperty("height").GetInt32();
            Assert.Equal(expected.GetProperty("size").EnumerateArray().Select(n=>n.GetInt32()),new[]{d.SizeX,d.SizeY,h});
            Assert.Equal(expected.GetProperty("price").GetInt64(),d.Price(h));
            Assert.Equal(expected.GetProperty("pricePerArea").GetInt64(),d.PricePerArea);
            Assert.Equal(expected.GetProperty("occupied").GetInt32(),d.Footprint(h).Members.Length);
            Assert.Equal(expected.GetProperty("computerEligible").GetBoolean(),!d.ComputerCannotBuild);
            var s=Complete(d,location:Point(input.GetProperty("base")),height:h,owned:false);
            var w=World(WithStructures(s));
            Assert.Equal(Point(expected.GetProperty("endExclusive")),new Cube(s.Location,d.SizeX,d.SizeY,h).End);
            Assert.All(d.Footprint(h).At(s.Location),p=>{Assert.Equal(s.Id,w.Spatial.At(p.Position).Entity!.Id);Assert.False(w.Spatial.At(p.Position).Reusable);});
            if(d.Population!=null) Assert.Equal(checked(d.Population.Residents*expected.GetProperty("providerMultiplier").GetInt32()),w.City!.ProducerPopulation(s.Id).Residents);
            Assert.Throws<ArgumentException>(()=>d.Price(d.MinimumHeight-1));
            Assert.Throws<ArgumentException>(()=>d.Price(d.MaximumHeight+1));count++;
        }
        Assert.Equal(225,count);
    }
    [Fact]
    public void All_45_source_road_masks_share_existing_Traffic_and_preserve_crossings()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var row in Cases(fixtures,"roads"))
        {
            var i=row.GetProperty("input");
            // Literal mask vectors map to existing eight-heading Traffic bits.
            var mask=i.GetProperty("mask").GetInt32();
            var d=Profile.Roads[count/15];var edges=CityRoadDefinition.Edges(mask);
            Assert.Equal(mask,CityRoadDefinition.SourceMask(edges));
            Assert.Equal(row.GetProperty("expected").GetProperty("directions").EnumerateArray().Select(n=>n.GetInt32()),
                Enumerable.Range(0,8).Where(h=>(edges&(1<<h))!=0));
            var p=new Location(20,20,2);var e=new SpatialEntity(1,0,Rail.TrafficOwner,p,Footprint.Box(new(new(0,0,0),1,1,1),true),true,false,0);
            var w=World(Empty(),entities:[e],voxels:[new(p,1,RailwayVoxelKind.Traffic,new(Road:new(d.Reference,edges)))]);
            Assert.Throws<ArgumentException>(()=>new MixedWorld(Rail,w.Terrain,w.Spatial.Capture(),w.Voxels,[]));
            Assert.Equal(d.Reference,w.At(p)!.Traffic!.Road!.Definition);Assert.Equal("street",d.Style);Assert.Equal("shoulder",d.Sidewalk);count++;
        }
        Assert.Equal(45,count);
        var normal=Rail.Bindings.First(b=>b.Handler==RailwayHandler.Normal && b.Variant==NormalRailPatterns.Find(0,4));
        var crossing=new TrafficComponents(new(normal.Reference,normal.Variant),new(Profile.Roads[0].Reference,CityRoadDefinition.Edges(10)));
        Assert.True(TrafficLifecycle.HasCrossing(crossing));
        Assert.False(TrafficLifecycle.HasCrossing(crossing with {Road=crossing.Road! with {Edges=CityRoadDefinition.Edges(5)}}));
        Assert.Throws<ArgumentException>(()=>CityRoadDefinition.Edges(0));Assert.Throws<ArgumentException>(()=>CityRoadDefinition.SourceMask(2));
    }
    [Fact]
    public void Full_invisible_upper_cells_reject_other_owners_and_outside_or_nonlevel_footprints()
    {
        var d=Profile.Structures.First(d=>d.Kind==CityStructureKind.Fixed && d.MinimumHeight>2);
        var s=Complete(d);var state=WithStructures(s);var w=World(state);var upper=s.Location with {Z=s.Location.Z+1};
        Assert.Equal(s.Id,w.Spatial.At(upper).Entity!.Id);
        Assert.Throws<ArgumentException>(()=>World(state with {Properties=[new(2,0,upper)]}));
        Assert.Throws<ArgumentException>(()=>CityOwnership.Reclaimable(w,upper,Footprint.Box(new(new(0,0,0),1,1,1))));
        Assert.Throws<ArgumentException>(()=>World(WithStructures(s with {Location=new(-100,0,2)})));
        Assert.Throws<ArgumentException>(()=>CityOwnership.Placement(w,s.Location with {Z=3},d,s.Height));
        var e=w.Spatial.Entities.Single();
        Assert.Throws<ArgumentException>(()=>World(state,entities:[e with {Footprint=Footprint.Box(new(new(0,0,0),1,1,1))}]));
        Assert.Throws<ArgumentException>(()=>World(state,entities:[e with {Value=e.Value+1}]));
        Assert.Single(w.Spatial.Entities);
    }
    [Fact]
    public void Property_reclamation_is_detached_and_allocators_and_owner_kinds_cannot_be_reused()
    {
        var state=Empty() with {Properties=[new(1,0,new(20,20,2))]};var w=World(state);
        var old=w.City!.SpatialOwner(1);Assert.True(old.Owned);Assert.True(old.SilentlyReclaimable);Assert.Equal(0,old.Value);
        Assert.Equal(new long[]{1},CityOwnership.Reclaimable(w,old.Anchor,Fixed.Footprint(1)));
        var s=Complete(id:2);var successor=new CityGraph(Profile,WithStructures(s));
        var next=w.Replace(w.Revision,w.Terrain,new(3,[successor.SpatialOwner(2)]),[],[],city:successor);
        Assert.Single(w.City.State.Properties);Assert.Empty(next.City!.State.Properties);Assert.Single(next.City.State.Structures);
        Assert.Equal(2,next.Spatial.At(old.Anchor).Entity!.Id);Assert.Equal(1,w.Spatial.At(old.Anchor).Entity!.Id);
        Assert.Throws<ArgumentException>(()=>w.Replace(w.Revision,w.Terrain,new(2,[successor.SpatialOwner(2)]),[],[],city:successor));
        var reused=new CityGraph(Profile,WithStructures(Complete(id:1) with {Revision=1}));
        Assert.Throws<ArgumentException>(()=>w.Replace(w.Revision,w.Terrain,new(2,[reused.SpatialOwner(1)]),[],[],city:reused));
        Assert.Throws<InvalidOperationException>(()=>w.Replace(w.Revision+1,w.Terrain,w.Spatial.Capture(),[],[],city:w.City));
    }
    [Fact]
    public void Half_buildings_share_one_whole_cell_and_retain_initial_population_and_market_snapshot()
    {
        var a=Profile.Structures.First(d=>d.Kind==CityStructureKind.Half);
        var b=Profile.Structures.Last(d=>d.Kind==CityStructureKind.Half);
        var half=new CityHalf(a.Reference,HalfSide.Back,0);
        var s=Complete(a) with {Halves=[half]};var state=WithStructures(s);var w=World(state);
        var second=new CityHalf(b.Reference,HalfSide.Fore,4);
        var changed=s with {Revision=1,Halves=[half,second]};var graph=new CityGraph(Profile,state with {Structures=[changed]});
        var next=w.Replace(0,w.Terrain,new(2,[graph.SpatialOwner(1)]),[],[],city:graph);
        Assert.Single(next.Spatial.Entities.Single().Footprint.Members);
        Assert.Equal(a.UnitPrice+b.UnitPrice,graph.StructurePrice(changed));Assert.Equal(s.Subsidiary,changed.Subsidiary);
        Assert.Equal(a.Population!.Residents,graph.ProducerPopulation(1).Residents);
        var removed=changed with {Revision=2,Halves=[second]};var last=new CityGraph(Profile,state with {Structures=[removed]});
        next.Replace(1,next.Terrain,new(2,[last.SpatialOwner(1)]),[],[],city:last);
        Assert.Equal(a.Population.Residents,last.ProducerPopulation(1).Residents);
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Structures=[s with {Halves=[half,second with {Direction=2}]}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Structures=[s with {Halves=[half,second with {Side=HalfSide.Back}]}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Structures=[s with {Halves=[half with {Color=999}]}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Producers=[new(1,b.Reference,b.MinimumHeight)]}));
    }
    [Fact]
    public void Construction_owns_full_volume_without_producer_or_subsidiary_and_requires_each_unfinished_handle()
    {
        var s=Complete();var action=new ScheduledAction(0,0,GameTime.StartMinute+40,CityHandlers.Construction(1,new(0,0,0)),0);
        s=s with {Owned=false,Subsidiary=null,Construction=[new(new(0,0,0),new(),action)]};
        var state=Empty() with {Structures=[s]};var w=World(state);
        Assert.False(w.Spatial.Entities.Single().Owned);Assert.Empty(w.City!.State.Producers);Assert.Equal(0,w.Spatial.Entities.Single().Value);
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Producers=[new(1,s.Definition,s.Height)]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Structures=[s with {Owned=true}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Structures=[s with {Construction=[s.Construction[0] with {Callback=null}]}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Structures=[s with {Construction=[s.Construction[0] with {Offset=new(0,0,1)}]}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Structures=[s with {Construction=[s.Construction[0] with {Callback=action with {HandlerId="wrong"}}]}]}));
    }
    [Fact]
    public void Ground_producers_and_voxel_fences_use_real_owners_including_Traffic_and_dummy_hosts()
    {
        var ground=Profile.Ground.First(d=>d.Handler=="land-0d" && d.Population!=null);
        var fence=Profile.Ground.First(d=>d.Handler=="fence" && !Profile.Bindings.Single(b=>b.Reference==d.Reference).Implementation.EndsWith("FenceRemover",StringComparison.Ordinal));
        var p=new Location(20,20,2);var q=new Location(23,20,2);var r=new Location(25,20,2);
        var state=Empty() with {Ground=[new(1,0,ground.Reference,p,false)],Producers=[new(1,ground.Reference,1)],FenceHosts=[new(2,0,q)],
            Fences=[new(1,p,0,fence.Reference),new(2,q,1,fence.Reference),new(3,r,2,fence.Reference)]};
        var road=new RailwayVoxel(r,3,RailwayVoxelKind.Traffic,new(Road:new(Profile.Roads[0].Reference,CityRoadDefinition.Edges(5))));
        var graph=new CityGraph(Profile,state);var traffic=new SpatialEntity(3,0,Rail.TrafficOwner,r,Footprint.Box(new(new(0,0,0),1,1,1),true),true,false,0);
        var w=World(state,entities:[graph.SpatialOwner(1),graph.SpatialOwner(2),traffic],voxels:[road]);
        Assert.True(w.Spatial.At(p).Transparent);Assert.True(w.Spatial.At(q).Reusable);Assert.True(w.Spatial.At(q).Entity!.Owned);
        Assert.Equal(ground.Population!.Residents,graph.ProducerPopulation(1).Residents);
        Assert.Throws<ArgumentException>(()=>World(state with {Fences=[new(1,q,0,fence.Reference)]},entities:w.Spatial.Entities,voxels:w.Voxels));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Producers=[new(99,ground.Reference,1)]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Ground=[state.Ground[0] with {Definition=fence.Reference}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Producers=[]}));
    }
    [Fact]
    public void Producer_and_fence_payload_changes_require_owner_revision_even_when_geometry_is_unchanged()
    {
        var s=Complete();var state=WithStructures(s);var w=World(state);
        var graph=new CityGraph(Profile,state with {Structures=[s with {Owned=false}]});
        Assert.Throws<ArgumentException>(()=>w.Replace(0,w.Terrain,new(2,[graph.SpatialOwner(1)]),[],[],city:graph));
        var revised=new CityGraph(Profile,state with {Structures=[s with {Revision=1,Owned=false}]});
        Assert.False(w.Replace(0,w.Terrain,new(2,[revised.SpatialOwner(1)]),[],[],city:revised).City!.Structure(1).Owned);
        var badProducer=new CityProducer(1,Profile.Structures.First(d=>d.Kind==CityStructureKind.Variable).Reference,2);
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Producers=[badProducer]}));
    }
    [Fact]
    public void All_three_literal_valuation_traces_keep_cumulative_market_price_separate_from_variable_entity_value()
    {
        using var fixtures=Fixtures();var count=0;
        foreach(var row in Cases(fixtures,"valuation"))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");var subsidiary=new CitySubsidiary(0,0);var prices=new List<long>();
            foreach(var land in input.GetProperty("land").EnumerateArray())
            { subsidiary=CityValuation.Reprice(subsidiary,input.GetProperty("structure").GetInt64(),land.GetInt64());prices.Add(subsidiary.MarketPrice); }
            Assert.Equal(expected.GetProperty("marketPrices").EnumerateArray().Select(n=>n.GetInt64()),prices);count++;
        }
        Assert.Equal(3,count);
        var d=Profile.Structures.First(d=>d.Kind==CityStructureKind.Variable);var s=Complete(d,height:d.MaximumHeight);
        var w=World(WithStructures(s));Assert.Equal(d.Price(s.Height),w.Spatial.Entities.Single().Value);
        Assert.NotEqual(s.Subsidiary!.MarketPrice,w.Spatial.Entities.Single().Value);
        Assert.Throws<OverflowException>(()=>CityValuation.Reprice(new(long.MaxValue,0),1,0));
        Assert.Throws<OverflowException>(()=>CityValuation.Profit(long.MaxValue,0));
    }
    [Fact]
    public void Heat_buffers_settings_and_cached_ULV_round_trip_without_reinitializing_or_sharing_mutable_state()
    {
        var state=Empty();var heat=state.Heat;var index=heat.Index(0,0);
        state=state with {Heat=heat with {Q=heat.Q.SetItem(index,12.5f),Back=heat.Back.SetItem(index,8.25f),Rho=heat.Rho.SetItem(index,0.25f)},
            LandCache=[new(new(20,20,2),2,3,120,450)]};
        var options=new JsonSerializerOptions {UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
        var json=JsonSerializer.Serialize(state,options);var restored=JsonSerializer.Deserialize<CityState>(json,options)!;
        var a=World(state);var b=World(restored);
        Assert.Equal(json,JsonSerializer.Serialize(b.City!.State,options));Assert.Equal(12.5f,b.City.State.Heat.Q[index]);
        Assert.Equal(8.25f,b.City.State.Heat.Back[index]);Assert.Equal(state.LandCache.ToArray(),b.City.State.LandCache.ToArray());
        Assert.NotSame(a.City,b.City);Assert.Equal(0,Empty().Heat.Q[index]);
        Assert.Equal(0.8f,heat.Rho[heat.Index(1,1)]);Assert.Equal(0,heat.Rho[index]);
        Assert.Throws<ArgumentException>(()=>World(state with {Heat=heat with {Q=heat.Q.RemoveAt(0)}}));
        Assert.Throws<ArgumentException>(()=>World(state with {Heat=heat with {Q=heat.Q.SetItem(0,float.NaN)}}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Settings=new(MaximumPricePower:0)}));
        Assert.Throws<ArgumentException>(()=>World(state with {LandCache=[new(new(-999,0,2),1,1,0,0)]}));
    }
    [Fact]
    public void Search_continuation_keeps_station_identity_phase_work_budget_selection_and_stale_ULV()
    {
        var station=StationStateTests.World(StationStateTests.Graph([StationStateTests.Building()]));
        var state=Empty(station.Terrain);var d=Fixed;var selected=new CitySelectedPlan(d.Reference,new(20,20,2),d.SizeX,d.SizeY,1,d.UnitPrice,d.UnitPrice);
        var plan=new CitySearchPlan(1,CitySearchPhase.Build,24,7,[1,3,1,3],new(20,20,2),selected);
        state=state with {Plans=[plan],LandCache=[new(new(20,20,2),1,1,20,500)]};
        var w=World(state,station);Assert.Equal(plan,w.City!.State.Plans.Single());
        var restored=JsonSerializer.Deserialize<CityState>(JsonSerializer.Serialize(state))!;World(restored,station);
        Assert.Throws<ArgumentException>(()=>World(state with {Plans=[plan with {StationId=999}]},station));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Plans=[plan with {Selected=null}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Plans=[plan with {Budget=-1}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Plans=[plan with {Work=[1,3]}]}));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {Plans=[plan with {Selected=selected with {FinalPrice=1}}]}));
        Assert.Throws<ArgumentException>(()=>World(state with {Plans=[plan with {Selected=selected with {Site=new(-100,0,2)}}]},station));
        var colliding=new CityGraph(Profile,state with {Properties=[new(1,0,new(25,20,2))]});
        Assert.Throws<ArgumentException>(()=>World(colliding.State,station));
    }
    [Fact]
    public void Detached_candidates_preserve_FIFO_handles_and_world_local_random_continuation_and_refuse_live_Save10_sessions()
    {
        var state=Empty();var time=GameTime.StartMinute;
        var heat=new ScheduledAction(0,0,time+600,CityHandlers.Heat,600);
        var development=new ScheduledAction(1,1,time+600,CityHandlers.Development,60);
        state=state with {HeatCallback=heat,DevelopmentCallback=development};var world=World(state);var construction=new MixedConstructionState(world);
        var clock=new ClockState(time,2,[heat,development]);var random=new RandomStreams(42);random["development"].Next(80);
        var saved=random.Capture();var candidate=new CityCandidate(construction,clock,saved);
        var expected=random["development"].Next(80);Assert.Equal(expected,RandomStreams.Restore(candidate.Random)["development"].Next(80));
        Assert.Equal(heat,candidate.Clock.Pending[0]);Assert.Equal(development,candidate.Clock.Pending[1]);
        Assert.Throws<ArgumentException>(()=>new CityCandidate(construction,clock with {Pending=[heat]},saved));
        Assert.Throws<ArgumentException>(()=>new CityCandidate(construction,clock with {Pending=[heat,development with {Order=0}]},saved));
        Assert.Throws<ArgumentException>(()=>new CityGraph(Profile,state with {DevelopmentCallback=development with {Registration=0}}));
        var provenance=new TerrainProvenance("fixture","empty","fixture","{}");
        // The city guard executes before provenance/lifecycle activation.
        Assert.Throws<ArgumentException>(()=>new MixedSessionState(construction,provenance));
        Assert.Throws<ArgumentException>(()=>MixedWire.Read(construction));
        Assert.Throws<ArgumentException>(()=>MixedWire.RailwayEntities(world));
    }

    [Fact]
    public void Entire_city_state_round_trips_in_a_mixed_station_fleet_and_road_world_and_rejects_identity_aliases()
    {
        var basis=StationStateTests.World(StationStateTests.Graph([StationStateTests.Building()])).EnableFleet(Rail.FleetProfile!);
        var state=Empty(basis.Terrain);
        var variable=Profile.Structures.First(d=>d.Kind==CityStructureKind.Variable && d.SizeX==1 && d.SizeY==1 && d.MinimumHeight<6);
        var half=Profile.Structures.First(d=>d.Kind==CityStructureKind.Half);
        var ground=Profile.Ground.First(d=>d.Handler=="land-0d" && d.Population!=null);
        var fence=Profile.Ground.First(d=>d.Handler=="fence" && !Profile.Bindings.Single(b=>b.Reference==d.Reference).Implementation.EndsWith("FenceRemover",StringComparison.Ordinal));
        var fixedOwner=Complete(id:6,location:new(22,22,2));
        var variableOwner=Complete(variable,7,new(15,22,2));
        var halfOwner=Complete(half,8,new(24,23,2)) with {Halves=[new(half.Reference,HalfSide.Back,0)]};
        var callback=new ScheduledAction(0,0,GameTime.StartMinute+40,CityHandlers.Construction(11,new(0,0,0)),0);
        var site=Complete(id:11,location:new(22,25,2)) with {Owned=false,Subsidiary=null,Construction=[new(new(0,0,0),new(),callback)]};
        state=state with {Properties=[new(5,0,new(20,20,2))],Structures=[fixedOwner,variableOwner,halfOwner,site],
            Ground=[new(9,0,ground.Reference,new(25,24,2),false)],FenceHosts=[new(10,0,new(26,25,2))],
            Producers=[new(6,fixedOwner.Definition,1),new(7,variable.Reference,variable.MinimumHeight),new(8,half.Reference,half.MinimumHeight),new(9,ground.Reference,1)],
            Fences=[new(10,new(26,25,2),0,fence.Reference)],
            Plans=[new(1,CitySearchPhase.SelectStruct2,24,7,[1,3,1,3],new(20,20,2),null)],LandCache=[new(new(20,20,2),1,1,20,0)]};
        var graph=new CityGraph(Profile,state);var p=new Location(18,18,2);
        var traffic=new SpatialEntity(12,0,Rail.TrafficOwner,p,Footprint.Box(new(new(0,0,0),1,1,1),true),true,false,0);
        var entities=basis.Spatial.Entities.Concat(graph.Identities.Select(i=>graph.SpatialOwner(i.Id))).Append(traffic).ToArray();
        var voxels=new[]{new RailwayVoxel(p,12,RailwayVoxelKind.Traffic,new(Road:new(Profile.Roads[0].Reference,CityRoadDefinition.Edges(10))))};
        var before=World(state,basis,entities,voxels);
        var json=JsonSerializer.Serialize(state);var restored=JsonSerializer.Deserialize<CityState>(json)!;
        var after=World(restored,basis,entities,voxels);
        Assert.Equal(json,JsonSerializer.Serialize(after.City!.State));Assert.Same(basis.Fleet,after.Fleet);Assert.Same(basis.Stations,after.Stations);
        var random=new RandomStreams(73).Capture();var clock=new ClockState(GameTime.StartMinute,1,[callback]);
        var candidate=new CityCandidate(new(after),JsonSerializer.Deserialize<ClockState>(JsonSerializer.Serialize(clock))!,
            JsonSerializer.Deserialize<RandomStreamsState>(JsonSerializer.Serialize(random))!);
        Assert.Equal(callback,Assert.Single(candidate.Clock.Pending));Assert.Equal(before.City!.Structure(8).Halves.ToArray(),after.City.Structure(8).Halves.ToArray());
        Assert.Throws<ArgumentException>(()=>World(state with {Properties=[new(2,0,new(20,20,2))]},basis)); // Fleet controller/group identity.
        Assert.Throws<ArgumentException>(()=>World(restored,basis,entities.Where(e=>e.Id!=11),voxels));
        Assert.Single(before.City.State.Properties);Assert.Equal(4,before.City.State.Structures.Length);
    }
}
