using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FreeTrain.Online.Simulation;

// Admission-envelope probe, not a terrain generator or whole-world capacity test.
// Allocate/touch every reserved byte, walk the full bounded normalization work and
// serialize six-tree decorated cells. The wire representation is deliberately verbose.
Console.WriteLine(JsonSerializer.Serialize(new { runtime = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    processors = Environment.ProcessorCount }));
foreach (var side in new[] { 30, 64, 128, 256 })
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var before = GC.GetTotalAllocatedBytes(true);
    var watch = Stopwatch.StartNew();
    var cells = side * side;
    var scratch = new byte[cells * 128];
    var transfer1 = new byte[65_536 + cells * 512];
    var transfer2 = new byte[transfer1.Length];
    var decoded = new byte[4_194_304 * 4];
    var rgba = new byte[decoded.Length];
    var metadata = new byte[1_048_576];
    var encoded = new byte[8_388_608];
    foreach (var buffer in new[] { scratch, transfer1, transfer2, decoded, rgba, metadata, encoded })
        for (var offset = 0; offset < buffer.Length; offset += 4096) buffer[offset] = 1;
    var touchedBytes = scratch.LongLength + transfer1.Length + transfer2.Length + decoded.Length + rgba.Length + metadata.Length + encoded.Length;
    using var stream = new MemoryStream(transfer1, 0, transfer1.Length, true, true);
    stream.SetLength(0);
    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartArray();
        for (var i = 0; i < cells; i++)
        {
            writer.WriteStartObject(); writer.WriteNumber("h", i % side); writer.WriteNumber("v", i / side);
            writer.WriteNumber("ground", 63); writer.WriteNumber("corners", 17476); writer.WriteString("kind", "natural");
            writer.WriteString("forest", "{FBD0CD06-016A-4037-AB15-02040218CBAA}-m");
            writer.WriteBoolean("owned", false); writer.WriteNumber("value", 100);
            writer.WriteStartArray("trees");
            for (var tree = 0; tree < 6; tree++)
            { writer.WriteStartObject(); writer.WriteNumber("x", 30); writer.WriteNumber("y", 15); writer.WriteNumber("sprite", 8); writer.WriteEndObject(); }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        writer.WriteEndArray(); writer.Flush();
    }
    var serializedBytes = stream.Length;
    long checksum = 0;
    for (var pass = 0; pass < WorldCreationProfile.MaxAttempts; pass++)
        for (var scan = 0; scan < 64 * 32 + 256; scan++)
            for (var cell = 0; cell < cells; cell++) checksum += scratch[cell * 128];
    watch.Stop();
    var request = WorldCreationProfile.Generated() with { Dimensions = new(side, side, 32) };
    var issues = WorldCreationProfile.Validate(request);
    var allocatedBytes = GC.GetTotalAllocatedBytes(true) - before;
    var maxImageBudget = issues.IsEmpty ? WorldCreationProfile.Estimate(WorldCreationProfile.Heightmap(
        new(new string('a', 64), "image/png", 8_388_608, 2048, 2048, 1)) with { Dimensions = request.Dimensions }) : null;
    if (maxImageBudget is not null && (serializedBytes > maxImageBudget.TransferBytes ||
        touchedBytes > maxImageBudget.WorkingBytes || allocatedBytes > maxImageBudget.WorkingBytes))
        throw new InvalidOperationException("Measured envelope exceeds the admitted reservation.");
    Console.WriteLine(JsonSerializer.Serialize(new { side, cells, levels = 32, maxImagePixels = 4_194_304,
        touchedBytes, allocatedBytes, serializedBytes,
        workUnits = (long)cells * (64 * 32 + 256) * WorldCreationProfile.MaxAttempts,
        elapsedMs = watch.Elapsed.TotalMilliseconds, checksum,
        admitted = issues.IsEmpty, issues = issues.Select(i => i.Code),
        noImageBudget = issues.IsEmpty ? WorldCreationProfile.Estimate(request) : null,
        maxImageBudget }));
    GC.KeepAlive(scratch); GC.KeepAlive(transfer2); GC.KeepAlive(decoded); GC.KeepAlive(rgba); GC.KeepAlive(metadata); GC.KeepAlive(encoded);
}
