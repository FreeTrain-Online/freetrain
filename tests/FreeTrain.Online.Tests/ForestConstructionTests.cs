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

public class ForestConstructionTests
{
    static readonly Location P=new(14,12,2);
    static readonly DefinitionReference D=TerrainProfiles.Bundled.Definitions.First(d=>d.TreeDensity>0).Reference;
    static CityCandidate Capture(MixedSessionState s)=>new(s.Construction,s.Clock.Capture(),s.Random.Capture());
    static LandTradePlan Plan(MixedSessionState s,DefinitionReference? d=null,Location? a=null,Location? b=null)=>ForestConstruction.Preview(Capture(s),a??P,b??a??P,d??D);
    static MixedSessionState Apply(MixedSessionState s,LandTradePlan p){var n=LandTrading.Commit(s,p);MixedWire.ValidateTransition(s,n);return n;}
    static MixedSessionState Reopen(MixedSessionState s){var bytes=SaveCodec.Write(s);var n=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;Assert.Equal(bytes,SaveCodec.Write(n));return n;}
    static MixedSessionState Draws(MixedSessionState s,bool zero=false)
    {
        // Literal valid subtractive-bank input: initial zero samples create density*2
        // trees at (0,8), pattern0; six near-one samples create zero cedar trees.
        var values=ImmutableArray.CreateRange(Enumerable.Repeat(0,55)).SetItem(54,1);
        if(zero)for(int i=0;i<6;i++)values=values.SetItem(i,int.MaxValue-1);
        var random=s.Random.Capture();random=random with {Streams=random.Streams.Select(r=>r.Id=="forest"?r with {State=new(0,values)}:r).ToImmutableArray()};
        return new(s.Construction,s.Provenance,clock:s.Clock.Capture(),random:random,revision:s.Revision);
    }
    static MixedSessionState Seed()
    {
        var s=Draws(LandTradingTests.Empty());
        return StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,new(12,12,2),StationStateTests.Definition.Reference));
    }
    [Fact]
    public void All_eighteen_actual_definitions_use_source_density_price_owned_mountain_and_no_provider_or_timer()
    {
        var defs=TerrainProfiles.Bundled.Definitions.Where(d=>d.TreeDensity>0).ToArray();Assert.Equal(18,defs.Length);
        foreach(var d in defs)
        {
            var s=Seed();var bytes=SaveCodec.Write(s);var plan=Plan(s,d.Reference);Assert.Equal(bytes,SaveCodec.Write(s));Assert.Equal(100,plan.Amount);
            var after=Apply(s,plan);var o=Assert.Single(after.World.Terrain.Occupants);var e=after.World.Spatial.At(P).Entity!;
            Assert.Equal(TerrainOccupantKind.Mountain,o.Kind);Assert.Equal(d.Reference,o.Forest!.Definition);Assert.Equal(2*d.TreeDensity,o.Forest.Trees.Length);
            Assert.All(o.Forest.Trees,t=>Assert.Equal(new TerrainTree(0,8,0),t));Assert.True(e.Owned);Assert.False(e.SilentlyReclaimable);Assert.Equal(100,e.Value);
            Assert.Empty(after.World.City!.State.Ground);Assert.Empty(after.World.City.State.Producers);Assert.Empty(after.World.Stations!.Listeners);
            Assert.Equal(JsonSerializer.Serialize(s.Clock.Capture()),JsonSerializer.Serialize(after.Clock.Capture()));
            Assert.Equal(s.Provenance,after.Provenance);Assert.Equal(s.World.Terrain.Cells,after.World.Terrain.Cells);
            foreach(var r in s.Random.Capture().Streams.Where(r=>r.Id!="forest"))Assert.Equal(JsonSerializer.Serialize(r.State),JsonSerializer.Serialize(after.Random.Capture().Streams.Single(x=>x.Id==r.Id).State));
            Assert.Equal(AccountGenre.Subsidiaries,after.Construction.Accounts.Transactions.Last().Genre);Reopen(after);
        }
    }
    [Fact]
    public void Forest_replaces_owned_farmland_and_detaches_both_listener_directions_but_zero_result_preserves_it()
    {
        var s=Seed();var farm=s.World.City!.Profile.Ground.First(d=>d.Handler=="land-0d");
        s=Apply(s,StaticGroundConstruction.Preview(Capture(s),P,P,"cover",farm.Reference));var original=SaveCodec.Write(s);
        var zero=Draws(s,true);var plan=Plan(zero);Assert.Equal(original,SaveCodec.Write(s));Assert.Empty(plan.Reclaimed);
        var kept=Apply(zero,plan);Assert.Equal(s.World.City!.State.Ground.ToArray(),kept.World.City!.State.Ground.ToArray());Assert.Equal(s.World.Stations!.Listeners.ToArray(),kept.World.Stations!.Listeners.ToArray());
        Assert.Empty(kept.World.Terrain.Occupants);Assert.Equal(100,kept.Construction.Accounts.Transactions.Last().Expenditure);Assert.Equal(6,kept.Random["forest"].Capture().Index);
        var after=Apply(s,Plan(s));Assert.Empty(after.World.City!.State.Ground);Assert.Empty(after.World.City.State.Producers);Assert.Empty(after.World.Stations!.Listeners);
        Assert.Empty(after.World.Stations.ListenerMemberships.Single().Providers);Assert.Single(after.World.Terrain.Occupants);Reopen(after);Reopen(kept);
    }
    [Fact]
    public void Replanting_owned_mountain_retains_identity_corners_and_zero_result_preserves_old_treatment()
    {
        var s=Seed();var draft=MixedTerrain.Draft(s.World);var raised=draft.Preview(P,"raise");Assert.True(raised.Legal);draft.Apply(raised);
        s=s.Successor(s.Revision,MixedTerrain.Replace(s.Construction,draft.Finish(),s.Clock.Time),false);
        var e=s.World.Spatial.At(P).Entity!;var corners=s.World.Terrain.OccupantAt(P)!.Corners;
        Assert.False(corners.Flat);s=Apply(s,Plan(s));var planted=s.World.Spatial.At(P).Entity!;Assert.Equal(e.Id,planted.Id);Assert.True(planted.Owned);Assert.Equal(corners,s.World.Terrain.OccupantAt(P)!.Corners);
        var changed=Apply(s,Plan(s,TerrainProfiles.Bundled.Definitions.Last(d=>d.TreeDensity>0).Reference));Assert.Equal(e.Id,changed.World.Spatial.At(P).Entity!.Id);
        var zero=Draws(changed,true);var kept=Apply(zero,Plan(zero));Assert.Equal(changed.World.Terrain.Occupants,kept.World.Terrain.Occupants);Assert.Equal(changed.World.Spatial.At(P).Entity,kept.World.Spatial.At(P).Entity);
        var cleared=Apply(changed,StaticGroundConstruction.Preview(Capture(changed),P,P,"removeCover",null));var bare=cleared.World.Spatial.At(P).Entity!;
        Assert.Equal(e.Id,bare.Id);Assert.False(bare.Owned);Assert.Equal(0,bare.Value);Assert.Equal(corners,cleared.World.Terrain.OccupantAt(P)!.Corners);Assert.Null(cleared.World.Terrain.OccupantAt(P)!.Forest);
        var (removed,kind,reason)=ObjectRemoval.Mixed(changed.World,P);Assert.Null(reason);Assert.Equal("forest",kind);Assert.False(removed!.Spatial.At(P).Entity!.Owned);
        Reopen(kept);Reopen(cleared);
    }
    [Fact]
    public void Full_rectangle_charge_skips_protected_uneven_and_outside_without_random_draws_and_water_is_allowed()
    {
        var s=Seed();var plan=Plan(s,a:new(11,12,2),b:new(13,12,2));Assert.Equal(new[]{"placeable","protected","placeable"},plan.Cells.Select(c=>c.Status));Assert.Equal(300,plan.Amount);
        var after=Apply(s,plan);Assert.Equal(48,after.Random["forest"].Capture().Index);Assert.Equal(2,after.World.Terrain.Occupants.Length);
        var blocked=Plan(s,a:new(12,12,2));Assert.Equal(100,blocked.Amount);Assert.Equal(JsonSerializer.Serialize(s.Random.Capture()),JsonSerializer.Serialize(blocked.Candidate.Random));Apply(s,blocked);
        Assert.Contains(Plan(s,b:new(0,12,2)).Cells,c=>c.Status=="outside");
        var terrain=s.World.Terrain;var hv=terrain.Space.ToStorage(new(15,12,2));
        terrain=new(terrain.Profile,terrain.Name,new(40,32,16),0,terrain.Cells.SetItem(hv.V*40+hv.H,new TerrainCell(3,TerrainSurface.Cut)),terrain.Spatial.Capture(),terrain.Occupants);
        var hill=LandTradingTests.With(s,s.World.City!.State,terrain);
        Assert.Contains(Plan(hill,b:new(15,12,2)).Cells,c=>c.Status=="uneven");
        var wet=Draws(LandTradingTests.Empty(4));Assert.Single(Apply(wet,Plan(wet)).World.Terrain.Occupants);Reopen(after);
    }
    [Fact]
    public void Exact_random_save_continuation_and_late_allocator_failure_are_atomic()
    {
        var s=Seed();s=Apply(s,Plan(s,b:new(15,13,2)));var reopened=Reopen(s);
        Assert.Equal(SaveCodec.Write(Apply(s,Plan(s,a:new(16,14,2),b:new(18,15,2)))),SaveCodec.Write(Apply(reopened,Plan(reopened,a:new(16,14,2),b:new(18,15,2)))));
        var w=new MixedWorld(s.World.Profile,s.World.Terrain,new(long.MaxValue-1,s.World.Spatial.Entities),[],[],s.World.Revision,stations:s.World.Stations,city:s.World.City);
        var full=new MixedSessionState(new(w,s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),random:s.Random.Capture(),revision:s.Revision);var bytes=SaveCodec.Write(full);
        Assert.ThrowsAny<Exception>(()=>Plan(full,a:new(16,14,2),b:new(18,15,2)));Assert.Equal(bytes,SaveCodec.Write(full));
        Assert.Throws<ArgumentException>(()=>Plan(s,b:new(100,100,2)));Assert.Throws<ArgumentException>(()=>Plan(s,s.World.City!.Profile.Ground.First(d=>d.Handler=="land-0d").Reference));
    }
    static async Task<byte[]> Save(SessionHost h){var task=h.CaptureSave();h.Pump(0);return await task;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload,ParticipantContext? who=null,string? id=null){var task=h.Enqueue(who??new("owner"),new(id??Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await task;}
    static LandQuery Query(SessionHost h,DefinitionReference? definition=null){var s=h.Snapshot();var d=definition??D;return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,"cover",new(14,12,2),new(15,13,2),new(d.PackId,d.ContributionId,d.RecipeId));}
    [Fact]
    public async Task Paid_forest_host_preview_roles_wrong_cost_duplicate_receipt_stale_quotes_and_removal()
    {
        using var h=new SessionHost(Seed(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);var original=await Save(h);var q=Query(h);var p=h.PreviewLand(q);Assert.True(p.Legal);Assert.Equal(400,p.Amount);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount),observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,1))).Code);Assert.Equal(original,await Save(h));
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);var now=DateTimeOffset.UtcNow;var connection=network.Connect(now,new ParticipantContext("owner"))!;
        var command=new CommandMessage("forest-retry",h.WorldEpoch,new TradeLandCommand(q,p.Amount));var task=network.Submit(connection,command,now);network.Tick(now);
        var receipt=await task;Assert.IsType<AcceptedMessage>(receipt);var saved=await Save(h);
        Assert.Equal(receipt,await network.Submit(connection,command,now));Assert.Equal(saved,await Save(h));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new TradeLandCommand(q,p.Amount),connection.Participant)).Code);Assert.Equal(saved,await Save(h));
        var remove=Query(h) with {Action="removeCover",Definition=null};var preview=h.PreviewLand(remove);Assert.True(preview.Legal);Assert.Equal(0,preview.Amount);
        Assert.IsType<AcceptedMessage>(await Send(h,new TradeLandCommand(remove,0),connection.Participant));Assert.Empty(h.Snapshot().Terrain!.Occupants);
    }
    [Fact]
    public void Generated_forest_replants_and_removes_with_unchanged_provenance_and_live_stream_isolation()
    {
        var r=global::ForestTerrainTests.Request(70,0);var terrain=ReliefTerrain.Create(r,TerrainProfiles.Bundled);
        var s=CityHeat.Enable(new MixedSessionState(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain)),TerrainProvenance.From(r)));
        s=Draws(s);var o=s.World.Terrain.Occupants.First(o=>o.Forest!=null);var e=s.World.Spatial.Entities.Single(e=>e.Id==o.EntityId);
        Assert.False(e.Owned);var after=Apply(s,Plan(s,a:e.Anchor));var planted=after.World.Spatial.At(e.Anchor).Entity!;
        Assert.Equal(e.Id,planted.Id);Assert.True(planted.Owned);Assert.Equal(o.Corners,after.World.Terrain.OccupantAt(e.Anchor)!.Corners);Assert.Equal(s.Provenance,after.Provenance);
        var other=s.World.Terrain.Occupants.Where(v=>v.EntityId!=e.Id).ToArray();Assert.Equal(other,after.World.Terrain.Occupants.Where(v=>v.EntityId!=e.Id).ToArray());
        var reopened=Reopen(after);Assert.Equal(SaveCodec.Write(Apply(after,Plan(after,a:e.Anchor))),SaveCodec.Write(Apply(reopened,Plan(reopened,a:e.Anchor))));
        var cleared=Apply(after,StaticGroundConstruction.Preview(Capture(after),e.Anchor,e.Anchor,"removeCover",null));Assert.Null(cleared.World.Terrain.OccupantAt(e.Anchor)?.Forest);Reopen(cleared);
    }
    [Theory]
    [InlineData(1980)]
    [InlineData(84959)] // February 28 -> March 1, Winter -> Spring.
    [InlineData(217439)] // May 31 -> June 1.
    [InlineData(349919)] // August 31 -> September 1.
    [InlineData(480959)] // November 30 -> December 1.
    public void Day_and_season_boundaries_do_not_age_resample_or_schedule_forests(long minuteBefore)
    {
        var empty=LandTradingTests.Empty();
        var s=Draws(CityHeat.Enable(new MixedSessionState(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,empty.World.Terrain)),empty.Provenance,clock:new(minuteBefore,0,[]))));
        s=Apply(s,Plan(s));var start=Capture(s);var working=start;
        for(int minute=0;minute<1440;minute++)working=CityConstruction.AdvanceOneMinute(working);
        Assert.Equal(s.World.Terrain.Occupants,working.Construction.World.Terrain.Occupants);
        Assert.Equal(JsonSerializer.Serialize(start.Random),JsonSerializer.Serialize(working.Random));Assert.Single(working.Clock.Pending,a=>a.HandlerId==CityHandlers.Heat);
        Assert.Equal(s.Construction.Accounts,working.Construction.Accounts);
    }
    [Fact]
    public void Forest_browser_seed_and_real_successor_fixtures()
    {
        var s=Seed();var before=MixedWire.Snapshot(s,1);var after=Apply(s,Plan(s,b:new(15,13,2)));Reopen(after);
        var removed=Apply(after,StaticGroundConstruction.Preview(Capture(after),P,new(15,13,2),"removeCover",null));Reopen(removed);
        var path=Environment.GetEnvironmentVariable("FREETRAIN_FOREST_OUTPUT");if(path==null)return;
        File.WriteAllBytes(path,SaveCodec.Write(s));var options=WireJson.CreateOptions();
        void Write(string suffix,WireMessage value)=>File.WriteAllText(path+suffix,JsonSerializer.Serialize(value,options));
        Write(".before.json",before);Write(".snapshot.json",MixedWire.Snapshot(after,1));Write(".delta.json",StateDifference.Between(before,MixedWire.Snapshot(after,1)));
        Write(".removed.json",MixedWire.Snapshot(removed,1));Write(".removed.delta.json",StateDifference.Between(MixedWire.Snapshot(after,1),MixedWire.Snapshot(removed,1)));
    }
}
