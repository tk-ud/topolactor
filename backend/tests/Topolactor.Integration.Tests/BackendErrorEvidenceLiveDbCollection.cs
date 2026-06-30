using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Serializes the backend error evidence live-DB tests. The post-notify bridge claims the global
/// logs.error_notify_queue, so these tests must not run in parallel against the same database.
/// </summary>
[CollectionDefinition("BackendErrorEvidenceLiveDb", DisableParallelization = true)]
public sealed class BackendErrorEvidenceLiveDbCollection { }
