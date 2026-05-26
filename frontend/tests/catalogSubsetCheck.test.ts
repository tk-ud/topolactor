import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  COMPONENT_CATALOG_ENTRIES,
  UI_UX_PRIMITIVE_CATALOG_IDENTITIES,
} from "../components/catalog.ts";

// Static check: UI_UX_PRIMITIVE_CATALOG_IDENTITIES is a subset of COMPONENT_CATALOG_ENTRIES.componentKey
// Catalog-only primitives (sourcePath: CATALOG_SSOT) must have runtimeConnected:false.
// runtimeConnected:true entries must NOT point to CATALOG_SSOT sourcePath.

const CATALOG_SSOT_PATH = "docs/design/ui-ux-primitive-catalog-ssot.yaml";

Deno.test("catalogSubsetCheck: UI_UX_PRIMITIVE_CATALOG_IDENTITIES keys are a subset of COMPONENT_CATALOG_ENTRIES", () => {
  const catalogKeys = new Set(COMPONENT_CATALOG_ENTRIES.map((e) => e.componentKey));
  for (const identity of UI_UX_PRIMITIVE_CATALOG_IDENTITIES) {
    assert(
      catalogKeys.has(identity.componentKey),
      `UI_UX_PRIMITIVE_CATALOG_IDENTITIES key '${identity.componentKey}' must be present in COMPONENT_CATALOG_ENTRIES`,
    );
  }
});

Deno.test("catalogSubsetCheck: UI_UX_PRIMITIVE_CATALOG_IDENTITIES all point to CATALOG_SSOT sourcePath", () => {
  for (const identity of UI_UX_PRIMITIVE_CATALOG_IDENTITIES) {
    assertEquals(
      identity.sourcePath,
      CATALOG_SSOT_PATH,
      `UI_UX_PRIMITIVE_CATALOG_IDENTITIES entry '${identity.componentKey}' must have sourcePath: CATALOG_SSOT`,
    );
  }
});

Deno.test("catalogSubsetCheck: COMPONENT_CATALOG_ENTRIES with sourcePath=CATALOG_SSOT have runtimeConnected:false", () => {
  for (const entry of COMPONENT_CATALOG_ENTRIES) {
    if (entry.sourcePath === CATALOG_SSOT_PATH) {
      assertEquals(
        entry.runtimeConnected,
        false,
        `CATALOG_SSOT entry '${entry.componentKey}' must have runtimeConnected:false (not yet promoted)`,
      );
    }
  }
});

Deno.test("catalogSubsetCheck: runtimeConnected:true entries do not point to CATALOG_SSOT", () => {
  for (const entry of COMPONENT_CATALOG_ENTRIES) {
    if (entry.runtimeConnected) {
      assert(
        entry.sourcePath !== CATALOG_SSOT_PATH,
        `runtimeConnected:true entry '${entry.componentKey}' must not point to CATALOG_SSOT sourcePath`,
      );
    }
  }
});

Deno.test("catalogSubsetCheck: UI_UX_PRIMITIVE_CATALOG_IDENTITIES componentKey/componentKind matches COMPONENT_CATALOG_ENTRIES", () => {
  const catalogMap = new Map(
    COMPONENT_CATALOG_ENTRIES.map((e) => [e.componentKey, e]),
  );
  for (const identity of UI_UX_PRIMITIVE_CATALOG_IDENTITIES) {
    const entry = catalogMap.get(identity.componentKey);
    assert(entry, `COMPONENT_CATALOG_ENTRIES entry missing for '${identity.componentKey}'`);
    assertEquals(
      entry.componentKind,
      identity.componentKind,
      `componentKind mismatch for '${identity.componentKey}'`,
    );
    assertEquals(
      entry.sourcePath,
      identity.sourcePath,
      `sourcePath mismatch for '${identity.componentKey}'`,
    );
  }
});

Deno.test("catalogSubsetCheck: no duplicate componentKeys in COMPONENT_CATALOG_ENTRIES", () => {
  const seen = new Set<string>();
  for (const entry of COMPONENT_CATALOG_ENTRIES) {
    assert(
      !seen.has(entry.componentKey),
      `duplicate componentKey in COMPONENT_CATALOG_ENTRIES: '${entry.componentKey}'`,
    );
    seen.add(entry.componentKey);
  }
});

Deno.test("catalogSubsetCheck: no duplicate componentKeys in UI_UX_PRIMITIVE_CATALOG_IDENTITIES", () => {
  const seen = new Set<string>();
  for (const identity of UI_UX_PRIMITIVE_CATALOG_IDENTITIES) {
    assert(
      !seen.has(identity.componentKey),
      `duplicate componentKey in UI_UX_PRIMITIVE_CATALOG_IDENTITIES: '${identity.componentKey}'`,
    );
    seen.add(identity.componentKey);
  }
});
