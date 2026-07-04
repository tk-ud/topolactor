using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class SeedRuntimeTopologySeedDiscussionTests
{

    [Fact]
    public async Task GeneratedSeedCandidate_FromStageFixtures_ValidatesPreviewsAndImportsThroughControlledPipeline()
    {
        var buildTemplate = RunTool("build-template", "--answers", FixturePath("stage2-admin-contents.json"));
        using var templateDoc = JsonDocument.Parse(buildTemplate);
        var tmpTemplate = templateDoc.RootElement.GetProperty("tmp_json_template").GetRawText();

        var tmpAnswers = Path.Combine(Path.GetTempPath(), "topology-seed-discussion-" + Guid.NewGuid().ToString("N") + ".tmp.json");
        await File.WriteAllTextAsync(tmpAnswers, tmpTemplate);
        try
        {
            var built = RunTool("build", "--answers", tmpAnswers);
            using var builtDoc = JsonDocument.Parse(built);
            var seedCandidate = builtDoc.RootElement.GetProperty("seed_candidate_payload").GetRawText();

            var apply = new RecordingSeedImportApplyBoundary(SeedImportApplyStatus.Inserted);
            using var workspace = new TempSeedWorkspace(seedCandidate, fromGeneratedJson: true);
            var runtime = workspace.CreateRuntime(apply);

            var validation = await runtime.ValidateAsync();
            var preview = await runtime.PreviewAsync();
            var import = await runtime.ImportAsync();

            Assert.True(validation.IsValid);
            Assert.True(preview.Success);
            Assert.True(import.Success);
            Assert.True(import.ValidatedRuntimeCount > 0);
            Assert.Single(apply.Calls);
            Assert.Equal("topology_seed_discussion", apply.Calls[0].Target);
        }
        finally
        {
            if (File.Exists(tmpAnswers)) File.Delete(tmpAnswers);
        }
    }

    [Fact]
    public async Task Build_InvalidGeneratedSeedCandidate_FailsCloseBeforeSeedRuntime()
    {
        var invalidTmp = Path.Combine(Path.GetTempPath(), "topology-seed-discussion-invalid-" + Guid.NewGuid().ToString("N") + ".tmp.json");
        await File.WriteAllTextAsync(invalidTmp, """
            {
              "schema_id": "topology_seed_discussion_ssot_seed_structure_v1",
              "question_space": "admin_contents_authoring",
              "enabled_keys": [],
              "seed_candidate_payload": { "runtimes": "not-an-array" }
            }
            """);
        try
        {
            var result = RunToolExpectFailure("build", "--answers", invalidTmp);
            Assert.Contains("invalid seed_candidate_payload", result.StandardError);
        }
        finally
        {
            if (File.Exists(invalidTmp)) File.Delete(invalidTmp);
        }
    }


    [Fact]
    public async Task Import_GeneratedNonEmptyRuntimeFixtureUsesApplyBoundaryWithoutLiveDb()
    {
        var apply = new RecordingSeedImportApplyBoundary(SeedImportApplyStatus.Inserted);
        using var workspace = new TempSeedWorkspace("seedruntime-generated-nonempty-import.json");
        var runtime = workspace.CreateRuntime(apply);

        var validation = await runtime.ValidateAsync();
        var preview = await runtime.PreviewAsync();
        var import = await runtime.ImportAsync();

        Assert.True(validation.IsValid);
        Assert.True(preview.Success);
        Assert.Equal(1, preview.Data!.RuntimeCount);
        Assert.True(import.Success);
        Assert.Equal(1, import.ValidatedRuntimeCount);
        Assert.Single(apply.Calls);
    }

    [Fact]
    public async Task Import_GeneratedNonEmptyRuntimeCandidateConflictFailsCloseWithoutLiveDb()
    {
        var buildTemplate = RunTool("build-template", "--answers", FixturePath("stage2-admin-contents.json"));
        using var templateDoc = JsonDocument.Parse(buildTemplate);
        var tmpTemplate = templateDoc.RootElement.GetProperty("tmp_json_template").GetRawText();
        using var workspace = new TempSeedWorkspace(ExtractSeedCandidateFromBuild(tmpTemplate), fromGeneratedJson: true);
        var runtime = workspace.CreateRuntime(new RecordingSeedImportApplyBoundary(
            SeedImportApplyStatus.Conflict,
            "conflicting active mapping from fake apply boundary"));

        var import = await runtime.ImportAsync();

        Assert.False(import.Success);
        Assert.Contains(import.Errors, e => e.Code == "SEED_IMPORT_CONFLICTING_ACTIVE_MAPPING");
    }

    [Fact]
    public async Task Validate_UnsupportedRuntimeDestinationFailsClose()
    {
        using var workspace = new TempSeedWorkspace("""
            {
              "version": 1,
              "runtimes": [
                {
                  "name": "unsupported destination",
                  "target": "topology_seed_discussion",
                  "layer": "admin_contents_authoring",
                  "action": "selected_ssot_elements",
                  "runtimeDestination": "unsupported_destination"
                }
              ]
            }
            """, fromGeneratedJson: true);
        var runtime = workspace.CreateRuntime(new RecordingSeedImportApplyBoundary(SeedImportApplyStatus.Inserted));

        var validation = await runtime.ValidateAsync();
        var import = await runtime.ImportAsync();

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Code == "SEED_RUNTIME_DESTINATION_UNKNOWN");
        Assert.False(import.Success);
    }

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
            var source = FixturePath(fixtureName);
            File.Copy(source, Path.Combine(_dir, "seed.json"));
        }

        public TempSeedWorkspace(string seedJson, bool fromGeneratedJson)
        {
            _dir = Path.Combine(Path.GetTempPath(), "topolactor-seedruntime-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "seed.json"), seedJson);
        }

        public SeedRuntime CreateRuntime(ISeedImportApplyBoundary? applyBoundary = null)
        {
            var seedRepository = new SeedJsonRepository(NullLogger<SeedJsonRepository>.Instance, _dir);
            return new SeedRuntime(
                NullLogger<SeedRuntime>.Instance,
                seedRepository,
                applyBoundary ?? new RecordingSeedImportApplyBoundary(SeedImportApplyStatus.Inserted));
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

    }

    private static string FixturePath(string fixtureName)
        => Path.Combine(FindRepoRoot(), ".agent", "tests", "fixtures", "topology-seed-discussion", fixtureName);

    private static string ExtractSeedCandidateFromBuild(string tmpTemplateJson)
    {
        var tmpAnswers = Path.Combine(Path.GetTempPath(), "topology-seed-discussion-" + Guid.NewGuid().ToString("N") + ".tmp.json");
        File.WriteAllText(tmpAnswers, tmpTemplateJson);
        try
        {
            var built = RunTool("build", "--answers", tmpAnswers);
            using var builtDoc = JsonDocument.Parse(built);
            return builtDoc.RootElement.GetProperty("seed_candidate_payload").GetRawText();
        }
        finally
        {
            if (File.Exists(tmpAnswers)) File.Delete(tmpAnswers);
        }
    }

    private sealed class RecordingSeedImportApplyBoundary : ISeedImportApplyBoundary
    {
        private readonly SeedImportApplyStatus _status;
        private readonly string? _message;

        public RecordingSeedImportApplyBoundary(SeedImportApplyStatus status, string? message = null)
        {
            _status = status;
            _message = message;
        }

        public List<ApplyCall> Calls { get; } = [];

        public Task<SeedImportApplyResult> ApplyRuntimeDeclarationAsync(
            string target,
            string layer,
            string action,
            string runtimeDestination,
            CancellationToken ct = default)
        {
            Calls.Add(new ApplyCall(target, layer, action, runtimeDestination));
            return Task.FromResult(new SeedImportApplyResult(_status, _message));
        }
    }

    private sealed record ApplyCall(string Target, string Layer, string Action, string RuntimeDestination);

    private static string RunTool(params string[] args)
    {
        var result = RunToolProcess(args);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"tool failed with {result.ExitCode}: {result.StandardError}");
        return result.StandardOutput;
    }

    private static ToolResult RunToolExpectFailure(params string[] args)
    {
        var result = RunToolProcess(args);
        if (result.ExitCode == 0)
            throw new InvalidOperationException("tool unexpectedly succeeded");
        return result;
    }

    private static ToolResult RunToolProcess(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        ApplyUtf8ToolProcessEnvironment(psi);

        if (OperatingSystem.IsWindows())
            ConfigureWindowsToolLaunch(psi, repoRoot, args);
        else
            ConfigureUnixToolLaunch(psi, repoRoot, args);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start topology-seed-discussion");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ToolResult(process.ExitCode, stdout, stderr);
    }

    private static void ConfigureUnixToolLaunch(ProcessStartInfo psi, string repoRoot, string[] args)
    {
        var toolPath = Path.Combine(repoRoot, ".agent", "tools", "topology-seed-discussion");
        psi.FileName = toolPath;
        foreach (var arg in args) psi.ArgumentList.Add(arg);
    }

    private static void ConfigureWindowsToolLaunch(ProcessStartInfo psi, string repoRoot, string[] args)
    {
        var scriptPath = Path.Combine(repoRoot, ".agent", "scripts", "agent_tools", "readonly_observation.py");
        if (TryConfigurePythonLaunch(psi, scriptPath, args))
            return;

        // Git Bash only resolves repo-local scripts via MSYS paths (E:/... is not executable).
        var toolPath = Path.Combine(repoRoot, ".agent", "tools", "topology-seed-discussion");
        psi.FileName = "bash";
        psi.ArgumentList.Add(ToMsysPath(toolPath));
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (i > 0 && args[i - 1] == "--answers" && LooksLikeFilesystemPath(arg))
                arg = ToMsysPath(arg);
            psi.ArgumentList.Add(arg);
        }
    }

    private static bool TryConfigurePythonLaunch(ProcessStartInfo psi, string scriptPath, string[] args)
    {
        (string executable, string[] prefix)[] candidates =
        [
            ("py", ["-3"]),
            ("python3", []),
            ("python", []),
        ];

        foreach (var (executable, prefix) in candidates)
        {
            if (!CommandRuns(executable, [.. prefix, "--version"]))
                continue;

            psi.FileName = executable;
            foreach (var token in prefix) psi.ArgumentList.Add(token);
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("topology-seed-discussion");
            foreach (var arg in args) psi.ArgumentList.Add(arg);
            return true;
        }

        return false;
    }

    private static bool CommandRuns(string executable, string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        try
        {
            using var process = Process.Start(psi);
            if (process is null) return false;
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string ToMsysPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.Length >= 2 && full[1] == ':' && char.IsLetter(full[0]))
        {
            var drive = char.ToLowerInvariant(full[0]);
            var rest = full[2..].Replace('\\', '/');
            return $"/{drive}{rest}";
        }

        return full.Replace('\\', '/');
    }

    private static bool LooksLikeFilesystemPath(string value)
        => value.Contains('\\') || value.Contains('/') || (value.Length > 1 && value[1] == ':');

    private static void ApplyUtf8ToolProcessEnvironment(ProcessStartInfo psi)
    {
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
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

    private sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError);

}
