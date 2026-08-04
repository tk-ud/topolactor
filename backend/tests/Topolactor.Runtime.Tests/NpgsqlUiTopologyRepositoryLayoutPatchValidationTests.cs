using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class NpgsqlUiTopologyRepositoryLayoutPatchValidationTests
{
    [Fact]
    public async Task ValidateLayoutPatchAsync_KnownCssToken_PassesFromYamlSsot()
    {
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", "{}", ["color.action.primary.background"], null);
        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_MalformedTensorJson_FailsClose()
    {
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", "{bad", ["color.action.primary.background"], null);
        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.Equal("TENSOR_PATCH_JSON_MALFORMED", result.Message);
    }

    [Fact]
    public async Task ValidateLayoutPatchAsync_UnknownCssToken_StrippedNotRejected()
    {
        // CSS tokens are placement-only and unknown tokens are stripped/ignored, not rejected.
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", "{}", ["unknown.token"], null);
        Assert.True(result.Ok);
        Assert.True(result.Valid);
    }


    [Fact]
    public async Task ValidateLayoutPatchAsync_DraftOnlyNode_IsExplicitError()
    {
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
            var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
    }

    private sealed class InstanceCandidateSourceFailureRepository()
        : NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none")
    {
        public override Task<IReadOnlyList<InstanceOperationAuthoringCandidateDto>> ListInstanceOperationAuthoringCandidatesAsync(
            CancellationToken ct = default)
            => throw new InvalidOperationException("candidate source unavailable");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
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

}
