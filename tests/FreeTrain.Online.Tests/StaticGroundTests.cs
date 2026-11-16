using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;

public class StaticGroundTests
{
    static readonly Location P=new(14,12,2);
    static readonly CityGroundDefinition D=RailwayProfiles.Bundled.City!.Ground.First(d=>d.Handler=="land-0d");
    static CityCandidate Capture(MixedSessionState s)=>new(s.Construction,s.Clock.Capture(),s.Random.Capture());
    static LandTradePlan Plan(MixedSessionState s,Location? end=null,DefinitionReference? definition=null,string action="cover",Location? anchor=null)=>
        StaticGroundConstruction.Preview(Capture(s),anchor??P,end??P,action,action=="cover"?definition??D.Reference:null);
    static MixedSessionState Apply(MixedSessionState s,LandTradePlan p){var next=LandTrading.Commit(s,p);MixedWire.ValidateTransition(s,next);return next;}
    static void Exact(MixedSessionState s){var b=SaveCodec.Write(s);Assert.Equal(b,SaveCodec.Write(SaveCodec.ReadWorld(b,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));}
    static MixedSessionState Seed()
    {
        var s=LandTradingTests.Empty();
        s=StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,new(12,12,2),StationStateTests.Definition.Reference));
        return LandTrading.Commit(s,LandTrading.Preview(Capture(s),P,P,"buy"));
    }
    [Fact]
    public void All_twenty_actual_static_definitions_have_source_price_grid_population_and_immediate_owned_lifecycle()
    {
        var defs=RailwayProfiles.Bundled.City!.Ground.Where(d=>d.Handler=="land-0d").ToArray();Assert.Equal(20,defs.Length);
        foreach(var d in defs)
        {
            Assert.Equal(300000,d.Price);Assert.Equal(1,d.GridX);Assert.Equal(1,d.GridY);Assert.NotNull(d.Population);
            var s=LandTradingTests.Empty();var original=SaveCodec.Write(s);var plan=Plan(s,definition:d.Reference);Assert.Equal(original,SaveCodec.Write(s));Assert.Equal(300000,plan.Amount);
            var after=Apply(s,plan);var g=Assert.Single(after.World.City!.State.Ground);Assert.True(g.Owned);Assert.Equal(0,g.Variant);
            Assert.Equal(d.Reference,g.Definition);Assert.Single(after.World.City.State.Producers);var listener=Assert.Single(after.World.Stations!.Listeners);
            Assert.Equal(g.Id,listener.ProviderId);Assert.Equal("city-ground",listener.Provider!.Input);Assert.Equal(5,listener.Provider.Population.Residents);
            Assert.Equal(0,listener.Provider.Population.Outgoing(after.Clock.Time));Assert.Equal(0,listener.Provider.Population.Incoming(after.Clock.Time));Assert.Empty(after.World.City.State.Structures);
            var receipt=Assert.Single(after.Construction.Accounts.Transactions);Assert.Equal(300000,receipt.Expenditure);Assert.Equal(AccountGenre.Subsidiaries,receipt.Genre);
            Assert.Equal(JsonSerializer.Serialize(s.Clock.Capture()),JsonSerializer.Serialize(after.Clock.Capture()));Assert.Equal(JsonSerializer.Serialize(s.Random.Capture()),JsonSerializer.Serialize(after.Random.Capture()));Exact(after);
        }
    }
    [Fact]
    public void Owned_property_and_owned_ground_are_reused_without_land_purchase_or_entity_value_charges_and_listeners_are_replaced()
    {
        var s=Seed();var station=Assert.Single(s.World.Stations!.Buildings);var after=Apply(s,Plan(s,new(15,13,2)));
        Assert.Empty(after.World.City!.State.Properties);Assert.Equal(4,after.World.City.State.Ground.Length);Assert.Equal(4,after.World.City.State.Producers.Length);
        Assert.Equal(4,after.World.Stations!.Listeners.Length);Assert.Equal(4,after.World.Stations.ListenerMemberships.Single(m=>m.StationId==station.Id).Providers.Length);
        Assert.Equal(new[]{P,new Location(14,13,2),new Location(15,12,2),new Location(15,13,2)},after.World.City.State.Ground.Select(g=>g.Location));
        Assert.Equal(1200000,after.Construction.Accounts.Transactions.Last().Expenditure);
        var oldIds=after.World.City.State.Ground.Select(g=>g.Id).ToArray();
        var replaced=Apply(after,Plan(after,new(15,13,2)));Assert.DoesNotContain(replaced.World.City!.State.Ground,g=>oldIds.Contains(g.Id));
        Assert.Equal(4,replaced.World.Stations!.Listeners.Length);Assert.DoesNotContain(replaced.World.Stations.Listeners,l=>oldIds.Contains(l.ProviderId));
        var removed=Apply(replaced,Plan(replaced,new(15,13,2),action:"removeCover"));Assert.Empty(removed.World.City!.State.Ground);Assert.Empty(removed.World.City.State.Producers);
        Assert.Empty(removed.World.Stations!.Listeners);Assert.Empty(removed.World.Stations.ListenerMemberships.Single(m=>m.StationId==station.Id).Providers);
        Assert.Equal(replaced.Construction.Accounts,removed.Construction.Accounts);Exact(replaced);Exact(removed);
    }
    [Fact]
    public void Fixed_anchor_height_skips_uneven_bounds_and_protected_cells_but_books_full_rectangle_including_all_blocked()
    {
        var s=Seed();var terrain=s.World.Terrain;var hv=terrain.Space.ToStorage(new(15,12,2));
        terrain=new(terrain.Profile,terrain.Name,new(40,32,16),0,terrain.Cells.SetItem(hv.V*40+hv.H,new TerrainCell(3,TerrainSurface.Cut)),new(terrain.Spatial.NextEntityId,terrain.Spatial.Entities),terrain.Occupants);
        s=LandTradingTests.With(s,s.World.City!.State,terrain);
        var p=Plan(s,new(15,12,2));Assert.Equal(new[]{"placeable","uneven"},p.Cells.Select(c=>c.Status));Assert.Equal(600000,p.Amount);Assert.Single(Apply(s,p).World.City!.State.Ground);
        // The actual constructed station protects its ground voxel.
        var station=s.World.Stations!.Buildings[0].Location;var blocked=Plan(s,anchor:station,end:station);Assert.Equal("protected",Assert.Single(blocked.Cells).Status);
        var charged=Apply(s,blocked);Assert.Empty(charged.World.City!.State.Ground);Assert.Equal(300000,charged.Construction.Accounts.Transactions.Last().Expenditure);
        Assert.Contains(Plan(s,new(0,12,2)).Cells,c=>c.Status=="outside");Assert.Throws<ArgumentException>(()=>Plan(s,anchor:P with {Z=3}));
        Assert.Throws<ArgumentException>(()=>Plan(s,new(15,12,3)));Assert.Throws<ArgumentException>(()=>Plan(s,new(int.MaxValue,12,2)));Exact(charged);
    }
    [Fact]
    public void Water_has_no_extra_land_voxel_restriction_and_rail_and_building_neighbors_remain_intact()
    {
        var s=LandTradingTests.Empty(4);var next=Apply(s,Plan(s));Assert.Single(next.World.City!.State.Ground);Exact(next);
        s=Seed();var station=s.World.Stations!.Buildings[0];var d=s.World.City!.Profile.Structures.First(d=>d.Kind==CityStructureKind.Fixed&&d.SizeX==1&&d.SizeY==1);
        s=FixedCityConstruction.Commit(s,FixedCityConstruction.Preview(Capture(s),new(18,12,2),d.Reference));
        var beforeSite=s.World.City!.State.Structures.Single();var p=Plan(s,new(18,12,2));Assert.Equal("protected",p.Cells.Last().Status);
        next=Apply(s,p);Assert.Equal(beforeSite,next.World.City!.State.Structures.Single());Assert.Equal(station.Id,next.World.Stations!.Buildings.Single().Id);Exact(next);
    }
    [Fact]
    public void General_map_demolition_removes_actual_ground_provider_and_fences_without_booking()
    {
        var s=Seed();s=Apply(s,Plan(s));
        var g=Assert.Single(s.World.City!.State.Ground);
        var f=s.World.City.Profile.Ground.First(d=>d.Handler=="fence"&&!s.World.City.Profile.Bindings.Single(b=>b.Reference==d.Reference).Implementation.EndsWith("FenceRemover",StringComparison.Ordinal));
        s=LandTradingTests.With(s,s.World.City.State with {Fences=[new(g.Id,P,0,f.Reference)]});
        var (world,kind,reason)=ObjectRemoval.Mixed(s.World,P);Assert.Null(reason);Assert.Equal("groundCover",kind);
        var next=s.Successor(s.Revision,new(world!,s.Construction.Accounts),s.ConstructionMode);MixedWire.ValidateTransition(s,next);
        Assert.Empty(next.World.City!.State.Ground);Assert.Empty(next.World.City.State.Fences);Assert.Empty(next.World.City.State.Producers);Assert.Empty(next.World.Stations!.Listeners);Exact(next);
    }
    [Fact]
    public void Capacity_invalid_handler_and_late_allocator_overflow_leave_entire_source_unchanged()
    {
        var s=LandTradingTests.Empty();var original=SaveCodec.Write(s);
        Assert.Throws<ArgumentException>(()=>Plan(s,new(100,100,2)));
        Assert.Throws<ArgumentException>(()=>Plan(s,definition:s.World.City!.Profile.Ground.First(d=>d.Handler is not ("land-0d" or "random-land")).Reference));
        Assert.Equal(original,SaveCodec.Write(s));
        var w=new MixedWorld(s.World.Profile,s.World.Terrain,new(long.MaxValue-1,[]),[],[],s.World.Revision,city:s.World.City);
        s=new(new(w),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);original=SaveCodec.Write(s);
        Assert.Throws<OverflowException>(()=>Plan(s,new(15,12,2)));Assert.Equal(original,SaveCodec.Write(s));
        Assert.Throws<OverflowException>(()=>Plan(s,new(15,12,2),s.World.City!.Profile.Ground.First(d=>d.Handler=="random-land").Reference));Assert.Equal(original,SaveCodec.Write(s));
    }
    [Fact]
    public void Actual_rail_neighbors_are_skipped_and_terrain_tools_protect_cover_while_editing_other_columns()
    {
        var s=Seed();var rail=NormalRailwayConstruction.PreviewBuild(s.World,new(16,12,2),new(16,14,2));Assert.True(rail.Legal);
        s=s.Successor(s.Revision,NormalRailwayConstruction.Apply(s.Construction,rail,s.Clock.Time),s.ConstructionMode);
        var voxels=s.World.Voxels;var plan=Plan(s,new(16,12,2));Assert.Equal(new[]{"placeable","placeable","protected"},plan.Cells.Select(c=>c.Status));Assert.Equal(900000,plan.Amount);
        s=Apply(s,plan);Assert.Equal(voxels.ToArray(),s.World.Voxels.ToArray());var ground=s.World.City!.State.Ground;
        var draft=MixedTerrain.Draft(s.World);Assert.False(draft.Preview(P,"raise").Legal);
        var far=draft.Preview(new(22,20,2),"raise");Assert.True(far.Legal);draft.Apply(far);
        s=s.Successor(s.Revision,MixedTerrain.Replace(s.Construction,draft.Finish(),s.Clock.Time),s.ConstructionMode);
        Assert.Equal(ground,s.World.City!.State.Ground);Assert.Equal(voxels.ToArray(),s.World.Voxels.ToArray());Exact(s);
    }
    [Fact]
    public async Task First_activation_cover_preview_is_private_and_commit_registers_exactly_one_heat_timer()
    {
        var s=StationBuildingConstructionTests.Empty();using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var original=await Save(h);Assert.Null(h.Snapshot().Mixed!.City);
        var q=Query(h);var p=h.PreviewLand(q);Assert.True(p.Legal);Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new TradeLandCommand(q,p.Amount)));
        Assert.Equal(4,h.Snapshot().Mixed!.City!.Ground.Length);Assert.Single(h.Snapshot().Mixed!.Queue!.Pending,a=>a.HandlerId==CityHandlers.Heat);
        Exact(SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!);
    }
    static MixedSessionState WithLiteralLandDraws(MixedSessionState s)
    {
        // Two source samples select the last and first candidate, respectively.
        var random=s.Random.Capture();var values=Enumerable.Repeat(1,55).ToImmutableArray().SetItem(0,int.MaxValue-1).SetItem(21,0).SetItem(1,0).SetItem(22,0);
        random=random with {Streams=random.Streams.Select(r=>r.Id=="land-selection"?r with {State=new(0,values)}:r).ToImmutableArray()};
        return new(s.Construction,s.Provenance,clock:s.Clock.Capture(),random:random,revision:s.Revision);
    }
    [Fact]
    public void All_five_mix_selectors_bind_literal_ordered_candidates_draw_only_eligible_cells_and_continue_exactly_after_reopen()
    {
        string prefix="{168C94A9-1D5B-4160-BBD8-21C830DCB752}-";
        string[][] expected=[["0,0","1,0"],["2,0","3,0","0,1","1,1","2,1","3,1"],["0,3","1,3","2,3","3,3","0,4","1,4"],["0,0","1,0","0,2","1,2","2,2","3,2"],["2,4","3,4"]];
        var defs=RailwayProfiles.Bundled.City!.Ground.Where(d=>d.Handler=="random-land").ToArray();Assert.Equal(5,defs.Length);
        for(int i=0;i<defs.Length;i++)
        {
            var d=defs[i];Assert.Equal(expected[i].Select(v=>prefix+v),d.Selections.Select(r=>r.ContributionId));Assert.Null(d.Population);
            var s=WithLiteralLandDraws(Seed());var original=SaveCodec.Write(s);var p=Plan(s,new(13,12,2),d.Reference,anchor:new(11,12,2));
            Assert.Equal(new[]{"placeable","protected","placeable"},p.Cells.Select(c=>c.Status));Assert.Equal(900000,p.Amount);Assert.Equal(original,SaveCodec.Write(s));
            Assert.Equal(new[]{prefix+expected[i][^1],prefix+expected[i][0]},p.Candidate.Construction.World.City!.State.Ground.Select(g=>g.Definition.ContributionId));
            Assert.Equal(2,p.Candidate.Random.Streams.Single(r=>r.Id=="land-selection").State.Index);
            var after=Apply(s,p);var reopened=SaveCodec.ReadWorld(SaveCodec.Write(after),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
            var a=Apply(after,Plan(after,new(15,13,2),d.Reference));var b=Apply(reopened,Plan(reopened,new(15,13,2),d.Reference));Assert.Equal(SaveCodec.Write(a),SaveCodec.Write(b));
            Assert.All(a.World.City!.State.Ground,g=>Assert.Contains(g.Definition,d.Selections));
            var blocked=Plan(s,new(12,12,2),d.Reference,anchor:new(12,12,2));Assert.Equal(0,blocked.Candidate.Random.Streams.Single(r=>r.Id=="land-selection").State.Index);
            foreach(var stream in s.Random.Capture().Streams.Where(r=>r.Id!="land-selection"))Assert.Equal(JsonSerializer.Serialize(stream.State),JsonSerializer.Serialize(p.Candidate.Random.Streams.Single(r=>r.Id==stream.Id).State));
        }
    }
    [Fact]
    public async Task Mixed_preview_is_private_and_different_later_random_choices_require_renewed_review()
    {
        var mixed=RailwayProfiles.Bundled.City!.Ground.First(d=>d.Handler=="random-land");var s=WithLiteralLandDraws(Seed());
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var q=Query(h) with {Definition=new(mixed.Reference.PackId,mixed.Reference.ContributionId,mixed.Reference.RecipeId)};
        var p=h.PreviewLand(q);Assert.True(p.Legal);var original=await Save(h);Assert.Equal(original,await Save(h));
        var other=q with {Anchor=new(20,20,2),End=new(20,20,2)};var next=h.PreviewLand(other);Assert.IsType<AcceptedMessage>(await Send(h,new TradeLandCommand(other,next.Amount)));
        var saved=await Save(h);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount))).Code);Assert.Equal(saved,await Save(h));
        p=h.PreviewLand(Query(h) with {Definition=q.Definition});Assert.IsType<AcceptedMessage>(await Send(h,new TradeLandCommand(p.Query,p.Amount)));
    }
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload,string? id=null,ParticipantContext? who=null)
    {var t=h.Enqueue(who??new("owner"),new(id??Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await t;}
    static LandQuery Query(SessionHost h,string action="cover"){var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,action,new(14,12,2),new(15,13,2),action=="cover"?new(D.Reference.PackId,D.Reference.ContributionId,D.Reference.RecipeId):null);}
    [Fact]
    public async Task Authoritative_cover_review_roles_wrong_cost_retry_removal_and_reopen_preserve_exact_state()
    {
        using var h=new SessionHost(Seed(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);var original=await Save(h);var q=Query(h);var p=h.PreviewLand(q);Assert.True(p.Legal);Assert.Equal(1200000,p.Amount);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount),who:observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,1))).Code);Assert.Equal(original,await Save(h));
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var now=DateTimeOffset.UtcNow;var connection=network.Connect(now,new ParticipantContext("owner"))!;
        var command=new CommandMessage("cover-retry",h.WorldEpoch,new TradeLandCommand(q,p.Amount));
        var task=network.Submit(connection,command,now);network.Tick(now);var result=await task;Assert.IsType<AcceptedMessage>(result);var saved=await Save(h);
        Assert.Equal(result,await network.Submit(connection,command,now));Assert.Equal(saved,await Save(h));
        var next=SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;Exact(next);
        using var reopened=new SessionHost(next,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);p=reopened.PreviewLand(Query(reopened,"removeCover"));Assert.True(p.Legal);Assert.Equal(0,p.Amount);
        Assert.IsType<AcceptedMessage>(await Send(reopened,new TradeLandCommand(p.Query,0)));Assert.Empty(reopened.Snapshot().Mixed!.City!.Ground);Assert.Empty(reopened.Snapshot().Mixed!.StationGraph!.Listeners);
    }
    [Fact]
    public void Static_ground_browser_seed_and_real_successors_export_exact_providers_and_teardown()
    {
        var s=Seed();var before=MixedWire.Snapshot(s,1);var after=Apply(s,Plan(s,new(15,13,2)));Exact(after);
        var removed=Apply(after,Plan(after,new(15,13,2),action:"removeCover"));Exact(removed);
        if(Environment.GetEnvironmentVariable("FREETRAIN_STATIC_GROUND_OUTPUT") is {} path)
        {
            var options=WireJson.CreateOptions();File.WriteAllBytes(path,SaveCodec.Write(s));
            void Write(string suffix,WireMessage value)=>File.WriteAllText(path+suffix,JsonSerializer.Serialize(value,options));
            Write(".before.json",before);Write(".snapshot.json",MixedWire.Snapshot(after,1));Write(".delta.json",StateDifference.Between(before,MixedWire.Snapshot(after,1)));
            Write(".removed.json",MixedWire.Snapshot(removed,1));Write(".removed.delta.json",StateDifference.Between(MixedWire.Snapshot(after,1),MixedWire.Snapshot(removed,1)));
        }
    }
}
