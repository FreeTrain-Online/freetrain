using System.Diagnostics;
using System.Text.Json;
using FreeTrain.Online.Content;

namespace FreeTrain.Online.Server;

public static class PluginStartup
{
    public static (string[] Directories, string[] Remaining) ParseArguments(string[] args)
    {
        var directories = new List<string>(); var remaining = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--plugin-dir")
            {
                if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[i]))
                    throw new ArgumentException("--plugin-dir requires a plugin-set directory.");
                directories.Add(Path.GetFullPath(args[i]));
            }
            else if (args[i].StartsWith("--plugin-dir=", StringComparison.Ordinal))
            {
                var path = args[i]["--plugin-dir=".Length..];
                if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("--plugin-dir requires a directory.");
                directories.Add(Path.GetFullPath(path));
            }
            else remaining.Add(args[i]);
        }
        if (directories.Distinct(StringComparer.Ordinal).Count() != directories.Count) throw new ArgumentException("Duplicate --plugin-dir.");
        return (directories.ToArray(), remaining.ToArray());
    }

    public static async Task<OptionalPluginRegistry> Load(string[] directories, string? preparationDirectory = null, string? cacheDirectory = null)
    {
        if (directories.Length == 0) return OptionalPluginRegistry.Empty;
        var prepared = directories.Where(d => File.Exists(Path.Combine(d, "prepared-pack.json"))).ToList();
        var sources = directories.Except(prepared).ToArray();
        if (sources.Length > 0)
        {
            var runtime = preparationDirectory ?? Path.Combine(AppContext.BaseDirectory, "PluginPreparation");
            var start = new ProcessStartInfo("python3") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(Path.Combine(runtime, "scripts", "prepare-optional-content.py"));
            if (!string.IsNullOrWhiteSpace(cacheDirectory)) { start.ArgumentList.Add("--output-dir"); start.ArgumentList.Add(Path.GetFullPath(cacheDirectory)); }
            foreach (var source in sources) { start.ArgumentList.Add("--plugin-dir"); start.ArgumentList.Add(source); }
            foreach (var pack in prepared) { start.ArgumentList.Add("--reference-catalog"); start.ArgumentList.Add(Path.Combine(pack, "catalog.json")); }
            start.ArgumentList.Add("--bundled-catalog"); start.ArgumentList.Add(Path.Combine(runtime, "bundled-v2.json"));
            start.ArgumentList.Add("--bundled-input"); start.ArgumentList.Add(Path.Combine(runtime, "bundled-v2"));
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start plugin preparation (python3 is required for source sets).");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await stdout; var error = await stderr;
            if (process.ExitCode != 0) throw new InvalidDataException("Optional plugin startup rejected the selected set(s):\n" + error);
            prepared.AddRange(JsonSerializer.Deserialize<string[]>(output) ?? throw new InvalidDataException("Invalid preparation result."));
        }
        return OptionalPluginRegistry.Load(prepared);
    }

    public static void MapPluginContent(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<OptionalPluginRegistry>();
        app.MapGet("/api/content", () => Results.Json(registry.Content));
        app.MapGet("/api/content/{id}/{key}/{**path}", (HttpContext context) =>
        {
            var url = context.Request.Path.Value!;
            var data = registry.ReadFile(url);
            if (data == null) return Results.NotFound();
            context.Response.Headers.CacheControl = "no-cache";
            var type = registry.ContentType(url) ?? (Path.GetExtension(url) switch { ".png" => "image/png", ".json" => "application/json", _ => "application/octet-stream" });
            return Results.Bytes(data, type);
        });
    }
}
