using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace FreeTrain.Online.Content;

public sealed record PreparedDefinition(string Id, string PluginId, string Type, JsonElement Declaration);
public sealed record PreparedPicture(string Id, bool HasPreparedVariants, JsonElement Data);

// Inert authoring data, deliberately separate from WorldProfiles. Finding a
// declaration in this registry never authorizes an unimplemented game handler.
public sealed class PreparedContentCatalog
{
    public const string BundledPackId = "exav-stable-bundled";
    public const string SourceRevision = "883cf7d81cf49db25ff38cd3a25d0f140afe31a0";
    private readonly FrozenDictionary<string, PreparedDefinition> definitions;
    private readonly FrozenDictionary<string, PreparedPicture> pictures;
    private static readonly Lazy<PreparedContentCatalog> bundled = new(LoadEmbedded);
    public static PreparedContentCatalog Bundled => bundled.Value;
    public string PackId { get; }
    public string Version { get; }
    public string CatalogHash { get; }
    public ImmutableArray<string> PluginOrder { get; }
    public ImmutableArray<PreparedDefinition> Definitions { get; }
    public ImmutableArray<PreparedPicture> Pictures { get; }
    public bool GameplayAvailable => false;

    public PreparedDefinition? FindDefinition(string packId, string id) =>
        packId == PackId ? definitions.GetValueOrDefault(id) : null;
    public PreparedPicture? FindPicture(string packId, string id) =>
        packId == PackId ? pictures.GetValueOrDefault(id) : null;

    public static PreparedContentCatalog Read(ReadOnlyMemory<byte> data, string expectedHash)
    {
        var actual = Convert.ToHexStringLower(SHA256.HashData(data.Span));
        if (actual != expectedHash) throw new ArgumentException("Prepared catalog hash mismatch.");
        using var document = JsonDocument.Parse(data);
        return new(document.RootElement, actual);
    }

    private PreparedContentCatalog(JsonElement root, string hash)
    {
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("packId").GetString() != BundledPackId ||
            root.GetProperty("version").GetString() != "1" ||
            root.GetProperty("source").GetProperty("revision").GetString() != SourceRevision ||
            root.GetProperty("source").GetProperty("subtree").GetString() != "trunk" ||
            root.GetProperty("status").GetString() != "prepared; not selected by a world profile")
            throw new ArgumentException("Unsupported prepared catalog identity.");
        PackId = BundledPackId; Version = "1"; CatalogHash = hash;
        PluginOrder = root.GetProperty("pluginOrder").EnumerateArray().Select(p => p.GetString()!).ToImmutableArray();
        var pluginIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plugin in root.GetProperty("plugins").EnumerateArray())
        {
            var id = plugin.GetProperty("id").GetString()!;
            if (!pluginIds.Add(id) || plugin.GetProperty("scope").GetString() != "in-scope" ||
                plugin.GetProperty("dependencies").EnumerateArray().Any(d =>
                    !PluginOrder.Contains(d.GetString()!) || PluginOrder.IndexOf(d.GetString()!) >= PluginOrder.IndexOf(id)))
                throw new ArgumentException("Invalid prepared plugin dependencies.");
        }
        if (PluginOrder.Length != pluginIds.Count || PluginOrder.Distinct(StringComparer.Ordinal).Count() != pluginIds.Count ||
            PluginOrder.Any(id => !pluginIds.Contains(id)))
            throw new ArgumentException("Invalid prepared plugin order.");
        Definitions = root.GetProperty("contributions").EnumerateArray().Select(row =>
        {
            var plugin = row.GetProperty("pluginId").GetString()!;
            if (!pluginIds.Contains(plugin) || row.GetProperty("status").GetString() != "prepared; gameplay-unavailable")
                throw new ArgumentException("Invalid prepared definition.");
            return new PreparedDefinition(row.GetProperty("id").GetString()!, plugin, row.GetProperty("type").GetString()!,
                row.TryGetProperty("declaration", out var declaration) ? declaration.Clone() : row.Clone());
        }).ToImmutableArray();
        Pictures = root.GetProperty("pictures").EnumerateArray().Select(row =>
            new PreparedPicture(row.GetProperty("id").GetString()!, row.GetProperty("status").GetString() == "prepared-picture", row.Clone()))
            .ToImmutableArray();
        if (Definitions.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != Definitions.Length ||
            Pictures.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != Pictures.Length)
            throw new ArgumentException("Duplicate prepared registry identity.");
        definitions = Definitions.ToFrozenDictionary(d => d.Id, StringComparer.Ordinal);
        pictures = Pictures.ToFrozenDictionary(p => p.Id, StringComparer.Ordinal);
        foreach (var alias in root.GetProperty("selections").EnumerateArray())
            if (!definitions.ContainsKey(alias.GetProperty("target").GetString()!))
                throw new ArgumentException("Unresolved prepared selection.");
    }

    private static PreparedContentCatalog LoadEmbedded()
    {
        var assembly = typeof(PreparedContentCatalog).Assembly;
        using var catalog = assembly.GetManifestResourceStream("BundledCatalog")!;
        using var hash = new StreamReader(assembly.GetManifestResourceStream("BundledCatalogHash")!);
        using var bytes = new MemoryStream(); catalog.CopyTo(bytes);
        return Read(bytes.ToArray(), hash.ReadToEnd().Trim());
    }
}
