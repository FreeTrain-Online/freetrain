using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState ThinPlatformState(ThinPlatformQuery query)
    {
        if (query.Position == null || query.ExpectedWorldEpoch < 0 || query.ExpectedGeometryRevision < 0 || query.ExpectedMinute < 0 ||
            query.Direction is not (0 or 2 or 4 or 6) || query.Length is < 1 or > ThinPlatformConstruction.MaximumLength ||
            query.Action is not ("place" or "remove") || query.PlatformId < 0 || query.ExpectedPlatformRevision < 0 ||
            query.Action == "place" && (query.PlatformId != 0 || query.ExpectedPlatformRevision != 0) ||
            query.Action == "remove" && (query.PlatformId == 0)) throw new ArgumentException("invalidInput");
        var state = RailwayState(query.ExpectedWorldEpoch,query.ExpectedGeometryRevision,query.ExpectedContentVersion);
        if (state.Clock.Time.AbsoluteMinute != query.ExpectedMinute) throw new InvalidOperationException("staleRevision");
        return state;
    }
    private static ThinPlatformPlan PlanThinPlatform(MixedSessionState state, ThinPlatformQuery query) =>
        ThinPlatformConstruction.Preview(state.World,new(query.Position.X,query.Position.Y,query.Position.Z),
            new(query.Direction),query.Length,query.Roof,
            query.Action == "remove",query.PlatformId,query.ExpectedPlatformRevision);

    public ThinPlatformPreviewRead PreviewThinPlatform(ThinPlatformQuery query)
    {
        lock (gate)
        {
            var state = ThinPlatformState(query);
            var plan = PlanThinPlatform(state,query);
            var reason = plan.Reason;
            if (reason == null)
            {
                try { MixedWire.ValidateTransition(state,ThinPlatformConstruction.Commit(state,plan)); }
                catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
                { reason = state.Construction.Accounts.Transactions.Length+plan.Booking.Charges.Length > NormalRailwayConstruction.MaximumAccountTransactions
                    ? "accountCapacityLimit" : "constructionOverflow"; }
            }
            static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
            return new(query,reason == null,reason,reason == null ? plan.Booking.Total : 0,
                plan.Positions.Select(P).ToArray());
        }
    }
    private string? CommitThinPlatform(EditThinPlatformCommand command)
    {
        try
        {
            var before = ThinPlatformState(command.Query);
            var plan = PlanThinPlatform(before,command.Query);
            if (!plan.Legal) return plan.Reason == "staleRevision" ? "staleRevision" : "invalidPlacement";
            if (plan.Booking.Total != command.ExpectedCost) return "staleRevision";
            var after = ThinPlatformConstruction.Commit(before,plan);
            MixedWire.ValidateTransition(before,after);
            mixedState = after;
            terrainState = null;
            return null;
        }
        catch (InvalidOperationException ex) when (ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") { return ex.Message; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
        { return "invalidPlacement"; }
    }
}
