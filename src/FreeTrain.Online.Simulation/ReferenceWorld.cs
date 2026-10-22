using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record ReferenceRail(Location Position, int Pattern);

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

// A static geometry reference, deliberately independent of the prototype world.
// The constructor validates the complete connected, reciprocal, eight-heading loop.
public sealed class ReferenceWorld
{
    public WorldSpace Space { get; }
    public ImmutableArray<ReferenceRail> Rails { get; }
    public ReferenceWorld(int width, int height, int levels, IEnumerable<ReferenceRail> rails)
    {
        if (width is < 1 or > 64 || height is < 1 or > 128 || levels is < 1 or > 16)
            throw new ArgumentException("Invalid reference dimensions.");
        Space = new(width, height, levels);
        Rails = rails.Take(1025).ToImmutableArray();
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
    }
}
