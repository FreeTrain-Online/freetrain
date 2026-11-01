using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class MixedTerrainIntegrationTests
{
    static Location P=>CrossToolDependencyTests.P;
    static Location Safe=>P with {X=P.X+7,Y=P.Y+5};
    static TerrainProvenance Provenance(MixedWorld w)=>TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0));
    static SessionHost Host(MixedWorld w)=>new(new MixedSessionState(new(w),Provenance(w),revision:w.Revision),"owner",TerrainProfiles.Resolve,(_,_,_)=>w.Profile);
    static CornerTerrainQuery Q(SessionHost h,Location? p=null,string action="raise") {
        var s=h.Snapshot();var site=p??Safe;return new(s.WorldEpoch,s.Mixed!.GeometryRevision,new(site.X,site.Y,site.Z),action);
    }
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload,ParticipantContext? who=null){
        var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await t;
    }
    [Theory]
    [InlineData(RailwayHandler.Normal)][InlineData(RailwayHandler.Junction)][InlineData(RailwayHandler.Bridge)]
    [InlineData(RailwayHandler.Girder)][InlineData(RailwayHandler.Garage)][InlineData(RailwayHandler.Signal)][InlineData(RailwayHandler.Tunnel)]
    public async Task Every_rail_subtype_blocks_terrain_at_occupancy_and_allows_atomic_edits_elsewhere(RailwayHandler handler)
    {
        var w=CrossToolDependencyTests.Rail(handler);using var h=Host(w);var before=await Save(h);
        foreach(var action in new[]{"raise","lower","placeTerrace","removeCut"}){
            var q=Q(h,P,action);Assert.False(h.PreviewCorner(q).Legal);
            Assert.IsType<RejectedMessage>(await Send(h,new EditCornerCommand(q)));Assert.Equal(before,await Save(h));
        }
        var preview=h.PreviewCorner(Q(h));Assert.True(preview.Legal,preview.Reason);Assert.Equal(4,preview.Changes.Length);Assert.Equal(before,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new EditCornerCommand(Q(h))));
        var after=SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,(_,_,_)=>w.Profile).Mixed!;
        Assert.True(w.Voxels.SequenceEqual(after.World.Voxels));Assert.Equal(JsonSerializer.Serialize(w.Spatial.Entities),JsonSerializer.Serialize(after.World.Spatial.Entities.Where(e=>e.Id<w.Spatial.NextEntityId)));
        Assert.True(after.World.Terrain.Spatial.Entities.All(e=>e.Id>=w.Spatial.NextEntityId));
        Assert.Equal(w.Spatial.NextEntityId+4,after.World.Spatial.NextEntityId);Assert.Empty(after.Construction.Accounts.Transactions);
        Assert.Equal(w.Terrain.TunnelBackups,after.World.Terrain.TunnelBackups);
        var saved=await Save(h);using var reopen=new SessionHost(after,"owner",TerrainProfiles.Resolve,(_,_,_)=>w.Profile);Assert.Equal(saved,await Save(reopen));
    }
    [Fact]
    public void Filler_support_wall_pier_and_stationary_occupancy_remain_protected_and_stored_flags_survive()
    {
        foreach(var underground in new[]{false,true}){
            var empty=GarageConstructionTests.World();var build=SlopeRailwayConstruction.PreviewBuild(empty,P with {Z=underground?1:2},new(2));Assert.True(build.Legal,build.Reason);
            var w=SlopeRailwayConstruction.Apply(new(empty),build,new(0)).World;
            var draft=MixedTerrain.Draft(w);
            foreach(var v in build.Members){Assert.Equal(v.OwnerId,draft.At(v.Position).Entity!.Id);Assert.False(TerraceTerrain.Preview(draft,v.Position,"placeTerrace").Legal);}
            var edit=draft.Preview(Safe,"raise");Assert.True(edit.Legal);draft.Apply(edit);
            var after=MixedTerrain.Replace(new(w),draft.Finish(),new(0)).World;
            Assert.True(w.Voxels.SequenceEqual(after.Voxels));Assert.True(w.Slopes.SequenceEqual(after.Slopes));
        }
        var bare=GarageConstructionTests.World();var q=BridgeRailwayConstruction.Preview(bare,P with{Z=3},P with{X=P.X+2,Z=3});
        var bridge=BridgeRailwayConstruction.Apply(new(bare),q,new(0)).World;
        foreach(var pier in bridge.Voxels.Where(v=>v.Kind==RailwayVoxelKind.Pier)){
            var draft=MixedTerrain.Draft(bridge);Assert.Equal(pier.OwnerId,draft.At(pier.Position).Entity!.Id);
            Assert.False(draft.Preview(pier.Position,"raise").Legal);Assert.False(TerraceTerrain.Preview(draft,pier.Position,"placeTerrace").Legal);
        }
        var type=bare.Profile.Bindings.First(b=>b.Stationary?.Height==2).Reference.ContributionId;
        var site=StationaryConstruction.Commit(new(new(bare),Provenance(bare)),StationaryConstruction.Preview(bare,P,type));
        var terrain=MixedTerrain.Draft(site.World);Assert.False(terrain.Preview(P,"raise").Legal);Assert.False(TerraceTerrain.Preview(terrain,P,"placeTerrace").Legal);
        var plan=terrain.Preview(Safe,"raise");terrain.Apply(plan);
        var next=site.Successor(site.Revision,MixedTerrain.Replace(site.Construction,terrain.Finish(),site.Clock.Time),site.ConstructionMode);
        Assert.Equal(JsonSerializer.Serialize(site.Clock.Capture()),JsonSerializer.Serialize(next.Clock.Capture()));Assert.True(site.World.Voxels.SequenceEqual(next.World.Voxels));
    }
    [Fact]
    public async Task Mixed_terrain_mode_decorations_rectangle_skips_and_repair_preserve_accounts_clock_and_random()
    {
        var w=CrossToolDependencyTests.Rail(RailwayHandler.Normal);using var h=Host(w);var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        var original=await Save(h);var q=Q(h);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new EditCornerCommand(q),observer)).Code);
        var detached=SaveCodec.ReadWorld(original,TerrainProfiles.Resolve,(_,_,_)=>w.Profile).Mixed!;
        var modeCandidate=MixedTerrain.Replace(detached.Construction,MixedTerrain.Draft(detached.World).Finish(),detached.Clock.Time);
        MixedWire.ValidateTransition(detached,detached.Successor(detached.Revision,modeCandidate,true));
        var modeResult=await Send(h,new SetAuthoringModeCommand(h.Snapshot().Mixed!.GeometryRevision,true));
        Assert.True(modeResult is AcceptedMessage,JsonSerializer.Serialize(modeResult));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditCornerCommand(q))).Code);
        foreach(var action in new[]{"plantForest","placeCreek"}){
            var forest=w.Profile.Terrain.Definitions.First(d=>d.TreeDensity>0).Reference;
            q=Q(h,P,action) with {Rectangle=new(action=="plantForest"?new(P.X+1,P.Y,P.Z):new(P.X,P.Y+1,P.Z)),Forest=action=="plantForest"?new(forest.PackId,forest.ContributionId,forest.RecipeId):null};
            var before=await Save(h);var preview=h.PreviewCorner(q);Assert.True(preview.Legal,preview.Reason);Assert.Equal(new PositionRead(P.X,P.Y,P.Z),Assert.Single(preview.Skipped).Position);Assert.Equal(before,await Save(h));
            Assert.IsType<AcceptedMessage>(await Send(h,new EditCornerCommand(q)));Assert.True(w.Voxels.SequenceEqual(SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,(_,_,_)=>w.Profile).Mixed!.World.Voxels));
        }
        // Inclusive terrace rectangle: occupied first cell skips, following cells
        // still apply in source X/Y order in one durable geometry revision.
        q=Q(h,P,"placeTerrace") with {Rectangle=new(new(P.X+2,P.Y,P.Z))};
        var quote=h.PreviewCorner(q);Assert.True(quote.Legal);Assert.Single(quote.Skipped);Assert.Equal(new[]{P.X+1,P.X+2},quote.Changes.Select(c=>c.Position.X));
        var revision=h.Snapshot().Mixed!.GeometryRevision;Assert.IsType<AcceptedMessage>(await Send(h,new EditCornerCommand(q)));Assert.Equal(revision+1,h.Snapshot().Mixed!.GeometryRevision);
        var current=SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,(_,_,_)=>w.Profile).Mixed!;
        Assert.True(w.Voxels.SequenceEqual(current.World.Voxels));Assert.Empty(current.Construction.Accounts.Transactions);Assert.Equal(new Clock().Time.AbsoluteMinute,current.Clock.Time.AbsoluteMinute);Assert.True(current.ConstructionMode);
        var beforeRandom=JsonSerializer.Serialize(current.Random.Capture());
        Assert.IsType<AcceptedMessage>(await Send(h,new EditCornerCommand(Q(h,Safe))));
        Assert.IsType<AcceptedMessage>(await Send(h,new SetAuthoringModeCommand(h.Snapshot().Mixed!.GeometryRevision,false)));
        current=SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,(_,_,_)=>w.Profile).Mixed!;
        Assert.Equal(beforeRandom,JsonSerializer.Serialize(current.Random.Capture()));Assert.False(current.ConstructionMode);Assert.NotEqual(original,await Save(h));
    }
    [Fact]
    public async Task Railway_edits_fence_terrain_drafts_even_when_terrain_revision_has_not_changed()
    {
        var w=CrossToolDependencyTests.Rail(RailwayHandler.Normal);using var h=Host(w);var q=Q(h);Assert.True(h.PreviewCorner(q).Legal);
        var s=h.Snapshot();var rail=new RailwayQuery(s.WorldEpoch,s.Mixed!.GeometryRevision,s.ContentVersion,new(P.X,P.Y,P.Z),new(P.X,P.Y,P.Z),"lay","source","pole");
        Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(rail)));
        var after=await Save(h);Assert.Equal(s.Terrain!.GeometryRevision,h.Snapshot().Terrain!.GeometryRevision);
        Assert.Equal("staleRevision",Assert.Throws<InvalidOperationException>(()=>h.PreviewCorner(q)).Message);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditCornerCommand(q))).Code);Assert.Equal(after,await Save(h));
    }

    [Fact]
    public void Terrace_backup_is_protected_under_track_then_restores_after_track_removal()
    {
        var bare=GarageConstructionTests.World();var draft=MixedTerrain.Draft(bare);
        var terrace=TerraceTerrain.Preview(draft,P,"placeTerrace");Assert.True(terrace.Legal);TerraceTerrain.Apply(draft,terrace);
        var w=MixedTerrain.Replace(new(bare),draft.Finish(),new(0)).World;
        var original=w.Terrain.OccupantAt(P)!;var start=P with{Z=3};var end=start with{X=start.X+2};
        var rail=NormalRailwayConstruction.PreviewBuild(w,start,end);Assert.True(rail.Legal,rail.Reason);
        w=NormalRailwayConstruction.Apply(new(w),rail,new(0)).World;
        Assert.False(TerraceTerrain.Preview(MixedTerrain.Draft(w),P,"removeTerrace").Legal);
        Assert.Null(ObjectRemoval.Mixed(w,P).Candidate);Assert.Equal(original,w.Terrain.OccupantAt(P));
        var removal=NormalRailwayConstruction.PreviewRemoval(w,start,end);Assert.True(removal.Legal,removal.Reason);
        w=NormalRailwayConstruction.Apply(new(w),removal,new(0)).World;
        Assert.Equal(original,w.Terrain.OccupantAt(P));draft=MixedTerrain.Draft(w);
        var restore=TerraceTerrain.Preview(draft,P,"removeTerrace");Assert.True(restore.Legal,restore.Reason);TerraceTerrain.Apply(draft,restore);
        w=MixedTerrain.Replace(new(w),draft.Finish(),new(0)).World;
        Assert.Equal(original.Corners,w.Terrain.OccupantAt(P)!.Corners);var storage=w.Terrain.Space.ToStorage(P);Assert.Equal(2,w.Terrain.Cell(storage.H,storage.V).Ground);
    }
}
