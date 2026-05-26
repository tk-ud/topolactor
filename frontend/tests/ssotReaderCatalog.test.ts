import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { parse } from "https://deno.land/std@0.208.0/yaml/mod.ts";

// SSOT reader tests for ui-ux-primitive-catalog-ssot.yaml
// Verifies category count, primitive count, and boundary key existence.
// Does NOT redefine vocabulary — expectations are derived from the SSOT itself.

type UiUxSsot = {
  catalog_summary?: {
    total_categories?: number;
    total_primitives?: number;
    categories?: unknown;
  };
  meaning_boundaries?: Record<string, unknown>;
  [key: string]: unknown;
};

type AbstractFunctionSsot = {
  registry_summary?: {
    total_categories?: number;
    total_functions?: number;
    categories?: unknown;
  };
  mutation_boundary_types?: Record<string, unknown>;
  [key: string]: unknown;
};

const CATALOG_SSOT_PATH = new URL(
  "../../docs/design/ui-ux-primitive-catalog-ssot.yaml",
  import.meta.url,
);
const FUNCTION_SSOT_PATH = new URL(
  "../../docs/design/abstract-function-primitive-registry-ssot.yaml",
  import.meta.url,
);

// Category keys in ui-ux-primitive-catalog-ssot.yaml
const CATEGORY_KEYS = [
  "category_a_search_suggest",
  "category_b_inline_edit_audit",
  "category_c_table_list_view",
  "category_d_kanban_drag_drop",
  "category_e_design_token",
  "category_f_calculation_topology",
  "category_g_external_lookup",
  "category_h_safety_guard",
];

// Category keys in abstract-function-primitive-registry-ssot.yaml
const FUNCTION_CATEGORY_KEYS = [
  "category_a_search_suggest",
  "category_b_normalize_lookup_import",
  "category_c_candidate_update_audit",
  "category_d_calculation_topology",
  "category_e_layout_design_token",
  "category_f_operation_safety",
  "category_g_basic_calculation_operators",
];

// Boundary keys declared in ui-ux-primitive-catalog-ssot.yaml meaning_boundaries
const MEANING_BOUNDARY_KEYS = [
  "registration_boundary",
  "frontend_topology_judgment",
  "mutation_boundary",
  "calculation_boundary",
  "external_lookup_boundary",
  "sql_attention_boundary",
  "seed_role_boundary",
  "style_token_boundary",
  "yaml_static_vocabulary_boundary",
  "layout_builder_reference",
];

Deno.test("ui-ux-primitive-catalog-ssot: file is readable and parseable", async () => {
  const raw = await Deno.readTextFile(CATALOG_SSOT_PATH);
  const ssot = parse(raw) as UiUxSsot;
  assert(ssot !== null && typeof ssot === "object", "SSOT must be a YAML object");
  assert(ssot.version !== undefined, "SSOT must have version field");
});

Deno.test("ui-ux-primitive-catalog-ssot: catalog_summary declares 8 categories and 82 primitives", async () => {
  const raw = await Deno.readTextFile(CATALOG_SSOT_PATH);
  const ssot = parse(raw) as UiUxSsot;
  assert(ssot.catalog_summary, "catalog_summary must exist");
  assertEquals(
    ssot.catalog_summary.total_categories,
    8,
    "catalog_summary.total_categories must be 8",
  );
  assertEquals(
    ssot.catalog_summary.total_primitives,
    82,
    "catalog_summary.total_primitives must be 82",
  );
});

Deno.test("ui-ux-primitive-catalog-ssot: all 8 category keys are present", async () => {
  const raw = await Deno.readTextFile(CATALOG_SSOT_PATH);
  const ssot = parse(raw) as UiUxSsot;
  for (const key of CATEGORY_KEYS) {
    assert(
      key in ssot,
      `category key '${key}' must exist in SSOT`,
    );
  }
});

Deno.test("ui-ux-primitive-catalog-ssot: actual primitive count matches declared total", async () => {
  const raw = await Deno.readTextFile(CATALOG_SSOT_PATH);
  const ssot = parse(raw) as UiUxSsot;
  let count = 0;
  for (const catKey of CATEGORY_KEYS) {
    const cat = ssot[catKey] as { primitives?: Record<string, unknown> } | undefined;
    assert(cat, `category '${catKey}' must exist`);
    assert(cat.primitives && typeof cat.primitives === "object", `'${catKey}.primitives' must be an object`);
    count += Object.keys(cat.primitives).length;
  }
  assertEquals(
    count,
    ssot.catalog_summary?.total_primitives,
    `actual primitive count ${count} must equal catalog_summary.total_primitives`,
  );
});

Deno.test("ui-ux-primitive-catalog-ssot: meaning_boundaries contains required boundary keys", async () => {
  const raw = await Deno.readTextFile(CATALOG_SSOT_PATH);
  const ssot = parse(raw) as UiUxSsot;
  assert(ssot.meaning_boundaries, "meaning_boundaries must exist");
  for (const key of MEANING_BOUNDARY_KEYS) {
    assert(
      key in ssot.meaning_boundaries,
      `meaning_boundaries must contain key '${key}'`,
    );
  }
});

Deno.test("ui-ux-primitive-catalog-ssot: every primitive has required fields (key, kind, runtimeConnected)", async () => {
  const raw = await Deno.readTextFile(CATALOG_SSOT_PATH);
  const ssot = parse(raw) as UiUxSsot;
  for (const catKey of CATEGORY_KEYS) {
    const cat = ssot[catKey] as { primitives?: Record<string, Record<string, unknown>> } | undefined;
    if (!cat?.primitives) continue;
    for (const [name, prim] of Object.entries(cat.primitives)) {
      assert(
        typeof prim.key === "string" && prim.key.length > 0,
        `primitive '${name}' in '${catKey}' must have a key field`,
      );
      assert(
        typeof prim.kind === "string" && prim.kind.length > 0,
        `primitive '${name}' in '${catKey}' must have a kind field`,
      );
      assert(
        "runtimeConnected" in prim,
        `primitive '${name}' in '${catKey}' must declare runtimeConnected`,
      );
    }
  }
});

// SSOT reader tests for abstract-function-primitive-registry-ssot.yaml

Deno.test("abstract-function-primitive-registry-ssot: file is readable and parseable", async () => {
  const raw = await Deno.readTextFile(FUNCTION_SSOT_PATH);
  const ssot = parse(raw) as AbstractFunctionSsot;
  assert(ssot !== null && typeof ssot === "object", "SSOT must be a YAML object");
  assert(ssot.version !== undefined, "SSOT must have version field");
});

Deno.test("abstract-function-primitive-registry-ssot: registry_summary declares 7 categories and 60 functions", async () => {
  const raw = await Deno.readTextFile(FUNCTION_SSOT_PATH);
  const ssot = parse(raw) as AbstractFunctionSsot;
  assert(ssot.registry_summary, "registry_summary must exist");
  assertEquals(
    ssot.registry_summary.total_categories,
    7,
    "registry_summary.total_categories must be 7",
  );
  assertEquals(
    ssot.registry_summary.total_functions,
    60,
    "registry_summary.total_functions must be 60",
  );
});

Deno.test("abstract-function-primitive-registry-ssot: all 6 category keys are present", async () => {
  const raw = await Deno.readTextFile(FUNCTION_SSOT_PATH);
  const ssot = parse(raw) as AbstractFunctionSsot;
  for (const key of FUNCTION_CATEGORY_KEYS) {
    assert(
      key in ssot,
      `category key '${key}' must exist in abstract function SSOT`,
    );
  }
});

Deno.test("abstract-function-primitive-registry-ssot: actual function count matches declared total", async () => {
  const raw = await Deno.readTextFile(FUNCTION_SSOT_PATH);
  const ssot = parse(raw) as AbstractFunctionSsot;
  let count = 0;
  for (const catKey of FUNCTION_CATEGORY_KEYS) {
    const cat = ssot[catKey] as { functions?: Record<string, unknown> } | undefined;
    assert(cat, `category '${catKey}' must exist`);
    assert(cat.functions && typeof cat.functions === "object", `'${catKey}.functions' must be an object`);
    count += Object.keys(cat.functions).length;
  }
  assertEquals(
    count,
    ssot.registry_summary?.total_functions,
    `actual function count ${count} must equal registry_summary.total_functions`,
  );
});

Deno.test("abstract-function-primitive-registry-ssot: mutation_boundary_types contains required boundary kinds", async () => {
  const raw = await Deno.readTextFile(FUNCTION_SSOT_PATH);
  const ssot = parse(raw) as AbstractFunctionSsot;
  assert(ssot.mutation_boundary_types, "mutation_boundary_types must exist");
  const requiredKinds = [
    "readonly",
    "candidate_surface",
    "preview_only",
    "preview_validate_explicit_apply",
    "explicit_confirm_required",
    "append_only",
    "sql_attention_observation_only",
  ];
  for (const kind of requiredKinds) {
    assert(
      kind in ssot.mutation_boundary_types,
      `mutation_boundary_types must contain '${kind}'`,
    );
  }
});

Deno.test("abstract-function-primitive-registry-ssot: every function has required fields (signature, mutation_boundary)", async () => {
  const raw = await Deno.readTextFile(FUNCTION_SSOT_PATH);
  const ssot = parse(raw) as AbstractFunctionSsot;
  for (const catKey of FUNCTION_CATEGORY_KEYS) {
    const cat = ssot[catKey] as { functions?: Record<string, Record<string, unknown>> } | undefined;
    if (!cat?.functions) continue;
    for (const [name, fn] of Object.entries(cat.functions)) {
      assert(
        typeof fn.signature === "string" && fn.signature.length > 0,
        `function '${name}' in '${catKey}' must have a signature field`,
      );
      assert(
        typeof fn.mutation_boundary === "string" && fn.mutation_boundary.length > 0,
        `function '${name}' in '${catKey}' must declare mutation_boundary`,
      );
    }
  }
});


Deno.test("abstract-function-primitive-registry-ssot: basic calculation operators exist and keep readonly/preview boundaries", async () => {
  const raw = await Deno.readTextFile(FUNCTION_SSOT_PATH);
  const ssot = parse(raw) as AbstractFunctionSsot;
  const category = ssot["category_g_basic_calculation_operators"] as {
    functions?: Record<string, Record<string, unknown>>;
  } | undefined;
  assert(category?.functions, "category_g_basic_calculation_operators.functions must exist");

  const required = ["add", "subtract", "multiply", "divide", "percentage", "round", "floor", "ceil", "min", "max", "clamp"];
  for (const name of required) {
    const fn = category.functions[name];
    assert(fn, `operator '${name}' must exist`);
    assert(typeof fn.signature === "string" && fn.signature.length > 0, `operator '${name}' must have signature`);
    assert(typeof fn.output_kind === "string" && fn.output_kind.length > 0, `operator '${name}' must have output_kind`);
    assert(typeof fn.mutation_boundary === "string" && fn.mutation_boundary.length > 0, `operator '${name}' must have mutation_boundary`);
    assert(["readonly", "preview_only"].includes(String(fn.mutation_boundary)), `operator '${name}' mutation_boundary must be readonly or preview_only`);
  }

  for (const name of ["divide", "percentage"]) {
    const fn = category.functions[name] as Record<string, unknown>;
    assert(typeof fn.zero_division_policy === "object" && fn.zero_division_policy !== null, `operator '${name}' must define zero_division_policy`);
  }
});
