using System.Collections.Immutable;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

// Data-only conversion; validation belongs to StationGraph/MixedWorld and the
// session callback boundary. Never rediscover hosts or regenerate names on load.
public static class StationWire
{
    private static PositionRead P(Location p) => new(p.X,p.Y,p.Z);
    private static Location P(PositionRead p) => new(p.X,p.Y,p.Z);
    private static DefinitionRead D(DefinitionReference d) => new(d.PackId,d.ContributionId,d.RecipeId);
    private static DefinitionReference D(DefinitionRead d) => new(d.PackId,d.ContributionId,d.RecipeId);
    private static MixedActionRead? T(ScheduledAction? t) => t == null ? null : new(t.Registration,t.Order,t.DueMinute,t.HandlerId,t.Interval);
    private static ScheduledAction? T(MixedActionRead? t) => t == null ? null : new(t.Registration,t.Order,t.DueMinute,t.HandlerId,t.Interval);
    private static StationLogRead L(StationTransportLog l) => new(l.Today,l.Yesterday,l.ThisWeek,l.LastWeekPerDay);
    private static StationTransportLog L(StationLogRead l) => new(l.Today,l.Yesterday,l.ThisWeek,l.LastWeekPerDay);
    private static string Tag<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
    private static T EnumValue<T>(string value) where T : struct, Enum => Enum.GetValues<T>().Single(v => Tag(v) == value);
    private static PopulationRead P(Population p) => new(p.Kind,p.Base,p.Factor,p.Entering,
        (p.Children.IsDefault ? [] : p.Children).Select(P).ToArray(),
        (p.Tables.IsDefault ? [] : p.Tables).Select(t=>t.ToArray()).ToArray());
    private static Population P(PopulationRead root)
    {
        var count = 0;
        return Read(root, 0);
        Population Read(PopulationRead p, int depth)
        {
            if (p == null || depth > 16 || ++count > 256 || p.Children == null || p.Tables == null || p.Children.Length > 256 || p.Tables.Length > 14 ||
                p.Tables.Any(t=>t == null || t.Length != 24)) throw new ArgumentException("Population wire limit.");
            return new(p.Kind,p.Base,p.Factor,p.Entering,p.Children.Select(c=>Read(c,depth+1)).ToImmutableArray(),p.Tables.Select(t=>t.ToImmutableArray()).ToImmutableArray());
        }
    }
    private static StationProviderRead? P(StationProvider? p) => p == null ? null : new(P(p.Population),p.LandValue,p.XYSize,p.ZSize,p.Special,p.Input);
    private static StationProvider? P(StationProviderRead? p) => p == null ? null : new(P(p.Population),p.LandValue,p.XYSize,p.ZSize,p.Special,p.Input);
    public static IEnumerable<MixedActionRead> Timers(StationGraphRead? g) =>
        (g?.Buildings ?? []).SelectMany(b => new[] {b.HourTimer,b.DayTimer})
        .Concat((g?.Platforms ?? []).Select(p => p.DayTimer)).OfType<MixedActionRead>();

    public static StationGraphRead? Read(StationGraph? g)
    {
        if (g == null) return null;
        var data = new StationGraphRead(g.NextStationName,g.NextPlatformName,
        g.Buildings.Select(b => new StationBuildingRead(b.Id,b.Revision,D(b.Definition),P(b.Location),b.Name,b.Children.ToArray(),
            new(b.Passengers.Gone,b.Passengers.AccumulatedLoaded,b.Passengers.AccumulatedUnloaded),L(b.Imports),L(b.Exports),L(b.Trains),T(b.HourTimer),T(b.DayTimer))).ToArray(),
        g.Platforms.Select(p => new StationPlatformRead(p.Id,p.Revision,Tag(p.Kind),P(p.Location),p.Direction.Index,p.Length,p.Name,
            p.Roof,p.Lane0,p.Lane1,D(p.Bell),p.HostId,p.Children.ToArray(),p.Yards.Select(y => new PlatformYardRead(y.TrafficOwnerId,
                P(y.Location),y.Lane,y.Index,y.Roof,new(Tag(y.Outlook.Kind),Tag(y.Outlook.Material),y.Outlook.Bridge,y.Outlook.Upward))).ToArray(),T(p.DayTimer))).ToArray(),
        g.Passages.Select(p => new StationPassageRead(p.TrafficOwnerId,P(p.Location),p.Direction.Index,Tag(p.Material))).ToArray(),
        g.Listeners.Select(l => new StationListenerRead(l.ProviderId,l.Stations.ToArray(),P(l.Provider))).ToArray(),
        g.ListenerMemberships.Select(l => new StationListenerMembershipRead(l.StationId,l.Providers.ToArray())).ToArray());
        ValidateLimits(data);
        return data;
    }

    public static StationGraph? Restore(StationGraphRead? g, StationProfile? profile)
    {
        if (g == null) return null;
        if (profile == null) throw new InvalidDataException("incompatibleSave");
        ValidateLimits(g);
        return new(profile,
            g.Buildings.Select(b => new StationBuilding(b.Id,b.Revision,D(b.Definition),P(b.Location),b.Name,b.Children.ToImmutableArray(),
                new(b.Passengers.Gone,b.Passengers.AccumulatedLoaded,b.Passengers.AccumulatedUnloaded),L(b.Imports),L(b.Exports),L(b.Trains),T(b.HourTimer),T(b.DayTimer))),
            g.Platforms.Select(p => new StationPlatform(p.Id,p.Revision,EnumValue<PlatformKind>(p.Kind),P(p.Location),new(p.Direction),p.Length,
                p.Name,p.Roof,p.Lane0,p.Lane1,D(p.Bell),p.HostId,p.Children.ToImmutableArray(),p.Yards.Select(y => new PlatformYard(y.TrafficOwnerId,
                    P(y.Location),y.Lane,y.Index,y.Roof,new(EnumValue<PlatformOutlookKind>(y.Outlook.Kind),EnumValue<PassageMaterial>(y.Outlook.Material),y.Outlook.Bridge,y.Outlook.Upward))).ToImmutableArray(),T(p.DayTimer))),
            g.Passages.Select(p => new StationPassage(p.TrafficOwnerId,P(p.Location),new(p.Direction),EnumValue<PassageMaterial>(p.Material))),g.NextStationName,g.NextPlatformName,
            g.Listeners.Select(l => new StationListener(l.ProviderId,l.Stations.ToImmutableArray(),P(l.Provider))),
            g.ListenerMemberships.Select(l => new StationListenerMembership(l.StationId,l.Providers.ToImmutableArray())));
    }
    private static void ValidateLimits(StationGraphRead g)
    {
        const int max = ProtocolIdentity.MaximumRailwayVoxels;
        if (g.Buildings.Length + (long)g.Platforms.Length + g.Passages.Length > max || g.Listeners.Length > max || g.ListenerMemberships.Length > max ||
            g.Listeners.Any(l => l.Stations.Length > 4) || g.ListenerMemberships.Any(l => l.Providers.Length > max) ||
            g.Buildings.Any(b => b.Children.Length > max) || g.Platforms.Any(p => p.Children.Length > max || p.Yards.Length > max) ||
            g.Platforms.Sum(p => (long)p.Yards.Length) > max ||
            g.Buildings.Sum(b => (long)b.Children.Length) + g.Platforms.Sum(p => (long)p.Children.Length) > max ||
            g.ListenerMemberships.Sum(l => (long)l.Providers.Length) > 4L * max) throw new ArgumentException("Station wire limit.");
    }

}
