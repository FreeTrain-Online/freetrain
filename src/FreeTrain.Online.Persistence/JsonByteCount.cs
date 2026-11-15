using System.Text.Json;

namespace FreeTrain.Online.Persistence;

// Admission needs the encoded length, not an output-sized byte array. The
// serializer streams to a bounded counter and discards output.
internal static class JsonByteCount
{
    internal static long Count<T>(T value, JsonSerializerOptions options, long maximum)
    {
        using var stream = new Counter(maximum);
        JsonSerializer.Serialize(stream, value, options);
        return stream.Length;
    }

    private sealed class Counter(long maximum) : Stream
    {
        private long length;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;
        public override long Position { get => length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > maximum - length) throw new InvalidDataException("saveTooLarge");
            length += buffer.Length;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
