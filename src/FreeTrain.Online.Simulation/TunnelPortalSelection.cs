namespace FreeTrain.Online.Simulation;

// Owner-requested browser selection aid (1 November 2026), separate from ExAV's
// exact construction/removal and booking rules. Resolve geometry once, then run
// the ordinary authoritative tunnel preview; never search around an obstruction.
public static class TunnelPortalSelection
{
    public static RailwayConstructionPlan Preview(MixedWorld world,Location from,Location to,bool automaticLevel)
    {
        try { return Resolve(world,from,to,automaticLevel); }
        catch(OverflowException) { return TunnelRailwayConstruction.Preview(world,from,to); }
    }
    private static RailwayConstructionPlan Resolve(MixedWorld world,Location from,Location to,bool automaticLevel)
    {
        to=BridgeRailwayConstruction.Snap(from,to);
        var length=Math.Max(Math.Abs((long)to.X-from.X),Math.Abs((long)to.Y-from.Y));
        if(from==to || length>=NormalRailway.MaximumRouteCells ||
            !world.Terrain.Space.ContainsVoxel(from) || !world.Terrain.Space.ContainsVoxel(to))
            return TunnelRailwayConstruction.Preview(world,from,to);
        var d=from.DirectionTo(to);var (dx,dy)=d.Offset;
        bool Portal(Location p)
        {
            if(!world.Terrain.Space.ContainsVoxel(p) || p.Z<world.Terrain.WaterLevel)return false;
            if(world.Terrain.OccupantAt(p) is {Kind:TerrainOccupantKind.Mountain} m)return !m.Corners.Flat;
            // Existing portals can anchor an extension; the exact tool still
            // requires an actual Mountain somewhere on the prospective route.
            return world.At(p)?.Traffic?.Rail?.TerrainBackup is {Flat:false};
        }
        var candidates=new List<(Location From,Location To,int HeightShift,int PointShift,int Extension)>();
        foreach(var dz in automaticLevel ? new[]{0,-1,1} : new[]{0})
        foreach(var a in new[]{0,-1,1})
        foreach(var b in new[]{0,-1,1})
        {
            var z=(long)from.Z+dz;var ax=(long)from.X+a*dx;var ay=(long)from.Y+a*dy;
            var bx=(long)to.X+b*dx;var by=(long)to.Y+b*dy;
            if(z<0 || z>=world.Terrain.Space.Levels || ax<int.MinValue || ax>int.MaxValue || ay<int.MinValue || ay>int.MaxValue ||
                bx<int.MinValue || bx>int.MaxValue || by<int.MinValue || by>int.MaxValue || length+b-a<1 || length+b-a>=NormalRailway.MaximumRouteCells)continue;
            var start=new Location((int)ax,(int)ay,(int)z);var end=new Location((int)bx,(int)by,(int)z);
            if(Portal(start)&&Portal(end))candidates.Add((start,end,Math.Abs(dz),Math.Abs(a)+Math.Abs(b),b-a));
        }
        if(candidates.Count==0)return TunnelRailwayConstruction.Preview(world,from,to);
        // Preserve the requested level first, then minimize endpoint movement.
        // Prefer extending equal-distance ties; ordering is stable in both axes.
        var selected=candidates.OrderBy(c=>c.HeightShift).ThenBy(c=>c.PointShift).ThenByDescending(c=>c.Extension)
            .ThenBy(c=>c.From.X).ThenBy(c=>c.From.Y).First();
        return TunnelRailwayConstruction.Preview(world,selected.From,selected.To);
    }
}
