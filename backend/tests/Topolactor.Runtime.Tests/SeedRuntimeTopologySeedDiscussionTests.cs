using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class SeedRuntimeTopologySeedDiscussionTests
{
    [Fact]
    public async Task ValidateAndPreview_AcceptImportableEmptyRuntimeFixture()
    {
        using var workspace = new TempSeedWorkspace("seedruntime-valid-empty-import.json");
        var runtime = workspace.CreateRuntime();

        var validation = await runtime.ValidateAsync();
        var preview = await runtime.PreviewAsync();

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
        Assert.True(preview.Success);
        Assert.NotNull(preview.Data);
        Assert.Equal(0, preview.Data!.RuntimeCount);
    }

    [Fact]
    public async Task Import_EmptyRuntimeFixtureUsesControlledPipelineWithoutDbMutation()
    {
        using var workspace = new TempSeedWorkspace("seedruntime-valid-empty-import.json");
        var runtime = workspace.CreateRuntime();

        var result = await runtime.ImportAsync();

        Assert.True(result.Success);
        Assert.Equal(0, result.ValidatedRuntimeCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_RecursiveImportFixtureFailsClose()
    {
        using var workspace = new TempSeedWorkspace("seedruntime-invalid-recursive.json");
        var runtime = workspace.CreateRuntime();

        var validation = await runtime.ValidateAsync();
        var preview = await runtime.PreviewAsync();
        var import = await runtime.ImportAsync();

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Code == "SEED_IMPORT_RECURSIVE_ROUTE_FORBIDDEN");
        Assert.False(preview.Success);
        Assert.False(import.Success);
        Assert.Equal(0, import.ValidatedRuntimeCount);
    }

    private sealed class TempSeedWorkspace : IDisposable
    {
        private readonly string _dir;

        public TempSeedWorkspace(string fixtureName)
        {
            _dir = Path.Combine(Path.GetTempPath(), "topolactor-seedruntime-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            var repoRoot = FindRepoRoot();
            var source = Path.Combine(repoRoot, ".agent", "tests", "fixtures", "topology-seed-discussion", fixtureName);
            File.Copy(source, Path.Combine(_dir, "seed.json"));
        }

        public SeedRuntime CreateRuntime()
        {
            var seedRepository = new SeedJsonRepository(NullLogger<SeedJsonRepository>.Instance, _dir);
            var applyRepository = new SeedImportApplyRepository("Host=127.0.0.1;Database=topolactor_seedruntime_test;Username=unused;Password=unused");
            return new SeedRuntime(NullLogger<SeedRuntime>.Instance, seedRepository, applyRepository);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(dir.FullName, ".agent")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Repository root not found from test base directory.");
        }
    }
}
