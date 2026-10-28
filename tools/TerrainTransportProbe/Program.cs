using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;

var json = new JsonSerializerOptions(WireJson.CreateOptions()) { WriteIndented = true };
if (args is ["--write-fixtures", var root])
{
    var state = TerrainTransportScenario.Create();
    var saved = JsonSerializer.Deserialize<SaveCodec.Document>(SaveCodec.Write(state), WireJson.CreateOptions())!;
    File.WriteAllText(Path.Combine(root, "prototype/protocol/saves/terrain-v6.json"), JsonSerializer.Serialize(saved, json) + "\n");
    var before = TerrainWire.Snapshot(state, 1);
    File.WriteAllText(Path.Combine(root, "prototype/protocol/fixtures/snapshot-terrain.json"), JsonSerializer.Serialize<WireMessage>(before, json) + "\n");
    state.Replace(state.Revision, state.World.Replace(state.World.Revision,
        state.World.Cells.Select(c => c with { Ground = 1, Surface = TerrainSurface.Cut }), 0, new(7, []), []), false);
    File.WriteAllText(Path.Combine(root, "prototype/protocol/fixtures/delta-terrain.json"), JsonSerializer.Serialize<WireMessage>(NetworkSession.Difference(before, TerrainWire.Snapshot(state, 1)), json) + "\n");
    var edited = TerrainTransportScenario.Create();
    edited.Replace(edited.Revision, TerrainTransportScenario.Edit(edited), true);
    File.WriteAllText(Path.Combine(root, "prototype/protocol/fixtures/delta-terrain-update.json"), JsonSerializer.Serialize<WireMessage>(NetworkSession.Difference(before, TerrainWire.Snapshot(edited, 1)), json) + "\n");
    return;
}
Console.WriteLine(JsonSerializer.Serialize(new { runtime = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString() }));
// Actual maximum-density terrain: two occupants/cell, six realized trees and distinct cut/terrace geometry.
// No browser/generator/capacity acceptance is inferred from this serialization probe.
foreach (var side in new[] { 30, 64, 128 })
{
    var p = TerrainProfiles.Bundled; var forest = p.Definitions.First(d => d.TreeSpriteCount > 0);
    var mountain = p.Definitions.First(d => d.Kind == TerrainOccupantKind.Mountain && d.TreeSpriteCount == 0).Reference;
    var terrace = p.Definitions.Single(d => d.Kind == TerrainOccupantKind.Terrace).Reference;
    var request = new EmptyWorldRequest(WorldCreationProfile.Id, "Measured terrain", new(side, side, 32), 1);
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var allocated = GC.GetTotalAllocatedBytes(true); var watch = Stopwatch.StartNew();
    var space = new WorldSpace(side, side, 32); var entities = ImmutableArray.CreateBuilder<SpatialEntity>();
    var occupants = new List<TerrainOccupant>(); long id = 1;
    for (var v = 0; v < side; v++) for (var h = 0; h < side; h++)
    {
        var anchor = space.ToLocation(new(h, v, 1));
        entities.Add(new(id, 0, terrace, anchor, new([new(new(0, 0, 0), false)]), false, false, 0));
        occupants.Add(new(id++, TerrainOccupantKind.Terrace, new(1, 2, 3, 4)));
        entities.Add(new(id, 0, mountain, new(anchor.X, anchor.Y, 2), new([new(new(0, 0, 0), false)]), false, false, forest.Price));
        occupants.Add(new(id++, TerrainOccupantKind.Mountain, new(1, 2, 3, 4), new(forest.Reference,
            Enumerable.Repeat(new TerrainTree(30, 15, 8), 6).ToImmutableArray())));
    }
    var state = new TerrainSessionState(new(p, request.Name, request.Dimensions, 3,
        Enumerable.Repeat(new TerrainCell(2, TerrainSurface.Terrace), side * side), new(id, entities.ToImmutable()), occupants), TerrainProvenance.From(request));
    var constructedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
    var snapshot = TerrainWire.Snapshot(state, 1);
    var snapshotBytes = JsonSerializer.SerializeToUtf8Bytes<WireMessage>(snapshot, NetworkSession.Json);
    byte[]? save = null; string? saveError = null;
    try { save = SaveCodec.Write(state); } catch (InvalidDataException ex) { saveError = ex.Message; }
    var serializedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
    if (save != null) SaveCodec.ReadWorld(save, TerrainProfiles.Resolve);
    watch.Stop();
    var envelope = WorldCreationProfile.Estimate(request);
    Console.WriteLine(JsonSerializer.Serialize(new { side, cells = side * side, entities = entities.Count,
        constructedBytes, serializedAllocatedBytes = serializedBytes, totalAllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated,
        snapshotBytes = snapshotBytes.Length, saveBytes = save?.Length, saveError,
        reservation = envelope.TransferBytes, fitsProvisionalReservation = snapshotBytes.Length <= envelope.TransferBytes,
        fitsProvisionalAllocationAllowance = GC.GetTotalAllocatedBytes(true) - allocated <= envelope.WorkingBytes,
        elapsedMs = watch.Elapsed.TotalMilliseconds }));
}
