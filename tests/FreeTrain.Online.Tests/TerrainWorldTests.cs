using System.Collections.Immutable;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Simulation;
using Xunit;

public class TerrainWorldTests
{
    private static TerrainProfile Profile => TerrainProfiles.Bundled;
    private static DefinitionReference Mountain => new(PreparedContentCatalog.BundledPackId,
        TerrainProfile.MountainId, TerrainProfile.MountainGeometry);
    private static DefinitionReference Definition(TerrainOccupantKind kind) => Profile.Definitions.First(d => d.Kind == kind && d.TreeSpriteCount == 0).Reference;
    private static SpatialEntity Entity(long id, Location anchor, QuarterCorners corners, TerrainOccupantKind kind = TerrainOccupantKind.Mountain) =>
        new(id, 0, Definition(kind), anchor, new Footprint([new(new(0, 0, 0),
            kind == TerrainOccupantKind.Creek || kind == TerrainOccupantKind.Mountain && corners.Flat)]), false,
            kind == TerrainOccupantKind.Creek, 0);
    private static TerrainWorld Single(QuarterCorners corners, int ground = 1, int levels = 4) =>
        new(Profile, "山", new(1, 1, levels), 1, [new(ground)], new(2, [Entity(1, new(0, 0, ground), corners)]),
            [new(1, TerrainOccupantKind.Mountain, corners)]);

    [Fact]
    public void Empty_default_includes_all_border_cells_zero_rails_and_no_constructor_side_effects()
    {
        var request = WorldCreationProfile.Empty("  名前  ");
        var world = TerrainWorld.Empty(request, Profile);
        var read = world.Read();
        Assert.Empty(world.Rails); Assert.Empty(read.Spatial.Entities); Assert.Equal(1, read.Spatial.NextEntityId);
        Assert.Empty(read.Occupants); Assert.Equal(1260, read.Cells.Length);
        Assert.All(read.Cells, c => Assert.Equal(new TerrainCell(1), c));
        Assert.Equal(new(30, 42, 6), read.Dimensions); Assert.Equal("  名前  ", read.Name);
        Assert.Equal(TerrainProfile.Ruleset, read.Ruleset); Assert.Equal(0, read.Revision);
        Assert.Equal(4, world.FineHeight(world.Space.ToLocation(new(29, 41, 0)), 3));
        // The stricter synthetic profile is still a loop, not a terrain fallback.
        Assert.Equal(32, SpatialReferenceScenario.Create().Rails.Length);
        Assert.Throws<ArgumentException>(() => new ReferenceWorld(30, 42, 6, []));
        Assert.Null(WorldProfiles.Resolve(TerrainProfile.Ruleset, TerrainProfile.ContentVersion, []));
    }

    [Theory]
    // TERRAIN-017..020: independent strict ground/water comparisons.
    [InlineData(2, 2, 1, GroundRelation.Underground, true)]
    [InlineData(2, 2, 2, GroundRelation.Ground, false)]
    [InlineData(1, 2, 1, GroundRelation.Ground, true)]
    [InlineData(1, 2, 3, GroundRelation.Elevated, false)]
    public void Ground_and_underwater_are_independent(int ground, int water, int z, GroundRelation relation, bool underwater)
    {
        var world = new TerrainWorld(Profile, "", new(1, 1, 4), water, [new(ground)], new(1, []), []);
        var read = world.At(new(0, 0, z));
        Assert.Equal(relation, read.Relation); Assert.Equal(underwater, read.Underwater);
        Assert.Equal(VoxelStatus.Vacant, read.Occupancy.Status);
        Assert.Equal(GroundRelation.Outside, world.At(new(0, 0, -1)).Relation);
        Assert.Equal(-1, world.FineHeight(new(9, 9, 0), 0));
    }

    [Fact]
    public void Stored_asymmetric_grid_is_not_shrunk_to_train_border()
    {
        var world = TerrainWorld.Empty(new(WorldCreationProfile.Id, "", new(5, 8, 4), 1), Profile);
        var corners = new[] { new Location(3, 0, 1), new(7, 4, 1), new(0, 4, 1), new(4, 8, 1) };
        foreach (var p in corners)
        {
            Assert.Equal(GroundRelation.Ground, world.At(p).Relation);
            Assert.True(world.IsTrainBorder(p));
        }
        var deeper = TerrainWorld.Empty(new(WorldCreationProfile.Id, "", new(5, 12, 4), 1), Profile);
        Assert.False(deeper.IsTrainBorder(deeper.Space.ToLocation(new(2, 4, 1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Cell(5, 0));
        Assert.Throws<OverflowException>(() => world.IsTrainBorder(new(3, 0, int.MaxValue)));
        Assert.Equal(GroundRelation.Outside, world.At(new(3, 0, 4)).Relation);
    }

    [Fact]
    public void Shared_quarter_peak_matches_four_touching_cells_with_source_corner_order()
    {
        // One shared NE corner at XYZ(3,3): NE/SW, N/SE, E/NW.
        var peaks = new[] { (new Location(3,3,1), new QuarterCorners(2,0,0,0)),
            (new Location(4,2,1), new QuarterCorners(0,0,2,0)),
            (new Location(3,2,1), new QuarterCorners(0,2,0,0)),
            (new Location(4,3,1), new QuarterCorners(0,0,0,2)) };
        var entities = peaks.Select((p, i) => Entity(i + 1, p.Item1, p.Item2)).ToImmutableArray();
        var occupants = peaks.Select((p, i) => new TerrainOccupant(i + 1, TerrainOccupantKind.Mountain, p.Item2)).ToArray();
        var world = new TerrainWorld(Profile, "", new(5,8,4), 0, Enumerable.Repeat(new TerrainCell(1), 40), new(5, entities), occupants);
        Assert.Equal(6, world.FineHeight(new(3,3,0), 0)); Assert.Equal(4, world.FineHeight(new(3,3,0), 1));
        Assert.Equal(6, world.FineHeight(new(4,2,0), 2));
        Assert.Equal(0x1234, new QuarterCorners(1,2,3,4).Packed);
        Assert.Throws<ArgumentException>(() => new TerrainWorld(Profile, "", new(5,8,4), 0,
            world.Cells, new(5, entities.RemoveAt(0)), occupants.Skip(1)));
    }

    [Fact]
    public void Flat_and_saturated_mountains_keep_identity_at_original_ground()
    {
        var flat = Single(default); var saturated = Single(new(4,4,4,4));
        Assert.True(flat.Spatial.At(new(0,0,1)).Transparent);
        Assert.False(saturated.Spatial.At(new(0,0,1)).Transparent);
        Assert.Equal(8, saturated.FineHeight(new(0,0,0), 0));
        Assert.Equal(1, saturated.Cell(0,0).Ground); Assert.Equal(1, saturated.Spatial.Entities[0].Id);
        Assert.True(saturated.Occupants[0].Corners.Saturated);
        Assert.Equal(15, Single(new(3,3,3,3), 3).FineHeight(new(0,0,0), 2));
        Assert.Throws<ArgumentException>(() => Single(new(4,0,0,0), 3));
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)][InlineData(0, 5, 0, 0)][InlineData(0, 0, 256, 0)][InlineData(0,0,0,int.MaxValue)]
    public void Invalid_corners_never_narrow_or_wrap(int ne, int se, int sw, int nw) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuarterCorners(ne,se,sw,nw));

    [Fact]
    public void Terrace_retains_only_backup_offsets_and_explicit_discontinuity()
    {
        var corners = new QuarterCorners(0, 1, 2, 3);
        var entity = Entity(7, new(0,0,1), corners, TerrainOccupantKind.Terrace);
        var world = new TerrainWorld(Profile, "", new(1,1,4), 1, [new(2,TerrainSurface.Terrace)], new(8,[entity]),
            [new(7,TerrainOccupantKind.Terrace,corners)]);
        Assert.Equal(GroundRelation.Underground, world.At(entity.Anchor).Relation);
        Assert.Equal(7, world.At(entity.Anchor).Occupancy.Entity!.Id);
        Assert.Equal(8, world.FineHeight(entity.Anchor, 0));
        Assert.False(world.At(entity.Anchor).Occupancy.Reusable);
        Assert.Equal(corners, world.Read().Occupants[0].Corners);
        Assert.Throws<ArgumentException>(() => new TerrainWorld(Profile, "", new(1,1,4), 1,
            [new(2,TerrainSurface.Terrace)], new(1,[]), []));
    }

    [Fact]
    public void Explicit_cut_allows_a_local_discontinuity_but_natural_surface_does_not()
    {
        // H2/V1 maps to adjacent diagonal XYZ(0,0),(1,1).
        TerrainWorld Create(TerrainSurface surface) => new(Profile, "", new(2,1,4), 1,
            [new(1,surface),new(2)], new(1,[]), []);
        var world = Create(TerrainSurface.Cut);
        Assert.Equal(4, world.FineHeight(new(0,0,0), 0)); Assert.Equal(8, world.FineHeight(new(1,1,0), 0));
        Assert.Throws<ArgumentException>(() => Create(TerrainSurface.Natural));
    }

    [Fact]
    public void Forest_uses_exact_prepared_recipe_and_keeps_realized_order_and_owned_value()
    {
        var definition = Profile.Definitions.Single(d => d.Reference.ContributionId == "{FBD0CD06-016A-4037-AB15-02040218CBAA}");
        Assert.Equal(9, definition.TreeSpriteCount); Assert.Equal(3, definition.TreeDensity);
        var trees = ImmutableArray.Create(new TerrainTree(12,8,4),new TerrainTree(4,6,1));
        var forest = new ForestDecoration(definition.Reference, trees);
        var flat = Single(default); var entity = flat.Spatial.Entities[0] with { Owned = true, Value = 100, Revision = 1 };
        var world = flat.Replace(0, flat.Cells, 2, new(2,[entity]), [flat.Occupants[0] with { Forest = forest }]);
        Assert.Equal(1, world.Revision); Assert.Equal(trees, world.Read().Occupants[0].Forest!.Trees);
        Assert.True(world.At(entity.Anchor).Underwater); // manual eligibility has no water exclusion.
        Assert.True(world.Spatial.Entities[0].Owned); Assert.Equal(100, world.Spatial.Entities[0].Value);
        Assert.Equal(0, flat.Spatial.Entities[0].Value); Assert.Null(flat.Occupants[0].Forest);
        Assert.Throws<ArgumentException>(() => flat.Replace(0, flat.Cells, 1, new(2,[entity]),
            [flat.Occupants[0] with { Forest = forest with { Trees = [new(0,0,9)] } }]));
        Assert.Throws<ArgumentException>(() => flat.Replace(0, flat.Cells, 1, new(2,[entity]),
            [flat.Occupants[0] with { Forest = forest with { Definition = definition.Reference with { PackId = "other" } } }]));
    }

    [Fact]
    public void Creek_occupies_reusable_surface_without_changing_water_or_ground()
    {
        var entity = Entity(1, new(0,0,1), default, TerrainOccupantKind.Creek);
        var world = new TerrainWorld(Profile, "", new(1,1,4), 2, [new(1)], new(2,[entity]),
            [new(1,TerrainOccupantKind.Creek,default, CreekConnections: 10)]);
        Assert.True(world.At(entity.Anchor).Occupancy.Reusable); Assert.True(world.At(entity.Anchor).Occupancy.Transparent);
        Assert.Equal(1, world.Cell(0,0).Ground); Assert.Equal(2, world.WaterLevel);
        Assert.Equal(4, world.FineHeight(entity.Anchor, 1));
    }

    [Theory]
    [InlineData("overlap")][InlineData("outside")][InlineData("unknown-recipe")][InlineData("wrong-case")]
    [InlineData("missing-payload")][InlineData("wrong-kind")][InlineData("ground")][InlineData("transparent")]
    [InlineData("water")][InlineData("allocator")][InlineData("forest-on-terrace")][InlineData("bad-grid")]
    public void Invalid_replacements_leave_world_graph_allocator_and_reads_untouched(string mutation)
    {
        var world = Single(default); var before = world.Read();
        var e = before.Spatial.Entities[0]; var o = before.Occupants[0];
        var entities = before.Spatial.Entities; var occupants = before.Occupants; var cells = before.Cells;
        var water = 1; long next = 2;
        switch (mutation)
        {
            case "overlap": entities = entities.Add(e with { Id = 2 }); occupants = occupants.Add(o with { EntityId = 2 }); next = 3; break;
            case "outside": entities = [e with { Anchor = new(0,0,4) }]; break;
            case "unknown-recipe": entities = [e with { Definition = e.Definition with { RecipeId = "unknown" } }]; break;
            case "wrong-case": entities = [e with { Definition = e.Definition with { PackId = e.Definition.PackId.ToUpperInvariant() } }]; break;
            case "missing-payload": occupants = []; break;
            case "wrong-kind": occupants = [o with { Kind = TerrainOccupantKind.Terrace }]; break;
            case "ground": cells = [new(4)]; break;
            case "transparent": entities = [e with { Footprint = new Footprint([new(new(0,0,0),false)]) }]; break;
            case "water": water = -1; break;
            case "allocator": next = 1; break;
            case "forest-on-terrace": occupants = [o with { Kind = TerrainOccupantKind.Creek, Forest = new(Mountain,[]) }]; break;
            case "bad-grid": cells = []; break;
        }
        Assert.Throws<ArgumentException>(() => world.Replace(0, cells, water, new(next,entities), occupants));
        Assert.Equal(before, world.Read()); Assert.Equal(2, world.Spatial.NextEntityId);
        Assert.Throws<InvalidOperationException>(() => world.Replace(1, world.Cells, water, world.Spatial.Capture(), world.Occupants));
    }

    [Fact]
    public void Removed_ids_are_not_reused_and_surviving_ids_are_stable()
    {
        var world = Single(default);
        var removed = world.Replace(0, world.Cells, 1, world.Spatial.Remove(1,0).Capture(), []);
        Assert.Equal(2, removed.Spatial.NextEntityId);
        Assert.Throws<ArgumentException>(() => removed.Replace(1, world.Cells, 1, world.Spatial.Capture(), world.Occupants));
        var graph = removed.Spatial.Add(Mountain, new(0,0,1), new Footprint([new(new(0,0,0),true)]), false,false,0);
        var added = removed.Replace(1, world.Cells, 1, graph.Capture(), [new(2,TerrainOccupantKind.Mountain,default)]);
        Assert.Equal(2, added.Spatial.Entities[0].Id); Assert.Equal(3, added.Spatial.NextEntityId);
    }

    [Fact]
    public void Terrace_and_decorated_surface_can_occupy_two_distinct_levels_in_one_column()
    {
        var terrace = Entity(1, new(0,0,1), new(1,2,3,4), TerrainOccupantKind.Terrace);
        var mountain = Entity(2, new(0,0,2), default);
        var world = new TerrainWorld(Profile, "", new(1,1,4), 1, [new(2,TerrainSurface.Terrace)], new(3,[terrace,mountain]),
            [new(1,TerrainOccupantKind.Terrace,new(1,2,3,4)), new(2,TerrainOccupantKind.Mountain,default)]);
        Assert.Equal(2, world.Occupants.Length);
        Assert.Equal(1, world.At(new(0,0,1)).Occupancy.Entity!.Id);
        Assert.Equal(2, world.At(new(0,0,2)).Occupancy.Entity!.Id);
    }

    [Theory]
    [InlineData(0,1,4)][InlineData(2049,1,4)][InlineData(1,1,65)][InlineData(int.MaxValue,int.MaxValue,int.MaxValue)]
    public void Invalid_dimensions_fail_before_reading_grid(int h, int v, int z)
    {
        static IEnumerable<TerrainCell> Unexpected()
        {
            yield return new(0);
            throw new Exception("Grid must not be enumerated.");
        }
        Assert.Throws<ArgumentException>(() => new TerrainWorld(Profile, "", new(h,v,z), 0, Unexpected(), new(1,[]), []));
    }

    [Fact]
    public void Entity_and_payload_changes_require_incremented_revision()
    {
        var world = Single(default); var original = world.Spatial.Entities[0];
        Assert.Throws<ArgumentException>(() => world.Replace(0, world.Cells, 1,
            new(2,[original with { Owned = true }]), world.Occupants));
        Assert.Throws<ArgumentException>(() => world.Replace(0, world.Cells, 1,
            new(2,[original with { Revision = 2 }]), world.Occupants));
        var slope = Single(new(1,1,1,1));
        Assert.Throws<ArgumentException>(() => slope.Replace(0, slope.Cells, 1, slope.Spatial.Capture(),
            [slope.Occupants[0] with { Corners = new(2,2,2,2) }]));
        var changed = slope.Replace(0, slope.Cells, 1, new(2,[slope.Spatial.Entities[0] with { Revision = 1 }]),
            [slope.Occupants[0] with { Corners = new(2,2,2,2) }]);
        Assert.Equal(6, changed.FineHeight(new(0,0,0),0));
        Assert.Equal(5, slope.FineHeight(new(0,0,0),0));
    }

    [Theory]
    [InlineData("ruleset")][InlineData("version")][InlineData("hash")][InlineData("missing")]
    [InlineData("extra")][InlineData("duplicate")][InlineData("case")]
    public void Profile_dispatch_is_exact_and_never_falls_back_to_reference(string mutation)
    {
        var ruleset = TerrainProfile.Ruleset; var version = TerrainProfile.ContentVersion; var packs = Profile.Packs;
        switch(mutation)
        {
            case "ruleset": ruleset = ProtocolIdentity.ReferenceRuleset; break;
            case "version": version = "terrain-content-2"; break;
            case "hash": packs = [packs[0] with { CatalogHash = new('0',64) }]; break;
            case "missing": packs = []; break;
            case "extra": packs = packs.Add(new("extra","1",new('a',64))); break;
            case "duplicate": packs = packs.Add(packs[0]); break;
            case "case": packs = [packs[0] with { Id = packs[0].Id.ToUpperInvariant() }]; break;
        }
        Assert.Null(TerrainProfiles.Resolve(ruleset, version, packs));
        Assert.Same(Profile, TerrainProfiles.Resolve(TerrainProfile.Ruleset, TerrainProfile.ContentVersion, Profile.Packs));
        Assert.Equal(18, Profile.Definitions.Count(d => d.TreeSpriteCount > 0));
        Assert.All(Profile.Definitions.Where(d => d.TreeSpriteCount > 0 || d.Kind != TerrainOccupantKind.Mountain),
            d => Assert.NotNull(PreparedContentCatalog.Bundled.FindRenderRecipe(d.Reference.PackId, d.Reference.RecipeId)));
        // State support alone still cannot launch jobs through the live creation path.
        Assert.NotEmpty(WorldCreationCapabilities.Inspect(PreparedContentCatalog.Bundled).Validate(WorldCreationProfile.Empty()));
    }
}
