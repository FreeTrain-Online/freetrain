// Source: ExAV 2.1.1.12, 883cf7d/trunk/core/controllers/terrain/MountainController.cs.
// Original authors, LGPL-2.1-or-later.
namespace FreeTrain.Online.Simulation;

public static class CornerTerrainSelection
{
    // The source selects the voxel whose NE corner is being edited. Offset is
    // relative to the projected top-left of the picked voxel, in source pixels.
    public static Location Select(Location picked, int offsetX, int offsetY, QuarterCorners corners)
    {
        if (offsetX < 8) return picked with { X = checked(picked.X - 1) };
        if (offsetX >= 24) return picked with { Y = checked(picked.Y + 1) };
        // Integer division deliberately matches C# truncation toward zero.
        if (offsetY >= (16 - (corners.NE + corners.SW) * 4) / 2)
            return picked with { X = checked(picked.X - 1), Y = checked(picked.Y + 1) };
        return picked;
    }
}
