// Stable ExAV 883cf7d PlatformController, Platform and PThreeDimStructure callers.
// Original authors, LGPL-2.1-or-later. Geometry legality belongs to each tool.
namespace FreeTrain.Online.Simulation;

public static class StationBooking
{
    public static ConstructionBooking BuildingPlacement(StationDefinition definition) => ConstructionBooking.Spend(definition.Price);
    public static ConstructionBooking BuildingRemoval(int sizeX, int sizeY)
    {
        if (sizeX < 1 || sizeY < 1) throw new ArgumentException("Invalid station footprint.");
        return ConstructionBooking.Spend(checked(500_000 * sizeX * sizeY),AccountGenre.Subsidiaries);
    }
    public static ConstructionBooking BuildingDaily(StationDefinition definition) => ConstructionBooking.Spend(definition.OperationCost);
    // Only the BASE ground enters the source quote, including its water early return.
    // Products remain Int32 before conversion to the account's Int64 amount.
    public static ConstructionBooking PlatformPlacement(int length, int z, int baseGround, int water)
    {
        if (length < 1) throw new ArgumentException("Invalid platform length.");
        if (baseGround <= z && z <= water && baseGround < water) return ConstructionBooking.Spend(0);
        var height = checked(z-baseGround);
        var multiplier = height < 0 ? checked(height * -2) : checked(height+1);
        return ConstructionBooking.Spend(checked(500_000 * length * multiplier));
    }
    public static ConstructionBooking PlatformDaily(int length)
    {
        if (length < 1) throw new ArgumentException("Invalid platform length.");
        return ConstructionBooking.Spend(checked(180_000 * length));
    }
    public static ConstructionBooking PlatformRemoval => ConstructionBooking.None;
    public static ConstructionBooking LaneChange => ConstructionBooking.None;
    public static ConstructionBooking PassageChange => ConstructionBooking.None;
    public static ConstructionBooking StairChange => ConstructionBooking.None;
}
