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
builder.Services.AddSingleton<ManifestRepository>(sp =>
    new NpgsqlManifestRepository(
        sp.GetRequiredService<ILogger<NpgsqlManifestRepository>>(),
        connectionString));
builder.Services.AddSingleton<DbNotifyRepository>(sp =>
    new NpgsqlDbNotifyRepository(
        sp.GetRequiredService<ILogger<NpgsqlDbNotifyRepository>>(),
        connectionString));
builder.Services.AddSingleton<SqlAttentionLogsRepository>(sp =>
    new NpgsqlSqlAttentionLogsRepository(
        sp.GetRequiredService<ILogger<NpgsqlSqlAttentionLogsRepository>>(),
        connectionString));
builder.Services.AddSingleton<HubAttractorExplorationRuntime>();

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
builder.Services.AddSingleton<AdminRuntime>(sp =>
    new AdminRuntime(
        sp.GetRequiredService<ILogger<AdminRuntime>>(),
        sp.GetRequiredService<ContextRouteRepository>(),
        sp.GetRequiredService<RegistrarValidationService>(),
        sp.GetRequiredService<PackageGeneratorRuntime>(),
        sp.GetRequiredService<UiTopologyRepository>(),
        sp.GetRequiredService<SeedRuntime>()));
builder.Services.AddSingleton<TopologyFunctionBinder>();
builder.Services.AddSingleton<OutputLaneRouter>(sp =>
    new OutputLaneRouter(
        sp.GetRequiredService<ILogger<OutputLaneRouter>>(),
        sp.GetRequiredService<DbNotifyRepository>()));
builder.Services.AddSingleton<TargetDispatchOverride>(sp =>
    new TargetDispatchOverride(
        sp.GetRequiredService<ILogger<TargetDispatchOverride>>(),
        sp.GetRequiredService<TopologyRepository>(),
        sp.GetRequiredService<AdminRuntime>()));
builder.Services.AddSingleton<RuntimeExecutor>(sp =>
    new RuntimeExecutor(
        sp.GetRequiredService<ILogger<RuntimeExecutor>>(),
        sp.GetRequiredService<OperationVectorResolver>(),
        sp.GetRequiredService<AttractorResolver>(),
        sp.GetRequiredService<StructureMapResolver>(),
        sp.GetRequiredService<PackageResolver>(),
        sp.GetRequiredService<SchemaResolver>(),
        sp.GetRequiredService<EmissionBuilder>(),
        sp.GetRequiredService<SemanticMapper>(),
        sp.GetRequiredService<DiffLogRepository>(),
        sp.GetRequiredService<RuntimeGuard>(),
        sp.GetRequiredService<ContextRouteRecommendationResolver>(),
        sp.GetRequiredService<OutputLaneRouter>()));
builder.Services.AddSingleton<AdminRuntimeDispatchAdapter>(sp =>
    new AdminRuntimeDispatchAdapter(
        sp.GetRequiredService<AdminRuntime>(),
        sp.GetRequiredService<OperationVectorResolver>()));
builder.Services.AddSingleton<SseProjectionRuntime>();
builder.Services.AddSingleton<ManifestDispatcher>(sp =>
{
    var handlers = new Dictionary<string, IDispatchableRuntime>
    {
        ["topology_transform_runtime"] = sp.GetRequiredService<RuntimeExecutor>(),
        ["admin_runtime"]              = sp.GetRequiredService<AdminRuntimeDispatchAdapter>(),
        ["sse_projection_runtime"]     = sp.GetRequiredService<SseProjectionRuntime>(),
    };
    return new ManifestDispatcher(
        sp.GetRequiredService<ILogger<ManifestDispatcher>>(),
        handlers,
        sp.GetRequiredService<OperationVectorResolver>(),
        sp.GetRequiredService<TargetDispatchOverride>(),
        sp.GetRequiredService<ManifestRepository>());
});
builder.Services.AddSingleton<LogRetentionRuntime>();
builder.Services.AddSingleton<PackageGeneratorRuntime>();

// Seed Runtime — Issue #84.
// SEED_STORAGE_PATH defaults to /storage (docker-compose volume mount).
// If not set, SeedRuntime is still registered but AdminRuntime logs SEED_RUNTIME_NOT_AVAILABLE.
var seedStoragePath = Environment.GetEnvironmentVariable("SEED_STORAGE_PATH") ?? "/storage";
builder.Services.AddSingleton<SeedJsonRepository>(sp =>
    new SeedJsonRepository(
        sp.GetRequiredService<ILogger<SeedJsonRepository>>(),
        seedStoragePath));
builder.Services.AddSingleton<SeedImportApplyRepository>(sp =>
    new SeedImportApplyRepository(connectionString));
builder.Services.AddSingleton<SeedRuntime>();

// ---------------------------------------------------------------------------
// SSE broadcaster — fan-out projection events to all connected SSE clients
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<SseEventBroadcaster>();

// ---------------------------------------------------------------------------
// Endpoint layer
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<DispatchEndpoint>();
builder.Services.AddSingleton<SseEndpoint>(sp =>
    new SseEndpoint(
        sp.GetRequiredService<ILogger<SseEndpoint>>(),
        sp.GetRequiredService<SseEventBroadcaster>()));
builder.Services.AddSingleton<AuthEndpoint>();
builder.Services.AddSingleton<LegacyChangeIntakeEndpoint>();
builder.Services.AddSingleton<JwtGuard>();

// ---------------------------------------------------------------------------
// Scheduler layer — unified cron/hook/client trigger alignment
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<RuntimeTimelineScheduler>();

// ---------------------------------------------------------------------------
// Background services
// ---------------------------------------------------------------------------
builder.Services.AddHostedService(sp => sp.GetRequiredService<RuntimeTimelineScheduler>());
builder.Services.AddHostedService<RetentionScheduler>();
builder.Services.AddHostedService<SystemOperationCiScheduler>();
builder.Services.AddHostedService<SqlAttentionScheduler>();
builder.Services.AddHostedService(sp => new DbNotifyListener(
    sp.GetRequiredService<ILogger<DbNotifyListener>>(),
    connectionString,
    sp.GetRequiredService<RuntimeTimelineScheduler>()));

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


// POST /intake/legacy-change — existing-system change-event intake boundary.
app.MapPost("/intake/legacy-change", (
    HttpContext ctx,
    LegacyChangeIntakeRequestDto request,
    LegacyChangeIntakeEndpoint intake,
    JwtGuard jwtGuard) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new LegacyChangeIntakeResponseDto(false, null, authErrors), statusCode: 401);

    var result = intake.Handle(request);
    return Results.Json(result, statusCode: result.Accepted ? 202 : 422);
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

// GET /sse — SSE projection lane.
// Streams projection events from DbNotifyListener via SseEventBroadcaster.
// Per pipeline-continuity-ssot.yaml sse_projection_lane.
app.MapGet("/sse", async (HttpContext ctx, SseEndpoint sse) =>
{
    await sse.StreamAsync(ctx.Response, ctx.RequestAborted);
});

app.Run();
