// Behavioral port: ExAV 2.1.1.12 / 883cf7d, TerrainLoader.cs (Kohsuke Kawaguchi et al., LGPL-2.1-or-later).
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Decoder-owned, untransformed RGBA8 raster. Simulation has no image library dependency.
public sealed class HeightmapRaster(int width, int height, byte[] rgba)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public ReadOnlyMemory<byte> Pixels { get; } = rgba;
    public float Depth(float h, int v, WorldDimensions d)
    {
        float x = (h + (v % 2) * 0.5f) * Width / d.H, y = (float)v * Height / d.V;
        int x1 = Math.Min((int)Math.Floor(x), Width - 1), x2 = Math.Min((int)Math.Ceiling(x), Width - 1);
        int y1 = Math.Min((int)Math.Floor(y), Height - 1), y2 = Math.Min((int)Math.Ceiling(y), Height - 1);
        float dx = x - x1, dy = y - y1;
        float Light(int xx, int yy)
        {
            var i = (yy * Width + xx) * 4; var p = Pixels.Span;
            return (Math.Min(p[i], Math.Min(p[i + 1], p[i + 2])) + Math.Max(p[i], Math.Max(p[i + 1], p[i + 2]))) / 510f;
        }
        float a = Light(x1, y1) * (1f - dx) + Light(x2, y1) * dx;
        float b = Light(x1, y2) * (1f - dx) + Light(x2, y2) * dx;
        return a * (1f - dy) + b * dy;
    }
}

public static class HeightmapTerrain
{
    public static TerrainWorld Create(HeightmapWorldRequest request, HeightmapRaster image, TerrainProfile profile, CancellationToken cancellation = default)
    {
        if (!WorldCreationProfile.Validate(request).IsEmpty || image.Width != request.Image.Width || image.Height != request.Image.Height ||
            image.Pixels.Length != (long)image.Width * image.Height * 4) throw new ArgumentException("Invalid heightmap raster.");
        var work = new GenerationWork(request, cancellation);
        for (var i = 0; i < image.Pixels.Length; i += 4)
        { work.Spend(1); if (image.Pixels.Span[i + 3] != 255) throw new ArgumentException("image-alpha"); }
        var d = request.Dimensions; var space = new WorldSpace(d.H, d.V, d.Z);
        var ground = new int[d.H * d.V]; var smooth = new byte[ground.Length];
        int Index(Location p) { var s = space.ToStorage(p); return s.V * d.H + s.H; }
        (int Min, int Max) Range(Location p, int size)
        {
            int min = int.MaxValue, max = int.MinValue;
            for (int y = p.Y; y < p.Y + size; y++) for (int x = p.X; x < p.X + size; x++)
            {
                work.Spend(1); var q = new Location(x, y, 0);
                if (!space.ContainsHorizontal(q)) continue;
                int z = ground[Index(q)]; min = Math.Min(min, z); max = Math.Max(max, z);
            }
            return (min, max);
        }
        for (int v = 0; v < d.V; v++) for (int h = 0; h < d.H; h++)
        { work.Spend(8); ground[v * d.H + h] = (int)Math.Min(d.Z - 1, d.Z * image.Depth(h, v, d)); }
        bool changed; var scans = 0;
        do
        {
            if (++scans > 64 * d.Z) throw new InvalidOperationException("generation-work-limit");
            changed = false;
            for (int v = -1; v < d.V; v++) for (int h = -1; h < d.H; h++)
            {
                var p = space.ToLocation(new(h, v, 0)); var range = Range(p, 2);
                for (int y = p.Y; y <= p.Y + 1; y++) for (int x = p.X; x <= p.X + 1; x++)
                {
                    work.Spend(1); var q = new Location(x, y, 0); if (!space.ContainsHorizontal(q)) continue;
                    int i = Index(q); if (ground[i] > range.Min + 1) { ground[i] = range.Min + 1; changed = true; }
                }
            }
        } while (changed);
        for (int v = 0; v < d.V; v++) for (int h = 0; h < d.H; h++)
        {
            var p = space.ToLocation(new(h, v, 0)); var r = Range(p, 2);
            if (r.Min != r.Max || Range(new(p.X - 1, p.Y - 1, 0), 4).Max == r.Max) continue;
            work.Spend(8); smooth[v * d.H + h] = (byte)((int)Math.Min(4 * d.Z - 1, 4 * d.Z * image.Depth(h + 0.5f, v, d)) % 4);
        }
        int Corner(int x, int y)
        {
            var p = new Location(x, y, 0); var r = Range(p, 2);
            if (r.Max - r.Min > 1 || r.Max < 0) throw new ArgumentException("Invalid heightmap corners.");
            return r.Max * 4 + (r.Min == r.Max && space.ContainsHorizontal(p) ? smooth[Index(p)] : 0);
        }
        var entities = ImmutableArray.CreateBuilder<SpatialEntity>(); var occupants = ImmutableArray.CreateBuilder<TerrainOccupant>();
        var mountain = profile.Definitions.Single(x => x.Reference.ContributionId == TerrainProfile.MountainId).Reference;
        for (int v = 0; v < d.V; v++) for (int h = 0; h < d.H; h++)
        {
            work.Spend(1); int g = ground[v * d.H + h]; var p = space.ToLocation(new(h, v, g));
            var corners = new QuarterCorners(Corner(p.X, p.Y - 1) - 4 * g, Corner(p.X, p.Y) - 4 * g,
                Corner(p.X - 1, p.Y) - 4 * g, Corner(p.X - 1, p.Y - 1) - 4 * g);
            if (corners.Flat) continue;
            long id = entities.Count + 1;
            entities.Add(new(id, 0, mountain, p, new([new(new(0, 0, 0), false)]), false, false, 0));
            occupants.Add(new(id, TerrainOccupantKind.Mountain, corners));
        }
        cancellation.ThrowIfCancellationRequested();
        return new(profile, request.Name, d, request.WaterLevel, ground.Select(g => new TerrainCell(g)),
            new(entities.Count + 1, entities.ToImmutable()), occupants);
    }
}
