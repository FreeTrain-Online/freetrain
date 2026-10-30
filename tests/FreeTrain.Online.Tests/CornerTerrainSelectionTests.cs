using FreeTrain.Online.Simulation;
using Xunit;

public class CornerTerrainSelectionTests
{
    [Theory]
    [InlineData(7, 0, 0, 0, -1, 0)]
    [InlineData(24, 0, 0, 0, 0, 1)]
    [InlineData(8, 7, 0, 0, 0, 0)]
    [InlineData(23, 8, 0, 0, -1, 1)]
    [InlineData(16, 1, 1, 2, 0, 0)]
    [InlineData(16, 2, 1, 2, -1, 1)]
    [InlineData(16, -2, 4, 2, -1, 1)]
    public void Source_selection_uses_slope_and_inclusive_boundaries(int x, int y, int ne, int sw, int dx, int dy)
    {
        var selected = CornerTerrainSelection.Select(new(10, 20, 3), x, y, new(ne, 0, sw, 0));
        Assert.Equal(new Location(10 + dx, 20 + dy, 3), selected);
    }
}
