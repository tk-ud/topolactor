import { assert, assertEquals } from "https://deno.land/std@0.208.0/assert/mod.ts";
import { parse } from "https://deno.land/std@0.208.0/yaml/mod.ts";

const CONTEXT_ROUTE_SSOT = new URL(
  "../../docs/design/context-route-recommendation.yaml",
  import.meta.url,
);
const SQL_ATTENTION_SSOT = new URL(
  "../../docs/design/sql-attention-logs-ssot.yaml",
  import.meta.url,
);
const ENUM_SSOT = new URL(
  "../../docs/design/enum-dictionary-ssot.yaml",
  import.meta.url,
);
const DB_SCHEMA_SSOT = new URL(
  "../../docs/design/db-schema.yaml",
  import.meta.url,
);

const HUB_LOCAL_LANES = ["ui_pressure", "state_pressure"] as const;
const SQL_ATTENTION_LANE = "sql_attention_projection";

type LaneBlock = {
  lane_id?: string;
  sources?: string[];
  outputs?: string[];
};

type HubLocalLanes = {
  canonical_reference?: string[];
  lanes?: Record<string, LaneBlock>;
  result_contract?: {
    hub_local_recommendation_result?: {
      lane_values?: string[];
      prohibited_lane_values?: string[];
    };
  };
  transition_stats_separation?: {
    context_transition_stats?: { transition_kind?: string };
    context_enum_transition_stats?: { transition_kind?: string };
  };
};

type SqlAttentionBoundary = {
  lane?: string;
  context_route_recommendation_relation?: { hub_local_lanes?: string[] };
  boundaries?: { prohibited?: string[] };
};

async function loadYaml(path: URL): Promise<Record<string, unknown>> {
  const raw = await Deno.readTextFile(path);
  const doc = parse(raw) as Record<string, unknown>;
  assert(doc !== null && typeof doc === "object");
  return doc;
}

Deno.test("recommendation pressure lanes: hub-local lanes are distinct from sql_attention_projection", async () => {
  const ctx = await loadYaml(CONTEXT_ROUTE_SSOT);
  const sql = await loadYaml(SQL_ATTENTION_SSOT);

  const hub = ctx.hub_local_recommendation_pressure_lanes as HubLocalLanes | undefined;
  assert(hub?.lanes, "hub_local_recommendation_pressure_lanes.lanes required");

  for (const laneId of HUB_LOCAL_LANES) {
    assertEquals(hub.lanes?.[laneId]?.lane_id, laneId);
  }

  const laneValues = hub.result_contract?.hub_local_recommendation_result?.lane_values ?? [];
  for (const laneId of HUB_LOCAL_LANES) {
    assert(laneValues.includes(laneId), `result lane_values must include ${laneId}`);
  }

  const prohibited = hub.result_contract?.hub_local_recommendation_result
    ?.prohibited_lane_values ?? [];
  assert(
    prohibited.includes(SQL_ATTENTION_LANE),
    "hub-local results must prohibit sql_attention_projection lane",
  );

  const rpl = sql.recommendation_pressure_lane_boundary as SqlAttentionBoundary | undefined;
  assertEquals(rpl?.lane, SQL_ATTENTION_LANE);

  const sqlHubLanes = rpl?.context_route_recommendation_relation?.hub_local_lanes ?? [];
  for (const laneId of HUB_LOCAL_LANES) {
    assert(sqlHubLanes.includes(laneId));
  }

  assertEquals(
    hub.transition_stats_separation?.context_transition_stats?.transition_kind,
    "operation",
  );
  assertEquals(
    hub.transition_stats_separation?.context_enum_transition_stats?.transition_kind,
    "enum_item",
  );
});

Deno.test("recommendation pressure lanes: ui_pressure and state_pressure sources/outputs are SSOT-fixed", async () => {
  const ctx = await loadYaml(CONTEXT_ROUTE_SSOT);
  const hub = ctx.hub_local_recommendation_pressure_lanes as HubLocalLanes;

  const ui = hub.lanes?.ui_pressure;
  assert(ui?.sources?.includes("context_event"));
  assert(ui?.sources?.includes("component_operation_event_log"));
  assert(ui?.outputs?.includes("next_operation"));
  assert(ui?.outputs?.includes("next_component"));
  assert(ui?.outputs?.includes("next_route_action"));

  const state = hub.lanes?.state_pressure;
  assert(state?.sources?.includes("logs.diff"));
  assert(state?.outputs?.includes("next_enum_item"));
  assert(state?.outputs?.includes("likely_status"));
  assert(state?.outputs?.includes("state_shift_candidate"));
});

Deno.test("recommendation pressure lanes: enum linear space contract is referenced from state_pressure", async () => {
  const ctx = await loadYaml(CONTEXT_ROUTE_SSOT);
  const enumDoc = await loadYaml(ENUM_SSOT);
  const hub = ctx.hub_local_recommendation_pressure_lanes as HubLocalLanes;

  const enumSsot = enumDoc.enum_dictionary_ssot as Record<string, unknown> | undefined;
  assert(
    enumSsot?.enum_group_linear_space_coordinate_contract,
    "enum_group_linear_space_coordinate_contract required",
  );

  const stateRefs = hub.lanes?.state_pressure as LaneBlock & {
    enum_linear_space?: { canonical_reference?: string[] };
  };
  const refs = stateRefs?.enum_linear_space?.canonical_reference ?? [];
  assert(
    refs.some((r) => r.includes("enum-dictionary-ssot")),
    "state_pressure must reference enum-dictionary-ssot",
  );

  const consumers = (enumSsot.enum_group_linear_space_coordinate_contract as {
    consumers?: string[];
  })?.consumers ?? [];
  assert(
    consumers.some((c) => c.includes("context-route-recommendation")),
    "enum linear space must list context-route-recommendation consumer",
  );
});

Deno.test("recommendation pressure lanes: cross-SSOT references and injection boundaries", async () => {
  const ctx = await loadYaml(CONTEXT_ROUTE_SSOT);
  const sql = await loadYaml(SQL_ATTENTION_SSOT);

  const hub = ctx.hub_local_recommendation_pressure_lanes as HubLocalLanes;
  assert(
    (hub.canonical_reference ?? []).some((p) => p.includes("sql-attention-logs-ssot")),
    "hub_local lanes must reference sql-attention-logs-ssot",
  );

  const rpl = sql.recommendation_pressure_lane_boundary as SqlAttentionBoundary & {
    canonical_reference?: string[];
  };
  assert(
    (rpl.canonical_reference ?? []).some((p) => p.includes("context-route-recommendation")),
    "sql_attention lane boundary must reference context-route-recommendation",
  );

  const prohibited = rpl.boundaries?.prohibited ?? [];
  assert(
    prohibited.some((p) => p.includes("hub_local") || p.includes("inject")),
    "sql_attention prohibited must guard hub_local injection",
  );

  const registryBoundaries = (
    (ctx.registry_tensor_principle as { sql_attention_relation?: { boundaries?: string[] } })
      ?.sql_attention_relation?.boundaries ?? []
  );
  assert(
    registryBoundaries.some((b) => b.includes("inject")),
    "registry_tensor sql_attention_relation.boundaries must guard injection",
  );
});

Deno.test("recommendation pressure lanes: db-schema tables and guardrails", async () => {
  const dbDoc = await loadYaml(DB_SCHEMA_SSOT);
  const db = dbDoc.db_schema as {
    tables?: Record<string, {
      transition_kind?: string;
      recommendation_lane?: string;
    }>;
    meaning_collision_guardrails?: unknown[];
  } | undefined;
  assert(db, "db-schema.db_schema root required");
  const tables = db.tables;
  assert(tables, "db-schema.db_schema.tables required");

  for (const name of [
    "context_transition_stats",
    "context_enum_transition_event",
    "context_enum_transition_stats",
    "component_operation_event_log",
  ]) {
    assert(tables[name], `db-schema.tables missing ${name}`);
  }

  assertEquals(tables.context_transition_stats?.transition_kind, "operation");
  assertEquals(tables.context_enum_transition_stats?.transition_kind, "enum_item");
  assertEquals(tables.context_transition_stats?.recommendation_lane, "ui_pressure");
  assertEquals(tables.context_enum_transition_stats?.recommendation_lane, "state_pressure");

  const guardrails = db.meaning_collision_guardrails ?? [];
  const hasGuardrailKey = (needle: string) =>
    guardrails.some((entry) => {
      if (typeof entry === "string") return entry.includes(needle);
      if (entry && typeof entry === "object") {
        return Object.keys(entry as Record<string, unknown>).some((k) => k.includes(needle));
      }
      return false;
    });
  assert(hasGuardrailKey("recommendation_lane_boundary"));
  assert(hasGuardrailKey("context_transition_stats_vs_enum"));
});
