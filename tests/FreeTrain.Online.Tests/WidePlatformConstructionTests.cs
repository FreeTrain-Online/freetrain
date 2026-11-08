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

public class WidePlatformConstructionTests
{
    static MixedSessionState Open(byte[] b) => SaveCodec.ReadWorld(b,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static Location Point(JsonElement e) => new(e[0].GetInt32(),e[1].GetInt32(),e[2].GetInt32());
    static MixedSessionState Place(MixedSessionState s, Location? at=null, int direction=2, int length=4, bool second=false)
    {
        var plan=WidePlatformConstruction.Preview(s.World,at??Base,new(direction),length,second);
        Assert.True(plan.Legal,plan.Reason);return WidePlatformConstruction.Commit(s,plan);
    }
    static PlatformConstructionPlan Removal(MixedSessionState s, Location? at=null,long? revision=null)
    {
        var p=Assert.Single(s.World.Stations!.Platforms);
        return WidePlatformConstruction.Preview(s.World,at??p.Location,new(0),1,false,true,p.Id,revision??p.Revision);
    }
    [Fact]
    public void Eight_independent_literal_constructible_geometries_own_center_and_distinct_lanes_and_restore_rails()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"wide").Where(r=>r.GetProperty("input").GetProperty("lanes")[0].GetBoolean()))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");var before=Empty();var bytes=SaveCodec.Write(before);
            var plan=WidePlatformConstruction.Preview(before.World,Point(input.GetProperty("base")),new(input.GetProperty("heading").GetInt32()),input.GetProperty("length").GetInt32(),input.GetProperty("lanes")[1].GetBoolean());
            Assert.True(plan.Legal,plan.Reason);Assert.Equal(bytes,SaveCodec.Write(before));
            var centers=expected.GetProperty("center").EnumerateArray().Select(Point).ToArray();
            var lanes=expected.GetProperty("lanes").EnumerateArray().SelectMany(a=>a.EnumerateArray().Select(Point)).ToArray();
            Assert.Equal(centers.Concat(lanes),plan.Positions);Assert.Equal(2_000_000,plan.Booking.Total);
            var after=WidePlatformConstruction.Commit(before,plan);MixedWire.ValidateTransition(before,after);
            var p=Assert.Single(after.World.Stations!.Platforms);Assert.Equal(PlatformKind.Wide,p.Kind);Assert.False(p.Roof);Assert.True(p.Lane0);
            Assert.Equal(lanes,p.Yards.Select(y=>y.Location));Assert.Equal("ホーム 1",p.Name);Assert.Null(p.HostId);Assert.Empty(p.Children);
            Assert.Equal("{9B087AEA-6E9C-48cd-A1F3-1B774500752E}",p.Bell.ContributionId);
            var centerOwner=after.World.Spatial.Entities.Single(e=>e.Id==p.Id);Assert.Equal(4,centerOwner.Footprint.Members.Length);
            Assert.Equal(centers.ToHashSet(),centerOwner.Footprint.At(p.Location).Select(c=>c.Position).ToHashSet());
            Assert.All(centers,c=>{Assert.Null(after.World.At(c));Assert.Equal(p.Id,after.World.Spatial.At(c).Entity!.Id);Assert.False(after.World.Spatial.At(c).Transparent);});
            Assert.All(p.Yards,y=>{Assert.Equal(y.TrafficOwnerId,after.World.At(y.Location)!.OwnerId);Assert.NotEqual(p.Id,y.TrafficOwnerId);Assert.Equal("system:yard",after.World.At(y.Location)!.Traffic!.Platform!.RecipeId);});
            Assert.Equal(1957,p.DayTimer!.DueMinute);Assert.Single(after.Clock.Capture().Pending);
            var save=SaveCodec.Write(after);Assert.Equal(save,SaveCodec.Write(Open(save)));
            // Every center and both lanes resolve the same complete demolition.
            foreach(var cell in plan.Positions)Assert.True(Removal(after,cell).Legal);
            var remove=Removal(after,p.Yards[^1].Location);Assert.Equal(0,remove.Booking.Total);
            var restored=WidePlatformConstruction.Commit(after,remove);MixedWire.ValidateTransition(after,restored);
            Assert.Empty(restored.World.Stations!.Platforms);Assert.Empty(restored.Clock.Capture().Pending);
            Assert.DoesNotContain(restored.World.Spatial.Entities,e=>e.Id==p.Id);Assert.All(centers,c=>Assert.Null(restored.World.Spatial.At(c).Entity));
            Assert.Equal(lanes.Length,restored.World.Voxels.Length);
            foreach(var v in restored.World.Voxels){Assert.Null(v.Traffic!.Platform);Assert.Equal(after.World.At(v.Position)!.Traffic!.Rail,v.Traffic.Rail);Assert.Equal(after.World.At(v.Position)!.OwnerId,v.OwnerId);}
            Assert.Equal(SaveCodec.Write(restored),SaveCodec.Write(Open(SaveCodec.Write(restored))));count++;
        }
        Assert.Equal(8,count); // no-lane / left-only editing belongs to P6-13
    }
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Compatible_attachment_preserves_traffic_poles_flags_and_duplicate_speed_lists(int direction)
    {
        var s=Place(Empty(),direction:direction,second:true);s=WidePlatformConstruction.Commit(s,Removal(s));
        var w=s.World;var pole=w.Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference;
        var voxels=w.Voxels.Select(v=>v with {Traffic=v.Traffic! with {Accessory=new(pole,0)}}).ToArray();var at=voxels[0].Position;
        var limits=new SpeedLimits([at,at],[at],[at]);
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),voxels,w.Slopes,w.Revision,limits,stations:w.Stations);
        s=Empty(world:w);var next=w.Spatial.NextEntityId;var before=SaveCodec.Write(s);
        var placed=Place(s,direction:direction,second:true);Assert.Equal(next+1,placed.World.Spatial.NextEntityId);
        var restored=WidePlatformConstruction.Commit(placed,Removal(placed));
        foreach(var v in w.Voxels)Assert.Equal(v,restored.World.At(v.Position));
        Assert.True(limits.Low.SequenceEqual(restored.World.SpeedLimits.Low));Assert.True(limits.Medium.SequenceEqual(restored.World.SpeedLimits.Medium));Assert.True(limits.Fast.SequenceEqual(restored.World.SpeedLimits.Fast));
        Assert.Equal(before,SaveCodec.Write(s));
    }
    [Theory]
    [InlineData(1,4_000_000)][InlineData(2,2_000_000)][InlineData(4,6_000_000)]
    public void One_and_two_lanes_have_the_same_base_quote_and_exact_height_reopen(int level,long cost)
    {
        foreach(var second in new[]{false,true}){
            var s=Empty();var plan=WidePlatformConstruction.Preview(s.World,Base with {Z=level},new(2),4,second);
            Assert.True(plan.Legal,plan.Reason);Assert.Equal(cost,plan.Booking.Total);
            var after=WidePlatformConstruction.Commit(s,plan);Assert.Same(s.World.Terrain,after.World.Terrain);
            Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Open(SaveCodec.Write(after))));
        }
    }
    [Fact]
    public void Center_first_and_optional_second_lane_obstacles_bounds_and_stale_ids_reject_atomically()
    {
        var s=Empty();var bytes=SaveCodec.Write(s);
        foreach(var length in new[]{0,-1,101,int.MaxValue})Assert.Equal("invalidPlatformGeometry",WidePlatformConstruction.Preview(s.World,Base,new(2),length).Reason);
        Assert.Equal("invalidPlatformGeometry",WidePlatformConstruction.Preview(s.World,Base,new(1),4).Reason);
        Assert.Equal("outside",WidePlatformConstruction.Preview(s.World,new(int.MaxValue,0,2),new(2),4).Reason);
        Assert.Equal(bytes,SaveCodec.Write(s));
        foreach(var dy in new[]{-1,0,1}){
            s=Build(Empty(),at:Base with {Y=Base.Y+dy});bytes=SaveCodec.Write(s);
            Assert.False(WidePlatformConstruction.Preview(s.World,Base,new(2),4,true).Legal);Assert.Equal(bytes,SaveCodec.Write(s));
            if(dy==-1)Assert.True(WidePlatformConstruction.Preview(s.World,Base,new(2),4,false).Legal);
        }
        s=Place(Empty(),second:true);bytes=SaveCodec.Write(s);Assert.Equal("staleRevision",Removal(s,revision:1).Reason);
        Assert.Equal("obstacle",WidePlatformConstruction.Preview(s.World,Base,new(2),4,true).Reason);
        Assert.Equal("platformDependency",NormalRailwayConstruction.PreviewRemoval(s.World,new(10,11,2),new(13,11,2)).Reason);
        Assert.Equal(bytes,SaveCodec.Write(s));
        // Removing a wide platform through the slim command cannot bypass kind checks.
        var p=s.World.Stations!.Platforms[0];Assert.Equal("platformNotFound",ThinPlatformConstruction.Preview(s.World,p.Yards[0].Location,new(2),4,false,true,p.Id,p.Revision).Reason);
    }
    [Fact]
    public void Curves_junctions_perpendicular_and_special_rails_cannot_be_converted_on_either_lane()
    {
        foreach(var lane in new[]{-1,1})
        foreach(var b in RailwayProfiles.Bundled.Bindings.Where(b=>b.Handler is RailwayHandler.Normal or RailwayHandler.Junction or RailwayHandler.Bridge or RailwayHandler.Girder or RailwayHandler.Garage or RailwayHandler.Signal))
        {
            var pattern=b.Handler is RailwayHandler.Normal or RailwayHandler.Junction?b.Variant:NormalRailPatterns.Find(2,6);
            var rail=new RailComponent(b.Reference,pattern,Direction:b.Handler==RailwayHandler.Signal?2:0,ControllerId:b.Handler==RailwayHandler.Signal?1:null);
            var w=Empty().World;var at=Base with {Y=Base.Y+lane};var v=new RailwayVoxel(at,1,RailwayVoxelKind.Traffic,new(rail));
            var world=new MixedWorld(w.Profile,w.Terrain,new(2,[new(1,0,w.Profile.TrafficOwner,at,new([new(new(0,0,0),true)]),true,false,0)]),[v],[]);
            var plan=WidePlatformConstruction.Preview(world,Base,new(2),1,true);
            var legal=b.Handler==RailwayHandler.Normal&&NormalRailPatterns.Ports[pattern] is var ports&&(ports.A==2||ports.B==2)&&(ports.A+4)%8==ports.B;
            Assert.Equal(legal,plan.Legal);
        }
    }
    [Fact]
    public void Native_maximum_builds_both_lanes_and_preview_rejects_scheduler_overflow_without_mutation()
    {
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Maximum wide",new(256,256,8),0,
            Enumerable.Repeat(new TerrainCell(2),256*256),new(1,[]),[]);
        var large=new MixedSessionState(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain)),
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(256,256,8),0)));
        var maximum=Place(large,new(128,128,2),2,100,true);
        var p=Assert.Single(maximum.World.Stations!.Platforms);Assert.Equal(100,p.Length);Assert.Equal(200,p.Yards.Length);
        Assert.Equal(100,maximum.World.Spatial.Entities.Single(e=>e.Id==p.Id).Footprint.Members.Length);
        Assert.Equal(SaveCodec.Write(maximum),SaveCodec.Write(Open(SaveCodec.Write(maximum))));
        using var h=new SessionHost(Empty(clock:new(517,long.MaxValue,[])),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var r=h.Snapshot();var q=new WidePlatformQuery(r.WorldEpoch,r.Mixed!.GeometryRevision,r.Tick,r.ContentVersion,"place",new(10,10,2),2,4,true,0,0);
        Assert.Equal("constructionOverflow",h.PreviewWidePlatform(q).Reason);
        Assert.Null(h.Snapshot().Mixed!.StationGraph);
    }
    [Fact]
    public void Daily_upkeep_is_per_center_length_and_continues_exactly_without_host_effects()
    {
        var s=Place(Empty(),second:true);var restored=Open(SaveCodec.Write(s));
        for(var n=0;n<1440;n++){s.AdvanceOneMinute();restored.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));Assert.Equal(720_000,s.Construction.Accounts.Transactions[^1].Expenditure);
        Assert.Equal(3397,s.World.Stations!.Platforms[0].DayTimer!.DueMinute);Assert.Null(s.World.Stations.Platforms[0].HostId);
    }
    [Theory]
    [InlineData(false,0)][InlineData(true,0)][InlineData(false,1)][InlineData(true,1)]
    public void Real_purchased_trains_on_either_lane_pass_from_both_approaches_and_prevent_complete_demolition(bool reverse,int lane)
    {
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Wide passing",new(64,64,8),0,Enumerable.Repeat(new TerrainCell(2),4096),new(1,[]),[]);
        var world=MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain).EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
        var s=new MixedSessionState(new(world),TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(64,64,8),0)),clock:new(517,0,[]),revision:world.Revision);
        var y=lane==0?31:29;
        var rail=NormalRailwayConstruction.PreviewBuild(s.World,new(20,y,2),new(38,y,2));Assert.True(rail.Legal,rail.Reason);
        s=new(NormalRailwayConstruction.Apply(s.Construction,rail,s.Clock.Time),s.Provenance,clock:s.Clock.Capture(),revision:rail.Source.Revision+1);
        s=Place(s,new(25,30,2),2,8,true);
        var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.MinutesPerVoxel==2&&t.PricePerCar==30_000_000);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,1,1));var train=s.Fleet!.Trains[0];
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,train.Id,reverse?new(32,y,2):new(25,y,2),reverse));
        var before=SaveCodec.Write(s);Assert.Equal("occupied",Removal(s).Reason);Assert.Equal(before,SaveCodec.Write(s));var reopened=Open(before);
        for(var n=0;n<12;n++){s.AdvanceOneMinute();reopened.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));Assert.Equal(0,s.Fleet!.Trains[0].Passengers);Assert.Equal(0,s.Fleet.Trains[0].StopCallCount);
        var pose=Assert.IsType<FleetCarState.Inside>(s.Fleet.Trains[0].Head.State);Assert.Equal(new(reverse?26:31,y,2),pose.Location);
    }
    [Fact]
    public async Task Session_detached_preview_roles_quotes_clock_and_reopen_preserve_atomicity()
    {
        using var h=new SessionHost(Empty(),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        async Task<byte[]> Save(){var task=h.CaptureSave();h.Pump(0);return await task;}
        async Task<WireMessage> Send(CommandPayload payload,ParticipantContext? who=null){var task=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await task;}
        WidePlatformQuery Query(){var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,"place",new(10,10,2),2,4,true,0,0);}
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);var op=new ParticipantContext("builder",ParticipantRole.Operator);h.RegisterParticipant(observer);h.RegisterParticipant(op);
        var q=Query();var bytes=await Save();var preview=h.PreviewWidePlatform(q);Assert.True(preview.Legal,preview.Reason);Assert.Equal(12,preview.Positions.Length);Assert.Equal(bytes,await Save());
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,preview.Cost),observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,preview.Cost+1))).Code);
        Assert.IsType<AcceptedMessage>(await Send(new EditWidePlatformCommand(q,preview.Cost),op));var built=await Save();
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,preview.Cost))).Code);Assert.Equal(built,await Save());
        q=Query() with {Position=new(20,10,2)};preview=h.PreviewWidePlatform(q);Assert.IsType<AcceptedMessage>(await Send(new StepCommand()));
        var stepped=await Save();Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,preview.Cost))).Code);Assert.Equal(stepped,await Save());
        Assert.Equal(built,SaveCodec.Write(Open(built)));
    }
}
