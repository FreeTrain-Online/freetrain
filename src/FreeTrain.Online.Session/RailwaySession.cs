using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    // Promotion is detached until a successful confirmation. Preview/cancel never
    // changes the terrain profile, initial cash, IDs, random streams or clock.
    private MixedSessionState RailwayState(RailwayQuery query)
    {
        if (query.ExpectedWorldEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        if (query.From == null || query.To == null || query.Action is not ("lay" or "remove") ||
            query.Routing is not ("closest" or "source" or "alternate")) throw new ArgumentException("invalidInput");
        var content = mixedState != null ? ProtocolIdentity.MixedContent : TerrainProfile.ContentVersion;
        if (query.ExpectedContentVersion != content) throw new InvalidOperationException("staleRevision");
        var revision = mixedState?.World.Revision ?? terrainState?.World.Revision;
        if (revision == null) throw new InvalidOperationException("operationUnavailable");
        if (query.ExpectedGeometryRevision != revision) throw new InvalidOperationException("staleRevision");
        if (mixedState != null) return mixedState;
        var state = terrainState!;
        var profile = railwayResolver?.Invoke(ProtocolIdentity.MixedRuleset,ProtocolIdentity.MixedContent,state.World.Profile.Packs)
            ?? throw new InvalidOperationException("operationUnavailable");
        var world = new MixedWorld(profile,state.World,state.World.Spatial.Capture(),[],[],state.World.Revision);
        return new(new(world,new ConstructionAccounts()),state.Provenance,state.ConstructionMode,
            state.Clock.Capture(),state.Paused,state.Random.Capture(),state.Revision);
    }

    private static NormalRailwayConstructionPlan PlanRailway(MixedSessionState state, RailwayQuery query)
    {
        var from = new Location(query.From.X,query.From.Y,query.From.Z);
        var to = new Location(query.To.X,query.To.Y,query.To.Z);
        NormalRailwayConstructionPlan Plan(bool alternate) => query.Action == "remove"
            ? NormalRailwayConstruction.PreviewRemoval(state.World,from,to,alternate)
            : NormalRailwayConstruction.PreviewBuild(state.World,from,to,alternate);
        if (query.Routing == "alternate") return Plan(true);
        var source = Plan(false);
        // Owner extension: compare both shortest sign-vector walks. Never adjust
        // endpoints, bypass legality or choose a different demolition route.
        if (query.Routing != "closest" || query.Action == "remove") return source;
        var other = Plan(true);
        return other.Legal && (!source.Legal || other.Booking.Total < source.Booking.Total) ? other : source;
    }

    public RailwayPreviewRead PreviewRailway(RailwayQuery query)
    {
        lock (gate)
        {
            var state = RailwayState(query);
            var plan = PlanRailway(state,query);
            var reason = plan.Reason ?? (state.Revision == long.MaxValue || state.World.Revision == long.MaxValue ? "constructionOverflow" : null) ?? (state.Construction.Accounts.Transactions.Length >= NormalRailwayConstruction.MaximumAccountTransactions
                ? "accountCapacityLimit" : null);
            static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
            var losses = plan.Reclaimed.Select(r => new RailwayLossRead("reclamation",P(r.Entity.Anchor),r.Entity.Id,null))
                .Concat(plan.RemovedPiers.Select(v => new RailwayLossRead("pier",P(v.Position),v.OwnerId,v.SupportedOwnerId)))
                .Concat(plan.ComponentLosses.Select(v => new RailwayLossRead("accessory",P(v.Position),state.World.At(v.Position)!.OwnerId,null))).ToArray();
            return new(query,reason == null,reason,reason == null ? plan.Booking.Total : 0,plan.Topology.ReverseBend,
                plan.Topology.Cells.Select(c => new RailwayRouteCellRead(P(c.Position),c.Before?.Pattern,c.After?.Pattern,c.ProtectedByCar)).ToArray(),
                plan.ElectedPiers.Select(p => P(p.Position)).ToArray(),losses);
        }
    }

    private string? CommitRailway(RailwayQuery query)
    {
        try
        {
            var before = RailwayState(query);
            var plan = PlanRailway(before,query);
            if (!plan.Legal) return "invalidPlacement";
            var construction = NormalRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time);
            var after = before.Successor(before.Revision,construction,before.ConstructionMode);
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
