using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    private MixedSessionState PassageState(PassageQuery query)
    {
        if (query.From == null || query.To == null || query.ExpectedWorldEpoch < 0 || query.ExpectedGeometryRevision < 0 || query.ExpectedMinute < 0 ||
            query.Part is not ("passage" or "stairs") || query.Part == "stairs" && query.From != query.To ||
            query.Action is not ("place" or "remove") || query.Material is not ("concrete" or "brown" or "green")) throw new ArgumentException("invalidInput");
        var state = RailwayState(query.ExpectedWorldEpoch,query.ExpectedGeometryRevision,query.ExpectedContentVersion);
        if (state.Clock.Time.AbsoluteMinute != query.ExpectedMinute) throw new InvalidOperationException("staleRevision");
        return state;
    }
    private static PassageConstructionPlan PlanPassage(MixedSessionState state, PassageQuery query) =>
        query.Part == "stairs" ? PassageConstruction.PreviewStair(state.World,new(query.From.X,query.From.Y,query.From.Z),
            Enum.Parse<PassageMaterial>(query.Material,true),query.Action == "remove") : PassageConstruction.Preview(state.World,new(query.From.X,query.From.Y,query.From.Z),new(query.To.X,query.To.Y,query.To.Z),
            Enum.Parse<PassageMaterial>(query.Material,true),query.Action == "remove");

    public PassagePreviewRead PreviewPassage(PassageQuery query)
    {
        lock (gate)
        {
            var state = PassageState(query);
            var plan = PlanPassage(state,query);
            var reason = plan.Reason;
            if (reason == null)
            {
                try { MixedWire.ValidateTransition(state,PassageConstruction.Commit(state,plan)); }
                catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
                { reason = "constructionOverflow"; }
            }
            static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
            return new(query,reason == null,reason,0,
                reason == null ? plan.Positions.Select(P).ToArray() : []);
        }
    }
    private string? CommitPassage(EditPassageCommand command)
    {
        try
        {
            var before = PassageState(command.Query);
            var plan = PlanPassage(before,command.Query);
            if (!plan.Legal) return plan.Reason == "staleRevision" ? "staleRevision" : "invalidPlacement";
            if (command.ExpectedCost != 0) return "staleRevision";
            var after = PassageConstruction.Commit(before,plan);
            MixedWire.ValidateTransition(before,after);
            if (ReferenceEquals(before,after)) return null;
            mixedState = after;
            terrainState = null;
            return null;
        }
        catch (InvalidOperationException ex) when (ex.Message is "staleEpoch" or "staleRevision" or "operationUnavailable") { return ex.Message; }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
        { return "invalidPlacement"; }
    }
}
