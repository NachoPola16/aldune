using System.Text.Json;
using Aldune.Core;

const long MaxObjectBytes = 5_000_000;

var builder = WebApplication.CreateBuilder(args);
// El tope lo aplica Kestrel mientras recibe, no solo el código de abajo: una subida sin
// Content-Length (por trozos) se leía entera en memoria antes de comprobar su tamaño.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxObjectBytes);
var app = builder.Build();

var dataPath = Environment.GetEnvironmentVariable(BrandIdentity.SyncDataDirEnvVar) ?? "/data";
var tokens = SyncTokenSet.Parse(
    Environment.GetEnvironmentVariable(BrandIdentity.SyncTokensEnvVar),
    Environment.GetEnvironmentVariable(BrandIdentity.SyncTokenEnvVar));
if (tokens.Count == 0)
    throw new InvalidOperationException(
        $"{BrandIdentity.SyncTokenEnvVar} or {BrandIdentity.SyncTokensEnvVar} must be configured.");

// Aviso y no rechazo: un servidor que ya funciona con un token corto no debe dejar de arrancar al
// actualizarse. El contenido de las notas va cifrado igual, pero un token corto se puede adivinar.
const int RecommendedTokenLength = 24;
if (tokens.Any(token => token.Length < RecommendedTokenLength))
    app.Logger.LogWarning(
        "A sync token is shorter than {Length} characters. Use a long random value, for example the output of 'openssl rand -base64 32'.",
        RecommendedTokenLength);

var objectPath = Path.Combine(dataPath, "objects");
Directory.CreateDirectory(objectPath);

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        !SyncTokenSet.Matches(context.Request.Headers.Authorization, tokens))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    format = SyncCompatibility.CurrentFormat,
    minimumFormat = SyncCompatibility.MinimumSupportedFormat
}));

app.MapGet("/api/v1/objects", () =>
{
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

app.MapGet("/api/v1/objects/{id:guid}", (Guid id) =>
{
    var path = ObjectFile(objectPath, id);
    return File.Exists(path)
        ? Results.File(path, "application/json")
        : Results.NotFound();
});

app.MapPut("/api/v1/objects/{id:guid}", async (Guid id, HttpRequest request) =>
{
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

    var finalPath = ObjectFile(objectPath, id);
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

    return Results.NoContent();
});

app.MapDelete("/api/v1/objects/{id:guid}", (Guid id) =>
{
    var path = ObjectFile(objectPath, id);
    if (File.Exists(path)) File.Delete(path);
    return Results.NoContent();
});

app.Run();

static string ObjectFile(string objectPath, Guid id) => Path.Combine(objectPath, $"{id:N}.json");

public partial class Program;