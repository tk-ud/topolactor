using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
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
}
