namespace FreeTrain.Online.Simulation.Prototype;

// Compatibility boundary for the obsolete four-direction model.
// These representation adapters do not migrate rails or prototype worlds.
public static class DirectionAdapter
{
    public static Heading ToHeading(this Direction direction) => direction switch
    {
        Direction.North => Heading.North, Direction.East => Heading.East,
        Direction.South => Heading.South, Direction.West => Heading.West,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    public static Direction ToPrototypeDirection(this Heading heading) => heading.Index switch
    {
        0 => Direction.North, 2 => Direction.East, 4 => Direction.South, 6 => Direction.West,
        _ => throw new InvalidOperationException("A diagonal heading has no prototype direction.")
    };
}
