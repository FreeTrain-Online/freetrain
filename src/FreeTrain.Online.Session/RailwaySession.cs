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
        if (query.From == null || query.To == null || query.Action is not ("lay" or "remove") ||
            query.Routing is not ("closest" or "source" or "alternate") || query.Infrastructure is not ("normal" or "bridge" or "girder" or "tunnel" or "garage" or "signal" or "pole" or "stationary" or "speed") ||
            query.Speed is not ("low" or "medium" or "fast") || query.SpeedClimb is not (0 or 2 or 4 or 6) ||
            query.Infrastructure=="speed" && query.SpeedSlope && query.From!=query.To ||
            query.SignalDirection is not (0 or 2 or 4 or 6) ||
            query.SignalType is not (SignalRailwayConstruction.ThreeAspect or SignalRailwayConstruction.Shunting) ||
            !PoleRailwayConstruction.IsBundledType(query.PoleType) || query.StationaryType is not {Length: <= 512} ||
            query.Infrastructure is ("signal" or "pole" or "stationary") && query.From != query.To ||
            query.TunnelRouting is not ("exact" or "portals" or "portals-auto") ||
            query.TunnelRouting != "exact" && (query.Infrastructure != "tunnel" || query.Action != "lay")) throw new ArgumentException("invalidInput");
        return RailwayState(query.ExpectedWorldEpoch,query.ExpectedGeometryRevision,query.ExpectedContentVersion);
    }
    private MixedSessionState RailwayState(long expectedEpoch, long expectedRevision, string expectedContent)
    {
        if (expectedEpoch != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        var content = mixedState != null ? ProtocolIdentity.MixedContent : TerrainProfile.ContentVersion;
        if (expectedContent != content) throw new InvalidOperationException("staleRevision");
        var revision = mixedState?.World.Revision ?? terrainState?.World.Revision;
        if (revision == null) throw new InvalidOperationException("operationUnavailable");
        if (expectedRevision != revision) throw new InvalidOperationException("staleRevision");
        if (mixedState != null) return mixedState;
        var state = terrainState!;
        var profile = railwayResolver?.Invoke(ProtocolIdentity.MixedRuleset,ProtocolIdentity.MixedContent,state.World.Profile.Packs)
            ?? throw new InvalidOperationException("operationUnavailable");
        var world = new MixedWorld(profile,state.World,state.World.Spatial.Capture(),[],[],state.World.Revision);
        return new(new(world,new ConstructionAccounts()),state.Provenance,state.ConstructionMode,
            state.Clock.Capture(),state.Paused,state.Random.Capture(),state.Revision);
    }

    private static RailwayConstructionPlan PlanRailway(MixedSessionState state, RailwayQuery query)
    {
        var from = new Location(query.From.X,query.From.Y,query.From.Z);
        var to = new Location(query.To.X,query.To.Y,query.To.Z);
        if (query.Infrastructure == "speed") return SpeedLimitConstruction.Preview(state.World,from,to,query.Speed,query.Action=="remove",query.SpeedSlope,query.SpeedClimb);
        if (query.Infrastructure == "stationary") return StationaryConstruction.Preview(state.World,from,query.StationaryType,query.Action == "remove");
        if (query.Infrastructure == "pole") return PoleRailwayConstruction.Preview(state.World,from,query.PoleType,query.Action == "remove");
        if (query.Infrastructure == "signal") return SignalRailwayConstruction.Preview(state.World,from,query.SignalType,query.SignalDirection,query.Action == "remove");
        if (query.Infrastructure == "garage") return GarageRailwayConstruction.Preview(state.World,from,to,query.Action == "remove");
        if (query.Infrastructure == "tunnel") return query.TunnelRouting == "exact"
            ? TunnelRailwayConstruction.Preview(state.World,from,to,query.Action == "remove")
            : TunnelPortalSelection.Preview(state.World,from,to,query.TunnelRouting == "portals-auto");
        if (query.Infrastructure == "girder") return GirderRailwayConstruction.Preview(state.World,from,to,query.Action == "remove");
        if (query.Infrastructure == "bridge") return BridgeRailwayConstruction.Preview(state.World,from,to,query.Action == "remove");
        RailwayConstructionPlan Plan(bool alternate) => query.Action == "remove"
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
            var reason = plan.Reason ?? (state.Revision == long.MaxValue || state.World.Revision == long.MaxValue ? "constructionOverflow" : null) ?? (plan.Booking.Charges.Length + plan.PendingCompletionCount > NormalRailwayConstruction.MaximumAccountTransactions-state.Construction.Accounts.Transactions.Length
                ? "accountCapacityLimit" : null);
            if (reason == null && query.Infrastructure == "stationary")
            {
                try { MixedWire.ValidateTransition(state,StationaryConstruction.Commit(state,plan)); }
                catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidDataException or InvalidOperationException)
                { reason = "constructionOverflow"; }
            }
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
            // Removing an unlisted section is a source no-op. In particular,
            // do not promote an untouched terrain world and disable its tools.
            if(query.Infrastructure=="speed" && query.Action=="remove")
            {
                var selected=plan.Topology.Cells.Select(c=>c.Position).ToHashSet();
                if(!before.World.SpeedLimits.Locations.Any(selected.Contains))return null;
            }
            var construction = query.Infrastructure == "speed" ? SpeedLimitConstruction.Apply(before.Construction,plan,before.Clock.Time) : query.Infrastructure == "stationary" ? before.Construction : query.Infrastructure == "pole"
                ? PoleRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time)
                : query.Infrastructure == "signal"
                ? SignalRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time)
                : query.Infrastructure == "garage"
                ? GarageRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time)
                : query.Infrastructure == "tunnel"
                ? TunnelRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time)
                : query.Infrastructure == "girder"
                ? GirderRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time)
                : query.Infrastructure == "bridge"
                ? BridgeRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time)
                : NormalRailwayConstruction.Apply(before.Construction,plan,before.Clock.Time);
            var after = query.Infrastructure == "stationary" ? StationaryConstruction.Commit(before,plan) : before.Successor(before.Revision,construction,before.ConstructionMode);
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
