import { useEffect, useRef, useState } from "preact/hooks";
import { JSX } from "preact";
import { COMPONENT_CATALOG_ENTRIES } from "../components/catalog.ts";
import { CSS_DICTIONARY_TOKENS } from "../runtime/cssDictionary.ts";
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

type ValidationError = { code: string; message: string };

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
    return { label: "promoted", variant: "ok" };
  }
  const item = bucketItems.find((b) => b.componentKey === componentKey);
  if (!item) return { label: "未登録", variant: "info" };
  const variant =
    item.status === "promoted" ? "ok"
    : item.status === "packaging" ? "info"
    : item.status === "bucketed" ? "warn"
    : "info";
  return { label: item.status, variant, bucketItemId: item.bucketItemId };
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
        {title ?? "manual override / unsafe / advanced"}
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
}: {
  canPatch: boolean;
  effectiveRouteKey: string;
  effectiveLayoutId: string;
  draftNodes: DraftNode[];
  selectedTokenRefs: string[];
  layoutClassRefError: string | null;
}): JSX.Element {
  const draftOnlyCount = draftNodes.filter((n) => n.isDraftOnly).length;
  const customPositionedCount = draftNodes.filter(
    (n) => n.x > 0 || n.y > 0 || n.width !== DEFAULT_NODE_WIDTH || n.height !== DEFAULT_NODE_HEIGHT,
  ).length;
  const allClear = canPatch && draftOnlyCount === 0 && !layoutClassRefError;

  return (
    <div class={`mb-3 rounded border p-3 text-sm ${allClear ? "border-green-300 bg-green-50" : "border-amber-300 bg-amber-50"}`}>
      <strong class="block mb-2">Apply 前チェック</strong>
      <ul class="space-y-1 pl-1">
        <li class="flex items-start gap-2">
          <span class={canPatch ? "text-green-700" : "text-red-600"}>{canPatch ? "✓" : "✗"}</span>
          <span>
            ルート / レイアウト選択:{" "}
            {canPatch
              ? <><code class="text-xs">{effectiveRouteKey}</code> / <code class="text-xs">{shortId(effectiveLayoutId)}</code></>
              : "未選択 — ルートとレイアウトを選択してください"}
          </span>
        </li>
        <li class="flex items-start gap-2">
          <span class={draftOnlyCount === 0 ? "text-green-700" : "text-red-600"}>{draftOnlyCount === 0 ? "✓" : "✗"}</span>
          <span>
            未プロモートノード:{" "}
            {draftOnlyCount === 0
              ? "なし"
              : <>{draftOnlyCount} 件 — バケット → プロモートを先に完了してください（適用はブロック）</>}
          </span>
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
          すべてのローカルチェック通過。preview → validate → apply の順で実行してください。
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
        <StatusBadge text={summary.valid ? "valid" : "invalid"} variant={summary.valid ? "ok" : "error"} />
      </div>
      <ul class="my-0 pl-4">
        <li>ノード数: {summary.nodeCount}（ドラフトのみ: {summary.draftOnlyCount}）</li>
        <li>ルート: <code>{summary.routeKey || "—"}</code></li>
        <li>レイアウト: {summary.layoutKey ? <code>{summary.layoutKey}</code> : <code>{shortId(summary.layoutId) || "—"}</code>}</li>
        <li>CSS トークン: {summary.cssTokenCount} 件</li>
        <li>メッセージ: {summary.message}</li>
        <li><strong>次のアクション:</strong> {summary.nextAction}</li>
      </ul>
      {summary.errors.length > 0 && (
        <ValidationErrorPanel errors={summary.errors} title="blocking errors" />
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
          placeholder="tokenKey 検索"
          class="input-mono flex-1 text-xs"
        />
        <select value={categoryFilter} onChange={(e) => setCategoryFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">カテゴリ（すべて）</option>
          {categories.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        <select value={scopeFilter} onChange={(e) => setScopeFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">componentScope（すべて）</option>
          {scopes.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        <select value={roleFilter} onChange={(e) => setRoleFilter((e.target as HTMLSelectElement).value)} class="input w-auto text-xs">
          <option value="">semanticRole（すべて）</option>
          {roles.map((r) => <option key={r} value={r}>{r}</option>)}
        </select>
      </div>

      {selectedTokenRefs.length > 0 && (
        <div class="mb-2 rounded border border-blue-200 bg-blue-50 p-2">
          <strong class="text-xs">選択済み ({selectedTokenRefs.length})</strong>
          <div class="mt-1 flex flex-wrap gap-1">
            {selectedTokenRefs.map((key) => (
              <button
                key={key}
                type="button"
                onClick={() => onToggle(key)}
                class="rounded border border-blue-400 bg-white px-1.5 py-0.5 font-mono text-xs hover:bg-red-50"
                title="クリックで解除"
              >
                {key} ✕
              </button>
            ))}
          </div>
        </div>
      )}

      <div class="table-wrap max-h-64 overflow-y-auto">
        <table class="table font-mono text-xs">
          <thead>
            <tr>
              {["選択", "トークンキー", "カテゴリ", "スコープ", "役割", "プロパティ"].map((h) => (
                <th key={h}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {filtered.map((t) => (
              <tr key={t.tokenKey} class={selectedTokenRefs.includes(t.tokenKey) ? "bg-blue-50" : ""}>
                <td>
                  <input
                    type="checkbox"
                    checked={selectedTokenRefs.includes(t.tokenKey)}
                    onChange={() => onToggle(t.tokenKey)}
                  />
                </td>
                <td><code>{t.tokenKey}</code></td>
                <td>{t.category}</td>
                <td>{t.componentScope.join(",")}</td>
                <td>{t.semanticRole}</td>
                <td>{t.property}</td>
              </tr>
            ))}
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
    component_key: "コンポーネントキー",
    kind: "種別",
    source_path: "ソースパス",
    family: "ファミリー",
    semantic_role: "セマンティクス役割",
    visual_role: "ビジュアル役割",
    lifecycle_status: "ライフサイクル",
    runtime_connected: "ランタイム接続",
    registration_required: "登録必須",
    capability_tags: "ケイパビリティタグ",
  };

  return (
    <div>
      <p class="text-muted mb-3">
        プリミティブコンポーネントは <code>frontend/components/</code> で定義されます。
        UI topology DB に登録（パッケージ生成経由）されて初めてトポロジーテンソルエンティティになります。
        コードのみのコンポーネントは drift/GAP として扱われます。
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
                <td><code>{c.sourcePath}</code></td>
                <td>{c.componentFamily}</td>
                <td>{c.semanticRole}</td>
                <td>{c.visualRole}</td>
                <td>
                  <StatusBadge
                    text={c.lifecycleStatus}
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
      <p class="text-muted-xs mt-2">
        drift → topology エンティティへのプロモーション:
        ui_component_bucket に登録後、パッケージジェネレーターを実行してください。
      </p>
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
    if (existing.label === "promoted" || existing.label === "bucketed" || existing.label === "packaging") {
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
        setStatus(`バケットアイテムを作成しました: ${selectedCatalog.componentKey}`);
        setSelectedId(body.emission.data.bucketItemId);
        await loadBucket();
      } else {
        setErrors(body?.errors ?? [{ code: "BUCKET_CREATE_FAILED", message: "バケット作成に失敗しました。" }]);
        setStatus("バケット作成に失敗しました。");
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
        setStatus(`バケットアイテムを作成しました: ${componentKey}`);
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
        setStatus(`生成完了: ${selectedCatalog?.componentKey ?? selectedId} → packaging`);
        await loadBucket();
      } else {
        setErrors(body?.errors ?? []);
        setStatus("パッケージ生成に失敗しました。");
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
        setStatus(`プロモート完了: route=${effectiveRouteKey}`);
        await loadBucket();
        const { candidates } = await loadLayoutCandidatesFromBackend();
        setLayoutCandidates(candidates);
      } else {
        setErrors(body?.errors ?? []);
        setStatus("パッケージプロモートに失敗しました。");
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
                {["選択", "componentKey", "kind", "sourcePath", "ステータス"].map((h) => (
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
                    <td><code>{c.sourcePath}</code></td>
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
              disabled={loading || resolveBucketStatus(selectedCatalog.componentKey, items, promotedKeys).label !== "未登録"}
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
              {selectedItem.status === "bucketed" ? " generate → promote" : " promote"}
            </p>
          )}

          <div class="mt-2 flex flex-wrap gap-2">
            <button
              onClick={handleGenerate}
              disabled={loading || !selectedId || !effectiveRouteKey}
              class="btn-primary"
            >
              生成 (bucketed → packaging)
            </button>
            <button
              onClick={handlePromote}
              disabled={loading || !selectedId || !effectiveRouteKey}
              class="btn-success"
            >
              プロモート (packaging → promoted)
            </button>
          </div>
          <AdminActionHint>
            生成: componentId 等を発行し packaging 状態へ。プロモート: UI topology DB に promoted として永続 — layout パレットに反映されます。
          </AdminActionHint>
        </Accordion>
      )}

      {loading && <p class="text-muted font-mono text-sm">処理中...</p>}
      {status && (
        <p class={`text-sm font-bold ${errors.length > 0 ? "text-red-600" : "text-green-700"}`}>
          {status}
        </p>
      )}
      {errors.length > 0 && <ValidationErrorPanel errors={errors} title="操作エラー" />}
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
        セレクター候補は <code>docs/design/css-dictionary-ssot.yaml</code> 派生アーティファクトから投影されます。
        ドラフトは <code>cssTokenRefs</code> / <code>responsiveTokenRefs</code> を保持してください。生の CSS はレガシーのみ。
      </p>
      <CssTokenPicker selectedTokenRefs={selectedTokenRefs} onToggle={toggleTokenRef} />
    </div>
  );
}

// ─── v2 ビジュアルキャンバスコンポーネント ────────────────────────────────────

const RESIZE_HANDLE_STYLE: Record<ResizeDir, Record<string, string>> = {
  nw: { top: "-4px", left: "-4px", cursor: "nw-resize" },
  n: { top: "-4px", left: "50%", transform: "translateX(-50%)", cursor: "n-resize" },
  ne: { top: "-4px", right: "-4px", cursor: "ne-resize" },
  w: { top: "50%", left: "-4px", transform: "translateY(-50%)", cursor: "w-resize" },
  e: { top: "50%", right: "-4px", transform: "translateY(-50%)", cursor: "e-resize" },
  sw: { bottom: "-4px", left: "-4px", cursor: "sw-resize" },
  s: { bottom: "-4px", left: "50%", transform: "translateX(-50%)", cursor: "s-resize" },
  se: { bottom: "-4px", right: "-4px", cursor: "se-resize" },
};

function ResizeHandle({
  dir,
  onMouseDown,
}: {
  dir: ResizeDir;
  onMouseDown: (e: Event) => void;
}): JSX.Element {
  return (
    <div
      class="absolute z-20 h-2 w-2 rounded-sm border border-blue-600 bg-white"
      style={RESIZE_HANDLE_STYLE[dir]}
      onMouseDown={(e: Event) => { e.stopPropagation(); onMouseDown(e); }}
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
  onSelect,
  onNodeMouseDown,
  onResizeHandleMouseDown,
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
}): JSX.Element {
  return (
    <div
      class={`absolute select-none rounded border-2 font-mono text-xs ${
        isSelected
          ? "border-blue-600 shadow-md"
          : node.isDraftOnly
          ? "border-yellow-300"
          : "border-blue-200"
      } ${node.isDraftOnly ? "bg-yellow-50" : "bg-white"}`}
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
    >
      <div class="flex h-full flex-col overflow-hidden p-1">
        <div class="truncate font-bold leading-tight">{node.componentKey}</div>
        {node.isDraftOnly && (
          <span class="text-[0.58rem] text-yellow-700">ドラフト — 適用ブロック</span>
        )}
        {node.slotKey && (
          <span class="truncate text-[0.58rem] text-gray-500">slot:{node.slotKey}</span>
        )}
        <span class="mt-auto text-[0.55rem] text-gray-300">
          {displayX},{displayY} {displayW}×{displayH}
        </span>
      </div>
      {isSelected && !isDragging && (
        <>
          {(["nw", "n", "ne", "w", "e", "sw", "s", "se"] as ResizeDir[]).map((dir) => (
            <ResizeHandle
              key={dir}
              dir={dir}
              onMouseDown={(e) => onResizeHandleMouseDown(e, dir)}
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
      class="relative overflow-auto rounded-lg border-2 border-dashed border-gray-300 bg-white"
      style={{ minHeight: `${CANVAS_MIN_HEIGHT}px`, ...gridStyle }}
      onClick={onDeselectAll}
      onMouseMove={onCanvasMouseMove}
      onMouseUp={onCanvasMouseUp}
      onMouseLeave={onCanvasMouseUp}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      {draftNodes.length === 0 && (
        <div class="pointer-events-none absolute inset-0 flex items-center justify-center font-mono text-sm text-gray-300">
          パレットからコンポーネントをドラッグしてください
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
      <div class="overflow-y-auto" style="max-height:300px;">
        {draftNodes.length === 0 && (
          <p class="px-2 py-4 text-center text-xs text-gray-400">なし</p>
        )}
        {[...draftNodes].reverse().map((node, reversedIdx) => {
          const origIdx = draftNodes.length - 1 - reversedIdx;
          const isSelected = node.nodeId === selectedNodeId;
          return (
            <div
              key={node.nodeId}
              onClick={() => onSelect(node.nodeId)}
              class={`flex cursor-pointer items-center gap-1 border-b border-gray-100 px-1.5 py-1 text-xs ${
                isSelected ? "bg-blue-50" : "hover:bg-gray-50"
              }`}
            >
              <span
                class={`h-2 w-2 shrink-0 rounded-sm ${
                  node.isDraftOnly ? "bg-yellow-400" : "bg-blue-400"
                }`}
              />
              <span class="flex-1 truncate font-mono">{node.componentKey}</span>
              <div class="flex shrink-0 gap-0.5">
                <button
                  type="button"
                  onClick={(e: Event) => { e.stopPropagation(); onMoveUp(node.nodeId); }}
                  disabled={origIdx === draftNodes.length - 1}
                  class="rounded px-0.5 text-[0.65rem] text-gray-400 hover:text-gray-600 disabled:opacity-30"
                  title="前面へ"
                >▲</button>
                <button
                  type="button"
                  onClick={(e: Event) => { e.stopPropagation(); onMoveDown(node.nodeId); }}
                  disabled={origIdx === 0}
                  class="rounded px-0.5 text-[0.65rem] text-gray-400 hover:text-gray-600 disabled:opacity-30"
                  title="背面へ"
                >▼</button>
                <button
                  type="button"
                  onClick={(e: Event) => { e.stopPropagation(); onDelete(node.nodeId); }}
                  class="rounded px-0.5 text-[0.65rem] text-red-400 hover:text-red-600"
                  title="削除"
                >✕</button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function CanvasInspector({
  node,
  draftNodes,
  slotKeyCandidates,
  onUpdate,
  onClose,
}: {
  node: DraftNode;
  draftNodes: DraftNode[];
  slotKeyCandidates: string[];
  onUpdate: (updates: Partial<DraftNode>) => void;
  onClose: () => void;
}): JSX.Element {
  const [manualSlotKey, setManualSlotKey] = useState("");
  const [parentCycleError, setParentCycleError] = useState<string | null>(null);
  const parentOptions = draftNodes.filter((n) => n.nodeId !== node.nodeId);

  const handleParentChange = (value: string) => {
    const parentId = value || null;
    if (parentId && wouldCreateParentCycle(draftNodes, node.nodeId, parentId)) {
      setParentCycleError("循環参照になる親は選択できません。");
      return;
    }
    setParentCycleError(null);
    onUpdate({ parentNodeId: parentId });
  };

  const handleNum = (
    field: "x" | "y" | "width" | "height" | "gridCol" | "gridRow",
    raw: string,
    applySnap = false,
  ) => {
    const v = parseInt(raw, 10);
    if (isNaN(v)) return;
    const min = field === "width" ? 40 : field === "height" ? 30 : field === "gridCol" ? 1 : 0;
    const clamped = Math.max(min, v);
    const final = applySnap ? snapToGrid(clamped, SNAP_SIZE) : clamped;
    onUpdate({ [field]: final } as Partial<DraftNode>);
  };

  return (
    <div
      class="w-52 shrink-0 overflow-y-auto rounded-lg border border-blue-600 bg-blue-50 p-2.5 font-mono text-xs"
      style="max-height:440px;"
    >
      <div class="mb-2 flex items-center justify-between">
        <strong class="text-sm">インスペクター</strong>
        <button type="button" onClick={onClose} class="btn-secondary px-1.5 py-0 text-xs">✕</button>
      </div>
      <div class="mb-2">
        <code class="text-xs">{node.componentKey}</code>
        {node.isDraftOnly && <span class="badge-warn ml-1">ドラフト</span>}
      </div>

      <div class="mb-2">
        <div class="mb-1 text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">位置・サイズ</div>
        <div class="grid grid-cols-2 gap-1">
          {(["x", "y", "width", "height"] as const).map((f) => (
            <label key={f} class="flex flex-col gap-0.5">
              <span class="text-[0.65rem]">{f.toUpperCase()}</span>
              <input
                type="number"
                value={node[f]}
                min={f === "width" ? 40 : f === "height" ? 30 : 0}
                step={SNAP_SIZE}
                onInput={(e) => handleNum(f, (e.target as HTMLInputElement).value, true)}
                class="input px-1 py-0.5"
              />
            </label>
          ))}
        </div>
      </div>

      <div class="mb-2 flex flex-col gap-1.5">
        <label class="flex flex-col gap-0.5">
          親ノード
          <select
            value={node.parentNodeId ?? ""}
            onChange={(e) => handleParentChange((e.target as HTMLSelectElement).value)}
            class="input px-1 py-0.5 text-xs"
          >
            <option value="">(none / top-level)</option>
            {parentOptions.map((n) => {
              const cyclic = wouldCreateParentCycle(draftNodes, node.nodeId, n.nodeId);
              return (
                <option key={n.nodeId} value={n.nodeId} disabled={cyclic}>
                  {n.componentKey} · {shortId(n.nodeId)}
                  {cyclic ? " (循環)" : ""}
                </option>
              );
            })}
          </select>
        </label>
        {parentCycleError && <p class="m-0 text-red-600">{parentCycleError}</p>}

        <label class="flex flex-col gap-0.5">
          スロットキー
          <select
            value={node.slotKey}
            onChange={(e) => onUpdate({ slotKey: (e.target as HTMLSelectElement).value })}
            class="input px-1 py-0.5 text-xs"
          >
            {slotKeyCandidates.map((sk) => (
              <option key={sk || "__empty__"} value={sk}>{sk || "(空)"}</option>
            ))}
          </select>
        </label>

        <AdvancedManualOverride title="manual slotKey">
          <div class="flex gap-1">
            <input
              value={manualSlotKey}
              onInput={(e) => setManualSlotKey((e.target as HTMLInputElement).value)}
              placeholder="カスタム slotKey"
              class="input-mono flex-1 px-1 py-0.5 text-xs"
            />
            <button
              type="button"
              class="btn-secondary text-xs"
              onClick={() => { onUpdate({ slotKey: manualSlotKey }); setManualSlotKey(""); }}
            >
              適用
            </button>
          </div>
        </AdvancedManualOverride>
      </div>

      <div class="flex flex-col gap-1">
        <div class="text-[0.65rem] font-semibold uppercase tracking-wide text-gray-500">グリッド</div>
        <label class="flex flex-col gap-0.5">
          列 (1–12)
          <input
            type="number" min={1} max={12} value={node.gridCol}
            onInput={(e) => handleNum("gridCol", (e.target as HTMLInputElement).value)}
            class="input px-1 py-0.5"
          />
        </label>
        <label class="flex flex-col gap-0.5">
          行
          <input
            type="number" min={1} value={node.gridRow}
            onInput={(e) => handleNum("gridRow", (e.target as HTMLInputElement).value)}
            class="input px-1 py-0.5"
          />
        </label>
      </div>
    </div>
  );
}

// ─── パレット ─────────────────────────────────────────────────────────────────

function LayoutPalette({
  onDragStart,
  entries,
  status,
}: {
  onDragStart: (entry: PaletteEntry) => void;
  entries: PaletteEntry[];
  status: string | null;
}): JSX.Element {
  return (
    <div class="w-44 shrink-0 overflow-y-auto rounded-lg border border-gray-200 bg-gray-50 p-2 max-h-[340px]">
      <h4 class="mb-1 text-sm font-semibold">パレット</h4>
      <p class="mb-1 text-[0.68rem] text-muted-xs">
        キャンバスにドラッグ。<strong class="text-yellow-700">(ドラフト)</strong> = 未登録 —
        コンポーネントバケットでプロモートするまで適用はブロックされます。
      </p>
      {status && <p class="text-[0.65rem] text-muted-xs">{status}</p>}
      {entries.map((c) => {
        const draftOnly = c.isDraftOnly;
        return (
          <div
            key={c.componentKey}
            draggable={true}
            onDragStart={() => onDragStart(c)}
            class={`mb-0.5 cursor-grab rounded border px-1.5 py-1 font-mono text-xs ${
              draftOnly
                ? "border-yellow-300 bg-yellow-50"
                : "border-blue-200 bg-blue-50"
            }`}
          >
            <div class="font-bold">
              {c.componentKey}
              {draftOnly && (
                <span class="ml-1 font-normal text-yellow-700">
                  (ドラフト)
                </span>
              )}
            </div>
            <div class="text-[0.65rem] text-gray-500">{c.componentKind}</div>
          </div>
        );
      })}
    </div>
  );
}

// ─── ノードインスペクター ─────────────────────────────────────────────────────

function LayoutNodeInspector({
  node,
  draftNodes,
  slotKeyCandidates,
  onUpdate,
  onClose,
}: {
  node: DraftNode;
  draftNodes: DraftNode[];
  slotKeyCandidates: string[];
  onUpdate: (updates: Partial<DraftNode>) => void;
  onClose: () => void;
}): JSX.Element {
  const [manualSlotKey, setManualSlotKey] = useState("");
  const [parentCycleError, setParentCycleError] = useState<string | null>(null);

  const parentOptions = draftNodes.filter((n) => n.nodeId !== node.nodeId);

  const handleParentChange = (value: string) => {
    const parentId = value || null;
    if (parentId && wouldCreateParentCycle(draftNodes, node.nodeId, parentId)) {
      setParentCycleError("循環参照になる親は選択できません。");
      return;
    }
    setParentCycleError(null);
    onUpdate({ parentNodeId: parentId });
  };

  return (
    <div class="w-52 shrink-0 rounded-lg border border-blue-600 bg-blue-50 p-2.5 font-mono text-xs">
      <div class="mb-2 flex items-center justify-between">
        <strong class="text-sm">インスペクター</strong>
        <button onClick={onClose} class="btn-secondary px-1.5 py-0 text-xs">✕</button>
      </div>
      <div class="mb-2 text-gray-600">
        <code class="text-xs">{node.componentKey}</code>
      </div>
      <div class="flex flex-col gap-1.5">
        <label class="flex flex-col gap-0.5">
          親ノード
          <select
            value={node.parentNodeId ?? ""}
            onChange={(e) => handleParentChange((e.target as HTMLSelectElement).value)}
            class="input px-1 py-0.5 text-xs"
          >
            <option value="">(none / top-level)</option>
            {parentOptions.map((n) => {
              const cyclic = wouldCreateParentCycle(draftNodes, node.nodeId, n.nodeId);
              return (
                <option key={n.nodeId} value={n.nodeId} disabled={cyclic}>
                  {n.componentKey} · #{n.orderIndex} · {shortId(n.nodeId)}
                  {cyclic ? " (循環)" : ""}
                </option>
              );
            })}
          </select>
        </label>
        {parentCycleError && <p class="text-red-600 m-0">{parentCycleError}</p>}

        <label class="flex flex-col gap-0.5">
          スロットキー
          <select
            value={node.slotKey}
            onChange={(e) => onUpdate({ slotKey: (e.target as HTMLSelectElement).value })}
            class="input px-1 py-0.5 text-xs"
          >
            {slotKeyCandidates.map((sk) => (
              <option key={sk || "__empty__"} value={sk}>
                {sk || "(空 — デフォルト)"}
              </option>
            ))}
          </select>
        </label>
        <p class="text-muted-xs m-0">
          TODO: slot metadata SSOT/API — 現在は DB tensor slot + canvas 既存値 + 一般候補
        </p>

        <AdvancedManualOverride title="manual override — slotKey">
          <input
            value={manualSlotKey}
            onInput={(e) => setManualSlotKey((e.target as HTMLInputElement).value)}
            placeholder="カスタム slotKey"
            class="input-mono w-full px-1 py-0.5 text-xs"
          />
          <button
            type="button"
            class="btn-secondary mt-1 text-xs"
            onClick={() => onUpdate({ slotKey: manualSlotKey })}
          >
            手動 slotKey を適用
          </button>
        </AdvancedManualOverride>

        <label class="flex flex-col gap-0.5">
          グリッド列 (1–12)
          <input
            type="number"
            min={1}
            max={12}
            value={node.gridCol}
            onInput={(e) => {
              const v = parseInt((e.target as HTMLInputElement).value);
              onUpdate({ gridCol: isNaN(v) ? 1 : v });
            }}
            class="input px-1 py-0.5"
          />
        </label>
        <label class="flex flex-col gap-0.5">
          グリッド行
          <input
            type="number"
            min={1}
            value={node.gridRow}
            onInput={(e) => {
              const v = parseInt((e.target as HTMLInputElement).value);
              onUpdate({ gridRow: isNaN(v) ? 1 : v });
            }}
            class="input px-1 py-0.5"
          />
        </label>
      </div>
    </div>
  );
}

// ─── レイアウトビルダーセクション v2 ──────────────────────────────────────────

function LayoutBuilderSection(): JSX.Element {
  // ── route/layout selection ───────────────────────────────────────────────
  const [layoutId, setLayoutId] = useState("");
  const [routeKey, setRouteKey] = useState("");
  const [manualLayoutId, setManualLayoutId] = useState("");
  const [manualRouteKey, setManualRouteKey] = useState("");

  // ── canvas draft state ───────────────────────────────────────────────────
  const [draftNodes, setDraftNodes] = useState<DraftNode[]>([]);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);

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
  const [debugJson, setDebugJson] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
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
  // Frontend submits intent only. Topology persistence authority: backend/DB.
  const callLayoutPatch = async (action: "preview" | "validate" | "apply") => {
    setError(null);
    setPatchSummary(null);
    setDebugJson(null);
    if (!canPatch) { setError("layoutId と routeKey を候補から選択してください。"); return; }
    if (action === "apply") {
      const draftOnlyNodes = draftNodes.filter((n) => n.isDraftOnly);
      if (draftOnlyNodes.length > 0) {
        setError(
          `APPLY_BLOCKED: ${draftOnlyNodes.length} 件のノードに DB 登録 ID がありません。` +
          ` 先にバケット → プロモートしてください: ${draftOnlyNodes.map((n) => n.componentKey).join(", ")}`,
        );
        return;
      }
    }
    setLoading(true);
    try {
      const body = await dispatchAdminOp("layout_patch", action, {
        layoutId: effectiveLayoutId,
        routeKey: effectiveRouteKey,
        tensorPatchJson,
        cssTokenRefs: selectedTokenRefs,
        responsiveTokenRefs: { md: selectedTokenRefs },
      });
      setDebugJson(JSON.stringify(body, null, 2));
      setPatchSummary(projectLayoutPatchSummary(action, body, draftNodes, selectedTokenRefs.length, selectedLayout?.layoutKey));
      if (body?.errors?.length) setError(`${body.errors[0].code}: ${body.errors[0].message}`);
    } catch (e) {
      setError(`${e}`);
    } finally {
      setLoading(false);
    }
  };

  // ── node operations ──────────────────────────────────────────────────────
  const moveLayoutNode = (nodeId: string, dir: "up" | "down") => {
    setDraftNodes((prev) => {
      const idx = prev.findIndex((n) => n.nodeId === nodeId);
      if (idx < 0) return prev;
      const swapIdx = dir === "up" ? idx - 1 : idx + 1;
      if (swapIdx < 0 || swapIdx >= prev.length) return prev;
      const next = [...prev];
      [next[idx], next[swapIdx]] = [next[swapIdx], next[idx]];
      return next.map((n, i) => ({ ...n, orderIndex: i }));
    });
  };

  const removeNode = (nodeId: string) => {
    setDraftNodes((prev) => prev.filter((n) => n.nodeId !== nodeId));
    if (selectedNodeId === nodeId) setSelectedNodeId(null);
  };

  const updateNode = (nodeId: string, updates: Partial<DraftNode>) => {
    setDraftNodes((prev) => prev.map((n) => (n.nodeId === nodeId ? { ...n, ...updates } : n)));
  };

  // ── palette drag (HTML5) → canvas drop with coordinate placement ─────────
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
    const newNode: DraftNode = {
      nodeId: makeNodeId(),
      componentKey: src.entry.componentKey,
      isDraftOnly: src.entry.isDraftOnly,
      componentId: src.entry.componentId,
      packageId: src.entry.packageId,
      layoutId: src.entry.layoutId,
      wiringId: src.entry.wiringId,
      tensorId: src.entry.tensorId,
      slotKey: "",
      orderIndex: draftNodes.length,
      parentNodeId: null,
      gridCol: Math.max(1, Math.floor(dropX / 50) + 1),
      gridRow: Math.max(1, Math.floor(dropY / 40) + 1),
      x: dropX,
      y: dropY,
      width: DEFAULT_NODE_WIDTH,
      height: DEFAULT_NODE_HEIGHT,
    };
    setDraftNodes((prev) => [...prev, newNode]);
  };

  // ── v2: mouse drag for canvas node movement ──────────────────────────────
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
    if (dragState.current && liveDragPos) updateNode(dragState.current.nodeId, { x: liveDragPos.x, y: liveDragPos.y });
    if (resizeState.current && liveResizePos) updateNode(resizeState.current.nodeId, { x: liveResizePos.x, y: liveResizePos.y, width: liveResizePos.width, height: liveResizePos.height });
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
      <div class="alert-warn mb-3.5">
        <strong>投影サーフェス境界 (v2):</strong> フロントエンドはドラフトレイアウト状態・マウスインタラクション・ビジュアルプレビューのみ保持。
        適用は <code>layout_patch:preview → validate → apply</code> 経由 — 直接 DB 書き込み・topology 判断・promotion 判断はしません。
      </div>

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
        <p class="text-sm text-yellow-700 mb-2">ルートとレイアウトを選択してから preview / validate / apply を実行してください。</p>
      )}

      {/* v2 canvas toolbar */}
      <div class="mb-2 flex flex-wrap items-center gap-3 text-xs">
        <label class="flex cursor-pointer items-center gap-1">
          <input type="checkbox" checked={showGrid} onChange={(e) => setShowGrid((e.target as HTMLInputElement).checked)} />
          グリッド ({SNAP_SIZE}px snap)
        </label>
        {draftNodes.length > 0 && (
          <button type="button" onClick={() => { setDraftNodes([]); setSelectedNodeId(null); }} class="btn-danger py-0 px-2 text-xs">
            キャンバスをクリア
          </button>
        )}
        <span class="text-gray-400">
          {draftNodes.length} ノード
          {selectedNode ? ` — 選択中: ${selectedNode.componentKey}` : ""}
        </span>
      </div>

      {/* v2 main canvas area: palette + canvas + layer/inspector */}
      <div class={`mb-3 flex gap-2.5 ${canvasPreviewClass}`}>
        <LayoutPalette onDragStart={handleDragStartPalette} entries={paletteEntries} status={paletteStatus} />

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
          />
        </div>

        {/* right panel: layer tree + inspector */}
        <div class="flex shrink-0 flex-col gap-2" style="width:196px;">
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
              onClose={() => setSelectedNodeId(null)}
            />
          )}
        </div>
      </div>

      <Accordion title="topology layout class refs (layoutClassRefs)" defaultOpen={false}>
        <TopologyLayoutClassPicker selectedClassRefs={selectedLayoutClassRefs} onToggle={toggleLayoutClassRef} scopeFilter="" allowedForFilter="" />
        {layoutClassRefError && <p class="text-red-600 text-sm mt-2">{layoutClassRefError}</p>}
        <AdvancedManualOverride title="manual override — raw classKey（理由必須: SSOT外 ref 検証用）">
          <div class="flex flex-wrap gap-2">
            <input value={manualLayoutClassRef} onInput={(e) => setManualLayoutClassRef((e.target as HTMLInputElement).value)} placeholder="layout.root.grid" class="input-mono flex-1 text-xs" />
            <button type="button" onClick={applyManualLayoutClassRef} class="btn-secondary text-xs">手動 classKey を適用</button>
          </div>
        </AdvancedManualOverride>
      </Accordion>

      <Accordion title="CSS トークン参照 (cssTokenRefs)" defaultOpen={false}>
        <CssTokenPicker selectedTokenRefs={selectedTokenRefs} onToggle={toggleTokenRef} />
      </Accordion>

      <ApplyReadinessPanel
        canPatch={canPatch}
        effectiveRouteKey={effectiveRouteKey}
        effectiveLayoutId={effectiveLayoutId}
        draftNodes={draftNodes}
        selectedTokenRefs={selectedTokenRefs}
        layoutClassRefError={layoutClassRefError}
      />

      <div class="mb-2.5 flex flex-wrap gap-2">
        <button onClick={() => callLayoutPatch("preview")} disabled={loading || !canPatch} class="btn-secondary">1. プレビュー</button>
        <button onClick={() => callLayoutPatch("validate")} disabled={loading || !canPatch} class="btn border border-blue-600 text-blue-600 hover:bg-blue-50">2. バリデート</button>
        <button onClick={() => callLayoutPatch("apply")} disabled={loading || !canPatch} class="btn-success">3. 適用</button>
      </div>
      <AdminActionHint>
        プレビュー: 解決結果のみ（DB 不変）。バリデート: ref 整合チェック。適用: layout を DB へ明示反映。ドラフトのみノードがあると適用はブロック。
      </AdminActionHint>

      {loading && <p class="text-muted font-mono text-sm">処理中...</p>}
      {error && (
        <div class="alert-error mb-2">
          <p class="font-mono text-sm"><strong>{error}</strong></p>
          <p class="text-muted-xs mt-1">
            missing ref / validation — layoutId・routeKey・componentId を確認。backend 未接続は JWT と DEMO_BACKEND_URL。
          </p>
        </div>
      )}
      {patchSummary && <LayoutPatchSummaryPanel summary={patchSummary} />}

      <Accordion title="debug — tensorPatchJson (v2 visual coords) / raw backend JSON" defaultOpen={false}>
        <p class="text-muted-xs mb-2">開発者向け。v2 payload には x/y/width/height が含まれます。</p>
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
  { id: "bucket", label: "バケット管理", hint: "Step 1: bucket → generate → promote" },
  { id: "layout", label: "レイアウトビルダー", hint: "Step 2: canvas 配置 → preview → validate → apply" },
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

      <TabBar tabs={TABS} activeTab={activeTab} onSelect={setActiveTab} />

      <div>
        {activeTab === "ci" && <CiAttentionGuidanceSection />}
        {activeTab === "catalog" && <PrimitiveCatalog />}
        {activeTab === "bucket" && <BucketSection />}
        {activeTab === "css" && <CssTokenSelectorSection />}
        {activeTab === "layout" && <LayoutBuilderSection />}
      </div>
    </main>
  );
}
