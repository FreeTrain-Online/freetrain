using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class RectangleTerrainTests
{
    private static readonly Location P=new(10,8,1),End=new(11,9,1);
    private static TerrainWorld Empty(int ground=1,int h=16,int v=24,int z=6)=>new(TerrainProfiles.Bundled,"Rectangle",new(h,v,z),1,
        Enumerable.Repeat(new TerrainCell(ground),h*v),new(1,[]),[]);
    private static (CornerTerrainDraft Draft,CornerTerrainPlan[] Plans) Plan(TerrainWorld w,string action="raise",bool soft=false,Location? start=null,Location? end=null)
    {
        var draft=new CornerTerrainDraft(w);return(draft,RectangleTerrain.Plan(draft,start??P,end??End,action,soft));
    }
    private static TerrainWorld Edit(TerrainWorld w,string action="raise",bool soft=false,Location? start=null,Location? end=null)=>Plan(w,action,soft,start,end).Draft.Finish();
    private static TerrainWorld Cut(TerrainWorld w,Location p,int ground,QuarterCorners? corners=null)
    {
        var cells=w.Cells.ToBuilder();var s=w.Space.ToStorage(p);cells[s.V*w.Space.HorizontalSize+s.H]=new(ground,TerrainSurface.Cut);
        var entities=w.Spatial.Entities;var occupants=w.Occupants;long next=w.Spatial.NextEntityId;
        if(corners is {} offsets){var d=w.Profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId);
            entities=entities.Add(new(next,0,d.Reference,p with{Z=ground},new([new(new(0,0,0),offsets.Flat)]),false,false,0));
            occupants=occupants.Add(new(next++,TerrainOccupantKind.Mountain,offsets));}
        return new(w.Profile,w.Name,new(w.Space.HorizontalSize,w.Space.VerticalSize,w.Space.Levels),w.WaterLevel,cells,new(next,entities),occupants);
    }
    private static SessionHost Host(TerrainWorld w)=>new(new TerrainSessionState(w,TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Name,
        new(w.Space.HorizontalSize,w.Space.VerticalSize,w.Space.Levels),1)),revision:w.Revision),"owner",TerrainProfiles.Resolve);
    private static CornerTerrainQuery Query(TerrainWorld w,string action="raise",Location? start=null,Location? end=null)=>new(1,w.Revision,
        new((start??P).X,(start??P).Y,(start??P).Z),action,Rectangle:new(new((end??End).X,(end??End).Y,(end??End).Z)));
    [Fact]
    public void Raise_targets_four_not_one_with_literal_SE_lookup_and_read_only_preview()
    {
        var w=Empty();var (draft,plans)=Plan(w);var plan=Assert.Single(plans);
        Assert.True(plan.Legal);Assert.Empty(w.Occupants);
        Assert.Equal(new[]{P,new(10,9,1),new(11,8,1),End},plan.Changes.Select(c=>c.Position));
        Assert.Equal(new[]{0x0400,0x4000,0x0040,0x0004},plan.Changes.Select(c=>c.After.Packed));
        var after=draft.Finish();Assert.Equal(8,after.FineHeight(P,1));Assert.Equal(4,after.Occupants.Length);
        Assert.All(after.Cells,c=>Assert.Equal(1,c.Ground));Assert.Equal(5,after.Spatial.NextEntityId);
        Assert.All(after.Spatial.Entities,e=>{Assert.False(e.Owned);Assert.Equal(0,e.Value);});
    }
    [Fact]
    public void Reversed_endpoints_are_identical_and_flat_multi_site_rectangle_saturates_center_once()
    {
        var w=Empty();var a=Edit(w,end:new(12,10,1));var b=Edit(w,start:new(12,10,1),end:P);
        Assert.Equal(a.Cells.ToArray(),b.Cells.ToArray());Assert.Equal(a.Occupants.ToArray(),b.Occupants.ToArray());
        Assert.Equal(8,a.Occupants.Length);Assert.Equal(2,a.Cell(a.Space.ToStorage(End).H,a.Space.ToStorage(End).V).Ground);
        Assert.Null(a.OccupantAt(End));Assert.Equal(1,a.Revision);
    }
    [Theory][InlineData(false,4)][InlineData(true,2)]
    public void Shift_profile_sets_half_height_at_selection_far_edges(bool soft,int offset)
    {
        var plan=Assert.Single(Plan(Empty(),soft:soft).Plans);Assert.Equal(offset,plan.Changes[0].After.SE);
        Assert.Equal(4+offset,Edit(Empty(),soft:soft).FineHeight(P,1));
    }
    [Fact]
    public void Shift_promotes_interior_when_two_corners_are_high_but_far_edge_stays_half()
    {
        var first=Edit(Empty(),soft:true,end:new(13,11,1));
        var plan=Plan(first,soft:true,end:new(13,11,1));
        Assert.Contains(plan.Plans.SelectMany(p=>p.Changes),c=>c.Position==new Location(10,9,1) && c.After.SE==4);
        Assert.Equal(6,plan.Draft.Finish().FineHeight(new(12,10,1),1));
    }
    [Fact]
    public void Excavation_sets_zero_and_preserves_other_three_fours()
    {
        var plan=Assert.Single(Plan(Empty(),"lower").Plans);Assert.True(plan.Legal);
        Assert.Equal(new[]{0x4044,0x0444,0x4404,0x4440},plan.Changes.Select(c=>c.After.Packed));
        Assert.All(plan.Changes,c=>Assert.Equal(0,c.AfterGround));Assert.Equal(0,Edit(Empty(),"lower").FineHeight(P,1));
        Assert.Equal("cornerLimit",Assert.Single(Plan(Edit(Empty()),"lower").Plans).Reason);
    }
    [Theory][InlineData("raise",5,"roof")][InlineData("lower",0,"floor")]
    public void Roof_and_floor_are_skipped_without_writes(string action,int ground,string reason)
    {
        var w=Empty(ground);var plan=Assert.Single(Plan(w,action,start:P with{Z=ground},end:End with{Z=ground}).Plans);
        Assert.Equal(reason,plan.Reason);Assert.Empty(w.Occupants);
    }
    [Theory][InlineData("raise")][InlineData("lower")]
    public void One_dimensional_selection_is_a_noop(string action)=>Assert.Equal("noChange",Assert.Single(Plan(Empty(),action,end:P with{X=P.X+2}).Plans).Reason);
    [Fact]
    public void Repair_includes_single_selected_cell_and_normalizes_saturation_without_modulo_fallback()
    {
        var w=Cut(Empty(2),P,1);var (draft,plans)=Plan(w,"repair",start:P,end:P);
        var repair=Assert.Single(plans);Assert.True(repair.Legal);var c=Assert.Single(repair.Changes);
        Assert.Equal(2,c.AfterGround);Assert.True(c.Remove);Assert.Equal(0,c.After.Packed);
        var after=draft.Finish();Assert.Empty(after.Occupants);Assert.Equal(8,after.FineHeight(P,0));
    }
    [Fact]
    public void Repair_sets_literal_neighbor_quarter_height_and_preserves_retained_forest_identity()
    {
        var w=Edit(Empty(),soft:true);var o=w.OccupantAt(P)!;
        // Explicitly cut cell with one wrong corner. Other cells retain the correct shared SE height 6.
        var entities=w.Spatial.Entities;var cells=w.Cells.ToBuilder();var s=w.Space.ToStorage(P);cells[s.V*w.Space.HorizontalSize+s.H]=new(1,TerrainSurface.Cut);
        var forest=w.Profile.Definitions.First(d=>d.TreeSpriteCount>0);
        w=new(w.Profile,w.Name,new(16,24,6),1,cells,new(w.Spatial.NextEntityId,entities.Select(e=>e.Anchor==P?e with{Owned=true,Value=forest.Price}:e).ToImmutableArray()),
            w.Occupants.Select(a=>a.EntityId==o.EntityId?a with{Corners=new(0,1,0,0),Forest=new(forest.Reference,[new(0,0,0)])}:a));
        var (draft,plans)=Plan(w,"repair",start:P,end:P);var c=Assert.Single(Assert.Single(plans).Changes);
        Assert.Equal(0x0200,c.After.Packed);Assert.False(c.LosesForest);Assert.False(c.LosesOwnership);
        var after=draft.Finish();Assert.Equal(o.EntityId,after.OccupantAt(P)!.EntityId);Assert.NotNull(after.OccupantAt(P)!.Forest);
        Assert.True(after.Spatial.At(P).Entity!.Owned);Assert.Equal(1,after.Spatial.At(P).Entity!.Revision);
    }
    [Fact]
    public void Unrepresentable_neighbor_reference_is_skipped_and_never_written_as_modulo_four()
    {
        var w=Cut(Empty(4),P,2);var p=Assert.Single(Plan(w,"repair",start:P,end:P).Plans);
        Assert.Equal("unrepresentableHeight",p.Reason);Assert.Empty(p.Changes);Assert.Equal(8,w.FineHeight(P,0));
    }
    [Fact]
    public void Inconsistent_neighbors_skip_the_whole_cell_without_partial_corner_writes()
    {
        var w=Cut(Empty(),P,0);w=Cut(w,P.Move(Heading.NorthEast),2);
        var p=Assert.Single(Plan(w,"repair",start:P,end:P).Plans);Assert.Equal("inconsistentNeighbors",p.Reason);Assert.Empty(p.Changes);
    }
    [Fact]
    public void Repair_unchanged_border_is_noop_and_raise_border_handles_bounded_neighbors()
    {
        var w=Empty();var p=w.Space.ToLocation(new(0,0,1));
        Assert.Equal("noChange",Assert.Single(Plan(w,"repair",start:p,end:p).Plans).Reason);
        Assert.Throws<ArgumentException>(()=>Plan(w,start:p,end:p with{X=p.X-1,Y=p.Y-1}));
    }
    [Fact]
    public void All_rectangle_targets_are_validated_before_any_work_and_input_modes_are_exclusive()
    {
        var w=Empty();using var host=Host(w);
        Assert.Throws<ArgumentException>(()=>Plan(w,end:End with{Z=2}));
        Assert.Throws<ArgumentException>(()=>Plan(w,end:new(18,16,1)));
        Assert.Throws<ArgumentException>(()=>host.PreviewCorner(Query(w) with{OffsetX=1,OffsetY=0}));
        Assert.Throws<ArgumentException>(()=>host.PreviewCorner(Query(w) with{Stroke=[new(new(10,8,1),"raise")]}));
        Assert.Throws<ArgumentException>(()=>Plan(w,"unknown"));Assert.Empty(w.Occupants);
    }
    [Fact]
    public void Obstructed_sites_are_skipped_but_other_sites_publish_together_in_source_order()
    {
        var w=Empty();var d=w.Profile.Definitions.First(d=>d.Kind==TerrainOccupantKind.Creek);
        var e=new SpatialEntity(1,0,d.Reference,P,new([new(new(0,0,0),true)]),false,true,0);
        w=new(w.Profile,w.Name,new(16,24,6),1,w.Cells,new(2,[e]),[new(1,TerrainOccupantKind.Creek,default)]);
        var (draft,plans)=Plan(w,end:new(12,10,1));Assert.Equal("occupied",plans[0].Reason);
        Assert.Contains(plans,p=>p.Legal);var after=draft.Finish();Assert.Equal(1,after.Revision);Assert.Equal(e,after.Spatial.At(P).Entity);
        Assert.Equal(new[]{P,new(10,9,1),new(11,8,1),End},plans.Select(p=>p.Position));
    }
    [Fact]
    public async Task Rectangle_uses_existing_fifo_roles_conflict_fences_clock_independence_and_exact_Save7_continuation()
    {
        var w=Empty();using var host=Host(w);var q=Query(w);var owner=new ParticipantContext("owner");var op=new ParticipantContext("op",ParticipantRole.Operator);var observer=new ParticipantContext("viewer",ParticipantRole.Observer);
        host.RegisterParticipant(op);host.RegisterParticipant(observer);var before=host.Snapshot();
        Assert.True(host.PreviewCorner(q).Legal);Assert.Equal(before,host.Snapshot());
        var step=host.Enqueue(owner,new("step",1,new StepCommand()));host.Pump(0);Assert.IsType<AcceptedMessage>(await step);
        var deny=host.Enqueue(observer,new("deny",1,new EditCornerCommand(q)));
        var commit=host.Enqueue(op,new("rect",1,new EditCornerCommand(q)));
        var conflict=host.Enqueue(owner,new("conflict",1,new EditCornerCommand(q)));host.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await deny).Code);Assert.IsType<AcceptedMessage>(await commit);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await conflict).Code);
        var save=host.CaptureSave();host.Pump(0);var bytes=await save;var state=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        Assert.Equal(bytes,SaveCodec.Write(state));Assert.Equal(8,state.World.FineHeight(P,1));Assert.Equal(before.Cash,host.Snapshot().Cash);
        using var reopened=new SessionHost(state,"owner",TerrainProfiles.Resolve);
        var repair=reopened.PreviewCorner(Query(state.World,"repair",P,P));Assert.False(repair.Legal);Assert.Equal("noChange",repair.Reason);
        var disconnected=host.Enqueue(op,new("gone",1,new EditCornerCommand(q with{ExpectedGeometryRevision=1})));host.DisconnectParticipant(op);host.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await disconnected).Code);
        var epoch=host.Enqueue(owner,new("epoch",1,new EditCornerCommand(q with{ExpectedWorldEpoch=2})));host.Pump(0);Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await epoch).Code);
    }
    [Fact]
    public void Capacity_limit_is_reported_before_applying_any_site()
    {
        var w=Empty();w=new(w.Profile,w.Name,new(16,24,6),1,w.Cells,new(long.MaxValue,[]),[]);
        var p=Assert.Single(Plan(w).Plans);Assert.Equal("capacityLimit",p.Reason);Assert.Empty(p.Changes);
    }
    [Fact]
    public void Mountain2_outer_ring_rejects_mismatch_even_when_the_edited_SE_vertex_matches()
    {
        var w=Cut(Empty(),new(P.X-1,P.Y,1),2);
        Assert.Equal(4,w.FineHeight(P,1));
        Assert.Equal("unmatchedCorners",Assert.Single(Plan(w).Plans).Reason);
    }
    [Fact]
    public void Repair_obstruction_skips_a_whole_cell_without_reclaiming_a_creek()
    {
        var w=Cut(Empty(),P,0);var d=w.Profile.Definitions.First(d=>d.Kind==TerrainOccupantKind.Creek);var p=P with{Z=0};
        var e=new SpatialEntity(1,0,d.Reference,p,new([new(new(0,0,0),true)]),false,true,0);
        w=new(w.Profile,w.Name,new(16,24,6),1,w.Cells,new(2,[e]),[new(1,TerrainOccupantKind.Creek,default)]);
        var plan=Assert.Single(Plan(w,"repair",start:P,end:P).Plans);
        Assert.Equal("occupied",plan.Reason);Assert.Empty(plan.Changes);Assert.Equal(e,w.Spatial.At(p).Entity);
    }
    [Fact]
    public void Saturating_repair_reports_forest_and_ownership_loss_and_keeps_allocator_monotonic()
    {
        var w=Cut(Empty(2),P,1,new(4,4,4,1));var forest=w.Profile.Definitions.First(d=>d.TreeSpriteCount>0);
        w=new(w.Profile,w.Name,new(16,24,6),1,w.Cells,
            new(2,w.Spatial.Entities.Select(e=>e with{Owned=true,Value=forest.Price}).ToImmutableArray()),
            w.Occupants.Select(o=>o with{Forest=new(forest.Reference,[new(0,0,0)])}));
        var (draft,plans)=Plan(w,"repair",start:P,end:P);var c=Assert.Single(Assert.Single(plans).Changes);
        Assert.True(c.LosesForest);Assert.True(c.LosesOwnership);Assert.True(c.Remove);
        var after=draft.Finish();Assert.Empty(after.Occupants);Assert.Equal(2,after.Spatial.NextEntityId);
    }
    [Fact]
    public void Submerged_rectangles_leave_water_and_source_zero_booking_metadata_unchanged()
    {
        var w=Empty();w=new(w.Profile,w.Name,new(16,24,6),3,w.Cells,new(1,[]),[]);
        var raised=Edit(w);var lower=Edit(w,"lower");
        Assert.Equal(3,raised.WaterLevel);Assert.Equal(3,lower.WaterLevel);
        Assert.All(raised.Spatial.Entities,e=>{Assert.False(e.Owned);Assert.Equal(0,e.Value);});
        Assert.All(lower.Spatial.Entities,e=>{Assert.False(e.Owned);Assert.Equal(0,e.Value);});
    }
    [Fact]
    public void Skew_grid_rectangle_with_outside_intermediate_cell_is_rejected_before_iteration()
    {
        var w=Empty();var a=w.Space.ToLocation(new(0,0,1));var b=w.Space.ToLocation(new(0,3,1));
        Assert.True(w.Space.ContainsVoxel(a));Assert.True(w.Space.ContainsVoxel(b));
        Assert.Throws<ArgumentException>(()=>Plan(w,start:a,end:b));Assert.Empty(w.Occupants);
    }

}
