// ExAV 883cf7d/trunk/core/contributions/population; original authors, LGPL-2.1-or-later.
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public static class PopulationTables
{
    public static FrozenDictionary<string, ImmutableArray<int>> All { get; } = new Dictionary<string, ImmutableArray<int>>
    {
        ["AgriculturalPopulation.weekdayDistribution"] = [5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5],
        ["AgriculturalPopulation.weekendDistribution"] = [5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5],
        ["AgriculturalPopulation.weekdayEntering"] = [5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5],
        ["AgriculturalPopulation.weekendEntering"] = [5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5],
        ["BarPopulation.weekdayDistribution"] = [5, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 5, 20, 10, 0, 0, 0, 10, 15, 30, 40, 40, 50, 30],
        ["BarPopulation.sundayDistribution"] = [30, 15, 5, 5, 5, 5, 10, 0, 0, 0, 0, 5, 20, 10, 10, 10, 10, 30, 30, 25, 25, 20, 20, 10],
        ["BarPopulation.fridayDistribution"] = [5, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 5, 20, 10, 0, 0, 0, 10, 15, 40, 80, 90, 100, 90],
        ["BarPopulation.saturdayDistribution"] = [50, 20, 10, 10, 10, 10, 20, 5, 5, 0, 0, 10, 20, 10, 10, 10, 10, 40, 40, 50, 60, 80, 80, 70],
        ["BarPopulation.weekdayEntering"] = [5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 20, 10, 0, 0, 10, 15, 30, 40, 40, 30, 30, 10],
        ["BarPopulation.sundayEntering"] = [10, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 20, 10, 10, 10, 30, 30, 25, 25, 20, 20, 20, 10],
        ["BarPopulation.fridayEntering"] = [5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 20, 10, 0, 0, 10, 15, 40, 90, 100, 100, 90, 70],
        ["BarPopulation.saturdayEntering"] = [20, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 20, 10, 10, 10, 30, 50, 50, 80, 80, 70, 70, 60],
        ["BusinessInnPopulation.weekdayDistribution"] = [5, 5, 5, 5, 5, 5, 10, 40, 80, 100, 50, 30, 10, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5],
        ["BusinessInnPopulation.weekdayEntering"] = [10, 5, 5, 5, 5, 5, 10, 10, 10, 10, 10, 10, 10, 10, 10, 20, 30, 60, 90, 90, 60, 30, 20, 10],
        ["LeisureHotelPopulation.weekdayDistribution"] = [0, 0, 0, 0, 0, 0, 5, 20, 30, 50, 30, 20, 10, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0, 0],
        ["LeisureHotelPopulation.weekendDistribution"] = [0, 0, 0, 0, 0, 0, 10, 40, 60, 100, 80, 20, 10, 20, 20, 5, 5, 10, 10, 5, 5, 5, 0, 0],
        ["LeisureHotelPopulation.weekdayEntering"] = [0, 0, 0, 0, 0, 0, 5, 10, 10, 10, 10, 10, 10, 10, 10, 20, 25, 30, 40, 40, 30, 15, 10, 5],
        ["LeisureHotelPopulation.weekendEntering"] = [0, 0, 0, 0, 0, 0, 5, 10, 10, 10, 20, 20, 20, 20, 30, 50, 60, 100, 80, 80, 60, 40, 20, 10],
        ["LeisureLandPopulation.weekdayDistribution"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 10, 10, 10, 20, 20, 30, 40, 30, 30, 30, 20, 5, 0],
        ["LeisureLandPopulation.weekendDistribution"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 20, 30, 30, 60, 60, 60, 80, 100, 70, 60, 60, 15, 5],
        ["LeisureLandPopulation.weekdayEntering"] = [0, 0, 0, 0, 0, 0, 5, 10, 10, 20, 20, 20, 20, 20, 20, 20, 25, 30, 40, 30, 10, 5, 0, 0],
        ["LeisureLandPopulation.weekendEntering"] = [0, 0, 0, 0, 0, 0, 5, 20, 40, 80, 100, 80, 70, 60, 50, 40, 40, 30, 30, 30, 10, 5, 0, 0],
        ["OfficePopulation.weekdayDistribution"] = [10, 5, 5, 5, 5, 5, 10, 10, 20, 20, 20, 20, 20, 25, 25, 25, 30, 80, 100, 80, 50, 30, 20, 10],
        ["OfficePopulation.weekendDistribution"] = [10, 5, 5, 5, 5, 5, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 20, 20, 15, 15, 10, 10, 5],
        ["OfficePopulation.weekdayEntering"] = [10, 5, 5, 5, 5, 5, 20, 50, 100, 60, 40, 40, 40, 40, 40, 40, 30, 20, 20, 20, 20, 20, 20, 10],
        ["OfficePopulation.weekendEntering"] = [10, 5, 5, 5, 5, 5, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10],
        ["ResidentialPopulation.weekdayDistribution"] = [10, 5, 5, 5, 5, 5, 40, 100, 80, 80, 60, 40, 40, 30, 30, 40, 40, 30, 30, 25, 25, 20, 20, 15],
        ["ResidentialPopulation.weekendDistribution"] = [10, 5, 5, 5, 5, 5, 20, 50, 40, 40, 50, 40, 40, 30, 30, 40, 40, 30, 30, 25, 25, 20, 20, 15],
        ["ResidentialPopulation.weekdayEntering"] = [20, 10, 5, 5, 5, 5, 5, 10, 10, 10, 20, 20, 30, 30, 30, 40, 50, 60, 70, 90, 100, 80, 60, 50],
        ["ResidentialPopulation.weekendEntering"] = [20, 10, 5, 5, 5, 5, 5, 10, 10, 10, 20, 30, 40, 30, 30, 40, 50, 60, 70, 100, 80, 60, 50, 40],
        ["RestaurantPopulation.weekdayDistribution"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 20, 75, 100, 75, 30, 10, 0, 10, 50, 80, 80, 40, 10, 0],
        ["RestaurantPopulation.weekendDistribution"] = [0, 0, 0, 0, 0, 0, 0, 20, 30, 30, 40, 75, 100, 75, 30, 10, 10, 10, 60, 90, 90, 60, 20, 0],
        ["RestaurantPopulation.weekdayEntering"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 20, 75, 100, 75, 30, 10, 0, 20, 50, 80, 60, 20, 5, 0],
        ["RestaurantPopulation.weekendEntering"] = [0, 0, 0, 0, 0, 0, 0, 20, 20, 10, 20, 80, 100, 80, 30, 10, 5, 20, 50, 80, 60, 20, 5, 0],
        ["SchoolPopulation.weekdayDistribution"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 20, 20, 10, 30, 60, 100, 70, 40, 20, 0, 0, 0],
        ["SchoolPopulation.weekendDistribution"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 10, 5, 5, 5, 10, 10, 5, 0, 0, 0, 0, 0],
        ["SchoolPopulation.weekdayEntering"] = [0, 0, 0, 0, 0, 0, 20, 60, 100, 20, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 0, 0, 0],
        ["SchoolPopulation.weekendEntering"] = [0, 0, 0, 0, 0, 0, 0, 5, 10, 20, 10, 5, 5, 5, 5, 5, 5, 0, 0, 0, 0, 0, 0, 0],
        ["ShopperPopulation.weekdayDistribution"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 20, 75, 45, 25, 15, 40, 70, 100, 45, 20, 10, 5, 0, 0],
        ["ShopperPopulation.weekendDistribution"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 20, 75, 45, 55, 75, 75, 70, 70, 70, 55, 40, 20, 5, 0],
        ["ShopperPopulation.weekdayEntering"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 30, 50, 40, 30, 30, 40, 50, 100, 50, 10, 10, 5, 0, 0],
        ["ShopperPopulation.weekendEntering"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 40, 60, 50, 50, 40, 50, 60, 90, 60, 20, 10, 5, 0, 0],
    }.ToFrozenDictionary();
}
