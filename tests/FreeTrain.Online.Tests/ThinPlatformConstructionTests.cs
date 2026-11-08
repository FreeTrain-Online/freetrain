using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;
using static FreeTrain.Online.Tests.StationBuildingConstructionTests;

namespace FreeTrain.Online.Tests;

public class ThinPlatformConstructionTests
{
    static MixedSessionState Open(byte[] b) => SaveCodec.ReadWorld(b,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static Location Point(JsonElement e) => new(e[0].GetInt32(),e[1].GetInt32(),e[2].GetInt32());
    static MixedSessionState Place(MixedSessionState s, Location p, int direction=2, int length=4, bool roof=true)
    {
        var plan=ThinPlatformConstruction.Preview(s.World,p,new(direction),length,roof);
        Assert.True(plan.Legal,plan.Reason);return ThinPlatformConstruction.Commit(s,plan);
    }
    [Fact]
    public void All_32_literal_thin_geometries_construct_with_exact_roof_runs_and_restore_normal_rails()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"thin"))
        {
            var i=row.GetProperty("input");var e=row.GetProperty("expected");var p=Point(i.GetProperty("base"));
            var before=Empty();var bytes=SaveCodec.Write(before);
            var plan=ThinPlatformConstruction.Preview(before.World,p,new(i.GetProperty("heading").GetInt32()),i.GetProperty("length").GetInt32(),i.GetProperty("roof").GetBoolean());
            Assert.True(plan.Legal,plan.Reason);Assert.Equal(bytes,SaveCodec.Write(before));
            Assert.Equal(e.GetProperty("cells").EnumerateArray().Select(Point),plan.Positions);
            var after=ThinPlatformConstruction.Commit(before,plan);MixedWire.ValidateTransition(before,after);
            var platform=Assert.Single(after.World.Stations!.Platforms);
            Assert.Equal(e.GetProperty("roof").EnumerateArray().Select(v=>v.GetBoolean()),platform.Yards.Select(y=>y.Roof));
            Assert.Equal("ホーム 1",platform.Name);Assert.Equal("{9B087AEA-6E9C-48cd-A1F3-1B774500752E}",platform.Bell.ContributionId);
            Assert.Null(platform.HostId);Assert.Empty(platform.Children);Assert.Equal(2,after.World.Stations.NextPlatformName);
            Assert.DoesNotContain(after.World.Spatial.Entities,v=>v.Id==platform.Id);
            Assert.Equal(1957,platform.DayTimer!.DueMinute);Assert.Equal(1440,platform.DayTimer.Interval);
            var save=SaveCodec.Write(after);Assert.Equal(save,SaveCodec.Write(Open(save)));
            var remove=ThinPlatformConstruction.Preview(after.World,platform.Yards[^1].Location,new(0),1,false,true,platform.Id,platform.Revision);
            Assert.True(remove.Legal,remove.Reason);Assert.Equal(0,remove.Booking.Total);
            var restored=ThinPlatformConstruction.Commit(after,remove);MixedWire.ValidateTransition(after,restored);
            Assert.Empty(restored.World.Stations!.Platforms);Assert.Empty(restored.Clock.Capture().Pending);
            Assert.Equal(platform.Length,restored.World.Voxels.Length);
            foreach(var v in restored.World.Voxels){Assert.Null(v.Traffic!.Platform);Assert.Equal(after.World.At(v.Position)!.Traffic!.Rail,v.Traffic.Rail);Assert.Equal(after.World.At(v.Position)!.OwnerId,v.OwnerId);}
            Assert.Equal(SaveCodec.Write(restored),SaveCodec.Write(Open(SaveCodec.Write(restored))));count++;
        }
        Assert.Equal(32,count); // four directions × two styles × four lengths
    }
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Existing_tracks_keep_traffic_accessories_flags_speed_membership_and_allocator(int heading)
    {
        var p=new Location(20,20,4);
        var s=Empty();
        // Elevated starts require a real rail; the source platform creates that rail first.
        s=Place(s,p,heading,4,false);var platform=s.World.Stations!.Platforms[0];
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,p,new(heading),4,false,true,platform.Id,platform.Revision));
        var w=s.World;var owners=w.Spatial.NextEntityId;var saved=SaveCodec.Write(s);
        var after=Place(s,p,heading,4,true);Assert.Equal(owners+1,after.World.Spatial.NextEntityId);
        foreach(var v in w.Voxels){var a=after.World.At(v.Position)!;Assert.Equal(v.OwnerId,a.OwnerId);Assert.Equal(v.Traffic!.Rail,a.Traffic!.Rail);Assert.Equal(v.DrawGroundAbove,a.DrawGroundAbove);Assert.Equal(v.DrawGroundBelow,a.DrawGroundBelow);}
        Assert.Equal(6_000_000,after.Construction.Accounts.Transactions[^1].Expenditure);Assert.Equal(saved,SaveCodec.Write(s));
    }
    [Theory]
    [InlineData(1,4_000_000)][InlineData(2,2_000_000)][InlineData(4,6_000_000)]
    public void Ground_elevated_and_underground_quotes_use_only_base_ground_and_keep_normal_track(int level,long cost)
    {
        var s=Empty();var p=Base with {Z=level};var plan=ThinPlatformConstruction.Preview(s.World,p,new(2),4,true);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(cost,plan.Booking.Total);
        var after=ThinPlatformConstruction.Commit(s,plan);Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Open(SaveCodec.Write(after))));
        Assert.All(after.World.Voxels,v=>Assert.Equal(level,v.Position.Z));
        Assert.Same(s.World.Terrain,after.World.Terrain);
    }
    [Fact]
    public void Compatible_track_attachment_and_removal_preserve_poles_and_all_speed_lists()
    {
        var s=Place(Empty(),Base);var p=s.World.Stations!.Platforms[0];
        s=ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,Base,new(2),4,true,true,p.Id,p.Revision));
        var w=s.World;var pole=w.Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference;
        var voxels=w.Voxels.Select(v=>v with {Traffic=v.Traffic! with {Accessory=new(pole,0)}}).ToArray();
        var limits=new SpeedLimits([Base,Base],[Base],[Base]);
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),voxels,w.Slopes,w.Revision,limits,stations:w.Stations);
        s=Empty(world:w);var before=s;var after=Place(s,Base);var platform=after.World.Stations!.Platforms[0];
        after=ThinPlatformConstruction.Commit(after,ThinPlatformConstruction.Preview(after.World,Base,new(2),4,false,true,platform.Id,platform.Revision));
        Assert.True(limits.Low.SequenceEqual(after.World.SpeedLimits.Low));Assert.True(limits.Medium.SequenceEqual(after.World.SpeedLimits.Medium));Assert.True(limits.Fast.SequenceEqual(after.World.SpeedLimits.Fast));
        foreach(var v in before.World.Voxels)Assert.Equal(v,after.World.At(v.Position));
    }
    [Fact]
    public void Overflow_and_stale_platform_identity_reject_before_live_mutation()
    {
        var s=Place(Empty(),Base);var p=s.World.Stations!.Platforms[0];var bytes=SaveCodec.Write(s);
        Assert.Equal("staleRevision",ThinPlatformConstruction.Preview(s.World,Base,new(2),4,true,true,p.Id,p.Revision+1).Reason);
        Assert.Equal("platformNotFound",ThinPlatformConstruction.Preview(s.World,Base,new(2),4,true,true,p.Id+999,p.Revision).Reason);
        Assert.Equal("platformDependency",NormalRailwayConstruction.PreviewRemoval(s.World,Base,Base.Move(new(2))).Reason);
        Assert.Equal(bytes,SaveCodec.Write(s));
        using var h=new SessionHost(Empty(clock:new(517,long.MaxValue,[])),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var r=h.Snapshot();var query=new ThinPlatformQuery(r.WorldEpoch,r.Mixed!.GeometryRevision,r.Tick,r.ContentVersion,"place",new(10,10,2),2,4,true,0,0);
        Assert.Equal("constructionOverflow",h.PreviewThinPlatform(query).Reason);
    }
    [Fact]
    public void Length_bounds_obstacles_edges_curves_junctions_and_all_special_handlers_reject_atomically()
    {
        var s=Empty();var bytes=SaveCodec.Write(s);
        foreach(var length in new[]{0,-1,101,int.MaxValue})Assert.Equal("invalidPlatformGeometry",ThinPlatformConstruction.Preview(s.World,Base,new(2),length,false).Reason);
        Assert.Equal("invalidPlatformGeometry",ThinPlatformConstruction.Preview(s.World,Base,new(1),4,false).Reason);
        Assert.Equal("outside",ThinPlatformConstruction.Preview(s.World,new(int.MaxValue,0,2),new(2),4,false).Reason);
        Assert.Equal(bytes,SaveCodec.Write(s));
        s=Build(s);Assert.Equal("obstacle",ThinPlatformConstruction.Preview(s.World,Base,new(2),4,false).Reason);
        foreach(var b in RailwayProfiles.Bundled.Bindings.Where(b=>b.Handler is RailwayHandler.Normal or RailwayHandler.Junction or RailwayHandler.Bridge or RailwayHandler.Girder or RailwayHandler.Garage or RailwayHandler.Signal))
        {
            var pattern=b.Handler is RailwayHandler.Normal or RailwayHandler.Junction?b.Variant:NormalRailPatterns.Find(2,6);
            var rail=new RailComponent(b.Reference,pattern,Direction:b.Handler==RailwayHandler.Signal?2:0,ControllerId:b.Handler==RailwayHandler.Signal?1:null);
            var w=Empty().World;var v=new RailwayVoxel(Base,1,RailwayVoxelKind.Traffic,new(rail));
            var world=new MixedWorld(w.Profile,w.Terrain,new(2,[new(1,0,w.Profile.TrafficOwner,Base,new([new(new(0,0,0),true)]),true,false,0)]),[v],[]);
            var plan=ThinPlatformConstruction.Preview(world,Base,new(2),1,false);
            var legal=b.Handler==RailwayHandler.Normal && NormalRailPatterns.Ports[pattern] is var ports && (ports.A==2||ports.B==2)&&(ports.A+4)%8==ports.B;
            Assert.Equal(legal,plan.Legal);
        }
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Long",new(256,256,8),0,
            Enumerable.Repeat(new TerrainCell(2),256*256),new(1,[]),[]);
        var large=new MixedSessionState(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain)),
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(256,256,8),0)));
        var maximum=Place(large,new(128,128,2),2,100,false);
        Assert.Equal(100,Assert.Single(maximum.World.Stations!.Platforms).Length);
    }
    [Fact]
    public void Daily_upkeep_runs_at_creation_plus_one_day_and_resumes_exactly_after_reopen()
    {
        var s=Place(Empty(),Base);var restored=Open(SaveCodec.Write(s));
        for(var n=0;n<1440;n++){s.AdvanceOneMinute();restored.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));
        Assert.Equal(720_000,s.Construction.Accounts.Transactions[^1].Expenditure);
        Assert.Equal(3397,s.World.Stations!.Platforms[0].DayTimer!.DueMinute);
        Assert.Null(s.World.Stations.Platforms[0].HostId);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Real_trains_pass_disconnected_yards_from_both_approaches_and_block_demolition(bool reverse)
    {
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Passing",new(64,64,8),0,
            Enumerable.Repeat(new TerrainCell(2),64*64),new(1,[]),[]);
        var world=MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain).EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
        var w=NormalRailwayConstruction.PreviewBuild(world,new(20,30,2),new(38,30,2));Assert.True(w.Legal,w.Reason);
        var s=new MixedSessionState(NormalRailwayConstruction.Apply(new(world),w,new(517)),
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(64,64,8),0)),clock:new(517,0,[]),revision:w.Source.Revision+1);
        s=Place(s,new(25,30,2),2,8);
        var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.MinutesPerVoxel==2&&t.PricePerCar==30_000_000);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,1,1));var train=s.Fleet!.Trains[0];
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,train.Id,reverse?new(32,30,2):new(25,30,2),reverse));
        var platform=s.World.Stations!.Platforms[0];var before=SaveCodec.Write(s);
        Assert.Equal("occupied",ThinPlatformConstruction.Preview(s.World,platform.Location,new(2),8,true,true,platform.Id,platform.Revision).Reason);
        Assert.Equal(before,SaveCodec.Write(s));var reopened=Open(before);
        for(var n=0;n<12;n++){s.AdvanceOneMinute();reopened.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));
        Assert.Equal(0,s.Fleet!.Trains[0].Passengers);Assert.Equal(0,s.Fleet.Trains[0].StopCallCount);
        var pose=Assert.IsType<FleetCarState.Inside>(s.Fleet.Trains[0].Head.State);
        Assert.Equal(reverse?26:31,pose.Location.X);
    }
    [Fact]
    public async Task Session_preview_roles_cost_clock_identity_replay_and_durable_state_are_atomic()
    {
        using var h=new SessionHost(Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        async Task<byte[]> Save(){var task=h.CaptureSave();h.Pump(0);return await task;}
        async Task<WireMessage> Send(CommandPayload payload,ParticipantContext? who=null,string? id=null){var task=h.Enqueue(who??new("owner"),new(id??Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await task;}
        ThinPlatformQuery Query(){var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,"place",new(10,10,2),2,4,true,0,0);}
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);var op=new ParticipantContext("builder",ParticipantRole.Operator);h.RegisterParticipant(observer);h.RegisterParticipant(op);
        var q=Query();var bytes=await Save();var preview=h.PreviewThinPlatform(q);Assert.True(preview.Legal,preview.Reason);Assert.Equal(bytes,await Save());
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(new EditThinPlatformCommand(q,preview.Cost),observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditThinPlatformCommand(q,preview.Cost+1))).Code);
        Assert.IsType<AcceptedMessage>(await Send(new EditThinPlatformCommand(q,preview.Cost),op,"once"));var built=await Save();
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditThinPlatformCommand(q,preview.Cost),op,"once")).Code);Assert.Equal(built,await Save());
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditThinPlatformCommand(q,preview.Cost))).Code);
        q=Query() with {Position=new(20,10,2)};preview=h.PreviewThinPlatform(q);Assert.IsType<AcceptedMessage>(await Send(new StepCommand()));
        var stepped=await Save();Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditThinPlatformCommand(q,preview.Cost))).Code);Assert.Equal(stepped,await Save());
        Assert.Equal(built,SaveCodec.Write(Open(built)));
    }
}
