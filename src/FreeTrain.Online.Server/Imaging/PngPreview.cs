using System.Buffers.Binary;
using System.IO.Compression;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Server;

// Only encodes known RGBA8 rasters to bounded RGB8, noninterlaced PNG previews.
// This is not a general image codec. Stored orientation and nearest samples match
// the previous preview. Chunk layout/filter choice are deterministic.
internal static class PngPreview
{
    internal static byte[] Encode(HeightmapRaster raster)
    {
        int width = Math.Min(192, raster.Width), height = Math.Min(192, raster.Height);
        using var result = new MemoryStream();
        result.Write(new byte[] {137,80,78,71,13,10,26,10});
        Span<byte> header = stackalloc byte[13]; header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; header[9] = 2;
        Chunk(result, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
        {
            var row = new byte[1 + width * 3];
            var pixels = raster.Pixels.Span;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int source = ((y * raster.Height / height) * raster.Width + x * raster.Width / width) * 4;
                    pixels.Slice(source, 3).CopyTo(row.AsSpan(1 + x * 3));
                }
                zlib.Write(row); // filter 0 on each row
            }
        }
        Chunk(result, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        Chunk(result, "IEND"u8, []);
        return result.ToArray();
    }
    private static void Chunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length); output.Write(word);
        output.Write(type); output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc(type, data)); output.Write(word);
    }
    internal static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data, CancellationToken cancellation = default)
    {
        uint crc = 0xffffffff;
        foreach (byte value in type) crc = Update(crc, value);
        for (int i = 0; i < data.Length; i++)
        {
            if ((i & 16383) == 0) cancellation.ThrowIfCancellationRequested();
            crc = Update(crc, data[i]);
        }
        return ~crc;
    }
    private static uint Update(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
        return crc;
    }
}
