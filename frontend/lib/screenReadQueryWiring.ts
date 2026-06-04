import type { ManifestScreenDesignDraft, SearchCondition, HavingCondition } from "./manifestScreenDesign.ts";

/** Runtime value source kinds (SSOT runtime_execution_contract). */
export type ConditionValueSourceKind =
  | "literal"
  | "runtimeParam"
  | "operationInput"
  | "routeQuery"
  | "authClaim"
  | "formValue";

export type ConditionValueSource = {
  kind: ConditionValueSourceKind;
  key?: string;
  /** Preview-only sample; not used at runtime when kind !== literal. */
  sampleValue?: string;
};

export type ScreenReadQueryWiringCandidate = {
  wiringKey: string;
  label: string;
  field: string;
  valueSourceKinds: ConditionValueSourceKind[];
};

export function literalValueSource(sampleValue?: string): ConditionValueSource {
  return { kind: "literal", sampleValue: sampleValue ?? "" };
}

/** Build value sources for assign payload — preview literals stay in value fields. */
export function searchConditionWithSources(
  cond: SearchCondition,
  valueSource?: ConditionValueSource,
  valueToSource?: ConditionValueSource,
): SearchConditionInputWithSources {
  return {
    column: cond.column,
    operator: cond.operator,
    value: cond.value,
    valueTo: cond.valueTo,
    values: cond.values,
    logicalConnector: cond.logicalConnector,
    valueSource: valueSource ?? literalValueSource(cond.value),
    valueToSource: cond.valueTo
      ? valueToSource ?? literalValueSource(cond.valueTo)
      : undefined,
  };
}

export type SearchConditionInputWithSources = {
  column: string;
  operator: string;
  value?: string;
  valueTo?: string;
  values?: string[];
  logicalConnector?: string;
  valueSource?: ConditionValueSource;
  valueToSource?: ConditionValueSource;
};

export type HavingConditionInputWithSources = {
  column: string;
  function: string;
  operator: string;
  value: string;
  valueSource?: ConditionValueSource;
};

export function havingConditionWithSources(
  hc: HavingCondition,
  valueSource?: ConditionValueSource,
): HavingConditionInputWithSources {
  return {
    column: hc.column,
    function: hc.function,
    operator: hc.operator,
    value: hc.value,
    valueSource: valueSource ?? literalValueSource(hc.value),
  };
}

export function buildScreenReadQueryWiringCandidates(
  rawTopologyJson: string,
): ScreenReadQueryWiringCandidate[] {
  let parsed: unknown;
  try {
    parsed = JSON.parse(rawTopologyJson);
  } catch {
    return [];
  }
  if (!Array.isArray(parsed)) return [];

  const candidates: ScreenReadQueryWiringCandidate[] = [];
  for (const entry of parsed) {
    if (typeof entry !== "object" || entry === null) continue;
    const e = entry as Record<string, unknown>;
    if (e.type !== "screen_data_shape") continue;
    const wiring = e.screenReadQueryWiring;
    if (typeof wiring !== "object" || wiring === null) continue;
    collectFromWiringSection(wiring as Record<string, unknown>, candidates);
  }
  return candidates;
}

function collectFromWiringSection(
  wiring: Record<string, unknown>,
  out: ScreenReadQueryWiringCandidate[],
): void {
  for (const field of ["searchConditions", "havingConditions", "aggregationMeasures", "displayColumns"] as const) {
    const section = wiring[field];
    if (typeof section !== "object" || section === null) continue;
    const sec = section as Record<string, unknown>;
    const bindings = Array.isArray(sec.bindings) ? sec.bindings : [];
    for (const b of bindings) {
      if (typeof b !== "object" || b === null) continue;
      const binding = b as Record<string, unknown>;
      const wiringKey = typeof binding.wiringKey === "string" ? binding.wiringKey : "";
      if (!wiringKey) continue;
      const labelParts: string[] = [field];
      if (typeof binding.column === "string") labelParts.push(binding.column);
      if (typeof binding.operator === "string") labelParts.push(binding.operator);
      if (typeof binding.function === "string") labelParts.push(binding.function);
      out.push({
        wiringKey,
        label: labelParts.join(" · "),
        field,
        valueSourceKinds: field === "searchConditions" || field === "havingConditions"
          ? ["literal", "runtimeParam", "operationInput", "routeQuery", "authClaim", "formValue"]
          : [],
      });
    }
  }
  const mode = wiring.displayColumnMode;
  if (typeof mode === "object" && mode !== null) {
    const m = mode as Record<string, unknown>;
    const wiringKey = typeof m.wiringKey === "string" ? m.wiringKey : "screen.displayColumnMode";
    out.push({
      wiringKey,
      label: `displayColumnMode · ${typeof m.mode === "string" ? m.mode : ""}`,
      field: "displayColumnMode",
      valueSourceKinds: [],
    });
  }
}

export function assignSearchConditionsFromDesign(
  design: ManifestScreenDesignDraft,
): SearchConditionInputWithSources[] {
  return design.searchConditions.map((c) => searchConditionWithSources(c));
}

export function assignHavingConditionsFromDesign(
  design: ManifestScreenDesignDraft,
): HavingConditionInputWithSources[] {
  return design.havingConditions.map((hc) => havingConditionWithSources(hc));
}
