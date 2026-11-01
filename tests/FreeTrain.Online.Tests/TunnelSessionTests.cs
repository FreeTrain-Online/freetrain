using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class TunnelSessionTests
{
    static SessionHost Host(bool natural=false)
    {
        var w=TunnelConstructionTests.World([TunnelConstructionTests.Start,new(18,10,2)],natural?new(1,1,1,1):null,natural:natural);
        var r=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0);
        return new(new TerrainSessionState(w.Terrain,TerrainProvenance.From(r)),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    }
    static RailwayQuery Q(SessionHost h,PositionRead? from=null,PositionRead? to=null,string action="lay")
    {
        var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,
            from??new(14,10,2),to??new(18,10,2),action,"source","tunnel");
    }
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,RailwayQuery q,ParticipantContext? who=null,string? id=null)
    {var t=h.Enqueue(who??new("owner"),new(id??Guid.NewGuid().ToString(),h.WorldEpoch,new EditRailwayCommand(q)));h.Pump(0);return await t;}
    static SessionHost Reopen(byte[] bytes) => new(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Preview_cancel_reopen_checkpoint_and_both_removal_orders_preserve_exact_terrain_and_booking(bool natural)
    {
        using var h=Host(natural);var original=await Save(h);var before=h.Snapshot();var q=Q(h);
        var preview=h.PreviewRailway(q);Assert.True(preview.Legal);Assert.Equal(60_000_000,preview.Cost);Assert.Empty(preview.AddedPiers);
        Assert.Equal(natural?5:2,preview.Losses.Length);Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,q));var built=h.Snapshot();
        Assert.Equal(before.WorldEpoch,built.WorldEpoch);Assert.Equal(before.Tick,built.Tick);
        Assert.Equal(before.Terrain!.Cells,built.Terrain!.Cells);
        var bytes=await Save(h);using var reopened=Reopen(bytes);Assert.Equal(bytes,await Save(reopened));
        Assert.Equal(0,SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!.World.Terrain.Occupants.Count(o=>o.Forest!=null));
        var removal=Q(h,action:"remove");var removalRestart=Q(reopened,action:"remove");
        Assert.Equal(natural?16_000_000:4_000_000,h.PreviewRailway(removal).Cost);
        Assert.IsType<AcceptedMessage>(await Send(h,removal));Assert.IsType<AcceptedMessage>(await Send(reopened,removalRestart));
        Assert.Equal(await Save(h),await Save(reopened));
        var remaining=await Save(h);using var restarted=Reopen(remaining);Assert.Equal(remaining,await Save(restarted));
        var reverse=Q(h,new(18,10,2),new(14,10,2),"remove");var reverseRestart=Q(restarted,new(18,10,2),new(14,10,2),"remove");
        Assert.Equal(4_000_000,h.PreviewRailway(reverse).Cost);
        Assert.IsType<AcceptedMessage>(await Send(h,reverse));Assert.IsType<AcceptedMessage>(await Send(restarted,reverseRestart));
        Assert.Equal(await Save(h),await Save(restarted));
        var after=SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Empty(after.World.Terrain.TunnelBackups);Assert.Equal(before.Terrain.Cells,h.Snapshot().Terrain!.Cells);
        Assert.Equal(natural?0:3,after.World.Voxels.Length);
        Assert.Equal(3,after.Construction.Accounts.Transactions.Length);
        foreach(var p in new[]{new Location(14,10,2),new(18,10,2)})
        {Assert.Equal(natural?new QuarterCorners(1,1,1,1):new(0,1,2,3),after.World.Terrain.OccupantAt(p)!.Corners);Assert.False(after.World.Terrain.Spatial.At(p).Entity!.Owned);}
    }
    [Fact]
    public async Task Fifo_roles_disconnect_epoch_content_and_replay_reject_without_losing_terrain_or_booking()
    {
        using var h=Host();var q=Q(h);var original=await Save(h);
        var viewer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(viewer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,q,viewer)).Code);
        foreach(var bad in new[]{q with{Infrastructure="unknown"},q with{ExpectedWorldEpoch=9},q with{ExpectedGeometryRevision=9},q with{ExpectedContentVersion="unknown"},q with{From=new(14,10,0)}})
            Assert.IsType<RejectedMessage>(await Send(h,bad));
        Assert.Equal(original,await Save(h));
        var a=h.Enqueue(new("owner"),new("one",h.WorldEpoch,new EditRailwayCommand(q)));
        var b=h.Enqueue(new("owner"),new("two",h.WorldEpoch,new EditRailwayCommand(q)));h.Pump(0);
        Assert.IsType<AcceptedMessage>(await a);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await b).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,q,id:"one")).Code);Assert.Single(h.Snapshot().Mixed!.Accounts.Transactions);
        var built=await Save(h);var op=new ParticipantContext("op",ParticipantRole.Operator);h.RegisterParticipant(op);
        var disconnected=h.Enqueue(op,new("gone",h.WorldEpoch,new EditRailwayCommand(Q(h,action:"remove"))));h.DisconnectParticipant(op);h.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await disconnected).Code);Assert.Equal(built,await Save(h));
        // Backup tampering cannot excuse a nonmatching natural terrain boundary.
        using var natural=Host(true);Assert.IsType<AcceptedMessage>(await Send(natural,Q(natural)));
        var save=System.Text.Json.Nodes.JsonNode.Parse(await Save(natural))!;
        save["mixed"]!["voxels"]![0]!["traffic"]!["rail"]!["terrainBackup"]!["ne"]=2;
        Assert.ThrowsAny<Exception>(()=>SaveCodec.ReadWorld(System.Text.Encoding.UTF8.GetBytes(save.ToJsonString()),TerrainProfiles.Resolve,RailwayProfiles.Resolve));
    }
    [Fact]
    public async Task Tolerant_query_echoes_requested_points_but_commits_only_resolved_portals_with_stale_fences()
    {
        var w=TunnelPortalSelectionTests.Ridge();var request=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,40,24),4);
        using var h=new SessionHost(new TerrainSessionState(w.Terrain,TerrainProvenance.From(request)),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
        var original=await Save(h);var q=Q(h,new(15,14,7),new(26,14,7)) with{TunnelRouting="portals-auto"};
        var preview=h.PreviewRailway(q);Assert.True(preview.Legal);Assert.Equal(q,preview.Query);
        Assert.Equal(new PositionRead(14,14,6),preview.Cells[0].Position);Assert.Equal(new PositionRead(27,14,6),preview.Cells[^1].Position);
        Assert.Equal(168_000_000,preview.Cost);Assert.Equal(original,await Save(h));
        foreach(var bad in new[]{q with{TunnelRouting="unknown"},q with{Infrastructure="normal"},q with{Action="remove"}})
            Assert.Throws<ArgumentException>(()=>h.PreviewRailway(bad));
        Assert.IsType<AcceptedMessage>(await Send(h,q));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,q)).Code);
        var captured=await Save(h);using var reopened=Reopen(captured);Assert.Equal(captured,await Save(reopened));
        Assert.IsType<AcceptedMessage>(await Send(reopened,Q(reopened,new(14,14,6),new(27,14,6),"remove")));
        Assert.IsType<AcceptedMessage>(await Send(reopened,Q(reopened,new(27,14,6),new(14,14,6),"remove")));
        var restored=SaveCodec.ReadWorld(await Save(reopened),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!.World;
        Assert.True(w.Terrain.Cells.SequenceEqual(restored.Terrain.Cells));Assert.Empty(restored.Terrain.TunnelBackups);
    }

    [Fact]
    public void Prepared_tunnel_plan_cannot_be_substituted_with_another_tools_quote()
    {
        var w=TunnelConstructionTests.World([]);var plan=NormalRailwayConstruction.PreviewBuild(w,new(14,10,2),new(16,10,2));
        Assert.True(plan.Legal);var state=new MixedConstructionState(w);
        Assert.Equal("invalidTunnelPlan",Assert.Throws<InvalidOperationException>(()=>TunnelRailwayConstruction.Apply(state,plan,new(0))).Message);
        Assert.Empty(state.World.Voxels);Assert.Empty(state.Accounts.Transactions);
    }
    [Fact]
    public void Validated_terrain_successor_publishes_only_real_changes_and_retains_unrelated_wire_records()
    {
        var w=TunnelPortalSelectionTests.Ridge();var request=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,40,24),4);
        var before=new MixedSessionState(new(w),TerrainProvenance.From(request));var snapshot=MixedWire.Snapshot(before,1);
        var plan=TunnelRailwayConstruction.Preview(w,new(14,14,6),new(27,14,6));
        var after=before.Successor(before.Revision,TunnelRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time),before.ConstructionMode);
        var next=MixedWire.Snapshot(after,1);var delta=StateDifference.Between(snapshot,next);
        Assert.Same(snapshot.Terrain!.Cells,next.Terrain!.Cells);Assert.Empty(delta.Terrain!.Cells);
        Assert.Equal(2,delta.Removed.EntityIds!.Length);Assert.Empty(delta.Terrain.Occupants);
        var survivor=snapshot.Terrain.Occupants.First(o=>!delta.Removed.EntityIds.Contains(o.EntityId));
        Assert.Same(survivor,next.Terrain.Occupants.Single(o=>o.EntityId==survivor.EntityId));
        Assert.Same(snapshot.Entities!.Single(e=>e.Id==survivor.EntityId),next.Entities!.Single(e=>e.Id==survivor.EntityId));
    }
    [Fact]
    public void Uncertified_imported_terrain_cannot_skip_predecessor_revision_validation()
    {
        var w=TunnelPortalSelectionTests.Ridge();var t=w.Terrain;
        var bad=t.Spatial.Entities.SetItem(0,t.Spatial.Entities[0] with{Revision=2});
        var imported=new TerrainWorld(t.Profile,t.Name,new(40,40,24),t.WaterLevel,t.Cells,new(t.Spatial.NextEntityId,bad),t.Occupants,t.Revision+1);
        Assert.Throws<ArgumentException>(()=>w.Replace(w.Revision,imported,imported.Spatial.Capture(),[],[]));
        Assert.Equal(0,w.Revision);Assert.Equal(0,w.Terrain.Revision);
    }

}
