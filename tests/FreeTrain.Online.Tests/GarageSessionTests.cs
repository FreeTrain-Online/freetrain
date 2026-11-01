using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;
public class GarageSessionTests
{
    static SessionHost Host()
    {
        var w=GarageConstructionTests.World();var r=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0);
        return new(new TerrainSessionState(w.Terrain,TerrainProvenance.From(r)),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    }
    static RailwayQuery Q(SessionHost h,string action="lay",bool reverse=false)
    {var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,reverse?new(18,10,2):new(14,10,2),reverse?new(14,10,2):new(18,10,2),action,"source","garage");}
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,RailwayQuery q,ParticipantContext? who=null)
    {var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,new EditRailwayCommand(q)));h.Pump(0);return await t;}
    static SessionHost Open(byte[] b)=>new(SaveCodec.ReadWorld(b,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    [Fact]
    public async Task Detached_preview_roles_stale_quotes_and_exact_save_checkpoint_continuation()
    {
        using var h=Host();var original=await Save(h);var q=Q(h);var tick=h.Snapshot().Tick;
        Assert.Equal(32_500_000,h.PreviewRailway(q).Cost);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,q,observer)).Code);
        Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,q));Assert.Equal(tick,h.Snapshot().Tick);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,q)).Code);
        var bytes=await Save(h);using var reopened=Open(bytes);Assert.Equal(bytes,await Save(reopened));
        foreach(var reverse in new[]{false,true})
        {
            var a=Q(h,"remove",reverse);var b=Q(reopened,"remove",reverse);
            Assert.Equal(reverse?800_000:2_000_000,h.PreviewRailway(a).Cost);
            Assert.IsType<AcceptedMessage>(await Send(h,a));Assert.IsType<AcceptedMessage>(await Send(reopened,b));
            Assert.Equal(await Save(h),await Save(reopened));
        }
        var state=SaveCodec.ReadWorld(await Save(h),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.All(state.World.Voxels,v=>Assert.Equal(RailwayHandler.Normal,state.World.Profile.Resolve(v.Traffic!.Rail!.Definition).Handler));
        Assert.Equal(9,state.Construction.Accounts.Transactions.Length);Assert.Equal(5,state.World.Voxels.Length);
        Assert.Equal(35_300_000,state.Construction.Accounts.Transactions.Sum(t=>t.Expenditure));
    }
}
