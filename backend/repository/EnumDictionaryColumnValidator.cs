using Topolactor.Schema;

namespace Topolactor.Repository;

public static class EnumDictionaryColumnValidator
{
    public static IEnumerable<AdminManifestScreenColumnDto> EnumerateColumns(
        IReadOnlyList<AdminManifestLogicalTableDto>? logicalTables,
        IReadOnlyList<AdminManifestScreenColumnDto>? columns)
    {
        if (logicalTables is { Count: > 0 })
        {
            foreach (var table in logicalTables)
            {
                foreach (var col in table.Columns)
                    yield return col;
            }
        }

        if (columns is { Count: > 0 })
        {
            foreach (var col in columns)
                yield return col;
        }
    }

    public static async Task<ValidationError?> ValidateEnumGroupReferencesAsync(
        EnumDictionaryRepository? repository,
        IReadOnlyList<AdminManifestLogicalTableDto>? logicalTables,
        IReadOnlyList<AdminManifestScreenColumnDto>? columns,
        CancellationToken ct)
    {
        var referenced = EnumerateColumns(logicalTables, columns)
            .Where(c => !string.IsNullOrWhiteSpace(c.EnumGroupId))
            .ToList();

        if (referenced.Count == 0) return null;

        if (repository is null)
        {
            return new ValidationError(
                "ENUM_DICTIONARY_NOT_AVAILABLE",
                "Enum dictionary repository is not configured; cannot resolve enumGroupId references.");
        }

        foreach (var col in referenced)
        {
            var raw = col.EnumGroupId!.Trim();
            if (!Guid.TryParse(raw, out var groupId))
            {
                return new ValidationError(
                    "ENUM_GROUP_ID_MALFORMED",
                    $"Column '{col.Name}' enumGroupId is not a valid UUID.");
            }

            var error = await repository.ValidateGroupReferenceAsync(groupId, ct);
            if (error is not null) return error;
        }

        return null;
    }
}
