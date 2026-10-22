// Behavioral extraction from ExAV 2.1.1.12, archive 883cf7d/trunk/core/world/World.cs.
// Original: Copyright (c) 2002 Kohsuke Kawaguchi et al.; LGPL-2.1-or-later (COPYING.txt).
namespace FreeTrain.Online.Simulation;

public readonly record struct StoragePosition(int H, int V, int Z);
public readonly record struct ProjectedPoint(int A, int B);

// Dimensions describe H/V/Z storage, not a rectangular X/Y/Z world.
public sealed class WorldSpace
{
    public WorldSpace(int horizontalSize, int verticalSize, int levels)
    {
        if (horizontalSize <= 0) throw new ArgumentOutOfRangeException(nameof(horizontalSize));
        if (verticalSize <= 0) throw new ArgumentOutOfRangeException(nameof(verticalSize));
        if (levels <= 0) throw new ArgumentOutOfRangeException(nameof(levels));
        HorizontalSize = horizontalSize;
        VerticalSize = verticalSize;
        Levels = levels;
    }

    public int HorizontalSize { get; }
    public int VerticalSize { get; }
    public int Levels { get; }
    private int Center => (VerticalSize - 1) / 2;

    public StoragePosition ToStorage(Location location)
    {
        var xx = checked(location.X - Center);
        // Arithmetic shift deliberately floors negative odd sums; / 2 would truncate.
        return new(checked(xx + location.Y) >> 1, checked(location.Y - xx), location.Z);
    }

    public Location ToLocation(StoragePosition position) => new(
        checked(position.H - position.V / 2 + Center),
        checked(position.H + checked(position.V + 1) / 2), position.Z);

    // Matches source isInsideWorld(Location), which intentionally ignores Z.
    public bool ContainsHorizontal(Location location)
    {
        var storage = ToStorage(location);
        return storage.H >= 0 && storage.H < HorizontalSize && storage.V >= 0 && storage.V < VerticalSize;
    }

    public bool ContainsVoxel(Location location) =>
        location.Z >= 0 && location.Z < Levels && ContainsHorizontal(location);

    // Camera-independent top-left of a voxel; its 32x32 bounds begin at B-16.
    public ProjectedPoint Project(Location location)
    {
        var x = checked(checked(location.X + location.Z) - Center);
        var y = checked(location.Y - location.Z);
        return new(checked(16 * checked(x + y)), checked(8 * checked(-x + y)));
    }
}
