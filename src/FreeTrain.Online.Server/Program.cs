using FreeTrain.Online.Server;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;

if (args.Length > 0 && args[0] == "--headless-trace")
    return HeadlessTrace.Run(args);
if (args.Length == 1 && args[0] == "--headless-profile")
    return HeadlessProfile.Run();

// Explicit loopback-only historical UI regression harness. Normal service startup is canonical.
var prototypeRegression = args.Contains("--prototype-regression-harness");
var pluginArguments = PluginStartup.ParseArguments(args.Where(a => a != "--prototype-regression-harness").ToArray());
var builder = WebApplication.CreateBuilder(pluginArguments.Remaining);
var configuredPlugins = builder.Configuration.GetSection("FreeTrainOnline:PluginDirectories").Get<string[]>() ?? [];
var optionalPlugins = await PluginStartup.Load(pluginArguments.Directories.Concat(configuredPlugins.Select(Path.GetFullPath)).ToArray(),
    cacheDirectory: builder.Configuration["FreeTrainOnline:PluginCacheDirectory"]);
_ = FixedHeightmaps.Read(optionalPlugins); // Validate reviewed provider metadata before serving requests.
builder.Services.AddSingleton(optionalPlugins);
// Validate and register prepared authoring data. WorldProfiles continues to
// select only implemented rules/content; this does not enable recipe handlers.
builder.Services.AddSingleton(PreparedContentCatalog.Bundled);
// Loopback only unless an explicit deployment URL is configured.
if (string.IsNullOrEmpty(builder.Configuration["urls"]))
    builder.WebHost.UseUrls("http://127.0.0.1:5080");
if (string.IsNullOrEmpty(builder.Configuration["FreeTrainOnline:OperatorSecret"]) &&
    builder.Configuration["urls"]!.Split(';').Any(url => !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsLoopback))
    throw new InvalidOperationException("A non-loopback binding requires FreeTrainOnline__OperatorSecret.");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = SaveCodec.MaximumBytes);
var networkOptions = builder.Configuration.GetSection("FreeTrainOnline:Network").Get<NetworkOptions>() ?? new();
networkOptions.Validate();
var checkpointOptions = builder.Configuration.GetSection("FreeTrainOnline:Saves").Get<CheckpointOptions>() ?? new();
if (checkpointOptions.Interval < TimeSpan.FromSeconds(1) || checkpointOptions.Interval > TimeSpan.FromMinutes(5))
    throw new InvalidOperationException("Checkpoint interval must be between one second and five minutes.");
builder.Services.AddSingleton(checkpointOptions);
builder.Services.AddSingleton(_ => new SaveStorage(checkpointOptions.Directory));
builder.Services.AddSingleton(networkOptions);
if (prototypeRegression && builder.Configuration["urls"]!.Split(';').Any(url => !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsLoopback))
    throw new InvalidOperationException("The prototype regression harness requires loopback binding.");
builder.Services.AddSingleton<NetworkSession>(services => new(networkOptions, services.GetRequiredService<SaveStorage>(),
    services.GetRequiredService<ILogger<NetworkSession>>(),
    prototypeRegression ? (epoch,expiry) => SessionComposition.CreatePrototypeHost(epoch,expiry) : null));
builder.Services.AddSingleton<SessionDriver>();
builder.Services.AddHostedService(services => services.GetRequiredService<SessionDriver>());
var app = builder.Build();
app.UseDeployment();
app.MapGet("/health", (SessionDriver driver) => Results.Json(new { status = driver.CheckpointError == null ? "ok" : "degraded", stage = "deployment", protocolVersion = ProtocolIdentity.Version, checkpointError = driver.CheckpointError }, statusCode: driver.CheckpointError == null ? 200 : 503));
app.MapNetwork();
app.MapPluginContent();
app.Run();

return 0;
