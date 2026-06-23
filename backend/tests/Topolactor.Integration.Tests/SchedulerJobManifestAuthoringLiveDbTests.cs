using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Scheduler;
using Xunit;

namespace Topolactor.Integration.Tests;

/// <summary>
/// Live DB proof that an admin.contents-authored scheduler job manifest is executed
/// end-to-end by SchedulerJobRunner:
///   CreateJobAsync (scheduler_jobs + scheduler_job_steps, transactional)
///   -> LoadActiveJobsAsync -> lease due pending input (pending -> processing)
///   -> abstract function step chain (AbstractFunctionExecutor)
///   -> result_binding output upsert -> input row + run reach completed.
/// Uses the registered `projection` primitive; no domain-specific scheduler body.
/// TOPOLACTOR_TEST_DB_CONNECTION unset means explicit local skip.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public class SchedulerJobManifestAuthoringLiveDbTests
{
    private const string FnId = "00000000-0000-0000-0000-0000000a5ec0";
    private const string StepId = "00000000-0000-0000-0000-0000000a5ec1";
    private const string BindingId = "00000000-0000-0000-0000-0000000a5ec2";

    [Fact]
    public async Task AuthoredManifest_RunByRunner_LeasesInputUpsertsOutputAndCompletes()
    {
        var cs = GetConnectionString();
        if (cs is null) return;

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await CleanupAsync(conn);
        try
        {
            // Input + output tables (manifest-authorized targets).
            await ExecAsync(conn, "CREATE TABLE topology.sched_e2e_input (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), status text, region text)");
            await ExecAsync(conn, "CREATE TABLE topology.sched_e2e_output (region text PRIMARY KEY, observed text)");
            await ExecAsync(conn, "INSERT INTO topology.sched_e2e_input(status, region) VALUES ('pending','tokyo')");

            // Abstract function manifest (projection primitive; scheduler_job_runtime lane).
            await ExecAsync(conn,
                "INSERT INTO topology.abstract_function_manifests (abstract_function_id, function_key, runtime_lane, authority_scope, output_shape, projection_deny_keys, active) " +
                $"VALUES ('{FnId}', 'test.sched_echo', 'scheduler_job_runtime', 'sched_e2e_scope', '{{\"obs\":\"obs\"}}', ARRAY[]::text[], true)");
            await ExecAsync(conn,
                "INSERT INTO topology.abstract_function_steps (abstract_function_step_id, abstract_function_id, step_order, primitive_key, step_config, result_context_key, active) " +
                $"VALUES ('{StepId}', '{FnId}', 1, 'projection', '{{}}', 'obs', true)");
            await ExecAsync(conn,
                "INSERT INTO topology.abstract_function_input_bindings (input_binding_id, abstract_function_step_id, input_key, binding_source, binding_path, required, secret, active) " +
                $"VALUES ('{BindingId}', '{StepId}', 'region', 'scheduler_input', 'region', false, false, true)");
            await ExecAsync(conn,
                "INSERT INTO topology.abstract_function_authority_bindings (abstract_function_id, authority_kind, authority_ref, active) " +
                $"VALUES ('{FnId}', 'policy', 'sched_e2e_policy', true)");

            // Author the scheduler job manifest through the repository (transactional jobs + steps).
            var repo = new NpgsqlSchedulerJobManifestRepository(cs);
            var draft = new SchedulerJobDraft(
                JobKey: "sched_e2e_job",
                TriggerKind: "cron",
                SchedulePolicyKind: "interval_seconds",
                CronExpression: null,
                ScheduleIntervalSeconds: 300,
                ManualRunAllowed: false,
                Active: true,
                AuthorityScope: "sched_e2e_scope",
                MaxBatchSize: 10,
                LeaseSeconds: 60,
                CredentialRequirementRef: null,
                ExternalPortRef: null)
            {
                InputTableRef = "topology.sched_e2e_input",
                InputIdColumn = "id",
                InputStatusColumn = "status",
                InputStatusPendingValue = "pending",
                InputStatusProcessingValue = "processing",
                InputStatusCompletedValue = "completed",
                InputStatusFailedValue = "failed",
                OutputTableRef = "topology.sched_e2e_output",
                ProjectionPolicyJson = "{\"allowed_result_keys\":[\"obs\"]}",
                Steps = new[]
                {
                    new SchedulerJobStepDraft(
                        StepOrder: 1,
                        AbstractFunctionKey: "test.sched_echo",
                        OnError: "fail_run",
                        ResultContextKey: "obs",
                        InputBindingJson: "{}",
                        ResultBindingJson: "{\"kind\":\"output_upsert\",\"result_context_key\":\"obs\",\"conflict_columns\":[\"region\"],\"column_map\":{\"region\":\"obs.region\"}}",
                        AuthorityScope: null,
                        Active: true),
                },
            };

            var jobId = await repo.CreateJobAsync(draft);
            Assert.NotEqual(Guid.Empty, jobId);

            // Step persisted alongside the header.
            Assert.Equal(1L, await ScalarAsync(conn, $"SELECT count(*) FROM topology.scheduler_job_steps WHERE scheduler_job_id = '{jobId}'"));

            // Run it through the real runner.
            var executor = new AbstractFunctionExecutor(
                new NpgsqlAbstractFunctionManifestRepository(cs),
                new IAbstractFunctionPrimitiveAdapter[] { new ProjectionPrimitiveAdapter() });
            var runner = new SchedulerJobRunner(NullLogger<SchedulerJobRunner>.Instance, repo, executor);

            var job = (await repo.LoadActiveJobsAsync()).Single(j => j.JobKey == "sched_e2e_job");
            await runner.TryExecuteJobAsync(job, default);

            // Input row leased and completed.
            Assert.Equal("completed", (string)(await ScalarAsync(conn, "SELECT status FROM topology.sched_e2e_input WHERE region='tokyo'"))!);
            // Output upsert applied (manifest-authorized table/column from result_binding).
            Assert.Equal(1L, await ScalarAsync(conn, "SELECT count(*) FROM topology.sched_e2e_output WHERE region='tokyo'"));
            // Run ledger completed.
            Assert.Equal("completed", (string)(await ScalarAsync(conn, $"SELECT run_status FROM topology.scheduler_job_runs WHERE scheduler_job_id='{jobId}' ORDER BY started_at DESC LIMIT 1"))!);
        }
        finally
        {
            await CleanupAsync(conn);
        }
    }

    private static async Task CleanupAsync(NpgsqlConnection conn)
    {
        await ExecAsync(conn, "DELETE FROM topology.scheduler_job_runs WHERE job_key='sched_e2e_job'");
        await ExecAsync(conn, "DELETE FROM topology.scheduler_jobs WHERE job_key='sched_e2e_job'");
        await ExecAsync(conn, $"DELETE FROM topology.abstract_function_input_bindings WHERE abstract_function_step_id='{StepId}'");
        await ExecAsync(conn, $"DELETE FROM topology.abstract_function_steps WHERE abstract_function_id='{FnId}'");
        await ExecAsync(conn, $"DELETE FROM topology.abstract_function_authority_bindings WHERE abstract_function_id='{FnId}'");
        await ExecAsync(conn, $"DELETE FROM topology.abstract_function_manifests WHERE abstract_function_id='{FnId}'");
        await ExecAsync(conn, "DROP TABLE IF EXISTS topology.sched_e2e_input");
        await ExecAsync(conn, "DROP TABLE IF EXISTS topology.sched_e2e_output");
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    private static string? GetConnectionString()
    {
        var cs = Environment.GetEnvironmentVariable("TOPOLACTOR_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY") == "1")
                throw new InvalidOperationException(
                    "TOPOLACTOR_TEST_DB_CONNECTION is required when TOPOLACTOR_CI_REQUIRE_DB_CONTINUITY=1.");
            return null;
        }
        return cs;
    }
}
