import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { adaptComponentDataHub } from "../runtime/runtimeComponentAdapter.ts";

Deno.test("runtimeComponentAdapter: missing componentId is explicit error", () => {
  const result = adaptComponentDataHub({
    componentKind: "action/button",
    props: { label: "x" },
    eventBinding: {},
  });
  assertEquals(result, { ok: false, error: "RUNTIME_COMPONENT_ADAPTER_MISSING_COMPONENT_ID" });
});

Deno.test("runtimeComponentAdapter: valid hub returns runtime spec", () => {
  const result = adaptComponentDataHub({
    componentId: "c1",
    componentKind: "action/button",
    props: { label: "x" },
    eventBinding: { onClick: { eventType: "click" } },
  });
  assertEquals(result.ok, true);
});
