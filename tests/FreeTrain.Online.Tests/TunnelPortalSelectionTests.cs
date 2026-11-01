using FreeTrain.Online.Simulation;
using System.Collections.Immutable;
using Xunit;
namespace FreeTrain.Online.Tests;

public class TunnelPortalSelectionTests
{
    // Literal cross-section from the reported route: lower ground at Z6,
    // twelve buried cells at ground7, then the opposite sloping portal.
    internal static MixedWorld Ridge()
    {
        var profile=TunnelConstructionTests.Profile;var space=new WorldSpace(40,40,24);
        var entities=new List<SpatialEntity>();var occupants=new List<TerrainOccupant>();var cells=new List<TerrainCell>();
        var definition=profile.Terrain.Definitions.Single(d=>d.Kind==TerrainOccupantKind.Mountain&&d.TreeSpriteCount==0).Reference;
        long id=1;
        for(var v=0;v<40;v++)for(var h=0;h<40;h++)
        {
            var p=space.ToLocation(new(h,v,6));cells.Add(new(p.X is >=15 and <=26?7:6));
            if(p.X is not (14 or 27))continue;
            var corners=p.X==14?new QuarterCorners(4,4,0,0):new(0,0,4,4);
            entities.Add(new(id,0,definition,p,new([new(new(0,0,0),false)]),false,false,0));
            occupants.Add(new(id++,TerrainOccupantKind.Mountain,corners));
        }
        return MixedWorld.FromTerrain(profile,new(profile.Terrain,"Sloped ridge",new(40,40,24),4,cells,new(id,entities.ToImmutableArray()),occupants));
    }
    [Theory]
    [InlineData(13,28,6,false)][InlineData(14,27,6,false)][InlineData(15,26,6,false)][InlineData(15,26,7,true)]
    public void Reported_hill_cross_section_resolves_and_keeps_source_backups_and_actual_cost(int x,int endX,int z,bool auto)
    {
        var w=Ridge();var plan=TunnelPortalSelection.Preview(w,new(x,14,z),new(endX,14,z),auto);
        Assert.True(plan.Legal,plan.Reason);Assert.Equal(new Location(14,14,6),plan.Topology.From);Assert.Equal(new Location(27,14,6),plan.Topology.To);
        Assert.Equal(168_000_000,plan.Booking.Total);Assert.Equal(2,plan.Reclaimed.Length);
        Assert.Equal(new QuarterCorners(4,4,0,0),plan.Topology.Cells[0].After!.TerrainBackup);
        Assert.Equal(new QuarterCorners(0,0,4,4),plan.Topology.Cells[^1].After!.TerrainBackup);
    }
    [Theory]
    [InlineData(0)][InlineData(2)][InlineData(4)][InlineData(6)]
    public void Outside_surface_and_inside_picks_resolve_to_same_portals_in_both_route_orders(int direction)
    {
        var d=new Heading(direction);var a=TunnelConstructionTests.Start;var b=a.Move(d).Move(d).Move(d).Move(d);
        var w=TunnelConstructionTests.World([a,b]);
        foreach(var left in new[]{a.Move(d.Opposite),a,a.Move(d)})
        foreach(var right in new[]{b.Move(d.Opposite),b,b.Move(d)})
        {
            var plan=TunnelPortalSelection.Preview(w,left,right,false);
            Assert.True(plan.Legal,plan.Reason);Assert.Equal(a,plan.Topology.From);Assert.Equal(b,plan.Topology.To);
            Assert.Equal(60_000_000,plan.Booking.Total);Assert.Empty(w.Voxels);
        }
    }
    [Fact]
    public void Automatic_height_can_find_nearby_lower_portals_but_manual_height_is_never_changed()
    {
        var a=TunnelConstructionTests.Start;var b=a with{X=a.X+4};var w=TunnelConstructionTests.World([a,b]);
        var from=a.Move(new(2)) with{Z=3};var to=b.Move(new(6)) with{Z=3};
        var automatic=TunnelPortalSelection.Preview(w,from,to,true);Assert.True(automatic.Legal);
        Assert.Equal(a,automatic.Topology.From);Assert.Equal(b,automatic.Topology.To);
        var manual=TunnelPortalSelection.Preview(w,from,to,false);Assert.False(manual.Legal);Assert.Equal("tunnelMountainRequired",manual.Reason);
    }
    [Fact]
    public void Selection_never_skips_obstructions_or_moves_distant_points_and_exact_tool_keeps_its_route()
    {
        var a=TunnelConstructionTests.Start;var b=a with{X=a.X+4};var w=TunnelConstructionTests.World([a,b]);
        var blocked=TunnelConstructionTests.Add(w,a with{X=a.X+2},pattern:10);
        Assert.Equal("cannotAttach",TunnelPortalSelection.Preview(blocked,a.Move(new(2)),b.Move(new(6)),false).Reason);
        var exact=TunnelRailwayConstruction.Preview(w,a.Move(new(2)),b.Move(new(6)));
        Assert.False(exact.Legal);Assert.Equal("tunnelMountainRequired",exact.Reason);
        var distant=TunnelPortalSelection.Preview(w,a with{X=a.X-2},b with{X=b.X+2},false);
        Assert.Equal(a with{X=a.X-2},distant.Topology.From);Assert.Equal(b with{X=b.X+2},distant.Topology.To);
        Assert.Equal("routeWorkLimit",TunnelPortalSelection.Preview(w,a,new(int.MaxValue,a.Y,a.Z),true).Reason);
        Assert.False(TunnelPortalSelection.Preview(w,new(int.MinValue,0,2),new(int.MinValue+2,0,2),true).Legal);
    }
}
