using System.Buffers.Binary;
using System.Text.Json;
using System.Runtime.CompilerServices;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Persistence;

// terrain-packed-1: one byte per ground cell (six height bits, two surface bits),
// interned definitions, and fixed 43-byte occupant headers followed by realized
// three-byte trees. Integers are little endian; IDs/revisions/value remain Int64.
// This is an encoding of actual state, never a seed or an inferred terrain recipe.
public static class PackedTerrain
{
    public const string Format = "terrain-packed-1";
    public const int HeaderBytes = 43;
    private const ushort NoForest = ushort.MaxValue;
    private sealed record SnapshotGeometry(SpatialEntityRead[] Entities, TerrainOccupantRead[] Occupants, PackedTerrainRead Packed);
    private static readonly ConditionalWeakTable<TerrainCellRead[], SnapshotGeometry> snapshotGeometry = new();

    public static PackedTerrainRead Capture(SnapshotMessage snapshot)
    {
        var t = snapshot.Terrain ?? throw new ArgumentException("Missing terrain.");
        var cached = snapshotGeometry.GetValue(t.Cells, _ => new(snapshot.Entities!, t.Occupants, CaptureSnapshot(snapshot)));
        var p = ReferenceEquals(cached.Entities, snapshot.Entities) && ReferenceEquals(cached.Occupants, t.Occupants)
            ? cached.Packed : CaptureSnapshot(snapshot);
        return p with { Name = t.Name, WaterLevel = t.WaterLevel, ConstructionMode = t.ConstructionMode,
            GeometryRevision = t.GeometryRevision, NextEntityId = t.NextEntityId, Provenance = t.Provenance };
    }
    private static PackedTerrainRead CaptureSnapshot(SnapshotMessage snapshot)
    {
        var t = snapshot.Terrain ?? throw new ArgumentException("Missing terrain.");
        var definitions = new List<DefinitionRead>(); var indices = new Dictionary<DefinitionRead, ushort>();
        ushort Intern(DefinitionRead d)
        {
            if (indices.TryGetValue(d, out var index)) return index;
            if (definitions.Count >= NoForest) throw new InvalidDataException("saveTooLarge");
            index = checked((ushort)definitions.Count); definitions.Add(d); indices.Add(d, index); return index;
        }
        var cells = t.Cells.Select(c => checked((byte)(c.Ground | (c.Surface switch
            { "natural" => 0, "cut" => 1, "terrace" => 2, _ => throw new ArgumentException("Invalid surface.") }) << 6))).ToArray();
        var bytes = new byte[t.Occupants.Aggregate(0, (n, o) => checked(n + HeaderBytes + 3 * (o.Forest?.Trees.Length ?? 0)))];
        var offset = 0;
        var entities = snapshot.Entities!.ToDictionary(e => e.Id);
        foreach (var o in t.Occupants)
        {
            var e = entities[o.EntityId]; var row = bytes.AsSpan(offset, HeaderBytes);
            BinaryPrimitives.WriteInt64LittleEndian(row, e.Id); BinaryPrimitives.WriteInt64LittleEndian(row[8..], e.Revision);
            BinaryPrimitives.WriteUInt16LittleEndian(row[16..], Intern(e.Definition));
            BinaryPrimitives.WriteInt32LittleEndian(row[18..], e.Anchor.X); BinaryPrimitives.WriteInt32LittleEndian(row[22..], e.Anchor.Y);
            row[26] = checked((byte)e.Anchor.Z);
            row[27] = (byte)((e.Owned ? 1 : 0) | (e.SilentlyReclaimable ? 2 : 0) | (e.Footprint[0].Transparent ? 4 : 0));
            BinaryPrimitives.WriteInt64LittleEndian(row[28..], e.Value);
            row[36] = o.Kind switch { "mountain" => 0, "terrace" => 1, "creek" => 2, _ => throw new ArgumentException("Invalid kind.") };
            BinaryPrimitives.WriteUInt16LittleEndian(row[37..], checked((ushort)(o.NE << 12 | o.SE << 8 | o.SW << 4 | o.NW)));
            row[39] = checked((byte)o.CreekConnections);
            BinaryPrimitives.WriteUInt16LittleEndian(row[40..], o.Forest is { } f ? Intern(f.Definition) : NoForest);
            row[42] = checked((byte)(o.Forest?.Trees.Length ?? 0)); offset += HeaderBytes;
            if (o.Forest is { } forest) foreach (var tree in forest.Trees)
            { bytes[offset++] = checked((byte)tree.X); bytes[offset++] = checked((byte)tree.Y); bytes[offset++] = checked((byte)tree.Sprite); }
        }
        return new(Format, t.Name, t.WaterLevel, t.ConstructionMode, t.GeometryRevision, t.NextEntityId,
            t.Provenance, definitions.ToArray(), cells, t.Occupants.Length, bytes);
    }

    public static PackedTerrainRead Capture(TerrainSessionState state)
    {
        var w = state.World;
        var definitions = new List<DefinitionRead>();
        var indices = new Dictionary<DefinitionReference, ushort>();
        ushort Intern(DefinitionReference d)
        {
            if (indices.TryGetValue(d, out var index)) return index;
            if (definitions.Count >= NoForest) throw new InvalidDataException("saveTooLarge");
            index = checked((ushort)definitions.Count);
            definitions.Add(new(d.PackId, d.ContributionId, d.RecipeId)); indices.Add(d, index);
            return index;
        }
        var cells = new byte[w.Cells.Length];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = checked((byte)(w.Cells[i].Ground | (int)w.Cells[i].Surface << 6));
        var length = w.Occupants.Aggregate(0, (n, o) => checked(n + HeaderBytes + 3 * (o.Forest?.Trees.Length ?? 0)));
        var bytes = new byte[length];
        var offset = 0;
        // Both arrays are validated and sorted by stable ID by TerrainWorld.
        for (var i = 0; i < w.Occupants.Length; i++)
        {
            var o = w.Occupants[i]; var e = w.Spatial.Entities[i];
            if (e.Id != o.EntityId) throw new InvalidOperationException("Terrain identity order.");
            var row = bytes.AsSpan(offset, HeaderBytes);
            BinaryPrimitives.WriteInt64LittleEndian(row, e.Id);
            BinaryPrimitives.WriteInt64LittleEndian(row[8..], e.Revision);
            BinaryPrimitives.WriteUInt16LittleEndian(row[16..], Intern(e.Definition));
            BinaryPrimitives.WriteInt32LittleEndian(row[18..], e.Anchor.X);
            BinaryPrimitives.WriteInt32LittleEndian(row[22..], e.Anchor.Y);
            row[26] = checked((byte)e.Anchor.Z);
            row[27] = (byte)((e.Owned ? 1 : 0) | (e.SilentlyReclaimable ? 2 : 0) | (e.Footprint.Members[0].Transparent ? 4 : 0));
            BinaryPrimitives.WriteInt64LittleEndian(row[28..], e.Value);
            row[36] = (byte)o.Kind;
            BinaryPrimitives.WriteUInt16LittleEndian(row[37..], checked((ushort)o.Corners.Packed));
            row[39] = checked((byte)o.CreekConnections);
            BinaryPrimitives.WriteUInt16LittleEndian(row[40..], o.Forest is { } f ? Intern(f.Definition) : NoForest);
            row[42] = checked((byte)(o.Forest?.Trees.Length ?? 0));
            offset += HeaderBytes;
            if (o.Forest is { } forest) foreach (var t in forest.Trees)
            { bytes[offset++] = checked((byte)t.X); bytes[offset++] = checked((byte)t.Y); bytes[offset++] = checked((byte)t.Sprite); }
        }
        var p = state.Provenance;
        return new(Format, w.Name, w.WaterLevel, state.ConstructionMode, w.Revision, w.Spatial.NextEntityId,
            new(p.Profile, p.Source, p.RequestHash, p.RequestJson), definitions.ToArray(), cells, w.Occupants.Length, bytes);
    }

    public static TerrainSessionState Restore(this PackedTerrainRead data, MapRead map, TerrainProfile profile, ClockState clock, bool paused,
        RandomStreamsState random, long revision, IReadOnlyDictionary<Location, QuarterCorners>? tunnelBackups = null)
    {
        var (Encoding, Name, WaterLevel, ConstructionMode, GeometryRevision, NextEntityId, Provenance,
            Definitions, Cells, OccupantCount, Occupants) = data;
        // Bound dimensions/counts/lengths before allocating the expanded graph.
        var request = new EmptyWorldRequest(WorldCreationProfile.Id, Name, new(map.Width, map.Height, map.Levels), WaterLevel);
        if (Encoding != Format || map.CoordinateSystem != "hv" || !WorldCreationProfile.Validate(request).IsEmpty ||
            Cells.Length != checked(map.Width * map.Height) || Definitions.Length > profile.Definitions.Length ||
            Definitions.Distinct().Count() != Definitions.Length || OccupantCount < 0 || OccupantCount > 2L * Cells.Length ||
            Occupants.Length < (long)OccupantCount * HeaderBytes || Occupants.Length > (long)OccupantCount * (HeaderBytes + 18) ||
            Cells.LongLength * 24 + (long)OccupantCount * 1024 + Occupants.LongLength * 2 > WorldCreationProfile.MaximumRealizedTerrainBytes)
            throw new JsonException("Invalid packed terrain envelope.");
        var definitions = Definitions.Select(d => new DefinitionReference(d.PackId, d.ContributionId, d.RecipeId)).ToArray();
        foreach (var d in definitions) profile.Resolve(d);
        var cells = new TerrainCell[Cells.Length];
        // Share the tiny set of immutable cell values, including on restoration.
        var values = new TerrainCell[192];
        for (var i = 0; i < cells.Length; i++)
        {
            var value = Cells[i];
            if (value >= values.Length || (value & 63) >= map.Levels) throw new JsonException("Invalid packed ground.");
            cells[i] = values[value] ??= new(value & 63, (TerrainSurface)(value >> 6));
        }
        var entities = System.Collections.Immutable.ImmutableArray.CreateBuilder<SpatialEntity>(OccupantCount);
        var occupants = new TerrainOccupant[OccupantCount];
        var solid = new Footprint([new(new(0, 0, 0), false)]);
        var transparent = new Footprint([new(new(0, 0, 0), true)]);
        var offset = 0; long lastId = 0;
        for (var i = 0; i < OccupantCount; i++)
        {
            if (offset > Occupants.Length - HeaderBytes) throw new JsonException("Truncated packed occupant.");
            var row = Occupants.AsSpan(offset, HeaderBytes);
            var id = BinaryPrimitives.ReadInt64LittleEndian(row);
            var entityRevision = BinaryPrimitives.ReadInt64LittleEndian(row[8..]);
            var definition = BinaryPrimitives.ReadUInt16LittleEndian(row[16..]);
            var flags = row[27]; var kind = row[36];
            var corners = BinaryPrimitives.ReadUInt16LittleEndian(row[37..]);
            var forestIndex = BinaryPrimitives.ReadUInt16LittleEndian(row[40..]); var count = row[42];
            if (id <= lastId || definition >= definitions.Length || flags > 7 || kind > 2 || count > 6 ||
                forestIndex == NoForest && count != 0 || forestIndex != NoForest && forestIndex >= definitions.Length)
                throw new JsonException("Invalid packed occupant.");
            lastId = id;
            entities.Add(new(id, entityRevision, definitions[definition],
                new(BinaryPrimitives.ReadInt32LittleEndian(row[18..]), BinaryPrimitives.ReadInt32LittleEndian(row[22..]), row[26]),
                (flags & 4) != 0 ? transparent : solid, (flags & 1) != 0, (flags & 2) != 0,
                BinaryPrimitives.ReadInt64LittleEndian(row[28..])));
            offset += HeaderBytes;
            if (offset > Occupants.Length - count * 3) throw new JsonException("Truncated packed trees.");
            var trees = System.Collections.Immutable.ImmutableArray.CreateBuilder<TerrainTree>(count);
            for (var t = 0; t < count; t++) trees.Add(new(Occupants[offset++], Occupants[offset++], Occupants[offset++]));
            occupants[i] = new(id, (TerrainOccupantKind)kind,
                new(corners >> 12, (corners >> 8) & 15, (corners >> 4) & 15, corners & 15),
                forestIndex == NoForest ? null : new(definitions[forestIndex], trees.MoveToImmutable()), row[39]);
        }
        if (offset != Occupants.Length) throw new JsonException("Trailing packed occupant bytes.");
        var world = new TerrainWorld(profile, Name, request.Dimensions, WaterLevel, cells,
            new(NextEntityId, entities.MoveToImmutable()), occupants, GeometryRevision, tunnelBackups);
        return new(world, new(Provenance.Profile, Provenance.Source, Provenance.RequestHash, Provenance.RequestJson),
            ConstructionMode, clock, paused, random, revision);
    }
}
