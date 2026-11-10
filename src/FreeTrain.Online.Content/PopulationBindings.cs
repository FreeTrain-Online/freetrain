using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

// Data-only consumers of the already pinned catalog. A binding makes population
// queries available; it does not authorize constructing a P7 city structure.
public static class PopulationBindings
{
    public static FrozenDictionary<string, Population> Build(PreparedContentCatalog catalog) => catalog.Definitions
        .Where(d => d.Declaration.TryGetProperty("children", out _))
        .Select(d => (d.Id, Node: Children(d.Declaration).SingleOrDefault(n => Tag(n) == "population")))
        .Where(p => p.Node.ValueKind != JsonValueKind.Undefined).ToFrozenDictionary(p => p.Id, p => Parse(p.Node));

    public static Population Parse(JsonElement node)
    {
        var count = 0;
        var result = Read(node, 0);
        result.Validate();
        return result;
        Population Read(JsonElement e, int depth)
        {
            if (depth > 16 || ++count > 256 || Tag(e) != "population") throw new ArgumentException("Invalid population declaration.");
            var children = Children(e).ToArray();
            var name = children.Single(n => Tag(n) == "class").GetProperty("attributes").GetProperty("name").GetString()!;
            var kind = name.StartsWith("freetrain.contributions.population.", StringComparison.Ordinal)
                ? name["freetrain.contributions.population.".Length..]
                : name == "freetrain.world.soccerstadium.PopulationImpl" ? "StadiumPopulation" : throw new ArgumentException("Unsupported population implementation.");
            if (kind == "CombinationPopulation") return new(kind, Children: children.Where(n => Tag(n) == "population").Select(n => Read(n, depth + 1)).ToImmutableArray());
            if (kind == "StadiumPopulation") return new(kind, Base: 10, Entering: 10);
            if (kind != "ConstantPopulation" && !Population.ConcreteKinds.Contains(kind)) throw new ArgumentException("Unsupported manifest population.");
            return new(kind, int.Parse(children.Single(n => Tag(n) == "base").GetProperty("text").GetString()!, CultureInfo.InvariantCulture));
        }
    }
    private static IEnumerable<JsonElement> Children(JsonElement e) => e.GetProperty("children").EnumerateArray();
    private static string? Tag(JsonElement e) => e.GetProperty("tag").GetString();
}
