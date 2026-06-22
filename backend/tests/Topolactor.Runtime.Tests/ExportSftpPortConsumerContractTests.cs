using System.Runtime.CompilerServices;
using Xunit;

namespace Topolactor.Runtime.Tests;

public class ExportSftpPortConsumerContractTests
{
    [Fact]
    public void TopologySchema_SftpTransferLog_DependsOnFileStorageAndRejectsUnsafeSecretFields()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "db", "topology_tables.sql"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS topology.sftp_transfer_log", sql);
        Assert.Contains("export_job_id        UUID REFERENCES topology.export_jobs", sql);
        Assert.Contains("file_artifact_id     UUID REFERENCES topology.file_artifacts", sql);
        Assert.Contains("manifest_id          UUID REFERENCES topology.export_manifests", sql);
        Assert.Contains("checksum_record_id   UUID REFERENCES topology.file_checksum_records", sql);
        Assert.Contains("checksum_mismatch", sql);
        Assert.Contains("retry_attempted", sql);
        Assert.Contains("failure_reason", sql);
        Assert.Contains("retry_evidence_json", sql);
        Assert.DoesNotContain("sftp_host", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sftp_private_key", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("remote_path        ", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SeedEmpty_ExportSftpPolicy_RecordsFullLifecycleRuntimeEventsWithoutNewDbFunctionMutation()
    {
        var sql = File.ReadAllText(Path.Combine(RepoRoot(), "db", "seed_empty.sql"));
        Assert.Contains("'export_sftp_response_port_generic'", sql);
        Assert.Contains("transfer_initiated", sql);
        Assert.Contains("transfer_completed", sql);
        Assert.Contains("transfer_failed", sql);
        Assert.Contains("checksum_mismatch", sql);
        Assert.Contains("retry_attempted", sql);
        Assert.Contains("\"requires_export_job\":\"true\"", sql);
        Assert.Contains("\"requires_manifest\":\"true\"", sql);
        Assert.Contains("\"requires_checksum\":\"true\"", sql);
        Assert.DoesNotContain("execute_db_function',     '{\"function\":\"export_sftp", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImplementationSsot_DoesNotSpecifyProviderSecretsOrSpecificClient()
    {
        var ssot = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "design", "runtime-bundle-export-sftp-implementation-ssot.yaml"));
        Assert.Contains("provider_specific_sftp_client_or_runtime", ssot);
        Assert.Contains("external-port:response_port:00000000-0000-0000-0000-000000000f08", ssot);
        Assert.Contains("topology.file_checksum_records", ssot);
        Assert.Contains("raw_provider_response", ssot);
        Assert.DoesNotContain("sftp://", ssot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN OPENSSH", ssot, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepoRoot([CallerFilePath] string sourceFile = "")
    {
        var fromSource = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", ".."));
        if (File.Exists(Path.Combine(fromSource, "db", "seed_empty.sql"))) return fromSource;
        const string workspaceRoot = "/workspace/topolactor";
        if (File.Exists(Path.Combine(workspaceRoot, "db", "seed_empty.sql"))) return workspaceRoot;
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "db", "seed_empty.sql"))) return cwd;
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "db", "seed_empty.sql")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? throw new InvalidOperationException("repo root not found");
    }
}
