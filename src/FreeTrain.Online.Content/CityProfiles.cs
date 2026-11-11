using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

// P7-03 binds definitions only. Tool registration, city lifecycle and presentation
// remain in their numbered slices. Metadata/empty definitions never own voxels.
public static class CityProfiles
{
    private static readonly HashSet<string> supportIds = ["null-contrib", "null-contrib-0:0",
        "{2E1EE15E-101A-43E5-C477-95363F8E8AF0}", "{C7E08038-DADA-46FD-C477-AEB5A4112B1C}",
        "{01AA051B-6112-4C00-C477-DE82E240F19F}", "{3773CA2C-11B5-4DCC-C477-2296E9EBEBA7}",
        "{BFC035E9-8B0E-41D6-C477-72F99D820290}", "{458C17B4-9426-4060-97B5-9A7090664175}",
        "{0EB17496-838E-43c8-941F-896EAF9396AF}", "{173B77D5-08FD-4897-BD37-FFA64CED1493}", "{develop-config}"];
    public static CityProfile Build(PreparedContentCatalog catalog, TerrainProfile terrain)
    {
        if(!terrain.Packs.Contains(new TerrainPack(catalog.PackId,catalog.Version,catalog.CatalogHash)))
            throw new ArgumentException("City/terrain catalog mismatch.");
        var population=PopulationBindings.Build(catalog);
        var bindings=new List<CityContentBinding>(); var structures=new List<CityStructureDefinition>();
        var roads=new List<CityRoadDefinition>(); var ground=new List<CityGroundDefinition>();
        foreach(var d in catalog.Definitions.Where(d=>d.Type is "commercial" or "varHeightBuilding" or "HalfVoxelStructure" or "road" or "land" || supportIds.Contains(d.Id)))
        {
            var reference=new DefinitionReference(catalog.PackId,d.Id,d.Id);
            var recipe=catalog.FindRenderRecipe(catalog.PackId,d.Id) ?? throw new ArgumentException("Missing city recipe binding.");
            if(recipe.Data.GetProperty("status").GetString() is not ("ready" or "source-diagnostic"))
                throw new ArgumentException("Unavailable city recipe.");
            var children=d.Declaration.TryGetProperty("children",out var childNodes) ? childNodes.EnumerateArray().ToArray() : [];
            JsonElement Node(string tag) => children.SingleOrDefault(n=>n.GetProperty("tag").GetString()==tag);
            string Text(string tag,string fallback="") => Node(tag) is var n && n.ValueKind!=JsonValueKind.Undefined ? n.GetProperty("text").GetString()! : fallback;
            int Integer(string tag,int fallback) => int.Parse(Text(tag,fallback.ToString(CultureInfo.InvariantCulture)),CultureInfo.InvariantCulture);
            var computer=Node("computerCannotBuild").ValueKind!=JsonValueKind.Undefined;
            var player=Node("playerCannotBuild").ValueKind!=JsonValueKind.Undefined;
            bindings.Add(new(reference,d.Type,d.Implementation,recipe.Handler,computer,player));
            population.TryGetValue(d.Id,out var pop);
            var price=long.Parse(Text("price","0"),CultureInfo.InvariantCulture);
            if(d.Type is "commercial" or "varHeightBuilding" or "HalfVoxelStructure")
            {
                var kind=d.Type switch { "commercial"=>CityStructureKind.Fixed,"varHeightBuilding"=>CityStructureKind.Variable,_=>CityStructureKind.Half };
                if(recipe.Handler!=(kind switch {CityStructureKind.Fixed=>"fixed-3d",CityStructureKind.Variable=>"variable-height",_=>"half-voxel"}))
                    throw new ArgumentException("City structure recipe mismatch.");
                var size=Text("size","1,1").Split(',').Select(s=>int.Parse(s,CultureInfo.InvariantCulture)).ToArray();
                if(size.Length!=2) throw new ArgumentException("Invalid city size.");
                var min=kind==CityStructureKind.Variable ? Integer("minHeight",1) : Integer("height",1);
                var max=kind==CityStructureKind.Variable ? Integer("maxHeight",1) : min;
                var variants=kind==CityStructureKind.Half ? recipe.Data.GetProperty("frames").EnumerateArray().Select(f=>
                {
                    var s=f.GetProperty("selector");
                    return new CityHalfVariant(s.GetProperty("direction").GetInt32(),s.GetProperty("side").GetInt32(),s.GetProperty("color").GetInt32());
                }).Distinct().ToImmutableArray() : ImmutableArray<CityHalfVariant>.Empty;
                structures.Add(new(reference,kind,Text("name",Text("group")),size[0],size[1],min,max,price,pop,computer,variants));
            }
            else if(d.Type=="road")
            {
                var style=Node("style").GetProperty("attributes");
                roads.Add(new(reference,style.GetProperty("name").GetString()!,style.GetProperty("sidewalk").GetString()!,
                    int.Parse(style.GetProperty("lanes").GetString()!,CultureInfo.InvariantCulture)));
            }
            else if(d.Type=="land")
            {
                var grid=Text("grid","1,1").Split(',').Select(s=>int.Parse(s,CultureInfo.InvariantCulture)).ToArray();
                if(grid.Length!=2) throw new ArgumentException("Invalid city grid.");
                ground.Add(new(reference,recipe.Handler,price,grid[0],grid[1],pop,computer));
            }
        }
        return new(terrain.Packs,bindings,structures,roads,ground);
    }
}
