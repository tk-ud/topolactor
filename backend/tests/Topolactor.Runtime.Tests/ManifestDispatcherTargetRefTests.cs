using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Verifies ManifestDispatcher target_ref routing contract:
/// - Payload with target_ref="manifest:{uuid}:{key}" routes to LoadByIdAsync (exact manifest).
/// - Absent target_ref falls through to ResolveActiveManifestAsync (axes-based).
/// - Invalid target_ref format (non-manifest prefix) falls through to axes resolution.
/// - Valid target_ref with unknown UUID returns MANIFEST_NOT_FOUND.
/// </summary>
public class ManifestDispatcherTargetRefTests
{
    private static readonly Guid KnownManifestId = new("aaaaaaaa-bbbb-cccc-dddd-000000000001");

    private static ManifestDispatcher BuildDispatcher(ManifestRepository repo, IReadOnlyDictionary<string, IDispatchableRuntime>? extraHandlers = null)
    {
        var targetOverride = RuntimeExecutorTests.CreateTargetDispatchOverride();
        var handlers = new Dictionary<string, IDispatchableRuntime>
        {
            ["topology_transform_runtime"] = new StubSuccessRuntime(),
        };
        if (extraHandlers is not null)
        {
            foreach (var pair in extraHandlers) handlers[pair.Key] = pair.Value;
        }
        return new ManifestDispatcher(
            NullLogger<ManifestDispatcher>.Instance,
            handlers,
            new OperationVectorResolver(),
            targetOverride,
            repo);
    }

    private static EndpointRequestDto MakeRequest(JsonElement? payload = null) =>
        new("Search", "screen", "screen_list", "Search",
            IdOrHubId: null,
            Payload: payload,
            Context: null,
            TriggerKind: "client",
            Role: null);

    private static JsonElement BuildPayload(string? targetRef = null, string? wiringKey = null, string? wiringId = null)
    {
        var dict = new Dictionary<string, object?>();
        if (targetRef is not null) dict["target_ref"] = targetRef;
        if (wiringKey is not null) dict["wiring_key"] = wiringKey;
        if (wiringId is not null) dict["wiring_id"] = wiringId;
        return JsonSerializer.SerializeToElement(dict);
    }


    [Fact]
    public async Task DispatchAsync_ExternalPortAxes_UsesManifestRuntimeMappingWithoutHardcodedTargetBranch()
    {
        var repo = new TrackingManifestRepository(KnownManifestId, runtimeDestination: "external_port_runtime");
        var externalHandler = new CapturingRuntime();
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["external_port_runtime"] = externalHandler,
        });
        var payload = JsonSerializer.SerializeToElement(new
        {
            port_target_ref = "external-port:response_port:00000000-0000-0000-0000-00000000abcd",
            dispatch_payload = new { subject = "hello" }
        });

        var response = await dispatcher.DispatchAsync(new EndpointRequestDto(
            "dispatchExternalPort",
            "external_port",
            "external_port",
            "dispatchExternalPort",
            IdOrHubId: null,
            Payload: payload,
            Context: null,
            TriggerKind: "client",
            Role: "admin"));

        Assert.True(response.Success);
        Assert.True(repo.ResolveAxesCalled, "external_port dispatch must use manifest axis resolution.");
        Assert.False(repo.LoadByIdCalled, "external-port port_target_ref must not use manifest target_ref LoadById routing.");
        Assert.True(externalHandler.Called);
        Assert.Equal(KnownManifestId, externalHandler.ManifestId);
    }

    // ─── target_ref present: routes to LoadByIdAsync ─────────────────────────

    [Fact]
    public async Task DispatchAsync_TargetRef_ManifestFormat_CallsLoadByIdAsync()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{KnownManifestId}:search_wiring";
        var payload = BuildPayload(targetRef: targetRef, wiringKey: "search_wiring", wiringId: "wiring-001");

        await dispatcher.DispatchAsync(MakeRequest(payload));

        Assert.True(repo.LoadByIdCalled, "LoadByIdAsync must be called when target_ref is present");
        Assert.False(repo.ResolveAxesCalled, "ResolveActiveManifestAsync must NOT be called when target_ref routes to exact manifest");
        Assert.Equal(KnownManifestId, repo.LoadByIdCalledWith);
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_ManifestFormat_ManifestFound_ReturnsSuccess()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{KnownManifestId}:search_wiring";

        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: targetRef)));

        Assert.True(response.Success);
        Assert.Empty(response.Errors);
    }

    // ─── target_ref absent: falls through to axes resolution ─────────────────

    [Fact]
    public async Task DispatchAsync_NoTargetRef_CallsResolveActiveManifestAsync()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        await dispatcher.DispatchAsync(MakeRequest(BuildPayload(wiringKey: "search_wiring")));

        Assert.False(repo.LoadByIdCalled, "LoadByIdAsync must NOT be called when target_ref is absent");
        Assert.True(repo.ResolveAxesCalled, "ResolveActiveManifestAsync must be called when target_ref absent");
    }

    [Fact]
    public async Task DispatchAsync_EmptyPayload_CallsResolveActiveManifestAsync()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        await dispatcher.DispatchAsync(MakeRequest());

        Assert.False(repo.LoadByIdCalled);
        Assert.True(repo.ResolveAxesCalled);
    }

    // ─── invalid target_ref format: explicit TARGET_REF_INVALID error ───────────
    // Rationale: silent axes-fallback for a malformed admin-configured ref risks routing
    // to an unintended manifest. An explicit error surfaces the broken configuration.

    [Fact]
    public async Task DispatchAsync_TargetRef_NonManifestPrefix_ReturnsTargetRefInvalidError()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: "screen:some-ref")));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_INVALID");
        Assert.False(repo.LoadByIdCalled, "LoadByIdAsync must not be called for malformed target_ref");
        Assert.False(repo.ResolveAxesCalled, "Axes resolution must not be called when target_ref is present but malformed");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_InvalidUuid_ReturnsTargetRefInvalidError()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        // "manifest:" prefix but invalid UUID portion — must return explicit error, not axes fallback
        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: "manifest:not-a-uuid:key")));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_INVALID");
        Assert.False(repo.LoadByIdCalled);
        Assert.False(repo.ResolveAxesCalled);
    }

    // ─── route: prefix target_ref: explicit TARGET_REF_INVALID ──────────────
    // Defensive: navigation wiring is frontend-local, so route:<routeKey> must not
    // reach ManifestDispatcher. If it ever does (misconfiguration), ManifestDispatcher
    // returns TARGET_REF_INVALID explicitly — no silent axes-fallback.

    [Fact]
    public async Task DispatchAsync_TargetRef_RoutePrefix_ReturnsTargetRefInvalidError()
    {
        var repo = new TrackingManifestRepository(KnownManifestId);
        var dispatcher = BuildDispatcher(repo);

        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: "route:/admin/manifests")));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_INVALID");
        Assert.False(repo.LoadByIdCalled, "LoadByIdAsync must not be called for route: target_ref");
        Assert.False(repo.ResolveAxesCalled, "Axes resolution must not be called when target_ref is present but malformed");
    }

    // ─── valid target_ref format but manifest not found ───────────────────────

    [Fact]
    public async Task DispatchAsync_TargetRef_ManifestNotFound_ReturnsManifestNotFoundError()
    {
        var unknownId = Guid.NewGuid();
        var repo = new TrackingManifestRepository(knownManifestId: null); // no known manifest
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{unknownId}:search_wiring";

        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: targetRef)));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "MANIFEST_NOT_FOUND");
        Assert.True(repo.LoadByIdCalled);
    }

    // ─── round 17 hardening: admin_runtime target_ref TOCTOU + layer/action authorization ────
    // These checks apply identically whether the target_ref reaching this branch came from the
    // uniform per-layout tensor row (NpgsqlTopologyRepository.LoadLayoutNodesAsync) or a per-node
    // dispatchTargetRefByTrigger override (renderEmission.ts buildAdminRuntimeTargetRefOverrideByTrigger)
    // -- both produce the exact same payload.target_ref/Request.Layer/Request.Action shape by the
    // time they reach ManifestDispatcher, so there is no separate "override path" to special-case.

    private static EndpointRequestDto MakeAdminRuntimeRequest(string layer, string action, string targetRef, string role = "admin") =>
        new("Search", "manifest", layer, action,
            IdOrHubId: null,
            Payload: BuildPayload(targetRef: targetRef),
            Context: null,
            TriggerKind: "client",
            Role: role);

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_NotActive_ReturnsNotActiveError()
    {
        var repo = new ConfigurableManifestRepository(KnownManifestId, status: "draft", dispatcherMappingLayer: "enum_dictionary", dispatcherMappingAction: "create_group");
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{KnownManifestId}:enum_dictionary:create_group";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "create_group", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_MANIFEST_NOT_ACTIVE");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_LayerActionMismatch_ReturnsMismatchError()
    {
        // target_ref's own embedded suffix says create_group, but the request separately declares
        // Layer/Action=enum_dictionary/delete_group -- these must be self-consistent.
        var repo = new ConfigurableManifestRepository(KnownManifestId, status: "active", dispatcherMappingLayer: "enum_dictionary", dispatcherMappingAction: "create_group");
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{KnownManifestId}:enum_dictionary:create_group";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "delete_group", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_LAYER_ACTION_MISMATCH");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_ActionNotInDispatcherMapping_ReturnsUnauthorizedError()
    {
        // The exact scenario the owner flagged: a create_group-only manifest's UUID combined with
        // a delete_group action. Both target_ref suffix and request axes agree on delete_group, so
        // the mismatch check above passes -- but this manifest's own dispatcher_mapping never
        // declared delete_group, only create_group, so it must still fail closed.
        var repo = new ConfigurableManifestRepository(KnownManifestId, status: "active", dispatcherMappingLayer: "enum_dictionary", dispatcherMappingAction: "create_group");
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{KnownManifestId}:enum_dictionary:delete_group";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "delete_group", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_LAYER_ACTION_UNAUTHORIZED");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_ColonContainingAction_ParsesAndDispatches()
    {
        // team_markdown's entire action vocabulary is "<resource>:<verb>" shaped
        // (e.g. "saved_view:update", "template:create") -- AdminRuntimeTargetRefRe's action
        // group must capture the WHOLE remainder after the layer's own colon, not stop at the
        // action's own internal colon (a bug this fixture would have caught: the pre-fix
        // "([^:]+):([^:]+)$" pattern rejects this target_ref as TARGET_REF_INVALID entirely,
        // never reaching authorization or dispatch). Mirrors frontend/runtime/renderEmission.ts's
        // ADMIN_RUNTIME_TARGET_REF_RE, tested against the identical shape in
        // adminWiringExecutionLane.test.ts.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: "team_markdown", dispatcherMappingAction: "saved_view:update");
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:team_markdown:saved_view:update";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("team_markdown", "saved_view:update", targetRef));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_NonLayerActionShape_SkipsAuthorization_StillDispatches()
    {
        // Round 17 correction (caught by live-DB CI, not locally reproducible without real
        // PostgreSQL): a genuine, pre-existing production convention dispatches an explicit
        // manifest selection for a plain page load using a GENERIC wiringKey target_ref (e.g.
        // "manifest:<uuid>:projection_entry", "manifest:<uuid>:hub_relations_read") against
        // Layer="screen_list"/Action="Search" axes, even when that manifest's runtime_destination
        // is admin_runtime -- this never encodes a specific admin_runtime operation and must not
        // be rejected. Round 18: the skip is scoped to the EXACT structural-read-fallback
        // pre-condition (manifest has a ui_projection entry AND request axes are a registered
        // screen-read shape) -- hasUiProjection:true here is what makes this fixture a genuine
        // instance of that case, not merely "any non-matching shape" (see the sibling
        // AdminRuntimeManifest_ConcreteOperation_NonLayerActionShape... test below, which proves
        // the same non-matching shape is REJECTED when there is no ui_projection entry / the axes
        // are not a screen-read shape).
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null, hasUiProjection: true);
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:some_wiring_key"; // no layer:action suffix

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("screen_list", "Search", targetRef));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_ConcreteOperation_NonLayerActionShape_StillRejected()
    {
        // Round 18: proves the generic-suffix skip above is scoped to genuine structural reads,
        // not to "any non-matching shape" -- the exact live-DB-caught vulnerability this round
        // closes. A generic wiringKey combined with a CONCRETE operation's Layer/Action (not
        // screen_list/Search) must still fail closed, even though the shape itself is identical to
        // the legitimate navigation convention above.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null, hasUiProjection: true);
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:some_wiring_key"; // no layer:action suffix

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "create_group", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_ConcreteOperation_NoUiProjection_NonLayerActionShape_StillRejected()
    {
        // Same negative proof without hasUiProjection at all, and with screen-read axes -- both
        // conditions (ui_projection presence AND screen-read axes) are required for the skip;
        // neither alone is sufficient.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null, hasUiProjection: false);
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:some_wiring_key";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("screen_list", "Search", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_OperationShapeTargetRef_ScreenReadRequestAxes_StillMismatchRejected()
    {
        // Round 19 audit catch: a target_ref that ITSELF parses as the strict operation shape
        // (manifest:<uuid>:<layer>:<action>) must always be validated against the request's own
        // Layer/Action, even when the REQUEST's axes happen to look like a generic screen-read
        // (Layer=screen_list/Action=Search) and the manifest has a ui_projection entry -- i.e. even
        // when the request axes alone would otherwise qualify for the generic-structural-read
        // exemption. Before this fix, IsGenericStructuralReadTargetRef's request-axes-only check
        // was consulted BEFORE the target_ref string's own shape was ever inspected, so this exact
        // scenario (target_ref honestly claims enum_dictionary:create_group, but request axes are
        // screen_list:Search) would have silently taken the generic exemption and skipped
        // authorization entirely -- never even noticing the target_ref named a totally different
        // concrete operation.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: "enum_dictionary", dispatcherMappingAction: "create_group",
            hasUiProjection: true);
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:enum_dictionary:create_group";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("screen_list", "Search", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_LAYER_ACTION_MISMATCH");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_HubNavigationGetHubRelations_AxesRegistered_Succeeds()
    {
        // Second live-DB catch (round 18): CredentialManagementHubRelationUiProjectionLiveDbTests
        // dispatches hub_navigation:get_hub_relations via target_ref against a manifest that
        // deliberately declares NEITHER dispatcher_mapping NOR ui_projection (a bare
        // "runtime_mapping only" identity selector) -- reproduces that shape here with a generic
        // (non layer:action) wiringKey, proving the bare-manifest navigation-read exemption
        // (IsBareManifestNavigationReadTargetRefAsync) admits it once the SAME (role, layer, action)
        // is independently, actively registered under the axes-based target="admin" system AND that
        // axes-registered manifest's OWN dispatcher_mapping entry declares
        // "identity_selector_read": true (round 19: SSOT-owned seed data classification, not a
        // hardcoded action-name allowlist in this test double or in ManifestDispatcher itself).
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null,
            hasUiProjection: false,
            adminAxesRegisteredOperation: ("admin", "hub_navigation", "get_hub_relations", true));
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:hub_relations_read"; // bare wiringKey, no layer:action suffix

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("hub_navigation", "get_hub_relations", targetRef));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_AxesManifestOwnCapabilityRequirement_RoleMismatch_FailsClosed()
    {
        // Round 21 audit catch: the BARE target_ref manifest (KnownManifestId) declares no
        // dispatcher_mapping/ui_projection/capability_requirement of its own (by this whole
        // exemption's own precondition) -- previously ONLY that bare manifest's (necessarily empty)
        // capability_requirement was ever checked, so a MORE RESTRICTIVE explicit
        // capability_requirement declared on the axes-registered operation-authority manifest
        // itself (here: required_role="super_admin", stricter than the request's "admin") was never
        // consulted at all. Proves it now fails closed via that axes manifest's OWN requirement.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null,
            hasUiProjection: false,
            adminAxesRegisteredOperation: ("admin", "hub_navigation", "get_hub_relations", true),
            axesManifestCapabilityRequirementRole: "super_admin");
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:hub_relations_read";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("hub_navigation", "get_hub_relations", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "AUTH_CAPABILITY_DENIED");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_AxesManifestOwnCapabilityRequirement_RoleMatches_Succeeds()
    {
        // Same axes-manifest-owned capability_requirement as above, but matching the request's role
        // -- proves the new check is a genuine role comparison, not an unconditional fail-close.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null,
            hasUiProjection: false,
            adminAxesRegisteredOperation: ("admin", "hub_navigation", "get_hub_relations", true),
            axesManifestCapabilityRequirementRole: "admin");
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:hub_relations_read";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("hub_navigation", "get_hub_relations", targetRef));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_HubNavigationGetHubRelations_NoAdminAxesRegistration_StillRejected()
    {
        // Proves the axes-registration re-check is actually load-bearing, not just the allowlist
        // membership alone: without an independently-registered target="admin" entry for this
        // (role, layer, action), the bare manifest must still fail closed.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null,
            hasUiProjection: false, adminAxesRegisteredOperation: null);
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:hub_relations_read";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("hub_navigation", "get_hub_relations", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_HubNavigationCreate_NotDeclaredIdentitySelectorRead_StillRejected()
    {
        // hub_navigation:create is a real mutation in the same layer as the identity-selector-read
        // actions, and (like them) happens to be axes-registered under target="admin" too -- but its
        // own dispatcher_mapping entry does not declare "identity_selector_read": true (mirroring
        // db/seed_empty.sql, where only list_manifests/get_hub_relations carry that field). Proves
        // the axes-registered manifest's OWN declared classification (not the axes lookup alone) is
        // what keeps this exemption from becoming a blanket target="admin"-registered-operation
        // bypass for bare manifests.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null,
            hasUiProjection: false,
            adminAxesRegisteredOperation: ("admin", "hub_navigation", "create", false));
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:hub_relations_read";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("hub_navigation", "create", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_ConcreteWriteAction_NotDeclaredIdentitySelectorRead_StillRejected()
    {
        // A bare manifest must never become a generic skeleton key for arbitrary admin_runtime
        // operations -- even though enum_dictionary:create_group is ALSO axes-registered under
        // target="admin" (db/seed_empty.sql's pre-existing a7-ae series), that entry does not
        // declare "identity_selector_read": true, so it must still fail closed.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null,
            hasUiProjection: false,
            adminAxesRegisteredOperation: ("admin", "enum_dictionary", "create_group", false));
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:some_wiring_key";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "create_group", targetRef));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_ADMIN_RUNTIME_LAYER_ACTION_MISSING");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_BareManifest_ArbitraryLayerAction_DeclaredIdentitySelectorRead_Succeeds()
    {
        // Round 19: proves genericity -- the exemption is keyed on the axes-registered manifest's
        // OWN "identity_selector_read": true declaration, not on a hardcoded "hub_navigation:
        // get_hub_relations"/"list_manifests" literal in ManifestDispatcher. An entirely different,
        // made-up (layer, action) pair with the SAME declared field is admitted identically.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: null, dispatcherMappingAction: null,
            hasUiProjection: false,
            adminAxesRegisteredOperation: ("admin", "some_other_layer", "some_other_read_action", true));
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:some_bare_wiring_key";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("some_other_layer", "some_other_read_action", targetRef));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_DuplicateDispatcherMappingEntries_DisagreeingRole_FailsClosed()
    {
        // Round 19: two dispatcher_mapping entries for the SAME (layer, action) with DIFFERENT
        // declared roles must fail closed rather than silently using whichever one JSONB array
        // order/FindDeclaredRole happens to see first.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: "enum_dictionary", dispatcherMappingAction: "create_group",
            dispatcherMappingRole: "admin", secondDispatcherMappingRole: "user");
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:enum_dictionary:create_group";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "create_group", targetRef, role: "admin"));

        Assert.False(response.Success);
        Assert.Contains(response.Errors, e => e.Code == "TARGET_REF_DUPLICATE_AXIS_DECLARATION");
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_DuplicateDispatcherMappingEntries_AgreeingRole_Succeeds()
    {
        // Two entries for the same (layer, action) that fully agree are not ambiguous -- no
        // authorization decision actually depends on which one is seen first.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: "enum_dictionary", dispatcherMappingAction: "create_group",
            dispatcherMappingRole: "admin", secondDispatcherMappingRole: "admin");
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:enum_dictionary:create_group";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "create_group", targetRef, role: "admin"));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_RoleComparisonIsCaseInsensitive_Succeeds()
    {
        // Round 19: aligns FindDeclaredRole's comparison with AxisMatches's pre-existing
        // case-insensitive role-axis convention (used throughout axes resolution) -- a request role
        // that differs only in case from the declared role must still be authorized. Explicit
        // capability_requirement="ADMIN" neutralizes that OTHER, independently Ordinal gate so this
        // test isolates the dispatcher_mapping.role comparison specifically.
        var repo = new ConfigurableManifestRepository(
            KnownManifestId, status: "active", dispatcherMappingLayer: "enum_dictionary", dispatcherMappingAction: "create_group",
            dispatcherMappingRole: "admin", capabilityRequirementRole: "ADMIN");
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:enum_dictionary:create_group";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "create_group", targetRef, role: "ADMIN"));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_AdminRuntimeManifest_Authorized_Succeeds()
    {
        var repo = new ConfigurableManifestRepository(KnownManifestId, status: "active", dispatcherMappingLayer: "enum_dictionary", dispatcherMappingAction: "create_group");
        var dispatcher = BuildDispatcher(repo, new Dictionary<string, IDispatchableRuntime>
        {
            ["admin_runtime"] = new StubSuccessRuntime(),
        });
        var targetRef = $"manifest:{KnownManifestId}:enum_dictionary:create_group";

        var response = await dispatcher.DispatchAsync(MakeAdminRuntimeRequest("enum_dictionary", "create_group", targetRef));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_NFormUuid_GuidTryParseAcceptsButAdminRuntimeTargetRefReRejects_ProvingStrictSubsetRelationship()
    {
        // Round 17 (f): pins the UUID accept-set relationship from the dispatch-time half.
        // ManifestDispatcher.TryParseManifestTargetRef uses Guid.TryParse, which accepts the
        // 32-hex-digit "N" form (no hyphens) -- unlike the strict 36-char hyphenated form the
        // save-time boundary (NpgsqlUiTopologyRepository.AdminRuntimeTargetRefRe) requires. This
        // proves TryParseManifestTargetRef really is the more lenient superset: a target_ref no
        // save-time-validated dispatchTargetRefByTrigger entry could ever contain still resolves
        // here. See NpgsqlUiTopologyRepositoryLayoutPatchValidationTests.cs
        // ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_NonHyphenatedUuid_FailsClose for the
        // save-time half rejecting the identical string.
        const string nFormUuid = "aaaaaaaabbbbccccddddaaaaaaaabbbb"; // 32 hex digits, no hyphens
        var manifestGuid = Guid.Parse(nFormUuid);
        Assert.True(Guid.TryParse(nFormUuid, out _), "test fixture precondition: nFormUuid must itself be Guid.TryParse-valid");

        var repo = new TrackingManifestRepository(manifestGuid);
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{nFormUuid}:search_wiring";

        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: targetRef)));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
        Assert.True(repo.LoadByIdCalled);
        Assert.Equal(manifestGuid, repo.LoadByIdCalledWith);
    }

    [Fact]
    public async Task DispatchAsync_TargetRef_NonAdminRuntimeDestination_SkipsLayerActionAuthorization()
    {
        // The generic package-wiring target_ref shape (manifest:<uuid>:<wiringKey>, e.g.
        // screenReadQueryWiring) has no layer:action suffix to authorize and must stay unaffected --
        // this authorization is scoped to admin_runtime-destination manifests only.
        var repo = new TrackingManifestRepository(KnownManifestId, runtimeDestination: "topology_transform_runtime");
        var dispatcher = BuildDispatcher(repo);
        var targetRef = $"manifest:{KnownManifestId}:search_wiring";

        var response = await dispatcher.DispatchAsync(MakeRequest(BuildPayload(targetRef: targetRef)));

        Assert.True(response.Success, string.Join(";", response.Errors.Select(e => e.Code + ":" + e.Message)));
    }
}

// ─── Test doubles ─────────────────────────────────────────────────────────────

internal sealed class TrackingManifestRepository(Guid? knownManifestId, string runtimeDestination = "topology_transform_runtime")
    : ManifestRepository(NullLogger<ManifestRepository>.Instance)
{
    public bool LoadByIdCalled { get; private set; }
    public Guid LoadByIdCalledWith { get; private set; }
    public bool ResolveAxesCalled { get; private set; }

    private ManifestRecord MakeManifest(Guid id) => new(
        ManifestId: id,
        RelationRegistryId: null,
        Topology: JsonSerializer.SerializeToElement(new[]
        {
            new { type = "runtime_mapping", runtime_destination = runtimeDestination },
        }).EnumerateArray().ToArray(),
        Status: "active");

    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default)
    {
        LoadByIdCalled = true;
        LoadByIdCalledWith = manifestId;
        var result = knownManifestId.HasValue && manifestId == knownManifestId.Value
            ? MakeManifest(manifestId)
            : null;
        return Task.FromResult<ManifestRecord?>(result);
    }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default)
    {
        ResolveAxesCalled = true;
        var result = knownManifestId.HasValue ? MakeManifest(knownManifestId.Value) : null;
        return Task.FromResult<ManifestRecord?>(result);
    }

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestDetailRecord?>(null);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId, Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedLifecycle();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedLifecycle();

    public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.EmptyPromotionList(statusFilter, ct);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId, JsonElement promotionEntry, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();
}

/// <summary>Test double for round 17 admin_runtime target_ref hardening: configurable manifest
/// status and a single optional dispatcher_mapping entry, so tests can control the exact
/// active/authorization scenario under test.</summary>
internal sealed class ConfigurableManifestRepository(
    Guid knownManifestId, string status, string? dispatcherMappingLayer, string? dispatcherMappingAction,
    bool hasUiProjection = false, string? dispatcherMappingRole = "admin",
    (string Role, string Layer, string Action, bool IdentitySelectorRead)? adminAxesRegisteredOperation = null,
    string? secondDispatcherMappingRole = null, string? capabilityRequirementRole = null,
    // Round 21: the axes-registered manifest ResolveActiveManifestAsync resolves for
    // adminAxesRegisteredOperation's own OWN explicit capability_requirement -- distinct from
    // capabilityRequirementRole above, which is the BARE target_ref manifest's own (LoadByIdAsync).
    string? axesManifestCapabilityRequirementRole = null)
    : ManifestRepository(NullLogger<ManifestRepository>.Instance)
{
    public override Task<ManifestRecord?> LoadByIdAsync(Guid manifestId, CancellationToken ct = default)
    {
        if (manifestId != knownManifestId) return Task.FromResult<ManifestRecord?>(null);

        var entries = new List<object> { new { type = "runtime_mapping", runtime_destination = "admin_runtime" } };
        // Round 19: explicit capability_requirement, when set, overrides the admin_runtime-inferred
        // "admin" requirement (capability_requirement's own role check is independently Ordinal
        // case-sensitive, unaffected by this class's dispatcher_mapping-role case-insensitivity
        // fix) -- lets a test isolate ONLY the dispatcher_mapping.role comparison under test.
        if (capabilityRequirementRole is not null)
        {
            entries.Add(new { type = "capability_requirement", required_role = capabilityRequirementRole });
        }
        if (dispatcherMappingLayer is not null && dispatcherMappingAction is not null)
        {
            entries.Add(new
            {
                type = "dispatcher_mapping",
                role = dispatcherMappingRole,
                target = "manifest",
                layer = dispatcherMappingLayer,
                action = dispatcherMappingAction,
            });
            // Round 19: a SECOND dispatcher_mapping entry for the SAME (layer, action), to test
            // HasConflictingDispatcherMappingEntries's disagreeing/agreeing-role fail-close.
            if (secondDispatcherMappingRole is not null)
            {
                entries.Add(new
                {
                    type = "dispatcher_mapping",
                    role = secondDispatcherMappingRole,
                    target = "manifest",
                    layer = dispatcherMappingLayer,
                    action = dispatcherMappingAction,
                });
            }
        }
        if (hasUiProjection)
        {
            entries.Add(new
            {
                type = "ui_projection",
                packageIds = new[] { Guid.NewGuid().ToString() },
                layoutId = Guid.NewGuid().ToString(),
            });
        }

        return Task.FromResult<ManifestRecord?>(new ManifestRecord(
            ManifestId: manifestId,
            RelationRegistryId: null,
            Topology: JsonSerializer.SerializeToElement(entries.ToArray()).EnumerateArray().ToArray(),
            Status: status));
    }

    public override Task<ManifestRecord?> ResolveActiveManifestAsync(
        string? role, string? target, string? layer, string? action, CancellationToken ct = default)
    {
        if (adminAxesRegisteredOperation is { } reg &&
            string.Equals(target, "admin", StringComparison.Ordinal) &&
            string.Equals(layer, reg.Layer, StringComparison.Ordinal) &&
            string.Equals(action, reg.Action, StringComparison.Ordinal) &&
            string.Equals(role, reg.Role, StringComparison.Ordinal))
        {
            var axesEntries = new List<object>
            {
                new
                {
                    type = "dispatcher_mapping", role = reg.Role, target = "admin", layer = reg.Layer, action = reg.Action,
                    identity_selector_read = reg.IdentitySelectorRead,
                },
            };
            if (axesManifestCapabilityRequirementRole is not null)
            {
                axesEntries.Add(new { type = "capability_requirement", required_role = axesManifestCapabilityRequirementRole });
            }
            return Task.FromResult<ManifestRecord?>(new ManifestRecord(
                ManifestId: Guid.NewGuid(),
                RelationRegistryId: null,
                Topology: JsonSerializer.SerializeToElement(axesEntries.ToArray()).EnumerateArray().ToArray(),
                Status: "active"));
        }
        return Task.FromResult<ManifestRecord?>(null);
    }

    public override Task<IReadOnlyList<ManifestListItem>> ListManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ManifestListItem>>([]);

    public override Task<ManifestDetailRecord?> LoadDetailByIdAsync(Guid manifestId, CancellationToken ct = default) =>
        Task.FromResult<ManifestDetailRecord?>(null);

    public override Task<int> CountActiveAxisConflictsAsync(
        string role, string target, string layer, string action, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> CreateDraftAsync(
        Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdateDraftAsync(
        Guid manifestId, Guid? relationRegistryId, IReadOnlyList<JsonElement> topology, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedDraft();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> PromoteAsync(
        Guid manifestId, IReadOnlySet<string> allowedRuntimeDestinations, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedLifecycle();

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> DeprecateAsync(
        Guid manifestId, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedLifecycle();

    public override Task<IReadOnlyList<PromotionManifestListItem>> ListPromotionManifestsAsync(
        string? statusFilter, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.EmptyPromotionList(statusFilter, ct);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> UpdatePromotionMetadataDraftAsync(
        Guid manifestId, JsonElement promotionEntry, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();

    public override Task<int> CountActivePromotionKeyConflictsAsync(
        string manifestKey, string versionLabel, Guid? excludeManifestId, CancellationToken ct = default) =>
        Task.FromResult(0);

    public override Task<(ManifestDetailRecord? Manifest, ValidationError? Error)> MergeTopologyExtensionDraftAsync(
        Guid manifestId, string entryType, JsonElement entryBody, CancellationToken ct = default) =>
        ManifestRepositoryStubDefaults.NotImplementedMerge();
}

internal sealed class StubSuccessRuntime : IDispatchableRuntime
{
    public Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default) =>
        Task.FromResult(new EndpointResponseDto(
            Success: true,
            Emission: new Emission(null, null, null, [], null, [], null, null),
            Errors: []));
}

internal sealed class CapturingRuntime : IDispatchableRuntime
{
    public bool Called { get; private set; }
    public Guid? ManifestId { get; private set; }

    public Task<EndpointResponseDto> ExecuteAsync(
        EndpointRequestDto request, Guid? manifestId, CancellationToken ct = default)
    {
        Called = true;
        ManifestId = manifestId;
        return Task.FromResult(new EndpointResponseDto(
            Success: true,
            Emission: new Emission(null, null, null, [], null, [], null, null),
            Errors: []));
    }
}
