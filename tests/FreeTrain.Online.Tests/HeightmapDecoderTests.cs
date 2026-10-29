using System.Buffers.Binary;
using System.IO.Compression;
using FreeTrain.Online.Server;
using FreeTrain.Online.Simulation;
using StbImageSharp;
using Xunit;
using Xunit.Abstractions;

[Collection("World creation")]
public class HeightmapDecoderTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("rgba8.png")][InlineData("rgba16.png")][InlineData("rgba8-adam7.png")][InlineData("rgba16-adam7.png")]
    public void Independent_16bit_and_Adam7_fixtures_preserve_literal_RGBA(string file)
    {
        var image = HeightmapDecoder.Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Heightmaps", file)));
        var expected = new List<byte>();
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) expected.AddRange([(byte)(x * 17), (byte)(y * 17), (byte)((x + y) * 8), 255]);
        Assert.Equal(expected.ToArray(), image.Raster.Pixels.ToArray());
        Assert.Equal(0, DecodeBudget.ActiveBytes);
    }

    [Theory]
    [InlineData("png")][InlineData("gif")][InlineData("jpeg")]
    public void Oversized_dimensions_fail_before_expanded_native_allocation(string format)
    {
        byte[] bytes;
        if (format == "png")
        {
            bytes = HeightmapTests.Png(1, 1, (_, _) => (0, 0, 0, 255));
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), 4097);
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29), PngPreview.Crc(bytes.AsSpan(12, 4), bytes.AsSpan(16, 13)));
        }
        else if (format == "gif")
        {
            bytes = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAkQBADs=");
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 4097);
        }
        else
        {
            bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Heightmaps/vertical.jpg"));
            int marker = bytes.AsSpan().IndexOf(new byte[] {255, 192});
            Assert.True(marker >= 0);
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(marker + 7), 4097);
        }
        Assert.Equal("image-pixels", Assert.Throws<HeightmapException>(() => HeightmapDecoder.Decode(bytes)).Code);
        Assert.Equal(0, DecodeBudget.ActiveBytes);
    }

    [Fact]
    public void Png_crc_corruption_and_actual_GIF_transparency_have_distinct_errors()
    {
        var png = HeightmapTests.Png(1, 1, (_, _) => (0, 0, 0, 255)); png[29] ^= 1;
        Assert.Equal("image-invalid", Assert.Throws<HeightmapException>(() => HeightmapDecoder.Decode(png)).Code);
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAkQBADs=");
        var transparent = gif[..19].Concat(Convert.FromHexString("21F9040100000000")).Concat(gif[19..]).ToArray();
        Assert.Equal("image-alpha", Assert.Throws<HeightmapException>(() => HeightmapDecoder.Decode(transparent)).Code);
        Assert.Equal(0, DecodeBudget.ActiveBytes);
    }

    [Fact]
    public void Packed_grayscale_and_palette_pixels_have_independent_color_oracles()
    {
        var palette = HeightmapDecoder.Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Heightmaps/palette.png")));
        Assert.Equal(new byte[] {255,0,0,255,0,255,0,255,0,0,255,255,255,255,255,255}, palette.Raster.Pixels.ToArray());
        var gray = HeightmapDecoder.Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Heightmaps/gray1.png")));
        Assert.Equal(Enumerable.Range(0, 8).SelectMany(i => new byte[] {(byte)(i%2*255),(byte)(i%2*255),(byte)(i%2*255),255}).ToArray(), gray.Raster.Pixels.ToArray());
    }

    [Fact]
    public void Small_header_cannot_hide_unbounded_deflate_and_failed_decodes_release_all_native_buffers()
    {
        // A valid compressed 2048² image, falsely advertised as 1². Expansion must
        // stop at the arena limit rather than trusting the tiny header reservation.
        var bomb = HeightmapTests.Png(2048, 2048, (_, _) => (0, 0, 0, 255));
        BinaryPrimitives.WriteInt32BigEndian(bomb.AsSpan(16), 1);
        BinaryPrimitives.WriteInt32BigEndian(bomb.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bomb.AsSpan(29), PngPreview.Crc(bomb.AsSpan(12, 4), bomb.AsSpan(16, 13)));
        for (int i = 0; i < 4; i++)
        {
            Assert.Throws<HeightmapException>(() => HeightmapDecoder.Decode(bomb));
            Assert.Equal(0, DecodeBudget.ActiveBytes);
        }
        Assert.Single(HeightmapDecoder.Decode(HeightmapTests.Bmp(1, 1, (_, _) => (1, 2, 3))).Raster.Pixels.ToArray().Chunk(4));
    }

    [Fact]
    public void Arena_bounds_moving_realloc_and_cancellation_and_releases_abandoned_intermediates()
    {
        using (var scope = new DecodeBudget(default))
        {
            nint first = DecodeBudget.Allocate(48 * 1024 * 1024);
            Assert.Throws<InvalidDataException>(() => DecodeBudget.Reallocate(first, 48 * 1024 * 1024));
            Assert.Equal(48 * 1024 * 1024, DecodeBudget.ActiveBytes);
        }
        Assert.Equal(0, DecodeBudget.ActiveBytes);
        using (var scope = new DecodeBudget(default))
        {
            for (int i = 0; i < 256; i++) DecodeBudget.Allocate(1);
            Assert.Throws<InvalidDataException>(() => DecodeBudget.Allocate(1));
        }
        Assert.Equal(0, DecodeBudget.ActiveBytes);
        using var cancellation = new CancellationTokenSource();
        using (var scope = new DecodeBudget(cancellation.Token))
        {
            DecodeBudget.Allocate(8192); cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => { for (int i = 0; i < 1024; i++) DecodeBudget.Check(); });
        }
        Assert.Equal(0, DecodeBudget.ActiveBytes);
    }

    [Theory]
    [InlineData("png")][InlineData("bmp")][InlineData("gif")][InlineData("jpeg")]
    public void Truncated_headers_and_pixels_are_rejected_without_retained_allocations(string format)
    {
        byte[] bytes = format switch
        {
            "png" => HeightmapTests.Png(8, 8, (_, _) => (23, 100, 219, 255)),
            "bmp" => HeightmapTests.Bmp(8, 8, (_, _) => (23, 100, 219)),
            "gif" => Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAkQBADs="),
            _ => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Heightmaps/vertical.jpg"))
        };
        foreach (int length in new[] {3, bytes.Length / 2, bytes.Length - 3})
        {
            Assert.Throws<HeightmapException>(() => HeightmapDecoder.Decode(bytes[..length]));
            Assert.Equal(0, DecodeBudget.ActiveBytes);
        }
    }

    [Fact]
    public async Task Cancellation_during_real_expansion_releases_native_memory_and_four_decoders_are_independent()
    {
        var bytes = HeightmapTests.Png(2048, 2048, (x, y) => ((byte)x, (byte)y, 77, 255));
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.CancelAfter(1);
            Assert.Throws<OperationCanceledException>(() => HeightmapDecoder.Decode(bytes, cancellation: cancellation.Token));
        }
        Assert.Equal(0, DecodeBudget.ActiveBytes);
        var images = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => HeightmapDecoder.Decode(bytes))));
        Assert.All(images, image => Assert.Equal(images[0].Raster.Pixels.ToArray(), image.Raster.Pixels.ToArray()));
        Assert.All(images, image => Assert.InRange(image.DecoderPeakBytes, 1, DecodeBudget.MaxNativeBytes));
        Assert.Equal(0, DecodeBudget.ActiveBytes);
        output.WriteLine($"Four concurrent maximum-pixel PNG decoders: per-job native peaks {string.Join(',', images.Select(i => i.DecoderPeakBytes))}; active after completion {DecodeBudget.ActiveBytes}.");
    }

    [Theory]
    [InlineData(1, 1)][InlineData(3, 2)][InlineData(321, 257)]
    public void Preview_has_valid_chunks_crcs_bounded_RGB_rows_and_exact_stored_samples(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        { int i = (y * width + x) * 4; pixels[i] = (byte)x; pixels[i + 1] = (byte)y; pixels[i + 2] = 219; pixels[i + 3] = 255; }
        var raster = new HeightmapRaster(width, height, pixels);
        string uri = HeightmapDecoder.Preview(raster);
        Assert.Equal(uri, HeightmapDecoder.Preview(raster));
        byte[] png = Convert.FromBase64String(uri.Split(',')[1]);
        Assert.Equal(new byte[] {137,80,78,71,13,10,26,10}, png[..8]);
        var tags = new List<string>(); byte[] compressed = []; int w = 0, h = 0;
        for (int i = 8; i < png.Length;)
        {
            int count = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(i));
            string tag = System.Text.Encoding.ASCII.GetString(png, i + 4, 4); tags.Add(tag);
            // Independent CRC using polynomial long division across tag + payload.
            uint crc = uint.MaxValue;
            for (int j = i + 4; j < i + 8 + count; j++)
            { crc ^= png[j]; for (int b = 0; b < 8; b++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1)); }
            Assert.Equal(~crc, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(i + 8 + count)));
            if (tag == "IHDR")
            {
                w = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(i + 8)); h = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(i + 12));
                Assert.Equal(new byte[] {8,2,0,0,0}, png[(i + 16)..(i + 21)]);
            }
            if (tag == "IDAT") compressed = png[(i + 8)..(i + 8 + count)];
            i += 12 + count;
        }
        Assert.Equal(new[] {"IHDR","IDAT","IEND"}, tags);
        Assert.Equal(Math.Min(192, width), w); Assert.Equal(Math.Min(192, height), h);
        using var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var raw = new MemoryStream(); zlib.CopyTo(raw); var rows = raw.ToArray();
        Assert.Equal((1 + 3 * w) * h, rows.Length);
        for (int y = 0; y < h; y++)
        {
            Assert.Equal(0, rows[y * (1 + 3 * w)]);
            for (int x = 0; x < w; x++)
            {
                int source = ((y * height / h) * width + x * width / w) * 4;
                int target = y * (1 + 3 * w) + 1 + x * 3;
                Assert.Equal(pixels[source..(source + 3)], rows[target..(target + 3)]);
            }
        }
    }
}
