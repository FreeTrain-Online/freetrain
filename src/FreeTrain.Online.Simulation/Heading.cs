// Behavioral extraction from ExAV 2.1.1.12, archive 883cf7d/trunk/core/world/Direction.cs.
// Original: Copyright (c) 2002 Kohsuke Kawaguchi et al.; LGPL-2.1-or-later (COPYING.txt).
namespace FreeTrain.Online.Simulation;

// Canonical game heading indices, independent of screen projection.
public readonly record struct Heading
{
    public Heading(int index)
    {
        if (index is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(index));
        Index = index;
    }

    public int Index { get; }
    public static Heading North => new(0);
    public static Heading NorthEast => new(1);
    public static Heading East => new(2);
    public static Heading SouthEast => new(3);
    public static Heading South => new(4);
    public static Heading SouthWest => new(5);
    public static Heading West => new(6);
    public static Heading NorthWest => new(7);

    public (int X, int Y) Offset => Index switch
    {
        0 => (0, -1), 1 => (1, -1), 2 => (1, 0), 3 => (1, 1),
        4 => (0, 1), 5 => (-1, 1), 6 => (-1, 0), 7 => (-1, -1),
        _ => throw new InvalidOperationException()
    };

    public bool IsCardinal => Index % 2 == 0;
    public bool IsParallelToX => Index % 4 == 2;
    public bool IsParallelToY => Index % 4 == 0;
    public Heading Left => new((Index + 7) % 8);
    public Heading Right => new((Index + 1) % 8);
    public Heading Left90 => new((Index + 6) % 8);
    public Heading Right90 => new((Index + 2) % 8);
    public Heading Opposite => new((Index + 4) % 8);

    // Angle in eighth-turns, in the inclusive range 0..4.
    public int AngleTo(Heading other)
    {
        var difference = Math.Abs(Index - other.Index);
        return Math.Min(difference, 8 - difference);
    }

    public static Heading FromOffset(int x, int y) => (x, y) switch
    {
        (0, -1) => North, (1, -1) => NorthEast, (1, 0) => East, (1, 1) => SouthEast,
        (0, 1) => South, (-1, 1) => SouthWest, (-1, 0) => West, (-1, -1) => NorthWest,
        _ => throw new ArgumentOutOfRangeException(nameof(x), "Expected one of the eight unit direction vectors.")
    };
}
