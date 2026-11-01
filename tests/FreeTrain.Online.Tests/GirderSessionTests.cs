using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class GirderSessionTests
{
    static SessionHost Host(int water=0)
    {
        var r=new EmptyWorldRequest(WorldCreationProfile.Id,"Girder",new(40,32,8),water);
        var w=new TerrainWorld(TerrainProfiles.Bundled,"Girder",new(40,32,8),water,
            Enumerable.Repeat(new TerrainCell(0,TerrainSurface.Cut),1280),new(1,[]),[]);
        return new(new TerrainSessionState(w,TerrainProvenance.From(r)),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    }
    static RailwayQuery Q(SessionHost h,PositionRead? from=null,PositionRead? to=null,string action="lay")
    {
        var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,
            from??new(14,10,2),to??new(18,10,2),action,"source","girder");
    }
    static async Task<byte[]> Save(SessionHost h){var t=h.CaptureSave();h.Pump(0);return await t;}
    static async Task<WireMessage> Send(SessionHost h,RailwayQuery q,ParticipantContext? who=null,string? id=null)
    {var t=h.Enqueue(who??new("owner"),new(id??Guid.NewGuid().ToString(),h.WorldEpoch,new EditRailwayCommand(q)));h.Pump(0);return await t;}

    [Theory]
    [InlineData(14,6,3)][InlineData(18,6,1)][InlineData(18,10,0)][InlineData(18,14,2)]
    [InlineData(14,14,3)][InlineData(10,14,1)][InlineData(10,10,0)][InlineData(10,6,2)]
    public async Task All_directions_promote_save_reopen_remove_with_exact_art_accounts_and_world_identity(int x,int y,int sprite)
    {
        using var h=Host();var original=await Save(h);var before=h.Snapshot();var q=Q(h,to:new(x,y,7));
        var preview=h.PreviewRailway(q);Assert.True(preview.Legal);Assert.Equal(90_000_000,preview.Cost);
        Assert.Equal(5,preview.Cells.Length);Assert.All(preview.Cells,c=>Assert.Equal(2,c.Position.Z));
        Assert.Equal(10,preview.AddedPiers.Length);Assert.Equal(original,await Save(h));
        Assert.IsType<AcceptedMessage>(await Send(h,q));var built=h.Snapshot();
        Assert.Equal(before.WorldEpoch,built.WorldEpoch);Assert.Equal(before.Tick,built.Tick);Assert.Equal(before.Terrain,built.Terrain);
        Assert.Equal(Enumerable.Repeat(sprite,5),preview.Cells.Select(c=>built.Mixed!.Voxels.Single(v=>v.Position==c.Position).Traffic!.Rail!.Variant));
        Assert.Single(built.Mixed!.Accounts.Transactions);Assert.Equal(90_000_000,built.Mixed.Accounts.Transactions[0].Expenditure);
        var bytes=await Save(h);using var reopened=new SessionHost(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(bytes,await Save(reopened));
        var remove=Q(reopened,to:new(x,y,2),action:"remove");var p=reopened.PreviewRailway(remove);
        Assert.Equal(30_000_000,p.Cost);Assert.Equal(10,p.Losses.Length);Assert.All(p.Losses,l=>Assert.NotNull(l.SupportedOwnerId));
        Assert.IsType<AcceptedMessage>(await Send(reopened,remove));Assert.Empty(reopened.Snapshot().Mixed!.Voxels);
        Assert.Equal(2,reopened.Snapshot().Mixed!.Accounts.Transactions.Length);
    }

    [Fact]
    public async Task Actual_water_zero_booking_is_legal_and_repeated_unchanged_girder_is_still_charged_on_land()
    {
        using var wet=Host(2);var q=Q(wet);Assert.Equal(0,wet.PreviewRailway(q).Cost);
        Assert.IsType<AcceptedMessage>(await Send(wet,q));Assert.Equal(0,Assert.Single(wet.Snapshot().Mixed!.Accounts.Transactions).Expenditure);
        q=Q(wet,action:"remove");Assert.Equal(0,wet.PreviewRailway(q).Cost);Assert.IsType<AcceptedMessage>(await Send(wet,q));Assert.Empty(wet.Snapshot().Mixed!.Voxels);
        using var dry=Host();Assert.IsType<AcceptedMessage>(await Send(dry,Q(dry)));var s=dry.Snapshot();
        var p=dry.PreviewRailway(Q(dry));Assert.True(p.Legal);Assert.Empty(p.AddedPiers);Assert.Equal(90_000_000,p.Cost);
        Assert.IsType<AcceptedMessage>(await Send(dry,Q(dry)));Assert.Equal(s.Mixed!.Voxels,dry.Snapshot().Mixed!.Voxels);
        Assert.Equal(2,dry.Snapshot().Mixed!.Accounts.Transactions.Length);
    }

    [Fact]
    public async Task Fifo_stale_role_disconnect_content_and_replay_fences_protect_atomic_commit()
    {
        using var h=Host();var q=Q(h);var original=await Save(h);
        var viewer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(viewer);
        Assert.True(h.PreviewRailway(q).Legal);Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(h,q,viewer)).Code);
        foreach(var bad in new[]{q with{Infrastructure="unknown"},q with{ExpectedWorldEpoch=9},q with{ExpectedGeometryRevision=9},q with{ExpectedContentVersion="unknown"}})
            Assert.IsType<RejectedMessage>(await Send(h,bad));
        Assert.Equal(original,await Save(h));
        var a=h.Enqueue(new("owner"),new("one",h.WorldEpoch,new EditRailwayCommand(q)));
        var b=h.Enqueue(new("owner"),new("two",h.WorldEpoch,new EditRailwayCommand(q)));h.Pump(0);
        Assert.IsType<AcceptedMessage>(await a);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await b).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(h,q,id:"one")).Code);Assert.Single(h.Snapshot().Mixed!.Accounts.Transactions);
        var built=await Save(h);var op=new ParticipantContext("op",ParticipantRole.Operator);h.RegisterParticipant(op);
        var disconnected=h.Enqueue(op,new("gone",h.WorldEpoch,new EditRailwayCommand(Q(h,action:"remove"))));h.DisconnectParticipant(op);h.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await disconnected).Code);Assert.Equal(built,await Save(h));
    }
}
