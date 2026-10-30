// Source: ExAV 2.1.1.12 plugins/foobarbazer.Mountain2/src/Mountain2Controller.cs.
// Original authors, LGPL-2.1-or-later. Portable atomic policy: doc/web-parity-rectangle-terrain.md.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class RectangleTerrain
{
    // Source SE shared vertex, x-major/y-minor application order.
    private static readonly (int X,int Y,int Corner)[] touching = [(0,0,1),(0,1,0),(1,0,2),(1,1,3)];
    public static CornerTerrainPlan[] Plan(CornerTerrainDraft world,Location start,Location end,string action,bool soft=false)
    {
        if (action is not ("raise" or "lower" or "repair") || start.Z != end.Z ||
            !world.Space.ContainsVoxel(start) || !world.Space.ContainsVoxel(end)) throw new ArgumentException("invalidInput");
        int x1=Math.Min(start.X,end.X),x2=Math.Max(start.X,end.X),y1=Math.Min(start.Y,end.Y),y2=Math.Max(start.Y,end.Y);
        // Bounded review, work and response, independent of world size. Check before iteration.
        if (((long)x2-x1+1)*((long)y2-y1+1)>64) throw new ArgumentException("invalidInput");
        // A skew-grid bounding rectangle can include outside cells even when both endpoints exist.
        for(int x=x1;x<=x2;x++)for(int y=y1;y<=y2;y++)
            if(!world.Space.ContainsVoxel(new(x,y,start.Z)))throw new ArgumentException("invalidInput");
        if(action!="repair" && (x1==x2 || y1==y2))return [new(new(x1,y1,start.Z),action,"noChange",[])];
        var plans=new List<CornerTerrainPlan>();
        for(int x=x1;x<=(action=="repair"?x2:x2-1);x++)
            for(int y=y1;y<=(action=="repair"?y2:y2-1);y++)
            {
                var p=new Location(x,y,start.Z);
                var plan=world.Admit(action=="repair"?Repair(world,p):Edit(world,p,action,soft,x2,y2));
                if(plan.Legal)world.ApplyChanges(plan);
                plans.Add(plan);
            }
        return plans.ToArray();
    }
    private static int Ground(CornerTerrainDraft w,Location p)
    {
        if(!w.Space.ContainsHorizontal(p))return -1;
        var s=w.Space.ToStorage(p);return w.Cell(s.H,s.V).Ground;
    }
    private static int[] Heights(CornerTerrainDraft w,Location p,int c)
    {
        var d=new Heading(c*2+1);
        return [w.FineHeight(p,c),w.FineHeight(p.Move(d),(c+2)%4),
            w.FineHeight(p.Move(d.Left),(c+1)%4),w.FineHeight(p.Move(d.Right),(c+3)%4)];
    }
    private static bool Matched(CornerTerrainDraft w,Location p,int c)=>Heights(w,p,c).Where(h=>h>=0).Distinct().Count()<=1;
    private static CornerTerrainPlan Edit(CornerTerrainDraft w,Location p,string action,bool soft,int maxX,int maxY)
    {
        CornerTerrainPlan Reject(string reason)=>new(p,action,reason,[]);
        // Mountain2's outer ring starts one cell SOUTH of the base (ordinary corner starts at base).
        var ring=p.Move(Heading.South);var direction=Heading.North;
        for(int i=0;i<4;i++)
        {
            if(!Matched(w,ring,direction.Left.Index/2))return Reject("unmatchedCorners");
            ring=ring.Move(direction);direction=direction.Right90;
        }
        if(!Matched(w,p,1))return Reject("unmatchedCorners");
        int working=p.Z,target=4;
        if(action=="raise")
        {
            if(p.Z==w.Space.Levels-1)return Reject("roof");
            if(soft)
            {
                var mountain=w.OccupantAt(p);
                int count=mountain?.Kind==TerrainOccupantKind.Mountain?Enumerable.Range(0,4).Count(c=>mountain.Corners[c]>=2):0;
                bool Edge(Location q)=>Ground(w,q)<p.Z || w.At(q).Status!=VoxelStatus.Vacant && w.OccupantAt(q)?.Kind!=TerrainOccupantKind.Mountain;
                target=count<2 || p.X==maxX-1 || p.Y==maxY-1 || Edge(p with{X=p.X+2}) || Edge(p with{Y=p.Y+2})?2:4;
            }
        }
        else
        {
            if(p.Z==0)return Reject("floor");
            if(w.OccupantAt(p)?.Kind==TerrainOccupantKind.Mountain)return Reject("cornerLimit");
            if(Ground(w,p)!=p.Z && Ground(w,p)!=p.Z-1)return Reject("groundLevel");
            working--;target=0;
        }
        var changes=ImmutableArray.CreateBuilder<CornerTerrainChange>();
        foreach(var t in touching)
        {
            var q=new Location(p.X+t.X,p.Y+t.Y,working);
            if(!w.Space.ContainsVoxel(q))return Reject("outside");
            var occupant=w.OccupantAt(q);var entity=w.At(q).Entity;
            var mountain=occupant?.Kind==TerrainOccupantKind.Mountain?occupant:null;
            int ground=Ground(w,q),afterGround=ground;QuarterCorners before=mountain?.Corners??default,offsets=before;
            if(action=="raise")
            {
                if(ground!=p.Z)return Reject("groundLevel");
                if(entity!=null && mountain==null)return Reject("occupied");
                if(before[t.Corner]==4)return Reject("cornerLimit");
            }
            else if(mountain==null)
            {
                if(w.At(q).Status!=VoxelStatus.Vacant || w.At(q with{Z=working+1}).Status!=VoxelStatus.Vacant)return Reject("occupied");
                if(working>=ground)continue;
                if(ground!=working+1)return Reject("groundLevel");
                afterGround--;offsets=new(4,4,4,4);
            }
            var after=Set(offsets,t.Corner,target);
            bool remove=action=="raise"?after.Saturated:after.Flat;
            if(action=="raise" && remove){afterGround++;after=default;}
            var change=Change(w,q,ground,afterGround,before,after,remove);
            if(ground!=afterGround && Cell(w,q).Surface==TerrainSurface.Terrace)return Reject("occupied");
            if(ground!=afterGround || before!=after || remove && entity!=null)changes.Add(change);
        }
        return changes.Count==0?Reject("noChange"):new(p,action,null,changes.ToImmutable());
    }
    private static TerrainCell Cell(CornerTerrainDraft w,Location p){var s=w.Space.ToStorage(p);return w.Cell(s.H,s.V);}
    private static QuarterCorners Set(QuarterCorners offsets,int corner,int target)
    {
        int[] values=[offsets.NE,offsets.SE,offsets.SW,offsets.NW];values[corner]=target;
        return new(values[0],values[1],values[2],values[3]);
    }
    private static CornerTerrainChange Change(CornerTerrainDraft w,Location p,int ground,int afterGround,QuarterCorners before,QuarterCorners after,bool remove)
    {
        var e=w.At(p).Entity;var o=w.OccupantAt(p);
        return new(p,ground,afterGround,before,after,e?.Id,remove,remove&&o?.Forest!=null,remove&&e?.Owned==true);
    }
    private static CornerTerrainPlan Repair(CornerTerrainDraft w,Location selected)
    {
        CornerTerrainPlan Reject(string reason)=>new(selected,"repair",reason,[]);
        int ground=Ground(w,selected);var p=selected with{Z=ground};
        var o=w.OccupantAt(p);var before=o?.Kind==TerrainOccupantKind.Mountain?o.Corners:default;
        var after=before;bool changed=false;
        for(int c=0;c<4;c++)
        {
            var heights=Heights(w,p,c);
            if(heights.Where(h=>h>=0).Distinct().Count()<=1)continue;
            if(w.At(p).Entity!=null && o?.Kind!=TerrainOccupantKind.Mountain)return Reject("occupied");
            var neighbors=heights.Skip(1).Where(h=>h>=0).Distinct().ToArray();
            if(neighbors.Length!=1)return Reject("inconsistentNeighbors");
            int target=neighbors[0]-4*ground;
            if(target is <0 or >4)return Reject("unrepresentableHeight");
            if(4*ground+target>=4*w.Space.Levels)return Reject("roof");
            after=Set(after,c,target);changed=true;
        }
        if(!changed)return Reject("noChange");
        int afterGround=ground;bool remove=after.Flat||after.Saturated;
        if(after.Saturated){afterGround++;after=default;}
        if(afterGround!=ground && Cell(w,p).Surface==TerrainSurface.Terrace)return Reject("occupied");
        return new(selected,"repair",null,[Change(w,p,ground,afterGround,before,after,remove)]);
    }
}
