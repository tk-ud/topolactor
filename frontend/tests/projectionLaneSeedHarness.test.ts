import { assert, assertEquals, assertExists } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { createProjectionRuntime } from "../runtime/projectionRuntime.ts";
import { createSseDispatcher } from "../runtime/sseDispatcher.ts";
import { createProjectionLaneReReadHandler } from "../runtime/projectionLaneReRead.ts";
import { ProjectionView } from "../components/ProjectionView.tsx";
import { h } from "preact";
import { renderToString } from "preact-render-to-string";
import { projectionFromEmission } from "../runtime/renderEmission.ts";
import type { Emission } from "../api/dispatch.ts";
import type { ProjectionDefinition, UiProjection } from "../runtime/projectionConstructor.ts";

type TopologyEntry = Record<string, unknown> & { type?: string };
type SeedManifest = { seedFile: string; manifestId: string; topology: TopologyEntry[] };

const seedFiles = ["db/seed_empty.sql", "db/demo_seed.sql"];
const explicitLiteralFixture = { seedLabel: "explicit-literal-fixture-not-seed-derived" };

function readSeed(path: string): string {
  return Deno.readTextFileSync(path);
}

function parseJsonbLiterals(sql: string): TopologyEntry[] {
  const entries: TopologyEntry[] = [];
  const re = /'((?:[^']|'')*)'::jsonb/g;
  for (const match of sql.matchAll(re)) {
    const raw = match[1].replaceAll("''", "'");
    try {
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) entries.push(parsed as TopologyEntry);
    } catch {
      // Non-topology JSONB literals are outside this static topology parser.
    }
  }
  return entries;
}

function parseManifests(path: string): SeedManifest[] {
  const sql = readSeed(path);
  const manifests: SeedManifest[] = [];
  const tupleRe = /\(\s*'([0-9a-fA-F-]{36})'\s*,\s*(?:NULL|'[0-9a-fA-F-]{36}')\s*,\s*ARRAY\[([\s\S]*?)\]\s*::jsonb\[\]/g;
  for (const match of sql.matchAll(tupleRe)) {
    manifests.push({ seedFile: path, manifestId: match[1], topology: parseJsonbLiterals(match[2]) });
  }
  return manifests;
}

function byType(topology: TopologyEntry[], type: string): TopologyEntry | undefined {
  return topology.find((entry) => entry.type === type);
}

function describeLane(seed: SeedManifest, mapping?: TopologyEntry): string {
  const dispatcher = byType(seed.topology, "dispatcher_mapping");
  return `lane=projection_seed_static seed=${seed.seedFile} manifest=${seed.manifestId} ` +
    `target=${String(dispatcher?.target ?? "?")} layer=${String(dispatcher?.layer ?? "?")} action=${String(dispatcher?.action ?? "?")} ` +
    `mapping=${String(mapping?.type ?? "?")}`;
}

function projectionDefinition(seed: SeedManifest): ProjectionDefinition {
  const mapping = byType(seed.topology, "projection_constructor_mapping");
  assertExists(mapping, `${describeLane(seed, mapping)} missing projection_constructor_mapping`);
  const definition = mapping.projection_definition;
  assert(definition && typeof definition === "object" && !Array.isArray(definition), `${describeLane(seed, mapping)} missing projection_definition`);
  return definition as ProjectionDefinition;
}

function seedData(seed: SeedManifest): Record<string, unknown> {
  const shape = byType(seed.topology, "screen_data_shape");
  assertExists(shape, `${describeLane(seed, shape)} missing screen_data_shape`);
  const rows = shape.initialDataRows;
  assert(Array.isArray(rows) && rows.length > 0, `${describeLane(seed, shape)} missing initialDataRows; do not fallback with seedData ?? null`);
  const first = rows[0] as { values?: unknown };
  assert(first.values && typeof first.values === "object" && !Array.isArray(first.values), `${describeLane(seed, shape)} seed row missing values`);
  return first.values as Record<string, unknown>;
}

function laneHarness(seed: SeedManifest, data: Record<string, unknown>, definition: ProjectionDefinition): UiProjection {
  const emission: Emission = {
    structureMapId: `seed:${seed.manifestId}`,
    packageId: definition.packageIds[0],
    schemaId: "seed-static-schema",
    componentIds: [],
    data,
    projectionDefinition: definition,
  };
  const result = projectionFromEmission(emission, emission.projectionDefinition!);
  assertExists(result.projection, `${describeLane(seed)} projectionFromEmission failed: ${result.error}`);

  const runtime = createProjectionRuntime();
  let projected: UiProjection | null = null;
  runtime.onProjectionUpdate((projection) => projected = projection);
  runtime.setProjectionDefinition(emission.projectionDefinition!);
  runtime.handleProjectionEvent(JSON.stringify({ manifest_id: seed.manifestId, data }));
  assertExists(projected, `${describeLane(seed)} SSE dispatcher/projection runtime failed to emit projection`);
  return projected!;
}

function targetSeedManifests(): SeedManifest[] {
  const manifests = seedFiles.flatMap(parseManifests);
  return manifests.filter((manifest) => {
    const dispatcher = byType(manifest.topology, "dispatcher_mapping");
    return dispatcher?.target === "demo" || (dispatcher?.target === "admin" && dispatcher?.layer === "manifest");
  });
}

Deno.test("projection lane seed contract: demo/admin manifest seeds carry dispatcher/runtime/projection/screen/notify mappings", () => {
  const manifests = targetSeedManifests();
  assert(manifests.length > 0, "lane=projection_seed_static no demo/admin manifest seeds parsed");
  for (const seed of manifests) {
    for (const type of ["dispatcher_mapping", "runtime_mapping", "projection_constructor_mapping", "screen_data_shape", "db_notify_projection_mapping"]) {
      assertExists(byType(seed.topology, type), `${describeLane(seed, { type })} missing ${type}`);
    }
  }
});

Deno.test("projection lane seed integration: seedData loops through lane harness to projection assertion", () => {
  for (const seed of targetSeedManifests()) {
    const data = seedData(seed) ?? explicitLiteralFixture;
    assertEquals(data, seedData(seed), `${describeLane(seed)} explicitLiteralFixture must not mask present seedData`);
    const projection = laneHarness(seed, data, projectionDefinition(seed));
    assertEquals(projection.kind, "form_inputs", describeLane(seed));
    if (projection.kind === "form_inputs") {
      const field = projection.fields.find((f) => f.key === "seedLabel");
      assertExists(field, `${describeLane(seed)} seedLabel field missing`);
      assertEquals(field.value, "projection-lane-seed", `${describeLane(seed)} seedData did not reach projection assertion`);
    }
  }
});

Deno.test("projection lane seed contract: screen_data_shape.tableRef has physical table and package wiring seed", () => {
  const allSql = seedFiles.map(readSeed).join("\n");
  for (const seed of targetSeedManifests()) {
    const shape = byType(seed.topology, "screen_data_shape");
    const tableRef = shape?.tableRef;
    assert(typeof tableRef === "string" && tableRef.length > 0, `${describeLane(seed, shape)} tableRef missing`);
    assert(allSql.includes(`VALUES ('${tableRef}'`), `${describeLane(seed, shape)} physical_tables seed missing tableRef=${tableRef}`);
    assert(allSql.includes(`WHERE table_ref = '${tableRef}'`), `${describeLane(seed, shape)} wiring_physical_to_package seed missing tableRef=${tableRef}`);
  }
});

Deno.test("projection lane collapse: backend SSE identity re-read reaches frontend projectionRuntime and user-facing render", async () => {
  const seed = targetSeedManifests().find((m) => m.manifestId === "00000000-0000-0000-0000-000000000085");
  assertExists(seed, "lane=projection_seed_collapse seed=db/demo_seed.sql manifest=00000000-0000-0000-0000-000000000085 missing seed manifest");
  const data = seedData(seed);
  const definition = projectionDefinition(seed);
  const backendEmission: Emission = {
    structureMapId: "seed-projection-lane-structure-map",
    packageId: definition.packageIds[0],
    schemaId: "seed-static-schema",
    componentIds: [],
    data: {
      kind: "screen_data_shape_query_result",
      manifestId: seed.manifestId,
      rows: [data],
      aggregationResults: [],
      activeColumns: ["seedLabel"],
      displayColumnMode: "all",
    },
    projectionDefinition: definition,
  };
  const backendSsePayload = JSON.stringify({
    table_id: "demo:screen_list:read",
    table_registry_id: "demo",
    manifest_id: seed.manifestId,
  });

  const runtime = createProjectionRuntime({ definitionMissingPolicy: "ignore" });
  const projectedBox: { value: UiProjection | null; payload: Record<string, unknown> | null } = { value: null, payload: null };
  runtime.onProjectionUpdate((projection, payload) => {
    projectedBox.value = projection;
    projectedBox.payload = payload as unknown as Record<string, unknown>;
  });

  const dispatcher = createSseDispatcher({ unhandledEventPolicy: "ignore" });
  const errors: string[] = [];
  let reReadPromise: Promise<void> | null = null;
  const reReadHandler = createProjectionLaneReReadHandler({
    projectionRuntime: runtime,
    readEmission: async (identity) => {
      assertEquals(identity.manifestId, seed.manifestId, `${describeLane(seed)} sse_path=frontend_re_read manifest identity mismatch`);
      assertEquals(identity.tableId, "demo:screen_list:read", `${describeLane(seed)} sse_path=frontend_re_read table identity mismatch`);
      return backendEmission;
    },
    onError: (message) => errors.push(message),
  });
  dispatcher.register("projection", (raw) => {
    reReadPromise = reReadHandler(raw);
  });

  runtime.handleProjectionEvent(backendSsePayload);
  assertEquals(projectedBox.value, null, `${describeLane(seed)} identity-only backend SSE payload must not be treated as seedData projection`);

  dispatcher.route("projection", backendSsePayload);
  await reReadPromise;

  assertEquals(errors, [], `${describeLane(seed)} frontend re-read errors: ${errors.join("; ")}`);
  assertExists(projectedBox.value, `${describeLane(seed)} sse_path=backend_sse->sseDispatcher->reRead->projectionRuntime did not project`);
  assertEquals(projectedBox.payload?.manifest_id, seed.manifestId, `${describeLane(seed)} projected payload manifest mismatch`);
  assertEquals(projectedBox.payload?.table_id, "demo:screen_list:read", `${describeLane(seed)} projected payload table mismatch`);
  assertEquals(projectedBox.value.kind, "form_inputs", describeLane(seed));
  if (projectedBox.value.kind === "form_inputs") {
    const field = projectedBox.value.fields.find((f) => f.key === "seedLabel");
    assertExists(field, `${describeLane(seed)} seedLabel field missing after frontend re-read`);
    assertEquals(field.value, "projection-lane-seed", `${describeLane(seed)} seedLabel did not reach frontend projectionRuntime`);
  }

  const html = renderToString(h(ProjectionView, { emission: backendEmission }));
  assert(
    html.includes("projection-lane-seed"),
    `${describeLane(seed)} user-facing ProjectionView must render seedLabel from backend ScreenDataShapeQueryRuntime emission.Data`,
  );
});

Deno.test("projection lane NG: missing seedData is explicit and never seedData ?? null silent fallback", () => {
  const seed = targetSeedManifests()[0];
  const broken: SeedManifest = { ...seed, topology: seed.topology.filter((entry) => entry.type !== "screen_data_shape") };
  let failed = false;
  try {
    seedData(broken);
  } catch (error) {
    failed = String(error).includes("missing screen_data_shape");
  }
  assertEquals(failed, true, `${describeLane(seed)} missing seedData must be explicit NG failure`);
});
