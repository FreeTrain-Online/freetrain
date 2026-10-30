using System.Buffers.Binary;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Session;

// Creation-only overview, derived from the retained state, never regeneration.
// Samples are bounded independently of the full ground grid and save transfer.
public sealed record TerrainPreview(int Width, int Height, int Step, string Samples, TerrainPreviewMetrics Metrics)
{
    public const int MaximumSide = 128;
    public static TerrainPreview Capture(TerrainWorld world, string? startingMask = null, CancellationToken cancellation = default)
    {
        int h = world.Space.HorizontalSize, v = world.Space.VerticalSize, length = world.Cells.Length;
        var corners = new ushort[length]; var flags = new byte[length];
        for (int k = 0; k < world.Occupants.Length; k++)
        {
            if ((k & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            var o = world.Occupants[k]; var p = world.Space.ToStorage(world.Spatial.Entities[k].Anchor);
            int i = p.V * h + p.H;
            if (o.Kind == TerrainOccupantKind.Mountain) corners[i] = (ushort)o.Corners.Packed;
            // Even a zero-tree forest cell is occupied by its realized treatment.
            if (o.Forest != null) flags[i] |= 2;
            if (o.Forest != null || o.Kind != TerrainOccupantKind.Mountain) flags[i] |= 16;
        }
        var reserved = startingMask == null ? null : Convert.FromBase64String(startingMask);
        int wet = 0, forest = 0, flat = 0, min = int.MaxValue, max = 0;
        for (int i = 0; i < length; i++)
        {
            if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            int ground = world.Cells[i].Ground, q = corners[i];
            min = Math.Min(min, 4 * ground + Math.Min(Math.Min(q >> 12, q >> 8 & 15), Math.Min(q >> 4 & 15, q & 15)));
            max = Math.Max(max, 4 * ground + Math.Max(Math.Max(q >> 12, q >> 8 & 15), Math.Max(q >> 4 & 15, q & 15)));
            if (ground * 4 < world.WaterLevel * 4 &&
                (ground * 4 + (q >> 12) < world.WaterLevel * 4 || ground * 4 + (q >> 8 & 15) < world.WaterLevel * 4 ||
                 ground * 4 + (q >> 4 & 15) < world.WaterLevel * 4 || ground * 4 + (q & 15) < world.WaterLevel * 4))
            { flags[i] |= 1; wet++; }
            if ((flags[i] & 2) != 0) forest++;
            if (reserved != null && (reserved[i / 8] & (1 << (i % 8))) != 0) flags[i] |= 4;
            if ((flags[i] & 17) == 0 && q == 0 && world.Cells[i].Surface == TerrainSurface.Natural)
            { flags[i] |= 8; flat++; }
        }
        // Same-height, clear, flat dry land connected through canonical XY faces.
        // H/V image-adjacent cells are not canonical neighbors.
        var queue = new int[length]; int largest = 0;
        for (int start = 0; start < length; start++)
        {
            if ((start & 4095) == 0) cancellation.ThrowIfCancellationRequested();
            if ((flags[start] & 40) != 8) continue;
            int head = 0, tail = 1; queue[0] = start; flags[start] |= 32;
            while (head < tail)
            {
                if ((head & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                int i = queue[head++], x = i % h, y = i / h, right = x + y % 2;
                for (int row = y - 1; row <= y + 1; row += 2)
                for (int column = right - 1; column <= right; column++)
                {
                    if (row < 0 || row >= v || column < 0 || column >= h) continue;
                    int n = row * h + column;
                    if ((flags[n] & 40) != 8 || world.Cells[n].Ground != world.Cells[i].Ground) continue;
                    flags[n] |= 32; queue[tail++] = n;
                }
            }
            largest = Math.Max(largest, tail);
        }
        int step = (Math.Max(h, v) + MaximumSide - 1) / MaximumSide;
        int width = (h + step - 1) / step, height = (v + step - 1) / step;
        var samples = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                int i = Math.Min(v - 1, y * step) * h + Math.Min(h - 1, x * step), offset = (y * width + x) * 4;
                samples[offset] = (byte)world.Cells[i].Ground;
                BinaryPrimitives.WriteUInt16LittleEndian(samples.AsSpan(offset + 1, 2), corners[i]);
                samples[offset + 3] = (byte)(flags[i] & 15);
            }
        }
        return new(width, height, step, Convert.ToBase64String(samples),
            new(length, wet, forest, flat, largest, min, max));
    }
}

// Heights are quarter levels, including realized Mountain corners. Buildable is
// geometric availability, not a promise about later tool cost/ownership rules.
public sealed record TerrainPreviewMetrics(int Cells, int WetCells, int ForestCells, int ClearFlatCells,
    int LargestBuildableArea, int MinimumQuarterHeight, int MaximumQuarterHeight);
