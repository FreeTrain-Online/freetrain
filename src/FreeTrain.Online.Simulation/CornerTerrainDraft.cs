using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Sparse detached working state for an ordered stroke. No world-sized clone per site.
public sealed class CornerTerrainDraft(TerrainWorld original)
{
    private readonly Dictionary<int,TerrainCell> cells = [];
    private readonly Dictionary<Location,(SpatialEntity Entity,TerrainOccupant Occupant)?> entities = [];
    private long entityCount = original.Occupants.Length, treeCount = original.RealizedTreeCount;
    private long nextId = original.Spatial.NextEntityId;
    public WorldSpace Space => original.Space;
    public TerrainCell Cell(int h,int v) => cells.GetValueOrDefault(v*Space.HorizontalSize+h,original.Cell(h,v));
    public VoxelOccupancy At(Location p) => !Space.ContainsVoxel(p) ? new(VoxelStatus.Outside,null,true) :
        entities.TryGetValue(p,out var value) ? value is {} pair ? new(VoxelStatus.Occupied,pair.Entity,pair.Entity.Footprint.Members[0].Transparent) :
            new(VoxelStatus.Vacant,null,true) : original.Spatial.At(p);
    public TerrainOccupant? OccupantAt(Location p) => entities.TryGetValue(p,out var value) ? value?.Occupant : original.OccupantAt(p);
    public int FineHeight(Location p,int corner)
    {
        if (!Space.ContainsHorizontal(p)) return -1;
        var s=Space.ToStorage(p);int ground=Cell(s.H,s.V).Ground;
        var o=OccupantAt(p with{Z=ground});
        return 4*ground+(o?.Kind==TerrainOccupantKind.Mountain?o.Corners[corner]:0);
    }
    public CornerTerrainPlan Preview(Location position,string action)
    {
        var plan=CornerTerrain.Preview(this,position,action);
        if(!plan.Legal)return plan;
        long count=entityCount,trees=treeCount,added=0;
        foreach(var c in plan.Changes)
        {
            if(c.Remove&&c.EntityId!=null){count--;trees-=OccupantAt(c.Position)?.Forest?.Trees.Length??0;}
            else if(!c.Remove&&c.EntityId==null){count++;added++;}
            else if(!c.Remove&&At(c.Position).Entity?.Revision==long.MaxValue)
                return plan with{Reason="capacityLimit",Changes=[]};
        }
        if(original.Revision==long.MaxValue||nextId>long.MaxValue-added||
            WorldCreationProfile.RealizedTerrainBytes(original.Cells.Length,count,trees)>WorldCreationProfile.MaximumRealizedTerrainBytes)
            return plan with{Reason="capacityLimit",Changes=[]};
        return plan;
    }
    public void Apply(CornerTerrainPlan plan)
    {
        var actual=Preview(plan.Position,plan.Action);
        if (!plan.Legal || !actual.Legal || !plan.Changes.SequenceEqual(actual.Changes)) throw new ArgumentException("Invalid corner plan.");
        foreach(var c in plan.Changes)
        {
            var s=Space.ToStorage(c.Position);int i=s.V*Space.HorizontalSize+s.H;
            cells[i]=Cell(s.H,s.V) with{Ground=c.AfterGround};
            var previous=At(c.Position).Entity;var occupant=OccupantAt(c.Position);
            if(c.Remove){if(previous!=null){entityCount--;treeCount-=occupant?.Forest?.Trees.Length??0;}entities[c.Position]=null;continue;}
            var footprint=new Footprint([new(new(0,0,0),c.After.Flat)]);
            if(previous==null)
            {
                var definition=original.Profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId).Reference;
                entityCount++;
                var entity=new SpatialEntity(nextId++,0,definition,c.Position,footprint,false,false,0);
                entities[c.Position]=(entity,new(entity.Id,TerrainOccupantKind.Mountain,c.After));
            }
            else
            {
                // A whole atomic stroke increments retained IDs once, irrespective of visits.
                long revision=previous.Id<original.Spatial.NextEntityId ? checked(original.Spatial.At(c.Position).Entity!.Revision+1) : 0;
                entities[c.Position]=(previous with{Revision=revision,Footprint=footprint},occupant! with{Corners=c.After});
            }
        }
    }
    public TerrainWorld Finish()
    {
        var grid=original.Cells.ToBuilder();foreach(var (i,cell) in cells)grid[i]=cell;
        var removed=entities.Keys.Select(p=>original.Spatial.At(p).Entity?.Id).Where(id=>id!=null).Select(id=>id!.Value).ToHashSet();
        var changed=entities.Values.Where(v=>v!=null).Select(v=>v!.Value).ToArray();
        return original.Replace(original.Revision,grid,original.WaterLevel,
            new(nextId,original.Spatial.Entities.Where(e=>!removed.Contains(e.Id)).Concat(changed.Select(p=>p.Entity)).ToImmutableArray()),
            original.Occupants.Where(o=>!removed.Contains(o.EntityId)).Concat(changed.Select(p=>p.Occupant)));
    }
}
