// Portable population queries from stable ExAV 883cf7d. Original authors,
// Kohsuke Kawaguchi et al., LGPL-2.1-or-later. No legacy object activation.
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

// Immutable expression; Persistent retains its child binding rather than cloning
// a legacy resolver. Stadium carries controlled outgoing/entering values until P7.
public sealed record Population(string Kind, int Base = 0, int Factor = 1, int Entering = 0,
    ImmutableArray<Population> Children = default, ImmutableArray<ImmutableArray<int>> Tables = default)
{
    public static readonly ImmutableArray<string> ConcreteKinds = ["AgriculturalPopulation", "BarPopulation", "BusinessInnPopulation",
        "LeisureHotelPopulation", "LeisureLandPopulation", "OfficePopulation", "ResidentialPopulation", "RestaurantPopulation", "SchoolPopulation", "ShopperPopulation"];

    public void Validate()
    {
        var count = 0;
        Visit(this, 0);
        void Visit(Population p, int depth)
        {
            if (p == null || depth > 16 || ++count > 256 || p.Base < 0 || p.Factor < 0 || p.Entering < 0)
                throw new ArgumentException("Invalid population expression.");
            var n = p.Children.IsDefault ? 0 : p.Children.Length;
            var t = p.Tables.IsDefault ? 0 : p.Tables.Length;
            var valid = p.Kind switch {
                "CombinationPopulation" => t == 0,
                "MultiplierPopulation" or "PersistentPopulation" => n == 1 && t == 0,
                "HourlyPopulation" => n == 0 && t == 4,
                "HourlyWeeklyPopulation" => n == 0 && t == 14,
                "ConstantPopulation" or "StadiumPopulation" => n == 0 && t == 0,
                _ => ConcreteKinds.Contains(p.Kind) && n == 0 && t == 0
            };
            if (!valid || (!p.Tables.IsDefault && p.Tables.Any(a => a.IsDefault || a.Length != 24 || a.Any(v => v < 0))))
                throw new ArgumentException("Invalid population kind/tables.");
            foreach (var child in p.Children.IsDefault ? [] : p.Children) Visit(child, depth + 1);
        }
    }
    public int Residents => Kind switch {
        "SchoolPopulation" or "StadiumPopulation" => 1,
        "LeisureHotelPopulation" or "LeisureLandPopulation" => 10,
        "CombinationPopulation" => Sum(p => p.Residents),
        "MultiplierPopulation" => checked(Children[0].Residents * Factor),
        "PersistentPopulation" => Children[0].Residents,
        _ => Base
    };
    public int Outgoing(GameTime time) => Query(time, false);
    public int Incoming(GameTime time) => Query(time, true);
    private int Sum(Func<Population, int> query)
    {
        var sum = 0;
        foreach (var child in Children.IsDefault ? [] : Children) sum = checked(sum + query(child));
        return sum;
    }
    private int Query(GameTime time, bool entering)
    {
        switch (Kind)
        {
            case "ConstantPopulation": return Base;
            case "StadiumPopulation": return entering ? Entering : Base;
            case "CombinationPopulation": return Sum(p => p.Query(time, entering));
            case "MultiplierPopulation": return checked(Children[0].Query(time, entering) * Factor);
            case "PersistentPopulation": return Children[0].Query(time, entering);
        }
        var weekend = time.IsWeekend || time.IsHoliday;
        var day = time.IsHoliday ? 0 : time.DayOfWeek;
        if (Kind == "HourlyPopulation") return Scale(Base, Tables[(entering ? 2 : 0) + (weekend ? 1 : 0)][time.HourOfDay]);
        if (Kind == "HourlyWeeklyPopulation") return Scale(Base, Tables[(entering ? 7 : 0) + day][time.HourOfDay]);
        if (!ConcreteKinds.Contains(Kind)) throw new ArgumentException("Unknown population kind.");
        var prefix = Kind switch {
            "BarPopulation" => day switch { 0 => "sunday", 5 => "friday", 6 => "saturday", _ => "weekday" },
            "BusinessInnPopulation" => "weekday",
            "SchoolPopulation" => weekend || time.IsVacation ? "weekend" : "weekday",
            "LeisureLandPopulation" or "LeisureHotelPopulation" => weekend || time.IsVacation ? "weekend" : "weekday",
            _ => weekend ? "weekend" : "weekday"
        };
        var divisor = Kind is "LeisureLandPopulation" or "LeisureHotelPopulation" && !weekend && time.IsVacation ? 125 : 100;
        return Scale(Base, PopulationTables.All[$"{Kind}.{prefix}{(entering ? "Entering" : "Distribution")}"][time.HourOfDay], divisor);
    }
    public static int Scale(int population, int percent, int divisor = 100) => checked(population * percent) / divisor;
}

// Location and identity always come from the actual spatial owner. Land value,
// XY/height and special supply are explicit producer inputs, never station totals.
public sealed record StationProvider(Population Population, int LandValue, int XYSize = 1, int ZSize = 1,
    bool Special = false, string Input = "controlled")
{
    public void Validate()
    {
        if (Population == null || LandValue < 0 || XYSize < 1 || ZSize < 1 || Input is not ("controlled" or "city-fixed" or "city-variable" or "city-half" or "city-ground"))
            throw new ArgumentException("Invalid station provider.");
        Population.Validate();
    }
    public int Share(GameTime time, int stations) => Contribution(Population.Outgoing(time), stations, LandValue, XYSize, ZSize, Special);
    public static int Contribution(int raw, int stations, int land, int xy, int z, bool special = false)
    {
        if (raw < 0 || stations < 1 || land < 0 || xy < 1 || z < 1) throw new ArgumentException("Invalid provider query.");
        var share = raw / stations;
        return special ? share : Math.Min(share, checked(land * xy * Math.Max(1, z / 4) + 10));
    }
}
