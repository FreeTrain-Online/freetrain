// Source: ExAV 2.1.1.12 terrace plugin's four strategies and TerraceVoxel.
// Original authors, LGPL-2.1-or-later. See doc/web-parity-terraces.md.
namespace FreeTrain.Online.Simulation;

public static class TerraceTerrain
{
    public static bool IsAction(string action) => action is "placeTerrace" or "removeTerrace" or "placeCut" or "removeCut";
    public static CornerTerrainPlan Preview(TerrainWorld world, Location p, string action) => Preview(new CornerTerrainDraft(world),p,action);
    public static CornerTerrainPlan Preview(CornerTerrainDraft world, Location p, string action)
    {
        CornerTerrainPlan Reject(string reason) => new(p,action,reason,[]);
        if(!IsAction(action))return Reject("invalidInput");
        if(!world.Space.ContainsVoxel(p))return Reject("outside");
        var s=world.Space.ToStorage(p);var cell=world.Cell(s.H,s.V);
        var occupant=world.OccupantAt(p);var entity=world.At(p).Entity;
        QuarterCorners before=occupant?.Corners??default,after=before;
        int ground=cell.Ground;bool remove=false;
        if(action=="removeTerrace")
        {
            if(occupant?.Kind!=TerrainOccupantKind.Terrace)return Reject("terraceRequired");
            if(world.At(p with{Z=p.Z+1}).Status!=VoxelStatus.Vacant)return Reject("occupied");
            ground--;
        }
        else
        {
            if(p.Z!=ground)return Reject("groundLevel");
            if(action=="removeCut")
            {
                if(entity!=null)return Reject("occupied");
                // Source getHeight samples at the selected Z, even if neighboring ground differs.
                int Sample(Location q,int corner)=>world.OccupantAt(q) is {Kind:TerrainOccupantKind.Mountain} o?o.Corners[corner]:0;
                int[] dx=[1,1,0,0],dy=[-1,0,0,-1];int[] offsets=new int[4];
                for(int i=0;i<4;i++)
                {
                    var q=new Location(p.X+dx[i],p.Y+dy[i],p.Z);
                    offsets[i]=Math.Max(Math.Max(Sample(q,2),Sample(q.Move(Heading.South),3)),
                        Math.Max(Sample(q.Move(Heading.SouthWest),0),Sample(q.Move(Heading.West),1)));
                }
                after=new(offsets[0],offsets[1],offsets[2],offsets[3]);
                if(after.Flat)return Reject("noChange");
            }
            else
            {
                if(occupant?.Kind!=TerrainOccupantKind.Mountain)return Reject("mountainRequired");
                if(action=="placeTerrace")
                {
                    if(ground>=world.Space.Levels-1)return Reject("roof");
                    if(world.At(p with{Z=p.Z+1}).Status!=VoxelStatus.Vacant)return Reject("occupied");
                    ground++;
                }
                else
                {
                    for(int i=0;i<4;i++)
                    {
                        var q=p.Move(new Heading(i*2));
                        if(world.Space.ContainsHorizontal(q)) { var t=world.Space.ToStorage(q);if(world.Cell(t.H,t.V).Ground>ground)return Reject("higherNeighbor"); }
                    }
                    after=default;remove=true;
                }
            }
        }
        if(action is "removeTerrace" or "removeCut")
            for(int i=0;i<4;i++)if(4*ground+after[i]>=4*world.Space.Levels)return Reject("roof");
        if(!remove&&!world.CanAllocateTerrainEntity)return Reject("capacityLimit");
        return world.Admit(new(p,action,null,[new(p,cell.Ground,ground,before,after,entity?.Id,remove,
            occupant?.Forest!=null,entity?.Owned==true)]));
    }
    public static void Apply(CornerTerrainDraft draft,CornerTerrainPlan plan)
    {
        var actual=Preview(draft,plan.Position,plan.Action);
        if(!plan.Legal||!actual.Legal||!plan.Changes.SequenceEqual(actual.Changes))throw new ArgumentException("Invalid terrace plan.");
        draft.ReplaceTerrainSite(plan.Changes[0],plan.Action=="placeTerrace"?TerrainOccupantKind.Terrace:TerrainOccupantKind.Mountain,
            plan.Action=="placeTerrace"?TerrainSurface.Terrace:TerrainSurface.Cut);
    }
}
