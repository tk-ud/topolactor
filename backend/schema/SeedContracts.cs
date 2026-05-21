using System.Text.Json.Serialization;

namespace Topolactor.Schema;

public record SeedSaveResult(bool Success, string? ErrorCode, string? ErrorMessage);

public record SeedLoadResult(bool Success, string? Json, string? ErrorCode, string? ErrorMessage);

public record SeedValidationError(string Code, string Message);

public record SeedValidationResult(bool IsValid, IReadOnlyList<SeedValidationError> Errors);

public record SeedRuntimePreview(string Name, string Target, string Layer, string Action);

public record SeedPreviewData(IReadOnlyList<SeedRuntimePreview> Runtimes, int RuntimeCount);

public record SeedPreviewResult(bool Success, SeedPreviewData? Data, IReadOnlyList<SeedValidationError> Errors);

public record SeedImportResult(bool Success, int ImportedCount, IReadOnlyList<SeedValidationError> Errors);

public record SeedSaveResponseDto(
    [property: JsonPropertyName("ok")]      bool Ok,
    [property: JsonPropertyName("error")]   string? Error = null
);

public record SeedLoadResponseDto(
    [property: JsonPropertyName("ok")]      bool Ok,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("error")]   string? Error = null
);

public record SeedValidationErrorDto(
    [property: JsonPropertyName("code")]    string Code,
    [property: JsonPropertyName("message")] string Message
);

public record SeedValidationResponseDto(
    [property: JsonPropertyName("ok")]     bool Ok,
    [property: JsonPropertyName("valid")]  bool Valid,
    [property: JsonPropertyName("errors")] IReadOnlyList<SeedValidationErrorDto> Errors
);

public record SeedRuntimePreviewDto(
    [property: JsonPropertyName("name")]   string Name,
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("layer")]  string Layer,
    [property: JsonPropertyName("action")] string Action
);

public record SeedPreviewResponseDto(
    [property: JsonPropertyName("ok")]           bool Ok,
    [property: JsonPropertyName("runtimeCount")] int RuntimeCount,
    [property: JsonPropertyName("runtimes")]     IReadOnlyList<SeedRuntimePreviewDto> Runtimes,
    [property: JsonPropertyName("errors")]       IReadOnlyList<SeedValidationErrorDto> Errors
);

public record SeedImportResponseDto(
    [property: JsonPropertyName("ok")]            bool Ok,
    [property: JsonPropertyName("importedCount")] int ImportedCount,
    [property: JsonPropertyName("note")]          string? Note,
    [property: JsonPropertyName("errors")]        IReadOnlyList<SeedValidationErrorDto> Errors
);
