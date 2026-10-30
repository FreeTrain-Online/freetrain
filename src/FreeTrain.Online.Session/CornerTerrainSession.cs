using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private (TerrainWorld World, CornerTerrainDraft Draft, CornerTerrainPlan[] Plans) PlanCorner(CornerTerrainQuery query)
    {
        if (query.ExpectedWorldEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        var world = terrainState?.World ?? throw new InvalidOperationException("operationUnavailable");
        if (query.ExpectedGeometryRevision != world.Revision) throw new InvalidOperationException("staleRevision");
        if (terrainState!.Revision == long.MaxValue) throw new InvalidOperationException("operationUnavailable");
        if (query.Stroke?.Length > 63) throw new ArgumentException("invalidInput");
        var draft = new CornerTerrainDraft(world);
        if (query.Rectangle is {} rectangle)
        {
            if (query.Position == null || rectangle.End == null || query.OffsetX != null || query.OffsetY != null || query.Stroke?.Length > 0)
                throw new ArgumentException("invalidInput");
            return (world,draft,RectangleTerrain.Plan(draft,
                new(query.Position.X,query.Position.Y,query.Position.Z),
                new(rectangle.End.X,rectangle.End.Y,rectangle.End.Z),query.Action,rectangle.Soft));
        }
        var sites = new[] { new CornerTerrainSite(query.Position,query.Action,query.OffsetX,query.OffsetY) }.Concat(query.Stroke ?? []);
        var plans = new List<CornerTerrainPlan>();
        foreach (var site in sites)
        {
            if (site == null || site.Position == null || site.Action is not ("raise" or "lower") ||
                (site.OffsetX == null) != (site.OffsetY == null)) throw new ArgumentException("invalidInput");
            var p = new Location(site.Position.X,site.Position.Y,site.Position.Z);
            if (site.OffsetX is {} x && site.OffsetY is {} y)
            {
                if (!world.Space.ContainsVoxel(p) || x is < 0 or > 31 || y is < -32 or > 32) throw new ArgumentException("invalidInput");
                var s = world.Space.ToStorage(p);
                // The hit belongs to the original read; preceding stroke sites may change ground.
                if (world.Cell(s.H,s.V).Ground != p.Z) throw new ArgumentException("invalidInput");
                p = p with { Z = draft.Cell(s.H,s.V).Ground };
                var o = draft.OccupantAt(p);
                p = CornerTerrainSelection.Select(p,x,y,o?.Kind == TerrainOccupantKind.Mountain ? o.Corners : default);
            }
            var plan = draft.Preview(p,site.Action);
            if (plan.Legal) draft.Apply(plan);
            plans.Add(plan);
        }
        return (world,draft,plans.ToArray());
    }
    public CornerTerrainPreviewRead PreviewCorner(CornerTerrainQuery query)
    {
        lock (gate)
        {
            var (world,_,plans) = PlanCorner(query);
            var first = plans[0];
            return new(WorldEpoch,world.Revision,new(first.Position.X,first.Position.Y,first.Position.Z),first.Action,plans.Any(p=>p.Legal),
                plans.Any(p=>p.Legal) ? null : first.Reason,0,
                plans.SelectMany(p=>p.Changes).Select(c => new CornerTerrainChangeRead(new(c.Position.X,c.Position.Y,c.Position.Z),c.BeforeGround,c.AfterGround,
                    c.Before.NE,c.Before.SE,c.Before.SW,c.Before.NW,c.After.NE,c.After.SE,c.After.SW,c.After.NW,
                    c.Remove,c.LosesForest,c.LosesOwnership)).ToArray(),
                plans.Where(p=>!p.Legal).Select(p=>new CornerTerrainSkipRead(new(p.Position.X,p.Position.Y,p.Position.Z),p.Reason!)).ToArray());
        }
    }
    private string? CommitCorner(CornerTerrainQuery query)
    {
        try
        {
            var (world,draft,plans) = PlanCorner(query);
            if (!plans.Any(p=>p.Legal)) return "invalidPlacement";
            var candidate = draft.Finish();
            var state = terrainState!;
            // Validate durable admission before publication. No financial booking or RNG draws.
            SaveCodec.Write(new TerrainSessionState(candidate,state.Provenance,state.ConstructionMode,
                state.Clock.Capture(),state.Paused,state.Random.Capture(),checked(state.Revision+1)));
            state.Replace(state.Revision,candidate,state.ConstructionMode);
            return null;
        }
        catch (InvalidOperationException ex) when (ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") { return ex.Message; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException) { return "invalidPlacement"; }
    }
}
