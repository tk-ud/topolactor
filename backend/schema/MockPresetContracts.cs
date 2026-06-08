using System.Text.Json;
using System.Text.Json.Serialization;

namespace Topolactor.Schema;

// ---------------------------------------------------------------------------
// Mock Preset Intake Compiler — backend contract types
// SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
// Authority: AdminRuntime.MockPreset partial class
// ---------------------------------------------------------------------------

// ─── create ─────────────────────────────────────────────────────────────────

public record MockPresetCreateRequest(
    string PresetKey,
    string PresetLabel,
    string SourceKind,          // svg_xml | generic_xml | figma_json | ui_builder_canvas
    string SourceHash,
    JsonElement SourceSnapshotJson,
    JsonElement VisualTreeJson
);

public record MockPresetCreateResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("presetId")] string? PresetId,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null
);

// ─── list ────────────────────────────────────────────────────────────────────

public record MockPresetListItem(
    [property: JsonPropertyName("presetId")] string PresetId,
    [property: JsonPropertyName("presetKey")] string PresetKey,
    [property: JsonPropertyName("presetLabel")] string PresetLabel,
    [property: JsonPropertyName("sourceKind")] string SourceKind,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] string CreatedAt
);

// ─── get ─────────────────────────────────────────────────────────────────────

public record MockPresetObjectMappingRecord(
    [property: JsonPropertyName("mappingId")] string MappingId,
    [property: JsonPropertyName("sourceObjectId")] string SourceObjectId,
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("nodeKind")] string NodeKind,
    [property: JsonPropertyName("componentKey")] string? ComponentKey,
    [property: JsonPropertyName("componentKind")] string? ComponentKind,
    [property: JsonPropertyName("htmlTag")] string? HtmlTag,
    [property: JsonPropertyName("parentSourceObjectId")] string? ParentSourceObjectId,
    [property: JsonPropertyName("slotKey")] string? SlotKey,
    [property: JsonPropertyName("orderIndex")] int OrderIndex,
    [property: JsonPropertyName("bboxJson")] JsonElement BboxJson,
    [property: JsonPropertyName("textJson")] JsonElement TextJson,
    [property: JsonPropertyName("styleCandidateJson")] JsonElement StyleCandidateJson,
    [property: JsonPropertyName("mappingStatus")] string MappingStatus
);

public record MockPresetWiringCandidateRecord(
    [property: JsonPropertyName("wiringCandidateId")] string WiringCandidateId,
    [property: JsonPropertyName("sourceObjectId")] string SourceObjectId,
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("capabilityTag")] string CapabilityTag,
    [property: JsonPropertyName("wiringKind")] string? WiringKind,
    [property: JsonPropertyName("targetSurface")] string? TargetSurface,
    [property: JsonPropertyName("targetRef")] string? TargetRef,
    [property: JsonPropertyName("bindingJson")] JsonElement BindingJson,
    [property: JsonPropertyName("status")] string Status
);

public record MockPresetDetail(
    [property: JsonPropertyName("presetId")] string PresetId,
    [property: JsonPropertyName("presetKey")] string PresetKey,
    [property: JsonPropertyName("presetLabel")] string PresetLabel,
    [property: JsonPropertyName("sourceKind")] string SourceKind,
    [property: JsonPropertyName("sourceHash")] string SourceHash,
    [property: JsonPropertyName("visualTreeJson")] JsonElement VisualTreeJson,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("objectMappings")] IReadOnlyList<MockPresetObjectMappingRecord> ObjectMappings,
    [property: JsonPropertyName("wiringCandidates")] IReadOnlyList<MockPresetWiringCandidateRecord> WiringCandidates
);

// ─── compile ─────────────────────────────────────────────────────────────────

public record MockPresetCompileResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("compileSnapshotId")] string? CompileSnapshotId,
    [property: JsonPropertyName("layoutPatchJson")] JsonElement? LayoutPatchJson,
    [property: JsonPropertyName("unresolvedJson")] JsonElement? UnresolvedJson,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
    [property: JsonPropertyName("message")] string? Message = null
);

// ─── bind ────────────────────────────────────────────────────────────────────

public record MockPresetBindResult(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("layoutPatchJson")] JsonElement? LayoutPatchJson,
    [property: JsonPropertyName("packageMembershipCandidateJson")] JsonElement? PackageMembershipCandidateJson,
    [property: JsonPropertyName("wiringCandidateJson")] JsonElement? WiringCandidateJson,
    [property: JsonPropertyName("styleCandidateJson")] JsonElement? StyleCandidateJson,
    [property: JsonPropertyName("unresolvedJson")] JsonElement? UnresolvedJson,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
    [property: JsonPropertyName("message")] string? Message = null
);
