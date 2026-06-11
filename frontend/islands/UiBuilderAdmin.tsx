import { useCallback, useEffect, useMemo, useRef, useState } from "preact/hooks";
import { JSX } from "preact";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import { PresetUploaderDrawer, type CanvasPresetSeed } from "./PresetUploaderDrawer.tsx";
import {
  bindMockPreset,
  listMockPresets,
  type MockPresetListItem,
} from "../api/mockPresetApi.ts";
import { computeSourceHash } from "../runtime/visualMockParser.ts";
import {
  buildInlineStyleFromCssTokenRefs,
  CSS_DICTIONARY_TOKENS,
  resolveCssTokenValue,
} from "../runtime/cssDictionary.ts";
import { TOPOLOGY_LAYOUT_CLASS_DICTIONARY } from "../runtime/topologyLayoutClassDictionary.ts";
import { resolveTopologyLayoutClassRefs } from "../runtime/topologyLayoutClassResolver.ts";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ADMIN_UI_BUILDER_GUIDE } from "../content/adminGuides.ts";
import UiBuilderFlowStepper, {
  type UiBuilderFlowStepId,
} from "../components/UiBuilderFlowStepper.tsx";
import {
  UX_COMPONENT_ADD_PANEL_LABEL,
  UX_COMPONENT_BUCKET_CARD_DRAG_HINT,
  UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION,
  UX_DASHBOARD_PRESET_CANDIDATE_LABEL,
  UX_DESIGN_EDITOR_SURFACE,
  UX_DESIGN_INSPECTOR_SECTION,
  UX_DESIGN_NODE_SAVE_LABEL,
  UX_EMPTY_CANVAS_DRAG_GUIDANCE,
  UX_LAYOUT_EDITOR_SURFACE,
  UX_LAYOUT_INSPECTOR_SECTION,
  UX_ROUTE_KEY_REQUIRED_FOR_CANVAS,
  UX_ROUTE_NAVIGATION_NONE_LABEL,
  UX_ROUTE_NAVIGATION_PRESET_LABEL,
  UX_ROUTE_NAVIGATION_ROUTE_SELECT_LABEL,
  UX_ROUTE_NAVIGATION_SAVE_LABEL,
} from "../content/adminUxTerms.ts";
import {
  buildVisualLayoutPatchJson,
  enrichDraftNodesWithPaletteComponentIds,
  filterEmptyResponsiveRules,
  type LayoutDimension,
  type LayoutNodeKind,
  layoutDimensionLabel,
  isLegacyAbsoluteLayoutPatch,
  makeStructuralHtmlNode,
  migrateAbsolutePatchToFlowStack,
  parseLayoutDimensionInput,
  type PaletteDraftSeedEntry,
  parseVisualLayoutPatchJson,
  reorderLayoutNodeStack,
  resolveSizingModeAfterToggle,
  RESPONSIVE_BREAKPOINTS,
  type ResponsiveTokenRules,
  type SizingMode,
  isPaletteAutoSeedCanvas,
  seedDraftNodesFromPalette,
  snapToGrid,
  STRUCTURAL_HTML_TAG_ALLOWLIST,
  type StructuralHtmlTag,
  validateResponsiveTokenRulesJson,
  wouldCreateVisualParentCycle,
} from "../runtime/visualLayoutUtils.ts";
import { resolveCanvasRootPreviewClassName } from "../runtime/layoutClassPreviewUtils.ts";
import {
  FlowLayoutCanvas,
  type FlowCanvasDesignDraft,
} from "../components/FlowLayoutCanvas.tsx";
import { lookupTopologyLayoutClassKey } from "../runtime/topologyLayoutClassResolver.ts";
import {
  LayoutPatchApplyModal,
  type LayoutPatchApplyModalPhase,
} from "../components/LayoutPatchApplyModal.tsx";
import { type BucketItem } from "../runtime/bucketUtils.ts";
import { PACKAGE_WIRING_TARGET_SURFACES } from "../lib/packageWiringOptions.ts";
import {
  buildWiringKindSelectOptions,
  encodeManifestPackageTargetRef,
  encodeRouteNavigationTargetRef,
  isRouteNavigationTargetRef,
  manifestIdFromTargetRef,
  type ManifestPickerOption,
  manifestWiringKeyFromTargetRef,
  mergeManifestPickerOptions,
  parseRouteNavigationTargetRef,
} from "../lib/packageWiringPicker.ts";
import {
  buildScreenReadQueryWiringCandidates,
  type ScreenReadQueryWiringCandidate,
} from "../lib/screenReadQueryWiring.ts";
import { getAdminManifest, listAdminManifests } from "../api/adminApi.ts";
import { getStoredScreenLabel } from "../runtime/screenAuthoringIntent.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";
import { resolveVisibleTopologyName, topologySystemNameToUiBuilderKey, isValidTopologySystemName } from "../lib/topologySystemName.ts";
import { useConfirm } from "../hooks/useConfirm.tsx";
import {
  enrichLayoutPreviewNodes,
  getLayoutPreviewDefaultSize,
  resolveComponentKindForLayoutPreview,
} from "../runtime/layoutComponentPreview.ts";
import {
  COMPONENT_ARRAY_PROP_CAPABILITIES,
  ALLOWED_PROP_BINDING_TRANSFORMS,
  validatePropBindingsStructure,
} from "../runtime/propBindingResolver.ts";
import {
  evaluateAllCalcBindings,
  validateCalcBinding,
  type CalcBinding,
  type CalcOperation,
  type CalcVariableSource,
  type RoundingPolicy,
} from "../runtime/frontendLocalCalculationResolver.ts";

/**
 * /admin/ui-builder — UI コンポーネントシステム & レイアウトビルダー v2。
 *
 * Issue #86: プリミティブコンポーネントシステム + UI topology DB 登録。
 * Issue #89: admin ビジュアルレイアウトビルダー v2 — Figma-like mouse-driven canvas。
 *
 * Frontend authority: draft layout state, mouse interaction, visual preview,
 *   local readiness display, intent submission, backend result display.
 * Forbidden: direct DB write, backend validate bypass, topology judgment,
 *   promotion judgment, draft-only node apply allow.
 * SSOT: docs/design/admin-console-workflow-ssot.yaml (step 4 package-only edit route)
 */

import { queueAdminClientCommand } from "../runtime/frontendScheduler.ts";

/** Canvas workspace contract marker (SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract). */
export const UI_BUILDER_WORKSPACE_MODE = "canvas_workspace_v2" as const;
/** No separate layout/design/visual tabs — single unified workspace. */
export const UI_BUILDER_HAS_SEPARATE_TABS = false as const;
/** Palette panels are left docked inside the canvas workspace row, not a strip above. */
export const UI_BUILDER_LEFT_PANEL_DOCKED = true as const;
/** Design save action is positioned above the inspector tabs (not tab-specific). */
export const UI_BUILDER_DESIGN_SAVE_ABOVE_TABS = true as const;
/** Drawer shells are display-density chrome inside the same canvas workspace. */
export const UI_BUILDER_DRAWER_SHELL_KIND = "same_workspace_display_shell" as const;
/** Drawer open/closed flags are frontend-local UI state only, never persisted. */
export const UI_BUILDER_DRAWER_STATE_BOUNDARY = "frontend_local_state_only" as const;

const SESSION_TOKEN_KEY = "demo_jwt_token";

// ─── ユーティリティ ──────────────────────────────────────────────────────────

// deno-lint-ignore no-explicit-any
async function dispatchAdminOp(
  layer: string,
  action: string,
  payload?: unknown,
): Promise<any> {
  const token = typeof globalThis.sessionStorage !== "undefined"
    ? sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined
    : undefined;
  return queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer,
    action,
    payload: payload != null ? payload as Record<string, unknown> : undefined,
  }, token);
}

// ─── 型定義 ──────────────────────────────────────────────────────────────────

type ValidationError = {
  code: string;
  message: string;
  field?: string;
  nodeId?: string;
  componentKey?: string;
};

type DraftNode = {
  nodeId: string;
  componentKey: string;
  /** Resolved from palette/catalog for live canvas preview. */
  componentKind?: string;
  nodeKind?: LayoutNodeKind;
  htmlTag?: StructuralHtmlTag;
  layoutClassRefs?: string[];
  isDraftOnly: boolean;
  slotKey: string;
  orderIndex: number;
  parentNodeId: string | null;
  gridCol: number;
  gridRow: number;
  // v2: visual canvas position/size (px or % for width/height)
  x: number;
  y: number;
  width: LayoutDimension;
  height: LayoutDimension;
  widthMode?: SizingMode;
  heightMode?: SizingMode;
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
  /** Serialized component props override (JSON string). Flowed through layout_patch_json → backend → renderEmission. SSOT: layout_patch_json */
  propsJson?: string;
  /** Serialized component state override (JSON string). open:boolean for disclosure/drawer — merged into props.data at render time. SSOT: layout_patch_json */
  stateJson?: string;
  /**
   * Array prop bindings: maps component prop name → runtime data path binding.
   * Resolved from emission.data.* after propsJson/stateJson at render time.
   * Only valid for components with COMPONENT_ARRAY_PROP_CAPABILITIES entries.
   * SSOT: admin-console-workflow-ssot.yaml layout_node_props_contract.propBindings
   */
  propBindings?: Record<string, PropBindingDraft>;
};

/** Draft-time prop binding descriptor stored in DraftNode. */
type PropBindingDraft = {
  source: string;
  keyPath?: string;
  labelPath?: string;
  valuePath?: string;
  childrenPath?: string;
  transform?: string;
};

type DesignDraft = FlowCanvasDesignDraft;

type BucketCardDragPayload = {
  componentKey: string;
  componentKind: string;
  statusLabel: string;
  isDraftOnly: boolean;
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
  routeKey?: string;
};

const BUCKET_CARD_DRAG_MIME = "application/x-topolactor-bucket-card";

function paletteEntryFromDragPayload(payload: BucketCardDragPayload): PaletteEntry {
  return {
    componentKey: payload.componentKey,
    componentKind: payload.componentKind,
    isDraftOnly: payload.isDraftOnly,
    componentId: payload.componentId,
    packageId: payload.packageId,
    layoutId: payload.layoutId,
    wiringId: payload.wiringId,
    tensorId: payload.tensorId,
    routeKey: payload.routeKey,
  };
}

function bucketCardDragPayloadFromEntry(
  entry: PaletteEntry,
  statusLabel: string,
): BucketCardDragPayload {
  return {
    componentKey: entry.componentKey,
    componentKind: entry.componentKind,
    statusLabel,
    isDraftOnly: entry.isDraftOnly,
    componentId: entry.componentId,
    packageId: entry.packageId,
    layoutId: entry.layoutId,
    wiringId: entry.wiringId,
    tensorId: entry.tensorId,
    routeKey: entry.routeKey,
  };
}

function writeBucketCardDragData(e: DragEvent, payload: BucketCardDragPayload): void {
  e.dataTransfer?.setData(BUCKET_CARD_DRAG_MIME, JSON.stringify(payload));
  e.dataTransfer?.setData("text/plain", payload.componentKey);
  if (e.dataTransfer) e.dataTransfer.effectAllowed = "copy";
}

function readBucketCardDragPayload(e: DragEvent): BucketCardDragPayload | null {
  const raw = e.dataTransfer?.getData(BUCKET_CARD_DRAG_MIME);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as BucketCardDragPayload;
  } catch {
    return null;
  }
}

type DragSrc =
  | { kind: "palette"; entry: PaletteEntry }
  | { kind: "canvas"; nodeId: string };

type PromotedPaletteEntry = {
  componentKey: string;
  componentKind: string;
  componentId: string;
  packageId: string;
  layoutId: string;
  wiringId: string;
  tensorId: string;
  routeKey: string;
};

type PaletteEntry = {
  componentKey: string;
  componentKind: string;
  isDraftOnly: boolean;
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
  routeKey?: string;
};

/** Canvas workspace panel actions (replaces old tab navigation). */
// Gap 1: Lifecycle state machine
type LifecyclePhase =
  | "idle"
  | "previewing"
  | "previewed"
  | "validating"
  | "validated"
  | "applying"
  | "applied_ok"
  | "applied_fail"
  | "persisted";

// Gap 2: History snapshot for undo/redo
type HistorySnapshot = { nodes: DraftNode[]; label: string };

// ─── アコーディオン ──────────────────────────────────────────────────────────

// deno-lint-ignore no-explicit-any
function Accordion({
  title,
  defaultOpen = false,
  children,
}: {
  title: string;
  defaultOpen?: boolean;
  // preact children: typed as any to remain compatible without explicit ComponentChildren import
  // deno-lint-ignore no-explicit-any
  children?: any;
}): JSX.Element {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div class="accordion">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        class={open ? "accordion-trigger-open" : "accordion-trigger-closed"}
      >
        <span>{title}</span>
        <span class="text-muted-xs">{open ? "▲ 閉じる" : "▼ 開く"}</span>
      </button>
      {open && (
        <div class="accordion-body">
          {children}
        </div>
      )}
    </div>
  );
}

type InspectorTabDef = {
  id: string;
  label: string;
  content: JSX.Element;
};

/** Compact tab strip for accordion-nested inspectors (limits vertical scroll per section). */
function InspectorTabPanel({
  tabs,
  defaultTabId,
  panelMaxHeight = "min(320px, 42vh)",
  ariaLabel,
}: {
  tabs: InspectorTabDef[];
  defaultTabId?: string;
  panelMaxHeight?: string;
  ariaLabel?: string;
}): JSX.Element | null {
  const firstId = tabs[0]?.id ?? "";
  const [activeId, setActiveId] = useState(defaultTabId ?? firstId);
  const active = tabs.find((t) => t.id === activeId) ?? tabs[0];
  if (!active || tabs.length === 0) return null;

  return (
    <div aria-label={ariaLabel}>
      <div
        class="mb-2 flex flex-wrap gap-0.5 border-b border-slate-200"
        role="tablist"
        aria-label={ariaLabel ? `${ariaLabel} タブ` : "インスペクタタブ"}
      >
        {tabs.map((tab) => {
          const selected = activeId === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              role="tab"
              id={`inspector-tab-${tab.id}`}
              aria-selected={selected}
              aria-controls={`inspector-panel-${tab.id}`}
              class={`rounded-t px-2 py-1 text-[0.65rem] font-semibold transition-colors ${
                selected
                  ? "border border-b-0 border-slate-200 bg-white text-blue-700"
                  : "border border-transparent bg-slate-50 text-slate-600 hover:bg-slate-100 hover:text-slate-800"
              }`}
              onClick={() => setActiveId(tab.id)}
            >
              {tab.label}
            </button>
          );
        })}
      </div>
      <div
        id={`inspector-panel-${active.id}`}
        role="tabpanel"
        aria-labelledby={`inspector-tab-${active.id}`}
        class="overflow-y-auto rounded-b border border-t-0 border-slate-200 bg-white p-2"
        style={{ maxHeight: panelMaxHeight }}
      >
        {active.content}
      </div>
    </div>
  );
}

// ─── ステータスバッジ ─────────────────────────────────────────────────────────

function StatusBadge(
  { text, variant }: {
    text: string;
    variant: "ok" | "warn" | "error" | "info";
  },
): JSX.Element {
  const cls = {
    ok: "badge-ok",
    warn: "badge-warn",
    error: "badge-error",
    info: "badge-info",
  }[variant];
  return <span class={cls}>{text}</span>;
}

const LAYOUT_RIGHT_DOCK_WIDTH = "clamp(320px, 24vw, 420px)";

const COMPONENT_BUCKET_KIND_ICONS: Record<string, string> = {
  primitive: "◆",
  display: "▣",
  action: "▶",
  form: "☰",
  layout: "▦",
  data_display: "▤",
};


/** Component event wiring entry (saved to propsJson.eventWirings — layout_patch_json boundary). */
export type ComponentEventWiring = {
  eventType: string;
  actionType: string;
  targetStateKey?: string;
};

export const COMPONENT_EVENT_TYPES = [
  "onClick",
  "onChange",
  "onSubmit",
  "onOpen",
  "onClose",
  "onSelect",
  "onInput",
  "onFocus",
  "onBlur",
] as const;

export const COMPONENT_ACTION_TYPES = [
  "setState",
  "navigate",
  "openModal",
  "closeModal",
  "openDrawer",
  "closeDrawer",
  "submitForm",
  "clearForm",
  "triggerAction",
] as const;

function bucketKindIcon(componentKind: string): string {
  const base = componentKind.split("/")[0]?.split(".")[0] ?? componentKind;
  return COMPONENT_BUCKET_KIND_ICONS[base] ?? "◈";
}

function ComponentBucketCard({
  componentKey,
  componentKind,
  sourcePath,
  statusLabel,
  statusVariant,
  selected = false,
  selectable = false,
  draggable = false,
  placementReady = true,
  onSelect,
  onDragStart,
  dragPayload,
  onAddToCanvas,
}: {
  componentKey: string;
  componentKind: string;
  sourcePath?: string;
  statusLabel: string;
  statusVariant: "ok" | "warn" | "error" | "info";
  selected?: boolean;
  selectable?: boolean;
  draggable?: boolean;
  placementReady?: boolean;
  onSelect?: () => void;
  onDragStart?: (e: DragEvent, payload: BucketCardDragPayload) => void;
  dragPayload?: BucketCardDragPayload;
  onAddToCanvas?: () => void;
}): JSX.Element {
  const blocked = !placementReady;
  const cardClass = blocked
    ? "border-amber-300 bg-amber-50"
    : selected
    ? "border-blue-500 bg-blue-50 ring-2 ring-blue-300"
    : "border-slate-200 bg-white hover:border-blue-300";

  return (
    <div
      class={`component-bucket-card flex flex-col rounded-lg border p-2 text-left shadow-sm transition-colors ${cardClass}`}
      role={selectable ? "option" : "listitem"}
      aria-selected={selectable ? selected : undefined}
      data-component-key={componentKey}
      data-placement-ready={placementReady ? "true" : "false"}
    >
      <div
        draggable={draggable && placementReady}
        onDragStart={(e: DragEvent) => {
          if (!draggable || !placementReady || !onDragStart || !dragPayload) return;
          writeBucketCardDragData(e, dragPayload);
          onDragStart(e, dragPayload);
        }}
        class={`component-bucket-card__body flex gap-2 ${draggable && placementReady ? "cursor-grab" : ""}`}
      >
        <div
          class="component-bucket-card__icon flex h-9 w-9 shrink-0 items-center justify-center rounded-md border border-slate-200 bg-slate-50 text-base text-slate-700"
          aria-hidden="true"
          title={componentKind}
        >
          {bucketKindIcon(componentKind)}
        </div>
        <div class="min-w-0 flex-1">
          <div class="truncate text-xs font-semibold text-slate-900" title={componentKey}>
            {friendlyComponentLabel(componentKey)}
          </div>
          <div class="truncate font-mono text-[0.6rem] text-slate-500" title={componentKey}>
            {componentKey}
          </div>
          <div class="mt-0.5 truncate text-[0.6rem] text-slate-600">{componentKind}</div>
          {sourcePath && (
            <div
              class="component-bucket-card__source-path mt-0.5 truncate font-mono text-[0.55rem] text-slate-500"
              title={sourcePath}
            >
              {sourcePath}
            </div>
          )}
          <div class="mt-1">
            <StatusBadge text={statusLabel} variant={statusVariant} />
          </div>
        </div>
      </div>

      {selectable && (
        <button
          type="button"
          onClick={onSelect}
          class="component-bucket-card__select mt-2 w-full rounded border border-slate-200 bg-slate-50 px-1 py-0.5 text-[0.62rem] font-medium text-slate-700 hover:bg-slate-100"
          aria-pressed={selected}
        >
          {selected ? "選択中" : "選択"}
        </button>
      )}

      {draggable && (
        <p class="component-bucket-card__drag-hint mt-1 text-[0.58rem] text-slate-500">
          {placementReady
            ? UX_COMPONENT_BUCKET_CARD_DRAG_HINT
            : "先にパッケージ化・配置可能化が必要です"}
        </p>
      )}

      {onAddToCanvas && (
        <button
          type="button"
          onClick={onAddToCanvas}
          disabled={blocked}
          class="component-bucket-card__add mt-1 w-full rounded border border-blue-200 bg-blue-50 px-1 py-0.5 text-[0.62rem] font-medium text-blue-800 hover:bg-blue-100 disabled:opacity-40"
        >
          + キャンバスに追加
        </button>
      )}

      {sourcePath && (
        <details class="mt-1">
          <summary class="cursor-pointer text-[0.55rem] text-gray-400">技術詳細</summary>
          <code class="block break-all text-[0.55rem] text-gray-500">{sourcePath}</code>
        </details>
      )}
    </div>
  );
}

function LayoutRightDock({
  draftNodes,
  selectedNodeId,
  selectedNode,
  packageId,
  onSelectNode,
  onReparent,
  onCopy,
  onDelete,
  slotKeyCandidates,
  onUpdateNode,
  onCommitNode,
  onToggleLayoutClassRef,
  onDesignChange,
  routeCandidates,
  canvasDesignDraft,
  calculationBindings,
  draftNodeIds,
  calcResults,
  onCalcBindingsChange,
  emissionDataJson,
  onEmissionDataJsonChange,
}: {
  draftNodes: DraftNode[];
  selectedNodeId: string | null;
  selectedNode: DraftNode | null;
  packageId: string;
  canvasDesignDraft?: DesignDraft;
  onSelectNode: (id: string | null) => void;
  onReparent: (nodeId: string, newParentId: string | null, insertBeforeId: string | null) => void;
  onCopy: (id: string) => void;
  onDelete: (id: string) => void;
  slotKeyCandidates: string[];
  onUpdateNode: (updates: Partial<DraftNode>) => void;
  onCommitNode: (updates: Partial<DraftNode>, label: string) => void;
  onToggleLayoutClassRef: (classKey: string) => void;
  onDesignChange: (nodeId: string, partial: DesignDraft) => void;
  routeCandidates?: string[];
  calculationBindings: CalcBinding[];
  draftNodeIds: string[];
  calcResults: ReadonlyMap<string, { targetNodeId: string; targetProp: string; result: { ok: true; value: number } | { ok: false; error: string } }>;
  onCalcBindingsChange: (bindings: CalcBinding[]) => void;
  emissionDataJson: string;
  onEmissionDataJsonChange: (json: string) => void;
}): JSX.Element {
  return (
    <aside
      class="layout-right-dock flex shrink-0 flex-col gap-2 self-stretch overflow-y-auto"
      style={{ width: LAYOUT_RIGHT_DOCK_WIDTH, minWidth: "320px", maxWidth: "420px" }}
      aria-label="レイアウト編集ドック"
      data-selected-node-id={selectedNodeId ?? ""}
    >
      <Accordion title={`レイヤー (${draftNodes.length})`} defaultOpen={true}>
        <LayerTree
          embedded
          draftNodes={draftNodes}
          selectedNodeId={selectedNodeId}
          onSelect={(id) => onSelectNode(id === selectedNodeId ? null : id)}
          onReparent={onReparent}
          onCopy={onCopy}
          onDelete={onDelete}
        />
      </Accordion>

      {selectedNode ? (
        <>
          <Accordion
            title={`${UX_LAYOUT_INSPECTOR_SECTION} — ${friendlyNodeLabel(selectedNode)}`}
            defaultOpen={true}
          >
            <CanvasInspector
              key={selectedNode.nodeId}
              embedded
              node={selectedNode}
              draftNodes={draftNodes}
              slotKeyCandidates={slotKeyCandidates}
              onUpdate={onUpdateNode}
              onCommit={onCommitNode}
              onToggleLayoutClassRef={onToggleLayoutClassRef}
              onCopy={() => onCopy(selectedNode.nodeId)}
              onClose={() => onSelectNode(null)}
            />
          </Accordion>
          <Accordion
            title={`${UX_DESIGN_INSPECTOR_SECTION} — ${friendlyNodeLabel(selectedNode)}`}
            defaultOpen={false}
          >
            <PackageDesignPanel
              selectedPackageId={packageId}
              selectedCanvasNode={selectedNode}
              routeCandidates={routeCandidates}
              canvasDesignDraft={canvasDesignDraft}
              onDesignPreviewChange={onDesignChange}
              onCommitNode={onCommitNode}
            />
          </Accordion>
        </>
      ) : (
        <p class="rounded border border-dashed border-gray-200 px-2 py-3 text-center text-xs text-gray-500">
          ノードを選択してください
        </p>
      )}
      <Accordion title={`ローカル計算 (${calculationBindings.length})`} defaultOpen={false}>
        <LocalCalcBindingPanel
          bindings={calculationBindings}
          draftNodeIds={draftNodeIds}
          calcResults={calcResults}
          onBindingsChange={onCalcBindingsChange}
          emissionDataJson={emissionDataJson}
          onEmissionDataJsonChange={onEmissionDataJsonChange}
        />
      </Accordion>
    </aside>
  );
}

// ─── ヘルパー ─────────────────────────────────────────────────────────────────

function makeNodeId(): string {
  return `node_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
}

function nextOrderIndexForParent(
  nodes: DraftNode[],
  parentNodeId: string | null,
): number {
  const siblings = nodes.filter((n) => (n.parentNodeId ?? null) === parentNodeId);
  if (siblings.length === 0) return 0;
  return Math.max(...siblings.map((n) => n.orderIndex)) + 1;
}

function isLayoutContainerNode(
  node: DraftNode,
  draftNodes: DraftNode[],
): boolean {
  if (draftNodes.some((n) => n.parentNodeId === node.nodeId)) return true;
  return (node.layoutClassRefs ?? []).some((key) => {
    const entry = lookupTopologyLayoutClassKey(key.trim());
    return entry?.allowedFor.some((r) =>
      r === "layout_root" || r === "layout_section" || r === "layout_row"
    ) ?? false;
  });
}

function isDraftOnlyEntry(c: { registrationRequired: boolean }): boolean {
  return c.registrationRequired;
}

type LayoutRouteCandidate = {
  layoutId: string;
  layoutKey: string;
  routeKey: string;
  layoutKind: string;
  slotKeys: string[];
};

type LayoutPatchSummary = {
  action: "validate" | "apply";
  valid: boolean;
  layoutId: string;
  routeKey: string;
  layoutKey?: string;
  nodeCount: number;
  draftOnlyCount: number;
  cssTokenCount: number;
  message: string;
  nextAction: string;
  errors: ValidationError[];
};

const GENERIC_SLOT_KEYS = [
  "main",
  "header",
  "footer",
  "sidebar",
  "content",
  "body",
];

// v2 visual canvas constants
const SNAP_SIZE = 10;
const DEFAULT_NODE_WIDTH = 140;
const DEFAULT_NODE_HEIGHT = 60;
const CANVAS_MIN_HEIGHT = 480;
/** Canvas workspace height — palettes are left-docked inside the row, no strip overhead. */
const CANVAS_WORKSPACE_HEIGHT = "calc(100dvh - 11rem)";
const MAX_HISTORY = 50;

// Gap 3: Error code → actionable cause + fix
const ERROR_CODE_FIX: Record<
  string,
  { cause: string; suggestion: string }
> = {
  DRAFT_ONLY_NODES: {
    cause: "まだ使えない部品が含まれています",
    suggestion:
      "/admin/contents で部品登録を完了するか、配置可能な部品のみ canvas に置いてください",
  },
  LAYOUT_NOT_FOUND: {
    cause: "レイアウトIDが見つかりません",
    suggestion: "ルート/レイアウト選択を確認し、再選択してください",
  },
  ROUTE_NOT_FOUND: {
    cause: "ルートキーが存在しません",
    suggestion: "先に部品を登録してルートを作成してください",
  },
  CSS_TOKEN_INVALID: {
    cause: "CSSトークン参照が無効です",
    suggestion: "右パネルのデザインインスペクタで正しいトークンを選択してください",
  },
  LAYOUT_CLASS_REF_INVALID: {
    cause: "レイアウトクラス参照が解決できません",
    suggestion:
      "topology layout class ref を確認し、有効なキーを選択してください",
  },
  LAYOUT_CANDIDATES_LOAD_FAILED: {
    cause: "レイアウト候補の取得に失敗しました",
    suggestion: "バックエンド接続と認証トークンを確認してください",
  },
  BUCKET_CREATE_FAILED: {
    cause: "部品の登録に失敗しました",
    suggestion: "すでに登録済みでないか /admin/contents で確認してください",
  },
  GENERATE_FAILED: {
    cause: "パッケージ化に失敗しました",
    suggestion:
      "バックエンド接続とルートキーを確認するか /admin/contents で登録を進めてください",
  },
  PROMOTE_FAILED: {
    cause: "配置可能化に失敗しました",
    suggestion: "先にパッケージ化を完了するか /admin/contents で登録を進めてください",
  },
  LAYOUT_ID_MISMATCH: {
    cause: "サーバーが異なるレイアウトIDを返しました",
    suggestion:
      "レイアウト候補を再読み込みして、正しいレイアウトを選択してください",
  },
  RESPONSIVE_TOKEN_RULE_JSON_INVALID: {
    cause: "レスポンシブルール JSON が不正です",
    suggestion:
      '形式: {"sm": ["token.key"], "md": ["token.key"]}。有効ブレークポイント: sm, md, lg, xl',
  },
};

function deriveCandidatesFromPalette(
  promoted: PromotedPaletteEntry[],
): LayoutRouteCandidate[] {
  const seen = new Set<string>();
  const out: LayoutRouteCandidate[] = [];
  for (const p of promoted) {
    const key = `${p.routeKey}::${p.layoutId}`;
    if (seen.has(key)) continue;
    seen.add(key);
    out.push({
      layoutId: p.layoutId,
      layoutKey: `${p.routeKey}:${p.componentKey}:layout`,
      routeKey: p.routeKey,
      layoutKind: p.componentKind,
      slotKeys: [],
    });
  }
  return out;
}

type PackagedHandoff = {
  packageId: string;
  routeKey: string;
  layoutId: string;
};

type AdminPackageRow = {
  packageId: string;
  packageKey: string;
  routeKey?: string | null;
  layoutId?: string | null;
  wiringId?: string | null;
};

async function findPackageForRoute(
  routeKey: string,
): Promise<AdminPackageRow | null> {
  const body = await dispatchAdminOp("ui_topology", "list_packages");
  if (dispatchOpFailed(body)) return null;
  const data = body?.emission?.data;
  if (!Array.isArray(data)) return null;
  const list = data as AdminPackageRow[];
  return list.find((p) => p.routeKey === routeKey) ?? null;
}

async function ensureShellPackageForRoute(
  routeKey: string,
): Promise<{ handoff: PackagedHandoff | null; error: ValidationError | null }> {
  const existing = await findPackageForRoute(routeKey);
  if (existing?.packageId && existing?.layoutId) {
    return {
      handoff: {
        packageId: existing.packageId,
        routeKey,
        layoutId: existing.layoutId,
      },
      error: null,
    };
  }
  const body = await dispatchAdminOp("package_generator", "promote_package", {
    routeKey,
    bucketItemIds: [],
  });
  const handoff = parsePackagedHandoff(body, routeKey);
  if (!handoff) {
    return {
      handoff: null,
      error: body?.errors?.[0] ?? {
        code: "SHELL_PACKAGE_FAILED",
        message: "ルート用パッケージの自動生成に失敗しました。",
      },
    };
  }
  return { handoff, error: null };
}

async function registerCatalogComponentInPackage(
  routeKey: string,
  componentKey: string,
): Promise<{ ok: boolean; error?: ValidationError }> {
  const catalogEntry = COMPONENT_CATALOG_ENTRIES.find((c) =>
    c.componentKey === componentKey
  );
  if (!catalogEntry) {
    return {
      ok: false,
      error: { code: "CATALOG_NOT_FOUND", message: componentKey },
    };
  }

  const paletteBody = await dispatchAdminOp("ui_topology", "promoted_palette");
  const promoted = paletteBody?.emission?.data as
    | PromotedPaletteEntry[]
    | undefined;
  if (Array.isArray(promoted)) {
    const pkg = await findPackageForRoute(routeKey);
    if (
      pkg &&
      promoted.some((p) =>
        p.componentKey === componentKey && p.packageId === pkg.packageId
      )
    ) {
      return { ok: true };
    }
  }

  const [bucketedBody, packagingBody] = await Promise.all([
    dispatchAdminOp("ui_component_bucket", "list"),
    dispatchAdminOp("ui_component_bucket", "list", { status: "packaging" }),
  ]);
  const bucketItems = [
    ...(Array.isArray(bucketedBody?.emission?.data)
      ? bucketedBody.emission.data as BucketItem[]
      : []),
    ...(Array.isArray(packagingBody?.emission?.data)
      ? packagingBody.emission.data as BucketItem[]
      : []),
  ];
  let bucketId = bucketItems.find((b) => b.componentKey === componentKey)
    ?.bucketItemId;

  if (!bucketId) {
    const createBody = await dispatchAdminOp("ui_component_bucket", "create", {
      componentKey: catalogEntry.componentKey,
      sourcePath: catalogEntry.sourcePath,
      componentKind: catalogEntry.componentKind,
      metadataJson: "{}",
    });
    if (dispatchOpFailed(createBody)) {
      return {
        ok: false,
        error: createBody?.errors?.[0] ?? {
          code: "BUCKET_CREATE_FAILED",
          message: `${componentKey} の登録に失敗しました。`,
        },
      };
    }
    bucketId = createBody?.emission?.data?.bucketItemId as string | undefined;
    if (!bucketId) {
      return {
        ok: false,
        error: {
          code: "BUCKET_CREATE_FAILED",
          message: `${componentKey} の bucketItemId が取得できませんでした。`,
        },
      };
    }
  }

  const promBody = await dispatchAdminOp("package_generator", "promote_package", {
    routeKey,
    bucketItemIds: [bucketId],
  });
  if (dispatchOpFailed(promBody)) {
    return {
      ok: false,
      error: promBody?.errors?.[0] ?? {
        code: "PROMOTE_FAILED",
        message: "部品のパッケージ追加に失敗しました。",
      },
    };
  }
  return { ok: true };
}

async function detachComponentFromPackage(
  routeKey: string,
  componentKey: string,
): Promise<{ ok: boolean; error?: ValidationError }> {
  const body = await dispatchAdminOp(
    "package_generator",
    "detach_package_components",
    { routeKey, componentKeys: [componentKey] },
  );
  if (dispatchOpFailed(body)) {
    return {
      ok: false,
      error: body?.errors?.[0] ?? {
        code: "DETACH_FAILED",
        message: "パッケージからの部品削除に失敗しました。",
      },
    };
  }
  return { ok: true };
}

function dispatchOpFailed(
  body: { success?: boolean; errors?: ValidationError[] } | null | undefined,
): boolean {
  return !body?.success ||
    (Array.isArray(body?.errors) && body.errors.length > 0);
}

function parsePackagedHandoff(
  body: { success?: boolean; emission?: { data?: unknown } } | null | undefined,
  fallbackRouteKey: string,
): PackagedHandoff | null {
  if (dispatchOpFailed(body)) return null;
  const data = body?.emission?.data as Record<string, unknown> | undefined;
  if (!data) return null;
  const packageId = typeof data.packageId === "string" ? data.packageId : null;
  const layoutId = typeof data.layoutId === "string" ? data.layoutId : null;
  const routeKey = typeof data.routeKey === "string"
    ? data.routeKey
    : fallbackRouteKey;
  if (!packageId || !layoutId) return null;
  return { packageId, routeKey, layoutId };
}

function layoutCandidateForPackage(
  routeKey: string,
  layoutId: string,
  packageKey?: string,
): LayoutRouteCandidate {
  return {
    layoutId,
    layoutKey: packageKey ? `${packageKey}:layout` : `${routeKey}:layout`,
    routeKey,
    layoutKind: "package",
    slotKeys: [],
  };
}

/** Inject selected package route/layout when layout_candidates query omits the new row yet. */
function ensureScopedLayoutCandidates(
  candidates: LayoutRouteCandidate[],
  scopedRouteKey?: string | null,
  scopedLayoutId?: string | null,
): LayoutRouteCandidate[] {
  if (!scopedRouteKey?.trim() || !scopedLayoutId?.trim()) return candidates;
  const rk = scopedRouteKey.trim();
  const lid = scopedLayoutId.trim();
  if (candidates.some((c) => c.routeKey === rk && c.layoutId === lid)) {
    return candidates;
  }
  return [...candidates, layoutCandidateForPackage(rk, lid)];
}

function uniqueRouteKeys(candidates: LayoutRouteCandidate[]): string[] {
  return [...new Set(candidates.map((c) => c.routeKey))].sort();
}

function layoutsForRoute(
  candidates: LayoutRouteCandidate[],
  routeKey: string,
): LayoutRouteCandidate[] {
  return candidates.filter((c) => c.routeKey === routeKey);
}

function wouldCreateParentCycle(
  nodes: DraftNode[],
  nodeId: string,
  parentId: string | null,
): boolean {
  return wouldCreateVisualParentCycle(nodes, nodeId, parentId);
}

function buildSlotKeyCandidates(
  draftNodes: DraftNode[],
  dbSlotKeys: string[] = [],
): string[] {
  const fromCanvas = draftNodes.map((n) => n.slotKey).filter(Boolean);
  return [...new Set(["", ...dbSlotKeys, ...fromCanvas, ...GENERIC_SLOT_KEYS])];
}

function shortId(id: string): string {
  return id.length > 8 ? id.slice(0, 8) : id;
}

async function loadLayoutCandidatesFromBackend(): Promise<{
  candidates: LayoutRouteCandidate[];
  errors: ValidationError[];
}> {
  const body = await dispatchAdminOp("ui_topology", "layout_candidates");
  if (body?.errors?.length) {
    return { candidates: [], errors: body.errors };
  }
  const data = body?.emission?.data;
  if (!Array.isArray(data)) {
    return {
      candidates: [],
      errors: [{
        code: "LAYOUT_CANDIDATES_LOAD_FAILED",
        message: "候補データが取得できませんでした。",
      }],
    };
  }
  return { candidates: data as LayoutRouteCandidate[], errors: [] };
}

function projectLayoutPatchSummary(
  action: "validate" | "apply",
  body: Record<string, unknown> | null,
  draftNodes: DraftNode[],
  cssTokenCount: number,
  layoutKey?: string,
): LayoutPatchSummary {
  const errors: ValidationError[] = Array.isArray(body?.errors)
    ? (body.errors as ValidationError[])
    : [];
  const emission = body?.emission as
    | { data?: Record<string, unknown> }
    | undefined;
  const data = emission?.data ?? body;
  const dataValid = data?.valid !== false && data?.ok !== false;
  const valid = errors.length === 0 && dataValid;
  const message =
    (typeof data?.message === "string" ? data.message : undefined) ??
      errors[0]?.message ??
      (valid ? "成功" : "失敗");
  const layoutId = typeof data?.layoutId === "string" ? data.layoutId : "";
  const routeKey = typeof data?.routeKey === "string" ? data.routeKey : "";
  const draftOnlyCount = draftNodes.filter((n) => n.isDraftOnly).length;

  let nextAction = "";
  if (!valid) {
    if (
      errors.some((e) => e.code?.includes("CSS") || e.message?.includes("CSS"))
    ) {
      nextAction = "CSS トークン参照を修正してください";
    } else if (draftOnlyCount > 0 && action === "apply") {
      nextAction = "下書きのみの部品を配置可能化するか削除してください";
    } else {
      nextAction = "エラーを修正してから再実行してください";
    }
  } else if (action === "validate") {
    nextAction = "問題なければ「DB に反映する」を実行";
  } else {
    nextAction =
      "モーダルで次のステップ（デザイン設定 / デモ / ページ群管理）を選んでください";
  }

  return {
    action,
    valid,
    layoutId,
    routeKey,
    layoutKey,
    nodeCount: draftNodes.length,
    draftOnlyCount,
    cssTokenCount,
    message,
    nextAction,
    errors,
  };
}

// deno-lint-ignore no-explicit-any
// Gap 1: Lifecycle step indicator — draft → validated → applied → persisted
function LifecycleStepIndicator(
  { phase }: { phase: LifecyclePhase },
): JSX.Element {
  const steps: { id: string; label: string; phases: LifecyclePhase[] }[] = [
    {
      id: "draft",
      label: "ドラフト編集",
      phases: ["idle"],
    },
    { id: "checked", label: "検証済み", phases: ["validating", "validated"] },
    {
      id: "applied",
      label: "適用済み",
      phases: ["applying", "applied_ok", "applied_fail"],
    },
    { id: "persisted", label: "永続化完了", phases: ["persisted"] },
  ];
  const currentIdx = steps.findIndex((s) => s.phases.includes(phase));
  const isError = phase === "applied_fail";

  return (
    <div
      class="mb-4"
      role="status"
      aria-label={`現在のフェーズ: ${
        steps[Math.max(0, currentIdx)]?.label ?? phase
      }`}
    >
      <div class="flex items-center gap-0">
        {steps.map((step, i) => {
          const isDone = i < currentIdx;
          const isCurrent = i === currentIdx;
          const isErrorStep = isCurrent && isError;
          return (
            <div key={step.id} class="flex flex-1 flex-col items-center">
              <div class="flex w-full items-center">
                {i > 0 && (
                  <div
                    class={`h-0.5 flex-1 ${
                      isDone ? "bg-blue-500" : "bg-gray-200"
                    }`}
                  />
                )}
                <div
                  class={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${
                    isErrorStep
                      ? "bg-red-500 text-white"
                      : isCurrent
                      ? "bg-blue-600 text-white ring-2 ring-blue-300 ring-offset-1"
                      : isDone
                      ? "bg-blue-500 text-white"
                      : "border-2 border-gray-300 bg-white text-gray-400"
                  }`}
                  aria-current={isCurrent ? "step" : undefined}
                >
                  {isDone ? "✓" : isErrorStep ? "✗" : String(i + 1)}
                </div>
                {i < steps.length - 1 && (
                  <div
                    class={`h-0.5 flex-1 ${
                      isDone ? "bg-blue-500" : "bg-gray-200"
                    }`}
                  />
                )}
              </div>
              <div
                class={`mt-1 text-center text-[0.65rem] font-medium ${
                  isErrorStep
                    ? "text-red-600"
                    : isCurrent
                    ? "text-blue-700"
                    : isDone
                    ? "text-blue-500"
                    : "text-gray-400"
                }`}
              >
                {step.label}
              </div>
            </div>
          );
        })}
      </div>
      {isError && (
        <p role="alert" class="mt-2 text-xs text-red-700">
          エラー — 「エラー —
          修正方法」を確認してください。まだ使えない部品がある場合は部品登録パネルへ戻ってください。
        </p>
      )}
      {(phase === "applied_ok" || phase === "persisted") && (
        <p
          role="status"
          class="mt-2 rounded border border-green-300 bg-green-50 px-2 py-1.5 text-xs font-medium text-green-800"
        >
          配置を DB に保存しました — 次のステップへ進んでください。
        </p>
      )}
    </div>
  );
}

// Gap 3: Actionable validation errors with cause + fix suggestion + contextual node info
type AnnotatedValidationError = {
  code: string;
  message: string;
  field?: string;
  nodeId?: string;
  componentKey?: string;
};

function ActionableValidationErrorPanel({
  errors,
  title,
}: {
  errors: AnnotatedValidationError[];
  title?: string;
}): JSX.Element | null {
  if (errors.length === 0) return null;
  return (
    <div
      role="alert"
      class="rounded-lg border border-red-300 bg-red-50 p-3 text-sm"
    >
      {title && <div class="mb-2 font-semibold text-red-800">{title}</div>}
      <ul class="space-y-2 pl-0">
        {errors.map((e, i) => {
          const fix = ERROR_CODE_FIX[e.code] ?? null;
          return (
            <li key={e.code ?? String(i)} class="flex flex-col gap-0.5">
              <span class="font-medium text-red-700">
                {fix?.cause ?? e.message}
              </span>
              {fix && (
                <span class="text-xs text-red-600">
                  修正方法: {fix.suggestion}
                </span>
              )}
              {/* Contextual detail: field / nodeId / componentKey when available */}
              {(e.field || e.nodeId || e.componentKey) && (
                <div class="mt-0.5 rounded border border-red-200 bg-white px-2 py-0.5 font-mono text-xs text-red-700">
                  {e.componentKey && (
                    <span>
                      部品:{" "}
                      <code>{friendlyComponentLabel(e.componentKey)}</code>
                      {" "}
                    </span>
                  )}
                  {e.field && (
                    <span>
                      フィールド: <code>{e.field}</code>
                      {" "}
                    </span>
                  )}
                  {e.nodeId && (
                    <span class="text-gray-400">({e.nodeId.slice(0, 8)})</span>
                  )}
                </div>
              )}
              <span class="font-mono text-[0.65rem] text-gray-400">
                [{e.code}]
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function AdvancedManualOverride({
  title,
  children,
}: {
  title?: string;
  // deno-lint-ignore no-explicit-any
  children?: any;
}): JSX.Element {
  return (
    <details class="mt-2 rounded border border-orange-300 bg-orange-50 p-2">
      <summary class="cursor-pointer text-sm font-bold text-orange-800">
        {title ?? "上級者向け設定（通常は不要）"}
      </summary>
      <p class="text-muted-xs mt-1 mb-2">
        通常導線外の手入力です。SSOT key/UUID
        を直接指定する場合のみ使用してください。
      </p>
      {children}
    </details>
  );
}

function ApplyReadinessPanel({
  canPatch,
  effectiveRouteKey,
  effectiveLayoutId,
  draftNodes,
  layoutClassRefError,
}: {
  canPatch: boolean;
  effectiveRouteKey: string;
  effectiveLayoutId: string;
  draftNodes: DraftNode[];
  layoutClassRefError: string | null;
}): JSX.Element {
  const draftOnlyCount = draftNodes.filter((n) => n.isDraftOnly).length;
  const customPositionedCount = draftNodes.filter(
    (n) =>
      n.x > 0 || n.y > 0 || n.width !== DEFAULT_NODE_WIDTH ||
      n.height !== DEFAULT_NODE_HEIGHT,
  ).length;
  const allClear = canPatch && draftOnlyCount === 0 && !layoutClassRefError;

  return (
    <div
      class={`mb-3 rounded border p-3 text-sm ${
        allClear
          ? "border-green-300 bg-green-50"
          : "border-amber-300 bg-amber-50"
      }`}
    >
      <strong class="block mb-2">保存前チェック</strong>
      <ul class="space-y-1 pl-1">
        <li class="flex items-start gap-2">
          <span class={canPatch ? "text-green-700" : "text-red-600"}>
            {canPatch ? "✓" : "✗"}
          </span>
          <span>
            ルート / レイアウト選択: {canPatch
              ? (
                <>
                  <code class="text-xs">{effectiveRouteKey}</code> /{" "}
                  <code class="text-xs">{shortId(effectiveLayoutId)}</code>
                </>
              )
              : "未選択 — ルートとレイアウトを選択してください"}
          </span>
          {!canPatch && (
            <a
              href="/admin/contents"
              class="ml-2 rounded bg-amber-600 px-1.5 py-0.5 text-xs font-medium text-white no-underline hover:bg-amber-700"
            >
              /admin/contents で確認
            </a>
          )}
        </li>
        <li class="flex items-start gap-2">
          <span
            class={draftOnlyCount === 0 ? "text-green-700" : "text-red-600"}
          >
            {draftOnlyCount === 0 ? "✓" : "✗"}
          </span>
          <span>
            まだ使えない部品: {draftOnlyCount === 0 ? "なし" : (
              <>
                {draftOnlyCount}{" "}
                件 — 先に部品登録を完了してください（保存はブロック）
              </>
            )}
          </span>
          {draftOnlyCount > 0 && (
            <a
              href="/admin/contents"
              class="ml-2 rounded bg-red-600 px-1.5 py-0.5 text-xs font-medium text-white no-underline hover:bg-red-700"
            >
              /admin/contents で修正 →
            </a>
          )}
        </li>
        <li class="flex items-start gap-2">
          <span
            class={!layoutClassRefError ? "text-green-700" : "text-red-600"}
          >
            {!layoutClassRefError ? "✓" : "✗"}
          </span>
          <span>
            layout class ref 解決:{" "}
            {layoutClassRefError ? layoutClassRefError : "OK"}
          </span>
        </li>
        <li class="flex items-start gap-2">
          <span class="text-blue-600 text-xs">i</span>
          <span class="text-xs">
            layout nodes: {draftNodes.length} 件
            {customPositionedCount > 0
              ? ` (${customPositionedCount} 件 canvas 位置調整済み)`
              : draftNodes.length > 0
              ? " (canvas デフォルト配置)"
              : ""}
          </span>
        </li>
        <li class="flex items-start gap-2">
          <span class="text-muted-xs">i</span>
          <span class="text-muted-xs">
            保存対象は配置構造情報（親部品・配置スロット・表示順・layoutClassRefs・位置・サイズ）。
            cssTokenRefs・色・形は「デザインを編集」タブで保存します。
          </span>
        </li>
      </ul>
      {allClear && (
        <p class="mt-2 text-green-700 font-semibold text-xs">
          すべてのローカルチェック通過。「適用」でバリデーション後に DB へ保存できます。
        </p>
      )}
    </div>
  );
}

function LayoutPatchSummaryPanel(
  { summary }: { summary: LayoutPatchSummary },
): JSX.Element {
  return (
    <div
      class={`rounded border p-3 text-sm ${
        summary.valid
          ? "border-green-300 bg-green-50"
          : "border-red-300 bg-red-50"
      }`}
    >
      <div class="mb-2 flex flex-wrap items-center gap-2">
        <strong>
          {summary.action === "validate" ? "バリデート" : "適用"} 結果
        </strong>
        <StatusBadge
          text={summary.valid ? "問題なし" : "エラーあり"}
          variant={summary.valid ? "ok" : "error"}
        />
      </div>
      <ul class="my-0 pl-4">
        <li>
          ノード数: {summary.nodeCount}（まだ使えない部品:{" "}
          {summary.draftOnlyCount}）
        </li>
        <li>
          ルート: <code>{summary.routeKey || "—"}</code>
        </li>
        <li>
          レイアウト: {summary.layoutKey
            ? <code>{summary.layoutKey}</code>
            : <code>{shortId(summary.layoutId) || "—"}</code>}
          {summary.layoutId && (
            <span class="ml-1 font-mono text-xs text-slate-600">
              (layoutId: {summary.layoutId})
            </span>
          )}
        </li>
        <li>CSS トークン: {summary.cssTokenCount} 件</li>
        <li>メッセージ: {summary.message}</li>
        <li>
          <strong>次のアクション:</strong> {summary.nextAction}
        </li>
      </ul>
      {summary.errors.length > 0 && (
        <ValidationErrorPanel
          errors={summary.errors}
          title="修正が必要なエラー"
        />
      )}
    </div>
  );
}

function RouteLayoutSelector({
  candidates,
  routeKey,
  layoutId,
  onRouteChange,
  onLayoutChange,
  disabled,
  loadError,
}: {
  candidates: LayoutRouteCandidate[];
  routeKey: string;
  layoutId: string;
  onRouteChange: (routeKey: string) => void;
  onLayoutChange: (layoutId: string) => void;
  disabled?: boolean;
  loadError?: ValidationError[] | null;
}): JSX.Element {
  const routes = uniqueRouteKeys(candidates);
  const layouts = routeKey ? layoutsForRoute(candidates, routeKey) : [];

  return (
    <div class="mb-3 flex flex-wrap gap-2">
      <label class="flex min-w-[200px] flex-1 flex-col gap-0.5 text-sm">
        ページルート
        <select
          value={routeKey}
          disabled={disabled || routes.length === 0}
          onChange={(e) => onRouteChange((e.target as HTMLSelectElement).value)}
          class="input w-full"
        >
          <option value="">— ルートを選択 —</option>
          {routes.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
      </label>
      <label class="flex min-w-[240px] flex-[2] flex-col gap-0.5 text-sm">
        レイアウト
        <select
          value={layoutId}
          disabled={disabled || !routeKey || layouts.length === 0}
          onChange={(e) =>
            onLayoutChange((e.target as HTMLSelectElement).value)}
          class="input w-full font-mono text-xs"
        >
          <option value="">— レイアウトを選択 —</option>
          {layouts.map((l) => (
            <option key={l.layoutId} value={l.layoutId}>
              {l.layoutKey} ({shortId(l.layoutId)})
            </option>
          ))}
        </select>
      </label>
      {loadError && loadError.length > 0 && (
        <ValidationErrorPanel errors={loadError} title="候補ロードエラー" />
      )}
      {candidates.length === 0 && !loadError?.length && (
        <div class="w-full rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <p class="font-semibold">ルート・レイアウト候補がまだありません</p>
          <p class="mt-1 text-xs">
            部品登録タブで「ページルートを直接入力」→「パッケージ化」→「配置可能にする」を完了すると、ここに候補が表示されます。
          </p>
        </div>
      )}
    </div>
  );
}

// Gap 8: CSS token visual swatch — values resolved from SSOT via resolveCssTokenValue
function CssTokenSwatch(
  { token }: {
    token: {
      tokenKey: string;
      category: string;
      property: string;
      semanticRole: string;
    };
  },
): JSX.Element {
  if (token.category === "color") {
    const resolvedValue = resolveCssTokenValue(token.tokenKey);
    // Determine bg/fg from property to render a meaningful swatch
    const isTextColor = token.property === "color";
    const swatchBg = isTextColor ? "#f3f4f6" : (resolvedValue ?? "#e5e7eb");
    const swatchFg = isTextColor ? (resolvedValue ?? "#333") : undefined;
    return (
      <span
        class="inline-flex items-center justify-center h-4 w-4 rounded border border-gray-300 align-middle font-mono text-[0.55rem] font-bold"
        style={{ backgroundColor: swatchBg, color: swatchFg }}
        aria-label={`色プレビュー: ${resolvedValue ?? token.semanticRole}`}
        title={resolvedValue}
      >
        {isTextColor ? "A" : ""}
      </span>
    );
  }
  if (token.category === "spacing") {
    const val = resolveCssTokenValue(token.tokenKey) ?? "";
    return (
      <span
        class="inline-block rounded-sm border border-dashed border-gray-400 align-middle"
        style={{ width: "12px", height: "12px" }}
        aria-label={`スペーシング: ${val}`}
        title={val}
      />
    );
  }
  if (token.category === "radius") {
    const val = resolveCssTokenValue(token.tokenKey) ?? "4px";
    return (
      <span
        class="inline-block h-4 w-4 border border-gray-400 align-middle"
        style={{ borderRadius: val }}
        aria-label={`角丸: ${val}`}
        title={val}
      />
    );
  }
  if (token.category === "typography") {
    const val = resolveCssTokenValue(token.tokenKey) ?? "monospace";
    return (
      <span
        class="align-middle text-[0.6rem] text-gray-500"
        style={{ fontFamily: val }}
        aria-label={`フォント: ${val}`}
        title={val}
      >
        Aa
      </span>
    );
  }
  const val = resolveCssTokenValue(token.tokenKey);
  return (
    <span class="text-[0.6rem] text-gray-400" title={val}>
      {token.property.slice(0, 3)}
    </span>
  );
}

function CssTokenPicker({
  selectedTokenRefs,
  onToggle,
}: {
  selectedTokenRefs: string[];
  onToggle: (tokenKey: string) => void;
}): JSX.Element {
  const [tokenFilter, setTokenFilter] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [scopeFilter, setScopeFilter] = useState("");
  const [roleFilter, setRoleFilter] = useState("");

  const categories = [...new Set(CSS_DICTIONARY_TOKENS.map((t) => t.category))]
    .sort();
  const scopes = [
    ...new Set(CSS_DICTIONARY_TOKENS.flatMap((t) => t.componentScope)),
  ].sort();
  const roles = [...new Set(CSS_DICTIONARY_TOKENS.map((t) => t.semanticRole))]
    .sort();

  const filtered = CSS_DICTIONARY_TOKENS.filter((t) => {
    if (tokenFilter) {
      const label = t.userLabel ?? "";
      const matchesKey = t.tokenKey.toLowerCase().includes(tokenFilter.toLowerCase());
      const matchesLabel = label.toLowerCase().includes(tokenFilter.toLowerCase());
      if (!matchesKey && !matchesLabel) return false;
    }
    if (categoryFilter && t.category !== categoryFilter) return false;
    if (scopeFilter && !t.componentScope.includes(scopeFilter)) return false;
    if (roleFilter && t.semanticRole !== roleFilter) return false;
    return true;
  });

  return (
    <div>
      <div class="mb-2 flex flex-wrap gap-2">
        <input
          value={tokenFilter}
          onInput={(e) => setTokenFilter((e.target as HTMLInputElement).value)}
          placeholder="トークン名で検索"
          class="input-mono flex-1 text-xs"
          aria-label="CSSトークンを検索"
        />
        <select
          value={categoryFilter}
          onChange={(e) =>
            setCategoryFilter((e.target as HTMLSelectElement).value)}
          class="input w-auto text-xs"
          aria-label="カテゴリでフィルター"
        >
          <option value="">カテゴリ（すべて）</option>
          {categories.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        <select
          value={scopeFilter}
          onChange={(e) =>
            setScopeFilter((e.target as HTMLSelectElement).value)}
          class="input w-auto text-xs"
          aria-label="スコープでフィルター"
        >
          <option value="">対象部品（すべて）</option>
          {scopes.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select
          value={roleFilter}
          onChange={(e) => setRoleFilter((e.target as HTMLSelectElement).value)}
          class="input w-auto text-xs"
          aria-label="役割でフィルター"
        >
          <option value="">役割（すべて）</option>
          {roles.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
      </div>

      {/* Gap 8: before/after visual diff for selected tokens */}
      {selectedTokenRefs.length > 0 && (
        <div class="mb-3 rounded border border-blue-200 bg-blue-50 p-2">
          <strong class="text-xs text-blue-800">
            選択済みトークン ({selectedTokenRefs.length}) — クリックで解除
          </strong>
          <div class="mt-2 flex flex-wrap gap-2">
            {selectedTokenRefs.map((key) => {
              const token = CSS_DICTIONARY_TOKENS.find((t) =>
                t.tokenKey === key
              );
              const label = token?.userLabel ?? key;
              return (
                <button
                  key={key}
                  type="button"
                  onClick={() => onToggle(key)}
                  class="flex items-center gap-1.5 rounded border border-blue-400 bg-white px-2 py-1 text-xs hover:border-red-400 hover:bg-red-50"
                  title={`${key} — クリックで選択解除`}
                  aria-label={`${key}を選択解除`}
                >
                  {token && <CssTokenSwatch token={token} />}
                  <span>{label}</span>
                  <span class="text-gray-400">✕</span>
                </button>
              );
            })}
          </div>
        </div>
      )}

      <div
        class="table-wrap max-h-64 overflow-y-auto"
        role="region"
        aria-label="CSSトークン一覧"
      >
        <table class="table text-xs">
          <thead>
            <tr>
              {[
                "",
                "プレビュー",
                "スタイル名",
                "カテゴリ",
                "対象",
                "CSSプロパティ",
              ].map((h) => <th key={h} scope="col">{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {filtered.map((t) => {
              const isSelected = selectedTokenRefs.includes(t.tokenKey);
              const label = t.userLabel;
              return (
                <tr
                  key={t.tokenKey}
                  class={isSelected ? "bg-blue-50" : "hover:bg-gray-50"}
                  onClick={() => onToggle(t.tokenKey)}
                  style={{ cursor: "pointer" }}
                >
                  <td onClick={(e) => e.stopPropagation()}>
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => onToggle(t.tokenKey)}
                      aria-label={`${label}を選択`}
                    />
                  </td>
                  <td>
                    <CssTokenSwatch token={t} />
                  </td>
                  <td title={t.tokenKey}>
                    {label}
                  </td>
                  <td>{t.category}</td>
                  <td>{t.componentScope.join(", ")}</td>
                  <td class="text-gray-600">{t.property}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      {filtered.length === 0 && (
        <p class="text-muted-xs mt-1">
          該当トークンなし — フィルタを調整してください。
        </p>
      )}
    </div>
  );
}

const CATEGORY_LABEL_MAP: Record<string, string> = {
  layout: "レイアウト",
  direction: "方向",
  alignment: "揃え",
  sizing: "サイズ",
  spacing: "余白・間隔",
  responsive: "レスポンシブ",
  surface: "面・見た目",
  shell: "シェル",
  container: "コンテナ種別",
  form_layout: "フォームレイアウト",
  boundary: "境界",
  state: "状態",
};

function TopologyLayoutClassPicker({
  selectedClassRefs,
  onToggle,
  scopeFilter = "",
  allowedForFilter = "",
  allowedForAny = [],
}: {
  selectedClassRefs: string[];
  onToggle: (classKey: string) => void;
  scopeFilter?: string;
  allowedForFilter?: string;
  allowedForAny?: string[];
}): JSX.Element {
  const [keyFilter, setKeyFilter] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");

  const categories = [
    ...new Set(TOPOLOGY_LAYOUT_CLASS_DICTIONARY.map((e) => e.category)),
  ].sort();

  const filtered = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.filter((e) => {
    // preview_state classes are for runtime preview only — not shown in normal inspector view
    if (e.category === "state") return false;
    if (
      keyFilter && !e.classKey.toLowerCase().includes(keyFilter.toLowerCase()) &&
      !e.label.toLowerCase().includes(keyFilter.toLowerCase())
    ) return false;
    if (categoryFilter && e.category !== categoryFilter) return false;
    if (allowedForFilter && !e.allowedFor.includes(allowedForFilter)) {
      return false;
    }
    if (
      allowedForAny.length > 0 &&
      !allowedForAny.some((role) => e.allowedFor.includes(role))
    ) {
      return false;
    }
    return true;
  });

  const preview = selectedClassRefs.length > 0
    ? resolveTopologyLayoutClassRefs(selectedClassRefs)
    : null;

  const grouped = filtered.reduce<Record<string, typeof filtered>>((acc, e) => {
    (acc[e.category] = acc[e.category] ?? []).push(e);
    return acc;
  }, {});

  return (
    <div>
      <p class="text-muted-xs mb-2">
        レイアウト投影専用のスタイルクラスです。画面装飾用ではありません。保存されるのはクラスキーのみです。
      </p>
      <div class="mb-2 flex flex-wrap gap-2">
        <input
          value={keyFilter}
          onInput={(e) => setKeyFilter((e.target as HTMLInputElement).value)}
          placeholder="クラス検索"
          class="input-mono flex-1 text-xs"
        />
        <select
          value={categoryFilter}
          onChange={(e) =>
            setCategoryFilter((e.target as HTMLSelectElement).value)}
          class="input w-auto text-xs"
        >
          <option value="">カテゴリ（すべて）</option>
          {categories.map((c) => (
            <option key={c} value={c}>{CATEGORY_LABEL_MAP[c] ?? c}</option>
          ))}
        </select>
      </div>

      {selectedClassRefs.length > 0 && (
        <div class="mb-2 rounded border border-blue-200 bg-blue-50 p-2">
          <strong class="text-xs">
            選択済みスタイルクラス ({selectedClassRefs.length})
          </strong>
          <div class="mt-1 flex flex-wrap gap-1">
            {selectedClassRefs.map((key) => {
              const entry = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.find((e) => e.classKey === key);
              return (
                <button
                  key={key}
                  type="button"
                  onClick={() => onToggle(key)}
                  class="rounded border border-blue-400 bg-white px-1.5 py-0.5 text-xs hover:bg-red-50"
                  title={key}
                >
                  {entry?.label ?? key} ✕
                </button>
              );
            })}
          </div>
          {preview?.ok && (
            <p class="text-muted-xs mt-1 mb-0">
              プレビュー解決: <code>{preview.className}</code>
            </p>
          )}
          {preview && !preview.ok && (
            <p class="text-red-600 text-xs mt-1 mb-0">{preview.error}</p>
          )}
        </div>
      )}

      <div class="max-h-72 overflow-y-auto">
        {Object.entries(grouped).map(([cat, entries]) => (
          <div key={cat} class="mb-2">
            <div class="mb-1 text-[0.6rem] font-semibold uppercase tracking-wide text-gray-400">
              {CATEGORY_LABEL_MAP[cat] ?? cat}
            </div>
            <div class="flex flex-wrap gap-1">
              {entries.map((e) => {
                const isSelected = selectedClassRefs.includes(e.classKey);
                return (
                  <button
                    key={e.classKey}
                    type="button"
                    onClick={() => onToggle(e.classKey)}
                    title={`${e.classKey}${e.description ? "\n" + e.description : ""}`}
                    class={`flex items-center gap-1 rounded border px-2 py-1 text-xs ${
                      isSelected
                        ? "border-blue-500 bg-blue-100 font-semibold"
                        : "border-gray-200 bg-white hover:bg-gray-50"
                    }`}
                  >
                    <span>{e.label}</span>
                    {e.conflictGroup && (
                      <span class="text-[0.5rem] text-gray-400 font-mono">[{e.conflictGroup}]</span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>
        ))}
      </div>
      <details class="mt-2">
        <summary class="cursor-pointer text-[0.6rem] text-gray-400">raw keys (advanced)</summary>
        <div class="mt-1 flex flex-wrap gap-1">
          {filtered.map((e) => (
            <span key={e.classKey} class="font-mono text-[0.6rem] text-gray-500">
              {e.classKey}
            </span>
          ))}
        </div>
      </details>
    </div>
  );
}

type ManifestRouteOption = {
  manifestId: string;
  topologySystemName: string;
  label: string;
  derivedRouteKey: string;
};

function readUiBuilderHandoffManifestId(): string {
  if (typeof globalThis.location === "undefined") return "";
  return new URLSearchParams(globalThis.location.search).get("manifestId")?.trim() ??
    "";
}

/**
 * Normal-flow route entry: loads manifests and derives routeKey from topologySystemName.
 * SSOT: routeKey = topologySystemNameToUiBuilderKey(topologySystemName).
 * Auto-commits on handoff (?manifestId=), single candidate, or manifest select change.
 */
function ManifestRouteEntry({
  initialManifestId,
  committedRouteKey,
  onCommit,
}: {
  initialManifestId: string;
  committedRouteKey: string;
  onCommit: (routeKey: string) => void;
}): JSX.Element {
  const [options, setOptions] = useState<ManifestRouteOption[]>([]);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const autoCommitAttempted = useRef(false);
  const onCommitRef = useRef(onCommit);
  onCommitRef.current = onCommit;

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setLoading(true);
      setLoadError(null);
      try {
        const items = await listAdminManifests();
        if (cancelled || !items) return;
        const resolved: ManifestRouteOption[] = [];
        for (const item of items) {
          const detail = await getAdminManifest(item.manifestId);
          if (cancelled) return;
          const shape = detail
            ? extractScreenDataShapeFromTopology(detail.topologyRawJson)
            : null;
          const sysName = shape?.topologySystemName?.trim() ?? "";
          if (!sysName || !isValidTopologySystemName(sysName)) continue;
          const derivedRouteKey = topologySystemNameToUiBuilderKey(sysName);
          const label = resolveVisibleTopologyName(shape?.userFacingTopologyLabel, sysName);
          resolved.push({ manifestId: item.manifestId, topologySystemName: sysName, label, derivedRouteKey });
        }
        if (!cancelled) setOptions(resolved);
      } catch (e) {
        if (!cancelled) setLoadError(String(e));
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (loading || options.length === 0 || committedRouteKey || autoCommitAttempted.current) {
      return;
    }
    const handoff = initialManifestId.trim();
    if (handoff) {
      const match = options.find((o) => o.manifestId === handoff);
      if (match) {
        autoCommitAttempted.current = true;
        onCommitRef.current(match.derivedRouteKey);
        return;
      }
    }
    if (options.length === 1) {
      autoCommitAttempted.current = true;
      onCommitRef.current(options[0]!.derivedRouteKey);
    }
  }, [loading, options, initialManifestId, committedRouteKey]);

  const committedOption = options.find((o) =>
    o.derivedRouteKey === committedRouteKey
  );

  if (committedRouteKey && committedOption && !pickerOpen) {
    return (
      <div class="mb-3 flex flex-wrap items-center gap-2 rounded border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-800">
        <span>
          編集中: <strong>{committedOption.label}</strong>
          {" "}
          <span class="font-mono text-slate-600">
            ({committedOption.topologySystemName} → {committedRouteKey})
          </span>
          <span class="ml-1 text-slate-500">— topology.name から自動</span>
        </span>
        {options.length > 1 && (
          <button
            type="button"
            class="btn-secondary px-2 py-0.5 text-[0.7rem]"
            onClick={() => setPickerOpen(true)}
          >
            別のページに切り替え
          </button>
        )}
      </div>
    );
  }

  return (
    <div class="mb-3 rounded border border-blue-200 bg-blue-50/40 p-3 text-xs">
      <p class="mb-1.5 font-semibold text-blue-900">
        編集するページを選択（topology.name から routeKey を自動派生）
      </p>
      {loading && <p class="text-slate-600">マニフェストを読み込み中…</p>}
      {loadError && <p class="text-red-700">読み込みエラー: {loadError}</p>}
      {!loading && options.length === 0 && !loadError && (
        <p class="text-slate-600">
          topology.name が登録済みのマニフェストが見つかりません。
          先に /admin/contents で Step 1 を完了させてください。
        </p>
      )}
      {options.length > 0 && (
        <>
          <label class="block">
            マニフェストを選択
            <select
              class="mt-1 w-full rounded border bg-white px-2 py-1 font-mono text-xs"
              value={committedRouteKey || options[0]?.derivedRouteKey || ""}
              onChange={(e) => {
                const key = (e.target as HTMLSelectElement).value;
                if (key) {
                  setPickerOpen(false);
                  onCommit(key);
                }
              }}
            >
              {options.map((o) => (
                <option key={o.manifestId} value={o.derivedRouteKey}>
                  {o.label} → {o.derivedRouteKey}
                </option>
              ))}
            </select>
          </label>
          <p class="mt-1 text-[0.7rem] text-blue-800">
            選択すると routeKey が自動確定し、パッケージが生成されます。
          </p>
          {committedRouteKey && pickerOpen && (
            <button
              type="button"
              class="btn-secondary mt-2 text-xs"
              onClick={() => setPickerOpen(false)}
            >
              キャンセル
            </button>
          )}
        </>
      )}
    </div>
  );
}

function BucketPackageRouteFields({
  routeKey,
  committedRouteKey,
  routeOptions,
  candidateErrors,
  onRouteKeyChange,
}: {
  routeKey: string;
  committedRouteKey: string;
  routeOptions: string[];
  candidateErrors: ValidationError[];
  onRouteKeyChange: (routeKey: string) => void;
}): JSX.Element {
  return (
    <div class="mt-3 rounded border border-slate-200 bg-slate-50 p-3">
      <p class="mb-2 text-xs font-semibold text-slate-700">
        既存パッケージのルート候補（移行用）
      </p>
      <label class="mb-2 flex flex-col gap-0.5 text-sm">
        候補から選択
        <select
          value={routeKey}
          onChange={(e) => {
            onRouteKeyChange((e.target as HTMLSelectElement).value);
          }}
          disabled={routeOptions.length === 0}
          class="input font-mono text-xs"
        >
          <option value="">— ルートを選択 —</option>
          {routeOptions.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
      </label>
      {routeOptions.length === 0 && candidateErrors.length === 0 && (
        <p class="mb-2 text-xs text-slate-600">
          登録済みパッケージのルート候補がありません。通常導線では上のマニフェスト選択から
          topology.name 派生のルートキーを確定してください。
        </p>
      )}
      {committedRouteKey && (
        <p class="mt-2 text-xs text-slate-600">
          使用中のルート: <code class="font-mono">{committedRouteKey}</code>
        </p>
      )}
    </div>
  );
}

/** Legacy/debug only: manual routeKey typing for migration — not part of normal authoring. */
function LegacyManualRouteInput({
  manualRouteDraft,
  committedRouteKey,
  routeKey,
  onManualRouteDraftChange,
  onManualRouteCommit,
}: {
  manualRouteDraft: string;
  committedRouteKey: string;
  routeKey: string;
  onManualRouteDraftChange: (draft: string) => void;
  onManualRouteCommit: () => void;
}): JSX.Element {
  const draftDiffers = manualRouteDraft.trim() !== committedRouteKey &&
    manualRouteDraft.trim() !== routeKey;

  const commitManualRoute = () => {
    const next = manualRouteDraft.trim();
    if (!next) return;
    onManualRouteCommit();
  };

  return (
    <div class="mt-3 rounded border border-dashed border-slate-300 bg-slate-50/80 p-3">
      <p class="mb-2 text-xs font-semibold text-slate-600">
        legacy / debug: 手動ルートキー入力（移行・互換のみ）
      </p>
      <label class="flex flex-col gap-0.5 text-sm">
        ルートキーを直接入力
        <div class="flex gap-2">
          <input
            value={manualRouteDraft}
            onInput={(e) =>
              onManualRouteDraftChange((e.target as HTMLInputElement).value)}
            onKeyDown={(e: KeyboardEvent) => {
              if (e.key === "Enter") {
                e.preventDefault();
                commitManualRoute();
              }
            }}
            placeholder="例: customer-management"
            class="input-mono min-w-0 flex-1 text-xs"
          />
          <button
            type="button"
            class="btn-secondary shrink-0 px-2 py-1 text-xs"
            disabled={!manualRouteDraft.trim()}
            onClick={commitManualRoute}
          >
            確定
          </button>
        </div>
      </label>
      {draftDiffers && (
        <p class="mt-1 text-xs text-amber-800">
          未確定: <code class="font-mono">{manualRouteDraft.trim()}</code>
          {" "}— Enter または「確定」で反映されます
        </p>
      )}
    </div>
  );
}

// ─── フローレイアウトキャンバス（FlowLayoutCanvas コンポーネント） ─────────────

function friendlyComponentLabel(componentKey: string): string {
  const parts = componentKey.split("/");
  return parts[parts.length - 1] ?? componentKey;
}

function friendlyNodeLabel(
  node: Pick<DraftNode, "componentKey" | "nodeKind" | "htmlTag">,
): string {
  if (node.nodeKind === "structural_html" && node.htmlTag) {
    return `<${node.htmlTag}>`;
  }
  return friendlyComponentLabel(node.componentKey);
}

type LayerTreeItem = { node: DraftNode; depth: number };

function buildLayerTreeItems(nodes: DraftNode[]): LayerTreeItem[] {
  const byParent = new Map<string | null, DraftNode[]>();
  for (const node of nodes) {
    const pk = node.parentNodeId ?? null;
    if (!byParent.has(pk)) byParent.set(pk, []);
    byParent.get(pk)!.push(node);
  }
  for (const ch of byParent.values()) {
    ch.sort((a, b) => a.orderIndex - b.orderIndex);
  }
  const items: LayerTreeItem[] = [];
  function walk(pid: string | null, depth: number) {
    for (const node of (byParent.get(pid) ?? [])) {
      items.push({ node, depth });
      walk(node.nodeId, depth + 1);
    }
  }
  walk(null, 0);
  return items;
}

function LayerTree({
  draftNodes,
  selectedNodeId,
  onSelect,
  onCopy,
  onDelete,
  embedded = false,
  onReparent,
}: {
  draftNodes: DraftNode[];
  selectedNodeId: string | null;
  onSelect: (nodeId: string) => void;
  onCopy: (nodeId: string) => void;
  onDelete: (nodeId: string) => void;
  embedded?: boolean;
  onReparent: (
    nodeId: string,
    newParentId: string | null,
    insertBeforeId: string | null,
  ) => void;
}): JSX.Element {
  const [draggedId, setDraggedId] = useState<string | null>(null);
  const [dropTarget, setDropTarget] = useState<
    { id: string; pos: "before" | "after" | "into" } | null
  >(null);

  const items = buildLayerTreeItems(draftNodes);

  const handleDragStart = (e: DragEvent, nodeId: string) => {
    e.dataTransfer?.setData("text/plain", nodeId);
    setDraggedId(nodeId);
  };

  const handleDragOver = (e: DragEvent, nodeId: string) => {
    e.preventDefault();
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const pct = (e.clientY - rect.top) / rect.height;
    const pos = pct < 0.33 ? "before" : pct > 0.67 ? "after" : "into";
    setDropTarget({ id: nodeId, pos });
  };

  const handleDrop = (e: DragEvent, targetId: string) => {
    e.preventDefault();
    const sourceId = e.dataTransfer?.getData("text/plain") ?? "";
    const pos = dropTarget?.pos ?? "after";
    setDropTarget(null);
    setDraggedId(null);
    if (!sourceId || sourceId === targetId) return;
    const target = draftNodes.find((n) => n.nodeId === targetId);
    if (!target) return;
    if (pos === "into") {
      onReparent(sourceId, targetId, null);
    } else {
      onReparent(
        sourceId,
        target.parentNodeId ?? null,
        pos === "before" ? targetId : null,
      );
    }
  };

  return (
    <div class={`${embedded ? "w-full" : "w-44 shrink-0"} rounded-lg border border-gray-200 bg-white`}>
      {!embedded && (
        <div class="border-b border-gray-200 px-2 py-1.5">
          <h4 class="text-xs font-semibold text-gray-600">
            レイヤー ({draftNodes.length})
          </h4>
          <p class="text-[0.6rem] text-gray-400">
            ドラッグで並び替え・入れ子変更
          </p>
        </div>
      )}
      <div
        role="treegrid"
        aria-label="レイヤーツリー"
        class="overflow-y-auto"
        style="max-height:300px;"
        onDragLeave={() => setDropTarget(null)}
      >
        {items.length === 0 && (
          <p class="px-2 py-4 text-center text-xs text-gray-400">なし</p>
        )}
        {items.map(({ node, depth }) => {
          const isSelected = node.nodeId === selectedNodeId;
          const isDragging = node.nodeId === draggedId;
          const isTarget = dropTarget?.id === node.nodeId;
          const dropPos = isTarget ? dropTarget?.pos : null;
          return (
            <div
              key={node.nodeId}
              role="row"
              aria-selected={isSelected}
              tabIndex={0}
              draggable
              onDragStart={(e: DragEvent) => handleDragStart(e, node.nodeId)}
              onDragOver={(e: DragEvent) => handleDragOver(e, node.nodeId)}
              onDrop={(e: DragEvent) => handleDrop(e, node.nodeId)}
              onDragEnd={() => {
                setDropTarget(null);
                setDraggedId(null);
              }}
              onClick={() => onSelect(node.nodeId)}
              onKeyDown={(e: KeyboardEvent) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  onSelect(node.nodeId);
                }
                if (e.key === "Delete") {
                  e.preventDefault();
                  onDelete(node.nodeId);
                }
              }}
              class={`relative flex cursor-pointer items-center gap-1 border-b border-gray-100 py-1 text-xs focus:outline-none focus-visible:ring-1 focus-visible:ring-blue-400 ${
                isSelected ? "bg-blue-50" : "hover:bg-gray-50"
              } ${isDragging ? "opacity-40" : ""} ${
                isTarget && dropPos === "into"
                  ? "outline outline-1 outline-blue-400"
                  : ""
              }`}
              style={`padding-left:${depth * 10 + 6}px;padding-right:4px;`}
            >
              {isTarget && dropPos === "before" && (
                <div class="pointer-events-none absolute left-0 top-0 h-0.5 w-full bg-blue-500" />
              )}
              {isTarget && dropPos === "after" && (
                <div class="pointer-events-none absolute bottom-0 left-0 h-0.5 w-full bg-blue-500" />
              )}
              <span
                class={`h-2 w-2 shrink-0 rounded-sm ${
                  node.isDraftOnly ? "bg-yellow-400" : "bg-blue-400"
                }`}
                aria-hidden="true"
              />
              <span
                class="flex-1 truncate font-mono text-[0.63rem]"
                title={node.nodeId}
              >
                {friendlyNodeLabel(node)}
              </span>
              <div class="flex shrink-0 gap-0.5">
                <button
                  type="button"
                  onClick={(e: Event) => {
                    e.stopPropagation();
                    onCopy(node.nodeId);
                  }}
                  class="rounded px-0.5 text-[0.65rem] text-gray-400 hover:text-gray-600 focus-visible:ring-1 focus-visible:ring-blue-400"
                  title="コピー"
                  aria-label={`${friendlyNodeLabel(node)}をコピー`}
                >
                  ⧉
                </button>
                <button
                  type="button"
                  onClick={(e: Event) => {
                    e.stopPropagation();
                    onDelete(node.nodeId);
                  }}
                  class="rounded px-0.5 text-[0.65rem] text-red-400 hover:text-red-600 focus-visible:ring-1 focus-visible:ring-red-400"
                  title="削除"
                  aria-label={`${friendlyNodeLabel(node)}を削除`}
                >
                  ✕
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// Gap 4: Friendly field labels for CanvasInspector
const FIELD_LABELS: Record<string, string> = {
  x: "左端の位置 (px)",
  y: "上端の位置 (px)",
  width: "幅 (px / % / auto)",
  height: "高さ (px / % / auto)",
};

const DISCLOSURE_COMPONENT_KINDS = new Set([
  "disclosure/modal",
  "table_op/row_detail_drawer",
  "inline_edit/audit_diff_drawer",
  "safety_guard/apply_confirm_dialog",
  "safety_guard/command_palette",
  "search_suggest/select_import_dialog",
]);

function isDisclosureKind(componentKind?: string): boolean {
  if (!componentKind) return false;
  return DISCLOSURE_COMPONENT_KINDS.has(componentKind);
}

function CanvasInspector({
  node,
  draftNodes,
  slotKeyCandidates,
  onUpdate,
  onCommit,
  onToggleLayoutClassRef,
  onCopy,
  onEditDesign,
  onClose,
  embedded = false,
}: {
  node: DraftNode;
  draftNodes: DraftNode[];
  slotKeyCandidates: string[];
  onUpdate: (updates: Partial<DraftNode>) => void;
  onCommit: (updates: Partial<DraftNode>, label: string) => void;
  onToggleLayoutClassRef: (classKey: string) => void;
  onCopy: () => void;
  onEditDesign?: () => void;
  onClose: () => void;
  embedded?: boolean;
}): JSX.Element {
  const [manualSlotKey, setManualSlotKey] = useState("");
  const [parentCycleError, setParentCycleError] = useState<string | null>(null);

  const parentOptions = draftNodes.filter((n) => n.nodeId !== node.nodeId);
  const isContainer = isLayoutContainerNode(node, draftNodes);

  const handleParentChange = (value: string) => {
    const parentId = value || null;
    if (parentId && wouldCreateParentCycle(draftNodes, node.nodeId, parentId)) {
      setParentCycleError(
        "その親を選択すると循環参照になります。別のノードを選択してください。",
      );
      return;
    }
    setParentCycleError(null);
    onCommit({ parentNodeId: parentId }, "親部品を変更");
  };

  const handleNum = (
    field: "gridCol" | "gridRow",
    raw: string,
    commit = false,
  ) => {
    const v = parseInt(raw, 10);
    if (isNaN(v)) return;
    const min = field === "gridCol" ? 1 : 0;
    const final = Math.max(min, v);
    if (commit) {
      onCommit(
        { [field]: final } as Partial<DraftNode>,
        `${FIELD_LABELS[field] ?? field}を変更`,
      );
    } else {
      onUpdate({ [field]: final } as Partial<DraftNode>);
    }
  };

  const handleDimension = (
    field: "width" | "height",
    raw: string,
    applySnap = false,
    commit = false,
  ) => {
    const parsed = parseLayoutDimensionInput(raw);
    if (parsed === null) return;
    let final: LayoutDimension = parsed;
    if (typeof parsed === "number") {
      const min = field === "width" ? 40 : 30;
      const clamped = Math.max(min, parsed);
      final = applySnap ? snapToGrid(clamped, SNAP_SIZE) : clamped;
    }
    if (commit) {
      onCommit(
        { [field]: final },
        `${FIELD_LABELS[field]}を変更`,
      );
    } else {
      onUpdate({ [field]: final });
    }
  };

  const overviewTab = (
    <div class="space-y-2">
      <div class="rounded border border-blue-200 bg-blue-50/50 p-1.5">
        <div class="font-bold text-blue-900">{friendlyNodeLabel(node)}</div>
        {node.isDraftOnly && (
          <div class="mt-0.5 text-[0.65rem] font-medium text-yellow-700">
            ⚠ まだ使えない部品 — 先に部品登録を完了してください
          </div>
        )}
        <details class="mt-1">
          <summary class="cursor-pointer text-[0.6rem] text-gray-400">
            技術情報
          </summary>
          <code class="text-[0.6rem] text-gray-500 break-all">
            {node.componentKey}
          </code>
        </details>
      </div>
      <fieldset>
        <legend class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
          サイズ
        </legend>
        {node.widthMode === "preset" && (
          <p class="mb-1 rounded bg-blue-50 px-1.5 py-0.5 text-[0.6rem] text-blue-700">
            幅は sizing classRef が制御しています（preset モード）。カスタム幅入力は無効です。
          </p>
        )}
        <div class="grid grid-cols-2 gap-1">
          {(["width", "height"] as const).map((f) => {
            const modeField = f === "width" ? node.widthMode : node.heightMode;
            const isPreset = modeField === "preset";
            return (
              <label key={f} class="flex flex-col gap-0.5">
                <span class="text-[0.65rem] text-gray-600">
                  {FIELD_LABELS[f]}
                  {modeField && modeField !== "custom" && (
                    <span class="ml-1 font-mono text-[0.55rem] text-gray-400">
                      [{modeField}]
                    </span>
                  )}
                </span>
                <input
                  type="text"
                  inputMode="decimal"
                  value={isPreset ? "" : layoutDimensionLabel(node[f])}
                  placeholder={
                    isPreset
                      ? "sizing classRef が制御"
                      : f === "width" ? "140 / 50% / auto" : "60 / 100% / auto"
                  }
                  disabled={isPreset}
                  onInput={(e) =>
                    handleDimension(
                      f,
                      (e.target as HTMLInputElement).value,
                      true,
                      false,
                    )}
                  onChange={(e) =>
                    handleDimension(
                      f,
                      (e.target as HTMLInputElement).value,
                      true,
                      true,
                    )}
                  class={`input px-1 py-0.5 ${isPreset ? "cursor-not-allowed opacity-50" : ""}`}
                  aria-label={FIELD_LABELS[f]}
                />
              </label>
            );
          })}
        </div>
      </fieldset>
      <div class="flex flex-wrap gap-1">
        <button type="button" class="btn-secondary text-xs" onClick={onCopy}>
          コピー
        </button>
        {onEditDesign && (
          <button
            type="button"
            class="btn-secondary text-xs"
            onClick={onEditDesign}
          >
            デザインを編集
          </button>
        )}
      </div>
    </div>
  );

  const treeTab = (
    <fieldset class="flex flex-col gap-1.5">
      <legend class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
        ツリー配置
      </legend>
      <label class="flex flex-col gap-0.5">
        <span class="text-[0.65rem] text-gray-600">親部品</span>
        <select
          value={node.parentNodeId ?? ""}
          onChange={(e) =>
            handleParentChange((e.target as HTMLSelectElement).value)}
          class="input px-1 py-0.5 text-xs"
          aria-label="親部品を選択"
        >
          <option value="">(なし — トップレベル)</option>
          {parentOptions.map((n) => {
            const cyclic = wouldCreateParentCycle(
              draftNodes,
              node.nodeId,
              n.nodeId,
            );
            return (
              <option key={n.nodeId} value={n.nodeId} disabled={cyclic}>
                {friendlyComponentLabel(n.componentKey)}
                {cyclic ? " (循環参照になるため不可)" : ""}
              </option>
            );
          })}
        </select>
      </label>
      {parentCycleError && (
        <p
          class="m-0 rounded bg-red-50 px-1.5 py-1 text-red-600"
          role="alert"
        >
          {parentCycleError}
        </p>
      )}
      <label class="flex flex-col gap-0.5">
        <span class="text-[0.65rem] text-gray-600">配置スロット</span>
        <select
          value={node.slotKey}
          onChange={(e) =>
            onCommit(
              { slotKey: (e.target as HTMLSelectElement).value },
              "配置スロットを変更",
            )}
          class="input px-1 py-0.5 text-xs"
          aria-label="配置スロットを選択"
        >
          {slotKeyCandidates.map((sk) => (
            <option key={sk || "__empty__"} value={sk}>
              {sk || "(デフォルト)"}
            </option>
          ))}
        </select>
      </label>
      <AdvancedManualOverride title="カスタムスロットを直接入力">
        <div class="flex gap-1">
          <input
            value={manualSlotKey}
            onInput={(e) =>
              setManualSlotKey((e.target as HTMLInputElement).value)}
            placeholder="スロット名を入力"
            class="input-mono flex-1 px-1 py-0.5 text-xs"
            aria-label="カスタム配置スロット名"
          />
          <button
            type="button"
            class="btn-secondary text-xs"
            onClick={() => {
              onCommit({ slotKey: manualSlotKey }, "カスタムスロットを設定");
              setManualSlotKey("");
            }}
          >
            適用
          </button>
        </div>
      </AdvancedManualOverride>
      <label class="flex flex-col gap-0.5">
        <span class="text-[0.65rem] text-gray-600">表示順 (orderIndex)</span>
        <input
          type="number"
          min={0}
          value={node.orderIndex}
          onInput={(e) => {
            const v = parseInt((e.target as HTMLInputElement).value, 10);
            if (!isNaN(v) && v >= 0) {
              onCommit(
                { orderIndex: v },
                "orderIndexを変更",
              );
            }
          }}
          class="input px-1 py-0.5"
          aria-label="orderIndex (表示順)"
        />
      </label>
      <AdvancedManualOverride title="グリッド位置（フロープレビューに影響しません）">
        <p class="text-muted-xs mb-1">
          gridCol / gridRow はフローレイアウトのプレビューには反映されません。レガシー補助項目です。
        </p>
        <div class="grid grid-cols-2 gap-1">
          <label class="flex flex-col gap-0.5">
            <span class="text-[0.65rem] text-gray-600">列 (1〜12)</span>
            <input
              type="number"
              min={1}
              max={12}
              value={node.gridCol}
              onInput={(e) =>
                handleNum(
                  "gridCol",
                  (e.target as HTMLInputElement).value,
                  false,
                )}
              onChange={(e) =>
                handleNum(
                  "gridCol",
                  (e.target as HTMLInputElement).value,
                  true,
                )}
              class="input px-1 py-0.5"
              aria-label="グリッド列 (1〜12)"
            />
          </label>
          <label class="flex flex-col gap-0.5">
            <span class="text-[0.65rem] text-gray-600">行</span>
            <input
              type="number"
              min={1}
              value={node.gridRow}
              onInput={(e) =>
                handleNum(
                  "gridRow",
                  (e.target as HTMLInputElement).value,
                  false,
                )}
              onChange={(e) =>
                handleNum(
                  "gridRow",
                  (e.target as HTMLInputElement).value,
                  true,
                )}
              class="input px-1 py-0.5"
              aria-label="グリッド行"
            />
          </label>
        </div>
      </AdvancedManualOverride>
    </fieldset>
  );

  const classTab = (
    <fieldset class="flex flex-col gap-1.5">
      <legend class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
        {isContainer ? "layoutClassRefs（コンテナ）" : "layoutClassRefs（部品ラッパー）"}
      </legend>
      {isContainer ? (
        <TopologyLayoutClassPicker
          selectedClassRefs={node.layoutClassRefs ?? []}
          onToggle={onToggleLayoutClassRef}
          scopeFilter=""
          allowedForAny={["layout_root", "layout_section", "layout_row"]}
        />
      ) : (
        <TopologyLayoutClassPicker
          selectedClassRefs={node.layoutClassRefs ?? []}
          onToggle={onToggleLayoutClassRef}
          scopeFilter=""
          allowedForFilter="component_wrapper"
        />
      )}
    </fieldset>
  );

  return (
    <div
      role="complementary"
      aria-label={`${friendlyNodeLabel(node)} の${UX_LAYOUT_INSPECTOR_SECTION}`}
      class={`${embedded ? "w-full" : "w-52 shrink-0"} rounded-lg border border-blue-600 bg-blue-50 p-2.5 font-mono text-xs`}
    >
      <div class="mb-2 flex items-center justify-between">
        <strong class="text-sm">{UX_LAYOUT_INSPECTOR_SECTION}</strong>
        <button
          type="button"
          onClick={onClose}
          class="btn-secondary px-1.5 py-0 text-xs"
          aria-label="プロパティパネルを閉じる"
        >
          ✕
        </button>
      </div>

      <InspectorTabPanel
        key={node.nodeId}
        ariaLabel={UX_LAYOUT_INSPECTOR_SECTION}
        tabs={[
          { id: "overview", label: "概要", content: overviewTab },
          { id: "tree", label: "ツリー", content: treeTab },
          { id: "class", label: "クラス", content: classTab },
        ]}
      />
    </div>
  );
}

// ─── レスポンシブトークンルールエディター ──────────────────────────────────────

const BREAKPOINT_LABELS: Record<string, string> = {
  sm: "sm (640px〜)",
  md: "md (768px〜)",
  lg: "lg (1024px〜)",
  xl: "xl (1280px〜)",
};

function ResponsiveTokenRuleEditor({
  rules,
  onChange,
}: {
  rules: ResponsiveTokenRules;
  onChange: (rules: ResponsiveTokenRules) => void;
}): JSX.Element {
  const [activeBreakpoint, setActiveBreakpoint] = useState<string>(
    RESPONSIVE_BREAKPOINTS[1],
  );

  const toggleToken = (bp: string, tokenKey: string) => {
    const current = rules[bp] ?? [];
    const next = current.includes(tokenKey)
      ? current.filter((k) => k !== tokenKey)
      : [...current, tokenKey];
    onChange({ ...rules, [bp]: next });
  };

  const clearBreakpoint = (bp: string) => {
    const next = { ...rules };
    delete next[bp];
    onChange(next);
  };

  const activeTokens = rules[activeBreakpoint] ?? [];
  const hasRules = Object.values(rules).some((v) => v && v.length > 0);

  return (
    <div>
      <p class="text-muted-xs mb-2">
        画面幅ごとに異なるCSSトークンを設定します。未設定のブレークポイントはデフォルト（全体設定）のトークンを使用します。
      </p>

      {/* Breakpoint selector */}
      <div
        class="mb-2 flex flex-wrap gap-1"
        role="tablist"
        aria-label="ブレークポイント選択"
      >
        {RESPONSIVE_BREAKPOINTS.map((bp) => {
          const count = rules[bp]?.length ?? 0;
          const isActive = bp === activeBreakpoint;
          return (
            <button
              key={bp}
              type="button"
              role="tab"
              aria-selected={isActive}
              aria-controls={`responsive-panel-${bp}`}
              onClick={() => setActiveBreakpoint(bp)}
              class={`rounded px-2.5 py-1 text-xs font-mono transition-colors focus-visible:ring-2 focus-visible:ring-blue-400 ${
                isActive
                  ? "bg-blue-600 text-white"
                  : count > 0
                  ? "border border-blue-300 bg-blue-50 text-blue-700"
                  : "border border-gray-200 bg-gray-50 text-gray-600 hover:bg-gray-100"
              }`}
              title={BREAKPOINT_LABELS[bp]}
            >
              {bp}
              {count > 0 && (
                <span
                  class={`ml-1 rounded-full px-1 text-[0.6rem] font-bold ${
                    isActive
                      ? "bg-blue-400 text-white"
                      : "bg-blue-200 text-blue-800"
                  }`}
                >
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Per-breakpoint token picker */}
      <div
        id={`responsive-panel-${activeBreakpoint}`}
        role="tabpanel"
        aria-label={`${
          BREAKPOINT_LABELS[activeBreakpoint] ?? activeBreakpoint
        } のトークン設定`}
      >
        <div class="mb-1 flex items-center justify-between">
          <span class="text-xs font-medium text-gray-700">
            {BREAKPOINT_LABELS[activeBreakpoint] ?? activeBreakpoint} のトークン
            {activeTokens.length > 0 && (
              <span class="ml-1 text-blue-600">
                ({activeTokens.length} 件選択)
              </span>
            )}
          </span>
          {activeTokens.length > 0 && (
            <button
              type="button"
              onClick={() => clearBreakpoint(activeBreakpoint)}
              class="text-[0.65rem] text-red-500 hover:text-red-700"
              aria-label={`${activeBreakpoint} のトークン設定をクリア`}
            >
              クリア
            </button>
          )}
        </div>
        <CssTokenPicker
          selectedTokenRefs={activeTokens}
          onToggle={(key) => toggleToken(activeBreakpoint, key)}
        />
      </div>

      {/* Configured rules summary */}
      {hasRules && (
        <div class="mt-2 rounded border border-blue-200 bg-blue-50 p-2">
          <strong class="text-xs text-blue-800">
            設定済みブレークポイント
          </strong>
          <ul class="mt-1 space-y-0.5 pl-0">
            {RESPONSIVE_BREAKPOINTS.filter((bp) => (rules[bp]?.length ?? 0) > 0)
              .map((bp) => (
                <li key={bp} class="flex items-start gap-2 text-xs">
                  <code class="shrink-0 font-mono text-blue-700">{bp}:</code>
                  <span class="flex-1 text-gray-600 break-all">
                    {rules[bp]!.join(", ")}
                  </span>
                  <button
                    type="button"
                    onClick={() => clearBreakpoint(bp)}
                    class="shrink-0 text-red-400 hover:text-red-600"
                    aria-label={`${bp} をクリア`}
                  >
                    ✕
                  </button>
                </li>
              ))}
          </ul>
        </div>
      )}
    </div>
  );
}

// ─── パレット ─────────────────────────────────────────────────────────────────

function LayoutPalette({
  onDragStart,
  onAddToCanvas,
  entries,
  status,
  packageOnly = false,
}: {
  onDragStart: (entry: PaletteEntry, payload: BucketCardDragPayload) => void;
  onAddToCanvas: (entry: PaletteEntry) => void;
  entries: PaletteEntry[];
  status: string | null;
  /** パッケージスコープ時はプロモート済みのみ（ドラフト catalog 非表示） */
  packageOnly?: boolean;
}): JSX.Element {
  const [filter, setFilter] = useState("");
  const scopeEntries = packageOnly
    ? entries.filter((e) => !e.isDraftOnly)
    : entries;
  const filtered = filter
    ? scopeEntries.filter((e) =>
      e.componentKey.toLowerCase().includes(filter.toLowerCase()) ||
      e.componentKind.toLowerCase().includes(filter.toLowerCase())
    )
    : scopeEntries;

  return (
    <div class="flex min-h-0 flex-1 flex-col">
      <input
        value={filter}
        onInput={(e) => setFilter((e.target as HTMLInputElement).value)}
        placeholder="絞り込み..."
        class="mb-1 w-full rounded border border-gray-300 px-2 py-1 text-xs focus:border-blue-400 focus:outline-none"
        aria-label="パレットの部品を絞り込み"
      />
      <p class="mb-1.5 text-[0.62rem] text-gray-500">
        {UX_COMPONENT_BUCKET_CARD_DRAG_HINT}
      </p>
      {status && <p class="text-[0.62rem] text-gray-400">{status}</p>}
      {filtered.length === 0 && (
        <p class="py-3 text-center text-[0.65rem] text-gray-400">該当なし</p>
      )}
      <div class="component-bucket-panel flex min-h-0 flex-1 flex-col gap-1.5 overflow-y-auto" role="list">
        {filtered.map((c) => {
          const draftOnly = c.isDraftOnly;
          const catalogEntry = COMPONENT_CATALOG_ENTRIES.find(
            (e) => e.componentKey === c.componentKey,
          );
          return (
            <ComponentBucketCard
              key={c.componentKey}
              componentKey={c.componentKey}
              componentKind={c.componentKind}
              sourcePath={catalogEntry?.sourcePath}
              statusLabel="カタログ"
              statusVariant="info"
              draggable
              placementReady
              dragPayload={bucketCardDragPayloadFromEntry(
                c,
                draftOnly ? "未配置可能" : "配置可能",
              )}
              onDragStart={(_e, payload) => onDragStart(c, payload)}
              onAddToCanvas={() => onAddToCanvas(c)}
            />
          );
        })}
      </div>
    </div>
  );
}

// Dashboard component candidates — catalog entries tagged dashboard_placement_candidate.
// These are implemented components (registrationRequired:false) that can be placed on dashboard
// layouts without going through the DB bucket registration flow.
// NOT a UIBuilder preset_ecosystem permanent child surface.
function DashboardCandidatePalette({
  onDragStart,
  onAddToCanvas,
  disabled = false,
}: {
  onDragStart: (entry: PaletteEntry, payload: BucketCardDragPayload) => void;
  onAddToCanvas: (entry: PaletteEntry) => void;
  disabled?: boolean;
}): JSX.Element {
  const [filter, setFilter] = useState("");
  const entries: PaletteEntry[] = COMPONENT_CATALOG_ENTRIES
    .filter((c) => c.capabilityTags.includes("dashboard_placement_candidate"))
    .map((c) => ({
      componentKey: c.componentKey,
      componentKind: c.componentKind,
      isDraftOnly: false,
    }));
  const filtered = filter
    ? entries.filter((e) =>
      e.componentKey.toLowerCase().includes(filter.toLowerCase()) ||
      e.componentKind.toLowerCase().includes(filter.toLowerCase())
    )
    : entries;

  if (entries.length === 0) return <></>;

  return (
    <div
      class="flex min-h-0 flex-1 flex-col"
      data-dashboard-candidate-palette="true"
    >
      <input
        value={filter}
        onInput={(e) => setFilter((e.target as HTMLInputElement).value)}
        placeholder="絞り込み..."
        class="mb-1 w-full rounded border border-blue-300 px-2 py-1 text-xs focus:border-blue-500 focus:outline-none"
        aria-label={`${UX_DASHBOARD_PRESET_CANDIDATE_LABEL}を絞り込み`}
      />
      <p class="mb-1.5 text-[0.62rem] text-blue-700">
        {UX_DASHBOARD_PRESET_CANDIDATE_DESCRIPTION}
      </p>
      {filtered.length === 0 && (
        <p class="py-3 text-center text-[0.65rem] text-blue-400">該当なし</p>
      )}
      <div class="component-bucket-panel flex min-h-0 flex-1 flex-col gap-1.5 overflow-y-auto" role="list">
        {filtered.map((c) => {
          const catalogEntry = COMPONENT_CATALOG_ENTRIES.find(
            (e) => e.componentKey === c.componentKey,
          );
          return (
            <ComponentBucketCard
              key={c.componentKey}
              componentKey={c.componentKey}
              componentKind={c.componentKind}
              sourcePath={catalogEntry?.sourcePath}
              statusLabel="preset 候補"
              statusVariant="info"
              draggable={!disabled}
              placementReady
              dragPayload={bucketCardDragPayloadFromEntry(c, UX_DASHBOARD_PRESET_CANDIDATE_LABEL)}
              onDragStart={(_e, payload) => onDragStart(c, payload)}
              onAddToCanvas={() => onAddToCanvas(c)}
            />
          );
        })}
      </div>
    </div>
  );
}

function StructuralHtmlPalette({
  onAddTag,
  disabled = false,
}: {
  onAddTag: (tag: StructuralHtmlTag) => void;
  disabled?: boolean;
}): JSX.Element {
  const [filter, setFilter] = useState("");
  const filtered = filter
    ? STRUCTURAL_HTML_TAG_ALLOWLIST.filter((tag) =>
      tag.toLowerCase().includes(filter.toLowerCase())
    )
    : STRUCTURAL_HTML_TAG_ALLOWLIST;

  return (
    <div class="flex min-h-0 flex-1 flex-col">
      <input
        value={filter}
        onInput={(e) => setFilter((e.target as HTMLInputElement).value)}
        placeholder="絞り込み..."
        class="mb-1 w-full rounded border border-emerald-300 px-2 py-1 text-xs focus:border-emerald-500 focus:outline-none"
        aria-label="構造 HTML タグを絞り込み"
      />
      <p class="mb-1.5 text-[0.62rem] text-emerald-800">
        SSOT 許可タグを layout ノードとして追加します。
      </p>
      {filtered.length === 0 && (
        <p class="py-3 text-center text-[0.65rem] text-emerald-500">該当なし</p>
      )}
      <div class="component-bucket-panel flex min-h-0 flex-1 flex-col gap-1.5 overflow-y-auto" role="list">
        {filtered.map((tag) => (
          <button
            key={tag}
            type="button"
            disabled={disabled}
            onClick={() => onAddTag(tag)}
            class="component-bucket-card flex items-center gap-2 rounded-lg border border-emerald-300 bg-white p-2 text-left shadow-sm hover:bg-emerald-100 disabled:opacity-40"
          >
            <span
              class="component-bucket-card__icon flex h-8 w-8 shrink-0 items-center justify-center rounded-md border border-emerald-200 bg-emerald-50 font-mono text-xs text-emerald-900"
              aria-hidden="true"
            >
              &lt;{tag.slice(0, 1)}&gt;
            </span>
            <span class="min-w-0 flex-1">
              <span class="block truncate font-mono text-xs font-semibold text-emerald-950">&lt;{tag}&gt;</span>
              <span class="block truncate text-[0.58rem] text-emerald-800">構造 HTML</span>
            </span>
          </button>
        ))}
      </div>
    </div>
  );
}

// ─── 左 docked panel — 部品追加パネル（SSOT: canvas_workspace_contract.left_panel）──

/** Left docked panel integrating component_bucket / dashboard preset candidate / structural HTML
 * as tabs within a single unified 部品追加 area. */
function LeftDockedPalettePanel({
  onDragStart,
  onAddToCanvas,
  paletteEntries,
  paletteStatus,
  onAddStructuralHtmlTag,
  selectorsDisabled,
}: {
  onDragStart: (entry: PaletteEntry, payload: BucketCardDragPayload) => void;
  onAddToCanvas: (entry: PaletteEntry) => void;
  paletteEntries: PaletteEntry[];
  paletteStatus: string | null;
  onAddStructuralHtmlTag: (tag: StructuralHtmlTag) => void;
  selectorsDisabled: boolean;
}): JSX.Element {
  const [activeTab, setActiveTab] = useState<"bucket" | "dashboard" | "html">("bucket");
  const tabs: Array<{ id: "bucket" | "dashboard" | "html"; label: string }> = [
    { id: "bucket", label: "配置可能部品" },
    { id: "dashboard", label: "preset 候補" },
    { id: "html", label: "構造 HTML" },
  ];

  return (
    <aside
      class="left-docked-panel flex shrink-0 flex-col overflow-hidden rounded-lg border border-slate-200 bg-slate-50"
      style={{ width: "clamp(180px, 15vw, 240px)", minWidth: "180px", maxWidth: "240px" }}
      aria-label={UX_COMPONENT_ADD_PANEL_LABEL}
      data-component-add-panel="true"
    >
      <div
        class="flex shrink-0 border-b border-slate-200 bg-white"
        role="tablist"
        aria-label={`${UX_COMPONENT_ADD_PANEL_LABEL}タブ`}
      >
        {tabs.map(({ id, label }) => (
          <button
            key={id}
            type="button"
            role="tab"
            aria-selected={activeTab === id}
            aria-controls={`left-panel-tab-${id}`}
            id={`left-panel-tabbutton-${id}`}
            class={`flex-1 border-r border-slate-200 px-1 py-1.5 text-[0.58rem] font-semibold transition-colors last:border-r-0 ${
              activeTab === id
                ? "bg-white text-blue-700 shadow-[inset_0_-2px_0_#3b82f6]"
                : "bg-slate-50 text-slate-600 hover:bg-slate-100 hover:text-slate-800"
            }`}
            onClick={() => setActiveTab(id)}
          >
            {label}
          </button>
        ))}
      </div>
      <div
        id={`left-panel-tab-${activeTab}`}
        role="tabpanel"
        aria-labelledby={`left-panel-tabbutton-${activeTab}`}
        class="min-h-0 flex-1 overflow-y-auto p-2"
      >
        {activeTab === "bucket" && (
          <LayoutPalette
            onDragStart={onDragStart}
            onAddToCanvas={onAddToCanvas}
            entries={paletteEntries}
            status={paletteStatus}
            packageOnly={true}
          />
        )}
        {activeTab === "dashboard" && (
          <DashboardCandidatePalette
            onDragStart={onDragStart}
            onAddToCanvas={onAddToCanvas}
            disabled={selectorsDisabled}
          />
        )}
        {activeTab === "html" && (
          <StructuralHtmlPalette
            onAddTag={onAddStructuralHtmlTag}
            disabled={selectorsDisabled}
          />
        )}
      </div>
    </aside>
  );
}

// ─── レイアウトビルダーセクション v2 + UX強化 ─────────────────────────────────

function LayoutBuilderSection({
  scopedPackageId,
  scopedRouteKey,
  scopedLayoutId,
  routeCanvasReady = false,
  onRegisterComponentBeforePlace,
  onDetachComponentAfterRemove,
  paletteReloadToken = 0,
}: {
  scopedPackageId?: string;
  scopedRouteKey?: string | null;
  scopedLayoutId?: string | null;
  routeCanvasReady?: boolean;
  onRegisterComponentBeforePlace?: (componentKey: string) => Promise<boolean>;
  onDetachComponentAfterRemove?: (componentKey: string) => Promise<void>;
  paletteReloadToken?: number;
}): JSX.Element {
  const { confirm, ConfirmDialogHost } = useConfirm();
  // ── route/layout selection ───────────────────────────────────────────────
  const [layoutId, setLayoutId] = useState("");
  const [routeKey, setRouteKey] = useState("");
  const [manualLayoutId, setManualLayoutId] = useState("");
  const [manualRouteKey, setManualRouteKey] = useState("");

  // ── canvas draft state ───────────────────────────────────────────────────
  const [draftNodes, setDraftNodes] = useState<DraftNode[]>([]);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [leftDrawerOpen, setLeftDrawerOpen] = useState(false);
  const [rightDrawerOpen, setRightDrawerOpen] = useState(false);

  // Gap 1: Lifecycle state machine
  const [lifecyclePhase, setLifecyclePhase] = useState<LifecyclePhase>("idle");

  // Gap 2: Undo/redo history
  const historyRef = useRef<HistorySnapshot[]>([{
    nodes: [],
    label: "初期状態",
  }]);
  const historyPtrRef = useRef(0);
  const [canUndo, setCanUndo] = useState(false);
  const [canRedo, setCanRedo] = useState(false);

  // Gap 6: ARIA live region message
  const [liveAnnouncement, setLiveAnnouncement] = useState("");

  const canvasRef = useRef<HTMLDivElement | null>(null);
  const [designDraftByNodeId, setDesignDraftByNodeId] = useState<
    Map<string, DesignDraft>
  >(new Map());
  const [legacyLayoutWarning, setLegacyLayoutWarning] = useState(false);

  // ── layout class refs (placement); design tokens are selected-node inspector state ─
  const [selectedLayoutClassRefs, setSelectedLayoutClassRefs] = useState<
    string[]
  >([]);
  const [layoutClassRefError] = useState<string | null>(null);

  // ── patch / status ───────────────────────────────────────────────────────
  const [patchSummary, setPatchSummary] = useState<LayoutPatchSummary | null>(
    null,
  );
  const [patchErrors, setPatchErrors] = useState<
    { code: string; message: string }[]
  >([]);
  const [debugJson, setDebugJson] = useState<string | null>(null);
  const [layoutApplyModalOpen, setLayoutApplyModalOpen] = useState(false);
  const [layoutApplyModalPhase, setLayoutApplyModalPhase] = useState<
    LayoutPatchApplyModalPhase
  >("validating");
  const lifecycleRef = useRef<HTMLDivElement | null>(null);
  const [loading, setLoading] = useState(false);

  // ── palette / layout candidates ──────────────────────────────────────────
  const [paletteEntries, setPaletteEntries] = useState<PaletteEntry[]>([]);
  const [paletteStatus, setPaletteStatus] = useState<string | null>(null);
  const [layoutCandidates, setLayoutCandidates] = useState<
    LayoutRouteCandidate[]
  >([]);
  const [candidateErrors, setCandidateErrors] = useState<ValidationError[]>([]);
  const [paletteLoadFailed, setPaletteLoadFailed] = useState(false);

  // ── palette drag (HTML5 drag API — palette→canvas only) ─────────────────
  const dragSrc = useRef<DragSrc | null>(null);

  // ── preset intake state ──────────────────────────────────────────────────
  const [presetDrawerOpen, setPresetDrawerOpen] = useState(false);
  const [canvasPresetSeed, setCanvasPresetSeed] = useState<CanvasPresetSeed | null>(null);
  const [savedPresets, setSavedPresets] = useState<MockPresetListItem[]>([]);
  const [selectedPresetId, setSelectedPresetId] = useState<string>("");
  const [presetLoadStatus, setPresetLoadStatus] = useState<"idle" | "loading" | "error">("idle");
  const [presetLoadError, setPresetLoadError] = useState<string | null>(null);
  const [presetsLoaded, setPresetsLoaded] = useState(false);

  // ── frontend-local calc bindings ─────────────────────────────────────────
  const [calculationBindings, setCalculationBindings] = useState<CalcBinding[]>([]);
  const [nodeValues, setNodeValues] = useState<Record<string, Record<string, unknown>>>({});
  const [calcResults, setCalcResults] = useState<ReadonlyMap<string, { targetNodeId: string; targetProp: string; result: { ok: true; value: number } | { ok: false; error: string } }>>(new Map());
  const lastEmissionDataRef = useRef<Record<string, unknown>>({});
  const [emissionDataJson, setEmissionDataJson] = useState<string>("");

  useEffect(() => {
    if (!emissionDataJson.trim()) {
      lastEmissionDataRef.current = {};
      return;
    }
    try {
      const parsed = JSON.parse(emissionDataJson);
      if (typeof parsed === "object" && parsed !== null && !Array.isArray(parsed)) {
        lastEmissionDataRef.current = parsed as Record<string, unknown>;
      }
    } catch {
      // keep previous value on parse error
    }
  }, [emissionDataJson]);

  // ── derived ─────────────────────────────────────────────────────────────
  const paletteSeedEntries: PaletteDraftSeedEntry[] = paletteEntries.map((
    e,
  ) => ({
    componentKey: e.componentKey,
    componentKind: e.componentKind,
    isDraftOnly: e.isDraftOnly,
    componentId: e.componentId,
    packageId: e.packageId,
    layoutId: e.layoutId,
    wiringId: e.wiringId,
    tensorId: e.tensorId,
  }));
  const tensorPatchJson = buildVisualLayoutPatchJson(
    enrichDraftNodesWithPaletteComponentIds(draftNodes, paletteSeedEntries),
    selectedLayoutClassRefs,
    calculationBindings,
  );
  const effectiveLayoutId = manualLayoutId.trim() || layoutId;
  const effectiveRouteKey = manualRouteKey.trim() || routeKey;
  const selectedLayout = layoutCandidates.find(
    (c) => c.layoutId === effectiveLayoutId && c.routeKey === effectiveRouteKey,
  );
  const dbSlotKeys = selectedLayout?.slotKeys ?? [];
  const slotKeyCandidates = buildSlotKeyCandidates(draftNodes, dbSlotKeys);
  const packageScopedLayout = routeCanvasReady ||
    Boolean(scopedRouteKey?.trim());
  const packageAuthoringReady = Boolean(scopedPackageId?.trim()) &&
    packageScopedLayout;
  const displayCandidates =
    packageScopedLayout && scopedRouteKey && scopedLayoutId
      ? layoutCandidates.filter(
        (c) => c.routeKey === scopedRouteKey && c.layoutId === scopedLayoutId,
      )
      : layoutCandidates;
  const layoutSelectorsLocked = packageScopedLayout &&
    Boolean(scopedRouteKey && scopedLayoutId);
  const selectorsDisabled = candidateErrors.length > 0 || paletteLoadFailed ||
    !packageAuthoringReady;
  const canPatch = packageAuthoringReady &&
    Boolean(effectiveLayoutId && effectiveRouteKey);
  const routeOptions = uniqueRouteKeys(layoutCandidates);

  // ── frontend-local calc binding handlers & derived ────────────────────────
  const handleNodeValueChange = useCallback(
    (nodeId: string, propKey: string, value: unknown) => {
      setNodeValues((prev) => {
        const updated = {
          ...prev,
          [nodeId]: { ...(prev[nodeId] ?? {}), [propKey]: value },
        };
        // Re-evaluate all calc bindings — no backend dispatch
        if (calculationBindings.length > 0) {
          const ctx = {
            nodeValues: updated,
            emissionData: lastEmissionDataRef.current,
          };
          const results = evaluateAllCalcBindings(calculationBindings, ctx);
          setCalcResults(results);
        }
        return updated;
      });
    },
    [calculationBindings],
  );

  const calcTriggerNodeIds = useMemo(() => {
    const ids = new Set<string>();
    for (const binding of calculationBindings) {
      for (const source of Object.values(binding.variables)) {
        if (source.kind === "node") ids.add(source.nodeId);
        if (source.kind === "ruleTable") {
          for (const cond of source.matchConditions) {
            if (cond.valueFrom.kind === "node") ids.add(cond.valueFrom.nodeId);
          }
        }
      }
    }
    return ids;
  }, [calculationBindings]);

  const calcOverridesByNodeId = useMemo(() => {
    const map = new Map<string, Record<string, unknown>>();
    for (const [, entry] of calcResults) {
      if (entry.result.ok) {
        const existing = map.get(entry.targetNodeId) ?? {};
        map.set(entry.targetNodeId, { ...existing, [entry.targetProp]: entry.result.value });
      }
    }
    return map;
  }, [calcResults]);

  const rejectDraftPaletteEntry = (entry: PaletteEntry): boolean => {
    if (!packageScopedLayout) {
      announce(UX_ROUTE_KEY_REQUIRED_FOR_CANVAS);
      return true;
    }
    if (!packageAuthoringReady) {
      announce("パッケージを自動生成中です。少し待ってから再度お試しください。");
      return true;
    }
    if (scopedPackageId && entry.packageId && entry.packageId !== scopedPackageId) {
      announce("選択中パッケージに属さない部品は配置できません。");
      return true;
    }
    return false;
  };
  const selectedNode = draftNodes.find((n) => n.nodeId === selectedNodeId) ??
    null;
  const canvasPreviewClass = resolveCanvasRootPreviewClassName(
    selectedLayoutClassRefs,
  );

  const applyCanvasFromTensorPatch = (
    tensorPatchJson: string,
    historyLabel: string,
    options: { seedWhenEmpty?: boolean } = {},
  ): boolean => {
    const parsed = parseVisualLayoutPatchJson(
      tensorPatchJson,
      paletteSeedEntries,
    );
    if (!parsed.ok) {
      setPatchErrors([{
        code: parsed.error,
        message: "layout_patch_json の解析に失敗しました。",
      }]);
      return false;
    }
    let nodes = enrichLayoutPreviewNodes(
      parsed.value.nodes as DraftNode[],
      paletteSeedEntries,
    ) as DraftNode[];
    if (nodes.length === 0 && options.seedWhenEmpty === true) {
      nodes = seedDraftNodesFromPalette(paletteSeedEntries) as DraftNode[];
    }
    setDraftNodes(nodes);
    setSelectedLayoutClassRefs(parsed.value.layoutClassRefs);
    setCalculationBindings(parsed.value.calculationBindings ?? []);
    setLegacyLayoutWarning(isLegacyAbsoluteLayoutPatch(nodes));
    setSelectedNodeId(null);
    setDesignDraftByNodeId(new Map());
    historyRef.current = [{
      nodes: nodes.map((n) => ({ ...n })),
      label: historyLabel,
    }];
    historyPtrRef.current = 0;
    setCanUndo(false);
    setCanRedo(false);
    setLifecyclePhase("idle");
    return true;
  };

  // ── Preset: load presets list ────────────────────────────────────────────
  const loadPresetList = async () => {
    try {
      const list = await listMockPresets("active");
      setSavedPresets(list);
      setPresetsLoaded(true);
    } catch {
      setSavedPresets([]);
    }
  };

  // ── Preset: load selected preset into tmp canvas draft ───────────────────
  // SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml §bind_to_canvas_contract
  // Writes to draftNodes (local tmp canvas state) ONLY.
  // Does NOT write to topology.ui_topology_tensor or canonical topology tables.
  const handleLoadPreset = async () => {
    if (!selectedPresetId) return;
    const activeRouteKey = scopedRouteKey?.trim() ?? effectiveRouteKey;
    if (!activeRouteKey) {
      setPresetLoadError("ルートキーが選択されていません。先にルートパッケージを選択してください。");
      setPresetLoadStatus("error");
      return;
    }

    // Confirm merge/replace when canvas has existing nodes.
    if (draftNodes.length > 0) {
      const ok = globalThis.confirm(
        "既存のキャンバスノードがあります。プリセットを読み込むと現在のキャンバスが置き換えられます。続きますか？",
      );
      if (!ok) return;
    }

    setPresetLoadStatus("loading");
    setPresetLoadError(null);

    try {
      const result = await bindMockPreset({
        presetId: selectedPresetId,
        routeKey: activeRouteKey,
      });

      if (!result.ok) {
        setPresetLoadError(result.message ?? result.errorCode ?? "読み込みに失敗しました");
        setPresetLoadStatus("error");
        return;
      }

      if (!result.layoutPatchJson) {
        setPresetLoadError("プリセットの layout_patch_json がありません。先にコンパイルしてください。");
        setPresetLoadStatus("error");
        return;
      }

      const patchStr = JSON.stringify(result.layoutPatchJson);
      const applied = applyCanvasFromTensorPatch(patchStr, "プリセット読み込み", { seedWhenEmpty: false });
      if (!applied) {
        setPresetLoadError("プリセットをキャンバスに適用できませんでした。");
        setPresetLoadStatus("error");
        return;
      }

      setPresetLoadStatus("idle");
      announce("プリセットをキャンバス（一時ドラフト）に読み込みました。active topology には反映されていません。");
    } catch (err) {
      setPresetLoadError(err instanceof Error ? err.message : "読み込みに失敗しました");
      setPresetLoadStatus("error");
    }
  };

  // ── Preset: save current canvas as preset ────────────────────────────────
  // Exports current draftNodes as a CanvasPresetSeed and opens the drawer.
  // source_kind = ui_builder_canvas. Does NOT write to active topology.
  // SSOT: docs/design/mock-preset-intake-compiler-ssot.yaml §save_current_canvas_as_preset_button
  const handleSaveCanvasAsPreset = () => {
    if (draftNodes.length === 0) {
      announce("キャンバスが空です。部品を配置してからプリセット保存してください。");
      return;
    }

    // Build component mappings from current draftNodes.
    const componentMappings = draftNodes.map((n) => ({
      sourceObjectId: n.nodeId,
      nodeId: n.nodeId,
      mappingKind: (n.nodeKind === "structural_html" ? "structural_html" : "catalog_component") as "catalog_component" | "structural_html",
      componentKey: n.nodeKind === "structural_html" ? undefined : n.componentKey,
      componentKind: n.componentKind,
      htmlTag: n.htmlTag as string | undefined,
    }));

    // Visual tree JSON: capture current canvas geometry.
    const visualTreeJson = {
      source_kind: "ui_builder_canvas",
      nodes: draftNodes.map((n, idx) => ({
        source_object_id: n.nodeId,
        object_type: n.componentKey,
        parent_source_object_id: n.parentNodeId,
        z_index: idx,
        bbox: { x: n.x, y: n.y, width: n.width, height: n.height },
        transform: null,
        text_content: null,
        style_attributes: {},
        children: [],
      })),
    };

    // layout_patch_json from the already-computed tensorPatchJson.
    const layoutPatchJson = JSON.parse(tensorPatchJson);

    const sourceHash = computeSourceHash(JSON.stringify(visualTreeJson));

    const seed: CanvasPresetSeed = {
      sourceKind: "ui_builder_canvas",
      layoutPatchJson,
      componentMappings,
      visualTreeJson,
      sourceHash,
    };

    setCanvasPresetSeed(seed);
    setPresetDrawerOpen(true);
  };

  // ── Gap 2: History management ─────────────────────────────────────────────
  const pushHistory = (nodes: DraftNode[], label: string) => {
    const snapshots = historyRef.current.slice(0, historyPtrRef.current + 1);
    snapshots.push({ nodes: nodes.map((n) => ({ ...n })), label });
    if (snapshots.length > MAX_HISTORY) snapshots.shift();
    historyRef.current = snapshots;
    historyPtrRef.current = snapshots.length - 1;
    setCanUndo(historyPtrRef.current > 0);
    setCanRedo(false);
  };

  const undo = () => {
    if (historyPtrRef.current <= 0) return;
    historyPtrRef.current--;
    const snap = historyRef.current[historyPtrRef.current];
    setDraftNodes(snap.nodes.map((n) => ({ ...n })));
    setCanUndo(historyPtrRef.current > 0);
    setCanRedo(true);
    setLifecyclePhase("idle");
    announce(`元に戻しました: ${snap.label}`);
  };

  const redo = () => {
    if (historyPtrRef.current >= historyRef.current.length - 1) return;
    historyPtrRef.current++;
    const snap = historyRef.current[historyPtrRef.current];
    setDraftNodes(snap.nodes.map((n) => ({ ...n })));
    setCanUndo(true);
    setCanRedo(historyPtrRef.current < historyRef.current.length - 1);
    setLifecyclePhase("idle");
    announce(`やり直しました: ${snap.label}`);
  };

  // Gap 6: ARIA live region announcer
  const announce = (msg: string) => {
    setLiveAnnouncement("");
    setTimeout(() => setLiveAnnouncement(msg), 10);
  };

  // ── Gap 2: Keyboard undo/redo shortcut ───────────────────────────────────
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const ctrl = e.ctrlKey || e.metaKey;
      if (ctrl && e.key === "z" && !e.shiftKey) {
        e.preventDefault();
        undo();
      }
      if (ctrl && (e.key === "y" || (e.key === "z" && e.shiftKey))) {
        e.preventDefault();
        redo();
      }
    };
    globalThis.addEventListener("keydown", handler);
    return () => globalThis.removeEventListener("keydown", handler);
  }, []);

  useEffect(() => {
    if (scopedPackageId && scopedRouteKey) {
      setRouteKey(scopedRouteKey);
      setManualRouteKey("");
    }
    if (scopedPackageId && scopedLayoutId) {
      setLayoutId(scopedLayoutId);
      setManualLayoutId("");
    }
  }, [scopedPackageId, scopedRouteKey, scopedLayoutId]);

  // ── initial load ─────────────────────────────────────────────────────────
  useEffect(() => {
    const load = async () => {
      setPaletteStatus("候補をロード中...");
      setPaletteLoadFailed(false);
      setCandidateErrors([]);
      const { candidates, errors: candErr } =
        await loadLayoutCandidatesFromBackend();
      let scopedCandidates = scopedPackageId && scopedRouteKey && scopedLayoutId
        ? candidates.filter(
          (c) => c.routeKey === scopedRouteKey && c.layoutId === scopedLayoutId,
        )
        : candidates;
      scopedCandidates = ensureScopedLayoutCandidates(
        scopedCandidates,
        scopedRouteKey,
        scopedLayoutId,
      );
      const nextCandidates = ensureScopedLayoutCandidates(
        scopedCandidates.length > 0 ? scopedCandidates : candidates,
        scopedRouteKey,
        scopedLayoutId,
      );
      setLayoutCandidates(nextCandidates);
      if (candErr.length) setCandidateErrors(candErr);
      try {
        const body = await dispatchAdminOp("ui_topology", "promoted_palette");
        if (body?.errors?.length) {
          setPaletteLoadFailed(true);
          setCandidateErrors((prev) => [...prev, ...body.errors]);
          setPaletteEntries([]);
          setPaletteStatus("配置可能部品一覧の読み込みに失敗しました。");
          return;
        }
        const promoted = body?.emission?.data as
          | PromotedPaletteEntry[]
          | undefined;
        if (!Array.isArray(promoted)) {
          setPaletteLoadFailed(true);
          setCandidateErrors((prev) => [
            ...prev,
            {
              code: "PROMOTED_PALETTE_LOAD_FAILED",
              message: "配置可能部品一覧が取得できませんでした。",
            },
          ]);
          setPaletteEntries([]);
          setPaletteStatus("配置可能部品一覧の読み込みに失敗しました。");
          return;
        }
        const scopedPromoted = scopedPackageId
          ? promoted.filter((p) => p.packageId === scopedPackageId)
          : promoted.filter((p) =>
            !scopedRouteKey || p.routeKey === scopedRouteKey
          );
        const promotedByKey = new Map(
          scopedPromoted.map((p) => [p.componentKey, p]),
        );
        const catalogEntries = COMPONENT_CATALOG_ENTRIES
          .filter((c) => c.registrationRequired)
          .map((c) => {
            const promotedEntry = promotedByKey.get(c.componentKey);
            if (promotedEntry) {
              return {
                ...promotedEntry,
                isDraftOnly: false,
              } satisfies PaletteEntry;
            }
            return {
              componentKey: c.componentKey,
              componentKind: c.componentKind,
              isDraftOnly: false,
            } satisfies PaletteEntry;
          });
        setPaletteEntries(packageScopedLayout ? catalogEntries : []);
        setPaletteStatus(
          packageScopedLayout
            ? `カタログ ${catalogEntries.length} 件（drop で自動追加）`
            : UX_ROUTE_KEY_REQUIRED_FOR_CANVAS,
        );
        if (candidates.length === 0 && scopedPromoted.length > 0) {
          setLayoutCandidates(deriveCandidatesFromPalette(scopedPromoted));
        }
        const routeLayoutSource = nextCandidates.length > 0
          ? nextCandidates
          : candidates;
        if (!routeKey && routeLayoutSource.length > 0) {
          setRouteKey(routeLayoutSource[0].routeKey);
          setLayoutId(routeLayoutSource[0].layoutId);
        }
        if (scopedRouteKey && scopedLayoutId) {
          setRouteKey(scopedRouteKey);
          setLayoutId(scopedLayoutId);
          setManualRouteKey("");
          setManualLayoutId("");
        }
      } catch (e) {
        setPaletteLoadFailed(true);
        setCandidateErrors((
          prev,
        ) => [...prev, {
          code: "PROMOTED_PALETTE_LOAD_ERROR",
          message: String(e),
        }]);
        setPaletteEntries([]);
        setPaletteStatus(`配置可能部品一覧の読み込みに失敗しました: ${e}`);
      }
    };
    load();
  }, [scopedPackageId, scopedRouteKey, scopedLayoutId, paletteReloadToken, packageScopedLayout]);

  // ── hydrate layout_patch_json from DB on package / route / layout selection ─
  useEffect(() => {
    if (!scopedPackageId?.trim() || !effectiveLayoutId || !effectiveRouteKey) {
      return;
    }
    if (paletteLoadFailed) return;

    let cancelled = false;
    const hydrate = async () => {
      try {
        const body = await dispatchAdminOp(
          "ui_topology",
          "get_layout_patch_draft",
          {
            packageId: scopedPackageId.trim(),
            layoutId: effectiveLayoutId,
            routeKey: effectiveRouteKey,
          },
        );
        if (cancelled) return;
        if (body?.errors?.length) {
          const notFound = body.errors.some(
            (e: ValidationError) =>
              e.code === "LAYOUT_PATCH_DRAFT_NOT_FOUND" ||
              e.code === "PACKAGE_WIRING_NOT_FOUND",
          );
          if (notFound) {
            applyCanvasFromTensorPatch("{}", "空キャンバス", {
              seedWhenEmpty: false,
            });
            announce(
              "保存済み layout draft なし — 空のキャンバスから開始します",
            );
          } else {
            setPatchErrors(body.errors);
          }
          return;
        }
        const data = body?.emission?.data as
          | { tensorPatchJson?: string }
          | undefined;
        const json = typeof data?.tensorPatchJson === "string"
          ? data.tensorPatchJson
          : "{}";
        if (
          applyCanvasFromTensorPatch(json, "DB layout draft 読込", {
            seedWhenEmpty: false,
          })
        ) {
          announce("layout_patch_json を canvas に読み込みました");
        }
      } catch (e) {
        if (!cancelled) {
          setPatchErrors([{
            code: "LAYOUT_PATCH_DRAFT_LOAD_ERROR",
            message: String(e),
          }]);
        }
      }
    };
    hydrate();
    return () => {
      cancelled = true;
    };
  }, [
    scopedPackageId,
    effectiveLayoutId,
    effectiveRouteKey,
    paletteLoadFailed,
    paletteEntries.length,
  ]);

  // ── _tmp draft: backend auto-save + sessionStorage fallback ──────────────
  // Backend: layout_draft_tmp_json column on topology.ui_topology_tensor.
  // SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract.draft_persistence_model
  // sessionStorage keeps the synchronous last-known copy as a local fallback.
  const [tmpSaveStatus, setTmpSaveStatus] = useState<
    "idle" | "saving" | "saved" | "error"
  >("idle");
  const tmpSaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const tmpDraftKey = scopedPackageId?.trim()
    ? `ui_builder_tmp_draft_${scopedPackageId.trim()}`
    : null;

  // sessionStorage restore (synchronous fallback when backend hydrate finds nothing)
  useEffect(() => {
    if (!tmpDraftKey || typeof globalThis.sessionStorage === "undefined") {
      return;
    }
    const saved = sessionStorage.getItem(tmpDraftKey);
    if (!saved) return;
    try {
      const { nodes, classRefs } = JSON.parse(saved) as {
        nodes: DraftNode[];
        classRefs: string[];
      };
      if (Array.isArray(nodes) && nodes.length > 0) {
        setDraftNodes(nodes);
        if (Array.isArray(classRefs)) setSelectedLayoutClassRefs(classRefs);
        historyRef.current = [{
          nodes: nodes.map((n) => ({ ...n })),
          label: "_tmp 復元",
        }];
        historyPtrRef.current = 0;
        setCanUndo(false);
        setCanRedo(false);
        setLifecyclePhase("idle");
      }
    } catch {
      sessionStorage.removeItem(tmpDraftKey);
    }
  }, [tmpDraftKey]);

  // Debounced backend save + sessionStorage sync on every canvas mutation
  useEffect(() => {
    if (draftNodes.length === 0) return;
    // sessionStorage sync (synchronous, fallback)
    if (tmpDraftKey && typeof globalThis.sessionStorage !== "undefined") {
      try {
        sessionStorage.setItem(
          tmpDraftKey,
          JSON.stringify({
            nodes: draftNodes,
            classRefs: selectedLayoutClassRefs,
          }),
        );
      } catch { /* storage full */ }
    }
    // Backend save (debounced 1500ms)
    if (!scopedPackageId?.trim() || !effectiveLayoutId || !effectiveRouteKey) {
      return;
    }
    if (tmpSaveTimerRef.current) clearTimeout(tmpSaveTimerRef.current);
    setTmpSaveStatus("saving");
    tmpSaveTimerRef.current = setTimeout(async () => {
      try {
        const body = await dispatchAdminOp("layout_patch", "save_tmp", {
          packageId: scopedPackageId.trim(),
          layoutId: effectiveLayoutId,
          routeKey: effectiveRouteKey,
          tmpJson: tensorPatchJson,
        });
        if (body?.errors?.length) {
          setPatchErrors(body.errors);
          setTmpSaveStatus("error");
          return;
        }
        setTmpSaveStatus("saved");
      } catch {
        setTmpSaveStatus("error");
      }
    }, 1500);
    return () => {
      if (tmpSaveTimerRef.current) clearTimeout(tmpSaveTimerRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draftNodes, selectedLayoutClassRefs]);

  // ── layout patch (validate in modal / apply on confirm) ─────────────────
  const callLayoutPatch = async (
    action: "validate" | "apply",
    options?: { viaApplyModal?: boolean },
  ) => {
    const inApplyModal = options?.viaApplyModal === true || layoutApplyModalOpen;
    setPatchErrors([]);
    if (action === "validate") {
      setPatchSummary(null);
    }
    setDebugJson(null);

    if (!canPatch) {
      setPatchErrors([{
        code: "NO_ROUTE_LAYOUT",
        message: "ルートとレイアウトを選択してください。",
      }]);
      if (inApplyModal) setLayoutApplyModalPhase("validated");
      return;
    }
    if (action === "apply") {
      const draftOnlyNodes = draftNodes.filter((n) => n.isDraftOnly);
      if (draftOnlyNodes.length > 0) {
        setPatchErrors(draftOnlyNodes.map((n) => ({
          code: "DRAFT_ONLY_NODES",
          message:
            `まだ使えない部品が ${draftOnlyNodes.length} 件あります — 先に登録してください`,
          nodeId: n.nodeId,
          componentKey: n.componentKey,
        })));
        announce(
          `保存ブロック: ${draftOnlyNodes.length} 件のまだ使えない部品があります`,
        );
        setLayoutApplyModalPhase("validated");
        return;
      }
      setLayoutApplyModalPhase("applying");
    }

    const phaseMap: Record<string, LifecyclePhase> = {
      validate: "validating",
      apply: "applying",
    };
    setLifecyclePhase(phaseMap[action] as LifecyclePhase);
    setLoading(true);
    announce(
      action === "validate" ? "バリデートを実行中..." : "適用を実行中...",
    );

    if (!scopedPackageId?.trim()) {
      setPatchErrors([{
        code: "PACKAGE_REQUIRED",
        message: "配置を保存する前にパッケージを選択してください。",
      }]);
      setLoading(false);
      setLifecyclePhase("idle");
      return;
    }

    const submittedTensorPatchJson = tensorPatchJson;

    try {
      const body = await dispatchAdminOp("layout_patch", action, {
        packageId: scopedPackageId.trim(),
        layoutId: effectiveLayoutId,
        routeKey: effectiveRouteKey,
        tensorPatchJson: submittedTensorPatchJson,
      });
      setDebugJson(JSON.stringify(body, null, 2));
      const summary = projectLayoutPatchSummary(
        action,
        body,
        draftNodes,
        selectedLayoutClassRefs.length,
        selectedLayout?.layoutKey,
      );
      setPatchSummary(summary);

      const failPhase: Record<string, LifecyclePhase> = {
        validate: "validated",
        apply: "applied_fail",
      };
      if (body?.errors?.length) {
        setPatchErrors(body.errors);
        setLifecyclePhase(failPhase[action] as LifecyclePhase);
        if (action === "validate" && inApplyModal) {
          setLayoutApplyModalPhase("validated");
        }
        if (action === "apply" && inApplyModal) {
          setLayoutApplyModalPhase("validated");
        }
        announce(`エラー: ${body.errors[0].message}`);
      } else {
        const donePhase: Record<string, LifecyclePhase> = {
          validate: "validated",
          apply: "applied_ok",
        };
        const isPersisted = body?.emission?.data?.persisted === true;
        setLifecyclePhase(
          isPersisted ? "persisted" : donePhase[action] as LifecyclePhase,
        );

        if (action === "validate" && inApplyModal) {
          setLayoutApplyModalPhase("validated");
        }

        if (action === "apply" && summary.valid) {
          if (tmpDraftKey && typeof globalThis.sessionStorage !== "undefined") {
            sessionStorage.removeItem(tmpDraftKey);
          }
          setTmpSaveStatus("idle");
          setLayoutApplyModalPhase("success");
          lifecycleRef.current?.scrollIntoView({
            behavior: "smooth",
            block: "start",
          });
          announce("配置の保存が完了しました — 次のステップを選んでください");
        }

        // layoutId round-trip: confirm DB-authoritative identity from backend response.
        // Backend NormalizeLayoutPatch returns the same layoutId it received, confirming
        // the DB record was found and updated. Surface explicit mismatch rather than silent fallback.
        const confirmedLayoutId = summary.layoutId;
        const confirmedRouteKey = summary.routeKey;
        if (action === "apply" && confirmedLayoutId) {
          if (confirmedLayoutId !== effectiveLayoutId) {
            setPatchErrors([{
              code: "LAYOUT_ID_MISMATCH",
              message:
                `サーバーが異なるレイアウトID (${confirmedLayoutId}) を返しました。候補を再読み込みしてください。`,
            }]);
            setLifecyclePhase("applied_fail");
            announce("レイアウトIDの不一致が発生しました");
            return;
          }
          // Confirm the DB-authoritative layoutId in frontend state
          setLayoutId(confirmedLayoutId);
          if (confirmedRouteKey) setRouteKey(confirmedRouteKey);
        }

        announce(summary.message);
      }
    } catch (e) {
      const failPhase: Record<string, LifecyclePhase> = {
        validate: "validated",
        apply: "applied_fail",
      };
      setPatchErrors([{ code: "NETWORK_ERROR", message: String(e) }]);
      setLifecyclePhase(failPhase[action] as LifecyclePhase);
      if (inApplyModal) {
        setLayoutApplyModalPhase("validated");
      }
      announce(`ネットワークエラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const openLayoutApplyModal = () => {
    if (!canPatch) {
      setPatchErrors([{
        code: "NO_ROUTE_LAYOUT",
        message: "ルートとレイアウトを選択してください。",
      }]);
      return;
    }
    const draftOnlyNodes = draftNodes.filter((n) => n.isDraftOnly);
    if (draftOnlyNodes.length > 0) {
      setPatchErrors(draftOnlyNodes.map((n) => ({
        code: "DRAFT_ONLY_NODES",
        message:
          `まだ使えない部品が ${draftOnlyNodes.length} 件あります — 先に登録してください`,
        nodeId: n.nodeId,
        componentKey: n.componentKey,
      })));
      announce(
        `保存ブロック: ${draftOnlyNodes.length} 件のまだ使えない部品があります`,
      );
      return;
    }
    setPatchErrors([]);
    setPatchSummary(null);
    setLayoutApplyModalOpen(true);
    setLayoutApplyModalPhase("validating");
    void callLayoutPatch("validate", { viaApplyModal: true });
  };

  // ── node factory ─────────────────────────────────────────────────────────
  const makeNewNode = (
    entry: PaletteEntry,
    parentNodeId: string | null = null,
    orderIndex?: number,
  ): DraftNode => {
    const componentKind = entry.componentKind ||
      resolveComponentKindForLayoutPreview(entry.componentKey) ||
      undefined;
    const defaults = componentKind
      ? getLayoutPreviewDefaultSize(componentKind)
      : { width: DEFAULT_NODE_WIDTH, height: DEFAULT_NODE_HEIGHT };
    const oi = orderIndex ?? nextOrderIndexForParent(draftNodes, parentNodeId);
    return {
      nodeId: makeNodeId(),
      componentKey: entry.componentKey,
      componentKind,
      isDraftOnly: entry.isDraftOnly,
      componentId: entry.componentId,
      packageId: entry.packageId,
      layoutId: entry.layoutId,
      wiringId: entry.wiringId,
      tensorId: entry.tensorId,
      slotKey: "",
      orderIndex: oi,
      parentNodeId,
      gridCol: 1,
      gridRow: oi + 1,
      x: 0,
      y: 0,
      width: defaults.width,
      height: defaults.height,
      widthMode: "auto" as SizingMode,
      heightMode: "auto" as SizingMode,
    };
  };

  // ── node operations (all push to history) ────────────────────────────────
  const addNode = (newNode: DraftNode) => {
    if (!packageScopedLayout) {
      announce("先に上のパッケージを選択してください。");
      return;
    }
    if (newNode.isDraftOnly) {
      announce("ドラフト部品はキャンバスに配置できません。");
      return;
    }
    const next = [...draftNodes, newNode];
    setDraftNodes(next);
    pushHistory(next, `追加: ${friendlyComponentLabel(newNode.componentKey)}`);
    setLifecyclePhase("idle");
    announce(
      `${
        friendlyComponentLabel(newNode.componentKey)
      }をキャンバスに追加しました`,
    );
  };

  const reparentNode = (
    nodeId: string,
    newParentId: string | null,
    insertBeforeId: string | null,
  ) => {
    if (wouldCreateVisualParentCycle(draftNodes, nodeId, newParentId)) {
      announce("循環参照になるため移動できません");
      return;
    }
    setDraftNodes((prev) => {
      const node = prev.find((n) => n.nodeId === nodeId);
      if (!node) return prev;
      const siblings = prev
        .filter((n) => n.parentNodeId === newParentId && n.nodeId !== nodeId)
        .sort((a, b) => a.orderIndex - b.orderIndex);
      const insertAt = insertBeforeId
        ? Math.max(0, siblings.findIndex((n) => n.nodeId === insertBeforeId))
        : siblings.length;
      siblings.splice(insertAt < 0 ? siblings.length : insertAt, 0, {
        ...node,
        parentNodeId: newParentId,
      });
      const reordered = siblings.map((n, i) => ({ ...n, orderIndex: i }));
      const others = prev.filter(
        (n) => n.parentNodeId !== newParentId && n.nodeId !== nodeId,
      );
      const result = [...others, ...reordered];
      pushHistory(result, `入れ子/並び替え: ${friendlyNodeLabel(node)}`);
      return result;
    });
    setLifecyclePhase("idle");
    announce("レイヤーを移動しました");
  };

  const moveLayoutNode = (nodeId: string, dir: "up" | "down") => {
    const direction = dir === "up" ? "front" : "back";
    setDraftNodes((prev) => {
      const result = reorderLayoutNodeStack(prev, nodeId, direction);
      if (!result) return prev;
      const moved = prev.find((n) => n.nodeId === nodeId);
      pushHistory(
        result,
        `順序変更: ${friendlyComponentLabel(moved?.componentKey ?? nodeId)}`,
      );
      return result;
    });
  };

  const handleDesignDraftChange = useCallback((
    nodeId: string,
    partial: DesignDraft,
  ) => {
    setDesignDraftByNodeId((prev) => {
      const current = prev.get(nodeId) ?? {};
      const merged: DesignDraft = { ...current, ...partial };
      const cssUnchanged =
        JSON.stringify(current.cssTokenRefs ?? []) ===
        JSON.stringify(merged.cssTokenRefs ?? []);
      if (
        current.inlineText === merged.inlineText &&
        current.linkHref === merged.linkHref &&
        current.linkTarget === merged.linkTarget &&
        cssUnchanged
      ) {
        return prev;
      }
      const next = new Map(prev);
      next.set(nodeId, merged);
      return next;
    });
  }, []);

  const toggleRootLayoutClassRef = (classKey: string) => {
    setSelectedLayoutClassRefs((prev) =>
      prev.includes(classKey)
        ? prev.filter((k) => k !== classKey)
        : [...prev, classKey]
    );
    setLifecyclePhase("idle");
  };

  const handleMigrateLegacyLayout = async () => {
    if (
      !(await confirm(
        "座標ベースのレイアウトをフロースタックに変換します。x/y は削除されます。よろしいですか？",
      ))
    ) {
      return;
    }
    const migrated = migrateAbsolutePatchToFlowStack(draftNodes) as DraftNode[];
    setDraftNodes(migrated);
    setLegacyLayoutWarning(false);
    pushHistory(migrated, "フロースタックへ変換");
    setLifecyclePhase("idle");
    announce("レイアウトをフロースタックに変換しました");
  };

  const removeNode = async (nodeId: string) => {
    const node = draftNodes.find((n) => n.nodeId === nodeId);
    const next = draftNodes.filter((n) => n.nodeId !== nodeId);
    setDraftNodes(next);
    if (selectedNodeId === nodeId) setSelectedNodeId(null);
    setDesignDraftByNodeId((prev) => {
      if (!prev.has(nodeId)) return prev;
      const nextMap = new Map(prev);
      nextMap.delete(nodeId);
      return nextMap;
    });
    pushHistory(next, `削除: ${node ? friendlyNodeLabel(node) : nodeId}`);
    setLifecyclePhase("idle");
    if (node) announce(`${friendlyNodeLabel(node)}を削除しました`);
    if (
      node?.componentKey &&
      scopedRouteKey &&
      onDetachComponentAfterRemove &&
      !next.some((n) => n.componentKey === node.componentKey)
    ) {
      await onDetachComponentAfterRemove(node.componentKey);
    }
  };

  const copyNode = (nodeId: string) => {
    const source = draftNodes.find((n) => n.nodeId === nodeId);
    if (!source || !packageScopedLayout) return;
    const parentNodeId = source.parentNodeId ?? null;
    const cloned: DraftNode = {
      ...source,
      nodeId: makeNodeId(),
      orderIndex: nextOrderIndexForParent(draftNodes, parentNodeId),
      x: 0,
      y: 0,
    };
    const next = [...draftNodes, cloned];
    setDraftNodes(next);
    setSelectedNodeId(cloned.nodeId);
    pushHistory(next, `コピー: ${friendlyNodeLabel(source)}`);
    setLifecyclePhase("idle");
    announce(`${friendlyNodeLabel(source)}をコピーしました`);
  };

  const addStructuralHtmlNode = (htmlTag: StructuralHtmlTag) => {
    if (!packageScopedLayout) {
      announce("先に上のパッケージを選択してください。");
      return;
    }
    const parentNodeId = selectedNodeId;
    const orderIndex = nextOrderIndexForParent(draftNodes, parentNodeId);
    const newNode = makeStructuralHtmlNode(htmlTag, {
      nodeId: makeNodeId(),
      x: 0,
      y: 0,
      orderIndex,
      parentNodeId,
    }) as DraftNode;
    addNode(newNode);
  };

  const toggleNodeLayoutClassRef = (nodeId: string, classKey: string) => {
    let toggledOn = false;
    setDraftNodes((prev) => {
      const next = prev.map((n) => {
        if (n.nodeId !== nodeId) return n;
        const current = n.layoutClassRefs ?? [];
        toggledOn = !current.includes(classKey);
        const layoutClassRefs = toggledOn
          ? [...current, classKey]
          : current.filter((k) => k !== classKey);
        const widthMode = resolveSizingModeAfterToggle(layoutClassRefs, n.widthMode);
        return { ...n, layoutClassRefs, widthMode };
      });
      pushHistory(next, "ノード layoutClassRefs を変更");
      return next;
    });
    setLifecyclePhase("idle");
    announce(
      toggledOn
        ? `layoutClassRefs に ${classKey} を追加しました（canvas に反映済み）`
        : `layoutClassRefs から ${classKey} を外しました（canvas に反映済み）`,
    );
  };

  const updateNode = (nodeId: string, updates: Partial<DraftNode>) => {
    setDraftNodes((prev) => {
      const next = prev.map((
        n,
      ) => (n.nodeId === nodeId ? { ...n, ...updates } : n));
      return next;
    });
    setLifecyclePhase("idle");
  };

  const commitNodeUpdate = (
    nodeId: string,
    updates: Partial<DraftNode>,
    label: string,
  ) => {
    setDraftNodes((prev) => {
      const next = prev.map((
        n,
      ) => (n.nodeId === nodeId ? { ...n, ...updates } : n));
      pushHistory(next, label);
      return next;
    });
    setLifecyclePhase("idle");
  };

  // ── palette drag ─────────────────────────────────────────────────────────
  const handleDragStartPalette = (entry: PaletteEntry, _payload: BucketCardDragPayload) => {
    dragSrc.current = { kind: "palette", entry };
  };

  const handleDragOverCanvas = (e: Event) => {
    e.preventDefault();
  };

  const handleDropOnCanvas = async (e: Event) => {
    e.preventDefault();
    const de = e as DragEvent;
    let entry: PaletteEntry | null = null;
    const payload = readBucketCardDragPayload(de);
    if (payload) {
      entry = paletteEntryFromDragPayload(payload);
    } else if (dragSrc.current?.kind === "palette") {
      entry = dragSrc.current.entry;
    }
    dragSrc.current = null;
    if (!entry) return;
    if (rejectDraftPaletteEntry(entry)) return;
    const dropCatalogEntry = COMPONENT_CATALOG_ENTRIES.find((c) =>
      c.componentKey === entry.componentKey
    );
    if (
      dropCatalogEntry?.registrationRequired !== false && entry.componentKey &&
      onRegisterComponentBeforePlace
    ) {
      const registered = await onRegisterComponentBeforePlace(entry.componentKey);
      if (!registered) return;
    }
    if (entry.routeKey && !routeKey) setRouteKey(entry.routeKey);
    if (entry.layoutId && !layoutId) setLayoutId(entry.layoutId);
    addNode(makeNewNode(entry, null));
  };

  // Gap 5: Non-drag add from palette button
  const handleAddFromPalette = async (entry: PaletteEntry) => {
    if (rejectDraftPaletteEntry(entry)) return;
    const addCatalogEntry = COMPONENT_CATALOG_ENTRIES.find((c) =>
      c.componentKey === entry.componentKey
    );
    if (
      addCatalogEntry?.registrationRequired !== false && entry.componentKey &&
      onRegisterComponentBeforePlace
    ) {
      const registered = await onRegisterComponentBeforePlace(entry.componentKey);
      if (!registered) return;
    }
    const parentNodeId = selectedNodeId;
    if (entry.routeKey && !routeKey) setRouteKey(entry.routeKey);
    if (entry.layoutId && !layoutId) setLayoutId(entry.layoutId);
    addNode(makeNewNode(entry, parentNodeId));
  };

  // Gap 7: Quick-start templates from empty state
  const handleAddFromEmptyState = (templateId: string) => {
    if (!packageScopedLayout) return;
    const catalog = paletteEntries.filter((e) => !e.isDraftOnly);
    const pick = (key: string) =>
      catalog.find((e) => e.componentKey.includes(key)) ?? catalog[0];

    let nodes: DraftNode[] = [];
    if (templateId === "starter_header_main" && catalog.length >= 2) {
      const header = pick("header") ?? catalog[0];
      const main = pick("main") ?? catalog[1] ?? catalog[0];
      nodes = [
        makeNewNode(header, null, 0),
        { ...makeNewNode(main, null, 1), height: 200 },
      ];
    } else if (templateId === "starter_card" && catalog.length >= 1) {
      nodes = [{
        ...makeNewNode(pick("card") ?? catalog[0], null, 0),
        width: 200,
        height: 100,
      }];
    } else if (templateId === "starter_form" && catalog.length >= 1) {
      const form = pick("form") ?? catalog[0];
      nodes = [{ ...makeNewNode(form, null, 0), width: 300, height: 200 }];
    } else if (catalog.length >= 1) {
      nodes = [makeNewNode(catalog[0], null, 0)];
    }

    if (nodes.length === 0) return;
    setDraftNodes(nodes);
    setLegacyLayoutWarning(false);
    pushHistory(nodes, `テンプレート: ${templateId}`);
    setLifecyclePhase("idle");
    announce(`${nodes.length} 件の部品を追加しました`);
  };

  return (
    <div>
      {/* Gap 6: ARIA live region for status announcements */}
      <div role="status" aria-live="polite" aria-atomic="true" class="sr-only">
        {liveAnnouncement}
      </div>

      {/* Gap 1: Lifecycle step indicator */}
      <div ref={lifecycleRef}>
        <LifecycleStepIndicator phase={lifecyclePhase} />
      </div>

      {!packageScopedLayout && (
        <p class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          先に上のパッケージを選択してください。配置の編集は選択したパッケージに紐づきます。
        </p>
      )}
      {packageScopedLayout && layoutSelectorsLocked && (
        <p class="mb-3 rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-800">
          ルート <code class="font-mono text-xs">{scopedRouteKey}</code>{" "}
          / レイアウト{" "}
          <code class="font-mono text-xs">
            {scopedLayoutId?.slice(0, 8)}…
          </code>（選択パッケージに固定）
        </p>
      )}

      <details class="mb-3.5">
        <summary class="cursor-pointer text-xs text-gray-500 hover:text-gray-700">
          技術情報
        </summary>
        <div class="alert-warn mt-1 text-xs">
          <strong>投影サーフェス境界:</strong>{" "}
          フロントエンドはドラフト状態・視覚プレビュー・intent 送信のみ担当。
          適用はモーダル内で <code>validate → apply</code>{" "}
          経由。直接 DB 書き込みは行いません。
        </div>
      </details>

      {!layoutSelectorsLocked && (
        <RouteLayoutSelector
          candidates={displayCandidates}
          routeKey={routeKey}
          layoutId={layoutId}
          onRouteChange={(r) => {
            setRouteKey(r);
            setManualRouteKey("");
            const first = layoutsForRoute(displayCandidates, r)[0];
            setLayoutId(first?.layoutId ?? "");
            setManualLayoutId("");
          }}
          onLayoutChange={(l) => {
            setLayoutId(l);
            setManualLayoutId("");
          }}
          disabled={selectorsDisabled}
          loadError={candidateErrors}
        />
      )}

      {displayCandidates.length === 0 && candidateErrors.length === 0 &&
        packageScopedLayout && (
        <div class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <p class="font-semibold">部品登録タブで配置可能化が必要です</p>
          <p class="mt-1 text-xs">
            ルート候補は配置可能化のあとに表示されます。部品選択タブでルート入力
            → パッケージ化 → 配置可能化の順で進めてください。
          </p>
          <label class="mt-2 flex flex-col gap-0.5 text-xs">
            ページルート（手入力で先に進める場合）
            <input
              value={manualRouteKey}
              onInput={(e) =>
                setManualRouteKey((e.target as HTMLInputElement).value)}
              placeholder="部品登録タブと同じ routeKey"
              class="input-mono w-full text-xs"
            />
          </label>
        </div>
      )}

      {!layoutSelectorsLocked && (
        <AdvancedManualOverride title="詳細設定 — レイアウト・ルートを直接指定">
          <div class="flex flex-wrap gap-2">
            <input
              value={manualRouteKey}
              onInput={(e) =>
                setManualRouteKey((e.target as HTMLInputElement).value)}
              placeholder="routeKey 手入力"
              class="input-mono flex-1 text-xs"
            />
            <input
              value={manualLayoutId}
              onInput={(e) =>
                setManualLayoutId((e.target as HTMLInputElement).value)}
              placeholder="layoutId UUID 手入力"
              class="input-mono flex-[2] text-xs"
            />
          </div>
        </AdvancedManualOverride>
      )}

      {!canPatch && !selectorsDisabled && (
        <p class="text-sm text-yellow-700 mb-2">
          ルートとレイアウトを選択してから操作してください。
        </p>
      )}

      {packageScopedLayout && (
        <div class="mb-2 rounded border border-blue-200 bg-blue-50 px-3 py-2 text-xs text-blue-900">
          <strong class="block text-sm text-blue-900 mb-1">
            {UX_LAYOUT_EDITOR_SURFACE}
          </strong>
          <span class="text-[0.7rem] text-blue-700">
            左パネルの部品カードをドラッグしてキャンバスへ配置します。
            parentNodeId・slotKey・orderIndex は右ドックの配置インスペクタで編集してください。
            layoutClassRefs は右ドックの配置インスペクタで編集し、canvas に即時反映されます。適用で DB 保存します。
          </span>
        </div>
      )}

      {/* Canvas workspace — maximized viewport block (left dock + canvas + right dock) */}
      <section
        class="ui-builder-canvas-workspace mb-4 flex flex-col overflow-hidden rounded-xl border border-slate-300 bg-white shadow-sm"
        style={{ height: CANVAS_WORKSPACE_HEIGHT, minHeight: CANVAS_WORKSPACE_HEIGHT }}
        aria-label="キャンバスワークスペース"
      >
      {/* layout draft プレビュー & 操作ツールバー */}
      <div class="flex shrink-0 flex-wrap items-center gap-2 border-b border-gray-200 bg-gray-50 px-3 py-2">
        <div class="flex items-center gap-1">
          <button
            type="button"
            onClick={undo}
            disabled={!canUndo}
            class="btn-secondary py-1 px-2 text-xs disabled:opacity-40"
            title="元に戻す (Ctrl+Z)"
            aria-label="元に戻す"
          >
            ↩ 元に戻す
          </button>
          <button
            type="button"
            onClick={redo}
            disabled={!canRedo}
            class="btn-secondary py-1 px-2 text-xs disabled:opacity-40"
            title="やり直す (Ctrl+Y)"
            aria-label="やり直す"
          >
            ↪ やり直す
          </button>
        </div>

        <div class="h-4 w-px bg-gray-300" />

        <details class="text-xs">
          <summary class="cursor-pointer text-gray-600">ルート layoutClassRefs</summary>
          <div class="mt-1 max-w-md">
            <TopologyLayoutClassPicker
              selectedClassRefs={selectedLayoutClassRefs}
              onToggle={toggleRootLayoutClassRef}
              scopeFilter=""
              allowedForAny={["layout_root", "layout_section", "layout_row"]}
            />
          </div>
        </details>

        {draftNodes.length > 0 && (
          <button
            type="button"
            onClick={() => {
              const next: DraftNode[] = [];
              setDraftNodes(next);
              setSelectedNodeId(null);
              pushHistory(next, "キャンバスをクリア");
              setLifecyclePhase("idle");
              announce("キャンバスをクリアしました");
            }}
            class="btn-danger py-0.5 px-2 text-xs"
            aria-label="キャンバスを全クリア"
          >
            クリア
          </button>
        )}

        <div class="h-4 w-px bg-gray-300" />

        {/* Preset controls — SSOT: mock-preset-intake-compiler-ssot.yaml §canvas_workspace_action_group */}
        <button
          type="button"
          class="btn-secondary py-0.5 px-2 text-xs"
          aria-label="ビジュアルモックをアップロード"
          title="SVG/XML ビジュアルモックをインポートしてプリセット作成"
          onClick={() => setPresetDrawerOpen(true)}
        >
          ↑ モックをインポート
        </button>

        <button
          type="button"
          class="btn-secondary py-0.5 px-2 text-xs"
          aria-label="現在のキャンバスをプリセットとして保存"
          title="現在のキャンバス状態をプリセットとして保存"
          disabled={draftNodes.length === 0}
          onClick={handleSaveCanvasAsPreset}
        >
          ☆ プリセット保存
        </button>

        <span class="text-[0.6rem] text-gray-400">|</span>

        <select
          class="rounded border border-gray-300 px-1 py-0.5 text-xs disabled:opacity-50"
          value={selectedPresetId}
          aria-label="保存済みプリセットを選択"
          onFocus={!presetsLoaded ? loadPresetList : undefined}
          onChange={(e) => setSelectedPresetId((e.target as HTMLSelectElement).value)}
        >
          <option value="">プリセットを選択...</option>
          {savedPresets.map((p) => (
            <option key={p.presetId} value={p.presetId}>
              {p.presetLabel}
            </option>
          ))}
        </select>

        <button
          type="button"
          class="btn-secondary py-0.5 px-2 text-xs disabled:opacity-50"
          disabled={!selectedPresetId || presetLoadStatus === "loading" || !packageScopedLayout}
          aria-label="選択したプリセットをキャンバスに読み込む"
          title={!packageScopedLayout ? "ルートパッケージを選択してからプリセットを読み込んでください" : "プリセットを一時キャンバスドラフトに読み込む"}
          onClick={handleLoadPreset}
        >
          {presetLoadStatus === "loading" ? "読み込み中..." : "↓ プリセット読み込み"}
        </button>

        {presetLoadError && (
          <span class="text-xs text-red-600" role="alert">
            {presetLoadError}
          </span>
        )}

        <span class="ml-auto text-xs text-gray-400" aria-live="polite">
          {draftNodes.length} 部品
          {selectedNode
            ? ` — 選択中: ${friendlyComponentLabel(selectedNode.componentKey)}`
            : ""}
        </span>
      </div>

      {/* Preset uploader drawer — SSOT: mock-preset-intake-compiler-ssot.yaml §preset_uploader_surface */}
      <PresetUploaderDrawer
        open={presetDrawerOpen}
        onClose={() => {
          setPresetDrawerOpen(false);
          setCanvasPresetSeed(null);
        }}
        onPresetSaved={(preset) => {
          setSavedPresets((prev) => [preset, ...prev]);
          setSelectedPresetId(preset.presetId);
          setPresetsLoaded(true);
          setCanvasPresetSeed(null);
        }}
        canvasPreset={canvasPresetSeed}
      />

      {legacyLayoutWarning && (
        <div
          class="mx-2 mb-2 flex flex-wrap items-center gap-2 rounded border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-900"
          role="alert"
        >
          <span>
            座標ベース（x/y）のレガシーレイアウトです。フロー配置へ明示的に変換してください。
          </span>
          <button
            type="button"
            class="btn-secondary py-0.5 px-2 text-xs"
            onClick={handleMigrateLegacyLayout}
          >
            フロースタックへ変換
          </button>
        </div>
      )}

      {/* layout draft プレビュー & 操作エリア: left dock + flow canvas + right inspector */}
      <div class={`flex min-h-0 flex-1 gap-2.5 p-2 ${canvasPreviewClass}`}>
        {/* Left Drawer shell — display-density chrome only; bucket semantics stay inside existing panel. */}
        {leftDrawerOpen && (
          <div
            class="ui-builder-left-drawer-shell flex shrink-0 flex-col overflow-hidden rounded-lg border border-blue-100 bg-blue-50/50 shadow-sm"
            data-ui-builder-drawer-shell="left"
            data-drawer-state-boundary={UI_BUILDER_DRAWER_STATE_BOUNDARY}
          >
            <div class="flex items-center justify-between border-b border-blue-100 bg-white px-2 py-1 text-[0.62rem] font-semibold text-blue-900">
              <span>部品パネル</span>
              <button
                type="button"
                class="rounded border border-blue-200 bg-blue-50 px-1.5 py-0.5 text-[0.62rem] text-blue-800 hover:bg-blue-100"
                aria-label="左パネルを閉じる"
                onClick={() => setLeftDrawerOpen(false)}
              >
                ×
              </button>
            </div>
            {packageScopedLayout ? (
              <LeftDockedPalettePanel
                onDragStart={handleDragStartPalette}
                onAddToCanvas={handleAddFromPalette}
                paletteEntries={paletteEntries}
                paletteStatus={paletteStatus}
                onAddStructuralHtmlTag={addStructuralHtmlNode}
                selectorsDisabled={selectorsDisabled}
              />
            ) : (
              <aside
                class="left-docked-panel flex shrink-0 flex-col items-center justify-center rounded-lg border border-dashed border-slate-200 bg-slate-50 px-2 py-4 text-center text-[0.65rem] text-slate-400"
                style={{ width: "clamp(180px, 15vw, 240px)", minWidth: "180px", maxWidth: "240px" }}
                aria-label={UX_COMPONENT_ADD_PANEL_LABEL}
              >
                {UX_ROUTE_KEY_REQUIRED_FOR_CANVAS}
              </aside>
            )}
          </div>
        )}
        <div class="relative flex min-h-0 min-w-0 flex-1 flex-col">
          <div class="pointer-events-none absolute inset-x-2 top-2 z-20 flex items-start justify-between">
            <button
              type="button"
              class="pointer-events-auto rounded-full border border-blue-200 bg-white/90 px-2 py-1 text-xs font-semibold text-blue-800 shadow-sm hover:bg-blue-50"
              aria-label={leftDrawerOpen ? "左パネルを閉じる" : "左パネルを開く"}
              aria-pressed={leftDrawerOpen}
              onClick={() => setLeftDrawerOpen((open) => !open)}
            >
              ◧
            </button>
            <button
              type="button"
              class="pointer-events-auto rounded-full border border-indigo-200 bg-white/90 px-2 py-1 text-xs font-semibold text-indigo-800 shadow-sm hover:bg-indigo-50"
              aria-label={rightDrawerOpen ? "右パネルを閉じる" : "右パネルを開く"}
              aria-pressed={rightDrawerOpen}
              onClick={() => setRightDrawerOpen((open) => !open)}
            >
              ◨
            </button>
          </div>
          <FlowLayoutCanvas
            nodes={draftNodes}
            selectedNodeId={selectedNodeId}
            rootLayoutClassRefs={selectedLayoutClassRefs}
            designDraftByNodeId={designDraftByNodeId}
            canvasRef={canvasRef}
            minHeight={CANVAS_MIN_HEIGHT}
            onSelectNode={(id) => {
              setSelectedNodeId(id);
              setRightDrawerOpen(true);
            }}
            onDeselectAll={() => setSelectedNodeId(null)}
            onDragOver={handleDragOverCanvas}
            onDrop={handleDropOnCanvas}
            onDeleteNode={removeNode}
            onAddFromEmptyState={packageScopedLayout
              ? handleAddFromEmptyState
              : undefined}
            allowEmptyStateTemplates={packageScopedLayout}
            calcTriggerNodeIds={calcTriggerNodeIds}
            calcOverridesByNodeId={calcOverridesByNodeId}
            onNodeValueChange={handleNodeValueChange}
          />
        </div>

        {rightDrawerOpen && (
          <div
            class="ui-builder-right-drawer-shell flex shrink-0 flex-col overflow-hidden rounded-lg border border-indigo-100 bg-indigo-50/50 shadow-sm"
            data-ui-builder-drawer-shell="right"
            data-drawer-state-boundary={UI_BUILDER_DRAWER_STATE_BOUNDARY}
          >
            <div class="flex items-center justify-between border-b border-indigo-100 bg-white px-2 py-1 text-[0.62rem] font-semibold text-indigo-900">
              <span>インスペクターパネル</span>
              <button
                type="button"
                class="rounded border border-indigo-200 bg-indigo-50 px-1.5 py-0.5 text-[0.62rem] text-indigo-800 hover:bg-indigo-100"
                aria-label="右パネルを閉じる"
                onClick={() => setRightDrawerOpen(false)}
              >
                ×
              </button>
            </div>
            <LayoutRightDock
              draftNodes={draftNodes}
              selectedNodeId={selectedNodeId}
              selectedNode={selectedNode}
              packageId={scopedPackageId ?? ""}
              onSelectNode={(id) => setSelectedNodeId(id)}
              canvasDesignDraft={selectedNodeId
                ? designDraftByNodeId.get(selectedNodeId)
                : undefined}
              onReparent={reparentNode}
              onCopy={copyNode}
              onDelete={removeNode}
              slotKeyCandidates={slotKeyCandidates}
              onUpdateNode={(updates) => selectedNode && updateNode(selectedNode.nodeId, updates)}
              onCommitNode={(updates, label) => selectedNode && commitNodeUpdate(selectedNode.nodeId, updates, label)}
              onToggleLayoutClassRef={(classKey) =>
                selectedNode && toggleNodeLayoutClassRef(selectedNode.nodeId, classKey)}
              onDesignChange={handleDesignDraftChange}
              routeCandidates={routeOptions}
              calculationBindings={calculationBindings}
              draftNodeIds={draftNodes.map((n) => n.nodeId)}
              calcResults={calcResults}
              onCalcBindingsChange={setCalculationBindings}
              emissionDataJson={emissionDataJson}
              onEmissionDataJsonChange={setEmissionDataJson}
            />
          </div>
        )}
      </div>
      </section>

      {/* _tmp auto-save status indicator */}
      {(tmpSaveStatus === "saving" || tmpSaveStatus === "saved" ||
        tmpSaveStatus === "error") && (
        <div class="flex items-center gap-1 text-xs mb-2">
          {tmpSaveStatus === "saving" && (
            <span class="text-amber-600">自動保存中…</span>
          )}
          {tmpSaveStatus === "saved" && (
            <span class="text-green-600">自動保存済</span>
          )}
          {tmpSaveStatus === "error" && (
            <span class="text-red-500">
              自動保存エラー（sessionStorage は有効）
            </span>
          )}
        </div>
      )}

      {(() => {
        const hasReadinessError = !canPatch ||
          draftNodes.some((n) => n.isDraftOnly) || !!layoutClassRefError;
        return (
          <details class="mb-3" open={hasReadinessError}>
            <summary
              class={`cursor-pointer rounded px-2 py-1 text-xs font-semibold ${
                hasReadinessError
                  ? "bg-amber-100 text-amber-900"
                  : "bg-green-50 text-green-700"
              }`}
            >
              {hasReadinessError
                ? "⚠ 保存前チェック（要確認）"
                : "✓ 保存前チェック（問題なし）"}
            </summary>
            <ApplyReadinessPanel
              canPatch={canPatch}
              effectiveRouteKey={effectiveRouteKey}
              effectiveLayoutId={effectiveLayoutId}
              draftNodes={draftNodes}
              layoutClassRefError={layoutClassRefError}
            />
          </details>
        );
      })()}

      <div class="mb-3 rounded border border-slate-200 bg-slate-50 px-3 py-2">
        <div class="flex flex-wrap items-center gap-2">
          <button
            onClick={openLayoutApplyModal}
            disabled={loading || !canPatch}
            class="btn-success min-w-[120px]"
            aria-label="適用 — モーダルでバリデーション後に DB へ反映"
          >
            適用
          </button>
          {loading && (
            <span
              class="flex items-center text-sm text-gray-500"
              aria-live="polite"
            >
              実行中...
            </span>
          )}
        </div>
        <p class="mt-1 text-[0.65rem] text-gray-400">
          canvas は編集中の配置をリアルタイム表示します。適用でバリデーション後に
          layout_patch_json へ保存します。
        </p>
      </div>

      <LayoutPatchApplyModal
        open={layoutApplyModalOpen}
        phase={layoutApplyModalPhase}
        summary={patchSummary
          ? {
            valid: patchSummary.valid,
            message: patchSummary.message,
            nodeCount: patchSummary.nodeCount,
            draftOnlyCount: patchSummary.draftOnlyCount,
            routeKey: patchSummary.routeKey,
            layoutId: patchSummary.layoutId,
            layoutKey: patchSummary.layoutKey,
            errors: patchSummary.errors,
          }
          : null}
        routeKey={effectiveRouteKey}
        layoutId={effectiveLayoutId}
        layoutLabel={selectedLayout?.layoutKey ?? shortId(effectiveLayoutId)}
        loading={loading}
        onClose={() => {
          setLayoutApplyModalOpen(false);
          setLayoutApplyModalPhase("validating");
        }}
        onConfirmApply={() => callLayoutPatch("apply", { viaApplyModal: true })}
        onGoDesign={() => {
          setLayoutApplyModalOpen(false);
          setLayoutApplyModalPhase("validating");
          setRightDrawerOpen(true);
          announce("右パネルのデザインインスペクタで選択ノードを編集できます");
        }}
      />

      {/* Gap 3: Actionable error panel */}
      {patchErrors.length > 0 && !layoutApplyModalOpen && (
        <ActionableValidationErrorPanel
          errors={patchErrors}
          title="エラー — 修正方法"
        />
      )}

      <Accordion title="詳細情報（開発者向け）" defaultOpen={false}>
        <p class="text-muted-xs mb-2">
          フロー配置 (parentNodeId/orderIndex/layoutClassRefs + width/height)。x/y は出力しません。
        </p>
        <pre class="pre-box max-h-40 overflow-y-auto m-0 mb-2">{tensorPatchJson}</pre>
        {debugJson && (
          <pre class="pre-box max-h-[200px] overflow-y-auto border border-gray-200 m-0">{debugJson}</pre>
        )}
      </Accordion>
      <ConfirmDialogHost />
    </div>
  );
}

// ─── メインエクスポート ────────────────────────────────────────────────────────

type AdminPackageComponentRow = {
  componentId: string;
  componentKey: string;
  componentKind: string;
};

type AdminPackageWiringRow = {
  wiringId: string;
  wiringKey: string;
  wiringKind: string;
  targetSurface: string;
  targetRef?: string | null;
};

/**
 * Normal-view route navigation preset for package-level wiring.
 * Allows authors to configure "click → navigate to route" in business vocabulary
 * without exposing raw dispatcher fields.
 * Raw wiring fields remain in <details> PackageWiringEditor below.
 */
function RouteNavigationWiringPreset({
  selectedPackageId,
  routeCandidates,
}: {
  selectedPackageId: string;
  routeCandidates: string[];
}): JSX.Element {
  const [savedRouteKey, setSavedRouteKey] = useState<string | null>(null);
  const [selectedRouteKey, setSelectedRouteKey] = useState("");
  const [wiringId, setWiringId] = useState<string | null>(null);
  const [loadStatus, setLoadStatus] = useState<string | null>(null);
  const [saveStatus, setSaveStatus] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!selectedPackageId) {
      setSavedRouteKey(null);
      setSelectedRouteKey("");
      setWiringId(null);
      setLoadStatus(null);
      setSaveStatus(null);
      return;
    }
    (async () => {
      setLoadStatus("読み込み中…");
      setSaveStatus(null);
      const body = await dispatchAdminOp("ui_topology", "get_package_wiring", {
        packageId: selectedPackageId,
      });
      const data = body?.emission?.data as AdminPackageWiringRow | undefined;
      if (data?.wiringId) {
        setWiringId(data.wiringId);
        if (isRouteNavigationTargetRef(data.targetRef)) {
          const rk = parseRouteNavigationTargetRef(data.targetRef ?? "");
          setSavedRouteKey(rk);
          setSelectedRouteKey(rk ?? "");
        } else {
          setSavedRouteKey(null);
          setSelectedRouteKey("");
        }
        setLoadStatus(null);
      } else {
        setWiringId(null);
        setSavedRouteKey(null);
        setLoadStatus(
          body?.errors?.[0]?.message ?? "配線が未登録です（ルートを確定後に配線が生成されます）。",
        );
      }
    })();
  }, [selectedPackageId]);

  const handleSaveRouteNavigation = async () => {
    if (!wiringId) {
      setSaveStatus("配線が未登録です。先にルートを選択してパッケージを確定してください。");
      return;
    }
    setSaving(true);
    setSaveStatus(null);
    const nextTargetRef = selectedRouteKey
      ? encodeRouteNavigationTargetRef(selectedRouteKey)
      : null;
    try {
      const body = await dispatchAdminOp(
        "ui_topology",
        "update_package_wiring",
        {
          packageId: selectedPackageId,
          wiringId,
          wiringKind: "navigation",
          targetSurface: "route",
          targetRef: nextTargetRef,
        },
      );
      const refreshed = body?.emission?.data?.wiring as
        | AdminPackageWiringRow
        | undefined;
      if (body?.success && refreshed?.wiringId) {
        setWiringId(refreshed.wiringId);
        if (isRouteNavigationTargetRef(refreshed.targetRef)) {
          const rk = parseRouteNavigationTargetRef(refreshed.targetRef ?? "");
          setSavedRouteKey(rk);
          setSelectedRouteKey(rk ?? "");
        } else {
          setSavedRouteKey(null);
          setSelectedRouteKey("");
        }
        setSaveStatus("ルート遷移の配線を保存しました。");
      } else {
        setSaveStatus(body?.errors?.[0]?.message ?? "保存に失敗しました。");
      }
    } finally {
      setSaving(false);
    }
  };

  const isDirty = selectedRouteKey !== (savedRouteKey ?? "");

  return (
    <section class="mb-4 rounded border border-emerald-100 bg-emerald-50/40 p-3 text-xs">
      <h4 class="mb-1 font-semibold text-emerald-900">
        {UX_ROUTE_NAVIGATION_PRESET_LABEL}
      </h4>
      {loadStatus
        ? <p class="text-slate-600">{loadStatus}</p>
        : (
          <>
            <label class="block">
              {UX_ROUTE_NAVIGATION_ROUTE_SELECT_LABEL}
              <select
                class="mt-1 w-full rounded border bg-white px-2 py-1 text-xs"
                value={selectedRouteKey}
                onChange={(e) =>
                  setSelectedRouteKey((e.target as HTMLSelectElement).value)}
              >
                <option value="">{UX_ROUTE_NAVIGATION_NONE_LABEL}</option>
                {routeCandidates.map((rk) => (
                  <option key={rk} value={rk}>{rk}</option>
                ))}
              </select>
            </label>
            {savedRouteKey && (
              <p class="mt-1 text-[0.7rem] text-emerald-800">
                保存済み: <span class="font-mono">{savedRouteKey}</span>
              </p>
            )}
            <button
              type="button"
              class={`btn-primary mt-2 text-xs ${
                (!isDirty || saving || !wiringId)
                  ? "opacity-50 cursor-not-allowed"
                  : ""
              }`}
              disabled={!isDirty || saving || !wiringId}
              onClick={handleSaveRouteNavigation}
            >
              {saving ? "保存中…" : UX_ROUTE_NAVIGATION_SAVE_LABEL}
            </button>
          </>
        )}
      {saveStatus && (
        <p class="mt-2 text-xs font-semibold text-emerald-900">{saveStatus}</p>
      )}
    </section>
  );
}

function PackageWiringEditor({
  selectedPackageId,
  packageComponents,
  onWiringSaved,
}: {
  selectedPackageId: string;
  packageComponents: AdminPackageComponentRow[];
  onWiringSaved?: (wiring: AdminPackageWiringRow) => void;
}): JSX.Element {
  const { confirm, ConfirmDialogHost } = useConfirm();
  const [wiring, setWiring] = useState<AdminPackageWiringRow | null>(null);
  const [wiringKind, setWiringKind] = useState("");
  const [targetSurface, setTargetSurface] = useState("route");
  const [targetRef, setTargetRef] = useState("");
  const [loadStatus, setLoadStatus] = useState<string | null>(null);
  const [saveStatus, setSaveStatus] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [screenWiringCandidates, setScreenWiringCandidates] = useState<
    ScreenReadQueryWiringCandidate[]
  >([]);
  const [manifestOptions, setManifestOptions] = useState<
    ManifestPickerOption[]
  >([]);
  const [loadingManifests, setLoadingManifests] = useState(false);
  const [selectedManifestId, setSelectedManifestId] = useState("");
  const [selectedManifestWiringKey, setSelectedManifestWiringKey] = useState(
    "",
  );

  const wiringKindOptions = buildWiringKindSelectOptions(
    packageComponents.map((c) => c.componentKind),
    screenWiringCandidates,
  );

  const applyLoadedWiring = (data: AdminPackageWiringRow) => {
    setWiring(data);
    setWiringKind(data.wiringKind);
    setTargetSurface(data.targetSurface);
    setTargetRef(data.targetRef ?? "");
    if (data.targetSurface === "manifest") {
      const manifestId = manifestIdFromTargetRef(
        data.targetRef ?? "",
        "manifest",
      );
      const wiringKey = manifestWiringKeyFromTargetRef(
        data.targetRef ?? "",
        "manifest",
      );
      setSelectedManifestId(manifestId);
      setSelectedManifestWiringKey(wiringKey);
    } else {
      setSelectedManifestId("");
      setSelectedManifestWiringKey("");
    }
  };

  useEffect(() => {
    if (!selectedPackageId) {
      setWiring(null);
      setWiringKind("");
      setTargetSurface("route");
      setTargetRef("");
      setSelectedManifestId("");
      setSelectedManifestWiringKey("");
      setLoadStatus(null);
      setSaveStatus(null);
      return;
    }
    (async () => {
      setLoadStatus("配線を読み込み中...");
      setSaveStatus(null);
      const body = await dispatchAdminOp("ui_topology", "get_package_wiring", {
        packageId: selectedPackageId,
      });
      const data = body?.emission?.data as AdminPackageWiringRow | undefined;
      if (data?.wiringId) {
        applyLoadedWiring(data);
        setLoadStatus(null);
      } else {
        setWiring(null);
        setLoadStatus(
          body?.errors?.[0]?.message ??
            "このパッケージに配線がありません。先に部品登録タブで配置可能化してください。",
        );
      }
    })();
  }, [selectedPackageId]);

  useEffect(() => {
    if (targetSurface !== "manifest") {
      setManifestOptions([]);
      setScreenWiringCandidates([]);
      return;
    }
    (async () => {
      setLoadingManifests(true);
      try {
        const [active, draft] = await Promise.all([
          listAdminManifests("active"),
          listAdminManifests("draft"),
        ]);
        const items = mergeManifestPickerOptions(
          await buildManifestPickerOptions(active ?? []),
          await buildManifestPickerOptions(draft ?? []),
        );
        setManifestOptions(items);
      } finally {
        setLoadingManifests(false);
      }
    })();
  }, [targetSurface]);

  useEffect(() => {
    if (targetSurface !== "manifest" || !selectedManifestId.trim()) {
      setScreenWiringCandidates([]);
      return;
    }
    (async () => {
      const body = await dispatchAdminOp(
        "manifest",
        "list_screen_read_query_wiring",
        {
          manifestId: selectedManifestId.trim(),
        },
      );
      const data = body?.emission?.data as {
        candidates?: ScreenReadQueryWiringCandidate[];
      } | undefined;
      if (Array.isArray(data?.candidates)) {
        setScreenWiringCandidates(data.candidates);
        return;
      }
      const getBody = await dispatchAdminOp("manifest", "get", {
        manifestId: selectedManifestId.trim(),
      });
      const detail = getBody?.emission?.data as
        | { topologyRawJson?: string }
        | undefined;
      if (typeof detail?.topologyRawJson === "string") {
        setScreenWiringCandidates(
          buildScreenReadQueryWiringCandidates(detail.topologyRawJson),
        );
      } else {
        setScreenWiringCandidates([]);
      }
    })();
  }, [targetSurface, selectedManifestId]);

  const resolvedTargetRefForSave = (): string | null => {
    if (targetSurface === "manifest") {
      if (!selectedManifestId.trim()) return null;
      return encodeManifestPackageTargetRef(
        selectedManifestId,
        selectedManifestWiringKey,
      );
    }
    return targetRef.trim() || null;
  };

  const handleSaveWiring = async () => {
    if (
      !selectedPackageId || !wiring?.wiringId || !wiringKind.trim() ||
      !targetSurface
    ) {
      setSaveStatus("配線種別と接続先サーフェスを入力してください。");
      return;
    }
    if (targetSurface === "manifest" && !selectedManifestId.trim()) {
      setSaveStatus("接続先ページを選択してください。");
      return;
    }
    if (!(await confirm("パッケージ配線を保存します。よろしいですか？"))) {
      return;
    }
    setSaving(true);
    setSaveStatus(null);
    const nextTargetRef = resolvedTargetRefForSave();
    try {
      const body = await dispatchAdminOp(
        "ui_topology",
        "update_package_wiring",
        {
          packageId: selectedPackageId,
          wiringId: wiring.wiringId,
          wiringKind: wiringKind.trim(),
          targetSurface,
          targetRef: nextTargetRef,
        },
      );
      const refreshed = body?.emission?.data?.wiring as
        | AdminPackageWiringRow
        | undefined;
      if (body?.success && refreshed?.wiringId) {
        applyLoadedWiring(refreshed);
        setSaveStatus("パッケージ配線を保存しました。");
        onWiringSaved?.(refreshed);
      } else {
        setSaveStatus(
          body?.errors?.[0]?.message ?? "配線の保存に失敗しました。",
        );
      }
    } finally {
      setSaving(false);
    }
  };

  const handleSelectManifestWiring = (wiringKey: string) => {
    setSelectedManifestWiringKey(wiringKey);
    if (wiringKey && !wiringKindOptions.includes(wiringKind)) {
      setWiringKind(wiringKey);
    }
  };

  return (
    <section class="mt-3 rounded border border-indigo-100 bg-indigo-50/40 p-3 text-xs">
      <h4 class="font-semibold text-indigo-900">パッケージ配線（編集）</h4>
      <p class="text-muted-xs mb-2">
        保存済み配線を読み込み、一覧から選んで接続先を設定します（UUID
        の手入力は不要です）。
      </p>
      {loadStatus && <p class="mb-2 text-slate-600">{loadStatus}</p>}
      {wiring && (
        <>
          <div class="mb-3 rounded border border-slate-200 bg-white px-3 py-2 text-[0.7rem] text-slate-700">
            <p class="font-semibold text-slate-900">保存済み配線</p>
            <p class="mt-1 font-mono">
              kind: {wiring.wiringKind} / surface: {wiring.targetSurface}
              {wiring.targetRef ? ` / ref: ${wiring.targetRef}` : ""}
            </p>
          </div>
          <p class="mb-2 font-mono text-[0.65rem] text-slate-500">
            wiring: {wiring.wiringId.slice(0, 8)}… / key: {wiring.wiringKey}
          </p>
          <div class="grid gap-3 sm:grid-cols-2">
            <label class="block">
              配線種別 (wiring_kind)
              <select
                class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                value={wiringKind}
                onChange={(e) =>
                  setWiringKind((e.target as HTMLSelectElement).value)}
              >
                <option value="">— 選択 —</option>
                {wiringKindOptions.map((k) => (
                  <option key={k} value={k}>{k}</option>
                ))}
              </select>
            </label>
            <label class="block">
              接続先サーフェス
              <select
                class="mt-1 w-full rounded border px-2 py-1 text-xs"
                value={targetSurface}
                onChange={(e) => {
                  const next = (e.target as HTMLSelectElement).value;
                  setTargetSurface(next);
                  if (next !== "manifest") {
                    setSelectedManifestId("");
                    setSelectedManifestWiringKey("");
                  }
                }}
              >
                {PACKAGE_WIRING_TARGET_SURFACES.map((s) => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
            </label>

            {targetSurface === "manifest" && (
              <div class="sm:col-span-2 space-y-3 rounded border border-indigo-200 bg-white p-3">
                <label class="block">
                  接続先ページ
                  <select
                    class="mt-1 w-full rounded border px-2 py-1 text-xs"
                    value={selectedManifestId}
                    onChange={(e) => {
                      const next = (e.target as HTMLSelectElement).value;
                      setSelectedManifestId(next);
                      setSelectedManifestWiringKey("");
                    }}
                  >
                    <option value="">— ページを選択 —</option>
                    {manifestOptions.map((m) => (
                      <option key={m.manifestId} value={m.manifestId}>
                        {m.label} [{m.status}]
                      </option>
                    ))}
                  </select>
                </label>
                {loadingManifests && (
                  <p class="text-slate-500">ページ一覧を読み込み中…</p>
                )}
                {!loadingManifests && manifestOptions.length === 0 && (
                  <p class="text-amber-800">
                    選択できるページがありません。先にコンテンツ管理で Step 1
                    を保存してください。
                  </p>
                )}
                {selectedManifestId && (
                  <fieldset>
                    <legend class="mb-2 font-medium text-slate-800">
                      Step3 read/query 配線（接続先参照）
                    </legend>
                    {screenWiringCandidates.length === 0
                      ? (
                        <p class="text-slate-500">
                          このページに read/query 配線候補がありません。Step 3
                          の保存後に再度お試しください。
                        </p>
                      )
                      : (
                        <ul class="space-y-2">
                          <li>
                            <label class="flex cursor-pointer items-start gap-2 rounded border border-slate-200 px-2 py-1.5 hover:bg-slate-50">
                              <input
                                type="radio"
                                name={`manifest-wiring-${wiring.wiringId}`}
                                checked={selectedManifestWiringKey === ""}
                                onChange={() => handleSelectManifestWiring("")}
                              />
                              <span>接続なし（ページのみ紐づけ）</span>
                            </label>
                          </li>
                          {screenWiringCandidates.map((c) => (
                            <li key={c.wiringKey}>
                              <label class="flex cursor-pointer items-start gap-2 rounded border border-slate-200 px-2 py-1.5 hover:bg-slate-50">
                                <input
                                  type="radio"
                                  name={`manifest-wiring-${wiring.wiringId}`}
                                  checked={selectedManifestWiringKey ===
                                    c.wiringKey}
                                  onChange={() =>
                                    handleSelectManifestWiring(c.wiringKey)}
                                />
                                <span>
                                  <span class="font-mono text-indigo-900">
                                    {c.wiringKey}
                                  </span>
                                  <span class="mt-0.5 block text-slate-600">
                                    {c.label}
                                  </span>
                                </span>
                              </label>
                            </li>
                          ))}
                        </ul>
                      )}
                  </fieldset>
                )}
              </div>
            )}

            {targetSurface !== "manifest" && (
              <label class="block sm:col-span-2">
                接続先参照 (target_ref、任意)
                <input
                  class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                  value={targetRef}
                  placeholder="例: route:admin:demo"
                  onInput={(e) =>
                    setTargetRef((e.target as HTMLInputElement).value)}
                />
              </label>
            )}
          </div>
          <button
            type="button"
            class="btn-primary mt-2 text-xs"
            disabled={saving}
            onClick={handleSaveWiring}
          >
            {saving ? "保存中…" : "配線を保存"}
          </button>
        </>
      )}
      {saveStatus && <p class="mt-2">{saveStatus}</p>}
      <details class="mt-2 text-[0.65rem] text-slate-500">
        <summary class="cursor-pointer">技術情報（dispatcher raw）</summary>
        <p class="mt-1">
          wiring_id は <code>ui_topology_tensor</code>{" "}
          経由でパッケージに紐づきます。 manifest 接続時の target_ref は{" "}
          <code>manifest:&lt;uuid&gt;:&lt;wiringKey&gt;</code>{" "}
          形式で保存されます。
        </p>
      </details>
      <ConfirmDialogHost />
    </section>
  );
}

async function buildManifestPickerOptions(
  items: { manifestId: string; status: string }[],
): Promise<ManifestPickerOption[]> {
  return Promise.all(items.map(async (item) => {
    const stored = getStoredScreenLabel(item.manifestId);
    if (stored) {
      return {
        manifestId: item.manifestId,
        label: stored,
        status: item.status,
      };
    }
    const detail = await getAdminManifest(item.manifestId);
    const shape = detail
      ? extractScreenDataShapeFromTopology(detail.topologyRawJson)
      : null;
    const label = resolveVisibleTopologyName(shape?.userFacingTopologyLabel, shape?.topologySystemName) ||
      `${item.status} ${item.manifestId.slice(0, 8)}…`;
    return { manifestId: item.manifestId, label, status: item.status };
  }));
}

type SavedComponentDesignRow = {
  designId: string;
  name: string;
  componentId: string | null;
  layoutNodeId: string | null;
  cssTokenRefs: string[];
  responsiveTokenRefs: ResponsiveTokenRules;
  inlineText: string;
  linkHref: string;
  linkTarget: string;
  reactionIntent: string;
  classname: string;
  tailwind: string;
  hasDesignTmpDraft?: boolean;
};

type LayoutNodeDesignOption = {
  nodeId: string;
  label: string;
  nodeKind?: LayoutNodeKind;
  htmlTag?: StructuralHtmlTag;
};

function defaultDesignName(componentKey: string): string {
  const slug =
    componentKey.split("/").pop()?.replace(/[^a-zA-Z0-9._-]+/g, "_") ?? "part";
  return `${slug}_design`;
}

function mapSavedDesignRow(
  raw: Record<string, unknown>,
): SavedComponentDesignRow {
  const refs = Array.isArray(raw.cssTokenRefs)
    ? raw.cssTokenRefs.filter((v): v is string => typeof v === "string")
    : [];
  const responsiveRaw = raw.responsiveTokenRefs;
  const responsiveTokenRefs = typeof responsiveRaw === "object" &&
      responsiveRaw !== null &&
      !Array.isArray(responsiveRaw)
    ? responsiveRaw as ResponsiveTokenRules
    : {};
  return {
    designId: String(raw.designId ?? ""),
    name: String(raw.name ?? ""),
    componentId: typeof raw.componentId === "string" ? raw.componentId : null,
    layoutNodeId: typeof raw.layoutNodeId === "string"
      ? raw.layoutNodeId
      : null,
    cssTokenRefs: refs,
    responsiveTokenRefs,
    inlineText: typeof raw.inlineText === "string" ? raw.inlineText : "",
    linkHref: typeof raw.linkHref === "string" ? raw.linkHref : "",
    linkTarget: typeof raw.linkTarget === "string" ? raw.linkTarget : "",
    reactionIntent: typeof raw.reactionIntent === "string"
      ? raw.reactionIntent
      : "",
    classname: typeof raw.classname === "string" ? raw.classname : "",
    tailwind: typeof raw.tailwind === "string" ? raw.tailwind : "",
    hasDesignTmpDraft: raw.hasDesignTmpDraft === true,
  };
}

function designPreviewDraft(
  inlineText: string,
  linkHref: string,
  linkTarget: string,
): DesignDraft {
  return {
    inlineText: inlineText.trim() || undefined,
    linkHref: linkHref.trim() || undefined,
    linkTarget: linkTarget.trim() || undefined,
    // cssTokenRefs intentionally excluded — token updates go via direct onDesignPreviewChange
  };
}

function hasCanvasDesignDraft(draft: DesignDraft | undefined): boolean {
  if (!draft) return false;
  return Boolean(
    draft.inlineText?.trim() ||
    draft.linkHref?.trim() ||
    draft.linkTarget?.trim() ||
    (draft.cssTokenRefs && draft.cssTokenRefs.length > 0),
  );
}

function hydrateDesignFormFromDraft(
  draft: DesignDraft,
  setters: {
    setInlineText: (v: string) => void;
    setLinkHref: (v: string) => void;
    setLinkTarget: (v: string) => void;
    setCssTokenRefs: (v: string[]) => void;
  },
): void {
  setters.setInlineText(draft.inlineText ?? "");
  setters.setLinkHref(draft.linkHref ?? "");
  setters.setLinkTarget(draft.linkTarget ?? "");
  setters.setCssTokenRefs(draft.cssTokenRefs ?? []);
}

// ─── ローカル計算バインドコンポーネント ────────────────────────────────────────

const ALLOWED_OPERATIONS = [
  "multiply", "add", "subtract", "divide", "percent",
  "taxIncluded", "taxAmount", "round", "floor", "ceil",
] as const;

const ROUNDING_POLICIES: RoundingPolicy[] = ["none", "round", "floor", "ceil"];

const TARGET_PROPS = ["value", "result", "preview", "inlineText"];

function makeCalcId(): string {
  return `calc_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`;
}

function emptyCalcBinding(targetNodeId: string = ""): CalcBinding {
  return {
    calculationId: makeCalcId(),
    variables: {
      a: { kind: "literal", value: 0 },
      b: { kind: "literal", value: 0 },
    },
    operation: { op: "multiply", a: "a", b: "b" },
    targetNodeId,
    targetProp: "value",
  };
}

function LocalCalcBindingPanel({
  bindings,
  draftNodeIds,
  calcResults,
  onBindingsChange,
  emissionDataJson,
  onEmissionDataJsonChange,
}: {
  bindings: CalcBinding[];
  draftNodeIds: string[];
  calcResults: ReadonlyMap<string, { targetNodeId: string; targetProp: string; result: { ok: true; value: number } | { ok: false; error: string } }>;
  onBindingsChange: (bindings: CalcBinding[]) => void;
  emissionDataJson: string;
  onEmissionDataJsonChange: (json: string) => void;
}): JSX.Element {
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [emissionJsonError, setEmissionJsonError] = useState<string | null>(null);

  function addBinding() {
    const b = emptyCalcBinding(draftNodeIds[0] ?? "");
    onBindingsChange([...bindings, b]);
    setExpandedId(b.calculationId);
  }

  function removeBinding(id: string) {
    onBindingsChange(bindings.filter((b) => b.calculationId !== id));
    if (expandedId === id) setExpandedId(null);
  }

  function updateBinding(updated: CalcBinding) {
    onBindingsChange(bindings.map((b) => b.calculationId === updated.calculationId ? updated : b));
  }

  return (
    <div class="flex flex-col gap-2 text-xs">
      <p class="text-[0.65rem] text-slate-500">
        入力変更で backend dispatch なしに計算値を即時反映します。業務基準値は emission.data.* / ruleTable source を使ってください。
      </p>
      <details class="rounded border border-slate-200 p-1.5">
        <summary class="cursor-pointer text-[0.65rem] font-semibold text-slate-600">
          参照データ (emission.data.*) — テスト用 JSON
        </summary>
        <p class="mt-1 text-[0.6rem] text-slate-400">
          本番では backend から emission.data に自動供給されます。ここで入力した JSON はキャンバスプレビューの計算テストにのみ使われます。
        </p>
        <textarea
          class={`mt-1 w-full rounded border px-1 py-0.5 font-mono text-[0.65rem] ${emissionJsonError ? "border-red-400 bg-red-50" : "border-slate-300 bg-white"}`}
          rows={5}
          value={emissionDataJson}
          placeholder={'{\n  "laborRates": [{"rate": 2500, "priority": 1, "enabled": true}]\n}'}
          onInput={(e) => {
            const raw = (e.currentTarget as HTMLTextAreaElement).value;
            onEmissionDataJsonChange(raw);
            try {
              JSON.parse(raw || "{}");
              setEmissionJsonError(null);
            } catch (err) {
              setEmissionJsonError(err instanceof Error ? err.message : "JSON parse error");
            }
          }}
        />
        {emissionJsonError && (
          <p class="mt-0.5 text-[0.6rem] text-red-500">{emissionJsonError}</p>
        )}
      </details>
      {bindings.length === 0 && (
        <p class="text-slate-400">計算バインドがありません。</p>
      )}
      {bindings.map((binding) => {
        const resultEntry = [...calcResults.values()].find(
          (e) => e.targetNodeId === binding.targetNodeId && e.targetProp === binding.targetProp,
        );
        const errors = validateCalcBinding(binding);
        const isExpanded = expandedId === binding.calculationId;
        return (
          <div
            key={binding.calculationId}
            class="rounded border border-slate-200 bg-slate-50 p-2"
          >
            <div class="flex items-center gap-1">
              <button
                type="button"
                class="flex-1 text-left text-[0.7rem] font-semibold text-slate-700"
                onClick={() => setExpandedId(isExpanded ? null : binding.calculationId)}
              >
                {isExpanded ? "▲" : "▼"} {binding.calculationId}
              </button>
              {errors.length > 0 && (
                <span class="rounded bg-red-100 px-1 text-[0.6rem] text-red-600">
                  エラー {errors.length}
                </span>
              )}
              {resultEntry?.result.ok && (
                <span class="rounded bg-green-100 px-1 font-mono text-[0.65rem] text-green-700">
                  = {resultEntry.result.value}
                </span>
              )}
              {resultEntry && !resultEntry.result.ok && (
                <span class="rounded bg-red-100 px-1 text-[0.6rem] text-red-600" title={(resultEntry.result as { ok: false; error: string }).error}>
                  !
                </span>
              )}
              <button
                type="button"
                class="ml-auto text-[0.6rem] text-red-400 hover:text-red-600"
                onClick={() => removeBinding(binding.calculationId)}
                aria-label="削除"
              >
                ✕
              </button>
            </div>
            {isExpanded && (
              <CalcBindingEditor
                binding={binding}
                draftNodeIds={draftNodeIds}
                onChange={updateBinding}
              />
            )}
            {isExpanded && errors.length > 0 && (
              <ul class="mt-1 list-inside list-disc text-[0.6rem] text-red-600">
                {errors.map((e, i) => <li key={i}>{e}</li>)}
              </ul>
            )}
          </div>
        );
      })}
      <button
        type="button"
        class="mt-1 rounded border border-dashed border-blue-300 px-2 py-1 text-[0.7rem] text-blue-600 hover:bg-blue-50"
        onClick={addBinding}
      >
        + ローカル計算を追加
      </button>
    </div>
  );
}

function CalcBindingEditor({
  binding,
  draftNodeIds,
  onChange,
}: {
  binding: CalcBinding;
  draftNodeIds: string[];
  onChange: (b: CalcBinding) => void;
}): JSX.Element {
  const [varName, setVarName] = useState("a");

  const varEntries = Object.entries(binding.variables);

  function updateTargetNodeId(val: string) {
    onChange({ ...binding, targetNodeId: val });
  }

  function updateTargetProp(val: string) {
    onChange({ ...binding, targetProp: val });
  }

  function updateOp(val: string) {
    const op = val as CalcOperation["op"];
    // Build minimal valid operation for the selected op
    if (op === "round" || op === "floor" || op === "ceil") {
      onChange({ ...binding, operation: { op, a: varEntries[0]?.[0] ?? "a" } });
    } else if (op === "multiply" || op === "add" || op === "subtract" || op === "divide") {
      const names = varEntries.map(([n]) => n);
      onChange({ ...binding, operation: { op, a: names[0] ?? "a", b: names[1] ?? "b" } });
    } else if (op === "percent" || op === "taxIncluded" || op === "taxAmount") {
      const names = varEntries.map(([n]) => n);
      onChange({ ...binding, operation: { op, base: names[0] ?? "base", rate: names[1] ?? "rate" } });
    }
  }

  function addVar(name: string) {
    if (!name.trim() || name in binding.variables) return;
    const newSource: CalcVariableSource = { kind: "literal", value: 0, note: "テスト用。業務基準値には literal を使わないでください" };
    onChange({ ...binding, variables: { ...binding.variables, [name.trim()]: newSource } });
  }

  function removeVar(name: string) {
    const vars = { ...binding.variables };
    delete vars[name];
    onChange({ ...binding, variables: vars });
  }

  function updateVarSource(name: string, source: CalcVariableSource) {
    onChange({ ...binding, variables: { ...binding.variables, [name]: source } });
  }

  function updateRounding(policy: RoundingPolicy) {
    onChange({ ...binding, roundingPolicy: policy });
  }

  return (
    <div class="mt-2 flex flex-col gap-2 border-t border-slate-200 pt-2">
      {/* Target */}
      <div class="flex flex-col gap-1">
        <span class="text-[0.65rem] font-semibold text-slate-600">反映先 (target)</span>
        <div class="flex gap-1">
          <select
            class="flex-1 rounded border border-slate-300 bg-white px-1 py-0.5 text-[0.65rem]"
            value={binding.targetNodeId}
            onChange={(e) => updateTargetNodeId((e.currentTarget as HTMLSelectElement).value)}
          >
            <option value="">ノードを選択...</option>
            {draftNodeIds.map((id) => (
              <option key={id} value={id}>{id}</option>
            ))}
          </select>
          <select
            class="w-24 rounded border border-slate-300 bg-white px-1 py-0.5 text-[0.65rem]"
            value={binding.targetProp}
            onChange={(e) => updateTargetProp((e.currentTarget as HTMLSelectElement).value)}
          >
            {TARGET_PROPS.map((p) => <option key={p} value={p}>{p}</option>)}
          </select>
        </div>
      </div>

      {/* Variables */}
      <div class="flex flex-col gap-1">
        <span class="text-[0.65rem] font-semibold text-slate-600">変数</span>
        {varEntries.map(([name, source]) => (
          <CalcVarEditor
            key={name}
            name={name}
            source={source}
            draftNodeIds={draftNodeIds}
            onChange={(s) => updateVarSource(name, s)}
            onRemove={() => removeVar(name)}
          />
        ))}
        <div class="flex gap-1">
          <input
            type="text"
            class="w-16 rounded border border-slate-200 px-1 py-0.5 text-[0.65rem]"
            placeholder="変数名"
            value={varName}
            onInput={(e) => setVarName((e.currentTarget as HTMLInputElement).value)}
          />
          <button
            type="button"
            class="rounded border border-dashed border-slate-300 px-1 py-0.5 text-[0.65rem] text-slate-500 hover:border-blue-300 hover:text-blue-600"
            onClick={() => addVar(varName)}
          >
            + 変数追加
          </button>
        </div>
      </div>

      {/* Operation */}
      <div class="flex flex-col gap-1">
        <span class="text-[0.65rem] font-semibold text-slate-600">演算</span>
        <select
          class="rounded border border-slate-300 bg-white px-1 py-0.5 text-[0.65rem]"
          value={binding.operation.op}
          onChange={(e) => updateOp((e.currentTarget as HTMLSelectElement).value)}
        >
          {ALLOWED_OPERATIONS.map((op) => <option key={op} value={op}>{op}</option>)}
        </select>
        <pre class="rounded bg-slate-100 px-1 py-0.5 text-[0.6rem] text-slate-600">
          {JSON.stringify(binding.operation, null, 0)}
        </pre>
      </div>

      {/* Rounding */}
      <div class="flex items-center gap-2">
        <span class="text-[0.65rem] font-semibold text-slate-600">丸め</span>
        <select
          class="rounded border border-slate-300 bg-white px-1 py-0.5 text-[0.65rem]"
          value={binding.roundingPolicy ?? "none"}
          onChange={(e) => updateRounding((e.currentTarget as HTMLSelectElement).value as RoundingPolicy)}
        >
          {ROUNDING_POLICIES.map((p) => <option key={p} value={p}>{p}</option>)}
        </select>
      </div>
    </div>
  );
}

function CalcVarEditor({
  name,
  source,
  draftNodeIds,
  onChange,
  onRemove,
}: {
  name: string;
  source: CalcVariableSource;
  draftNodeIds: string[];
  onChange: (s: CalcVariableSource) => void;
  onRemove: () => void;
}): JSX.Element {
  return (
    <div class="flex flex-col gap-0.5 rounded border border-slate-100 bg-white p-1">
      <div class="flex items-center gap-1">
        <span class="font-mono text-[0.65rem] font-semibold text-slate-700">{name}</span>
        <select
          class="ml-auto rounded border border-slate-200 bg-white px-1 py-0.5 text-[0.6rem]"
          value={source.kind}
          onChange={(e) => {
            const kind = (e.currentTarget as HTMLSelectElement).value;
            if (kind === "literal") onChange({ kind: "literal", value: 0, note: "テスト・固定係数のみ" });
            else if (kind === "node") onChange({ kind: "node", nodeId: draftNodeIds[0] ?? "", propKey: "value" });
            else if (kind === "emission") onChange({ kind: "emission", path: "emission.data." });
            else if (kind === "ruleTable") onChange({ kind: "ruleTable", tablePath: "emission.data.", matchConditions: [], priorityOrder: "desc", effectiveDateHandling: "latest_effective", selectedField: "" });
          }}
        >
          <option value="node">node</option>
          <option value="emission">emission</option>
          <option value="ruleTable">ruleTable</option>
          <option value="literal">literal (テスト用)</option>
        </select>
        <button type="button" class="text-[0.6rem] text-red-400 hover:text-red-600" onClick={onRemove}>✕</button>
      </div>
      {source.kind === "literal" && (
        <div>
          <span class="text-[0.55rem] text-amber-600">⚠ literal は業務基準値に使わないでください。税率・レートは ruleTable/emission を使ってください。</span>
          <input
            type="number"
            class="mt-0.5 w-full rounded border border-slate-200 px-1 py-0.5 text-[0.65rem]"
            value={source.value}
            onInput={(e) => onChange({ ...source, value: parseFloat((e.currentTarget as HTMLInputElement).value) || 0 })}
          />
        </div>
      )}
      {source.kind === "node" && (
        <div class="flex gap-1">
          <select
            class="flex-1 rounded border border-slate-200 bg-white px-1 py-0.5 text-[0.6rem]"
            value={source.nodeId}
            onChange={(e) => onChange({ ...source, nodeId: (e.currentTarget as HTMLSelectElement).value })}
          >
            {draftNodeIds.map((id) => <option key={id} value={id}>{id}</option>)}
          </select>
          <input
            type="text"
            class="w-16 rounded border border-slate-200 px-1 py-0.5 text-[0.6rem]"
            placeholder="propKey"
            value={source.propKey}
            onInput={(e) => onChange({ ...source, propKey: (e.currentTarget as HTMLInputElement).value })}
          />
        </div>
      )}
      {source.kind === "emission" && (
        <input
          type="text"
          class="w-full rounded border border-slate-200 px-1 py-0.5 text-[0.6rem] font-mono"
          placeholder="emission.data.path"
          value={source.path}
          onInput={(e) => onChange({ ...source, path: (e.currentTarget as HTMLInputElement).value })}
        />
      )}
      {source.kind === "ruleTable" && (
        <div class="flex flex-col gap-0.5">
          <input
            type="text"
            class="w-full rounded border border-slate-200 px-1 py-0.5 text-[0.6rem] font-mono"
            placeholder="emission.data.tablePath"
            value={source.tablePath}
            onInput={(e) => onChange({ ...source, tablePath: (e.currentTarget as HTMLInputElement).value })}
          />
          <input
            type="text"
            class="w-full rounded border border-slate-200 px-1 py-0.5 text-[0.6rem]"
            placeholder="selectedField (例: hourlyRate, taxRate)"
            value={source.selectedField}
            onInput={(e) => onChange({ ...source, selectedField: (e.currentTarget as HTMLInputElement).value })}
          />
          <span class="text-[0.55rem] text-slate-400">matchConditions: 現在 {source.matchConditions.length} 件（JSON編集で設定）</span>
        </div>
      )}
    </div>
  );
}

function PackageDesignPanel({
  selectedPackageId,
  selectedCanvasNode,
  routeCandidates,
  canvasDesignDraft,
  onDesignPreviewChange,
  onCommitNode,
}: {
  selectedPackageId: string;
  selectedCanvasNode: DraftNode | null;
  routeCandidates?: string[];
  /** Canvas preview state for the selected node — survives deselect/reselect. */
  canvasDesignDraft?: DesignDraft;
  onDesignPreviewChange?: (nodeId: string, partial: DesignDraft) => void;
  onCommitNode?: (updates: Partial<DraftNode>, label: string) => void;
}): JSX.Element {
  const { confirm, ConfirmDialogHost } = useConfirm();
  const [designName, setDesignName] = useState("");
  const [classname, setClassname] = useState("");
  const [tailwind, setTailwind] = useState("");
  const [reactionIntent, setReactionIntent] = useState("");
  const [cssTokenRefs, setCssTokenRefs] = useState<string[]>([]);
  const [responsiveTokenRefs, setResponsiveTokenRefs] = useState<
    ResponsiveTokenRules
  >({});
  const [inlineText, setInlineText] = useState("");
  const [linkHref, setLinkHref] = useState("");
  const [linkTarget, setLinkTarget] = useState("");
  const [status, setStatus] = useState<string | null>(null);
  const [saveOk, setSaveOk] = useState<boolean | null>(null);
  const [saving, setSaving] = useState(false);
  const [designTmpStatus, setDesignTmpStatus] = useState<
    "idle" | "saving" | "saved" | "error"
  >("idle");
  const [packageComponents, setPackageComponents] = useState<
    AdminPackageComponentRow[]
  >([]);
  const [savedDesigns, setSavedDesigns] = useState<SavedComponentDesignRow[]>(
    [],
  );
  const [componentsLoadStatus, setComponentsLoadStatus] = useState<
    string | null
  >(null);
  const [designsLoadStatus, setDesignsLoadStatus] = useState<string | null>(
    null,
  );
  const designTmpTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastDesignInitKeyRef = useRef<string | null>(null);
  const [propsDraft, setPropsDraft] = useState(selectedCanvasNode?.propsJson ?? "");
  const [propsError, setPropsError] = useState<string | null>(null);
  const [stateDraft, setStateDraft] = useState(selectedCanvasNode?.stateJson ?? "");
  const [stateError, setStateError] = useState<string | null>(null);
  const [propBindingsDraft, setPropBindingsDraft] = useState<Record<string, PropBindingDraft>>(
    selectedCanvasNode?.propBindings ?? {},
  );
  const [propBindingsError, setPropBindingsError] = useState<string | null>(null);

  const componentKind = selectedCanvasNode?.componentKind ?? "";
  const acceptsArrayProps = COMPONENT_ARRAY_PROP_CAPABILITIES[componentKind] ?? [];
  const isDisclosure = isDisclosureKind(selectedCanvasNode?.componentKind);

  const layoutNodeId = selectedCanvasNode?.nodeId ?? "";
  const selectedNodeLabel = selectedCanvasNode
    ? selectedCanvasNode.nodeKind === "structural_html" &&
        selectedCanvasNode.htmlTag
      ? `<${selectedCanvasNode.htmlTag}>`
      : friendlyComponentLabel(selectedCanvasNode.componentKey)
    : "";
  const savedForSelectedNode = savedDesigns.filter((d) =>
    d.layoutNodeId === layoutNodeId
  );
  const canSave = Boolean(
    selectedPackageId && layoutNodeId && designName.trim(),
  );

  const toggleCssToken = (tokenKey: string) => {
    setCssTokenRefs((prev) =>
      prev.includes(tokenKey)
        ? prev.filter((k) => k !== tokenKey)
        : [...prev, tokenKey]
    );
  };

  const pushCanvasPreview = (nodeId: string, text: string, href: string, target: string) => {
    onDesignPreviewChange?.(nodeId, designPreviewDraft(text, href, target));
  };

  // Push only cssTokenRefs change: key present with actual value ([] included) so the
  // simple spread in handleDesignDraftChange correctly overwrites or clears tokens.
  useEffect(() => {
    if (layoutNodeId) {
      onDesignPreviewChange?.(layoutNodeId, { cssTokenRefs });
    }
  }, [cssTokenRefs]);

  const applySavedDesign = (design: SavedComponentDesignRow) => {
    setDesignName(design.name);
    setCssTokenRefs(design.cssTokenRefs);
    setResponsiveTokenRefs(design.responsiveTokenRefs);
    setInlineText(design.inlineText);
    setLinkHref(design.linkHref);
    setLinkTarget(design.linkTarget);
    setReactionIntent(design.reactionIntent);
    setClassname(design.classname);
    setTailwind(design.tailwind);
    setDesignTmpStatus(design.hasDesignTmpDraft ? "saved" : "idle");
    if (layoutNodeId) {
      // Push complete design state in one shot so canvas reflects the loaded design
      // without an intermediate render showing stale tokens.
      onDesignPreviewChange?.(layoutNodeId, {
        inlineText: design.inlineText.trim() || undefined,
        linkHref: design.linkHref.trim() || undefined,
        linkTarget: design.linkTarget.trim() || undefined,
        cssTokenRefs: design.cssTokenRefs,
      });
    }
  };

  const buildDesignPayload = () => ({
    packageId: selectedPackageId,
    layoutNodeId,
    name: designName.trim(),
    classname: classname.trim(),
    tailwind: tailwind.trim(),
    cssTokenRefs,
    responsiveTokenRefs: filterEmptyResponsiveRules(responsiveTokenRefs),
    inlineText: inlineText.trim(),
    linkHref: linkHref.trim(),
    linkTarget: linkTarget.trim(),
    reactionIntent: reactionIntent.trim(),
  });

  const reloadSavedDesigns = async () => {
    const designBody = await dispatchAdminOp("component_style_design", "list", {
      packageId: selectedPackageId,
    });
    const designData = designBody?.emission?.data;
    if (Array.isArray(designData)) {
      const rows = designData.map((raw) =>
        mapSavedDesignRow(raw as Record<string, unknown>)
      );
      setSavedDesigns(rows);
      setDesignsLoadStatus(
        rows.length === 0 ? "保存済みデザインはまだありません。" : null,
      );
      return rows;
    }
    setSavedDesigns([]);
    setDesignsLoadStatus("保存済みデザインの取得に失敗しました。");
    return [];
  };

  useEffect(() => {
    if (!selectedPackageId) {
      setPackageComponents([]);
      setSavedDesigns([]);
      setComponentsLoadStatus(null);
      setDesignsLoadStatus(null);
      setStatus(null);
      setSaveOk(null);
      setDesignTmpStatus("idle");
      return;
    }
    (async () => {
      setComponentsLoadStatus("部品一覧を読み込み中...");
      setDesignsLoadStatus("保存済みデザインを読み込み中...");
      const [compBody] = await Promise.all([
        dispatchAdminOp("ui_topology", "list_package_components", {
          packageId: selectedPackageId,
        }),
        reloadSavedDesigns(),
      ]);
      const compData = compBody?.emission?.data;
      if (Array.isArray(compData)) {
        const rows = compData as AdminPackageComponentRow[];
        setPackageComponents(rows);
        setComponentsLoadStatus(
          rows.length === 0 ? "このパッケージに部品がありません。" : null,
        );
      } else {
        setPackageComponents([]);
        setComponentsLoadStatus("部品一覧の取得に失敗しました。");
      }
    })();
  }, [selectedPackageId]);

  useEffect(() => {
    if (!selectedCanvasNode) {
      lastDesignInitKeyRef.current = null;
      setDesignName("");
      setCssTokenRefs([]);
      setResponsiveTokenRefs({});
      setInlineText("");
      setLinkHref("");
      setLinkTarget("");
      setReactionIntent("");
      setClassname("");
      setTailwind("");
      setDesignTmpStatus("idle");
      setPropsDraft("");
      setPropsError(null);
      setStateDraft("");
      setStateError(null);
      return;
    }
    const nodeId = selectedCanvasNode.nodeId;
    const saved = savedDesigns.find((d) => d.layoutNodeId === nodeId);
    const initKey = `${nodeId}::${saved?.designId ?? "none"}`;
    if (lastDesignInitKeyRef.current === initKey) {
      return;
    }
    lastDesignInitKeyRef.current = initKey;

    if (hasCanvasDesignDraft(canvasDesignDraft)) {
      setDesignName(saved?.name ?? `${nodeId}_design`);
      setResponsiveTokenRefs(saved?.responsiveTokenRefs ?? {});
      setReactionIntent(saved?.reactionIntent ?? "");
      setClassname(saved?.classname ?? "");
      setTailwind(saved?.tailwind ?? "");
      setDesignTmpStatus(saved?.hasDesignTmpDraft ? "saved" : "idle");
      hydrateDesignFormFromDraft(canvasDesignDraft!, {
        setInlineText,
        setLinkHref,
        setLinkTarget,
        setCssTokenRefs,
      });
    } else if (saved) {
      applySavedDesign(saved);
    } else {
      const defaultText = selectedCanvasNode.htmlTag === "a" ? "リンクテキスト" : "";
      setDesignName(`${nodeId}_design`);
      setCssTokenRefs([]);
      setResponsiveTokenRefs({});
      setInlineText(defaultText);
      setLinkHref("");
      setLinkTarget("");
      setReactionIntent("");
      setClassname("");
      setTailwind("");
      setDesignTmpStatus("idle");
      onDesignPreviewChange?.(nodeId, {
        inlineText: defaultText.trim() || undefined,
        linkHref: undefined,
        linkTarget: undefined,
        cssTokenRefs: [],
      });
    }
    setPropsDraft(selectedCanvasNode.propsJson ?? "");
    setPropsError(null);
    setStateDraft(selectedCanvasNode.stateJson ?? "");
    setStateError(null);
    setPropBindingsDraft(selectedCanvasNode.propBindings ?? {});
    setPropBindingsError(null);
  }, [selectedCanvasNode?.nodeId, savedDesigns]);

  useEffect(() => {
    if (!canSave) return;
    if (designTmpTimerRef.current) clearTimeout(designTmpTimerRef.current);
    designTmpTimerRef.current = setTimeout(async () => {
      setDesignTmpStatus("saving");
      try {
        const body = await dispatchAdminOp(
          "component_style_design",
          "save_tmp",
          buildDesignPayload(),
        );
        if (body?.errors?.length) {
          setStatus(
            body.errors[0]?.message ?? "デザイン自動保存に失敗しました。",
          );
          setSaveOk(false);
          setDesignTmpStatus("error");
          return;
        }
        setDesignTmpStatus("saved");
      } catch (e) {
        setStatus(`デザイン自動保存エラー: ${e}`);
        setSaveOk(false);
        setDesignTmpStatus("error");
      }
    }, 1500);
    return () => {
      if (designTmpTimerRef.current) clearTimeout(designTmpTimerRef.current);
    };
  }, [
    selectedPackageId,
    layoutNodeId,
    designName,
    classname,
    tailwind,
    reactionIntent,
    cssTokenRefs,
    responsiveTokenRefs,
    inlineText,
    linkHref,
    linkTarget,
  ]);

  const handleUpsertDesign = async () => {
    if (!canSave) {
      setStatus(
        selectedPackageId
          ? "ノードを選択してください。"
          : "パッケージを選択してください。",
      );
      setSaveOk(false);
      return;
    }
    if (
      !(await confirm("選択中ノードのデザインを保存します。よろしいですか？"))
    ) {
      return;
    }
    setSaving(true);
    setStatus(null);
    setSaveOk(null);
    try {
      const body = await dispatchAdminOp(
        "component_style_design",
        "upsert",
        buildDesignPayload(),
      );
      const ok = Boolean(body?.success) && !body?.errors?.length;
      setSaveOk(ok);
      setStatus(
        ok
          ? "選択中ノードのデザイン設定を保存しました。"
          : (body?.errors?.[0]?.message ?? "保存に失敗しました。"),
      );
      if (ok) {
        setDesignTmpStatus("idle");
        await reloadSavedDesigns();
      }
    } catch (e) {
      setSaveOk(false);
      setStatus(`保存エラー: ${e}`);
    } finally {
      setSaving(false);
    }
  };

  const commitOpenState = (checked: boolean) => {
    try {
      const existing = stateDraft.trim() ? JSON.parse(stateDraft) : {};
      const next = JSON.stringify({ ...existing, open: checked });
      setStateDraft(next);
      setStateError(null);
      onCommitNode?.({ stateJson: next }, "open状態を変更");
    } catch {
      setStateError("stateJson のパースに失敗しました");
    }
  };


  if (!selectedPackageId) {
    return (
      <section class="mb-4 rounded border border-slate-200 p-3 text-sm">
        <div
          class="rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900"
          role="alert"
        >
          <strong>パッケージ未選択</strong> — パッケージを選択してください。
        </div>
        <ConfirmDialogHost />
      </section>
    );
  }

  if (!selectedCanvasNode) {
    return (
      <section class="mb-4 rounded border border-slate-200 p-3 text-sm">
        <div
          class="rounded border border-slate-200 bg-slate-50 px-3 py-6 text-center text-sm text-slate-600"
          role="status"
        >
          ノードを選択してください
        </div>
        <ConfirmDialogHost />
      </section>
    );
  }

  return (
    <section class="mb-4 rounded border border-slate-200 p-3 text-sm">
      <div class="mb-3 rounded border border-slate-200 bg-white px-3 py-2 text-xs">
        <div class="mb-0.5 text-[0.65rem] font-semibold uppercase tracking-wide text-slate-400">
          選択中 canvas node
        </div>
        <div class="flex flex-wrap gap-x-4 gap-y-1">
          <span>
            <span class="text-slate-500">layoutNodeId:</span>
            <code class="font-mono text-[0.65rem]">
              {layoutNodeId.slice(0, 8)}…
            </code>
          </span>
          <span>
            <span class="text-slate-500">ノード:</span>
            <strong>{selectedNodeLabel}</strong>
          </span>
          {selectedCanvasNode.htmlTag && (
            <span>
              <span class="text-slate-500">htmlTag:</span>
              <code class="font-mono">
                &lt;{selectedCanvasNode.htmlTag}&gt;
              </code>
            </span>
          )}
          {designTmpStatus === "saving" && (
            <span class="text-amber-700">デザイン _tmp 自動保存中…</span>
          )}
          {designTmpStatus === "saved" && (
            <span class="text-green-700">デザイン _tmp 自動保存済</span>
          )}
          {designTmpStatus === "error" && (
            <span class="text-red-700">デザイン _tmp 自動保存エラー</span>
          )}
        </div>
      </div>

      {/* design_inspector 上部固定アクション — tab 外 (SSOT: design_inspector.responsibilities) */}
      <div class="mb-3 flex items-center gap-2 border-b border-slate-100 pb-2">
        <button
          type="button"
          class={`btn-primary text-xs ${
            (!canSave || saving) ? "opacity-50 cursor-not-allowed" : ""
          }`}
          onClick={handleUpsertDesign}
          disabled={!canSave || saving}
          aria-disabled={!canSave || saving}
          data-design-save-action="true"
        >
          {saving ? "保存中…" : UX_DESIGN_NODE_SAVE_LABEL}
        </button>
        {status && (
          <span
            class={`text-xs font-semibold ${
              saveOk === true
                ? "text-green-700"
                : saveOk === false
                ? "text-red-700"
                : "text-slate-700"
            }`}
            role={saveOk === false ? "alert" : "status"}
          >
            {status}
          </span>
        )}
      </div>

      <p class="text-muted-xs mb-2">
        タブごとに編集項目を分けています。変更は _tmp に自動保存され、明示保存で正本に反映されます。
      </p>

      {componentsLoadStatus && (
        <p class="mb-1 text-xs text-slate-500">{componentsLoadStatus}</p>
      )}
      {designsLoadStatus && (
        <p class="mb-2 text-xs text-slate-500">{designsLoadStatus}</p>
      )}

      <InspectorTabPanel
        key={layoutNodeId}
        ariaLabel={UX_DESIGN_INSPECTOR_SECTION}
        panelMaxHeight="min(360px, 48vh)"
        tabs={[
          {
            id: "content",
            label: "表示",
            content: (
              <div class="space-y-3">
                {isDisclosure && (
                  <fieldset class="flex flex-col gap-1">
                    <legend class="text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
                      表示状態（open state）
                    </legend>
                    <label class="flex items-center gap-2 text-xs">
                      <input
                        type="checkbox"
                        checked={(() => {
                          try { return Boolean(JSON.parse(stateDraft || "{}").open); } catch { return false; }
                        })()}
                        onChange={(e) => commitOpenState((e.target as HTMLInputElement).checked)}
                        aria-label="open state"
                      />
                      open（previewで開いた状態で表示）
                    </label>
                  </fieldset>
                )}
                <div class="grid gap-2 sm:grid-cols-2">
                  <label class="text-xs">
                    デザイン名（保存キー）
                    <input
                      class="mt-1 w-full rounded border px-2 py-1 text-xs"
                      value={designName}
                      onInput={(e) =>
                        setDesignName((e.target as HTMLInputElement).value)}
                      placeholder="例: selected_node_design"
                    />
                  </label>
                  {savedForSelectedNode.length > 0 && (
                    <label class="text-xs">
                      保存済み / _tmp を読み込む
                      <select
                        class="mt-1 w-full rounded border px-2 py-1 text-xs"
                        value={designName}
                        onChange={(e) => {
                          const name = (e.target as HTMLSelectElement).value;
                          const design = savedForSelectedNode.find((d) =>
                            d.name === name
                          );
                          if (design) applySavedDesign(design);
                        }}
                      >
                        {savedForSelectedNode.map((d) => (
                          <option key={d.designId} value={d.name}>
                            {d.name}
                            {d.hasDesignTmpDraft ? "（_tmp）" : ""}（トークン{" "}
                            {d.cssTokenRefs.length} 件）
                          </option>
                        ))}
                      </select>
                    </label>
                  )}
                </div>
                <label class="block text-xs">
                  インラインテキスト
                  <input
                    class="mt-1 w-full rounded border px-2 py-1 text-xs"
                    value={inlineText}
                    onInput={(e) => {
                      const v = (e.target as HTMLInputElement).value;
                      setInlineText(v);
                      pushCanvasPreview(layoutNodeId, v, linkHref, linkTarget);
                    }}
                    placeholder="表示テキスト / 子テキストノード"
                  />
                </label>
                <div class="grid gap-2 sm:grid-cols-2">
                  <label class="text-xs">
                    リンク URL（href）
                    <input
                      class="mt-1 w-full rounded border px-2 py-1 text-xs"
                      value={linkHref}
                      onInput={(e) => {
                        const v = (e.target as HTMLInputElement).value;
                        setLinkHref(v);
                        pushCanvasPreview(layoutNodeId, inlineText, v, linkTarget);
                      }}
                      placeholder="https://..."
                    />
                  </label>
                  <label class="text-xs">
                    リンク target
                    <input
                      class="mt-1 w-full rounded border px-2 py-1 text-xs"
                      value={linkTarget}
                      onInput={(e) => {
                        const v = (e.target as HTMLInputElement).value;
                        setLinkTarget(v);
                        pushCanvasPreview(layoutNodeId, inlineText, linkHref, v);
                      }}
                      placeholder="_blank 等"
                    />
                  </label>
                </div>
                <label class="block text-xs">
                  リアクション意図（hover / focus 等）
                  <input
                    class="mt-1 w-full rounded border px-2 py-1 text-xs"
                    value={reactionIntent}
                    onInput={(e) =>
                      setReactionIntent((e.target as HTMLInputElement).value)}
                    placeholder="例: ホバーで背景を primary に変化"
                  />
                </label>
              </div>
            ),
          },
          {
            id: "tokens",
            label: "トークン",
            content: (
              <div>
                <p class="mb-2 text-xs font-semibold text-slate-700">
                  CSS 辞書トークン
                </p>
                <p class="text-muted-xs mb-2">
                  チェックしたトークンはデザイン draft として自動保存されます。
                </p>
                <CssTokenPicker
                  selectedTokenRefs={cssTokenRefs}
                  onToggle={toggleCssToken}
                />
              </div>
            ),
          },
          {
            id: "responsive",
            label: "レスポンシブ",
            content: (
              <ResponsiveTokenRuleEditor
                rules={responsiveTokenRefs}
                onChange={setResponsiveTokenRefs}
              />
            ),
          },
          {
            id: "data_binding",
            label: "データ接続",
            content: (
              <div class="space-y-3">
                <p class="text-[0.65rem] text-slate-600">
                  API受信データ（emission.data.*）をこのコンポーネントの配列 props に接続します。
                  DB rows を複数表示したい場合は <strong>CardList / List / Table / Tree</strong> を配置してください。
                </p>
                {acceptsArrayProps.length === 0 && componentKind && componentKind !== "data_display/tree" && (
                  <div class="rounded border border-amber-200 bg-amber-50 p-2">
                    <p class="mb-1 text-[0.65rem] font-semibold text-amber-800">
                      {componentKind === "display/card"
                        ? "display/card は単体表示専用です"
                        : `${componentKind} は配列バインド非対応です`}
                    </p>
                    <p class="text-[0.6rem] text-amber-700">
                      {componentKind === "display/card"
                        ? "複数 Card を DB rows から表示したい場合は display/card_list（CardList）、data_display/list（List）、data_display/table（Table）を選んでください。display/card は items/rows を受け取りません。"
                        : "配列データ（DB rows）を表示するには CardList（display/card_list）、List（data_display/list）、Table（data_display/table）、Tree（data_display/tree）を配置してください。"}
                    </p>
                  </div>
                )}
                {componentKind === "data_display/tree" && (
                  <div class="rounded border border-slate-200 bg-slate-50 p-2">
                    <p class="mb-1 text-[0.65rem] font-semibold text-slate-700">
                      data_display/tree — propBindings 対応・ランタイム factory 未接続
                    </p>
                    <p class="text-[0.6rem] text-slate-600">
                      nodes / items の propBindings 設定は保存されますが、canvas preview はランタイム factory 未接続のため表示されません。
                      propBindings を設定する場合は childrenPath（再帰構造フィールド名）も指定してください。
                    </p>
                  </div>
                )}
                {acceptsArrayProps.length > 0 && (
                  <fieldset class="flex flex-col gap-2">
                    <legend class="text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
                      配列 Prop バインド（emission.data.*）
                    </legend>
                    <p class="text-[0.6rem] text-slate-500">
                      バインド対象 props: {acceptsArrayProps.join(", ")}
                    </p>
                    {acceptsArrayProps.map((propName) => {
                      const binding = propBindingsDraft[propName];
                      return (
                        <div key={propName} class="rounded border border-slate-200 p-2 text-[0.65rem]">
                          <div class="mb-1 flex items-center justify-between">
                            <span class="font-semibold text-slate-700">{propName}</span>
                            {binding ? (
                              <button
                                type="button"
                                class="text-[0.6rem] text-red-500 hover:underline"
                                onClick={() => {
                                  const next = { ...propBindingsDraft };
                                  delete next[propName];
                                  setPropBindingsDraft(next);
                                  setPropBindingsError(null);
                                  onCommitNode?.({ propBindings: Object.keys(next).length > 0 ? next : undefined }, `propBindings.${propName}を削除`);
                                }}
                              >
                                削除
                              </button>
                            ) : (
                              <button
                                type="button"
                                class="text-[0.6rem] text-blue-500 hover:underline"
                                onClick={() => {
                                  const next = { ...propBindingsDraft, [propName]: { source: "emission.data.rows" } };
                                  setPropBindingsDraft(next);
                                }}
                              >
                                + バインドを追加
                              </button>
                            )}
                          </div>
                          {binding && (
                            <div class="flex flex-col gap-1">
                              <label class="block">
                                source（必須 — emission.data.* パス）
                                <input
                                  class="mt-0.5 w-full rounded border px-1 py-0.5 font-mono text-[0.6rem]"
                                  value={binding.source}
                                  placeholder="emission.data.rows"
                                  onInput={(e) => {
                                    const v = (e.target as HTMLInputElement).value;
                                    const next = { ...propBindingsDraft, [propName]: { ...binding, source: v } };
                                    setPropBindingsDraft(next);
                                  }}
                                  onBlur={() => {
                                    const errs = validatePropBindingsStructure(propBindingsDraft, componentKind);
                                    if (errs.length > 0) { setPropBindingsError(errs[0]); return; }
                                    setPropBindingsError(null);
                                    onCommitNode?.({ propBindings: propBindingsDraft }, `propBindings.${propName}.sourceを更新`);
                                  }}
                                />
                              </label>
                              <label class="block">
                                transform（任意）
                                <select
                                  class="mt-0.5 w-full rounded border px-1 py-0.5 text-[0.6rem]"
                                  value={binding.transform ?? ""}
                                  onChange={(e) => {
                                    const v = (e.target as HTMLSelectElement).value;
                                    const next = { ...propBindingsDraft, [propName]: { ...binding, transform: v || undefined } };
                                    setPropBindingsDraft(next);
                                    const errs = validatePropBindingsStructure(next, componentKind);
                                    if (errs.length > 0) { setPropBindingsError(errs[0]); return; }
                                    setPropBindingsError(null);
                                    onCommitNode?.({ propBindings: next }, `propBindings.${propName}.transformを更新`);
                                  }}
                                >
                                  <option value="">（なし）</option>
                                  {[...ALLOWED_PROP_BINDING_TRANSFORMS].map((t) => (
                                    <option key={t} value={t}>{t}</option>
                                  ))}
                                </select>
                              </label>
                              <label class="block">
                                keyPath（任意 — 行キーフィールド）
                                <input
                                  class="mt-0.5 w-full rounded border px-1 py-0.5 font-mono text-[0.6rem]"
                                  value={binding.keyPath ?? ""}
                                  placeholder="id"
                                  onInput={(e) => {
                                    const v = (e.target as HTMLInputElement).value;
                                    const next = { ...propBindingsDraft, [propName]: { ...binding, keyPath: v || undefined } };
                                    setPropBindingsDraft(next);
                                  }}
                                  onBlur={() => {
                                    const errs = validatePropBindingsStructure(propBindingsDraft, componentKind);
                                    if (errs.length > 0) { setPropBindingsError(errs[0]); return; }
                                    setPropBindingsError(null);
                                    onCommitNode?.({ propBindings: propBindingsDraft }, `propBindings.${propName}.keyPathを更新`);
                                  }}
                                />
                              </label>
                              <label class="block">
                                labelPath（任意 — 表示ラベルフィールド）
                                <input
                                  class="mt-0.5 w-full rounded border px-1 py-0.5 font-mono text-[0.6rem]"
                                  value={binding.labelPath ?? ""}
                                  placeholder="name"
                                  onInput={(e) => {
                                    const v = (e.target as HTMLInputElement).value;
                                    const next = { ...propBindingsDraft, [propName]: { ...binding, labelPath: v || undefined } };
                                    setPropBindingsDraft(next);
                                  }}
                                  onBlur={() => {
                                    const errs = validatePropBindingsStructure(propBindingsDraft, componentKind);
                                    if (errs.length > 0) { setPropBindingsError(errs[0]); return; }
                                    setPropBindingsError(null);
                                    onCommitNode?.({ propBindings: propBindingsDraft }, `propBindings.${propName}.labelPathを更新`);
                                  }}
                                />
                              </label>
                              <label class="block">
                                valuePath（任意 — 値フィールド）
                                <input
                                  class="mt-0.5 w-full rounded border px-1 py-0.5 font-mono text-[0.6rem]"
                                  value={binding.valuePath ?? ""}
                                  placeholder="id"
                                  onInput={(e) => {
                                    const v = (e.target as HTMLInputElement).value;
                                    const next = { ...propBindingsDraft, [propName]: { ...binding, valuePath: v || undefined } };
                                    setPropBindingsDraft(next);
                                  }}
                                  onBlur={() => {
                                    const errs = validatePropBindingsStructure(propBindingsDraft, componentKind);
                                    if (errs.length > 0) { setPropBindingsError(errs[0]); return; }
                                    setPropBindingsError(null);
                                    onCommitNode?.({ propBindings: propBindingsDraft }, `propBindings.${propName}.valuePathを更新`);
                                  }}
                                />
                              </label>
                              <label class="block">
                                childrenPath（任意 — 子ノードフィールド / tree 用）
                                <input
                                  class="mt-0.5 w-full rounded border px-1 py-0.5 font-mono text-[0.6rem]"
                                  value={binding.childrenPath ?? ""}
                                  placeholder="children"
                                  onInput={(e) => {
                                    const v = (e.target as HTMLInputElement).value;
                                    const next = { ...propBindingsDraft, [propName]: { ...binding, childrenPath: v || undefined } };
                                    setPropBindingsDraft(next);
                                  }}
                                  onBlur={() => {
                                    const errs = validatePropBindingsStructure(propBindingsDraft, componentKind);
                                    if (errs.length > 0) { setPropBindingsError(errs[0]); return; }
                                    setPropBindingsError(null);
                                    onCommitNode?.({ propBindings: propBindingsDraft }, `propBindings.${propName}.childrenPathを更新`);
                                  }}
                                />
                              </label>
                            </div>
                          )}
                        </div>
                      );
                    })}
                    {propBindingsError && <p class="text-[0.6rem] text-red-600">{propBindingsError}</p>}
                  </fieldset>
                )}
              </div>
            ),
          },
          {
            id: "wiring",
            label: "API送信配線",
            content: (
              <div class="space-y-3">
                {selectedPackageId && (
                  <RouteNavigationWiringPreset
                    selectedPackageId={selectedPackageId}
                    routeCandidates={routeCandidates ?? []}
                  />
                )}
                <details class="mb-4 rounded border border-slate-200 p-3">
                  <summary class="cursor-pointer text-xs font-semibold text-slate-700">
                    パッケージ配線（イベント接続）
                  </summary>
                  <div class="mt-2">
                    <PackageWiringEditor
                      selectedPackageId={selectedPackageId}
                      packageComponents={packageComponents}
                    />
                  </div>
                </details>
              </div>
            ),
          },
          {
            id: "advanced",
            label: "上級",
            content: (
              <div class="space-y-3">
                <AdvancedManualOverride title="classname / tailwind 手入力（補助メモのみ）">
                  <p class="text-muted-xs mb-2">
                    通常は cssTokenRefs を使ってください。補助メモとしてのみ保存されます。
                  </p>
                  <label class="mb-2 block text-xs">
                    classname（補助メモ）
                    <input
                      class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                      value={classname}
                      onInput={(e) =>
                        setClassname((e.target as HTMLInputElement).value)}
                      placeholder="例: btn-primary（cssTokenRefs を優先）"
                    />
                  </label>
                  <label class="block text-xs">
                    tailwind（非正本・補助メモ）
                    <input
                      class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                      value={tailwind}
                      onInput={(e) =>
                        setTailwind((e.target as HTMLInputElement).value)}
                      placeholder="辞書トークンを優先してください"
                    />
                  </label>
                </AdvancedManualOverride>
                <AdvancedManualOverride title="イベント配線（実験的・SSOT未定義）">
                  <p class="text-[0.6rem] text-slate-500 mb-2">
                    SSOT未定義 — 実験的。保存対象はlayout_patch_json経由（propsJson.eventWirings）。previewモードではinert bindingを使用します。
                  </p>
                  <fieldset class="flex flex-col gap-1">
                    <legend class="text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
                      イベント配線（propsJson.eventWirings）
                    </legend>
                    {(() => {
                      let wirings: ComponentEventWiring[] = [];
                      try {
                        const parsed = JSON.parse(propsDraft || "{}");
                        if (Array.isArray(parsed.eventWirings)) wirings = parsed.eventWirings;
                      } catch { /* ignore */ }

                      const removeWiring = (idx: number) => {
                        const next = wirings.filter((_, i) => i !== idx);
                        try {
                          const existing = propsDraft.trim() ? JSON.parse(propsDraft) : {};
                          const nextJson = JSON.stringify({ ...existing, eventWirings: next });
                          setPropsDraft(nextJson);
                          setPropsError(null);
                          onCommitNode?.({ propsJson: nextJson }, "イベント配線を削除");
                        } catch {
                          setPropsError("propsJsonの更新に失敗しました");
                        }
                      };

                      const addWiring = () => {
                        const next: ComponentEventWiring[] = [...wirings, { eventType: "onClick", actionType: "setState", targetStateKey: "" }];
                        try {
                          const existing = propsDraft.trim() ? JSON.parse(propsDraft) : {};
                          const nextJson = JSON.stringify({ ...existing, eventWirings: next });
                          setPropsDraft(nextJson);
                          setPropsError(null);
                          onCommitNode?.({ propsJson: nextJson }, "イベント配線を追加");
                        } catch {
                          setPropsError("propsJsonの更新に失敗しました");
                        }
                      };

                      const updateWiring = (idx: number, updates: Partial<ComponentEventWiring>) => {
                        const next = wirings.map((w, i) => i === idx ? { ...w, ...updates } : w);
                        try {
                          const existing = propsDraft.trim() ? JSON.parse(propsDraft) : {};
                          const nextJson = JSON.stringify({ ...existing, eventWirings: next });
                          setPropsDraft(nextJson);
                          setPropsError(null);
                          onCommitNode?.({ propsJson: nextJson }, "イベント配線を更新");
                        } catch {
                          setPropsError("propsJsonの更新に失敗しました");
                        }
                      };

                      return (
                        <div class="space-y-2 mt-1">
                          {wirings.map((w, i) => (
                            <div key={i} class="flex flex-col gap-1 rounded border border-slate-200 p-2">
                              <div class="flex items-center justify-between">
                                <span class="text-[0.65rem] font-semibold text-slate-600">配線 #{i + 1}</span>
                                <button
                                  type="button"
                                  class="text-[0.6rem] text-red-500 hover:text-red-700"
                                  onClick={() => removeWiring(i)}
                                  aria-label={`配線 ${i + 1} を削除`}
                                >
                                  削除
                                </button>
                              </div>
                              <label class="flex flex-col gap-0.5 text-[0.65rem]">
                                イベント
                                <select
                                  value={w.eventType}
                                  onChange={(e) => updateWiring(i, { eventType: (e.target as HTMLSelectElement).value })}
                                  class="input px-1 py-0.5 text-xs"
                                  aria-label={`配線 ${i + 1} イベントタイプ`}
                                >
                                  {COMPONENT_EVENT_TYPES.map((t) => (
                                    <option key={t} value={t}>{t}</option>
                                  ))}
                                </select>
                              </label>
                              <label class="flex flex-col gap-0.5 text-[0.65rem]">
                                アクション
                                <select
                                  value={w.actionType}
                                  onChange={(e) => updateWiring(i, { actionType: (e.target as HTMLSelectElement).value })}
                                  class="input px-1 py-0.5 text-xs"
                                  aria-label={`配線 ${i + 1} アクションタイプ`}
                                >
                                  {COMPONENT_ACTION_TYPES.map((t) => (
                                    <option key={t} value={t}>{t}</option>
                                  ))}
                                </select>
                              </label>
                              <label class="flex flex-col gap-0.5 text-[0.65rem]">
                                対象ステートキー（任意）
                                <input
                                  value={w.targetStateKey ?? ""}
                                  onInput={(e) => updateWiring(i, { targetStateKey: (e.target as HTMLInputElement).value })}
                                  onBlur={(e) => updateWiring(i, { targetStateKey: (e.target as HTMLInputElement).value })}
                                  class="input-mono px-1 py-0.5 text-xs"
                                  placeholder="例: modalOpen"
                                  aria-label={`配線 ${i + 1} ターゲットステートキー`}
                                />
                              </label>
                            </div>
                          ))}
                          <button
                            type="button"
                            class="btn-secondary text-xs"
                            onClick={addWiring}
                          >
                            + イベント配線を追加
                          </button>
                          {propsError && <p class="text-red-600 text-[0.6rem]">{propsError}</p>}
                        </div>
                      );
                    })()}
                  </fieldset>
                </AdvancedManualOverride>
                <AdvancedManualOverride title="生JSON（上級者向け）">
                  <fieldset class="flex flex-col gap-1 mb-2">
                    <legend class="text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
                      Props JSON（全体）
                    </legend>
                    <textarea
                      value={propsDraft}
                      onInput={(e) => setPropsDraft((e.target as HTMLTextAreaElement).value)}
                      onBlur={() => {
                        const v = propsDraft.trim();
                        if (!v) {
                          setPropsError(null);
                          onCommitNode?.({ propsJson: undefined }, "propsJsonをクリア");
                          return;
                        }
                        try {
                          JSON.parse(v);
                          setPropsError(null);
                          onCommitNode?.({ propsJson: v }, "propsJsonを更新");
                        } catch {
                          setPropsError("JSON形式が不正です");
                        }
                      }}
                      rows={3}
                      placeholder='{"label": "送信", "variant": "primary", "eventWirings": []}'
                      class="input-mono w-full text-[0.6rem]"
                      aria-label="propsJson（生JSON）"
                    />
                    {propsError && <p class="text-red-600 text-[0.6rem]">{propsError}</p>}
                  </fieldset>
                  <fieldset class="flex flex-col gap-1">
                    <legend class="text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
                      State JSON{isDisclosure ? "（open state）" : ""}
                    </legend>
                    <textarea
                      value={stateDraft}
                      onInput={(e) => setStateDraft((e.target as HTMLTextAreaElement).value)}
                      onBlur={() => {
                        const v = stateDraft.trim();
                        if (!v) {
                          setStateError(null);
                          onCommitNode?.({ stateJson: undefined }, "stateJsonをクリア");
                          return;
                        }
                        try {
                          JSON.parse(v);
                          setStateError(null);
                          onCommitNode?.({ stateJson: v }, "stateJsonを更新");
                        } catch {
                          setStateError("JSON形式が不正です");
                        }
                      }}
                      rows={2}
                      placeholder='{"open": false}'
                      class="input-mono w-full text-[0.6rem]"
                      aria-label="stateJson（生JSON）"
                    />
                    {stateError && <p class="text-red-600 text-[0.6rem]">{stateError}</p>}
                  </fieldset>
                </AdvancedManualOverride>
              </div>
            ),
          },
        ]}
      />

      <ConfirmDialogHost />
    </section>
  );
}

/**
 * Canvas-first workspace (SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract).
 * Component registration lives on /admin/contents; linking on /admin/manifests.
 */
export default function UiBuilderAdmin(): JSX.Element {
  const [packages, setPackages] = useState<AdminPackageRow[]>([]);
  const [selectedPackageId, setSelectedPackageId] = useState("");
  const [routeKey, setRouteKey] = useState("");
  const [manualRouteDraft, setManualRouteDraft] = useState("");
  const [committedManualRouteKey, setCommittedManualRouteKey] = useState("");
  const [layoutCandidates, setLayoutCandidates] = useState<
    LayoutRouteCandidate[]
  >([]);
  const [candidateErrors, setCandidateErrors] = useState<ValidationError[]>([]);
  const [autoPackageLoading, setAutoPackageLoading] = useState(false);
  const [autoPackageError, setAutoPackageError] = useState<
    ValidationError | null
  >(null);
  const [paletteReloadToken, setPaletteReloadToken] = useState(0);
  const [flowStep, setFlowStep] = useState<UiBuilderFlowStepId>("route");

  const committedRouteKey = committedManualRouteKey.trim() || routeKey;
  const routeCanvasReady = Boolean(committedRouteKey);
  const selectedPackage = packages.find((p) =>
    p.packageId === selectedPackageId
  ) ?? packages.find((p) => p.routeKey === committedRouteKey);

  const reloadPackages = async (): Promise<AdminPackageRow[]> => {
    const body = await dispatchAdminOp("ui_topology", "list_packages");
    const data = body?.emission?.data;
    const list = Array.isArray(data) ? data as AdminPackageRow[] : [];
    setPackages(list);
    return list;
  };

  useEffect(() => {
    const init = async () => {
      await reloadPackages();
      const { candidates, errors } = await loadLayoutCandidatesFromBackend();
      setLayoutCandidates(candidates);
      setCandidateErrors(errors);
    };
    init();
  }, []);

  useEffect(() => {
    if (!committedRouteKey) {
      setSelectedPackageId("");
      setAutoPackageError(null);
      setFlowStep("route");
      return;
    }
    let cancelled = false;
    const run = async () => {
      setAutoPackageLoading(true);
      setAutoPackageError(null);
      const { handoff, error } = await ensureShellPackageForRoute(
        committedRouteKey,
      );
      if (cancelled) return;
      if (error || !handoff) {
        setAutoPackageError(error ?? {
          code: "SHELL_PACKAGE_FAILED",
          message: "ルート用パッケージの自動生成に失敗しました。",
        });
        setSelectedPackageId("");
        setFlowStep("route");
      } else {
        setSelectedPackageId(handoff.packageId);
        setFlowStep("canvas_edit");
        await reloadPackages();
        const { candidates, errors } = await loadLayoutCandidatesFromBackend();
        if (!cancelled) {
          setLayoutCandidates(candidates);
          setCandidateErrors(errors);
        }
      }
      setAutoPackageLoading(false);
    };
    run();
    return () => {
      cancelled = true;
    };
  }, [committedRouteKey]);

  const handleRegisterComponentBeforePlace = async (
    componentKey: string,
  ): Promise<boolean> => {
    if (!committedRouteKey) {
      setAutoPackageError({
        code: "ROUTE_KEY_REQUIRED",
        message: UX_ROUTE_KEY_REQUIRED_FOR_CANVAS,
      });
      return false;
    }
    const result = await registerCatalogComponentInPackage(
      committedRouteKey,
      componentKey,
    );
    if (!result.ok) {
      setAutoPackageError(result.error ?? {
        code: "REGISTER_FAILED",
        message: "部品の自動追加に失敗しました。",
      });
      return false;
    }
    setPaletteReloadToken((n) => n + 1);
    await reloadPackages();
    return true;
  };

  const handleDetachComponentAfterRemove = async (
    componentKey: string,
  ): Promise<void> => {
    if (!committedRouteKey) return;
    const result = await detachComponentFromPackage(
      committedRouteKey,
      componentKey,
    );
    if (!result.ok) {
      setAutoPackageError(result.error ?? {
        code: "DETACH_FAILED",
        message: "パッケージからの部品削除に失敗しました。",
      });
      return;
    }
    setPaletteReloadToken((n) => n + 1);
    await reloadPackages();
  };

  const routeOptions = uniqueRouteKeys(layoutCandidates);

  return (
    <main class="page-main-wide">
      <h1 class="page-title">
        topolactor — 管理 / 画面づくり
      </h1>
      <p class="mb-1">
        <a href="/admin" class="link">← 管理インデックスへ戻る</a>
      </p>
      <AdminHowTo
        steps={ADMIN_UI_BUILDER_GUIDE.howToSteps}
        prerequisites={ADMIN_UI_BUILDER_GUIDE.prerequisites}
      />
      <AdminHelpPanel {...ADMIN_UI_BUILDER_GUIDE} />

      <UiBuilderFlowStepper activeStep={flowStep} />

      {!committedRouteKey && (
        <div
          class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900"
          role="note"
        >
          ページ内容（/admin/contents）で登録した topology.name から routeKey を自動派生します。
          単一ページまたは handoff 付きで開いた場合は自動で canvas workspace が有効になります。
        </div>
      )}

      <ManifestRouteEntry
        initialManifestId={readUiBuilderHandoffManifestId()}
        committedRouteKey={committedRouteKey}
        onCommit={(key) => {
          setCommittedManualRouteKey(key);
          setRouteKey("");
          setManualRouteDraft("");
        }}
      />

      <details class="mb-3">
        <summary class="cursor-pointer rounded border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-700 hover:bg-slate-100">
          legacy / debug: 既存ルート候補・手動ルートキー（移行用）
        </summary>
      <div class="mt-2 rounded border border-slate-200 bg-white p-3">
        <BucketPackageRouteFields
          routeKey={routeKey}
          committedRouteKey={committedRouteKey}
          routeOptions={routeOptions}
          candidateErrors={candidateErrors}
          onRouteKeyChange={(key) => {
            setRouteKey(key);
            setCommittedManualRouteKey("");
            setManualRouteDraft("");
          }}
        />
        <LegacyManualRouteInput
          manualRouteDraft={manualRouteDraft}
          committedRouteKey={committedRouteKey}
          routeKey={routeKey}
          onManualRouteDraftChange={setManualRouteDraft}
          onManualRouteCommit={() => {
            const next = manualRouteDraft.trim();
            if (!next) return;
            setCommittedManualRouteKey(next);
            setRouteKey("");
          }}
        />
        {autoPackageLoading && (
          <p class="mt-2 text-xs text-blue-800">パッケージを自動生成中…</p>
        )}
        {autoPackageError && (
          <ValidationErrorPanel
            errors={[autoPackageError]}
            title="パッケージ自動生成エラー"
          />
        )}
        {selectedPackage && routeCanvasReady && !autoPackageLoading && (
          <p class="mt-2 text-xs text-slate-600">
            自動生成パッケージ:{" "}
            <code class="font-mono">{selectedPackage.packageKey}</code>
            {" "}
            <span class="text-slate-400">
              ({selectedPackage.packageId.slice(0, 8)}…)
            </span>
          </p>
        )}
      </div>
      </details>

      <div class="mb-4">
        <div class="mb-2 flex flex-wrap items-center gap-2 rounded border border-slate-200 bg-slate-50 px-3 py-2">
          <strong class="text-sm text-slate-800">
            {UX_LAYOUT_EDITOR_SURFACE}
          </strong>
          {committedRouteKey && (
            <span class="font-mono text-xs text-slate-600">
              route: {committedRouteKey}
            </span>
          )}
        </div>

        <LayoutBuilderSection
          scopedPackageId={selectedPackageId}
          scopedRouteKey={selectedPackage?.routeKey ?? committedRouteKey}
          scopedLayoutId={selectedPackage?.layoutId}
          routeCanvasReady={routeCanvasReady && !autoPackageLoading &&
            !autoPackageError}
          onRegisterComponentBeforePlace={handleRegisterComponentBeforePlace}
          onDetachComponentAfterRemove={handleDetachComponentAfterRemove}
          paletteReloadToken={paletteReloadToken}
        />
      </div>
    </main>
  );
}
