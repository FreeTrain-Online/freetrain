// Project-owned runtime for the pinned stb port. No Hebron.Runtime source is used.
namespace StbImageSharp.Hebron.Runtime;

internal static unsafe class CRuntime
{
    public static void* malloc(ulong size) => malloc(checked((long)size));
    public static void* malloc(long size) => (void*)DecodeBudget.Allocate(size);
    public static void free(void* pointer) => DecodeBudget.Free((nint)pointer);
    public static void* realloc(void* pointer, ulong size) => realloc(pointer, checked((long)size));
    public static void* realloc(void* pointer, long size) => (void*)DecodeBudget.Reallocate((nint)pointer, size);
    public static void memcpy(void* destination, void* source, long size) => Buffer.MemoryCopy(source, destination, size, size);
    public static void memcpy(void* destination, void* source, ulong size) => memcpy(destination, source, checked((long)size));
    public static void memset(void* destination, int value, long size) => new Span<byte>(destination, checked((int)size)).Fill((byte)value);
    public static void memset(void* destination, int value, ulong size) => memset(destination, value, checked((long)size));
    public static uint _lrotl(uint value, int bits) => System.Numerics.BitOperations.RotateLeft(value, bits);
    public static int abs(int value) => Math.Abs(value);
    public static double pow(double value, double exponent) => Math.Pow(value, exponent);
    public static double ldexp(double value, int exponent) => Math.ScaleB(value, exponent);
    public static int strcmp(sbyte* value, string text) => strncmp(value, text, (ulong)text.Length);
    public static int strncmp(sbyte* value, string text, ulong count)
    {
        for (int i = 0; i < (int)Math.Min((ulong)text.Length, count); i++)
            if (value[i] != text[i]) return value[i] - text[i];
        return 0;
    }
    public static long strtol(sbyte* value, sbyte** end, int radix)
    {
        long result = 0;
        while (*value is >= (sbyte)'0' and <= (sbyte)'9') { DecodeBudget.Check(); result = checked(result * radix + *value++ - '0'); }
        if (end != null) *end = value;
        return result;
    }
}
internal static class Utility
{
    public static T[][] CreateArray<T>(int rows, int columns)
    {
        // All callers use fixed palette/JPEG table sizes, independent of input.
        if (rows is < 0 or > 256 || columns is < 0 or > 512) throw new InvalidDataException();
        var result = new T[rows][];
        for (int i = 0; i < rows; i++) result[i] = new T[columns];
        return result;
    }
}
