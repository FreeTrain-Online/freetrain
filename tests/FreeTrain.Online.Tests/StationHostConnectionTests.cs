using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;
using static FreeTrain.Online.Tests.StationStateTests;

namespace FreeTrain.Online.Tests;

public class StationHostConnectionTests
{
    static MixedSessionState Empty(int ground=2)
    {
        var terrain=new TerrainWorld(TerrainProfiles.Bundled,"Host discovery",new(96,128,45),0,
            Enumerable.Repeat(new TerrainCell(ground),96*128),new(1,[]),[]);
        return new(new(MixedWorld.FromTerrain(RailwayProfiles.Bundled,terrain)),
            TerrainProvenance.From(new EmptyWorldRequest(WorldCreationProfile.Id,terrain.Name,new(96,128,45),0)),clock:new(517,0,[]));
    }
    static MixedSessionState Build(MixedSessionState s,Location p,StationDefinition? definition=null) =>
        StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,p,(definition??Definition).Reference));
    static MixedSessionState Place(MixedSessionState s,Location p,int heading=2,bool wide=false,int length=4)
    {
        var plan=wide ? WidePlatformConstruction.Preview(s.World,p,new(heading),length,false) :
            ThinPlatformConstruction.Preview(s.World,p,new(heading),length,false);
        Assert.True(plan.Legal,plan.Reason);
        var after=wide ? WidePlatformConstruction.Commit(s,plan) : ThinPlatformConstruction.Commit(s,plan);
        MixedWire.ValidateTransition(s,after);return after;
    }
    static MixedSessionState Open(byte[] bytes)=>SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve,RailwayProfiles.Resolve).Mixed!;
    static MixedSessionState Connect(MixedSessionState s,long id,long? host)
    {
        var graph=s.World.Stations!;
        var plan=StationHostConnections.Preview(s.World,id,graph.Platform(id).Revision,host,
            host is {} h ? graph.Identities.Single(i=>i.Id==h).Revision : 0);
        var after=StationHostConnections.Commit(s,plan);MixedWire.ValidateTransition(s,after);return after;
    }
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Literal_range_probes_find_constructed_station_voxels_at_inclusive_edges_and_only_L_full_height_columns(int heading)
    {
        using var fixtures=Fixtures();
        var row=Cases(fixtures,"hostRange").Single(r=>r.GetProperty("input").GetProperty("heading").GetInt32()==heading);
        var expected=row.GetProperty("expected");
        var platform=Platform(direction:heading,location:new(70,70,2));
        var other=expected.GetProperty("otherEnd");
        Assert.Equal(new(other[0].GetInt32()+60,other[1].GetInt32()+60,2),StationHostConnections.OtherEnd(platform));
        var candidates=expected.GetProperty("candidates").EnumerateArray().Select(e=>e.GetString()).ToHashSet();
        foreach(var probe in row.GetProperty("input").GetProperty("candidates").EnumerateArray().Where(p=>p.GetProperty("kind").GetString()=="station"))
        {
            var locations=probe.GetProperty("voxels").EnumerateArray().Select(p=>new Location(p[0].GetInt32()+60,p[1].GetInt32()+60,p[2].GetInt32())).ToArray();
            var found=false;
            foreach(var p in locations.Where(p=>p.Z<45))
            {
                var s=Build(Empty(p.Z),p);
                found |= !StationHostConnections.Candidates(s.World,platform).IsEmpty;
            }
            Assert.Equal(candidates.Contains(probe.GetProperty("id").GetString()),found);
        }
    }
    [Theory]
    [InlineData(false,0)][InlineData(false,2)][InlineData(false,4)][InlineData(false,6)]
    [InlineData(true,0)][InlineData(true,2)][InlineData(true,4)][InlineData(true,6)]
    public void Constructed_platforms_choose_smallest_direct_station_ID_not_nearest_and_never_auto_select_platform_parents(bool wide,int heading)
    {
        var (dx,dy)=new Heading(heading).Offset;
        var s=Build(Empty(),new(70-dx*8-dy*8,70-dy*8+dx*8,2));s=Build(s,new(70-dx,70-dy,2));
        var station=s.World.Stations!.Buildings[0];
        s=Place(s,new(70,70,2),heading,wide);
        var platform=s.World.Stations!.Platforms[0];
        Assert.Equal(station.Id,platform.HostId);Assert.Equal(new[]{platform.Id},s.World.Stations.Building(station.Id).Children);
        Assert.Equal(1,s.World.Stations.Building(station.Id).Revision);
        Assert.Equal(1,s.World.Spatial.Entities.Single(e=>e.Id==station.Id).Revision);
        Assert.Equal(0,platform.Revision);Assert.Equal(station.Id,s.World.Stations.HostStation(platform.Id)!.Id);
        var copy=Open(SaveCodec.Write(s));Assert.Equal(SaveCodec.Write(s),SaveCodec.Write(copy));
        // This distant platform is within 8 of the first platform's far end but
        // outside both station rectangles. Construction must leave it isolated.
        var far=StationGraph.CenterLocation(platform,platform.Length+7);
        s=Place(s,far,heading,wide);
        Assert.Null(s.World.Stations!.Platforms[^1].HostId);
        Assert.Contains(StationHostConnections.Candidates(s.World,s.World.Stations.Platforms[^1]),c=>c.Id==platform.Id&&!c.IsStation);
    }
    [Fact]
    public void Any_occupied_station_member_can_be_found_when_anchor_is_outside_and_vertical_columns_connect_real_platforms()
    {
        var type=Profile.Definitions.First(d=>d.SizeX>1&&d.Height>1);
        var s=Build(Empty(),new(60-type.SizeX+1,70,2),type);
        // East-facing rectangle starts at x60, so only the building's last X
        // column overlaps; its anchor is strictly outside.
        s=Place(s,new(68,70,3));
        var p=s.World.Stations!.Platforms[0];Assert.Equal(s.World.Stations.Buildings[0].Id,p.HostId);
        var vertical=Build(Empty(),new(70,70,2));vertical=Place(vertical,new(70,70,44),wide:true);
        Assert.Equal(vertical.World.Stations!.Buildings[0].Id,vertical.World.Stations.Platforms[0].HostId);
        var miss=Build(Empty(),new(74,70,2));miss=Place(miss,new(70,70,44));
        Assert.Null(miss.World.Stations!.Platforms[0].HostId); // otherEnd is not an actual column
    }
    [Fact]
    public void Wide_hosts_are_discovered_only_through_center_voxels_and_explicit_connected_parents_do_not_affect_automatic_choice()
    {
        var s=Build(Empty(),new(70,70,2));s=Place(s,new(70,70,5),wide:true);
        var parent=s.World.Stations!.Platforms[0];
        s=Place(s,new(70,79,5));var outside=s.World.Stations!.Platforms[^1];
        Assert.Null(outside.HostId);
        // The wide yard at y71 touches this rectangle, but FatPlatform.get
        // recognizes only its center strip at y70. ThinPlatform.get recognizes
        // only ThinPlatform.RailRoadImpl, not a wide yard's RailRoadImpl.
        Assert.DoesNotContain(StationHostConnections.Candidates(s.World,outside),c=>c.Id==parent.Id);
        s=Place(s,new(70,78,5));var child=s.World.Stations!.Platforms[^1];
        Assert.Null(child.HostId);
        var host=Assert.Single(StationHostConnections.Candidates(s.World,child));Assert.Equal(parent.Id,host.Id);
        s=Connect(s,child.Id,parent.Id);
        Assert.Equal(s.World.Stations!.Buildings[0].Id,s.World.Stations.HostStation(child.Id)!.Id);
        Assert.DoesNotContain(StationHostConnections.Candidates(s.World,s.World.Stations.Platform(child.Id)),c=>c.Id==child.Id);
        var bytes=SaveCodec.Write(s);Assert.Equal(bytes,SaveCodec.Write(Open(bytes)));
    }
    [Fact]
    public void All_literal_graph_operations_execute_source_disconnect_order_without_publishing_invalid_graphs()
    {
        using var fixtures=Fixtures();
        foreach(var row in Cases(fixtures,"hostGraph"))
        {
            var input=row.GetProperty("input");var expected=row.GetProperty("expected");
            var stations=input.GetProperty("stations").EnumerateArray().Select(s=>s.GetString()!).ToArray();
            var parents=input.GetProperty("parents").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.ValueKind==JsonValueKind.Null?null:p.Value.GetString());
            var ids=stations.Concat(parents.Keys).Select((s,i)=>(s,Id:(long)i+1)).ToDictionary(p=>p.s,p=>p.Id);
            ImmutableArray<long> ChildrenOf(string name)=>parents.Where(p=>p.Value==name).Select(p=>ids[p.Key]).ToImmutableArray();
            StationGraph Read()=>Graph(stations.Select(s=>Building(ids[s],ChildrenOf(s))),parents.Select((p,i)=>
                Platform(ids[p.Key],host:p.Value==null?null:ids[p.Value],children:ChildrenOf(p.Key),length:1,firstTraffic:100+i,location:new(20+i*3,20+i*3,2))));
            if(row.GetProperty("id").GetString()=="graph-6"){Assert.Throws<ArgumentException>(Read);continue;}
            var graph=Read();var node=ids[input.GetProperty("node").GetString()!];
            var target=input.GetProperty("target");long? host=target.ValueKind==JsonValueKind.Null?null:ids.GetValueOrDefault(target.GetString()!,999);
            if(expected.TryGetProperty("rejected",out _)){Assert.Throws<ArgumentException>(()=>StationHostConnections.Reparent(graph,node,host));continue;}
            var after=StationHostConnections.Reparent(graph,node,host);
            foreach(var p in expected.GetProperty("parents").EnumerateObject())
                Assert.Equal(p.Value.ValueKind==JsonValueKind.Null?(long?)null:ids[p.Value.GetString()!],after.Platform(ids[p.Name]).HostId);
            foreach(var p in expected.GetProperty("resolved").EnumerateObject())Assert.Equal(p.Value.GetBoolean(),after.HostStation(ids[p.Name])!=null);
            if(row.GetProperty("id").GetString()=="graph-3")Assert.Same(graph,after);
            Assert.Equal(parents["A"]==null?(long?)null:ids[parents["A"]!],graph.Platform(node).HostId);
        }
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Constructed_chains_reparent_descendant_disconnect_remove_rebuild_and_continue_exactly_without_service_effects(bool removeStation)
    {
        var s=Build(Empty(),new(68,68,2));s=Build(s,new(72,68,2));
        s=Place(s,new(70,70,2),wide:true);s=Place(s,new(70,73,2));s=Place(s,new(70,76,2));
        var g=s.World.Stations!;var ids=g.Platforms.Select(p=>p.Id).ToArray();var station=g.Buildings[0].Id;
        s=Connect(s,ids[1],ids[0]);s=Connect(s,ids[2],ids[1]);
        s=Connect(s,ids[0],g.Buildings[1].Id);
        Assert.Equal(g.Buildings[1].Id,s.World.Stations!.HostStation(ids[0])!.Id);
        Assert.All(s.World.Stations.Platforms.Skip(1),p=>{Assert.Null(p.HostId);Assert.Empty(p.Children);});
        Assert.Empty(s.World.Stations.Building(station).Children);
        s=Connect(s,ids[0],station);s=Connect(s,ids[1],ids[0]);s=Connect(s,ids[2],ids[1]);
        Assert.Same(s,Connect(s,ids[0],station));
        var root=s.World.Stations!.HostStation(ids[2])!;Assert.Equal(station,root.Id);
        var c=StationHostConnections.Candidates(s.World,s.World.Stations.Platform(ids[2])).Single(c=>c.Id==ids[1]);
        Assert.Equal((root.Id,root.Revision,root.Location),(c.StationId,c.StationRevision,c.StationLocation));
        var before=SaveCodec.Write(s);var rng=JsonSerializer.Serialize(s.Random.Capture());var queue=s.Clock.Capture();var accounts=s.Construction.Accounts;
        var stale=StationHostConnections.Preview(s.World,ids[0],s.World.Stations.Platform(ids[0]).Revision,null);
        Assert.Throws<InvalidOperationException>(()=>StationHostConnections.Preview(s.World,ids[0],999,station,root.Revision));
        Assert.Throws<InvalidOperationException>(()=>StationHostConnections.Preview(s.World,ids[0],s.World.Stations.Platform(ids[0]).Revision,station,root.Revision+1));
        Assert.Throws<ArgumentException>(()=>StationHostConnections.Preview(s.World,ids[0],s.World.Stations.Platform(ids[0]).Revision,ids[0],s.World.Stations.Platform(ids[0]).Revision));
        Assert.Equal(before,SaveCodec.Write(s));
        var disconnected=Connect(s,ids[0],ids[2]);
        Assert.All(disconnected.World.Stations!.Platforms,p=>{Assert.Null(p.HostId);Assert.Empty(p.Children);});
        Assert.True(queue.Pending.SequenceEqual(disconnected.Clock.Capture().Pending));Assert.Equal(queue.NextOrder,disconnected.Clock.NextOrder);
        Assert.Same(accounts,disconnected.Construction.Accounts);
        Assert.Equal(rng,JsonSerializer.Serialize(disconnected.Random.Capture()));
        Assert.All(disconnected.World.Stations.Buildings,b=>{Assert.Equal(new StationPassengers(),b.Passengers);Assert.Equal(new StationTransportLog(),b.Trains);});
        s=Connect(disconnected,ids[0],station);Assert.Throws<InvalidOperationException>(()=>StationHostConnections.Commit(s,stale));
        s=Connect(s,ids[1],ids[0]);s=Connect(s,ids[2],ids[1]);
        var graph=s.World.Stations!;MixedSessionState removed;
        if(removeStation){var b=graph.Building(station);removed=StationBuildingConstruction.Commit(s,StationBuildingConstruction.Preview(s.World,b.Location,null,true,b.Id,b.Revision));}
        else{var p=graph.Platform(ids[0]);removed=WidePlatformConstruction.Commit(s,WidePlatformConstruction.Preview(s.World,p.Location,p.Direction,p.Length,false,true,p.Id,p.Revision));}
        MixedWire.ValidateTransition(s,removed);Assert.All(removed.World.Stations!.Platforms,p=>Assert.Null(p.HostId));
        var reopened=Open(SaveCodec.Write(removed));
        MixedSessionState Rebuild(MixedSessionState state)=>removeStation ? Build(state,new(68,68,2)) : Place(state,new(70,70,2),wide:true);
        removed=Rebuild(removed);reopened=Rebuild(reopened);
        Assert.Equal(SaveCodec.Write(removed),SaveCodec.Write(reopened));
        Assert.All(removed.World.Stations!.Platforms.Where(p=>ids.Skip(1).Contains(p.Id)),p=>Assert.Null(p.HostId));
        for(var n=0;n<61;n++){removed.AdvanceOneMinute();reopened.AdvanceOneMinute();}
        Assert.Equal(SaveCodec.Write(removed),SaveCodec.Write(reopened));
    }
}
