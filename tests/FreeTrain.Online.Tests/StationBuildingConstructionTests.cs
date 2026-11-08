using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;

namespace FreeTrain.Online.Tests;

public class StationBuildingConstructionTests
{
    internal static Location Base => new(10,10,2);
    internal static MixedSessionState Empty(long now=517, MixedWorld? world=null, ClockState? clock=null)
    {
        var w=world ?? MixedWorld.FromTerrain(RailwayProfiles.Bundled,Terrain());
        return new(new(w),TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,w.Terrain.Name,new(64,24,8),0)),
            clock:clock ?? new(now,0,[]),revision:w.Revision);
    }
    internal static MixedSessionState Build(MixedSessionState s, StationDefinition? definition=null, Location? at=null) =>
        StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,at ?? Base,(definition ?? Definition).Reference));
    static MixedSessionState Open(byte[] bytes) => SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    public static IEnumerable<object[]> Definitions()
    {
        using var fixture=Fixtures();
        return Cases(fixture,"stations").Select(row=>new object[]{row.GetProperty("id").GetString()!}).ToArray();
    }
    [Theory]
    [MemberData(nameof(Definitions))]
    public void All_35_authored_buildings_construct_immediately_and_remove_from_any_member_with_exact_source_booking(string id)
    {
        using var fixture=Fixtures();
        var expected=Cases(fixture,"stations").Single(row=>row.GetProperty("id").GetString()==id).GetProperty("expected");
        var type=Profile.Definitions.Single(d=>d.Reference.ContributionId==id);
        var s=Empty();var original=SaveCodec.Write(s);var rng=JsonSerializer.Serialize(s.Random.Capture());
        var plan=StationBuildingConstruction.Preview(s.World,Base,type.Reference);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(original,SaveCodec.Write(s));
        Assert.Equal(expected.GetProperty("price").GetInt64(),plan.Booking.Total);
        Assert.Equal(expected.GetProperty("voxelCount").GetInt32(),plan.Positions.Length);
        s=StationBuildingConstruction.Commit(s,plan);MixedWire.ValidateTransition(Empty(),s);
        var b=Assert.Single(s.World.Stations!.Buildings);var e=Assert.Single(s.World.Spatial.Entities);
        Assert.Equal("ST 1",b.Name);Assert.Equal(2,s.World.Stations.NextStationName);
        Assert.Equal(type.Reference,b.Definition);Assert.Equal(Base,b.Location);
        Assert.True(e.Owned);Assert.False(e.SilentlyReclaimable);Assert.Equal(0,e.Value);
        Assert.Empty(s.World.Voxels);Assert.Equal(rng,JsonSerializer.Serialize(s.Random.Capture()));
        Assert.Equal(new long[]{540,1440},s.Clock.Capture().Pending.Select(t=>t.DueMinute));
        Assert.Equal(new long[]{60,1440},s.Clock.Capture().Pending.Select(t=>t.Interval));
        Assert.Equal(AccountGenre.RailService,Assert.Single(s.Construction.Accounts.Transactions).Genre);
        foreach (var p in plan.Positions) { Assert.Equal(b.Id,s.World.Spatial.At(p).Entity!.Id);Assert.False(s.World.Spatial.At(p).Reusable); }
        var bytes=SaveCodec.Write(s);Assert.Equal(bytes,SaveCodec.Write(Open(bytes)));
        var remove=StationBuildingConstruction.Preview(s.World,plan.Positions[^1],null,true,b.Id,b.Revision);
        Assert.True(remove.Legal,remove.Reason);Assert.Equal(expected.GetProperty("demolition").GetInt64(),remove.Booking.Total);
        var removed=StationBuildingConstruction.Commit(s,remove);MixedWire.ValidateTransition(s,removed);
        Assert.Empty(removed.World.Stations!.Buildings);Assert.Empty(removed.World.Spatial.Entities);Assert.Empty(removed.Clock.Capture().Pending);
        Assert.Equal(AccountGenre.Subsidiaries,removed.Construction.Accounts.Transactions[^1].Genre);
        Assert.Equal(remove.Booking.Total,removed.Construction.Accounts.Transactions[^1].Expenditure);
        Assert.Equal(SaveCodec.Write(removed),SaveCodec.Write(Open(SaveCodec.Write(removed))));
        var rebuilt=Build(removed,type);Assert.Equal("ST 2",rebuilt.World.Stations!.Buildings[0].Name);
        Assert.True(rebuilt.World.Stations.Buildings[0].Id>b.Id);
    }
    [Theory]
    [InlineData(10,10,1,"stationGround")][InlineData(10,10,3,"stationGround")]
    [InlineData(10,10,7,"outside")][InlineData(int.MaxValue,10,2,"outside")]
    [InlineData(-1,-1,2,"outside")]
    public void Height_bounds_and_ground_reject_without_mutating_ids_random_clock_or_accounts(int x,int y,int z,string reason)
    {
        var s=Empty();var before=SaveCodec.Write(s);var tall=Profile.Definitions.First(d=>d.Height>1);
        var plan=StationBuildingConstruction.Preview(s.World,new(x,y,z),tall.Reference);
        Assert.False(plan.Legal);Assert.Equal(reason,plan.Reason);Assert.Equal(before,SaveCodec.Write(s));
    }
    [Fact]
    public void Nonflat_footprint_and_upper_cell_occupancy_are_checked_across_the_whole_definition()
    {
        var type=Profile.Definitions.First(d=>d.SizeX>1 || d.SizeY>1);
        var terrain=Terrain();var far=type.Bounds(Base).Locations().Last() with {Z=2};
        var uneven=CornerTerrain.Apply(terrain,CornerTerrain.Preview(terrain,far,"lower"));
        var s=Empty(world:MixedWorld.FromTerrain(RailwayProfiles.Bundled,uneven));var bytes=SaveCodec.Write(s);
        Assert.Equal("stationGround",StationBuildingConstruction.Preview(s.World,Base,type.Reference).Reason);
        Assert.Equal(bytes,SaveCodec.Write(s));
        var tall=Profile.Definitions.First(d=>d.Height>1);var blocker=Building() with {Location=Base with {Z=3}};
        s=Empty(world:World(Graph([blocker])));bytes=SaveCodec.Write(s);
        Assert.Equal("obstacle",StationBuildingConstruction.Preview(s.World,Base,tall.Reference).Reason);Assert.Equal(bytes,SaveCodec.Write(s));
    }
    [Fact]
    public void Reclaimable_creek_is_reported_and_removed_atomically_without_a_generic_stationary_payload()
    {
        var terrain=Terrain();var binding=terrain.Profile.Definitions.First(d=>d.Kind==TerrainOccupantKind.Creek);
        var e=new SpatialEntity(1,0,binding.Reference,Base,new([new(new(0,0,0),true)]),false,true,0);
        terrain=new(terrain.Profile,terrain.Name,new(64,24,8),0,terrain.Cells,new(2,[e]),[new(1,TerrainOccupantKind.Creek,new(),CreekConnections:0)]);
        var s=Empty(world:MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain));var before=SaveCodec.Write(s);
        var plan=StationBuildingConstruction.Preview(s.World,Base,Definition.Reference);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(1,Assert.Single(plan.Reclaimed).Entity.Id);Assert.Equal(before,SaveCodec.Write(s));
        var after=StationBuildingConstruction.Commit(s,plan);MixedWire.ValidateTransition(s,after);
        Assert.Empty(after.World.Terrain.Occupants);Assert.Equal(2,Assert.Single(after.World.Spatial.Entities).Id);
        Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Open(SaveCodec.Write(after))));
    }
    [Fact]
    public void Occupied_wrong_definition_and_stale_removal_candidates_are_pure_and_cannot_commit_against_a_successor()
    {
        var s=Empty();var plan=StationBuildingConstruction.Preview(s.World,Base,Definition.Reference);var built=StationBuildingConstruction.Commit(s,plan);
        var bytes=SaveCodec.Write(built);var b=built.World.Stations!.Buildings[0];
        Assert.Equal("obstacle",StationBuildingConstruction.Preview(built.World,Base,Definition.Reference).Reason);
        Assert.Equal("stationNotFound",StationBuildingConstruction.Preview(built.World,Base,null,true,b.Id+1,0).Reason);
        Assert.Equal("staleRevision",StationBuildingConstruction.Preview(built.World,Base,null,true,b.Id,b.Revision+1).Reason);
        Assert.Equal("unavailableStationBinding",StationBuildingConstruction.Preview(built.World,new(14,10,2),Profile.Art[0].Reference).Reason);
        Assert.Equal(bytes,SaveCodec.Write(built));Assert.Throws<InvalidOperationException>(()=>StationBuildingConstruction.Commit(built,plan));
    }
    [Fact]
    public void Removal_disconnects_all_host_descendants_and_ordered_asymmetric_listener_edges_and_cancels_only_owned_timers()
    {
        var b=Building(children:[2]);var a=Platform(kind:PlatformKind.Wide,host:1,children:[3]);var c=Platform(id:3,host:2,firstTraffic:20,location:new(24,20,2));
        var graph=new StationGraph(Profile,[b],[a,c],listeners:[new(a.Yards[0].TrafficOwnerId,[1])],listenerMemberships:[]);
        var w=World(graph);var registered=StationLifecycle.Reconcile(null,graph,new(517,0,[]));
        w=new(w.Profile,w.Terrain,w.Spatial.Capture(),w.Voxels,[],stations:registered.Graph);
        var s=Empty(world:w,clock:registered.Clock);var before=SaveCodec.Write(s);
        var plan=StationBuildingConstruction.Preview(w,b.Location,null,true,b.Id,b.Revision);Assert.True(plan.Legal,plan.Reason);Assert.Equal(before,SaveCodec.Write(s));
        var after=StationBuildingConstruction.Commit(s,plan);MixedWire.ValidateTransition(s,after);
        Assert.All(after.World.Stations!.Platforms,p=>{Assert.Null(p.HostId);Assert.Empty(p.Children);Assert.Equal(1,p.Revision);});
        Assert.Empty(after.World.Stations.Listeners[0].Stations);Assert.Equal(1,after.World.Spatial.At(a.Yards[0].Location).Entity!.Revision);
        Assert.Equal(2,after.Clock.Capture().Pending.Length);Assert.All(after.Clock.Capture().Pending,t=>Assert.StartsWith("platform/",t.HandlerId));
        Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Open(SaveCodec.Write(after))));
    }
    [Fact]
    public void Reclaiming_a_registered_provider_removes_its_edges_and_revises_the_existing_station_without_restarting_timers()
    {
        var s=Build(Empty());var station=s.World.Stations!.Buildings[0];
        var at=new Location(14,12,2);var terrain=s.World.Terrain;var binding=terrain.Profile.Definitions.First(d=>d.Kind==TerrainOccupantKind.Creek);
        var id=s.World.Spatial.NextEntityId;
        var e=new SpatialEntity(id,0,binding.Reference,at,new([new(new(0,0,0),true)]),false,true,0);
        terrain=new(terrain.Profile,terrain.Name,new(64,24,8),0,terrain.Cells,new(id+1,[e]),[new(id,TerrainOccupantKind.Creek,new(),CreekConnections:0)]);
        var graph=new StationGraph(Profile,[station],[],nextStationName:2,listeners:[new(id,[station.Id])],listenerMemberships:[new(station.Id,[id])]);
        var w=new MixedWorld(s.World.Profile,terrain,new(id+1,s.World.Spatial.Entities.Add(e)),[],[],s.World.Revision,stations:graph);
        s=Empty(world:w,clock:s.Clock.Capture());var before=SaveCodec.Write(s);
        var plan=StationBuildingConstruction.Preview(w,at,Definition.Reference);Assert.True(plan.Legal,plan.Reason);Assert.Equal(before,SaveCodec.Write(s));
        var after=StationBuildingConstruction.Commit(s,plan);MixedWire.ValidateTransition(s,after);
        Assert.Empty(after.World.Stations!.Listeners);Assert.Empty(after.World.Stations.ListenerMemberships[0].Providers);
        var retained=after.World.Stations.Building(station.Id);Assert.Equal(station.HourTimer,retained.HourTimer);Assert.Equal(station.DayTimer,retained.DayTimer);
        Assert.Equal(station.Revision+1,retained.Revision);Assert.Equal(retained.Revision,after.World.Spatial.At(station.Location).Entity!.Revision);
        Assert.Equal(SaveCodec.Write(after),SaveCodec.Write(Open(SaveCodec.Write(after))));
    }
    [Theory]
    [InlineData(0,60,1440)][InlineData(540,600,1440)][InlineData(1439,1440,1440)]
    public void Construction_at_aligned_and_equal_boundaries_registers_hour_before_day(long now,long hour,long day)
    {
        var s=Build(Empty(now));var timers=s.Clock.Capture().Pending;
        Assert.Equal(new[]{hour,day},timers.Select(t=>t.DueMinute));
        Assert.EndsWith("/hour",timers[0].HandlerId);Assert.EndsWith("/day",timers[1].HandlerId);Assert.True(timers[0].Order<timers[1].Order);
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(Open(SaveCodec.Write(s))));
    }
    [Fact]
    public void Immediately_saved_timers_continue_to_the_same_due_boundary_and_default_dispatch_stays_explicitly_pending()
    {
        var s=Build(Empty());var reopened=Open(SaveCodec.Write(s));
        for(var i=0;i<22;i++){s.AdvanceOneMinute();reopened.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(reopened));var before=SaveCodec.Write(s);
        Assert.Throws<InvalidOperationException>(()=>s.AdvanceOneMinute());Assert.Equal(before,SaveCodec.Write(s));
        var b=s.World.Stations!.Buildings[0];s=StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,b.Location,null,true,b.Id,b.Revision));
        s.AdvanceOneMinute();Assert.Equal(540,s.Clock.Time.AbsoluteMinute);Assert.Empty(s.Clock.Capture().Pending);
    }
}
