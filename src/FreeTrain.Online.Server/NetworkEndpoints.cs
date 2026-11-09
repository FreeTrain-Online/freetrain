using FreeTrain.Online.Simulation;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FreeTrain.Online.Contracts;
using FreeTrain.Online.Content;
using FreeTrain.Online.Persistence;
using FreeTrain.Online.Session;

namespace FreeTrain.Online.Server;

public static class NetworkEndpoints
{
    private static readonly SemaphoreSlim stateWriters = new(2);
    private const string CookieName = "freetrain-online.operator";
    public const int MaximumMessageBytes = 16384;
    public static void MapNetwork(this WebApplication app)
    {
        var saveInputs = new SemaphoreSlim(2);
        var session = app.Services.GetRequiredService<NetworkSession>();
        app.Lifetime.ApplicationStopping.Register(session.PrepareShutdown);
        var secret = app.Configuration["FreeTrainOnline:OperatorSecret"];
        if (string.IsNullOrEmpty(secret)) secret = null;
        var configured = app.Configuration.GetSection("FreeTrainOnline:Participants").Get<ParticipantAccess[]>() ?? [];
        if (configured.Length > 32 || configured.Any(p => string.IsNullOrWhiteSpace(p.Id) || p.Id.Length > 64 ||
            p.Id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || p.Id == "operator-1" ||
            string.IsNullOrWhiteSpace(p.Secret) || !Enum.TryParse<ParticipantRole>(p.Role, true, out var role) || !Enum.IsDefined(role)) ||
            configured.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != configured.Length ||
            configured.Select(p => p.Secret).Append(secret ?? "").Distinct(StringComparer.Ordinal).Count() != configured.Length + 1)
            throw new InvalidOperationException("Invalid FreeTrainOnline:Participants credentials.");
        var attempts = 0;
        var attemptWindow = DateTimeOffset.UtcNow;
        var accessGate = new object();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(5), KeepAliveTimeout = TimeSpan.FromSeconds(10) });
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
            context.Response.Headers.CacheControl = "no-store";
            if (!Permitted(context, app.Configuration, secret)) { context.Response.StatusCode = 403; return; }
            await next(context);
        });
        CookieOptions AccessCookieOptions(HttpContext context) => new()
        { HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = context.Request.IsHttps, Path = "/api", MaxAge = TimeSpan.FromHours(8) };
        bool Cookie(HttpContext context, ParticipantContext participant)
        {
            var credential = session.IssueAccess(participant);
            if (credential == null) return false;
            session.RevokeAccess(context.Request.Cookies[CookieName]);
            context.Response.Cookies.Append(CookieName, credential, AccessCookieOptions(context));
            return true;
        }
        ParticipantContext? Identity(HttpContext context) => session.ResolveAccess(context.Request.Cookies[CookieName]);
        bool Authorized(HttpContext context) => Identity(context) != null;
        var plugins = app.Services.GetService<OptionalPluginRegistry>() ?? OptionalPluginRegistry.Empty;
        bool Compatible(HttpContext context) => context.Request.Query["protocolVersion"] == ProtocolIdentity.Version.ToString() && context.Request.Query["contentVersion"].Contains(session.ContentVersion) &&
            (context.Request.Path == "/api/bootstrap" || plugins.Compatible(context.Request.Query["packSetHash"]));
        app.MapPost("/api/access", async (HttpContext context) =>
        {
            lock (accessGate)
            {
                if (DateTimeOffset.UtcNow - attemptWindow >= TimeSpan.FromMinutes(1)) { attemptWindow = DateTimeOffset.UtcNow; attempts = 0; }
                if (++attempts > 10) { context.Response.StatusCode = 429; return; }
            }
            if (context.Request.ContentLength is > MaximumMessageBytes) { context.Response.StatusCode = 413; return; }
            try
            {
                using var body = new MemoryStream(); var chunk = new byte[1024]; int read;
                while ((read = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (body.Length + read > MaximumMessageBytes) { context.Response.StatusCode = 413; return; }
                    body.Write(chunk, 0, read);
                }
                var login = JsonSerializer.Deserialize<AccessRequest>(body.ToArray(), NetworkSession.Json);
                if (login == null) { context.Response.StatusCode = 401; return; }
                ParticipantContext? participant = Equal(login.Secret, secret) ? new("operator-1")
                    : secret == null && login.Secret.Length == 0 ? new("local-" + Guid.NewGuid().ToString("N")) : null;
                foreach (var access in configured)
                    if (Equal(login.Secret, access.Secret)) participant = new(access.Id, Enum.Parse<ParticipantRole>(access.Role, true));
                if (participant == null) { context.Response.StatusCode = 401; return; }
                context.Response.StatusCode = Cookie(context, participant) ? 204 : 503;
            }
            catch (JsonException) { context.Response.StatusCode = 400; }
        });
        app.MapPost("/api/leave", (HttpContext context) =>
        {
            session.RevokeAccess(context.Request.Cookies[CookieName]);
            // A non-credential cookie prevents anonymous bootstrap from auto-joining
            // immediately after leaving, including after a browser refresh.
            context.Response.Cookies.Append(CookieName, "signed-out", AccessCookieOptions(context));
            return Results.NoContent();
        });
        app.MapGet("/api/bootstrap", (HttpContext context) =>
        {
            context.Response.Headers["X-FreeTrain-Content-Set"] = plugins.Content.SetHash;
            if (!Compatible(context)) return Results.Json(new { code = "incompatible", protocolVersion = ProtocolIdentity.Version, contentVersion = session.ContentVersion }, statusCode: 426);
            var participant = Identity(context);
            if (participant == null)
            {
                if (secret != null || context.Request.Cookies.ContainsKey(CookieName))
                    return Results.Json(new { code = "accessRequired", anonymousAccess = secret == null }, statusCode: 401);
                participant = new("local-" + Guid.NewGuid().ToString("N"));
                if (!Cookie(context, participant)) return Results.StatusCode(503); // Independent loopback identities.
            }
            var join = session.Bootstrap(participant);
            WireMessage response = context.Request.Query["metadataOnly"] == "true" || join.Snapshot.Terrain?.Cells.Length > 4096
                ? new SessionOfferMessage(join.ProtocolVersion, join.ContentVersion, join.ServerIncarnation, join.SessionId,
                    join.ParticipantId, join.Role, join.WorldEpoch, join.Ruleset, join.RequiredPacks) : join;
            return Results.Json<WireMessage>(response, NetworkSession.Json);
        });
        // Observers may query geometry; this takes one coherent read and never queues a mutation.
        app.MapPost("/api/spatial/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            if (context.Request.ContentLength is > MaximumMessageBytes) return Results.StatusCode(413);
            try
            {
                using var buffer = new MemoryStream();
                var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk, 0, count);
                }
                var query = JsonSerializer.Deserialize<SpatialQuery>(buffer.ToArray(), NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewSpatial(query), NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException)
            { return Results.Json(new { code = "staleEpoch" }, statusCode: 409); }
        });
        app.MapPost("/api/terrain/corner/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<CornerTerrainQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewCorner(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/removal/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<RemovalQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewRemoval(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/fleet/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<FleetTradeQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewFleetTrade(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/fleet/deployment/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<FleetDeploymentQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewFleetDeployment(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/station/building/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<StationBuildingQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewStationBuilding(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/station/platform/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<ThinPlatformQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewThinPlatform(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/station/wide-platform/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<WidePlatformQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewWidePlatform(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/station/platform/properties", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<PlatformPropertiesQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PlatformProperties(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/railway/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<RailwayQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewRailway(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        app.MapPost("/api/railway/slope/preview", async Task<IResult> (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            if (!Compatible(context)) return Results.StatusCode(426);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk,0,count);
                }
                var query = JsonSerializer.Deserialize<SlopeQuery>(buffer.ToArray(),NetworkSession.Json);
                if (query == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(session.PreviewSlope(query),NetworkSession.Json);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message },statusCode:409); }
        });
        async Task<IResult> CreationOperation<T>(HttpContext context, Func<T, ParticipantContext, Task<object>> action)
        {
            var participant = Identity(context);
            if (participant == null) return Results.Unauthorized();
            if (participant.Role != ParticipantRole.Owner) return Results.StatusCode(403);
            try
            {
                using var buffer = new MemoryStream(); var chunk = new byte[1024]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > MaximumMessageBytes) return Results.StatusCode(413);
                    buffer.Write(chunk, 0, count);
                }
                var input = JsonSerializer.Deserialize<T>(buffer.ToArray(), NetworkSession.Json);
                if (input == null) return Results.BadRequest(new { code = "invalidInput" });
                return Results.Json(await action(input, participant), NetworkSession.Json);
            }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (GeographyException ex) { return Results.BadRequest(new { code = "geographyImpossible", constraint = ex.Constraint }); }
            catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidDataException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message is "staleEpoch" or "staleRevision" or "creationBusy" ? ex.Message : "staleCandidate" }, statusCode: 409); }
            catch (OperationCanceledException) { return Results.Json(new { code = "creationFailed" }, statusCode: 408); }
        }
        app.MapPost("/api/worlds/empty/preview", async Task<IResult> (HttpContext context) => await CreationOperation<EmptyPreviewRequest>(context,
            async (input, participant) => await session.PreviewEmpty(input, participant, context.RequestAborted)));
        app.MapPost("/api/worlds/empty/commit", async Task<IResult> (HttpContext context) => await CreationOperation<EmptyCommitRequest>(context,
            async (input, participant) => { await session.CommitEmpty(input, participant); return new { status = "confirmed" }; }));
        app.MapPost("/api/worlds/empty/cancel", async Task<IResult> (HttpContext context) => await CreationOperation<Guid>(context,
            async (input, participant) => { await session.CancelEmpty(input, participant); return new { status = "cancelled" }; }));
        app.MapPost("/api/worlds/generated/preview", async Task<IResult> (HttpContext context) => await CreationOperation<GeneratedPreviewRequest>(context,
            async (input, participant) => await session.PreviewGenerated(input, participant, context.RequestAborted)));
        app.MapGet("/api/worlds/heightmap/providers", () => Results.Json(FixedHeightmaps.Read(plugins), NetworkSession.Json));
        app.MapGet("/api/worlds/generated/forests", () => Results.Json(WorldCreationCapabilities.ForestChoices(), NetworkSession.Json));
        app.MapPost("/api/worlds/heightmap/preview", async Task<IResult> (HttpContext context) =>
        {
            var identity = Identity(context);
            if (identity == null) return Results.Unauthorized();
            if (identity.Role != ParticipantRole.Owner) return Results.StatusCode(403);
            try
            {
                using var lease = await session.ReserveCreationWorker(identity);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                deadline.CancelAfter(TimeSpan.FromSeconds(WorldCreationJob.WorkerDeadlineSeconds));
                var query = context.Request.Query;
                if (!int.TryParse(query["h"], out var h) || !int.TryParse(query["v"], out var v) || !int.TryParse(query["z"], out var z) ||
                    !int.TryParse(query["water"], out var water) || !long.TryParse(query["epoch"], out var epoch) || !long.TryParse(query["revision"], out var revision))
                    return Results.BadRequest(new { code = "invalidInput" });
                var options = new HeightmapPreviewOptions(query["name"].ToString(), new(h, v, z), water, epoch, revision,
                    query.ContainsKey("provider") ? query["provider"].ToString() : null);
                if (!WorldCreationProfile.Validate(new EmptyWorldRequest(WorldCreationProfile.Id, options.Name, options.Dimensions, water)).IsEmpty)
                    return Results.BadRequest(new { code = "invalidInput" });
                byte[] bytes;
                string? mediaType = context.Request.ContentType is "application/octet-stream" ? null : context.Request.ContentType;
                if (options.Provider != null)
                {
                    var map = FixedHeightmaps.Read(plugins).SingleOrDefault(m => m.Binding.ContributionId == options.Provider);
                    if (map == null || map.Bytes > WorldCreationProfile.MaxImageBytes) return Results.BadRequest(new { code = "invalidInput" });
                    bytes = plugins.ReadFile(map.ImageUrl, WorldCreationProfile.MaxImageBytes) ?? throw new ArgumentException(); mediaType = null;
                }
                else
                {
                    if (context.Request.ContentLength is > WorldCreationProfile.MaxImageBytes) return Results.Json(new { code = "image-bytes" }, statusCode: 413);
                    using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
                    while ((count = await context.Request.Body.ReadAsync(chunk, deadline.Token)) > 0)
                    {
                        if (buffer.Length + count > WorldCreationProfile.MaxImageBytes) return Results.Json(new { code = "image-bytes" }, statusCode: 413);
                        buffer.Write(chunk, 0, count);
                    }
                    bytes = buffer.ToArray();
                }
                return Results.Json(await session.PreviewHeightmap(options, bytes, mediaType, plugins, identity, lease, deadline.Token), NetworkSession.Json);
            }
            catch (HeightmapException ex) { return Results.BadRequest(new { code = ex.Code }); }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or OverflowException)
            { return Results.BadRequest(new { code = "invalidInput" }); }
            catch (InvalidOperationException ex)
            { return Results.Json(new { code = ex.Message is "creationBusy" or "staleEpoch" or "staleRevision" ? ex.Message : "staleCandidate" }, statusCode: 409); }
            catch (OperationCanceledException) { return Results.Json(new { code = "creationFailed" }, statusCode: 408); }
        });
        // Save HTTP requests use the same trusted owner credential and host FIFO as socket commands.
        app.MapGet("/api/saves", (HttpContext context) => Authorized(context)
            ? Results.Ok(session.SaveSlots()) : Results.Unauthorized());
        app.MapGet("/api/saves/status", (HttpContext context) => Authorized(context)
            ? Results.Ok(new { savingAvailable = session.SavingAvailable, loadingAvailable = session.SavingAvailable, startupError = session.StartupSaveError }) : Results.Unauthorized());
        app.MapGet("/api/saves/download", async (HttpContext context) =>
        {
            if (!Authorized(context)) return Results.Unauthorized();
            try { return Results.File(await session.CaptureSave(participant: Identity(context)), "application/json", "freetrain-online-save.json"); }
            catch (InvalidDataException) { return Results.Json(new { code = "savingUnavailable" }, statusCode: 409); }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (InvalidOperationException) { return Results.Json(new { code = "queueFull" }, statusCode: 503); }
        });
        async Task<IResult> SaveOperation(HttpContext context, Func<long, ParticipantContext, Task> action)
        {
            var participant = Identity(context);
            if (participant == null) return Results.Unauthorized();
            if (participant.Role != ParticipantRole.Owner) return Results.StatusCode(403);
            if (!long.TryParse(context.Request.Query["expectedWorldEpoch"], out var epoch) || epoch < 0)
                return Results.BadRequest(new { code = "invalidInput" });
            try { await action(epoch, participant); return Results.Ok(new { status = "confirmed" }); }
            catch (InvalidDataException ex)
            {
                var code = ex.Message is "saveTooLarge" or "incompatibleSave" or "savingUnavailable" ? ex.Message : "invalidSave";
                return Results.Json(new { code }, statusCode: code == "saveTooLarge" ? 413 : code == "savingUnavailable" ? 409 : 400);
            }
            catch (FileNotFoundException) { return Results.NotFound(new { code = "missingSave" }); }
            catch (InvalidOperationException ex) { return Results.Json(new { code = ex.Message == "staleEpoch" ? "staleEpoch" : "queueFull" }, statusCode: 409); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { return Results.Json(new { code = "saveFailed" }, statusCode: 503); }
        }
        app.MapPost("/api/saves/validate", async Task<IResult> (HttpContext context) => await SaveOperation(context, async (epoch, participant) =>
        {
            if (!await saveInputs.WaitAsync(0)) throw new InvalidOperationException("queueFull");
            try
            {
                if (context.Request.ContentLength is > SaveCodec.MaximumBytes) throw new InvalidDataException("saveTooLarge");
                using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > SaveCodec.MaximumBytes) throw new InvalidDataException("saveTooLarge");
                    buffer.Write(chunk,0,count);
                }
                if (session.Bootstrap().WorldEpoch != epoch) throw new InvalidOperationException("staleEpoch");
                await session.ValidateSave(buffer.ToArray(), epoch, participant);
            }
            finally { saveInputs.Release(); }
        }));
        app.MapPost("/api/saves/upload", async Task<IResult> (HttpContext context) => await SaveOperation(context, async (epoch, participant) =>
        {
            if (!await saveInputs.WaitAsync(0)) throw new InvalidOperationException("queueFull");
            try
            {
                if (context.Request.ContentLength is > SaveCodec.MaximumBytes) throw new InvalidDataException("saveTooLarge");
                using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
                while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
                {
                    if (buffer.Length + count > SaveCodec.MaximumBytes) throw new InvalidDataException("saveTooLarge");
                    buffer.Write(chunk, 0, count);
                }
                await session.LoadSave(buffer.ToArray(), epoch, participant);
            }
            finally { saveInputs.Release(); }
        }));
        app.MapPost("/api/saves/{id}/save", (HttpContext context, string id) => SaveOperation(context, async (epoch, participant) =>
        {
            if (session.Bootstrap().WorldEpoch != epoch) throw new InvalidOperationException("staleEpoch");
            if (!SaveStorage.Slots.Contains(id)) throw new InvalidDataException("invalidSlot");
            await session.Checkpoint(id, epoch, participant);
        }));
        app.MapPost("/api/saves/{id}/load", (HttpContext context, string id) => SaveOperation(context, (epoch, participant) => session.LoadSlot(id, epoch, participant)));
        app.MapGet("/api/session", async (HttpContext context) =>
        {
            if (!Authorized(context)) { context.Response.StatusCode = 401; return; }
            if (!Compatible(context)) { context.Response.StatusCode = 426; return; }
            if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
            var peer = session.Connect(DateTimeOffset.UtcNow, Identity(context), context.Request.Cookies[CookieName]);
            if (peer == null) { context.Response.StatusCode = 409; return; }
            try
            {
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, peer.Stop.Token);
                var sender = Send(socket, peer, linked.Token);
                var receiver = Receive(socket, peer, session, linked.Token, app.Logger);
                await Task.WhenAny(sender, receiver);
                linked.Cancel(); socket.Abort();
                try { await Task.WhenAll(sender, receiver); }
                catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or JsonException or InvalidDataException) { }
            }
            finally { session.Disconnect(peer, DateTimeOffset.UtcNow); }
        });
    }
    private sealed record ParticipantAccess(string Id, string Role, string Secret);
    private sealed record AccessRequest(string Secret);
    private static bool Equal(string? left, string? right) => left != null && right != null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static bool Permitted(HttpContext context, IConfiguration configuration, string? secret)
    {
        if (secret == null && (!IPAddress.TryParse(context.Connection.RemoteIpAddress?.ToString(), out var ip) || !IPAddress.IsLoopback(ip)
            || !(context.Request.Host.Host is "127.0.0.1" or "localhost" or "[::1]" or "::1"))) return false;
        var origin = context.Request.Headers.Origin.ToString();
        if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site") return false;
        if (origin.Length == 0) return true; // CLI clients still need the operator credential remotely.
        var own = $"{context.Request.Scheme}://{context.Request.Host}";
        var allowed = configuration.GetSection("FreeTrainOnline:AllowedOrigins").Get<string[]>() ?? [];
        return origin == own || allowed.Contains(origin, StringComparer.Ordinal);
    }
    private static async Task Send(WebSocket socket, NetworkSession.Peer peer, CancellationToken cancellationToken)
    {
        await foreach (var message in peer.Output.Reader.ReadAllAsync(cancellationToken))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(message is JoinMessage or SnapshotMessage ? 30 : 2));
            if (message is JoinMessage or SnapshotMessage)
            {
                peer.StateTransferInProgress = true;
                await stateWriters.WaitAsync(timeout.Token);
                try
                {
                    var packed = StateTransfer.Serialize(message);
                    var terrain = message is JoinMessage j ? j.Snapshot.Terrain : ((SnapshotMessage)message).Terrain;
                    if (packed.Length > StateTransfer.InlineBytes || terrain?.Cells.Length > 4096)
                    {
                        foreach (var chunk in StateTransfer.Chunks(message, packed))
                            await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes<WireMessage>(chunk, NetworkSession.Json), WebSocketMessageType.Text, true, timeout.Token);
                        continue;
                    }
                }
                finally { stateWriters.Release(); peer.StateTransferInProgress = false; }
            }
            await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes<WireMessage>(message, NetworkSession.Json), WebSocketMessageType.Text, true, timeout.Token);
        }
    }
    private static async Task Receive(WebSocket socket, NetworkSession.Peer peer, NetworkSession session, CancellationToken cancellationToken, ILogger logger)
    {
        var buffer = new byte[MaximumMessageBytes + 1];
        while (!cancellationToken.IsCancellationRequested)
        {
            var length = 0;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            WebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), timeout.Token);
                if (frame.MessageType == WebSocketMessageType.Close)
                {
                    if((int?)frame.CloseStatus is 4001 or 4002)
                        logger.LogWarning("Client requested session recovery ({Code}): {Reason}",(int?)frame.CloseStatus,
                            frame.CloseStatusDescription?.Replace('\r',' ').Replace('\n',' ').Replace('\t',' '));
                    return;
                }
                if (frame.MessageType != WebSocketMessageType.Text || (length += frame.Count) > MaximumMessageBytes) throw new InvalidDataException("Message limit/type.");
            } while (!frame.EndOfMessage);
            if (!session.Touch(peer, DateTimeOffset.UtcNow)) return;
            var message = JsonSerializer.Deserialize<WireMessage>(buffer.AsSpan(0, length), NetworkSession.Json);
            switch (message)
            {
                case CommandMessage command when Valid(command):
                    var result = await session.Submit(peer, command, DateTimeOffset.UtcNow).WaitAsync(cancellationToken);
                    session.Reply(peer, result); break;
                case ResyncMessage: session.Resync(peer); break;
                case HeartbeatMessage: break;
                case InputResetMessage: session.ResetInput(peer); break;
                default: throw new InvalidDataException("Unexpected message.");
            }
        }
    }
    private static bool Valid(CommandMessage command) => !string.IsNullOrWhiteSpace(command.RequestId) && command.RequestId.Length <= 128 && command.ExpectedWorldEpoch >= 0 && command.Payload switch
    {
        DeployFleetCommand deploy => deploy.Query is {} q && q.TrainId>0 && q.ExpectedWorldEpoch>=0 && q.ExpectedRevision>=0 && q.ExpectedMinute>=0 &&
            q.ExpectedContentVersion==ProtocolIdentity.MixedContent && (q.Action=="place"&&q.Tail!=null||(q.Action is "withdraw" or "reverse")&&q.Tail==null&&!q.Reverse),
        TradeFleetCommand trade => trade.Query != null && trade.ExpectedPrice >= 0,
        ManageFleetControllerCommand manage => manage.ControllerId>=0 && manage.ExpectedRevision>=0 &&
            manage.Action is "create" or "rename" or "copy" or "configure" or "delete" or "cleanJunctions" && manage.Name is {Length: <= 256} &&
            (manage.ExpectedGeometryRevision==null || manage.ExpectedGeometryRevision>=0) &&
            (manage.Junctions==null || manage.Junctions.Length<=1 && manage.Junctions.All(j=>j!=null && j.Location!=null && j.DefaultRoute is "straight" or "curve")) &&
            (manage.Harbors==null || manage.Harbors.Length<=1 && manage.Harbors.All(h=>h!=null && h.Location!=null && h.Kind is "pass" or "fixed" or "daily" or "advanced")),
        EditFleetRuleCommand edit => edit.ControllerId>0 && edit.ExpectedRevision>=0 && edit.Location!=null &&
            edit.SettingKind is "harbor" or "junction" && edit.Operation is "add" or "replace" or "copy" or "move" or "remove" or "clear" or "append" &&
            (edit.RulesBase64==null || edit.RulesBase64.Length<=8192) &&
            edit.Index is >=-1 and <1024 && edit.Offset is >=-1 and <=1 && (edit.ExpectedGeometryRevision==null || edit.ExpectedGeometryRevision>=0),
        AssignFleetControllerCommand assign => assign.TargetKind is "train" or "group" && assign.TargetId>0 &&
            assign.ControllerId>0 && assign.ExpectedRevision>=0 && assign.ExpectedControllerRevision>=0,
        ManageFleetGroupCommand group => group.Action is "create" or "rename" or "move" && group.TargetKind is "train" or "group" &&
            group.TargetId>=0 && group.ExpectedRevision>=0 && group.Name is {Length: <=256} &&
            (group.ParentId==null || group.ParentId>=0) && (group.ExpectedParentRevision==null || group.ExpectedParentRevision>=0),
        ConfigureFleetColorCommand color => color.ExpectedRevision>=0 && color.Configuration is {} c && c.Template!=null &&
            new[]{c.Body,c.Line1,c.Line2,c.Line3}.All(v=>v is {Length:7}),
        RenameFleetCommand rename => rename.TrainId > 0 && rename.ExpectedRevision >= 0 && rename.Name is {Length: <= 256},
        AcquireControlCommand acquire => !string.IsNullOrWhiteSpace(acquire.TrainId),
        ReleaseControlCommand release => !string.IsNullOrWhiteSpace(release.TrainId) && !string.IsNullOrWhiteSpace(release.LeaseId),
        EditPlatformPropertiesCommand edit => edit.Query != null && edit.Query.PlatformId > 0 &&
            edit.Query.ExpectedWorldEpoch >= 0 && edit.Query.ExpectedGeometryRevision >= 0 && edit.Query.ExpectedPlatformRevision >= 0 &&
            edit.Name is {Length: <= 128} && edit.Bell is {PackId.Length: > 0 and <= 512, ContributionId.Length: > 0 and <= 512, RecipeId.Length: > 0 and <= 512} &&
            (edit.HostId == null || edit.HostId > 0) && edit.ExpectedHostRevision >= 0 && edit.ExpectedStationRevision >= 0,
        RenameStationCommand rename => rename.ExpectedRevision >= 0 && !string.IsNullOrWhiteSpace(rename.StationId) && rename.Name != null,
        RemoveTrackCommand remove => remove.ExpectedRevision >= 0 && remove.Position != null,
        RemoveObjectCommand remove => remove.Query != null && remove.Query.Position != null &&
            remove.Query.ExpectedWorldEpoch >= 0 && remove.Query.ExpectedGeometryRevision >= 0 &&
            (remove.Query.End == null ? remove.Query.ExpectedEntityId > 0 && remove.Query.ExpectedEntityRevision >= 0 :
                remove.Query.ExpectedEntityId == 0 && remove.Query.ExpectedEntityRevision == 0) &&
            remove.Query.ExpectedContentVersion is TerrainProfile.ContentVersion or ProtocolIdentity.MixedContent,
        SetAuthoringModeCommand mode => mode.ExpectedGeometryRevision >= 0,
        EditSlopeCommand edit => edit.Query != null && edit.Query.Position != null &&
            edit.Query.ExpectedWorldEpoch >= 0 && edit.Query.ExpectedGeometryRevision >= 0 &&
            edit.Query.ExpectedContentVersion is "terrain-content-1" or "mixed-railway-content-1" &&
            edit.Query.Action is "lay" or "remove" && edit.Query.Climb is 0 or 2 or 4 or 6,
        EditStationBuildingCommand edit => edit.Query != null && edit.Query.Position != null && edit.ExpectedCost >= 0 &&
            edit.Query.ExpectedWorldEpoch >= 0 && edit.Query.ExpectedGeometryRevision >= 0 && edit.Query.ExpectedMinute >= 0 &&
            edit.Query.ExpectedContentVersion is "terrain-content-1" or "mixed-railway-content-1" &&
            (edit.Query.Action == "place" && edit.Query.Definition is {PackId.Length: > 0 and <= 512, ContributionId.Length: > 0 and <= 512, RecipeId.Length: > 0 and <= 512} && edit.Query.StationId == 0 && edit.Query.ExpectedStationRevision == 0 ||
             edit.Query.Action == "remove" && edit.Query.Definition == null && edit.Query.StationId > 0 && edit.Query.ExpectedStationRevision >= 0),
        EditThinPlatformCommand edit => edit.Query != null && edit.Query.Position != null && edit.ExpectedCost >= 0 &&
            edit.Query.ExpectedWorldEpoch >= 0 && edit.Query.ExpectedGeometryRevision >= 0 && edit.Query.ExpectedMinute >= 0 &&
            edit.Query.ExpectedContentVersion is "terrain-content-1" or "mixed-railway-content-1" &&
            edit.Query.Direction is 0 or 2 or 4 or 6 && edit.Query.Length is >= 1 and <= 100 &&
            (edit.Query.Action == "place" && edit.Query.PlatformId == 0 && edit.Query.ExpectedPlatformRevision == 0 ||
             edit.Query.Action == "remove" && edit.Query.PlatformId > 0 && edit.Query.ExpectedPlatformRevision >= 0),
        EditWidePlatformCommand edit => edit.Query != null && edit.Query.Position != null && edit.ExpectedCost >= 0 &&
            edit.Query.ExpectedWorldEpoch >= 0 && edit.Query.ExpectedGeometryRevision >= 0 && edit.Query.ExpectedMinute >= 0 &&
            edit.Query.ExpectedContentVersion is "terrain-content-1" or "mixed-railway-content-1" &&
            edit.Query.Direction is 0 or 2 or 4 or 6 && edit.Query.Length is >= 1 and <= 100 &&
            (edit.Query.Action == "place" && edit.Query.PlatformId == 0 && edit.Query.ExpectedPlatformRevision == 0 ||
             edit.Query.Action == "remove" && edit.Query.PlatformId > 0 && edit.Query.ExpectedPlatformRevision >= 0),
        EditRailwayCommand edit => edit.Query != null && edit.Query.From != null && edit.Query.To != null &&
            edit.Query.ExpectedWorldEpoch >= 0 && edit.Query.ExpectedGeometryRevision >= 0 &&
            edit.Query.ExpectedContentVersion is "terrain-content-1" or "mixed-railway-content-1" &&
            edit.Query.Action is "lay" or "remove" && edit.Query.Routing is "closest" or "source" or "alternate" &&
            edit.Query.Infrastructure is "normal" or "bridge" or "girder" or "tunnel" or "garage" or "signal" or "pole" or "stationary" or "speed" &&
            edit.Query.StationaryType is {Length: <= 512} &&
            edit.Query.Speed is "low" or "medium" or "fast" && edit.Query.SpeedClimb is 0 or 2 or 4 or 6 &&
            (edit.Query.Infrastructure != "speed" || !edit.Query.SpeedSlope || edit.Query.From == edit.Query.To) &&
            PoleRailwayConstruction.IsBundledType(edit.Query.PoleType) &&
            edit.Query.SignalDirection is 0 or 2 or 4 or 6 &&
            edit.Query.SignalType is SignalRailwayConstruction.ThreeAspect or SignalRailwayConstruction.Shunting &&
            edit.Query.TunnelRouting is "exact" or "portals" or "portals-auto",
        EditCornerCommand edit => edit.Query != null && edit.Query.Position != null && edit.Query.ExpectedWorldEpoch >= 0 && edit.Query.ExpectedGeometryRevision >= 0 && (edit.Query.Action is "raise" or "lower" or "placeTerrace" or "removeTerrace" or "placeCut" or "removeCut" or "plantForest" or "placeCreek" || edit.Query.Action == "repair" && edit.Query.Rectangle != null),
        BuildTrackCommand build => build.Position != null && build.Shape != null,
        SetControllerCommand controller => controller.ExpectedRevision >= 0 && !string.IsNullOrWhiteSpace(controller.TrainId) && !string.IsNullOrWhiteSpace(controller.LeaseId) && controller.Controller is "manual" or "automatic",
        ControllerInputCommand input => input.InputSequence >= 0 && !string.IsNullOrWhiteSpace(input.TrainId) && !string.IsNullOrWhiteSpace(input.LeaseId),
        SetPlaybackRateCommand rate => SessionHost.IsPlaybackRate(rate.Rate),
        RunCommand or PauseCommand or StepCommand => true,
        _ => false
    };
}
