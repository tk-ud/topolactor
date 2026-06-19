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
builder.Services.AddSingleton<MockPresetRepository>(sp =>
    new NpgsqlMockPresetRepository(
        sp.GetRequiredService<ILogger<NpgsqlMockPresetRepository>>(),
        connectionString));
builder.Services.AddSingleton<TeamMarkdownRepository>(sp =>
    new NpgsqlTeamMarkdownRepository(
        sp.GetRequiredService<ILogger<NpgsqlTeamMarkdownRepository>>(),
        connectionString));
builder.Services.AddSingleton<IExternalPortPolicyRepository>(sp =>
    new NpgsqlExternalPortPolicyRepository(
        sp.GetRequiredService<ILogger<NpgsqlExternalPortPolicyRepository>>(),
        connectionString));
builder.Services.AddSingleton<IExternalCredentialVaultRepository>(sp =>
    new NpgsqlExternalCredentialVaultRepository(
        sp.GetRequiredService<ILogger<NpgsqlExternalCredentialVaultRepository>>(),
        connectionString));
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
builder.Services.AddSingleton<IExternalPortResolver, ExternalPortResolver>();
builder.Services.AddSingleton<IFileStorageRepository>(sp =>
    new NpgsqlFileStorageRepository(
        sp.GetRequiredService<ILogger<NpgsqlFileStorageRepository>>(),
        connectionString));
builder.Services.AddSingleton<IExternalPortBundleStepHandler>(_ => new FileStorageBundleStepHandler());
builder.Services.AddSingleton<IExternalPortDbFunctionRepository, NpgsqlExternalPortDbFunctionRepository>();
builder.Services.AddSingleton<IAbstractFunctionManifestRepository>(_ =>
    new NpgsqlAbstractFunctionManifestRepository(connectionString));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(_ =>
    new CallPostgresFunctionPrimitiveAdapter(connectionString));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter, ProjectionPrimitiveAdapter>();
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter, FailClosePrimitiveAdapter>();
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new SqlAttentionProjectionPrimitiveAdapter(
        sp.GetRequiredService<ILogger<SqlAttentionProjectionPrimitiveAdapter>>(),
        sp.GetRequiredService<SqlAttentionLogsRepository>(),
        sp.GetRequiredService<TopologyRepository>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new RecommendationAttentionPrimitiveAdapter(
        sp.GetRequiredService<ILogger<RecommendationAttentionPrimitiveAdapter>>(),
        sp.GetRequiredService<ContextRouteRecommendationResolver>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new RecommendationCandidateSourcePrimitiveAdapter(
        sp.GetRequiredService<ILogger<RecommendationCandidateSourcePrimitiveAdapter>>(),
        sp.GetRequiredService<ContextRouteRecommendationResolver>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new RecommendationEligibilityPrimitiveAdapter(
        sp.GetRequiredService<ILogger<RecommendationEligibilityPrimitiveAdapter>>(),
        sp.GetRequiredService<ContextRouteRecommendationResolver>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new RecommendationScoreRankPrimitiveAdapter(
        sp.GetRequiredService<ILogger<RecommendationScoreRankPrimitiveAdapter>>(),
        sp.GetRequiredService<ContextRouteRecommendationResolver>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(
    new RecommendationProjectionPrimitiveAdapter());
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new CredentialAcquireLeaseAdapter(sp.GetRequiredService<IExternalCredentialVaultRepository>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new CredentialHttpRequestAdapter(sp.GetRequiredService<IExternalPortHttpClient>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new CredentialComputeTokenHashAdapter(sp.GetRequiredService<IExternalCredentialCrypto>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(
    new CredentialParseExpiresAtAdapter());
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new CredentialWriteVaultAdapter(
        sp.GetRequiredService<IExternalCredentialVaultRepository>(),
        sp.GetRequiredService<IExternalCredentialCrypto>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new CredentialReleaseLeaseAdapter(sp.GetRequiredService<IExternalCredentialVaultRepository>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new CredentialFailLeaseAdapter(sp.GetRequiredService<IExternalCredentialVaultRepository>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new EventLogPrimitiveAdapter(sp.GetRequiredService<IExternalPortRuntimeEventLogRepository>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(sp =>
    new HttpRequestPrimitiveAdapter(sp.GetRequiredService<IExternalPortHttpClient>()));
builder.Services.AddSingleton<IAbstractFunctionPrimitiveAdapter>(
    new SchedulerEnqueuePrimitiveAdapter());
builder.Services.AddSingleton<AbstractFunctionExecutor>();
builder.Services.AddSingleton<IExternalPortRuntimeEventLogRepository>(_ =>
    new NpgsqlExternalPortRuntimeEventLogRepository(connectionString));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IExternalPortHttpClient>(sp =>
    new HttpExternalPortHttpClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ExternalPort")));
builder.Services.AddSingleton<IExternalPortCredentialReferenceResolver>(sp =>
    new ExternalPortCredentialReferenceResolver(sp.GetRequiredService<IExternalCredentialVaultRepository>()));
builder.Services.AddSingleton<IExternalCredentialCrypto, AesExternalCredentialCrypto>();
builder.Services.AddSingleton<IExternalPortPolicyStepExecutor>(sp =>
    new ExternalPortPolicyStepExecutor(
        httpClient: sp.GetRequiredService<IExternalPortHttpClient>(),
        credentialReferenceResolver: sp.GetRequiredService<IExternalPortCredentialReferenceResolver>(),
        crypto: sp.GetRequiredService<IExternalCredentialCrypto>(),
        portResolver: sp.GetRequiredService<IExternalPortResolver>(),
        bundleHandlers: sp.GetServices<IExternalPortBundleStepHandler>(),
        dbFunctionRepository: sp.GetRequiredService<IExternalPortDbFunctionRepository>(),
        abstractFunctionExecutor: sp.GetRequiredService<AbstractFunctionExecutor>(),
        runtimeEventLogRepository: sp.GetRequiredService<IExternalPortRuntimeEventLogRepository>()));
builder.Services.AddSingleton<ExternalPortDispatchRuntime>(sp =>
    new ExternalPortDispatchRuntime(
        sp.GetRequiredService<ILogger<ExternalPortDispatchRuntime>>(),
        sp.GetRequiredService<IExternalPortPolicyRepository>(),
        sp.GetRequiredService<IExternalPortPolicyStepExecutor>(),
        sp.GetRequiredService<SseEventBroadcaster>()));
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
        sp.GetRequiredService<SqlAttentionLogsRepository>(),
        sp.GetRequiredService<AbstractFunctionExecutor>(),
        sp.GetRequiredService<MockPresetRepository>(),
        sp.GetRequiredService<TeamMarkdownRepository>()));
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
        sp.GetRequiredService<AbstractFunctionExecutor>(),
        sp.GetRequiredService<OutputLaneRouter>(),
        sp.GetRequiredService<HubNavigationResolver>(),
        sp.GetRequiredService<ManifestRepository>()));
builder.Services.AddSingleton<AdminRuntimeDispatchAdapter>(sp =>
    new AdminRuntimeDispatchAdapter(
        sp.GetRequiredService<AdminRuntime>(),
        sp.GetRequiredService<OperationVectorResolver>()));
builder.Services.AddSingleton<SseProjectionRuntime>();
builder.Services.AddSingleton<RegistryAttractorDispatchRuntime>(sp =>
    new RegistryAttractorDispatchRuntime(
        sp.GetRequiredService<ILogger<RegistryAttractorDispatchRuntime>>(),
        sp.GetRequiredService<HubAttractorExplorationRuntime>(),
        sp.GetRequiredService<SqlAttentionLogsRepository>()));
builder.Services.AddSingleton<ManifestDispatcher>(sp =>
{
    var handlers = new Dictionary<string, IDispatchableRuntime>
    {
        ["topology_transform_runtime"]  = sp.GetRequiredService<RuntimeExecutor>(),
        ["admin_runtime"]               = sp.GetRequiredService<AdminRuntimeDispatchAdapter>(),
        ["sse_projection_runtime"]      = sp.GetRequiredService<SseProjectionRuntime>(),
        ["registry_attractor_runtime"]  = sp.GetRequiredService<RegistryAttractorDispatchRuntime>(),
        ["external_port_runtime"]      = sp.GetRequiredService<ExternalPortDispatchRuntime>(),
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
// COOKIE_SECURE controls refresh token cookie Secure attribute per SSOT policy.
// Set to "true" for HTTPS/production; absent or not "true" is explicit local/demo HTTP exception.
var cookieSecure = string.Equals(
    Environment.GetEnvironmentVariable("COOKIE_SECURE"),
    "true", StringComparison.OrdinalIgnoreCase);

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
    // Accept any valid JWT; role claim drives authoritative capability — no surface-string heuristic.
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new EndpointResponseDto(false, null, authErrors), statusCode: 401);

    var jwtRole = jwtGuard.TryGetRole(token);
    if (string.IsNullOrWhiteSpace(jwtRole))
    {
        var errors = new[] { new ValidationError("AUTH_TOKEN_ROLE_MISSING", "Token is missing required role claim.") };
        return Results.Json(new EndpointResponseDto(false, null, errors), statusCode: 401);
    }

    // Prevent privilege escalation: body claiming admin when JWT says user is denied.
    // Admin can use body role for routing (admin dispatching as user is not an escalation).
    var bodyRole = request.Role;
    if (bodyRole is "admin" && jwtRole != "admin")
    {
        var errors = new[] { new ValidationError("AUTH_CAPABILITY_DENIED", "Token role insufficient for requested role.") };
        return Results.Json(new EndpointResponseDto(false, null, errors), statusCode: 403);
    }
    // Use body role for axes resolution (routing); fall back to JWT role when not set.
    // Capability gate in ManifestDispatcher enforces runtime-level requirements against routing role.
    var routingRole = bodyRole ?? jwtRole;
    var authoritativeRequest = request with { Role = routingRole };
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

void AppendRefreshCookie(HttpResponse response, string refreshPlain)
{
    var maxAge = 60 * 60 * 24 * 7;
    var secureAttr = cookieSecure ? "; Secure" : "";
    response.Headers.Append("Set-Cookie",
        $"{AuthCookieNames.RefreshToken}={Uri.EscapeDataString(refreshPlain)}; Path=/; HttpOnly; SameSite=Lax; Max-Age={maxAge}{secureAttr}");
}

void ClearRefreshCookie(HttpResponse response)
{
    var secureAttr = cookieSecure ? "; Secure" : "";
    response.Headers.Append("Set-Cookie",
        $"{AuthCookieNames.RefreshToken}=; Path=/; HttpOnly; SameSite=Lax; Max-Age=0{secureAttr}");
}

static string? ReadRefreshCookie(HttpRequest request)
{
    if (!request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out var value))
        return null;
    return string.IsNullOrWhiteSpace(value) ? null : Uri.UnescapeDataString(value);
}

// POST /auth/projection-login — projection surface login: admin JWT if granted, user JWT otherwise.
// Login surface and authority are orthogonal; admin users entering via /auth retain admin capability.
app.MapPost("/auth/projection-login", async (HttpContext ctx, LoginRequestDto request, AuthEndpoint auth) =>
{
    var (result, refresh) = await auth.LoginProjectionAsync(request, ctx.RequestAborted);
    if (result.Success && refresh is not null)
        AppendRefreshCookie(ctx.Response, refresh);
    return Results.Json(result, statusCode: result.Success ? 200 : 401);
});

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

// POST /draft-preview/preview — layout projection + manifest screen_data_shape topology intent
// Request: { layoutId: string }
// Resolves initialDataRows from draft manifest matched by routeKey (= topologySystemName).
app.MapPost("/draft-preview/preview", async (
    HttpContext ctx,
    DraftPreviewRequest request,
    TopologyRepository topoRepo,
    ManifestRepository manifestRepo,
    UiTopologyRepository uiRepo,
    JwtGuard jwtGuard,
    CancellationToken ct) =>
{
    var token = ExtractBearerToken(ctx);
    var authErrors = jwtGuard.Validate(token);
    if (authErrors.Count > 0)
        return Results.Json(new { success = false, errors = authErrors }, statusCode: 401);

    var (success, error, statusCode) = await DraftPreviewComposer.ComposeAsync(
        request.LayoutId ?? string.Empty,
        topoRepo,
        manifestRepo,
        uiRepo,
        ct);

    if (error is not null)
        return Results.Json(new { success = false, errors = new[] { error } }, statusCode: statusCode);

    return Results.Json(new
    {
        success = true,
        layoutId = success!.LayoutId,
        previewMode = success.PreviewMode,
        routeKey = success.RouteKey,
        packageId = success.PackageId,
        layoutNodes = success.LayoutNodes,
        rootLayoutClassRefs = success.RootLayoutClassRefs,
        designByNodeId = success.DesignByNodeId,
        manifestId = success.ManifestId,
        manifestStatus = success.ManifestStatus,
        topologySystemName = success.TopologySystemName,
        initialDataRows = success.InitialDataRows,
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
