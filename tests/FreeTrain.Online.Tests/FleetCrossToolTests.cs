using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public partial class FleetMovementTests
{
    // Only terrain is seeded. Every rail, car and callback below comes from the
    // P4 construction and P5 purchase/deployment paths, with bundled bindings.
    static MixedSessionState CrossToolLine(string infrastructure)
    {
        var z=infrastructure is "bridge" or "girder"?3:2;
        var w=infrastructure=="tunnel"
            ?TunnelConstructionTests.World(Enumerable.Range(18,12).Select(x=>new Location(x,20,2)))
                .EnableFleet(FreeTrain.Online.Content.RollingStockCatalog.Bundled.FleetProfile)
            :Empty(width:40,height:32);
        var s=Session(w,new(100,0,[]));
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var read=h.Snapshot();
        var query=new RailwayQuery(read.WorldEpoch,read.Mixed!.GeometryRevision,read.ContentVersion,
            new(18,20,z),new(28,20,z),"lay","source",infrastructure=="signal"?"normal":infrastructure);
        Assert.True(h.PreviewRailway(query).Legal);
        var task=h.Enqueue(FleetOwner,new("build-line",h.WorldEpoch,new EditRailwayCommand(query)));
        h.Pump(0,FleetNow);Assert.IsType<AcceptedMessage>(task.GetAwaiter().GetResult());
        if(infrastructure=="signal") {
            query=CrossQuery(h,"signal",22,z:z) with {To=new(22,20,z),SignalDirection=2};
            task=h.Enqueue(FleetOwner,new("build-signal",h.WorldEpoch,new EditRailwayCommand(query)));
            h.Pump(0,FleetNow);Assert.IsType<AcceptedMessage>(task.GetAwaiter().GetResult());
        }
        var capture=h.CaptureSave();h.Pump(0,FleetNow);s=Restore(capture.GetAwaiter().GetResult());
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,3,1));
        return Place(s,0,new(20,20,z));
    }

    static RailwayQuery CrossQuery(SessionHost h,string infrastructure,int x=22,string action="lay",int z=2)
    {
        var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.ContentVersion,
            new(x,20,z),new(x+2,20,z),action,"source",infrastructure,SignalDirection:2);
    }
    static async Task CrossBoundary(SessionHost h)
    {
        var bytes=await FleetSave(h);var s=Restore(bytes);OperationsBoundary(s);
        Assert.Equal(bytes,SaveCodec.Write(s));
        Assert.Equal(3,Assert.Single(s.Fleet!.Trains).Cars.Length);
    }

    [Theory]
    [InlineData("normal",6_000_000L)]
    [InlineData("bridge",12_000_000L)]
    [InlineData("girder",12_000_000L)]
    [InlineData("garage",600_000L)]
    [InlineData("tunnel",0L)]
    [InlineData("signal",0L)]
    public async Task Cross_tool_real_moving_consist_preserves_source_blocking_skips_and_booking(string infrastructure,long cost)
    {
        using var h=new SessionHost(CrossToolLine(infrastructure),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var z=infrastructure is "bridge" or "girder"?3:2;
        await FleetStep(h,2);var s=h.Snapshot();var train=Assert.Single(s.Mixed!.Fleet!.Trains);
        Assert.Equal(1,train.MoveCount);Assert.Equal(new PositionRead(23,20,z),train.Cars[0].State.Location);
        var before=await FleetSave(h);
        // Building over any of the three real car hosts rejects the whole route.
        var build=CrossQuery(h,infrastructure,z:z);
        if(infrastructure=="signal")build=build with {To=build.From};
        Assert.False(h.PreviewRailway(build).Legal);
        ManagerRejected(await FleetSend(h,new EditRailwayCommand(build)),"invalidPlacement");
        Assert.Equal(before,await FleetSave(h));
        var corner=new CornerTerrainQuery(s.WorldEpoch,s.Mixed.GeometryRevision,new(22,20,z),"raise");
        Assert.False(h.PreviewCorner(corner).Legal);
        Assert.IsType<RejectedMessage>(await FleetSend(h,new EditCornerCommand(corner)));
        Assert.Equal(before,await FleetSave(h));

        var remove=build with {Action="remove"};var quote=h.PreviewRailway(remove);
        if(infrastructure=="signal") {
            Assert.Equal("carOccupied",quote.Reason);
            ManagerRejected(await FleetSend(h,new EditRailwayCommand(remove)),"invalidPlacement");
            Assert.Equal(before,await FleetSave(h));
        } else {
            Assert.True(quote.Legal,quote.Reason);Assert.Equal(cost,quote.Cost);
            Assert.Equal(new[]{22,23},quote.Cells.Where(c=>c.ProtectedByCar).Select(c=>c.Position.X));
            Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(remove)));
            var after=h.Snapshot();Assert.Equal(s.Cash-cost,after.Cash);
            Assert.Equal(JsonSerializer.Serialize(train),JsonSerializer.Serialize(Assert.Single(after.Mixed!.Fleet!.Trains)));
            Assert.Equal(s.Mixed.Queue!.Pending,after.Mixed.Queue!.Pending);
            foreach(var car in train.Cars)Assert.Equal(s.Mixed.Voxels.Single(v=>v.Position==car.State.Location),after.Mixed.Voxels.Single(v=>v.Position==car.State.Location));
        }
        await CrossBoundary(h);
    }

    [Theory]
    [InlineData("normal")][InlineData("bridge")][InlineData("girder")]
    [InlineData("garage")][InlineData("tunnel")][InlineData("signal")]
    [InlineData("pole")][InlineData("speed")]
    public async Task Cross_tool_commit_fences_train_entering_and_leaving_a_quote(string infrastructure)
    {
        using var h=new SessionHost(CrossToolLine(infrastructure is "bridge" or "girder" or "tunnel"?infrastructure:"normal"),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        h.RegisterParticipant(FleetOperator);
        var z=infrastructure is "bridge" or "girder"?3:2;
        var query=CrossQuery(h,infrastructure,23,z:z);
        if(infrastructure=="normal")query=query with {To=new(20,23,2)};
        if(infrastructure=="tunnel")query=query with {To=new(29,20,2)};
        if(infrastructure is "signal" or "pole")query=query with {To=query.From};
        var quote=h.PreviewRailway(query);var before=await FleetSave(h);
        Assert.True(quote.Legal,quote.Reason);
        Assert.Equal(before,await FleetSave(h));
        await FleetStep(h,2);var moved=await FleetSave(h);
        ManagerRejected(await FleetSend(h,new EditRailwayCommand(query)),"staleRevision");
        Assert.Equal(moved,await FleetSave(h));
        query=query with {ExpectedGeometryRevision=h.Snapshot().Mixed!.GeometryRevision};
        var occupied=h.PreviewRailway(query);
        if(infrastructure is "pole" or "speed") {
            Assert.True(occupied.Legal,occupied.Reason);Assert.Equal(0,occupied.Cost);
            Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(query),FleetOperator));
        } else Assert.False(occupied.Legal);
        await CrossBoundary(h);
        // All three cars clear x20 after a movement. Fresh signal/accessory work
        // at the vacated host must preserve the complete consist.
        var clearTool=infrastructure is "bridge" or "girder" or "tunnel"?"pole":"signal";
        query=CrossQuery(h,clearTool,20,z:z) with {To=new(20,20,z)};
        var snapshot=h.Snapshot();Assert.True(h.PreviewRailway(query).Legal);
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(query),FleetOperator));
        Assert.Equal(JsonSerializer.Serialize(snapshot.Mixed!.Fleet),JsonSerializer.Serialize(h.Snapshot().Mixed!.Fleet));
        await CrossBoundary(h);
    }

    [Fact]
    public async Task Cross_tool_blocked_signal_quote_becomes_legal_only_after_all_cars_clear_and_a_fresh_quote()
    {
        using var h=new SessionHost(CrossToolLine("normal"),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var q=CrossQuery(h,"signal",22) with {To=new(22,20,2)};
        Assert.Equal("carOccupied",h.PreviewRailway(q).Reason);
        await FleetStep(h,6);var bytes=await FleetSave(h);
        ManagerRejected(await FleetSend(h,new EditRailwayCommand(q)),"staleRevision");Assert.Equal(bytes,await FleetSave(h));
        q=q with {ExpectedGeometryRevision=h.Snapshot().Mixed!.GeometryRevision};
        Assert.True(h.PreviewRailway(q).Legal);Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(q)));
        await CrossBoundary(h);
    }

    [Fact]
    public async Task Cross_tool_normal_car_skip_still_tears_down_lower_bridge_piers_at_source_call_sites()
    {
        using var h=new SessionHost(CrossToolLine("bridge"),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        await FleetStep(h,2);var before=h.Snapshot();
        var q=CrossQuery(h,"normal",22,"remove",3);var preview=h.PreviewRailway(q);
        Assert.True(preview.Legal,preview.Reason);Assert.Equal(8_000_000,preview.Cost);
        Assert.Contains(preview.Losses,l=>l.Position==new PositionRead(22,20,2)&&l.Kind=="pier");
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(q)));
        var after=h.Snapshot();Assert.Equal(before.Cash-8_000_000,after.Cash);
        foreach(var car in before.Mixed!.Fleet!.Trains[0].Cars)
            Assert.Equal(before.Mixed.Voxels.Single(v=>v.Position==car.State.Location),after.Mixed!.Voxels.Single(v=>v.Position==car.State.Location));
        Assert.DoesNotContain(after.Mixed!.Voxels,v=>v.Position==new PositionRead(22,20,2));
        await CrossBoundary(h);
    }

    [Fact]
    public async Task Cross_tool_accessories_speed_and_whole_object_line_preserve_real_consist_and_cleanup_only_absent_hosts()
    {
        using var h=new SessionHost(CrossToolLine("normal"),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        await FleetStep(h,2);
        foreach(var infrastructure in new[]{"pole","speed"}) {
            var q=CrossQuery(h,infrastructure,22);
            if(infrastructure=="pole")q=q with {To=q.From};
            Assert.True(h.PreviewRailway(q).Legal);Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(q)));
        }
        var before=h.Snapshot();var from=new PositionRead(22,20,2);
        var removal=new RemovalQuery(before.WorldEpoch,before.Mixed!.GeometryRevision,before.ContentVersion,from,0,0,new(24,20,2));
        var preview=h.PreviewRemoval(removal);Assert.True(preview.Legal,preview.Reason);
        Assert.Equal(new[]{22,23},preview.Skipped.Select(s=>s.Position.X));
        Assert.All(preview.Skipped,s=>Assert.Equal("occupiedCar",s.Reason));
        Assert.IsType<AcceptedMessage>(await FleetSend(h,new RemoveObjectCommand(removal)));
        var after=h.Snapshot();Assert.Equal(before.Cash,after.Cash);Assert.Null(after.Mixed!.Voxels.SingleOrDefault(v=>v.Position==new PositionRead(24,20,2)));
        Assert.Equal(new[]{22,23},after.Mixed.SpeedLimits!.Low.Select(p=>p.X));
        Assert.Equal(JsonSerializer.Serialize(before.Mixed.Fleet),JsonSerializer.Serialize(after.Mixed.Fleet));
        Assert.Equal(before.Mixed.Queue!.Pending,after.Mixed.Queue!.Pending);
        var pole=CrossQuery(h,"pole",22) with {To=from,Action="remove"};
        Assert.True(h.PreviewRailway(pole).Legal);Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(pole)));
        await CrossBoundary(h);
    }

    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public async Task Cross_tool_whole_ramp_removal_rechecks_all_car_hosts_after_real_movement(int direction)
    {
        var d=new Heading(direction);var b=new Location(24,20,2);var (dx,dy)=d.Offset;
        var w=Empty(width:64,height:64);var plan=SlopeRailwayConstruction.PreviewBuild(w,b,d);
        Assert.True(plan.Legal,plan.Reason);var c=SlopeRailwayConstruction.Apply(new(w),plan,new(100));
        var start=new Location(b.X-4*dx,b.Y-4*dy,2);
        var approach=NormalRailwayConstruction.PreviewBuild(c.World,start,b);Assert.True(approach.Legal,approach.Reason);
        c=NormalRailwayConstruction.Apply(c,approach,new(100));
        var s=Session(c.World,new(100,0,[]),c.Accounts);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,Stock.Binding,3,1));
        s=Place(s,0,direction<4?start:new(b.X-dx,b.Y-dy,2),direction>=4);
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var read=h.Snapshot();var q=new SlopeQuery(read.WorldEpoch,read.Mixed!.GeometryRevision,read.ContentVersion,new(b.X,b.Y,b.Z),direction,"remove");
        Assert.True(h.PreviewSlope(q).Legal);
        await FleetStep(h,4);var before=await FleetSave(h);
        ManagerRejected(await FleetSend(h,new EditSlopeCommand(q)),"staleRevision");Assert.Equal(before,await FleetSave(h));
        q=q with {ExpectedGeometryRevision=h.Snapshot().Mixed!.GeometryRevision};
        Assert.Equal("occupiedCar",h.PreviewSlope(q).Reason);
        ManagerRejected(await FleetSend(h,new EditSlopeCommand(q)),"invalidPlacement");Assert.Equal(before,await FleetSave(h));
        await CrossBoundary(h);
    }

    [Fact]
    public async Task Cross_tool_stationary_accessories_reject_real_car_hosts_and_keep_both_callback_lifecycles()
    {
        using var h=new SessionHost(CrossToolLine("normal"),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var type=RailwayProfiles.Bundled.Bindings.First(b=>b.Stationary?.Height==1).Reference.ContributionId;
        var q=CrossQuery(h,"stationary",22) with {To=new(22,20,2),StationaryType=type};
        var bytes=await FleetSave(h);Assert.False(h.PreviewRailway(q).Legal);
        ManagerRejected(await FleetSend(h,new EditRailwayCommand(q)),"invalidPlacement");Assert.Equal(bytes,await FleetSave(h));
        q=q with {From=new(32,25,2),To=new(32,25,2)};
        Assert.True(h.PreviewRailway(q).Legal);Assert.IsType<AcceptedMessage>(await FleetSend(h,new EditRailwayCommand(q)));
        var before=h.Snapshot();Assert.Equal(2,before.Mixed!.Queue!.Pending.Length);
        var construction=before.Mixed.Queue.Pending.Where(a=>!a.HandlerId.StartsWith("train/",StringComparison.Ordinal)).ToArray();
        Assert.Single(construction);await FleetStep(h,2);var after=h.Snapshot();
        Assert.Equal(1,after.Mixed!.Fleet!.Trains[0].MoveCount);
        Assert.Equal(construction,after.Mixed.Queue!.Pending.Where(a=>!a.HandlerId.StartsWith("train/",StringComparison.Ordinal)));
        Assert.Equal(before.Mixed.Voxels.Where(v=>v.Construction!=null),after.Mixed.Voxels.Where(v=>v.Construction!=null));
        bytes=await FleetSave(h);var restored=Restore(bytes);AssertOccupancy(restored);Assert.Equal(bytes,SaveCodec.Write(restored));
    }

    [Fact]
    public async Task Cross_tool_expired_connection_receipts_roles_and_replacement_never_replay_fleet_or_construction()
    {
        using var h=new SessionHost(CrossToolLine("normal"),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var network=new NetworkSession(new(),hostFactory:(_,_)=>h);
        var owner=network.Connect(FleetNow,FleetOwner)!;
        var op=network.Connect(FleetNow,FleetOperator)!;
        var observer=network.Connect(FleetNow,new("viewer",ParticipantRole.Observer,"viewer-connection"))!;
        var query=CrossQuery(h,"pole",22) with {To=new(22,20,2)};
        var command=new CommandMessage("one-accessory",h.WorldEpoch,new EditRailwayCommand(query));
        var bytes=await FleetSave(h);
        var denied=network.Submit(observer,command,FleetNow);network.Tick(FleetNow);
        ManagerRejected(await denied,"notAuthorized");Assert.Equal(bytes,await FleetSave(h));
        var first=network.Submit(op,command,FleetNow);Assert.Same(first,network.Submit(op,command,FleetNow));
        network.Tick(FleetNow);Assert.IsType<AcceptedMessage>(await first);bytes=await FleetSave(h);
        network.Disconnect(op,FleetNow);op=network.Connect(FleetNow,FleetOperator)!;
        Assert.Equal(await first,await network.Submit(op,command,FleetNow));Assert.Equal(bytes,await FleetSave(h));
        // An idle socket has no authority while its former train stays deployed.
        network.Touch(owner,FleetNow.AddSeconds(16));network.Tick(FleetNow.AddSeconds(16));
        var expired=network.Submit(op,new("expired-step",h.WorldEpoch,new StepCommand()),FleetNow.AddSeconds(16));
        Assert.IsType<RejectedMessage>(await expired);Assert.Equal(bytes,await FleetSave(h));
        var load=h.LoadSave(bytes);h.Pump(0,FleetNow.AddSeconds(16));await load;
        var replaced=await FleetSave(h);
        var stale=network.Submit(owner,new("old-world-query",h.WorldEpoch,new EditRailwayCommand(query)),FleetNow.AddSeconds(16));
        network.Tick(FleetNow.AddSeconds(16));ManagerRejected(await stale,"staleEpoch");
        var train=h.Snapshot().Mixed!.Fleet!.Trains[0];
        var manual=network.Submit(owner,new("retired-lease",h.WorldEpoch,new AcquireControlCommand(train.Id.ToString())),FleetNow.AddSeconds(16));
        network.Tick(FleetNow.AddSeconds(16));ManagerRejected(await manual,"operationUnavailable");
        Assert.Equal(replaced,await FleetSave(h));await CrossBoundary(h);
    }
}
