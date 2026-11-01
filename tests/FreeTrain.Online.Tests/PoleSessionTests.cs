using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;
public class PoleSessionTests
{
    static SessionHost Host()
    {
        var source=GarageConstructionTests.World();var w=NormalRailwayConstruction.Apply(new(source),NormalRailwayConstruction.PreviewBuild(source,new(14,10,2),new(16,10,2)),new(0)).World;var r=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0);
        return new(new MixedSessionState(new(w),TerrainProvenance.From(r),revision:w.Revision),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    }
    static RailwayQuery Q(SessionHost h,string action="lay")
    {var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,new(14,10,2),new(14,10,2),action,"source","pole");}
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,RailwayQuery q,ParticipantContext? who=null)
    {var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,new EditRailwayCommand(q)));h.Pump(0);return await t;}
    static SessionHost Open(byte[] b)=>new(SaveCodec.ReadWorld(b,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    [Fact]
    public async Task Detached_preview_roles_stale_queries_and_save_continuation()
    {
        using var h=Host();var original=await Save(h);var q=Q(h);var tick=h.Snapshot().Tick;
        var preview=h.PreviewRailway(q);Assert.True(preview.Legal,preview.Reason);Assert.Equal(0,preview.Cost);Assert.Single(preview.Cells);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,q,observer)).Code);Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,q));Assert.Equal(tick,h.Snapshot().Tick);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,q)).Code);
        var bytes=await Save(h);using var reopened=Open(bytes);Assert.Equal(bytes,await Save(reopened));
        var saved=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Empty(saved.Construction.Accounts.Transactions);var owner=saved.World.At(new(14,10,2))!.OwnerId;
        Assert.Equal(PoleRailwayConstruction.Double1,saved.World.At(new(14,10,2))!.Traffic!.Accessory!.Definition.ContributionId);
        Assert.True(h.PreviewRailway(Q(h)).Legal);
        Assert.IsType<AcceptedMessage>(await Send(h,Q(h,"remove")));Assert.IsType<AcceptedMessage>(await Send(reopened,Q(reopened,"remove")));
        Assert.Equal(await Save(h),await Save(reopened));
        var next=SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal(owner,next.World.At(new(14,10,2))!.OwnerId);Assert.Null(next.World.At(new(14,10,2))!.Traffic!.Accessory);
        Assert.Empty(next.Construction.Accounts.Transactions);
        Assert.Throws<ArgumentException>(()=>h.PreviewRailway(Q(h) with{PoleType="bad"}));
        Assert.Throws<ArgumentException>(()=>h.PreviewRailway(Q(h) with{PoleType=null!}));
        Assert.Throws<ArgumentException>(()=>h.PreviewRailway(Q(h) with{To=new(15,10,2)}));
    }
}
