using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record ReferenceRail(Location Position, int Pattern, long EntityId = 0);

// Source normal patterns: ExAV 2.1.1.12, 883cf7d/trunk/core/world/rail/RailPattern.cs,
// singlePatterns (indices 0..11). Original copyright Kohsuke Kawaguchi et al., LGPL-2.1-or-later.
public static class NormalRailPatterns
{
    public static ImmutableArray<(int A, int B)> Ports { get; } =
        [(1,4), (1,5), (1,6), (2,5), (2,6), (2,7), (3,6), (3,7), (3,0), (4,7), (4,0), (5,0)];
    public static int Find(int a, int b)
    {
        for (var i = 0; i < Ports.Length; i++)
            if (Ports[i] == (a,b) || Ports[i] == (b,a)) return i;
        throw new ArgumentException("Unsupported normal rail ports.");
    }
}

// Canonical geometry and source-minute time, independent of the prototype world.
// The constructor validates the complete connected, reciprocal, eight-heading loop.
public sealed class ReferenceWorld
{
    public Clock Clock { get; private set; } = new();
    public RandomStreams Random { get; private set; }
    public void RestoreRandom(RandomStreamsState state) => Random = RandomStreams.Restore(state);
    public bool Paused { get; private set; } = true;
    public long Revision { get; private set; }
    public void ReviseSession() => Revision = checked(Revision + 1);
    public void SetPaused(bool paused)
    {
        if (Paused == paused) return;
        var revision = checked(Revision + 1);
        Paused = paused; Revision = revision;
    }
    public bool CanAdvance => Clock.Time.AbsoluteMinute < GameTime.MaximumMinute;
    public void AdvanceOneMinute()
    {
        var revision = checked(Revision + 1);
        Clock.Advance(); Revision = revision;
    }
    public void RestoreTime(ClockState state, bool paused)
    {
        // No gameplay action bindings exist in this reference slice. Unknown
        // pending actions fail before this world can replace the active world.
        var restored = Clock.Restore(state);
        Clock = restored; Paused = paused;
    }
    public WorldSpace Space { get; }
    public ImmutableArray<ReferenceRail> Rails { get; }
    public SpatialIndex Spatial { get; }
    public static DefinitionReference RailDefinition(int pattern) => new("reference-spatial", "normal-rail", $"normal{pattern}");
    public ReferenceWorld(int width, int height, int levels, IEnumerable<ReferenceRail> rails, int randomSeed = RandomStreams.ReferenceSeed, SpatialState? spatial = null)
    {
        Random = new(randomSeed);
        if (width is < 1 or > 64 || height is < 1 or > 128 || levels is < 1 or > 16)
            throw new ArgumentException("Invalid reference dimensions.");
        Space = new(width, height, levels);
        var input = rails.Take(1025).ToImmutableArray();
        if (spatial == null && input.All(r => r != null && r.EntityId == 0))
        {
            var ids = input.Select(r => r.Position).Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ThenBy(p => p.Z)
                .Select((p, i) => (p, Id: (long)i + 1)).ToDictionary(row => row.p, row => row.Id);
            input = input.Select(r => r with { EntityId = ids[r.Position] }).ToImmutableArray();
        }
        Rails = input;
        if (Rails.Length is < 8 or > 1024 || Rails.Any(r => r == null || r.Pattern is < 0 or > 11
            || r.Position.Z != 0 || !Space.ContainsVoxel(r.Position))
            || Rails.Select(r => r.Position).Distinct().Count() != Rails.Length)
            throw new ArgumentException("Invalid reference rails.");
        var cells = Rails.ToDictionary(r => r.Position);
        var headings = new HashSet<int>();
        foreach (var rail in Rails)
        {
            var (a,b) = NormalRailPatterns.Ports[rail.Pattern];
            foreach (var heading in new[] { a,b })
            {
                headings.Add(heading);
                if (!cells.TryGetValue(rail.Position.Move(new(heading)), out var neighbor))
                    throw new ArgumentException("Disconnected reference rail.");
                var ports = NormalRailPatterns.Ports[neighbor.Pattern];
                var opposite = new Heading(heading).Opposite.Index;
                if (ports.A != opposite && ports.B != opposite) throw new ArgumentException("Nonreciprocal reference rail.");
            }
        }
        var visited = new HashSet<Location>();
        var pending = new Stack<Location>(); pending.Push(Rails[0].Position);
        while (pending.TryPop(out var position))
        {
            if (!visited.Add(position)) continue;
            var (a,b) = NormalRailPatterns.Ports[cells[position].Pattern];
            pending.Push(position.Move(new(a))); pending.Push(position.Move(new(b)));
        }
        if (visited.Count != Rails.Length || headings.Count != 8) throw new ArgumentException("Expected one eight-heading loop.");
        var expected = Rails.Select(r => new SpatialEntity(r.EntityId, 0, RailDefinition(r.Pattern), r.Position,
            new Footprint([new(new(0,0,0), true)]), true, false, 0)).ToArray();
        Spatial = spatial == null ? new(Space, checked(expected.Max(e => e.Id) + 1), expected) : new(Space, spatial.NextEntityId, spatial.Entities);
        // Reference rules admit exactly one transparent occupied voxel per normal rail.
        // Broader entity kinds are admitted only with their implementing ruleset/catalog.
        if (Spatial.Entities.Length != Rails.Length || expected.Any(e =>
            Spatial.At(e.Anchor).Entity is not {} actual || actual.Id != e.Id || actual.Revision != 0 ||
            actual.Definition != e.Definition || actual.Anchor != e.Anchor || !actual.Owned || actual.SilentlyReclaimable || actual.Value != 0 ||
            actual.Footprint.Members.Length != 1 || actual.Footprint.Members[0] != e.Footprint.Members[0]))
            throw new ArgumentException("Reference entity graph does not match rails.");
    }
}
