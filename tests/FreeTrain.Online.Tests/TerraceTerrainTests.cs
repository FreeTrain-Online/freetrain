using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class TerraceTerrainTests
{
    private static readonly Location P=new(10,8,1);
    private static TerrainWorld Scene(int ground=1,int levels=6,bool forest=false,QuarterCorners? corners=null)
    {
        var offsets=corners??new QuarterCorners(1,2,3,4);
        var profile=TerrainProfiles.Bundled;var mountain=profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId);
        var tree=profile.Definitions.First(d=>d.TreeSpriteCount>0);
        var cells=Enumerable.Repeat(new TerrainCell(ground,TerrainSurface.Cut),16*24);
        return new(profile,"Terrace",new(16,24,levels),1,cells,new(2,[new(1,0,mountain.Reference,P with{Z=ground},
            new([new(new(0,0,0),offsets.Flat)]),forest,false,forest?tree.Price:0)]),
            [new(1,TerrainOccupantKind.Mountain,offsets,forest?new(tree.Reference,[new(0,0,0)]):null)]);
    }
    private static TerrainWorld Edit(TerrainWorld w,string action,Location? p=null)
    {var d=new CornerTerrainDraft(w);var plan=TerraceTerrain.Preview(d,p??P,action);Assert.True(plan.Legal,plan.Reason);TerraceTerrain.Apply(d,plan);return d.Finish();}
    private static SessionHost Host(TerrainWorld w)=>new(new TerrainSessionState(w,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Name,new(16,24,w.Space.Levels),1))),"owner",TerrainProfiles.Resolve);
    private static CornerTerrainQuery Query(TerrainWorld w,string action,Location? p=null)=>new(1,w.Revision,new((p??P).X,(p??P).Y,(p??P).Z),action);
    [Fact]
    public void Terrace_round_trip_restores_only_literal_offsets_and_allocates_fresh_unowned_entities()
    {
        var w=Scene(forest:true);var preview=TerraceTerrain.Preview(w,P,"placeTerrace");
        var c=Assert.Single(preview.Changes);Assert.True(c.LosesForest);Assert.True(c.LosesOwnership);Assert.Equal(2,c.AfterGround);
        Assert.Equal(1,w.Spatial.At(P).Entity!.Id);
        var terrace=Edit(w,"placeTerrace");Assert.Equal(TerrainSurface.Terrace,terrace.Cell(terrace.Space.ToStorage(P).H,terrace.Space.ToStorage(P).V).Surface);
        var o=terrace.OccupantAt(P)!;Assert.Equal(TerrainOccupantKind.Terrace,o.Kind);Assert.Equal(0x1234,o.Corners.Packed);
        Assert.Equal(2,o.EntityId);Assert.False(terrace.Spatial.At(P).Entity!.Owned);Assert.Equal(0,terrace.Spatial.At(P).Entity!.Value);
        var restored=Edit(terrace,"removeTerrace");o=restored.OccupantAt(P)!;
        Assert.Equal(TerrainOccupantKind.Mountain,o.Kind);Assert.Equal(0x1234,o.Corners.Packed);Assert.Null(o.Forest);
        Assert.Equal(3,o.EntityId);Assert.Equal(4,restored.Spatial.NextEntityId);Assert.False(restored.Spatial.At(P).Entity!.Owned);
        Assert.Equal(1,restored.Cell(restored.Space.ToStorage(P).H,restored.Space.ToStorage(P).V).Ground);
        Assert.Equal(2,restored.Revision);
    }
    [Fact]
    public void Cut_preserves_ground_and_restores_literal_same_Z_neighbor_maxima()
    {
        var w=Scene();var cut=Edit(w,"placeCut");Assert.Null(cut.OccupantAt(P));
        Assert.Equal(4,cut.FineHeight(P,0));Assert.Equal("noChange",TerraceTerrain.Preview(cut,P,"removeCut").Reason);
        // Each named corner sees exactly one source-neighbor sample, without invoking production helpers.
        var d=w.Profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId).Reference;
        var entities=new[]{new SpatialEntity(2,0,d,new(11,7,1),new([new(new(0,0,0),false)]),false,false,0),
            new SpatialEntity(3,0,d,new(11,9,1),new([new(new(0,0,0),false)]),false,false,0),
            new SpatialEntity(4,0,d,new(9,9,1),new([new(new(0,0,0),false)]),false,false,0),
            new SpatialEntity(5,0,d,new(9,7,1),new([new(new(0,0,0),false)]),false,false,0)};
        cut=new(w.Profile,w.Name,new(16,24,6),1,cut.Cells,new(6,entities.ToImmutableArray()),
            [new(2,TerrainOccupantKind.Mountain,new(0,0,1,0)),new(3,TerrainOccupantKind.Mountain,new(0,0,0,2)),
             new(4,TerrainOccupantKind.Mountain,new(3,0,0,0)),new(5,TerrainOccupantKind.Mountain,new(0,4,0,0))]);
        var restored=Edit(cut,"removeCut");Assert.Equal(0x1234,restored.OccupantAt(P)!.Corners.Packed);
        Assert.Equal(6,restored.OccupantAt(P)!.EntityId);Assert.Equal(1,restored.Occupants.Length-4);
    }
    [Theory][InlineData("placeTerrace")][InlineData("placeCut")][InlineData("removeTerrace")][InlineData("removeCut")]
    public void Outside_unknown_and_wrong_elevation_do_not_mutate(string action)
    {
        var w=Scene();Assert.Equal("outside",TerraceTerrain.Preview(w,new(-999,0,0),action).Reason);
        Assert.False(TerraceTerrain.Preview(w,P with{Z=2},action).Legal);Assert.Equal(0,w.Revision);
        Assert.Equal("invalidInput",TerraceTerrain.Preview(w,P,"unknown").Reason);
    }
    [Fact]
    public void Roof_saturated_backup_water_and_allocator_guards()
    {
        var top=P with{Z=5};var w=Scene(5,corners:new(1,2,3,3));Assert.Equal("roof",TerraceTerrain.Preview(w,top,"placeTerrace").Reason);
        w=Scene(corners:new(4,4,4,4));Assert.Equal(0x4444,Edit(Edit(w,"placeTerrace"),"removeTerrace").OccupantAt(P)!.Corners.Packed);
        w=new(w.Profile,w.Name,new(16,24,6),4,w.Cells,w.Spatial.Capture(),w.Occupants);
        Assert.Equal(4,Edit(w,"placeTerrace").WaterLevel);
        w=new(w.Profile,w.Name,new(16,24,6),4,w.Cells,new(long.MaxValue,w.Spatial.Entities),w.Occupants);
        Assert.Equal("capacityLimit",TerraceTerrain.Preview(w,P,"placeTerrace").Reason);
    }
    [Fact]
    public void Higher_cardinal_ground_prevents_cut_but_same_Z_restoration_ignores_other_levels()
    {
        var w=Scene();var cells=w.Cells.ToBuilder();var s=w.Space.ToStorage(P.Move(Heading.North));cells[s.V*16+s.H]=new(3,TerrainSurface.Cut);
        w=new(w.Profile,w.Name,new(16,24,6),1,cells,w.Spatial.Capture(),w.Occupants);
        Assert.Equal("higherNeighbor",TerraceTerrain.Preview(w,P,"placeCut").Reason);
        var empty=new TerrainWorld(w.Profile,w.Name,new(16,24,6),1,cells,new(2,[]),[]);
        Assert.Equal("noChange",TerraceTerrain.Preview(empty,P,"removeCut").Reason);
    }
    [Fact]
    public void Decorated_surface_above_terrace_blocks_removal_and_is_never_reclaimed()
    {
        var w=Edit(Scene(),"placeTerrace");var d=w.Profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId).Reference;
        var above=P with{Z=2};var e=new SpatialEntity(3,0,d,above,new([new(new(0,0,0),true)]),false,false,0);
        w=new(w.Profile,w.Name,new(16,24,6),1,w.Cells,new(4,w.Spatial.Entities.Add(e)),w.Occupants.Add(new(3,TerrainOccupantKind.Mountain,default)));
        Assert.Equal("occupied",TerraceTerrain.Preview(w,P,"removeTerrace").Reason);Assert.Equal(2,w.Occupants.Length);
        Assert.Equal("occupied",TerraceTerrain.Preview(w,above,"removeCut").Reason);
    }
    [Fact]
    public async Task FIFO_roles_conflict_no_booking_clock_independence_and_Save7_continuation()
    {
        var w=Scene(forest:true);using var host=Host(w);var owner=new ParticipantContext("owner");var op=new ParticipantContext("op",ParticipantRole.Operator);var observer=new ParticipantContext("viewer",ParticipantRole.Observer);host.RegisterParticipant(op);host.RegisterParticipant(observer);
        var q=Query(w,"placeTerrace");var before=host.Snapshot();Assert.True(host.PreviewCorner(q).Legal);Assert.Equal(before,host.Snapshot());
        Assert.Throws<ArgumentException>(()=>host.PreviewCorner(q with{OffsetX=1,OffsetY=0}));
        Assert.Throws<ArgumentException>(()=>host.PreviewCorner(q with{Stroke=[new(new(10,8,1),"raise")]}));
        var step=host.Enqueue(owner,new("step",1,new StepCommand()));host.Pump(0);Assert.IsType<AcceptedMessage>(await step);
        var deny=host.Enqueue(observer,new("deny",1,new EditCornerCommand(q)));var commit=host.Enqueue(op,new("place",1,new EditCornerCommand(q)));var conflict=host.Enqueue(owner,new("conflict",1,new EditCornerCommand(q)));host.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await deny).Code);Assert.IsType<AcceptedMessage>(await commit);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await conflict).Code);
        Assert.Equal(before.Cash,host.Snapshot().Cash);Assert.True(host.Snapshot().Paused);
        var save=host.CaptureSave();host.Pump(0);var bytes=await save;var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;Assert.Equal(bytes,SaveCodec.Write(restored));
        using var reopened=new SessionHost(restored,"owner",TerrainProfiles.Resolve);q=Query(restored.World,"removeTerrace");
        var remove=reopened.Enqueue(owner,new("remove",1,new EditCornerCommand(q)));reopened.Pump(0);Assert.IsType<AcceptedMessage>(await remove);
        var end=reopened.CaptureSave();reopened.Pump(0);var world=SaveCodec.ReadWorld(await end,TerrainProfiles.Resolve).Terrain!.World;Assert.Equal(0x1234,world.OccupantAt(P)!.Corners.Packed);Assert.Null(world.OccupantAt(P)!.Forest);
    }
    [Theory][InlineData(0,0)][InlineData(15,0)][InlineData(0,23)][InlineData(15,23)]
    public void Border_terraces_restore_exact_offsets_and_border_cuts_restore_without_phantom_samples(int h,int v)
    {
        var w=Scene();var p=w.Space.ToLocation(new(h,v,1));var entity=w.Spatial.Entities[0] with{Anchor=p};
        w=new(w.Profile,w.Name,new(16,24,6),1,w.Cells,new(2,[entity]),w.Occupants);
        var restored=Edit(Edit(w,"placeTerrace",p),"removeTerrace",p);Assert.Equal(0x1234,restored.OccupantAt(p)!.Corners.Packed);
        var cut=Edit(w,"placeCut",p);Assert.Equal("noChange",TerraceTerrain.Preview(cut,p,"removeCut").Reason);
    }
    [Fact]
    public void Natural_slope_terrace_and_cut_make_explicit_local_discontinuities()
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Natural",new(16,24,6),1);var w=TerrainWorld.Empty(request,TerrainProfiles.Bundled);
        var d=new CornerTerrainDraft(w);RectangleTerrain.Plan(d,P,P with{X=P.X+1,Y=P.Y+1},"raise");w=d.Finish();
        Assert.All(w.Cells,c=>Assert.Equal(TerrainSurface.Natural,c.Surface));
        var terrace=Edit(w,"placeTerrace");var restored=Edit(terrace,"removeTerrace");Assert.Equal(0x0400,restored.OccupantAt(P)!.Corners.Packed);
        var cut=Edit(w,"placeCut");Assert.Null(cut.OccupantAt(P));Assert.Equal(0x0400,Edit(cut,"removeCut").OccupantAt(P)!.Corners.Packed);
    }

    private static TerrainWorld BatchScene()
    {
        var w=Scene(forest:true);var definition=w.Spatial.Entities[0].Definition;
        var entities=w.Spatial.Entities.ToBuilder();var occupants=w.Occupants.ToBuilder();long id=2;
        foreach(var p in new[]{P with{Y=P.Y+1},P with{X=P.X+1},P with{X=P.X+1,Y=P.Y+1}})
        {
            entities.Add(new(id,0,definition,p,new([new(new(0,0,0),false)]),false,false,0));
            occupants.Add(new(id++,TerrainOccupantKind.Mountain,new(1,2,3,4)));
        }
        return new(w.Profile,w.Name,new(16,24,6),1,w.Cells,new(id,entities.ToImmutable()),occupants);
    }
    private static CornerTerrainQuery BatchQuery(TerrainWorld w)=>Query(w,"placeTerrace") with{Rectangle=new(new(P.X+1,P.Y+1,P.Z))};
    [Fact]
    public async Task Rectangle_terraces_preview_exact_losses_and_commit_once_with_fresh_ids_and_saved_backups()
    {
        var w=BatchScene();using var host=Host(w);var q=BatchQuery(w);var before=host.Snapshot();
        var preview=host.PreviewCorner(q);Assert.True(preview.Legal);Assert.Equal(4,preview.Changes.Length);Assert.Empty(preview.Skipped);
        Assert.Single(preview.Changes,c=>c.LosesForest&&c.LosesOwnership);Assert.Equal(before,host.Snapshot());
        Assert.Equal(new[]{(10,8),(10,9),(11,8),(11,9)},preview.Changes.Select(c=>(c.Position.X,c.Position.Y)));
        var reversed=host.PreviewCorner(q with{Position=q.Rectangle!.End,Rectangle=new(q.Position)});Assert.Equal(preview.Changes,reversed.Changes);
        var commit=host.Enqueue(new("owner"),new("batch",1,new EditCornerCommand(q)));host.Pump(0);Assert.IsType<AcceptedMessage>(await commit);
        var capture=host.CaptureSave();host.Pump(0);var bytes=await capture;var restored=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        Assert.Equal(bytes,SaveCodec.Write(restored));Assert.Equal(1,restored.World.Revision);Assert.Equal(9,restored.World.Spatial.NextEntityId);
        Assert.All(restored.World.Occupants,o=>{Assert.Equal(TerrainOccupantKind.Terrace,o.Kind);Assert.Equal(0x1234,o.Corners.Packed);Assert.Null(o.Forest);});
        Assert.Equal(new long[]{5,6,7,8},restored.World.Spatial.Entities.Select(e=>e.Id));Assert.All(restored.World.Spatial.Entities,e=>Assert.False(e.Owned));
        Assert.Equal(before.Tick,host.Snapshot().Tick);Assert.Equal(before.Paused,host.Snapshot().Paused);Assert.Equal(before.Cash,host.Snapshot().Cash);
        // Each source removal restores its own backed-up corners after a batch save/reopen.
        var draft=new CornerTerrainDraft(restored.World);
        foreach(var c in preview.Changes){var plan=TerraceTerrain.Preview(draft,new(c.Position.X,c.Position.Y,c.Position.Z),"removeTerrace");TerraceTerrain.Apply(draft,plan);}
        Assert.All(draft.Finish().Occupants,o=>Assert.Equal(0x1234,o.Corners.Packed));
    }
    [Fact]
    public void Rectangle_terraces_report_ineligible_cells_without_bypassing_source_rules()
    {
        var w=Scene();var cells=w.Cells.ToBuilder();
        foreach(var p in new[]{P with{Y=P.Y+1},P with{X=P.X+1},P with{X=P.X+1,Y=P.Y+1}}){var t=w.Space.ToStorage(p);cells[t.V*16+t.H]=new(2,TerrainSurface.Cut);}
        w=new(w.Profile,w.Name,new(16,24,6),1,cells,w.Spatial.Capture(),w.Occupants);
        using var host=Host(w);var result=host.PreviewCorner(BatchQuery(w));
        Assert.True(result.Legal);Assert.Single(result.Changes);Assert.Equal(3,result.Skipped.Length);Assert.All(result.Skipped,s=>Assert.Equal("groundLevel",s.Reason));
        var roof=Scene(5,corners:new(1,2,3,3));using var roofHost=Host(roof);var q=BatchQuery(roof) with{Position=new(P.X,P.Y,5),Rectangle=new(new(P.X+1,P.Y+1,5))};
        var blocked=roofHost.PreviewCorner(q);Assert.False(blocked.Legal);Assert.Empty(blocked.Changes);Assert.Contains(blocked.Skipped,s=>s.Reason=="roof");
        Assert.Equal(0,roofHost.Snapshot().Terrain!.GeometryRevision);
    }
    [Fact]
    public void Rectangle_terraces_admit_64_cells_and_reject_oversized_or_mixed_selections()
    {
        var p=TerrainProfiles.Bundled;var definition=p.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId).Reference;
        var entities=new List<SpatialEntity>();var occupants=new List<TerrainOccupant>();long id=1;
        for(int x=20;x<28;x++)for(int y=20;y<28;y++){entities.Add(new(id,0,definition,new(x,y,1),new([new(new(0,0,0),true)]),false,false,0));occupants.Add(new(id++,TerrainOccupantKind.Mountain,default));}
        var w=new TerrainWorld(p,"Batch limit",new(30,42,6),1,Enumerable.Repeat(new TerrainCell(1),30*42),new(id,entities.ToImmutableArray()),occupants);
        var draft=new CornerTerrainDraft(w);var plans=TerraceTerrain.PlaceRectangle(draft,new(20,20,1),new(27,27,1));Assert.Equal(64,plans.Length);Assert.All(plans,s=>Assert.True(s.Legal));Assert.Equal(64,draft.Finish().Occupants.Length);
        using var host=Host(BatchScene());var q=BatchQuery(BatchScene());
        foreach(var bad in new[]{q with{Rectangle=new(new(P.X+8,P.Y+7,1))},q with{Rectangle=new(new(P.X,P.Y,2))},q with{Rectangle=new(q.Position,true)},q with{OffsetX=0,OffsetY=0},q with{Stroke=[new(q.Position,"raise")]},q with{Action="removeTerrace"},q with{Action="placeCut"},q with{Action="removeCut"}})
            Assert.Throws<ArgumentException>(()=>host.PreviewCorner(bad));
        Assert.Equal(0,host.Snapshot().Terrain!.GeometryRevision);
    }
    [Fact]
    public async Task Rectangle_terraces_revalidate_role_revision_and_all_skipped_commits()
    {
        var w=BatchScene();using var host=Host(w);var op=new ParticipantContext("op",ParticipantRole.Operator);var observer=new ParticipantContext("viewer",ParticipantRole.Observer);host.RegisterParticipant(op);host.RegisterParticipant(observer);var q=BatchQuery(w);
        var denied=host.Enqueue(observer,new("denied",1,new EditCornerCommand(q)));var accepted=host.Enqueue(op,new("operator",1,new EditCornerCommand(q)));var stale=host.Enqueue(new("owner"),new("stale",1,new EditCornerCommand(q)));host.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await denied).Code);Assert.IsType<AcceptedMessage>(await accepted);Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await stale).Code);
        var current=q with{ExpectedGeometryRevision=1};var before=host.Snapshot();Assert.False(host.PreviewCorner(current).Legal);
        var noChange=host.Enqueue(op,new("none",1,new EditCornerCommand(current)));host.Pump(0);Assert.Equal("invalidPlacement",Assert.IsType<RejectedMessage>(await noChange).Code);Assert.Equal(before,host.Snapshot());
    }

    [Theory][InlineData(TerrainSurface.Natural)][InlineData(TerrainSurface.Cut)]
    public async Task Bare_ground_terrace_needs_no_stroke_and_restores_flat_corners_after_save(TerrainSurface surface)
    {
        var w=TerrainWorld.Empty(new(WorldCreationProfile.Id,"Direct terrace",new(16,24,6),1),TerrainProfiles.Bundled);
        if(surface==TerrainSurface.Cut)w=new(w.Profile,w.Name,new(16,24,6),1,Enumerable.Repeat(new TerrainCell(1,surface),16*24),w.Spatial.Capture(),[]);
        using var host=Host(w);var before=host.Snapshot();var q=Query(w,"placeTerrace");var preview=host.PreviewCorner(q);
        var change=Assert.Single(preview.Changes);Assert.True(preview.Legal);Assert.Equal(1,change.BeforeGround);Assert.Equal(2,change.AfterGround);
        Assert.Equal((0,0,0,0),(change.BeforeNE,change.BeforeSE,change.BeforeSW,change.BeforeNW));Assert.Equal(before,host.Snapshot());
        var command=host.Enqueue(new("owner"),new("direct",1,new EditCornerCommand(q)));host.Pump(0);Assert.IsType<AcceptedMessage>(await command);
        var capture=host.CaptureSave();host.Pump(0);var bytes=await capture;var state=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        Assert.Equal(bytes,SaveCodec.Write(state));Assert.Equal(1,state.World.OccupantAt(P)!.EntityId);Assert.True(state.World.OccupantAt(P)!.Corners.Flat);
        Assert.Equal(TerrainOccupantKind.Terrace,state.World.OccupantAt(P)!.Kind);Assert.Equal(2,state.World.Spatial.NextEntityId);
        Assert.Equal("occupied",TerraceTerrain.Preview(state.World,P with{Z=2},"placeTerrace").Reason);
        var restored=Edit(state.World,"removeTerrace");Assert.Equal(1,restored.Cell(restored.Space.ToStorage(P).H,restored.Space.ToStorage(P).V).Ground);
        Assert.True(restored.OccupantAt(P)!.Corners.Flat);Assert.Equal(TerrainOccupantKind.Mountain,restored.OccupantAt(P)!.Kind);
        Assert.Equal(before.Tick,host.Snapshot().Tick);Assert.Equal(before.Paused,host.Snapshot().Paused);Assert.Equal(before.Cash,host.Snapshot().Cash);
    }
    [Fact]
    public void Bare_ground_rectangle_admits_64_cells_but_roof_occupancy_and_allocator_still_apply()
    {
        var point=new Location(20,20,1);
        var w=TerrainWorld.Empty(new(WorldCreationProfile.Id,"Bare batch",new(30,42,6),1),TerrainProfiles.Bundled);
        var draft=new CornerTerrainDraft(w);var plans=TerraceTerrain.PlaceRectangle(draft,new(20,20,1),new(27,27,1));
        Assert.Equal(64,plans.Length);Assert.All(plans,p=>Assert.True(p.Legal));var placed=draft.Finish();Assert.Equal(64,placed.Occupants.Length);Assert.Equal(65,placed.Spatial.NextEntityId);
        Assert.All(placed.Occupants,o=>{Assert.True(o.Corners.Flat);Assert.Equal(TerrainOccupantKind.Terrace,o.Kind);});
        Assert.Equal("roof",TerraceTerrain.Preview(TerrainWorld.Empty(new(WorldCreationProfile.Id,"Roof",new(30,42,6),5),w.Profile),point with{Z=5},"placeTerrace").Reason);
        var exhausted=new TerrainWorld(w.Profile,w.Name,new(30,42,6),1,w.Cells,new(long.MaxValue,[]),[]);
        Assert.Equal("capacityLimit",TerraceTerrain.Preview(exhausted,point,"placeTerrace").Reason);
        var creek=w.Profile.Definitions.Single(d=>d.Kind==TerrainOccupantKind.Creek).Reference;
        var occupied=new TerrainWorld(w.Profile,w.Name,new(30,42,6),1,w.Cells,new(2,[new(1,0,creek,point,new([new(new(0,0,0),true)]),false,true,0)]),[new(1,TerrainOccupantKind.Creek,default)]);
        Assert.Equal("occupied",TerraceTerrain.Preview(occupied,point,"placeTerrace").Reason);Assert.Equal("mountainRequired",TerraceTerrain.Preview(w,point,"placeCut").Reason);
    }

}
