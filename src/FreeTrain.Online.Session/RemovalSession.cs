using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private (TerrainWorld? Terrain, MixedWorld? Mixed, RemovalPreviewRead Preview) PlanRemoval(RemovalQuery query)
    {
        if (query.Position == null || (query.End == null ? query.ExpectedEntityId < 1 || query.ExpectedEntityRevision < 0 :
            query.ExpectedEntityId != 0 || query.ExpectedEntityRevision != 0))
            throw new ArgumentException("invalidInput");
        if (query.ExpectedWorldEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        var terrain = terrainState?.World ?? mixedState?.World.Terrain ?? throw new InvalidOperationException("operationUnavailable");
        var mixed = mixedState?.World;
        if (query.ExpectedGeometryRevision != (mixed?.Revision ?? terrain.Revision) ||
            query.ExpectedContentVersion != (mixed == null ? TerrainProfile.ContentVersion : ProtocolIdentity.MixedContent))
            throw new InvalidOperationException("staleRevision");
        var p = new Location(query.Position.X,query.Position.Y,query.Position.Z);
        static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
        if (query.End is {} end)
        {
            ImmutableArray<Location> path;
            try {path=ObjectRemoval.Line(p,new(end.X,end.Y,end.Z));}
            catch(ArgumentException ex) {return (null,null,new(query,false,ex.Message,"line",[],[],[]));}
            if (path.Any(p=>!terrain.Space.ContainsVoxel(p))) return (null,null,new(query,false,"outside","line",[],path.Select(P).ToArray(),[]));
            if (mixed == null) return (null,null,new(query,false,"noRailway","line",[],path.Select(P).ToArray(),[]));
            var (candidate,reason,skipped)=ObjectRemoval.RailwayLine(mixed,path);
            if (mixedState!.Revision == long.MaxValue) reason="constructionOverflow";
            var affected=reason!=null?[]:mixed.Voxels.Where(v=>candidate!.At(v.Position)!=v).Select(v=>P(v.Position)).ToArray();
            return (null,candidate,new(query,reason==null,reason,"line",affected,path.Select(P).ToArray(),
                skipped.Select(s=>new RemovalSkipRead(P(s.Position),s.Reason)).ToArray()));
        }
        var entity = (mixed?.Spatial ?? terrain.Spatial).At(p).Entity;
        if (entity?.Id != query.ExpectedEntityId || entity.Revision != query.ExpectedEntityRevision)
            throw new InvalidOperationException("staleRevision");
        TerrainWorld? nextTerrain = null; MixedWorld? nextMixed = null; string kind;
        string? rejection;
        if (mixed == null) (nextTerrain,kind,rejection) = ObjectRemoval.Terrain(terrain,p);
        else (nextMixed,kind,rejection) = ObjectRemoval.Mixed(mixed,p);
        if ((mixedState?.Revision ?? terrainState!.Revision) == long.MaxValue) rejection = "constructionOverflow";
        var positions = rejection != null ? [] : entity.Footprint.At(entity.Anchor).Select(m=>m.Position)
            .Concat(mixed == null ? [] : mixed.Voxels.Where(v=>nextMixed!.At(v.Position)!=v).Select(v=>v.Position))
            .Select(p=>new PositionRead(p.X,p.Y,p.Z))
            .Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y).ThenBy(p=>p.Z).ToArray();
        return (nextTerrain,nextMixed,new(query,rejection == null,rejection,kind,positions,[query.Position],[]));
    }

    public RemovalPreviewRead PreviewRemoval(RemovalQuery query)
    {
        lock (gate) return PlanRemoval(query).Preview;
    }

    private string? CommitRemoval(RemovalQuery query)
    {
        try
        {
            var plan = PlanRemoval(query);
            if (!plan.Preview.Legal) return "invalidPlacement";
            if (mixedState is {} before)
            {
                var construction = new MixedConstructionState(plan.Mixed!,before.Construction.Accounts);
                var after = before.Successor(before.Revision,construction,before.ConstructionMode);
                MixedWire.ValidateTransition(before,after);
                mixedState = after;
            }
            else
            {
                var state = terrainState!;
                SaveCodec.ValidateTerrainAdmission(new TerrainSessionState(plan.Terrain!,state.Provenance,state.ConstructionMode,
                    state.Clock.Capture(),state.Paused,state.Random.Capture(),checked(state.Revision+1)));
                state.Replace(state.Revision,plan.Terrain!,state.ConstructionMode);
            }
            return null;
        }
        catch (InvalidOperationException ex) when (ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") { return ex.Message; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
        { return "invalidPlacement"; }
    }
}
