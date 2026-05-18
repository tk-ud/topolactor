using Microsoft.AspNetCore.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Topolactor.Endpoint;
using Topolactor.Guard;
using Topolactor.Mapper;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Topolactor.Schema;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

// ---------------------------------------------------------------------------
// Repository layer — DATABASE_URL is required (no in-memory fallback)
// ---------------------------------------------------------------------------
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? string.Empty;
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("DATABASE_URL is required. Backend runtime must connect to PostgreSQL; in-memory fallback is not allowed.");

builder.Services.AddSingleton<TopologyRepository>(sp =>
    new NpgsqlTopologyRepository(
        sp.GetRequiredService<ILogger<NpgsqlTopologyRepository>>(),
        connectionString));
builder.Services.AddSingleton<ContextRouteRepository>(sp =>
    new NpgsqlContextRouteRepository(
        sp.GetRequiredService<ILogger<NpgsqlContextRouteRepository>>(),
        connectionString));

// ---------------------------------------------------------------------------
// Runtime layer
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DiffLogRepository>(sp =>
    new NpgsqlDiffLogRepository(
        sp.GetRequiredService<ILogger<NpgsqlDiffLogRepository>>(),
        connectionString));
builder.Services.AddSingleton<OperationVectorResolver>();
builder.Services.AddSingleton<AttractorResolver>();
builder.Services.AddSingleton<StructureMapResolver>();
builder.Services.AddSingleton<PackageResolver>();
builder.Services.AddSingleton<SchemaResolver>();
builder.Services.AddSingleton<EmissionBuilder>();
builder.Services.AddSingleton<SemanticMapper>();
builder.Services.AddSingleton<RuntimeGuard>();
builder.Services.AddSingleton<ContextVectorBuilder>();
builder.Services.AddSingleton<ContextNeighborSearch>();
builder.Services.AddSingleton<ContextRouteRecommendationResolver>();
builder.Services.AddSingleton<TopologyVectorRuntime>();
builder.Services.AddSingleton<RegistrarValidationService>();
builder.Services.AddSingleton<RuntimeExecutor>();
builder.Services.AddSingleton<LogRetentionRuntime>();

// ---------------------------------------------------------------------------
// Endpoint layer
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DispatchEndpoint>();
builder.Services.AddSingleton<AuthEndpoint>();
builder.Services.AddSingleton<AdminEndpoint>();
builder.Services.AddSingleton<JwtGuard>();

// ---------------------------------------------------------------------------
// Background services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<RetentionScheduler>();

// ---------------------------------------------------------------------------
// HTTP layer
// ---------------------------------------------------------------------------
var port = Environment.GetEnvironmentVariable("BACKEND_PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Health check — used by Docker healthcheck and nginx upstream check
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// POST /dispatch — always JWT-guarded.
// JwtGuard.Validate returns AUTH_JWT_SECRET_NOT_CONFIGURED when DEMO_JWT_SECRET
// is not set, so the endpoint is never silently unauthenticated.
app.MapPost("/dispatch", async (
    HttpContext ctx,
    EndpointRequestDto request,
    DispatchEndpoint dispatch,
    JwtGuard jwtGuard) =>
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var token = authHeader?.StartsWith("Bearer ", StringComparison.Ordinal) == true
        ? authHeader[7..]
        : null;
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new EndpointResponseDto(false, null, authErrors), statusCode: 401);

    var result = await dispatch.HandleAsync(request, ctx.RequestAborted);
    return Results.Json(result, statusCode: result.Success ? 200 : 422);
});

// POST /auth/login — demo login scaffold
app.MapPost("/auth/login", async (
    HttpContext ctx,
    LoginRequestDto request,
    AuthEndpoint auth) =>
{
    var result = await auth.HandleAsync(request, ctx.RequestAborted);
    return Results.Json(result, statusCode: result.Success ? 200 : 401);
});

// Admin routes — JWT-guarded, same guard as /dispatch.
// GET  /admin/context-token-registry  — list all tokens
// POST /admin/context-token-registry  — create a token
// POST /admin/context-token-registry/{tokenId}/deprecate — deprecate a token

app.MapGet("/admin/context-token-registry", async (
    HttpContext ctx,
    AdminEndpoint admin,
    JwtGuard jwtGuard) =>
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var token = authHeader?.StartsWith("Bearer ", StringComparison.Ordinal) == true
        ? authHeader[7..]
        : null;
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { ok = false, errors = authErrors }, statusCode: 401);

    var tokens = await admin.HandleListTokensAsync(ctx.RequestAborted);
    return Results.Json(tokens);
});

app.MapPost("/admin/context-token-registry", async (
    HttpContext ctx,
    AdminCreateTokenRequestDto request,
    AdminEndpoint admin,
    JwtGuard jwtGuard) =>
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var token = authHeader?.StartsWith("Bearer ", StringComparison.Ordinal) == true
        ? authHeader[7..]
        : null;
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { ok = false, errors = authErrors }, statusCode: 401);

    var result = await admin.HandleCreateTokenAsync(request, ctx.RequestAborted);
    int createStatus = result.Ok ? 200
        : result.ErrorCode == "DUPLICATE_LABEL_GROUP" ? 409
        : 422;
    return Results.Json(result, statusCode: createStatus);
});

// POST /admin/registry-vector-validate — validate a candidate registry ID array.
// Returns RegistryVectorValidationResult. Policy from function_parameters.
// Policy missing → 422 REGISTRY_VALIDATION_POLICY_NOT_FOUND.
// DB unavailable → 422 REGISTRY_VECTOR_VALIDATION_DB_UNAVAILABLE (blocking).
app.MapPost("/admin/registry-vector-validate", async (
    HttpContext ctx,
    AdminRegistryVectorValidateRequestDto request,
    AdminEndpoint admin,
    JwtGuard jwtGuard) =>
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var token = authHeader?.StartsWith("Bearer ", StringComparison.Ordinal) == true
        ? authHeader[7..]
        : null;
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { ok = false, errors = authErrors }, statusCode: 401);

    var (result, statusCode) = await admin.HandleValidateRegistryVectorAsync(request, ctx.RequestAborted);
    return Results.Json(result, statusCode: statusCode);
});

app.MapPost("/admin/context-token-registry/{tokenId}/deprecate", async (
    HttpContext ctx,
    string tokenId,
    AdminEndpoint admin,
    JwtGuard jwtGuard) =>
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var token = authHeader?.StartsWith("Bearer ", StringComparison.Ordinal) == true
        ? authHeader[7..]
        : null;
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { ok = false, errors = authErrors }, statusCode: 401);

    if (!Guid.TryParse(tokenId, out var tokenGuid))
        return Results.Json(new { ok = false, message = "tokenId must be a valid UUID." }, statusCode: 400);

    var (result, found) = await admin.HandleDeprecateTokenAsync(tokenGuid, ctx.RequestAborted);
    return Results.Json(result, statusCode: found ? 200 : 404);
});

app.Run();
