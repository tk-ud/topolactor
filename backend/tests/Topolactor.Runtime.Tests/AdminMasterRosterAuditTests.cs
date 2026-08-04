using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Topolactor.Repository;
using Topolactor.Runtime;
using Topolactor.Schema;
using Xunit;

namespace Topolactor.Runtime.Tests;

/// <summary>
/// Proves AdminMasterRosterAudit.AppendAsync builds the generic JSONB audit envelope
/// (logs.diff.changed_fields_json: {"schemaVersion":1,"changedFields":[{"name","type","before","after"},...]})
/// documented in docs/design/admin-master-roster-management-ssot.yaml logs_diff_admin_projection --
/// resolving the previously dead-code changed_fields gap (envelope built but never passed to
/// LogsDiffAppendRequest). Uses a recording in-memory repository, not a real Postgres round trip --
/// the live-DB persistence proof lives in AdminEnumHubRelationUiProjectionLiveDbTests.cs.
/// </summary>
public class AdminMasterRosterAuditTests
{
    private sealed class RecordingSqlAttentionLogsRepository()
        : SqlAttentionLogsRepository(NullLogger<SqlAttentionLogsRepository>.Instance, "test-double")
    {
        public LogsDiffAppendRequest? LastRequest { get; private set; }

        public override Task AppendLogsDiffAsync(LogsDiffAppendRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.CompletedTask;
        }
    }

    private static JsonElement ParseEnvelope(LogsDiffAppendRequest? request)
    {
        Assert.NotNull(request);
        Assert.NotNull(request!.ChangedFieldsJson);
        return JsonSerializer.Deserialize<JsonElement>(request.ChangedFieldsJson!);
    }

    [Fact]
    public async Task AppendAsync_CreateWithNullBefore_EnvelopeCarriesNullBeforeAndTypedAfter()
    {
        var repo = new RecordingSqlAttentionLogsRepository();

        await AdminMasterRosterAudit.AppendAsync(
            repo, "tester", "enum.groups", "g1", "create", null, new { groupName = "demo_status" },
            [new AuditChangedField("group_name", null, "demo_status")]);

        var envelope = ParseEnvelope(repo.LastRequest);
        Assert.Equal(1, envelope.GetProperty("schemaVersion").GetInt32());
        var field = envelope.GetProperty("changedFields")[0];
        Assert.Equal("group_name", field.GetProperty("name").GetString());
        Assert.Equal("string", field.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, field.GetProperty("before").ValueKind);
        Assert.Equal("demo_status", field.GetProperty("after").GetString());
    }

    [Fact]
    public async Task AppendAsync_UpdateWithMultipleFields_EnvelopeCarriesEachFieldsOwnBeforeAndAfter()
    {
        var repo = new RecordingSqlAttentionLogsRepository();

        await AdminMasterRosterAudit.AppendAsync(
            repo, "tester", "enum.groups", "g1", "update",
            new { groupName = "old_name", indexNum = 1 },
            new { groupName = "new_name", indexNum = 2 },
            [
                new AuditChangedField("group_name", "old_name", "new_name"),
                new AuditChangedField("index_num", 1, 2),
            ]);

        var envelope = ParseEnvelope(repo.LastRequest);
        var fields = envelope.GetProperty("changedFields");
        Assert.Equal(2, fields.GetArrayLength());

        var nameField = fields[0];
        Assert.Equal("group_name", nameField.GetProperty("name").GetString());
        Assert.Equal("string", nameField.GetProperty("type").GetString());
        Assert.Equal("old_name", nameField.GetProperty("before").GetString());
        Assert.Equal("new_name", nameField.GetProperty("after").GetString());

        var indexField = fields[1];
        Assert.Equal("index_num", indexField.GetProperty("name").GetString());
        Assert.Equal("number", indexField.GetProperty("type").GetString());
        Assert.Equal(1, indexField.GetProperty("before").GetInt32());
        Assert.Equal(2, indexField.GetProperty("after").GetInt32());
    }

    [Fact]
    public async Task AppendAsync_DeleteWithNullAfter_EnvelopeInfersTypeFromBeforeValue()
    {
        var repo = new RecordingSqlAttentionLogsRepository();

        await AdminMasterRosterAudit.AppendAsync(
            repo, "tester", "enum.items", "10", "delete", new { indexNum = 10 }, null,
            [new AuditChangedField("index_num", 10, null)]);

        var envelope = ParseEnvelope(repo.LastRequest);
        var field = envelope.GetProperty("changedFields")[0];
        Assert.Equal("number", field.GetProperty("type").GetString());
        Assert.Equal(10, field.GetProperty("before").GetInt32());
        Assert.Equal(JsonValueKind.Null, field.GetProperty("after").ValueKind);
    }

    [Fact]
    public async Task AppendAsync_NoChangedFields_EnvelopeStillHasSchemaVersionAndEmptyArray()
    {
        var repo = new RecordingSqlAttentionLogsRepository();

        await AdminMasterRosterAudit.AppendAsync(
            repo, "tester", "enum.groups", "g1", "create", null, new { }, null);

        var envelope = ParseEnvelope(repo.LastRequest);
        Assert.Equal(1, envelope.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(0, envelope.GetProperty("changedFields").GetArrayLength());
    }

    [Fact]
    public async Task AppendAsync_NullLogsRepository_IsNoOp()
    {
        // fail-close precedent unchanged: logsRepository is null -> AppendAsync returns without
        // throwing (mirrors the pre-existing behavior this test guards against regressing).
        await AdminMasterRosterAudit.AppendAsync(
            null, "tester", "enum.groups", "g1", "create", null, new { groupName = "x" },
            [new AuditChangedField("group_name", null, "x")]);
    }
}
