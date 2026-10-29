using System.Buffers.Binary;
using System.Security.Cryptography;
using FreeTrain.Online.Simulation;
using StbImageSharp;
using System.Runtime.InteropServices;

namespace FreeTrain.Online.Server;

public sealed class HeightmapException(string code) : ArgumentException(code) { public string Code { get; } = code; }
public sealed record DecodedHeightmap(HeightmapInput Input, HeightmapRaster Raster)
{
    internal long DecoderPeakBytes { get; init; }
}

// Pinned source decoder, bounded arena and own preview encoder. No display color
// management or EXIF rotation: height sampling uses stored, untransformed pixels.
public static class HeightmapDecoder
{
    public static DecodedHeightmap Decode(byte[] bytes, string? mediaType = null, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        if (bytes.LongLength is < 1 or > WorldCreationProfile.MaxImageBytes) throw new HeightmapException("image-bytes");
        string actual = bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png" :
            bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) ? "image/jpeg" :
            bytes.AsSpan().StartsWith("BM"u8) ? "image/bmp" :
            bytes.AsSpan().StartsWith("GIF87a"u8) || bytes.AsSpan().StartsWith("GIF89a"u8) ? "image/gif" : throw new HeightmapException("image-format");
        if (mediaType != null && mediaType != actual) throw new HeightmapException("image-format");
        // Reject APNG even if a decoder would quietly decode just its first frame.
        if (actual == "image/png")
        {
            int i = 8; bool ended = false;
            while (i < bytes.Length)
            {
                cancellation.ThrowIfCancellationRequested();
                if (bytes.Length - i < 12) throw new HeightmapException("image-invalid");
                uint size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i));
                if (size > bytes.Length - i - 12) throw new HeightmapException("image-invalid");
                if (bytes.AsSpan(i + 4, 4).SequenceEqual("acTL"u8)) throw new HeightmapException("image-animation");
                var type = bytes.AsSpan(i + 4, 4);
                if (i == 8 && (!type.SequenceEqual("IHDR"u8) || size != 13)) throw new HeightmapException("image-invalid");
                uint crc = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i + 8 + (int)size));
                if (PngPreview.Crc(type, bytes.AsSpan(i + 8, (int)size), cancellation) != crc) throw new HeightmapException("image-invalid");
                i += (int)size + 12;
                if (type.SequenceEqual("IEND"u8))
                {
                    if (size != 0 || i != bytes.Length) throw new HeightmapException("image-invalid");
                    ended = true;
                }
            }
            if (!ended) throw new HeightmapException("image-invalid");
        }
        if (actual == "image/gif") CheckGif(bytes, cancellation);
        try { return DecodeRaster(bytes, actual, cancellation); }
        catch (HeightmapException) { throw; }
        catch (Exception error) when (error is InvalidOperationException or InvalidDataException or EndOfStreamException or OverflowException or ArgumentException or IndexOutOfRangeException)
        { throw new HeightmapException("image-invalid"); }
    }
    private static unsafe DecodedHeightmap DecodeRaster(byte[] bytes, string actual, CancellationToken cancellation)
    {
        byte[] pixels; int w = 0, h = 0, components = 0; long peak;
        using (var budget = new DecodeBudget(cancellation))
        using (var stream = new MemoryStream(bytes, false))
        {
            var context = new StbImage.stbi__context(stream);
            if (StbImage.stbi__info_main(context, &w, &h, &components) == 0) throw new HeightmapException("image-invalid");
            if (w is < 1 or > WorldCreationProfile.MaxImageSide || h is < 1 or > WorldCreationProfile.MaxImageSide ||
                (long)w * h > WorldCreationProfile.MaxImagePixels) throw new HeightmapException("image-pixels");
            stream.Position = 0;
            int decodedW = 0, decodedH = 0;
            byte* result = StbImage.stbi__load_and_postprocess_8bit(context, &decodedW, &decodedH, &components, 4);
            if (result == null || decodedW != w || decodedH != h) throw new HeightmapException("image-invalid");
            cancellation.ThrowIfCancellationRequested();
            budget.ReleaseExcept((nint)result, checked(w * h * 4));
            pixels = new byte[checked(w * h * 4)];
            Marshal.Copy((nint)result, pixels, 0, pixels.Length);
            peak = budget.PeakBytes;
        } // Frees ALL native intermediates on success, malformed data, limit and cancellation.
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (i % 16384 == 0) cancellation.ThrowIfCancellationRequested();
            if (pixels[i + 3] != 255) throw new HeightmapException("image-alpha");
        }
        return new(new(Convert.ToHexStringLower(SHA256.HashData(bytes)), actual, bytes.Length, w, h, 1), new(w, h, pixels)) { DecoderPeakBytes = peak };
    }
    public static string Preview(HeightmapRaster raster) => "data:image/png;base64," + Convert.ToBase64String(PngPreview.Encode(raster));
    private static void CheckGif(byte[] b, CancellationToken token)
    {
        // Count descriptors without decoding frames or allocating a FrameInfo array.
        if (b.Length < 13) throw new HeightmapException("image-invalid");
        int i = 13 + ((b[10] & 128) != 0 ? 3 * (1 << ((b[10] & 7) + 1)) : 0), frames = 0;
        void SkipBlocks()
        {
            while (true) { token.ThrowIfCancellationRequested(); if (i >= b.Length) throw new HeightmapException("image-invalid"); int n = b[i++]; if (n == 0) return; i += n; if (i > b.Length) throw new HeightmapException("image-invalid"); }
        }
        while (i < b.Length)
        {
            token.ThrowIfCancellationRequested(); byte type = b[i++];
            if (type == 0x3b) { if (frames != 1 || i != b.Length) throw new HeightmapException("image-invalid"); return; }
            if (type == 0x21) { i++; SkipBlocks(); }
            else if (type == 0x2c)
            {
                if (++frames > 1) throw new HeightmapException("image-animation");
                if (i + 9 > b.Length) throw new HeightmapException("image-invalid");
                int left = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(i)), top = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(i + 2));
                int width = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(i + 4)), height = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(i + 6));
                if (width == 0 || height == 0 || left + width > BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(6)) || top + height > BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(8)))
                    throw new HeightmapException("image-pixels");
                int packed = b[i + 8]; i += 9 + ((packed & 128) != 0 ? 3 * (1 << ((packed & 7) + 1)) : 0);
                i++; SkipBlocks();
            }
            else throw new HeightmapException("image-invalid");
        }
        throw new HeightmapException("image-invalid");
    }
}
