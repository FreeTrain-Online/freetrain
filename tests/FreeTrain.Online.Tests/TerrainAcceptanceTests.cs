using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class TerrainAcceptanceTests
{
    private static readonly JsonSerializerOptions WireOptions = WireJson.CreateOptions();

    static async Task<byte[]> Save(SessionHost host)
    { var pending=host.CaptureSave();host.Pump(0);return await pending; }
    static async Task Send(SessionHost host,CommandPayload command)
    { var pending=host.Enqueue(new("owner"),new(Guid.NewGuid().ToString(),1,command));host.Pump(0);Assert.IsType<AcceptedMessage>(await pending); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Incremental_edits_equal_full_restore_validation_and_generic_publication(bool dense)
    {
        var initial=dense?TerrainCapacityScenario.Create(32,48,8):new TerrainSessionState(
            TerrainWorld.Empty(new(WorldCreationProfile.Id,"Acceptance 日本語",new(32,48,8),1),TerrainProfiles.Bundled),
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,"Acceptance 日本語",new(32,48,8),1)));
        using var host=new SessionHost(initial,"owner",TerrainProfiles.Resolve);
        var random=new Random(17);var history=new List<SnapshotMessage>{host.Snapshot()};
        await Send(host,new SetAuthoringModeCommand(initial.World.Revision,true));
        var forest=TerrainProfiles.Bundled.Definitions.First(d=>d.TreeSpriteCount>0).Reference;
        var actions=new[]{"raise","lower","placeCut","removeCut","placeTerrace","removeTerrace","plantForest","placeCreek"};
        int accepted=0;
        for(int n=0;n<100;n++)
        {
            var before=host.Snapshot();var p=initial.World.Space.ToLocation(new(random.Next(32),random.Next(48),dense?2:1));
            var action=actions[n%actions.Length];
            var q=new CornerTerrainQuery(1,before.Terrain!.GeometryRevision,new(p.X,p.Y,p.Z),action,
                Rectangle:action is "plantForest" or "placeCreek"?new(new(p.X,p.Y,p.Z)):null,
                Forest:action=="plantForest"?new(forest.PackId,forest.ContributionId,forest.RecipeId):null);
            if(!host.PreviewCorner(q).Legal)continue;
            await Send(host,new EditCornerCommand(q));accepted++;
            var after=host.Snapshot();var bytes=await Save(host);var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
            Assert.Equal(bytes,SaveCodec.Write(restored));
            Assert.True(SaveCodec.TerrainSaveUpperBound(restored)>=bytes.LongLength);
            var expanded=TerrainWire.Snapshot(restored,1);
            Assert.Equal(JsonSerializer.Serialize<WireMessage>(after,WireOptions),JsonSerializer.Serialize<WireMessage>(expanded,WireOptions));
            Assert.True(TerrainWire.TryDifference(before,after,out var fast));
            // Clones have no publication certificate and exercise the general comparator.
            var ordinary=NetworkSession.Difference(before with {},after with {});
            Assert.Equal(JsonSerializer.Serialize<WireMessage>(ordinary,WireOptions),JsonSerializer.Serialize<WireMessage>(fast,WireOptions));
            if(history.Count>1)Assert.False(TerrainWire.TryDifference(history[^2],after,out _));
            history.Add(after);
        }
        Assert.True(accepted>15);
        // Scalar ticks share exact graph/validation without changing edit deltas.
        var last=host.Snapshot();await Send(host,new StepCommand());
        Assert.True(TerrainWire.TryDifference(last,host.Snapshot(),out var scalar));
        Assert.Empty(scalar.Entities!);Assert.Empty(scalar.Terrain!.Cells);Assert.Empty(scalar.Terrain.Occupants);
    }

    [Fact]
    public void P4_queries_distinguish_ground_water_elevation_terrace_support_and_outside_without_build_legality()
    {
        var w=TerrainTransportScenario.Create().World;var space=w.Space;
        var terrace=space.ToLocation(new(0,0,1));var ground=terrace with{Z=2};
        Assert.Equal((GroundRelation.Underground,true,VoxelStatus.Occupied),Read(terrace));
        Assert.Equal(TerrainOccupantKind.Terrace,w.OccupantAt(terrace)!.Kind);
        Assert.Equal((GroundRelation.Ground,true,VoxelStatus.Occupied),Read(ground));
        Assert.Equal(new QuarterCorners(1,2,3,4),w.OccupantAt(ground)!.Corners);
        Assert.Equal((GroundRelation.Elevated,false,VoxelStatus.Vacant),Read(ground with{Z=3}));
        var clear=space.ToLocation(new(2,2,2));Assert.Equal((GroundRelation.Ground,true,VoxelStatus.Vacant),Read(clear));
        Assert.Equal((GroundRelation.Outside,false,VoxelStatus.Outside),Read(ground with{Z=6}));
        Assert.True(w.Spatial.At(space.ToLocation(new(2,2,3))).Reusable);
        Assert.False(w.Spatial.At(terrace).Reusable);
        (GroundRelation,bool,VoxelStatus) Read(Location p){var q=w.At(p);return(q.Relation,q.Underwater,q.Occupancy.Status);}
    }

    [Fact]
    public void A_validated_draft_cannot_bypass_revision_checks_for_another_predecessor()
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Fences",new(30,42,6),1);
        var w=TerrainWorld.Empty(request,TerrainProfiles.Bundled);var p=new Location(20,20,1);
        var a=CornerTerrain.Apply(w,CornerTerrain.Preview(w,p,"raise"));
        var b=CornerTerrain.Apply(w,CornerTerrain.Preview(w,p with{X=22},"raise"));
        var state=new TerrainSessionState(a,TerrainProvenance.From(request),revision:1);
        Assert.Throws<ArgumentException>(()=>state.Replace(1,b,false));
        Assert.Same(a,state.World);
    }
    [Fact]
    public async Task Prepared_domain_creation_freezes_mutable_headers_and_requires_the_exact_registered_profile()
    {
        var r=new EmptyWorldRequest(WorldCreationProfile.Id,"Detached creation",new(30,42,6),1);
        var world=TerrainWorld.Empty(r,TerrainProfiles.Bundled);var provenance=TerrainProvenance.From(r);
        var owner=new ParticipantContext("owner");
        using var host=new SessionHost(new TerrainSessionState(world,provenance),"owner",TerrainProfiles.Resolve);
        var begun=host.BeginEmpty(r,WorldCreationCapabilities.EmptyReady(),1,0,owner);host.Pump(0);var job=await begun;
        var created=new TerrainSessionState(world,provenance);var expected=SaveCodec.Write(created);
        var prepared=host.PrepareCreation(created);
        created.SetPaused(false);created.AdvanceOneMinute();created.Random["forest"].Next();
        var completed=host.CompletePreparedCreation(job,prepared,owner);host.Pump(0);var preview=await completed;
        var committed=host.CommitEmpty(preview.Id,preview.RequestHash,preview.CandidateHash,1,0,owner);host.Pump(0);await committed;
        Assert.Equal(expected,await Save(host));
        var profile=new TerrainProfile(world.Profile.Packs,world.Profile.GroundRecipes,world.Profile.Definitions);
        var foreign=new TerrainSessionState(TerrainWorld.Empty(r,profile),provenance);
        Assert.Throws<ArgumentException>(()=>host.PrepareCreation(foreign));
    }

}
