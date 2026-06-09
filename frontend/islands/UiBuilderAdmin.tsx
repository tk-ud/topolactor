import { useCallback, useEffect, useRef, useState } from "preact/hooks";
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
  RESPONSIVE_BREAKPOINTS,
  type ResponsiveTokenRules,
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
import { LayoutPatchApplyHandoffModal } from "../components/LayoutPatchApplyHandoffModal.tsx";
import type { LayoutPreviewNodeInput } from "../runtime/layoutComponentPreview.ts";
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
import { useConfirm } from "../hooks/useConfirm.tsx";
import {
  enrichLayoutPreviewNodes,
  getLayoutPreviewDefaultSize,
  resolveComponentKindForLayoutPreview,
} from "../runtime/layoutComponentPreview.ts";

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
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
  /** Serialized component props override (JSON string). Flowed through layout_patch_json → backend → renderEmission. SSOT: layout_patch_json */
  propsJson?: string;
  /** Serialized component state override (JSON string). open:boolean for disclosure/drawer — merged into props.data at render time. SSOT: layout_patch_json */
  stateJson?: string;
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
}: {
  draftNodes: DraftNode[];
  selectedNodeId: string | null;
  selectedNode: DraftNode | null;
  packageId: string;
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
              onDesignPreviewChange={onDesignChange}
            />
          </Accordion>
        </>
      ) : (
        <p class="rounded border border-dashed border-gray-200 px-2 py-3 text-center text-xs text-gray-500">
          ノードを選択してください
        </p>
      )}
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
  action: "preview" | "validate" | "apply";
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
  action: "preview" | "validate" | "apply",
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
  } else if (action === "preview") {
    nextAction = "問題なければ「バリデート」を実行";
  } else if (action === "validate") {
    nextAction = "問題なければ「適用」を実行";
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
      phases: ["idle", "previewing", "previewed"],
    },
    { id: "validated", label: "検証済み", phases: ["validating", "validated"] },
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
          すべてのローカルチェック通過。canvasプレビュー → バリデート →
          適用 の順で実行してください。
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
          {summary.action === "preview"
            ? "プレビュー"
            : summary.action === "validate"
            ? "バリデート"
            : "適用"} 結果
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
    if (
      tokenFilter &&
      !t.tokenKey.toLowerCase().includes(tokenFilter.toLowerCase())
    ) return false;
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
              return (
                <button
                  key={key}
                  type="button"
                  onClick={() => onToggle(key)}
                  class="flex items-center gap-1.5 rounded border border-blue-400 bg-white px-2 py-1 font-mono text-xs hover:border-red-400 hover:bg-red-50"
                  title={`${key} — クリックで選択解除`}
                  aria-label={`${key}を選択解除`}
                >
                  {token && <CssTokenSwatch token={token} />}
                  <span>{key}</span>
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
        <table class="table font-mono text-xs">
          <thead>
            <tr>
              {[
                "",
                "プレビュー",
                "トークンキー",
                "カテゴリ",
                "対象",
                "CSSプロパティ",
              ].map((h) => <th key={h} scope="col">{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {filtered.map((t) => {
              const isSelected = selectedTokenRefs.includes(t.tokenKey);
              return (
                <tr
                  key={t.tokenKey}
                  class={isSelected ? "bg-blue-50" : "hover:bg-gray-50"}
                  onClick={() => onToggle(t.tokenKey)}
                  style={{ cursor: "pointer" }}
                >
                  <td>
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => onToggle(t.tokenKey)}
                      aria-label={`${t.tokenKey}を選択`}
                    />
                  </td>
                  <td>
                    <CssTokenSwatch token={t} />
                  </td>
                  <td>
                    <code>{t.tokenKey}</code>
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

function BucketPackageRouteFields({
  routeKey,
  manualRouteDraft,
  committedRouteKey,
  routeOptions,
  candidateErrors,
  onRouteKeyChange,
  onManualRouteDraftChange,
  onManualRouteCommit,
}: {
  routeKey: string;
  manualRouteDraft: string;
  committedRouteKey: string;
  routeOptions: string[];
  candidateErrors: ValidationError[];
  onRouteKeyChange: (routeKey: string) => void;
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
    <div class="mt-3 rounded border border-slate-200 bg-slate-50 p-3">
      <p class="mb-2 text-xs font-semibold text-slate-700">
        ページルート（canvas workspace の入口）
      </p>
      <label class="mb-2 flex flex-col gap-0.5 text-sm">
        候補から選択
        <select
          value={routeKey}
          onChange={(e) => {
            onRouteKeyChange((e.target as HTMLSelectElement).value);
            onManualRouteDraftChange("");
          }}
          disabled={routeOptions.length === 0}
          class="input font-mono text-xs"
        >
          <option value="">— ルートを選択 —</option>
          {routeOptions.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
      </label>
      {routeOptions.length === 0 && candidateErrors.length === 0 && (
        <p class="mb-2 text-xs text-amber-900">
          初回は下の直接入力にルート名を入れてください（例:{" "}
          <code>admin_demo_screen_list</code>）。
          Enter または「確定」でパッケージを自動生成します（入力中は登録されません）。
        </p>
      )}
      <label class="flex flex-col gap-0.5 text-sm">
        直接入力（初回はこちら）
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
            placeholder="例: employees"
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
      {committedRouteKey && (
        <p class="mt-2 text-xs text-slate-600">
          使用中のルート: <code class="font-mono">{committedRouteKey}</code>
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
  const [propsDraft, setPropsDraft] = useState(node.propsJson ?? "");
  const [propsError, setPropsError] = useState<string | null>(null);
  const [stateDraft, setStateDraft] = useState(node.stateJson ?? "");
  const [stateError, setStateError] = useState<string | null>(null);

  const isDisclosure = isDisclosureKind(node.componentKind);
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
        <div class="grid grid-cols-2 gap-1">
          {(["width", "height"] as const).map((f) => (
            <label key={f} class="flex flex-col gap-0.5">
              <span class="text-[0.65rem] text-gray-600">
                {FIELD_LABELS[f]}
              </span>
              <input
                type="text"
                inputMode="decimal"
                value={layoutDimensionLabel(node[f])}
                placeholder={f === "width" ? "140 / 50% / auto" : "60 / 100% / auto"}
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
                class="input px-1 py-0.5"
                aria-label={FIELD_LABELS[f]}
              />
            </label>
          ))}
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

  const gridTab = (
    <fieldset class="flex flex-col gap-1">
      <legend class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
        グリッド位置（レガシー補助）
      </legend>
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
    </fieldset>
  );

  const commitOpenState = (checked: boolean) => {
    try {
      const existing = stateDraft.trim() ? JSON.parse(stateDraft) : {};
      const next = JSON.stringify({ ...existing, open: checked });
      setStateDraft(next);
      setStateError(null);
      onCommit({ stateJson: next }, "open状態を変更");
    } catch {
      setStateError("stateJson のパースに失敗しました");
    }
  };

  const wiringTab = (
    <div class="space-y-3">
      <p class="text-[0.6rem] text-slate-500">
        保存対象はlayout_patch_json経由。previewモードでは inert binding を使用しランタイムdispatchは行いません。
      </p>

      {/* Props JSON */}
      <fieldset class="flex flex-col gap-1">
        <legend class="text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
          Props JSON
        </legend>
        <textarea
          value={propsDraft}
          onInput={(e) => setPropsDraft((e.target as HTMLTextAreaElement).value)}
          onBlur={() => {
            const v = propsDraft.trim();
            if (!v) {
              setPropsError(null);
              onCommit({ propsJson: undefined }, "propsJsonをクリア");
              return;
            }
            try {
              JSON.parse(v);
              setPropsError(null);
              onCommit({ propsJson: v }, "propsJsonを更新");
            } catch {
              setPropsError("JSON形式が不正です");
            }
          }}
          rows={3}
          placeholder='{"label": "送信", "variant": "primary"}'
          class="input-mono w-full text-[0.6rem]"
          aria-label="propsJson"
        />
        {propsError && <p class="text-red-600 text-[0.6rem]">{propsError}</p>}
      </fieldset>

      {/* State JSON — open toggle for disclosure components */}
      <fieldset class="flex flex-col gap-1">
        <legend class="text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">
          State JSON{isDisclosure ? " (open state)" : ""}
        </legend>
        {isDisclosure && (
          <label class="flex items-center gap-2 text-xs">
            <input
              type="checkbox"
              checked={(() => {
                try { return Boolean(JSON.parse(stateDraft || "{}").open); } catch { return false; }
              })()}
              onChange={(e) => commitOpenState((e.target as HTMLInputElement).checked)}
              aria-label="open state"
            />
            open (preview で開いた状態で表示)
          </label>
        )}
        <textarea
          value={stateDraft}
          onInput={(e) => setStateDraft((e.target as HTMLTextAreaElement).value)}
          onBlur={() => {
            const v = stateDraft.trim();
            if (!v) {
              setStateError(null);
              onCommit({ stateJson: undefined }, "stateJsonをクリア");
              return;
            }
            try {
              JSON.parse(v);
              setStateError(null);
              onCommit({ stateJson: v }, "stateJsonを更新");
            } catch {
              setStateError("JSON形式が不正です");
            }
          }}
          rows={2}
          placeholder='{"open": false}'
          class="input-mono w-full text-[0.6rem]"
          aria-label="stateJson"
        />
        {stateError && <p class="text-red-600 text-[0.6rem]">{stateError}</p>}
      </fieldset>

    </div>
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
          { id: "grid", label: "グリッド", content: gridTab },
          { id: "wiring", label: "配線", content: wiringTab },
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
  const [layoutPatchPreviewNodes, setLayoutPatchPreviewNodes] = useState<
    LayoutPreviewNodeInput[]
  >([]);
  const [layoutPatchPreviewClassRefs, setLayoutPatchPreviewClassRefs] =
    useState<string[]>([]);
  const [layoutApplyHandoffOpen, setLayoutApplyHandoffOpen] = useState(false);
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

  // ── derived ─────────────────────────────────────────────────────────────
  const tensorPatchJson = buildVisualLayoutPatchJson(
    draftNodes,
    selectedLayoutClassRefs,
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

  // ── layout patch (preview / validate / apply) ────────────────────────────
  const callLayoutPatch = async (action: "preview" | "validate" | "apply") => {
    setPatchErrors([]);
    setPatchSummary(null);
    setDebugJson(null);
    setLayoutApplyHandoffOpen(false);
    if (action !== "preview") {
      setLayoutPatchPreviewNodes([]);
      setLayoutPatchPreviewClassRefs([]);
    }

    if (!canPatch) {
      setPatchErrors([{
        code: "NO_ROUTE_LAYOUT",
        message: "ルートとレイアウトを選択してください。",
      }]);
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
        return;
      }
    }

    const phaseMap: Record<string, LifecyclePhase> = {
      preview: "previewing",
      validate: "validating",
      apply: "applying",
    };
    setLifecyclePhase(phaseMap[action] as LifecyclePhase);
    setLoading(true);
    announce(
      `${
        action === "preview"
          ? "プレビュー"
          : action === "validate"
          ? "バリデート"
          : "適用"
      }を実行中...`,
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

      // Failure phases are scoped to the action: preview/validate errors stay in their
      // respective phase (pipeline not advanced); only apply failures use applied_fail.
      const failPhase: Record<string, LifecyclePhase> = {
        preview: "previewed",
        validate: "validated",
        apply: "applied_fail",
      };
      if (body?.errors?.length) {
        setPatchErrors(body.errors);
        setLifecyclePhase(failPhase[action] as LifecyclePhase);
        announce(`エラー: ${body.errors[0].message}`);
      } else {
        const donePhase: Record<string, LifecyclePhase> = {
          preview: "previewed",
          validate: "validated",
          apply: "applied_ok",
        };
        const isPersisted = body?.emission?.data?.persisted === true;
        setLifecyclePhase(
          isPersisted ? "persisted" : donePhase[action] as LifecyclePhase,
        );

        if (action === "preview") {
          const previewData = (body?.emission?.data ?? body) as {
            tensorPatchJson?: string;
          };
          const normalizedJson =
            typeof previewData?.tensorPatchJson === "string"
              ? previewData.tensorPatchJson
              : submittedTensorPatchJson;
          if (
            applyCanvasFromTensorPatch(
              normalizedJson,
              "プレビュー結果を反映",
              { seedWhenEmpty: false },
            )
          ) {
            announce("プレビュー結果を canvas に反映しました");
          }
          const parsed = parseVisualLayoutPatchJson(
            normalizedJson,
            paletteSeedEntries,
          );
          if (parsed.ok) {
            setLayoutPatchPreviewClassRefs(parsed.value.layoutClassRefs);
          }
          announce("プレビュー結果を canvas とステータスに反映しました");
        }

        if (action === "apply" && summary.valid) {
          // Clear _tmp on successful apply (backend cleared server-side; clear local fallbacks too)
          if (tmpDraftKey && typeof globalThis.sessionStorage !== "undefined") {
            sessionStorage.removeItem(tmpDraftKey);
          }
          setTmpSaveStatus("idle");
          const parsed = parseVisualLayoutPatchJson(
            submittedTensorPatchJson,
            paletteSeedEntries,
          );
          const handoffNodes = enrichLayoutPreviewNodes(
            parsed.ok ? parsed.value.nodes : draftNodes,
            paletteSeedEntries,
          ) as LayoutPreviewNodeInput[];
          setLayoutPatchPreviewNodes(handoffNodes);
          setLayoutPatchPreviewClassRefs(
            parsed.ok ? parsed.value.layoutClassRefs : selectedLayoutClassRefs,
          );
          setLayoutApplyHandoffOpen(true);
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
        preview: "previewed",
        validate: "validated",
        apply: "applied_fail",
      };
      setPatchErrors([{ code: "NETWORK_ERROR", message: String(e) }]);
      setLifecyclePhase(failPhase[action] as LifecyclePhase);
      announce(`ネットワークエラー: ${e}`);
    } finally {
      setLoading(false);
    }
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
      if (
        current.inlineText === merged.inlineText &&
        current.linkHref === merged.linkHref &&
        current.linkTarget === merged.linkTarget
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
    setDraftNodes((prev) => {
      const next = prev.map((n) => {
        if (n.nodeId !== nodeId) return n;
        const current = n.layoutClassRefs ?? [];
        const layoutClassRefs = current.includes(classKey)
          ? current.filter((k) => k !== classKey)
          : [...current, classKey];
        return { ...n, layoutClassRefs };
      });
      pushHistory(next, "ノード layoutClassRefs を変更");
      return next;
    });
    setLifecyclePhase("idle");
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
          適用は <code>preview → validate → apply</code>{" "}
          経由。直接 DB 書き込みは行いません。
        </div>
      </details>

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
        disabled={selectorsDisabled || layoutSelectorsLocked}
        loadError={candidateErrors}
      />

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
            layoutClassRefs は右ドックの配置インスペクタで編集し、プレビュー → 検証 → 保存反映します。
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
        {/* Left docked panel — 部品追加 (SSOT: canvas_workspace_contract.left_panel) */}
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
        <div class="flex min-h-0 min-w-0 flex-1 flex-col">
          <FlowLayoutCanvas
            nodes={draftNodes}
            selectedNodeId={selectedNodeId}
            rootLayoutClassRefs={selectedLayoutClassRefs}
            designDraftByNodeId={designDraftByNodeId}
            canvasRef={canvasRef}
            minHeight={CANVAS_MIN_HEIGHT}
            onSelectNode={(id) => setSelectedNodeId(id)}
            onDeselectAll={() => {
              if (selectedNodeId) {
                setDesignDraftByNodeId((prev) => {
                  const next = new Map(prev);
                  next.delete(selectedNodeId);
                  return next;
                });
              }
              setSelectedNodeId(null);
            }}
            onDragOver={handleDragOverCanvas}
            onDrop={handleDropOnCanvas}
            onDeleteNode={removeNode}
            onAddFromEmptyState={packageScopedLayout
              ? handleAddFromEmptyState
              : undefined}
            allowEmptyStateTemplates={packageScopedLayout}
          />
        </div>

        <LayoutRightDock
          draftNodes={draftNodes}
          selectedNodeId={selectedNodeId}
          selectedNode={selectedNode}
          packageId={scopedPackageId ?? ""}
          onSelectNode={(id) => {
            if (selectedNodeId && selectedNodeId !== id) {
              setDesignDraftByNodeId((prev) => {
                const next = new Map(prev);
                next.delete(selectedNodeId);
                return next;
              });
            }
            setSelectedNodeId(id);
          }}
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
        />
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

      {/* アクションバー: preview / validate / apply — 常時表示 */}
      <div class="mb-3 rounded border border-slate-200 bg-slate-50 px-3 py-2">
        <div class="mb-1.5 text-[0.65rem] font-semibold uppercase tracking-wide text-slate-500">
          保存フロー: プレビュー → バリデート → 適用
        </div>
        <div class="flex flex-wrap gap-2">
          <button
            onClick={() => callLayoutPatch("preview")}
            disabled={loading || !canPatch}
            class="btn-secondary min-w-[100px]"
            aria-label="プレビュー — canvasへ保存前結果を反映（DB変更なし）"
          >
            1. プレビュー
          </button>
          <button
            onClick={() => callLayoutPatch("validate")}
            disabled={loading || !canPatch}
            class="btn border border-blue-600 text-blue-600 hover:bg-blue-50 min-w-[100px]"
            aria-label="バリデート実行 — ref整合チェック"
          >
            2. バリデート
          </button>
          <button
            onClick={() => callLayoutPatch("apply")}
            disabled={loading || !canPatch}
            class="btn-success min-w-[100px]"
            aria-label="適用実行 — DBへ反映"
          >
            3. 適用
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
          プレビュー: center canvas / inline summary に反映（DB変更なし） → バリデート:
          ref整合チェック → 適用: DBへ反映
        </p>
      </div>


      <LayoutPatchApplyHandoffModal
        open={layoutApplyHandoffOpen}
        routeKey={effectiveRouteKey}
        layoutLabel={selectedLayout?.layoutKey ?? shortId(effectiveLayoutId)}
        nodeCount={draftNodes.length}
        previewNodes={layoutPatchPreviewNodes}
        layoutClassRefs={layoutPatchPreviewClassRefs.length > 0
          ? layoutPatchPreviewClassRefs
          : selectedLayoutClassRefs}
        onClose={() => setLayoutApplyHandoffOpen(false)}
        onGoDesign={() => {
          setLayoutApplyHandoffOpen(false);
          announce("右パネルのデザインインスペクタで選択ノードを編集できます");
        }}
      />

      {/* Gap 3: Actionable error panel */}
      {patchErrors.length > 0 && (
        <ActionableValidationErrorPanel
          errors={patchErrors}
          title="エラー — 修正方法"
        />
      )}

      {patchSummary && <LayoutPatchSummaryPanel summary={patchSummary} />}

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
    const label = shape?.userFacingTopologyLabel?.trim() ??
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
  };
}

function PackageDesignPanel({
  selectedPackageId,
  selectedCanvasNode,
  routeCandidates,
  onDesignPreviewChange,
}: {
  selectedPackageId: string;
  selectedCanvasNode: DraftNode | null;
  routeCandidates?: string[];
  onDesignPreviewChange?: (nodeId: string, partial: DesignDraft) => void;
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

  const pushCanvasPreview = (
    nodeId: string,
    text: string,
    href: string,
    target: string,
  ) => {
    onDesignPreviewChange?.(nodeId, designPreviewDraft(text, href, target));
  };

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
      pushCanvasPreview(
        layoutNodeId,
        design.inlineText,
        design.linkHref,
        design.linkTarget,
      );
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
      return;
    }
    const saved = savedDesigns.find((d) =>
      d.layoutNodeId === selectedCanvasNode.nodeId
    );
    if (saved) {
      applySavedDesign(saved);
      return;
    }
    const defaultText = selectedCanvasNode.htmlTag === "a" ? "リンクテキスト" : "";
    setDesignName(`${selectedCanvasNode.nodeId}_design`);
    setCssTokenRefs([]);
    setResponsiveTokenRefs({});
    setInlineText(defaultText);
    setLinkHref("");
    setLinkTarget("");
    setReactionIntent("");
    setClassname("");
    setTailwind("");
    setDesignTmpStatus("idle");
    pushCanvasPreview(selectedCanvasNode.nodeId, defaultText, "", "");
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
            id: "wiring",
            label: "配線",
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

      <div
        class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900"
        role="note"
      >
        ルートを<strong>確定</strong>（候補選択または直接入力で Enter）するとパッケージが自動生成され、canvas
        workspace が使えます。入力のたびに登録はされません。
      </div>

      <div class="mb-3 rounded border border-slate-200 bg-white p-3">
        <BucketPackageRouteFields
          routeKey={routeKey}
          manualRouteDraft={manualRouteDraft}
          committedRouteKey={committedRouteKey}
          routeOptions={routeOptions}
          candidateErrors={candidateErrors}
          onRouteKeyChange={(key) => {
            setRouteKey(key);
            setCommittedManualRouteKey("");
            setManualRouteDraft("");
          }}
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
