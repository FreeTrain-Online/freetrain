using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class SlopeSessionTests
{
    static SessionHost Host(int ground=1)
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Slope",new(32,24,8),0);
        var w=new TerrainWorld(TerrainProfiles.Bundled,"Slope",new(32,24,8),0,
            Enumerable.Repeat(new TerrainCell(ground,TerrainSurface.Cut),768),new(1,[]),[]);
        return new(new TerrainSessionState(w,TerrainProvenance.From(request)),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    }
    static SlopeQuery Q(SessionHost h,int d=2,int z=1,string action="lay",PositionRead? p=null)
    {
        var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,p??new(14,10,z),d,action);
    }
    static async Task<WireMessage> Send(SessionHost h,SlopeQuery q,ParticipantContext? who=null)
    {
        var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,new EditSlopeCommand(q)));h.Pump(0);return await t;
    }
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}

    [Theory]
    [InlineData(0,1,1)][InlineData(2,1,1)][InlineData(4,0,2)][InlineData(6,3,1)]
    public async Task Preview_promote_reopen_and_remove_every_member_preserve_exact_state(int d,int ground,int z)
    {
        using var h=Host(ground);var before=await Save(h);var snapshot=h.Snapshot();var q=Q(h,d,z);
        var preview=h.PreviewSlope(q);Assert.True(preview.Legal);Assert.Equal(40_000_000L*(z+1),preview.Cost);
        Assert.Equal(8,preview.Members.Length);Assert.Equal(4,preview.Rails.Length);
        Assert.Equal(2,preview.Members.Count(m=>m.Kind=="slopefiller"));
        Assert.Equal(z<ground?44+d*2:28+d*2,preview.Rails[0].Pattern);
        Assert.Equal(before,await Save(h));Assert.IsType<AcceptedMessage>(await Send(h,q));
        var built=h.Snapshot();Assert.Equal(snapshot.Terrain,built.Terrain);Assert.Equal(snapshot.Tick,built.Tick);
        Assert.Equal(snapshot.WorldEpoch,built.WorldEpoch);Assert.Single(built.Mixed!.Accounts.Transactions);
        Assert.Equal(8,built.Mixed.Voxels.Count(v=>v.OwnerId==built.Mixed.Slopes[0].EntityId));
        var bytes=await Save(h);var loaded=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        foreach(var member in preview.Members)
        {
            using var reopened=new SessionHost(loaded,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
            Assert.Equal(bytes,await Save(reopened));var remove=Q(reopened,d,z,"remove",member.Position);
            var p=reopened.PreviewSlope(remove);Assert.True(p.Legal);Assert.Equal(preview.Base,p.Base);
            Assert.Equal(8_000_000L*(z+2),p.Cost);Assert.Equal(preview.AddedPiers.Length,p.Losses.Length);
            Assert.All(p.Losses,l=>Assert.NotNull(l.SupportedOwnerId));
            Assert.IsType<AcceptedMessage>(await Send(reopened,remove));Assert.Empty(reopened.Snapshot().Mixed!.Voxels);
            Assert.Equal(2,reopened.Snapshot().Mixed!.Accounts.Transactions.Length);
        }
    }

    [Fact]
    public async Task Fifo_roles_disconnect_epoch_geometry_content_and_invalid_requests_preserve_predecessor()
    {
        using var h=Host();var q=Q(h);var before=await Save(h);
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.True(h.PreviewSlope(q).Legal);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,q,observer)).Code);
        foreach(var bad in new[]{q with{Climb=1},q with{Action="removeEntity"},q with{ExpectedWorldEpoch=2},q with{ExpectedGeometryRevision=3},q with{ExpectedContentVersion="wrong"}})
            Assert.IsType<RejectedMessage>(await Send(h,bad));
        Assert.Throws<ArgumentException>(()=>h.PreviewSlope(q with{Climb=1}));
        var outside=h.PreviewSlope(q with{Position=new(100,100,1)});Assert.False(outside.Legal);Assert.Equal("outside",outside.Reason);
        Assert.Equal(8,outside.Members.Length);Assert.Equal(0,outside.Cost);Assert.Equal(before,await Save(h));
        var op=new ParticipantContext("builder",ParticipantRole.Operator);h.RegisterParticipant(op);
        var a=h.Enqueue(op,new("first",h.WorldEpoch,new EditSlopeCommand(q)));
        var b=h.Enqueue(new("owner"),new("stale",h.WorldEpoch,new EditSlopeCommand(q)));h.Pump(0);
        Assert.IsType<AcceptedMessage>(await a);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await b).Code);
        var built=await Save(h);var remove=Q(h,action:"remove");
        var gone=h.Enqueue(op,new("gone",h.WorldEpoch,new EditSlopeCommand(remove)));h.DisconnectParticipant(op);h.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await gone).Code);
        Assert.Equal("slopeDirectionMismatch",h.PreviewSlope(remove with{Climb=0}).Reason);
        Assert.Equal("notSlope",h.PreviewSlope(remove with{Position=new(2,2,1)}).Reason);
        Assert.Equal("obstacle",h.PreviewSlope(Q(h)).Reason);Assert.IsType<RejectedMessage>(await Send(h,Q(h)));
        Assert.Equal(built,await Save(h));
    }

    [Fact]
    public async Task Flat_slope_elevated_network_has_exact_transition_handoff_and_continuation()
    {
        using var h=Host();Assert.IsType<AcceptedMessage>(await Send(h,Q(h)));
        async Task Track(PositionRead from,PositionRead to)
        {
            var s=h.Snapshot();var q=new RailwayQuery(s.WorldEpoch,s.Mixed!.GeometryRevision,s.ContentVersion,from,to,"lay","source");
            Assert.True(h.PreviewRailway(q).Legal);var t=h.Enqueue(new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,new EditRailwayCommand(q)));h.Pump(0);Assert.IsType<AcceptedMessage>(await t);
        }
        await Track(new(11,10,1),new(14,10,1));await Track(new(17,10,2),new(20,10,2));
        var s=h.Snapshot();Assert.Single(s.Mixed!.Slopes);Assert.Equal(4,s.Mixed.Voxels.Count(v=>v.Traffic?.Rail?.Pattern>=28));
        var bytes=await Save(h);using var reopened=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(bytes,await Save(reopened));
        var remove=h.PreviewSlope(Q(h,action:"remove"));
        Assert.Equal(new[]{0,1,-1,0},remove.Rails.Select(r=>r.ZDiff));
        Assert.Equal(new[]{2,2,6,2},remove.Rails.Select(r=>r.ZAngle));
        Assert.Equal(8,remove.Members.Length);
        Assert.Equal(JsonSerializer.Serialize(remove),JsonSerializer.Serialize(reopened.PreviewSlope(Q(reopened,action:"remove"))));
    }
}
