// ExAV 883cf7d ForestBuilder.create, LandBuilderContribution.create and
// MountainVoxel.setTrees/removeTrees. Original authors, LGPL-2.1-or-later.
using System.Collections.Immutable;
namespace FreeTrain.Online.Simulation;

public static class ForestConstruction
{
    public static LandTradePlan Preview(CityCandidate before, Location anchor, Location end, DefinitionReference definition)
    {
        var world=before.Construction.World;var graph=world.City!;var terrain=world.Terrain;
        var d=graph.Profile.GroundDefinition(definition);var tree=terrain.Profile.Resolve(definition);
        if(d.Handler!="forest" || graph.Profile.Bindings.Single(b=>b.Reference==definition).PlayerCannotBuild ||
            tree.TreeDensity<1 || tree.TreeSpriteCount<1 || end.Z!=anchor.Z || terrain.At(anchor).Relation!=GroundRelation.Ground)
            throw new ArgumentException("Invalid forest selection.");
        long sx=Math.Abs((long)end.X-anchor.X)+1,sy=Math.Abs((long)end.Y-anchor.Y)+1;
        if(sx>LandTrading.MaximumCells || sy>LandTrading.MaximumCells || sx*sy>LandTrading.MaximumCells)
            throw new ArgumentException("Forest selection capacity exceeded.");
        var random=RandomStreams.Restore(before.Random);var cells=ImmutableArray.CreateBuilder<LandCell>();
        var reclaimed=ImmutableArray.CreateBuilder<LandReclamation>();var removed=new HashSet<long>();
        var edits=new Dictionary<Location,(SpatialEntity Entity,TerrainOccupant Occupant)?>();
        long next=world.Spatial.NextEntityId,trees=terrain.RealizedTreeCount;
        var mountain=terrain.Profile.Definitions.Single(d=>d.Reference.ContributionId==TerrainProfile.MountainId).Reference;
        for(long x=Math.Min(anchor.X,end.X);x<=Math.Max(anchor.X,end.X);x++)
        for(long y=Math.Min(anchor.Y,end.Y);y<=Math.Max(anchor.Y,end.Y);y++)
        {
            var p=new Location((int)x,(int)y,anchor.Z);var relation=terrain.At(p).Relation;
            var owner=world.Spatial.At(p).Entity;var occupant=terrain.OccupantAt(p);
            var retain=occupant?.Kind==TerrainOccupantKind.Mountain;
            var status=relation==GroundRelation.Outside?"outside":relation!=GroundRelation.Ground?"uneven":
                owner==null || owner.SilentlyReclaimable || retain?"placeable":"protected";
            if(status=="placeable")
            {
                var patterns=ForestTerrain.Scatter(tree.TreeDensity,tree.TreeSpriteCount,random["forest"].Next);
                // Source zero result leaves identity, ownership, art and listeners intact.
                if(!patterns.IsEmpty)
                {
                    if(owner!=null && !retain)
                    {
                        if(!graph.IsSpatialOwner(owner.Id) && occupant?.Kind!=TerrainOccupantKind.Creek)
                            throw new ArgumentException("Unavailable forest reclamation handler.");
                        removed.Add(owner.Id);reclaimed.Add(new(owner,0));
                    }
                    var id=retain?owner!.Id:next; if(!retain)next=checked(next+1);
                    var corners=retain?occupant!.Corners:default;
                    var entity=new SpatialEntity(id,retain?checked(owner!.Revision+1):0,mountain,p,
                        new([new(new(0,0,0),corners.Flat)]),true,false,tree.Price);
                    edits[p]=(entity,new(id,TerrainOccupantKind.Mountain,corners,new(definition,patterns)));
                    trees+=patterns.Length-(occupant?.Forest?.Trees.Length??0);
                }
            }
            cells.Add(new(p,status,0,owner?.Id,status=="placeable"?definition:null));
        }
        var amount=checked(checked(d.Price*sx)*sy);
        var candidateTerrain=new TerrainWorld(terrain,new Dictionary<int,TerrainCell>(),edits,next,trees);
        var construction=CityConstruction.Replace(before.Construction,CityConstruction.Without(graph.State,removed),next,
            ConstructionBooking.Spend(amount,AccountGenre.Subsidiaries),new(before.Clock.AbsoluteMinute),"forestConstruction",terrain:candidateTerrain,reclaimed:removed);
        return new(before,new(construction,before.Clock,random.Capture()),cells.ToImmutable(),reclaimed.ToImmutable(),amount);
    }
}
