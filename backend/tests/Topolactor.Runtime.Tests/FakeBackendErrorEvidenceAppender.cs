using System.Collections.Concurrent;
using Topolactor.Schema;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// In-memory IBackendErrorEvidenceAppender capturing every appended evidence row for assertions.
/// Optionally throws on append to exercise the "appender failure must not mask the original" path.
/// </summary>
internal sealed class FakeBackendErrorEvidenceAppender : IBackendErrorEvidenceAppender
{
    private readonly bool _throwOnAppend;
    public ConcurrentQueue<BackendErrorEvidence> Appended { get; } = new();
    public int AppendCount => Appended.Count;

    public FakeBackendErrorEvidenceAppender(bool throwOnAppend = false) => _throwOnAppend = throwOnAppend;

    public Task AppendAsync(BackendErrorEvidence evidence, CancellationToken ct = default)
    {
        if (_throwOnAppend)
            throw new InvalidOperationException("FAKE_APPENDER_DB_UNAVAILABLE");
        Appended.Enqueue(evidence);
        return Task.CompletedTask;
    }
}
