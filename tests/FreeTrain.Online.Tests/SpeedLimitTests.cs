using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
namespace FreeTrain.Online.Tests;
public class SpeedLimitTests
{
    static Location P=>GarageConstructionTests.Start;
    internal static MixedWorld Track()
    {
        var w=GarageConstructionTests.World();
        return NormalRailwayConstruction.Apply(new(w),NormalRailwayConstruction.PreviewBuild(w,P,P with{X=P.X+5}),new(0)).World;
    }
    static MixedWorld Apply(MixedWorld w,RailwayConstructionPlan plan)=>SpeedLimitConstruction.Apply(new(w),plan,new(0)).World;
    static RailwayConstructionPlan Quote(MixedWorld w,string speed="low",bool removal=false)=>SpeedLimitConstruction.Preview(w,P,P with{X=P.X+2},speed,removal);
    [Fact]
    public void Inclusive_flat_ranges_reject_all_overlaps_and_remove_without_geometry_or_booking_changes()
    {
        foreach(var speed in new[]{"low","medium","fast"}){
            var w=Track();var quote=Quote(w,speed);Assert.True(quote.Legal,quote.Reason);Assert.Equal(3,quote.Topology.Cells.Length);Assert.Empty(quote.Booking.Charges);
            Assert.Empty(w.SpeedLimits.Locations);var state=SpeedLimitConstruction.Apply(new(w),quote,new(0));var built=state.World;
            Assert.True(w.Voxels.SequenceEqual(built.Voxels));Assert.True(w.Spatial.Entities.SequenceEqual(built.Spatial.Entities));Assert.Equal(w.Spatial.NextEntityId,built.Spatial.NextEntityId);Assert.Same(w.Terrain,built.Terrain);Assert.Empty(state.Accounts.Transactions);
            Assert.Equal(new[]{P,P with{X=P.X+1},P with{X=P.X+2}},built.SpeedLimits.Locations);
            foreach(var other in new[]{"low","medium","fast"})Assert.Equal("speedLimitOverlap",Quote(built,other).Reason);
            var removed=Apply(built,Quote(built,removal:true));Assert.Empty(removed.SpeedLimits.Locations);
            Assert.True(Quote(removed,"fast").Legal);Assert.True(Quote(removed,removal:true).Legal);
            Assert.Throws<InvalidOperationException>(()=>SpeedLimitConstruction.Apply(new(built),quote,new(0)));
            Assert.Throws<InvalidOperationException>(()=>Apply(w,PoleRailwayConstruction.Preview(w,P,PoleRailwayConstruction.Double1)));
        }
    }
    [Fact]
    public void List_overloads_deduplicate_only_flat_and_remove_one_occurrence_in_each_list()
    {
        var limits=SpeedLimits.Empty.Add("low",[P,P],false).Add("medium",[P,P],true).Add("fast",[P,P],false);
        Assert.Equal(2,limits.Low.Length);Assert.Single(limits.Medium);Assert.Equal(2,limits.Fast.Length);
        var removed=limits.Remove([P]);Assert.Single(removed.Low);Assert.Empty(removed.Medium);Assert.Single(removed.Fast);
        Assert.Empty(removed.Remove([P]).Locations);Assert.Empty(limits.Clean(new HashSet<Location>()).Locations);
        Assert.True(limits.Locations.SequenceEqual(limits.Clean(new HashSet<Location>{P}).Locations));
    }
    [Fact]
    public void Only_Traffic_existence_matters_not_track_pattern_or_subtype()
    {
        var original=Track();var cells=original.Voxels;
        var road=new DefinitionReference(original.Profile.Terrain.Packs[0].Id,"test:road","test:road");
        var profile=new RailwayProfile(original.Profile.Terrain,original.Profile.Bindings.Add(new(road,RailwayHandler.Road)));
        foreach(var kind in new[]{"road","accessory","curve","junction"}){
            var w=new MixedWorld(profile,original.Terrain,original.Spatial.Capture(),cells.Select(v=>v with{Traffic=kind switch{
                "road"=>new(Road:new(road,17)),"accessory"=>new(Accessory:new(profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference)),
                _=>new(new(profile.Bindings.Single(b=>b.Handler==(kind=="curve"?RailwayHandler.Normal:RailwayHandler.Junction)&&b.Variant==(kind=="curve"?0:12)).Reference,kind=="curve"?0:12))}}),[],original.Revision);
            Assert.True(Quote(w).Legal,kind);Assert.True(Apply(w,Quote(w)).SpeedLimits.Contains(P));
        }
        Assert.Equal("speedLimitTraffic",SpeedLimitConstruction.Preview(original,P,P with{X=P.X+6},"low").Reason);
    }
    [Fact]
    public void Cleanup_retains_accessory_Traffic_then_last_component_removal_clears_membership()
    {
        var w=Track();w=PoleRailwayConstruction.Apply(new(w),PoleRailwayConstruction.Preview(w,P,PoleRailwayConstruction.Double1),new(0)).World;
        w=Apply(w,Quote(w));var removal=NormalRailwayConstruction.PreviewRemoval(w,P,P with{X=P.X+5});Assert.True(removal.Legal,removal.Reason);
        w=NormalRailwayConstruction.Apply(new(w),removal,new(0)).World;
        Assert.Equal(P,Assert.Single(w.SpeedLimits.Low));Assert.Null(w.At(P)!.Traffic!.Rail);
        w=PoleRailwayConstruction.Apply(new(w),PoleRailwayConstruction.Preview(w,P,PoleRailwayConstruction.Double1,true),new(0)).World;
        Assert.Empty(w.SpeedLimits.Locations);
    }
    [Fact]
    public void Four_slope_positions_all_directions_member_removal_and_infrastructure_cleanup()
    {
        foreach(var d in new[]{0,2,4,6}){
            var empty=GarageConstructionTests.World();var slope=SlopeRailwayConstruction.PreviewBuild(empty,P,new(d));Assert.True(slope.Legal,slope.Reason);
            var w=SlopeRailwayConstruction.Apply(new(empty),slope,new(0)).World;
            var quote=SpeedLimitConstruction.Preview(w,P,P,"medium",slope:true,climb:d);Assert.True(quote.Legal,quote.Reason);
            var (dx,dy)=new Heading(d).Offset;var expected=Enumerable.Range(0,4).Select(i=>new Location(P.X+i*dx,P.Y+i*dy,P.Z+i/2)).ToArray();
            Assert.Equal(expected,quote.Topology.Cells.Select(c=>c.Position));w=Apply(w,quote);Assert.Equal(expected,w.SpeedLimits.Medium);
            foreach(var member in w.Voxels){
                var remove=SpeedLimitConstruction.Preview(w,member.Position,member.Position,"low",true,true,d);Assert.True(remove.Legal,remove.Reason);
                Assert.Empty(Apply(w,remove).SpeedLimits.Locations);
                Assert.Equal("slopeDirectionMismatch",SpeedLimitConstruction.Preview(w,member.Position,member.Position,"low",true,true,(d+2)%8).Reason);
            }
            var demolish=SlopeRailwayConstruction.PreviewRemoval(w,P,new(d));Assert.True(demolish.Legal,demolish.Reason);
            Assert.Empty(SlopeRailwayConstruction.Apply(new(w),demolish,new(0)).World.SpeedLimits.Locations);
        }
    }
    [Fact]
    public void Slope_placement_requires_four_Traffic_cells_but_no_actual_slope_entity()
    {
        var original=Track();var members=original.Voxels.Take(4).Select((v,i)=>v with{Position=v.Position with{Z=P.Z+i/2}}).ToImmutableArray();
        var ids=members.Select(v=>v.OwnerId).ToHashSet();
        var entities=original.Spatial.Entities.Where(e=>ids.Contains(e.Id)).Select(e=>e with{Anchor=members.Single(v=>v.OwnerId==e.Id).Position}).ToImmutableArray();
        var w=new MixedWorld(original.Profile,original.Terrain,new(original.Spatial.NextEntityId,entities),members,[],original.Revision);
        var q=SpeedLimitConstruction.Preview(w,P,P,"fast",slope:true,climb:2);Assert.True(q.Legal,q.Reason);w=Apply(w,q);
        Assert.Empty(w.Slopes);Assert.Equal(4,w.SpeedLimits.Fast.Length);
        Assert.Equal("notSlope",SpeedLimitConstruction.Preview(w,P,P,"low",true,true,2).Reason);
        // A level range can still remove these entries one elevation at a time.
        w=Apply(w,SpeedLimitConstruction.Preview(w,P,P with{X=P.X+1},"low",true));
        Assert.Equal(2,w.SpeedLimits.Fast.Length);
    }
    [Fact]
    public void Input_bounds_reject_without_partial_state_and_empty_range_is_empty()
    {
        var w=Track();var same=SpeedLimitConstruction.Preview(w,P,P,"low");Assert.Equal("samePoint",same.Reason);Assert.Empty(same.Topology.Cells);
        Assert.Equal("invalidInput",Quote(w,"warp").Reason);
        Assert.Equal("noncoplanar",SpeedLimitConstruction.Preview(w,P,P with{Z=3},"low").Reason);
        Assert.Equal("routeWorkLimit",SpeedLimitConstruction.Preview(w,P,new(int.MaxValue,int.MinValue,2),"low").Reason);
        Assert.Equal("outside",SpeedLimitConstruction.Preview(w,new(-100,0,2),P,"low").Reason);
        Assert.Equal("invalidInput",SpeedLimitConstruction.Preview(w,P,P,"low",slope:true,climb:1).Reason);
        Assert.Empty(w.SpeedLimits.Locations);
        Assert.Throws<ArgumentException>(()=>new MixedWorld(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,new([P with{Z=7}],[],[])));
    }
    [Fact]
    public void Literal_flat_walks_cover_eight_bearings_bends_and_existing_curves()
    {
        int[][][] paths=[[[0,0],[0,-1],[0,-2]],[[0,0],[1,-1],[2,-2]],[[0,0],[1,0],[2,0]],
            [[0,0],[1,1],[2,2]],[[0,0],[0,1],[0,2]],[[0,0],[-1,1],[-2,2]],
            [[0,0],[-1,0],[-2,0]],[[0,0],[-1,-1],[-2,-2]],
            [[0,0],[1,1],[2,1],[3,1]],[[0,0],[-1,1],[-1,2],[-1,3]]];
        foreach(var offsets in paths){
            var empty=GarageConstructionTests.World();var points=offsets.Select(p=>new Location(P.X+p[0],P.Y+p[1],2)).ToArray();
            var rail=empty.Profile.Bindings.First(b=>b.Handler==RailwayHandler.Normal&&b.Variant==0).Reference;
            var entities=points.Select((p,i)=>new SpatialEntity(i+1,0,empty.Profile.TrafficOwner,p,new([new(new(0,0,0),true)]),true,false,0)).ToImmutableArray();
            var voxels=points.Select((p,i)=>new RailwayVoxel(p,i+1,RailwayVoxelKind.Traffic,new(new(rail,0))));
            var w=new MixedWorld(empty.Profile,empty.Terrain,new(points.Length+1,entities),voxels,[]);
            var q=SpeedLimitConstruction.Preview(w,points[0],points[^1],"low");Assert.True(q.Legal,q.Reason);Assert.Equal(points,q.Topology.Cells.Select(c=>c.Position));
            Assert.Equal(points,Apply(w,q).SpeedLimits.Low);
        }
    }
    [Fact]
    public void Exact_save_world_isolation_duplicates_and_full_journal_zero_booking()
    {
        var w=Track();var limits=new SpeedLimits([P,P],[P with{X=P.X+1}],[P with{X=P.X+2}]);
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,limits);
        var r=new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0);
        var accounts=new ConstructionAccounts().Book(new ConstructionBooking(Enumerable.Repeat(new AccountCharge(AccountGenre.RailService,0),8192)),w.Revision,new(0),"fixture");
        var state=new MixedSessionState(new(w,accounts),TerrainProvenance.From(r),revision:w.Revision);
        var bytes=SaveCodec.Write(state);var reopened=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
        Assert.Equal(bytes,SaveCodec.Write(reopened));Assert.True(limits.Low.SequenceEqual(reopened.World.SpeedLimits.Low));Assert.Empty(Track().SpeedLimits.Locations);
        var removed=SpeedLimitConstruction.Apply(reopened.Construction,Quote(reopened.World,removal:true),new(0));
        Assert.Equal(P,Assert.Single(removed.World.SpeedLimits.Locations));Assert.Equal(8192,removed.Accounts.Transactions.Length);
        var invalid=MixedWire.Read(reopened.Construction) with{SpeedLimits=new([new(-100,0,2)],[],[])};
        Assert.Throws<ArgumentException>(()=>MixedWire.Restore(invalid,w.Profile,w.Terrain,MixedWire.RailwayEntities(w)));
    }
}
