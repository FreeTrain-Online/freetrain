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

public class PassageConstructionTests
{
    static MixedSessionState Place(MixedSessionState s,Location at,int heading=0) =>
        ThinPlatformConstruction.Commit(s,ThinPlatformConstruction.Preview(s.World,at,new(heading),4,true));
    static MixedSessionState Edit(MixedSessionState s,Location a,Location b,PassageMaterial material=PassageMaterial.Concrete,bool remove=false)
    {
        var plan=PassageConstruction.Preview(s.World,a,b,material,remove);Assert.True(plan.Legal,plan.Reason);
        var result=PassageConstruction.Commit(s,plan);MixedWire.ValidateTransition(s,result);return result;
    }
    static MixedSessionState Stair(MixedSessionState s, Location at, PassageMaterial material=PassageMaterial.Concrete,bool remove=false)
    {
        var plan=PassageConstruction.PreviewStair(s.World,at,material,remove);Assert.True(plan.Legal,plan.Reason);
        var after=PassageConstruction.Commit(s,plan);MixedWire.ValidateTransition(s,after);return after;
    }
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Stairs_all_materials_directions_forward_precedence_missing_neighbor_and_exact_continuation(int heading)
    {
        var at=new Location(10,10,2);var d=new Heading(heading);
        foreach(var material in Enum.GetValues<PassageMaterial>())
        {
            var s=Place(Empty(),at.Move(d.Opposite),heading);var bytes=SaveCodec.Write(s);
            Assert.Equal("stairNeighbor",PassageConstruction.PreviewStair(s.World,at,material).Reason);
            Assert.Equal("stairTarget",PassageConstruction.PreviewStair(s.World,at.Move(d.Left90),material).Reason);
            Assert.Equal(bytes,SaveCodec.Write(s));Assert.Same(s,Stair(s,at,remove:true));
            var back=at.Move(d.Opposite);var forward=at.Move(d);
            s=Edit(s,back.Move(d.Left90),back.Move(d.Right90));
            var before=s;bytes=SaveCodec.Write(s);var preview=PassageConstruction.PreviewStair(s.World,at,material);
            Assert.True(preview.Legal);Assert.Equal(bytes,SaveCodec.Write(s));
            s=Stair(s,at,material);var p=s.World.Stations!.Platforms[0];
            Assert.Equal(new PlatformOutlook(PlatformOutlookKind.Stair,material,Upward:false),p.Yards.Single(y=>y.Location==at).Outlook);
            Assert.Same(before.Fleet,s.Fleet);Assert.Same(before.Construction.Accounts,s.Construction.Accounts);
            Assert.Equal(before.World.Voxels.ToArray(),s.World.Voxels.ToArray());Assert.Equal(before.World.Stations!.Platforms[0].Revision+1,p.Revision);
            Assert.Throws<InvalidOperationException>(()=>PassageConstruction.Commit(s,preview));
            s=Edit(s,forward.Move(d.Left90),forward.Move(d.Right90));s=Stair(s,at,material);
            Assert.True(s.World.Stations!.Platforms[0].Yards.Single(y=>y.Location==at).Outlook.Upward);
            s=Edit(s,forward.Move(d.Left90),forward.Move(d.Right90),remove:true);
            Assert.Equal(PlatformOutlookKind.Stair,s.World.Stations!.Platforms[0].Yards.Single(y=>y.Location==at).Outlook.Kind);
            s=Stair(s,at,material);Assert.False(s.World.Stations!.Platforms[0].Yards.Single(y=>y.Location==at).Outlook.Upward);
            var reopened=SaveCodec.ReadWorld(SaveCodec.Write(s),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
            s.AdvanceOneMinute();reopened.AdvanceOneMinute();Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));
            s=Stair(s,at,remove:true);Assert.Equal(new PlatformOutlook(),s.World.Stations!.Platforms[0].Yards.Single(y=>y.Location==at).Outlook);
        }
    }
    [Fact]
    public async Task Stair_queries_use_authoritative_roles_quotes_and_replay_fences()
    {
        var s=Place(Empty(),new(10,10,2));s=Edit(s,new(9,9,2),new(11,9,2));
        using var h=new SessionHost(s,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        var snap=h.Snapshot();var q=new PassageQuery(snap.WorldEpoch,snap.Mixed!.GeometryRevision,snap.Tick,snap.ContentVersion,"place",new(10,10,2),new(10,10,2),"green","stairs");
        Assert.True(h.PreviewPassage(q).Legal);
        Assert.Throws<ArgumentException>(()=>h.PreviewPassage(q with {To=new(11,10,2)}));
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        async Task<WireMessage> Send(ParticipantContext who,long cost=0){var task=h.Enqueue(who,new(Guid.NewGuid().ToString(),h.WorldEpoch,new EditPassageCommand(q,cost)));h.Pump(0);return await task;}
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new("owner"),1)).Code);
        Assert.IsType<AcceptedMessage>(await Send(new("owner")));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new("owner"))).Code);
    }
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Every_material_heading_endpoint_and_roof_retains_identity_service_and_exact_save(int heading)
    {
        foreach(var material in Enum.GetValues<PassageMaterial>())
        {
            var at=new Location(10,10,2);var direction=new Heading(heading);
            var s=Place(Empty(),at,heading);var p=s.World.Stations!.Platforms[0];var accounts=s.Construction.Accounts;
            var clock=JsonSerializer.Serialize(s.Clock.Capture());var rng=JsonSerializer.Serialize(s.Random.Capture());
            var from=at.Move(direction.Left90);var to=at.Move(direction.Right90);
            var bytes=SaveCodec.Write(s);var plan=PassageConstruction.Preview(s.World,from,to,material);
            Assert.True(plan.Legal,plan.Reason);Assert.Equal(bytes,SaveCodec.Write(s));
            var after=Edit(s,from,to,material);var changed=after.World.Stations!.Platform(p.Id);
            Assert.Equal(new PlatformOutlook(PlatformOutlookKind.Passage,material,true),changed.Yards[0].Outlook);
            Assert.Equal(p.Revision+1,changed.Revision);Assert.Equal(p.HostId,changed.HostId);Assert.Equal(p.Children,changed.Children);
            Assert.Equal(p.Bell,changed.Bell);Assert.Equal(p.Name,changed.Name);Assert.Equal(p.DayTimer,changed.DayTimer);
            Assert.Equal(p.Yards.Select(y=>y.Roof),changed.Yards.Select(y=>y.Roof));
            Assert.Same(accounts,after.Construction.Accounts);Assert.Equal(clock,JsonSerializer.Serialize(after.Clock.Capture()));Assert.Equal(rng,JsonSerializer.Serialize(after.Random.Capture()));
            Assert.Equal(s.World.Spatial.NextEntityId,after.World.Spatial.NextEntityId);foreach(var voxel in s.World.Voxels)Assert.Equal(voxel,after.World.At(voxel.Position));
            Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(SaveCodec.ReadWorld(SaveCodec.Write(after),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!));
            Assert.Same(after,Edit(after,from,to,PassageMaterial.Green)); // Bridged keeps original material.
            var removed=Edit(after,from,to,material,true);Assert.All(removed.World.Stations!.Platforms[0].Yards,y=>Assert.Equal(new PlatformOutlook(),y.Outlook));
            var end=Edit(s,at,from,material);Assert.False(end.World.Stations!.Platforms[0].Yards[0].Outlook.Bridge);
            var promoted=Edit(end,at,from,PassageMaterial.Green);Assert.True(promoted.World.Stations!.Platforms[0].Yards[0].Outlook.Bridge);Assert.Equal(PassageMaterial.Green,promoted.World.Stations.Platform(p.Id).Yards[0].Outlook.Material);
        }
    }
    [Fact]
    public void Literal_passage_paths_agree_with_independent_source_fixtures()
    {
        using var f=Fixtures();var count=0;
        foreach(var row in Cases(f,"passage").Where(r=>r.GetProperty("input").GetProperty("kind").GetString()!="stairs"))
        {
            var i=row.GetProperty("input");var expected=row.GetProperty("expected");var cells=i.GetProperty("cells").EnumerateArray().Select(c=>c.GetString()).ToArray();
            var s=Empty();var from=new Location(10,10,2);var to=new Location(10+cells.Length-1,10,2);
            for(var n=0;n<cells.Length;n++)
            {
                if(cells[n]=="orthogonal")s=Place(s,new(10+n,10,2),i.GetProperty("rightFacing").GetBoolean()?4:0);
                else if(cells[n]!="empty")s=Place(s,new(10+n,10,2),2);
            }
            var material=Enum.Parse<PassageMaterial>(i.GetProperty("material").GetString()!,true);
            var plan=PassageConstruction.Preview(s.World,from,to,material);Assert.Equal(expected.GetProperty("legal").GetBoolean(),plan.Legal);
            if(plan.Legal){var after=PassageConstruction.Commit(s,plan);for(var n=0;n<cells.Length;n++)if(cells[n]=="orthogonal")Assert.Equal(expected.GetProperty("bridges")[n].GetBoolean(),after.World.Stations!.Platforms.Single(p=>p.Location==new Location(10+n,10,2)).Yards[0].Outlook.Bridge);}
            count++;
        }
        Assert.True(count>=12);
    }
    [Fact]
    public void Empty_remove_and_repeated_bridged_operations_are_exact_noops_and_invalid_paths_are_atomic()
    {
        var s=Empty();var from=new Location(10,10,2);var to=new Location(12,10,2);
        Assert.Same(s,Edit(s,from,to));Assert.Same(s,Edit(s,from,to,remove:true));
        s=Place(s,from);var bytes=SaveCodec.Write(s);
        foreach(var end in new[]{from,new Location(11,11,2),new Location(12,10,3),new Location(int.MaxValue,10,2)})
            Assert.Equal("invalidPassageGeometry",PassageConstruction.Preview(s.World,from,end,PassageMaterial.Concrete).Reason);
        Assert.Equal("outside",PassageConstruction.Preview(s.World,new(0,0,8),new(1,0,8),PassageMaterial.Concrete).Reason);
        Assert.Equal("passageTarget",PassageConstruction.Preview(s.World,from,from.Move(new(0)),PassageMaterial.Concrete).Reason);
        Assert.Equal(bytes,SaveCodec.Write(s));
        var plan=PassageConstruction.Preview(s.World,from,to,PassageMaterial.Brown);var after=Edit(s,from,to);
        Assert.Throws<InvalidOperationException>(()=>PassageConstruction.Commit(after,plan));
    }
    [Fact]
    public void Connected_host_children_listeners_and_service_counters_survive_cosmetic_edits()
    {
        var s=Build(Empty(),at:new(7,10,2));s=Place(s,new(10,10,2));s=Place(s,new(12,10,2));
        var before=s.World.Stations!;Assert.All(before.Platforms,p=>Assert.NotNull(p.HostId));
        var after=Edit(s,new(9,10,2),new(13,10,2),PassageMaterial.Brown);
        Assert.Equal(JsonSerializer.Serialize(before.Buildings),JsonSerializer.Serialize(after.World.Stations!.Buildings));
        Assert.Equal(JsonSerializer.Serialize(before.Listeners),JsonSerializer.Serialize(after.World.Stations.Listeners));
        foreach(var p in before.Platforms){var a=after.World.Stations!.Platform(p.Id);Assert.Equal(p.HostId,a.HostId);Assert.Equal(p.Children,a.Children);Assert.Equal(before.HostStation(p.Id),after.World.Stations.HostStation(p.Id));}
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Passage_edits_on_real_occupied_yards_preserve_train_pose_and_movement_through_exact_reopen(bool reverse)
    {
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Passage train",new(64,64,8),0,
            Enumerable.Repeat(new TerrainCell(2),64*64),new(1,[]),[]);
        var world=MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain).EnableFleet(RollingStockCatalog.Bundled.FleetProfile);
        var plan=NormalRailwayConstruction.PreviewBuild(world,new(20,30,2),new(38,30,2));Assert.True(plan.Legal,plan.Reason);
        var s=new MixedSessionState(NormalRailwayConstruction.Apply(new(world),plan,new(517)),
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(64,64,8),0)),clock:new(517,0,[]),revision:plan.Source.Revision+1);
        s=Place(s,new(25,30,2),2);
        var stock=RollingStockCatalog.Bundled.Trains.First(t=>t.MinutesPerVoxel==2&&t.PricePerCar==30_000_000);
        s=FleetTrading.Commit(s,FleetTrading.QuotePurchase(s,stock.Binding,1,1));var train=s.Fleet!.Trains[0];
        s=FleetDeployment.Commit(s,FleetDeployment.PreviewPlacement(s,train.Id,reverse?new(28,30,2):new(25,30,2),reverse));
        var before=s;var at=Assert.IsType<FleetCarState.Inside>(s.Fleet!.Trains[0].Head.State).Location;
        s=Edit(s,at with {Y=29},at with {Y=31},PassageMaterial.Green);
        Assert.Same(before.Fleet,s.Fleet);Assert.Equal(before.World.At(at),s.World.At(at));
        s=Edit(s,at with {Y=29},at with {Y=31},remove:true);Assert.Same(before.Fleet,s.Fleet);
        var neighbor=at.Move(new Heading(reverse?6:2));
        s=Edit(s,neighbor with {Y=29},neighbor with {Y=31},PassageMaterial.Brown);
        s=Stair(s,at,PassageMaterial.Green);Assert.Same(before.Fleet,s.Fleet);
        Assert.Equal(before.World.At(at),s.World.At(at));
        var reopened=SaveCodec.ReadWorld(SaveCodec.Write(s),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        for(var n=0;n<6;n++){s.AdvanceOneMinute();reopened.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));Assert.Equal(0,s.Fleet!.Trains[0].Passengers);Assert.Equal(0,s.Fleet.Trains[0].StopCallCount);
        Assert.NotEqual(at,Assert.IsType<FleetCarState.Inside>(s.Fleet!.Trains[0].Head.State).Location);
    }
    [Fact]
    public async Task Empty_terrain_passages_do_not_promote_the_profile_or_change_saved_state()
    {
        var terrain=Terrain();var state=new TerrainSessionState(terrain,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(64,24,8),0)));
        using var host=new SessionHost(state,"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
        async Task<byte[]> Save(){var t=host.CaptureSave();host.Pump(0);return await t;}
        var before=await Save();var snapshot=host.Snapshot();
        var query=new PassageQuery(snapshot.WorldEpoch,snapshot.Terrain!.GeometryRevision,snapshot.Tick,snapshot.ContentVersion,"place",new(10,10,2),new(12,10,2),"concrete");
        Assert.True(host.PreviewPassage(query).Legal);Assert.Equal(before,await Save());
        var command=host.Enqueue(new("owner"),new("empty-passage",host.WorldEpoch,new EditPassageCommand(query,0)));host.Pump(0);Assert.IsType<AcceptedMessage>(await command);
        Assert.Equal(before,await Save());Assert.Null(host.Snapshot().Mixed);
    }
    [Fact]
    public async Task Session_preview_roles_quotes_replay_minute_and_epoch_fences_are_atomic()
    {
        using var h=new SessionHost(Place(Empty(),new(10,10,2)),"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        async Task<byte[]> Save(){var task=h.CaptureSave();h.Pump(0);return await task;}
        async Task<WireMessage> Send(CommandPayload payload,ParticipantContext? who=null){var task=h.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),h.WorldEpoch,payload));h.Pump(0);return await task;}
        PassageQuery Query(){var s=h.Snapshot();return new(s.WorldEpoch,s.Mixed!.GeometryRevision,s.Tick,s.ContentVersion,"place",new(9,10,2),new(11,10,2),"brown");}
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);h.RegisterParticipant(observer);
        var q=Query();var bytes=await Save();Assert.True(h.PreviewPassage(q).Legal);Assert.Equal(bytes,await Save());
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(new EditPassageCommand(q,0),observer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditPassageCommand(q,1))).Code);
        Assert.IsType<AcceptedMessage>(await Send(new EditPassageCommand(q,0)));var saved=await Save();
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditPassageCommand(q,0))).Code);Assert.Equal(saved,await Save());
        q=Query();Assert.IsType<AcceptedMessage>(await Send(new StepCommand()));
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(new EditPassageCommand(q,0))).Code);
        Assert.Throws<InvalidOperationException>(()=>h.PreviewPassage(Query() with {ExpectedWorldEpoch=h.WorldEpoch+1}));
    }
}
