import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { draftPreviewResultToEmission } from "../runtime/draftPreviewToEmission.ts";
import { renderEmission } from "../runtime/renderEmission.ts";
import { defaultComponentRegistry } from "../registry/componentRegistry.ts";

Deno.test("draftPreviewResultToEmission: maps design → componentDesign for renderEmission", () => {
  const emission = draftPreviewResultToEmission({
    success: true,
    layoutId: "layout-1",
    packageId: "pkg-1",
    layoutNodes: [
      {
        nodeId: "n-h1",
        nodeKind: "structural_html",
        htmlTag: "h1",
        orderIndex: 0,
        design: { inlineText: "従業員管理", cssTokenRefs: ["color.primary"] },
      },
      {
        nodeId: "n-btn",
        nodeKind: "catalog_component",
        componentKey: "button.primitive",
        componentKind: "action/button",
        componentId: "comp-btn",
        orderIndex: 1,
        design: { inlineText: "検索" },
      },
    ],
  });

  assertEquals(emission?.layoutId, "layout-1");
  assertEquals(emission?.layoutNodes?.[0]?.componentDesign?.inlineText, "従業員管理");
  assertEquals(emission?.layoutNodes?.[0]?.componentDesign?.cssTokenRefs, ["color.primary"]);

  const specs = renderEmission(emission!, defaultComponentRegistry);
  const h1 = specs.find((s) => s.nodeId === "n-h1");
  assertEquals(h1?.inlineText, "従業員管理");
  assertEquals(h1?.cssTokenRefs, ["color.primary"]);
});

Deno.test("draftPreviewResultToEmission: maps first initialDataRows entry to emission.data", () => {
  const emission = draftPreviewResultToEmission({
    success: true,
    layoutId: "layout-1",
    layoutNodes: [
      { nodeId: "n1", componentKey: "input.primitive", orderIndex: 0 },
    ],
    initialDataRows: [{ name: "Alice" }, { name: "Bob" }],
  });
  assertEquals(emission?.data, { name: "Alice" });
});

Deno.test("draftPreviewResultToEmission: returns null when no nodes", () => {
  assertEquals(
    draftPreviewResultToEmission({ success: true, layoutId: "x", layoutNodes: [] }),
    null,
  );
});
