import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { resolveTopologyLayoutClassRefs } from "../runtime/topologyLayoutClassResolver.ts";
import { adaptComponentDataHub } from "../runtime/runtimeComponentAdapter.ts";

Deno.test("topologyLayoutClassResolver: resolves known class keys", () => {
  const result = resolveTopologyLayoutClassRefs([
    "layout.root.grid",
    "layout.section.stack",
  ]);
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(
    result.className,
    "topolactor-topology-layout-root-grid topolactor-topology-layout-section-stack",
  );
});

Deno.test("topologyLayoutClassResolver: unknown class key is explicit error", () => {
  const result = resolveTopologyLayoutClassRefs(["layout.unknown.ref"]);
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.error, "UNKNOWN_TOPOLOGY_LAYOUT_CLASS_REF:layout.unknown.ref");
});

Deno.test("runtimeComponentAdapter: topologyLayoutClassRefs resolve without legacy tailwind", () => {
  const result = adaptComponentDataHub({
    componentId: "c1",
    componentKind: "display/card",
    props: {},
    eventBinding: {},
    design: {
      topologyLayoutClassRefs: ["layout.card.surface"],
      tailwind: "tw-should-not-be-authority",
    },
  });
  assertEquals(result.ok, true);
  if (!result.ok) return;
  assertEquals(result.value.className, "topolactor-topology-layout-card-surface");
});

Deno.test("runtimeComponentAdapter: unknown topologyLayoutClassRef is explicit error", () => {
  const result = adaptComponentDataHub({
    componentId: "c1",
    componentKind: "display/card",
    props: {},
    eventBinding: {},
    design: {
      topologyLayoutClassRefs: ["layout.not.in.ssot"],
    },
  });
  assertEquals(result.ok, false);
  if (result.ok) return;
  assertEquals(result.error, "UNKNOWN_TOPOLOGY_LAYOUT_CLASS_REF:layout.not.in.ssot");
});
