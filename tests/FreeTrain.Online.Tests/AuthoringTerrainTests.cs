using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using Xunit;

public class AuthoringTerrainTests
{
    static readonly Location P=new(20,20,1);
    static readonly ParticipantContext Owner=new("owner"),Operator=new("operator",ParticipantRole.Operator),Observer=new("observer",ParticipantRole.Observer);
    static TerrainWorld Empty(int water=1)=>TerrainWorld.Empty(new(WorldCreationProfile.Id,"Authoring",new(30,42,6),water),TerrainProfiles.Bundled);
    static SessionHost Host(TerrainWorld? world=null,bool mode=false)
    {
        var w=world??Empty();var host=new SessionHost(new TerrainSessionState(w,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Name,new(30,42,6),w.WaterLevel)),mode),"owner",TerrainProfiles.Resolve);
        host.RegisterParticipant(Operator);host.RegisterParticipant(Observer);return host;
    }
    static async Task<WireMessage> Send(SessionHost host,ParticipantContext participant,CommandPayload payload,long epoch=1)
    {var task=host.Enqueue(participant,new(Guid.NewGuid().ToString(),epoch,payload));host.Pump(0);return await task;}
    static CornerTerrainQuery Query(SessionHost host,string action="plantForest",PositionRead? end=null)
    {
        var s=host.Snapshot();var tree=TerrainProfiles.Bundled.Definitions.First(d=>d.TreeSpriteCount>0);
        return new(s.WorldEpoch,s.Terrain!.GeometryRevision,new(P.X,P.Y,P.Z),action,Rectangle:new(end??new(P.X+2,P.Y+2,P.Z)),
            Forest:action=="plantForest"?new(tree.Reference.PackId,tree.Reference.ContributionId,tree.Reference.RecipeId):null);
    }
    static async Task<byte[]> Save(SessionHost host){var t=host.CaptureSave();host.Pump(0);return await t;}
    [Fact]
    public async Task Owner_controls_mode_without_clock_random_geometry_or_role_bypass_and_mode_fences_drafts()
    {
        using var host=Host();var old=Query(host);var before=await Save(host);
        Assert.Throws<InvalidOperationException>(()=>host.PreviewCorner(old));
        foreach(var participant in new[]{Operator,Observer})Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(host,participant,new SetAuthoringModeCommand(0,true))).Code);
        Assert.Equal(before,await Save(host));
        Assert.IsType<AcceptedMessage>(await Send(host,Owner,new RunCommand()));var running=host.Snapshot();
        Assert.IsType<AcceptedMessage>(await Send(host,Owner,new SetAuthoringModeCommand(0,true)));
        var active=host.Snapshot();Assert.True(active.Terrain!.ConstructionMode);Assert.False(active.Paused);Assert.Equal(running.Tick,active.Tick);Assert.Empty(active.Entities!);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,Operator,new EditCornerCommand(old))).Code);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(host,Observer,new EditCornerCommand(Query(host)))).Code);
        var fresh=Query(host);Assert.True(host.PreviewCorner(fresh).Legal);
        Assert.IsType<AcceptedMessage>(await Send(host,Owner,new SetAuthoringModeCommand(1,false)));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,Operator,new EditCornerCommand(fresh))).Code);
        Assert.Equal("operationUnavailable",Assert.IsType<RejectedMessage>(await Send(host,Operator,new EditCornerCommand(Query(host)))).Code);
        // Ordinary tools remain available with the mode off.
        Assert.IsType<AcceptedMessage>(await Send(host,Operator,new EditCornerCommand(Query(host) with{Action="raise",Rectangle=null,Forest=null})));
        Assert.False(host.Snapshot().Paused);
    }
    [Fact]
    public async Task Forest_preview_is_exact_read_only_uses_saved_forest_stream_and_survives_reopen()
    {
        using var host=Host(mode:true);var query=Query(host);var before=await Save(host);
        var preview=host.PreviewCorner(query);Assert.True(preview.Legal);Assert.NotEmpty(preview.Decorations!);Assert.Equal(0,preview.Cost);
        Assert.Equal(before,await Save(host));Assert.Equal(preview.Decorations!.Select(d=>d.Forest!.Trees.Length),host.PreviewCorner(query).Decorations!.Select(d=>d.Forest!.Trees.Length));
        Assert.IsType<AcceptedMessage>(await Send(host,Operator,new EditCornerCommand(query)));
        var bytes=await Save(host);var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;Assert.Equal(bytes,SaveCodec.Write(restored));Assert.True(restored.ConstructionMode);
        foreach(var decoration in preview.Decorations!)
        {
            var p=decoration.Position;var actual=restored.World.OccupantAt(new(p.X,p.Y,p.Z))!;
            Assert.Equal(decoration.Forest!.Trees.Select(t=>(t.X,t.Y,t.Sprite)),actual.Forest!.Trees.Select(t=>(t.X,t.Y,t.Sprite)));
            Assert.False(restored.World.Spatial.At(new(p.X,p.Y,p.Z)).Entity!.Owned);
        }
        var initial=SaveCodec.ReadWorld(before,TerrainProfiles.Resolve).Terrain!;
        foreach(var stream in RandomStreams.Ids)
            Assert.Equal(stream!="forest",initial.Random[stream].Capture().Values.SequenceEqual(restored.Random[stream].Capture().Values));
        using var reopened=new SessionHost(restored,"owner",TerrainProfiles.Resolve);
        var next=Query(host) with{Position=new(23,20,1),Rectangle=new(new(24,21,1))};
        var a=host.PreviewCorner(next);var b=reopened.PreviewCorner(next);
        Assert.Equal(a.Decorations!.SelectMany(d=>d.Forest!.Trees),b.Decorations!.SelectMany(d=>d.Forest!.Trees));
        Assert.IsType<AcceptedMessage>(await Send(host,Owner,new EditCornerCommand(next)));
        Assert.IsType<AcceptedMessage>(await Send(reopened,Owner,new EditCornerCommand(next)));Assert.Equal(await Save(host),await Save(reopened));
    }
    [Fact]
    public async Task Mode_round_trip_keeps_draws_clock_pause_allocator_and_authored_geometry()
    {
        using var host=Host(mode:true);Assert.IsType<AcceptedMessage>(await Send(host,Operator,new EditCornerCommand(Query(host))));
        var before=SaveCodec.ReadWorld(await Save(host),TerrainProfiles.Resolve).Terrain!;
        Assert.IsType<AcceptedMessage>(await Send(host,Owner,new SetAuthoringModeCommand(before.World.Revision,false)));
        var after=SaveCodec.ReadWorld(await Save(host),TerrainProfiles.Resolve).Terrain!;Assert.False(after.ConstructionMode);
        Assert.Equal(before.Clock.Capture(),after.Clock.Capture());Assert.Equal(before.Paused,after.Paused);Assert.Equal(before.World.Spatial.NextEntityId,after.World.Spatial.NextEntityId);
        Assert.True(before.World.Cells.SequenceEqual(after.World.Cells));Assert.Equal(before.World.Spatial.Entities.Select(e=>(e.Id,e.Revision,e.Anchor,e.Definition,e.Owned,e.Value)),after.World.Spatial.Entities.Select(e=>(e.Id,e.Revision,e.Anchor,e.Definition,e.Owned,e.Value)));
        foreach(var id in RandomStreams.Ids)Assert.True(before.Random[id].Capture().Values.SequenceEqual(after.Random[id].Capture().Values));
    }
    [Fact]
    public async Task Creek_literal_straight_routes_join_bits_skip_mountains_and_never_change_water_or_rng()
    {
        Assert.Equal(new[]{(20,20,2),(20,21,10),(20,22,8)},AuthoringTerrain.CreekRoute(P,new(20,22,1)).Select(s=>(s.Position.X,s.Position.Y,s.Connections)));
        Assert.Equal(new[]{(20,20,4),(21,20,5),(22,20,1)},AuthoringTerrain.CreekRoute(P,new(22,20,1)).Select(s=>(s.Position.X,s.Position.Y,s.Connections)));
        Assert.Equal(0,Assert.Single(AuthoringTerrain.CreekRoute(P,P)).Connections);
        using var host=Host(mode:true);var before=SaveCodec.ReadWorld(await Save(host),TerrainProfiles.Resolve).Terrain!;
        var q=Query(host,"placeCreek",new(20,22,1));Assert.True(host.PreviewCorner(q).Legal);
        Assert.IsType<AcceptedMessage>(await Send(host,Operator,new EditCornerCommand(q)));
        q=Query(host,"placeCreek",new(22,20,1));Assert.Equal(6,host.PreviewCorner(q).Decorations![0].CreekConnections);
        Assert.IsType<AcceptedMessage>(await Send(host,Operator,new EditCornerCommand(q)));
        var after=SaveCodec.ReadWorld(await Save(host),TerrainProfiles.Resolve).Terrain!;Assert.Equal(6,after.World.OccupantAt(P)!.CreekConnections);
        Assert.True(before.World.Cells.SequenceEqual(after.World.Cells));Assert.Equal(before.World.WaterLevel,after.World.WaterLevel);
        foreach(var id in RandomStreams.Ids)Assert.True(before.Random[id].Capture().Values.SequenceEqual(after.Random[id].Capture().Values));
        // Forest can reclaim a creek; a creek cannot reclaim a Mountain.
        Assert.IsType<AcceptedMessage>(await Send(host,Owner,new EditCornerCommand(Query(host))));
        var skipped=host.PreviewCorner(Query(host,"placeCreek",new(20,22,1)));Assert.Contains(skipped.Skipped,s=>s.Reason=="occupied");
    }
    [Fact]
    public void Bounds_mixed_inputs_unavailable_definitions_and_extents_are_rejected_before_mutation()
    {
        using var host=Host(mode:true);var q=Query(host);
        foreach(var bad in new[]{q with{Rectangle=new(new(28,28,1))},q with{OffsetX=1,OffsetY=0},q with{Rectangle=null},q with{Rectangle=new(new(20,20,2))},q with{Forest=null},q with{Forest=new("missing","missing","missing")},q with{Stroke=[new(new(20,20,1),"raise")]},q with{Rectangle=new(new(20,20,1),true)},q with{Position=new(-999,0,1)}})
            Assert.Throws<ArgumentException>(()=>host.PreviewCorner(bad));
        Assert.Throws<ArgumentException>(()=>AuthoringTerrain.CreekRoute(P,new(84,20,1)));
        Assert.Throws<ArgumentException>(()=>host.PreviewCorner(q with{Action="placeCreek"}));
        Assert.Empty(host.Snapshot().Entities!);Assert.Equal(0,host.Snapshot().Terrain!.GeometryRevision);
    }
    [Theory][InlineData("cedar")][InlineData("pine")][InlineData("camphor")][InlineData("maple")][InlineData("ginkgo")][InlineData("beech")]
    public void All_ground_treatments_allow_underwater_manual_scatter(string species)
    {
        // Six species are in stable source manifest order; every suffix is an actual handler.
        int index=Array.IndexOf(new[]{"cedar","pine","camphor","maple","ginkgo","beech"},species);
        var w=Empty();w=new(w.Profile,w.Name,new(30,42,6),3,w.Cells,w.Spatial.Capture(),w.Occupants);
        var definitions=w.Profile.Definitions.Where(d=>d.TreeSpriteCount>0).ToArray();
        foreach(int n in new[]{index,index+6,index+12})
        {
            var random=new RandomStreams();var plan=AuthoringTerrain.Plan(w,P,new(22,22,1),"plantForest",definitions[n].Reference,random);
            Assert.All(plan.Sites,p=>Assert.True(p.Legal));Assert.NotEmpty(plan.Decorations);var result=plan.Draft.Finish();Assert.Equal(3,result.WaterLevel);
            Assert.All(result.Occupants,o=>Assert.Equal(definitions[n].Reference,o.Forest!.Definition));
        }
    }
    [Fact]
    public async Task FIFO_pending_edits_and_mode_changes_revalidate_at_boundary_and_disconnected_owners_cannot_toggle()
    {
        using var host=Host(mode:true);var query=Query(host);
        var edit=host.Enqueue(Operator,new("edit",1,new EditCornerCommand(query)));
        var exit=host.Enqueue(Owner,new("exit",1,new SetAuthoringModeCommand(0,false)));
        host.Pump(0);Assert.IsType<AcceptedMessage>(await edit);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await exit).Code);
        Assert.True(host.Snapshot().Terrain!.ConstructionMode);
        long revision=host.Snapshot().Terrain!.GeometryRevision;
        exit=host.Enqueue(Owner,new("exit2",1,new SetAuthoringModeCommand(revision,false)));
        edit=host.Enqueue(Operator,new("edit2",1,new EditCornerCommand(Query(host))));
        host.Pump(0);Assert.IsType<AcceptedMessage>(await exit);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await edit).Code);
        Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await Send(host,Owner,new SetAuthoringModeCommand(revision+1,true),2)).Code);
        var disconnected=host.Enqueue(Owner,new("gone",1,new SetAuthoringModeCommand(revision+1,true)));
        host.DisconnectParticipant(Owner);host.Pump(0);Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await disconnected).Code);
        Assert.False(host.Snapshot().Terrain!.ConstructionMode);
    }
    [Fact]
    public async Task Zero_tree_confirmation_is_a_valid_no_placement_and_consumes_only_the_saved_forest_stream()
    {
        var w=Empty();var random=new RandomStreams().Capture();
        // Controlled source draw vector: first Next(2), Next(2) are both 1.
        var values=Enumerable.Repeat(1,55).ToArray();values[0]=values[1]=int.MaxValue-1;
        random=random with{Streams=random.Streams.Select(s=>s.Id=="forest"?s with{State=new(0,values.ToImmutableArray())}:s).ToImmutableArray()};
        var state=new TerrainSessionState(w,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Name,new(30,42,6),1)),true,random:random);
        using var host=new SessionHost(state,"owner",TerrainProfiles.Resolve);
        var tree=w.Profile.Definitions.First(d=>d.TreeDensity==1);var q=Query(host) with{Rectangle=new(new(20,20,1)),Forest=new(tree.Reference.PackId,tree.Reference.ContributionId,tree.Reference.RecipeId)};
        var preview=host.PreviewCorner(q);Assert.True(preview.Legal);Assert.Empty(preview.Changes);Assert.Empty(preview.Decorations!);
        Assert.Equal(0,state.Random["forest"].Capture().Index);
        Assert.IsType<AcceptedMessage>(await Send(host,Owner,new EditCornerCommand(q)));
        Assert.Empty(state.World.Occupants);Assert.Equal(1,state.World.Spatial.NextEntityId);Assert.Equal(2,state.Random["forest"].Capture().Index);
        Assert.Equal(1,state.World.Revision);Assert.Equal(0,host.Snapshot().Cash);
    }

    [Fact]
    public async Task Two_network_peers_receive_mode_delta_and_authored_checkpoint_restarts_exactly()
    {
        using var host=Host(mode:true);Assert.IsType<AcceptedMessage>(await Send(host,Owner,new EditCornerCommand(Query(host))));
        Assert.IsType<AcceptedMessage>(await Send(host,Owner,new EditCornerCommand(Query(host,"placeCreek",new(23,20,1)) with{Position=new(23,18,1)})));
        var bytes=await Save(host);var directory=Path.Combine(Path.GetTempPath(),"freetrain-authoring-"+Guid.NewGuid());
        try
        {
            byte[] checkpointBytes;
            using(var storage=new SaveStorage(directory))
            {
                var session=new NetworkSession(new(),storage);var now=DateTimeOffset.UtcNow;
                var owner=session.Connect(now,Owner)!;var viewer=session.Connect(now,Observer)!;
                owner.Output.Reader.TryRead(out _);viewer.Output.Reader.TryRead(out _);
                var load=session.LoadSave(bytes,1);while(!load.IsCompleted){session.Tick(now);await Task.Delay(1);}await load;
                foreach(var peer in new[]{owner,viewer})
                {
                    var snapshot=Assert.IsType<SnapshotMessage>(await peer.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.True(snapshot.Terrain!.ConstructionMode);Assert.NotEmpty(snapshot.Terrain.Occupants);
                }
                long revision=session.Bootstrap().Snapshot.Terrain!.GeometryRevision;
                var mode=session.Submit(owner,new("exit",2,new SetAuthoringModeCommand(revision,false)),now);session.Tick(now);Assert.IsType<AcceptedMessage>(await mode);session.Reply(owner,await mode);
                foreach(var peer in new[]{owner,viewer})
                {
                    var delta=Assert.IsType<DeltaMessage>(await peer.Output.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));Assert.False(delta.Terrain!.ConstructionMode);
                    Assert.Empty(delta.Terrain.Cells);Assert.Empty(delta.Terrain.Occupants);Assert.Empty(delta.Removed.EntityIds!);
                }
                var capture=session.CaptureSave();session.Tick(now);checkpointBytes=await capture;
                var checkpoint=session.Checkpoint();session.Tick(now);await checkpoint;
            }
            using(var storage=new SaveStorage(directory))
            {
                var restarted=new NetworkSession(new(),storage);Assert.Null(restarted.StartupSaveError);Assert.False(restarted.Bootstrap().Snapshot.Terrain!.ConstructionMode);
                var capture=restarted.CaptureSave();restarted.Tick(DateTimeOffset.UtcNow);Assert.Equal(checkpointBytes,await capture);
            }
        }
        finally{Directory.Delete(directory,true);}
    }

    [Fact]
    public void Seeded_diagonal_routes_match_independent_source_draw_literals_and_preserve_endpoint_order()
    {
        // Independent Python source subtractive generator and N/E/S/W walk;
        // these are literals, never output generated by the production route.
        Assert.Equal(new[]{(2,2,4),(3,2,5),(4,2,3),(4,3,8)},AuthoringTerrain.CreekRoute(new(2,2,1),new(4,3,1)).Select(s=>(s.Position.X,s.Position.Y,s.Connections)));
        Assert.Equal(new[]{(4,3,1),(3,3,5),(2,3,12),(2,2,2)},AuthoringTerrain.CreekRoute(new(4,3,1),new(2,2,1)).Select(s=>(s.Position.X,s.Position.Y,s.Connections)));
        Assert.Equal(new[]{(20,20,2),(20,21,10),(20,22,9),(19,22,5),(18,22,5),(17,22,4)},AuthoringTerrain.CreekRoute(P,new(17,22,1)).Select(s=>(s.Position.X,s.Position.Y,s.Connections)));
        Assert.Equal(new[]{(20,20,8),(20,19,10),(20,18,6),(21,18,5),(22,18,5),(23,18,1)},AuthoringTerrain.CreekRoute(P,new(23,18,1)).Select(s=>(s.Position.X,s.Position.Y,s.Connections)));
    }

}
