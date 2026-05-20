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
builder.Services.AddSingleton<UiTopologyRepository>(sp =>
    new NpgsqlUiTopologyRepository(
        sp.GetRequiredService<ILogger<NpgsqlUiTopologyRepository>>(),
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
builder.Services.AddSingleton<SystemOperationCiRuntime>();
builder.Services.AddSingleton<ContextRouteRecommendationResolver>();
builder.Services.AddSingleton<TopologyVectorRuntime>();
builder.Services.AddSingleton<RegistrarValidationService>();
builder.Services.AddSingleton<AdminRuntime>();
builder.Services.AddSingleton<RuntimeExecutor>();
builder.Services.AddSingleton<ManifestDispatcher>();
builder.Services.AddSingleton<LogRetentionRuntime>();
builder.Services.AddSingleton<PackageGeneratorRuntime>();

// ---------------------------------------------------------------------------
// Endpoint layer
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DispatchEndpoint>();
builder.Services.AddSingleton<SseEndpoint>();
builder.Services.AddSingleton<AuthEndpoint>();
builder.Services.AddSingleton<JwtGuard>();

// ---------------------------------------------------------------------------
// Scheduler layer — client-flow trigger alignment
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<RuntimeTimelineScheduler>();

// ---------------------------------------------------------------------------
// Background services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService<RetentionScheduler>();
builder.Services.AddHostedService<SystemOperationCiScheduler>();

// ---------------------------------------------------------------------------
// HTTP layer
// ---------------------------------------------------------------------------
var port = Environment.GetEnvironmentVariable("BACKEND_PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

string? ExtractBearerToken(HttpContext ctx)
{
    var h = ctx.Request.Headers.Authorization.FirstOrDefault();
    return h?.StartsWith("Bearer ", StringComparison.Ordinal) == true ? h[7..] : null;
}

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
    var token = ExtractBearerToken(ctx);
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

// GET /sse — SSE projection lane skeleton.
// Streams keep-alive ping events. Actual topology events require notify_listen
// implementation (see pipeline-continuity-ssot.yaml sse_projection_lane).
app.MapGet("/sse", async (HttpContext ctx, SseEndpoint sse) =>
{
    await sse.StreamAsync(ctx.Response, ctx.RequestAborted);
});

app.Run();
