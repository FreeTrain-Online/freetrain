using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Object demolition is distinct from rail route editing. A slope is always a
// complete owner; traffic removal also runs the existing lower-pier callbacks.
public static class ObjectRemoval
{
    // Round each major-axis step to its nearest voxel. Integer interpolation
    // keeps the path close to the straight segment and identical when reversed.
    public static ImmutableArray<Location> Line(Location from, Location to)
    {
        if (from.Z != to.Z) throw new ArgumentException("noncoplanar");
        var length = Math.Max(Math.Abs((long)to.X-from.X),Math.Abs((long)to.Y-from.Y));
        if (length >= NormalRailway.MaximumRouteCells) throw new ArgumentException("routeWorkLimit");
        if (length == 0) return [from];
        int Interpolate(int start,int end,long i)
        {
            var value=(long)start*(length-i)+(long)end*i;
            var whole=Math.DivRem(value,length,out var remainder);
            if (remainder < 0) {whole--;remainder+=length;}
            return checked((int)(whole+(2*remainder>=length?1:0)));
        }
        return Enumerable.Range(0,(int)length+1).Select(i=>new Location(Interpolate(from.X,to.X,i),Interpolate(from.Y,to.Y,i),from.Z)).ToImmutableArray();
    }

    public static (MixedWorld? Candidate, string? Reason, ImmutableArray<(Location Position,string Reason)> Skipped)
        RailwayLine(MixedWorld world, ImmutableArray<Location> path)
    {
        if (path.IsEmpty || path.Length > NormalRailway.MaximumRouteCells) throw new ArgumentException("routeWorkLimit");
        if (path.Any(p=>!world.Terrain.Space.ContainsVoxel(p))) return (null,"outside",[]);
        var removed=new HashSet<long>();var inspected=new HashSet<long>();
        var skipped=ImmutableArray.CreateBuilder<(Location,string)>();
        var members=world.Voxels.GroupBy(v=>v.OwnerId).ToDictionary(g=>g.Key,g=>g.ToArray());
        foreach(var p in path)
        {
            var voxel=world.At(p);
            if (voxel?.Traffic?.Rail == null || !inspected.Add(voxel.OwnerId)) continue;
            var owner=members[voxel.OwnerId];
            var reason=owner.Any(v=>v.Traffic?.Car!=null)?"occupiedCar":
                owner.Any(v=>v.Traffic?.Platform!=null)?"platformDependency":
                owner.Any(v=>v.Traffic?.Rail?.TerrainBackup!=null)?"operationUnavailable":null;
            if (reason != null) {skipped.Add((p,reason));continue;}
            removed.Add(voxel.OwnerId);
            foreach(var rail in owner.Where(v=>v.Traffic?.Rail!=null))
                foreach(var pier in RailwaySupports.Teardown(world,rail.Position)) removed.Add(pier.OwnerId);
        }
        if (removed.Count == 0) return (null,skipped.Count>0?"nothingRemovable":"noRailway",skipped.ToImmutable());
        var candidate=world.Replace(world.Revision,world.Terrain,
            new(world.Spatial.NextEntityId,world.Spatial.Entities.Where(e=>!removed.Contains(e.Id)).ToImmutableArray()),
            world.Voxels.Where(v=>!removed.Contains(v.OwnerId)),world.Slopes.Where(s=>!removed.Contains(s.EntityId)));
        return (candidate,null,skipped.ToImmutable());
    }

    public static (TerrainWorld? Candidate, string Kind, string? Reason) Terrain(TerrainWorld world, Location p, long? nextEntityId = null)
    {
        var entity = world.Spatial.At(p).Entity;
        var occupant = world.OccupantAt(p);
        if (entity == null || occupant == null) return (null,"terrain","terrainRequired");
        if (occupant.Kind == TerrainOccupantKind.Terrace)
        {
            var draft = new CornerTerrainDraft(world,nextEntityId);
            var plan = TerraceTerrain.Preview(draft,p,"removeTerrace");
            if (!plan.Legal) return (null,"terrace",plan.Reason);
            TerraceTerrain.Apply(draft,plan);
            return (draft.Finish(),"terrace",null);
        }
        if (occupant.Kind == TerrainOccupantKind.Mountain && occupant.Forest == null)
            return (null,"terrain","terrainRequired");
        var kind = occupant.Kind == TerrainOccupantKind.Creek ? "creek" : "forest";
        // Forests are decorations on Mountain geometry. Keep non-flat corners,
        // identity and ownership, and remove only the forest/value payload.
        (SpatialEntity Entity,TerrainOccupant Occupant)? retained = kind == "forest" && !occupant.Corners.Flat
            ? (entity with {Revision=checked(entity.Revision+1),Value=0},occupant with {Forest=null}) : null;
        var candidate = new TerrainWorld(world,new Dictionary<int,TerrainCell>(),
            new Dictionary<Location,(SpatialEntity Entity,TerrainOccupant Occupant)?> {[p]=retained},
            Math.Max(world.Spatial.NextEntityId,nextEntityId ?? world.Spatial.NextEntityId),
            world.RealizedTreeCount-(occupant.Forest?.Trees.Length ?? 0));
        return (candidate,kind,null);
    }

    public static (MixedWorld? Candidate, string Kind, string? Reason) Mixed(MixedWorld world, Location p)
    {
        if (world.At(p) is not {} voxel)
        {
            var (terrain,kind,reason) = Terrain(world.Terrain,p,world.Spatial.NextEntityId);
            if (terrain == null) return (null,kind,reason);
            // A terrace's upper cell can be occupied by railway in a mixed world.
            if (kind == "terrace" && world.Spatial.At(p with {Z=p.Z+1}).Status != VoxelStatus.Vacant)
                return (null,kind,"obstacle");
            var terrainIds = world.Terrain.Spatial.Entities.Select(e=>e.Id).ToHashSet();
            var entities = world.Spatial.Entities.Where(e=>!terrainIds.Contains(e.Id)).Concat(terrain.Spatial.Entities).OrderBy(e=>e.Id).ToImmutableArray();
            return (world.Replace(world.Revision,terrain,new(Math.Max(world.Spatial.NextEntityId,terrain.Spatial.NextEntityId),entities),world.Voxels,world.Slopes),kind,null);
        }
        if (SlopeRailwayConstruction.Resolve(world,p) != null)
        {
            var plan = SlopeRailwayConstruction.PreviewEntityRemoval(world,p);
            return (plan.Candidate,"slope",plan.Reason);
        }
        var objectKind = voxel.Kind == RailwayVoxelKind.Pier ? "pier" : "railway";
        if (voxel.Traffic?.Car != null) return (null,objectKind,"occupiedCar");
        if (voxel.Traffic?.Platform != null) return (null,objectKind,"platformDependency");
        // Tunnel restoration requires its dedicated terrain-backup handler.
        if (voxel.Traffic?.Rail?.TerrainBackup != null) return (null,objectKind,"operationUnavailable");
        var removed = new HashSet<long> {voxel.OwnerId};
        if (voxel.Traffic?.Rail != null)
            foreach (var pier in RailwaySupports.Teardown(world,p)) removed.Add(pier.OwnerId);
        return (world.Replace(world.Revision,world.Terrain,
            new(world.Spatial.NextEntityId,world.Spatial.Entities.Where(e=>!removed.Contains(e.Id)).ToImmutableArray()),
            world.Voxels.Where(v=>!removed.Contains(v.OwnerId)),world.Slopes),objectKind,null);
    }
}
