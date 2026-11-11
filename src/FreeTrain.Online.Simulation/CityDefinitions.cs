// Stable ExAV 883cf7d/trunk: structure/land/road contributions and subsidiary inputs.
// Original authors, LGPL-2.1-or-later. No desktop types or mutable catalog state.
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace FreeTrain.Online.Simulation;

public enum CityStructureKind { Fixed, Variable, Half }
public sealed record CityContentBinding(DefinitionReference Reference, string SourceType, string Implementation,
    string Handler, bool ComputerCannotBuild, bool PlayerCannotBuild);
public sealed record CityHalfVariant(int Direction, int Side, int Color);
public sealed record CityStructureDefinition(DefinitionReference Reference, CityStructureKind Kind, string Name,
    int SizeX, int SizeY, int MinimumHeight, int MaximumHeight, long UnitPrice, Population? Population,
    bool ComputerCannotBuild, ImmutableArray<CityHalfVariant> HalfVariants = default)
{
    public int Area => checked(SizeX * SizeY);
    public long PricePerArea => UnitPrice / Area; // Source integral division before double exposure.
    public long Price(int height)
    {
        if (height < MinimumHeight || height > MaximumHeight) throw new ArgumentException("Invalid city height.");
        return Kind == CityStructureKind.Variable ? checked(UnitPrice * height) : UnitPrice;
    }
    public Footprint Footprint(int height)
    {
        Price(height);
        // HalfDividedVoxel occupies ONE full storage cell regardless of visual height.
        return Simulation.Footprint.Box(new(new(0,0,0),SizeX,SizeY,Kind == CityStructureKind.Half ? 1 : height));
    }
    public Population? PopulationAt(int height) => Population == null ? null : Kind == CityStructureKind.Variable
        ? new("MultiplierPopulation",Factor:height,Children:[Population]) : Population;
}
public sealed record CityRoadDefinition(DefinitionReference Reference, string Style, string Sidewalk, int Lanes)
{
    // Existing Traffic slots use eight-heading bits; source art uses four bits.
    public static int Edges(int sourceMask)
    {
        if(sourceMask is <1 or >15) throw new ArgumentException("Invalid source road mask.");
        return (sourceMask&1) | (sourceMask&2)<<1 | (sourceMask&4)<<2 | (sourceMask&8)<<3;
    }
    public static int SourceMask(int edges)
    {
        if(edges<1 || (edges&~0x55)!=0) throw new ArgumentException("Invalid cardinal road edges.");
        return (edges&1) | (edges&4)>>1 | (edges&16)>>2 | (edges&64)>>3;
    }
}
public sealed record CityGroundDefinition(DefinitionReference Reference, string Handler, long Price,
    int GridX, int GridY, Population? Population, bool ComputerCannotBuild);

public sealed class CityProfile
{
    public ImmutableArray<TerrainPack> Packs { get; }
    public ImmutableArray<CityContentBinding> Bindings { get; }
    public ImmutableArray<CityStructureDefinition> Structures { get; }
    public ImmutableArray<CityRoadDefinition> Roads { get; }
    public ImmutableArray<CityGroundDefinition> Ground { get; }
    public DefinitionReference PropertyDefinition { get; }
    public DefinitionReference FenceHostDefinition { get; }
    private readonly FrozenDictionary<DefinitionReference, CityStructureDefinition> structures;
    private readonly FrozenDictionary<DefinitionReference, CityGroundDefinition> ground;
    private readonly FrozenDictionary<DefinitionReference, CityRoadDefinition> roads;
    public CityProfile(IEnumerable<TerrainPack> packs, IEnumerable<CityContentBinding> bindings,
        IEnumerable<CityStructureDefinition> structures, IEnumerable<CityRoadDefinition> roads,
        IEnumerable<CityGroundDefinition> ground)
    {
        Packs=packs.ToImmutableArray(); Bindings=bindings.ToImmutableArray(); Structures=structures.ToImmutableArray();
        Roads=roads.ToImmutableArray(); Ground=ground.ToImmutableArray();
        if (Packs.IsEmpty || Packs.Any(p => p == null || string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Version) ||
                !WorldCreationProfile.IsHash(p.CatalogHash)) || Packs.Select(p=>p.Id).Distinct().Count()!=Packs.Length ||
            Bindings.Length>4096 || Bindings.Any(b=>b == null || b.Reference == null || string.IsNullOrWhiteSpace(b.SourceType) ||
                string.IsNullOrWhiteSpace(b.Handler)) || Bindings.Select(b=>b.Reference).Distinct().Count()!=Bindings.Length)
            throw new ArgumentException("Invalid city content graph.");
        foreach(var b in Bindings) { b.Reference.Validate(); if(!Packs.Any(p=>p.Id==b.Reference.PackId)) throw new ArgumentException("Missing city pack."); }
        var refs=Bindings.Select(b=>b.Reference).ToHashSet();
        var source=Bindings.ToDictionary(b=>b.Reference);
        if(Structures.Any(d=>d == null || !refs.Contains(d.Reference) || !Enum.IsDefined(d.Kind) ||
                string.IsNullOrWhiteSpace(d.Name) ||
                d.SizeX is <1 or >32 || d.SizeY is <1 or >32 || d.MinimumHeight<1 || d.MaximumHeight<d.MinimumHeight ||
                d.MaximumHeight>256 || d.UnitPrice<0 || d.Kind!=CityStructureKind.Variable && d.MinimumHeight!=d.MaximumHeight ||
                d.Kind==CityStructureKind.Half && (d.SizeX!=1 || d.SizeY!=1 || d.HalfVariants.IsDefaultOrEmpty ||
                    d.HalfVariants.Any(v=>v==null || v.Direction is <0 or >7 || v.Direction%2!=0 || v.Side is <0 or >1 || v.Color<0))) ||
            Roads.Any(d=>d == null || !refs.Contains(d.Reference) || string.IsNullOrWhiteSpace(d.Style) ||
                string.IsNullOrWhiteSpace(d.Sidewalk) || d.Lanes is <1 or >32) ||
            Ground.Any(d=>d == null || !refs.Contains(d.Reference) || d.Price<0 || d.GridX<1 || d.GridY<1))
            throw new ArgumentException("Invalid city definitions.");
        foreach(var d in Structures) { d.Population?.Validate(); d.Price(d.MaximumHeight); d.Footprint(d.MaximumHeight); }
        foreach(var d in Ground) d.Population?.Validate();
        if(Structures.Any(d=>source[d.Reference].SourceType!=(d.Kind switch {CityStructureKind.Fixed=>"commercial",CityStructureKind.Variable=>"varHeightBuilding",_=>"HalfVoxelStructure"}) ||
                source[d.Reference].Handler!=(d.Kind switch {CityStructureKind.Fixed=>"fixed-3d",CityStructureKind.Variable=>"variable-height",_=>"half-voxel"}) ||
                source[d.Reference].ComputerCannotBuild!=d.ComputerCannotBuild) ||
            Roads.Any(d=>source[d.Reference].SourceType!="road" || source[d.Reference].Handler!="road") ||
            Ground.Any(d=>source[d.Reference].SourceType!="land" || source[d.Reference].Handler!=d.Handler || source[d.Reference].ComputerCannotBuild!=d.ComputerCannotBuild))
            throw new ArgumentException("City definition/source binding mismatch.");
        var physical=Structures.Select(d=>d.Reference).Concat(Roads.Select(d=>d.Reference)).Concat(Ground.Select(d=>d.Reference)).ToArray();
        if(physical.Distinct().Count()!=physical.Length) throw new ArgumentException("Duplicate city definition.");
        this.structures=Structures.ToFrozenDictionary(d=>d.Reference); this.ground=Ground.ToFrozenDictionary(d=>d.Reference);
        this.roads=Roads.ToFrozenDictionary(d=>d.Reference);
        PropertyDefinition=new(Packs[0].Id,"system:land-property","system:land-property");
        FenceHostDefinition=new(Packs[0].Id,"system:fence-dummy","geometry:fence-dummy-1");
    }
    public CityStructureDefinition Structure(DefinitionReference reference) => structures.TryGetValue(reference,out var d)
        ? d : throw new ArgumentException("Unavailable city structure.");
    public CityGroundDefinition GroundDefinition(DefinitionReference reference) => ground.TryGetValue(reference,out var d)
        ? d : throw new ArgumentException("Unavailable city ground definition.");
    public CityRoadDefinition? Road(DefinitionReference reference) => roads.GetValueOrDefault(reference);
}
