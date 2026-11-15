using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

public sealed partial class SessionHost
{
    public const long QuoteValidityMinutes = 12 * 60;
    private sealed record QuoteLease(long Minute, string Fingerprint, bool? ConstructionMode, bool Consumed = false);
    private static readonly JsonSerializerOptions QuoteJson = WireJson.CreateOptions();
    private readonly Dictionary<string, QuoteLease> quoteLeases = new(StringComparer.Ordinal);
    private int quoteKeyCharacters;
    private bool rebasingQuote, reviewingQuote;
    private static long QuoteEpoch<T>(T query) => query switch {
        FleetTradeQuery q => q.ExpectedWorldEpoch, FleetDeploymentQuery q => q.ExpectedWorldEpoch,
        FixedCityQuery q => q.ExpectedWorldEpoch, CityQuery q => q.ExpectedWorldEpoch,
        StationBuildingQuery q => q.ExpectedWorldEpoch, PassageQuery q => q.ExpectedWorldEpoch,
        ThinPlatformQuery q => q.ExpectedWorldEpoch, WidePlatformQuery q => q.ExpectedWorldEpoch,
        RailwayQuery q => q.ExpectedWorldEpoch, SlopeQuery q => q.ExpectedWorldEpoch,
        CornerTerrainQuery q => q.ExpectedWorldEpoch, RemovalQuery q => q.ExpectedWorldEpoch,
        _ => throw new ArgumentException("Unsupported quote query.")
    };

    // Queries without a clock field (railway, terrain and removal) still get an
    // exact simulation-time lifetime. Keep only bounded, server-issued reviews.
    private static string QuoteKey<T>(T query) => typeof(T).Name + ":" + JsonSerializer.Serialize(query, QuoteJson);
    private string QuoteFingerprint<T>(T preview)
    {
        object value = preview is CornerTerrainPreviewRead terrain ? terrain with { GeometryRevision = 0 } : preview!;
        IEnumerable<PositionRead> positions = preview switch {
            RailwayPreviewRead p => p.Cells.Select(c => c.Position).Concat(p.AddedPiers),
            SlopePreviewRead p => p.Members.Select(c => c.Position).Concat(p.AddedPiers),
            CornerTerrainPreviewRead p => p.Changes.Select(c => c.Position),
            RemovalPreviewRead p => p.Positions,
            FixedCityPreviewRead p => p.Positions, StationBuildingPreviewRead p => p.Positions,
            ThinPlatformPreviewRead p => p.Positions, WidePlatformPreviewRead p => p.Positions,
            PassagePreviewRead p => p.Positions, FleetDeploymentPreviewRead p => p.Cars.Select(c => c.Location),
            _ => []
        };
        var world = mixedState?.World;
        var ground = world?.Terrain ?? terrainState?.World;
        var spatial = world?.Spatial ?? ground?.Spatial;
        // A replacement with the same art and price is still a different owner.
        // Ignore owner revisions: train occupancy and statistics can advance
        // them without changing what this action affects.
        var owners = positions.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z).Select(p => {
            var location = new Location(p.X,p.Y,p.Z);
            return new { Position = p, OwnerId = ground?.Space.ContainsVoxel(location) == true ? spatial?.At(location).Entity?.Id : null };
        }).ToArray();
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Preview = value, Owners = owners,
            HalfOwners = preview is FixedCityPreviewRead {Query.Half: not null} && world?.City is {} city
                ? owners.Select(o=>o.OwnerId).OfType<long>().Distinct().Select(id=>city.State.Structures.SingleOrDefault(s=>s.Id==id))
                    .Where(s=>s!=null && !s.Halves.IsEmpty).Select(s=>new {s!.Id,s.Halves}).ToArray() : null,
            ConstructionMode = mixedState?.ConstructionMode ?? terrainState?.ConstructionMode
        }, QuoteJson)));
    }
    private void ValidateQuoteAge(long minute)
    {
        var now = CanonicalMinute;
        if (minute < 0 || minute > now || now - minute > QuoteValidityMinutes)
            throw new InvalidOperationException("staleRevision");
    }
    private void ValidateQuoteRevision(long expected, long actual)
    {
        if (expected < 0 || expected > actual || !rebasingQuote && expected != actual)
            throw new InvalidOperationException("staleRevision");
    }
    private void ValidateQuoteMinute(long expected)
    {
        ValidateQuoteAge(expected);
        if (!rebasingQuote && expected != CanonicalMinute) throw new InvalidOperationException("staleRevision");
    }
    private sealed class QuoteScope : IDisposable
    {
        private readonly SessionHost host;
        private readonly bool rebase, review;
        public QuoteScope(SessionHost host, bool rebase, bool review)
        {
            this.host = host; this.rebase = host.rebasingQuote; this.review = host.reviewingQuote;
            host.rebasingQuote = rebase; host.reviewingQuote = review;
        }
        public void Dispose() { host.rebasingQuote = rebase; host.reviewingQuote = review; }
    }
    private TPreview BuildQuote<TQuery, TPreview>(TQuery query, Func<TPreview> build, long? minute = null)
    {
        if (QuoteEpoch(query) != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        using var scope = new QuoteScope(this, true, reviewingQuote);
        ValidateQuoteAge(minute ?? CanonicalMinute);
        var preview = build();
        if (!reviewingQuote)
        {
            var key = QuoteKey(query);
            // Limit both entry count and individual request size. Eviction only
            // requires a new preview; it never permits a stale confirmation.
            if (key.Length <= 16 * 1024)
            {
                if (!quoteLeases.ContainsKey(key))
                {
                    while (quoteLeases.Count >= 4096 || quoteKeyCharacters + key.Length > 2 * 1024 * 1024)
                    {
                        var oldest = quoteLeases.Keys.First();
                        quoteLeases.Remove(oldest); quoteKeyCharacters -= oldest.Length;
                    }
                    quoteLeases.Add(key, new(minute ?? CanonicalMinute, QuoteFingerprint(preview), mixedState?.ConstructionMode ?? terrainState?.ConstructionMode));
                    quoteKeyCharacters += key.Length;
                }
            }
        }
        return preview;
    }
    private IDisposable ReviewQuote<TQuery, TPreview>(TQuery query, Func<TPreview> preview)
    {
        if (QuoteEpoch(query) != WorldEpoch) throw new InvalidOperationException("staleEpoch");
        // Preserve exact-revision command handling for callers that have not
        // requested a preview. Only an issued quote may span world changes.
        var key = QuoteKey(query);
        if (!quoteLeases.TryGetValue(key, out var lease)) return new QuoteScope(this, false, true);
        ValidateQuoteAge(lease.Minute);
        if (lease.Consumed || lease.ConstructionMode != (mixedState?.ConstructionMode ?? terrainState?.ConstructionMode)) throw new InvalidOperationException("staleRevision");
        var scope = new QuoteScope(this, true, true);
        try
        {
            if (QuoteFingerprint(preview()) != lease.Fingerprint) throw new InvalidOperationException("staleRevision");
            return scope;
        }
        catch { scope.Dispose(); throw; }
    }
    private void ConsumeQuote<T>(T query)
    {
        var key = QuoteKey(query);
        if (quoteLeases.TryGetValue(key, out var lease)) quoteLeases[key] = lease with { Consumed = true };
    }
}
