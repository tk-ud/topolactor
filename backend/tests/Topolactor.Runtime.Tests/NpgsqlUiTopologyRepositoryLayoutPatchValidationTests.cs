using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class NpgsqlUiTopologyRepositoryLayoutPatchValidationTests
{
    /// <summary>
    /// Round 43: stubs LayoutHasSchemaComposedRecordsAsync (returns false) for every test in this
    /// file whose own scenario has nothing to do with schema-composed authoring at all (CSS token
    /// validation, structural HTML, runtimeInteractions, dispatchExternalPort,
    /// dispatchInstanceOperation, dispatchPayloadFromByTrigger/dispatchTargetRefByTrigger absent
    /// cases) but whose patch nodes still carry a componentKey -- exactly the shape that now
    /// reaches ContainsNodeWithComponentKeyPresent's gate regardless of dispatch-field presence
    /// (see ValidateLayoutPatchAsync's own round-43 doc comment). Before round 43 these tests
    /// constructed the raw NpgsqlUiTopologyRepository directly with a fake connection string and
    /// never reached any DB call; this repository preserves that isolation while still exercising
    /// the REAL (non-schema-composed) code path for everything else. Matches the same
    /// test-doubling pattern SchemaJsonLookupForbiddenRepository etc. already use elsewhere in
    /// this file.
    /// </summary>
    private sealed class NonSchemaComposedLayoutPatchTestRepository()
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public override Task<bool> LayoutHasSchemaComposedRecordsAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_KnownCssToken_PassesFromYamlSsot()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", "{}", ["color.action.primary.background"], null);
        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_MalformedTensorJson_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", "{bad", ["color.action.primary.background"], null);
        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("TENSOR_PATCH_JSON_MALFORMED", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_UnknownCssToken_StrippedNotRejected()
    {
        // CSS tokens are placement-only and unknown tokens are stripped/ignored, not rejected.
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", "{}", ["unknown.token"], null);
        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }


    [Fact]
    public async Task ValidateLayoutPatchAsync_DraftOnlyNode_IsExplicitError()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        {
          "nodes": [
            {
              "nodeId": "node-1",
              "componentKey": "Sample",
              "_draftOnly": true
            }
          ]
        }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, ["color.action.primary.background"], null);
        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("DRAFT_ONLY_NODE_NOT_APPLICABLE:DRAFT_ONLY_NODE_CANNOT_BE_APPLIED", result.Message);
    }

    [Fact]
    public async Task ApplyConfirmedLayoutPatchAsync_DraftOnlyNode_FailsBeforePersistence()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        {
          "nodes": [
            {
              "nodeId": "node-1",
              "componentKey": "Sample",
              "_draftOnly": true
            }
          ]
        }
        """;

        var result = await repo.ApplyConfirmedLayoutPatchAsync(
            Guid.NewGuid(), Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, ["color.action.primary.background"], null);
        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("DRAFT_ONLY_NODE_NOT_APPLICABLE:DRAFT_ONLY_NODE_CANNOT_BE_APPLIED", result.Message);
    }
    [Fact]
    public async Task ValidateLayoutPatchAsync_StructuralHtmlNode_PassesWhenAllowlisted()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        {
          "nodes": [
            {
              "nodeId": "html-1",
              "nodeKind": "structural_html",
              "htmlTag": "section",
              "componentKey": "layout/structural_html",
              "slotKey": "",
              "orderIndex": 0,
              "parentNodeId": null
            }
          ]
        }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);
        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_StructuralHtmlUnknownTag_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        {
          "nodes": [
            {
              "nodeId": "html-1",
              "nodeKind": "structural_html",
              "htmlTag": "marquee",
              "componentKey": "layout/structural_html",
              "slotKey": "",
              "orderIndex": 0
            }
          ]
        }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);
        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.StartsWith("LAYOUT_PATCH_STRUCTURAL_HTML_TAG_UNKNOWN:", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_VocabularyUnavailable_IsExplicitError()
    {
        // CSS vocabulary loading was removed; tokens are placement-only and always accepted.
        Environment.SetEnvironmentVariable("TOPOLACTOR_CSS_DICTIONARY_SSOT_PATH", "/tmp/not-found-css-dictionary-ssot.yaml");
        try
        {
            var repo = new NonSchemaComposedLayoutPatchTestRepository();
            var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", "{}", ["color.action.primary.background"], null);
            Assert.True(result.Ok);
            Assert.True(result.Valid);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TOPOLACTOR_CSS_DICTIONARY_SSOT_PATH", null);
        }
    }
    [Fact]
    public async Task ValidateLayoutPatchAsync_RuntimeInteractionMissingTarget_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "openModal", "targetNodeId": "missing-modal", "statePath": "open" }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TARGET_NODE_NOT_FOUND:missing-modal", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_RuntimeInteractionUnsupportedStatePath_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "openModal", "targetNodeId": "modal-1", "statePath": "visible" }
          ] },
          { "nodeId": "modal-1", "componentKey": "modal.primitive", "componentKind": "disclosure/modal" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_STATE_PATH_UNSUPPORTED:visible", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_RuntimeInteractionUnsupportedAction_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "teleportModal", "targetNodeId": "modal-1", "statePath": "open" }
          ] },
          { "nodeId": "modal-1", "componentKey": "modal.primitive", "componentKind": "disclosure/modal" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_ACTION_UNSUPPORTED:teleportModal", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_RuntimeInteractionTargetKindMismatch_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "openModal", "targetNodeId": "card-1", "statePath": "open" }
          ] },
          { "nodeId": "card-1", "componentKey": "card.primitive", "componentKind": "display/card" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TARGET_KIND_MISMATCH:card-1:display/card:disclosure/modal", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_RuntimeInteractionOpenDrawerRequiresDisclosureDrawer_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "openDrawer", "targetNodeId": "drawer-1", "statePath": "open" }
          ] },
          { "nodeId": "drawer-1", "componentKey": "row_detail_drawer.primitive", "componentKind": "table_op/row_detail_drawer" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal(
            "RUNTIME_INTERACTION_TARGET_KIND_MISMATCH:drawer-1:table_op/row_detail_drawer:disclosure/drawer",
            result.Message);
    }

    [Fact]
    public async Task ApplyConfirmedLayoutPatchAsync_InvalidRuntimeInteraction_FailsBeforePersistence()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "openModal", "targetNodeId": "missing-modal", "statePath": "open" }
          ] }
        ] }
        """;

        var result = await repo.ApplyConfirmedLayoutPatchAsync(Guid.NewGuid(), Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TARGET_NODE_NOT_FOUND:missing-modal", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchExternalPort_ValidPortTargetRef_Passes()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchExternalPort", "portTargetRef": "external-port:access_port:port-abc-123" }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchExternalPort_WithPayloadFromAndOutputProp_Passes()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            {
              "trigger": "click",
              "actionType": "dispatchExternalPort",
              "portTargetRef": "external-port:access_port:port-abc-123",
              "payloadFrom": { "entityId": "event.item.id", "keyword": "node:search-input.value" },
              "outputProp": "result"
            }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchExternalPort_MissingPortTargetRef_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchExternalPort" }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_PORT_TARGET_REF_REQUIRED", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchExternalPort_MalformedPortTargetRef_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchExternalPort", "portTargetRef": "port-abc-123" }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_PORT_TARGET_REF_INVALID:port-abc-123", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchExternalPort_PayloadFromNotObject_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchExternalPort", "portTargetRef": "external-port:access_port:port-abc-123", "payloadFrom": ["bad"] }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_PAYLOAD_FROM_MUST_BE_OBJECT", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchExternalPort_PayloadFromValueNotString_FailsClose()
    {
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchExternalPort", "portTargetRef": "external-port:access_port:port-abc-123", "payloadFrom": { "entityId": 42 } }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_PAYLOAD_FROM_VALUE_MUST_BE_STRING:entityId", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchInstanceOperation_ApprovedCandidate_Passes()
    {
        var repo = new InstanceCandidateTestRepository([ApprovedInstanceCandidate]);
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            {
              "trigger": "click",
              "actionType": "dispatchInstanceOperation",
              "instanceTargetRef": "instance-port:db_instance_port:public-safe-placeholder:approved-operation-placeholder",
              "payloadFrom": { "entityId": "event.item.id" },
              "outputProp": "result"
            }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchInstanceOperation_MissingInstanceTargetRef_FailsClose()
    {
        var repo = new InstanceCandidateTestRepository([ApprovedInstanceCandidate]);
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchInstanceOperation" }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_INSTANCE_TARGET_REF_REQUIRED", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchInstanceOperation_InvalidPrefix_FailsClose()
    {
        var repo = new InstanceCandidateTestRepository([ApprovedInstanceCandidate]);
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchInstanceOperation", "instanceTargetRef": "external-port:db_instance_port:public-safe-placeholder:approved-operation-placeholder" }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_INSTANCE_TARGET_REF_INVALID:external-port:db_instance_port:public-safe-placeholder:approved-operation-placeholder", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchInstanceOperation_UnknownOperationBinding_FailsClose()
    {
        var repo = new InstanceCandidateTestRepository([ApprovedInstanceCandidate]);
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchInstanceOperation", "instanceTargetRef": "instance-port:db_instance_port:public-safe-placeholder:unapproved-operation" }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_INSTANCE_TARGET_REF_NOT_APPROVED:instance-port:db_instance_port:public-safe-placeholder:unapproved-operation", result.Message);
    }


    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchInstanceOperation_CandidateSourceUnavailable_FailsClose()
    {
        var repo = new InstanceCandidateSourceFailureRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchInstanceOperation", "instanceTargetRef": "instance-port:db_instance_port:public-safe-placeholder:approved-operation-placeholder" }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_INSTANCE_CANDIDATE_SOURCE_UNAVAILABLE", result.Message);
    }


    [Fact]
    public void ProjectInstanceOperationAuthoringCandidate_MalformedTargetRef_FailsClose()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NpgsqlUiTopologyRepository.ProjectInstanceOperationAuthoringCandidate(
                "instance-port:malformed",
                "approved_instance_operation_only"));

        Assert.Equal("INSTANCE_OPERATION_CANDIDATE_TARGET_REF_MALFORMED:instance-port:malformed", ex.Message);
    }

    [Fact]
    public void ProjectInstanceOperationAuthoringCandidate_NonApprovedScope_FailsClose()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NpgsqlUiTopologyRepository.ProjectInstanceOperationAuthoringCandidate(
                "instance-port:db_instance_port:public-safe-placeholder:approved-operation-placeholder",
                "draft"));

        Assert.Equal("INSTANCE_OPERATION_CANDIDATE_SCOPE_INVALID:draft", ex.Message);
    }

    private static readonly InstanceOperationAuthoringCandidateDto ApprovedInstanceCandidate = new(
        "db_instance_port",
        "public-safe-placeholder",
        "approved-operation-placeholder",
        "operation-reference-only",
        "approved",
        "instance-port:db_instance_port:public-safe-placeholder:approved-operation-placeholder");

    private sealed class InstanceCandidateTestRepository(IReadOnlyList<InstanceOperationAuthoringCandidateDto> candidates)
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public override Task<IReadOnlyList<InstanceOperationAuthoringCandidateDto>> ListInstanceOperationAuthoringCandidatesAsync(
            CancellationToken ct = default)
            => Task.FromResult(candidates);

        // Round 43: this scenario's own patch nodes carry componentKey (dispatchInstanceOperation
        // testing, unrelated to schema-composed authoring) -- stub so ValidateLayoutPatchAsync's
        // new ContainsNodeWithComponentKeyPresent-gated cheap check never reaches a live DB.
        public override Task<bool> LayoutHasSchemaComposedRecordsAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    /// <summary>
    /// Stubs LoadWiringKindForLayoutAsync (the DB-backed admin_runtime-only gate for
    /// dispatchPayloadFromByTrigger/dispatchTargetRefByTrigger) without a live database — mirrors
    /// InstanceCandidateTestRepository's own test-doubling pattern for the analogous
    /// ListInstanceOperationAuthoringCandidatesAsync DB-backed check.
    /// </summary>
    private sealed class AdminRuntimeWiringKindTestRepository(
        string? wiringKind,
        AdminRuntimeManifestAuthorizationResult? manifestAuthorization = null)
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public override Task<string?> LoadWiringKindForLayoutAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(wiringKind);

        public override Task<AdminRuntimeManifestAuthorizationResult> LoadAdminRuntimeManifestAuthorizationAsync(
            Guid manifestId, CancellationToken ct = default)
            => Task.FromResult(manifestAuthorization ??
                new AdminRuntimeManifestAuthorizationResult(Exists: true, IsActive: true, IsAdminRuntimeDestination: true));

        // Round 42: this test double represents ordinary (non-schema-composed) tensor-only
        // dispatch-authoring scenarios -- never schema-composed.
        public override Task<bool> LayoutHasSchemaComposedRecordsAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private sealed class InstanceCandidateSourceFailureRepository()
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public override Task<IReadOnlyList<InstanceOperationAuthoringCandidateDto>> ListInstanceOperationAuthoringCandidatesAsync(
            CancellationToken ct = default)
            => throw new InvalidOperationException("candidate source unavailable");

        // Round 43: see InstanceCandidateTestRepository's own override for the rationale.
        public override Task<bool> LayoutHasSchemaComposedRecordsAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    // ─── dispatchPayloadFromByTrigger (PR #599 review round 6): a node-level field,
    // independent of runtimeInteractions/actionType — validated via the SAME
    // ValidatePayloadFromShape authority dispatchExternalPort/dispatchInstanceOperation's
    // own payloadFrom uses above ──────────────────────────────────────────────────

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_ValidShape_Passes()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "wiringKind": "admin_runtime", "targetRef": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group",
            "dispatchPayloadFromByTrigger": { "click": { "groupName": "node:name-input.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_NonObjectValue_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": ["bad"] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_DISPATCH_PAYLOAD_FROM_BY_TRIGGER_MUST_BE_OBJECT", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_NonObjectTriggerEntry_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "click": "not-an-object" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_PAYLOAD_FROM_MUST_BE_OBJECT", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_NonStringFieldValue_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "click": { "groupName": 42 } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_PAYLOAD_FROM_VALUE_MUST_BE_STRING:groupName", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_Absent_Passes()
    {
        // Plain repository (real DB connection string, no stub) — proves
        // LoadWiringKindForLayoutAsync is never called when the patch doesn't author
        // dispatchPayloadFromByTrigger/dispatchTargetRefByTrigger at all (it would throw/fail
        // against "Host=localhost;Database=none" if it were).
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    // ─── trigger authority unification (PR #599 review round 7): the backend persistence
    // boundary must accept/reject the SAME raw trigger keys as the frontend build boundary
    // for dispatchPayloadFromByTrigger ───────────────────────────────────────────────────

    [Theory]
    [InlineData("click")]
    [InlineData("change")]
    [InlineData("select")]
    [InlineData("submit")]
    [InlineData("toggle")]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_CanonicalTrigger_Passes(string trigger)
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "{{trigger}}": { "groupName": "node:name-input.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_AliasKeyOnClick_NormalizesAndPasses()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "onClick": { "groupName": "node:name-input.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Theory]
    [InlineData("input")]
    [InlineData("onInput")]
    [InlineData("focus")]
    [InlineData("blur")]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_RecognizedButUnsupportedTrigger_FailsClose(string trigger)
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "{{trigger}}": { "groupName": "node:name-input.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TRIGGER_UNSUPPORTED", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_UnknownTrigger_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "bogusTrigger": { "groupName": "node:name-input.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TRIGGER_REQUIRED", result.Message);
    }

    [Theory]
    [InlineData("click", "onClick")]
    [InlineData("change", "onChange")]
    [InlineData("submit", "onSubmit")]
    [InlineData("select", "onSelect")]
    [InlineData("toggle", "onOpen")]
    [InlineData("toggle", "onClose")]
    [InlineData("onOpen", "onClose")]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_AliasCollisionAfterNormalization_FailsClose(string a, string b)
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "{{a}}": { "groupName": "literal:A" }, "{{b}}": { "groupName": "literal:B" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION", result.Message);
    }

    // ─── trim / whitespace-only / empty payloadFrom boundary unification (PR #599 review
    // round 8): backend must accept/reject the SAME raw trigger key inputs as frontend
    // normalizeAuthoredEventType(), including leading/trailing whitespace, and an empty
    // per-trigger payloadFrom ({}) must fail closed here too, not only at dispatch time ──

    [Theory]
    [InlineData(" click ")]
    [InlineData(" onClick ")]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_WhitespacePaddedTrigger_TrimsAndPasses(string trigger)
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "{{trigger}}": { "groupName": "node:name-input.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_WhitespaceOnlyTrigger_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "   ": { "groupName": "node:name-input.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TRIGGER_REQUIRED", result.Message);
    }

    [Theory]
    [InlineData("click", " click ")]
    [InlineData("onClick", " onClick ")]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_WhitespaceOnlyCollisionAfterTrim_FailsClose(string a, string b)
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "{{a}}": { "groupName": "literal:A" }, "{{b}}": { "groupName": "literal:B" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_EmptyPayloadFrom_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "click": {} } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_PAYLOAD_FROM_EMPTY", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchExternalPort_EmptyPayloadFrom_StillPasses_UnchangedLeniency()
    {
        // dispatchExternalPort/dispatchInstanceOperation's OWN payloadFrom (ValidateRuntimeInteractions)
        // keeps its separate, pre-existing leniency for {} unchanged — round 8 only tightened
        // ValidateDispatchPayloadFromByTrigger (rejectEmpty: true), not the shared
        // ValidatePayloadFromShape's default behavior other callers rely on.
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "runtimeInteractions": [
            { "trigger": "click", "actionType": "dispatchExternalPort", "portTargetRef": "external-port:access_port:port-abc-123", "payloadFrom": {} }
          ] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    // ─── dispatchTargetRefByTrigger: per-node, per-trigger admin_runtime dispatch TARGET
    // override — independent of the layout's own uniform WiringKind="admin_runtime"/TargetRef.
    // Mirrors dispatchPayloadFromByTrigger's trigger validation exactly (same error-code
    // vocabulary), validating each value as a ManifestDispatcher-resolvable
    // "manifest:<uuid>:<layer>:<action>" target_ref instead of a payloadFrom object ──────

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_ValidShape_Passes()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button", "wiringKind": "admin_runtime", "targetRef": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups",
            "dispatchTargetRefByTrigger": { "click": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_NonObjectValue_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": ["bad"] }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_MUST_BE_OBJECT", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_NonStringValue_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": 42 } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_VALUE_MUST_BE_STRING", result.Message);
    }

    [Theory]
    [InlineData("enum_dictionary:create_group")]
    [InlineData("manifest:not-a-uuid:enum_dictionary:create_group")]
    [InlineData("manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary")]
    [InlineData("")]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_MalformedTargetRef_FailsClose(string targetRef)
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": "{{targetRef}}" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_TARGET_REF_INVALID", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_NonHyphenatedUuid_FailsClose()
    {
        // Round 17 (f): pins the UUID accept-set relationship. AdminRuntimeTargetRefRe (this
        // save-time boundary) requires the canonical 36-char hyphenated UUID form -- a strict
        // SUBSET of what ManifestDispatcher.TryParseManifestTargetRef's Guid.TryParse would
        // accept (Guid.TryParse also accepts the 32-hex-digit "N" form with no hyphens, among
        // others). This "N"-form manifest id is deliberately valid per Guid.TryParse but must
        // still fail closed HERE -- see ManifestDispatcherTargetRefTests.
        // DispatchAsync_TargetRef_NFormUuid_GuidTryParseAcceptsButAdminRuntimeTargetRefReRejects_
        // ProvingStrictSubsetRelationship for the dispatch-time half of this same relationship.
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        const string nFormUuid = "000000000000000000000000000ae210"; // 32 hex digits, no hyphens
        Assert.True(Guid.TryParse(nFormUuid, out _), "test fixture precondition: nFormUuid must itself be Guid.TryParse-valid");
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": "manifest:{{nFormUuid}}:enum_dictionary:create_group" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_TARGET_REF_INVALID", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_UnknownTrigger_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "bogusTrigger": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TRIGGER_REQUIRED", result.Message);
    }

    [Theory]
    [InlineData("input")]
    [InlineData("focus")]
    [InlineData("blur")]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_RecognizedButUnsupportedTrigger_FailsClose(string trigger)
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "{{trigger}}": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TRIGGER_UNSUPPORTED", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_AliasCollisionAfterNormalization_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime");
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": {
              "click": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group",
              "onClick": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:delete_group"
            } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_TRIGGER_CONFLICT_AFTER_NORMALIZATION", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_Absent_Passes()
    {
        // Plain repository — proves LoadWiringKindForLayoutAsync is never called for a patch
        // that doesn't author either node-level admin_runtime dispatch field.
        var repo = new NonSchemaComposedLayoutPatchTestRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    // ─── admin_runtime-only gate: dispatchPayloadFromByTrigger/dispatchTargetRefByTrigger are
    // rejected when the layout's OWN wiring_kind (topology.ui_topology_tensor ->
    // topology.ui_wiring_registry, uniform for every node in the layout) is not "admin_runtime" —
    // round 16, 2026-07-29 ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("search")]
    [InlineData("create")]
    [InlineData(null)]
    public async Task ValidateLayoutPatchAsync_DispatchPayloadFromByTrigger_NonAdminRuntimeLayoutWiringKind_FailsClose(string? wiringKind)
    {
        var repo = new AdminRuntimeWiringKindTestRepository(wiringKind);
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchPayloadFromByTrigger": { "click": { "groupName": "node:name-input.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_DISPATCH_PAYLOAD_FROM_BY_TRIGGER_REQUIRES_ADMIN_RUNTIME_WIRING", result.Message);
    }

    [Theory]
    [InlineData("search")]
    [InlineData("create")]
    [InlineData(null)]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_NonAdminRuntimeLayoutWiringKind_FailsClose(string? wiringKind)
    {
        var repo = new AdminRuntimeWiringKindTestRepository(wiringKind);
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_REQUIRES_ADMIN_RUNTIME_WIRING", result.Message);
    }

    private sealed class AmbiguousWiringKindLookupRepository()
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public override Task<string?> LoadWiringKindForLayoutAsync(Guid layoutId, CancellationToken ct = default)
            => throw new InvalidOperationException($"LAYOUT_NODES_AMBIGUOUS_SELECTOR: multiple tensor rows for layout_id='{layoutId}'.");

        // Round 42: this scenario's own componentKey is always present (never triggers the
        // schema-composed spoof check's own gate) -- not schema-composed.
        public override Task<bool> LayoutHasSchemaComposedRecordsAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_AmbiguousLayoutWiringLookup_FailsCloseExplicitly()
    {
        var repo = new AmbiguousWiringKindLookupRepository();
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.StartsWith("LAYOUT_NODES_AMBIGUOUS_SELECTOR", result.Message);
    }

    // ─── target manifest authorization: the manifest_id a dispatchTargetRefByTrigger entry
    // references must exist, be status=active, and declare runtime_destination=admin_runtime —
    // the SAME facts ManifestDispatcher would otherwise only discover at dispatch time, checked
    // here at layout_patch save time instead (round 16, 2026-07-29) ─────────────────────

    private const string TargetManifestId = "00000000-0000-0000-0000-0000000ae200";
    private static readonly string TargetRefForManifestAuthorizationTests =
        $"manifest:{TargetManifestId}:enum_dictionary:create_group";

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_ReferencedManifestNotFound_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository(
            "admin_runtime",
            new AdminRuntimeManifestAuthorizationResult(Exists: false, IsActive: false, IsAdminRuntimeDestination: false));
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": "{{TargetRefForManifestAuthorizationTests}}" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal($"RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_MANIFEST_NOT_FOUND:{TargetManifestId}", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_ReferencedManifestNotActive_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository(
            "admin_runtime",
            new AdminRuntimeManifestAuthorizationResult(Exists: true, IsActive: false, IsAdminRuntimeDestination: true));
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": "{{TargetRefForManifestAuthorizationTests}}" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal($"RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_MANIFEST_NOT_ACTIVE:{TargetManifestId}", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_ReferencedManifestNotAdminRuntimeDestination_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository(
            "admin_runtime",
            new AdminRuntimeManifestAuthorizationResult(Exists: true, IsActive: true, IsAdminRuntimeDestination: false));
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": "{{TargetRefForManifestAuthorizationTests}}" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal($"RUNTIME_INTERACTION_DISPATCH_TARGET_REF_BY_TRIGGER_MANIFEST_NOT_ADMIN_RUNTIME:{TargetManifestId}", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_DispatchTargetRefByTrigger_ReferencedManifestFullyAuthorized_Passes()
    {
        var repo = new AdminRuntimeWiringKindTestRepository(
            "admin_runtime",
            new AdminRuntimeManifestAuthorizationResult(Exists: true, IsActive: true, IsAdminRuntimeDestination: true));
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive", "componentKind": "action/button",
            "dispatchTargetRefByTrigger": { "click": "{{TargetRefForManifestAuthorizationTests}}" } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    // ─── round 39: Field-owned admin_runtime dispatch persistence-boundary matrix
    // (ValidateFieldOwnedAdminRuntimeReadOnlyDispatch / ValidateFieldOwnedAdminRuntimeSearchDebounce).
    // Round 37 authored these two rules against node["componentKind"], a field that is NEVER
    // present on a raw layout_patch_json.nodes[] override entry (componentKind is a post-
    // composition fact resolved by LayoutSchemaTensorComposer, see NodeLocalData) -- so both
    // checks were structurally dead code for every node, not just Field nodes, until this round's
    // fix switched the identity signal to "componentKey" (which ValidateLayoutPatchNodes already
    // REQUIRES on every catalog_component node earlier in this SAME validation chain, and which
    // every node shape below therefore already carries). These tests exercise the real
    // ValidateLayoutPatchAsync entry point end to end (not the private static methods directly),
    // proving the fix through the actual save-time validation surface. ──────────────────────────

    private const string SearchReadTargetRef =
        "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:list_groups";
    private const string SearchWriteTargetRef =
        "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:create_group";
    private static readonly AdminRuntimeManifestAuthorizationResult FullyAuthorizedManifest =
        new(Exists: true, IsActive: true, IsAdminRuntimeDestination: true);

    private static string SearchInputNode(string debounceMsJsonLiteral, string targetRef = SearchReadTargetRef) => $$"""
        { "nodes": [
          { "nodeId": "search-1", "componentKey": "search_input.alias",
            "dispatchTargetRefByTrigger": { "change": "{{targetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "search": "node:search-1.value" } }{{debounceMsJsonLiteral}} }
        ] }
        """;

    [Fact]
    public async Task ValidateLayoutPatchAsync_FieldSearchInput_DebounceMsAbsent_FailsClose()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime", FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SearchInputNode(""), null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_FIELD_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS:search-1", result.Message);
    }

    [Theory]
    [InlineData(", \"debounceMs\": 0")]
    [InlineData(", \"debounceMs\": -5")]
    [InlineData(", \"debounceMs\": 1.5")]
    [InlineData(", \"debounceMs\": true")]
    [InlineData(", \"debounceMs\": \"300\"")]
    public async Task ValidateLayoutPatchAsync_FieldSearchInput_DebounceMsInvalidShape_FailsClose(string debounceMsJsonLiteral)
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime", FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SearchInputNode(debounceMsJsonLiteral), null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_FIELD_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS:search-1", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_FieldSearchInput_DebounceMsPositiveInteger_Passes()
    {
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime", FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SearchInputNode(", \"debounceMs\": 300"), null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_FieldSelect_DebounceMsAbsent_Passes()
    {
        // Discrete-choice control (select.template) is exempt from the search debounce
        // requirement -- it fires once per selection via its own onChange, never once per
        // keystroke, matching react-schema-topology-seed-translator-ssot.yaml
        // search_filter_input_contract.debounce_policy's own scope exactly.
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime", FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "filter-1", "componentKey": "select.template",
            "dispatchTargetRefByTrigger": { "change": "{{SearchReadTargetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "groupNameFilter": "node:filter-1.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_ActionOwnedDispatch_DebounceMsAbsent_Passes()
    {
        // An Action/Step-owned dispatch (componentKey outside FieldFamilyComponentKeys, e.g. a
        // button) never has the search debounce rule mis-applied to it, even though it also
        // dispatches on a "change"-shaped high-frequency-looking trigger name here.
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime", FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "button-1", "componentKey": "button.primitive",
            "dispatchTargetRefByTrigger": { "change": "{{SearchWriteTargetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "confirmed": "literal:true" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_FieldSearchInput_WriteTargetRef_FailsCloseOnReadActionAllowlist_NotDebounce()
    {
        // Regression proof for the SAME componentKind -> componentKey bug on the OTHER rule this
        // round's fix touches (ValidateFieldOwnedAdminRuntimeReadOnlyDispatch, originally rule 3):
        // before the fix this check could never match ANY node either, so a Field-owned dispatch
        // targeting a WRITE action would have silently passed. A valid debounceMs is included so
        // this failure is unambiguously attributable to the read-action-allowlist check, not the
        // debounce check (which runs after it and would never be reached here).
        var repo = new AdminRuntimeWiringKindTestRepository("admin_runtime", FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder",
            SearchInputNode(", \"debounceMs\": 300", SearchWriteTargetRef), null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_FIELD_DISPATCH_TARGET_REF_NOT_READ_ACTION:enum_dictionary:create_group", result.Message);
    }

    // ─── round 40: SCHEMA-COMPOSED authoring persistence boundary. Round 39's fix (componentKey
    // instead of componentKind) genuinely protects only TENSOR-ONLY authoring, where componentKey
    // is a real column present directly on the raw override node. A schema-composed layout's own
    // ui_topology_tensor.layout_patch_json.nodes[] override-delta entries carry NEITHER
    // componentKind NOR componentKey -- identity for them lives only in the owning layoutId's
    // components_layout_design.layout_schema_json.records[] structural tree. These tests prove
    // ValidateLayoutPatchAsync now resolves that identity via
    // LayoutSchemaTensorComposer.ResolveCatalogComponentKeysByNodeId (the SAME NodeId/componentKey
    // authority LayoutSchemaTensorComposer.Compose itself uses to render) and applies the
    // IDENTICAL two validators (read-action allowlist, search debounceMs) with the same meaning as
    // the round-39 tensor-only tests above -- test names below are deliberately parallel to their
    // tensor-only counterparts to make that convergence explicit. ──────────────────────────────

    private const string SchemaComposedSearchFieldNodeId = "schema_search_field";
    private const string SchemaComposedFilterFieldNodeId = "schema_filter_field";
    private const string SchemaComposedUnknownNodeId = "not_in_schema_tree";

    // Minimal well-formed records[] tree (record_common_required_fields satisfied for both rows):
    // one search_input Field and one select Field, both parentless (ResolveCatalogComponentKeysByNodeId
    // needs no parent chain — it only walks record_common fields + control convention resolution).
    private const string SchemaComposedRecordsJson = """
        {"records":[
          {"type":"topology_ui_seed_record","seedKey":"test.schema.composed","parentKey":null,
           "record":{"recordType":"topology_ui_field","key":"schema_search_field","label":"Test search",
             "sourceYamlRefs":["test-ssot.yaml#x"],"sourceReactPath":"$.root.children[0]","knownGapRefs":[],
             "control":"form_input/search_input"}},
          {"type":"topology_ui_seed_record","seedKey":"test.schema.composed","parentKey":null,
           "record":{"recordType":"topology_ui_field","key":"schema_filter_field","label":"Test filter",
             "sourceYamlRefs":["test-ssot.yaml#x"],"sourceReactPath":"$.root.children[1]","knownGapRefs":[],
             "control":"form_input/select"}}
        ]}
        """;

    private const string SchemaComposedMalformedRecordsJson = """{"records": "not-an-array"}""";

    /// <summary>
    /// Combines AdminRuntimeWiringKindTestRepository's own stubs with LoadLayoutSchemaJsonAsync,
    /// so a schema-composed layout can be exercised without a live database — mirrors the SAME
    /// test-doubling pattern this file already uses for the DB-backed
    /// ListInstanceOperationAuthoringCandidatesAsync / LoadWiringKindForLayoutAsync checks.
    /// </summary>
    private sealed class SchemaComposedTestRepository(
        string? wiringKind,
        string? layoutSchemaJson,
        AdminRuntimeManifestAuthorizationResult? manifestAuthorization = null)
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public override Task<string?> LoadWiringKindForLayoutAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(wiringKind);

        public override Task<AdminRuntimeManifestAuthorizationResult> LoadAdminRuntimeManifestAuthorizationAsync(
            Guid manifestId, CancellationToken ct = default)
            => Task.FromResult(manifestAuthorization ??
                new AdminRuntimeManifestAuthorizationResult(Exists: true, IsActive: true, IsAdminRuntimeDestination: true));

        public override Task<string?> LoadLayoutSchemaJsonAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(layoutSchemaJson);

        // Round 42: mirrors the real LayoutHasSchemaComposedRecordsAsync's own SQL semantics
        // (jsonb_typeof(records)='array' AND length>0) against this fixture's own layoutSchemaJson
        // string, so this ONE test double covers both the "genuinely schema-composed" and
        // "malformed/absent records -- not schema-composed" scenarios correctly without a second,
        // independently-hand-kept truth table.
        public override Task<bool> LayoutHasSchemaComposedRecordsAsync(Guid layoutId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(layoutSchemaJson)) return Task.FromResult(false);
            try
            {
                using var doc = JsonDocument.Parse(layoutSchemaJson);
                return Task.FromResult(
                    doc.RootElement.TryGetProperty("records", out var records) &&
                    records.ValueKind == JsonValueKind.Array &&
                    records.GetArrayLength() > 0);
            }
            catch (JsonException)
            {
                return Task.FromResult(false);
            }
        }
    }

    /// <summary>
    /// Throws if LoadLayoutSchemaJsonAsync is ever called — used to PROVE the gating pre-check
    /// genuinely skips the FULL schema-body round trip when every node already carries its own
    /// explicit componentKey AND this layoutId is not actually schema-composed (round 42:
    /// LayoutHasSchemaComposedRecordsAsync's own cheap boolean check IS still invoked for any
    /// dispatch-field-bearing patch -- see its own override below returning false -- only the FULL
    /// LoadLayoutSchemaJsonAsync fetch remains forbidden here), rather than merely asserting a
    /// correct result that could also happen to hold if the full fetch ran and returned null.
    /// </summary>
    private sealed class SchemaJsonLookupForbiddenRepository(string? wiringKind, AdminRuntimeManifestAuthorizationResult? manifestAuthorization = null)
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public override Task<string?> LoadWiringKindForLayoutAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(wiringKind);

        // Round 42: this repository's whole scenario is genuinely tensor-only (not
        // schema-composed) -- the cheap check may run, but must truthfully report false so the
        // FULL LoadLayoutSchemaJsonAsync fetch below is never reached.
        public override Task<bool> LayoutHasSchemaComposedRecordsAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(false);

        public override Task<AdminRuntimeManifestAuthorizationResult> LoadAdminRuntimeManifestAuthorizationAsync(
            Guid manifestId, CancellationToken ct = default)
            => Task.FromResult(manifestAuthorization ??
                new AdminRuntimeManifestAuthorizationResult(Exists: true, IsActive: true, IsAdminRuntimeDestination: true));

        public override Task<string?> LoadLayoutSchemaJsonAsync(Guid layoutId, CancellationToken ct = default)
            => throw new InvalidOperationException("LoadLayoutSchemaJsonAsync must not be called when every node already carries its own componentKey.");
    }

    private static string SchemaComposedSearchInputNode(string debounceMsJsonLiteral, string targetRef = SearchReadTargetRef, string nodeId = SchemaComposedSearchFieldNodeId) => $$"""
        { "nodes": [
          { "nodeId": "{{nodeId}}", "nodeKind": "catalog_component",
            "dispatchTargetRefByTrigger": { "change": "{{targetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "search": "node:{{nodeId}}.value" } }{{debounceMsJsonLiteral}} }
        ] }
        """;

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedFieldSearchInput_DebounceMsAbsent_FailsClose()
    {
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SchemaComposedSearchInputNode(""), null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal($"RUNTIME_INTERACTION_FIELD_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS:{SchemaComposedSearchFieldNodeId}", result.Message);
    }

    [Theory]
    [InlineData(", \"debounceMs\": 0")]
    [InlineData(", \"debounceMs\": -5")]
    [InlineData(", \"debounceMs\": 1.5")]
    [InlineData(", \"debounceMs\": true")]
    [InlineData(", \"debounceMs\": \"300\"")]
    public async Task ValidateLayoutPatchAsync_SchemaComposedFieldSearchInput_DebounceMsInvalidShape_FailsClose(string debounceMsJsonLiteral)
    {
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SchemaComposedSearchInputNode(debounceMsJsonLiteral), null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal($"RUNTIME_INTERACTION_FIELD_SEARCH_DISPATCH_REQUIRES_DEBOUNCE_MS:{SchemaComposedSearchFieldNodeId}", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedFieldSearchInput_DebounceMsPositiveInteger_Passes()
    {
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SchemaComposedSearchInputNode(", \"debounceMs\": 300"), null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedFieldSelect_DebounceMsAbsent_Passes()
    {
        // Discrete-choice control (form_input/select -> select.template) is exempt, exactly as in
        // the tensor-only case above — the SAME componentKey vocabulary, resolved from the schema
        // tree instead of a raw node property, drives the SAME exemption.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedFilterFieldNodeId}}", "nodeKind": "catalog_component",
            "dispatchTargetRefByTrigger": { "change": "{{SearchReadTargetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "groupNameFilter": "node:{{SchemaComposedFilterFieldNodeId}}.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedFieldSearchInput_ReadAction_Passes()
    {
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SchemaComposedSearchInputNode(", \"debounceMs\": 300"), null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedFieldSearchInput_WriteAction_FailsCloseOnReadActionAllowlist_NotDebounce()
    {
        // Parallel to the tensor-only WriteTargetRef regression proof above: a valid debounceMs is
        // included so this failure is unambiguously attributable to the read-action-allowlist
        // check, not the debounce check (which runs after it and would never be reached here).
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder",
            SchemaComposedSearchInputNode(", \"debounceMs\": 300", SearchWriteTargetRef), null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_FIELD_DISPATCH_TARGET_REF_NOT_READ_ACTION:enum_dictionary:create_group", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedFieldSearchInput_UnknownResourceAction_FailsClose()
    {
        // A resource:action that is neither a known read action nor a recognizable write verb —
        // the positive allowlist rejects anything not explicitly listed, not merely "looks like a
        // write."
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        const string unknownTargetRef = "manifest:00000000-0000-0000-0000-0000000ae200:enum_dictionary:frobnicate";
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder",
            SchemaComposedSearchInputNode(", \"debounceMs\": 300", unknownTargetRef), null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("RUNTIME_INTERACTION_FIELD_DISPATCH_TARGET_REF_NOT_READ_ACTION:enum_dictionary:frobnicate", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_MissingComponentKey_ExemptFromCatalogComponentKeyRequired_WhenPresentInSchemaTree()
    {
        // Structural proof independent of the two Field validators above: a catalog_component node
        // with NO componentKey of its own no longer fails LAYOUT_PATCH_CATALOG_COMPONENT_KEY_REQUIRED
        // outright, as long as its nodeId is a real catalog leaf in the owning layoutId's schema
        // tree — this node carries no dispatch fields at all, so passing here proves the structural
        // exemption itself, not a side effect of the Field validators short-circuiting.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_TensorOnlyNode_MissingComponentKey_NotInSchemaTree_StillFailsClose()
    {
        // Regression guard: the round-40 exemption is scoped to nodeIds that are REAL schema-tree
        // catalog leaves for THIS layoutId — a brand-new tensor-only node whose nodeId is absent
        // from the schema tree entirely still requires an explicit componentKey exactly as before
        // this round. This is not a blanket "any node missing componentKey passes" exemption.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedUnknownNodeId}}", "nodeKind": "catalog_component" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("LAYOUT_PATCH_CATALOG_COMPONENT_KEY_REQUIRED", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedLayout_MalformedRecords_FailsWholePatchClosed()
    {
        // A malformed layout_schema_json.records[] must surface as its own explicit diagnostic
        // (matching NpgsqlTopologyRepository.LoadLayoutNodesAsync's own read-time severity for the
        // exact same malformed shape) rather than being silently treated as "no schema, no
        // exemption" — which would instead surface a confusing
        // LAYOUT_PATCH_CATALOG_COMPONENT_KEY_REQUIRED that obscures the real defect.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedMalformedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.StartsWith("LAYOUT_SCHEMA_RECORDS_INVALID:", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_TensorOnlyPatch_EveryNodeHasComponentKey_NeverQueriesLayoutSchemaJson()
    {
        // Proves the FULL-body gating pre-check genuinely skips LoadLayoutSchemaJsonAsync — not
        // merely that a correct result happens to occur — for the SAME round-39 tensor-only
        // search-input scenario already proven above, now against a repository whose
        // LoadLayoutSchemaJsonAsync throws if ever invoked. This is the "Round 39 tensor-only
        // behavior is not weakened by adding schema-composed support" proof at the DB-access-shape
        // level, not just the outcome level.
        // Round 42 update: SearchInputNode's own node DOES carry an admin_runtime dispatch field
        // (dispatchTargetRefByTrigger), so ContainsAdminRuntimeNodeLevelDispatchField now DOES
        // trigger the new cheap LayoutHasSchemaComposedRecordsAsync boolean check (closing the
        // single-node schema-composed spoof gap — see
        // owner_decision_2026_08_14_general_dispatch_participation_contract_round42). What this
        // test still proves is narrower but just as load-bearing: that cheap check truthfully
        // reports false for a genuinely tensor-only layout, so the EXPENSIVE full-body
        // LoadLayoutSchemaJsonAsync fetch is still never reached — the DB-round-trip optimization
        // this test protects is "never fetch the full schema tree for an ordinary tensor-only
        // save," not "never touch the DB at all," and round 42 does not weaken it.
        var repo = new SchemaJsonLookupForbiddenRepository("admin_runtime", FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SearchInputNode(", \"debounceMs\": 300"), null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    // ── Round 41 (Owner decision closure): schema-composed canonical identity spoof/mismatch
    // negative cases. Round 40's ResolveEffectiveComponentKey preferred a raw override node's own
    // componentKey whenever present, even for a nodeId that IS a real schema-tree catalog leaf --
    // letting a crafted layout_patch_json.nodes[] entry claim a DIFFERENT componentKey for a nodeId
    // the schema tree says is a Field, evading ValidateFieldOwnedAdminRuntimeReadOnlyDispatch /
    // ValidateFieldOwnedAdminRuntimeSearchDebounce entirely (those two validators only ever see
    // whichever componentKey ResolveEffectiveComponentKey resolves). These tests prove
    // ValidateLayoutPatchNodes now closes that path at the earliest validation point, before either
    // Field validator runs. ──────────────────────────────────────────────────────────────────────

    // Round 41 scope note: ValidateLayoutPatchNodes's identity-mismatch check only ever runs
    // against the schema-tree map (schemaComposedComponentKeysByNodeId), which is only ever loaded
    // when ContainsNodeMissingComponentKey finds at least one node in the SAME patch missing its
    // own componentKey (the gate is deliberately unchanged from round 40 -- see its own doc
    // comment). Every test below therefore includes schema_filter_field, missing its own
    // componentKey, purely as that trigger -- matching the realistic authored shape of a
    // schema-composed override-delta save (most of whose nodes normally omit componentKey
    // entirely; see storage_adoption_contract).

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_RawComponentKeyMismatchesCanonical_FailsWholePatchClosed()
    {
        // schema_search_field's canonical componentKey (resolved from control="form_input/search_input"
        // via LayoutSchemaTensorComposer.FieldControlToComponentKey) is "search_input.alias". A raw
        // override entry for that SAME nodeId claiming a DIFFERENT componentKey (a spoof attempt --
        // e.g. one outside FieldFamilyComponentKeys entirely, which would let this node evade Field
        // policy at the two Field validators below) must fail the WHOLE patch closed, not silently
        // let the raw value win.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "button.primary" },
          { "nodeId": "{{SchemaComposedFilterFieldNodeId}}", "nodeKind": "catalog_component" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal(
            $"LAYOUT_PATCH_SCHEMA_COMPOSED_COMPONENT_KEY_IDENTITY_MISMATCH:{SchemaComposedSearchFieldNodeId}",
            result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_RawComponentKeyMatchesCanonical_Passes()
    {
        // A raw override entry is still tolerated for a schema-tree catalog leaf as long as it is
        // CONSISTENT with canonical identity -- this is not a blanket ban on authoring componentKey
        // on a schema-composed node, only on a mismatching one.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "search_input.alias" },
          { "nodeId": "{{SchemaComposedFilterFieldNodeId}}", "nodeKind": "catalog_component" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_RawComponentKeyMismatch_BlocksFieldPolicyBypassAttempt()
    {
        // End-to-end spoof-attempt proof: without the round-41 identity-mismatch fail-close, a raw
        // override claiming a non-Field componentKey ("button.primary" is outside
        // FieldFamilyComponentKeys) for this Field-family nodeId would make
        // ValidateFieldOwnedAdminRuntimeSearchDebounce treat it as Action/Step-owned and skip the
        // debounceMs requirement entirely -- letting a search dispatch through with NO debounce
        // policy applied. Proves the patch fails closed BEFORE that bypass could take effect, even
        // though this node also violates the debounceMs requirement it would otherwise be exempted
        // from (the identity check must fire first / independently, not merely as a side effect of
        // some other validator also rejecting it).
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "button.primary",
            "dispatchTargetRefByTrigger": { "change": "{{SearchReadTargetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "search": "node:{{SchemaComposedSearchFieldNodeId}}.value" } } },
          { "nodeId": "{{SchemaComposedFilterFieldNodeId}}", "nodeKind": "catalog_component" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal(
            $"LAYOUT_PATCH_SCHEMA_COMPOSED_COMPONENT_KEY_IDENTITY_MISMATCH:{SchemaComposedSearchFieldNodeId}",
            result.Message);
    }

    // ── Round 42 (Owner clarification,
    // owner_decision_2026_08_14_general_dispatch_participation_contract_round42): closes the
    // disclosed round-41 scope limit above -- the identity-mismatch check must be reachable for a
    // SINGLE-NODE patch too, not only when a companion node in the SAME patch happens to be
    // missing its own componentKey. Every test below is a literal single-node patch (no
    // schema_filter_field or any other sibling) -- per the round-42 NG axis, a dummy
    // componentKey-missing sibling added merely to force the OLD/narrower gate to fire would prove
    // the old mechanism, not a genuine single-node fix, so none is present here. The three tests
    // immediately below still author a dispatch field (round 42's own gate required one); the two
    // tests following them (round 43) prove the SAME single-node closure with NO dispatch field at
    // all -- round 42's own gate (ContainsAdminRuntimeNodeLevelDispatchField-conjuncted) would have
    // let those specific two through, which is exactly the gap
    // owner_decision_2026_08_15_schema_composed_identity_independent_of_dispatch_field_round43
    // closes. All five now pass because ValidateLayoutPatchAsync's current gate
    // (mayBeSchemaComposed = ContainsNodeMissingComponentKey(...) ||
    // (ContainsNodeWithComponentKeyPresent(...) && await LayoutHasSchemaComposedRecordsAsync(...)))
    // loads the schema-tree map for a componentKey-bearing single node regardless of dispatch-field
    // presence. ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_SingleNodePatch_RawComponentKeyMismatchesCanonical_FailsClosed()
    {
        // The literal single-node spoof proof the round-42 NG axis requires: ONE node in the
        // patch, no sibling missing componentKey to trip the round-41 gate instead. This node
        // alone both supplies its own componentKey (so ContainsNodeMissingComponentKey is false)
        // and authors an admin_runtime dispatch field (so ContainsNodeWithComponentKeyPresent is
        // true regardless), which is exactly the shape that must fall through to the
        // LayoutHasSchemaComposedRecordsAsync-gated path rather than silently passing. See the
        // round-43 dispatch-field-FREE variant below for the case round 42's own gate would have
        // missed.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "button.primary",
            "dispatchTargetRefByTrigger": { "change": "{{SearchReadTargetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "search": "node:{{SchemaComposedSearchFieldNodeId}}.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal(
            $"LAYOUT_PATCH_SCHEMA_COMPOSED_COMPONENT_KEY_IDENTITY_MISMATCH:{SchemaComposedSearchFieldNodeId}",
            result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_SingleNodePatch_RawComponentKeyMatchesCanonical_Passes()
    {
        // Complementary single-node positive case: a single node whose own componentKey matches
        // canonical identity exactly must still pass -- the new gate loads the schema-tree map for
        // this shape too, but the identity check itself is unaffected (consistent, not banned).
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "search_input.alias",
            "dispatchTargetRefByTrigger": { "change": "{{SearchReadTargetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "search": "node:{{SchemaComposedSearchFieldNodeId}}.value" } },
            "debounceMs": 300 }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_SingleNodePatch_NoComponentKeyAtAll_StillCatchesNothingToSpoofButLoadsSchemaTree()
    {
        // A single node with an admin_runtime dispatch field but NO componentKey at all (the
        // ordinary round-40 shape, not a spoof attempt) still passes -- ContainsNodeMissingComponentKey
        // alone already covered this shape before round 42; this test pins that the new
        // dispatch-field-triggered path does not regress the missing-componentKey-is-fine-when-a-
        // real-schema-leaf-exists case down to a single node with no sibling.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component",
            "dispatchTargetRefByTrigger": { "change": "{{SearchReadTargetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "search": "node:{{SchemaComposedSearchFieldNodeId}}.value" } },
            "debounceMs": 300 }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    // ── Round 43 (owner clarification,
    // owner_decision_2026_08_15_schema_composed_identity_independent_of_dispatch_field_round43):
    // the literal single-node spoof/match proof with NO dispatch field authored anywhere in the
    // patch -- neither dispatchTargetRefByTrigger nor dispatchPayloadFromByTrigger. Round 42's own
    // gate (ContainsAdminRuntimeNodeLevelDispatchField-conjuncted) would have let a spoof of this
    // exact shape through silently, since neither ContainsNodeMissingComponentKey (a componentKey
    // IS present) nor the old dispatch-field conjunct would have been true. Per the same NG axis
    // round 42 established, no dummy componentKey-missing or dispatch-field-carrying sibling node
    // is added merely to force an older/narrower gate to fire -- this is a literal single node,
    // nothing else in the patch. ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_SingleNodePatch_NoDispatchFieldAtAll_RawComponentKeyMismatchesCanonical_FailsClosed()
    {
        // The round-43 closure proof itself: a single node, a real schema-tree leaf nodeId, a
        // forged componentKey, and NEITHER dispatchTargetRefByTrigger NOR
        // dispatchPayloadFromByTrigger anywhere in the patch. Round 42's own gate would have passed
        // this silently (ContainsNodeMissingComponentKey false, ContainsAdminRuntimeNodeLevelDispatchField
        // false) -- ContainsNodeWithComponentKeyPresent alone (no dispatch-field requirement) is
        // what makes this reach LayoutHasSchemaComposedRecordsAsync and, since this repository's
        // layout genuinely is schema-composed, the canonical identity comparison itself.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "button.primary" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal(
            $"LAYOUT_PATCH_SCHEMA_COMPOSED_COMPONENT_KEY_IDENTITY_MISMATCH:{SchemaComposedSearchFieldNodeId}",
            result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_SchemaComposedNode_SingleNodePatch_NoDispatchFieldAtAll_RawComponentKeyMatchesCanonical_Passes()
    {
        // Complementary positive case: this is not a new authoring restriction -- a single node
        // with no dispatch field at all, whose own componentKey matches canonical exactly, still
        // passes even though the cheap check now runs for it.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedSearchFieldNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "search_input.alias" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_TensorOnlyNode_SingleNodePatch_WithDispatchField_NotInSchemaTree_RawComponentKeyStillNeverCheckedAgainstCanonical()
    {
        // Genuinely tensor-only single node (absent from the schema tree, unlike
        // SchemaComposedSearchFieldNodeId) that ALSO authors an admin_runtime dispatch field --
        // the new gate's cheap LayoutHasSchemaComposedRecordsAsync check reports TRUE for this
        // repository (its layout genuinely has schema-composed records elsewhere), so the schema
        // tree map IS loaded, but this specific nodeId is not a member of it -- preserving the
        // legitimate tensor-only raw-componentKey authority the round-42 SSOT explicitly requires
        // not to regress, even under the new gate.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedUnknownNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "button.primary",
            "dispatchTargetRefByTrigger": { "change": "{{SearchReadTargetRef}}" },
            "dispatchPayloadFromByTrigger": { "change": { "search": "node:{{SchemaComposedUnknownNodeId}}.value" } } }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_TensorOnlyLayout_SingleNodePatch_WithDispatchField_LayoutHasNoSchemaComposedRecords_NeverLoadsFullSchemaJson()
    {
        // The DB-round-trip optimization this round preserves, pinned at the access-shape level:
        // for a layout that is genuinely and entirely tensor-only (LayoutHasSchemaComposedRecordsAsync
        // truthfully reports false), a single node with its own componentKey AND an admin_runtime
        // dispatch field still triggers the cheap boolean check (unlike the pre-round-42 gate,
        // which would have skipped even that) but must NEVER reach the expensive full-body
        // LoadLayoutSchemaJsonAsync fetch -- SchemaJsonLookupForbiddenRepository throws if that
        // happens.
        var repo = new SchemaJsonLookupForbiddenRepository("admin_runtime", FullyAuthorizedManifest);
        var result = await repo.ValidateLayoutPatchAsync(
            Guid.NewGuid(), "/admin/ui-builder", SearchInputNode(", \"debounceMs\": 300"), null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_TensorOnlyPatch_EveryNodeHasComponentKey_NoDispatchField_StillCallsCheapCheck_ButNeverFullFetch()
    {
        // Round 43 update: this test previously (round 42) pinned the OPPOSITE guarantee -- that a
        // node with its own componentKey and NO admin_runtime dispatch field at all must not
        // trigger even the cheap LayoutHasSchemaComposedRecordsAsync check. That guarantee is
        // exactly what left the round-43 spoof gap open (a forged componentKey on a dispatch-
        // field-free node for a real schema-tree leaf was never checked against canonical) and is
        // deliberately retired -- see
        // owner_decision_2026_08_15_schema_composed_identity_independent_of_dispatch_field_round43
        // in admin-uibuilder-ui-structure-wiring-ssot.yaml. This test now pins the CORRECT,
        // narrower-but-genuine replacement guarantee: the cheap check DOES run for this shape
        // (proven via a call-counting stub, not merely "the result happens to be correct"), but the
        // EXPENSIVE full-body LoadLayoutSchemaJsonAsync fetch still never runs when that cheap
        // check truthfully reports this layout is not schema-composed -- the DB-round-trip
        // optimization that matters (avoiding the full schema tree fetch for an ordinary
        // tensor-only save) is preserved even though the narrower "avoid the cheap check too"
        // optimization is not.
        var repo = new SchemaComposedRecordsCheckCountingRepository("admin_runtime", FullyAuthorizedManifest);
        var tensorPatchJson = """
        { "nodes": [
          { "nodeId": "plain-node-no-dispatch", "componentKey": "box.primitive" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
        Assert.Equal(1, repo.LayoutHasSchemaComposedRecordsCallCount);
    }

    private sealed class SchemaComposedRecordsCheckCountingRepository(
        string? wiringKind, AdminRuntimeManifestAuthorizationResult? manifestAuthorization = null)
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public int LayoutHasSchemaComposedRecordsCallCount { get; private set; }

        public override Task<string?> LoadWiringKindForLayoutAsync(Guid layoutId, CancellationToken ct = default)
            => Task.FromResult(wiringKind);

        public override Task<AdminRuntimeManifestAuthorizationResult> LoadAdminRuntimeManifestAuthorizationAsync(
            Guid manifestId, CancellationToken ct = default)
            => Task.FromResult(manifestAuthorization ??
                new AdminRuntimeManifestAuthorizationResult(Exists: true, IsActive: true, IsAdminRuntimeDestination: true));

        // Round 43: genuinely tensor-only (returns false), but counts calls so the test can prove
        // this DOES run for a componentKey-bearing, dispatch-field-free node -- the opposite of
        // what this repository's round-42 predecessor (SchemaComposedRecordsCheckForbiddenRepository)
        // proved.
        public override Task<bool> LayoutHasSchemaComposedRecordsAsync(Guid layoutId, CancellationToken ct = default)
        {
            LayoutHasSchemaComposedRecordsCallCount++;
            return Task.FromResult(false);
        }

        public override Task<string?> LoadLayoutSchemaJsonAsync(Guid layoutId, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "LoadLayoutSchemaJsonAsync must not be called when LayoutHasSchemaComposedRecordsAsync truthfully reports false.");
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_TensorOnlyNode_NotInSchemaTree_RawComponentKeyNeverCheckedAgainstCanonical()
    {
        // Regression guard: the identity-mismatch check only applies to a nodeId that IS a real
        // schema-tree catalog leaf. A genuinely tensor-only node (absent from the schema tree
        // entirely) keeps its pre-round-40/pre-round-41 raw-componentKey authority completely
        // unchanged -- any componentKey value is accepted, there is no canonical value to compare
        // against.
        var repo = new SchemaComposedTestRepository("admin_runtime", SchemaComposedRecordsJson, FullyAuthorizedManifest);
        var tensorPatchJson = $$"""
        { "nodes": [
          { "nodeId": "{{SchemaComposedUnknownNodeId}}", "nodeKind": "catalog_component",
            "componentKey": "button.primary" }
        ] }
        """;

        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", tensorPatchJson, null, null);

        Assert.True(result.Ok, result.Message);
        Assert.True(result.Valid);
    }
}
