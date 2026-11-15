// ExAV 883cf7d HalfVoxelContribution.create/destroy and HVStructure.add/remove.
// Original authors, LGPL-2.1-or-later. One storage voxel, one initial listener.
using System.Collections.Immutable;
namespace FreeTrain.Online.Simulation;

public static class HalfCityConstruction
{
    public static FixedCityPlan Preview(CityCandidate before,Location location,CityHalf half)
    {
        var world=before.Construction.World;var graph=world.City!;
        var d=graph.Profile.Structure(half.Definition);
        if(d.Kind!=CityStructureKind.Half || !Enum.IsDefined(half.Side) ||
            !d.HalfVariants.Contains(new(half.Direction,(int)half.Side,half.Color)) ||
            graph.Profile.Bindings.Single(b=>b.Reference==d.Reference).PlayerCannotBuild)
            throw new ArgumentException("Unavailable half selector.");
        if(world.Terrain.At(location).Relation is GroundRelation.Outside or GroundRelation.Underground)
            throw new ArgumentException("Half site outside or underground.");
        var cell=world.Spatial.At(location);
        if(cell.Entity is {} entity)
        {
            var owner=graph.State.Structures.SingleOrDefault(s=>s.Id==entity.Id);
            if(owner==null || owner.Halves.IsEmpty || owner.Halves.Any(h=>h.Side==half.Side || h.Direction%4!=half.Direction%4))
                throw new ArgumentException("Incompatible half occupancy.");
            var updated=owner with {Revision=checked(owner.Revision+1),Halves=owner.Halves.Add(half)};
            return Change(before,graph.State with {Structures=graph.State.Structures.Replace(owner,updated)},world.Spatial.NextEntityId,ConstructionBooking.None);
        }
        if(cell.Status!=VoxelStatus.Vacant) throw new ArgumentException("Occupied half site.");
        var id=world.Spatial.NextEntityId;var next=checked(id+1);
        var random=RandomStreams.Restore(before.Random);
        var delay=checked((long)(random["time-length"].NextDouble()*20160+20160));
        var order=before.Clock.NextOrder;
        var callback=new ScheduledAction(order,order,checked(before.Clock.AbsoluteMinute+delay),CityHandlers.Subsidiary(id),0);
        var subsidiary=CityValuation.Reprice(new(0,0),d.UnitPrice,CityHeat.Value(world,location)) with {Callback=callback};
        var structure=new CityStructure(id,0,d.Reference,location,d.MinimumHeight,true,subsidiary,[half],[],CityConstructionCaller.CatalogPlayer);
        var state=graph.State with {Structures=graph.State.Structures.Add(structure),
            Producers=d.Population==null?graph.State.Producers:graph.State.Producers.Add(new(id,d.Reference,d.MinimumHeight))};
        var after=CityConstruction.Replace(before.Construction,state,next,ConstructionBooking.Spend(d.UnitPrice,AccountGenre.Subsidiaries),
            new(before.Clock.AbsoluteMinute),"cityHalfCreate",attach:d.Population==null?null:id);
        return new(before,new(after,before.Clock with {NextOrder=checked(order+1),Pending=before.Clock.Pending.Add(callback).OrderBy(a=>a.DueMinute).ThenBy(a=>a.Order).ToImmutableArray()},random.Capture()));
    }
    public static FixedCityPlan PreviewRemoval(CityCandidate before,Location picked,long ownerId,long revision,HalfSide? side=null)
    {
        var world=before.Construction.World;var graph=world.City!;var owner=graph.Structure(ownerId);
        if(owner.Halves.IsEmpty || world.Spatial.At(picked).Entity?.Id!=ownerId || owner.Revision!=revision ||
            side is {} selected && (!Enum.IsDefined(selected) || !owner.Halves.Any(h=>h.Side==selected)))
            throw new ArgumentException("Stale or unavailable half owner/side.");
        if(side==null || owner.Halves.Length==1) return new(before,CityConstruction.Remove(before,ownerId));
        var updated=owner with {Revision=checked(owner.Revision+1),Halves=owner.Halves.Where(h=>h.Side!=side).ToImmutableArray()};
        return Change(before,graph.State with {Structures=graph.State.Structures.Replace(owner,updated)},world.Spatial.NextEntityId,ConstructionBooking.None);
    }
    private static FixedCityPlan Change(CityCandidate before,CityState state,long next,ConstructionBooking booking) =>
        new(before,new(CityConstruction.Replace(before.Construction,state,next,booking,new(before.Clock.AbsoluteMinute),"cityHalfEdit"),before.Clock,before.Random));
}
