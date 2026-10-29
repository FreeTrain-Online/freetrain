using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StbImageSharp;

// A synchronous, thread-confined arena owns every decoder pointer, including
// pointers abandoned when upstream code throws. No allocation can escape Decode.
internal sealed class DecodeBudget : IDisposable
{
    internal const long MaxNativeBytes = 80L * 1024 * 1024;
    internal const long MaxProbes = 256_000_000;
    [ThreadStatic] private static DecodeBudget? current;
    private readonly Dictionary<nint, long> allocations = [];
    private readonly CancellationToken cancellation;
    private readonly long deadline = Stopwatch.GetTimestamp() + 20 * Stopwatch.Frequency;
    private long bytes, probes;
    internal long PeakBytes { get; private set; }
    internal static long ActiveBytes => Interlocked.Read(ref activeBytes);
    private static long activeBytes;

    internal DecodeBudget(CancellationToken token)
    {
        if (current != null) throw new InvalidOperationException("Nested image decoding.");
        cancellation = token; current = this;
    }
    internal static void Check()
    {
        var scope = current ?? throw new InvalidOperationException("Missing decoder scope.");
        if (++scope.probes > MaxProbes) throw new InvalidDataException("Decoder work limit.");
        if ((scope.probes & 1023) == 1)
        {
            scope.cancellation.ThrowIfCancellationRequested();
            if (Stopwatch.GetTimestamp() > scope.deadline) throw new InvalidDataException("Decoder deadline.");
        }
    }
    internal static nint Allocate(long size)
    {
        Check(); var scope = current!;
        if (scope.allocations.Count >= 256 || size <= 0 || size > MaxNativeBytes - scope.bytes) throw new InvalidDataException("Decoder allocation limit.");
        // Clear allocations so short compressed data cannot expose uninitialized bytes.
        nint pointer = Marshal.AllocHGlobal(checked((int)size));
        unsafe { new Span<byte>((void*)pointer, (int)size).Clear(); }
        scope.allocations.Add(pointer, size); scope.bytes += size;
        Interlocked.Add(ref activeBytes, size); scope.PeakBytes = Math.Max(scope.PeakBytes, scope.bytes);
        return pointer;
    }
    internal static void Free(nint pointer)
    {
        if (pointer == 0) return;
        var scope = current!;
        if (!scope.allocations.Remove(pointer, out long size)) throw new InvalidDataException("Unknown decoder allocation.");
        Marshal.FreeHGlobal(pointer); scope.bytes -= size; Interlocked.Add(ref activeBytes, -size);
    }
    internal static unsafe nint Reallocate(nint pointer, long size)
    {
        if (pointer == 0) return Allocate(size);
        var scope = current!;
        if (!scope.allocations.TryGetValue(pointer, out long oldSize)) throw new InvalidDataException("Unknown decoder allocation.");
        // Include BOTH blocks in the limit: a moving realloc must not double the envelope.
        nint replacement = Allocate(size);
        Buffer.MemoryCopy((void*)pointer, (void*)replacement, size, Math.Min(size, oldSize));
        Free(pointer); return replacement;
    }
    internal void ReleaseExcept(nint result, long minimumSize)
    {
        if (!allocations.TryGetValue(result, out long size) || size < minimumSize) throw new InvalidDataException("Invalid decoder output.");
        foreach (var pointer in allocations.Keys.Where(pointer => pointer != result).ToArray()) Free(pointer);
    }
    public void Dispose()
    {
        foreach (var (pointer, size) in allocations) { Marshal.FreeHGlobal(pointer); Interlocked.Add(ref activeBytes, -size); }
        allocations.Clear(); bytes = 0; current = null;
    }
}
