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
if(args is ["--station-capacity",var stationMode,var stationDirectory]){StationCapacity.Run(stationMode,stationDirectory);return;}
if(args is ["--fleet-capacity",var fleetMode,var fleetDirectory]){FleetCapacity.Run(fleetMode,fleetDirectory);return;}
if(args is ["--railway-verify",var railVerificationDirectory]){RailwayCapacity.Verify(railVerificationDirectory);return;}
if(args is ["--railway-capacity",var railMode,var railDirectory]){RailwayCapacity.Run(railMode,railDirectory);return;}
if(args is ["--acceptance",var acceptanceManifest,var acceptanceDirectory]){await TerrainAcceptance.Run(acceptanceManifest,acceptanceDirectory);return;}
if(args is ["--authoring-capacity",var authorH,var authorV,var authorZ,var authorDirectory])
{
    int h=int.Parse(authorH),v=int.Parse(authorV),z=int.Parse(authorZ);Directory.CreateDirectory(authorDirectory);
    var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Authoring capacity",new(h,v,z),1);
    var world=TerrainWorld.Empty(request,TerrainProfiles.Bundled);
    using var host=new FreeTrain.Online.Session.SessionHost(new TerrainSessionState(world,TerrainProvenance.From(request)),"owner",TerrainProfiles.Resolve);
    var p=world.Space.ToLocation(new(h/2,v/2,1));var measurements=new List<object>();
    foreach(var action in new[]{"enter","plantForest","placeCreek","exit"})
    {
        long revision=host.Snapshot().Terrain!.GeometryRevision;CornerTerrainPreviewRead? preview=null;
        var tree=world.Profile.Definitions.First(d=>d.TreeSpriteCount>0);
        var query=new CornerTerrainQuery(1,revision,new(p.X,p.Y,p.Z),action,
            Rectangle:new(new(p.X+(action=="placeCreek"?63:7),p.Y+(action=="placeCreek"?12:7),1)),
            Forest:action=="plantForest"?new(tree.Reference.PackId,tree.Reference.ContributionId,tree.Reference.RecipeId):null);
        // The creek is a separate 64-cell straight route, outside the forest block.
        if(action=="placeCreek")query=query with{Position=new(p.X,p.Y+12,1)};
        var watch=Stopwatch.StartNew();
        if(action is "plantForest" or "placeCreek")preview=host.PreviewCorner(query);
        var previewMs=watch.Elapsed.TotalMilliseconds;if(preview is {Legal:false})throw new Exception("Authoring preview rejected.");
        CommandPayload payload=action is "enter" or "exit"?new SetAuthoringModeCommand(revision,action=="enter"):new EditCornerCommand(query);
        watch.Restart();var command=host.Enqueue(new("owner"),new(action,1,payload));host.Pump(0);
        if(await command is not AcceptedMessage)throw new Exception("Authoring commit rejected.");var commitMs=watch.Elapsed.TotalMilliseconds;
        watch.Restart();var capture=host.CaptureSave();host.Pump(0);var bytes=await capture;var reopened=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        if(!bytes.SequenceEqual(SaveCodec.Write(reopened))||reopened.ConstructionMode!=(action!="exit"))throw new Exception("Authoring continuation mismatch.");
        foreach(var decoration in preview?.Decorations??[])
        {
            var position=decoration.Position;var actual=reopened.World.OccupantAt(new(position.X,position.Y,position.Z))!;
            if(actual.CreekConnections!=decoration.CreekConnections||
                decoration.Forest is {} f&&!f.Trees.Select(t=>(t.X,t.Y,t.Sprite)).SequenceEqual(actual.Forest!.Trees.Select(t=>(t.X,t.Y,t.Sprite))))throw new Exception("Authoring preview mismatch.");
        }
        File.WriteAllBytes(Path.Combine(authorDirectory,action+"-v7.json"),bytes);
        measurements.Add(new{h,v,z,action,changed=preview?.Changes.Length??0,previewMs,commitMs,saveReopenMs=watch.Elapsed.TotalMilliseconds,saveBytes=bytes.Length,managedBytes=GC.GetTotalMemory(true)});
    }
    Console.WriteLine(JsonSerializer.Serialize(measurements,json));return;
}
if (args is ["--terrace-capacity",var terraceH,var terraceV,var terraceZ,var terraceDirectory])
{
    int h=int.Parse(terraceH),v=int.Parse(terraceV),z=int.Parse(terraceZ);Directory.CreateDirectory(terraceDirectory);
    var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Terrace capacity",new(h,v,z),1);
    var original=TerrainWorld.Empty(request,TerrainProfiles.Bundled);var p=original.Space.ToLocation(new(h/2,v/2,1));
    var draft=new CornerTerrainDraft(original);RectangleTerrain.Plan(draft,p,p with{X=p.X+1,Y=p.Y+1},"raise");var world=draft.Finish();
    using var host=new FreeTrain.Online.Session.SessionHost(new TerrainSessionState(world,TerrainProvenance.From(request),revision:world.Revision),"owner",TerrainProfiles.Resolve);
    var measurements=new List<object>();
    foreach(var action in new[]{"placeTerrace","removeTerrace","placeCut","removeCut"})
    {
        var query=new CornerTerrainQuery(1,world.Revision,new(p.X,p.Y,p.Z),action);
        var watch=Stopwatch.StartNew();var preview=host.PreviewCorner(query);var previewMs=watch.Elapsed.TotalMilliseconds;
        if(!preview.Legal||preview.Changes.Length!=1)throw new Exception("Terrace preview rejected.");
        watch.Restart();var command=host.Enqueue(new("owner"),new(action,1,new EditCornerCommand(query)));host.Pump(0);
        if(await command is not AcceptedMessage)throw new Exception("Terrace commit rejected.");var commitMs=watch.Elapsed.TotalMilliseconds;
        watch.Restart();var capture=host.CaptureSave();host.Pump(0);var bytes=await capture;var reopened=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        if(!bytes.SequenceEqual(SaveCodec.Write(reopened)))throw new Exception("Terrace save mismatch.");
        world=reopened.World;var o=world.OccupantAt(p);var cell=world.Cell(world.Space.ToStorage(p).H,world.Space.ToStorage(p).V);
        if(cell.Ground!=(action=="placeTerrace"?2:1)|| (action=="placeCut"?o!=null:o?.Corners.Packed!=0x0400)||
            action=="placeTerrace"&&o?.Kind!=TerrainOccupantKind.Terrace)throw new Exception("Terrace geometry mismatch.");
        File.WriteAllBytes(Path.Combine(terraceDirectory,action+"-v7.json"),bytes);
        measurements.Add(new{h,v,z,action,previewMs,commitMs,saveReopenMs=watch.Elapsed.TotalMilliseconds,saveBytes=bytes.Length,managedBytes=GC.GetTotalMemory(true)});
    }
    Console.WriteLine(JsonSerializer.Serialize(measurements,json));return;
}
if (args is ["--rectangle-capacity", var rectangleH, var rectangleV, var rectangleZ, var rectangleDirectory])
{
    int h=int.Parse(rectangleH),v=int.Parse(rectangleV),z=int.Parse(rectangleZ);
    Directory.CreateDirectory(rectangleDirectory);
    var measurements=new List<object>();
    foreach(var action in new[]{"raise","lower","repair"})
    {
        var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Rectangle capacity",new(h,v,z),1);
        var world=TerrainWorld.Empty(request,TerrainProfiles.Bundled);
        var p=world.Space.ToLocation(new(h/2,v/2,1));
        if(action=="repair")
        {
            var cells=world.Cells.ToBuilder();cells[(v/2)*h+h/2]=new(0,TerrainSurface.Cut);
            world=new(world.Profile,world.Name,new(h,v,z),1,cells,new(1,[]),[]);
        }
        using var host=new FreeTrain.Online.Session.SessionHost(new TerrainSessionState(world,TerrainProvenance.From(request)),"owner",TerrainProfiles.Resolve);
        var query=new CornerTerrainQuery(1,0,new(p.X,p.Y,p.Z),action,Rectangle:new(new(p.X+7,p.Y+7,p.Z)));
        var watch=Stopwatch.StartNew();var preview=host.PreviewCorner(query);var previewMs=watch.Elapsed.TotalMilliseconds;
        if(!preview.Legal)throw new Exception("Rectangle preview rejected.");
        watch.Restart();var command=host.Enqueue(new("owner"),new("rectangle-capacity",1,new EditCornerCommand(query)));host.Pump(0);
        if(await command is not AcceptedMessage)throw new Exception("Rectangle commit rejected.");var commitMs=watch.Elapsed.TotalMilliseconds;
        watch.Restart();var capture=host.CaptureSave();host.Pump(0);var bytes=await capture;
        var reopened=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
        if(!bytes.SequenceEqual(SaveCodec.Write(reopened)))throw new Exception("Rectangle continuation mismatch.");
        if(reopened.World.Revision!=1 || reopened.World.FineHeight(p,1)!=(action=="raise"?8:action=="lower"?0:4))throw new Exception("Rectangle result mismatch.");
        File.WriteAllBytes(Path.Combine(rectangleDirectory,action+"-v7.json"),bytes);
        measurements.Add(new{h,v,z,action,selectedCells=64,sites=action=="repair"?64:49,changed=preview.Changes.Length,skipped=preview.Skipped.Length,
            previewMs,commitMs,saveReopenMs=watch.Elapsed.TotalMilliseconds,saveBytes=bytes.Length,managedBytes=GC.GetTotalMemory(true)});
    }
    Console.WriteLine(JsonSerializer.Serialize(measurements,json));return;
}
if (args is ["--corner-capacity", var cornerH, var cornerV, var cornerZ, var cornerDirectory])
{
    int h=int.Parse(cornerH),v=int.Parse(cornerV),z=int.Parse(cornerZ);
    Directory.CreateDirectory(cornerDirectory);
    var request=new EmptyWorldRequest(WorldCreationProfile.Id,"Corner capacity",new(h,v,z),1);
    var world=TerrainWorld.Empty(request,TerrainProfiles.Bundled);
    using var host=new FreeTrain.Online.Session.SessionHost(new TerrainSessionState(world,TerrainProvenance.From(request)),"owner",TerrainProfiles.Resolve);
    var p=world.Space.ToLocation(new(h/2,v/2,1));
    var query=new CornerTerrainQuery(1,0,new(p.X,p.Y,p.Z),"raise",Stroke:Enumerable.Range(1,63)
        .Select(i=>new CornerTerrainSite(new(p.X+i%8,p.Y+i/8,p.Z),"raise")).ToArray());
    var watch=Stopwatch.StartNew();var preview=host.PreviewCorner(query);var previewMs=watch.Elapsed.TotalMilliseconds;
    if(!preview.Legal)throw new Exception("Corner preview rejected.");
    watch.Restart();var command=host.Enqueue(new("owner"),new("corner-capacity",1,new EditCornerCommand(query)));host.Pump(0);
    if(await command is not AcceptedMessage)throw new Exception("Corner commit rejected.");var commitMs=watch.Elapsed.TotalMilliseconds;
    watch.Restart();var capture=host.CaptureSave();host.Pump(0);var bytes=await capture;
    var reopened=SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!;
    if(!bytes.SequenceEqual(SaveCodec.Write(reopened)))throw new Exception("Corner continuation mismatch.");
    if(reopened.World.FineHeight(p,0)!=5||reopened.World.Revision!=1)throw new Exception("Corner result mismatch.");
    File.WriteAllBytes(Path.Combine(cornerDirectory,"corner-v7.json"),bytes);
    Console.WriteLine(JsonSerializer.Serialize(new{h,v,z,sites=64,changed=preview.Changes.Length,skipped=preview.Skipped.Length,
        previewMs,commitMs,saveReopenMs=watch.Elapsed.TotalMilliseconds,saveBytes=bytes.Length,managedBytes=GC.GetTotalMemory(true)},json));return;
}
if (args is ["--compare-png", var first, var second])
{
    var a = HeightmapDecoder.Decode(File.ReadAllBytes(first)).Raster;
    var b = HeightmapDecoder.Decode(File.ReadAllBytes(second)).Raster;
    if (a.Width != b.Width || a.Height != b.Height) throw new ArgumentException("Image extents differ.");
    int changed = 0, maximum = 0, left = a.Width, top = a.Height, right = -1, bottom = -1;
    var samples = new List<object>();
    for (var i = 0; i < a.Pixels.Length; i += 4)
    {
        if (a.Pixels.Span.Slice(i, 4).SequenceEqual(b.Pixels.Span.Slice(i, 4))) continue;
        changed++; var x = i / 4 % a.Width; var y = i / 4 / a.Width;
        left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
        for (var c = 0; c < 4; c++) maximum = Math.Max(maximum, Math.Abs(a.Pixels.Span[i+c] - b.Pixels.Span[i+c]));
        if (samples.Count < 10) samples.Add(new { x, y, a = a.Pixels.Slice(i, 4).ToArray(), b = b.Pixels.Slice(i, 4).ToArray() });
    }
    Console.WriteLine(JsonSerializer.Serialize(new { a.Width, a.Height, changed, maximum, left, top, right, bottom, samples }, json)); return;
}
if (args is ["--capacity", var hText, var vText, var zText, var mode, var directory])
{
    var h = int.Parse(hText); var v = int.Parse(vText); var z = int.Parse(zText);
    Directory.CreateDirectory(directory);
    var process = Process.GetCurrentProcess(); var start = Stopwatch.StartNew();
    TerrainSessionState state;
    if (mode == "dense" || mode == "heavy") state = TerrainCapacityScenario.Create(h, v, z, mode == "dense" ? 1 : 8);
    else if (mode == "heightmap")
    {
        var rgba = new byte[128 * 128 * 4];
        for (var y = 0; y < 128; y++) for (var x = 0; x < 128; x++)
        { var i = 4 * (y * 128 + x); var c = (byte)((3 * x + 5 * y) % 256); rgba[i] = rgba[i+1] = rgba[i+2] = c; rgba[i+3] = 255; }
        var source = Convert.FromBase64String(HeightmapDecoder.Preview(new(128, 128, rgba)).Split(',')[1]);
        File.WriteAllBytes(Path.Combine(directory, "source.png"), source);
        var decoded = HeightmapDecoder.Decode(source);
        var r = new HeightmapWorldRequest(WorldCreationProfile.Id, "Capacity heightmap", new(h, v, z), 4, decoded.Input);
        state = new(HeightmapTerrain.Create(r, decoded.Raster, TerrainProfiles.Bundled), TerrainProvenance.From(r));
    }
    else if (mode == "generated" || mode.StartsWith("forest-", StringComparison.Ordinal) || mode.StartsWith("coast-", StringComparison.Ordinal) || mode.StartsWith("water-", StringComparison.Ordinal))
    {
        var r = WorldCreationProfile.Generated(int.MinValue, "Capacity relief") with { Dimensions = new(h, v, z) };
        r = r with { Options = r.Options with { StartingArea = 0, Relief = new(ReliefKind.Mountains, 100, 4, 0) } };
        if (mode.StartsWith("coast-", StringComparison.Ordinal))
        {
            var sides = mode[6..] switch { "north" => CoastSides.North, "east" => CoastSides.East, "south" => CoastSides.South,
                "west" => CoastSides.West, "adjacent" => CoastSides.North | CoastSides.East, "opposite" => CoastSides.North | CoastSides.South,
                "island" => CoastSides.None, _ => throw new ArgumentException("Unknown coast capacity mode.") };
            r = r with { Options = r.Options with { Coast = new(sides == CoastSides.None ? CoastKind.Island : CoastKind.Sides, sides, 40) } };
        }
        if (mode.StartsWith("water-", StringComparison.Ordinal))
        {
            if (mode is not ("water-lakes" or "water-coast" or "water-edge" or "water-inland")) throw new ArgumentException("Unknown water capacity mode.");
            r = r with { Options = r.Options with { Lakes = new(mode == "water-edge" ? 0 : 2, mode == "water-edge" ? 0 : 3),
                Rivers = new(mode == "water-lakes" ? RiverDirection.None : mode == "water-edge" ? RiverDirection.EastWest : RiverDirection.NorthSouth, 3, 80),
                Coast = mode == "water-coast" ? new(CoastKind.Sides, CoastSides.North, 30) : new(CoastKind.Inland, CoastSides.None, 0) } };
        }
        if (mode.StartsWith("forest-", StringComparison.Ordinal))
        {
            if (mode is not ("forest-dense" or "forest-coast" or "forest-sparse")) throw new ArgumentException("Unknown forest capacity mode.");
            var palette = WorldCreationCapabilities.ForestChoices().Where(c => c.Variant == "mountain").Select(c => c.Binding).ToImmutableArray();
            r = r with { Options = r.Options with { StartingArea = mode == "forest-coast" ? 16 : 64,
                Forests = new(mode == "forest-sparse" ? 15 : 90, 80, palette),
                Relief = mode == "forest-coast" ? r.Options.Relief : new(ReliefKind.Hills, 50, 24, 50),
                Coast = mode == "forest-coast" ? new(CoastKind.Sides, CoastSides.North, 30) : r.Options.Coast,
                Lakes = mode == "forest-coast" ? new(2, 3) : r.Options.Lakes,
                Rivers = mode == "forest-coast" ? new(RiverDirection.NorthSouth, 3, 80) : r.Options.Rivers } };
        }
        state = new(ReliefTerrain.Create(r, TerrainProfiles.Bundled), TerrainProvenance.From(r));
    }
    else if (mode == "empty")
    {
        var r = new EmptyWorldRequest(WorldCreationProfile.Id, "Capacity empty", new(h, v, z), 1);
        state = new(TerrainWorld.Empty(r, TerrainProfiles.Bundled), TerrainProvenance.From(r));
    }
    else throw new ArgumentException("Capacity mode must be empty, heightmap, generated, coast-{island,north,east,south,west,adjacent,opposite}, water-{lakes,inland,coast,edge}, forest-{sparse,dense,coast}, heavy or dense.");
    var createMs = start.Elapsed.TotalMilliseconds; var retainedWorld = GC.GetTotalMemory(true);
    var bytes = SaveCodec.Write(state); var saveMs = start.Elapsed.TotalMilliseconds - createMs;
    File.WriteAllBytes(Path.Combine(directory, "world.json"), bytes);
    var snapshot = TerrainWire.Snapshot(state, 1); var packet = StateTransfer.Serialize(snapshot);
    File.WriteAllBytes(Path.Combine(directory, "packet.json"), packet);
    var beforeRead = start.Elapsed.TotalMilliseconds;
    var restored = SaveCodec.ReadWorld(bytes, TerrainProfiles.Resolve).Terrain!;
    var readMs = start.Elapsed.TotalMilliseconds - beforeRead;
    if (!bytes.SequenceEqual(SaveCodec.Write(restored))) throw new InvalidOperationException("Capacity round trip mismatch.");
    var chunkCount = StateTransfer.Chunks(snapshot, packet).Count();
    process.Refresh();
    var geography = mode.StartsWith("forest-", StringComparison.Ordinal) || mode.StartsWith("coast-", StringComparison.Ordinal) || mode.StartsWith("water-", StringComparison.Ordinal) ? WaterTerrain.Validate(
        (GeneratedWorldRequest)JsonSerializer.Deserialize<WorldCreationRequest>(state.Provenance.RequestJson)!, state.World) : null;
    var forests = mode.StartsWith("forest-", StringComparison.Ordinal) ? ForestTerrain.Measure(
        (GeneratedWorldRequest)JsonSerializer.Deserialize<WorldCreationRequest>(state.Provenance.RequestJson)!, state.World,
        ForestTerrain.Layout((GeneratedWorldRequest)JsonSerializer.Deserialize<WorldCreationRequest>(state.Provenance.RequestJson)!, state.World)) : null;
    var report = new { mode, h, v, z, geography, forests, entities = state.World.Occupants.Length, createMs, saveMs, readMs,
        saveBytes = bytes.Length, packetBytes = packet.Length, chunkCount, retainedWorld,
        retainedTwoWorldsAndTransport = GC.GetTotalMemory(true), peakRss = process.PeakWorkingSet64 > 0 ? (long?)process.PeakWorkingSet64 : null,
        elapsedMs = start.Elapsed.TotalMilliseconds, runtime = RuntimeInformation.FrameworkDescription,
        os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount };
    var output = JsonSerializer.Serialize(report, json); Console.WriteLine(output);
    File.WriteAllText(Path.Combine(directory, "measurement.json"), output + "\n");
    return;
}
if (args is ["--write-fixtures", var root])
{
    var state = TerrainTransportScenario.Create();
    var saved = JsonSerializer.Deserialize<SaveCodec.Document>(SaveCodec.Write(state), WireJson.CreateOptions())!;
    File.WriteAllText(Path.Combine(root, "prototype/protocol/saves/terrain-v7.json"), JsonSerializer.Serialize(saved, json) + "\n");
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
