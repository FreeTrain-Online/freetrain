// Stable ExAV 883cf7d station contributions, Thin/FatPlatform and PassagewayRail.
// State bindings only; construction, rendering and callback dispatch have later owners.
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public sealed record StationDefinition(DefinitionReference Reference, string Name, int SizeX, int SizeY,
    int Height, long Price, int OperationCost)
{
    public Cube Bounds(Location anchor) => new(anchor, SizeX, SizeY, Height);
    public Footprint Footprint => Footprint.Box(new(new(0,0,0),SizeX,SizeY,Height));
    public string HeightCutColor => "Gray";
}
public enum StationArtKind { Thin, WideCenter, Yard, Passage, Stair }
public enum PassageMaterial { Concrete, Brown, Green }
public sealed record StationArtFrame(string PictureId, int X, int Y, int Width, int Height,
    int AnchorX, int AnchorY, bool InvalidSourceCrop);
public sealed record StationArt(DefinitionReference Reference, StationArtKind Kind, int Index,
    PassageMaterial? Material, ImmutableArray<StationArtFrame> Frames)
{
    public bool Ready => Frames.All(f => !f.InvalidSourceCrop);
}
public sealed record StationBell(DefinitionReference Reference, string Name, string SourcePath, string Sha256, string MediaPath);

public sealed class StationProfile
{
    public ImmutableArray<TerrainPack> Packs { get; }
    public ImmutableArray<StationDefinition> Definitions { get; }
    public ImmutableArray<StationArt> Art { get; }
    public ImmutableArray<StationBell> Bells { get; }
    private readonly FrozenDictionary<DefinitionReference, StationDefinition> definitions;
    private readonly FrozenDictionary<DefinitionReference, StationBell> bells;
    private readonly FrozenDictionary<(StationArtKind, int, PassageMaterial?), StationArt> art;
    private readonly FrozenSet<DefinitionReference> platformArt;
    public StationProfile(IEnumerable<TerrainPack> packs, IEnumerable<StationDefinition> definitions,
        IEnumerable<StationArt> art, IEnumerable<StationBell> bells)
    {
        Packs = packs.ToImmutableArray(); Definitions = definitions.ToImmutableArray();
        Art = art.ToImmutableArray(); Bells = bells.ToImmutableArray();
        if (Packs.IsEmpty || Packs.Select(p => p.Id).Distinct().Count() != Packs.Length ||
            Definitions.Any(d => d == null || string.IsNullOrWhiteSpace(d.Name) || d.SizeX is < 1 or > 32 ||
                d.SizeY is < 1 or > 32 || d.Height is < 1 or > 32 || d.Price < 0 || d.OperationCost < 0) ||
            Art.Any(a => a == null || !Enum.IsDefined(a.Kind) || a.Frames.IsDefaultOrEmpty ||
                a.Material is {} m && !Enum.IsDefined(m) ||
                (a.Kind is StationArtKind.Passage or StationArtKind.Stair) != a.Material.HasValue ||
                a.Index < 0 || a.Index >= (a.Kind switch { StationArtKind.Thin => 8, StationArtKind.WideCenter => 2,
                    StationArtKind.Yard => 1, StationArtKind.Passage => 14, _ => 32 }) ||
                a.Frames.Any(f => f == null || string.IsNullOrWhiteSpace(f.PictureId) || f.Width < 1 || f.Height < 1 || f.X < 0 || f.Y < 0)) ||
            Bells.Any(b => b == null || string.IsNullOrWhiteSpace(b.Name) || string.IsNullOrWhiteSpace(b.SourcePath) ||
                string.IsNullOrWhiteSpace(b.MediaPath) || b.Sha256 is not { Length: 64 } || b.Sha256.Any(c => !char.IsAsciiHexDigit(c))))
            throw new ArgumentException("Invalid station content bindings.");
        var refs = Definitions.Select(d => d.Reference).Concat(Art.Select(a => a.Reference)).Concat(Bells.Select(b => b.Reference)).ToArray();
        if (refs.Any(r => r == null || !Packs.Any(p => p.Id == r.PackId)) || refs.Distinct().Count() != refs.Length ||
            Art.Select(a => (a.Kind,a.Index,a.Material)).Distinct().Count() != Art.Length)
            throw new ArgumentException("Duplicate/unavailable station content identity.");
        foreach (var r in refs) r.Validate();
        this.definitions = Definitions.ToFrozenDictionary(d => d.Reference);
        this.bells = Bells.ToFrozenDictionary(b => b.Reference);
        this.art = Art.ToFrozenDictionary(a => (a.Kind,a.Index,a.Material));
        platformArt = Art.Where(a => a.Kind is StationArtKind.Thin or StationArtKind.Yard).Select(a => a.Reference).ToFrozenSet();
    }
    public StationDefinition Resolve(DefinitionReference reference) => definitions.TryGetValue(reference,out var d)
        ? d : throw new ArgumentException("Unavailable station definition.");
    public StationBell Bell(DefinitionReference reference) => bells.TryGetValue(reference,out var b)
        ? b : throw new ArgumentException("Unavailable departure bell.");
    public StationArt Recipe(StationArtKind kind, int index, PassageMaterial? material = null) =>
        art.TryGetValue((kind,index,material),out var a) ? a : throw new ArgumentException("Unavailable station recipe.");
    public StationArt ThinRecipe(Heading direction, bool roof) => direction.IsCardinal
        ? Recipe(StationArtKind.Thin,direction.Index+(roof ? 1 : 0)) : throw new ArgumentException("Thin platform requires a cardinal heading.");
    public bool IsPlatformArt(DefinitionReference reference) => platformArt.Contains(reference);
}
