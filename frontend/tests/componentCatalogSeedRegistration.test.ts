import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { parse } from "https://deno.land/std@0.208.0/yaml/mod.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";

const seedSql = await Deno.readTextFile(new URL("../../db/seed_empty.sql", import.meta.url));
const ssotRaw = await Deno.readTextFile(new URL("../../docs/design/component-catalog-classification-ssot.yaml", import.meta.url));
const ssot = parse(ssotRaw) as Record<string, unknown>;

function extractSeedRows() {
  const rows = new Map<string, { sourcePath: string; componentKind: string; classification: Record<string, unknown> }>();
  const rowRegex = /\('([^']+)'\s*,\s*'([^']+)'\s*,\s*'([^']+)'\s*,\s*'bucketed'\s*,\s*'(\{[^;]+\})'::jsonb\)/g;
  let m: RegExpExecArray | null;
  while ((m = rowRegex.exec(seedSql)) !== null) {
    rows.set(m[1], {
      sourcePath: m[2],
      componentKind: m[3],
      classification: JSON.parse(m[4]).classification,
    });
  }
  return rows;
}

Deno.test("catalog entries that require registration are represented in ui_component_bucket seed", () => {
  const rows = extractSeedRows();
  for (const entry of COMPONENT_CATALOG_ENTRIES) {
    if (!entry.registrationRequired) continue;
    const row = rows.get(entry.componentKey);
    assert(row, `seed row missing for ${entry.componentKey}`);
    assertEquals(row.sourcePath, entry.sourcePath);
    assertEquals(row.componentKind, entry.componentKind);
    assertEquals(row.classification.runtimeConnected, entry.runtimeConnected);
    assertEquals(row.classification.registrationRequired, entry.registrationRequired);
    assertEquals(row.classification.lifecycleStatus, entry.lifecycleStatus);
    assertEquals(row.classification.componentFamily, entry.componentFamily);
    assertEquals(row.classification.semanticRole, entry.semanticRole);
    assertEquals(row.classification.visualRole, entry.visualRole);
    assertEquals(row.classification.capabilityTags, entry.capabilityTags);
  }
});

Deno.test("seed classification vocabulary is subset of SSOT allowed values", () => {
  const rows = extractSeedRows();
  const allowed = {
    componentFamily: new Set((ssot.component_family as string[]) ?? []),
    semanticRole: new Set((ssot.semantic_role as string[]) ?? []),
    visualRole: new Set((ssot.visual_role as string[]) ?? []),
    lifecycleStatus: new Set((ssot.lifecycle_status as string[]) ?? []),
    capabilityTags: new Set((ssot.capability_tags as string[]) ?? []),
  };

  for (const [, row] of rows) {
    const cls = row.classification;
    assert(allowed.componentFamily.has(String(cls.componentFamily)));
    assert(allowed.semanticRole.has(String(cls.semanticRole)));
    assert(allowed.visualRole.has(String(cls.visualRole)));
    assert(allowed.lifecycleStatus.has(String(cls.lifecycleStatus)));
    for (const tag of (cls.capabilityTags as string[])) {
      assert(allowed.capabilityTags.has(tag));
    }
  }
});
