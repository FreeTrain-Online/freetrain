using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Registration/formation admission. Fleet.cs owns identities and occupancy;
// a P4 trusted CarComponent is never a stock definition.
public static class StockAdmission
{
    // Source purchase controls: length minimum 1, implicit Maximum 100;
    // quantity implicit Maximum 100. Fleet/request budgets are portable limits,
    // measured at registration scope; moving-fleet acceptance remains P5-28.
    public const int MaximumFormationCars = 100;
    public const int MaximumQuantity = 100;
    public const int MaximumRequestCars = 1024;
    public const int MaximumFleetTrains = 128;
    public const int MaximumFleetCars = 1024;

    public static void Validate(int length, int quantity, int ownedTrains = 0, int ownedCars = 0)
    {
        if (length is < 1 or > MaximumFormationCars || quantity is < 1 or > MaximumQuantity ||
            ownedTrains is < 0 or > MaximumFleetTrains || ownedCars is < 0 or > MaximumFleetCars ||
            (long)length * quantity > MaximumRequestCars ||
            (long)ownedTrains + quantity > MaximumFleetTrains ||
            (long)ownedCars + (long)length * quantity > MaximumFleetCars)
            throw new ArgumentException("Rolling-stock admission limit exceeded.");
    }
}

public enum StockCarHandler { Symmetric, Asymmetric, Reverse, Colored, ColorTest }
public sealed record StockColor(byte Red, byte Green, byte Blue);
public sealed record StockCar(DefinitionReference Reference, StockCarHandler Handler,
    int NormalCapacity, int SeatedCapacity, DefinitionReference? BaseCar = null,
    DefinitionReference? ColorTemplate = null, ImmutableArray<StockColor> Colors = default);
public sealed record StockText(string Company, string Type, string Name, string Author, string Description);
public sealed record StockFormation(ContentBinding Train, ImmutableArray<StockCar> Cars,
    int Price, int NormalCapacity, int SeatedCapacity, int PackedCapacity);
public sealed record StockQuote(StockFormation Formation, int Quantity, int CarCount, long Price);

public sealed class StockTrain
{
    public ContentBinding Binding { get; }
    public StockText Text { get; }
    public int PricePerCar { get; }
    public int MinutesPerVoxel { get; }
    public int Fare { get; }
    public int Amenity { get; }
    public int TripRange { get; }
    public bool IsParameterized => body != null;
    public ImmutableArray<int> DeclaredLengths { get; }
    public ImmutableArray<int> PermittedLengths { get; }
    private readonly StockCar? head, body, tail;
    private readonly FrozenDictionary<int, ImmutableArray<StockCar>> patterns;

    public StockTrain(ContentBinding binding, StockText text, int pricePerCar, int minutesPerVoxel,
        int fare, int amenity, int tripRange, StockCar? head, StockCar? body, StockCar? tail,
        IEnumerable<ImmutableArray<StockCar>>? patterns = null)
    {
        if (pricePerCar < 0 || minutesPerVoxel <= 0 || fare < 0 || tripRange is < 0 or > 2)
            throw new ArgumentException("Invalid stock numeric fields.");
        Binding = binding; Text = text; PricePerCar = pricePerCar; MinutesPerVoxel = minutesPerVoxel;
        Fare = fare; Amenity = Math.Clamp(amenity, 50, 200); TripRange = tripRange;
        this.head = head; this.body = body; this.tail = tail;
        var rows = (patterns ?? []).Take(StockAdmission.MaximumFormationCars + 1).ToArray();
        if (rows.Length > StockAdmission.MaximumFormationCars ||
            rows.Any(p => p.IsDefaultOrEmpty || p.Length > StockAdmission.MaximumFormationCars || p.Any(c => c == null)) ||
            rows.Select(p => p.Length).Distinct().Count() != rows.Length ||
            (body == null ? rows.Length == 0 || head != null || tail != null : rows.Length != 0))
            throw new ArgumentException("Invalid stock compositions.");
        this.patterns = rows.ToFrozenDictionary(p => p.Length);
        DeclaredLengths = rows.Select(p => p.Length).Order().ToImmutableArray();
        // Reject overflow before a formation can enter the fleet. Preserve the
        // original Int32 width of price(length) and passenger aggregate methods.
        var lengths = ImmutableArray.CreateBuilder<int>();
        foreach (var length in body != null ? Enumerable.Range(1, StockAdmission.MaximumFormationCars) : DeclaredLengths)
            try { Create(length); lengths.Add(length); }
            catch (OverflowException) { }
        PermittedLengths = lengths.ToImmutable();
        if (PermittedLengths.IsEmpty) throw new ArgumentException("No numerically safe stock composition.");
    }

    public StockFormation Create(int length)
    {
        if (length is < 1 or > StockAdmission.MaximumFormationCars)
            throw new ArgumentException("Unsupported formation length.");
        ImmutableArray<StockCar> cars;
        if (body != null)
        {
            var result = ImmutableArray.CreateBuilder<StockCar>(length);
            for (var i = 0; i < length; i++) result.Add(body);
            if (head != null) result[0] = head;
            if (tail != null) result[length - 1] = tail; // source: tail wins at length one
            cars = result.MoveToImmutable();
        }
        else if (!patterns.TryGetValue(length, out cars)) throw new ArgumentException("Undeclared formation length.");
        var normal = 0; var seated = 0;
        checked
        {
            foreach (var car in cars)
            {
                if (car.NormalCapacity < 0) throw new ArgumentException("Negative car capacity.");
                normal += car.NormalCapacity;
                seated += car.SeatedCapacity >= 0 && car.SeatedCapacity <= car.NormalCapacity
                    ? car.SeatedCapacity : car.NormalCapacity;
            }
            var packedSeated = Math.Clamp(seated, 0, normal);
            return new(Binding, cars, PricePerCar * length, normal, seated,
                packedSeated + (normal - packedSeated) * 2);
        }
    }

    public StockQuote Quote(int length, int quantity, int ownedTrains = 0, int ownedCars = 0)
    {
        StockAdmission.Validate(length, quantity, ownedTrains, ownedCars);
        var formation = Create(length);
        return new(formation, quantity, checked(length * quantity), checked((long)formation.Price * quantity));
    }

    // Pure valuation only, with no inventory/account mutation. Nullable purchase
    // time is intentional source state. P5-06 owns sale/withdrawal/booking.
    public long ResaleValue(int length, long? purchaseMinute, long nowMinute)
    {
        var formation = Create(length);
        if (nowMinute < 0 || nowMinute > GameTime.MaximumMinute || purchaseMinute is {} minute &&
            (minute < 0 || minute > nowMinute)) throw new ArgumentException("Invalid stock purchase time.");
        if (purchaseMinute == null) return 0;
        var years = (nowMinute - purchaseMinute.Value) / (365L * 24 * 60);
        return formation.Price / (years + 2);
    }
}
