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
using Topolactor.Service;

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
builder.Services.AddSingleton<ContentBundleRepository>(sp =>
    new NpgsqlContentBundleRepository(
        sp.GetRequiredService<ILogger<NpgsqlContentBundleRepository>>(),
        connectionString));
builder.Services.AddSingleton<DbNotifyRepository>(sp =>
    new NpgsqlDbNotifyRepository(
        sp.GetRequiredService<ILogger<NpgsqlDbNotifyRepository>>(),
        connectionString));
builder.Services.AddSingleton<SqlAttentionLogsRepository>(sp =>
    new NpgsqlSqlAttentionLogsRepository(
        sp.GetRequiredService<ILogger<NpgsqlSqlAttentionLogsRepository>>(),
        connectionString));
builder.Services.AddSingleton<CiAttentionGuidanceRepository>(sp =>
    new NpgsqlCiAttentionGuidanceRepository(connectionString));
builder.Services.AddSingleton<EnumDictionaryRepository>(sp =>
    new NpgsqlEnumDictionaryRepository(connectionString));
builder.Services.AddSingleton<AuthMasterRepository>(_ =>
    new NpgsqlAuthMasterRepository(connectionString));
builder.Services.AddSingleton<AuthRepository>(_ =>
    new NpgsqlAuthRepository(connectionString));
builder.Services.AddSingleton<HubAttractorExplorationRuntime>();
builder.Services.AddSingleton<SqlAttentionEvidencePromotionRuntime>();
builder.Services.AddSingleton<SqlAttentionTopologyProjectionRuntime>();

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
builder.Services.AddSingleton<ISystemCiDiagnosticRunner>(sp =>
    sp.GetRequiredService<SystemOperationCiRuntime>());
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
        sp.GetRequiredService<ISystemCiDiagnosticRunner>(),
        sp.GetRequiredService<SeedRuntime>(),
        sp.GetRequiredService<CiAttentionGuidanceRepository>(),
        sp.GetRequiredService<SseEventBroadcaster>(),
        sp.GetRequiredService<AdminImportRuntime>(),
        sp.GetRequiredService<ManifestRepository>(),
        sp.GetRequiredService<ContentBundleRepository>(),
        sp.GetRequiredService<TopologyRepository>(),
        sp.GetRequiredService<EnumDictionaryRepository>(),
        sp.GetRequiredService<AuthMasterRepository>(),
        sp.GetRequiredService<SqlAttentionLogsRepository>()));
builder.Services.AddSingleton<TopologyFunctionBinder>();
builder.Services.AddSingleton<HubNavigationResolver>(sp =>
    new HubNavigationResolver(sp.GetRequiredService<ContentBundleRepository>()));
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
        sp.GetRequiredService<SqlAttentionLogsRepository>(),
        sp.GetRequiredService<RuntimeGuard>(),
        sp.GetRequiredService<ContextRouteRecommendationResolver>(),
        sp.GetRequiredService<OutputLaneRouter>(),
        sp.GetRequiredService<HubNavigationResolver>(),
        sp.GetRequiredService<ManifestRepository>()));
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

// Admin CSV/JSON Import Runtime — M6 validate-preview-apply boundary.
builder.Services.AddSingleton<AdminImportRepository>(sp =>
    new NpgsqlAdminImportRepository(
        sp.GetRequiredService<ILogger<NpgsqlAdminImportRepository>>(),
        connectionString));
builder.Services.AddSingleton<AdminImportRuntime>();

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
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<AuthRuntime>();
builder.Services.AddSingleton<AuthEndpoint>();
builder.Services.AddSingleton<ExistingSystemChangeIntakeEndpoint>();
builder.Services.AddSingleton<ComponentEventAppendEndpoint>();
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
    var isAdminSurface =
        string.Equals(request.OperationType, "admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(request.Target, "admin", StringComparison.OrdinalIgnoreCase);
    var authErrors = isAdminSurface
        ? jwtGuard.ValidateForContext(token, AuthRealm.AdminRealm, AuthRealm.AdminAudience, AuthRealm.AdminRole)
        : jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new EndpointResponseDto(false, null, authErrors), statusCode: 401);

    var role = jwtGuard.TryGetRole(token);
    if (string.IsNullOrWhiteSpace(role))
    {
        var errors = new[] { new ValidationError("AUTH_TOKEN_ROLE_MISSING", "Token is missing required role claim.") };
        return Results.Json(new EndpointResponseDto(false, null, errors), statusCode: 401);
    }

    var authoritativeRequest = request with { Role = role };
    var result = await dispatch.HandleAsync(authoritativeRequest, ctx.RequestAborted);
    return Results.Json(result, statusCode: result.Success ? 200 : 422);
});



app.MapPost("/component-events/append", async (
    HttpContext ctx,
    ComponentEventAppendRequestDto request,
    ComponentEventAppendEndpoint endpoint,
    JwtGuard jwtGuard) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new ComponentEventAppendResponseDto(false, 0, authErrors), statusCode: 401);

    var subject = jwtGuard.TryGetSubject(token);
    if (string.IsNullOrWhiteSpace(subject))
    {
        var errors = new[] { new ValidationError("AUTH_TOKEN_SUB_MISSING", "Token is missing required sub claim.") };
        return Results.Json(new ComponentEventAppendResponseDto(false, 0, errors), statusCode: 401);
    }

    var result = await endpoint.HandleAsync(request, subject, ctx.RequestAborted);
    return Results.Json(result, statusCode: result.Success ? 202 : 422);
});

// POST /intake/legacy-change — existing-system change-event intake boundary (URL stable; vocabulary canonical).
app.MapPost("/intake/legacy-change", (
    HttpContext ctx,
    ExistingSystemChangeIntakeRequestDto request,
    ExistingSystemChangeIntakeEndpoint intake,
    JwtGuard jwtGuard) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new ExistingSystemChangeIntakeResponseDto(false, null, authErrors), statusCode: 401);

    var role = jwtGuard.TryGetRole(token);
    var result = intake.Handle(request, role ?? string.Empty);
    return Results.Json(result, statusCode: result.Accepted ? 202 : 422);
});

static void AppendRefreshCookie(HttpResponse response, string refreshPlain)
{
    var maxAge = 60 * 60 * 24 * 7;
    response.Headers.Append("Set-Cookie",
        $"{AuthCookieNames.RefreshToken}={Uri.EscapeDataString(refreshPlain)}; Path=/; HttpOnly; SameSite=Lax; Max-Age={maxAge}");
}

static void ClearRefreshCookie(HttpResponse response) =>
    response.Headers.Append("Set-Cookie",
        $"{AuthCookieNames.RefreshToken}=; Path=/; HttpOnly; SameSite=Lax; Max-Age=0");

static string? ReadRefreshCookie(HttpRequest request)
{
    if (!request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var value))
        return null;
    return string.IsNullOrWhiteSpace(value) ? null : Uri.UnescapeDataString(value);
}

// POST /auth/register — normal user realm registration (pending approval; no session issuance)
app.MapPost("/auth/register", async (HttpContext ctx, RegisterRequestDto request, AuthEndpoint auth) =>
{
    var result = await auth.RegisterUserAsync(request, ctx.RequestAborted);
    return Results.Json(result, statusCode: result.Success ? 201 : 409);
});

// POST /auth/login — user realm (auth_runtime.login)
app.MapPost("/auth/login", async (HttpContext ctx, LoginRequestDto request, AuthEndpoint auth) =>
{
    var (result, refresh) = await auth.LoginUserAsync(request, ctx.RequestAborted);
    if (result.Success && refresh is not null)
        AppendRefreshCookie(ctx.Response, refresh);
    return Results.Json(result, statusCode: result.Success ? 200 : 401);
});

app.MapPost("/auth/refresh", async (HttpContext ctx, AuthEndpoint auth) =>
{
    var bodyRefresh = (await ctx.Request.ReadFromJsonAsync<RefreshRequestDto>(ctx.RequestAborted))?.RefreshToken;
    var refresh = bodyRefresh ?? ReadRefreshCookie(ctx.Request);
    var (result, newRefresh) = await auth.RefreshUserAsync(new RefreshRequestDto(refresh), ctx.RequestAborted);
    if (result.Success && newRefresh is not null)
        AppendRefreshCookie(ctx.Response, newRefresh);
    return Results.Json(result, statusCode: result.Success ? 200 : 401);
});

app.MapPost("/auth/logout", async (HttpContext ctx, AuthEndpoint auth) =>
{
    var bodyRefresh = (await ctx.Request.ReadFromJsonAsync<LogoutRequestDto>(ctx.RequestAborted))?.RefreshToken;
    var refresh = bodyRefresh ?? ReadRefreshCookie(ctx.Request);
    var result = await auth.LogoutAsync(new LogoutRequestDto(refresh), ctx.RequestAborted);
    ClearRefreshCookie(ctx.Response);
    return Results.Json(result, statusCode: result.Success ? 200 : 401);
});

app.MapGet("/auth/login-manifest", async (AuthEndpoint auth, CancellationToken ct) =>
{
    var result = await auth.LoadUserLoginManifestAsync(ct);
    return Results.Json(result, statusCode: result.Success ? 200 : 404);
});

// POST /super_auth/login — admin realm
app.MapPost("/super_auth/login", async (HttpContext ctx, LoginRequestDto request, AuthEndpoint auth) =>
{
    var (result, refresh) = await auth.LoginAdminAsync(request, ctx.RequestAborted);
    if (result.Success && refresh is not null)
        AppendRefreshCookie(ctx.Response, refresh);
    return Results.Json(result, statusCode: result.Success ? 200 : 401);
});

app.MapPost("/super_auth/refresh", async (HttpContext ctx, AuthEndpoint auth) =>
{
    var bodyRefresh = (await ctx.Request.ReadFromJsonAsync<RefreshRequestDto>(ctx.RequestAborted))?.RefreshToken;
    var refresh = bodyRefresh ?? ReadRefreshCookie(ctx.Request);
    var (result, newRefresh) = await auth.RefreshAdminAsync(new RefreshRequestDto(refresh), ctx.RequestAborted);
    if (result.Success && newRefresh is not null)
        AppendRefreshCookie(ctx.Response, newRefresh);
    return Results.Json(result, statusCode: result.Success ? 200 : 401);
});

// GET /auth/session — validate Bearer JWT; optional ?expected=admin|user
app.MapGet("/auth/session", (HttpContext ctx, JwtGuard jwtGuard) =>
{
    var token = ExtractBearerToken(ctx);
    var expected = ctx.Request.Query["expected"].FirstOrDefault();

    IReadOnlyList<ValidationError> authErrors;
    if (string.Equals(expected, "admin", StringComparison.OrdinalIgnoreCase))
        authErrors = jwtGuard.ValidateForContext(token, AuthRealm.AdminRealm, AuthRealm.AdminAudience, AuthRealm.AdminRole);
    else if (string.Equals(expected, "user", StringComparison.OrdinalIgnoreCase))
        authErrors = jwtGuard.ValidateForContext(token, AuthRealm.UserRealm, AuthRealm.UserAudience, AuthRealm.UserRole);
    else
        authErrors = jwtGuard.Validate(token);

    if (authErrors.Count > 0)
        return Results.Json(new SessionResponseDto(false, null, null, null, null, authErrors), statusCode: 401);

    var subject = jwtGuard.TryGetSubject(token);
    if (string.IsNullOrWhiteSpace(subject))
    {
        var errors = new[] { new ValidationError("AUTH_TOKEN_SUB_MISSING", "Token is missing required sub claim.") };
        return Results.Json(new SessionResponseDto(false, null, null, null, null, errors), statusCode: 401);
    }

    var role = jwtGuard.TryGetRole(token);
    if (string.IsNullOrWhiteSpace(role))
    {
        var errors = new[] { new ValidationError("AUTH_TOKEN_ROLE_MISSING", "Token is missing required role claim.") };
        return Results.Json(new SessionResponseDto(false, null, null, null, null, errors), statusCode: 401);
    }

    return Results.Json(new SessionResponseDto(
        true, subject, role, jwtGuard.TryGetRealm(token), jwtGuard.TryGetAudience(token), []));
});

// GET /sse — SSE projection lane (JWT-guarded runtime-adjacent surface).
// Streams projection events from DbNotifyListener via SseEventBroadcaster.
// Guarded to keep reader authorization boundary explicit for runtime/admin projections.
app.MapGet("/sql-attention/topology-projection", async (
    HttpContext ctx,
    string? sourceSetId,
    SqlAttentionTopologyProjectionRuntime projectionRuntime,
    JwtGuard jwtGuard) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { success = false, errors = authErrors }, statusCode: 401);

    if (string.IsNullOrWhiteSpace(sourceSetId))
    {
        return Results.Json(new
        {
            success = false,
            errors = new[] { new ValidationError("SOURCE_SET_ID_REQUIRED", "Query parameter 'sourceSetId' is required.") }
        }, statusCode: 400);
    }

    var result = await projectionRuntime.ProjectAsync(sourceSetId.Trim(), ctx.RequestAborted);
    return Results.Json(new { success = true, result });
});


// ─── Draft Preview surface endpoints (/draft-preview/*) ──────────────────────
// Read-only surface for the /demo draft preview UI.
// All 3 endpoints are JWT-guarded. No write operations, no topology transform pipeline.
// Explicit failure — no silent fallback.

// GET /draft-preview/layouts — lists admin-authored layout candidates with tensor slots
app.MapGet("/draft-preview/layouts", async (
    HttpContext ctx,
    UiTopologyRepository uiRepo,
    JwtGuard jwtGuard,
    CancellationToken ct) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { success = false, errors = authErrors }, statusCode: 401);

    var layouts = await uiRepo.ListLayoutCandidatesAsync(ct);
    return Results.Json(new { success = true, layouts });
});

// GET /draft-preview/drafts — lists content_entity_drafts with status='draft'
app.MapGet("/draft-preview/drafts", async (
    HttpContext ctx,
    ContentBundleRepository contentRepo,
    JwtGuard jwtGuard,
    CancellationToken ct) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { success = false, errors = authErrors }, statusCode: 401);

    var drafts = await contentRepo.ListEntityDraftsAsync(ct);
    return Results.Json(new { success = true, drafts });
});

// POST /draft-preview/preview — loads layout nodes + draft content for projection preview
// Request: { layoutId: string, draftId: string }
// Response: { success, layoutId, draftId, layoutNodes: [{slotKey, orderIndex}], draftEntityJson, draftStatus }
app.MapPost("/draft-preview/preview", async (
    HttpContext ctx,
    DraftPreviewRequest request,
    TopologyRepository topoRepo,
    ContentBundleRepository contentRepo,
    JwtGuard jwtGuard,
    CancellationToken ct) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { success = false, errors = authErrors }, statusCode: 401);

    if (string.IsNullOrWhiteSpace(request.LayoutId) || !Guid.TryParse(request.LayoutId, out var layoutId))
        return Results.Json(new { success = false,
            errors = new[] { new ValidationError("MALFORMED_LAYOUT_ID", "layoutId must be a non-empty valid UUID.") }
        }, statusCode: 422);

    if (string.IsNullOrWhiteSpace(request.DraftId) || !Guid.TryParse(request.DraftId, out var draftId))
        return Results.Json(new { success = false,
            errors = new[] { new ValidationError("MALFORMED_DRAFT_ID", "draftId must be a non-empty valid UUID.") }
        }, statusCode: 422);

    var tensorRows = await topoRepo.LoadLayoutNodesAsync(layoutId, ct);
    if (tensorRows.Count == 0)
        return Results.Json(new { success = false,
            errors = new[] { new ValidationError("LAYOUT_NODES_NOT_FOUND",
                $"layout_id '{layoutId}' has no tensor rows in ui_topology_tensor. " +
                "Broken layout configuration — no fallback.") }
        }, statusCode: 422);

    var draft = await contentRepo.LoadDraftAsync(draftId, ct);
    if (draft is null)
        return Results.Json(new { success = false,
            errors = new[] { new ValidationError("DRAFT_NOT_FOUND",
                $"draft_id '{draftId}' not found in content_entity_drafts.") }
        }, statusCode: 404);

    var orderedNodes = tensorRows
        .OrderBy(r => r.OrderIndex)
        .Select(r => new { slotKey = r.SlotKey, orderIndex = r.OrderIndex, layoutPatchJson = r.LayoutPatchJson })
        .ToList();

    return Results.Json(new
    {
        success = true,
        layoutId = layoutId.ToString(),
        draftId = draftId.ToString(),
        layoutNodes = orderedNodes,
        draftEntityJson = System.Text.Json.JsonSerializer.Deserialize<object>(draft.EntityJsonb),
        draftStatus = draft.Status,
    });
});

app.MapGet("/sse", async (HttpContext ctx, SseEndpoint sse, JwtGuard jwtGuard) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new EndpointResponseDto(false, null, authErrors), statusCode: 401);

    await sse.StreamAsync(ctx.Response, ctx.RequestAborted);
    return Results.Empty;
});

app.Run();
