// ExAV 883cf7d/trunk/core/controllers/land/LandPropertyController.cs and
// RectSelectorController.cs. Original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;
namespace FreeTrain.Online.Simulation;

public sealed record LandCell(Location Position, string Status, long Value, long? OwnerId);
public sealed record LandReclamation(SpatialEntity Owner, long RemovalCost);
public sealed record LandTradePlan(CityCandidate Source, CityCandidate Candidate,
    ImmutableArray<LandCell> Cells, ImmutableArray<LandReclamation> Reclaimed, long Amount);

public static class LandTrading
{
    public const int MaximumCells = 4096;
    // Source normalizes XY but retains the FIRST click's ground level. Later
    // cells can be elevated or underground; water is not a source SeaVoxel.
    public static LandTradePlan Preview(CityCandidate before, Location anchor, Location end, string action)
    {
        var world=before.Construction.World;var graph=world.City!;
        if(action is not ("buy" or "sell" or "inspect") || !world.Terrain.Space.ContainsVoxel(anchor) ||
            action!="inspect" && world.Terrain.At(anchor).Relation!=GroundRelation.Ground || end.Z!=anchor.Z ||
            action=="inspect" && end!=anchor)
            throw new ArgumentException("Invalid land selection anchor/level.");
        var sx=Math.Abs((long)end.X-anchor.X)+1;var sy=Math.Abs((long)end.Y-anchor.Y)+1;
        if(sx>MaximumCells || sy>MaximumCells || sx*sy>MaximumCells) throw new ArgumentException("Land selection capacity exceeded.");
        var cells=ImmutableArray.CreateBuilder<LandCell>();var reclaimed=new List<LandReclamation>();
        var traversal=new LandTradeTraversal(action=="sell");var removed=traversal.Removed;var properties=graph.State.Properties.ToBuilder();
        var next=world.Spatial.NextEntityId;
        for(long x=Math.Min(anchor.X,end.X);x<=Math.Max(anchor.X,end.X);x++)
        for(long y=Math.Min(anchor.Y,end.Y);y<=Math.Max(anchor.Y,end.Y);y++)
        {
            var p=new Location((int)x,(int)y,anchor.Z);var relation=world.Terrain.At(p).Relation;
            if(relation==GroundRelation.Outside){cells.Add(new(p,"outside",0,null));continue;}
            var value=CityHeat.Value(world,p);var owner=world.Spatial.At(p).Entity;
            if(owner!=null && removed.Contains(owner.Id)) owner=null; // source reread after WHOLE entity removal
            if(action=="inspect") {cells.Add(new(p,graph.State.Properties.Any(o=>o.Id==owner?.Id)?"owned":owner==null?"vacant":"protected",value,owner?.Id));continue;}
            if(relation==GroundRelation.Underground){cells.Add(new(p,"underground",value,owner?.Id));continue;}
            var property=graph.State.Properties.Any(o=>o.Id==owner?.Id);
            var status=traversal.Visit(value,owner==null?null:new(owner.Id,owner.Owned,owner.SilentlyReclaimable,owner.Value,property),id=>
            {
                var entity=owner!;
                if(!graph.IsSpatialOwner(id) && world.Terrain.OccupantAt(p)?.Kind!=TerrainOccupantKind.Creek)
                    throw new ArgumentException("Unavailable land reclamation handler.");
                var structure=graph.State.Structures.SingleOrDefault(s=>s.Id==id);
                var booking=structure==null?ConstructionBooking.None:CityConstruction.RemovalBooking(graph.Profile.Structure(structure.Definition),!structure.Construction.IsEmpty);
                reclaimed.Add(new(entity,booking.Total));return booking;
            });
            if(status=="sellable") properties.Remove(properties.Single(o=>o.Id==owner!.Id));
            if(status=="buyable"){properties.Add(new(next,0,p));next=checked(next+1);}
            cells.Add(new(p,status,value,status=="buyable"?null:owner?.Id));
        }
        if(action=="inspect" || !cells.Any(c=>c.Status is "buyable" or "sellable"))
            return new(before,before,cells.ToImmutable(),reclaimed.ToImmutableArray(),traversal.Amount);
        var reserved=world.Voxels.Where(v=>v.Construction!=null).Select(v=>v.OwnerId).Distinct().Count();
        if(traversal.Charges.Count+traversal.Revenues.Count>NormalRailwayConstruction.MaximumAccountTransactions-before.Construction.Accounts.Transactions.Length-reserved)
            throw new ArgumentException("Land account capacity exceeded.");
        var state=CityConstruction.Without(graph.State,removed) with {Properties=properties.ToImmutable()};
        var terrain=world.Terrain;
        if(terrain.Spatial.Entities.Any(e=>removed.Contains(e.Id)))
            terrain=terrain.Replace(terrain.Revision,terrain.Cells,terrain.WaterLevel,
                new(terrain.Spatial.NextEntityId,terrain.Spatial.Entities.Where(e=>!removed.Contains(e.Id)).ToImmutableArray()),terrain.Occupants.Where(o=>!removed.Contains(o.EntityId)));
        var operation=action=="buy"?"landPurchase":"landSale";var time=new GameTime(before.Clock.AbsoluteMinute);
        var construction=CityConstruction.Replace(before.Construction,state,next,new(traversal.Charges),time,operation,terrain:terrain,reclaimed:removed);
        if(action=="sell") construction=new(construction.World,construction.Accounts.EarnMany(traversal.Revenues,construction.World.Revision,time,operation,AccountGenre.Subsidiaries));
        var registrations=graph.State.Structures.Where(s=>removed.Contains(s.Id)).SelectMany(s=>s.Construction.Where(c=>c.Callback!=null).Select(c=>c.Callback!.Registration)
            .Concat(s.Subsidiary?.Callback is {} a?[a.Registration]:Array.Empty<long>())).ToHashSet();
        var candidate=new CityCandidate(construction,before.Clock with {Pending=before.Clock.Pending.Where(a=>!registrations.Contains(a.Registration)).ToImmutableArray()},before.Random);
        return new(before,candidate,cells.ToImmutable(),reclaimed.ToImmutableArray(),traversal.Amount);
    }
    public static MixedSessionState Commit(MixedSessionState before,LandTradePlan plan)
    {
        if(!ReferenceEquals(before.Construction,plan.Source.Construction) || before.Clock.Time.AbsoluteMinute!=plan.Source.Clock.AbsoluteMinute || before.Clock.NextOrder!=plan.Source.Clock.NextOrder)
            throw new InvalidOperationException("staleRevision");
        var after=plan.Candidate;
        return before.Successor(before.Revision,after.Construction,before.ConstructionMode,after.Random,after.Clock);
    }
}
