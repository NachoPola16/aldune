using System.Text.Json;
using Aldune.Core;
using Aldune.SyncServer;

const long MaxObjectBytes = 5_000_000;
const string SpaceItemKey = "aldune.objects-path";

var builder = WebApplication.CreateBuilder(args);
// El tope lo aplica Kestrel mientras recibe, no solo el código de abajo: una subida sin
// Content-Length (por trozos) se leía entera en memoria antes de comprobar su tamaño.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxObjectBytes);
var app = builder.Build();

var dataPath = Environment.GetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar) ?? "/data";
var tokens = SyncTokenSet.Parse(
    Environment.GetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar),
    Environment.GetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar));
// One store per token (ALDUNE_SYNC_SPACES): optional, alongside the original token(s) or instead of them.
IReadOnlyList<SyncSpace> spaces;
try
{
    spaces = SyncSpaces.Parse(Environment.GetEnvironmentVariable(BrandIdentity.SyncSpacesEnvVar));
    SyncSpaces.EnsureDistinct(tokens, spaces);
}
catch (FormatException ex)
{
    throw new InvalidOperationException($"{BrandIdentity.SyncSpacesEnvVar}: {ex.Message}", ex);
}

if (tokens.Count == 0 && spaces.Count == 0)
    throw new InvalidOperationException(
        $"{BrandIdentity.SyncTokenEnvVar}, {BrandIdentity.SyncTokensEnvVar} or {BrandIdentity.SyncSpacesEnvVar} must be configured.");

// El token de ejemplo de .env.example es público: quien copia el fichero sin editarlo tendría un servidor abierto.
// Es un valor literal que nadie usa a propósito, así que aquí sí se rechaza (a diferencia de un token corto).
const string ExampleToken = "replace-with-a-long-random-token";
if (tokens.Contains(ExampleToken) || spaces.Any(space => space.Token == ExampleToken))
    throw new InvalidOperationException(
        $"{BrandIdentity.SyncTokenEnvVar} or {BrandIdentity.SyncSpacesEnvVar} still has the placeholder from .env.example. Set a long random value, for example the output of 'openssl rand -base64 32'.");

// Aviso y no rechazo: un servidor que ya funciona con un token corto no debe dejar de arrancar al
// actualizarse. El contenido de las notas va cifrado igual, pero un token corto se puede adivinar.
const int RecommendedTokenLength = 24;
if (tokens.Concat(spaces.Select(space => space.Token)).Any(token => token.Length < RecommendedTokenLength))
    app.Logger.LogWarning(
        "A sync token is shorter than {Length} characters. Use a long random value, for example the output of 'openssl rand -base64 32'.",
        RecommendedTokenLength);

// Invitations (optional): with an admin token the owner can create one-use codes and a person redeems one for a space of their own.
// Without the token none of those routes exists.
var adminToken = Environment.GetEnvironmentVariable(BrandIdentity.SyncAdminTokenEnvVar)?.Trim();
if (string.IsNullOrEmpty(adminToken)) adminToken = null;
if (adminToken is not null && (adminToken == ExampleToken || tokens.Contains(adminToken) || spaces.Any(space => space.Token == adminToken)))
    throw new InvalidOperationException(
        $"{BrandIdentity.SyncAdminTokenEnvVar} must be its own long random value, different from every store token.");
var maxSpaces = int.TryParse(Environment.GetEnvironmentVariable(BrandIdentity.SyncMaxSpacesEnvVar), out var parsedMax) && parsedMax > 0 ? parsedMax : 20;
SyncRegistry? registry = null;
if (adminToken is not null)
{
    Directory.CreateDirectory(dataPath);
    registry = new SyncRegistry(Path.Combine(dataPath, "registry.json"), TimeProvider.System, maxSpaces);
}

var putGate = new SemaphoreSlim(1, 1);
// The original store keeps /data/objects (only if its token is configured, as before); each space gets /data/spaces/<name>/objects.
foreach (var path in (tokens.Count > 0 ? new[] { SyncSpaces.ObjectsPath(dataPath, null) } : Array.Empty<string>())
             .Concat(spaces.Select(space => SyncSpaces.ObjectsPath(dataPath, space.Name))))
{
    Directory.CreateDirectory(path);
    Aldune.SyncServer.DataDirectoryCheck.EnsureWritable(path);
}

if (registry is not null)
    foreach (var space in registry.Spaces)
        Directory.CreateDirectory(SyncSpaces.ObjectsPath(dataPath, space.Name));

// Límite de intentos fallidos de token por dirección (ver AuthFailureLimiter). /health queda fuera: no pide token.
var limiter = AuthFailureLimiter.FromEnvironment();
var trustProxy = AuthFailureLimiter.TrustProxyFromEnvironment();
if (!limiter.Enabled)
    app.Logger.LogWarning("{Variable}=0: failed-token rate limiting is disabled.", AuthFailureLimiter.MaxFailuresEnvVar);

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        var address = AuthFailureLimiter.ClientAddress(
            context.Request.Headers["X-Forwarded-For"].ToString(), context.Connection.RemoteIpAddress, trustProxy);
        var now = DateTimeOffset.UtcNow;
        // Antes de mirar el token: una dirección bloqueada no puede seguir probando, ni siquiera acertando.
        if (limiter.RetryAfter(address, now) is { } wait)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString();
            return;
        }

        var resolved = SyncSpaces.Resolve(context.Request.Headers.Authorization, tokens, spaces);
        if (resolved is null && registry?.Resolve(context.Request.Headers.Authorization) is { } created)
            resolved = new ResolvedSpace(created);
        if (resolved is null)
        {
            limiter.RecordFailure(address, now);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // The token decides the store: every handler below reads its folder from here, never from a shared variable.
        context.Items[SpaceItemKey] = SyncSpaces.ObjectsPath(dataPath, resolved.Value.Name);
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    format = SyncCompatibility.CurrentFormat,
    minimumFormat = SyncCompatibility.MinimumSupportedFormat
}));

app.MapGet("/api/v1/objects", (HttpContext http) =>
{
    var objectPath = ObjectsOf(http);
    var descriptors = new List<object>();
    foreach (var path in Directory.EnumerateFiles(objectPath, "*.json", SearchOption.TopDirectoryOnly))
    {
        try
        {
            var envelope = SyncEnvelopeCodec.Deserialize(File.ReadAllBytes(path));
            descriptors.Add(new
            {
                envelope.NoteId,
                envelope.Tombstone,
                envelope.UpdatedAt,
                envelope.DeviceId
            });
        }
        catch (FormatException)
        {
            // Ignorar archivos incompletos o de una versión futura; los demás siguen disponibles.
        }
        catch (JsonException)
        {
            // Igual que arriba: el servidor no debe dejar de servir todo el almacén por un archivo.
        }
    }

    return Results.Json(descriptors);
});

app.MapGet("/api/v1/objects/{id:guid}", (Guid id, HttpContext http) =>
{
    var path = ObjectFile(ObjectsOf(http), id);
    return File.Exists(path)
        ? Results.File(path, "application/json")
        : Results.NotFound();
});

app.MapPut("/api/v1/objects/{id:guid}", async (Guid id, HttpRequest request) =>
{
    var objectPath = ObjectsOf(request.HttpContext);
    if (request.ContentLength is > MaxObjectBytes)
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    using var stream = new MemoryStream();
    try
    {
        await request.Body.CopyToAsync(stream);
    }
    catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    SyncEnvelope envelope;
    try
    {
        envelope = SyncEnvelopeCodec.Deserialize(stream.ToArray());
    }
    catch (Exception ex) when (ex is FormatException or JsonException)
    {
        // JSON mal formado también es un 400: antes salía como 500, como si fallara el servidor.
        return Results.BadRequest("Unsupported or invalid sync object for this server version.");
    }

    if (envelope.NoteId != id)
        return Results.BadRequest("The object id does not match the URL.");

    // Una fecha muy futura gana a cualquier subida legítima posterior y dejaría la nota sin sincronizar para siempre.
    // Los clientes ya descartan lo fechado más de un día por delante; el servidor también lo rechaza.
    if (envelope.UpdatedAt > DateTimeOffset.UtcNow.AddDays(1))
        return Results.BadRequest("The object is dated too far in the future.");

    var finalPath = ObjectFile(objectPath, id);
    // Comprobar la versión y escribir va junto: sin esto, dos dispositivos subiendo la misma nota a la vez
    // pasaban los dos la comprobación contra la misma versión y la vieja podía acabar pisando a la nueva.
    await putGate.WaitAsync();
    try
    {
        if (File.Exists(finalPath))
        {
            try
            {
                var current = SyncEnvelopeCodec.Deserialize(await File.ReadAllBytesAsync(finalPath));
                if (SyncVersion.Compare(envelope, current) < 0) return Results.NoContent();
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                // A damaged object is recoverable by replacing it with a valid upload.
            }
        }

        var tempPath = Path.Combine(objectPath, $".{id:N}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(tempPath, stream.ToArray());
            File.Move(tempPath, finalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
    finally
    {
        putGate.Release();
    }

    return Results.NoContent();
});

// The look document (docs/specs/2026-11-01-sync-look-design.md): one opaque encrypted blob per space, beside `objects/` and never inside it,
// so versions that do not know it never list it. The server cannot read it (it is encrypted with the profile's key).
const long MaxLookBytes = 64 * 1024;

app.MapGet("/api/v1/look", (HttpContext http) =>
{
    var path = LookFile(ObjectsOf(http));
    return File.Exists(path) ? Results.File(path, "application/octet-stream") : Results.NotFound();
});

app.MapPut("/api/v1/look", async (HttpRequest request) =>
{
    if (request.ContentLength is > MaxLookBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    using var stream = new MemoryStream();
    var buffer = new byte[8192];
    int read;
    while ((read = await request.Body.ReadAsync(buffer)) > 0)
    {
        if (stream.Length + read > MaxLookBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        stream.Write(buffer, 0, read);
    }

    var path = LookFile(ObjectsOf(request.HttpContext));
    var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
        await File.WriteAllBytesAsync(temp, stream.ToArray());
        File.Move(temp, path, overwrite: true);
    }
    finally
    {
        if (File.Exists(temp)) File.Delete(temp);
    }

    return Results.NoContent();
});

app.MapDelete("/api/v1/objects/{id:guid}", (Guid id, HttpContext http) =>
{
    var path = ObjectFile(ObjectsOf(http), id);
    if (File.Exists(path)) File.Delete(path);
    return Results.NoContent();
});

if (registry is not null)
{
    // These routes sit outside /api on purpose: the store-token check above must not apply to them (a guest has no token yet).
    // Every failed attempt, at either, counts against the same per-address limit as a wrong store token.
    bool Throttled(HttpContext http, out IResult? refusal)
    {
        var address = AuthFailureLimiter.ClientAddress(http.Request.Headers["X-Forwarded-For"].ToString(), http.Connection.RemoteIpAddress, trustProxy);
        refusal = null;
        if (limiter.RetryAfter(address, DateTimeOffset.UtcNow) is not { } wait) return false;
        http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString();
        refusal = Results.StatusCode(StatusCodes.Status429TooManyRequests);
        return true;
    }

    void Failed(HttpContext http) => limiter.RecordFailure(
        AuthFailureLimiter.ClientAddress(http.Request.Headers["X-Forwarded-For"].ToString(), http.Connection.RemoteIpAddress, trustProxy), DateTimeOffset.UtcNow);

    bool IsAdmin(HttpContext http) => SyncTokenSet.Matches(http.Request.Headers.Authorization, new[] { adminToken! });

    app.MapPost("/admin/v1/invites", (HttpContext http, InviteRequest? request) =>
    {
        if (Throttled(http, out var refusal)) return refusal!;
        if (!IsAdmin(http)) { Failed(http); return Results.Unauthorized(); }
        var hours = request?.ExpiresInHours is > 0 ? request.ExpiresInHours.Value : 24 * 7;
        var invite = registry.CreateInvite(request?.Label, TimeSpan.FromHours(Math.Min(hours, 24 * 30)));
        return Results.Json(new { code = invite.Code, expiresAt = invite.ExpiresAt });
    });

    app.MapGet("/admin/v1/spaces", (HttpContext http) =>
    {
        if (Throttled(http, out var refusal)) return refusal!;
        if (!IsAdmin(http)) { Failed(http); return Results.Unauthorized(); }
        return Results.Json(registry.Spaces.Select(s => new { name = s.Name, label = s.Label, createdAt = s.CreatedAt }));
    });

    app.MapDelete("/admin/v1/spaces/{name}", (string name, HttpContext http) =>
    {
        if (Throttled(http, out var refusal)) return refusal!;
        if (!IsAdmin(http)) { Failed(http); return Results.Unauthorized(); }
        return registry.Revoke(name) ? Results.NoContent() : Results.NotFound();
    });

    app.MapPost("/invites/v1/redeem", (HttpContext http, RedeemRequest? request) =>
    {
        if (Throttled(http, out var refusal)) return refusal!;
        var result = registry.Redeem(request?.Code);
        switch (result.Status)
        {
            case RedeemStatus.Ok:
                Directory.CreateDirectory(SyncSpaces.ObjectsPath(dataPath, result.Space));
                return Results.Json(new { token = result.Token, space = result.Space });
            case RedeemStatus.Full:
                return Results.Conflict("This server cannot create more spaces.");
            default:
                // Unknown, used and expired look the same: nothing says which of them a guess was close to.
                Failed(http);
                return Results.BadRequest("The invitation is not valid.");
        }
    });
}

app.Run();

static string ObjectsOf(HttpContext http) => (string)http.Items[SpaceItemKey]!;

// Beside the objects folder (its parent), so the object listing, which reads only `objects/*.json`, never sees it.
static string LookFile(string objectPath) => Path.Combine(Path.GetDirectoryName(objectPath)!, "look.bin");

static string ObjectFile(string objectPath, Guid id) => Path.Combine(objectPath, $"{id:N}.json");

public partial class Program;

internal sealed record InviteRequest(string? Label, int? ExpiresInHours);

internal sealed record RedeemRequest(string? Code);
