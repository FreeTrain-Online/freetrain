using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class RailwaySessionTests
{
    static TerrainSessionState State(int ground=1)
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Track",new(32,24,8),0);
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Track",new(32,24,8),0,
            Enumerable.Repeat(new TerrainCell(ground,TerrainSurface.Cut),768),new(1,[]),[]);
        return new(terrain,TerrainProvenance.From(request));
    }
    static SessionHost Host(TerrainSessionState? state=null) => new(state??State(),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    static RailwayQuery Q(SessionHost host,PositionRead? from=null,PositionRead? to=null,string action="lay",string routing="closest")
    {
        var s=host.Snapshot();return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,
            from??new(10,10,1),to??new(14,10,1),action,routing);
    }
    static async Task<WireMessage> Send(SessionHost host,RailwayQuery q,ParticipantContext? who=null)
    {
        var task=host.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),host.WorldEpoch,new EditRailwayCommand(q)));
        host.Pump(0);return await task;
    }
    static async Task<byte[]> Save(SessionHost host){var save=host.CaptureSave();host.Pump(0);return await save;}

    [Fact]
    public async Task Preview_cancel_and_promotion_preserve_exact_terrain_clock_random_mode_and_ids()
    {
        var state=State();using var host=Host(state);var before=await Save(host);var read=host.Snapshot();var q=Q(host);
        var preview=host.PreviewRailway(q);Assert.True(preview.Legal);Assert.Equal(30_000_000,preview.Cost);
        Assert.Equal(5,preview.Cells.Length);Assert.Empty(preview.Losses);Assert.Equal(before,await Save(host));
        Assert.IsType<AcceptedMessage>(await Send(host,q));var after=host.Snapshot();
        Assert.Equal(read.WorldEpoch,after.WorldEpoch);Assert.Equal(read.Tick,after.Tick);Assert.Equal(read.Paused,after.Paused);
        Assert.Equal(read.Terrain,after.Terrain);Assert.Equal(1,after.Mixed!.GeometryRevision);Assert.Equal(6,after.Mixed.NextEntityId);
        Assert.Equal(ConstructionAccounts.DefaultInitialCash-30_000_000,after.Cash);Assert.Single(after.Mixed.Accounts.Transactions);
        var restored=SaveCodec.ReadWorld(await Save(host),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal(state.Clock.Capture(),restored.Clock.Capture());Assert.Equal(JsonSerializer.Serialize(state.Random.Capture()),JsonSerializer.Serialize(restored.Random.Capture()));
        using var reopened=new SessionHost(restored,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(await Save(host),await Save(reopened));
        Assert.IsType<AcceptedMessage>(await Send(reopened,Q(reopened,action:"remove")));
        Assert.Empty(reopened.Snapshot().Mixed!.Voxels);Assert.Equal(20_000_000,reopened.Snapshot().Mixed!.Accounts.Transactions[^1].Expenditure);
    }

    [Fact]
    public async Task Shortest_legal_heuristic_avoids_literal_obstacle_without_moving_either_endpoint()
    {
        var state=State();var w=state.World;var p=new Location(11,11,1);
        var definition=w.Profile.Definitions.First(d=>d.Kind==TerrainOccupantKind.Mountain&&d.TreeSpriteCount==0).Reference;
        var entity=new SpatialEntity(1,0,definition,p,new([new(new(0,0,0),true)]),false,false,0);
        var terrain=new TerrainWorld(w.Profile,w.Name,new(32,24,8),0,w.Cells,new(2,[entity]),[new(1,TerrainOccupantKind.Mountain,default)]);
        using var host=Host(new(terrain,state.Provenance));var q=Q(host,to:new(15,12,1));
        Assert.False(host.PreviewRailway(q with{Routing="source"}).Legal);
        var preview=host.PreviewRailway(q);Assert.True(preview.Legal);Assert.True(preview.ReverseBend);
        Assert.Equal(new[]{new PositionRead(10,10,1),new(11,10,1),new(12,10,1),new(13,10,1),new(14,11,1),new(15,12,1)},preview.Cells.Select(c=>c.Position));
        Assert.Equal(36_000_000,preview.Cost);Assert.IsType<AcceptedMessage>(await Send(host,q));
        Assert.DoesNotContain(host.Snapshot().Mixed!.Voxels,v=>v.Position==new PositionRead(11,11,1));
        Assert.Equal(entity.Id,Assert.Single(host.Snapshot().Terrain!.Occupants).EntityId);
    }

    [Fact]
    public async Task Endpoint_attachment_and_well_connected_branch_use_real_source_patterns()
    {
        using var host=Host();Assert.IsType<AcceptedMessage>(await Send(host,Q(host)));
        var first=host.Snapshot().Mixed!.Voxels.Single(v=>v.Position==new PositionRead(14,10,1));
        Assert.IsType<AcceptedMessage>(await Send(host,Q(host,new(14,10,1),new(16,10,1))));
        Assert.Equal(first,host.Snapshot().Mixed!.Voxels.Single(v=>v.Position==first.Position));
        var q=Q(host,new(12,10,1),new(14,12,1));var preview=host.PreviewRailway(q);
        Assert.True(preview.Legal);Assert.Equal(23,preview.Cells[0].AfterPattern);
        Assert.IsType<AcceptedMessage>(await Send(host,q));
        Assert.Equal(23,host.Snapshot().Mixed!.Voxels.Single(v=>v.Position==new PositionRead(12,10,1)).Traffic!.Rail!.Pattern);
    }

    [Fact]
    public async Task Two_legal_shortest_routes_prefer_lower_actual_booking_and_ties_keep_source_order()
    {
        var state=State();var w=state.World;var cells=w.Cells.ToBuilder();var p=w.Space.ToStorage(new(11,11,1));
        cells[p.V*32+p.H]=new(3,TerrainSurface.Cut);
        using var host=Host(new(new(w.Profile,w.Name,new(32,24,8),0,cells,new(1,[]),[]),state.Provenance));
        var q=Q(host,to:new(15,12,1));var source=host.PreviewRailway(q with{Routing="source"});
        Assert.True(source.Legal);Assert.Equal(54_000_000,source.Cost);
        var chosen=host.PreviewRailway(q);Assert.True(chosen.Legal);Assert.True(chosen.ReverseBend);Assert.Equal(36_000_000,chosen.Cost);
        Assert.IsType<AcceptedMessage>(await Send(host,q));Assert.Single(host.Snapshot().Mixed!.Accounts.Transactions);
        Assert.Equal(36_000_000,host.Snapshot().Mixed!.Accounts.Transactions[0].Expenditure);
        using var tied=Host();Assert.False(tied.PreviewRailway(Q(tied,to:new(15,12,1))).ReverseBend);
    }

    [Fact]
    public async Task Fifo_rejects_observer_stale_content_geometry_epoch_and_disconnected_generations_atomically()
    {
        using var host=Host();var q=Q(host);var viewer=new ParticipantContext("viewer",ParticipantRole.Observer);
        var op=new ParticipantContext("builder",ParticipantRole.Operator);host.RegisterParticipant(viewer);host.RegisterParticipant(op);
        var before=await Save(host);Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(host,q,viewer)).Code);
        Assert.Equal(before,await Save(host));
        var first=host.Enqueue(op,new("first",1,new EditRailwayCommand(q)));
        var stale=host.Enqueue(new("owner"),new("stale",1,new EditRailwayCommand(q)));host.Pump(0);
        Assert.IsType<AcceptedMessage>(await first);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await stale).Code);
        var after=await Save(host);var actual=Q(host,new(14,10,1),new(16,10,1));
        foreach(var bad in new[]{actual with{ExpectedWorldEpoch=2},actual with{ExpectedGeometryRevision=0},actual with{ExpectedContentVersion=TerrainProfile.ContentVersion}})
            Assert.IsType<RejectedMessage>(await Send(host,bad));
        var gone=host.Enqueue(op,new("gone",1,new EditRailwayCommand(actual)));host.DisconnectParticipant(op);host.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await gone).Code);Assert.Equal(after,await Save(host));
    }

    [Theory]
    [InlineData(1,2,"unusableStart")][InlineData(1,8,"outside")]
    public async Task Invalid_preview_and_commit_never_promote_or_book(int ground,int z,string reason)
    {
        using var host=Host(State(ground));var before=await Save(host);var q=Q(host,new(10,10,z),new(14,10,z));
        var preview=host.PreviewRailway(q);Assert.False(preview.Legal);Assert.Equal(reason,preview.Reason);Assert.Equal(0,preview.Cost);
        Assert.IsType<RejectedMessage>(await Send(host,q));Assert.Equal(before,await Save(host));Assert.Null(host.Snapshot().Mixed);
    }

    [Fact]
    public async Task Buried_and_elevated_routes_quote_and_reopen_with_full_pier_loss_preview()
    {
        using var buried=Host(State(3));var q=Q(buried);Assert.Equal(120_000_000,buried.PreviewRailway(q).Cost);
        Assert.IsType<AcceptedMessage>(await Send(buried,q));Assert.Equal(5,buried.Snapshot().Mixed!.Voxels.Length);
        var state=State(0);var w=state.World;var cells=w.Cells.ToBuilder();var storage=w.Space.ToStorage(new(10,10,2));
        cells[storage.V*32+storage.H]=new(2,TerrainSurface.Cut);
        var terrain=new TerrainWorld(w.Profile,w.Name,new(32,24,8),0,cells,new(1,[]),[]);
        using var elevated=Host(new(terrain,state.Provenance));q=Q(elevated,new(10,10,2),new(12,10,2));
        var quote=elevated.PreviewRailway(q);Assert.True(quote.Legal);Assert.Equal(42_000_000,quote.Cost);Assert.Equal(4,quote.AddedPiers.Length);
        Assert.IsType<AcceptedMessage>(await Send(elevated,q));
        var loaded=SaveCodec.ReadWorld(await Save(elevated),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        using var reopened=new SessionHost(loaded,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        q=Q(reopened,new(10,10,2),new(12,10,2),"remove");var removal=reopened.PreviewRailway(q);
        Assert.Equal(4,removal.Losses.Length);Assert.All(removal.Losses,l=>{Assert.Equal("pier",l.Kind);Assert.NotNull(l.SupportedOwnerId);});
        Assert.IsType<AcceptedMessage>(await Send(reopened,q));Assert.Empty(reopened.Snapshot().Mixed!.Voxels);
    }
}
