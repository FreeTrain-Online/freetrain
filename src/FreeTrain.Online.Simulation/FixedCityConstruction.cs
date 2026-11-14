// ExAV 883cf7d MultiSelectorController, FixedSizeStructController and Commercial.
// Original authors, LGPL-2.1-or-later. P7-07 domain plans; browser controls P7-09.
namespace FreeTrain.Online.Simulation;

public sealed class FixedCityPlan
{
    public CityCandidate Source { get; }
    public CityCandidate Candidate { get; }
    internal FixedCityPlan(CityCandidate source,CityCandidate candidate) { Source=source; Candidate=candidate; }
}

public static class FixedCityConstruction
{
    // Staging runs only on immutable geometry and copied clock/random state.
    // The retained candidate is the exact result committed after review.
    public static FixedCityPlan Preview(CityCandidate before,Location location,DefinitionReference definition,
        CityConstructionCaller caller = CityConstructionCaller.CatalogPlayer)
    {
        var world=before.Construction.World;
        var graph=world.City!;
        var d=graph.Profile.Structure(definition);
        if(d.Kind!=CityStructureKind.Fixed || !Enum.IsDefined(caller)) throw new ArgumentException("Fixed city definition/caller required.");
        var binding=graph.Profile.Bindings.Single(b=>b.Reference==definition);
        if(caller==CityConstructionCaller.Automated ? d.ComputerCannotBuild : binding.PlayerCannotBuild)
            throw new ArgumentException("City definition is unavailable to caller.");
        var reclaimed=new List<long>();
        foreach(var p in new Cube(location,d.SizeX,d.SizeY,d.MinimumHeight).Locations())
        {
            var terrain=world.Terrain.At(p);
            var cell=world.Spatial.At(p);
            // Source underground voxels and OutOfWorldVoxel are occupied too.
            if(terrain.Relation is GroundRelation.Outside or GroundRelation.Underground)
                throw new ArgumentException("City footprint is outside or underground.");
            if(caller==CityConstructionCaller.CatalogPlayer)
            {
                if(cell.Status!=VoxelStatus.Vacant) throw new ArgumentException("Catalog city footprint must be empty.");
            }
            else if(cell.Entity is {} owner)
            {
                if(caller==CityConstructionCaller.Automated ? !owner.Owned : !owner.SilentlyReclaimable)
                    throw new ArgumentException("Protected city footprint.");
                if(!graph.IsSpatialOwner(owner.Id) && world.Terrain.OccupantAt(p)?.Kind!=TerrainOccupantKind.Creek)
                    throw new ArgumentException("Unavailable city reclamation handler.");
                if(!reclaimed.Contains(owner.Id)) reclaimed.Add(owner.Id);
            }
        }
        // The generic selector checks volume only; Commercial's legacy/com
        // predicate also checks level ground and excludes MountainVoxel.
        if(caller!=CityConstructionCaller.CatalogPlayer)
            foreach(var p in new Cube(location,d.SizeX,d.SizeY,1).Locations())
                if(world.Terrain.At(p).Relation!=GroundRelation.Ground || world.Terrain.OccupantAt(p)?.Kind==TerrainOccupantKind.Mountain)
                    throw new ArgumentException("City placement is not on level ground.");
        return new(before,CityConstruction.StartCleared(before,location,d,d.MinimumHeight,caller,reclaimed));
    }

    public static FixedCityPlan PreviewRemoval(CityCandidate before,Location picked,long ownerId,long expectedOwnerRevision)
    {
        var world=before.Construction.World;
        var owner=world.City!.Structure(ownerId);
        if(world.City.Profile.Structure(owner.Definition).Kind!=CityStructureKind.Fixed ||
            world.Spatial.At(picked).Entity?.Id!=ownerId || owner.Revision!=expectedOwnerRevision)
            throw new ArgumentException("Stale or invalid fixed city owner.");
        return new(before,CityConstruction.Remove(before,ownerId));
    }

    public static MixedSessionState Commit(MixedSessionState before,FixedCityPlan plan)
    {
        if(!ReferenceEquals(before.Construction,plan.Source.Construction) ||
            before.Clock.Time.AbsoluteMinute!=plan.Source.Clock.AbsoluteMinute ||
            before.Clock.NextOrder!=plan.Source.Clock.NextOrder)
            throw new InvalidOperationException("staleRevision");
        var after=plan.Candidate;
        return before.Successor(before.Revision,after.Construction,before.ConstructionMode,after.Random,after.Clock);
    }
}
