using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState StationBuildingState(StationBuildingQuery query)
    {
        if (query.Position == null || query.ExpectedWorldEpoch < 0 || query.ExpectedGeometryRevision < 0 || query.ExpectedMinute < 0 ||
            query.Action is not ("place" or "remove") || query.StationId < 0 || query.ExpectedStationRevision < 0 ||
            query.Action == "place" && (query.Definition == null || query.StationId != 0 || query.ExpectedStationRevision != 0) ||
            query.Action == "remove" && (query.Definition != null || query.StationId == 0)) throw new ArgumentException("invalidInput");
        var state = RailwayState(query.ExpectedWorldEpoch,query.ExpectedGeometryRevision,query.ExpectedContentVersion);
        if (state.Clock.Time.AbsoluteMinute != query.ExpectedMinute) throw new InvalidOperationException("staleRevision");
        return state;
    }
    private static StationBuildingPlan PlanStationBuilding(MixedSessionState state, StationBuildingQuery query) =>
        StationBuildingConstruction.Preview(state.World,new(query.Position.X,query.Position.Y,query.Position.Z),
            query.Definition is {} d ? new(d.PackId,d.ContributionId,d.RecipeId) : null,
            query.Action == "remove",query.StationId,query.ExpectedStationRevision);

    public StationBuildingPreviewRead PreviewStationBuilding(StationBuildingQuery query)
    {
        lock (gate)
        {
            var state = StationBuildingState(query);
            var plan = PlanStationBuilding(state,query);
            var reason = plan.Reason;
            if (reason == null)
            {
                try { MixedWire.ValidateTransition(state,StationBuildingConstruction.Commit(state,plan)); }
                catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
                { reason = state.Construction.Accounts.Transactions.Length+plan.Booking.Charges.Length > NormalRailwayConstruction.MaximumAccountTransactions
                    ? "accountCapacityLimit" : "constructionOverflow"; }
            }
            static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
            return new(query,reason == null,reason,reason == null ? plan.Booking.Total : 0,
                plan.Positions.Select(P).ToArray(),plan.Reclaimed.Select(r=>new RailwayLossRead("reclamation",P(r.Entity.Anchor),r.Entity.Id,null)).ToArray());
        }
    }
    private string? CommitStationBuilding(EditStationBuildingCommand command)
    {
        try
        {
            var before = StationBuildingState(command.Query);
            var plan = PlanStationBuilding(before,command.Query);
            if (!plan.Legal) return plan.Reason == "staleRevision" ? "staleRevision" : "invalidPlacement";
            if (plan.Booking.Total != command.ExpectedCost) return "staleRevision";
            var after = StationBuildingConstruction.Commit(before,plan);
            MixedWire.ValidateTransition(before,after);
            mixedState = after;
            terrainState = null;
            return null;
        }
        catch (InvalidOperationException ex) when (ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") { return ex.Message; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
        { return "invalidPlacement"; }
    }
    private string? RenameStationBuilding(RenameStationCommand command)
    {
        try
        {
            var state = mixedState;
            if (state?.World.Stations is not {} graph) return "operationUnavailable";
            if (!long.TryParse(command.StationId,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var id)) return "invalidInput";
            var building = graph.Buildings.SingleOrDefault(b=>b.Id==id);
            if (building == null || building.Revision != command.ExpectedRevision) return "staleRevision";
            var name = command.Name.Trim();
            if (name.Length is < 1 or > 128 || name.Any(char.IsControl)) return "invalidInput";
            var changed = new StationGraph(graph.Profile,graph.Buildings.Select(b=>b.Id==id
                ? b with {Name=name,Revision=checked(b.Revision+1)} : b),graph.Platforms,graph.Passages,
                graph.NextStationName,graph.NextPlatformName,graph.Listeners,graph.ListenerMemberships);
            var w = state.World;
            var spatial = new SpatialState(w.Spatial.NextEntityId,w.Spatial.Entities.Select(e=>e.Id==id
                ? e with {Revision=checked(e.Revision+1)} : e).ToImmutableArray());
            var world = w.Replace(w.Revision,w.Terrain,spatial,w.Voxels,w.Slopes,stations:changed);
            var after = state.Successor(state.Revision,new(world,state.Construction.Accounts),state.ConstructionMode);
            MixedWire.ValidateTransition(state,after);
            mixedState = after;
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
        { return "invalidInput"; }
    }

}
