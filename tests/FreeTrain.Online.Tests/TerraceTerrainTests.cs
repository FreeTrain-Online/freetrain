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

}
