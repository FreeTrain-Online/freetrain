using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Simulation;

var cold = Stopwatch.StartNew();
var catalog = PreparedContentCatalog.Bundled;
var stock = args.Length == 0 ? RollingStockCatalog.Bundled : OptionalPluginRegistry.Load(args).RollingStock;
cold.Stop();
if (!stock.Diagnostics.IsEmpty) throw new InvalidDataException(JsonSerializer.Serialize(stock.Diagnostics));
var limits = new { formationCars = StockAdmission.MaximumFormationCars, quantity = StockAdmission.MaximumQuantity,
    requestCars = StockAdmission.MaximumRequestCars, fleetTrains = StockAdmission.MaximumFleetTrains, fleetCars = StockAdmission.MaximumFleetCars };
var fleetTypes = stock.Trains.Where(t => t.PermittedLengths.Contains(8)).ToArray();
StockFormation[] Fleet()
{
    var rows = new StockFormation[StockAdmission.MaximumFleetTrains];
    var cars = 0;
    for (var i = 0; i < rows.Length; i++)
    {
        var train = fleetTypes[i % fleetTypes.Length];
        train.Quote(8, 1, i, cars); rows[i] = train.Create(8); cars += 8;
    }
    if (cars != StockAdmission.MaximumFleetCars) throw new InvalidDataException("Invalid fleet admission scene.");
    return rows;
}
StockFormation[] Request(int length, int quantity)
{
    var train = stock.Trains[0]; var quote = train.Quote(length, quantity);
    if (quote.CarCount != length * quantity) throw new InvalidDataException("Invalid request admission scene.");
    return Enumerable.Range(0, quote.Quantity).Select(_ => train.Create(length)).ToArray();
}
Measurement Measure(Func<object> action, int samples)
{
    for (var i = 0; i < 20; i++) GC.KeepAlive(action());
    var durations = new double[samples];
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < samples; i++)
    {
        var start = Stopwatch.GetTimestamp(); var result = action();
        durations[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        GC.KeepAlive(result);
    }
    var bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / samples;
    Array.Sort(durations);
    return new(samples, durations[samples / 2], durations[(int)(samples * .95)], durations[^1], bytes);
}
var registration = Measure(() => RollingStockCatalog.Build(catalog), 200);
var request = Measure(() => Request(10, 100), 1000);
var maximumRequest = Measure(() => Request(16, 64), 1000);
var fleet = Measure(Fleet, 1000);
var passed = cold.Elapsed.TotalMilliseconds < 1000 && registration.P95Ms < 100 &&
    registration.AllocatedBytesPerOperation < 2 * 1024 * 1024 &&
    new[] { request, maximumRequest, fleet }.All(m => m.P95Ms < 10 && m.AllocatedBytesPerOperation < 256 * 1024);
Console.WriteLine(JsonSerializer.Serialize(new
{
    slice = "P5-03", scope = "registration-and-detached-formation-admission; moving-fleet-capacity-remains-P5-28",
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount,
    catalogHash = catalog.CatalogHash, packs = stock.Packs, coldCatalogAndRegistrationMs = cold.Elapsed.TotalMilliseconds,
    limits, admissionBudgetPassed = passed, registration, request100Trains1000Cars = request,
    maximumRequest64Trains1024Cars = maximumRequest, detachedFleet128Trains1024Cars = fleet,
    trains = stock.Trains.Length, cars = stock.Cars.Length, templates = stock.Templates.Length, stock.Diagnostics,
    bindings = stock.Trains.Select(t => new { t.Binding, t.Text, t.PricePerCar, t.MinutesPerVoxel, t.Fare,
        t.Amenity, t.TripRange, t.IsParameterized, t.DeclaredLengths, t.PermittedLengths,
        example = t.Create(t.PermittedLengths.Contains(5) ? 5 : t.PermittedLengths[^1]) }),
    carBindings = stock.Cars, templateBindings = stock.Templates
}, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
return passed ? 0 : 1;

record Measurement(int Samples, double MedianMs, double P95Ms, double MaximumMs, long AllocatedBytesPerOperation);
