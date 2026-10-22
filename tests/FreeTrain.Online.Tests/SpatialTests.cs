using FreeTrain.Online.Simulation;
using FreeTrain.Online.Simulation.Prototype;
using Xunit;

// Independent vectors from the pinned source and doc/web-parity-geometry.md.
// These exercise canonical primitives; prototype/session fixtures remain separate.
public class SpatialTests
{
    [Theory]
    [InlineData(0, 0, -1, 7, 1, 6, 2, 4)]
    [InlineData(1, 1, -1, 0, 2, 7, 3, 5)]
    [InlineData(2, 1, 0, 1, 3, 0, 4, 6)]
    [InlineData(3, 1, 1, 2, 4, 1, 5, 7)]
    [InlineData(4, 0, 1, 3, 5, 2, 6, 0)]
    [InlineData(5, -1, 1, 4, 6, 3, 7, 1)]
    [InlineData(6, -1, 0, 5, 7, 4, 0, 2)]
    [InlineData(7, -1, -1, 6, 0, 5, 1, 3)]
    public void All_eight_source_vectors_and_rotations(int index, int dx, int dy,
        int left, int right, int left90, int right90, int opposite)
    {
        var heading = new Heading(index);
        Assert.Equal((dx, dy), heading.Offset);
        Assert.Equal(heading, Heading.FromOffset(dx, dy));
        Assert.Equal(left, heading.Left.Index);
        Assert.Equal(right, heading.Right.Index);
        Assert.Equal(left90, heading.Left90.Index);
        Assert.Equal(right90, heading.Right90.Index);
        Assert.Equal(opposite, heading.Opposite.Index);
        Assert.Equal(new Location(10 + dx, 20 + dy, 3), new Location(10, 20, 3).Move(heading));
    }

    [Fact]
    public void Source_angles_and_axis_classification()
    {
        int[][] angles = [
            [0, 1, 2, 3, 4, 3, 2, 1], [1, 0, 1, 2, 3, 4, 3, 2],
            [2, 1, 0, 1, 2, 3, 4, 3], [3, 2, 1, 0, 1, 2, 3, 4],
            [4, 3, 2, 1, 0, 1, 2, 3], [3, 4, 3, 2, 1, 0, 1, 2],
            [2, 3, 4, 3, 2, 1, 0, 1], [1, 2, 3, 4, 3, 2, 1, 0]
        ];
        for (var a = 0; a < 8; a++)
        {
            var heading = new Heading(a);
            Assert.Equal(new[] { 0, 2, 4, 6 }.Contains(a), heading.IsCardinal);
            Assert.Equal(new[] { 2, 6 }.Contains(a), heading.IsParallelToX);
            Assert.Equal(new[] { 0, 4 }.Contains(a), heading.IsParallelToY);
            for (var b = 0; b < 8; b++) Assert.Equal(angles[a][b], heading.AngleTo(new(b)));
        }
    }

    [Theory]
    [InlineData(Direction.North, 0)]
    [InlineData(Direction.East, 2)]
    [InlineData(Direction.South, 4)]
    [InlineData(Direction.West, 6)]
    public void Prototype_conversion_uses_explicit_meanings(Direction prototype, int index)
    {
        Assert.Equal(index, prototype.ToHeading().Index);
        Assert.Equal(prototype, new Heading(index).ToPrototypeDirection());
    }

    [Fact]
    public void Invalid_directions_are_rejected_instead_of_wrapped_or_rounded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Heading(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Heading(8));
        Assert.Throws<ArgumentOutOfRangeException>(() => Heading.FromOffset(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Heading.FromOffset(2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Direction)4).ToHeading());
        foreach (var diagonal in new[] { 1, 3, 5, 7 })
            Assert.Throws<InvalidOperationException>(() => new Heading(diagonal).ToPrototypeDirection());
    }

    [Fact]
    public void Location_distance_steps_and_inclusive_volume_use_source_rules()
    {
        var origin = new Location(0, 0, 0);
        Assert.Equal(5, origin.DistanceTo(new(3, 4, 0)));
        Assert.Equal(3, origin.DistanceTo(new(0, 0, 3)));
        Assert.Equal(1, origin.DistanceTo(new(1, 1, 0)));
        Assert.Equal(new Location(1, -1, 0), origin.Toward(new(10, -3, 0)));
        Assert.Equal(Heading.SouthWest, new Location(10, -3, 0).DirectionTo(origin));
        Assert.True(new Location(2, 3, 4).IsBetween(new(5, 6, 7), new(2, 3, 4)));
        Assert.False(new Location(2, 3, 8).IsBetween(new(5, 6, 7), new(2, 3, 4)));
        Assert.Throws<ArgumentException>(() => origin.DirectionTo(origin));
        Assert.Throws<ArgumentException>(() => origin.DirectionTo(new(1, 0, 1)));
    }

    [Theory]
    [InlineData(3, 3, 3, 0, 3, 3)]
    [InlineData(2, 5, 0, 5, 2, 2)] // 5/2 == 2: diagonal, not vertical.
    [InlineData(2, 6, 0, 6, 0, 6)]
    [InlineData(5, 2, 5, 0, 5, 5)]
    [InlineData(6, 2, 6, 0, 6, 0)]
    [InlineData(-2, -5, 0, -5, -2, -2)]
    public void Snapping_preserves_ties_and_integer_half_thresholds(int x, int y,
        int fourX, int fourY, int eightX, int eightY)
    {
        var anchor = new Location(10, 20, 3);
        var point = new Location(10 + x, 20 + y, 3);
        Assert.Equal(new Location(10 + fourX, 20 + fourY, 3), point.Align4To(anchor));
        Assert.Equal(new Location(10 + eightX, 20 + eightY, 3), point.Align8To(anchor));
    }

    [Theory]
    [InlineData(3, 0, 0, 0)]
    [InlineData(3, 1, 0, 1)]
    [InlineData(2, 1, 0, 2)]
    [InlineData(0, 4, 0, 7)]
    [InlineData(6, 3, 3, 0)]
    [InlineData(3, 7, 3, 7)]
    public void Storage_is_HV_rather_than_an_XY_rectangle(int x, int y, int h, int v)
    {
        var space = new WorldSpace(4, 8, 3);
        var location = new Location(x, y, 2);
        Assert.Equal(new StoragePosition(h, v, 2), space.ToStorage(location));
        Assert.Equal(location, space.ToLocation(new(h, v, 2)));
        Assert.True(space.ContainsVoxel(location));
    }

    [Fact]
    public void Negative_odd_sums_use_signed_shift_and_negative_rows_retain_source_division()
    {
        var space = new WorldSpace(4, 8, 3);
        Assert.Equal(new StoragePosition(-2, 3, 0), space.ToStorage(new(0, 0, 0)));
        Assert.Equal(new Location(3, 0, 0), space.ToLocation(new(0, -1, 0)));
        // The source inverse is only a round trip for nonnegative storage rows.
        Assert.Equal(new StoragePosition(0, 0, 0), space.ToStorage(space.ToLocation(new(0, -1, 0))));
    }

    [Fact]
    public void Horizontal_source_bounds_and_voxel_bounds_have_distinct_meanings()
    {
        var space = new WorldSpace(4, 8, 3);
        Assert.True(space.ContainsHorizontal(new(3, 0, -1)));
        Assert.True(space.ContainsHorizontal(new(3, 0, 3)));
        Assert.False(space.ContainsVoxel(new(3, 0, -1)));
        Assert.False(space.ContainsVoxel(new(3, 0, 3)));
        Assert.False(space.ContainsHorizontal(new(0, 0, 0)));
        Assert.False(space.ContainsHorizontal(space.ToLocation(new(4, 0, 0))));
        Assert.False(space.ContainsHorizontal(space.ToLocation(new(0, 8, 0))));
        var otherWorld = new WorldSpace(4, 9, 3);
        Assert.Equal(new Location(4, 0, 0), otherWorld.ToLocation(new(0, 0, 0)));
        Assert.Equal(new Location(3, 0, 0), space.ToLocation(new(0, 0, 0)));
    }

    [Fact]
    public void Source_projection_has_independent_X_Y_Z_vectors_and_per_world_origin()
    {
        var space = new WorldSpace(4, 8, 3);
        Assert.Equal(new ProjectedPoint(0, 0), space.Project(new(3, 0, 0)));
        Assert.Equal(new ProjectedPoint(16, -8), space.Project(new(4, 0, 0)));
        Assert.Equal(new ProjectedPoint(16, 8), space.Project(new(3, 1, 0)));
        Assert.Equal(new ProjectedPoint(0, -16), space.Project(new(3, 0, 1)));
        Assert.Equal(new ProjectedPoint(48, -8), space.Project(new(4, 2, 1)));
        Assert.Equal(new ProjectedPoint(-16, 8), new WorldSpace(4, 9, 3).Project(new(3, 0, 0)));
    }

    [Fact]
    public void Unsupported_dimensions_and_Int32_overflow_fail_explicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldSpace(0, 8, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldSpace(4, -1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldSpace(4, 8, 0));
        Assert.Throws<OverflowException>(() => new Location(int.MaxValue, 0, 0).Move(Heading.East));
        Assert.Throws<OverflowException>(() => new Location(0, 0, 0).DistanceTo(new(46341, 0, 0)));
        Assert.Throws<OverflowException>(() => new WorldSpace(4, 8, 3).Project(new(int.MaxValue, 0, 1)));
        Assert.Throws<OverflowException>(() => new WorldSpace(4, 8, 3).ToStorage(new(int.MinValue, 0, 0)));
    }
}
