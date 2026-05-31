import { useEffect, useRef, useState } from "preact/hooks";
import { JSX } from "preact";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import { CSS_DICTIONARY_TOKENS, resolveCssTokenValue } from "../runtime/cssDictionary.ts";
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
import UiBuilderFlowStepper from "../components/UiBuilderFlowStepper.tsx";
import {
  snapToGrid,
  buildVisualLayoutPatchJson,
  wouldCreateVisualParentCycle,
} from "../runtime/visualLayoutUtils.ts";

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
 * SSOT: docs/registrar-admin-ui-specification.md §2.5, §7
 */

const SESSION_TOKEN_KEY = "demo_jwt_token";

// ─── ユーティリティ ──────────────────────────────────────────────────────────

function getAuthHeaders(): Record<string, string> {
  const token =
    typeof globalThis.sessionStorage !== "undefined"
      ? sessionStorage.getItem(SESSION_TOKEN_KEY)
      : null;
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  return headers;
}

async function dispatchAdminOp(layer: string, action: string, payload?: unknown) {
  const res = await fetch("/api/dispatch", {
    method: "POST",
    headers: getAuthHeaders(),
    body: JSON.stringify({
      operationType: "admin",
      target: "admin",
      layer,
      action,
      payload: payload ?? null,
    }),
  });
  return await res.json();
}

// ─── 型定義 ──────────────────────────────────────────────────────────────────

type BucketItem = {
  bucketItemId: string;
  componentKey: string;
  sourcePath: string;
  componentKind: string;
  status: string;
};

type ValidationError = { code: string; message: string; field?: string; nodeId?: string; componentKey?: string };

type DraftNode = {
  nodeId: string;
  componentKey: string;
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

type TabId = "ci" | "catalog" | "bucket" | "css" | "layout";

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

// ─── タブバー ─────────────────────────────────────────────────────────────────

function TabBar({
  tabs,
  activeTab,
  onSelect,
}: {
  tabs: { id: TabId; label: string; hint?: string }[];
  activeTab: TabId;
  onSelect: (id: TabId) => void;
}): JSX.Element {
  return (
    <div>
      <div class="tab-bar">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => onSelect(tab.id)}
            class={activeTab === tab.id ? "tab-active" : "tab-inactive"}
            title={tab.hint}
          >
            {tab.label}
          </button>
        ))}
      </div>
      {tabs.find((t) => t.id === activeTab)?.hint && (
        <p class="text-muted-xs mt-1 mb-2">
          {tabs.find((t) => t.id === activeTab)!.hint}
        </p>
      )}
    </div>
  );
}

// ─── ヘルパー ─────────────────────────────────────────────────────────────────

function buildLayoutPatchJson(nodes: DraftNode[], layoutClassRefs: string[] = []): string {
  return JSON.stringify(
    {
      grid: { cols: 12 },
      ...(layoutClassRefs.length > 0 ? { layoutClassRefs } : {}),
      nodes: nodes.map((n) => ({
        nodeId: n.nodeId,
        componentKey: n.componentKey,
        ...(n.isDraftOnly ? { _draftOnly: true } : {}),
        ...(n.componentId ? { componentId: n.componentId } : {}),
        ...(n.packageId ? { packageId: n.packageId } : {}),
        ...(n.layoutId ? { layoutId: n.layoutId } : {}),
        ...(n.wiringId ? { wiringId: n.wiringId } : {}),
        ...(n.tensorId ? { tensorId: n.tensorId } : {}),
        slotKey: n.slotKey || null,
        orderIndex: n.orderIndex,
        parentNodeId: n.parentNodeId || null,
        gridCol: n.gridCol,
        gridRow: n.gridRow,
      })),
    },
    null,
    2
  );
}

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
const ERROR_CODE_FIX: Record<string, { cause: string; suggestion: string; navigateTo?: TabId }> = {
  DRAFT_ONLY_NODES: {
    cause: "まだ使えない部品が含まれています",
    suggestion: "「部品登録」タブで対象の部品を配置可能にしてください",
    navigateTo: "bucket",
  },
  LAYOUT_NOT_FOUND: {
    cause: "レイアウトIDが見つかりません",
    suggestion: "ルート/レイアウト選択を確認し、再選択してください",
  },
  ROUTE_NOT_FOUND: {
    cause: "ルートキーが存在しません",
    suggestion: "先にコンポーネントをバケット登録してルートを作成してください",
  },
  CSS_TOKEN_INVALID: {
    cause: "CSSトークン参照が無効です",
    suggestion: "「CSS設定」タブで正しいトークンを選択してください",
    navigateTo: "css",
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
  },
  GENERATE_FAILED: {
    cause: "パッケージ化に失敗しました",
    suggestion: "バックエンド接続を確認し、ルートキーが正しいか再確認してください",
  },
  PROMOTE_FAILED: {
    cause: "配置可能化に失敗しました",
    suggestion: "先にパッケージ化を完了してから実行してください",
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

function resolveBucketStatus(
  componentKey: string,
  bucketItems: BucketItem[],
  promotedKeys: Set<string>,
): { label: string; variant: "ok" | "warn" | "error" | "info"; bucketItemId?: string } {
  if (promotedKeys.has(componentKey)) {
    return { label: "配置可能（登録完了）", variant: "ok" };
  }
  const item = bucketItems.find((b) => b.componentKey === componentKey);
  if (!item) return { label: "未登録（使用不可）", variant: "info" };
  const labelMap: Record<string, string> = {
    promoted: "配置可能（登録完了）",
    packaging: "パッケージ化中",
    bucketed: "部品登録済み（準備中）",
  };
  const variant =
    item.status === "promoted" ? "ok"
    : item.status === "packaging" ? "info"
    : item.status === "bucketed" ? "warn"
    : "info";
  return { label: labelMap[item.status] ?? item.status, variant, bucketItemId: item.bucketItemId };
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
      nextAction = "ドラフトのみノードをプロモートするか削除してください";
    } else {
      nextAction = "エラーを修正してから再実行してください";
    }
  } else if (action === "preview") {
    nextAction = "問題なければ「バリデート」を実行";
  } else if (action === "validate") {
    nextAction = "問題なければ「適用」を実行";
  } else {
    nextAction = "レイアウトパッチが適用されました";
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
          エラー — 「エラー — 修正方法」を確認してください。まだ使えない部品がある場合は部品登録タブへ戻ってください。
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
  onNavigate?: (tab: TabId) => void;
}): JSX.Element | null {
  if (errors.length === 0) return null;
  const shownNavigateTabs = new Set<TabId>();
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
                  {e.componentKey && <span>コンポーネント: <code>{friendlyComponentLabel(e.componentKey)}</code>{" "}</span>}
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
            const label = tab === "bucket" ? "→ 部品登録タブへ移動" : "→ CSS設定タブへ移動";
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
  selectedTokenRefs,
  layoutClassRefError,
  onNavigate,
}: {
  canPatch: boolean;
  effectiveRouteKey: string;
  effectiveLayoutId: string;
  draftNodes: DraftNode[];
  selectedTokenRefs: string[];
  layoutClassRefError: string | null;
  onNavigate?: (tab: TabId) => void;
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
              部品登録タブで確認する
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
              部品登録タブで修正する →
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
            visual canvas: {draftNodes.length} ノード
            {customPositionedCount > 0
              ? ` (${customPositionedCount} 件カスタム配置)`
              : draftNodes.length > 0 ? " (デフォルト配置)" : ""}
          </span>
        </li>
        <li class="flex items-start gap-2">
          <span class="text-muted-xs">i</span>
          <span class="text-muted-xs">
            CSS トークン: {selectedTokenRefs.length} 件選択済み。
            ref エラーは backend validate 結果に表示されます。
          </span>
        </li>
      </ul>
      {allClear && (
        <p class="mt-2 text-green-700 font-semibold text-xs">
          すべてのローカルチェック通過。確認 → 検証 → 保存反映 の順で実行してください。
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
        ルートキー
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
        <p class="text-sm text-yellow-700">
          候補なし — 先にバケット → プロモートで UI topology を登録してください。
        </p>
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
          <option value="">対象コンポーネント（すべて）</option>
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
        topology layout projection 専用 class ref（<code>docs/design/topology-layout-class-ssot.yaml</code>）。
        admin 画面装飾ではありません。保存は classKey のみ — raw className / Tailwind は通常導線では使いません。
      </p>
      <div class="mb-2 flex flex-wrap gap-2">
        <input
          value={keyFilter}
          onInput={(e) => setKeyFilter((e.target as HTMLInputElement).value)}
          placeholder="classKey 検索"
          class="input-mono flex-1 text-xs"
        />
        <select value={categoryFilter} onChange={(e) => setCategoryFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">category（すべて）</option>
          {categories.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        <select value={scopeFilterState} onChange={(e) => setScopeFilterState((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">projectionScope（すべて）</option>
          {scopes.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select value={roleFilter} onChange={(e) => setRoleFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">semanticRole（すべて）</option>
          {roles.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
      </div>

      {selectedClassRefs.length > 0 && (
        <div class="mb-2 rounded border border-blue-200 bg-blue-50 p-2">
          <strong class="text-xs">選択済み layoutClassRefs ({selectedClassRefs.length})</strong>
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
              {["選択", "classKey", "className", "category", "scope", "allowedFor"].map((h) => (
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

// ─── バケット管理セクション ───────────────────────────────────────────────────

function BucketSection(): JSX.Element {
  const [items, setItems] = useState<BucketItem[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [errors, setErrors] = useState<ValidationError[]>([]);
  const [loading, setLoading] = useState(false);
  const [routeKey, setRouteKey] = useState("");
  const [manualRouteKey, setManualRouteKey] = useState("");
  const [selectedId, setSelectedId] = useState("");
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
        setStatus(`${combined.length} 件のバケットアイテムをロードしました。`);
      } else {
        setErrors(bucketedBody?.errors ?? packagingBody?.errors ?? [{ code: "BUCKET_LOAD_FAILED", message: "バケットのロードに失敗しました。" }]);
        setStatus("バケットのロードに失敗しました。");
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

  const selectCatalog = (key: string) => {
    setSelectedCatalogKey(key);
    const entry = COMPONENT_CATALOG_ENTRIES.find((c) => c.componentKey === key);
    if (!entry) return;
    const bucketStatus = resolveBucketStatus(key, items, promotedKeys);
    if (bucketStatus.bucketItemId) {
      setSelectedId(bucketStatus.bucketItemId);
    }
  };

  const handleCreateFromCatalog = async () => {
    if (!selectedCatalog) {
      setStatus("カタログからコンポーネントを選択してください。");
      return;
    }
    const existing = resolveBucketStatus(selectedCatalog.componentKey, items, promotedKeys);
    if (existing.label === "配置可能（登録完了）" || existing.label === "部品登録済み（準備中）" || existing.label === "パッケージ化中") {
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

  const handleCreateManual = async (componentKey: string, sourcePath: string, componentKind: string) => {
    if (!componentKey || !sourcePath || !componentKind) {
      setStatus("componentKey / sourcePath / componentKind は必須です。");
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
        metadataJson: "{}",
      });
      if (body?.emission?.data?.bucketItemId) {
        setStatus(`${componentKey} を登録しました`);
        setSelectedId(body.emission.data.bucketItemId);
        await loadBucket();
      } else {
        setErrors(body?.errors ?? []);
        setStatus("バケット作成に失敗しました。");
      }
    } finally {
      setLoading(false);
    }
  };

  const handleGenerate = async () => {
    if (!selectedId || !effectiveRouteKey) {
      setStatus("バケットアイテムを選択し、ルートキーを選択してください。");
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

  return (
    <div>
      <p class="text-muted mb-3">
        カタログからコンポーネントを選択してバケット登録 intent を送信します。パッケージジェネレーターが
        UI topology テンソルエンティティへプロモートします。
      </p>

      {candidateErrors.length > 0 && (
        <ValidationErrorPanel errors={candidateErrors} title="候補ロードエラー" />
      )}

      <Accordion title="カタログからバケット登録" defaultOpen={true}>
        <div class="mb-2 flex flex-wrap gap-2">
          <input
            value={catalogFilter}
            onInput={(e) => setCatalogFilter((e.target as HTMLInputElement).value)}
            placeholder="componentKey 検索"
            class="input-mono flex-1 text-xs"
          />
          <select value={kindFilter} onChange={(e) => setKindFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
            <option value="">種別（すべて）</option>
            {catalogKinds.map((k) => <option key={k} value={k}>{k}</option>)}
          </select>
          <select value={lifecycleFilter} onChange={(e) => setLifecycleFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
            <option value="">ライフサイクル（すべて）</option>
            <option value="code_only_drift">code_only_drift</option>
          </select>
          <button onClick={loadBucket} disabled={loading} class="btn-secondary">バケット再ロード</button>
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
                        type="radio"
                        name="catalogEntry"
                        checked={selectedCatalogKey === c.componentKey}
                        onChange={() => selectCatalog(c.componentKey)}
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

        {selectedCatalog && (
          <div class="mt-2 rounded border border-gray-200 bg-gray-50 p-2 text-sm">
            <strong>選択中:</strong> <code>{selectedCatalog.componentKey}</code>
            <span class="ml-2 text-muted-xs">{selectedCatalog.componentKind}</span>
            <div class="mt-1 font-mono text-xs text-gray-600">{selectedCatalog.sourcePath}</div>
            <button
              onClick={handleCreateFromCatalog}
              disabled={loading || resolveBucketStatus(selectedCatalog.componentKey, items, promotedKeys).label !== "未登録（使用不可）"}
              class="btn-primary mt-2"
            >
              バケットに登録
            </button>
          </div>
        )}

        <AdvancedManualOverride title="manual override — カタログ外バケット作成">
          <ManualBucketCreateForm onCreate={handleCreateManual} loading={loading} />
        </AdvancedManualOverride>
      </Accordion>

      {(items.length > 0 || selectedId) && (
        <Accordion title="生成 / プロモート" defaultOpen={true}>
          <div class="table-wrap mb-3">
            <table class="table font-mono text-sm">
              <thead>
                <tr>
                  {["選択", "componentKey", "kind", "status"].map((h) => (
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
                      <StatusBadge
                        text={item.status}
                        variant={item.status === "packaging" ? "info" : "warn"}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <label class="mb-2 flex flex-col gap-0.5 text-sm">
            ルートキー（候補から選択）
            <select
              value={routeKey}
              onChange={(e) => {
                setRouteKey((e.target as HTMLSelectElement).value);
                setManualRouteKey("");
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
            <p class="text-sm text-yellow-700 mb-2">
              ルート候補なし — 初回プロモート時は advanced で新規ルートを指定してください。
            </p>
          )}

          <AdvancedManualOverride title="manual override — 新規 routeKey">
            <input
              value={manualRouteKey}
              onInput={(e) => setManualRouteKey((e.target as HTMLInputElement).value)}
              placeholder="例: /admin/ui-builder"
              class="input-mono w-full text-xs"
            />
          </AdvancedManualOverride>

          {selectedItem && effectiveRouteKey && (
            <p class="text-muted-xs mt-2">
              次: <code>{selectedItem.componentKey}</code> を <code>{effectiveRouteKey}</code> へ
              {selectedItem.status === "bucketed" ? " パッケージ化 → 配置可能化" : " 配置可能化"}
            </p>
          )}

          <div class="mt-2 flex flex-wrap gap-2">
            <button
              onClick={handleGenerate}
              disabled={loading || !selectedId || !effectiveRouteKey}
              class="btn-primary"
            >
              パッケージ化する
            </button>
            <button
              onClick={handlePromote}
              disabled={loading || !selectedId || !effectiveRouteKey}
              class="btn-success"
            >
              配置可能にする
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
      {errors.length > 0 && <ActionableValidationErrorPanel errors={errors} title="操作エラー" />}
    </div>
  );
}

function ManualBucketCreateForm({
  onCreate,
  loading,
}: {
  onCreate: (componentKey: string, sourcePath: string, componentKind: string) => void;
  loading: boolean;
}): JSX.Element {
  const [componentKey, setComponentKey] = useState("");
  const [sourcePath, setSourcePath] = useState("");
  const [componentKind, setComponentKind] = useState("primitive");
  return (
    <div class="flex flex-wrap gap-2">
      <input value={componentKey} onInput={(e) => setComponentKey((e.target as HTMLInputElement).value)} placeholder="componentKey" class="input-mono w-auto text-xs" />
      <input value={sourcePath} onInput={(e) => setSourcePath((e.target as HTMLInputElement).value)} placeholder="sourcePath" class="input-mono flex-1 text-xs" />
      <input value={componentKind} onInput={(e) => setComponentKind((e.target as HTMLInputElement).value)} placeholder="componentKind" class="input-mono w-auto text-xs" />
      <button type="button" onClick={() => onCreate(componentKey, sourcePath, componentKind)} disabled={loading} class="btn-secondary text-xs">
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

function VisualLayoutNode({
  node,
  isSelected,
  isDragging,
  displayX,
  displayY,
  displayW,
  displayH,
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
      class={`absolute select-none rounded border-2 font-mono text-xs transition-shadow ${
        isSelected
          ? "border-blue-600 shadow-lg ring-2 ring-blue-300 ring-offset-1"
          : node.isDraftOnly
          ? "border-yellow-300 hover:border-yellow-500"
          : "border-blue-200 hover:border-blue-400"
      } ${node.isDraftOnly ? "bg-yellow-50" : "bg-white"} focus:outline-none focus-visible:ring-2 focus-visible:ring-blue-500`}
      style={{
        left: `${displayX}px`,
        top: `${displayY}px`,
        width: `${displayW}px`,
        height: `${displayH}px`,
        zIndex: isSelected ? 10 : 1,
        cursor: isDragging ? "grabbing" : "grab",
        opacity: isDragging ? 0.75 : 1,
      }}
      onClick={(e: Event) => { (e as MouseEvent).stopPropagation(); onSelect(); }}
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
      <div class="flex h-full flex-col overflow-hidden p-1">
        <div class="truncate font-bold leading-tight" title={node.componentKey}>
          {friendlyComponentLabel(node.componentKey)}
        </div>
        {node.isDraftOnly && (
          <span
            class="text-[0.58rem] text-yellow-700 font-medium"
            title="部品登録タブでプロモートしてから apply してください"
          >⚠ まだ使えない部品 — 適用不可</span>
        )}
        {node.slotKey && (
          <span class="truncate text-[0.58rem] text-gray-500">配置: {node.slotKey}</span>
        )}
        <span class="mt-auto text-[0.55rem] text-gray-300">
          {displayW}×{displayH}
        </span>
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
}: {
  draftNodes: DraftNode[];
  selectedNodeId: string | null;
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
      aria-label="レイアウトキャンバス — ノードをドラッグして配置。キーボード: 矢印キーで移動、Delete で削除、Tab でノードを切り替え"
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
            <p class="text-base font-semibold text-gray-500">キャンバスが空です</p>
            <p class="mt-1 text-sm text-gray-400">
              左のパレットからドラッグするか、下のボタンでコンポーネントを追加してください
            </p>
          </div>
          {onAddFromEmptyState && (
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
        return (
          <VisualLayoutNode
            key={node.nodeId}
            node={node}
            isSelected={node.nodeId === selectedNodeId}
            isDragging={liveDragNodeId === node.nodeId}
            displayX={live?.x ?? node.x}
            displayY={live?.y ?? node.y}
            displayW={live?.width ?? node.width}
            displayH={live?.height ?? node.height}
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
  onDelete,
}: {
  draftNodes: DraftNode[];
  selectedNodeId: string | null;
  onSelect: (nodeId: string) => void;
  onMoveUp: (nodeId: string) => void;
  onMoveDown: (nodeId: string) => void;
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
                {friendlyComponentLabel(node.componentKey)}
              </span>
              <div class="flex shrink-0 gap-0.5">
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
  onClose,
}: {
  node: DraftNode;
  draftNodes: DraftNode[];
  slotKeyCandidates: string[];
  onUpdate: (updates: Partial<DraftNode>) => void;
  onCommit: (updates: Partial<DraftNode>, label: string) => void;
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
    onCommit({ parentNodeId: parentId }, "親コンポーネントを変更");
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
      aria-label={`${friendlyComponentLabel(node.componentKey)} のプロパティ`}
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
        <div class="font-bold text-blue-900">{friendlyComponentLabel(node.componentKey)}</div>
        {node.isDraftOnly && (
          <div class="mt-0.5 text-[0.65rem] font-medium text-yellow-700">
            ⚠ 未登録コンポーネント — 適用前にプロモートが必要です
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

        {/* Gap 4: "親コンポーネント" instead of "parentNodeId" */}
        <label class="flex flex-col gap-0.5">
          <span class="text-[0.65rem] text-gray-600">親コンポーネント</span>
          <select
            value={node.parentNodeId ?? ""}
            onChange={(e) => handleParentChange((e.target as HTMLSelectElement).value)}
            class="input px-1 py-0.5 text-xs"
            aria-label="親コンポーネントを選択"
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
      </fieldset>

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

// ─── パレット ─────────────────────────────────────────────────────────────────

function LayoutPalette({
  onDragStart,
  onAddToCanvas,
  entries,
  status,
}: {
  onDragStart: (entry: PaletteEntry) => void;
  onAddToCanvas: (entry: PaletteEntry) => void;
  entries: PaletteEntry[];
  status: string | null;
}): JSX.Element {
  const [filter, setFilter] = useState("");
  const filtered = filter
    ? entries.filter((e) =>
        e.componentKey.toLowerCase().includes(filter.toLowerCase()) ||
        e.componentKind.toLowerCase().includes(filter.toLowerCase())
      )
    : entries;

  return (
    <div class="w-44 shrink-0 overflow-y-auto rounded-lg border border-gray-200 bg-gray-50 p-2 max-h-[480px]">
      <h4 class="mb-1 text-sm font-semibold">コンポーネント</h4>

      {/* Gap 5: Filter so users can find components without scrolling */}
      <input
        value={filter}
        onInput={(e) => setFilter((e.target as HTMLInputElement).value)}
        placeholder="絞り込み..."
        class="mb-1 w-full rounded border border-gray-300 px-2 py-1 text-xs focus:border-blue-400 focus:outline-none"
        aria-label="パレットのコンポーネントを絞り込み"
      />

      <p class="mb-1.5 text-[0.62rem] text-gray-500">
        ドラッグ or「追加」ボタンで配置
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

// ─── レイアウトビルダーセクション v2 + UX強化 ─────────────────────────────────

function LayoutBuilderSection({ onNavigate }: { onNavigate?: (tab: TabId) => void }): JSX.Element {
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
  const [liveResizePos, setLiveResizePos] = useState<{ nodeId: string; x: number; y: number; width: number; height: number } | null>(null);
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const dragState = useRef<CanvasDragState>(null);
  const resizeState = useRef<CanvasResizeState>(null);

  // ── CSS / layout class refs ──────────────────────────────────────────────
  const [selectedTokenRefs, setSelectedTokenRefs] = useState<string[]>([]);
  const [selectedLayoutClassRefs, setSelectedLayoutClassRefs] = useState<string[]>([]);
  const [manualLayoutClassRef, setManualLayoutClassRef] = useState("");
  const [layoutClassRefError, setLayoutClassRefError] = useState<string | null>(null);

  // ── patch / status ───────────────────────────────────────────────────────
  const [patchSummary, setPatchSummary] = useState<LayoutPatchSummary | null>(null);
  const [patchErrors, setPatchErrors] = useState<{ code: string; message: string }[]>([]);
  const [debugJson, setDebugJson] = useState<string | null>(null);
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
  const topologyPreviewClasses = selectedLayoutClassRefs.length > 0
    ? resolveTopologyLayoutClassRefs(selectedLayoutClassRefs)
    : null;
  const effectiveLayoutId = manualLayoutId.trim() || layoutId;
  const effectiveRouteKey = manualRouteKey.trim() || routeKey;
  const selectedLayout = layoutCandidates.find(
    (c) => c.layoutId === effectiveLayoutId && c.routeKey === effectiveRouteKey,
  );
  const dbSlotKeys = selectedLayout?.slotKeys ?? [];
  const slotKeyCandidates = buildSlotKeyCandidates(draftNodes, dbSlotKeys);
  const selectorsDisabled = candidateErrors.length > 0 || paletteLoadFailed;
  const canPatch = Boolean(effectiveLayoutId && effectiveRouteKey);
  const selectedNode = draftNodes.find((n) => n.nodeId === selectedNodeId) ?? null;
  const canvasPreviewClass = topologyPreviewClasses?.ok ? topologyPreviewClasses.className : "";

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

  // ── initial load ─────────────────────────────────────────────────────────
  useEffect(() => {
    const load = async () => {
      setPaletteStatus("候補をロード中...");
      setPaletteLoadFailed(false);
      setCandidateErrors([]);
      const { candidates, errors: candErr } = await loadLayoutCandidatesFromBackend();
      setLayoutCandidates(candidates);
      if (candErr.length) setCandidateErrors(candErr);
      try {
        const body = await dispatchAdminOp("ui_topology", "promoted_palette");
        if (body?.errors?.length) {
          setPaletteLoadFailed(true);
          setCandidateErrors((prev) => [...prev, ...body.errors]);
          setPaletteEntries([]);
          setPaletteStatus("プロモートパレットのロードに失敗しました。");
          return;
        }
        const promoted = body?.emission?.data as PromotedPaletteEntry[] | undefined;
        if (!Array.isArray(promoted)) {
          setPaletteLoadFailed(true);
          setCandidateErrors((prev) => [
            ...prev,
            { code: "PROMOTED_PALETTE_LOAD_FAILED", message: "プロモートパレットデータが取得できませんでした。" },
          ]);
          setPaletteEntries([]);
          setPaletteStatus("プロモートパレットのロードに失敗しました。");
          return;
        }
        const promotedEntries = promoted.map((p) => ({ ...p, isDraftOnly: false } satisfies PaletteEntry));
        const promotedKeys = new Set(promotedEntries.map((p) => p.componentKey));
        const draftCatalog = COMPONENT_CATALOG_ENTRIES
          .filter((c) => isDraftOnlyEntry(c) && !promotedKeys.has(c.componentKey))
          .map((c) => ({ componentKey: c.componentKey, componentKind: c.componentKind, isDraftOnly: true } satisfies PaletteEntry));
        setPaletteEntries([...promotedEntries, ...draftCatalog]);
        setPaletteStatus(`プロモート済み ${promotedEntries.length} 件 / ドラフト ${draftCatalog.length} 件`);
        if (candidates.length === 0 && promoted.length > 0) setLayoutCandidates(deriveCandidatesFromPalette(promoted));
        if (!routeKey && candidates.length > 0) {
          setRouteKey(candidates[0].routeKey);
          setLayoutId(candidates[0].layoutId);
        }
      } catch (e) {
        setPaletteLoadFailed(true);
        setCandidateErrors((prev) => [...prev, { code: "PROMOTED_PALETTE_LOAD_ERROR", message: String(e) }]);
        setPaletteEntries([]);
        setPaletteStatus(`プロモートパレットのロードに失敗しました: ${e}`);
      }
    };
    load();
  }, []);

  // ── layout patch (preview / validate / apply) ────────────────────────────
  const callLayoutPatch = async (action: "preview" | "validate" | "apply") => {
    setPatchErrors([]);
    setPatchSummary(null);
    setDebugJson(null);

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

    try {
      const body = await dispatchAdminOp("layout_patch", action, {
        layoutId: effectiveLayoutId,
        routeKey: effectiveRouteKey,
        tensorPatchJson,
        cssTokenRefs: selectedTokenRefs,
        responsiveTokenRefs: { md: selectedTokenRefs },
      });
      setDebugJson(JSON.stringify(body, null, 2));
      const summary = projectLayoutPatchSummary(action, body, draftNodes, selectedTokenRefs.length, selectedLayout?.layoutKey);
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
  const makeNewNode = (entry: PaletteEntry, x: number, y: number): DraftNode => ({
    nodeId: makeNodeId(),
    componentKey: entry.componentKey,
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
    width: DEFAULT_NODE_WIDTH,
    height: DEFAULT_NODE_HEIGHT,
  });

  // ── node operations (all push to history) ────────────────────────────────
  const addNode = (newNode: DraftNode) => {
    const next = [...draftNodes, newNode];
    setDraftNodes(next);
    pushHistory(next, `追加: ${friendlyComponentLabel(newNode.componentKey)}`);
    setLifecyclePhase("idle");
    announce(`${friendlyComponentLabel(newNode.componentKey)}をキャンバスに追加しました`);
  };

  const moveLayoutNode = (nodeId: string, dir: "up" | "down") => {
    setDraftNodes((prev) => {
      const idx = prev.findIndex((n) => n.nodeId === nodeId);
      if (idx < 0) return prev;
      const swapIdx = dir === "up" ? idx - 1 : idx + 1;
      if (swapIdx < 0 || swapIdx >= prev.length) return prev;
      const next = [...prev];
      [next[idx], next[swapIdx]] = [next[swapIdx], next[idx]];
      const result = next.map((n, i) => ({ ...n, orderIndex: i }));
      pushHistory(result, `順序変更: ${friendlyComponentLabel(prev[idx].componentKey)}`);
      return result;
    });
  };

  const removeNode = (nodeId: string) => {
    const node = draftNodes.find((n) => n.nodeId === nodeId);
    const next = draftNodes.filter((n) => n.nodeId !== nodeId);
    setDraftNodes(next);
    if (selectedNodeId === nodeId) setSelectedNodeId(null);
    pushHistory(next, `削除: ${node ? friendlyComponentLabel(node.componentKey) : nodeId}`);
    setLifecyclePhase("idle");
    if (node) announce(`${friendlyComponentLabel(node.componentKey)}を削除しました`);
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
    const count = draftNodes.length;
    const x = snapToGrid(20 + (count % 5) * 160, SNAP_SIZE);
    const y = snapToGrid(20 + Math.floor(count / 5) * 80, SNAP_SIZE);
    if (entry.routeKey && !routeKey) setRouteKey(entry.routeKey);
    if (entry.layoutId && !layoutId) setLayoutId(entry.layoutId);
    addNode(makeNewNode(entry, x, y));
  };

  // Gap 7: Quick-start templates from empty state
  const handleAddFromEmptyState = (templateId: string) => {
    const catalog = paletteEntries;
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
    announce(`${nodes.length} 件のコンポーネントを追加しました`);
  };

  // ── mouse drag for canvas node movement ──────────────────────────────────
  const getCanvasPos = (e: Event): { x: number; y: number } => {
    const me = e as MouseEvent;
    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return { x: 0, y: 0 };
    return { x: me.clientX - rect.left, y: me.clientY - rect.top };
  };

  const handleNodeMouseDown = (e: Event, nodeId: string) => {
    if (resizeState.current) return;
    const me = e as MouseEvent;
    me.preventDefault();
    me.stopPropagation();
    const node = draftNodes.find((n) => n.nodeId === nodeId);
    if (!node) return;
    setSelectedNodeId(nodeId);
    const pos = getCanvasPos(e);
    dragState.current = { nodeId, startMouseX: pos.x, startMouseY: pos.y, startNodeX: node.x, startNodeY: node.y };
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
    if (dragState.current && liveDragPos) {
      commitNodeUpdate(dragState.current.nodeId, { x: liveDragPos.x, y: liveDragPos.y }, "移動");
    }
    if (resizeState.current && liveResizePos) {
      commitNodeUpdate(resizeState.current.nodeId, { x: liveResizePos.x, y: liveResizePos.y, width: liveResizePos.width, height: liveResizePos.height }, "リサイズ");
    }
    dragState.current = null;
    resizeState.current = null;
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

  // ── CSS / layout class token handlers ────────────────────────────────────
  const toggleTokenRef = (tokenKey: string) => {
    setSelectedTokenRefs((prev) => prev.includes(tokenKey) ? prev.filter((k) => k !== tokenKey) : [...prev, tokenKey]);
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
      <LifecycleStepIndicator phase={lifecyclePhase} />

      <details class="mb-3.5">
        <summary class="cursor-pointer text-xs text-gray-500 hover:text-gray-700">技術情報</summary>
        <div class="alert-warn mt-1 text-xs">
          <strong>投影サーフェス境界:</strong> フロントエンドはドラフト状態・視覚プレビュー・intent 送信のみ担当。
          適用は <code>preview → validate → apply</code> 経由。直接 DB 書き込みは行いません。
        </div>
      </details>

      <RouteLayoutSelector
        candidates={layoutCandidates}
        routeKey={routeKey}
        layoutId={layoutId}
        onRouteChange={(r) => { setRouteKey(r); setManualRouteKey(""); const first = layoutsForRoute(layoutCandidates, r)[0]; setLayoutId(first?.layoutId ?? ""); setManualLayoutId(""); }}
        onLayoutChange={(l) => { setLayoutId(l); setManualLayoutId(""); }}
        disabled={selectorsDisabled}
        loadError={candidateErrors}
      />

      <AdvancedManualOverride title="manual override — layoutId / routeKey">
        <div class="flex flex-wrap gap-2">
          <input value={manualRouteKey} onInput={(e) => setManualRouteKey((e.target as HTMLInputElement).value)} placeholder="routeKey 手入力" class="input-mono flex-1 text-xs" />
          <input value={manualLayoutId} onInput={(e) => setManualLayoutId((e.target as HTMLInputElement).value)} placeholder="layoutId UUID 手入力" class="input-mono flex-[2] text-xs" />
        </div>
      </AdvancedManualOverride>

      {!canPatch && !selectorsDisabled && (
        <p class="text-sm text-yellow-700 mb-2">ルートとレイアウトを選択してから操作してください。</p>
      )}

      {/* Canvas toolbar — Gap 2: Undo/Redo buttons */}
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
          {draftNodes.length} コンポーネント
          {selectedNode ? ` — 選択中: ${friendlyComponentLabel(selectedNode.componentKey)}` : ""}
        </span>
      </div>

      {/* v2 main canvas area: palette + canvas + layer/inspector */}
      <div class={`mb-3 flex gap-2.5 ${canvasPreviewClass}`}>
        <LayoutPalette
          onDragStart={handleDragStartPalette}
          onAddToCanvas={handleAddFromPalette}
          entries={paletteEntries}
          status={paletteStatus}
        />

        <div class="min-w-0 flex-1">
          <VisualLayoutCanvas
            draftNodes={draftNodes}
            selectedNodeId={selectedNodeId}
            canvasRef={canvasRef}
            liveDragNodeId={liveDragPos?.nodeId ?? null}
            liveResizeNodeId={liveResizePos?.nodeId ?? null}
            getLivePos={getLivePos}
            showGrid={showGrid}
            onSelectNode={(id) => setSelectedNodeId(id === selectedNodeId ? null : id)}
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
            onAddFromEmptyState={handleAddFromEmptyState}
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
            onDelete={removeNode}
          />
          {selectedNode && (
            <CanvasInspector
              node={selectedNode}
              draftNodes={draftNodes}
              slotKeyCandidates={slotKeyCandidates}
              onUpdate={(updates) => updateNode(selectedNode.nodeId, updates)}
              onCommit={(updates, label) => commitNodeUpdate(selectedNode.nodeId, updates, label)}
              onClose={() => setSelectedNodeId(null)}
            />
          )}
        </div>
      </div>

      <Accordion title="レイアウトクラス参照 (topology layout class refs)" defaultOpen={false}>
        <TopologyLayoutClassPicker selectedClassRefs={selectedLayoutClassRefs} onToggle={toggleLayoutClassRef} scopeFilter="" allowedForFilter="" />
        {layoutClassRefError && <p class="text-red-600 text-sm mt-2" role="alert">{layoutClassRefError}</p>}
        <AdvancedManualOverride title="manual override — raw classKey（SSOT外 ref 検証用）">
          <div class="flex flex-wrap gap-2">
            <input value={manualLayoutClassRef} onInput={(e) => setManualLayoutClassRef((e.target as HTMLInputElement).value)} placeholder="layout.root.grid" class="input-mono flex-1 text-xs" />
            <button type="button" onClick={applyManualLayoutClassRef} class="btn-secondary text-xs">適用</button>
          </div>
        </AdvancedManualOverride>
      </Accordion>

      <Accordion title="CSS トークン参照" defaultOpen={false}>
        <CssTokenPicker selectedTokenRefs={selectedTokenRefs} onToggle={toggleTokenRef} />
      </Accordion>

      <ApplyReadinessPanel
        canPatch={canPatch}
        effectiveRouteKey={effectiveRouteKey}
        effectiveLayoutId={effectiveLayoutId}
        draftNodes={draftNodes}
        selectedTokenRefs={selectedTokenRefs}
        layoutClassRefError={layoutClassRefError}
        onNavigate={onNavigate}
      />

      {/* Action buttons with clear step labels */}
      <div class="mb-1 flex flex-wrap gap-2">
        <button
          onClick={() => callLayoutPatch("preview")}
          disabled={loading || !canPatch}
          class="btn-secondary min-w-[100px]"
          aria-label="プレビュー実行 — DBへの変更なし"
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
      <p class="mb-3 text-xs text-gray-500">
        プレビュー: 解決結果のみ(DB変更なし) → バリデート: ref整合チェック → 適用: DBへ反映
      </p>

      {/* Gap 3: Actionable error panel */}
      {patchErrors.length > 0 && (
        <ActionableValidationErrorPanel
          errors={patchErrors}
          title="エラー — 修正方法"
          onNavigate={onNavigate}
        />
      )}

      {patchSummary && <LayoutPatchSummaryPanel summary={patchSummary} />}

      <Accordion title="開発者向け情報 — payload / backend JSON" defaultOpen={false}>
        <p class="text-muted-xs mb-2">v2 payload には x/y/width/height が含まれます。</p>
        <pre class="pre-box max-h-40 overflow-y-auto m-0 mb-2">{tensorPatchJson}</pre>
        {debugJson && (
          <pre class="pre-box max-h-[200px] overflow-y-auto border border-gray-200 m-0">{debugJson}</pre>
        )}
      </Accordion>
    </div>
  );
}

// ─── タブナビゲーション ───────────────────────────────────────────────────────

const TABS: { id: TabId; label: string; hint?: string }[] = [
  { id: "bucket", label: "部品登録", hint: "部品を選んで登録 → 配置可能にする" },
  { id: "layout", label: "レイアウトビルダー", hint: "キャンバスに配置 → 確認 → 保存反映" },
  { id: "catalog", label: "コンポーネントカタログ" },
  { id: "css", label: "CSS トークン" },
  { id: "ci", label: "CI ガイダンス" },
];

// ─── メインエクスポート ────────────────────────────────────────────────────────

export default function UiBuilderAdmin(): JSX.Element {
  const [activeTab, setActiveTab] = useState<TabId>("bucket");

  return (
    <main class="page-main-wide">
      <h1 class="page-title">
        topolactor — 管理 / UI ビルダー
      </h1>
      <p class="mb-1">
        <a href="/admin" class="link">← 管理インデックスへ戻る</a>
      </p>
      <AdminHowTo
        steps={ADMIN_UI_BUILDER_GUIDE.howToSteps}
        prerequisites={ADMIN_UI_BUILDER_GUIDE.prerequisites}
      />
      <AdminHelpPanel {...ADMIN_UI_BUILDER_GUIDE} />

      {/* Stepper: bucket → generate → promote → layout → preview → validate → apply → runtime */}
      <UiBuilderFlowStepper activeTab={activeTab} onNavigate={setActiveTab} />

      <TabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />

      <div>
        {activeTab === "ci" && <CiAttentionGuidanceSection />}
        {activeTab === "catalog" && <PrimitiveCatalog />}
        {activeTab === "bucket" && <BucketSection />}
        {activeTab === "css" && <CssTokenSelectorSection />}
        {activeTab === "layout" && <LayoutBuilderSection onNavigate={setActiveTab} />}
      </div>
    </main>
  );
}
