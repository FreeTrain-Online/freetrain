using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FreeTrain.Online.Content;

public sealed record PluginCredit(string Button, string Title, string Text);
public sealed record PluginCredits(string DefaultLocale, Dictionary<string, PluginCredit> Locales);
public sealed record PluginFile(string Sha256, long Bytes, string? ContentType = null);
public sealed record PluginDependency(string Id, string Version, string CatalogHash);
public sealed record PluginNotice(string Path, string Url, string Sha256);
public sealed record PluginPackDescriptor(string Id, string Version, string Hash, string CatalogHash,
    string CatalogUrl, string SourceRevision, string Profile, PluginCredits Credits, ImmutableArray<PluginNotice> Notices);
public sealed record EnabledContent(string SetHash, ImmutableArray<PluginPackDescriptor> Packs);

// Only inert catalogs/media are read. Legacy assemblies and XML class names are never activated.
public sealed class OptionalPluginRegistry
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex Identity = new("^[a-z0-9][a-z0-9._-]{0,95}$", RegexOptions.CultureInvariant);
    private static readonly Regex Hash = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private readonly FrozenDictionary<string, (string Path, PluginFile File)> files;
    public EnabledContent Content { get; }
    public ImmutableArray<JsonElement> Catalogs { get; }
    // Only the explicitly selected, verified packs enter this registry.
    public RollingStockCatalog RollingStock => rollingStock.Value;
    private readonly Lazy<RollingStockCatalog> rollingStock;
    public static OptionalPluginRegistry Empty { get; } = Load([]);

    public bool Compatible(string? setHash) => setHash == Content.SetHash || (Content.Packs.IsEmpty && string.IsNullOrEmpty(setHash));

    public string? ContentType(string url) => files.TryGetValue(url, out var entry) ? entry.File.ContentType : null;

    public byte[]? ReadFile(string url, long maximumBytes = int.MaxValue)
    {
        if (!files.TryGetValue(url, out var entry)) return null;
        if (entry.File.Bytes < 0 || entry.File.Bytes > maximumBytes || entry.File.Bytes > int.MaxValue) throw new InvalidDataException("Optional resource exceeds byte limit.");
        using var stream = File.OpenRead(entry.Path);
        if (stream.Length != entry.File.Bytes) throw new InvalidDataException("Optional resource changed length.");
        var data = new byte[(int)entry.File.Bytes]; stream.ReadExactly(data);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Optional resource changed length.");
        Verify(data, entry.File); // A mounted volume changed after startup: fail closed.
        return data;
    }

    public static OptionalPluginRegistry Load(IEnumerable<string> directories) => new(directories);

    private OptionalPluginRegistry(IEnumerable<string> directories)
    {
        var packs = new List<PluginPackDescriptor>();
        var catalogs = new List<JsonElement>();
        var exposed = new Dictionary<string, (string, PluginFile)>(StringComparer.Ordinal);
        var plugins = new Dictionary<string, (string Owner, string[] Dependencies)>(StringComparer.Ordinal);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        using var bundledStream = typeof(PreparedContentCatalog).Assembly.GetManifestResourceStream("BundledCatalog")!;
        using var bundled = JsonDocument.Parse(bundledStream);
        Register(bundled.RootElement, PreparedContentCatalog.BundledPackId, plugins, ids);
        var bundledMedia = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        void IndexMedia(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("url", out var url) && node.TryGetProperty("sha256", out _)) bundledMedia.TryAdd(url.GetString()!, node);
                foreach (var property in node.EnumerateObject()) IndexMedia(property.Value);
            }
            else if (node.ValueKind == JsonValueKind.Array) foreach (var value in node.EnumerateArray()) IndexMedia(value);
        }
        IndexMedia(bundled.RootElement);

        var requirements = new List<(string Owner, PluginDependency Dependency)>();
        foreach (var directory in directories.Order(StringComparer.Ordinal))
        {
            var root = Path.GetFullPath(directory);
            var raw = File.ReadAllBytes(SafeFile(root, "prepared-pack.json"));
            using var document = JsonDocument.Parse(raw);
            var manifest = document.RootElement;
            var id = manifest.GetProperty("id").GetString()!;
            var version = manifest.GetProperty("version").GetString()!;
            var key = manifest.GetProperty("contentKey").GetString()!;
            if (manifest.GetProperty("schemaVersion").GetInt32() != 1 || !Identity.IsMatch(id) || !Hash.IsMatch(key) ||
                string.IsNullOrWhiteSpace(version) || id == PreparedContentCatalog.BundledPackId || packs.Any(p => p.Id == id))
                throw new InvalidDataException($"Invalid/duplicate optional pack identity in {root}.");
            var prefix = $"/api/content/{id}/{key}/";
            var entries = manifest.GetProperty("files").Deserialize<Dictionary<string, PluginFile>>(Json)!;
            foreach (var (path, entry) in entries)
            {
                if (entry.ContentType is not (null or "text/plain; charset=utf-8" or "text/plain; charset=shift_jis" or "application/octet-stream"))
                    throw new InvalidDataException($"{id}: unsupported content MIME type.");
                var fullPath = SafeFile(root, path);
                Verify(File.ReadAllBytes(fullPath), entry);
                exposed.Add(prefix + path, (fullPath, entry));
            }
            if (!entries.TryGetValue("catalog.json", out var catalogFile)) throw new InvalidDataException($"{id}: missing catalog.");
            using var catalogDoc = JsonDocument.Parse(File.ReadAllBytes(SafeFile(root, "catalog.json")));
            var catalog = catalogDoc.RootElement;
            if (catalog.GetProperty("schemaVersion").GetInt32() != 2 || catalog.GetProperty("packId").GetString() != id ||
                catalog.GetProperty("version").GetString() != version ||
                catalog.GetProperty("status").GetString() != "prepared; not selected by a world profile")
                throw new InvalidDataException($"{id}: catalog identity mismatch.");
            var ownedPlugins = catalog.GetProperty("plugins").EnumerateArray().Select(p => p.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
            if (catalog.GetProperty("contributions").EnumerateArray().Any(d => !ownedPlugins.Contains(d.GetProperty("pluginId").GetString()!) ||
                d.GetProperty("status").GetString() != "prepared; gameplay-unavailable")) throw new InvalidDataException($"{id}: invalid definition owner/status.");
            Register(catalog, id, plugins, ids);
            ValidateRecipes(catalog, id);
            foreach (var asset in catalog.GetProperty("assets").EnumerateArray())
                if (asset.TryGetProperty("sourceFile", out var sourceFile) &&
                    (!entries.TryGetValue(sourceFile.GetString()!, out var source) || source.Sha256 != asset.GetProperty("sourceSha256").GetString()))
                    throw new InvalidDataException($"{id}: unbound source media.");
            var credits = manifest.GetProperty("credits").Deserialize<PluginCredits>(Json)!;
            if (!credits.Locales.ContainsKey("en") || !credits.Locales.ContainsKey("ja") || !credits.Locales.ContainsKey(credits.DefaultLocale) ||
                credits.Locales.Values.Any(c => string.IsNullOrWhiteSpace(c.Button) || string.IsNullOrWhiteSpace(c.Title) || string.IsNullOrWhiteSpace(c.Text)))
                throw new InvalidDataException($"{id}: invalid localized credits.");
            var notices = manifest.GetProperty("notices").Deserialize<ImmutableArray<PluginNotice>>(Json);
            if (notices.IsEmpty || notices.Any(n => !n.Url.StartsWith(prefix, StringComparison.Ordinal) ||
                !entries.TryGetValue(n.Url[prefix.Length..], out var f) || f.Sha256 != n.Sha256))
                throw new InvalidDataException($"{id}: invalid notices.");
            var dependencies = manifest.GetProperty("dependencies").Deserialize<PluginDependency[]>(Json)!;
            if (!dependencies.Any(d => d.Id == PreparedContentCatalog.BundledPackId)) throw new InvalidDataException($"{id}: missing bundled requirement.");
            foreach (var dependency in dependencies)
                requirements.Add((id, dependency));
            packs.Add(new(id, version, Digest(raw), catalogFile.Sha256, prefix + "catalog.json",
                manifest.GetProperty("sourceRevision").GetString()!, manifest.GetProperty("profile").GetString()!, credits, notices));
            catalogs.Add(catalog.Clone());
        }
        // Resolve URLs after every selected pack is registered, allowing shared art across directories.
        void Media(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("url", out var u) && node.TryGetProperty("sha256", out _))
                {
                    var url = u.GetString()!;
                    if (exposed.TryGetValue(url, out var entry))
                    {
                        if (!node.TryGetProperty("sha256", out var hash) || hash.GetString() != entry.Item2.Sha256 ||
                            node.TryGetProperty("bytes", out var bytes) && bytes.GetInt64() != entry.Item2.Bytes)
                            throw new InvalidDataException($"Unbound optional media hash {url}.");
                    }
                    else if (!bundledMedia.TryGetValue(url, out var original) || original.GetProperty("sha256").GetString() != node.GetProperty("sha256").GetString() ||
                        node.TryGetProperty("bytes", out var expectedBytes) && (!original.TryGetProperty("bytes", out var originalBytes) || originalBytes.GetInt64() != expectedBytes.GetInt64()))
                        throw new InvalidDataException($"Foreign media URL {url}.");
                }
                foreach (var p in node.EnumerateObject()) Media(p.Value);
            }
            else if (node.ValueKind == JsonValueKind.Array) foreach (var value in node.EnumerateArray()) Media(value);
        }
        foreach (var catalog in catalogs)
        {
            Media(catalog);
            void Reference(JsonElement node)
            {
                if (node.ValueKind == JsonValueKind.Object)
                {
                    if (node.TryGetProperty("attributes", out var attributes))
                        foreach (var key in new[] { "ref", "carRef" })
                            if (attributes.TryGetProperty(key, out var reference) && !ids.ContainsKey(reference.GetString()!))
                                throw new InvalidDataException($"Unresolved optional {key}: {reference.GetString()}.");
                    foreach (var property in node.EnumerateObject()) Reference(property.Value);
                }
                else if (node.ValueKind == JsonValueKind.Array) foreach (var value in node.EnumerateArray()) Reference(value);
            }
            Reference(catalog.GetProperty("contributions"));
        }
        foreach (var (owner, dependency) in requirements)
        {
            var target = packs.FirstOrDefault(p => p.Id == dependency.Id);
            var valid = dependency.Id == PreparedContentCatalog.BundledPackId
                ? dependency.Version == PreparedContentCatalog.Bundled.Version && dependency.CatalogHash == PreparedContentCatalog.Bundled.CatalogHash
                : target != null && target.Version == dependency.Version && target.CatalogHash == dependency.CatalogHash;
            if (!valid) throw new InvalidDataException($"{owner}: missing/incompatible pack dependency {dependency.Id} {dependency.Version} {dependency.CatalogHash}.");
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id, HashSet<string> visiting)
        {
            if (visited.Contains(id)) return;
            if (!plugins.TryGetValue(id, out var plugin)) throw new InvalidDataException($"Missing plugin dependency {id}.");
            if (!visiting.Add(id)) throw new InvalidDataException($"Cyclic plugin dependency {id} ({plugin.Owner}).");
            foreach (var dependency in plugin.Dependencies) Visit(dependency, visiting);
            visiting.Remove(id); visited.Add(id);
        }
        foreach (var id in plugins.Keys) Visit(id, new(StringComparer.Ordinal));
        var sorted = packs.OrderBy(p => p.Id, StringComparer.Ordinal).ToImmutableArray();
        var setHash = Digest(Encoding.UTF8.GetBytes(PreparedContentCatalog.Bundled.CatalogHash + "\n" + string.Join("\n", sorted.Select(p => p.Id + ":" + p.Hash))));
        Content = new(setHash, sorted); Catalogs = catalogs.ToImmutableArray(); files = exposed.ToFrozenDictionary(StringComparer.Ordinal);
        rollingStock = new(() => RollingStockCatalog.Build(PreparedContentCatalog.Bundled, this));
    }

    private static void Register(JsonElement catalog, string owner, Dictionary<string, (string Owner, string[] Dependencies)> plugins, Dictionary<string, string> ids)
    {
        foreach (var plugin in catalog.GetProperty("plugins").EnumerateArray())
        {
            var id = plugin.GetProperty("id").GetString()!;
            if (plugins.TryGetValue(id, out var previous)) throw new InvalidDataException($"Plugin collision {id}: {previous.Owner} and {owner}.");
            plugins.Add(id, (owner, plugin.GetProperty("dependencies").EnumerateArray().Select(d => d.GetString()!).ToArray()));
        }
        foreach (var field in new[] { "factories", "contributions" })
        foreach (var contribution in catalog.GetProperty(field).EnumerateArray())
        {
            var id = contribution.GetProperty("id").GetString()!;
            if (ids.TryGetValue(id, out var previous)) throw new InvalidDataException($"Contribution collision {id}: {previous} and {owner}.");
            ids.Add(id, owner);
        }
    }

    private static void ValidateRecipes(JsonElement catalog, string owner)
    {
        var definitions = catalog.GetProperty("contributions").EnumerateArray().ToDictionary(d => d.GetProperty("id").GetString()!);
        var recipes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in catalog.GetProperty("renderRecipes").EnumerateArray())
        {
            var id = recipe.GetProperty("id").GetString()!;
            if (!definitions.ContainsKey(id) || !recipes.Add(id)) throw new InvalidDataException($"{owner}: invalid recipe {id}.");
            foreach (var frame in recipe.GetProperty("frames").EnumerateArray())
            {
                var status = frame.GetProperty("status").GetString();
                if (status == "invisible") continue;
                if (status is not ("ready" or "invalid-source-crop") || frame.GetProperty("rectangle").GetArrayLength() != 4 ||
                    frame.GetProperty("anchor").GetArrayLength() != 2 || frame.GetProperty("cell").GetArrayLength() != 3)
                    throw new InvalidDataException($"{owner}: invalid render frame.");
                foreach (var field in new[] { "surfaceSet", "baseSurfaceSet" })
                    if (!catalog.GetProperty("surfaceSets").TryGetProperty(frame.GetProperty(field).GetString()!, out _))
                        throw new InvalidDataException($"{owner}: unbound render surface.");
            }
        }
        if (definitions.Keys.Any(id => !recipes.Contains(id))) throw new InvalidDataException($"{owner}: missing render recipes.");
    }

    private static void Verify(byte[] bytes, PluginFile entry)
    {
        if (entry.Bytes != bytes.LongLength || !Hash.IsMatch(entry.Sha256) || Digest(bytes) != entry.Sha256)
            throw new InvalidDataException("Optional pack file hash/length mismatch.");
    }
    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string SafeFile(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\') || relative.Contains(':') ||
            relative.Split('/').Any(p => p is "" or "." or "..")) throw new InvalidDataException("Unsafe optional pack path.");
        var current = root;
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Symlink pack root.");
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Symlink pack file.");
        }
        return current;
    }
}
