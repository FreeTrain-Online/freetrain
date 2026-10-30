// Source: ExAV 2.1.1.12 terrace plugin's four strategies and TerraceVoxel.
// Original authors, LGPL-2.1-or-later. See doc/web-parity-terraces.md.
namespace FreeTrain.Online.Simulation;

public static class TerraceTerrain
{
    public static bool IsAction(string action) => action is "placeTerrace" or "removeTerrace" or "placeCut" or "removeCut";
    // Browser batch policy: inclusive normalized selection, source per-cell rules,
    // bounded work and explicit skips, published as one atomic terrain edit.
    public static CornerTerrainPlan[] PlaceRectangle(CornerTerrainDraft world, Location start, Location end)
    {
        if(start.Z!=end.Z||!world.Space.ContainsVoxel(start)||!world.Space.ContainsVoxel(end)||
            (Math.Abs((long)end.X-start.X)+1)*(Math.Abs((long)end.Y-start.Y)+1)>64)
            throw new ArgumentException("invalidInput");
        var plans=new List<CornerTerrainPlan>();
        for(int x=Math.Min(start.X,end.X);x<=Math.Max(start.X,end.X);x++)
        for(int y=Math.Min(start.Y,end.Y);y<=Math.Max(start.Y,end.Y);y++)
        {
            var plan=Preview(world,new(x,y,start.Z),"placeTerrace");
            if(plan.Legal)Apply(world,plan);
            plans.Add(plan);
        }
        return plans.ToArray();
    }
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
                if(action=="placeTerrace")
                {
                    // Owner-requested authoring extension: bare ground has flat
                    // backup corners and needs no preparatory Mountain stroke.
                    // The column model supports one backed-up terrace per cell.
                    if(cell.Surface==TerrainSurface.Terrace||entity!=null&&occupant?.Kind!=TerrainOccupantKind.Mountain)
                        return Reject("occupied");
                    if(ground>=world.Space.Levels-1)return Reject("roof");
                    if(world.At(p with{Z=p.Z+1}).Status!=VoxelStatus.Vacant)return Reject("occupied");
                    ground++;
                }
                else
                {
                    if(occupant?.Kind!=TerrainOccupantKind.Mountain)return Reject("mountainRequired");
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
