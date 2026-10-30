// Source: ExAV 2.1.1.12, 883cf7d/trunk/core/world/rail and bundled rail plugins.
// Original copyright Kohsuke Kawaguchi et al., LGPL-2.1-or-later.
// Booking rules pinned/reviewed in doc/web-parity-railway-contract.md.
// Geometry, endpoint selection, car protection and legality belong to each tool.
// Inputs here are its validated visited/charged cells, not an authorizing preview.
namespace FreeTrain.Online.Simulation;

public readonly record struct RailConstructionCharge(int Z, int Ground, long ReclaimedValue = 0);

public static class RailwayBooking
{
    public static long Product(long unit, long multiplier, long count = 1)
    {
        if (unit < 0 || multiplier < 0 || count < 0) throw new ArgumentException("Negative cost factor.");
        if (unit == 0 || multiplier == 0 || count == 0) return 0;
        return checked(unit * multiplier * count);
    }
    private static long Multiplier(long height) => height < 0 ? checked(-2 * height) : checked(height + 1);
    private static long HeightCost(long unit, int z, int ground) => Product(unit,Multiplier((long)z-ground));
    private static ConstructionBooking PerCell(IEnumerable<RailConstructionCharge> cells, long unit, bool reclaim)
    {
        long total = 0;
        foreach (var cell in cells)
        {
            if (cell.ReclaimedValue < 0) throw new ArgumentException("Negative reclamation value.");
            total = checked(total + HeightCost(unit,cell.Z,cell.Ground) + (reclaim ? cell.ReclaimedValue : 0));
        }
        return ConstructionBooking.Spend(total);
    }
    public static ConstructionBooking NormalBuild(IEnumerable<RailConstructionCharge> inclusiveCells) => PerCell(inclusiveCells,6_000_000,true);
    // Count includes no-op detachments of special/slope rails, excludes car cells.
    public static ConstructionBooking NormalRemoval(long eligibleRailCells, int endZ, int water) =>
        ConstructionBooking.Spend(Product(2_000_000,Math.Abs((long)endZ-water)+1,eligibleRailCells));
    public static ConstructionBooking SlopeBuild(int baseZ, int water) => ConstructionBooking.Spend(HeightCost(40_000_000,baseZ,water));
    public static ConstructionBooking SlopeRemoval(int baseZ, int water) =>
        ConstructionBooking.Spend(Product(8_000_000,Math.Abs((long)baseZ+1-water)+1));

    // Bridge and girder charge all inclusive cells, even unchanged/skipped cells.
    // Source's starting-cell underwater early return applies to both cost functions.
    public static ConstructionBooking Elevated(IEnumerable<RailConstructionCharge> inclusiveCells,
        int startZ, int startGround, int water, bool remove) =>
        startGround <= startZ && startZ <= water && startGround < water
            ? ConstructionBooking.Spend(0) : PerCell(inclusiveCells,remove ? 2_000_000 : 6_000_000,false);
    public static ConstructionBooking TunnelBuild(long newlyCreatedCells) => ConstructionBooking.Spend(Product(12_000_000,newlyCreatedCells));
    public static ConstructionBooking TunnelRemoval(long restoredCellsExcludingDestination) => ConstructionBooking.Spend(Product(4_000_000,restoredCellsExcludingDestination));
    public static ConstructionBooking GarageBuild(long inclusiveCells) => ConstructionBooking.Spend(Product(6_500_000,inclusiveCells));
    // Caller supplies [from,to) axis matches, INCLUDING matching car cells.
    // Every iteration spends the subtotal, including nonmatches and zero subtotals.
    public static ConstructionBooking GarageRemoval(IEnumerable<bool> halfOpenAxisMatches)
    {
        long subtotal = 0; var charges = new List<AccountCharge>();
        foreach (var matches in halfOpenAxisMatches)
        {
            if (matches) subtotal = checked(subtotal + 200_000);
            charges.Add(new(AccountGenre.RailService,subtotal));
        }
        return new(charges);
    }
    public static ConstructionBooking StationaryCompletion(long price) => ConstructionBooking.Spend(price);
    public static ConstructionBooking StationaryRemoval(long sizeX, long sizeY) =>
        ConstructionBooking.Spend(Product(500_000,sizeX,sizeY),AccountGenre.Subsidiaries);
    // Signals, poles, speed limits, support election/teardown and site cancellation
    // have no spend caller. No standalone pier charge or authoring exemption.
}
