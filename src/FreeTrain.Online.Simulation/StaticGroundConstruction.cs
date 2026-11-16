// ExAV 883cf7d LandBuilderContribution, StaticLandBuilder, LandVoxel and
// World.isReusable. Original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;
namespace FreeTrain.Online.Simulation;

public static class StaticGroundConstruction
{
    public static LandTradePlan Preview(CityCandidate before,Location anchor,Location end,string action,DefinitionReference? definition)
    {
        var world=before.Construction.World;var graph=world.City!;
        var remove=action=="removeCover";
        if(action is not ("cover" or "removeCover") || end.Z!=anchor.Z ||
            world.Terrain.At(anchor).Relation!=GroundRelation.Ground || remove && definition!=null || !remove && definition==null)
            throw new ArgumentException("Invalid ground selection.");
        var d=remove?null:graph.Profile.GroundDefinition(definition!);
        if(d!=null && (d.Handler is not ("land-0d" or "random-land") || graph.Profile.Bindings.Single(b=>b.Reference==definition).PlayerCannotBuild || d.GridX!=1 || d.GridY!=1))
            throw new ArgumentException("Unavailable static ground definition.");
        var sx=Math.Abs((long)end.X-anchor.X)+1;var sy=Math.Abs((long)end.Y-anchor.Y)+1;
        if(sx>LandTrading.MaximumCells || sy>LandTrading.MaximumCells || sx*sy>LandTrading.MaximumCells)
            throw new ArgumentException("Ground selection capacity exceeded.");
        var random=d?.Handler=="random-land"?RandomStreams.Restore(before.Random):null;
        var cells=ImmutableArray.CreateBuilder<LandCell>();var reclaimed=ImmutableArray.CreateBuilder<LandReclamation>();
        var removed=new HashSet<long>();var added=new List<CityGround>();var next=world.Spatial.NextEntityId;
        for(long x=Math.Min(anchor.X,end.X);x<=Math.Max(anchor.X,end.X);x++)
        for(long y=Math.Min(anchor.Y,end.Y);y<=Math.Max(anchor.Y,end.Y);y++)
        {
            var p=new Location((int)x,(int)y,anchor.Z);var relation=world.Terrain.At(p).Relation;
            var owner=world.Spatial.At(p).Entity;
            var existing=graph.State.Ground.SingleOrDefault(g=>g.Id==owner?.Id);
            var status=relation==GroundRelation.Outside?"outside":relation!=GroundRelation.Ground?"uneven":
                remove ? existing!=null && graph.Profile.GroundDefinition(existing.Definition).Handler=="land-0d"?"removable":"notCover":
                owner==null || owner.SilentlyReclaimable?"placeable":"protected";
            DefinitionReference? selected=null;
            if(status is "placeable" or "removable")
            {
                if(owner!=null && removed.Add(owner.Id))
                {
                    if(!graph.IsSpatialOwner(owner.Id) && world.Terrain.OccupantAt(p)?.Kind!=TerrainOccupantKind.Creek)
                        throw new ArgumentException("Unavailable ground reclamation handler.");
                    reclaimed.Add(new(owner,0)); // Silent reuse/removal does not purchase the entity or land.
                }
                if(!remove){selected=random==null?definition!:d!.Selections[random["land-selection"].Next(d.Selections.Length)];added.Add(new(next,0,selected,p,true));next=checked(next+1);}
            }
            cells.Add(new(p,status,0,owner?.Id,selected));
        }
        // LandBuilderContribution books the full selected area, including skipped
        // cells. Review shows this exact source charge, even for an all-blocked rectangle.
        var amount=remove?0:checked(checked(d!.Price*sx)*sy);
        var state=CityConstruction.Without(graph.State,removed);
        state=state with {Ground=state.Ground.AddRange(added),Producers=state.Producers.AddRange(added.Where(g=>graph.Profile.GroundDefinition(g.Definition).Population!=null).Select(g=>new CityProducer(g.Id,g.Definition,1)))};
        var terrain=world.Terrain;
        if(terrain.Spatial.Entities.Any(e=>removed.Contains(e.Id)))
            terrain=terrain.Replace(terrain.Revision,terrain.Cells,terrain.WaterLevel,new(terrain.Spatial.NextEntityId,terrain.Spatial.Entities.Where(e=>!removed.Contains(e.Id)).ToImmutableArray()),terrain.Occupants.Where(o=>!removed.Contains(o.EntityId)));
        var construction=CityConstruction.Replace(before.Construction,state,next,remove?ConstructionBooking.None:ConstructionBooking.Spend(amount,AccountGenre.Subsidiaries),
            new(before.Clock.AbsoluteMinute),remove?"staticGroundRemoval":"staticGroundConstruction",terrain:terrain,reclaimed:removed,
            attachMany:added.Where(g=>graph.Profile.GroundDefinition(g.Definition).Population!=null).Select(g=>g.Id));
        return new(before,new(construction,before.Clock,random?.Capture()??before.Random),cells.ToImmutable(),reclaimed.ToImmutable(),amount);
    }
}
