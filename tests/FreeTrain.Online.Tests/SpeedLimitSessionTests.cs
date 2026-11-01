using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;
public class SpeedLimitSessionTests
{
    static SessionHost Host()=>new(new MixedSessionState(new(SpeedLimitTests.Track()),TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,"Tunnel",new(40,32,8),0)),revision:1),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    static RailwayQuery Q(SessionHost h,string action="lay")
    {var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.ContentVersion,new(14,10,2),new(16,10,2),action,"source","speed");}
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,RailwayQuery q,ParticipantContext? who=null)
    {var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,new EditRailwayCommand(q)));h.Pump(0);return await t;}
    [Fact]
    public async Task Preview_is_detached_roles_stale_queries_exact_reopen_and_no_shared_time_change()
    {
        using var h=Host();var original=await Save(h);var q=Q(h);var tick=h.Snapshot().Tick;
        Assert.True(h.PreviewRailway(q).Legal);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,q,observer)).Code);Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,q));Assert.Equal(tick,h.Snapshot().Tick);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,q)).Code);
        Assert.Equal("speedLimitOverlap",h.PreviewRailway(Q(h) with{Speed="fast"}).Reason);
        Assert.IsType<RejectedMessage>(await Send(h,Q(h) with{Speed="fast"}));
        var bytes=await Save(h);using var restored=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(bytes,await Save(restored));Assert.IsType<AcceptedMessage>(await Send(h,Q(h,"remove")));Assert.IsType<AcceptedMessage>(await Send(restored,Q(restored,"remove")));
        Assert.Equal(await Save(h),await Save(restored));Assert.Null(h.Snapshot().Mixed!.SpeedLimits);
        Assert.Throws<ArgumentException>(()=>h.PreviewRailway(Q(h) with{Speed="warp"}));
        Assert.Throws<ArgumentException>(()=>h.PreviewRailway(Q(h) with{SpeedClimb=1}));
        Assert.Throws<ArgumentException>(()=>h.PreviewRailway(Q(h) with{SpeedSlope=true}));
    }
    [Fact]
    public async Task Removing_an_unlisted_section_does_not_promote_or_mutate_a_terrain_world()
    {
        var w=GarageConstructionTests.World();var state=new TerrainSessionState(w.Terrain,
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0)));
        using var h=new SessionHost(state,"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
        var before=await Save(h);var snapshot=h.Snapshot();var q=new RailwayQuery(snapshot.WorldEpoch,snapshot.Terrain!.GeometryRevision,snapshot.ContentVersion,
            new(14,10,2),new(16,10,2),"remove","source","speed");
        Assert.True(h.PreviewRailway(q).Legal);Assert.IsType<AcceptedMessage>(await Send(h,q));
        Assert.Null(h.Snapshot().Mixed);Assert.Equal(TerrainProfile.Ruleset,h.Snapshot().Ruleset);Assert.Equal(before,await Save(h));
    }

}
