/**
 * mockPresetApi.ts — Frontend API for Mock Preset Intake Compiler.
 * SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml
 *
 * All operations go through AdminRuntime via admin dispatch.
 * Frontend does NOT write to topology.mock_preset_* directly.
 */

import type { DispatchRequest } from "./dispatch.ts";
import { queueAdminClientCommand } from "../runtime/frontendScheduler.ts";
import { SESSION_TOKEN_KEY } from "../lib/demoSession.ts";

function getToken(): string | undefined {
  if (typeof globalThis.sessionStorage === "undefined") return undefined;
  return sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined;
}

async function dispatchMockPreset(
  action: string,
  options: { payload?: Record<string, unknown>; idOrHubId?: string } = {},
): Promise<unknown> {
  const request: Omit<DispatchRequest, "triggerKind"> = {
    operationType: "admin",
    target: "admin",
    layer: "mock_preset",
    action,
    payload: options.payload,
    idOrHubId: options.idOrHubId,
  };
  const result = await queueAdminClientCommand(request, getToken());
  if (!result.success) {
    const code = result.errors?.[0]?.code ?? result.errors?.[0]?.Code ?? "DISPATCH_FAILED";
    const msg = result.errors?.[0]?.message ?? result.errors?.[0]?.Message ?? "dispatch failed";
    throw new Error(`[${code}] ${msg}`);
  }
  if (!result.emission) throw new Error("dispatch: no emission in response");
  return result.emission.data;
}

// ─── types ────────────────────────────────────────────────────────────────────

export type MockPresetListItem = {
  presetId: string;
  presetKey: string;
  presetLabel: string;
  sourceKind: string;
  status: string;
  createdAt: string;
};

export type MockPresetCreateResult = {
  ok: boolean;
  presetId?: string;
  presetKey?: string;
  errorCode?: string;
  message?: string;
};

export type MockPresetBindResult = {
  ok: boolean;
  layoutPatchJson?: unknown;
  packageMembershipCandidateJson?: unknown;
  wiringCandidateJson?: unknown;
  styleCandidateJson?: unknown;
  unresolvedJson?: unknown;
  errorCode?: string;
  message?: string;
};

export type MockPresetCompileResult = {
  ok: boolean;
  compileSnapshotId?: string;
  layoutPatchJson?: unknown;
  unresolvedJson?: unknown;
  errorCode?: string;
};

// ─── API functions ────────────────────────────────────────────────────────────

/**
 * Create a new preset from a source snapshot and visual tree.
 * source_kind: svg_xml | generic_xml | figma_json | ui_builder_canvas
 */
export async function createMockPreset(params: {
  presetKey: string;
  presetLabel: string;
  sourceKind: string;
  sourceHash: string;
  sourceSnapshotJson: unknown;
  visualTreeJson: unknown;
}): Promise<MockPresetCreateResult> {
  return dispatchMockPreset("create", { payload: params as Record<string, unknown> }) as Promise<MockPresetCreateResult>;
}

/**
 * List active presets from topology.mock_preset_registry.
 */
export async function listMockPresets(status = "active"): Promise<MockPresetListItem[]> {
  const result = await dispatchMockPreset("list", { payload: { status } });
  return (result as MockPresetListItem[]) ?? [];
}

/**
 * Get detail for a single preset including object mappings and wiring candidates.
 */
export async function getMockPreset(presetId: string): Promise<unknown> {
  return dispatchMockPreset("get", { idOrHubId: presetId });
}

/**
 * Compile a preset from its object mappings → layout_patch_json.
 * Deterministic; no AI inference.
 */
export async function compileMockPreset(presetId: string): Promise<MockPresetCompileResult> {
  return dispatchMockPreset("compile", { idOrHubId: presetId }) as Promise<MockPresetCompileResult>;
}

/**
 * Bind compiled preset to selected route package tmp canvas draft.
 * Returns layout_patch_json as a data payload only.
 * Does NOT write to topology.ui_topology_tensor or canonical topology tables.
 * SSOT: mock-preset-intake-compiler-ssot.yaml §bind_to_canvas_contract
 */
export async function bindMockPreset(params: {
  presetId: string;
  routeKey: string;
}): Promise<MockPresetBindResult> {
  return dispatchMockPreset("bind", { payload: params as Record<string, unknown> }) as Promise<MockPresetBindResult>;
}
