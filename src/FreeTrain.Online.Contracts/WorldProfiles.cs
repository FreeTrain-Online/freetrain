using System.Collections.Immutable;

namespace FreeTrain.Online.Contracts;

// Exact profile dispatch. No version ranges, hash fallback, pack overrides or
// caller-supplied registration. P2 extends this registry with reviewed prepared packs.
public sealed record WorldProfile(string Ruleset, string ContentVersion, string CoordinateSystem,
    ImmutableArray<RequiredPackRead> Packs)
{
    public bool Matches(string ruleset, string contentVersion, IEnumerable<RequiredPackRead> packs)
    {
        var actual = packs.ToArray();
        return Ruleset == ruleset && ContentVersion == contentVersion && actual.Length == Packs.Length &&
            actual.All(p => p != null) && actual.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() == actual.Length &&
            Packs.All(p => actual.Contains(p));
    }
    public bool Contains(DefinitionRead definition) => definition != null &&
        Ruleset == ProtocolIdentity.ReferenceRuleset && definition.PackId == "reference-spatial" &&
        definition.ContributionId == "normal-rail" && Enumerable.Range(0, 12).Any(p => definition.RecipeId == $"normal{p}");
}
public static class WorldProfiles
{
    public static WorldProfile Reference { get; } = new(ProtocolIdentity.ReferenceRuleset, ProtocolIdentity.ReferenceContent, "hv", ProtocolIdentity.ReferencePacks.ToImmutableArray());
    public static WorldProfile Prototype { get; } = new(ProtocolIdentity.PrototypeRuleset, ProtocolIdentity.PrototypeContent, "prototypeXY", ProtocolIdentity.PrototypePacks.ToImmutableArray());
    public static WorldProfile? Resolve(string ruleset, string contentVersion, IEnumerable<RequiredPackRead> packs) =>
        new[] { Reference, Prototype }.SingleOrDefault(p => p.Matches(ruleset, contentVersion, packs));
}
