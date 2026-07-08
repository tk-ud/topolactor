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

        var assignedId = Interaction(result, 0, 0).GetProperty("runtimeInteractionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(assignedId));
        Assert.NotEqual("   ", assignedId);
    }
}
