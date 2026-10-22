// Behavioral extraction from ExAV 2.1.1.12, archive 883cf7d/trunk/core/world/Location.cs.
// Original: Copyright (c) 2002 Kohsuke Kawaguchi et al.; LGPL-2.1-or-later (COPYING.txt).
namespace FreeTrain.Online.Simulation;

public readonly record struct Location(int X, int Y, int Z)
{
    // Checked spatial arithmetic defines the supported range; it never wraps a location.
    public Location Move(Heading heading)
    {
        var (dx, dy) = heading.Offset;
        return new(checked(X + dx), checked(Y + dy), Z);
    }

    public Heading DirectionTo(Location target)
    {
        if (Z != target.Z || (X == target.X && Y == target.Y))
            throw new ArgumentException("Direction requires distinct locations at the same level.", nameof(target));
        return Heading.FromOffset(Math.Sign(checked(target.X - X)), Math.Sign(checked(target.Y - Y)));
    }

    public Location Toward(Location target) => Move(DirectionTo(target));

    public int DistanceTo(Location target)
    {
        var dx = checked(target.X - X);
        var dy = checked(target.Y - Y);
        var dz = checked(target.Z - Z);
        // Preserve source Int32 intermediates, rejecting overflow before sqrt/casting.
        return (int)Math.Sqrt(checked(dx * dx + dy * dy + dz * dz));
    }

    public bool IsBetween(Location first, Location second) =>
        Between(X, first.X, second.X) && Between(Y, first.Y, second.Y) && Between(Z, first.Z, second.Z);

    private static bool Between(int value, int first, int second) =>
        value >= Math.Min(first, second) && value <= Math.Max(first, second);

    public Location Align4To(Location anchor)
    {
        if (Z != anchor.Z) throw new ArgumentException("Four-way snapping requires the same level.", nameof(anchor));
        var dx = Math.Abs(checked(X - anchor.X));
        var dy = Math.Abs(checked(Y - anchor.Y));
        return dx >= dy ? new(X, anchor.Y, Z) : new(anchor.X, Y, Z);
    }

    public Location Align8To(Location anchor)
    {
        var dx = Math.Abs(checked(X - anchor.X));
        var dy = Math.Abs(checked(Y - anchor.Y));
        if (dy / 2 > dx) return new(anchor.X, Y, Z);
        if (dx / 2 > dy) return new(X, anchor.Y, Z);
        return new(X, checked(anchor.Y + dx * Math.Sign(checked(Y - anchor.Y))), Z);
    }
}
