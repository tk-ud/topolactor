using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Serializes test classes that modify process-level environment variables (DEMO_JWT_SECRET, etc.)
/// to prevent race conditions when xUnit runs test classes in parallel.
/// </summary>
[CollectionDefinition("env-sequential", DisableParallelization = true)]
public sealed class EnvDependentTestCollection { }
