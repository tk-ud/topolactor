import { assertEquals, assert } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";

Deno.test("component catalog classification: required fields exist", () => {
  for (const entry of COMPONENT_CATALOG_ENTRIES) {
    assert(entry.componentKey.length > 0);
    assert(entry.componentKind.length > 0);
    assert(entry.sourcePath.length > 0);
    assert(entry.componentFamily.length > 0);
    assert(entry.semanticRole.length > 0);
    assert(entry.visualRole.length > 0);
    assert(entry.lifecycleStatus.length > 0);
    assert(Array.isArray(entry.capabilityTags));
  }
});

Deno.test("component catalog classification: lifecycle invariants", () => {
  for (const entry of COMPONENT_CATALOG_ENTRIES) {
    if (entry.lifecycleStatus === "code_only_drift") assertEquals(entry.registrationRequired, true);
    if (entry.lifecycleStatus === "alias_maintained") assertEquals(entry.runtimeConnected, true);
    if (entry.componentFamily === "template") {
      assertEquals(entry.lifecycleStatus, "code_only_drift");
      assertEquals(entry.registrationRequired, true);
    }
  }
});

Deno.test("component catalog classification: textarea alias is alias-maintained, not template", () => {
  const textareaAlias = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "textarea.alias");
  const textareaTemplate = COMPONENT_CATALOG_ENTRIES.find((e) => e.componentKey === "textarea.template");
  assert(textareaAlias);
  assert(textareaTemplate);
  assertEquals(textareaAlias.componentFamily, "alias");
  assertEquals(textareaAlias.lifecycleStatus, "alias_maintained");
  assertEquals(textareaAlias.runtimeConnected, true);
  assertEquals(textareaTemplate.componentFamily, "template");
  assertEquals(textareaTemplate.lifecycleStatus, "code_only_drift");
});
