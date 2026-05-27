using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class NpgsqlUiTopologyRepositoryLayoutPatchValidationTests
{
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
    public async Task ValidateLayoutPatchAsync_UnknownCssToken_FailsClose()
    {
        var repo = new NpgsqlUiTopologyRepository(NullLogger<NpgsqlUiTopologyRepository>.Instance, "Host=localhost;Database=none");
        var result = await repo.ValidateLayoutPatchAsync(Guid.NewGuid(), "/admin/ui-builder", "{}", ["unknown.token"], null);
        Assert.False(result.Ok);
        Assert.False(result.Valid);
        Assert.StartsWith("CSS_TOKEN_REF_UNKNOWN:", result.Message);
    }
}
