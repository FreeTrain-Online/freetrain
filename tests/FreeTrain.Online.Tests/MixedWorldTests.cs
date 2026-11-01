using System.Collections.Immutable;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Simulation;
using Xunit;

namespace FreeTrain.Online.Tests;

public class MixedWorldTests
{
    static RailwayProfile Profile => RailwayProfiles.Bundled;
    static DefinitionReference Ref(string id) => new(PreparedContentCatalog.BundledPackId, id, id);
    static RailComponent Rail(int pattern) => new(Ref($"system:pattern/{pattern}"), pattern);
    static Location P => new(8,8,2);
    static TerrainWorld Terrain() => new(TerrainProfiles.Bundled,"Mixed",new(32,16,8),0,
        Enumerable.Repeat(new TerrainCell(2),32*16),new(1,[]),[]);
    static SpatialEntity Traffic(long id, Location p, long revision = 0) => new(id,revision,Profile.TrafficOwner,p,
        new([new(new(0,0,0),true)]),true,false,0);
    static MixedWorld Single(TrafficComponents components, RailwayProfile? profile = null) =>
        new(profile ?? Profile,Terrain(),new(2,[Traffic(1,P)]),[new(P,1,RailwayVoxelKind.Traffic,components)],[]);
    static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spatial/railway-fixtures.json")));
    static Location Point(JsonElement a) => new(a[0].GetInt32(),a[1].GetInt32(),a[2].GetInt32());

    [Fact]
    public void Zero_rail_terrain_remains_exact_and_reference_loop_keeps_its_invariant()
    {
        var terrain = Terrain(); var mixed = MixedWorld.FromTerrain(Profile,terrain);
        Assert.Same(terrain,mixed.Terrain); Assert.Empty(mixed.Voxels); Assert.Equal(1,mixed.Spatial.NextEntityId);
        Assert.Throws<ArgumentException>(() => new ReferenceWorld(32,32,8,[new(P,1)]));
        Assert.False(PreparedContentCatalog.Bundled.GameplayAvailable);
    }

    [Fact]
    public void Every_literal_normal_and_junction_pattern_allows_open_disconnected_track()
    {
        using var fixtures = Fixtures();
        foreach (var row in fixtures.RootElement.GetProperty("patterns").EnumerateArray().Take(28))
        {
            var pattern = row.GetProperty("id").GetInt32();
            var world = new MixedWorld(Profile,Terrain(),new(3,[Traffic(1,P),Traffic(2,new(14,14,4))]),
                [new(P,1,RailwayVoxelKind.Traffic,new(Rail(pattern))),new(new(14,14,4),2,RailwayVoxelKind.Traffic,new(Rail(pattern)))],[]);
            Assert.Equal(pattern,world.At(P)!.Traffic!.Rail!.Pattern);
            Assert.False(world.Spatial.At(P).Reusable); Assert.True(world.Spatial.At(P).Transparent);
        }
    }

    [Fact]
    public void Bundled_registration_is_exact_and_does_not_admit_prepared_unimplemented_content()
    {
        Assert.Equal(213,Profile.Bindings.Length);
        Assert.Equal(54,Profile.Bindings.Count(b => b.Handler == RailwayHandler.Stationary));
        Assert.Equal(67,Profile.Bindings.Count(b => b.Handler == RailwayHandler.ConstructionArt));
        Assert.Equal(60,Profile.Bindings.Count(b => b.Handler is RailwayHandler.Normal or RailwayHandler.Junction or RailwayHandler.Slope));
        foreach (var reference in new[] { Ref("system:pattern/60"),Ref("system:pattern/01"),
            Ref("system:fat-0"),Ref("system:ground/spring"),new DefinitionReference("other","system:pattern/1","system:pattern/1"),
            new DefinitionReference(PreparedContentCatalog.BundledPackId,"system:pattern/1","system:pattern/2") })
            Assert.Throws<ArgumentException>(() => Single(new(new(reference,1))));
        Assert.Throws<ArgumentException>(() => Single(new(Rail(1) with { Pattern = 12 })));
        Assert.Throws<ArgumentException>(() => Single(new(Rail(1) with { Pattern = -1 })));
        Assert.Throws<ArgumentException>(() => Single(new(Rail(1) with { TerrainBackup = new(1,2,3,4) })));
        Assert.Throws<ArgumentException>(() => Single(new(Rail(1) with { Definition = Ref("system:pier") })));
        Assert.Throws<ArgumentException>(() => new RailwayProfile(Profile.Terrain,[Profile.Bindings[0],Profile.Bindings[0]]));
        Assert.Throws<ArgumentException>(() => new RailwayProfile(Profile.Terrain,[Profile.Bindings[0] with { Variant = 12 }]));
        var top = P with { Z = 7 };
        Assert.Throws<ArgumentException>(() => new MixedWorld(Profile,Terrain(),new(2,[Traffic(1,top)]),
            [new(top,1,RailwayVoxelKind.Traffic,new(new(Ref("{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}"),10,
                TerrainBackup:new(4,0,0,0))))],[]));
    }

    [Fact]
    public void Trusted_traffic_slots_coexist_under_one_owner_without_implying_other_gameplay()
    {
        var road = Ref("test:road"); var car = Ref("test:car"); var crossing = Ref("test:crossing"); var platform = Ref("test:platform");
        var profile = new RailwayProfile(Profile.Terrain,Profile.Bindings.Concat(new[] {
            new RailwayBinding(road,RailwayHandler.Road),new(car,RailwayHandler.Car),
            new(crossing,RailwayHandler.Crossing),new(platform,RailwayHandler.Platform) }));
        var t = new TrafficComponents(Rail(10),new(road,68),new(crossing),new(car,11,12),platform);
        var world = Single(t,profile);
        Assert.Single(world.Spatial.Entities); Assert.Equal(t,world.At(P)!.Traffic);
        Assert.Throws<ArgumentException>(() => Single(t));
        Assert.NotNull(Single(t with { Rail = null, Platform = null, Accessory = null },profile).At(P)!.Traffic!.Road);
        Assert.Throws<ArgumentException>(() => Single(new(Car: new(car,11,12)),profile));
        Assert.Throws<ArgumentException>(() => Single(t with { Road = new(road,256) },profile));
        Assert.Throws<ArgumentException>(() => Single(t with { Car = new(car,0,12) },profile));
        Assert.Throws<ArgumentException>(() => Single(t with { Accessory = new(road) },profile));
        Assert.Throws<ArgumentException>(() => Single(t with { Platform = road },profile));
        Assert.Throws<ArgumentException>(() => Single(t with { Rail = null },profile));
    }

    internal static (SpatialEntity Entity, RailwayVoxel[] Voxels, SlopeOwnership Owner) Slope(JsonElement row)
    {
        var input = row.GetProperty("input"); var expected = row.GetProperty("expected");
        var anchor = Point(input.GetProperty("base")); var direction = input.GetProperty("direction").GetInt32();
        var cells = new List<RailwayVoxel>();
        foreach (var r in expected.GetProperty("rails").EnumerateArray())
            cells.Add(new(Point(r.GetProperty("location")),1,RailwayVoxelKind.Traffic,new(Rail(r.GetProperty("patternId").GetInt32()))));
        var i = 0;
        foreach (var pair in expected.GetProperty("paired").EnumerateArray())
        {
            var kind = pair.GetProperty("kind").GetString() switch {
                "filler" => RailwayVoxelKind.SlopeFiller,"support" => RailwayVoxelKind.SlopeSupport,_ => RailwayVoxelKind.SlopeWall };
            var pattern = direction/2*4+i;
            cells.Add(new(Point(pair.GetProperty("location")),1,kind,Definition: kind == RailwayVoxelKind.SlopeFiller ? null :
                Ref($"system:pattern/{(kind == RailwayVoxelKind.SlopeWall ? "underground-" : "")}slope-support-{pattern}"),
                Sprite: kind == RailwayVoxelKind.SlopeFiller ? 0 : pair.GetProperty("sprite").GetInt32(),
                DrawGroundBelow:pair.GetProperty("below").GetBoolean(),DrawGroundAbove:pair.GetProperty("above").GetBoolean()));
            i++;
        }
        var e = new SpatialEntity(1,0,Profile.SlopeOwner,anchor,new(cells.Select(v => new VoxelMember(
            new(v.Position.X-anchor.X,v.Position.Y-anchor.Y,v.Position.Z-anchor.Z),true))),true,false,0);
        return (e,cells.ToArray(),new(1,new(direction)));
    }

    [Fact]
    public void Literal_cardinal_slope_footprints_preserve_stored_mixed_art_and_flags()
    {
        using var fixtures = Fixtures(); var count = 0;
        foreach (var row in fixtures.RootElement.GetProperty("groups").GetProperty("slopes").GetProperty("cases").EnumerateArray()
            .Where(r => r.GetProperty("expected").GetProperty("outcome").GetString() == "apply"))
        {
            var (entity,cells,owner) = Slope(row);
            var world = new MixedWorld(Profile,Terrain(),new(2,[entity]),cells,[owner]);
            Assert.Equal(8,world.Voxels.Length); Assert.Single(world.Spatial.Entities);
            foreach (var cell in cells)
            {
                Assert.Equal(cell,world.At(cell.Position));
                Assert.True(world.Spatial.At(cell.Position).Transparent);
                Assert.False(world.Spatial.At(cell.Position).Reusable);
            }
            count++;
        }
        Assert.True(count >= 12);
    }

    [Fact]
    public void Malformed_slope_members_owners_patterns_recipes_and_transparency_reject_atomically()
    {
        using var fixtures = Fixtures(); var row = fixtures.RootElement.GetProperty("groups").GetProperty("slopes").GetProperty("cases")[0];
        var (e,cells,owner) = Slope(row); var original = MixedWorld.FromTerrain(Profile,Terrain());
        void Reject(SpatialEntity entity, RailwayVoxel[] v, SlopeOwnership[] s) =>
            Assert.Throws<ArgumentException>(() => original.Replace(0,original.Terrain,new(2,[entity]),v,s));
        Reject(e,cells[..7],[owner]); Reject(e,cells,[]); Reject(e,cells,[owner with { Climb = new(1) }]);
        var bad = cells.ToArray(); bad[0] = bad[0] with { Traffic = new(Rail(32)) }; Reject(e,bad,[owner]);
        bad = cells.ToArray(); bad[4] = bad[4] with { Kind = RailwayVoxelKind.SlopeSupport,Definition = Ref("system:pier") }; Reject(e,bad,[owner]);
        bad = cells.ToArray(); bad[0] = bad[0] with { OwnerId = 2 }; Reject(e,bad,[owner]);
        Reject(e with { Footprint = new(e.Footprint.Members.Select(m => m with { Transparent = false })) },cells,[owner]);
        Reject(e with { Anchor = P with { Z = 7 } },cells,[owner]);
        Assert.Empty(original.Voxels); Assert.Equal(1,original.Spatial.NextEntityId); Assert.Equal(0,original.Revision);
    }

    [Fact]
    public void Terrain_entities_and_backups_survive_mixed_composition_and_overlap_rejection()
    {
        var terrain = Terrain();
        terrain = CornerTerrain.Apply(terrain,CornerTerrain.Preview(terrain,P,"raise"));
        var draft = new CornerTerrainDraft(terrain);
        TerraceTerrain.Apply(draft,TerraceTerrain.Preview(draft,P,"placeTerrace")); terrain = draft.Finish();
        var id = terrain.Spatial.NextEntityId;
        var track = Traffic(id,P with { Z = 3 });
        var all = terrain.Spatial.Entities.Add(track);
        var world = new MixedWorld(Profile,terrain,new(id+1,all),[new(track.Anchor,id,RailwayVoxelKind.Traffic,new(Rail(1)))],[],terrain.Revision);
        Assert.Same(terrain,world.Terrain); Assert.Equal(terrain.OccupantAt(P),world.Terrain.OccupantAt(P));
        Assert.Equal(terrain.Spatial.Entities.Length+1,world.Spatial.Entities.Length);
        Assert.False(world.Terrain.OccupantAt(P)!.Corners.Flat);
        Assert.Throws<ArgumentException>(() => new MixedWorld(Profile,terrain,new(id+1,all.SetItem(all.Length-1,track with { Anchor = P })),
            [new(P,id,RailwayVoxelKind.Traffic,new(Rail(1)))],[],terrain.Revision));
        Assert.Throws<ArgumentException>(() => new MixedWorld(Profile,terrain,new(id+1,[track]),
            [new(track.Anchor,id,RailwayVoxelKind.Traffic,new(Rail(1)))],[],terrain.Revision));
        Assert.Equal(terrain.OccupantAt(P)!.EntityId,world.Spatial.At(P).Entity!.Id);
    }

    [Theory]
    [InlineData("{874B8BFD-1EF4-4828-9C5A-D0EA1049D28D}",RailwayHandler.Bridge)]
    [InlineData("{7FD493FF-D6A7-4a13-A471-FB737641D8A0}",RailwayHandler.Girder)]
    [InlineData("{3E66FC7A-2B58-4182-8A8E-2D5A01BE78E5}",RailwayHandler.Tunnel)]
    [InlineData("{93481807-D998-46d7-B1BE-CB2B5D086D67}",RailwayHandler.Garage)]
    [InlineData("{5349DE2A-DF3E-49B4-87A0-973105B7E6A4}",RailwayHandler.Signal)]
    public void Special_subtype_and_tunnel_backup_are_preserved(string id,RailwayHandler kind)
    {
        var backup = kind == RailwayHandler.Tunnel ? new QuarterCorners(1,2,3,4) : (QuarterCorners?)null;
        var rail = new RailComponent(Ref(id),10,TerrainBackup:backup);
        Assert.Equal(rail,Single(new(rail)).At(P)!.Traffic!.Rail);
        Assert.Throws<ArgumentException>(() => Single(new(rail with { TerrainBackup = backup.HasValue ? null : new(1,2,3,4) })));
    }

    [Fact]
    public void Pier_is_a_separate_voxel_with_explicit_supported_owner_and_removes_independently()
    {
        var p = P with { Z = 1 }; var e = new SpatialEntity(2,0,Ref("system:pier"),p,
            new([new(new(0,0,0),true)]),true,false,0);
        var t = new RailwayVoxel(P,1,RailwayVoxelKind.Traffic,new(Rail(1)));
        var pier = new RailwayVoxel(p,2,RailwayVoxelKind.Pier,Definition:Ref("system:pier"),SupportedOwnerId:1);
        var world = new MixedWorld(Profile,Terrain(),new(3,[Traffic(1,P),e]),[t,pier],[]);
        var after = world.Replace(0,world.Terrain,new(3,[Traffic(1,P)]),[t],[]);
        Assert.Null(after.At(p)); Assert.Equal(t,after.At(P)); Assert.Equal(3,after.Spatial.NextEntityId);
        Assert.Equal(pier,world.At(p));
        // Canonical historical support references can outlive their rail.
        var orphan = world.Replace(0,world.Terrain,new(3,[e]),[pier],[]);
        Assert.Equal(1,orphan.At(p)!.SupportedOwnerId); Assert.Null(orphan.At(P));
        Assert.Throws<ArgumentException>(() => new MixedWorld(Profile,Terrain(),new(3,[Traffic(1,P),e]),[t,pier with { SupportedOwnerId = 7 }],[]));
    }

    [Fact]
    public void Replacement_validates_allocator_identity_revision_and_all_payloads_before_publication()
    {
        var world = Single(new(Rail(1))); var t = world.Voxels[0];
        var updated = t with { Traffic = new(Rail(4)) }; var e = world.Spatial.Entities[0];
        Assert.Throws<InvalidOperationException>(() => world.Replace(1,world.Terrain,world.Spatial.Capture(),[t],[]));
        Assert.Throws<ArgumentException>(() => world.Replace(0,world.Terrain,world.Spatial.Capture(),[updated],[]));
        Assert.Throws<ArgumentException>(() => world.Replace(0,world.Terrain,new(3,[e with { Revision = 2 }]),[updated],[]));
        Assert.Throws<ArgumentException>(() => world.Replace(0,world.Terrain,new(1,[]),[],[]));
        Assert.Throws<ArgumentException>(() => world.Replace(0,world.Terrain,new(3,[e,Traffic(2,P)]),[t],[]));
        Assert.Throws<ArgumentException>(() => world.Replace(0,world.Terrain,new(2,[e]),[t,t],[]));
        Assert.Throws<ArgumentException>(() => world.Replace(0,world.Terrain,new(2,[e]),[updated with { Traffic = new(Rail(1) with { Pattern = -1 }) }],[]));
        var next = world.Replace(0,world.Terrain,new(2,[e with { Revision = 1 }]),[updated],[]);
        Assert.Equal(1,next.Revision); Assert.Equal(4,next.At(P)!.Traffic!.Rail!.Pattern);
        var removed = next.Replace(1,next.Terrain,new(2,[]),[],[]);
        Assert.Throws<ArgumentException>(() => removed.Replace(2,removed.Terrain,new(2,[e]),[t],[]));
        Assert.Equal(1,world.At(P)!.Traffic!.Rail!.Pattern); Assert.Equal(0,world.Revision); Assert.Equal(2,world.Spatial.NextEntityId);
        var identity = world.Replace(0,world.Terrain,world.Spatial.Capture(),[t],[]); Assert.Equal(1,identity.Revision);
    }
    [Fact]
    public void Shared_allocator_and_detached_terrain_changes_publish_together_or_preserve_the_original()
    {
        var world = Single(new(Rail(1))); var old = world.Terrain;
        var request = new EmptyWorldRequest(WorldCreationProfile.Id,old.Name,new(32,16,8),old.WaterLevel);
        var session = new TerrainSessionState(old,TerrainProvenance.From(request));
        var before = SaveCodec.Write(session);
        var site = new Location(14,14,2);
        // Terrain tools receive the shared high-water mark before they allocate.
        var basis = new TerrainWorld(old.Profile,old.Name,new(32,16,8),old.WaterLevel,old.Cells,
            new(world.Spatial.NextEntityId,old.Spatial.Entities),old.Occupants,old.Revision);
        var edited = CornerTerrain.Apply(basis,CornerTerrain.Preview(basis,site,"raise"));
        var spatial = new SpatialState(edited.Spatial.NextEntityId,edited.Spatial.Entities.AddRange(world.Spatial.Entities));
        var mixed = world.Replace(0,edited,spatial,world.Voxels,[]);
        Assert.Same(edited,mixed.Terrain); Assert.True(world.Voxels.SequenceEqual(mixed.Voxels));
        Assert.True(mixed.Terrain.Occupants.Length > 0); Assert.Equal(1,mixed.Revision);
        // A terrain-only allocator would reuse rail owner 1. Reject the entire graph.
        var reused = CornerTerrain.Apply(old,CornerTerrain.Preview(old,site,"raise"));
        Assert.Throws<ArgumentException>(() => world.Replace(0,reused,
            new(reused.Spatial.NextEntityId,reused.Spatial.Entities.AddRange(world.Spatial.Entities)),world.Voxels,[]));
        Assert.Same(old,world.Terrain); Assert.Equal(2,world.Spatial.NextEntityId);
        Assert.Equal(before,SaveCodec.Write(session));
    }

}
