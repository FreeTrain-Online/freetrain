using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Server;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;
using Xunit;
using Xunit.Abstractions;

[Collection("World creation")]
public class HeightmapTests(ITestOutputHelper output)
{
    // Independent, uncompressed 24-bit BMP writer. No image-library encoder in the decoder oracle.
    public static byte[] Bmp(int w, int h, Func<int, int, (byte R, byte G, byte B)> color)
    {
        int stride = (3 * w + 3) & ~3; var b = new byte[54 + stride * h]; b[0] = 66; b[1] = 77;
        void Put(int offset, int n) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(offset), n);
        Put(2, b.Length); Put(10, 54); Put(14, 40); Put(18, w); Put(22, h); b[26] = 1; b[28] = 24;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        { var c = color(x, y); int i = 54 + (h - 1 - y) * stride + x * 3; b[i] = c.B; b[i + 1] = c.G; b[i + 2] = c.R; }
        return b;
    }
    // PNG chunks, CRC and zlib are written independently of the production decoder.
    public static byte[] Png(int width, int height, Func<int,int,(byte R,byte G,byte B,byte A)> color, int padding = 0, int bitDepth = 8)
    {
        using var output=new MemoryStream();output.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string type,byte[] data)
        {
            var tag=System.Text.Encoding.ASCII.GetBytes(type);var size=new byte[4];BinaryPrimitives.WriteInt32BigEndian(size,data.Length);output.Write(size);output.Write(tag);output.Write(data);
            uint crc=0xffffffff;
            foreach(byte value in tag.Concat(data)){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}
            BinaryPrimitives.WriteUInt32BigEndian(size,~crc);output.Write(size);
        }
        var header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,width);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);header[8]=(byte)bitDepth;header[9]=6;Chunk("IHDR",header);if(padding>0)Chunk("frEe",new byte[padding]);
        using var compressed=new MemoryStream();
        using(var zlib=new ZLibStream(compressed,CompressionLevel.Fastest,true))
        {
            var row=new byte[1+width*4*(bitDepth/8)];
            for(int y=0;y<height;y++){for(int x=0;x<width;x++){var c=color(x,y);int i=1+4*x*(bitDepth/8); if(bitDepth==8){row[i]=c.R;row[i+1]=c.G;row[i+2]=c.B;row[i+3]=c.A;} else {row[i]=row[i+1]=c.R;row[i+2]=row[i+3]=c.G;row[i+4]=row[i+5]=c.B;row[i+6]=row[i+7]=c.A;}}zlib.Write(row);}
        }
        Chunk("IDAT",compressed.ToArray());Chunk("IEND",[]);return output.ToArray();
    }
    [Fact]
    public void Png_and_gif_decode_stored_pixels_and_opaque_alpha_without_display_transforms()
    {
        var png=HeightmapDecoder.Decode(Png(2,1,(x,_)=>x==0?((byte)255,(byte)0,(byte)0,(byte)255):((byte)0,(byte)0,(byte)255,(byte)255)));
        Assert.Equal(new byte[]{255,0,0,255,0,0,255,255},png.Raster.Pixels.ToArray());
        var gif=HeightmapDecoder.Decode(Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAkQBADs="));
        Assert.Equal(1,gif.Input.Frames);Assert.Equal(new byte[]{0,0,0,255},gif.Raster.Pixels.ToArray());
    }
    [Fact]
    public void Jpeg_keeps_stored_orientation_even_with_exif_rotation()
    {
        var jpeg=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Heightmaps/vertical.jpg"));
        // APP1: Exif little-endian TIFF with orientation=6 (90° clockwise).
        var exif=Convert.FromHexString("FFE1002245786966000049492A0008000000010012010300010000000600000000000000");
        var rotated=jpeg[..2].Concat(exif).Concat(jpeg[2..]).ToArray();
        foreach(var bytes in new[]{jpeg,rotated})
        {
            var image=HeightmapDecoder.Decode(bytes,"image/jpeg");Assert.Equal(1,image.Input.Width);Assert.Equal(2,image.Input.Height);
            Assert.InRange(image.Raster.Pixels.Span[0],0,3);Assert.InRange(image.Raster.Pixels.Span[4],252,255);
        }
    }
    [Theory]
    [InlineData(8)][InlineData(16)]
    public void Maximum_decoded_image_plus_dense_heightmap_stays_inside_the_job_allocation_envelope(int bitDepth)
    {
        var bytes=Png(2048,2048,(x,y)=>{byte c=(byte)(((x/64+y/64)%2)*255);return(c,c,c,(byte)255);},bitDepth:bitDepth);
        int pad=8*1024*1024-bytes.Length-12;
        bytes=Png(2048,2048,(x,y)=>{byte c=(byte)(((x/64+y/64)%2)*255);return(c,c,c,(byte)255);},pad,bitDepth);
        Assert.Equal(8*1024*1024,bytes.Length);
        long before=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
        var decoded=HeightmapDecoder.Decode(bytes);var request=Request(decoded.Input,32,32,64);
        var world=HeightmapTerrain.Create(request,decoded.Raster,TerrainProfiles.Bundled);
        var saved=SaveCodec.Write(new TerrainSessionState(world,TerrainProvenance.From(request)));
        var restored=SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve).Terrain!;
        var snapshot=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<FreeTrain.Online.Contracts.WireMessage>(TerrainWire.Snapshot(restored,1),FreeTrain.Online.Contracts.WireJson.CreateOptions());
        Assert.Equal(0, StbImageSharp.DecodeBudget.ActiveBytes);
        Assert.InRange(decoded.DecoderPeakBytes, 1, StbImageSharp.DecodeBudget.MaxNativeBytes);
        Assert.Equal(saved,SaveCodec.Write(restored));Assert.StartsWith("data:image/png;base64,",HeightmapDecoder.Preview(decoded.Raster));
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.True(allocated<WorldCreationProfile.MaxWorkingBytes);
        output.WriteLine($"max image: bitDepth={bitDepth}; encoded={bytes.Length}; pixels={2048*2048}; occupants={world.Occupants.Length}; save={saved.Length}; snapshot={snapshot.Length}; managed={allocated}; decoderNativePeak={decoded.DecoderPeakBytes}; ms={watch.Elapsed.TotalMilliseconds:F2}; process peak RSS={System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64}");
    }
    private static HeightmapWorldRequest Request(HeightmapInput image, int h = 4, int v = 4, int z = 8) => new(WorldCreationProfile.Id, "Imported / 地形", new(h, v, z), 0, image);
    private static DecodedHeightmap Ramp() => HeightmapDecoder.Decode(Bmp(2, 2, (x, y) => x == 0 ? ((byte)0, (byte)0, (byte)0) : ((byte)255, (byte)255, (byte)255)));
    [Theory]
    [InlineData(0,0,0,0)][InlineData(255,255,255,1)][InlineData(255,0,0,0.5f)][InlineData(0,255,0,0.5f)][InlineData(0,0,255,0.5f)]
    public void Opaque_one_pixel_black_white_and_primaries_use_HSL_not_luminance(int r, int g, int b, float depth)
    {
        var bytes = Bmp(1, 1, (_, _) => ((byte)r, (byte)g, (byte)b)); var image = HeightmapDecoder.Decode(bytes, "image/bmp");
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), image.Input.Sha256);
        Assert.Equal(depth, image.Raster.Depth(0, 0, new(1, 1, 8)));
        var world = HeightmapTerrain.Create(Request(image.Input, 1, 1), image.Raster, TerrainProfiles.Bundled);
        Assert.Equal(Math.Min(7, (int)(8 * depth)), world.Cells[0].Ground); Assert.Empty(world.Occupants); Assert.Empty(world.Rails);
    }
    [Fact]
    public void Staggered_bilinear_sampling_clamped_borders_and_lowering_have_literal_source_expectations()
    {
        var image = Ramp(); var d = new WorldDimensions(4, 4, 8);
        Assert.Equal(0f, image.Raster.Depth(0, 0, d)); Assert.Equal(.25f, image.Raster.Depth(0, 1, d));
        Assert.Equal(.75f, image.Raster.Depth(1, 1, d)); Assert.Equal(1f, image.Raster.Depth(3, 1, d));
        Assert.Equal(1f, image.Raster.Depth(3.5f, 3, d));
        var world = HeightmapTerrain.Create(Request(image.Input), image.Raster, TerrainProfiles.Bundled);
        // Independent min-plus graph distances across shared-corner cliques.
        // Distances from the zero-height left boundary constrain all cells, even
        // though the displaced right-hand columns started at height seven.
        Assert.Equal(new[] {0,1,2,3, 1,2,3,4, 0,1,2,3, 1,2,3,4}, world.Cells.Select(c => c.Ground));
        Assert.Contains(world.Occupants, o => o.Corners.Packed != 0);
        Assert.All(world.Occupants, o => Assert.All(Enumerable.Range(0,4), c => Assert.InRange(o.Corners[c],0,4)));
    }
    [Fact]
    public void Smoothing_has_quarter_offsets_and_borders_remain_natural_on_independent_gradients_and_checkerboards()
    {
        foreach (var side in new[] {1, 2, 3, 8, 16}) foreach (bool checker in new[] {false, true})
        {
            var image = HeightmapDecoder.Decode(Bmp(side, side, (x,y) => { byte c = checker ? (byte)((x+y)%2*255) : (byte)(x*255/Math.Max(1,side-1)); return(c,c,c); }));
            var world = HeightmapTerrain.Create(Request(image.Input, 8, 8, 6), image.Raster, TerrainProfiles.Bundled);
            Assert.All(world.Cells, c => Assert.Equal(TerrainSurface.Natural,c.Surface));
            if (side == 8 && !checker)
            {
                // At H0,V0 the flat 2x2 has a higher surround. The half-pixel
                // sample is 18/255; floor(24*18/255)=1. Outside corners stay0.
                Assert.Equal(new QuarterCorners(0,1,0,0),world.Occupants[0].Corners);
                Assert.Equal(world.Space.ToLocation(new(0,0,0)),world.Spatial.Entities[0].Anchor);
            }
            var bytes = SaveCodec.Write(new TerrainSessionState(world, TerrainProvenance.From(Request(image.Input,8,8,6))));
            Assert.Equal(bytes, SaveCodec.Write(SaveCodec.ReadWorld(bytes,TerrainProfiles.Resolve).Terrain!));
        }
    }
    [Theory]
    [InlineData("format")][InlineData("mime")][InlineData("bytes")][InlineData("pixels")][InlineData("truncated")][InlineData("alpha")][InlineData("apng")][InlineData("animation")]
    public void Decoder_rejects_untrusted_input_before_generation(string kind)
    {
        var bytes = Bmp(1,1,(_,_)=>((byte)0,(byte)0,(byte)0)); string expected = "image-invalid"; string? mime = null;
        switch(kind)
        {
            case "format": bytes="not an image"u8.ToArray();expected="image-format";break;
            case "mime":mime="image/png";expected="image-format";break;
            case "bytes":bytes=new byte[8*1024*1024+1];expected="image-bytes";break;
            case "pixels":BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18),4097);expected="image-pixels";break;
            case "truncated":bytes=bytes[..30];break;
            case "alpha":bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR4nGNgAAIAAAUAAXpeqz8AAAAASUVORK5CYII=");expected="image-alpha";break;
            case "apng": bytes=new byte[]{137,80,78,71,13,10,26,10,0,0,0,0,97,99,84,76,0,0,0,0};expected="image-animation";break;
            case "animation":bytes=Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAkQBADs=");bytes=bytes[..^1].Concat(bytes[19..^1]).Append((byte)59).ToArray();expected="image-animation";break;
        }
        Assert.Equal(expected,Assert.Throws<HeightmapException>(()=>HeightmapDecoder.Decode(bytes,mime)).Code);
    }
    [Fact]
    public void Cancellation_is_cooperative_and_never_produces_partial_terrain()
    {
        using var cancel = new CancellationTokenSource();cancel.Cancel();var image=Ramp();
        Assert.Throws<OperationCanceledException>(()=>HeightmapDecoder.Decode(Bmp(1,1,(_,_)=>((byte)0,(byte)0,(byte)0)), cancellation:cancel.Token));
        Assert.Throws<OperationCanceledException>(()=>HeightmapTerrain.Create(Request(image.Input),image.Raster,TerrainProfiles.Bundled,cancel.Token));
    }
    [Fact]
    public async Task Upload_reservations_are_global_released_on_cancel_and_fenced_to_the_original_connection()
    {
        var session=new NetworkSession(new());var identity=new ParticipantContext("upload-owner",ParticipantRole.Owner);
        var peer=session.Connect(DateTimeOffset.UtcNow,identity)!;
        var leases=new List<NetworkSession.CreationWorkerLease>();
        try
        {
            for(int i=0;i<4;i++)leases.Add(await session.ReserveCreationWorker(identity));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ReserveCreationWorker(identity));
            session.Disconnect(peer,DateTimeOffset.UtcNow);session.Connect(DateTimeOffset.UtcNow,identity);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>session.PreviewHeightmap(new("stale",new(1,1,1),0,1,0),
                Bmp(1,1,(_,_)=>((byte)0,(byte)0,(byte)0)),"image/bmp",OptionalPluginRegistry.Empty,identity,leases[0],CancellationToken.None));
        }
        finally{foreach(var lease in leases)lease.Dispose();}
        using var released=await session.ReserveCreationWorker(identity);
        foreach(var role in new[]{ParticipantRole.Observer,ParticipantRole.Operator})
        {
            var other=new ParticipantContext(role.ToString(),role);session.Connect(DateTimeOffset.UtcNow,other);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>session.ReserveCreationWorker(other));
        }
    }
    [Fact]
    public async Task Http_import_is_fenced_and_publishes_exact_terrain_to_two_clients_then_restarts_without_image()
    {
        string directory=Path.Combine(Path.GetTempPath(),"heightmap-http-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        byte[] saved;
        try
        {
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                var before=await server.Bootstrap();using var first=await server.Connect();await NetworkTests.Read(first);
                var secondCookies=new System.Net.CookieContainer();using var secondHttp=new HttpClient(new HttpClientHandler{CookieContainer=secondCookies}){BaseAddress=server.Http.BaseAddress};
                await secondHttp.GetStringAsync("/api/bootstrap?protocolVersion=5&contentVersion=reference-spatial-content-2");
                using var second=await server.Connect(secondCookies);await NetworkTests.Read(second);
                async Task<HttpResponseMessage> Preview(long revision, byte[]? bytes=null)
                {
                    var body=new ByteArrayContent(bytes??Bmp(2,2,(x,y)=>x==0?((byte)0,(byte)0,(byte)0):((byte)255,(byte)255,(byte)255)));
                    body.Headers.ContentType=new("image/bmp");return await server.Http.PostAsync($"/api/worlds/heightmap/preview?name=Imported&h=4&v=4&z=8&water=1&epoch={before.WorldEpoch}&revision={revision}",body);
                }
                Assert.Equal(System.Net.HttpStatusCode.Conflict,(await Preview(before.Snapshot.Revision+1)).StatusCode);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest,(await Preview(before.Snapshot.Revision,"bad"u8.ToArray())).StatusCode);
                var response=await Preview(before.Snapshot.Revision);Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
                var candidate=System.Text.Json.JsonSerializer.Deserialize<EmptyCandidateRead>(await response.Content.ReadAsStringAsync(),NetworkSession.Json)!;
                Assert.Equal(before.WorldEpoch,(await server.Bootstrap()).WorldEpoch);
                var commit=new EmptyCommitRequest(candidate.Id,candidate.RequestHash,candidate.CandidateHash,before.WorldEpoch,before.Snapshot.Revision);
                Assert.True((await server.Http.PostAsync("/api/worlds/empty/commit",new StringContent(System.Text.Json.JsonSerializer.Serialize(commit,NetworkSession.Json),System.Text.Encoding.UTF8,"application/json"))).IsSuccessStatusCode);
                foreach(var socket in new[]{first,second})
                {
                    var snapshot=Assert.IsType<FreeTrain.Online.Contracts.SnapshotMessage>(await NetworkTests.Until(socket,m=>m is FreeTrain.Online.Contracts.SnapshotMessage));
                    Assert.Equal(before.WorldEpoch+1,snapshot.WorldEpoch);Assert.Equal(candidate.Ground,snapshot.Terrain!.Cells.Select(c=>c.Ground));Assert.Empty(snapshot.Tracks);
                }
                saved=await server.Http.GetByteArrayAsync("/api/saves/download");
                Assert.Equal("heightmap",SaveCodec.ReadWorld(saved,TerrainProfiles.Resolve).Terrain!.Provenance.Source);
            }
            await using(var server=await NetworkTests.Harness.Start(saveDirectory:directory,canonical:true))
            {
                await server.Http.GetStringAsync("/api/bootstrap?protocolVersion=5&contentVersion=terrain-content-1");
                Assert.Equal(saved,await server.Http.GetByteArrayAsync("/api/saves/download"));
            }
        }
        finally{Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task Four_real_retained_heightmaps_exclude_a_fifth_and_cancel_releases_capacity_without_replacing_worlds()
    {
        var owner = new ParticipantContext("owner", ParticipantRole.Owner, "retained-heightmap");
        var hosts = Enumerable.Range(0, 5).Select(_ => SessionComposition.CreateReferenceHost()).ToArray();
        async Task<T> Drain<T>(SessionHost host, Task<T> task) { host.Pump(); return await task; }
        try
        {
            foreach (var host in hosts) host.RegisterParticipant(owner);
            var image = HeightmapDecoder.Decode(Bmp(64, 64, (x, y) => { byte c = (byte)((x + y) % 2 * 255); return (c, c, c); }));
            var request = Request(image.Input, 32, 32, 64);
            var bytes = SaveCodec.Write(new TerrainSessionState(HeightmapTerrain.Create(request, image.Raster, TerrainProfiles.Bundled), TerrainProvenance.From(request)));
            var candidates = new List<EmptyCandidateRead>();
            var originals = new List<byte[]>();
            for (int i = 0; i < 4; i++)
            {
                var host = hosts[i]; originals.Add(await Drain(host, host.CaptureSave()));
                var job = await Drain(host, host.BeginHeightmap(request, WorldCreationCapabilities.EmptyReady(), host.WorldEpoch, host.Snapshot().Revision, owner));
                candidates.Add(await Drain(host, host.CompleteEmpty(job, bytes, owner)));
                Assert.Equal(originals[i], await Drain(host, host.CaptureSave()));
            }
            var fifth = hosts[4];
            var busy = fifth.BeginHeightmap(request, WorldCreationCapabilities.EmptyReady(), fifth.WorldEpoch, fifth.Snapshot().Revision, owner); fifth.Pump();
            await Assert.ThrowsAsync<InvalidOperationException>(() => busy);
            var cancel = hosts[0].CancelEmpty(candidates[0].Id, owner); hosts[0].Pump(); await cancel;
            Assert.Equal(originals[0], await Drain(hosts[0], hosts[0].CaptureSave()));
            var admitted = await Drain(fifth, fifth.BeginHeightmap(request, WorldCreationCapabilities.EmptyReady(), fifth.WorldEpoch, fifth.Snapshot().Revision, owner));
            Assert.NotNull(admitted);
            output.WriteLine($"Four real retained dense candidates: saveBytes={string.Join(',', candidates.Select(c => c.SaveBytes))}; snapshotBytes={string.Join(',', candidates.Select(c => c.SnapshotBytes))}; fifth rejected, then admitted after cancellation.");
        }
        finally { foreach (var host in hosts) host.Dispose(); }
        using var released = SessionHost.ReserveCreationInput();
    }

    [Theory]
    [InlineData(1,1,1)][InlineData(16,16,16)][InlineData(32,32,64)]
    public async Task Real_heightmap_preview_exact_commit_reopen_and_allocation_envelope(int h,int v,int z)
    {
        var owner=new ParticipantContext("owner",ParticipantRole.Owner,"heightmap-test");
        using var host=SessionComposition.CreateReferenceHost();host.RegisterParticipant(owner);
        async Task<T> Drain<T>(Task<T> task){host.Pump();return await task;}
        long before=GC.GetAllocatedBytesForCurrentThread();var timer=System.Diagnostics.Stopwatch.StartNew();
        var image=HeightmapDecoder.Decode(Bmp(64,64,(x,y)=>{byte c=(byte)((x+y)%2*255);return(c,c,c);}));
        var request=Request(image.Input,h,v,z);var job=await Drain(host.BeginHeightmap(request,WorldCreationCapabilities.EmptyReady(),host.WorldEpoch,host.Snapshot().Revision,owner));
        var world=HeightmapTerrain.Create(request,image.Raster,TerrainProfiles.Bundled);
        var bytes=SaveCodec.Write(new TerrainSessionState(world,TerrainProvenance.From(request)));var original=await Drain(host.CaptureSave());
        var c=await Drain(host.CompleteEmpty(job,bytes,owner)); Assert.Equal(original,await Drain(host.CaptureSave()));
        await Drain(host.CommitEmpty(c.Id,c.RequestHash,c.CandidateHash,host.WorldEpoch,host.Snapshot().Revision,owner));
        Assert.Equal(bytes,await Drain(host.CaptureSave()));await Drain(host.LoadSave(bytes));Assert.Equal(bytes,await Drain(host.CaptureSave()));
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        output.WriteLine($"{h}x{v}x{z}: occupants={world.Occupants.Length}; save={c.SaveBytes}; snapshot={c.SnapshotBytes}; managed={allocated}; ms={timer.Elapsed.TotalMilliseconds:F2}");
        Assert.True(allocated<WorldCreationProfile.MaxWorkingBytes);Assert.True(c.SaveBytes<=WorldCreationProfile.Estimate(request).TransferBytes);
    }
}
