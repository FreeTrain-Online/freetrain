using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using FreeTrain.Online.Simulation;

namespace FreeTrain.Online.Content;

// Bind inert prepared recipes, including the documented EXAV-GAL-001 crop correction.
public static class StationProfiles
{
    public static StationProfile Build(PreparedContentCatalog catalog, TerrainProfile terrain)
    {
        if (!terrain.Packs.Contains(new TerrainPack(catalog.PackId,catalog.Version,catalog.CatalogHash)))
            throw new ArgumentException("Station/terrain catalog mismatch.");
        DefinitionReference Ref(string id) => new(catalog.PackId,id,id);
        var definitions = catalog.Definitions.Where(d => d.Type == "station").Select(d =>
        {
            if (d.Implementation != "freetrain.contributions.rail.StationContribution")
                throw new ArgumentException("Unsupported station implementation.");
            var r = catalog.FindRenderRecipe(catalog.PackId,d.Id)!;
            if (r.Handler != "fixed-3d" || r.Data.GetProperty("status").GetString() != "ready")
                throw new ArgumentException("Unavailable station art.");
            var f = r.Data.GetProperty("fields"); var size = f.GetProperty("size");
            long Number(string key) => long.Parse(f.GetProperty(key).GetString()!,CultureInfo.InvariantCulture);
            return new StationDefinition(Ref(d.Id),Text(d.Declaration,"name",Text(d.Declaration,"group")),size[0].GetInt32(),size[1].GetInt32(),
                checked((int)Number("height")),Number("price"),checked((int)Number("operationCost")));
        }).ToArray();
        var art = new List<StationArt>();
        void Add(string id, StationArtKind kind, int index, PassageMaterial? material = null)
        {
            var r = catalog.FindRenderRecipe(catalog.PackId,id);
            if (r?.Handler != "source-geometry" || r.Data.GetProperty("status").GetString() is not ("ready" or "source-diagnostic"))
                throw new ArgumentException("Missing station geometry recipe.");
            var frames = r.Data.GetProperty("frames").EnumerateArray().Select(f =>
            {
                var rect = f.GetProperty("rectangle"); var anchor = f.GetProperty("anchor");
                var status = f.GetProperty("status").GetString();
                if (status is not ("ready" or "invalid-source-crop")) throw new ArgumentException("Invalid station art frame.");
                return new StationArtFrame(f.GetProperty("pictureId").GetString()!,rect[0].GetInt32(),rect[1].GetInt32(),
                    rect[2].GetInt32(),rect[3].GetInt32(),anchor[0].GetInt32(),anchor[1].GetInt32(),status == "invalid-source-crop");
            }).ToImmutableArray();
            art.Add(new(Ref(id),kind,index,material,frames));
        }
        for (var i=0;i<8;i++) Add($"system:thin-{i}",StationArtKind.Thin,i);
        for (var i=0;i<2;i++) Add($"system:fat-{i}",StationArtKind.WideCenter,i);
        Add("system:yard",StationArtKind.Yard,0);
        foreach (var material in Enum.GetValues<PassageMaterial>())
        {
            var name = material.ToString().ToLowerInvariant();
            for (var i=0;i<14;i++) Add($"system:{name}-passage-{i}",StationArtKind.Passage,i,material);
            for (var i=0;i<32;i++) Add($"system:{name}-stair-{i}",StationArtKind.Stair,i,material);
        }
        var bells = catalog.Definitions.Where(d => d.Type == "trainDepartureBell").Select(d =>
        {
            var sound = d.Declaration.GetProperty("children").EnumerateArray().Single(n => n.GetProperty("tag").GetString() == "sound")
                .GetProperty("attributes").GetProperty("href").GetString()!;
            if (string.IsNullOrWhiteSpace(sound) || sound.Contains('/') || sound.Contains('\\') || sound.Contains(':') || sound is "." or "..")
                throw new ArgumentException("Invalid departure bell resource.");
            var resource = catalog.BellResource(d.Id,sound);
            return new StationBell(Ref(d.Id),Text(d.Declaration,"name"),resource.SourcePath,resource.Sha256,resource.MediaPath);
        });
        return new(terrain.Packs,definitions,art,bells);
    }
    private static string Text(JsonElement declaration, string tag, string fallback = "")
    {
        var node = declaration.GetProperty("children").EnumerateArray().SingleOrDefault(n => n.GetProperty("tag").GetString() == tag);
        return node.ValueKind == JsonValueKind.Undefined ? fallback : node.GetProperty("text").GetString()!;
    }
}
