using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

public class PackedTerrainTests
{
    [Fact]
    public void Independent_little_endian_vectors_preserve_the_complete_edited_scene()
    {
        // Literal Python struct.pack('<qqHiiBBqBHBHB') study: terrace at (1,0,1),
        // forest Mountain at (1,0,2), Creek at (2,1,2). Independent of this codec.
        var expected = Convert.FromHexString(
            "01000000000000000000000000000000000001000000000000000100000000000000000001341200ffff00" +
            "020000000000000000000000000000000100010000000000000002006400000000000000003412000200020000001e0f08" +
            "0300000000000000000000000000000003000200000001000000020600000000000000000200000dffff00");
        var original = TerrainTransportScenario.Create(); var packed = PackedTerrain.Capture(original);
        Assert.Equal(expected, packed.Occupants);
        Assert.Equal(new byte[] { 130, 66, 66, 66, 66, 66, 66, 66, 66 }, packed.Cells);
        Assert.Equal(4, packed.Definitions.Length); Assert.Equal(3, packed.OccupantCount);
        var restored = packed.Restore(new(3, 3, 6, "hv"), TerrainProfiles.Bundled,
            original.Clock.Capture(), original.Paused, original.Random.Capture(), original.Revision);
        Assert.Equal(SaveCodec.Write(original), SaveCodec.Write(restored));
        Assert.Same(restored.World.Cells[1], restored.World.Cells[8]);
    }

    [Fact]
    public void Packed_Int64_ids_and_entity_revisions_do_not_pass_through_floating_point()
    {
        var original = TerrainTransportScenario.Create(); var packed = PackedTerrain.Capture(original);
        foreach (var offset in new[] { 0, 43, 92 })
        {
            var row = packed.Occupants.AsSpan(offset);
            BinaryPrimitives.WriteInt64LittleEndian(row, 9007199254740992 + BinaryPrimitives.ReadInt64LittleEndian(row));
            BinaryPrimitives.WriteInt64LittleEndian(row[8..], long.MaxValue);
        }
        packed = packed with { NextEntityId = long.MaxValue };
        var restored = packed.Restore(new(3, 3, 6, "hv"), TerrainProfiles.Bundled,
            original.Clock.Capture(), original.Paused, original.Random.Capture(), original.Revision);
        Assert.Equal(9007199254740993, restored.World.Spatial.Entities[0].Id);
        Assert.All(restored.World.Spatial.Entities, e => Assert.Equal(long.MaxValue, e.Revision));
        Assert.Equal(packed.Occupants, PackedTerrain.Capture(restored).Occupants);
    }

    [Theory]
    [InlineData("encoding")][InlineData("countBomb")][InlineData("negativeCount")][InlineData("shortCells")]
    [InlineData("reservedSurface")][InlineData("shortRow")][InlineData("extraByte")][InlineData("flags")]
    [InlineData("definition")][InlineData("duplicateDefinition")][InlineData("idOrder")][InlineData("kind")]
    [InlineData("missingForest")][InlineData("treeCount")][InlineData("coordinates")][InlineData("oldTerrain")]
    public void Malformed_envelopes_or_binary_rows_are_rejected_before_world_replacement(string mutation)
    {
        var original = TerrainTransportScenario.Create(); var before = SaveCodec.Write(original);
        var node = JsonNode.Parse(before)!; var p = node["packedTerrain"]!;
        var cells = Convert.FromBase64String(p["cells"]!.GetValue<string>());
        var rows = Convert.FromBase64String(p["occupants"]!.GetValue<string>());
        switch (mutation)
        {
            case "encoding": p["encoding"] = "terrain-packed-0"; break;
            case "countBomb": p["occupantCount"] = int.MaxValue; break;
            case "negativeCount": p["occupantCount"] = -1; break;
            case "shortCells": cells = cells[..^1]; break;
            case "reservedSurface": cells[0] = 192; break;
            case "shortRow": rows = rows[..^1]; break;
            case "extraByte": rows = [..rows, 0]; break;
            case "flags": rows[27] = 8; break;
            case "definition": rows[16] = 254; break;
            case "duplicateDefinition": p["definitions"]!.AsArray().Add(p["definitions"]![0]!.DeepClone()); break;
            case "idOrder": rows[43] = 1; break;
            case "kind": rows[36] = 3; break;
            case "missingForest": rows[83] = 255; rows[84] = 255; break;
            case "treeCount": rows[85] = 255; break;
            case "coordinates": rows[18] = 255; break;
            case "oldTerrain": node["terrain"] = new JsonObject(); break;
        }
        p["cells"] = Convert.ToBase64String(cells); p["occupants"] = Convert.ToBase64String(rows);
        Assert.Throws<InvalidDataException>(() => SaveCodec.ReadWorld(JsonSerializer.SerializeToUtf8Bytes(node), TerrainProfiles.Resolve));
        Assert.Equal(before, SaveCodec.Write(original));
    }

    [Fact]
    public void Empty_128_squared_save_has_one_byte_per_cell_and_no_expanded_graph()
    {
        var request = new EmptyWorldRequest(WorldCreationProfile.Id, "capacity", new(128, 128, 16), 4);
        var state = new TerrainSessionState(TerrainWorld.Empty(request, TerrainProfiles.Bundled), TerrainProvenance.From(request));
        var bytes = SaveCodec.Write(state);
        Assert.True(bytes.Length < 32 * 1024);
        using var parsed = JsonDocument.Parse(bytes);
        Assert.False(parsed.RootElement.TryGetProperty("terrain", out _));
        Assert.Empty(parsed.RootElement.GetProperty("spatial").GetProperty("entities").EnumerateArray());
        Assert.Equal(bytes, SaveCodec.Write(SaveCodec.ReadWorld(bytes, TerrainProfiles.Resolve).Terrain!));
    }
}
