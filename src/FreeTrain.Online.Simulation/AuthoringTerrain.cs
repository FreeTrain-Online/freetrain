// Source ForestBuilder/CreekBuilder, ExAV 883cf7d (Kohsuke Kawaguchi et al., LGPL-2.1-or-later).
// Authoring uses the source's explicit unowned initialization branch (zero booking).
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record AuthoringDecoration(Location Position, ForestDecoration? Forest, int CreekConnections);
public sealed record AuthoringTerrainPlan(CornerTerrainDraft Draft, ImmutableArray<CornerTerrainPlan> Sites,
    ImmutableArray<AuthoringDecoration> Decorations);

public static class AuthoringTerrain
{
    public static bool IsAction(string action) => action is "plantForest" or "placeCreek";
    public static ImmutableArray<(Location Position,int Connections)> CreekRoute(Location start,Location end)
    {
        if(start.Z!=end.Z||(long)Math.Abs((long)end.X-start.X)+Math.Abs((long)end.Y-start.Y)>=64)
            throw new ArgumentException("Invalid creek extent.");
        var random=RandomStreams.ForCreek(start.X,start.Y,end.X,end.Y);
        var route=ImmutableArray.CreateBuilder<(Location,int)>();var here=start;Heading? previous=null;
        int Bit(Heading h)=>h.Index switch{0=>8,2=>4,4=>2,6=>1,_=>throw new ArgumentException()};
        while(here!=end)
        {
            var direction=Heading.FromOffset(Math.Sign(end.X-here.X),Math.Sign(end.Y-here.Y));
            if(!direction.IsCardinal)direction=random.Next(2)==0?direction.Left:direction.Right;
            route.Add((here,Bit(direction)|(previous is {} p?Bit(p.Opposite):0)));
            here=here.Move(direction);previous=direction;
        }
        route.Add((here,previous is {} last?Bit(last.Opposite):0));return route.ToImmutable();
    }
    public static AuthoringTerrainPlan Plan(TerrainWorld world,Location start,Location end,string action,
        DefinitionReference? forest,RandomStreams random, long? nextEntityId = null, SpatialIndex? protectedSpatial = null)
    {
        if(!IsAction(action)||!world.Space.ContainsVoxel(start)||!world.Space.ContainsVoxel(end)||start.Z!=end.Z)
            throw new ArgumentException("Invalid authoring selection.");
        TerrainDefinition? tree=null;
        IEnumerable<(Location Position,int Connections)> sites;
        if(action=="plantForest")
        {
            tree=world.Profile.Resolve(forest??throw new ArgumentException("Missing forest."));
            if(tree.TreeSpriteCount==0||tree.TreeDensity==0)throw new ArgumentException("Invalid forest.");
            long area=(Math.Abs((long)end.X-start.X)+1)*(Math.Abs((long)end.Y-start.Y)+1);
            if(area>64)throw new ArgumentException("Invalid forest extent.");
            var list=new List<(Location,int)>();
            for(int x=Math.Min(start.X,end.X);x<=Math.Max(start.X,end.X);x++)
            for(int y=Math.Min(start.Y,end.Y);y<=Math.Max(start.Y,end.Y);y++)list.Add((new(x,y,start.Z),0));
            sites=list;
        }
        else
        {
            if(forest!=null)throw new ArgumentException("Unexpected forest.");
            sites=CreekRoute(start,end);
        }
        var draft=new CornerTerrainDraft(world,nextEntityId,protectedSpatial);var plans=ImmutableArray.CreateBuilder<CornerTerrainPlan>();
        var decorations=ImmutableArray.CreateBuilder<AuthoringDecoration>();
        foreach(var (p,bits) in sites)
        {
            string? reason=null;var entity=draft.At(p).Entity;var occupant=draft.OccupantAt(p);
            int ground=0;
            if(!world.Space.ContainsVoxel(p))reason="outside";
            else
            {
                var s=world.Space.ToStorage(p);ground=world.Cell(s.H,s.V).Ground;
                if(ground!=p.Z)reason="groundLevel";
                else if(entity!=null&&!entity.SilentlyReclaimable&&!(tree!=null&&occupant?.Kind==TerrainOccupantKind.Mountain))reason="occupied";
            }
            if(reason!=null){plans.Add(new(p,action,reason,[]));continue;}
            ForestDecoration? realized=null;int connections=0;
            if(tree!=null)
            {
                var trees=ForestTerrain.Scatter(tree.TreeDensity,tree.TreeSpriteCount,random["forest"].Next);
                // Source zero draws leave existing decoration/identity unchanged, but
                // accepted confirmation still advances the isolated forest stream.
                if(trees.IsEmpty){plans.Add(new(p,action,null,[]));continue;}
                realized=new(tree.Reference,trees);
            }
            else connections=bits|(occupant?.Kind==TerrainOccupantKind.Creek?occupant.CreekConnections:0);
            draft.SetDecoration(p,realized,tree==null?connections:null);
            var corners=occupant?.Kind==TerrainOccupantKind.Mountain?occupant.Corners:default;
            plans.Add(new(p,action,null,[new(p,ground,ground,corners,corners,entity?.Id,false,occupant?.Forest!=null,entity?.Owned==true)]));
            decorations.Add(new(p,realized,connections));
        }
        return new(draft,plans.ToImmutable(),decorations.ToImmutable());
    }
}
