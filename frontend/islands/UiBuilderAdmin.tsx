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

/**
 * /admin/ui-builder — UI コンポーネントシステム & レイアウトビルダー。
 *
 * Issue #86: プリミティブコンポーネントシステム + UI topology DB 登録。
 * Issue #89: admin ビジュアルレイアウトビルダー（スケルトン）。
 *
 * SSOT: docs/registrar-admin-ui-specification.md §2.5
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
  componentId?: string;
  packageId?: string;
  layoutId?: string;
  wiringId?: string;
  tensorId?: string;
};

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
  tabs: { id: TabId; label: string }[];
  activeTab: TabId;
  onSelect: (id: TabId) => void;
}): JSX.Element {
  return (
    <div class="tab-bar">
      {tabs.map((tab) => (
        <button
          key={tab.id}
          type="button"
          onClick={() => onSelect(tab.id)}
          class={activeTab === tab.id ? "tab-active" : "tab-inactive"}
        >
          {tab.label}
        </button>
      ))}
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
  if (!parentId) return false;
  let current: string | null = parentId;
  while (current) {
    if (current === nodeId) return true;
    const parent = nodes.find((n) => n.nodeId === current);
    current = parent?.parentNodeId ?? null;
  }
  return false;
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

// ─── レイアウトビルダーセクション ─────────────────────────────────────────────

function LayoutBuilderSection(): JSX.Element {
  const [layoutId, setLayoutId] = useState("");
  const [routeKey, setRouteKey] = useState("");
  const [manualLayoutId, setManualLayoutId] = useState("");
  const [manualRouteKey, setManualRouteKey] = useState("");
  const [draftNodes, setDraftNodes] = useState<DraftNode[]>([]);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedTokenRefs, setSelectedTokenRefs] = useState<string[]>([]);
  const [selectedLayoutClassRefs, setSelectedLayoutClassRefs] = useState<string[]>([]);
  const [manualLayoutClassRef, setManualLayoutClassRef] = useState("");
  const [layoutClassRefError, setLayoutClassRefError] = useState<string | null>(null);
  const [patchSummary, setPatchSummary] = useState<LayoutPatchSummary | null>(null);
  const [debugJson, setDebugJson] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [paletteEntries, setPaletteEntries] = useState<PaletteEntry[]>([]);
  const [paletteStatus, setPaletteStatus] = useState<string | null>(null);
  const [layoutCandidates, setLayoutCandidates] = useState<LayoutRouteCandidate[]>([]);
  const [candidateErrors, setCandidateErrors] = useState<ValidationError[]>([]);
  const [paletteLoadFailed, setPaletteLoadFailed] = useState(false);

  const dragSrc = useRef<DragSrc | null>(null);
  const tensorPatchJson = buildLayoutPatchJson(draftNodes, selectedLayoutClassRefs);
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
          .map((c) => ({
            componentKey: c.componentKey,
            componentKind: c.componentKind,
            isDraftOnly: true,
          } satisfies PaletteEntry));
        setPaletteEntries([...promotedEntries, ...draftCatalog]);
        setPaletteStatus(`プロモート済み ${promotedEntries.length} 件 / ドラフト ${draftCatalog.length} 件`);

        if (candidates.length === 0 && promoted.length > 0) {
          setLayoutCandidates(deriveCandidatesFromPalette(promoted));
        }
        if (!routeKey && candidates.length > 0) {
          setRouteKey(candidates[0].routeKey);
          setLayoutId(candidates[0].layoutId);
        }
      } catch (e) {
        setPaletteLoadFailed(true);
        setCandidateErrors((prev) => [
          ...prev,
          { code: "PROMOTED_PALETTE_LOAD_ERROR", message: String(e) },
        ]);
        setPaletteEntries([]);
        setPaletteStatus(`プロモートパレットのロードに失敗しました: ${e}`);
      }
    };
    load();
  }, []);

  const callLayoutPatch = async (action: "preview" | "validate" | "apply") => {
    setError(null);
    setPatchSummary(null);
    setDebugJson(null);
    if (!canPatch) {
      setError("layoutId と routeKey を候補から選択してください。");
      return;
    }
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
      const summary = projectLayoutPatchSummary(
        action,
        body,
        draftNodes,
        selectedTokenRefs.length,
        selectedLayout?.layoutKey,
      );
      setPatchSummary(summary);
      if (body?.errors?.length) {
        setError(`${body.errors[0].code}: ${body.errors[0].message}`);
      }
    } catch (e) {
      setError(`${e}`);
    } finally {
      setLoading(false);
    }
  };

  const handleDragStartPalette = (entry: PaletteEntry) => {
    dragSrc.current = { kind: "palette", entry };
  };

  const handleDragStartCanvas = (nodeId: string) => {
    dragSrc.current = { kind: "canvas", nodeId };
  };

  const handleDragOverCanvas = (e: Event) => { e.preventDefault(); };

  const handleDropOnCanvas = (e: Event) => {
    e.preventDefault();
    const src = dragSrc.current;
    if (!src) return;
    if (src.kind === "palette") {
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
        gridCol: 1,
        gridRow: draftNodes.length + 1,
      };
      setDraftNodes((prev) => [...prev, newNode]);
    } else if (src.kind === "canvas") {
      const nodeId = src.nodeId;
      setDraftNodes((prev) => {
        const node = prev.find((n) => n.nodeId === nodeId);
        if (!node) return prev;
        const rest = prev.filter((n) => n.nodeId !== nodeId);
        return [...rest, node].map((n, i) => ({ ...n, orderIndex: i }));
      });
    }
    dragSrc.current = null;
  };

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
    setDraftNodes((prev) =>
      prev.map((n) => (n.nodeId === nodeId ? { ...n, ...updates } : n))
    );
  };

  const toggleTokenRef = (tokenKey: string) => {
    setSelectedTokenRefs((prev) =>
      prev.includes(tokenKey) ? prev.filter((k) => k !== tokenKey) : [...prev, tokenKey]
    );
  };

  const toggleLayoutClassRef = (classKey: string) => {
    setLayoutClassRefError(null);
    setSelectedLayoutClassRefs((prev) => {
      const next = prev.includes(classKey)
        ? prev.filter((k) => k !== classKey)
        : [...prev, classKey];
      if (next.length > 0) {
        const resolved = resolveTopologyLayoutClassRefs(next);
        if (!resolved.ok) setLayoutClassRefError(resolved.error);
      }
      return next;
    });
  };

  const applyManualLayoutClassRef = () => {
    const key = manualLayoutClassRef.trim();
    if (!key) return;
    const resolved = resolveTopologyLayoutClassRefs([key]);
    if (!resolved.ok) {
      setLayoutClassRefError(resolved.error);
      return;
    }
    setLayoutClassRefError(null);
    setSelectedLayoutClassRefs((prev) => prev.includes(key) ? prev : [...prev, key]);
    setManualLayoutClassRef("");
  };

  const selectedNode = draftNodes.find((n) => n.nodeId === selectedNodeId) ?? null;
  const canvasPreviewClass = topologyPreviewClasses?.ok ? topologyPreviewClasses.className : "";

  return (
    <div>
      <div class="alert-warn mb-3.5">
        <strong>投影サーフェス境界:</strong> フロントエンドはドラフトレイアウト状態のみを保持します。
        適用は <code>layout_patch:preview → validate → apply</code> 経由 — 直接 DB 書き込みはしません。
      </div>

      <RouteLayoutSelector
        candidates={layoutCandidates}
        routeKey={routeKey}
        layoutId={layoutId}
        onRouteChange={(r) => {
          setRouteKey(r);
          setManualRouteKey("");
          const first = layoutsForRoute(layoutCandidates, r)[0];
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

      <AdvancedManualOverride title="manual override — layoutId / routeKey">
        <div class="flex flex-wrap gap-2">
          <input
            value={manualRouteKey}
            onInput={(e) => setManualRouteKey((e.target as HTMLInputElement).value)}
            placeholder="routeKey 手入力"
            class="input-mono flex-1 text-xs"
          />
          <input
            value={manualLayoutId}
            onInput={(e) => setManualLayoutId((e.target as HTMLInputElement).value)}
            placeholder="layoutId UUID 手入力"
            class="input-mono flex-[2] text-xs"
          />
        </div>
      </AdvancedManualOverride>

      {!canPatch && !selectorsDisabled && (
        <p class="text-sm text-yellow-700 mb-2">ルートとレイアウトを選択してから preview / validate / apply を実行してください。</p>
      )}

      <div class="mb-3 flex min-h-[280px] gap-2.5">
        <LayoutPalette onDragStart={handleDragStartPalette} entries={paletteEntries} status={paletteStatus} />

        <div
          onDragOver={handleDragOverCanvas}
          onDrop={handleDropOnCanvas}
          class={`relative min-h-[200px] flex-1 rounded-lg border-2 border-dashed border-gray-400 bg-gray-50 p-2.5 ${canvasPreviewClass}`}
        >
          <div class="mb-2 text-xs text-gray-400">
            キャンバス — パレットからドロップ。クリックでインスペクター。
          </div>
          {draftNodes.length === 0 && (
            <div class="py-16 text-center font-mono text-sm text-gray-300">
              空です。パレットからコンポーネントをドラッグしてください。
            </div>
          )}
          {draftNodes.map((node, idx) => (
            <div
              key={node.nodeId}
              draggable={true}
              onDragStart={() => handleDragStartCanvas(node.nodeId)}
              onClick={() =>
                setSelectedNodeId(node.nodeId === selectedNodeId ? null : node.nodeId)
              }
              class={`mb-1 flex cursor-grab select-none items-center justify-between rounded border-2 px-2.5 py-1.5 font-mono text-sm ${
                node.nodeId === selectedNodeId
                  ? "border-blue-600 bg-blue-50"
                  : node.isDraftOnly
                  ? "border-yellow-300 bg-yellow-50"
                  : "border-blue-200 bg-white"
              }`}
            >
              <div>
                <span class="font-bold">{node.componentKey}</span>
                {node.isDraftOnly && (
                  <span class="badge-warn ml-1.5">ドラフトのみ — 適用ブロック</span>
                )}
                {node.slotKey && (
                  <span class="ml-2 text-gray-600">スロット:<code>{node.slotKey}</code></span>
                )}
                <span class="ml-2 text-xs text-gray-300">
                  順序:{node.orderIndex} 列:{node.gridCol} 行:{node.gridRow}
                  {node.parentNodeId ? ` 親:${shortId(node.parentNodeId)}` : ""}
                </span>
              </div>
              <div class="flex shrink-0 gap-0.5">
                <button onClick={(e) => { e.stopPropagation(); moveLayoutNode(node.nodeId, "up"); }} disabled={idx === 0} class="btn-secondary px-1.5 py-0 text-xs">↑</button>
                <button onClick={(e) => { e.stopPropagation(); moveLayoutNode(node.nodeId, "down"); }} disabled={idx === draftNodes.length - 1} class="btn-secondary px-1.5 py-0 text-xs">↓</button>
                <button onClick={(e) => { e.stopPropagation(); removeNode(node.nodeId); }} class="btn-danger px-1.5 py-0 text-xs">✕</button>
              </div>
            </div>
          ))}
        </div>

        {selectedNode && (
          <LayoutNodeInspector
            node={selectedNode}
            draftNodes={draftNodes}
            slotKeyCandidates={slotKeyCandidates}
            onUpdate={(updates) => updateNode(selectedNode.nodeId, updates)}
            onClose={() => setSelectedNodeId(null)}
          />
        )}
      </div>

      <Accordion title="topology layout class refs (layoutClassRefs)" defaultOpen={false}>
        <TopologyLayoutClassPicker
          selectedClassRefs={selectedLayoutClassRefs}
          onToggle={toggleLayoutClassRef}
          scopeFilter=""
          allowedForFilter=""
        />
        {layoutClassRefError && (
          <p class="text-red-600 text-sm mt-2">{layoutClassRefError}</p>
        )}
        <AdvancedManualOverride title="manual override — raw classKey（理由必須: SSOT外 ref 検証用）">
          <div class="flex flex-wrap gap-2">
            <input
              value={manualLayoutClassRef}
              onInput={(e) => setManualLayoutClassRef((e.target as HTMLInputElement).value)}
              placeholder="layout.root.grid"
              class="input-mono flex-1 text-xs"
            />
            <button type="button" onClick={applyManualLayoutClassRef} class="btn-secondary text-xs">
              手動 classKey を適用
            </button>
          </div>
        </AdvancedManualOverride>
      </Accordion>

      <Accordion title="CSS トークン参照 (cssTokenRefs)" defaultOpen={false}>
        <CssTokenPicker selectedTokenRefs={selectedTokenRefs} onToggle={toggleTokenRef} />
      </Accordion>

      {draftNodes.some((n) => n.isDraftOnly) && (
        <div class="alert-warn mb-2.5">
          <strong>適用ブロック:</strong>{" "}
          {draftNodes.filter((n) => n.isDraftOnly).length} 件がドラフトのみ。プレビュー・バリデートは可能、適用はブロック。
        </div>
      )}

      <div class="mb-2.5 flex flex-wrap gap-2">
        <button onClick={() => callLayoutPatch("preview")} disabled={loading || !canPatch} class="btn-secondary">1. プレビュー</button>
        <button onClick={() => callLayoutPatch("validate")} disabled={loading || !canPatch} class="btn border border-blue-600 text-blue-600 hover:bg-blue-50">2. バリデート</button>
        <button onClick={() => callLayoutPatch("apply")} disabled={loading || !canPatch} class="btn-success">3. 適用</button>
        {draftNodes.length > 0 && (
          <button onClick={() => { setDraftNodes([]); setSelectedNodeId(null); }} class="btn-danger">キャンバスをクリア</button>
        )}
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

      <Accordion title="debug — tensorPatchJson / raw backend JSON" defaultOpen={false}>
        <p class="text-muted-xs mb-2">開発者向け。通常操作では summary のみ参照してください。</p>
        <pre class="pre-box max-h-40 overflow-y-auto m-0 mb-2">{tensorPatchJson}</pre>
        {debugJson && (
          <pre class="pre-box max-h-[200px] overflow-y-auto border border-gray-200 m-0">{debugJson}</pre>
        )}
      </Accordion>
    </div>
  );
}

// ─── タブナビゲーション ───────────────────────────────────────────────────────

const TABS: { id: TabId; label: string }[] = [
  { id: "ci", label: "CI ガイダンス" },
  { id: "catalog", label: "コンポーネントカタログ" },
  { id: "bucket", label: "バケット管理" },
  { id: "css", label: "CSS トークン" },
  { id: "layout", label: "レイアウトビルダー" },
];

// ─── メインエクスポート ────────────────────────────────────────────────────────

export default function UiBuilderAdmin(): JSX.Element {
  const [activeTab, setActiveTab] = useState<TabId>("ci");

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
