using System.Linq;
using System.Text.Json;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// SSOT: docs/design/admin-uibuilder-ui-structure-wiring-ssot.yaml
/// lifecycle_policy.projection_authority_runtime_interaction_identity.
///
/// Proves NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds — the pure JSON
/// transform applied at the layout_patch persistence boundary — assigns a stable
/// runtimeInteractionId to id-less runtimeInteractions[] entries, leaves already-id'd
/// entries untouched, and is a no-op (byte-identical) when nothing needs assignment.
/// </summary>
public class NpgsqlUiTopologyRepositoryRuntimeInteractionIdentityTests
{
    private static JsonElement Node(string patchJson, int nodeIndex) =>
        JsonDocument.Parse(patchJson).RootElement.GetProperty("nodes")[nodeIndex];

    private static JsonElement Interaction(string patchJson, int nodeIndex, int interactionIndex) =>
        Node(patchJson, nodeIndex).GetProperty("runtimeInteractions")[interactionIndex];

    [Fact]
    public void AssignRuntimeInteractionIds_AssignsStableIdToEntryLackingOne()
    {
        var patch = """
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x"}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        var interaction = Interaction(result, 0, 0);
        Assert.True(interaction.TryGetProperty("runtimeInteractionId", out var idEl));
        Assert.Equal(JsonValueKind.String, idEl.ValueKind);
        Assert.True(Guid.TryParse(idEl.GetString(), out _));
        // Existing fields survive the rewrite untouched.
        Assert.Equal("click", interaction.GetProperty("trigger").GetString());
        Assert.Equal("port:x", interaction.GetProperty("portTargetRef").GetString());
    }

    [Fact]
    public void AssignRuntimeInteractionIds_LeavesExistingIdUntouched()
    {
        const string existingId = "aaaaaaaa-0000-0000-0000-000000000001";
        var patch = $$"""
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":"{{existingId}}"}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        var interaction = Interaction(result, 0, 0);
        Assert.Equal(existingId, interaction.GetProperty("runtimeInteractionId").GetString());
    }

    [Fact]
    public void AssignRuntimeInteractionIds_NoAssignmentNeeded_ReturnsInputUnchanged()
    {
        const string existingId = "aaaaaaaa-0000-0000-0000-000000000001";
        var patch = $$"""
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":"{{existingId}}"}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        // Same reference — no reserialization performed when every entry already has an id.
        Assert.Same(patch, result);
    }

    [Fact]
    public void AssignRuntimeInteractionIds_TwoDistinctIdLessEntries_GetDistinctIds()
    {
        var patch = """
        {"nodes":[
          {"nodeId":"n1","runtimeInteractions":[{"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x"}]},
          {"nodeId":"n2","runtimeInteractions":[{"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:y"}]}
        ]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        var id1 = Interaction(result, 0, 0).GetProperty("runtimeInteractionId").GetString();
        var id2 = Interaction(result, 1, 0).GetProperty("runtimeInteractionId").GetString();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void AssignRuntimeInteractionIds_MixedNodeWithAndWithoutId_OnlyAssignsMissingOne()
    {
        const string existingId = "aaaaaaaa-0000-0000-0000-000000000001";
        var patch = $$"""
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":"{{existingId}}"},
          {"trigger":"change","actionType":"dispatchInstanceOperation","instanceTargetRef":"instance:y"}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        Assert.Equal(existingId, Interaction(result, 0, 0).GetProperty("runtimeInteractionId").GetString());
        var secondId = Interaction(result, 0, 1).GetProperty("runtimeInteractionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secondId));
        Assert.NotEqual(existingId, secondId);
    }

    [Fact]
    public void AssignRuntimeInteractionIds_NodeWithoutRuntimeInteractions_IsUnaffected()
    {
        var patch = """{"nodes":[{"nodeId":"n1","componentKey":"button.primitive"}]}""";

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        Assert.Same(patch, result);
    }

    [Fact]
    public void AssignRuntimeInteractionIds_EmptyOrMissingNodesArray_IsUnaffected()
    {
        Assert.Same("{}", NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds("{}"));
        Assert.Same("", NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(""));
    }

    [Fact]
    public void AssignRuntimeInteractionIds_OtherNodeAndInteractionFieldsSurviveRewrite()
    {
        var patch = """
        {"nodes":[{"nodeId":"n1","componentKey":"button.primitive","slotKey":"root","orderIndex":0,
          "runtimeInteractions":[
            {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x",
             "debounceMs":300,"lifecycleDispatchConfirmed":true,"idempotencyPolicy":"once_per_mount"}
          ]}],"rootLayoutClassRefs":["layout.card"]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);
        using var doc = JsonDocument.Parse(result);

        Assert.Equal("layout.card", doc.RootElement.GetProperty("rootLayoutClassRefs")[0].GetString());
        var node = Node(result, 0);
        Assert.Equal("button.primitive", node.GetProperty("componentKey").GetString());
        Assert.Equal("root", node.GetProperty("slotKey").GetString());
        var interaction = Interaction(result, 0, 0);
        Assert.Equal(300, interaction.GetProperty("debounceMs").GetInt32());
        Assert.True(interaction.GetProperty("lifecycleDispatchConfirmed").GetBoolean());
        Assert.Equal("once_per_mount", interaction.GetProperty("idempotencyPolicy").GetString());
        Assert.True(interaction.TryGetProperty("runtimeInteractionId", out _));
    }

    [Fact]
    public void AssignRuntimeInteractionIds_BlankOrWhitespaceIdIsTreatedAsMissing()
    {
        var patch = """
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":"   "}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        var interaction = Interaction(result, 0, 0);
        var assignedId = interaction.GetProperty("runtimeInteractionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(assignedId));
        Assert.NotEqual("   ", assignedId);
        AssertExactlyOneRuntimeInteractionIdKey(interaction);
    }

    // ── field_shape enforcement: runtimeInteractionId must be a valid UUID string ──
    // PR577 follow-up fix. SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml
    // lifecycle_policy.projection_authority_runtime_interaction_identity field_shape.

    private static void AssertExactlyOneRuntimeInteractionIdKey(JsonElement interaction)
    {
        var count = interaction.EnumerateObject()
            .Count(p => p.NameEquals("runtimeInteractionId"));
        Assert.Equal(1, count);
    }

    [Fact]
    public void AssignRuntimeInteractionIds_NonUuidFormatExistingId_IsReplacedWithValidUuid_NotPreserved()
    {
        var patch = """
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":"not-a-real-uuid"}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        var interaction = Interaction(result, 0, 0);
        var assignedId = interaction.GetProperty("runtimeInteractionId").GetString();
        Assert.NotEqual("not-a-real-uuid", assignedId);
        Assert.True(Guid.TryParse(assignedId, out _), "replacement id must be a valid UUID string (SSOT field_shape)");
        AssertExactlyOneRuntimeInteractionIdKey(interaction);
    }

    [Fact]
    public void AssignRuntimeInteractionIds_NonStringExistingId_IsReplacedWithValidUuid_NoDuplicateKey()
    {
        // A JSON number in the runtimeInteractionId slot (e.g. from a malformed
        // hand-edited patch) must not be treated as a valid existing id, and must
        // not survive alongside the freshly assigned one as a duplicate key.
        var patch = """
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":12345}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        var interaction = Interaction(result, 0, 0);
        AssertExactlyOneRuntimeInteractionIdKey(interaction);
        var assignedId = interaction.GetProperty("runtimeInteractionId");
        Assert.Equal(JsonValueKind.String, assignedId.ValueKind);
        Assert.True(Guid.TryParse(assignedId.GetString(), out _));
    }

    [Fact]
    public void AssignRuntimeInteractionIds_BlankExistingId_ProducesNoDuplicateKey()
    {
        var patch = """
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":""}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        AssertExactlyOneRuntimeInteractionIdKey(Interaction(result, 0, 0));
    }

    [Fact]
    public void AssignRuntimeInteractionIds_ValidUuidExistingId_ProducesNoDuplicateKeyAndIsPreserved()
    {
        const string existingId = "aaaaaaaa-0000-0000-0000-000000000001";
        var patch = $$"""
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":"{{existingId}}"}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        var interaction = Interaction(result, 0, 0);
        AssertExactlyOneRuntimeInteractionIdKey(interaction);
        Assert.Equal(existingId, interaction.GetProperty("runtimeInteractionId").GetString());
    }

    [Fact]
    public void AssignRuntimeInteractionIds_AllEntriesHaveNonUuidGarbageIds_PreScanStillTriggersReassignment()
    {
        // Regression guard: the cheap pre-scan that decides whether reserialization
        // is needed at all must use the SAME UUID-format check as the write path.
        // If every entry has a non-blank but non-UUID string, the pre-scan must
        // still detect that reassignment is needed — it must not short-circuit to
        // "nothing to do" just because every value is a non-empty string.
        var patch = """
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":"garbage-value"}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        Assert.NotSame(patch, result);
        var assignedId = Interaction(result, 0, 0).GetProperty("runtimeInteractionId").GetString();
        Assert.True(Guid.TryParse(assignedId, out _));
    }

    [Fact]
    public void AssignRuntimeInteractionIds_MixedValidAndInvalidAcrossEntries_OnlyInvalidOnesAreReplaced()
    {
        const string validId = "aaaaaaaa-0000-0000-0000-000000000001";
        var patch = $$"""
        {"nodes":[{"nodeId":"n1","runtimeInteractions":[
          {"trigger":"click","actionType":"dispatchExternalPort","portTargetRef":"port:x","runtimeInteractionId":"{{validId}}"},
          {"trigger":"change","actionType":"dispatchInstanceOperation","instanceTargetRef":"instance:y","runtimeInteractionId":"not-valid"}
        ]}]}
        """;

        var result = NpgsqlUiTopologyRepository.AssignRuntimeInteractionIds(patch);

        var first = Interaction(result, 0, 0);
        var second = Interaction(result, 0, 1);
        AssertExactlyOneRuntimeInteractionIdKey(first);
        AssertExactlyOneRuntimeInteractionIdKey(second);
        Assert.Equal(validId, first.GetProperty("runtimeInteractionId").GetString());
        var replacedId = second.GetProperty("runtimeInteractionId").GetString();
        Assert.NotEqual("not-valid", replacedId);
        Assert.True(Guid.TryParse(replacedId, out _));
        Assert.NotEqual(validId, replacedId);
    }
}
