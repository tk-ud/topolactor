import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { parse } from "https://deno.land/std@0.208.0/yaml/mod.ts";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";

// Verifies SSOT / frontend catalog / DB seed alignment for document_canvas_template_editor.primitive.
// This test detects drift between any of the four surfaces.

const COMPONENT_KEY = "document_canvas_template_editor.primitive";
const COMPONENT_KIND = "document_canvas/document_canvas_template_editor";
const SOURCE_PATH = "frontend/components/DocumentCanvasTemplateEditor.tsx";

async function loadSeed(): Promise<string> {
  return await Deno.readTextFile(
    new URL("../../db/seed_empty.sql", import.meta.url),
  );
}

async function loadSsot(): Promise<Record<string, unknown>> {
  const raw = await Deno.readTextFile(
    new URL(
      "../../docs/design/ui-ux-primitive-catalog-ssot.yaml",
      import.meta.url,
    ),
  );
  return parse(raw) as Record<string, unknown>;
}

Deno.test("documentCanvasCatalogCheck: entry exists in COMPONENT_CATALOG_ENTRIES", () => {
  const entry = COMPONENT_CATALOG_ENTRIES.find(
    (e) => e.componentKey === COMPONENT_KEY,
  );
  assert(entry, `${COMPONENT_KEY} must be in COMPONENT_CATALOG_ENTRIES`);
  assertEquals(entry.componentKind, COMPONENT_KIND);
  assertEquals(entry.sourcePath, SOURCE_PATH);
  assertEquals(entry.componentFamily, "primitive");
  assertEquals(entry.semanticRole, "admin_operation");
  assertEquals(entry.visualRole, "canvas");
  assertEquals(entry.lifecycleStatus, "code_only_drift");
  assertEquals(entry.runtimeConnected, true);
  assertEquals(entry.registrationRequired, true);
  assert(
    entry.capabilityTags.includes("field_binding"),
    "capabilityTags must include field_binding",
  );
  assert(
    entry.capabilityTags.includes("preview_surface"),
    "capabilityTags must include preview_surface",
  );
  assert(
    entry.capabilityTags.includes("export_snapshot"),
    "capabilityTags must include export_snapshot",
  );
  assert(
    entry.capabilityTags.includes("admin_only"),
    "capabilityTags must include admin_only",
  );
});

Deno.test("documentCanvasCatalogCheck: sourcePath file exists", async () => {
  const repoRoot = new URL("../../", import.meta.url);
  const filePath = new URL(SOURCE_PATH, repoRoot);
  const stat = await Deno.stat(filePath);
  assert(stat.isFile, `${SOURCE_PATH} must be a file`);
});

Deno.test("documentCanvasCatalogCheck: component is exported from frontend/components/index.ts", async () => {
  const indexContent = await Deno.readTextFile(
    new URL("../../frontend/components/index.ts", import.meta.url),
  );
  assert(
    indexContent.includes("DocumentCanvasTemplateEditor"),
    "index.ts must export DocumentCanvasTemplateEditor",
  );
  assert(
    indexContent.includes("DocumentCanvasTemplateEditorProps"),
    "index.ts must export DocumentCanvasTemplateEditorProps",
  );
});

Deno.test("documentCanvasCatalogCheck: seed_empty.sql contains matching ui_component_bucket row", async () => {
  const sql = await loadSeed();
  assert(
    sql.includes(`'${COMPONENT_KEY}'`),
    `seed_empty.sql must contain component_key '${COMPONENT_KEY}'`,
  );
  assert(
    sql.includes(`'${SOURCE_PATH}'`),
    `seed_empty.sql must contain source_path '${SOURCE_PATH}'`,
  );
  assert(
    sql.includes(`'${COMPONENT_KIND}'`),
    `seed_empty.sql must contain component_kind '${COMPONENT_KIND}'`,
  );
});

Deno.test("documentCanvasCatalogCheck: SSOT contains document_canvas_template_editor entry", async () => {
  const ssot = await loadSsot();
  const cat = ssot["category_g_document_canvas"] as
    | { primitives?: Record<string, Record<string, unknown>> }
    | undefined;
  assert(
    cat?.primitives?.DocumentCanvasTemplateEditor,
    "category_g_document_canvas.primitives.DocumentCanvasTemplateEditor must exist in SSOT",
  );
  const prim = cat!.primitives!.DocumentCanvasTemplateEditor;
  assertEquals(
    prim.key,
    COMPONENT_KEY,
    "SSOT primitive key must match COMPONENT_KEY",
  );
  assertEquals(
    prim.kind,
    COMPONENT_KIND,
    "SSOT primitive kind must match COMPONENT_KIND",
  );
  assertEquals(
    prim.runtimeConnected,
    true,
    "SSOT runtimeConnected must be true after factory/projectionConstructor wiring",
  );
});

Deno.test("documentCanvasCatalogCheck: catalog and seed classification fields match", async () => {
  const sql = await loadSeed();
  const entry = COMPONENT_CATALOG_ENTRIES.find(
    (e) => e.componentKey === COMPONENT_KEY,
  );
  assert(entry);

  // Extract the seed row JSON for this component_key
  const rowRegex =
    /\('([^']+)'\s*,\s*'([^']+)'\s*,\s*'([^']+)'\s*,\s*'bucketed'\s*,\s*'(\{[\s\S]*?\})'::jsonb\)/g;
  let seedRow: Record<string, unknown> | null = null;
  let m: RegExpExecArray | null;
  while ((m = rowRegex.exec(sql)) !== null) {
    if (m[1] === COMPONENT_KEY) {
      seedRow = JSON.parse(m[4]).classification;
      break;
    }
  }
  assert(seedRow, `seed row for ${COMPONENT_KEY} must be parseable`);
  assertEquals(seedRow.runtimeConnected, entry.runtimeConnected);
  assertEquals(seedRow.registrationRequired, entry.registrationRequired);
  assertEquals(seedRow.lifecycleStatus, entry.lifecycleStatus);
  assertEquals(seedRow.componentFamily, entry.componentFamily);
  assertEquals(seedRow.semanticRole, entry.semanticRole);
  assertEquals(seedRow.visualRole, entry.visualRole);
  assertEquals(seedRow.capabilityTags, entry.capabilityTags);
});
