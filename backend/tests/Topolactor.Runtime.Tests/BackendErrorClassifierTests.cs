using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Classifier proves the SSOT boundary: only backend execution substrate system errors append to
/// logs.error. DTO failure / ordinary auth reject / business policy reject / user input miss never do.
/// </summary>
public class BackendErrorClassifierTests
{
    [Theory]
    [InlineData(BackendRejectCategory.DtoValidation)]
    [InlineData(BackendRejectCategory.OrdinaryAuth)]
    [InlineData(BackendRejectCategory.BusinessPolicy)]
    [InlineData(BackendRejectCategory.UserInputMiss)]
    [InlineData(BackendRejectCategory.SecurityOrAuditEvidence)]
    public void RejectCategories_NeverAppendToLogsError(BackendRejectCategory category)
    {
        var classification = BackendErrorClassifier.Classify(category);
        Assert.False(BackendErrorClassifier.AppendsToLogsError(classification),
            $"Reject category {category} must never append to logs.error.");
    }

    [Theory]
    // user input miss — NOT a substrate failure, must not append.
    [InlineData(AbstractFunctionFailCloseStatus.MissingInput, false)]
    // substrate / authority / binding / registry / projection failures — append.
    [InlineData(AbstractFunctionFailCloseStatus.MissingAuthority, true)]
    [InlineData(AbstractFunctionFailCloseStatus.InvalidAuthority, true)]
    [InlineData(AbstractFunctionFailCloseStatus.InvalidInputBinding, true)]
    [InlineData(AbstractFunctionFailCloseStatus.InvalidProjection, true)]
    [InlineData(AbstractFunctionFailCloseStatus.SecretProjectionDenied, true)]
    [InlineData(AbstractFunctionFailCloseStatus.UnsupportedPrimitive, true)]
    public void FailCloseStatus_ClassifiesSubstrateVsInputMiss(string status, bool appends)
    {
        var ex = new AbstractFunctionFailCloseException(status, "msg");
        var classification = BackendErrorClassifier.Classify(ex);
        Assert.Equal(appends, BackendErrorClassifier.AppendsToLogsError(classification));
    }

    [Fact]
    public void UnexpectedException_IsSystemError()
    {
        var ex = new InvalidOperationException("DB_UNAVAILABLE");
        Assert.Equal(BackendErrorClass.SystemError, BackendErrorClassifier.Classify(ex));
        Assert.True(BackendErrorClassifier.AppendsToLogsError(BackendErrorClassifier.Classify(ex)));
    }

    [Fact]
    public void Cancellation_IsNotSystemError()
    {
        var ex = new OperationCanceledException();
        Assert.Equal(BackendErrorClass.Cancellation, BackendErrorClassifier.Classify(ex));
        Assert.False(BackendErrorClassifier.AppendsToLogsError(BackendErrorClassifier.Classify(ex)));
    }
}
