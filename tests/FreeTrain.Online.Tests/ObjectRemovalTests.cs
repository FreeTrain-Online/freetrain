using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class ObjectRemovalTests
{
    [Theory]
    [InlineData(10,10,16,12)][InlineData(10,10,12,16)][InlineData(16,12,10,10)]
    [InlineData(-5,-2,1,1)][InlineData(1,1,-5,-2)][InlineData(10,10,10,10)]
    public void Line_stays_near_the_segment_includes_endpoints_and_reverses_exactly(int x1,int y1,int x2,int y2)
    {
        var from=new Location(x1,y1,1);var to=new Location(x2,y2,1);var path=ObjectRemoval.Line(from,to);
        Assert.Equal(from,path[0]);Assert.Equal(to,path[^1]);Assert.Equal(path.ToArray(),ObjectRemoval.Line(to,from).Reverse().ToArray());
        var length=Math.Max(Math.Abs(x2-x1),Math.Abs(y2-y1));Assert.Equal(length+1,path.Length);
        for(var i=0;i<path.Length;i++)
        {
            var ratio=length==0?0:(double)i/length;
            Assert.InRange(Math.Abs(path[i].X-(x1+(x2-x1)*ratio)),0,.5);
            Assert.InRange(Math.Abs(path[i].Y-(y1+(y2-y1)*ratio)),0,.5);
        }
    }

    [Fact]
    public void Line_rejects_different_levels_and_unbounded_work_before_interpolating()
    {
        Assert.Equal("noncoplanar",Assert.Throws<ArgumentException>(()=>ObjectRemoval.Line(new(0,0,0),new(1,1,1))).Message);
        Assert.Equal("routeWorkLimit",Assert.Throws<ArgumentException>(()=>ObjectRemoval.Line(new(int.MinValue,0,0),new(int.MaxValue,0,0))).Message);
    }

    static TerrainSessionState State()
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Remove",new(32,24,8),0);
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,request.Name,request.Dimensions,0,
            Enumerable.Repeat(new TerrainCell(1,TerrainSurface.Cut),768),new(1,[]),[]);
        return new(terrain,TerrainProvenance.From(request));
    }
    static SessionHost Host(TerrainSessionState? state=null) => new(state??State(),"owner",TerrainProfiles.Resolve,railwayResolver:RailwayProfiles.Resolve);
    static RemovalQuery Query(SessionHost host,PositionRead p)
    {
        var s=host.Snapshot();var e=s.Entities!.Single(e=>e.Footprint.Any(m=>
            new PositionRead(e.Anchor.X+m.Offset.X,e.Anchor.Y+m.Offset.Y,e.Anchor.Z+m.Offset.Z)==p));
        return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,p,e.Id,e.Revision);
    }
    static async Task<WireMessage> Send(SessionHost host,CommandPayload payload,ParticipantContext? who=null)
    {
        var task=host.Enqueue(who??new("owner"),new(Guid.NewGuid().ToString(),host.WorldEpoch,payload));host.Pump(0);return await task;
    }
    static async Task<byte[]> Save(SessionHost host){var task=host.CaptureSave();host.Pump(0);return await task;}
    static RailwayQuery Rail(SessionHost host)
    {
        var s=host.Snapshot();return new(s.WorldEpoch,s.Terrain!.GeometryRevision,s.ContentVersion,new(10,10,1),new(14,10,1),"lay","closest");
    }

    static RemovalQuery LineQuery(SessionHost host,PositionRead from,PositionRead to)
    {
        var s=host.Snapshot();return new(s.WorldEpoch,s.Mixed?.GeometryRevision??s.Terrain!.GeometryRevision,s.ContentVersion,from,0,0,to);
    }

    [Fact]
    public async Task One_line_removes_normal_rails_and_bridges_across_gaps_in_one_fenced_publication()
    {
        using var host=Host();Assert.IsType<AcceptedMessage>(await Send(host,new EditRailwayCommand(Rail(host))));
        var s=host.Snapshot();var bridge=Rail(host) with{ExpectedGeometryRevision=s.Mixed!.GeometryRevision,ExpectedContentVersion=s.ContentVersion,From=new(15,10,1),To=new(17,10,1),Infrastructure="bridge"};
        Assert.IsType<AcceptedMessage>(await Send(host,new EditRailwayCommand(bridge)));
        s=host.Snapshot();Assert.IsType<AcceptedMessage>(await Send(host,new EditRailwayCommand(bridge with{ExpectedGeometryRevision=s.Mixed!.GeometryRevision,From=new(10,12,1),To=new(14,12,1)})));
        Assert.IsType<AcceptedMessage>(await Send(host,new RemoveObjectCommand(Query(host,new(12,10,1)))));
        var q=LineQuery(host,new(10,10,1),new(17,10,1));var before=host.Snapshot();var bytes=await Save(host);
        var preview=host.PreviewRemoval(q);Assert.True(preview.Legal);Assert.Equal("line",preview.ObjectKind);
        Assert.Equal(8,preview.Path.Length);Assert.Equal(7,preview.Positions.Length);Assert.Empty(preview.Skipped);
        Assert.Equal(bytes,await Save(host));
        var viewer=new ParticipantContext("viewer",ParticipantRole.Observer);host.RegisterParticipant(viewer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(host,new RemoveObjectCommand(q),viewer)).Code);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,new RemoveObjectCommand(q with{ExpectedGeometryRevision=0}))).Code);
        Assert.IsType<AcceptedMessage>(await Send(host,new RemoveObjectCommand(q)));
        var after=host.Snapshot();Assert.Equal(before.Revision+1,after.Revision);Assert.Equal(before.Mixed!.GeometryRevision+1,after.Mixed!.GeometryRevision);
        Assert.Equal(5,after.Mixed.Voxels.Length);Assert.All(after.Mixed.Voxels,v=>Assert.Equal(12,v.Position.Y));
        Assert.Equal(before.Cash,after.Cash);Assert.Equal(before.Tick,after.Tick);Assert.Equal(before.Terrain,after.Terrain);
        Assert.Equal("noRailway",host.PreviewRemoval(LineQuery(host,q.Position,q.End!)).Reason);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,new RemoveObjectCommand(q))).Code);
        Assert.NotNull(SaveCodec.ReadWorld(await Save(host),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed);
    }

    [Fact]
    public void Straight_line_does_not_follow_a_bent_track_or_remove_scenery_or_other_levels()
    {
        var w=State().World;var p=new Location(13,11,1);var forest=w.Profile.Definitions.First(d=>d.TreeSpriteCount>0);
        var geometry=w.Profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId).Reference;
        var e=new SpatialEntity(1,0,geometry,p,new([new(new(0,0,0),true)]),false,false,forest.Price);
        var terrain=new TerrainWorld(w.Profile,w.Name,new(32,24,8),0,w.Cells,new(2,[e]),[new(1,TerrainOccupantKind.Mountain,default,new(forest.Reference,[new(10,8,0)]))]);
        var world=MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain);var time=new GameTime(GameTime.StartMinute);
        world=NormalRailwayConstruction.Apply(new(world),NormalRailwayConstruction.PreviewBuild(world,new(10,10,1),new(16,12,1)),time).World;
        world=BridgeRailwayConstruction.Apply(new(world),BridgeRailwayConstruction.Preview(world,new(10,10,3),new(16,10,3)),time).World;
        var path=ObjectRemoval.Line(new(10,10,1),new(16,12,1));
        var plan=ObjectRemoval.RailwayLine(world,path);Assert.Null(plan.Reason);Assert.Empty(plan.Skipped);
        var after=plan.Candidate!;Assert.Same(terrain,after.Terrain);Assert.Equal(e,after.Spatial.At(p).Entity);
        Assert.All(after.Voxels.Where(v=>v.Traffic?.Rail!=null&&v.Position.Z==1),v=>Assert.DoesNotContain(v.Position,path));
        Assert.Equal(world.Voxels.Where(v=>v.Traffic?.Rail!=null&&v.Position.Z==3).ToArray(),after.Voxels.Where(v=>v.Traffic?.Rail!=null&&v.Position.Z==3).ToArray());
        Assert.Contains(after.Voxels,v=>v.Position==new Location(11,11,1));
    }

    [Fact]
    public async Task Empty_terrain_lines_preview_without_promoting_and_invalid_bounds_never_mutate()
    {
        using var host=Host();var before=await Save(host);var q=LineQuery(host,new(10,10,1),new(14,10,1));
        Assert.Equal("noRailway",host.PreviewRemoval(q).Reason);
        Assert.Equal("outside",host.PreviewRemoval(q with{End=new(999,999,1)}).Reason);
        Assert.Equal("noncoplanar",host.PreviewRemoval(q with{End=new(14,10,2)}).Reason);
        Assert.IsType<RejectedMessage>(await Send(host,new RemoveObjectCommand(q)));Assert.Equal(before,await Save(host));Assert.Null(host.Snapshot().Mixed);
    }

    [Fact]
    public async Task Picked_rail_removes_only_that_object_and_preview_is_read_only_durable_and_fenced()
    {
        using var host=Host();Assert.IsType<AcceptedMessage>(await Send(host,new EditRailwayCommand(Rail(host))));
        var q=Query(host,new(12,10,1));var before=await Save(host);var read=host.Snapshot();
        var preview=host.PreviewRemoval(q);Assert.True(preview.Legal);Assert.Equal("railway",preview.ObjectKind);
        Assert.Equal(new[]{q.Position},preview.Positions);Assert.Equal(before,await Save(host));
        var viewer=new ParticipantContext("viewer",ParticipantRole.Observer);host.RegisterParticipant(viewer);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await Send(host,new RemoveObjectCommand(q),viewer)).Code);
        Assert.Equal(before,await Save(host));
        foreach(var bad in new[]{q with{ExpectedGeometryRevision=0},q with{ExpectedEntityId=999},q with{ExpectedEntityRevision=1},q with{ExpectedContentVersion=TerrainProfile.ContentVersion}})
            Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,new RemoveObjectCommand(bad))).Code);
        Assert.IsType<AcceptedMessage>(await Send(host,new RemoveObjectCommand(q)));
        var after=host.Snapshot();Assert.Equal(4,after.Mixed!.Voxels.Length);Assert.DoesNotContain(after.Mixed.Voxels,v=>v.Position==q.Position);
        Assert.Equal(read.Cash,after.Cash);Assert.Equal(read.Tick,after.Tick);Assert.Equal(read.Terrain,after.Terrain);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await Send(host,new RemoveObjectCommand(q))).Code);
        var restored=SaveCodec.ReadWorld(await Save(host),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        using var reopened=new SessionHost(restored,"owner",TerrainProfiles.Resolve,RailwayProfiles.Resolve);
        Assert.Equal(await Save(host),await Save(reopened));
    }

    [Fact]
    public void Forest_removal_preserves_nonflat_corners_identity_and_ownership()
    {
        var state=State();var w=state.World;var p=new Location(10,10,1);
        var geometry=w.Profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId).Reference;
        var forest=w.Profile.Definitions.First(d=>d.TreeSpriteCount>0);
        var e=new SpatialEntity(1,0,geometry,p,new([new(new(0,0,0),false)]),true,false,forest.Price);
        var o=new TerrainOccupant(1,TerrainOccupantKind.Mountain,new(1,2,3,4),new(forest.Reference,[new(10,8,0)]));
        var world=new TerrainWorld(w.Profile,w.Name,new(32,24,8),0,w.Cells,new(2,[e]),[o]);
        var (after,kind,reason)=ObjectRemoval.Terrain(world,p);Assert.Null(reason);Assert.Equal("forest",kind);
        var kept=Assert.Single(after!.Spatial.Entities);Assert.Equal(e with{Revision=1,Value=0},kept);
        var occupant=Assert.Single(after.Occupants);Assert.Equal(o.Corners,occupant.Corners);Assert.Null(occupant.Forest);
        Assert.Equal(world.Cells,after.Cells);Assert.Equal(2,after.Spatial.NextEntityId);Assert.Equal(0,after.RealizedTreeCount);
        Assert.Equal("terrainRequired",ObjectRemoval.Terrain(after,p).Reason);
    }

    [Fact]
    public async Task Flat_forest_and_creek_removal_clear_occupancy_without_changing_ground_or_promoting()
    {
        foreach(var creek in new[]{false,true})
        {
            var state=State();var w=state.World;var p=new Location(10,10,1);
            var binding=w.Profile.Definitions.First(d=>d.Kind==(creek?TerrainOccupantKind.Creek:TerrainOccupantKind.Mountain)&&d.TreeSpriteCount==0);
            var f=w.Profile.Definitions.First(d=>d.TreeSpriteCount>0);
            var e=new SpatialEntity(1,0,binding.Reference,p,new([new(new(0,0,0),true)]),false,creek,creek?0:f.Price);
            var o=new TerrainOccupant(1,binding.Kind,default,creek?null:new(f.Reference,[new(10,8,0)]),creek?5:0);
            var terrain=new TerrainWorld(w.Profile,w.Name,new(32,24,8),0,w.Cells,new(2,[e]),[o]);
            using var host=Host(new(terrain,state.Provenance));var q=Query(host,new(10,10,1));var bytes=await Save(host);
            Assert.Equal(creek?"creek":"forest",host.PreviewRemoval(q).ObjectKind);Assert.Equal(bytes,await Save(host));
            Assert.IsType<AcceptedMessage>(await Send(host,new RemoveObjectCommand(q)));
            var read=host.Snapshot();Assert.Null(read.Mixed);Assert.Empty(read.Entities!);Assert.Empty(read.Terrain!.Occupants);
            Assert.Equal(2,read.Terrain.NextEntityId);Assert.Equal(terrain.Revision+1,read.Terrain.GeometryRevision);
            Assert.Equal(terrain.Cells.Select(c=>c.Ground),read.Terrain.Cells.Select(c=>c.Ground));
            Assert.NotNull(SaveCodec.ReadWorld(await Save(host),TerrainProfiles.Resolve,RailwayProfiles.Resolve).Terrain);
        }
    }

    [Fact]
    public void Any_slope_member_resolves_the_complete_owner_and_lower_piers()
    {
        var w=MixedWorld.FromTerrain(RailwayProfiles.Bundled,State().World);var p=new Location(10,10,2);
        var plan=SlopeRailwayConstruction.PreviewBuild(w,p,Heading.East);Assert.True(plan.Legal);
        w=SlopeRailwayConstruction.Apply(new(w),plan,new(GameTime.StartMinute)).World;
        foreach(var member in w.Voxels.Where(v=>v.Kind!=RailwayVoxelKind.Pier))
        {
            var (after,kind,reason)=ObjectRemoval.Mixed(w,member.Position);Assert.Null(reason);Assert.Equal("slope",kind);
            Assert.Empty(after!.Voxels);Assert.Empty(after.Slopes);
        }
        Assert.Empty(ObjectRemoval.RailwayLine(w,ObjectRemoval.Line(p,p with{X=p.X+3})).Candidate!.Voxels);
        Assert.Equal("terrainRequired",ObjectRemoval.Mixed(w,new(20,10,1)).Reason);
    }

    [Theory]
    [InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public void Cars_and_platforms_protect_the_whole_object(bool slope,bool car)
    {
        var terrain=State().World;var bundled=RailwayProfiles.Bundled;
        var definition=new DefinitionReference(terrain.Profile.Packs[0].Id,"test:dependent","test:dependent");
        var profile=new RailwayProfile(terrain.Profile,bundled.Bindings.Add(new(definition,car?RailwayHandler.Car:RailwayHandler.Platform)));
        var world=MixedWorld.FromTerrain(profile,terrain);var p=new Location(10,10,1);var time=new GameTime(GameTime.StartMinute);
        world=slope?SlopeRailwayConstruction.Apply(new(world),SlopeRailwayConstruction.PreviewBuild(world,p,Heading.East),time).World:
            NormalRailwayConstruction.Apply(new(world),NormalRailwayConstruction.PreviewBuild(world,p,new(12,10,1)),time).World;
        var occupied=world.Voxels.First(v=>v.Traffic?.Rail!=null);
        var traffic=car?occupied.Traffic! with{Car=new(definition,1,1)}:occupied.Traffic! with{Platform=definition};
        var blocked=new MixedWorld(profile,terrain,world.Spatial.Capture(),world.Voxels.Select(v=>v==occupied?v with{Traffic=traffic}:v),world.Slopes,world.Revision);
        var selected=slope?world.Voxels.First(v=>v.Kind==RailwayVoxelKind.SlopeSupport).Position:occupied.Position;
        var (candidate,_,reason)=ObjectRemoval.Mixed(blocked,selected);
        Assert.Null(candidate);Assert.Equal(car?"occupiedCar":"platformDependency",reason);
        Assert.Equal(world.Voxels.Length,blocked.Voxels.Length);
        var line=ObjectRemoval.RailwayLine(blocked,ObjectRemoval.Line(occupied.Position,occupied.Position with{X=occupied.Position.X+2}));
        Assert.Equal(car?"occupiedCar":"platformDependency",Assert.Single(line.Skipped).Reason);
        if(slope){Assert.Null(line.Candidate);Assert.Equal("nothingRemovable",line.Reason);}
        else {Assert.NotNull(line.Candidate);Assert.Single(line.Candidate!.Voxels);}
    }

    [Fact]
    public void Terrace_removal_in_mixed_world_uses_the_shared_allocator_and_protects_rail_above_it()
    {
        var state=State();var p=new Location(10,10,1);var draft=new CornerTerrainDraft(state.World);
        TerraceTerrain.Apply(draft,TerraceTerrain.Preview(draft,p,"placeTerrace"));
        var terrain=draft.Finish();var world=MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain);
        var plan=NormalRailwayConstruction.PreviewBuild(world,new(12,12,1),new(14,12,1));Assert.True(plan.Legal);
        world=NormalRailwayConstruction.Apply(new(world),plan,new(GameTime.StartMinute)).World;
        var (after,kind,reason)=ObjectRemoval.Mixed(world,p);Assert.Null(reason);Assert.Equal("terrace",kind);
        Assert.Equal(world.Voxels.ToArray(),after!.Voxels.ToArray());Assert.Equal(world.Spatial.NextEntityId+1,after.Spatial.NextEntityId);
        Assert.True(after.Terrain.Spatial.Entities.Any(e=>e.Id==world.Spatial.NextEntityId));
        // Placing railway on the terrace top prevents restoring the lower ground.
        var top=p with{Z=2};var build=NormalRailwayConstruction.PreviewBuild(world,top,new(12,10,2));Assert.True(build.Legal);
        var occupied=NormalRailwayConstruction.Apply(new(world),build,new(GameTime.StartMinute)).World;
        Assert.Equal("obstacle",ObjectRemoval.Mixed(occupied,p).Reason);
    }
}
