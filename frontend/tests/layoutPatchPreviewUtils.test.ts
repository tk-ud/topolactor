import { assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import {
  buildLayoutClassPreviewResolutions,
  buildLayoutPatchPreviewAudit,
} from "../runtime/layoutPatchPreviewUtils.ts";

Deno.test("buildLayoutClassPreviewResolutions: known class ref resolves className", () => {
  const rows = buildLayoutClassPreviewResolutions(["layout.root.grid"]);
  assertEquals(rows.length, 1);
  assertEquals(rows[0].classRef, "layout.root.grid");
  assertEquals(rows[0].ok, true);
  assertEquals(rows[0].className.includes("topolactor-topology-layout-root-grid"), true);
});

Deno.test("buildLayoutClassPreviewResolutions: unknown class ref fails explicitly", () => {
  const rows = buildLayoutClassPreviewResolutions(["layout.unknown.ref"]);
  assertEquals(rows.length, 1);
  assertEquals(rows[0].ok, false);
  assertEquals(rows[0].message, "UNKNOWN_CLASS_REF");
});

Deno.test("buildLayoutPatchPreviewAudit: reports node coordinate diff after backend normalization", () => {
  const before = JSON.stringify({
    grid: { cols: 12 },
    nodes: [{ nodeId: "n1", componentKey: "Box", x: 10, y: 20, width: 140, height: 60 }],
  });
  const after = JSON.stringify({
    grid: { cols: 12 },
    nodes: [{ nodeId: "n1", componentKey: "Box", x: 20, y: 20, width: 140, height: 60 }],
  });
  const audit = buildLayoutPatchPreviewAudit(before, after, "Layout patch normalized (placement only).");
  assertEquals(audit.nodeCountBefore, 1);
  assertEquals(audit.nodeCountAfter, 1);
  assertEquals(
    audit.fields.some((field) => field.fieldLabel === "node:n1:x"),
    true,
  );
  assertEquals(audit.fields.find((field) => field.fieldLabel === "node:n1:x")?.before, "10");
  assertEquals(audit.fields.find((field) => field.fieldLabel === "node:n1:x")?.after, "20");
});

Deno.test("buildLayoutPatchPreviewAudit: includes layoutClassRefs diff and canvas root class", () => {
  const before = JSON.stringify({ grid: { cols: 12 }, nodes: [] });
  const after = JSON.stringify({
    grid: { cols: 12 },
    layoutClassRefs: ["layout.root.grid"],
    nodes: [],
  });
  const audit = buildLayoutPatchPreviewAudit(before, after);
  assertEquals(
    audit.fields.some((field) => field.fieldLabel === "layoutClassRefs"),
    true,
  );
  assertEquals(audit.classResolutions.length, 1);
  assertEquals(audit.canvasRootClassName.includes("topolactor-topology-layout-root-grid"), true);
});

Deno.test("buildLayoutPatchPreviewAudit: identical payload yields empty diff fields", () => {
  const json = JSON.stringify({
    grid: { cols: 12 },
    layoutClassRefs: ["layout.root.grid"],
    nodes: [{ nodeId: "n1", componentKey: "Box", x: 0, y: 0, width: 140, height: 60 }],
  });
  const audit = buildLayoutPatchPreviewAudit(json, json);
  assertEquals(audit.fields.length, 0);
  assertEquals(audit.classResolutions.length, 1);
});
