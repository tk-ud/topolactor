import type { ProjectionDefinition } from "./projectionConstructor.ts";

export type ManifestProjectionOutputKind = ProjectionDefinition["outputKind"];

/**
 * Draft-side input mapping selection. "" means "not declared" — the editor
 * emits no inputMapping key, matching the runtime default (collection outer
 * shape preserved by projectionInputFromData). "collection" is the explicit
 * declared form of that same default; "single_row" declares the explicit
 * rows[0] collapse. There is no other allowed value — see
 * parseProjectionDefinitionToDraft for the fail-close read boundary.
 */
export type ManifestProjectionInputMapping = "" | "collection" | "single_row";

export const PROJECTION_INPUT_MAPPING_OPTIONS: ManifestProjectionInputMapping[] = [
  "",
  "collection",
  "single_row",
];

export type ManifestProjectionDraft = {
  enabled: boolean;
  constructorKey: string;
  outputKind: ManifestProjectionOutputKind;
  inputMapping: ManifestProjectionInputMapping;
  packageIds: string;
  componentId: string;
  fieldDefsJson: string;
  componentDefinitionJson: string;
  projectionOverridesJson: string;
};

export const PROJECTION_OUTPUT_KIND_OPTIONS: ManifestProjectionOutputKind[] = [
  "form_inputs",
  "component_projection",
  "ui_projection",
];

export function emptyManifestProjectionDraft(): ManifestProjectionDraft {
  return {
    enabled: false,
    constructorKey: "",
    outputKind: "form_inputs",
    inputMapping: "",
    packageIds: "",
    componentId: "",
    fieldDefsJson: "",
    componentDefinitionJson: "",
    projectionOverridesJson: "",
  };
}

export function extractProjectionDefinitionFromTopology(
  topologyRawJson: string,
): Record<string, unknown> | null {
  try {
    const entries = JSON.parse(topologyRawJson) as unknown;
    if (!Array.isArray(entries)) return null;
    for (const entry of entries) {
      if (!entry || typeof entry !== "object") continue;
      const typed = entry as { type?: string; projection_definition?: unknown };
      if (typed.type !== "projection_constructor_mapping") continue;
      if (typed.projection_definition && typeof typed.projection_definition === "object" &&
        !Array.isArray(typed.projection_definition)) {
        return typed.projection_definition as Record<string, unknown>;
      }
    }
  } catch {
    return null;
  }
  return null;
}

export function parseProjectionDefinitionToDraft(
  definition: Record<string, unknown> | null,
): ManifestProjectionDraft {
  if (!definition) return emptyManifestProjectionDraft();

  const outputKind = definition.outputKind;
  const draft = emptyManifestProjectionDraft();
  draft.enabled = true;
  draft.constructorKey = typeof definition.constructorKey === "string" ? definition.constructorKey : "";
  draft.outputKind = PROJECTION_OUTPUT_KIND_OPTIONS.includes(outputKind as ManifestProjectionOutputKind)
    ? (outputKind as ManifestProjectionOutputKind)
    : "form_inputs";
  if (definition.inputMapping === undefined) {
    draft.inputMapping = "";
  } else if (definition.inputMapping === "single_row" || definition.inputMapping === "collection") {
    draft.inputMapping = definition.inputMapping;
  } else {
    // Fail-close: an inputMapping value outside the allowed set must not be
    // silently normalized away (that would lose or mask the authored data on
    // the next save) and must not be silently accepted (unknown collapse
    // behavior). Surface it as an explicit read error instead.
    throw new Error(
      `projection_definition.inputMapping must be "collection" or "single_row" (got: ${
        JSON.stringify(definition.inputMapping)
      })`,
    );
  }
  draft.packageIds = Array.isArray(definition.packageIds)
    ? definition.packageIds.filter((id): id is string => typeof id === "string").join(", ")
    : "";
  draft.componentId = typeof definition.componentId === "string" ? definition.componentId : "";
  draft.fieldDefsJson = definition.fieldDefs !== undefined
    ? JSON.stringify(definition.fieldDefs, null, 2)
    : "";
  draft.componentDefinitionJson = definition.componentDefinition !== undefined
    ? JSON.stringify(definition.componentDefinition, null, 2)
    : "";
  draft.projectionOverridesJson = definition.projectionOverrides !== undefined
    ? JSON.stringify(definition.projectionOverrides, null, 2)
    : "";
  return draft;
}

function parseOptionalJsonObject(
  label: string,
  raw: string,
): Record<string, unknown> | undefined {
  const trimmed = raw.trim();
  if (!trimmed) return undefined;
  const parsed = JSON.parse(trimmed) as unknown;
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    throw new Error(`${label} must be a JSON object`);
  }
  return parsed as Record<string, unknown>;
}

function parseOptionalJsonArray(label: string, raw: string): unknown[] | undefined {
  const trimmed = raw.trim();
  if (!trimmed) return undefined;
  const parsed = JSON.parse(trimmed) as unknown;
  if (!Array.isArray(parsed)) {
    throw new Error(`${label} must be a JSON array`);
  }
  return parsed;
}

/** Builds draft payload for backend manifest:create_draft / update_draft. */
export function buildProjectionDefinitionPayload(
  draft: ManifestProjectionDraft,
): Record<string, unknown> | null {
  if (!draft.enabled) return null;

  const constructorKey = draft.constructorKey.trim();
  if (!constructorKey) {
    throw new Error("projection_constructor_mapping を有効にする場合、constructorKey が必要です。");
  }

  const payload: Record<string, unknown> = {
    constructorKey,
    outputKind: draft.outputKind,
    packageIds: draft.packageIds
      .split(",")
      .map((id) => id.trim())
      .filter(Boolean),
  };

  // "" (not declared) omits the key entirely — this preserves the runtime
  // default (collection outer shape) without over-specifying it. Explicit
  // "collection" / "single_row" are emitted verbatim so a saved single_row
  // mapping survives the admin editor round-trip instead of disappearing.
  if (draft.inputMapping === "single_row" || draft.inputMapping === "collection") {
    payload.inputMapping = draft.inputMapping;
  }

  const componentId = draft.componentId.trim();
  if (componentId) payload.componentId = componentId;

  const fieldDefs = parseOptionalJsonArray("fieldDefs", draft.fieldDefsJson);
  if (fieldDefs) payload.fieldDefs = fieldDefs;

  const componentDefinition = parseOptionalJsonObject(
    "componentDefinition",
    draft.componentDefinitionJson,
  );
  if (componentDefinition) payload.componentDefinition = componentDefinition;

  const projectionOverrides = parseOptionalJsonObject(
    "projectionOverrides",
    draft.projectionOverridesJson,
  );
  if (projectionOverrides) payload.projectionOverrides = projectionOverrides;

  return payload;
}

export function formatProjectionSummary(definition: Record<string, unknown> | null): string {
  if (!definition) return "none";
  const key = typeof definition.constructorKey === "string" ? definition.constructorKey : "?";
  const kind = typeof definition.outputKind === "string" ? definition.outputKind : "?";
  const packageCount = Array.isArray(definition.packageIds) ? definition.packageIds.length : 0;
  const inputMapping = definition.inputMapping === "single_row" ||
      definition.inputMapping === "collection"
    ? ` [inputMapping=${definition.inputMapping}]`
    : "";
  return `${key} → ${kind} (${packageCount} packageId(s))${inputMapping}`;
}
