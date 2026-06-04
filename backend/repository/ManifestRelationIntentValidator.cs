using System.Text.Json;
using Topolactor.Schema;

namespace Topolactor.Repository;

/// <summary>
/// Validates Step 2.5 relation intents: local side draft-scoped; remote may be draft or active manifest.
/// </summary>
public static class ManifestRelationIntentValidator
{
    public record LogicalTableModel(string TableName, IReadOnlyList<string> ColumnNames);

    public static IReadOnlyList<LogicalTableModel> ExtractLogicalTables(IReadOnlyList<JsonElement> topology)
    {
        foreach (var entry in topology)
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var typeEl) ||
                typeEl.ValueKind != JsonValueKind.String ||
                !string.Equals(typeEl.GetString(), ManifestCanonicalProjection.ScreenDataShapeEntryType,
                    StringComparison.Ordinal))
                continue;

            return ParseLogicalTablesFromShapeEntry(entry);
        }

        return Array.Empty<LogicalTableModel>();
    }

    public static IReadOnlyList<LogicalTableModel> FromRequestLogicalTables(
        IReadOnlyList<AdminManifestLogicalTableDto>? logicalTables)
    {
        if (logicalTables is null || logicalTables.Count == 0)
            return Array.Empty<LogicalTableModel>();

        return logicalTables
            .Where(t => !string.IsNullOrWhiteSpace(t.TableName))
            .Select(t => new LogicalTableModel(
                t.TableName.Trim(),
                t.Columns
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name.Trim())
                    .ToList()))
            .Where(t => t.ColumnNames.Count > 0)
            .ToList();
    }

    public static async Task<IReadOnlyList<ValidationError>> ValidateRelationIntentsAsync(
        IReadOnlyList<AdminManifestRelationIntentDto> relationIntents,
        IReadOnlyList<LogicalTableModel> draftLogicalTables,
        Func<Guid, CancellationToken, Task<ManifestDetailRecord?>> loadManifestById,
        CancellationToken ct)
    {
        if (relationIntents.Count == 0) return [];

        var errors = new List<ValidationError>();
        var draftByName = draftLogicalTables.ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < relationIntents.Count; i++)
        {
            var rel = relationIntents[i];
            var prefix = $"relationIntents[{i}]";

            var localTable = string.IsNullOrWhiteSpace(rel.LocalTableRef)
                ? draftLogicalTables.FirstOrDefault()?.TableName ?? ""
                : rel.LocalTableRef.Trim();

            if (string.IsNullOrWhiteSpace(localTable))
            {
                errors.Add(new ValidationError("RELATION_LOCAL_TABLE_REQUIRED",
                    $"{prefix}: local table ref is required (draft manifest logical tables only)."));
                continue;
            }

            if (!draftByName.TryGetValue(localTable, out var localModel))
            {
                errors.Add(new ValidationError("RELATION_LOCAL_TABLE_UNRESOLVED",
                    $"{prefix}: local table '{localTable}' is not defined on the editing draft manifest."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(rel.LocalKey) ||
                !localModel.ColumnNames.Contains(rel.LocalKey.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationError("RELATION_LOCAL_KEY_UNRESOLVED",
                    $"{prefix}: local key '{rel.LocalKey}' is not a column on draft table '{localTable}'."));
            }

            var remoteTable = rel.JoinTableRef?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(remoteTable))
            {
                errors.Add(new ValidationError("RELATION_REMOTE_TABLE_REQUIRED",
                    $"{prefix}: remote join table ref is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(rel.RemoteKey))
            {
                errors.Add(new ValidationError("RELATION_REMOTE_KEY_REQUIRED",
                    $"{prefix}: remote key column is required."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(rel.RemoteManifestId))
            {
                if (!Guid.TryParse(rel.RemoteManifestId, out var remoteManifestId))
                {
                    errors.Add(new ValidationError("RELATION_REMOTE_MANIFEST_MALFORMED",
                        $"{prefix}: remoteManifestId must be a valid UUID."));
                    continue;
                }

                var remoteDetail = await loadManifestById(remoteManifestId, ct);
                if (remoteDetail is null)
                {
                    errors.Add(new ValidationError("RELATION_REMOTE_MANIFEST_NOT_FOUND",
                        $"{prefix}: remote manifest {remoteManifestId} was not found."));
                    continue;
                }

                if (!remoteDetail.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationError("RELATION_REMOTE_MANIFEST_NOT_ACTIVE",
                        $"{prefix}: remote manifest {remoteManifestId} must be active (status={remoteDetail.Status})."));
                    continue;
                }

                var remoteTables = ExtractLogicalTables(remoteDetail.Topology);
                var remoteModel = remoteTables.FirstOrDefault(t =>
                    string.Equals(t.TableName, remoteTable, StringComparison.OrdinalIgnoreCase));
                if (remoteModel is null)
                {
                    errors.Add(new ValidationError("RELATION_REMOTE_TABLE_UNRESOLVED",
                        $"{prefix}: table '{remoteTable}' is not defined on active manifest {remoteManifestId}."));
                    continue;
                }

                if (!remoteModel.ColumnNames.Contains(rel.RemoteKey.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationError("RELATION_REMOTE_KEY_UNRESOLVED",
                        $"{prefix}: remote key '{rel.RemoteKey}' is not a column on '{remoteTable}' (manifest {remoteManifestId})."));
                }
            }
            else
            {
                if (!draftByName.TryGetValue(remoteTable, out var remoteDraftModel))
                {
                    errors.Add(new ValidationError("RELATION_REMOTE_TABLE_UNRESOLVED",
                        $"{prefix}: remote table '{remoteTable}' is not on the editing draft. " +
                        "Select an active manifest remote target or add the table in Step 2."));
                    continue;
                }

                if (string.Equals(localTable, remoteTable, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationError("RELATION_REMOTE_TABLE_SAME_AS_LOCAL",
                        $"{prefix}: remote table must differ from local table for draft-scoped joins."));
                    continue;
                }

                if (!remoteDraftModel.ColumnNames.Contains(rel.RemoteKey.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(new ValidationError("RELATION_REMOTE_KEY_UNRESOLVED",
                        $"{prefix}: remote key '{rel.RemoteKey}' is not a column on draft table '{remoteTable}'."));
                }
            }
        }

        return errors;
    }

    private static List<LogicalTableModel> ParseLogicalTablesFromShapeEntry(JsonElement entry)
    {
        var result = new List<LogicalTableModel>();
        if (!entry.TryGetProperty("logicalTables", out var tablesEl) ||
            tablesEl.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var tableEl in tablesEl.EnumerateArray())
        {
            if (tableEl.ValueKind != JsonValueKind.Object) continue;
            var name = tableEl.TryGetProperty("tableName", out var nameEl) &&
                       nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()?.Trim() ?? ""
                : "";
            if (string.IsNullOrWhiteSpace(name)) continue;

            var cols = new List<string>();
            if (tableEl.TryGetProperty("columns", out var colsEl) &&
                colsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var colEl in colsEl.EnumerateArray())
                {
                    if (colEl.ValueKind != JsonValueKind.Object) continue;
                    if (colEl.TryGetProperty("name", out var colName) &&
                        colName.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(colName.GetString()))
                        cols.Add(colName.GetString()!.Trim());
                }
            }

            if (cols.Count > 0)
                result.Add(new LogicalTableModel(name, cols));
        }

        return result;
    }
}
