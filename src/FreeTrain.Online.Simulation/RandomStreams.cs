using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FreeTrain.Online.Simulation;

public sealed record RandomState(int Index, ImmutableArray<int> Values);
public sealed record RandomStreamState(string Id, RandomState State);
public sealed record RandomStreamsState(int Version, string Algorithm, int Seed, ImmutableArray<RandomStreamState> Streams);

// Frozen modified subtractive generator, matching the seeded System.Random
// operations used by stable ExAV. Never delegate durable draws to the host runtime.
// Algorithm reference: dotnet/runtime v10.0.0, Random.CompatImpl.cs (MIT).
// Copyright (c) .NET Foundation and Contributors. MIT license: NOTICE.txt.
// See doc/parity/random-review.json for source bindings and version decisions.
public sealed class GameRandom
{
    public const string Algorithm = "subtractive-55-v1";
    private const int Modulus = int.MaxValue;
    private readonly int[] values;
    private int index;

    public GameRandom(int seed)
    {
        var table = new int[56]; // Initialization uses the source algorithm's 1-based table.
        unchecked
        {
            var previous = 161803398 - (seed == int.MinValue ? int.MaxValue : Math.Abs(seed));
            table[55] = previous;
            var current = 1;
            for (var i = 1; i < 55; i++)
            {
                var slot = 21 * i % 55;
                table[slot] = current;
                current = previous - current;
                if (current < 0) current += Modulus;
                previous = table[slot];
            }
            for (var pass = 0; pass < 4; pass++)
                for (var i = 1; i <= 55; i++)
                {
                    table[i] -= table[1 + (i + 30) % 55];
                    if (table[i] < 0) table[i] += Modulus;
                }
        }
        values = table[1..];
    }

    private GameRandom(RandomState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Index is < 0 or >= 55 || state.Values.IsDefault || state.Values.Length != 55
            || state.Values.Any(v => v < 0 || v >= Modulus) || state.Values.All(v => v == 0))
            throw new ArgumentException("Invalid random state.", nameof(state));
        index = state.Index;
        values = state.Values.ToArray();
    }

    public int Next()
    {
        var result = values[index] - values[(index + 21) % 55];
        if (result == Modulus) result--;
        if (result < 0) result += Modulus;
        values[index] = result;
        index = (index + 1) % 55;
        return result;
    }
    public double NextDouble() => Next() * (1.0 / Modulus);
    public int Next(int maxValue)
    {
        if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
        // Even Next(0) consumes one sample, as does an equal-bound range.
        return (int)(NextDouble() * maxValue);
    }
    public int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
        var range = (long)maxValue - minValue;
        if (range <= Modulus) return (int)(NextDouble() * range) + minValue;
        var sample = Next();
        if (Next() % 2 == 0) sample = -sample;
        var scaled = (sample + (double)Modulus - 1) / (2.0 * Modulus - 1);
        return (int)((long)(scaled * range) + minValue);
    }
    public RandomState Capture() => new(index, values.ToImmutableArray());
    public static GameRandom Restore(RandomState state) => new(state);
}

// One bank per authoritative world. IDs follow source generator ownership, so
// consumers sharing a source generator must share the corresponding stream.
public sealed class RandomStreams
{
    public const int Version = 1;
    public const int ReferenceSeed = 0;
    public static ImmutableArray<string> Ids { get; } =
        ["construction", "development", "forest", "land-selection", "stadium",
         "stock-economy", "stock-effects", "stock-tables", "subsidiaries", "time-length"];
    private readonly Dictionary<string, GameRandom> streams = new(StringComparer.Ordinal);
    public int Seed { get; }
    public RandomStreams(int seed = ReferenceSeed)
    {
        Seed = seed;
        foreach (var id in Ids) streams.Add(id, new(DeriveSeed(seed, id)));
    }
    private RandomStreams(RandomStreamsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Version != Version || state.Algorithm != GameRandom.Algorithm || state.Streams.IsDefault
            || state.Streams.Length != Ids.Length) throw new ArgumentException("Unsupported random streams.");
        Seed = state.Seed;
        foreach (var stream in state.Streams)
        {
            if (stream == null || !Ids.Contains(stream.Id) || streams.ContainsKey(stream.Id))
                throw new ArgumentException("Unknown or duplicate random stream.");
            streams.Add(stream.Id, GameRandom.Restore(stream.State));
        }
    }
    public GameRandom this[string id] => streams.TryGetValue(id, out var stream) ? stream
        : throw new ArgumentException("Unknown random stream.", nameof(id));
    public static int DeriveSeed(int seed, string id)
    {
        if (!Ids.Contains(id)) throw new ArgumentException("Unknown random stream.", nameof(id));
        var bytes = Encoding.UTF8.GetBytes("freetrain.random-streams.v1\n" + seed.ToString(CultureInfo.InvariantCulture) + "\n" + id);
        return BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(bytes)) & int.MaxValue;
    }
    // CreekBuilder seeds a fresh operation-local generator from endpoint coordinates.
    // Explicit unchecked Int32 arithmetic preserves its stable seed expression.
    public static GameRandom ForCreek(int x1, int y1, int x2, int y2)
        => new(unchecked((x1 * 128 + y1) ^ (x2 * 128 + y2)));
    public RandomStreamsState Capture() => new(Version, GameRandom.Algorithm, Seed,
        Ids.Select(id => new RandomStreamState(id, streams[id].Capture())).ToImmutableArray());
    public static RandomStreams Restore(RandomStreamsState state) => new(state);
}
