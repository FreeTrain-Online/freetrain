using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

// Trusted canonical interface fixtures. These bindings never enter the bundled
// gameplay profile and grant no P5 train, P6 platform or P7 road runtime credit.
public class CrossToolDependencyTests
{
    internal static Location P => GarageConstructionTests.Start;
    static Location End => P with { X=P.X+2 };
    internal static DefinitionReference Ref(string id) => new(RailwayProfiles.Bundled.Terrain.Packs[0].Id,"fixture:"+id,"fixture:"+id);
    internal static RailwayProfile Profile => new(RailwayProfiles.Bundled.Terrain,RailwayProfiles.Bundled.Bindings.AddRange(new RailwayBinding[]{
        new(Ref("road"),RailwayHandler.Road),new(Ref("car"),RailwayHandler.Car),
        new(Ref("platform"),RailwayHandler.Platform),new(Ref("crossing"),RailwayHandler.Crossing)}));
    internal static MixedWorld Decorate(MixedWorld w,Func<RailwayVoxel,TrafficComponents?> traffic) =>
        new(Profile,w.Terrain,w.Spatial.Capture(),w.Voxels.Select(v=>v.Traffic==null?v:v with {Traffic=traffic(v)}),w.Slopes,w.Revision,w.SpeedLimits);
    internal static MixedWorld Rail(RailwayHandler handler, bool road=false, bool car=false, bool platform=false)
    {
        var w=GarageConstructionTests.World();
        if(handler==RailwayHandler.Tunnel)
        {
            w=TunnelConstructionTests.World([P]);
            w=TunnelRailwayConstruction.Apply(new(w),TunnelRailwayConstruction.Preview(w,P,End),new(0)).World;
        }
        else
        {
            var pattern=handler==RailwayHandler.Junction?14:4;
            var binding=w.Profile.Bindings.First(b=>b.Handler==handler && (handler is not (RailwayHandler.Normal or RailwayHandler.Junction) || b.Variant==pattern));
            var owner=new SpatialEntity(1,0,w.Profile.TrafficOwner,P,new([new(new(0,0,0),true)]),true,false,0);
            w=new(w.Profile,w.Terrain,new(2,[owner]),[new(P,1,RailwayVoxelKind.Traffic,new(new(binding.Reference,pattern,Direction:handler==RailwayHandler.Signal?2:0)))],[]);
        }
        return Decorate(w,v=>v.Traffic! with {
            Road=road?new(Ref("road"),17):null,
            Car=car&&v.Position==P?new(Ref("car"),11,12):null,
            Platform=platform&&v.Position==P?Ref("platform"):null });
    }
    static MixedWorld Normal(MixedWorld w,RailwayConstructionPlan p)=>NormalRailwayConstruction.Apply(new(w),p,new(0)).World;
    static MixedWorld Signal(MixedWorld w,int direction,bool remove=false)=>SignalRailwayConstruction.Apply(new(w),SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,direction,remove),new(0)).World;
    static MixedWorld Pole(MixedWorld w,bool remove=false)=>PoleRailwayConstruction.Apply(new(w),PoleRailwayConstruction.Preview(w,P,PoleRailwayConstruction.Double1,remove),new(0)).World;
    internal static byte[] Save(MixedWorld w)=>SaveCodec.Write(new MixedSessionState(new(w),TerrainProvenance.From(
        new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(40,32,8),0)),revision:w.Revision));

    [Fact]
    public void Crossing_matrix_includes_cardinal_slope_transitions_and_all_road_masks()
    {
        // Literal source pattern axes: flat 4/10, then four positions in each
        // of N/E/S/W twice (surface/underground). No runtime topology oracle.
        int[] northSouth=[10,28,29,30,31,36,37,38,39,44,45,46,47,52,53,54,55];
        int[] eastWest=[4,32,33,34,35,40,41,42,43,48,49,50,51,56,57,58,59];
        var profile=Profile;
        for(var pattern=0;pattern<60;pattern++)for(var mask=1;mask<256;mask++){
            var definition=profile.Bindings.Single(b=>b.Variant==pattern&&b.Handler==(pattern<12?RailwayHandler.Normal:pattern<28?RailwayHandler.Junction:RailwayHandler.Slope)).Reference;
            var input=new TrafficComponents(new(definition,pattern),new(Ref("road"),mask));
            var expected=northSouth.Contains(pattern)&&(mask&68)==68 || eastWest.Contains(pattern)&&(mask&17)==17;
            var actual=TrafficLifecycle.Refresh(profile,input);Assert.Equal(expected,actual.Accessory!=null);
            var pole=new AccessoryComponent(profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference);
            Assert.Equal(pole,TrafficLifecycle.Refresh(profile,input with {Accessory=pole}).Accessory);
        }
    }

    [Fact]
    public void Road_only_normal_build_creates_crossing_then_detach_removes_only_crossing_and_retains_limit()
    {
        var w=Rail(RailwayHandler.Normal,road:true);w=Decorate(w,v=>v.Traffic! with{Rail=null});
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,new([P],[],[]));
        var id=w.At(P)!.OwnerId;var q=NormalRailwayConstruction.PreviewBuild(w,P,End);Assert.True(q.Legal,q.Reason);
        w=Normal(w,q);Assert.Equal(Ref("crossing"),w.At(P)!.Traffic!.Accessory!.Definition);Assert.Equal(id,w.At(P)!.OwnerId);
        q=NormalRailwayConstruction.PreviewRemoval(w,P,End);Assert.Single(q.ComponentLosses);w=Normal(w,q);
        Assert.Null(w.At(P)!.Traffic!.Rail);Assert.Null(w.At(P)!.Traffic!.Accessory);Assert.NotNull(w.At(P)!.Traffic!.Road);Assert.True(w.SpeedLimits.Contains(P));
    }

    [Fact]
    public void Slope_crossings_are_previewed_as_losses_and_all_paired_members_resolve_one_atomic_removal()
    {
        foreach(var d in new[]{0,2,4,6}){
            var empty=GarageConstructionTests.World();var build=SlopeRailwayConstruction.PreviewBuild(empty,P,new(d));
            var w=SlopeRailwayConstruction.Apply(new(empty),build,new(0)).World;
            w=Decorate(w,v=>v.Traffic! with{Road=new(Ref("road"),d%4==0?68:17),Accessory=new(Ref("crossing"))});
            foreach(var member in build.Members){
                var q=SlopeRailwayConstruction.PreviewRemoval(w,member.Position,new(d));Assert.True(q.Legal,q.Reason);Assert.Equal(4,q.ComponentLosses.Length);
                var after=SlopeRailwayConstruction.Apply(new(w),q,new(0)).World;Assert.Empty(after.Slopes);Assert.Equal(4,after.Voxels.Length);
                Assert.All(after.Voxels,v=>{Assert.Null(v.Traffic!.Rail);Assert.Null(v.Traffic.Accessory);Assert.NotNull(v.Traffic.Road);});
            }
        }
    }

    [Fact]
    public void Missing_crossing_handler_rejects_without_partial_geometry_or_charge()
    {
        var w=Rail(RailwayHandler.Normal,road:true);w=Decorate(w,v=>v.Traffic! with{Rail=null});
        w=new(new(w.Profile.Terrain,w.Profile.Bindings.Where(b=>b.Handler!=RailwayHandler.Crossing)),w.Terrain,w.Spatial.Capture(),w.Voxels,[]);
        var before=Save(w);var q=NormalRailwayConstruction.PreviewBuild(w,P,End);
        Assert.Equal("unavailableCrossingBinding",q.Reason);Assert.Empty(q.Booking.Charges);Assert.Equal(before,Save(w));
    }

    [Theory]
    [InlineData(RailwayHandler.Normal)][InlineData(RailwayHandler.Junction)][InlineData(RailwayHandler.Bridge)]
    [InlineData(RailwayHandler.Girder)][InlineData(RailwayHandler.Garage)][InlineData(RailwayHandler.Signal)]
    public void Whole_object_demolition_removes_Traffic_including_road_pole_and_speed_but_protects_dependents(RailwayHandler handler)
    {
        var w=Rail(handler,road:true);
        w=Decorate(w,v=>v.Traffic! with{Accessory=new(Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference)});
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,new([P],[],[]));
        var (after,_,reason)=ObjectRemoval.Mixed(w,P);Assert.Null(reason);Assert.NotNull(after);Assert.Null(after.At(P));Assert.Empty(after.SpeedLimits.Locations);
        var line=ObjectRemoval.RailwayLine(w,[P,End]);Assert.Null(line.Reason);Assert.Null(line.Candidate!.At(P));Assert.Empty(line.Candidate.SpeedLimits.Locations);
        foreach(var dependent in new[]{"car","platform"}){
            var guarded=Rail(handler,car:dependent=="car",platform:dependent=="platform");var before=Save(guarded);
            Assert.Null(ObjectRemoval.Mixed(guarded,P).Candidate);
            var skipped=ObjectRemoval.RailwayLine(guarded,[P,End]);Assert.Null(skipped.Candidate);Assert.Single(skipped.Skipped);Assert.Equal(before,Save(guarded));
        }
    }

    [Fact]
    public void Whole_object_line_keeps_source_order_of_skips_and_removes_other_owners_in_one_result()
    {
        var w=Rail(RailwayHandler.Tunnel);var before=Save(w);
        var result=ObjectRemoval.RailwayLine(w,ObjectRemoval.Line(P,End));Assert.Null(result.Reason);Assert.NotNull(result.Candidate);
        Assert.Equal(P,Assert.Single(result.Skipped).Position);Assert.Equal("operationUnavailable",result.Skipped[0].Reason);
        Assert.Equal(w.At(P),result.Candidate.At(P));Assert.Null(result.Candidate.At(End));Assert.Single(result.Candidate.Terrain.TunnelBackups);
        Assert.Equal(before,Save(w));Assert.Equal("operationUnavailable",ObjectRemoval.Mixed(w,P).Reason);
    }

    [Fact]
    public void Last_pole_cannot_leave_a_protected_car_without_its_canonical_Traffic_host()
    {
        var w=Rail(RailwayHandler.Normal,car:true);
        w=Decorate(w,v=>v.Traffic! with{Rail=null,Accessory=new(Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference)});
        var before=Save(w);var q=PoleRailwayConstruction.Preview(w,P,PoleRailwayConstruction.Double1,true);
        Assert.Equal("carOccupied",q.Reason);Assert.Empty(q.Booking.Charges);Assert.Equal(before,Save(w));
    }

    [Theory]
    [InlineData(0,68,true)][InlineData(2,17,true)][InlineData(4,68,true)][InlineData(6,17,true)]
    [InlineData(0,17,false)][InlineData(2,68,false)][InlineData(0,4,false)][InlineData(2,1,false)]
    public void Signal_setters_refresh_crossings_for_both_axes_and_incomplete_roads(int direction,int edges,bool crossing)
    {
        var w=Rail(RailwayHandler.Normal,road:true);
        w=Decorate(w,v=>v.Traffic! with {Road=new(Ref("road"),edges)});
        var before=Save(w);var plan=SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,direction);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(before,Save(w));
        w=SignalRailwayConstruction.Apply(new(w),plan,new(0)).World;
        Assert.Equal(crossing,w.At(P)!.Traffic!.Accessory!=null);
        Assert.Equal(edges,w.At(P)!.Traffic!.Road!.Edges);
        Assert.Equal(crossing,Signal(w,(direction+2)%8,true).At(P)!.Traffic!.Accessory!=null);
        Assert.Empty(plan.Booking.Charges);
    }

    [Fact]
    public void Crossing_loss_pole_suppression_and_null_setter_recreation_preserve_owner_and_speed()
    {
        var w=Rail(RailwayHandler.Normal,road:true);
        w=Signal(w,2);w=Signal(w,0,true);var id=w.At(P)!.OwnerId;
        Assert.Equal(Ref("crossing"),w.At(P)!.Traffic!.Accessory!.Definition);
        var q=SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.Shunting,0);
        Assert.Equal(Ref("crossing"),Assert.Single(q.ComponentLosses).Accessory.Definition);
        w=SignalRailwayConstruction.Apply(new(w),q,new(0)).World;
        Assert.Null(w.At(P)!.Traffic!.Accessory);w=Signal(w,2,true);w=Signal(w,2);w=Signal(w,2,true);
        w=Pole(w);Assert.Equal(RailwayHandler.Pole,w.Profile.Resolve(w.At(P)!.Traffic!.Accessory!.Definition).Handler);
        // Even a protected car does not forbid accessory setters.
        w=Decorate(w,v=>v.Traffic! with {Car=new(Ref("car"),11,12)});
        w=Pole(w,true);Assert.Equal(Ref("crossing"),w.At(P)!.Traffic!.Accessory!.Definition);
        Assert.Equal(id,w.At(P)!.OwnerId);Assert.NotNull(w.At(P)!.Traffic!.Car);
    }

    [Theory]
    [InlineData(RailwayHandler.Normal,true)][InlineData(RailwayHandler.Junction,true)]
    [InlineData(RailwayHandler.Bridge,false)][InlineData(RailwayHandler.Girder,false)]
    [InlineData(RailwayHandler.Tunnel,false)][InlineData(RailwayHandler.Garage,false)][InlineData(RailwayHandler.Signal,false)]
    public void Normal_demolition_changes_only_detachable_subtypes_and_retains_road_speed_and_backups(RailwayHandler handler,bool clears)
    {
        var w=Rail(handler,road:true);
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,new([P],[],[]));
        var q=NormalRailwayConstruction.PreviewRemoval(w,P,End);Assert.True(q.Legal,q.Reason);
        var result=Normal(w,q);Assert.Equal(clears,result.At(P)!.Traffic!.Rail==null);
        Assert.NotNull(result.At(P)!.Traffic!.Road);Assert.Equal(w.At(P)!.OwnerId,result.At(P)!.OwnerId);
        Assert.Equal(P,Assert.Single(result.SpeedLimits.Low));
        Assert.Equal(w.Terrain.TunnelBackups,result.Terrain.TunnelBackups);
        Assert.Equal(w.Spatial.NextEntityId,result.Spatial.NextEntityId);
        var bytes=Save(result);Assert.Equal(bytes,SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,(_,_,_)=>Profile).Mixed!));
    }

    [Theory]
    [InlineData(RailwayHandler.Normal)][InlineData(RailwayHandler.Junction)][InlineData(RailwayHandler.Bridge)]
    [InlineData(RailwayHandler.Girder)][InlineData(RailwayHandler.Tunnel)][InlineData(RailwayHandler.Garage)][InlineData(RailwayHandler.Signal)]
    public void Every_rail_subtype_protects_cars_from_builds_and_normal_demolition(RailwayHandler handler)
    {
        var w=Rail(handler,car:true);var before=Save(w);
        RailwayConstructionPlan[] builds=[NormalRailwayConstruction.PreviewBuild(w,P,End),
            BridgeRailwayConstruction.Preview(w,P,End),GirderRailwayConstruction.Preview(w,P,End),
            GarageRailwayConstruction.Preview(w,P,End),TunnelRailwayConstruction.Preview(w,P,End),
            SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,2)];
        Assert.All(builds,p=>{Assert.False(p.Legal);Assert.Empty(p.Booking.Charges);});
        var q=NormalRailwayConstruction.PreviewRemoval(w,P,End);Assert.True(q.Legal,q.Reason);Assert.True(q.Topology.Cells[0].ProtectedByCar);
        var after=Normal(w,q);Assert.Equal(w.At(P),after.At(P));Assert.Equal(before,Save(w));
        Assert.Equal(handler,after.Profile.Resolve(after.At(P)!.Traffic!.Rail!.Definition).Handler);
    }

    [Theory]
    [InlineData(RailwayHandler.Normal)][InlineData(RailwayHandler.Bridge)][InlineData(RailwayHandler.Girder)]
    [InlineData(RailwayHandler.Tunnel)][InlineData(RailwayHandler.Garage)][InlineData(RailwayHandler.Signal)]
    public void Subtype_demolition_preserves_cars_with_exact_source_skips_and_booking(RailwayHandler handler)
    {
        var w=Rail(handler,car:true);RailwayConstructionPlan q;MixedWorld after;
        if(handler==RailwayHandler.Signal){Assert.Equal("carOccupied",SignalRailwayConstruction.Preview(w,P,SignalRailwayConstruction.ThreeAspect,2,true).Reason);return;}
        q=handler switch {
            RailwayHandler.Bridge=>BridgeRailwayConstruction.Preview(w,P,End,true),
            RailwayHandler.Girder=>GirderRailwayConstruction.Preview(w,P,End,true),
            RailwayHandler.Garage=>GarageRailwayConstruction.Preview(w,P,End,true),
            RailwayHandler.Tunnel=>TunnelRailwayConstruction.Preview(w,P,End,true),
            _=>NormalRailwayConstruction.PreviewRemoval(w,P,End)};
        Assert.True(q.Legal,q.Reason);Assert.True(q.Topology.Cells[0].ProtectedByCar);
        Assert.Equal(handler switch {RailwayHandler.Bridge or RailwayHandler.Girder=>6_000_000L,RailwayHandler.Garage=>400_000L,_=>0L},q.Booking.Total);
        after=(handler switch {
            RailwayHandler.Bridge=>BridgeRailwayConstruction.Apply(new(w),q,new(0)),
            RailwayHandler.Girder=>GirderRailwayConstruction.Apply(new(w),q,new(0)),
            RailwayHandler.Garage=>GarageRailwayConstruction.Apply(new(w),q,new(0)),
            RailwayHandler.Tunnel=>TunnelRailwayConstruction.Apply(new(w),q,new(0)),
            _=>NormalRailwayConstruction.Apply(new(w),q,new(0))}).World;
        Assert.Equal(w.At(P),after.At(P));Assert.Equal(w.Terrain.TunnelBackups,after.Terrain.TunnelBackups);
    }

    [Theory]
    [InlineData(RailwayHandler.Normal)][InlineData(RailwayHandler.Bridge)][InlineData(RailwayHandler.Girder)][InlineData(RailwayHandler.Tunnel)]
    public void Platform_dependency_rejects_complete_route_before_any_charge(RailwayHandler handler)
    {
        var w=Rail(handler,platform:true);var before=Save(w);
        var q=handler switch {
            RailwayHandler.Bridge=>BridgeRailwayConstruction.Preview(w,P,End,true),
            RailwayHandler.Girder=>GirderRailwayConstruction.Preview(w,P,End,true),
            RailwayHandler.Tunnel=>TunnelRailwayConstruction.Preview(w,P,End,true),
            _=>NormalRailwayConstruction.PreviewRemoval(w,P,End)};
        Assert.Equal("platformDependency",q.Reason);Assert.Empty(q.Booking.Charges);Assert.Equal(before,Save(w));
    }

    [Fact]
    public void Tunnel_accessory_and_road_restoration_dependencies_reject_without_losing_corners()
    {
        foreach(var component in new[]{"road","pole"}){
            var w=Rail(RailwayHandler.Tunnel,road:component=="road");if(component=="pole")w=Pole(w);
            var before=Save(w);var q=TunnelRailwayConstruction.Preview(w,P,End,true);
            Assert.Equal("tunnelComponentDependency",q.Reason);Assert.Empty(q.Booking.Charges);Assert.Equal(before,Save(w));
            Assert.Equal(new QuarterCorners(0,1,2,3),w.Terrain.TunnelBackups[P]);
        }
    }

    [Fact]
    public void Slope_removal_from_every_member_preserves_surviving_Traffic_and_cleans_only_absent_limits()
    {
        foreach(var direction in new[]{0,2,4,6}){
            var empty=GarageConstructionTests.World();var build=SlopeRailwayConstruction.PreviewBuild(empty,P,new(direction));
            var w=SlopeRailwayConstruction.Apply(new(empty),build,new(0)).World;
            w=Decorate(w,v=>v.Traffic! with {Road=new(Ref("road"),17),Accessory=new(w.Profile.Bindings.First(b=>b.Handler==RailwayHandler.Pole).Reference)});
            var railPositions=w.Voxels.Where(v=>v.Traffic!=null).Select(v=>v.Position).ToImmutableArray();
            w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,w.Slopes,w.Revision,new(railPositions,[],[]));
            foreach(var member in build.Members){
                var q=SlopeRailwayConstruction.PreviewRemoval(w,member.Position,new(direction));Assert.True(q.Legal,q.Reason);
                var after=SlopeRailwayConstruction.Apply(new(w),q,new(0)).World;
                Assert.Empty(after.Slopes);Assert.Equal(4,after.Voxels.Length);Assert.Equal(4,after.Spatial.Entities.Length);
                Assert.True(railPositions.SequenceEqual(after.SpeedLimits.Low));Assert.All(after.Voxels,v=>{Assert.Null(v.Traffic!.Rail);Assert.NotNull(v.Traffic.Road);Assert.NotEqual(build.OwnerId,v.OwnerId);});
                var entity=SlopeRailwayConstruction.Apply(new(w),SlopeRailwayConstruction.PreviewEntityRemoval(w,member.Position),new(0)).World;
                Assert.Empty(entity.Voxels);Assert.Empty(entity.SpeedLimits.Locations);
            }
            foreach(var dependency in new[]{"car","platform"}){
                var protectedWorld=Decorate(w,v=>v.Traffic! with{Car=dependency=="car"?new(Ref("car"),11,v.Position==railPositions[3]?12:13+Array.IndexOf(railPositions.ToArray(),v.Position)):null,Platform=dependency=="platform"?Ref("platform"):null});
                foreach(var member in build.Members){
                    Assert.False(SlopeRailwayConstruction.PreviewRemoval(protectedWorld,member.Position,new(direction)).Legal);
                    Assert.False(SlopeRailwayConstruction.PreviewEntityRemoval(protectedWorld,member.Position).Legal);
                }
            }
        }
    }
}
