using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;
public class StationarySessionTests
{
    static string Type => RailwayProfiles.Bundled.Bindings.First(b=>b.Stationary?.Height==2).Reference.ContributionId;
    static SessionHost Host()=>new(StationaryConstructionTests.Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    static SessionHost Open(byte[] b)=>new(SaveCodec.ReadWorld(b,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
    static RailwayQuery Q(SessionHost h,string action="lay")
    {var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.ContentVersion,new(14,10,2),new(14,10,2),action,"source","stationary",StationaryType:Type);}
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,CommandPayload payload,ParticipantContext? who=null)
    {var t=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await t;}
    [Fact]
    public async Task Preview_role_stale_epoch_and_point_fences_protect_the_entire_construction_lifecycle()
    {
        using var h=Host();var original=await Save(h);var q=Q(h);
        var preview=h.PreviewRailway(q);Assert.True(preview.Legal,preview.Reason);Assert.Equal(2,preview.Cells.Length);Assert.Equal(0,preview.Cost);Assert.Equal(original,await Save(h));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,new EditRailwayCommand(q),observer)).Code);Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(q)));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditRailwayCommand(q))).Code);
        var bytes=await Save(h);using var reopened=Open(bytes);Assert.Equal(bytes,await Save(reopened));
        Assert.IsType<AcceptedMessage>(await Send(h,new RunCommand()));Assert.IsType<AcceptedMessage>(await Send(reopened,new RunCommand()));
        for(var i=0;i<6;i++){h.Pump(256);reopened.Pump(256);Assert.Equal(await Save(h),await Save(reopened));}
        Assert.All(h.Snapshot().Mixed!.Voxels,v=>Assert.Null(v.Construction));Assert.Single(h.Snapshot().Mixed!.Accounts.Transactions);
        Assert.IsType<AcceptedMessage>(await Send(h,new PauseCommand()));Assert.IsType<AcceptedMessage>(await Send(reopened,new PauseCommand()));
        Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(Q(h,"remove"))));
        Assert.IsType<AcceptedMessage>(await Send(reopened,new EditRailwayCommand(Q(reopened,"remove"))));
        Assert.Equal(await Save(h),await Save(reopened));Assert.Empty(h.Snapshot().Mixed!.Voxels);
        Assert.Throws<ArgumentException>(()=>h.PreviewRailway(Q(h) with{To=new(15,10,2)}));
        Assert.Throws<ArgumentException>(()=>h.PreviewRailway(Q(h) with{StationaryType=null!}));
    }
    [Fact]
    public async Task Work_running_in_a_different_owner_does_not_retarget_old_confirmation_and_cancel_is_durable()
    {
        using var h=Host();Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(Q(h))));
        var q=Q(h,"remove");Assert.True(h.PreviewRailway(q).Legal);
        Assert.IsType<AcceptedMessage>(await Send(h,new RunCommand()));h.Pump(256);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,new EditRailwayCommand(q))).Code);
        Assert.IsType<AcceptedMessage>(await Send(h,new PauseCommand()));
        Assert.IsType<AcceptedMessage>(await Send(h,new EditRailwayCommand(Q(h,"remove"))));
        var bytes=await Save(h);using var reopened=Open(bytes);Assert.Equal(bytes,await Save(reopened));
        Assert.Empty(h.Snapshot().Mixed!.Accounts.Transactions);Assert.Empty(h.Snapshot().Mixed!.Voxels);
    }
}
