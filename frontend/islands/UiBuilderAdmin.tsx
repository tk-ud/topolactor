import { useEffect, useRef, useState } from "preact/hooks";
import { JSX } from "preact";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import {
  CSS_DICTIONARY_TOKENS,
  buildInlineStyleFromCssTokenRefs,
  resolveCssTokenValue,
} from "../runtime/cssDictionary.ts";
import { TOPOLOGY_LAYOUT_CLASS_DICTIONARY } from "../runtime/topologyLayoutClassDictionary.ts";
import { resolveTopologyLayoutClassRefs } from "../runtime/topologyLayoutClassResolver.ts";
import { OperationGuardBanner } from "../components/OperationGuardBanner.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { CandidateConfidenceBadge } from "../components/CandidateConfidenceBadge.tsx";
import { projectCiAttentionGuidance, type CiAttentionGuidanceItem } from "../runtime/abstractFunctions.ts";
import { createSseReceiver, extractCiAttentionFragmentPayload, type CiAttentionFragmentProjectionPayload } from "../runtime/sseReceiver.ts";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel, { AdminActionHint } from "../components/AdminHelpPanel.tsx";
import { ADMIN_UI_BUILDER_GUIDE } from "../content/adminGuides.ts";
import {
  UX_UI_BUILDER_TAB_LABELS,
  UX_LAYOUT_EDITOR_SURFACE,
  UX_DESIGN_EDITOR_SURFACE,
  UX_VISUAL_VIEW_SURFACE,
} from "../content/adminUxTerms.ts";
import {
  snapToGrid,
  buildVisualLayoutPatchJson,
  parseVisualLayoutPatchJson,
  seedDraftNodesFromPalette,
  wouldCreateVisualParentCycle,
  RESPONSIVE_BREAKPOINTS,
  filterEmptyResponsiveRules,
  validateResponsiveTokenRulesJson,
  reorderLayoutNodeStack,
  cloneVisualNode,
  makeStructuralHtmlNode,
  STRUCTURAL_HTML_TAG_ALLOWLIST,
  type LayoutNodeKind,
  type PaletteDraftSeedEntry,
  type ResponsiveTokenRules,
  type StructuralHtmlTag,
} from "../runtime/visualLayoutUtils.ts";
import { LayoutVisualAuditCanvas } from "../components/LayoutVisualAuditCanvas.tsx";
import {
  resolveCanvasRootPreviewClassName,
  resolveNodeWrapperPreviewClassName,
} from "../runtime/layoutClassPreviewUtils.ts";
import {
  buildLayoutPatchPreviewAudit,
  type LayoutPatchPreviewAudit,
} from "../runtime/layoutPatchPreviewUtils.ts";
import { LayoutPatchPreviewModal } from "../components/LayoutPatchPreviewModal.tsx";
import { LayoutPatchApplyHandoffModal } from "../components/LayoutPatchApplyHandoffModal.tsx";
import type { LayoutPreviewNodeInput } from "../runtime/layoutComponentPreview.ts";
import { resolveBucketStatus, type BucketItem } from "../runtime/bucketUtils.ts";
import {
  createEmptyLabelValueEditorRow,
  LABEL_VALUE_DISPLAY_POLICIES,
  serializeLabelValueMetadataJson,
  type LabelValueEditorRow,
} from "../runtime/labelValueEditor.ts";
import {
  PACKAGE_WIRING_TARGET_SURFACES,
} from "../lib/packageWiringOptions.ts";
import {
  buildWiringKindSelectOptions,
  encodeManifestPackageTargetRef,
  manifestIdFromTargetRef,
  manifestWiringKeyFromTargetRef,
  mergeManifestPickerOptions,
  type ManifestPickerOption,
} from "../lib/packageWiringPicker.ts";
import {
  buildScreenReadQueryWiringCandidates,
  type ScreenReadQueryWiringCandidate,
} from "../lib/screenReadQueryWiring.ts";
import {
  listAdminManifests,
  getAdminManifest,
} from "../api/adminApi.ts";
import { getStoredScreenLabel } from "../runtime/screenAuthoringIntent.ts";
import { extractScreenDataShapeFromTopology } from "../lib/manifestTopologyExtensions.ts";
import { useConfirm } from "../hooks/useConfirm.tsx";
import { LayoutPreviewNodeFrame } from "../components/LayoutPreviewNodeFrame.tsx";
import {
  resolveComponentKindForLayoutPreview,
  getLayoutPreviewDefaultSize,
  enrichLayoutPreviewNodes,
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

const SESSION_TOKEN_KEY = "demo_jwt_token";

// ─── ユーティリティ ──────────────────────────────────────────────────────────

// deno-lint-ignore no-explicit-any
async function dispatchAdminOp(layer: string, action: string, payload?: unknown): Promise<any> {
  const token = typeof globalThis.sessionStorage !== "undefined"
    ? sessionStorage.getItem(SESSION_TOKEN_KEY) ?? undefined : undefined;
  return queueAdminClientCommand({
    operationType: "admin",
    target: "admin",
    layer,
    action,
    payload: payload != null ? payload as Record<string, unknown> : undefined,
  }, token);
}

// ─── 型定義 ──────────────────────────────────────────────────────────────────

type ValidationError = { code: string; message: string; field?: string; nodeId?: string; componentKey?: string };

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
  // v2: visual canvas position/size (pixels, snapped to SNAP_SIZE grid)
  x: number;
  y: number;
  width: number;
  height: number;
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
};

// v2: canvas interaction types
type ResizeDir = "n" | "s" | "e" | "w" | "nw" | "ne" | "sw" | "se";

type CanvasDragState = {
  nodeId: string;
  startMouseX: number;
  startMouseY: number;
  startNodeX: number;
  startNodeY: number;
} | null;

/** Hold left button this long before canvas node drag activates (avoids click/drag conflict). */
const CANVAS_DRAG_HOLD_MS = 300;
const CANVAS_DRAG_MOVE_THRESHOLD_PX = 5;

type CanvasPendingDragState = {
  nodeId: string;
  startMouseX: number;
  startMouseY: number;
  startNodeX: number;
  startNodeY: number;
  holdTimerId: ReturnType<typeof setTimeout>;
} | null;

type CanvasResizeState = {
  nodeId: string;
  dir: ResizeDir;
  startMouseX: number;
  startMouseY: number;
  startNodeX: number;
  startNodeY: number;
  startNodeW: number;
  startNodeH: number;
} | null;

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
type WorkspacePanel = "bucket" | "design";

// Gap 1: Lifecycle state machine
type LifecyclePhase =
  | "idle"
  | "previewing" | "previewed"
  | "validating" | "validated"
  | "applying" | "applied_ok" | "applied_fail"
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

// ─── ステータスバッジ ─────────────────────────────────────────────────────────

function StatusBadge({ text, variant }: { text: string; variant: "ok" | "warn" | "error" | "info" }): JSX.Element {
  const cls = {
    ok: "badge-ok",
    warn: "badge-warn",
    error: "badge-error",
    info: "badge-info",
  }[variant];
  return <span class={cls}>{text}</span>;
}

// ─── ヘルパー ─────────────────────────────────────────────────────────────────

function makeNodeId(): string {
  return `node_${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
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

const GENERIC_SLOT_KEYS = ["main", "header", "footer", "sidebar", "content", "body"];

// v2 visual canvas constants
const SNAP_SIZE = 10;
const DEFAULT_NODE_WIDTH = 140;
const DEFAULT_NODE_HEIGHT = 60;
const CANVAS_MIN_HEIGHT = 400;
const MAX_HISTORY = 50;

// Gap 3: Error code → actionable cause + fix
const ERROR_CODE_FIX: Record<string, { cause: string; suggestion: string; navigateTo?: WorkspacePanel }> = {
  DRAFT_ONLY_NODES: {
    cause: "まだ使えない部品が含まれています",
    suggestion: "「部品登録」パネルで対象の部品を配置可能にしてください",
    navigateTo: "bucket",
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
    suggestion: "デザインインスペクタで正しいトークンを選択してください",
    navigateTo: "design",
  },
  LAYOUT_CLASS_REF_INVALID: {
    cause: "レイアウトクラス参照が解決できません",
    suggestion: "topology layout class ref を確認し、有効なキーを選択してください",
  },
  LAYOUT_CANDIDATES_LOAD_FAILED: {
    cause: "レイアウト候補の取得に失敗しました",
    suggestion: "バックエンド接続と認証トークンを確認してください",
  },
  BUCKET_CREATE_FAILED: {
    cause: "部品の登録に失敗しました",
    suggestion: "すでに登録済みでないか確認してください",
    navigateTo: "bucket",
  },
  GENERATE_FAILED: {
    cause: "パッケージ化に失敗しました",
    suggestion: "バックエンド接続を確認し、ルートキーが正しいか再確認してください",
    navigateTo: "bucket",
  },
  PROMOTE_FAILED: {
    cause: "配置可能化に失敗しました",
    suggestion: "先にパッケージ化を完了してから実行してください",
    navigateTo: "bucket",
  },
  LAYOUT_ID_MISMATCH: {
    cause: "サーバーが異なるレイアウトIDを返しました",
    suggestion: "レイアウト候補を再読み込みして、正しいレイアウトを選択してください",
  },
  RESPONSIVE_TOKEN_RULE_JSON_INVALID: {
    cause: "レスポンシブルール JSON が不正です",
    suggestion: "形式: {\"sm\": [\"token.key\"], \"md\": [\"token.key\"]}。有効ブレークポイント: sm, md, lg, xl",
  },
};

function deriveCandidatesFromPalette(promoted: PromotedPaletteEntry[]): LayoutRouteCandidate[] {
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

function dispatchOpFailed(
  body: { success?: boolean; errors?: ValidationError[] } | null | undefined,
): boolean {
  return !body?.success || (Array.isArray(body?.errors) && body.errors.length > 0);
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
  const routeKey = typeof data.routeKey === "string" ? data.routeKey : fallbackRouteKey;
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
  if (candidates.some((c) => c.routeKey === rk && c.layoutId === lid)) return candidates;
  return [...candidates, layoutCandidateForPackage(rk, lid)];
}

function uniqueRouteKeys(candidates: LayoutRouteCandidate[]): string[] {
  return [...new Set(candidates.map((c) => c.routeKey))].sort();
}

function layoutsForRoute(candidates: LayoutRouteCandidate[], routeKey: string): LayoutRouteCandidate[] {
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
      errors: [{ code: "LAYOUT_CANDIDATES_LOAD_FAILED", message: "候補データが取得できませんでした。" }],
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
  const emission = body?.emission as { data?: Record<string, unknown> } | undefined;
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
    if (errors.some((e) => e.code?.includes("CSS") || e.message?.includes("CSS"))) {
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
    nextAction = "モーダルで次のステップ（デザイン設定 / デモ / ページ群管理）を選んでください";
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
function LifecycleStepIndicator({ phase }: { phase: LifecyclePhase }): JSX.Element {
  const steps: { id: string; label: string; phases: LifecyclePhase[] }[] = [
    { id: "draft", label: "ドラフト編集", phases: ["idle", "previewing", "previewed"] },
    { id: "validated", label: "検証済み", phases: ["validating", "validated"] },
    { id: "applied", label: "適用済み", phases: ["applying", "applied_ok", "applied_fail"] },
    { id: "persisted", label: "永続化完了", phases: ["persisted"] },
  ];
  const currentIdx = steps.findIndex((s) => s.phases.includes(phase));
  const isError = phase === "applied_fail";

  return (
    <div class="mb-4" role="status" aria-label={`現在のフェーズ: ${steps[Math.max(0, currentIdx)]?.label ?? phase}`}>
      <div class="flex items-center gap-0">
        {steps.map((step, i) => {
          const isDone = i < currentIdx;
          const isCurrent = i === currentIdx;
          const isErrorStep = isCurrent && isError;
          return (
            <div key={step.id} class="flex flex-1 flex-col items-center">
              <div class="flex w-full items-center">
                {i > 0 && (
                  <div class={`h-0.5 flex-1 ${isDone ? "bg-blue-500" : "bg-gray-200"}`} />
                )}
                <div
                  class={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${
                    isErrorStep ? "bg-red-500 text-white"
                    : isCurrent ? "bg-blue-600 text-white ring-2 ring-blue-300 ring-offset-1"
                    : isDone ? "bg-blue-500 text-white"
                    : "border-2 border-gray-300 bg-white text-gray-400"
                  }`}
                  aria-current={isCurrent ? "step" : undefined}
                >
                  {isDone ? "✓" : isErrorStep ? "✗" : String(i + 1)}
                </div>
                {i < steps.length - 1 && (
                  <div class={`h-0.5 flex-1 ${isDone ? "bg-blue-500" : "bg-gray-200"}`} />
                )}
              </div>
              <div class={`mt-1 text-center text-[0.65rem] font-medium ${
                isErrorStep ? "text-red-600"
                : isCurrent ? "text-blue-700"
                : isDone ? "text-blue-500"
                : "text-gray-400"
              }`}>
                {step.label}
              </div>
            </div>
          );
        })}
      </div>
      {isError && (
        <p role="alert" class="mt-2 text-xs text-red-700">
          エラー — 「エラー — 修正方法」を確認してください。まだ使えない部品がある場合は部品登録パネルへ戻ってください。
        </p>
      )}
      {(phase === "applied_ok" || phase === "persisted") && (
        <p role="status" class="mt-2 rounded border border-green-300 bg-green-50 px-2 py-1.5 text-xs font-medium text-green-800">
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
  onNavigate,
}: {
  errors: AnnotatedValidationError[];
  title?: string;
  onNavigate?: (panel: WorkspacePanel) => void;
}): JSX.Element | null {
  if (errors.length === 0) return null;
  const shownNavigateTabs = new Set<WorkspacePanel>();
  return (
    <div role="alert" class="rounded-lg border border-red-300 bg-red-50 p-3 text-sm">
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
                  {e.componentKey && <span>部品: <code>{friendlyComponentLabel(e.componentKey)}</code>{" "}</span>}
                  {e.field && <span>フィールド: <code>{e.field}</code>{" "}</span>}
                  {e.nodeId && <span class="text-gray-400">({e.nodeId.slice(0, 8)})</span>}
                </div>
              )}
              <span class="font-mono text-[0.65rem] text-gray-400">[{e.code}]</span>
            </li>
          );
        })}
      </ul>
      {onNavigate && (() => {
        const navButtons: JSX.Element[] = [];
        for (const e of errors) {
          const fix = ERROR_CODE_FIX[e.code];
          if (fix?.navigateTo && !shownNavigateTabs.has(fix.navigateTo)) {
            shownNavigateTabs.add(fix.navigateTo);
            const tab = fix.navigateTo;
            const label = tab === "bucket" ? "→ 部品登録パネルへ移動"
              : "→ デザインインスペクタを開く";
            navButtons.push(
              <button
                key={tab}
                type="button"
                onClick={() => onNavigate(tab)}
                class="mt-2 rounded bg-red-600 px-2 py-1 text-xs font-medium text-white hover:bg-red-700"
              >
                {label}
              </button>
            );
          }
        }
        return navButtons.length > 0 ? <div class="mt-2 flex flex-wrap gap-2">{navButtons}</div> : null;
      })()}
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
        通常導線外の手入力です。SSOT key/UUID を直接指定する場合のみ使用してください。
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
  onNavigate,
}: {
  canPatch: boolean;
  effectiveRouteKey: string;
  effectiveLayoutId: string;
  draftNodes: DraftNode[];
  layoutClassRefError: string | null;
  onNavigate?: (panel: WorkspacePanel) => void;
}): JSX.Element {
  const draftOnlyCount = draftNodes.filter((n) => n.isDraftOnly).length;
  const customPositionedCount = draftNodes.filter(
    (n) => n.x > 0 || n.y > 0 || n.width !== DEFAULT_NODE_WIDTH || n.height !== DEFAULT_NODE_HEIGHT,
  ).length;
  const allClear = canPatch && draftOnlyCount === 0 && !layoutClassRefError;

  return (
    <div class={`mb-3 rounded border p-3 text-sm ${allClear ? "border-green-300 bg-green-50" : "border-amber-300 bg-amber-50"}`}>
      <strong class="block mb-2">保存前チェック</strong>
      <ul class="space-y-1 pl-1">
        <li class="flex items-start gap-2">
          <span class={canPatch ? "text-green-700" : "text-red-600"}>{canPatch ? "✓" : "✗"}</span>
          <span>
            ルート / レイアウト選択:{" "}
            {canPatch
              ? <><code class="text-xs">{effectiveRouteKey}</code> / <code class="text-xs">{shortId(effectiveLayoutId)}</code></>
              : "未選択 — ルートとレイアウトを選択してください"}
          </span>
          {!canPatch && onNavigate && (
            <button
              type="button"
              onClick={() => onNavigate("bucket")}
              class="ml-2 rounded bg-amber-600 px-1.5 py-0.5 text-xs font-medium text-white hover:bg-amber-700"
            >
              部品登録パネルで確認する
            </button>
          )}
        </li>
        <li class="flex items-start gap-2">
          <span class={draftOnlyCount === 0 ? "text-green-700" : "text-red-600"}>{draftOnlyCount === 0 ? "✓" : "✗"}</span>
          <span>
            まだ使えない部品:{" "}
            {draftOnlyCount === 0
              ? "なし"
              : <>{draftOnlyCount} 件 — 先に部品登録を完了してください（保存はブロック）</>}
          </span>
          {draftOnlyCount > 0 && onNavigate && (
            <button
              type="button"
              onClick={() => onNavigate("bucket")}
              class="ml-2 rounded bg-red-600 px-1.5 py-0.5 text-xs font-medium text-white hover:bg-red-700"
            >
              部品登録パネルで修正する →
            </button>
          )}
        </li>
        <li class="flex items-start gap-2">
          <span class={!layoutClassRefError ? "text-green-700" : "text-red-600"}>{!layoutClassRefError ? "✓" : "✗"}</span>
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
              : draftNodes.length > 0 ? " (canvas デフォルト配置)" : ""}
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
          すべてのローカルチェック通過。プレビュー（視覚監査） → バリデート → 適用 の順で実行してください。
        </p>
      )}
    </div>
  );
}

function LayoutPatchSummaryPanel({ summary }: { summary: LayoutPatchSummary }): JSX.Element {
  return (
    <div class={`rounded border p-3 text-sm ${summary.valid ? "border-green-300 bg-green-50" : "border-red-300 bg-red-50"}`}>
      <div class="mb-2 flex flex-wrap items-center gap-2">
        <strong>{summary.action === "preview" ? "プレビュー" : summary.action === "validate" ? "バリデート" : "適用"} 結果</strong>
        <StatusBadge text={summary.valid ? "問題なし" : "エラーあり"} variant={summary.valid ? "ok" : "error"} />
      </div>
      <ul class="my-0 pl-4">
        <li>ノード数: {summary.nodeCount}（まだ使えない部品: {summary.draftOnlyCount}）</li>
        <li>ルート: <code>{summary.routeKey || "—"}</code></li>
        <li>レイアウト: {summary.layoutKey ? <code>{summary.layoutKey}</code> : <code>{shortId(summary.layoutId) || "—"}</code>}</li>
        <li>CSS トークン: {summary.cssTokenCount} 件</li>
        <li>メッセージ: {summary.message}</li>
        <li><strong>次のアクション:</strong> {summary.nextAction}</li>
      </ul>
      {summary.errors.length > 0 && (
        <ValidationErrorPanel errors={summary.errors} title="修正が必要なエラー" />
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
          {routes.map((r) => (
            <option key={r} value={r}>{r}</option>
          ))}
        </select>
      </label>
      <label class="flex min-w-[240px] flex-[2] flex-col gap-0.5 text-sm">
        レイアウト
        <select
          value={layoutId}
          disabled={disabled || !routeKey || layouts.length === 0}
          onChange={(e) => onLayoutChange((e.target as HTMLSelectElement).value)}
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
function CssTokenSwatch({ token }: { token: { tokenKey: string; category: string; property: string; semanticRole: string } }): JSX.Element {
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
    return <span class="align-middle text-[0.6rem] text-gray-500" style={{ fontFamily: val }} aria-label={`フォント: ${val}`} title={val}>Aa</span>;
  }
  const val = resolveCssTokenValue(token.tokenKey);
  return <span class="text-[0.6rem] text-gray-400" title={val}>{token.property.slice(0, 3)}</span>;
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

  const categories = [...new Set(CSS_DICTIONARY_TOKENS.map((t) => t.category))].sort();
  const scopes = [...new Set(CSS_DICTIONARY_TOKENS.flatMap((t) => t.componentScope))].sort();
  const roles = [...new Set(CSS_DICTIONARY_TOKENS.map((t) => t.semanticRole))].sort();

  const filtered = CSS_DICTIONARY_TOKENS.filter((t) => {
    if (tokenFilter && !t.tokenKey.toLowerCase().includes(tokenFilter.toLowerCase())) return false;
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
        <select value={categoryFilter} onChange={(e) => setCategoryFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs" aria-label="カテゴリでフィルター">
          <option value="">カテゴリ（すべて）</option>
          {categories.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        <select value={scopeFilter} onChange={(e) => setScopeFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs" aria-label="スコープでフィルター">
          <option value="">対象部品（すべて）</option>
          {scopes.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select value={roleFilter} onChange={(e) => setRoleFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs" aria-label="役割でフィルター">
          <option value="">役割（すべて）</option>
          {roles.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
      </div>

      {/* Gap 8: before/after visual diff for selected tokens */}
      {selectedTokenRefs.length > 0 && (
        <div class="mb-3 rounded border border-blue-200 bg-blue-50 p-2">
          <strong class="text-xs text-blue-800">選択済みトークン ({selectedTokenRefs.length}) — クリックで解除</strong>
          <div class="mt-2 flex flex-wrap gap-2">
            {selectedTokenRefs.map((key) => {
              const token = CSS_DICTIONARY_TOKENS.find((t) => t.tokenKey === key);
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

      <div class="table-wrap max-h-64 overflow-y-auto" role="region" aria-label="CSSトークン一覧">
        <table class="table font-mono text-xs">
          <thead>
            <tr>
              {["", "プレビュー", "トークンキー", "カテゴリ", "対象", "CSSプロパティ"].map((h) => (
                <th key={h} scope="col">{h}</th>
              ))}
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
                  <td><CssTokenSwatch token={t} /></td>
                  <td><code>{t.tokenKey}</code></td>
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
        <p class="text-muted-xs mt-1">該当トークンなし — フィルタを調整してください。</p>
      )}
    </div>
  );
}

function TopologyLayoutClassPicker({
  selectedClassRefs,
  onToggle,
  scopeFilter = "",
  allowedForFilter = "",
}: {
  selectedClassRefs: string[];
  onToggle: (classKey: string) => void;
  scopeFilter?: string;
  allowedForFilter?: string;
}): JSX.Element {
  const [keyFilter, setKeyFilter] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [scopeFilterState, setScopeFilterState] = useState(scopeFilter);
  const [roleFilter, setRoleFilter] = useState("");

  const categories = [...new Set(TOPOLOGY_LAYOUT_CLASS_DICTIONARY.map((e) => e.category))].sort();
  const scopes = [...new Set(TOPOLOGY_LAYOUT_CLASS_DICTIONARY.flatMap((e) => e.projectionScope))].sort();
  const roles = [...new Set(TOPOLOGY_LAYOUT_CLASS_DICTIONARY.map((e) => e.semanticRole))].sort();

  const filtered = TOPOLOGY_LAYOUT_CLASS_DICTIONARY.filter((e) => {
    if (keyFilter && !e.classKey.toLowerCase().includes(keyFilter.toLowerCase())) return false;
    if (categoryFilter && e.category !== categoryFilter) return false;
    if (scopeFilterState && !e.projectionScope.includes(scopeFilterState)) return false;
    if (roleFilter && e.semanticRole !== roleFilter) return false;
    if (allowedForFilter && !e.allowedFor.includes(allowedForFilter)) return false;
    return true;
  });

  const preview = selectedClassRefs.length > 0
    ? resolveTopologyLayoutClassRefs(selectedClassRefs)
    : null;

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
        <select value={categoryFilter} onChange={(e) => setCategoryFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">カテゴリ（すべて）</option>
          {categories.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        <select value={scopeFilterState} onChange={(e) => setScopeFilterState((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">適用範囲（すべて）</option>
          {scopes.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select value={roleFilter} onChange={(e) => setRoleFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">役割（すべて）</option>
          {roles.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
      </div>

      {selectedClassRefs.length > 0 && (
        <div class="mb-2 rounded border border-blue-200 bg-blue-50 p-2">
          <strong class="text-xs">選択済みスタイルクラス ({selectedClassRefs.length})</strong>
          <div class="mt-1 flex flex-wrap gap-1">
            {selectedClassRefs.map((key) => (
              <button
                key={key}
                type="button"
                onClick={() => onToggle(key)}
                class="rounded border border-blue-400 bg-white px-1.5 py-0.5 font-mono text-xs hover:bg-red-50"
              >
                {key} ✕
              </button>
            ))}
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

      <div class="table-wrap max-h-64 overflow-y-auto">
        <table class="table font-mono text-xs">
          <thead>
            <tr>
              {["選択", "クラスキー", "クラス名", "カテゴリ", "適用範囲", "対象"].map((h) => (
                <th key={h}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.map((e) => (
              <tr key={e.classKey} class={selectedClassRefs.includes(e.classKey) ? "bg-blue-50" : ""}>
                <td>
                  <input
                    type="checkbox"
                    checked={selectedClassRefs.includes(e.classKey)}
                    onChange={() => onToggle(e.classKey)}
                  />
                </td>
                <td><code>{e.classKey}</code></td>
                <td><code>{e.className}</code></td>
                <td>{e.category}</td>
                <td>{e.projectionScope.join(",")}</td>
                <td>{e.allowedFor.join(",")}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ─── CI ガイダンスセクション ──────────────────────────────────────────────────

function CiAttentionGuidanceSection(): JSX.Element {
  const [guidance, setGuidance] = useState<CiAttentionGuidanceItem[]>([]);
  const [status, setStatus] = useState("未ロード");
  const [liveFragments, setLiveFragments] = useState<CiAttentionFragmentProjectionPayload[]>([]);
  const [errors, setErrors] = useState<{ message: string; code?: string }[]>([]);

  useEffect(() => {
    const receiver = createSseReceiver({
      onProjectionHookTrigger: (trigger) => {
        const fragment = extractCiAttentionFragmentPayload(trigger.data);
        if (fragment !== null) {
          setLiveFragments((prev) => {
            const idx = prev.findIndex((f) => f.FragmentId === fragment.FragmentId);
            if (idx >= 0) {
              const next = [...prev];
              next[idx] = fragment;
              return next;
            }
            return [...prev, fragment];
          });
        }
      },
      onError: (state) => {
        if (state.kind !== "connection_closed") {
          setErrors((prev) => [
            ...prev,
            {
              code: state.kind,
              message: state.kind === "parse_error" ? state.error : state.kind,
            },
          ]);
        }
      },
    });
    receiver.connect();
    return () => receiver.disconnect();
  }, []);

  const loadGuidance = async () => {
    const targetsBody = await dispatchAdminOp("system_ci", "list_targets");
    const targets = targetsBody?.emission?.data;
    if (!Array.isArray(targets) || targets.length === 0) {
      setStatus("system_ci ターゲットが見つかりません。");
      return;
    }
    const target = targets[0]?.target;
    const inspectBody = await dispatchAdminOp("system_ci", "inspect", { target });
    const projected = projectCiAttentionGuidance(inspectBody?.emission?.data);
    if (!projected.ok) {
      setErrors([{ code: "GUIDANCE_PROJECTION_FAILED", message: projected.error }]);
      setStatus("ガイダンス投影に失敗しました。");
      return;
    }
    setGuidance(projected.data);
    setStatus(`${projected.data.length} 件のガイダンスをロードしました`);
  };

  const byKind = (kind: CiAttentionGuidanceItem["kind"]) =>
    guidance.filter((g) => g.kind === kind);

  const kindLabel: Record<string, string> = {
    missing_input: "入力不足",
    valid_candidate: "有効候補",
    structural_violation: "構造違反",
  };

  return (
    <div>
      <p class="text-muted mb-3">
        ドラフト編集はガイダンス状態に関わらず常時利用可能です。正規プロモーションにはブロッキングフラグメントの解消が必要な場合があります。
      </p>

      <Accordion title="ガイダンスロード & ライブフラグメント" defaultOpen={true}>
        <button
          type="button"
          onClick={loadGuidance}
          class="btn-primary mb-2"
        >
          ガイダンスをロード
        </button>
        <p class="font-mono text-muted text-sm">{status}</p>

        {liveFragments.length > 0 && (
          <div class="alert-info mb-2 font-mono text-sm">
            <strong>ライブフラグメント更新 ({liveFragments.length} 件):</strong>
            <ul class="my-1 pl-4">
              {liveFragments.map((f) => (
                <li key={f.FragmentId}>
                  [{f.Kind}] {f.TargetKind}/{f.TargetKey} — ステータス:{f.Status}
                </li>
              ))}
            </ul>
            <span class="text-muted-xs">ライブ投影のみ — ドラフト編集には影響しません。</span>
          </div>
        )}
        <ValidationErrorPanel errors={errors} title="ガイダンスエラー" />
      </Accordion>

      <Accordion title="ブレーク境界ガード" defaultOpen={false}>
        <OperationGuardBanner
          level={byKind("break_boundary").length > 0 ? "error" : "info"}
          title="break_boundary"
          message={byKind("break_boundary")[0]?.message ?? "ブレーク境界ガイダンスなし。"}
        />
      </Accordion>

      <Accordion title="ガイダンス詳細" defaultOpen={false}>
        {(["missing_input", "valid_candidate", "structural_violation"] as const).map((kind) => (
          <div key={kind} class="mb-2.5">
            <strong class="text-sm">
              {kindLabel[kind] ?? kind}
            </strong>
            {byKind(kind).length === 0 ? (
              <p class="text-muted-xs mt-1 mb-0">該当なし</p>
            ) : (
              <ul class="mt-1 ml-2 pl-3">
                {byKind(kind).map((item) => (
                  <li key={item.id} class="text-sm">
                    {item.title}: {item.actionable}
                    {kind === "valid_candidate" && (
                      <CandidateConfidenceBadge label="候補" confidence="medium" score={item.confidence} />
                    )}
                  </li>
                ))}
              </ul>
            )}
          </div>
        ))}
      </Accordion>
    </div>
  );
}

// ─── プリミティブカタログ ─────────────────────────────────────────────────────

function PrimitiveCatalog(): JSX.Element {
  const headerMap: Record<string, string> = {
    component_key: "部品名",
    kind: "種別",
    family: "ファミリー",
    semantic_role: "セマンティクス役割",
    visual_role: "ビジュアル役割",
    lifecycle_status: "状態",
    runtime_connected: "DB連携",
    registration_required: "登録要",
    capability_tags: "機能タグ",
  };

  return (
    <div>
      <p class="text-muted mb-3">
        ここに表示されている部品をレイアウトで使うには、部品登録タブで「登録済み」状態にする必要があります。
      </p>
      <div class="table-wrap">
        <table class="table font-mono text-xs">
          <thead>
            <tr>
              {Object.entries(headerMap).map(([k, label]) => (
                <th key={k}>
                  {label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {COMPONENT_CATALOG_ENTRIES.map((c) => (
              <tr key={c.componentKey}>
                <td><code>{c.componentKey}</code></td>
                <td>{c.componentKind}</td>
                <td>{c.componentFamily}</td>
                <td>{c.semanticRole}</td>
                <td>{c.visualRole}</td>
                <td>
                  <StatusBadge
                    text={c.lifecycleStatus === "code_only_drift" ? "未登録（コードのみ）" : c.lifecycleStatus}
                    variant={c.lifecycleStatus === "code_only_drift" ? "warn" : "ok"}
                  />
                </td>
                <td>{String(c.runtimeConnected)}</td>
                <td>{String(c.registrationRequired)}</td>
                <td><code>{c.capabilityTags.join(",")}</code></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <AdvancedManualOverride title="ソースパス一覧（技術詳細）">
        <table class="table font-mono text-xs mt-1">
          <thead><tr><th>部品名</th><th>ソースパス</th></tr></thead>
          <tbody>
            {COMPONENT_CATALOG_ENTRIES.map((c) => (
              <tr key={c.componentKey}>
                <td><code>{c.componentKey}</code></td>
                <td><code>{c.sourcePath}</code></td>
              </tr>
            ))}
          </tbody>
        </table>
      </AdvancedManualOverride>
    </div>
  );
}

function BucketPackageRouteFields({
  routeKey,
  manualRouteKey,
  routeOptions,
  candidateErrors,
  onRouteKeyChange,
  onManualRouteKeyChange,
}: {
  routeKey: string;
  manualRouteKey: string;
  routeOptions: string[];
  candidateErrors: ValidationError[];
  onRouteKeyChange: (routeKey: string) => void;
  onManualRouteKeyChange: (manualRouteKey: string) => void;
}): JSX.Element {
  const effectiveRouteKey = manualRouteKey.trim() || routeKey;
  return (
    <div class="mt-3 rounded border border-slate-200 bg-slate-50 p-3">
      <p class="mb-2 text-xs font-semibold text-slate-700">
        ページルート（パッケージ化に必須）
      </p>
      <label class="mb-2 flex flex-col gap-0.5 text-sm">
        候補から選択
        <select
          value={routeKey}
          onChange={(e) => {
            onRouteKeyChange((e.target as HTMLSelectElement).value);
            onManualRouteKeyChange("");
          }}
          disabled={routeOptions.length === 0}
          class="input font-mono text-xs"
        >
          <option value="">— ルートを選択 —</option>
          {routeOptions.map((r) => (
            <option key={r} value={r}>{r}</option>
          ))}
        </select>
      </label>
      {routeOptions.length === 0 && candidateErrors.length === 0 && (
        <p class="mb-2 text-xs text-amber-900">
          初回は下の直接入力にルート名を入れてください（例: <code>admin_demo_screen_list</code>）。
          パッケージ化後、候補から選べるようになります。
        </p>
      )}
      <label class="flex flex-col gap-0.5 text-sm">
        直接入力（初回はこちら）
        <input
          value={manualRouteKey}
          onInput={(e) => onManualRouteKeyChange((e.target as HTMLInputElement).value)}
          placeholder="例: admin_demo_screen_list"
          class="input-mono w-full text-xs"
        />
      </label>
      {effectiveRouteKey && (
        <p class="mt-2 text-xs text-slate-600">
          使用中のルート: <code class="font-mono">{effectiveRouteKey}</code>
        </p>
      )}
    </div>
  );
}

// ─── バケット管理セクション ───────────────────────────────────────────────────

function BucketSection({
  onNavigate,
  onPackaged,
}: {
  onNavigate?: (panel: WorkspacePanel) => void;
  onPackaged?: (handoff: PackagedHandoff) => void;
}): JSX.Element {
  const { confirm, ConfirmDialogHost } = useConfirm();
  const [items, setItems] = useState<BucketItem[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationError[]>([]);
  const [loading, setLoading] = useState(false);
  const [routeKey, setRouteKey] = useState("");
  const [manualRouteKey, setManualRouteKey] = useState("");
  const [selectedId, setSelectedId] = useState("");
  const [selectedCatalogKeys, setSelectedCatalogKeys] = useState<Set<string>>(new Set());
  const [selectedCatalogKey, setSelectedCatalogKey] = useState("");
  const [catalogFilter, setCatalogFilter] = useState("");
  const [kindFilter, setKindFilter] = useState("");
  const [lifecycleFilter, setLifecycleFilter] = useState("");
  const [layoutCandidates, setLayoutCandidates] = useState<LayoutRouteCandidate[]>([]);
  const [candidateErrors, setCandidateErrors] = useState<ValidationError[]>([]);
  const [promotedKeys, setPromotedKeys] = useState<Set<string>>(new Set());

  const selectedCatalog = COMPONENT_CATALOG_ENTRIES.find((c) => c.componentKey === selectedCatalogKey);
  const effectiveRouteKey = manualRouteKey.trim() || routeKey;

  const loadBucket = async () => {
    setLoading(true);
    setStatus(null);
    setErrors([]);
    try {
      const [bucketedBody, packagingBody] = await Promise.all([
        dispatchAdminOp("ui_component_bucket", "list"),
        dispatchAdminOp("ui_component_bucket", "list", { status: "packaging" }),
      ]);
      const bucketed = Array.isArray(bucketedBody?.emission?.data) ? bucketedBody.emission.data as BucketItem[] : [];
      const packaging = Array.isArray(packagingBody?.emission?.data) ? packagingBody.emission.data as BucketItem[] : [];
      const combined = [...bucketed, ...packaging];
      if (combined.length > 0 || (!bucketedBody?.errors?.length && !packagingBody?.errors?.length)) {
        setItems(combined);
        setStatus(`${combined.length} 件の部品をロードしました。`);
      } else {
        setErrors(bucketedBody?.errors ?? packagingBody?.errors ?? [{ code: "BUCKET_LOAD_FAILED", message: "登録済み部品の読み込みに失敗しました。" }]);
        setStatus("登録済み部品の読み込みに失敗しました。");
      }
    } catch (e) {
      setStatus(`エラー: ${e}`);
      setErrors([{ code: "BUCKET_LOAD_ERROR", message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const init = async () => {
      await loadBucket();
      const { candidates, errors: candErr } = await loadLayoutCandidatesFromBackend();
      setLayoutCandidates(candidates);
      setCandidateErrors(candErr);
      const paletteBody = await dispatchAdminOp("ui_topology", "promoted_palette");
      const promoted = paletteBody?.emission?.data;
      if (Array.isArray(promoted)) {
        setPromotedKeys(new Set(promoted.map((p: PromotedPaletteEntry) => p.componentKey)));
      } else if (paletteBody?.errors?.length) {
        setCandidateErrors((prev) => [...prev, ...paletteBody.errors]);
      }
    };
    init();
  }, []);

  const filteredCatalog = COMPONENT_CATALOG_ENTRIES.filter((c) => {
    if (catalogFilter && !c.componentKey.toLowerCase().includes(catalogFilter.toLowerCase())) return false;
    if (kindFilter && c.componentKind !== kindFilter) return false;
    if (lifecycleFilter && c.lifecycleStatus !== lifecycleFilter) return false;
    return c.registrationRequired;
  });

  const catalogKinds = [...new Set(COMPONENT_CATALOG_ENTRIES.filter((c) => c.registrationRequired).map((c) => c.componentKind))].sort();
  const routeOptions = uniqueRouteKeys(layoutCandidates);

  const toggleCatalogKey = (key: string) => {
    setSelectedCatalogKeys((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
    setSelectedCatalogKey(key);
    const bucketStatus = resolveBucketStatus(key, items, promotedKeys);
    if (bucketStatus.bucketItemId) setSelectedId(bucketStatus.bucketItemId);
  };

  const handlePackageSelected = async () => {
    if (!effectiveRouteKey) {
      setStatus("ページルートを指定してください。");
      return;
    }
    const keys = [...selectedCatalogKeys];
    if (keys.length === 0 && selectedCatalogKey) keys.push(selectedCatalogKey);
    if (keys.length === 0) {
      setStatus("パッケージ化する部品を1件以上選択してください。");
      return;
    }
    if (!(await confirm(`選択した ${keys.length} 件をパッケージ化します。よろしいですか？`))) {
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      // Step 1: ensure all items exist in the bucket (create if absent).
      // Already-promoted components must not be re-bucketed: skip them.
      const bucketItemIds: string[] = [];
      let skippedPromoted = 0;
      for (const key of keys) {
        const entry = COMPONENT_CATALOG_ENTRIES.find((c) => c.componentKey === key);
        if (!entry) continue;
        const bucketStatus = resolveBucketStatus(key, items, promotedKeys);
        // Guard: already-promoted components must not generate duplicate bucket/package entries.
        if (bucketStatus.status === "promoted") {
          skippedPromoted++;
          continue;
        }
        let bucketId = bucketStatus.bucketItemId;
        if (!bucketId) {
          const body = await dispatchAdminOp("ui_component_bucket", "create", {
            componentKey: entry.componentKey,
            sourcePath: entry.sourcePath,
            componentKind: entry.componentKind,
            metadataJson: "{}",
          });
          if (dispatchOpFailed(body)) {
            setErrors(body?.errors ?? [{ code: "BUCKET_CREATE_FAILED", message: `${key} の登録に失敗しました。` }]);
            setStatus("部品の登録に失敗しました。");
            return;
          }
          bucketId = body?.emission?.data?.bucketItemId as string | undefined;
          if (!bucketId) {
            setErrors([{ code: "BUCKET_CREATE_FAILED", message: `${key} の bucketItemId が取得できませんでした。` }]);
            setStatus("部品の登録に失敗しました。");
            return;
          }
        }
        bucketItemIds.push(bucketId);
      }

      if (bucketItemIds.length === 0) {
        if (skippedPromoted > 0) {
          setStatus("選択した部品は既に配置可能です。「配置」タブでパッケージを選択してください。");
        } else {
          setStatus("パッケージ化できる部品が見つかりませんでした。");
        }
        return;
      }

      // Step 2: single promote_package call — 1 route = 1 package with all selected components
      const promBody = await dispatchAdminOp("package_generator", "promote_package", {
        routeKey: effectiveRouteKey,
        bucketItemIds,
      });
      if (dispatchOpFailed(promBody)) {
        setErrors(promBody?.errors ?? [{ code: "PROMOTE_FAILED", message: "パッケージ化に失敗しました。" }]);
        setStatus(promBody?.errors?.[0]?.message ?? "パッケージ化に失敗しました。");
        return;
      }
      const data = promBody?.emission?.data as Record<string, unknown> | undefined;
      const packageId = typeof data?.packageId === "string" ? data.packageId : null;
      const layoutId = typeof data?.layoutId === "string" ? data.layoutId : null;
      const routeKey = typeof data?.routeKey === "string" ? data.routeKey : effectiveRouteKey;
      if (!packageId || !layoutId) {
        setStatus("パッケージ化結果を取得できませんでした。");
        return;
      }
      const lastHandoff: PackagedHandoff = { packageId, routeKey, layoutId };

      const newCount = bucketItemIds.length;
      const skipMsg = skippedPromoted > 0 ? `（${skippedPromoted} 件は既配置のためスキップ）` : "";
      setStatus(`${newCount} 件のパッケージ化が完了しました。「配置」タブで編集を続けます。${skipMsg}`);
      await loadBucket();
      const paletteBody = await dispatchAdminOp("ui_topology", "promoted_palette");
      const promoted = paletteBody?.emission?.data;
      if (Array.isArray(promoted)) {
        setPromotedKeys(new Set(promoted.map((p: PromotedPaletteEntry) => p.componentKey)));
      }
      onPackaged?.(lastHandoff);
      // Canvas workspace is always visible — no tab navigation needed after packaging.
    } catch (e) {
      setStatus(`エラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateFromCatalog = async () => {
    if (!selectedCatalog) {
      setStatus("カタログから部品を選択してください。");
      return;
    }
    const existing = resolveBucketStatus(selectedCatalog.componentKey, items, promotedKeys);
    if (existing.status === "promoted" || existing.status === "bucketed" || existing.status === "packaging") {
      setStatus(`既に登録済みです（${existing.label}）。重複登録はできません。`);
      return;
    }
    setLoading(true);
    setStatus(null);
    setErrors([]);
    try {
      const body = await dispatchAdminOp("ui_component_bucket", "create", {
        componentKey: selectedCatalog.componentKey,
        sourcePath: selectedCatalog.sourcePath,
        componentKind: selectedCatalog.componentKind,
        metadataJson: "{}",
      });
      if (body?.emission?.data?.bucketItemId) {
        setStatus(`${selectedCatalog.componentKey} を登録しました`);
        setSelectedId(body.emission.data.bucketItemId);
        await loadBucket();
      } else {
        setErrors(body?.errors?.length ? body.errors : [{ code: "BUCKET_CREATE_FAILED", message: "部品の登録に失敗しました。" }]);
        setStatus("部品の登録に失敗しました。");
      }
    } finally {
      setLoading(false);
    }
  };

  const handleCreateManual = async (componentKey: string, sourcePath: string, componentKind: string, metadataJson: string) => {
    if (!componentKey || !sourcePath || !componentKind) {
      setStatus("部品キー / sourcePath / 部品種別 は必須です。");
      return;
    }
    setLoading(true);
    setStatus(null);
    setErrors([]);
    try {
      const body = await dispatchAdminOp("ui_component_bucket", "create", {
        componentKey,
        sourcePath,
        componentKind,
        metadataJson,
      });
      if (body?.emission?.data?.bucketItemId) {
        setStatus(`${componentKey} を登録しました`);
        setSelectedId(body.emission.data.bucketItemId);
        await loadBucket();
      } else {
        setErrors(body?.errors ?? []);
        setStatus("部品の登録に失敗しました。");
      }
    } finally {
      setLoading(false);
    }
  };

  const handleGenerate = async () => {
    if (!selectedId || !effectiveRouteKey) {
      setStatus("部品とページルートを選択してください。");
      return;
    }
    setLoading(true);
    setStatus(null);
    setErrors([]);
    try {
      const body = await dispatchAdminOp("package_generator", "generate", {
        bucketItemId: selectedId,
        routeKey: effectiveRouteKey,
      });
      if (body?.success || body?.emission?.data?.ok) {
        setStatus(`${selectedCatalog?.componentKey ?? selectedId} のパッケージ化が完了しました`);
        await loadBucket();
      } else {
        setErrors(body?.errors?.length ? body.errors : [{ code: "GENERATE_FAILED", message: "パッケージ化に失敗しました。" }]);
        setStatus("パッケージ化に失敗しました。");
      }
    } catch (e) {
      setStatus(`エラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handlePromote = async () => {
    if (!selectedId || !effectiveRouteKey) return;
    setLoading(true);
    setStatus(null);
    setErrors([]);
    try {
      const body = await dispatchAdminOp("package_generator", "promote", {
        bucketItemId: selectedId,
        routeKey: effectiveRouteKey,
      });
      if (body?.success || body?.emission?.data?.ok) {
        setStatus(`${selectedItem?.componentKey ?? selectedId} が配置可能になりました（ルート: ${effectiveRouteKey}）`);
        await loadBucket();
        const { candidates } = await loadLayoutCandidatesFromBackend();
        setLayoutCandidates(candidates);
      } else {
        setErrors(body?.errors?.length ? body.errors : [{ code: "PROMOTE_FAILED", message: "配置可能化に失敗しました。" }]);
        setStatus("配置可能化に失敗しました。");
      }
    } finally {
      setLoading(false);
    }
  };

  const selectedItem = items.find((i) => i.bucketItemId === selectedId);
  const hasCatalogSelection = selectedCatalogKeys.size > 0;

  return (
    <div>
      <p class="text-muted mb-3">
        部品を複数選択し、1 回の操作でパッケージ化します（編集ルートはパッケージのみ）。
      </p>

      {candidateErrors.length > 0 && (
        <ValidationErrorPanel errors={candidateErrors} title="候補ロードエラー" />
      )}

      <Accordion title="部品選択でパッケージ化" defaultOpen={true}>
        <div class="mb-2 flex flex-wrap gap-2">
          <input
            value={catalogFilter}
            onInput={(e) => setCatalogFilter((e.target as HTMLInputElement).value)}
            placeholder="部品名で検索"
            class="input-mono flex-1 text-xs"
          />
          <select value={kindFilter} onChange={(e) => setKindFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
            <option value="">種別（すべて）</option>
            {catalogKinds.map((k) => <option key={k} value={k}>{k}</option>)}
          </select>
          <select value={lifecycleFilter} onChange={(e) => setLifecycleFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
            <option value="">状態（すべて）</option>
            <option value="code_only_drift">未登録（コードのみ）</option>
          </select>
          <button onClick={loadBucket} disabled={loading} class="btn-secondary">一覧を再読み込み</button>
        </div>

        <div class="table-wrap max-h-64 overflow-y-auto">
          <table class="table font-mono text-xs">
            <thead>
              <tr>
                {["選択", "部品名", "種別", "ステータス"].map((h) => (
                  <th key={h}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {filteredCatalog.map((c) => {
                const st = resolveBucketStatus(c.componentKey, items, promotedKeys);
                return (
                  <tr
                    key={c.componentKey}
                    class={selectedCatalogKey === c.componentKey ? "bg-blue-50" : ""}
                  >
                    <td>
                      <input
                        type="checkbox"
                        checked={selectedCatalogKeys.has(c.componentKey)}
                        onChange={() => toggleCatalogKey(c.componentKey)}
                      />
                    </td>
                    <td><code>{c.componentKey}</code></td>
                    <td>{c.componentKind}</td>
                    <td><StatusBadge text={st.label} variant={st.variant} /></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        <BucketPackageRouteFields
          routeKey={routeKey}
          manualRouteKey={manualRouteKey}
          routeOptions={routeOptions}
          candidateErrors={candidateErrors}
          onRouteKeyChange={setRouteKey}
          onManualRouteKeyChange={setManualRouteKey}
        />

        {hasCatalogSelection && (
          <div class="mt-2 rounded border border-gray-200 bg-gray-50 p-2 text-sm">
            <strong>選択中:</strong>{" "}
            {[...selectedCatalogKeys].map((k) => (
              <code key={k} class="mr-2">{k}</code>
            ))}
            {selectedCatalog && (
              <span class="text-muted-xs">{selectedCatalog.componentKind}</span>
            )}
            <button
              type="button"
              onClick={handlePackageSelected}
              disabled={loading || !effectiveRouteKey}
              class="btn-primary mt-2"
            >
              選択した部品をパッケージ化
            </button>
            {!effectiveRouteKey && (
              <p class="mt-1 text-xs text-amber-800">
                上のページルートを選択または入力するとボタンが有効になります。
              </p>
            )}
            {selectedCatalog && (
              <details class="mt-1">
                <summary class="cursor-pointer text-xs text-gray-400 hover:text-gray-600">技術詳細</summary>
                <div class="mt-0.5 font-mono text-xs text-gray-500">{selectedCatalog.sourcePath}</div>
              </details>
            )}
          </div>
        )}

        <AdvancedManualOverride title="詳細設定 — カタログ外から直接登録">
          <ManualBucketCreateForm onCreate={handleCreateManual} loading={loading} />
        </AdvancedManualOverride>
      </Accordion>

      {(items.length > 0 || selectedId) && (
        <Accordion title="詳細 — 登録済み部品の個別操作" defaultOpen={false}>
          <div class="table-wrap mb-3">
            <table class="table font-mono text-sm">
              <thead>
                <tr>
                  {["選択", "部品名", "種別", "状態"].map((h) => (
                    <th key={h}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {items.filter((i) => i.status !== "promoted").map((item) => (
                  <tr key={item.bucketItemId} class={selectedId === item.bucketItemId ? "bg-blue-50" : ""}>
                    <td>
                      <input
                        type="radio"
                        name="bucketItem"
                        checked={selectedId === item.bucketItemId}
                        onChange={() => setSelectedId(item.bucketItemId)}
                      />
                    </td>
                    <td><code>{item.componentKey}</code></td>
                    <td>{item.componentKind}</td>
                    <td>
                      {(() => {
                        const st = resolveBucketStatus(item.componentKey, items, promotedKeys);
                        return <StatusBadge text={st.label} variant={st.variant} />;
                      })()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <BucketPackageRouteFields
            routeKey={routeKey}
            manualRouteKey={manualRouteKey}
            routeOptions={routeOptions}
            candidateErrors={candidateErrors}
            onRouteKeyChange={setRouteKey}
            onManualRouteKeyChange={setManualRouteKey}
          />

          {selectedItem && effectiveRouteKey && (
            <p class="text-muted-xs mt-2">
              次: <code>{friendlyComponentLabel(selectedItem.componentKey)}</code> を <code>{effectiveRouteKey}</code> へ
              {selectedItem.status === "bucketed" ? " パッケージ化 → 配置可能化" : " 配置可能化"}
            </p>
          )}

          <div class="mt-2 flex flex-wrap gap-2">
            <button
              onClick={handleGenerate}
              disabled={loading || !selectedId || !effectiveRouteKey}
              class="btn-primary"
            >
              パッケージ化（単体）
            </button>
            <button
              onClick={handlePromote}
              disabled={loading || !selectedId || !effectiveRouteKey}
              class="btn-success"
            >
              配置可能化（単体・詳細）
            </button>
          </div>
          <AdminActionHint>
            パッケージ化する: 部品をシステムに正式登録し、レイアウトで使えるようにします。配置可能にする: 部品を配置可能状態へ昇格します。
          </AdminActionHint>
        </Accordion>
      )}

      {loading && <p class="text-muted font-mono text-sm">処理中...</p>}
      {status && (
        <p class={`text-sm font-bold ${errors.length > 0 ? "text-red-600" : "text-green-700"}`}>
          {status}
        </p>
      )}
      {errors.length > 0 && <ActionableValidationErrorPanel errors={errors} title="操作エラー" onNavigate={onNavigate} />}
      <ConfirmDialogHost />
    </div>
  );
}

function ManualBucketCreateForm({
  onCreate,
  loading,
}: {
  onCreate: (
    componentKey: string,
    sourcePath: string,
    componentKind: string,
    metadataJson: string,
  ) => void;
  loading: boolean;
}): JSX.Element {
  const [componentKey, setComponentKey] = useState("");
  const [sourcePath, setSourcePath] = useState("");
  const [componentKind, setComponentKind] = useState("primitive");
  const [labelValueRows, setLabelValueRows] = useState<
    Array<LabelValueEditorRow & { rowId: string }>
  >([]);
  const [editorError, setEditorError] = useState<string | null>(null);

  const addLabelValueRow = () => {
    setLabelValueRows((rows) => [
      ...rows,
      { ...createEmptyLabelValueEditorRow(), rowId: crypto.randomUUID() },
    ]);
    setEditorError(null);
  };
  const updateLabelValueRow = (
    rowId: string,
    patch: Partial<LabelValueEditorRow>,
  ) => {
    setLabelValueRows((rows) =>
      rows.map((row) => row.rowId === rowId ? { ...row, ...patch } : row)
    );
    setEditorError(null);
  };
  const removeLabelValueRow = (rowId: string) => {
    setLabelValueRows((rows) => rows.filter((row) => row.rowId !== rowId));
    setEditorError(null);
  };
  const submit = () => {
    try {
      const metadataJson = serializeLabelValueMetadataJson(labelValueRows);
      setEditorError(null);
      onCreate(componentKey, sourcePath, componentKind, metadataJson);
    } catch (error) {
      setEditorError(error instanceof Error ? error.message : String(error));
    }
  };

  const handleKeyInput = (e: Event) =>
    setComponentKey((e.target as HTMLInputElement).value);
  const handleSourcePathInput = (e: Event) =>
    setSourcePath((e.target as HTMLInputElement).value);
  const handleKindInput = (e: Event) =>
    setComponentKind((e.target as HTMLInputElement).value);

  return (
    <div>
      <div class="flex flex-wrap gap-2">
        <input
          value={componentKey}
          onInput={handleKeyInput}
          placeholder="部品キー"
          class="input-mono w-auto text-xs"
        />
        <input
          value={sourcePath}
          onInput={handleSourcePathInput}
          placeholder="sourcePath"
          class="input-mono flex-1 text-xs"
        />
        <input
          value={componentKind}
          onInput={handleKindInput}
          placeholder="部品種別"
          class="input-mono w-auto text-xs"
        />
      </div>

      <div class="mt-3 rounded border border-gray-200 bg-gray-50 p-2">
        <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
          <div>
            <strong class="text-sm">JSONB label/value fields</strong>
            <p class="text-muted-xs m-0">
              DocumentCanvas 用の <code>label(key名): value</code>{" "}
              ドラフト。未追加なら従来どおり空 metadata を送信します。
            </p>
          </div>
          <button
            type="button"
            onClick={addLabelValueRow}
            disabled={loading}
            class="btn-secondary text-xs"
          >
            + 追加
          </button>
        </div>

        {labelValueRows.length > 0 && (
          <div class="overflow-x-auto">
            <table class="table min-w-[920px] font-mono text-xs">
              <thead>
                <tr>
                  {[
                    "key *",
                    "label",
                    "value",
                    "jsonPath",
                    "x",
                    "y",
                    "displayPolicy",
                    "",
                  ].map((heading) => <th key={heading}>{heading}</th>)}
                </tr>
              </thead>
              <tbody>
                {labelValueRows.map((row) => (
                  <tr key={row.rowId}>
                    <td>
                      <input
                        value={row.key}
                        onInput={(e) =>
                          updateLabelValueRow(row.rowId, {
                            key: (e.target as HTMLInputElement).value,
                          })}
                        placeholder="company_name"
                        class="input-mono w-32 text-xs"
                      />
                    </td>
                    <td>
                      <input
                        value={row.label}
                        onInput={(e) =>
                          updateLabelValueRow(row.rowId, {
                            label: (e.target as HTMLInputElement).value,
                          })}
                        placeholder="会社名"
                        class="input w-28 text-xs"
                      />
                    </td>
                    <td>
                      <input
                        value={row.value}
                        onInput={(e) =>
                          updateLabelValueRow(row.rowId, {
                            value: (e.target as HTMLInputElement).value,
                          })}
                        placeholder="株式会社テスト"
                        class="input w-36 text-xs"
                      />
                    </td>
                    <td>
                      <input
                        value={row.jsonPath}
                        onInput={(e) =>
                          updateLabelValueRow(row.rowId, {
                            jsonPath: (e.target as HTMLInputElement).value,
                          })}
                        placeholder={`$.${row.key || "key"}`}
                        class="input-mono w-32 text-xs"
                      />
                    </td>
                    <td>
                      <input
                        type="number"
                        value={row.x}
                        onInput={(e) =>
                          updateLabelValueRow(row.rowId, {
                            x: Number((e.target as HTMLInputElement).value),
                          })}
                        class="input-mono w-16 text-xs"
                      />
                    </td>
                    <td>
                      <input
                        type="number"
                        value={row.y}
                        onInput={(e) =>
                          updateLabelValueRow(row.rowId, {
                            y: Number((e.target as HTMLInputElement).value),
                          })}
                        class="input-mono w-16 text-xs"
                      />
                    </td>
                    <td>
                      <select
                        value={row.displayPolicy}
                        onChange={(e) =>
                          updateLabelValueRow(row.rowId, {
                            displayPolicy: (e.target as HTMLSelectElement)
                              .value as LabelValueEditorRow["displayPolicy"],
                          })}
                        class="input w-32 text-xs"
                      >
                        {LABEL_VALUE_DISPLAY_POLICIES.map((policy) => (
                          <option key={policy} value={policy}>{policy}</option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <button
                        type="button"
                        onClick={() => removeLabelValueRow(row.rowId)}
                        disabled={loading}
                        class="btn-danger px-2 py-1 text-xs"
                      >
                        削除
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {editorError && (
          <p class="mt-2 text-sm font-bold text-red-600">{editorError}</p>
        )}
      </div>

      <button
        type="button"
        onClick={submit}
        disabled={loading}
        class="btn-secondary mt-2 text-xs"
      >
        手動作成
      </button>
    </div>
  );
}

// ─── CSS トークンセレクターセクション ────────────────────────────────────────

function CssTokenSelectorSection(): JSX.Element {
  const [selectedTokenRefs, setSelectedTokenRefs] = useState<string[]>([]);
  const toggleTokenRef = (tokenKey: string) => {
    setSelectedTokenRefs((prev) =>
      prev.includes(tokenKey) ? prev.filter((k) => k !== tokenKey) : [...prev, tokenKey]
    );
  };
  return (
    <div>
      <p class="text-muted mb-3">
        見た目の設定（色・余白・フォント）を選択できます。選んだ設定はレイアウト保存時に適用されます。
      </p>
      <CssTokenPicker selectedTokenRefs={selectedTokenRefs} onToggle={toggleTokenRef} />
    </div>
  );
}

// ─── v2 ビジュアルキャンバスコンポーネント ────────────────────────────────────

// Outer hit area: 24×24px centered on the handle position (WCAG 2.5.8 minimum target size)
const RESIZE_HANDLE_STYLE: Record<ResizeDir, Record<string, string>> = {
  nw: { top: "-12px", left: "-12px", cursor: "nw-resize" },
  n: { top: "-12px", left: "50%", transform: "translateX(-50%)", cursor: "n-resize" },
  ne: { top: "-12px", right: "-12px", cursor: "ne-resize" },
  w: { top: "50%", left: "-12px", transform: "translateY(-50%)", cursor: "w-resize" },
  e: { top: "50%", right: "-12px", transform: "translateY(-50%)", cursor: "e-resize" },
  sw: { bottom: "-12px", left: "-12px", cursor: "sw-resize" },
  s: { bottom: "-12px", left: "50%", transform: "translateX(-50%)", cursor: "s-resize" },
  se: { bottom: "-12px", right: "-12px", cursor: "se-resize" },
};

const RESIZE_DIR_LABEL: Record<ResizeDir, string> = {
  nw: "左上リサイズ", n: "上リサイズ", ne: "右上リサイズ",
  w: "左リサイズ", e: "右リサイズ",
  sw: "左下リサイズ", s: "下リサイズ", se: "右下リサイズ",
};

// Gap 6: Accessible resize handle — 24×24px touch target (WCAG 2.5.8) with 12×12px visual dot
function ResizeHandle({
  dir,
  onMouseDown,
  onKeyboardActivate,
}: {
  dir: ResizeDir;
  onMouseDown: (e: Event) => void;
  onKeyboardActivate: () => void;
}): JSX.Element {
  return (
    <div
      role="button"
      tabIndex={0}
      aria-label={RESIZE_DIR_LABEL[dir]}
      class="absolute z-20 flex h-6 w-6 items-center justify-center focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-400 focus-visible:ring-offset-1"
      style={RESIZE_HANDLE_STYLE[dir]}
      onMouseDown={(e: Event) => { e.stopPropagation(); onMouseDown(e); }}
      onKeyDown={(e: KeyboardEvent) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          e.stopPropagation();
          onKeyboardActivate();
        }
      }}
    >
      <span class="block h-3 w-3 rounded-sm border-2 border-blue-600 bg-white shadow-sm pointer-events-none" />
    </div>
  );
}

// Gap 4/6: Friendly component display name (hide raw key detail unless needed)
function friendlyComponentLabel(componentKey: string): string {
  const parts = componentKey.split("/");
  return parts[parts.length - 1] ?? componentKey;
}

function friendlyNodeLabel(node: Pick<DraftNode, "componentKey" | "nodeKind" | "htmlTag">): string {
  if (node.nodeKind === "structural_html" && node.htmlTag) return `<${node.htmlTag}>`;
  return friendlyComponentLabel(node.componentKey);
}

/** Read-only live component preview inside a layout manipulation frame. */
function LayoutComponentPreviewPane({ node }: { node: DraftNode }): JSX.Element {
  return (
    <LayoutPreviewNodeFrame
      componentKey={node.componentKey}
      componentKind={node.componentKind}
      componentId={node.componentId}
      isDraftOnly={node.isDraftOnly}
    />
  );
}

function VisualLayoutNode({
  node,
  isSelected,
  isDragging,
  displayX,
  displayY,
  displayW,
  displayH,
  wrapperPreviewClassName = "",
  onSelect,
  onNodeMouseDown,
  onResizeHandleMouseDown,
  onKeyboardMove,
  onKeyboardResize,
  onDelete,
}: {
  node: DraftNode;
  isSelected: boolean;
  isDragging: boolean;
  displayX: number;
  displayY: number;
  displayW: number;
  displayH: number;
  wrapperPreviewClassName?: string;
  onSelect: () => void;
  onNodeMouseDown: (e: Event) => void;
  onResizeHandleMouseDown: (e: Event, dir: ResizeDir) => void;
  onKeyboardMove?: (dx: number, dy: number) => void;
  onKeyboardResize?: (dir: ResizeDir) => void;
  onDelete?: () => void;
}): JSX.Element {
  const STEP = 10;
  const BIG_STEP = 50;

  return (
    <div
      role="button"
      tabIndex={0}
      aria-selected={isSelected}
      aria-label={`${friendlyComponentLabel(node.componentKey)}${node.isDraftOnly ? " (ドラフト)" : ""}${node.slotKey ? ` スロット:${node.slotKey}` : ""} 位置(${displayX},${displayY}) サイズ(${displayW}×${displayH})`}
      class={`absolute select-none rounded border-2 text-sm transition-shadow ${
        isSelected
          ? "border-blue-600 shadow-lg ring-2 ring-blue-300 ring-offset-1"
          : node.isDraftOnly
          ? "border-yellow-300 hover:border-yellow-500"
          : "border-blue-200 hover:border-blue-400"
      } ${node.isDraftOnly ? "bg-yellow-50" : "bg-white"} ${wrapperPreviewClassName} focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500`}
      style={{
        left: `${displayX}px`,
        top: `${displayY}px`,
        width: `${displayW}px`,
        height: `${displayH}px`,
        zIndex: isSelected ? 10 : 1,
        cursor: isDragging ? "grabbing" : "grab",
        opacity: isDragging ? 0.75 : 1,
      }}
      onClick={(e: Event) => { (e as MouseEvent).stopPropagation(); }}
      onMouseDown={onNodeMouseDown}
      onKeyDown={(e: KeyboardEvent) => {
        if (e.key === "Enter" || e.key === " ") { e.preventDefault(); onSelect(); return; }
        if (e.key === "Delete" || e.key === "Backspace") { e.preventDefault(); onDelete?.(); return; }
        if (e.key === "Escape") { e.preventDefault(); (e.currentTarget as HTMLElement).blur(); return; }
        const step = e.shiftKey ? BIG_STEP : STEP;
        if (e.key === "ArrowLeft") { e.preventDefault(); onKeyboardMove?.(-step, 0); }
        if (e.key === "ArrowRight") { e.preventDefault(); onKeyboardMove?.(step, 0); }
        if (e.key === "ArrowUp") { e.preventDefault(); onKeyboardMove?.(0, -step); }
        if (e.key === "ArrowDown") { e.preventDefault(); onKeyboardMove?.(0, step); }
      }}
    >
      <div
        class="pointer-events-none flex h-full min-h-0 flex-col overflow-hidden"
        aria-hidden="false"
      >
        <div class="min-h-0 flex-1 overflow-hidden p-0.5">
          <LayoutComponentPreviewPane node={node} />
        </div>
        {(node.slotKey || node.parentNodeId) && (
          <div class="shrink-0 border-t border-slate-100 bg-white/80 px-1 py-0.5 font-mono text-[0.48rem] text-slate-400">
            {node.slotKey && <span>slot:{node.slotKey} </span>}
            {node.parentNodeId && (
              <span>parent:{node.parentNodeId.slice(0, 8)}…</span>
            )}
          </div>
        )}
      </div>
      <div
        class="pointer-events-none absolute right-0.5 top-0.5 rounded bg-white/90 px-1 font-mono text-[0.48rem] text-slate-300 shadow-sm"
        aria-hidden="true"
      >
        {displayW}×{displayH}
      </div>
      {isSelected && !isDragging && (
        <>
          {(["nw", "n", "ne", "w", "e", "sw", "s", "se"] as ResizeDir[]).map((dir) => (
            <ResizeHandle
              key={dir}
              dir={dir}
              onMouseDown={(e) => onResizeHandleMouseDown(e, dir)}
              onKeyboardActivate={() => onKeyboardResize?.(dir)}
            />
          ))}
        </>
      )}
    </div>
  );
}

function VisualLayoutCanvas({
  draftNodes,
  selectedNodeId,
  layoutClassRefs,
  canvasRef,
  liveDragNodeId,
  liveResizeNodeId,
  getLivePos,
  showGrid,
  onSelectNode,
  onDeselectAll,
  onNodeMouseDown,
  onResizeHandleMouseDown,
  onCanvasMouseMove,
  onCanvasMouseUp,
  onDragOver,
  onDrop,
  onKeyboardMoveNode,
  onKeyboardResizeNode,
  onDeleteNode,
  onAddFromEmptyState,
  allowEmptyStateTemplates = true,
}: {
  draftNodes: DraftNode[];
  selectedNodeId: string | null;
  layoutClassRefs: string[];
  // deno-lint-ignore no-explicit-any
  canvasRef: { current: any };
  liveDragNodeId: string | null;
  liveResizeNodeId: string | null;
  getLivePos: (nodeId: string) => { x: number; y: number; width: number; height: number } | null;
  showGrid: boolean;
  onSelectNode: (nodeId: string) => void;
  onDeselectAll: () => void;
  onNodeMouseDown: (e: Event, nodeId: string) => void;
  onResizeHandleMouseDown: (e: Event, nodeId: string, dir: ResizeDir) => void;
  onCanvasMouseMove: (e: Event) => void;
  onCanvasMouseUp: () => void;
  onDragOver: (e: Event) => void;
  onDrop: (e: Event) => void;
  onKeyboardMoveNode: (nodeId: string, dx: number, dy: number) => void;
  onKeyboardResizeNode: (nodeId: string, dir: ResizeDir) => void;
  onDeleteNode: (nodeId: string) => void;
  onAddFromEmptyState?: (templateId: string) => void;
  /** false のとき空キャンバスのクイックスタート（ドラフト部品混入）を非表示 */
  allowEmptyStateTemplates?: boolean;
}): JSX.Element {
  const gridStyle = showGrid
    ? {
        backgroundImage:
          `linear-gradient(to right,#e5e7eb 1px,transparent 1px),` +
          `linear-gradient(to bottom,#e5e7eb 1px,transparent 1px)`,
        backgroundSize: `${SNAP_SIZE * 4}px ${SNAP_SIZE * 4}px`,
      }
    : {};

  return (
    <div
      ref={canvasRef}
      role="application"
      aria-label="レイアウトキャンバス — クリックで選択。0.3秒長押しまたはドラッグで移動。キーボード: 矢印キーで移動、Delete で削除、Tab でノードを切り替え"
      class="relative overflow-auto rounded-lg border-2 border-dashed border-gray-300 bg-white focus-within:border-blue-300"
      style={{ minHeight: `${CANVAS_MIN_HEIGHT}px`, ...gridStyle }}
      onClick={onDeselectAll}
      onMouseMove={onCanvasMouseMove}
      onMouseUp={onCanvasMouseUp}
      onMouseLeave={onCanvasMouseUp}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      {/* Gap 7: First-run empty state with quick-start actions */}
      {draftNodes.length === 0 && (
        <div class="pointer-events-auto absolute inset-0 flex flex-col items-center justify-center gap-4 p-6 text-center">
          <div class="text-4xl text-gray-200" aria-hidden="true">☐</div>
          <div>
            <p class="text-base font-semibold text-gray-500">layout draft が空です</p>
            <p class="mt-1 text-sm text-gray-400">
              {allowEmptyStateTemplates
                ? "左のパレットで部品を追加すると、ここに layout draft のリアルタイムプレビューが表示されます。追加後にドラッグ・リサイズで位置を調整できます。"
                : "左のパレット（パッケージ内の配置可能部品）で「追加」すると layout draft のプレビューが表示されます。"}
            </p>
          </div>
          {onAddFromEmptyState && allowEmptyStateTemplates && (
            <div class="flex flex-wrap justify-center gap-2">
              <button
                type="button"
                onClick={() => onAddFromEmptyState("starter_header_main")}
                class="rounded-lg border border-blue-200 bg-blue-50 px-4 py-2 text-sm font-medium text-blue-700 hover:bg-blue-100 focus-visible:ring-2 focus-visible:ring-blue-500"
              >
                ヘッダー + メイン を追加
              </button>
              <button
                type="button"
                onClick={() => onAddFromEmptyState("starter_card")}
                class="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-blue-500"
              >
                カードを追加
              </button>
              <button
                type="button"
                onClick={() => onAddFromEmptyState("starter_form")}
                class="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-blue-500"
              >
                フォームを追加
              </button>
            </div>
          )}
        </div>
      )}
      {draftNodes.map((node) => {
        const live = getLivePos(node.nodeId);
        const isSelected = node.nodeId === selectedNodeId;
        const wrapperPreviewClassName = resolveNodeWrapperPreviewClassName(
          layoutClassRefs,
          isSelected,
        );
        return (
          <VisualLayoutNode
            key={node.nodeId}
            node={node}
            isSelected={isSelected}
            isDragging={liveDragNodeId === node.nodeId}
            displayX={live?.x ?? node.x}
            displayY={live?.y ?? node.y}
            displayW={live?.width ?? node.width}
            displayH={live?.height ?? node.height}
            wrapperPreviewClassName={wrapperPreviewClassName}
            onSelect={() => onSelectNode(node.nodeId)}
            onNodeMouseDown={(e) => onNodeMouseDown(e, node.nodeId)}
            onResizeHandleMouseDown={(e, dir) => onResizeHandleMouseDown(e, node.nodeId, dir)}
            onKeyboardMove={(dx, dy) => onKeyboardMoveNode(node.nodeId, dx, dy)}
            onKeyboardResize={(dir) => onKeyboardResizeNode(node.nodeId, dir)}
            onDelete={() => onDeleteNode(node.nodeId)}
          />
        );
      })}
    </div>
  );
}

function LayerTree({
  draftNodes,
  selectedNodeId,
  onSelect,
  onMoveUp,
  onMoveDown,
  onCopy,
  onDelete,
}: {
  draftNodes: DraftNode[];
  selectedNodeId: string | null;
  onSelect: (nodeId: string) => void;
  onMoveUp: (nodeId: string) => void;
  onMoveDown: (nodeId: string) => void;
  onCopy: (nodeId: string) => void;
  onDelete: (nodeId: string) => void;
}): JSX.Element {
  return (
    <div class="w-44 shrink-0 rounded-lg border border-gray-200 bg-white">
      <div class="border-b border-gray-200 px-2 py-1.5">
        <h4 class="text-xs font-semibold text-gray-600">
          レイヤー ({draftNodes.length})
        </h4>
      </div>
      <div
        role="listbox"
        aria-label="レイヤー一覧"
        class="overflow-y-auto"
        style="max-height:300px;"
      >
        {draftNodes.length === 0 && (
          <p class="px-2 py-4 text-center text-xs text-gray-400">なし</p>
        )}
        {[...draftNodes].reverse().map((node, reversedIdx) => {
          const origIdx = draftNodes.length - 1 - reversedIdx;
          const isSelected = node.nodeId === selectedNodeId;
          return (
            <div
              key={node.nodeId}
              role="option"
              aria-selected={isSelected}
              tabIndex={0}
              onClick={() => onSelect(node.nodeId)}
              onKeyDown={(e: KeyboardEvent) => {
                if (e.key === "Enter" || e.key === " ") { e.preventDefault(); onSelect(node.nodeId); }
                if (e.key === "Delete") { e.preventDefault(); onDelete(node.nodeId); }
              }}
              class={`flex cursor-pointer items-center gap-1 border-b border-gray-100 px-1.5 py-1 text-xs focus:outline-none focus-visible:ring-1 focus-visible:ring-blue-400 ${
                isSelected ? "bg-blue-50" : "hover:bg-gray-50"
              }`}
            >
              <span
                class={`h-2 w-2 shrink-0 rounded-sm ${
                  node.isDraftOnly ? "bg-yellow-400" : "bg-blue-400"
                }`}
                aria-hidden="true"
              />
              <span class="flex-1 truncate font-mono" title={node.componentKey}>
                {friendlyNodeLabel(node)}
              </span>
              <div class="flex shrink-0 gap-0.5">
                <button
                  type="button"
                  onClick={(e: Event) => { e.stopPropagation(); onCopy(node.nodeId); }}
                  class="rounded px-0.5 text-[0.65rem] text-gray-400 hover:text-gray-600 focus-visible:ring-1 focus-visible:ring-blue-400"
                  title="コピー"
                  aria-label={`${friendlyNodeLabel(node)}をコピー`}
                >⧉</button>
                <button
                  type="button"
                  onClick={(e: Event) => { e.stopPropagation(); onMoveUp(node.nodeId); }}
                  disabled={origIdx === draftNodes.length - 1}
                  class="rounded px-0.5 text-[0.65rem] text-gray-400 hover:text-gray-600 disabled:opacity-30 focus-visible:ring-1 focus-visible:ring-blue-400"
                  title="前面へ"
                  aria-label={`${friendlyComponentLabel(node.componentKey)}を前面へ`}
                >▲</button>
                <button
                  type="button"
                  onClick={(e: Event) => { e.stopPropagation(); onMoveDown(node.nodeId); }}
                  disabled={origIdx === 0}
                  class="rounded px-0.5 text-[0.65rem] text-gray-400 hover:text-gray-600 disabled:opacity-30 focus-visible:ring-1 focus-visible:ring-blue-400"
                  title="背面へ"
                  aria-label={`${friendlyComponentLabel(node.componentKey)}を背面へ`}
                >▼</button>
                <button
                  type="button"
                  onClick={(e: Event) => { e.stopPropagation(); onDelete(node.nodeId); }}
                  class="rounded px-0.5 text-[0.65rem] text-red-400 hover:text-red-600 focus-visible:ring-1 focus-visible:ring-red-400"
                  title="削除"
                  aria-label={`${friendlyComponentLabel(node.componentKey)}を削除`}
                >✕</button>
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
  width: "幅 (px)",
  height: "高さ (px)",
};

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
}): JSX.Element {
  const [manualSlotKey, setManualSlotKey] = useState("");
  const [parentCycleError, setParentCycleError] = useState<string | null>(null);
  const parentOptions = draftNodes.filter((n) => n.nodeId !== node.nodeId);

  const handleParentChange = (value: string) => {
    const parentId = value || null;
    if (parentId && wouldCreateParentCycle(draftNodes, node.nodeId, parentId)) {
      setParentCycleError("その親を選択すると循環参照になります。別のノードを選択してください。");
      return;
    }
    setParentCycleError(null);
    onCommit({ parentNodeId: parentId }, "親部品を変更");
  };

  // onInput → live preview (no history); onChange/blur → commit to history
  const handleNum = (
    field: "x" | "y" | "width" | "height" | "gridCol" | "gridRow",
    raw: string,
    applySnap = false,
    commit = false,
  ) => {
    const v = parseInt(raw, 10);
    if (isNaN(v)) return;
    const min = field === "width" ? 40 : field === "height" ? 30 : field === "gridCol" ? 1 : 0;
    const clamped = Math.max(min, v);
    const final = applySnap ? snapToGrid(clamped, SNAP_SIZE) : clamped;
    if (commit) {
      onCommit({ [field]: final } as Partial<DraftNode>, `${FIELD_LABELS[field] ?? field}を変更`);
    } else {
      onUpdate({ [field]: final } as Partial<DraftNode>);
    }
  };

  return (
    <div
      role="complementary"
      aria-label={`${friendlyNodeLabel(node)} のプロパティ`}
      class="w-52 shrink-0 overflow-y-auto rounded-lg border border-blue-600 bg-blue-50 p-2.5 font-mono text-xs"
      style="max-height:440px;"
    >
      <div class="mb-2 flex items-center justify-between">
        <strong class="text-sm">プロパティ</strong>
        <button
          type="button"
          onClick={onClose}
          class="btn-secondary px-1.5 py-0 text-xs"
          aria-label="プロパティパネルを閉じる"
        >✕</button>
      </div>

      {/* Gap 4: Friendly name first, technical key in details */}
      <div class="mb-3 rounded border border-blue-200 bg-white p-1.5">
        <div class="font-bold text-blue-900">{friendlyNodeLabel(node)}</div>
        {node.isDraftOnly && (
          <div class="mt-0.5 text-[0.65rem] font-medium text-yellow-700">
            ⚠ まだ使えない部品 — 先に部品登録を完了してください
          </div>
        )}
        <details class="mt-1">
          <summary class="cursor-pointer text-[0.6rem] text-gray-400">技術情報</summary>
          <code class="text-[0.6rem] text-gray-500 break-all">{node.componentKey}</code>
        </details>
      </div>

      {/* Gap 4: Friendly position/size labels */}
      <fieldset class="mb-2">
        <legend class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">位置・サイズ</legend>
        <div class="grid grid-cols-2 gap-1">
          {(["x", "y", "width", "height"] as const).map((f) => (
            <label key={f} class="flex flex-col gap-0.5">
              <span class="text-[0.65rem] text-gray-600">{FIELD_LABELS[f]}</span>
              <input
                type="number"
                value={node[f]}
                min={f === "width" ? 40 : f === "height" ? 30 : 0}
                step={SNAP_SIZE}
                onInput={(e) => handleNum(f, (e.target as HTMLInputElement).value, true, false)}
                onChange={(e) => handleNum(f, (e.target as HTMLInputElement).value, true, true)}
                class="input px-1 py-0.5"
                aria-label={FIELD_LABELS[f]}
              />
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset class="mb-2 flex flex-col gap-1.5">
        <legend class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">配置設定</legend>

        {/* Gap 4: friendly parent label instead of "parentNodeId" */}
        <label class="flex flex-col gap-0.5">
          <span class="text-[0.65rem] text-gray-600">親部品</span>
          <select
            value={node.parentNodeId ?? ""}
            onChange={(e) => handleParentChange((e.target as HTMLSelectElement).value)}
            class="input px-1 py-0.5 text-xs"
            aria-label="親部品を選択"
          >
            <option value="">(なし — トップレベル)</option>
            {parentOptions.map((n) => {
              const cyclic = wouldCreateParentCycle(draftNodes, node.nodeId, n.nodeId);
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
          <p class="m-0 rounded bg-red-50 px-1.5 py-1 text-red-600" role="alert">{parentCycleError}</p>
        )}

        {/* Gap 4: "配置スロット" instead of "slotKey" */}
        <label class="flex flex-col gap-0.5">
          <span class="text-[0.65rem] text-gray-600">配置スロット</span>
          <select
            value={node.slotKey}
            onChange={(e) => onCommit({ slotKey: (e.target as HTMLSelectElement).value }, "配置スロットを変更")}
            class="input px-1 py-0.5 text-xs"
            aria-label="配置スロットを選択"
          >
            {slotKeyCandidates.map((sk) => (
              <option key={sk || "__empty__"} value={sk}>{sk || "(デフォルト)"}</option>
            ))}
          </select>
        </label>

        <AdvancedManualOverride title="カスタムスロットを直接入力">
          <div class="flex gap-1">
            <input
              value={manualSlotKey}
              onInput={(e) => setManualSlotKey((e.target as HTMLInputElement).value)}
              placeholder="スロット名を入力"
              class="input-mono flex-1 px-1 py-0.5 text-xs"
              aria-label="カスタム配置スロット名"
            />
            <button
              type="button"
              class="btn-secondary text-xs"
              onClick={() => { onCommit({ slotKey: manualSlotKey }, "カスタムスロットを設定"); setManualSlotKey(""); }}
            >
              適用
            </button>
          </div>
        </AdvancedManualOverride>

        {/* orderIndex — layout child の表示順 */}
        <label class="flex flex-col gap-0.5">
          <span class="text-[0.65rem] text-gray-600">表示順 (orderIndex)</span>
          <input
            type="number"
            min={0}
            value={node.orderIndex}
            onInput={(e) => {
              const v = parseInt((e.target as HTMLInputElement).value, 10);
              if (!isNaN(v) && v >= 0) onCommit({ orderIndex: v }, "orderIndexを変更");
            }}
            class="input px-1 py-0.5"
            aria-label="orderIndex (表示順)"
          />
        </label>
      </fieldset>

      <fieldset class="mb-2 flex flex-col gap-1.5">
        <legend class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">layoutClassRefs（ノード単位）</legend>
        <TopologyLayoutClassPicker
          selectedClassRefs={node.layoutClassRefs ?? []}
          onToggle={onToggleLayoutClassRef}
          scopeFilter=""
          allowedForFilter="component_wrapper"
        />
      </fieldset>

      <div class="mb-2 flex flex-wrap gap-1">
        <button type="button" class="btn-secondary text-xs" onClick={onCopy}>コピー</button>
        {onEditDesign && (
          <button type="button" class="btn-secondary text-xs" onClick={onEditDesign}>
            デザインを編集
          </button>
        )}
      </div>

      {/* Gap 4: Friendly grid labels */}
      <fieldset class="flex flex-col gap-1">
        <legend class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">グリッド位置</legend>
        <label class="flex flex-col gap-0.5">
          <span class="text-[0.65rem] text-gray-600">列 (1〜12)</span>
          <input
            type="number" min={1} max={12} value={node.gridCol}
            onInput={(e) => handleNum("gridCol", (e.target as HTMLInputElement).value, false, false)}
            onChange={(e) => handleNum("gridCol", (e.target as HTMLInputElement).value, false, true)}
            class="input px-1 py-0.5"
            aria-label="グリッド列 (1〜12)"
          />
        </label>
        <label class="flex flex-col gap-0.5">
          <span class="text-[0.65rem] text-gray-600">行</span>
          <input
            type="number" min={1} value={node.gridRow}
            onInput={(e) => handleNum("gridRow", (e.target as HTMLInputElement).value, false, false)}
            onChange={(e) => handleNum("gridRow", (e.target as HTMLInputElement).value, false, true)}
            class="input px-1 py-0.5"
            aria-label="グリッド行"
          />
        </label>
      </fieldset>
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
  const [activeBreakpoint, setActiveBreakpoint] = useState<string>(RESPONSIVE_BREAKPOINTS[1]);

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
      <div class="mb-2 flex flex-wrap gap-1" role="tablist" aria-label="ブレークポイント選択">
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
                <span class={`ml-1 rounded-full px-1 text-[0.6rem] font-bold ${isActive ? "bg-blue-400 text-white" : "bg-blue-200 text-blue-800"}`}>
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
        aria-label={`${BREAKPOINT_LABELS[activeBreakpoint] ?? activeBreakpoint} のトークン設定`}
      >
        <div class="mb-1 flex items-center justify-between">
          <span class="text-xs font-medium text-gray-700">
            {BREAKPOINT_LABELS[activeBreakpoint] ?? activeBreakpoint} のトークン
            {activeTokens.length > 0 && <span class="ml-1 text-blue-600">({activeTokens.length} 件選択)</span>}
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
          <strong class="text-xs text-blue-800">設定済みブレークポイント</strong>
          <ul class="mt-1 space-y-0.5 pl-0">
            {RESPONSIVE_BREAKPOINTS.filter((bp) => (rules[bp]?.length ?? 0) > 0).map((bp) => (
              <li key={bp} class="flex items-start gap-2 text-xs">
                <code class="shrink-0 font-mono text-blue-700">{bp}:</code>
                <span class="flex-1 text-gray-600 break-all">{rules[bp]!.join(", ")}</span>
                <button
                  type="button"
                  onClick={() => clearBreakpoint(bp)}
                  class="shrink-0 text-red-400 hover:text-red-600"
                  aria-label={`${bp} をクリア`}
                >✕</button>
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
  onDragStart: (entry: PaletteEntry) => void;
  onAddToCanvas: (entry: PaletteEntry) => void;
  entries: PaletteEntry[];
  status: string | null;
  /** パッケージスコープ時はプロモート済みのみ（ドラフト catalog 非表示） */
  packageOnly?: boolean;
}): JSX.Element {
  const [filter, setFilter] = useState("");
  const scopeEntries = packageOnly ? entries.filter((e) => !e.isDraftOnly) : entries;
  const filtered = filter
    ? scopeEntries.filter((e) =>
        e.componentKey.toLowerCase().includes(filter.toLowerCase()) ||
        e.componentKind.toLowerCase().includes(filter.toLowerCase())
      )
    : scopeEntries;

  return (
    <div class="w-44 shrink-0 overflow-y-auto rounded-lg border border-gray-200 bg-gray-50 p-2 max-h-[480px]">
      <h4 class="mb-1 text-sm font-semibold">部品</h4>

      {/* Gap 5: Filter so users can find components without scrolling */}
      <input
        value={filter}
        onInput={(e) => setFilter((e.target as HTMLInputElement).value)}
        placeholder="絞り込み..."
        class="mb-1 w-full rounded border border-gray-300 px-2 py-1 text-xs focus:border-blue-400 focus:outline-none"
        aria-label="パレットの部品を絞り込み"
      />

      <p class="mb-1.5 text-[0.62rem] text-gray-500">
        「追加」で layout node として追加。canvas 上でドラッグして位置調整できます。
      </p>
      {status && <p class="text-[0.62rem] text-gray-400">{status}</p>}

      {filtered.length === 0 && (
        <p class="py-3 text-center text-[0.65rem] text-gray-400">該当なし</p>
      )}

      {filtered.map((c) => {
        const draftOnly = c.isDraftOnly;
        return (
          <div
            key={c.componentKey}
            class={`mb-1 rounded border font-mono text-xs ${
              draftOnly ? "border-yellow-300 bg-yellow-50" : "border-blue-200 bg-blue-50"
            }`}
          >
            {/* Drag target area */}
            <div
              draggable={true}
              onDragStart={() => onDragStart(c)}
              class="cursor-grab px-1.5 pt-1 pb-0.5"
              aria-label={`${friendlyComponentLabel(c.componentKey)}をドラッグ`}
              title="ドラッグしてキャンバスへ"
            >
              <div class="font-bold truncate" title={c.componentKey}>
                {friendlyComponentLabel(c.componentKey)}
                {draftOnly && (
                  <span
                    class="ml-1 font-normal text-yellow-700 text-[0.6rem]"
                    title="この部品はまだ登録されていません。部品登録タブでパッケージ化してから使用してください。"
                  >⚠ まだ使えません</span>
                )}
              </div>
              <div class="text-[0.62rem] text-gray-500">{c.componentKind}</div>
            </div>
            {/* Gap 5: Non-drag "追加" button */}
            <button
              type="button"
              onClick={() => onAddToCanvas(c)}
              class={`w-full rounded-b border-t px-1.5 py-0.5 text-[0.65rem] font-medium transition-colors focus-visible:ring-2 focus-visible:ring-blue-400 ${
                draftOnly
                  ? "border-yellow-200 text-yellow-700 hover:bg-yellow-100"
                  : "border-blue-100 text-blue-700 hover:bg-blue-100"
              }`}
              aria-label={`${friendlyComponentLabel(c.componentKey)}をキャンバスに追加`}
            >
              + 追加
            </button>
          </div>
        );
      })}
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
  return (
    <div class="w-44 shrink-0 overflow-y-auto rounded-lg border border-emerald-200 bg-emerald-50 p-2 max-h-[480px]">
      <h4 class="mb-1 text-sm font-semibold text-emerald-900">構造 HTML</h4>
      <p class="mb-1.5 text-[0.62rem] text-emerald-800">
        SSOT 許可タグを layout ノードとして追加します。
      </p>
      <div class="flex flex-col gap-1">
        {STRUCTURAL_HTML_TAG_ALLOWLIST.map((tag) => (
          <button
            key={tag}
            type="button"
            disabled={disabled}
            onClick={() => onAddTag(tag)}
            class="rounded border border-emerald-300 bg-white px-2 py-1 text-left font-mono text-xs text-emerald-900 hover:bg-emerald-100 disabled:opacity-40"
          >
            + &lt;{tag}&gt;
          </button>
        ))}
      </div>
    </div>
  );
}

// ─── レイアウトビルダーセクション v2 + UX強化 ─────────────────────────────────

function LayoutBuilderSection({
  onNavigate,
  scopedPackageId,
  scopedRouteKey,
  scopedLayoutId,
}: {
  onNavigate?: (panel: WorkspacePanel) => void;
  scopedPackageId?: string;
  scopedRouteKey?: string | null;
  scopedLayoutId?: string | null;
}): JSX.Element {
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
  const historyRef = useRef<HistorySnapshot[]>([{ nodes: [], label: "初期状態" }]);
  const historyPtrRef = useRef(0);
  const [canUndo, setCanUndo] = useState(false);
  const [canRedo, setCanRedo] = useState(false);

  // Gap 6: ARIA live region message
  const [liveAnnouncement, setLiveAnnouncement] = useState("");

  // ── v2: canvas visual interaction state ─────────────────────────────────
  const [showGrid, setShowGrid] = useState(true);
  const [liveDragPos, setLiveDragPos] = useState<{ nodeId: string; x: number; y: number } | null>(null);
  const [activeDragNodeId, setActiveDragNodeId] = useState<string | null>(null);
  const [liveResizePos, setLiveResizePos] = useState<{ nodeId: string; x: number; y: number; width: number; height: number } | null>(null);
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const dragState = useRef<CanvasDragState>(null);
  const pendingDragState = useRef<CanvasPendingDragState>(null);
  const resizeState = useRef<CanvasResizeState>(null);

  // ── layout class refs (placement only; design tokens → component design tab) ─
  const [selectedLayoutClassRefs, setSelectedLayoutClassRefs] = useState<string[]>([]);
  const [manualLayoutClassRef, setManualLayoutClassRef] = useState("");
  const [layoutClassRefError, setLayoutClassRefError] = useState<string | null>(null);

  // ── patch / status ───────────────────────────────────────────────────────
  const [patchSummary, setPatchSummary] = useState<LayoutPatchSummary | null>(null);
  const [patchErrors, setPatchErrors] = useState<{ code: string; message: string }[]>([]);
  const [debugJson, setDebugJson] = useState<string | null>(null);
  const [layoutPatchPreviewOpen, setLayoutPatchPreviewOpen] = useState(false);
  const [layoutPatchPreviewAudit, setLayoutPatchPreviewAudit] = useState<LayoutPatchPreviewAudit | null>(null);
  const [layoutPatchPreviewNodes, setLayoutPatchPreviewNodes] = useState<LayoutPreviewNodeInput[]>([]);
  const [layoutPatchPreviewClassRefs, setLayoutPatchPreviewClassRefs] = useState<string[]>([]);
  const [layoutApplyHandoffOpen, setLayoutApplyHandoffOpen] = useState(false);
  const lifecycleRef = useRef<HTMLDivElement | null>(null);
  const [loading, setLoading] = useState(false);

  // ── palette / layout candidates ──────────────────────────────────────────
  const [paletteEntries, setPaletteEntries] = useState<PaletteEntry[]>([]);
  const [paletteStatus, setPaletteStatus] = useState<string | null>(null);
  const [layoutCandidates, setLayoutCandidates] = useState<LayoutRouteCandidate[]>([]);
  const [candidateErrors, setCandidateErrors] = useState<ValidationError[]>([]);
  const [paletteLoadFailed, setPaletteLoadFailed] = useState(false);

  // ── palette drag (HTML5 drag API — palette→canvas only) ─────────────────
  const dragSrc = useRef<DragSrc | null>(null);

  // ── derived ─────────────────────────────────────────────────────────────
  const tensorPatchJson = buildVisualLayoutPatchJson(draftNodes, selectedLayoutClassRefs);
  const effectiveLayoutId = manualLayoutId.trim() || layoutId;
  const effectiveRouteKey = manualRouteKey.trim() || routeKey;
  const selectedLayout = layoutCandidates.find(
    (c) => c.layoutId === effectiveLayoutId && c.routeKey === effectiveRouteKey,
  );
  const dbSlotKeys = selectedLayout?.slotKeys ?? [];
  const slotKeyCandidates = buildSlotKeyCandidates(draftNodes, dbSlotKeys);
  const packageScopedLayout = Boolean(scopedPackageId?.trim());
  const displayCandidates =
    packageScopedLayout && scopedRouteKey && scopedLayoutId
      ? layoutCandidates.filter(
        (c) => c.routeKey === scopedRouteKey && c.layoutId === scopedLayoutId,
      )
      : layoutCandidates;
  const layoutSelectorsLocked =
    packageScopedLayout && Boolean(scopedRouteKey && scopedLayoutId);
  const selectorsDisabled =
    candidateErrors.length > 0 || paletteLoadFailed || !packageScopedLayout;
  const canPatch =
    packageScopedLayout && Boolean(effectiveLayoutId && effectiveRouteKey);

  const rejectDraftPaletteEntry = (entry: PaletteEntry): boolean => {
    if (entry.isDraftOnly) {
      announce("この部品はまだパッケージに含まれていません。部品登録パネルで配置可能化してください。");
      return true;
    }
    return false;
  };
  const selectedNode = draftNodes.find((n) => n.nodeId === selectedNodeId) ?? null;
  const canvasPreviewClass = resolveCanvasRootPreviewClassName(selectedLayoutClassRefs);
  const paletteSeedEntries: PaletteDraftSeedEntry[] = paletteEntries.map((e) => ({
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
    const parsed = parseVisualLayoutPatchJson(tensorPatchJson, paletteSeedEntries);
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
    if (nodes.length === 0 && options.seedWhenEmpty !== false) {
      nodes = seedDraftNodesFromPalette(paletteSeedEntries) as DraftNode[];
    }
    setDraftNodes(nodes);
    setSelectedLayoutClassRefs(parsed.value.layoutClassRefs);
    setSelectedNodeId(null);
    historyRef.current = [{ nodes: nodes.map((n) => ({ ...n })), label: historyLabel }];
    historyPtrRef.current = 0;
    setCanUndo(false);
    setCanRedo(false);
    setLifecyclePhase("idle");
    return true;
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
      if (ctrl && e.key === "z" && !e.shiftKey) { e.preventDefault(); undo(); }
      if (ctrl && (e.key === "y" || (e.key === "z" && e.shiftKey))) { e.preventDefault(); redo(); }
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
      const { candidates, errors: candErr } = await loadLayoutCandidatesFromBackend();
      let scopedCandidates =
        scopedPackageId && scopedRouteKey && scopedLayoutId
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
        const promoted = body?.emission?.data as PromotedPaletteEntry[] | undefined;
        if (!Array.isArray(promoted)) {
          setPaletteLoadFailed(true);
          setCandidateErrors((prev) => [
            ...prev,
            { code: "PROMOTED_PALETTE_LOAD_FAILED", message: "配置可能部品一覧が取得できませんでした。" },
          ]);
          setPaletteEntries([]);
          setPaletteStatus("配置可能部品一覧の読み込みに失敗しました。");
          return;
        }
        const scopedPromoted = scopedPackageId
          ? promoted.filter((p) => p.packageId === scopedPackageId)
          : promoted;
        const promotedEntries = scopedPromoted.map((p) => ({
          ...p,
          isDraftOnly: false,
        } satisfies PaletteEntry));
        const promotedKeys = new Set(promotedEntries.map((p) => p.componentKey));
        const draftCatalog = scopedPackageId
          ? []
          : COMPONENT_CATALOG_ENTRIES
            .filter((c) => isDraftOnlyEntry(c) && !promotedKeys.has(c.componentKey))
            .map((c) => ({
              componentKey: c.componentKey,
              componentKind: c.componentKind,
              isDraftOnly: true,
            } satisfies PaletteEntry));
        setPaletteEntries([...promotedEntries, ...draftCatalog]);
        setPaletteStatus(
          scopedPackageId
            ? `パッケージ内 ${promotedEntries.length} 件`
            : `配置可能 ${promotedEntries.length} 件 / 下書き ${draftCatalog.length} 件`,
        );
        const layoutSource = scopedPromoted.length > 0 ? scopedPromoted : promoted;
        if (candidates.length === 0 && layoutSource.length > 0) {
          setLayoutCandidates(deriveCandidatesFromPalette(layoutSource));
        }
        const routeLayoutSource = nextCandidates.length > 0 ? nextCandidates : candidates;
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
        setCandidateErrors((prev) => [...prev, { code: "PROMOTED_PALETTE_LOAD_ERROR", message: String(e) }]);
        setPaletteEntries([]);
        setPaletteStatus(`配置可能部品一覧の読み込みに失敗しました: ${e}`);
      }
    };
    load();
  }, [scopedPackageId, scopedRouteKey, scopedLayoutId]);

  // ── hydrate layout_patch_json from DB on package / route / layout selection ─
  useEffect(() => {
    if (!scopedPackageId?.trim() || !effectiveLayoutId || !effectiveRouteKey) return;
    if (paletteLoadFailed) return;

    let cancelled = false;
    const hydrate = async () => {
      try {
        const body = await dispatchAdminOp("ui_topology", "get_layout_patch_draft", {
          packageId: scopedPackageId.trim(),
          layoutId: effectiveLayoutId,
          routeKey: effectiveRouteKey,
        });
        if (cancelled) return;
        if (body?.errors?.length) {
          const notFound = body.errors.some(
            (e: ValidationError) =>
              e.code === "LAYOUT_PATCH_DRAFT_NOT_FOUND" ||
              e.code === "PACKAGE_WIRING_NOT_FOUND",
          );
          if (notFound) {
            applyCanvasFromTensorPatch("{}", "パレットから初期配置", { seedWhenEmpty: true });
            announce("保存済み layout draft なし — パレットから初期ノードを配置しました");
          } else {
            setPatchErrors(body.errors);
          }
          return;
        }
        const data = body?.emission?.data as { tensorPatchJson?: string } | undefined;
        const json = typeof data?.tensorPatchJson === "string" ? data.tensorPatchJson : "{}";
        if (applyCanvasFromTensorPatch(json, "DB layout draft 読込", { seedWhenEmpty: true })) {
          announce("layout_patch_json を canvas に読み込みました");
        }
      } catch (e) {
        if (!cancelled) {
          setPatchErrors([{ code: "LAYOUT_PATCH_DRAFT_LOAD_ERROR", message: String(e) }]);
        }
      }
    };
    hydrate();
    return () => { cancelled = true; };
  }, [scopedPackageId, effectiveLayoutId, effectiveRouteKey, paletteLoadFailed, paletteEntries.length]);

  // ── _tmp draft: sessionStorage auto-save / resume ────────────────────────
  // Key: ui_builder_tmp_draft_<packageId>. Auto-save on canvas ops; clear on apply success.
  // TODO: backend _tmp column for cross-device persistence (pending schema migration).
  const tmpDraftKey = scopedPackageId?.trim()
    ? `ui_builder_tmp_draft_${scopedPackageId.trim()}`
    : null;

  useEffect(() => {
    if (!tmpDraftKey || typeof globalThis.sessionStorage === "undefined") return;
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
        historyRef.current = [{ nodes: nodes.map((n) => ({ ...n })), label: "_tmp 復元" }];
        historyPtrRef.current = 0;
        setCanUndo(false);
        setCanRedo(false);
        setLifecyclePhase("idle");
      }
    } catch {
      sessionStorage.removeItem(tmpDraftKey);
    }
  }, [tmpDraftKey]);

  useEffect(() => {
    if (!tmpDraftKey || typeof globalThis.sessionStorage === "undefined") return;
    if (draftNodes.length === 0) return;
    try {
      sessionStorage.setItem(tmpDraftKey, JSON.stringify({
        nodes: draftNodes,
        classRefs: selectedLayoutClassRefs,
      }));
    } catch {
      // storage full — ignore
    }
  }, [draftNodes, selectedLayoutClassRefs, tmpDraftKey]);

  // ── layout patch (preview / validate / apply) ────────────────────────────
  const callLayoutPatch = async (action: "preview" | "validate" | "apply") => {
    setPatchErrors([]);
    setPatchSummary(null);
    setDebugJson(null);
    setLayoutApplyHandoffOpen(false);
    if (action !== "preview") {
      setLayoutPatchPreviewOpen(false);
      setLayoutPatchPreviewAudit(null);
      setLayoutPatchPreviewNodes([]);
      setLayoutPatchPreviewClassRefs([]);
    }

    if (!canPatch) {
      setPatchErrors([{ code: "NO_ROUTE_LAYOUT", message: "ルートとレイアウトを選択してください。" }]);
      return;
    }
    if (action === "apply") {
      const draftOnlyNodes = draftNodes.filter((n) => n.isDraftOnly);
      if (draftOnlyNodes.length > 0) {
        setPatchErrors(draftOnlyNodes.map((n) => ({
          code: "DRAFT_ONLY_NODES",
          message: `まだ使えない部品が ${draftOnlyNodes.length} 件あります — 先に登録してください`,
          nodeId: n.nodeId,
          componentKey: n.componentKey,
        })));
        announce(`保存ブロック: ${draftOnlyNodes.length} 件のまだ使えない部品があります`);
        return;
      }
    }

    const phaseMap: Record<string, LifecyclePhase> = {
      preview: "previewing", validate: "validating", apply: "applying",
    };
    setLifecyclePhase(phaseMap[action] as LifecyclePhase);
    setLoading(true);
    announce(`${action === "preview" ? "プレビュー" : action === "validate" ? "バリデート" : "適用"}を実行中...`);

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
      const summary = projectLayoutPatchSummary(action, body, draftNodes, selectedLayoutClassRefs.length, selectedLayout?.layoutKey);
      setPatchSummary(summary);

      // Failure phases are scoped to the action: preview/validate errors stay in their
      // respective phase (pipeline not advanced); only apply failures use applied_fail.
      const failPhase: Record<string, LifecyclePhase> = {
        preview: "previewed", validate: "validated", apply: "applied_fail",
      };
      if (body?.errors?.length) {
        setPatchErrors(body.errors);
        setLifecyclePhase(failPhase[action] as LifecyclePhase);
        announce(`エラー: ${body.errors[0].message}`);
      } else {
        const donePhase: Record<string, LifecyclePhase> = {
          preview: "previewed", validate: "validated", apply: "applied_ok",
        };
        const isPersisted = body?.emission?.data?.persisted === true;
        setLifecyclePhase(isPersisted ? "persisted" : donePhase[action] as LifecyclePhase);

        if (action === "preview") {
          const previewData = (body?.emission?.data ?? body) as { tensorPatchJson?: string };
          const normalizedJson = typeof previewData?.tensorPatchJson === "string"
            ? previewData.tensorPatchJson
            : submittedTensorPatchJson;
          if (applyCanvasFromTensorPatch(
            normalizedJson,
            "プレビュー結果を反映",
            { seedWhenEmpty: false },
          )) {
            announce("プレビュー結果を canvas に反映しました");
          }
          const parsed = parseVisualLayoutPatchJson(normalizedJson, paletteSeedEntries);
          const previewNodes = enrichLayoutPreviewNodes(
            parsed.ok ? parsed.value.nodes : draftNodes,
            paletteSeedEntries,
          ) as LayoutPreviewNodeInput[];
          const previewClassRefs = parsed.ok
            ? parsed.value.layoutClassRefs
            : selectedLayoutClassRefs;
          setLayoutPatchPreviewAudit(buildLayoutPatchPreviewAudit(
            submittedTensorPatchJson,
            normalizedJson,
            summary.message,
          ));
          setLayoutPatchPreviewNodes(previewNodes);
          setLayoutPatchPreviewClassRefs(previewClassRefs);
          setLayoutPatchPreviewOpen(true);
          announce("視覚監査モーダルを表示しました");
        }

        if (action === "apply" && summary.valid) {
          // Clear _tmp draft on successful apply
          if (tmpDraftKey && typeof globalThis.sessionStorage !== "undefined") {
            sessionStorage.removeItem(tmpDraftKey);
          }
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
          lifecycleRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
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
              message: `サーバーが異なるレイアウトID (${confirmedLayoutId}) を返しました。候補を再読み込みしてください。`,
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
        preview: "previewed", validate: "validated", apply: "applied_fail",
      };
      setPatchErrors([{ code: "NETWORK_ERROR", message: String(e) }]);
      setLifecyclePhase(failPhase[action] as LifecyclePhase);
      announce(`ネットワークエラー: ${e}`);
    } finally {
      setLoading(false);
    }
  };

  // ── node factory ─────────────────────────────────────────────────────────
  const makeNewNode = (entry: PaletteEntry, x: number, y: number): DraftNode => {
    const componentKind = entry.componentKind ||
      resolveComponentKindForLayoutPreview(entry.componentKey) ||
      undefined;
    const defaults = componentKind
      ? getLayoutPreviewDefaultSize(componentKind)
      : { width: DEFAULT_NODE_WIDTH, height: DEFAULT_NODE_HEIGHT };
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
      orderIndex: draftNodes.length,
      parentNodeId: null,
      gridCol: Math.max(1, Math.floor(x / 50) + 1),
      gridRow: Math.max(1, Math.floor(y / 40) + 1),
      x, y,
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
    announce(`${friendlyComponentLabel(newNode.componentKey)}をキャンバスに追加しました`);
  };

  const moveLayoutNode = (nodeId: string, dir: "up" | "down") => {
    const direction = dir === "up" ? "front" : "back";
    setDraftNodes((prev) => {
      const result = reorderLayoutNodeStack(prev, nodeId, direction);
      if (!result) return prev;
      const moved = prev.find((n) => n.nodeId === nodeId);
      pushHistory(result, `順序変更: ${friendlyComponentLabel(moved?.componentKey ?? nodeId)}`);
      return result;
    });
  };

  const removeNode = (nodeId: string) => {
    const node = draftNodes.find((n) => n.nodeId === nodeId);
    const next = draftNodes.filter((n) => n.nodeId !== nodeId);
    setDraftNodes(next);
    if (selectedNodeId === nodeId) setSelectedNodeId(null);
    pushHistory(next, `削除: ${node ? friendlyNodeLabel(node) : nodeId}`);
    setLifecyclePhase("idle");
    if (node) announce(`${friendlyNodeLabel(node)}を削除しました`);
  };

  const copyNode = (nodeId: string) => {
    const source = draftNodes.find((n) => n.nodeId === nodeId);
    if (!source || !packageScopedLayout) return;
    const cloned = cloneVisualNode(source, makeNodeId()) as DraftNode;
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
    const newNode = makeStructuralHtmlNode(htmlTag, {
      nodeId: makeNodeId(),
      x: 40 + draftNodes.length * 12,
      y: 40 + draftNodes.length * 12,
      orderIndex: draftNodes.length,
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
      const next = prev.map((n) => (n.nodeId === nodeId ? { ...n, ...updates } : n));
      return next;
    });
    setLifecyclePhase("idle");
  };

  const commitNodeUpdate = (nodeId: string, updates: Partial<DraftNode>, label: string) => {
    setDraftNodes((prev) => {
      const next = prev.map((n) => (n.nodeId === nodeId ? { ...n, ...updates } : n));
      pushHistory(next, label);
      return next;
    });
    setLifecyclePhase("idle");
  };

  // Gap 5: Keyboard move for selected node
  const handleKeyboardMoveNode = (nodeId: string, dx: number, dy: number) => {
    const node = draftNodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    const newX = snapToGrid(Math.max(0, node.x + dx), SNAP_SIZE);
    const newY = snapToGrid(Math.max(0, node.y + dy), SNAP_SIZE);
    commitNodeUpdate(nodeId, { x: newX, y: newY }, `移動: ${friendlyComponentLabel(node.componentKey)}`);
  };

  // Gap 5/6: Keyboard resize via resize handle Enter/Space — real delta, no mouse coords needed
  const handleKeyboardResizeNode = (nodeId: string, dir: ResizeDir) => {
    const node = draftNodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    const DELTA = SNAP_SIZE;
    let { x, y, width, height } = node;
    if (dir.includes("e")) width = snapToGrid(Math.max(40, width + DELTA), SNAP_SIZE);
    if (dir.includes("w")) { x = snapToGrid(Math.max(0, x - DELTA), SNAP_SIZE); width = snapToGrid(Math.max(40, width + DELTA), SNAP_SIZE); }
    if (dir.includes("s")) height = snapToGrid(Math.max(30, height + DELTA), SNAP_SIZE);
    if (dir.includes("n")) { y = snapToGrid(Math.max(0, y - DELTA), SNAP_SIZE); height = snapToGrid(Math.max(30, height + DELTA), SNAP_SIZE); }
    commitNodeUpdate(nodeId, { x, y, width, height }, `リサイズ: ${friendlyComponentLabel(node.componentKey)}`);
    announce(`${RESIZE_DIR_LABEL[dir]} — ${width}×${height}`);
  };

  // ── palette drag ─────────────────────────────────────────────────────────
  const handleDragStartPalette = (entry: PaletteEntry) => {
    dragSrc.current = { kind: "palette", entry };
  };

  const handleDragOverCanvas = (e: Event) => { e.preventDefault(); };

  const handleDropOnCanvas = (e: Event) => {
    e.preventDefault();
    const src = dragSrc.current;
    dragSrc.current = null;
    if (!src || src.kind !== "palette") return;
    if (rejectDraftPaletteEntry(src.entry)) return;
    const de = e as unknown as DragEvent;
    const rect = canvasRef.current?.getBoundingClientRect();
    const dropX = rect ? snapToGrid(Math.max(0, de.clientX - rect.left), SNAP_SIZE) : 20;
    const dropY = rect ? snapToGrid(Math.max(0, de.clientY - rect.top), SNAP_SIZE) : 20;
    if (src.entry.routeKey && !routeKey) setRouteKey(src.entry.routeKey);
    if (src.entry.layoutId && !layoutId) setLayoutId(src.entry.layoutId);
    addNode(makeNewNode(src.entry, dropX, dropY));
  };

  // Gap 5: Non-drag add from palette button
  const handleAddFromPalette = (entry: PaletteEntry) => {
    if (rejectDraftPaletteEntry(entry)) return;
    const count = draftNodes.length;
    const x = snapToGrid(20 + (count % 5) * 160, SNAP_SIZE);
    const y = snapToGrid(20 + Math.floor(count / 5) * 80, SNAP_SIZE);
    if (entry.routeKey && !routeKey) setRouteKey(entry.routeKey);
    if (entry.layoutId && !layoutId) setLayoutId(entry.layoutId);
    addNode(makeNewNode(entry, x, y));
  };

  // Gap 7: Quick-start templates from empty state
  const handleAddFromEmptyState = (templateId: string) => {
    if (!packageScopedLayout) return;
    const catalog = paletteEntries.filter((e) => !e.isDraftOnly);
    const pick = (key: string) => catalog.find((e) => e.componentKey.includes(key)) ?? catalog[0];

    let nodes: DraftNode[] = [];
    if (templateId === "starter_header_main" && catalog.length >= 2) {
      const header = pick("header") ?? catalog[0];
      const main = pick("main") ?? catalog[1] ?? catalog[0];
      nodes = [
        makeNewNode(header, 20, 20),
        { ...makeNewNode(main, 20, 100), height: 200 },
      ];
    } else if (templateId === "starter_card" && catalog.length >= 1) {
      nodes = [{ ...makeNewNode(pick("card") ?? catalog[0], 40, 40), width: 200, height: 100 }];
    } else if (templateId === "starter_form" && catalog.length >= 1) {
      const form = pick("form") ?? catalog[0];
      nodes = [{ ...makeNewNode(form, 40, 40), width: 300, height: 200 }];
    } else if (catalog.length >= 1) {
      nodes = [makeNewNode(catalog[0], 40, 40)];
    }

    if (nodes.length === 0) return;
    setDraftNodes(nodes);
    pushHistory(nodes, `テンプレート: ${templateId}`);
    setLifecyclePhase("idle");
    announce(`${nodes.length} 件の部品を追加しました`);
  };

  // ── mouse drag for canvas node movement ──────────────────────────────────
  const clearPendingDrag = () => {
    const pending = pendingDragState.current;
    if (pending?.holdTimerId !== undefined) {
      clearTimeout(pending.holdTimerId);
    }
    pendingDragState.current = null;
  };

  useEffect(() => () => clearPendingDrag(), []);

  const activateCanvasDrag = (
    nodeId: string,
    pos: { x: number; y: number },
    startNodeX: number,
    startNodeY: number,
  ) => {
    dragState.current = {
      nodeId,
      startMouseX: pos.x,
      startMouseY: pos.y,
      startNodeX,
      startNodeY,
    };
    setActiveDragNodeId(nodeId);
  };

  const getCanvasPos = (e: Event): { x: number; y: number } => {
    const me = e as MouseEvent;
    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return { x: 0, y: 0 };
    return { x: me.clientX - rect.left, y: me.clientY - rect.top };
  };

  const handleNodeMouseDown = (e: Event, nodeId: string) => {
    if (resizeState.current) return;
    const me = e as MouseEvent;
    me.stopPropagation();
    const node = draftNodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    setSelectedNodeId(nodeId);
    clearPendingDrag();
    dragState.current = null;
    setActiveDragNodeId(null);
    setLiveDragPos(null);

    const pos = getCanvasPos(e);
    const holdTimerId = setTimeout(() => {
      if (pendingDragState.current?.nodeId !== nodeId) return;
      pendingDragState.current = null;
      activateCanvasDrag(nodeId, pos, node.x, node.y);
    }, CANVAS_DRAG_HOLD_MS);

    pendingDragState.current = {
      nodeId,
      startMouseX: pos.x,
      startMouseY: pos.y,
      startNodeX: node.x,
      startNodeY: node.y,
      holdTimerId,
    };
  };

  const handleResizeHandleMouseDown = (e: Event, nodeId: string, dir: ResizeDir) => {
    const me = e as MouseEvent;
    me.preventDefault();
    me.stopPropagation();
    dragState.current = null;
    const node = draftNodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    const pos = getCanvasPos(e);
    resizeState.current = { nodeId, dir, startMouseX: pos.x, startMouseY: pos.y, startNodeX: node.x, startNodeY: node.y, startNodeW: node.width, startNodeH: node.height };
  };

  const handleCanvasMouseMove = (e: Event) => {
    const pos = getCanvasPos(e);
    const pending = pendingDragState.current;
    if (pending && !dragState.current) {
      const dist = Math.hypot(pos.x - pending.startMouseX, pos.y - pending.startMouseY);
      if (dist >= CANVAS_DRAG_MOVE_THRESHOLD_PX) {
        clearPendingDrag();
        activateCanvasDrag(
          pending.nodeId,
          { x: pending.startMouseX, y: pending.startMouseY },
          pending.startNodeX,
          pending.startNodeY,
        );
      }
    }
    if (dragState.current) {
      const { startMouseX, startMouseY, startNodeX, startNodeY, nodeId } = dragState.current;
      setLiveDragPos({
        nodeId,
        x: snapToGrid(Math.max(0, startNodeX + pos.x - startMouseX), SNAP_SIZE),
        y: snapToGrid(Math.max(0, startNodeY + pos.y - startMouseY), SNAP_SIZE),
      });
    } else if (resizeState.current) {
      const { startMouseX, startMouseY, startNodeX, startNodeY, startNodeW, startNodeH, nodeId, dir } = resizeState.current;
      const dx = pos.x - startMouseX;
      const dy = pos.y - startMouseY;
      let newX = startNodeX, newY = startNodeY, newW = startNodeW, newH = startNodeH;
      if (dir.includes("e")) newW = Math.max(40, snapToGrid(startNodeW + dx, SNAP_SIZE));
      if (dir.includes("s")) newH = Math.max(30, snapToGrid(startNodeH + dy, SNAP_SIZE));
      if (dir.includes("w")) { newW = Math.max(40, snapToGrid(startNodeW - dx, SNAP_SIZE)); newX = snapToGrid(startNodeX + (startNodeW - newW), SNAP_SIZE); }
      if (dir.includes("n")) { newH = Math.max(30, snapToGrid(startNodeH - dy, SNAP_SIZE)); newY = snapToGrid(startNodeY + (startNodeH - newH), SNAP_SIZE); }
      setLiveResizePos({ nodeId, x: newX, y: newY, width: newW, height: newH });
    }
  };

  const handleCanvasMouseUp = () => {
    clearPendingDrag();
    if (dragState.current && liveDragPos) {
      const { nodeId } = dragState.current;
      const node = draftNodes.find((n) => n.nodeId === nodeId);
      if (node && (liveDragPos.x !== node.x || liveDragPos.y !== node.y)) {
        commitNodeUpdate(nodeId, { x: liveDragPos.x, y: liveDragPos.y }, "移動");
      }
    }
    if (resizeState.current && liveResizePos) {
      commitNodeUpdate(resizeState.current.nodeId, { x: liveResizePos.x, y: liveResizePos.y, width: liveResizePos.width, height: liveResizePos.height }, "リサイズ");
    }
    dragState.current = null;
    resizeState.current = null;
    setActiveDragNodeId(null);
    setLiveDragPos(null);
    setLiveResizePos(null);
  };

  const getLivePos = (nodeId: string): { x: number; y: number; width: number; height: number } | null => {
    if (liveDragPos?.nodeId === nodeId) {
      const node = draftNodes.find((n) => n.nodeId === nodeId);
      return node ? { x: liveDragPos.x, y: liveDragPos.y, width: node.width, height: node.height } : null;
    }
    if (liveResizePos?.nodeId === nodeId) return liveResizePos;
    return null;
  };

  const toggleLayoutClassRef = (classKey: string) => {
    setLayoutClassRefError(null);
    setSelectedLayoutClassRefs((prev) => {
      const next = prev.includes(classKey) ? prev.filter((k) => k !== classKey) : [...prev, classKey];
      if (next.length > 0) { const r = resolveTopologyLayoutClassRefs(next); if (!r.ok) setLayoutClassRefError(r.error); }
      return next;
    });
  };

  const applyManualLayoutClassRef = () => {
    const key = manualLayoutClassRef.trim();
    if (!key) return;
    const r = resolveTopologyLayoutClassRefs([key]);
    if (!r.ok) { setLayoutClassRefError(r.error); return; }
    setLayoutClassRefError(null);
    setSelectedLayoutClassRefs((prev) => prev.includes(key) ? prev : [...prev, key]);
    setManualLayoutClassRef("");
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
          ルート <code class="font-mono text-xs">{scopedRouteKey}</code> / レイアウト{" "}
          <code class="font-mono text-xs">{scopedLayoutId?.slice(0, 8)}…</code>（選択パッケージに固定）
        </p>
      )}

      <details class="mb-3.5">
        <summary class="cursor-pointer text-xs text-gray-500 hover:text-gray-700">技術情報</summary>
        <div class="alert-warn mt-1 text-xs">
          <strong>投影サーフェス境界:</strong> フロントエンドはドラフト状態・視覚プレビュー・intent 送信のみ担当。
          適用は <code>preview → validate → apply</code> 経由。直接 DB 書き込みは行いません。
        </div>
      </details>

      <RouteLayoutSelector
        candidates={displayCandidates}
        routeKey={routeKey}
        layoutId={layoutId}
        onRouteChange={(r) => { setRouteKey(r); setManualRouteKey(""); const first = layoutsForRoute(displayCandidates, r)[0]; setLayoutId(first?.layoutId ?? ""); setManualLayoutId(""); }}
        onLayoutChange={(l) => { setLayoutId(l); setManualLayoutId(""); }}
        disabled={selectorsDisabled || layoutSelectorsLocked}
        loadError={candidateErrors}
      />

      {displayCandidates.length === 0 && candidateErrors.length === 0 && packageScopedLayout && (
        <div class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
          <p class="font-semibold">部品登録タブで配置可能化が必要です</p>
          <p class="mt-1 text-xs">
            ルート候補は配置可能化のあとに表示されます。部品選択タブでルート入力 → パッケージ化 → 配置可能化の順で進めてください。
          </p>
          <label class="mt-2 flex flex-col gap-0.5 text-xs">
            ページルート（手入力で先に進める場合）
            <input
              value={manualRouteKey}
              onInput={(e) => setManualRouteKey((e.target as HTMLInputElement).value)}
              placeholder="部品登録タブと同じ routeKey"
              class="input-mono w-full text-xs"
            />
          </label>
        </div>
      )}

      {!layoutSelectorsLocked && (
        <AdvancedManualOverride title="詳細設定 — レイアウト・ルートを直接指定">
          <div class="flex flex-wrap gap-2">
            <input value={manualRouteKey} onInput={(e) => setManualRouteKey((e.target as HTMLInputElement).value)} placeholder="routeKey 手入力" class="input-mono flex-1 text-xs" />
            <input value={manualLayoutId} onInput={(e) => setManualLayoutId((e.target as HTMLInputElement).value)} placeholder="layoutId UUID 手入力" class="input-mono flex-[2] text-xs" />
          </div>
        </AdvancedManualOverride>
      )}

      {!canPatch && !selectorsDisabled && (
        <p class="text-sm text-yellow-700 mb-2">ルートとレイアウトを選択してから操作してください。</p>
      )}

      {packageScopedLayout && (
        <div class="mb-2 rounded border border-blue-200 bg-blue-50 px-3 py-2 text-xs text-blue-900">
          <strong class="block text-sm text-blue-900 mb-1">{UX_LAYOUT_EDITOR_SURFACE}</strong>
          <span class="text-[0.7rem] text-blue-700">
            編集対象: 位置 (x/y)・サイズ (幅/高さ)・親部品・配置スロット・表示順・layoutClassRefs —
            パレットで部品を追加し、canvas 上でドラッグ・リサイズして位置を調整します。
            構造設定は右のインスペクタで編集してください。
          </span>
        </div>
      )}

      {/* layout draft プレビュー & 操作ツールバー */}
      <div class="mb-2 flex flex-wrap items-center gap-2 rounded-lg border border-gray-200 bg-gray-50 px-3 py-2">
        <div class="flex items-center gap-1">
          <button
            type="button"
            onClick={undo}
            disabled={!canUndo}
            class="btn-secondary py-1 px-2 text-xs disabled:opacity-40"
            title="元に戻す (Ctrl+Z)"
            aria-label="元に戻す"
          >↩ 元に戻す</button>
          <button
            type="button"
            onClick={redo}
            disabled={!canRedo}
            class="btn-secondary py-1 px-2 text-xs disabled:opacity-40"
            title="やり直す (Ctrl+Y)"
            aria-label="やり直す"
          >↪ やり直す</button>
        </div>

        <div class="h-4 w-px bg-gray-300" />

        <label class="flex cursor-pointer items-center gap-1 text-xs">
          <input type="checkbox" checked={showGrid} onChange={(e) => setShowGrid((e.target as HTMLInputElement).checked)} />
          グリッド表示
        </label>

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

        <span class="ml-auto text-xs text-gray-400" aria-live="polite">
          {draftNodes.length} 部品
          {selectedNode ? ` — 選択中: ${friendlyComponentLabel(selectedNode.componentKey)}` : ""}
        </span>
      </div>

      {/* layout draft プレビュー & 操作エリア: palette + live canvas + inspector */}
      <div class={`mb-3 flex gap-2.5 ${canvasPreviewClass}`}>
        {packageScopedLayout ? (
          <>
            <LayoutPalette
              onDragStart={handleDragStartPalette}
              onAddToCanvas={handleAddFromPalette}
              entries={paletteEntries}
              status={paletteStatus}
              packageOnly={true}
            />
            <StructuralHtmlPalette onAddTag={addStructuralHtmlNode} />
          </>
        ) : (
          <div class="w-44 shrink-0 rounded-lg border border-dashed border-gray-200 bg-gray-50 p-3 text-center text-xs text-gray-400">
            パッケージ選択後にパレットが有効になります
          </div>
        )}

        <div class="min-w-0 flex-1">
          <VisualLayoutCanvas
            draftNodes={draftNodes}
            selectedNodeId={selectedNodeId}
            layoutClassRefs={selectedLayoutClassRefs}
            canvasRef={canvasRef}
            liveDragNodeId={activeDragNodeId ?? liveDragPos?.nodeId ?? null}
            liveResizeNodeId={liveResizePos?.nodeId ?? null}
            getLivePos={getLivePos}
            showGrid={showGrid}
            onSelectNode={(id) => setSelectedNodeId(id)}
            onDeselectAll={() => setSelectedNodeId(null)}
            onNodeMouseDown={handleNodeMouseDown}
            onResizeHandleMouseDown={handleResizeHandleMouseDown}
            onCanvasMouseMove={handleCanvasMouseMove}
            onCanvasMouseUp={handleCanvasMouseUp}
            onDragOver={handleDragOverCanvas}
            onDrop={handleDropOnCanvas}
            onKeyboardMoveNode={handleKeyboardMoveNode}
            onKeyboardResizeNode={handleKeyboardResizeNode}
            onDeleteNode={removeNode}
            onAddFromEmptyState={packageScopedLayout ? handleAddFromEmptyState : undefined}
            allowEmptyStateTemplates={!packageScopedLayout}
          />
        </div>

        {/* right panel: layer tree + inspector */}
        <div class="flex shrink-0 flex-col gap-2" style="width:200px;">
          <LayerTree
            draftNodes={draftNodes}
            selectedNodeId={selectedNodeId}
            onSelect={(id) => setSelectedNodeId(id === selectedNodeId ? null : id)}
            onMoveUp={(id) => moveLayoutNode(id, "up")}
            onMoveDown={(id) => moveLayoutNode(id, "down")}
            onCopy={copyNode}
            onDelete={removeNode}
          />
          {selectedNode
            ? (
              <CanvasInspector
                node={selectedNode}
                draftNodes={draftNodes}
                slotKeyCandidates={slotKeyCandidates}
                onUpdate={(updates) => updateNode(selectedNode.nodeId, updates)}
                onCommit={(updates, label) => commitNodeUpdate(selectedNode.nodeId, updates, label)}
                onToggleLayoutClassRef={(classKey) => toggleNodeLayoutClassRef(selectedNode.nodeId, classKey)}
                onCopy={() => copyNode(selectedNode.nodeId)}
                onEditDesign={onNavigate ? () => onNavigate("design") : undefined}
                onClose={() => setSelectedNodeId(null)}
              />
            )
            : (
              <div class="rounded border border-dashed border-gray-200 bg-gray-50 p-3 text-center text-xs text-gray-400">
                canvas またはレイヤーから要素を選択してください
              </div>
            )
          }
        </div>
      </div>

      <Accordion title="layoutClassRefs 設定（layout child の responsibility）" defaultOpen={false}>
        <p class="text-muted-xs mb-2">
          レイアウト投影専用のスタイルクラスを選択します。canvas の視覚装飾（cssTokenRefs 等）はここではなく右パネルのデザインインスペクタで設定します。
        </p>
        <details class="mb-2 rounded border border-slate-200 bg-slate-50 px-2 py-1.5 text-xs text-slate-700">
          <summary class="cursor-pointer font-semibold">allowed_for と canvas 反映（技術詳細）</summary>
          <p class="mt-1 mb-0">
            <code>layout_root</code> / <code>layout_section</code> / <code>layout_row</code> → キャンバス外枠、
            <code>component_wrapper</code> → 各ノード枠、
            <code>preview_state</code> → 選択中ノードのみ。
            選択した class のうち対象ロールに合うものだけがプレビューに適用されます。
          </p>
        </details>
        <TopologyLayoutClassPicker selectedClassRefs={selectedLayoutClassRefs} onToggle={toggleLayoutClassRef} scopeFilter="" allowedForFilter="" />
        {layoutClassRefError && <p class="text-red-600 text-sm mt-2" role="alert">{layoutClassRefError}</p>}
        <AdvancedManualOverride title="詳細設定 — クラスキーを直接入力">
          <div class="flex flex-wrap gap-2">
            <input value={manualLayoutClassRef} onInput={(e) => setManualLayoutClassRef((e.target as HTMLInputElement).value)} placeholder="layout.root.grid" class="input-mono flex-1 text-xs" />
            <button type="button" onClick={applyManualLayoutClassRef} class="btn-secondary text-xs">適用</button>
          </div>
        </AdvancedManualOverride>
      </Accordion>

      <p class="mb-3 text-xs text-slate-600">
        cssTokenRefs・color・spacing・radius は右パネルのデザインインスペクタ（またはボタン{" "}
        <button type="button" class="link" onClick={() => onNavigate?.("design")}>
          デザインインスペクタを開く
        </button>
        ）で保存します（design inspector 担当）。ここでは canvas 操作と layout child のみ保存します。
      </p>

      {(() => {
        const hasReadinessError = !canPatch || draftNodes.some((n) => n.isDraftOnly) || !!layoutClassRefError;
        return (
          <details class="mb-3" open={hasReadinessError}>
            <summary class={`cursor-pointer rounded px-2 py-1 text-xs font-semibold ${
              hasReadinessError
                ? "bg-amber-100 text-amber-900"
                : "bg-green-50 text-green-700"
            }`}>
              {hasReadinessError ? "⚠ 保存前チェック（要確認）" : "✓ 保存前チェック（問題なし）"}
            </summary>
            <ApplyReadinessPanel
              canPatch={canPatch}
              effectiveRouteKey={effectiveRouteKey}
              effectiveLayoutId={effectiveLayoutId}
              draftNodes={draftNodes}
              layoutClassRefError={layoutClassRefError}
              onNavigate={onNavigate}
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
            aria-label="プレビュー — 保存前の視覚監査（部品の見た目・DB変更なし）"
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
          {loading && <span class="flex items-center text-sm text-gray-500" aria-live="polite">実行中...</span>}
        </div>
        <p class="mt-1 text-[0.65rem] text-gray-400">
          プレビュー: 視覚監査モーダル（DB変更なし） → バリデート: ref整合チェック → 適用: DBへ反映
        </p>
      </div>

      <LayoutPatchPreviewModal
        open={layoutPatchPreviewOpen}
        audit={layoutPatchPreviewAudit}
        previewNodes={layoutPatchPreviewNodes}
        layoutClassRefs={layoutPatchPreviewClassRefs}
        onClose={() => setLayoutPatchPreviewOpen(false)}
        onProceedValidate={() => {
          setLayoutPatchPreviewOpen(false);
          void callLayoutPatch("validate");
        }}
      />

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
          onNavigate?.("design");
        }}
      />

      {/* Gap 3: Actionable error panel */}
      {patchErrors.length > 0 && (
        <ActionableValidationErrorPanel
          errors={patchErrors}
          title="エラー — 修正方法"
          onNavigate={onNavigate}
        />
      )}

      {patchSummary && <LayoutPatchSummaryPanel summary={patchSummary} />}

      <Accordion title="詳細情報（開発者向け）" defaultOpen={false}>
        <p class="text-muted-xs mb-2">v2 ビジュアル座標 (x/y/width/height) が含まれます。</p>
        <pre class="pre-box max-h-40 overflow-y-auto m-0 mb-2">{tensorPatchJson}</pre>
        {debugJson && (
          <pre class="pre-box max-h-[200px] overflow-y-auto border border-gray-200 m-0">{debugJson}</pre>
        )}
      </Accordion>
    </div>
  );
}

// ─── メインエクスポート ────────────────────────────────────────────────────────

type AdminPackageRow = {
  packageId: string;
  packageKey: string;
  routeKey?: string | null;
  layoutId?: string | null;
  wiringId?: string | null;
};

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
  const [manifestOptions, setManifestOptions] = useState<ManifestPickerOption[]>([]);
  const [loadingManifests, setLoadingManifests] = useState(false);
  const [selectedManifestId, setSelectedManifestId] = useState("");
  const [selectedManifestWiringKey, setSelectedManifestWiringKey] = useState("");

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
      const manifestId = manifestIdFromTargetRef(data.targetRef ?? "", "manifest");
      const wiringKey = manifestWiringKeyFromTargetRef(data.targetRef ?? "", "manifest");
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
      const body = await dispatchAdminOp("manifest", "list_screen_read_query_wiring", {
        manifestId: selectedManifestId.trim(),
      });
      const data = body?.emission?.data as { candidates?: ScreenReadQueryWiringCandidate[] } | undefined;
      if (Array.isArray(data?.candidates)) {
        setScreenWiringCandidates(data.candidates);
        return;
      }
      const getBody = await dispatchAdminOp("manifest", "get", {
        manifestId: selectedManifestId.trim(),
      });
      const detail = getBody?.emission?.data as { topologyRawJson?: string } | undefined;
      if (typeof detail?.topologyRawJson === "string") {
        setScreenWiringCandidates(buildScreenReadQueryWiringCandidates(detail.topologyRawJson));
      } else {
        setScreenWiringCandidates([]);
      }
    })();
  }, [targetSurface, selectedManifestId]);

  const resolvedTargetRefForSave = (): string | null => {
    if (targetSurface === "manifest") {
      if (!selectedManifestId.trim()) return null;
      return encodeManifestPackageTargetRef(selectedManifestId, selectedManifestWiringKey);
    }
    return targetRef.trim() || null;
  };

  const handleSaveWiring = async () => {
    if (!selectedPackageId || !wiring?.wiringId || !wiringKind.trim() || !targetSurface) {
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
      const body = await dispatchAdminOp("ui_topology", "update_package_wiring", {
        packageId: selectedPackageId,
        wiringId: wiring.wiringId,
        wiringKind: wiringKind.trim(),
        targetSurface,
        targetRef: nextTargetRef,
      });
      const refreshed = body?.emission?.data?.wiring as AdminPackageWiringRow | undefined;
      if (body?.success && refreshed?.wiringId) {
        applyLoadedWiring(refreshed);
        setSaveStatus("パッケージ配線を保存しました。");
        onWiringSaved?.(refreshed);
      } else {
        setSaveStatus(body?.errors?.[0]?.message ?? "配線の保存に失敗しました。");
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
        保存済み配線を読み込み、一覧から選んで接続先を設定します（UUID の手入力は不要です）。
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
                onChange={(e) => setWiringKind((e.target as HTMLSelectElement).value)}
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
                    選択できるページがありません。先にコンテンツ管理で Step 1 を保存してください。
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
                          このページに read/query 配線候補がありません。Step 3 の保存後に再度お試しください。
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
                                  checked={selectedManifestWiringKey === c.wiringKey}
                                  onChange={() => handleSelectManifestWiring(c.wiringKey)}
                                />
                                <span>
                                  <span class="font-mono text-indigo-900">{c.wiringKey}</span>
                                  <span class="mt-0.5 block text-slate-600">{c.label}</span>
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
                  onInput={(e) => setTargetRef((e.target as HTMLInputElement).value)}
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
          wiring_id は <code>ui_topology_tensor</code> 経由でパッケージに紐づきます。
          manifest 接続時の target_ref は <code>manifest:&lt;uuid&gt;:&lt;wiringKey&gt;</code> 形式で保存されます。
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
      return { manifestId: item.manifestId, label: stored, status: item.status };
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

function PackageScopeSelector({
  packages,
  selectedPackageId,
  onSelectPackage,
  heading = "パッケージ選択（編集ルート）",
}: {
  packages: AdminPackageRow[];
  selectedPackageId: string;
  onSelectPackage: (id: string) => void;
  heading?: string;
}): JSX.Element {
  const selected = packages.find((p) => p.packageId === selectedPackageId);
  return (
    <section class="mb-4 rounded border border-slate-200 p-3 text-sm">
      <h3 class="font-semibold">{heading}</h3>
      <p class="text-muted-xs mb-2">
        配置・デザイン設定・配線は選択したパッケージに紐づきます。
      </p>
      <label class="block text-xs mb-2">
        パッケージ
        <select
          class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
          value={selectedPackageId}
          onChange={(e) => onSelectPackage((e.target as HTMLSelectElement).value)}
        >
          <option value="">— 選択 —</option>
          {packages.map((p) => (
            <option key={p.packageId} value={p.packageId}>
              {p.packageKey}{p.routeKey ? ` (${p.routeKey})` : ""}
            </option>
          ))}
        </select>
      </label>
      {selected && (!selected.routeKey || !selected.layoutId) && (
        <p class="mb-2 rounded border border-amber-200 bg-amber-50 px-2 py-1 text-xs text-amber-900">
          このパッケージにページルートや配置が未連携です。step 4.1 でパッケージ化からやり直してください。
        </p>
      )}
      {selected && (
        <p class="mb-2 font-mono text-[0.65rem] text-slate-500">
          package: {selected.packageId.slice(0, 8)}…
          {selected.layoutId && <> / layout: {selected.layoutId.slice(0, 8)}…</>}
        </p>
      )}
    </section>
  );
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
};

type LayoutNodeDesignOption = {
  nodeId: string;
  label: string;
  nodeKind?: LayoutNodeKind;
  htmlTag?: StructuralHtmlTag;
};

function defaultDesignName(componentKey: string): string {
  const slug = componentKey.split("/").pop()?.replace(/[^a-zA-Z0-9._-]+/g, "_") ?? "part";
  return `${slug}_design`;
}

function ComponentDesignPreviewCanvas({
  componentKey,
  componentKind,
  cssTokenRefs,
  sampleText,
  reactionIntent,
  onSampleTextChange,
}: {
  componentKey: string;
  componentKind?: string;
  cssTokenRefs: string[];
  sampleText: string;
  reactionIntent: string;
  onSampleTextChange: (value: string) => void;
}): JSX.Element {
  const previewStyle = buildInlineStyleFromCssTokenRefs(cssTokenRefs);
  return (
    <section
      class="rounded-lg border-2 border-slate-300 bg-slate-50 p-4"
      aria-label={UX_DESIGN_EDITOR_SURFACE}
    >
      <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div class="flex items-center gap-2">
          <h4 class="text-sm font-semibold text-slate-800">デザインプレビュー</h4>
          <span class="rounded bg-slate-200 px-1.5 py-0.5 text-[0.6rem] font-medium text-slate-600">
            読み取り専用
          </span>
        </div>
        <span class="text-[0.65rem] text-slate-500">
          トークン {cssTokenRefs.length} 件をリアルタイム反映
        </span>
      </div>
      <label class="mb-3 block text-xs text-slate-700">
        サンプルテキスト
        <input
          class="mt-1 w-full rounded border bg-white px-2 py-1 text-sm"
          value={sampleText}
          onInput={(e) => onSampleTextChange((e.target as HTMLInputElement).value)}
          placeholder="プレビュー用の表示テキスト"
        />
      </label>
      <div
        class="mx-auto min-h-[220px] max-w-lg rounded-lg border border-slate-300 bg-white p-6 shadow-sm"
        style={previewStyle}
      >
        <p class="mb-3 text-xs font-medium text-slate-500">
          {friendlyComponentLabel(componentKey)}
          {componentKind ? ` (${componentKind})` : ""}
        </p>
        <div class="mb-3 min-h-[80px] rounded border border-slate-200 bg-white/80 p-2">
          <LayoutPreviewNodeFrame
            componentKey={componentKey}
            componentKind={componentKind}
          />
        </div>
        <p class="text-sm" style={{ color: previewStyle.color, fontFamily: previewStyle["font-family"] }}>
          {sampleText || "サンプルテキストを入力するとここに表示されます"}
        </p>
      </div>
      {reactionIntent.trim() && (
        <p class="mt-2 text-xs text-slate-600">
          反応意図: {reactionIntent.trim()}
        </p>
      )}
      {cssTokenRefs.length > 0 && (
        <ul class="mt-3 flex flex-wrap gap-1.5">
          {cssTokenRefs.map((key) => {
            const token = CSS_DICTIONARY_TOKENS.find((t) => t.tokenKey === key);
            return (
              <li
                key={key}
                class="flex items-center gap-1 rounded border border-indigo-200 bg-white px-2 py-0.5 font-mono text-[0.65rem]"
              >
                {token && <CssTokenSwatch token={token} />}
                {key}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

function mapSavedDesignRow(raw: Record<string, unknown>): SavedComponentDesignRow {
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
    layoutNodeId: typeof raw.layoutNodeId === "string" ? raw.layoutNodeId : null,
    cssTokenRefs: refs,
    responsiveTokenRefs,
    inlineText: typeof raw.inlineText === "string" ? raw.inlineText : "",
    linkHref: typeof raw.linkHref === "string" ? raw.linkHref : "",
    linkTarget: typeof raw.linkTarget === "string" ? raw.linkTarget : "",
    reactionIntent: typeof raw.reactionIntent === "string" ? raw.reactionIntent : "",
    classname: typeof raw.classname === "string" ? raw.classname : "",
    tailwind: typeof raw.tailwind === "string" ? raw.tailwind : "",
  };
}

const DESIGN_TARGET_PACKAGE_ITEM = "package_item" as const;
const DESIGN_TARGET_LAYOUT_NODE = "layout_node" as const;
type DesignTargetKind = typeof DESIGN_TARGET_PACKAGE_ITEM | typeof DESIGN_TARGET_LAYOUT_NODE;

function PackageDesignPanel({
  packages,
  selectedPackageId,
  onSelectPackage,
}: {
  packages: AdminPackageRow[];
  selectedPackageId: string;
  onSelectPackage: (id: string) => void;
}): JSX.Element {
  const { confirm, ConfirmDialogHost } = useConfirm();
  const [designTarget, setDesignTarget] = useState<DesignTargetKind>(DESIGN_TARGET_PACKAGE_ITEM);
  const [componentId, setComponentId] = useState("");
  const [layoutNodeId, setLayoutNodeId] = useState("");
  const [designName, setDesignName] = useState("");
  const [classname, setClassname] = useState("");
  const [tailwind, setTailwind] = useState("");
  const [reactionIntent, setReactionIntent] = useState("");
  const [cssTokenRefs, setCssTokenRefs] = useState<string[]>([]);
  const [responsiveTokenRefs, setResponsiveTokenRefs] = useState<ResponsiveTokenRules>({});
  const [inlineText, setInlineText] = useState("");
  const [linkHref, setLinkHref] = useState("");
  const [linkTarget, setLinkTarget] = useState("");
  const [status, setStatus] = useState<string | null>(null);
  const [saveOk, setSaveOk] = useState<boolean | null>(null);
  const [saving, setSaving] = useState(false);
  const [packageComponents, setPackageComponents] = useState<AdminPackageComponentRow[]>([]);
  const [layoutNodeOptions, setLayoutNodeOptions] = useState<LayoutNodeDesignOption[]>([]);
  const [savedDesigns, setSavedDesigns] = useState<SavedComponentDesignRow[]>([]);
  const [componentsLoadStatus, setComponentsLoadStatus] = useState<string | null>(null);
  const [layoutNodesLoadStatus, setLayoutNodesLoadStatus] = useState<string | null>(null);
  const [designsLoadStatus, setDesignsLoadStatus] = useState<string | null>(null);

  const selectedPackage = packages.find((p) => p.packageId === selectedPackageId);
  const selectedComponent = packageComponents.find((c) => c.componentId === componentId);
  const selectedLayoutNode = layoutNodeOptions.find((n) => n.nodeId === layoutNodeId);
  const designsForTarget = savedDesigns.filter((d) =>
    designTarget === DESIGN_TARGET_PACKAGE_ITEM
      ? d.componentId === componentId
      : d.layoutNodeId === layoutNodeId
  );
  const canSave = Boolean(
    selectedPackageId &&
      designName.trim() &&
      ((designTarget === DESIGN_TARGET_PACKAGE_ITEM && componentId) ||
        (designTarget === DESIGN_TARGET_LAYOUT_NODE && layoutNodeId)),
  );

  const toggleCssToken = (tokenKey: string) => {
    setCssTokenRefs((prev) =>
      prev.includes(tokenKey) ? prev.filter((k) => k !== tokenKey) : [...prev, tokenKey]
    );
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
  };

  useEffect(() => {
    if (!selectedPackageId) {
      setPackageComponents([]);
      setLayoutNodeOptions([]);
      setSavedDesigns([]);
      setComponentId("");
      setLayoutNodeId("");
      setComponentsLoadStatus(null);
      setLayoutNodesLoadStatus(null);
      setDesignsLoadStatus(null);
      setStatus(null);
      setSaveOk(null);
      return;
    }
    (async () => {
      setComponentsLoadStatus("部品一覧を読み込み中...");
      setLayoutNodesLoadStatus("layout ノードを読み込み中...");
      setDesignsLoadStatus("保存済みデザインを読み込み中...");
      const pkg = packages.find((p) => p.packageId === selectedPackageId);
      const [compBody, designBody, draftBody] = await Promise.all([
        dispatchAdminOp("ui_topology", "list_package_components", {
          packageId: selectedPackageId,
        }),
        dispatchAdminOp("component_style_design", "list", {
          packageId: selectedPackageId,
        }),
        pkg?.layoutId && pkg.routeKey
          ? dispatchAdminOp("ui_topology", "get_layout_patch_draft", {
            packageId: selectedPackageId,
            layoutId: pkg.layoutId,
            routeKey: pkg.routeKey,
          })
          : Promise.resolve(null),
      ]);
      const compData = compBody?.emission?.data;
      if (Array.isArray(compData)) {
        const rows = compData as AdminPackageComponentRow[];
        setPackageComponents(rows);
        setComponentsLoadStatus(rows.length === 0 ? "このパッケージに部品がありません。" : null);
        if (rows.length > 0 && !rows.some((r) => r.componentId === componentId)) {
          setComponentId(rows[0].componentId);
        }
      } else {
        setPackageComponents([]);
        setComponentsLoadStatus("部品一覧の取得に失敗しました。");
      }

      const draftData = draftBody?.emission?.data as Record<string, unknown> | undefined;
      const tensorPatchJson = typeof draftData?.tensorPatchJson === "string"
        ? draftData.tensorPatchJson
        : "";
      const parsedLayout = parseVisualLayoutPatchJson(tensorPatchJson);
      if (parsedLayout.ok) {
        const options = parsedLayout.value.nodes.map((n) => ({
          nodeId: n.nodeId,
          label: n.nodeKind === "structural_html" && n.htmlTag
            ? `<${n.htmlTag}>`
            : friendlyComponentLabel(n.componentKey),
          nodeKind: n.nodeKind,
          htmlTag: n.htmlTag,
        }));
        setLayoutNodeOptions(options);
        setLayoutNodesLoadStatus(options.length === 0 ? "layout ノードがありません。" : null);
        if (options.length > 0 && !options.some((o) => o.nodeId === layoutNodeId)) {
          setLayoutNodeId(options[0].nodeId);
        }
      } else {
        setLayoutNodeOptions([]);
        setLayoutNodesLoadStatus("layout ノードの取得に失敗しました。");
      }

      const designData = designBody?.emission?.data;
      if (Array.isArray(designData)) {
        const rows = designData.map((raw) => mapSavedDesignRow(raw as Record<string, unknown>));
        setSavedDesigns(rows);
        setDesignsLoadStatus(rows.length === 0 ? "保存済みデザインはまだありません。" : null);
      } else {
        setSavedDesigns([]);
        setDesignsLoadStatus("保存済みデザインの取得に失敗しました。");
      }
    })();
  }, [selectedPackageId, packages]);

  useEffect(() => {
    if (designTarget !== DESIGN_TARGET_PACKAGE_ITEM || !componentId) return;
    const saved = savedDesigns.find((d) => d.componentId === componentId);
    if (saved) {
      applySavedDesign(saved);
      return;
    }
    const comp = packageComponents.find((c) => c.componentId === componentId);
    if (comp) {
      setDesignName(defaultDesignName(comp.componentKey));
      setCssTokenRefs([]);
      setResponsiveTokenRefs({});
      setInlineText("");
      setLinkHref("");
      setLinkTarget("");
      setReactionIntent("");
      setClassname("");
      setTailwind("");
    }
  }, [designTarget, componentId, savedDesigns, packageComponents]);

  useEffect(() => {
    if (designTarget !== DESIGN_TARGET_LAYOUT_NODE || !layoutNodeId) return;
    const saved = savedDesigns.find((d) => d.layoutNodeId === layoutNodeId);
    if (saved) {
      applySavedDesign(saved);
      return;
    }
    const node = layoutNodeOptions.find((n) => n.nodeId === layoutNodeId);
    if (node) {
      setDesignName(`${node.nodeId}_design`);
      setCssTokenRefs([]);
      setResponsiveTokenRefs({});
      setInlineText(node.htmlTag === "a" ? "リンクテキスト" : "");
      setLinkHref("");
      setLinkTarget("");
      setReactionIntent("");
      setClassname("");
      setTailwind("");
    }
  }, [designTarget, layoutNodeId, savedDesigns, layoutNodeOptions]);

  const handleUpsertDesign = async () => {
    if (!canSave) {
      setStatus("パッケージ・部品・デザイン名を指定してください。");
      setSaveOk(false);
      return;
    }
    if (!(await confirm("部品デザインを保存します。よろしいですか？"))) {
      return;
    }
    setSaving(true);
    setStatus(null);
    setSaveOk(null);
    try {
      const body = await dispatchAdminOp("component_style_design", "upsert", {
        packageId: selectedPackageId,
        ...(designTarget === DESIGN_TARGET_PACKAGE_ITEM
          ? { componentId }
          : { layoutNodeId }),
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
      const ok = Boolean(body?.success);
      setSaveOk(ok);
      setStatus(ok
        ? "デザイン設定を保存しました。"
        : (body?.errors?.[0]?.message ?? "保存に失敗しました。"));
      if (ok) {
        const designBody = await dispatchAdminOp("component_style_design", "list", {
          packageId: selectedPackageId,
        });
        const designData = designBody?.emission?.data;
        if (Array.isArray(designData)) {
          setSavedDesigns(designData.map((raw) =>
            mapSavedDesignRow(raw as Record<string, unknown>)
          ));
          setDesignsLoadStatus(null);
        }
      }
    } catch (e) {
      setSaveOk(false);
      setStatus(`保存エラー: ${e}`);
    } finally {
      setSaving(false);
    }
  };

  const noPackage = !selectedPackageId;
  const noComponent = designTarget === DESIGN_TARGET_PACKAGE_ITEM && selectedPackageId && !componentId;
  const noLayoutNode = designTarget === DESIGN_TARGET_LAYOUT_NODE && selectedPackageId && !layoutNodeId;

  return (
    <section class="mb-4 rounded border border-slate-200 p-3 text-sm">
      <PackageScopeSelector
        packages={packages}
        selectedPackageId={selectedPackageId}
        onSelectPackage={onSelectPackage}
        heading="デザインを編集"
      />

      {noPackage && (
        <div class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900" role="alert">
          <strong>パッケージ未選択</strong> — 上でパッケージを選択してください。デザイン設定の保存はパッケージが必須です。
        </div>
      )}

      {!noPackage && noComponent && packageComponents.length === 0 && !componentsLoadStatus && (
        <div class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900" role="alert">
          <strong>部品なし</strong> — このパッケージに部品が登録されていません。「部品選択でパッケージ化」タブでパッケージ化してください。
        </div>
      )}

      {!noPackage && noLayoutNode && layoutNodeOptions.length === 0 && !layoutNodesLoadStatus && (
        <div class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900" role="alert">
          <strong>layout ノードなし</strong> — 「配置を編集」タブで structural HTML または部品を配置してください。
        </div>
      )}

      <p class="text-muted-xs mb-3">
        色・余白・フォント（cssTokenRefs）を canvas で確認しながら設定します。
        部品の位置・サイズは「配置を編集」タブの canvas で編集してください。
      </p>

      {componentsLoadStatus && (
        <p class="mb-2 text-xs text-slate-500">{componentsLoadStatus}</p>
      )}
      {layoutNodesLoadStatus && (
        <p class="mb-2 text-xs text-slate-500">{layoutNodesLoadStatus}</p>
      )}
      {designsLoadStatus && (
        <p class="mb-2 text-xs text-slate-500">{designsLoadStatus}</p>
      )}

      {/* TODO 3: 選択対象の常時表示ヘッダー */}
      {selectedPackageId && (
        <div class="mb-3 rounded border border-slate-200 bg-white px-3 py-2 text-xs">
          <div class="mb-0.5 text-[0.65rem] font-semibold uppercase tracking-wide text-slate-400">
            現在の編集対象
          </div>
          <div class="flex flex-wrap gap-x-4 gap-y-1">
            <span>
              <span class="text-slate-500">package: </span>
              <code class="font-mono">{selectedPackage?.packageKey ?? selectedPackageId.slice(0, 8)}</code>
            </span>
            {designTarget === DESIGN_TARGET_PACKAGE_ITEM && componentId && (
              <>
                <span>
                  <span class="text-slate-500">componentId: </span>
                  <code class="font-mono text-[0.65rem]">{componentId.slice(0, 8)}…</code>
                </span>
                {selectedComponent && (
                  <span>
                    <span class="text-slate-500">componentKey: </span>
                    <code class="font-mono">{selectedComponent.componentKey}</code>
                  </span>
                )}
              </>
            )}
            {designTarget === DESIGN_TARGET_LAYOUT_NODE && layoutNodeId && (
              <>
                <span>
                  <span class="text-slate-500">layoutNodeId: </span>
                  <code class="font-mono text-[0.65rem]">{layoutNodeId.slice(0, 8)}…</code>
                </span>
                {selectedLayoutNode && (
                  <span>
                    <span class="text-slate-500">ノード: </span>
                    <strong>{selectedLayoutNode.label}</strong>
                  </span>
                )}
                {selectedLayoutNode?.htmlTag && (
                  <span>
                    <span class="text-slate-500">htmlTag: </span>
                    <code class="font-mono">&lt;{selectedLayoutNode.htmlTag}&gt;</code>
                  </span>
                )}
              </>
            )}
          </div>
        </div>
      )}

      <div class="mb-3 flex flex-wrap gap-2">
        <label class="flex items-center gap-1 text-xs">
          <input
            type="radio"
            name="design-target"
            checked={designTarget === DESIGN_TARGET_PACKAGE_ITEM}
            onChange={() => setDesignTarget(DESIGN_TARGET_PACKAGE_ITEM)}
          />
          パッケージ部品
        </label>
        <label class="flex items-center gap-1 text-xs">
          <input
            type="radio"
            name="design-target"
            checked={designTarget === DESIGN_TARGET_LAYOUT_NODE}
            onChange={() => setDesignTarget(DESIGN_TARGET_LAYOUT_NODE)}
          />
          layout ノード（構造 HTML 含む）
        </label>
      </div>

      <div class="mb-4 grid gap-2 sm:grid-cols-2">
        {designTarget === DESIGN_TARGET_PACKAGE_ITEM
          ? (
            <label class="text-xs">
              部品
              <select
                class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                value={componentId}
                disabled={packageComponents.length === 0}
                onChange={(e) => setComponentId((e.target as HTMLSelectElement).value)}
              >
                <option value="">— 部品を選択 —</option>
                {packageComponents.map((c) => (
                  <option key={c.componentId} value={c.componentId}>
                    {c.componentKey} ({c.componentKind})
                  </option>
                ))}
              </select>
            </label>
          )
          : (
            <label class="text-xs">
              layout ノード
              <select
                class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
                value={layoutNodeId}
                disabled={layoutNodeOptions.length === 0}
                onChange={(e) => setLayoutNodeId((e.target as HTMLSelectElement).value)}
              >
                <option value="">— ノードを選択 —</option>
                {layoutNodeOptions.map((n) => (
                  <option key={n.nodeId} value={n.nodeId}>
                    {n.label} ({n.nodeId.slice(0, 8)}…)
                  </option>
                ))}
              </select>
            </label>
          )}
        <label class="text-xs">
          デザイン名（保存キー）
          <input
            class="mt-1 w-full rounded border px-2 py-1 text-xs"
            value={designName}
            onInput={(e) => setDesignName((e.target as HTMLInputElement).value)}
            placeholder="例: primary_card_design"
          />
        </label>
        {designsForTarget.length > 0 && (
          <label class="text-xs sm:col-span-2">
            保存済みデザインを読み込む
            <select
              class="mt-1 w-full rounded border px-2 py-1 text-xs"
              value={designName}
              onChange={(e) => {
                const name = (e.target as HTMLSelectElement).value;
                const design = designsForTarget.find((d) => d.name === name);
                if (design) applySavedDesign(design);
              }}
            >
              {designsForTarget.map((d) => (
                <option key={d.designId} value={d.name}>
                  {d.name}（トークン {d.cssTokenRefs.length} 件）
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      {designTarget === DESIGN_TARGET_PACKAGE_ITEM && selectedComponent && (
        <div class="mb-4">
          <ComponentDesignPreviewCanvas
            componentKey={selectedComponent.componentKey}
            componentKind={selectedComponent.componentKind}
            cssTokenRefs={cssTokenRefs}
            sampleText={inlineText}
            reactionIntent={reactionIntent}
            onSampleTextChange={setInlineText}
          />
        </div>
      )}

      {designTarget === DESIGN_TARGET_LAYOUT_NODE && selectedLayoutNode && (
        <div class="mb-4 rounded border border-emerald-100 bg-emerald-50 p-3 text-xs text-emerald-900">
          編集対象: <strong>{selectedLayoutNode.label}</strong>
          {selectedLayoutNode.htmlTag === "a" && " — linkHref / inlineText を設定できます"}
        </div>
      )}

      <div class="mb-4 rounded border border-slate-100 bg-slate-50 p-2">
        <p class="mb-1 text-xs font-semibold text-slate-700">
          色・余白・フォント — CSS 辞書トークン（チェックで選択）
        </p>
        <p class="text-muted-xs mb-2">
          チェックしたトークンが上の canvas に即時反映されます。保存は「デザイン設定を保存」で行います。
        </p>
        <CssTokenPicker selectedTokenRefs={cssTokenRefs} onToggle={toggleCssToken} />
      </div>

      <label class="mb-4 block text-xs">
        インラインテキスト
        <input
          class="mt-1 w-full rounded border px-2 py-1 text-xs"
          value={inlineText}
          onInput={(e) => setInlineText((e.target as HTMLInputElement).value)}
          placeholder="表示テキスト / 子テキストノード"
        />
      </label>

      <div class="mb-4 grid gap-2 sm:grid-cols-2">
        <label class="text-xs">
          リンク URL（href）
          <input
            class="mt-1 w-full rounded border px-2 py-1 text-xs"
            value={linkHref}
            onInput={(e) => setLinkHref((e.target as HTMLInputElement).value)}
            placeholder="https://..."
          />
        </label>
        <label class="text-xs">
          リンク target
          <input
            class="mt-1 w-full rounded border px-2 py-1 text-xs"
            value={linkTarget}
            onInput={(e) => setLinkTarget((e.target as HTMLInputElement).value)}
            placeholder="_blank 等"
          />
        </label>
      </div>

      <div class="mb-4">
        <ResponsiveTokenRuleEditor
          rules={responsiveTokenRefs}
          onChange={setResponsiveTokenRefs}
        />
      </div>

      <label class="mb-4 block text-xs">
        リアクション意図（hover / focus 等）
        <input
          class="mt-1 w-full rounded border px-2 py-1 text-xs"
          value={reactionIntent}
          onInput={(e) => setReactionIntent((e.target as HTMLInputElement).value)}
          placeholder="例: ホバーで背景を primary に変化"
        />
      </label>

      {selectedPackageId && (
        <details class="mb-4 rounded border border-slate-200 p-3">
          <summary class="cursor-pointer text-xs font-semibold text-slate-700">
            パッケージ配線（イベント接続・詳細）
          </summary>
          <PackageWiringEditor
            selectedPackageId={selectedPackageId}
            packageComponents={packageComponents}
          />
        </details>
      )}

      <AdvancedManualOverride title="上級者向け — classname / tailwind 手入力（補助メモのみ・保存対象外）">
        <p class="text-muted-xs mb-2">
          通常は cssTokenRefs を使ってください。入力した文字列は補助メモとしてのみ保存され、投影の正式参照にはなりません。
        </p>
        <label class="mb-2 block text-xs">
          classname（補助メモ）
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
            value={classname}
            onInput={(e) => setClassname((e.target as HTMLInputElement).value)}
            placeholder="例: btn-primary（cssTokenRefs を優先）"
          />
        </label>
        <label class="block text-xs">
          tailwind（非正本・補助メモ）
          <input
            class="mt-1 w-full rounded border px-2 py-1 font-mono text-xs"
            value={tailwind}
            onInput={(e) => setTailwind((e.target as HTMLInputElement).value)}
            placeholder="辞書トークンを優先してください"
          />
        </label>
      </AdvancedManualOverride>

      {!canSave && selectedPackageId && (
        <p class="mt-2 text-xs text-amber-800">
          {!componentId ? "部品を選択してください。" : "デザイン名を入力してください。"}
        </p>
      )}

      <button
        type="button"
        class={`btn-primary mt-3 text-xs ${(!canSave || saving) ? "opacity-50 cursor-not-allowed" : ""}`}
        onClick={handleUpsertDesign}
        disabled={!canSave || saving}
        aria-disabled={!canSave || saving}
      >
        {saving ? "保存中…" : "デザイン設定を保存"}
      </button>

      {status && (
        <p
          class={`mt-2 text-xs font-semibold ${saveOk === true ? "text-green-700" : saveOk === false ? "text-red-700" : "text-slate-700"}`}
          role={saveOk === false ? "alert" : "status"}
        >
          {status}
        </p>
      )}

      <ConfirmDialogHost />
    </section>
  );
}


/**
 * Canvas-first workspace (SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract).
 * No separate layout/design/visual tabs — single workspace with docked panels.
 *
 * Layout:
 *   - Bucket panel (collapsible): Phase A component registration + package generation
 *   - Canvas workspace (LayoutBuilderSection): left palette + center canvas + right inspector
 *   - Design inspector panel (collapsible, opened from canvas inspector): cssTokenRefs etc.
 *   - Reference sections (catalog/CI/CSS): collapsed by default
 */
export default function UiBuilderAdmin(): JSX.Element {
  const [packages, setPackages] = useState<AdminPackageRow[]>([]);
  const [selectedPackageId, setSelectedPackageId] = useState("");
  const [bucketPanelOpen, setBucketPanelOpen] = useState(true);
  const [designPanelOpen, setDesignPanelOpen] = useState(false);
  const selectedPackage = packages.find((p) => p.packageId === selectedPackageId);

  const reloadPackages = async (): Promise<AdminPackageRow[]> => {
    const body = await dispatchAdminOp("ui_topology", "list_packages");
    const data = body?.emission?.data;
    const list = Array.isArray(data) ? data as AdminPackageRow[] : [];
    setPackages(list);
    return list;
  };

  useEffect(() => {
    reloadPackages();
  }, []);

  const handlePackaged = (handoff: PackagedHandoff) => {
    setSelectedPackageId(handoff.packageId);
    setBucketPanelOpen(false);
    reloadPackages();
  };

  const handleWorkspaceNavigate = (panel: WorkspacePanel) => {
    if (panel === "bucket") setBucketPanelOpen(true);
    if (panel === "design") setDesignPanelOpen(true);
  };

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

      <div
        class="mb-3 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900"
        role="note"
      >
        Step 4 の編集ルートは<strong> パッケージのみ</strong>です。配置・デザイン設定・配線は
        パッケージ選択後にキャンバスワークスペースで編集してください。
      </div>

      {/* Phase A: bucket panel (collapsible) */}
      <details
        class="mb-3 rounded border border-blue-200 bg-blue-50"
        open={bucketPanelOpen}
        onToggle={(e: Event) => setBucketPanelOpen((e.target as HTMLDetailsElement).open)}
      >
        <summary class="cursor-pointer px-3 py-2 text-sm font-semibold text-blue-900">
          {UX_UI_BUILDER_TAB_LABELS.bucket} — 部品登録・パッケージ化
        </summary>
        <div class="px-3 pb-3">
          <BucketSection onNavigate={handleWorkspaceNavigate} onPackaged={handlePackaged} />
        </div>
      </details>

      {/* Phase B: canvas workspace — layout editor + design inspector */}
      <div class="mb-4">
        <div class="mb-2 flex flex-wrap items-center gap-2 rounded border border-slate-200 bg-slate-50 px-3 py-2">
          <strong class="text-sm text-slate-800">{UX_LAYOUT_EDITOR_SURFACE}</strong>
          <label class="flex items-center gap-1 text-xs text-slate-600">
            パッケージ:
            <select
              class="rounded border px-2 py-0.5 font-mono text-xs"
              value={selectedPackageId}
              onChange={(e) => {
                const id = (e.target as HTMLSelectElement).value;
                setSelectedPackageId(id);
                reloadPackages();
              }}
            >
              <option value="">— 選択 —</option>
              {packages.map((p) => (
                <option key={p.packageId} value={p.packageId}>
                  {p.packageKey}{p.routeKey ? ` (${p.routeKey})` : ""}
                </option>
              ))}
            </select>
          </label>
          {selectedPackage && (
            <span class="font-mono text-[0.65rem] text-slate-400">
              {selectedPackage.packageId.slice(0, 8)}…
            </span>
          )}
          <button
            type="button"
            onClick={() => setDesignPanelOpen((v) => !v)}
            class={`ml-auto rounded px-2 py-1 text-xs font-medium border ${
              designPanelOpen
                ? "border-blue-600 bg-blue-600 text-white"
                : "border-blue-300 bg-white text-blue-700 hover:bg-blue-50"
            }`}
            aria-pressed={designPanelOpen}
          >
            {designPanelOpen ? "デザインインスペクタを閉じる" : "デザインインスペクタを開く"}
          </button>
        </div>

        <LayoutBuilderSection
          onNavigate={handleWorkspaceNavigate}
          scopedPackageId={selectedPackageId}
          scopedRouteKey={selectedPackage?.routeKey}
          scopedLayoutId={selectedPackage?.layoutId}
        />
      </div>

      {/* Design inspector panel (docked, selection-driven, opened by canvas inspector or toolbar) */}
      {designPanelOpen && (
        <div class="mb-4 rounded border border-slate-300 bg-white shadow-sm">
          <div class="flex items-center justify-between border-b border-slate-200 px-3 py-2">
            <strong class="text-sm text-slate-800">{UX_DESIGN_EDITOR_SURFACE}</strong>
            <button
              type="button"
              onClick={() => setDesignPanelOpen(false)}
              class="rounded px-2 py-0.5 text-xs text-slate-500 hover:bg-slate-100"
            >
              ✕ 閉じる
            </button>
          </div>
          <PackageDesignPanel
            packages={packages}
            selectedPackageId={selectedPackageId}
            onSelectPackage={setSelectedPackageId}
          />
        </div>
      )}

      {/* Reference sections */}
      <details class="mb-3 mt-4 rounded border border-slate-200 p-3 text-sm">
        <summary class="cursor-pointer font-medium text-slate-700">
          参照専用: コンポーネントカタログ（編集ルートではない）
        </summary>
        <p class="mt-2 text-xs text-slate-500">
          部品の登録は上の「{UX_UI_BUILDER_TAB_LABELS.bucket}」パネルから行います。ここは分類・候補の参照のみです。
        </p>
        <div class="mt-2">
          <PrimitiveCatalog />
        </div>
      </details>
      <details class="mb-4 rounded border border-slate-200 p-3 text-sm">
        <summary class="cursor-pointer font-medium text-slate-700">
          参照専用: CI ガイダンス（編集ルートではない）
        </summary>
        <p class="mt-2 text-xs text-slate-500">
          保存前の注意喚起です。配置・デザインの編集はキャンバスワークスペースで行います。
        </p>
        <div class="mt-2">
          <CiAttentionGuidanceSection />
        </div>
      </details>
      <details class="mb-4 rounded border border-slate-200 p-3 text-sm">
        <summary class="cursor-pointer font-medium text-slate-700">
          参照専用: CSS 辞書トークン一覧（保存はデザインインスペクタで行います）
        </summary>
        <p class="mt-2 text-xs text-slate-500">
          ここでは選択しても保存されません。cssTokenRefs の保存はデザインインスペクタ → cssTokenRefs セクションを使ってください。
        </p>
        <div class="mt-2">
          <CssTokenSelectorSection />
        </div>
      </details>
    </main>
  );
}
