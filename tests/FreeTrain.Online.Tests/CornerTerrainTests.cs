using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;

public class CornerTerrainTests
{
    private static EmptyWorldRequest Request(int h=5,int v=8,int z=4,int water=1) => new(WorldCreationProfile.Id,"Corner",new(h,v,z),water);
    private static TerrainWorld Empty(int ground=1,int water=1,int h=5,int v=8,int z=4) =>
        new(TerrainProfiles.Bundled,"Corner",new(h,v,z),water,Enumerable.Repeat(new TerrainCell(ground),h*v),new(1,[]),[]);
    private static int worldIndex(TerrainWorld w,Location p){var s=w.Space.ToStorage(p);return s.V*w.Space.HorizontalSize+s.H;}
    private static readonly Location P = new(3,3,1);
    private static TerrainWorld Edit(TerrainWorld world, Location p, string action) => CornerTerrain.Apply(world,CornerTerrain.Preview(world,p,action));
    private static SessionHost Host(TerrainWorld world) => new(new TerrainSessionState(world,TerrainProvenance.From(Request()),revision:world.Revision),"owner",TerrainProfiles.Resolve);
    private static CornerTerrainQuery Query(TerrainWorld w,Location? p=null,string action="raise",long epoch=1) =>
        new(epoch,w.Revision,new((p??P).X,(p??P).Y,(p??P).Z),action);
    [Fact]
    public void Preview_is_read_only_and_raise_updates_literal_four_corners_without_random_or_booking()
    {
        var world=Empty();var preview=CornerTerrain.Preview(world,P,"raise");
        Assert.True(preview.Legal);Assert.Empty(world.Occupants);Assert.Equal(0,world.Revision);
        Assert.Equal(new[]{new Location(3,2,1),P,new(4,2,1),new(4,3,1)},preview.Changes.Select(c=>c.Position));
        Assert.Equal(new[]{0x0100,0x1000,0x0010,0x0001},preview.Changes.Select(c=>c.After.Packed));
        var after=CornerTerrain.Apply(world,preview);
        Assert.Equal(4,after.Occupants.Length);Assert.Equal(5,after.Spatial.NextEntityId);
        Assert.Equal(5,after.FineHeight(P,0));Assert.Equal(1,after.Revision);
        Assert.All(after.Spatial.Entities,e=>{Assert.False(e.Owned);Assert.Equal(0,e.Value);});
        Assert.All(after.Cells,c=>Assert.Equal(1,c.Ground));
    }
    [Fact]
    public void Four_quarter_steps_saturate_and_fifth_is_rejected_until_other_corners_change()
    {
        var world=Empty();for(int i=0;i<4;i++)world=Edit(world,P,"raise");
        Assert.Equal(8,world.FineHeight(P,0));Assert.Equal("cornerLimit",CornerTerrain.Preview(world,P,"raise").Reason);
        for(int i=0;i<4;i++)world=Edit(world,P,"lower");
        Assert.Empty(world.Occupants);Assert.Empty(world.Spatial.Entities);Assert.Equal(5,world.Spatial.NextEntityId);
    }
    [Fact]
    public void Lower_flat_ground_creates_literal_three_high_corners_and_raising_reverses_it()
    {
        var original=Empty();var world=Edit(original,P,"lower");
        Assert.Equal(new[]{0x4344,0x3444,0x4434,0x4443},world.Occupants.Select(o=>o.Corners.Packed));
        Assert.Equal(3,world.FineHeight(P,0));Assert.Equal(4,world.Cells.Count(c=>c.Ground==0));
        var restored=Edit(world,P with{Z=0},"raise");
        Assert.Empty(restored.Occupants);Assert.All(restored.Cells,c=>Assert.Equal(1,c.Ground));
    }
    [Theory]
    [InlineData(0,0,"raise",true)][InlineData(0,0,"lower",false)]
    [InlineData(4,7,"raise",true)][InlineData(4,7,"lower",false)]
    public void Border_raise_skips_outside_and_lower_is_blocked(int h,int v,string action,bool legal)
    {
        var world=Empty();var p=world.Space.ToLocation(new(h,v,1));
        Assert.Equal(legal,CornerTerrain.Preview(world,p,action).Legal);
        if(legal)Assert.NotEmpty(Edit(world,p,action).Occupants);
    }
    [Fact]
    public void Roof_floor_outside_wrong_ground_and_unmatched_cut_fail_without_mutation()
    {
        var floor=Empty(0);Assert.Equal("floor",CornerTerrain.Preview(floor,P with{Z=0},"lower").Reason);
        var roof=Empty(3);for(int i=0;i<3;i++)roof=Edit(roof,P with{Z=3},"raise");
        Assert.Equal("roof",CornerTerrain.Preview(roof,P with{Z=3},"raise").Reason);
        Assert.Equal("outside",CornerTerrain.Preview(roof,new(999,999,1),"raise").Reason);
        Assert.Equal("groundLevel",CornerTerrain.Preview(Empty(),P with{Z=2},"raise").Reason);
        var cells=Empty().Cells.ToBuilder();cells[worldIndex(Empty(),P)]=new(2,TerrainSurface.Cut);
        var cut=new TerrainWorld(TerrainProfiles.Bundled,"Corner",new(5,8,4),1,cells,new(1,[]),[]);
        Assert.Equal("unmatchedCorners",CornerTerrain.Preview(cut,P with{Z=2},"raise").Reason);
    }
    [Fact]
    public void Water_does_not_block_corner_changes_or_move_the_global_plane()
    {
        var world=Empty(1,3);var raised=Edit(world,P,"raise");var lower=Edit(world,P,"lower");
        Assert.Equal(3,raised.WaterLevel);Assert.Equal(3,lower.WaterLevel);
        Assert.True(raised.At(P).Underwater);
    }
    [Fact]
    public void Creek_is_not_reclaimed_by_raise_or_lower()
    {
        var w=Empty();var d=w.Profile.Definitions.First(d=>d.Kind==TerrainOccupantKind.Creek);
        var e=new SpatialEntity(1,0,d.Reference,P,new Footprint([new(new(0,0,0),true)]),false,true,0);
        w=new(w.Profile,w.Name,new(5,8,4),1,w.Cells,new(2,[e]),[new(1,TerrainOccupantKind.Creek,default)]);
        Assert.Equal("occupied",CornerTerrain.Preview(w,P,"raise").Reason);
        Assert.Equal("occupied",CornerTerrain.Preview(w,P,"lower").Reason);
        Assert.Single(w.Occupants);
    }
    [Fact]
    public void Existing_forest_identity_value_and_ownership_survive_geometry_then_are_reported_on_flattening()
    {
        var w=Edit(Empty(),P,"raise");var entity=w.Spatial.At(P).Entity!;var forest=w.Profile.Definitions.First(d=>d.TreeSpriteCount>0);
        var e=entity with{Owned=true,Value=forest.Price,Revision=1};
        var o=w.OccupantAt(P)! with{Forest=new(forest.Reference,[new(0,0,0)])};
        w=w.Replace(w.Revision,w.Cells,w.WaterLevel,new(w.Spatial.NextEntityId,w.Spatial.Entities.Select(a=>a.Id==e.Id?e:a).ToImmutableArray()),w.Occupants.Select(a=>a.EntityId==o.EntityId?o:a));
        var raised=Edit(w,P,"raise");Assert.True(raised.Spatial.At(P).Entity!.Owned);Assert.NotNull(raised.OccupantAt(P)!.Forest);
        var plan=CornerTerrain.Preview(w,P,"lower");var loss=plan.Changes.Single(c=>c.Position==P);
        Assert.True(loss.LosesForest);Assert.True(loss.LosesOwnership);Assert.True(loss.Remove);
        Assert.Empty(CornerTerrain.Apply(w,plan).Occupants);
    }
    [Fact]
    public async Task Session_revalidates_fifo_authority_epoch_revision_and_save_continuation()
    {
        var w=Empty();using var host=Host(w);var owner=new ParticipantContext("owner");
        var observer=new ParticipantContext("viewer",ParticipantRole.Observer);var op=new ParticipantContext("builder",ParticipantRole.Operator);
        host.RegisterParticipant(observer);host.RegisterParticipant(op);
        var before=host.Snapshot();var q=Query(w);var preview=host.PreviewCorner(q);
        Assert.True(preview.Legal);Assert.Equal(0,preview.Cost);Assert.Equal(before,host.Snapshot());
        var step=host.Enqueue(owner,new("clock-only",1,new StepCommand()));host.Pump(0);Assert.IsType<AcceptedMessage>(await step);
        // A clock revision cannot invalidate an unchanged terrain geometry fence.
        Task<WireMessage> Send(ParticipantContext who,CornerTerrainQuery query,string id="a",long epoch=1)=>host.Enqueue(who,new(id,epoch,new EditCornerCommand(query)));
        var denied=Send(observer,q);var first=Send(op,q,"first");var stale=Send(owner,q,"stale");host.Pump(0);
        Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await denied).Code);Assert.IsType<AcceptedMessage>(await first);
        Assert.Equal("staleRevision",Assert.IsType<RejectedMessage>(await stale).Code);
        Assert.Equal("1",host.Snapshot().Terrain!.GeometryRevision.ToString());
        var save=host.CaptureSave();host.Pump(0);var bytes=await save;
        var loaded=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        Assert.Equal(5,loaded.World.FineHeight(P,0));Assert.Equal(before.Cash,host.Snapshot().Cash);Assert.Equal(before.Tick+1,loaded.Clock.Time.AbsoluteMinute);
        using var reopened=new SessionHost(loaded,"owner",TerrainProfiles.Resolve);
        var lower=reopened.Enqueue(owner,new("lower",1,new EditCornerCommand(Query(loaded.World,action:"lower"))));reopened.Pump(0);
        Assert.IsType<AcceptedMessage>(await lower);Assert.Empty(reopened.Snapshot().Terrain!.Occupants);
        var disconnected=Send(op,Query(loaded.World),"disconnected");host.DisconnectParticipant(op);host.Pump(0);Assert.Equal("notAuthorized",Assert.IsType<RejectedMessage>(await disconnected).Code);
        var epoch=Send(owner,q,"epoch",2);host.Pump(0);Assert.Equal("staleEpoch",Assert.IsType<RejectedMessage>(await epoch).Code);
    }
    [Fact]
    public void Server_uses_original_selection_formula_with_actual_slope_not_trusted_offsets()
    {
        var w=Edit(Empty(),P,"raise");using var host=Host(w);
        var a=host.PreviewCorner(Query(w) with{OffsetX=7,OffsetY=0});Assert.Equal(new PositionRead(2,3,1),a.Position);
        var b=host.PreviewCorner(Query(w) with{OffsetX=16,OffsetY=6});Assert.Equal(new PositionRead(2,4,1),b.Position);
        Assert.Throws<ArgumentException>(()=>host.PreviewCorner(Query(w) with{OffsetX=32,OffsetY=0}));
        Assert.Throws<ArgumentException>(()=>host.PreviewCorner(Query(w) with{OffsetX=7}));
    }
    [Fact]
    public async Task Ordered_stroke_keeps_local_skips_and_publishes_once_without_reusing_removed_ids()
    {
        var w=Empty();using var host=Host(w);var q=Query(w) with{Stroke=[
            new(new(3,3,1),"lower"), new(new(3,3,0),"lower"), new(new(3,3,1),"raise")]};
        var p=host.PreviewCorner(q);Assert.True(p.Legal);Assert.Single(p.Skipped);Assert.Equal("groundLevel",p.Skipped[0].Reason);
        Assert.Equal(12,p.Changes.Length);Assert.Equal(0,host.Snapshot().Revision);
        var send=host.Enqueue(new("owner"),new("stroke",1,new EditCornerCommand(q)));host.Pump(0);
        Assert.IsType<AcceptedMessage>(await send);var actual=host.Snapshot();
        Assert.Equal(1,actual.Terrain!.GeometryRevision);Assert.Equal(1,actual.Revision);
        Assert.Equal(new long[]{5,6,7,8},actual.Entities!.Select(e=>e.Id));Assert.Equal(9,actual.Terrain.NextEntityId);
        Assert.All(actual.Entities!,e=>Assert.Equal(0,e.Revision));
        var before=actual;
        var invalid=host.Enqueue(new("owner"),new("invalid",1,new EditCornerCommand(q with{ExpectedGeometryRevision=1,
            Stroke=[new(new(3,3,1),"raise",-1,0)]})));
        host.Pump(0);Assert.IsType<RejectedMessage>(await invalid);Assert.Equal(before,host.Snapshot());
    }
    [Fact]
    public async Task Repeated_sites_increment_retained_entity_revision_once_and_bound_stroke_size()
    {
        var w=Edit(Empty(),P,"raise");using var host=Host(w);
        var q=Query(w) with{Stroke=Enumerable.Repeat(new CornerTerrainSite(new(3,3,1),"raise"),63).ToArray()};
        var preview=host.PreviewCorner(q);Assert.Equal(61,preview.Skipped.Length);Assert.Equal(12,preview.Changes.Length);
        var send=host.Enqueue(new("owner"),new("repeat",1,new EditCornerCommand(q)));host.Pump(0);
        Assert.IsType<AcceptedMessage>(await send);Assert.All(host.Snapshot().Entities!,e=>Assert.Equal(1,e.Revision));
        Assert.Throws<ArgumentException>(()=>host.PreviewCorner(q with{ExpectedGeometryRevision=2,Stroke=Enumerable.Repeat(new CornerTerrainSite(new(3,3,1),"raise"),64).ToArray()}));
    }
    [Fact]
    public void Source_outer_ring_checks_reject_distant_cut_mismatch_even_when_edited_corner_matches()
    {
        var w=Empty();var cells=w.Cells.ToBuilder();cells[worldIndex(w,new(2,2,1))]=new(2,TerrainSurface.Cut);
        w=new(w.Profile,w.Name,new(5,8,4),1,cells,new(1,[]),[]);
        Assert.Equal(4,w.FineHeight(P,0));Assert.Equal("unmatchedCorners",CornerTerrain.Preview(w,P,"raise").Reason);
    }

    [Fact]
    public void Identity_and_revision_exhaustion_are_visible_in_read_only_legality_preview()
    {
        var w=Empty();
        var allocator=new TerrainWorld(w.Profile,w.Name,new(5,8,4),1,w.Cells,new(long.MaxValue,[]),[]);
        Assert.Equal("capacityLimit",CornerTerrain.Preview(allocator,P,"raise").Reason);
        var revision=new TerrainWorld(w.Profile,w.Name,new(5,8,4),1,w.Cells,new(1,[]),[],long.MaxValue);
        Assert.Equal("capacityLimit",CornerTerrain.Preview(revision,P,"raise").Reason);
        var mountain=Edit(w,P,"raise");
        var frozen=new TerrainWorld(w.Profile,w.Name,new(5,8,4),1,w.Cells,
            new(mountain.Spatial.NextEntityId,mountain.Spatial.Entities.Select(e=>e with{Revision=long.MaxValue}).ToImmutableArray()),mountain.Occupants);
        Assert.Equal("capacityLimit",CornerTerrain.Preview(frozen,P,"raise").Reason);
        // Removing an exhausted entity needs no new revision or ID.
        Assert.True(CornerTerrain.Preview(frozen,P,"lower").Legal);
        Assert.Empty(Edit(frozen,P,"lower").Occupants);
    }

    [Fact]
    public void Saturation_cannot_detach_existing_terrace_backing()
    {
        var profile=TerrainProfiles.Bundled;
        var terrace=profile.Definitions.First(d=>d.Kind==TerrainOccupantKind.Terrace);
        var mountain=profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId);
        var backing=new SpatialEntity(1,0,terrace.Reference,new(0,0,0),new Footprint([new(new(0,0,0),false)]),false,false,0);
        var peak=new SpatialEntity(2,0,mountain.Reference,new(0,0,1),new Footprint([new(new(0,0,0),false)]),false,false,0);
        var world=new TerrainWorld(profile,"",new(1,1,4),1,[new(1,TerrainSurface.Terrace)],new(3,[backing,peak]),
            [new(1,TerrainOccupantKind.Terrace,default),new(2,TerrainOccupantKind.Mountain,new(3,4,4,4))]);
        Assert.Equal("occupied",CornerTerrain.Preview(world,new(0,0,1),"raise").Reason);
        Assert.Equal(1,world.Cells[0].Ground);Assert.Equal(2,world.Occupants.Length);
    }

}
