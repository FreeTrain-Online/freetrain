using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using FreeTrain.Online.Content;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;
using FreeTrain.Online.Simulation;

static class TerrainAcceptance
{
    public static async Task Run(string manifest,string directory)
    {
        Directory.CreateDirectory(directory);using var data=JsonDocument.Parse(File.ReadAllBytes(manifest));var root=data.RootElement;
        var rows=new List<object>();var palette=WorldCreationCapabilities.ForestChoices().Where(c=>c.Variant=="mountain").Select(c=>c.Binding).ToImmutableArray();
        foreach(var scenario in root.GetProperty("scenarios").EnumerateArray())
        foreach(var size in root.GetProperty("sizes").EnumerateArray())
        foreach(var water in root.GetProperty("waterLevels").EnumerateArray())
        {
            string id=scenario.GetProperty("id").GetString()!,kind=scenario.GetProperty("kind").GetString()!;
            int h=size.GetProperty("h").GetInt32(),v=size.GetProperty("v").GetInt32(),z=size.GetProperty("z").GetInt32();
            var r=WorldCreationProfile.Generated(root.GetProperty("seed").GetInt32(),id) with{Dimensions=new(h,v,z),WaterLevel=water.GetInt32()};
            var o=r.Options with{StartingArea=0,Relief=new(ReliefKind.Hills,50,24,50)};
            var sides=kind switch{"north"=>CoastSides.North,"east"=>CoastSides.East,"south"=>CoastSides.South,"west"=>CoastSides.West,
                "adjacent"=>CoastSides.North|CoastSides.East,"opposite"=>CoastSides.North|CoastSides.South,_=>CoastSides.None};
            if(sides!=CoastSides.None||kind=="island")o=o with{Coast=new(kind=="island"?CoastKind.Island:CoastKind.Sides,sides,40)};
            if(kind=="plains")o=o with{Relief=new(ReliefKind.Plains,0,24,50)};
            if(kind=="mountains")o=o with{Relief=new(ReliefKind.Mountains,100,4,0)};
            if(kind is "lakes" or "combined")o=o with{Lakes=new(2,3)};
            if(kind=="combined")o=o with{Coast=new(CoastKind.Sides,CoastSides.North,30),Rivers=new(RiverDirection.NorthSouth,2,80)};
            if(kind is "sparse" or "dense" or "mixed")o=o with{StartingArea=16,Forests=new(kind=="sparse"?15:90,80,kind=="mixed"?palette:[palette[0]])};
            r=r with{Options=o};var state=new TerrainSessionState(ReliefTerrain.Create(r,TerrainProfiles.Bundled),TerrainProvenance.From(r));
            var bytes=SaveCodec.Write(state);var repeated=new TerrainSessionState(ReliefTerrain.Create(r,TerrainProfiles.Bundled),TerrainProvenance.From(r));
            if(!bytes.SequenceEqual(SaveCodec.Write(repeated)))throw new Exception("Generation is not deterministic: "+id);
            var geography=WaterTerrain.Validate(r,state.World);var forests=ForestTerrain.Measure(r,state.World,ForestTerrain.Layout(r,state.World));
            var stem=$"{id}-{h}x{v}-water{r.WaterLevel}";
            File.WriteAllBytes(Path.Combine(directory,stem+".before.json"),bytes);
            using var host=new SessionHost(state,"owner",TerrainProfiles.Resolve);var snapshot=host.Snapshot();
            CornerTerrainQuery? edit=null;
            for(int i=0;i<state.World.Cells.Length;i++)
            {
                var p=state.World.Space.ToLocation(new(i%h,i/h,state.World.Cells[i].Ground));
                var q=new CornerTerrainQuery(1,state.World.Revision,new(p.X,p.Y,p.Z),"placeTerrace");
                if(host.PreviewCorner(q).Legal){edit=q;break;}
            }
            if(edit==null)throw new Exception("No legal terrain handoff site: "+stem);
            async Task Send(CornerTerrainQuery q){var pending=host.Enqueue(new("owner"),new(Guid.NewGuid().ToString(),1,new EditCornerCommand(q)));host.Pump(0);if(await pending is not AcceptedMessage)throw new Exception("Edit rejected: "+stem);}
            await Send(edit);await Send(edit with{ExpectedGeometryRevision=state.World.Revision,Action="removeTerrace"});
            var pendingSave=host.CaptureSave();host.Pump(0);var edited=await pendingSave;
            var restored=SaveCodec.ReadWorld(edited,TerrainProfiles.Resolve).Terrain!;
            if(!edited.SequenceEqual(SaveCodec.Write(restored)))throw new Exception("Edited save continuation mismatch: "+stem);
            var finalGeography=WaterTerrain.Validate(r,restored.World);
            if(geography!=finalGeography)throw new Exception("Terrace restoration changed topology: "+stem);
            File.WriteAllBytes(Path.Combine(directory,stem+".edited.json"),edited);
            rows.Add(new{id,kind,h,v,z,water=r.WaterLevel,request=JsonSerializer.Deserialize<JsonElement>(state.Provenance.RequestJson),
                beforeSha256=Convert.ToHexStringLower(SHA256.HashData(bytes)),editedSha256=Convert.ToHexStringLower(SHA256.HashData(edited)),
                saveBytes=edited.Length,geography,forests,site=edit.Position});
            Console.WriteLine(stem+" exact generation/edit/reopen passed");
        }
        File.WriteAllText(Path.Combine(directory,"report.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{rows.Count} integrated WORLDGEN cases passed");
    }
}
