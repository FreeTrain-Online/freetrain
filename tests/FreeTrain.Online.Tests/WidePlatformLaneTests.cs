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

public class WidePlatformLaneTests
{
    static MixedSessionState Open(byte[] b) => SaveCodec.ReadWorld(b,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static StationPlatform Platform(MixedSessionState s) => Assert.Single(s.World.Stations!.Platforms);
    static MixedSessionState Place(MixedSessionState s,int direction=2) => WidePlatformConstruction.Commit(s,
        WidePlatformConstruction.Preview(s.World,Base,new(direction),4));
    static PlatformConstructionPlan Plan(MixedSessionState s,int lane,bool remove)
    {var p=Platform(s);return WidePlatformConstruction.PreviewLane(s.World,p.Location,p.Id,p.Revision,lane,remove);}
    static MixedSessionState Edit(MixedSessionState s,int lane,bool remove)
    {
        var bytes=SaveCodec.Write(s);var plan=Plan(s,lane,remove);Assert.True(plan.Legal,plan.Reason);
        Assert.Equal(0,plan.Booking.Total);Assert.Equal(bytes,SaveCodec.Write(s));
        var after=WidePlatformConstruction.Commit(s,plan);MixedWire.ValidateTransition(s,after);return after;
    }
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void All_lane_states_keep_graph_timers_accounts_and_exact_owners_with_both_approach_stop_positions(int direction)
    {
        var s=Place(Build(Empty(),at:new(7,7,2)),direction);var original=Platform(s);
        Assert.NotNull(original.HostId);var accounts=JsonSerializer.Serialize(s.Construction.Accounts);
        var clock=JsonSerializer.Serialize(s.Clock.Capture());var random=JsonSerializer.Serialize(s.Random.Capture());
        // Lane0 only -> both -> lane1 only -> neither -> lane0 -> both.
        foreach(var (lane,remove) in new[]{(1,false),(0,true),(1,true),(0,false),(1,false)})
        {
            var before=s;s=Edit(s,lane,remove);var p=Platform(s);
            Assert.Equal(original.Id,p.Id);Assert.Equal(original.HostId,p.HostId);Assert.Equal(original.DayTimer,p.DayTimer);
            Assert.Equal(original.Name,p.Name);Assert.Equal(original.Bell,p.Bell);
            Assert.Equal(accounts,JsonSerializer.Serialize(s.Construction.Accounts));Assert.Equal(clock,JsonSerializer.Serialize(s.Clock.Capture()));
            Assert.Equal(random,JsonSerializer.Serialize(s.Random.Capture()));
            Assert.Equal(p.Revision,s.World.Spatial.Entities.Single(e=>e.Id==p.Id).Revision);
            foreach(var y in before.World.Voxels)
            {var after=s.World.At(y.Position)!;Assert.Equal(y.OwnerId,after.OwnerId);Assert.Equal(y.Traffic!.Rail,after.Traffic!.Rail);}
            // Source literal L4 / train3: same heading index2; opposite index0.
            foreach(var active in new[]{0,1}.Where(l=>l==0?p.Lane0:p.Lane1))
            foreach(var index in new[]{2,0})
            {var yard=Assert.Single(p.Yards.Where(y=>y.Lane==active&&y.Index==index));Assert.Equal(StationGraph.YardLocation(p,active,index),yard.Location);}
            Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(SaveCodec.Write(s))));
            if(!p.Lane0&&!p.Lane1)
            {
                var emptyStrip=WidePlatformConstruction.Preview(s.World,p.Location,p.Direction,p.Length,removal:true,platformId:p.Id,expectedPlatformRevision:p.Revision);
                Assert.True(emptyStrip.Legal,emptyStrip.Reason);var removed=WidePlatformConstruction.Commit(s,emptyStrip);
                Assert.Empty(removed.World.Stations!.Platforms);Assert.Equal(2,removed.Clock.Capture().Pending.Length);
            }
        }
        var gone=WidePlatformConstruction.Preview(s.World,Platform(s).Location,new(0),1,removal:true,platformId:original.Id,expectedPlatformRevision:Platform(s).Revision);
        s=WidePlatformConstruction.Commit(s,gone);Assert.Empty(s.World.Stations!.Platforms);
        Assert.Equal(2,s.Clock.Capture().Pending.Length);Assert.All(s.World.Voxels,v=>Assert.Null(v.Traffic!.Platform));
    }
    [Fact]
    public void Lane_restoration_retains_accessories_duplicate_speed_lists_ground_flags_and_allocator()
    {
        var s=Edit(Place(Empty()),1,false);var w=s.World;
        var pole=w.Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference;
        var voxels=w.Voxels.Select(v=>v with {Traffic=v.Traffic! with {Accessory=new(pole,0)}}).ToArray();
        var at=voxels[0].Position;var limits=new SpeedLimits([at,at],[at],[at]);
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),voxels,w.Slopes,w.Revision,limits,stations:w.Stations);
        s=new(new(w,s.Construction.Accounts),s.Provenance,clock:s.Clock.Capture(),revision:s.Revision);
        s=Edit(Edit(Edit(Edit(s,0,true),1,true),1,false),0,false);
        Assert.Equal(w.Spatial.NextEntityId,s.World.Spatial.NextEntityId);
        Assert.True(limits.Low.SequenceEqual(s.World.SpeedLimits.Low));Assert.True(limits.Medium.SequenceEqual(s.World.SpeedLimits.Medium));Assert.True(limits.Fast.SequenceEqual(s.World.SpeedLimits.Fast));
        foreach(var v in voxels)Assert.Equal(v,s.World.At(v.Position));
    }
    [Fact]
    public void Invalid_missing_slim_duplicate_lanes_and_incompatible_tracks_are_atomic()
    {
        var s=Place(Empty());var p=Platform(s);var bytes=SaveCodec.Write(s);
        Assert.Equal("invalidPlatformGeometry",Plan(s,0,false).Reason);Assert.Equal("invalidPlatformGeometry",Plan(s,1,true).Reason);
        Assert.Equal("invalidPlatformGeometry",Plan(s,2,false).Reason);
        Assert.Equal("staleRevision",WidePlatformConstruction.PreviewLane(s.World,p.Location,p.Id,p.Revision+1,1,false).Reason);
        Assert.Equal("platformNotFound",WidePlatformConstruction.PreviewLane(s.World,p.Location,999,0,1,false).Reason);
        Assert.Equal("platformNotFound",WidePlatformConstruction.PreviewLane(s.World,new(8,8,2),p.Id,p.Revision,1,false).Reason);
        var empty=Empty();var slim=ThinPlatformConstruction.Commit(empty,ThinPlatformConstruction.Preview(empty.World,Base,new(2),4,false));
        var thin=Platform(slim);Assert.Equal("platformNotFound",WidePlatformConstruction.PreviewLane(slim.World,thin.Location,thin.Id,thin.Revision,1,false).Reason);
        Assert.Equal(bytes,SaveCodec.Write(s));
        var quoted=Plan(s,1,false);var changed=Edit(s,0,true);
        Assert.Throws<InvalidOperationException>(()=>WidePlatformConstruction.Commit(changed,quoted));
        var blocked=Build(s,at:new(10,9,2));Assert.Equal("obstacle",Plan(blocked,1,false).Reason);
        var edge=Empty();var location=edge.World.Terrain.Space.ToLocation(new(10,0,2));
        edge=WidePlatformConstruction.Commit(edge,WidePlatformConstruction.Preview(edge.World,location,new(2),1));
        Assert.Equal("outside",Plan(edge,1,false).Reason);
        foreach(var binding in RailwayProfiles.Bundled.Bindings.Where(b=>b.Handler is RailwayHandler.Normal or RailwayHandler.Junction or RailwayHandler.Signal))
        {
            var w=s.World;var at=new Location(10,9,2);var owner=w.Spatial.NextEntityId;
            var pattern=binding.Handler is RailwayHandler.Normal or RailwayHandler.Junction ? binding.Variant : NormalRailPatterns.Find(2,6);
            var rail=new RailComponent(binding.Reference,pattern,Direction:binding.Handler==RailwayHandler.Signal?2:0,ControllerId:binding.Handler==RailwayHandler.Signal?1:null);
            w=new(w.Profile,w.Terrain,new(owner+1,w.Spatial.Entities.Append(new(owner,0,w.Profile.TrafficOwner,at,new([new(new(0,0,0),true)]),true,false,0)).ToImmutableArray()),
                w.Voxels.Append(new(at,owner,RailwayVoxelKind.Traffic,new(rail))),w.Slopes,w.Revision,stations:w.Stations);
            var plan=WidePlatformConstruction.PreviewLane(w,p.Location,p.Id,p.Revision,1,false);
            Assert.Equal(binding.Handler==RailwayHandler.Normal&&pattern==NormalRailPatterns.Find(2,6),plan.Legal);
        }
    }
    [Theory]
    [InlineData(false,0,false)][InlineData(true,0,false)][InlineData(false,1,false)][InlineData(true,1,false)]
    [InlineData(false,0,true)][InlineData(true,0,true)][InlineData(false,1,true)][InlineData(true,1,true)]
    public void Moving_and_waiting_trains_block_only_the_occupied_lane_and_survive_parent_demolition_and_reopen(bool reverse,int lane,bool wait)
    {
        var s=Empty(world:MixedWorld.FromTerrain(RailwayProfiles.Bundled,Terrain()).EnableFleet(RollingStockCatalog.Bundled.FleetProfile));
        var track=NormalRailwayConstruction.PreviewBuild(s.World,new(7,lane==0?11:9,2),new(17,lane==0?11:9,2));
        s=new(NormalRailwayConstruction.Apply(s.Construction,track,s.Clock.Time),s.Provenance,clock:s.Clock.Capture(),revision:track.Source.Revision+1);
        s=Edit(Place(Build(s,at:new(7,7,2))),1,false);
        var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.MinutesPerVoxel==2&&t.PricePerCar==30_000_000);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,3,1));var id=s.Fleet!.Trains[0].Id;
        var head=new Location(reverse?10:12,lane==0?11:9,2);
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,id,new(10,lane==0?11:9,2),reverse));
        Assert.Equal(new FleetCarState.Inside(head,new(reverse?6:2)),s.Fleet!.Train(id).Head.State);
        if(wait){s=new(s.Construction,s.Provenance,clock:s.Clock.Capture(),revision:s.Revision,
            trainCallback:(session,trainId)=>FleetMovement.Tick(session,trainId,(_,t)=>new(t,t.StopCallCount==0?FleetStopDecision.Wait(10):FleetStopDecision.Proceed)));
            s.AdvanceOneMinute();s.AdvanceOneMinute();Assert.Equal(TrainOperationalState.StoppingAtStation,s.Fleet!.Train(id).State);}
        var before=SaveCodec.Write(s);Assert.Equal("occupied",Plan(s,lane,true).Reason);
        var p=Platform(s);Assert.Equal("occupied",WidePlatformConstruction.Preview(s.World,p.Location,p.Direction,p.Length,removal:true,platformId:p.Id,expectedPlatformRevision:p.Revision).Reason);
        Assert.False(NormalRailwayConstruction.PreviewRemoval(s.World,head,head).Legal);Assert.Equal(before,SaveCodec.Write(s));
        var trains=s.Fleet!.Trains;s=Edit(s,1-lane,true);Assert.True(trains.SequenceEqual(s.Fleet!.Trains));
        var station=Assert.Single(s.World.Stations!.Buildings);var queue=s.Clock.Capture().Pending;
        var demolition=StationBuildingConstruction.Preview(s.World,station.Location,null,true,station.Id,station.Revision);
        Assert.True(demolition.Legal,demolition.Reason);s=StationBuildingConstruction.Commit(s,demolition);
        Assert.Null(Platform(s).HostId);Assert.Equal(queue.Where(a=>!a.HandlerId.StartsWith("station/")),s.Clock.Capture().Pending);
        var restored=Open(SaveCodec.Write(s));
        // A waiting snapshot resumes through the production movement handler;
        // lane/host editing does not rewrite its existing callback or train state.
        s=Open(SaveCodec.Write(s));for(var n=0;n<12;n++){s.AdvanceOneMinute();restored.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(restored));Assert.True(s.Fleet!.Train(id).MoveCount>0);
    }
    [Fact]
    public async Task Session_revalidates_lane_quotes_roles_clock_cost_and_car_occupancy_at_commit()
    {
        var s=Place(Empty(world:MixedWorld.FromTerrain(RailwayProfiles.Bundled,Terrain()).EnableFleet(RollingStockCatalog.Bundled.FleetProfile)));
        s=Edit(s,0,true);var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.MinutesPerVoxel==2&&t.PricePerCar==30_000_000);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,1,1));
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        async Task<byte[]> Save(){var task=h.CaptureSave();h.Pump(0);return await task;}
        async Task<WireMessage> Send(CommandPayload payload,ParticipantContext? who=null){var task=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await task;}
        WidePlatformQuery Query(){var r=h.Snapshot();var p=r.Mixed!.StationGraph!.Platforms[0];return new(r.WorldEpoch,r.Mixed.GeometryRevision,r.Tick,r.ContentVersion,"addLane",p.Location,p.Direction,p.Length,false,p.Id,p.Revision);}
        var q=Query();var bytes=await Save();Assert.True(h.PreviewWidePlatform(q).Legal);Assert.Equal(bytes,await Save());
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,0),observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,1))).Code);
        var read=h.Snapshot();Assert.IsType<AcceptedMessage>(await Send(new DeployFleetCommand(new(read.WorldEpoch,read.Revision,read.Tick,read.ContentVersion,s.Fleet!.Trains[0].Id,"place",new(10,11,2),false))));
        bytes=await Save();Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,0))).Code);
        q=Query();Assert.Equal("occupied",h.PreviewWidePlatform(q).Reason);
        Assert.Equal("invalidPlacement",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,0))).Code);Assert.Equal(bytes,await Save());
        // A revision-frozen empty lane can be added after withdrawal; replay cannot toggle it back.
        read=h.Snapshot();Assert.IsType<AcceptedMessage>(await Send(new DeployFleetCommand(new(read.WorldEpoch,read.Revision,read.Tick,read.ContentVersion,s.Fleet.Trains[0].Id,"withdraw",null,false))));
        q=Query();Assert.True(h.PreviewWidePlatform(q).Legal);Assert.IsType<AcceptedMessage>(await Send(new EditWidePlatformCommand(q,0)));
        bytes=await Save();Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,0))).Code);Assert.Equal(bytes,await Save());
        q=Query() with {Action="removeLane"};Assert.True(h.PreviewWidePlatform(q).Legal);Assert.IsType<AcceptedMessage>(await Send(new StepCommand()));
        bytes=await Save();Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditWidePlatformCommand(q,0))).Code);Assert.Equal(bytes,await Save());
    }

}
