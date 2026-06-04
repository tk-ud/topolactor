import { useState, useEffect, useMemo } from "preact/hooks";
import { JSX } from "preact";
import type { UserOperation, OperationType } from "../runtime/resolveOperationVector.ts";
import { resolveOperationVector } from "../runtime/resolveOperationVector.ts";
import {
  buildDispatchContext,
  inferPresetId,
  presetById,
  presetHasContext,
  presetOperationKey,
  presetsForGroups,
  type OperationPreset,
  type OperationPresetGroup,
} from "../runtime/operationPresets.ts";
import { summarizeEmission } from "../runtime/emissionSummary.ts";
import { emitComponentOperationEvent, queueClientCommand, startComponentEventRuntime } from "../runtime/frontendScheduler.ts";
import type { Emission } from "../api/dispatch.ts";
import { EmissionView } from "../components/EmissionView.tsx";
import { SqlAttentionProjectionPanel } from "../components/SqlAttentionProjectionPanel.tsx";
import { fetchSqlAttentionProjection, type SqlAttentionProjectionResult } from "../api/sqlAttentionProjection.ts";

const SESSION_TOKEN_KEY = "demo_jwt_token";

const OPERATION_TYPES: OperationType[] = [
  "Search",
  "Create",
  "diffUpdate",
  "logicalDelete",
];

export type OperationPanelMode = "home" | "demo";

type Props = {
  mode?: OperationPanelMode;
  initialOperation?: Partial<UserOperation>;
  initialPresetId?: string;
};

function applyPresetToForm(
  preset: OperationPreset,
  setters: {
    setTarget: (v: string) => void;
    setLayer: (v: string) => void;
    setAction: (v: string) => void;
    setOperationType: (v: OperationType) => void;
  },
): void {
  const { operation } = preset;
  setters.setTarget(operation.target);
  setters.setLayer(operation.layer);
  setters.setAction(operation.action);
  setters.setOperationType(operation.operationType);
}

export default function OperationPanel({
  mode = "home",
  initialOperation,
  initialPresetId,
}: Props): JSX.Element {
  const presetGroups: OperationPresetGroup[] = mode === "demo" ? ["demo"] : ["default", "demo"];
  const availablePresets = useMemo(() => presetsForGroups(presetGroups), [mode]);

  const defaultPresetId = initialPresetId ??
    inferPresetId(initialOperation, presetGroups);
  const initialPreset = presetById(defaultPresetId) ?? availablePresets[0];
  const initialOp = initialPreset?.operation;

  const [selectedPresetId, setSelectedPresetId] = useState(defaultPresetId);
  const [target, setTarget] = useState(initialOp?.target ?? initialOperation?.target ?? "default");
  const [layer, setLayer] = useState(initialOp?.layer ?? initialOperation?.layer ?? "entity");
  const [action, setAction] = useState(initialOp?.action ?? initialOperation?.action ?? "Search");
  const [operationType, setOperationType] = useState<OperationType>(
    (initialOp?.operationType ?? initialOperation?.operationType ?? "Search") as OperationType,
  );
  const [contextSessionId, setContextSessionId] = useState("");
  const [contextTokenIds, setContextTokenIds] = useState("");

  const [token, setToken] = useState<string | null>(null);
  const [emission, setEmission] = useState<Emission | null>(null);
  const [dispatchSuccess, setDispatchSuccess] = useState<boolean | null>(null);
  const [loading, setLoading] = useState(false);
  const [vectorPreview, setVectorPreview] = useState<string | null>(null);
  const [rawEmissionJson, setRawEmissionJson] = useState<string | null>(null);
  const [projectionSourceSetId, setProjectionSourceSetId] = useState("");
  const [projection, setProjection] = useState<SqlAttentionProjectionResult | null>(null);

  const selectedPreset = presetById(selectedPresetId) ?? availablePresets[0];

  useEffect(() => {
    startComponentEventRuntime();
    setToken(sessionStorage.getItem(SESSION_TOKEN_KEY));
  }, []);

  useEffect(() => {
    if (selectedPreset) {
      applyPresetToForm(selectedPreset, { setTarget, setLayer, setAction, setOperationType });
    }
  }, [selectedPresetId]);

  function selectPreset(id: string): void {
    setSelectedPresetId(id);
    const preset = presetById(id);
    if (!preset) return;
    applyPresetToForm(preset, { setTarget, setLayer, setAction, setOperationType });
    emitComponentOperationEvent({
      componentId: "operation-panel:preset-select",
      packageId: "runtime-operation-panel",
      layoutId: "runtime-operation-layout",
      eventType: "select",
      actorOrSource: "OperationPanel",
      payload: { presetId: id },
    });
  }

  const effectiveContext = selectedPreset
    ? buildDispatchContext(selectedPreset, {
      ContextSessionId: contextSessionId,
      ContextTokenIds: contextTokenIds,
    })
    : {};

  const hasContext = Object.keys(effectiveContext).length > 0;
  const operationKey = selectedPreset
    ? presetOperationKey(selectedPreset)
    : resolveOperationVector({ operationType, target, layer, action }).attractorKey;

  async function handleDispatch(): Promise<void> {
    const currentToken = sessionStorage.getItem(SESSION_TOKEN_KEY);
    setToken(currentToken);

    const op: UserOperation = { operationType, target, layer, action };
    const vector = resolveOperationVector(op);
    setVectorPreview(JSON.stringify(vector, null, 2));
    setEmission(null);
    setRawEmissionJson(null);
    setDispatchSuccess(null);
    setLoading(true);

    emitComponentOperationEvent({
      componentId: "operation-panel:dispatch-form",
      packageId: "runtime-operation-panel",
      layoutId: "runtime-operation-layout",
      eventType: "submit",
      actorOrSource: "OperationPanel",
      payload: {
        presetId: selectedPresetId,
        operationType,
        target,
        layer,
        action,
        hasContext,
      },
    });

    const response = await queueClientCommand(
      op,
      currentToken ?? undefined,
      effectiveContext,
    );

    setLoading(false);
    setDispatchSuccess(response.success);

    if (response.emission) {
      setEmission(response.emission);
      setRawEmissionJson(JSON.stringify(response.emission, null, 2));
    } else {
      setEmission({
        errors: response.errors ?? [{ message: "dispatch: emission が返されませんでした" }],
      });
      setRawEmissionJson(
        JSON.stringify({ errors: response.errors ?? [] }, null, 2),
      );
    }
  }

  async function handleLoadProjection(): Promise<void> {
    const currentToken = sessionStorage.getItem(SESSION_TOKEN_KEY);
    setToken(currentToken);
    const response = await fetchSqlAttentionProjection(
      projectionSourceSetId.trim(),
      currentToken ?? undefined,
    );
    if (response.success && response.result) {
      setProjection(response.result);
      return;
    }
    setProjection({
      status: "db_unavailable",
      statusDetail: response.errors?.[0]?.message ?? "投影の取得に失敗しました",
      candidates: [],
      evaluatedAt: new Date().toISOString(),
      lane: "sql_attention_projection",
    });
  }

  const emissionSummary = emission ? summarizeEmission(emission) : null;

  const defaultPresets = availablePresets.filter((p) => p.group === "default");
  const demoPresets = availablePresets.filter((p) => p.group === "demo");

  function renderPresetCard(preset: OperationPreset): JSX.Element {
    const selected = preset.id === selectedPresetId;
    return (
      <button
        key={preset.id}
        type="button"
        onClick={() => selectPreset(preset.id)}
        class={`w-full rounded-lg border p-4 text-left transition ${
          selected
            ? "border-blue-600 bg-blue-50 ring-1 ring-blue-600"
            : "border-gray-200 bg-white hover:border-gray-300"
        }`}
      >
        <div class="font-semibold text-gray-900">{preset.label}</div>
        <p class="mt-1 text-sm text-gray-600">{preset.description}</p>
        {presetHasContext(preset) && (
          <span class="mt-2 inline-block rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
            コンテキスト付き（seed demo session/token）
          </span>
        )}
      </button>
    );
  }

  return (
    <div class="operation-panel max-w-2xl">
      <h2 class="section-title">Runtime 操作</h2>

      <div class={token ? "alert-success mb-4" : "alert-warn mb-4"}>
        {token
          ? (
            <>
              <strong>認証済み。</strong> 操作カードを選び「実行」で backend に dispatch します。{" "}
              <a href="/auth" class="link text-sm">再ログイン</a>
            </>
          )
          : (
            <>
              <strong>未ログイン。</strong> 先に{" "}
              <a href="/auth" class="link font-semibold">ログイン</a>
              {" "}してください。トークンなしの dispatch は AUTH_TOKEN_MISSING になります。
            </>
          )}
      </div>

      <section class="mb-6">
        <h3 class="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">
          シナリオを選択
        </h3>
        {mode === "home" && defaultPresets.length > 0 && (
          <div class="mb-4 space-y-2">
            <p class="text-xs font-medium text-gray-500">Default runtime</p>
            <div class="space-y-2">{defaultPresets.map(renderPresetCard)}</div>
          </div>
        )}
        {demoPresets.length > 0 && (
          <div class="space-y-2">
            {mode === "home" && (
              <p class="text-xs font-medium text-gray-500">Demo topology（seed データ）</p>
            )}
            <div class="space-y-2">{demoPresets.map(renderPresetCard)}</div>
          </div>
        )}
      </section>

      <section class="card mb-4">
        <h3 class="mb-2 font-semibold">選択中の操作</h3>
        <dl class="space-y-1 text-sm text-gray-700">
          <div class="flex gap-2">
            <dt class="font-medium text-gray-500 w-28 shrink-0">操作キー</dt>
            <dd class="font-mono">{operationKey}</dd>
          </div>
          <div class="flex gap-2">
            <dt class="font-medium text-gray-500 w-28 shrink-0">route</dt>
            <dd class="font-mono">
              {target}:{layer}:{action} ({operationType})
            </dd>
          </div>
          <div class="flex gap-2">
            <dt class="font-medium text-gray-500 w-28 shrink-0">コンテキスト</dt>
            <dd>{hasContext ? "あり（レコメンド等）" : "なし"}</dd>
          </div>
          {selectedPreset && (
            <div class="flex gap-2">
              <dt class="font-medium text-gray-500 w-28 shrink-0 align-top">観測の目安</dt>
              <dd class="text-gray-600">{selectedPreset.expectedObservation}</dd>
            </div>
          )}
        </dl>
        <button
          type="button"
          onClick={handleDispatch}
          disabled={loading}
          class="btn-primary mt-4"
        >
          {loading ? "ディスパッチ中..." : "選択した操作を実行"}
        </button>
        <p class="mt-2 text-muted-xs">
          POST /api/dispatch → frontend scheduler → backend RuntimeExecutor → emission 投影
        </p>
      </section>

      {loading && (
        <p class="mb-4 text-sm text-gray-500" aria-live="polite">
          /api/dispatch へディスパッチ中...
        </p>
      )}

      {emission && emissionSummary && (
        <section class="card mb-4" aria-live="polite">
          <h3 class="mb-2 font-semibold">実行結果（要約）</h3>
          <div
            class={dispatchSuccess && emissionSummary.ok
              ? "alert-success mb-3"
              : "alert-warn mb-3"}
          >
            <strong>
              {dispatchSuccess && emissionSummary.ok ? "成功" : "エラーまたは部分失敗"}
            </strong>
            {emissionSummary.errorMessages.length > 0 && (
              <ul class="mt-1 list-inside list-disc text-sm">
                {emissionSummary.errorMessages.map((msg, i) => (
                  <li key={i}>{msg}</li>
                ))}
              </ul>
            )}
          </div>
          <dl class="space-y-1 text-sm font-mono">
            <div class="flex gap-2">
              <dt class="text-gray-500 w-36 shrink-0">structureMapId</dt>
              <dd>{emissionSummary.structureMapId ?? "—"}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="text-gray-500 w-36 shrink-0">packageId</dt>
              <dd>{emissionSummary.packageId ?? "—"}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="text-gray-500 w-36 shrink-0">schemaId</dt>
              <dd>{emissionSummary.schemaId ?? "—"}</dd>
            </div>
            <div class="flex gap-2">
              <dt class="text-gray-500 w-36 shrink-0">components</dt>
              <dd>{emissionSummary.componentCount}</dd>
            </div>
            {emissionSummary.recommendationStatus && (
              <div class="flex gap-2">
                <dt class="text-gray-500 w-36 shrink-0">recommendation</dt>
                <dd>
                  {emissionSummary.recommendationStatus}
                  {emissionSummary.recommendationDetail
                    ? ` — ${emissionSummary.recommendationDetail}`
                    : ""}
                </dd>
              </div>
            )}
          </dl>
        </section>
      )}

      <details class="mb-4 rounded border border-gray-200 p-3">
        <summary class="cursor-pointer text-sm font-medium text-gray-700">
          Advanced: raw dispatch vector（開発者 / デバッグ）
        </summary>
        <p class="mt-2 text-muted-xs">
          通常操作では上のシナリオカードを使ってください。ここで不正な target/layer/action を送ると
          backend は explicit error を返します（サイレントフォールバックなし）。正本ルートは preset です。
        </p>
        <div class="mt-3 space-y-3">
          <label class="block text-sm font-medium text-gray-700">
            ターゲット
            <input
              class="input-mono mt-1"
              type="text"
              value={target}
              onInput={(e) => setTarget((e.target as HTMLInputElement).value)}
            />
          </label>
          <label class="block text-sm font-medium text-gray-700">
            レイヤー
            <input
              class="input-mono mt-1"
              type="text"
              value={layer}
              onInput={(e) => setLayer((e.target as HTMLInputElement).value)}
            />
          </label>
          <label class="block text-sm font-medium text-gray-700">
            アクション
            <input
              class="input-mono mt-1"
              type="text"
              value={action}
              onInput={(e) => setAction((e.target as HTMLInputElement).value)}
            />
          </label>
          <label class="block text-sm font-medium text-gray-700">
            操作タイプ
            <select
              class="input-mono mt-1"
              value={operationType}
              onChange={(e) =>
                setOperationType((e.target as HTMLSelectElement).value as OperationType)}
            >
              {OPERATION_TYPES.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
          </label>
        </div>
      </details>

      <details class="mb-4 rounded border border-gray-200 p-3">
        <summary class="cursor-pointer text-sm font-medium text-gray-700">
          Advanced: context session / token IDs
        </summary>
        <p class="mt-2 text-muted-xs">
          レコメンド用コンテキスト。通常は「Demo hub + recommendation context」preset が
          seed UUID を自動設定します。手入力はデバッグ用です。
        </p>
        <div class="mt-3 space-y-3">
          <label class="block text-sm font-medium text-gray-700">
            コンテキストセッション ID
            <input
              class="input-mono mt-1"
              type="text"
              placeholder={`例: ${selectedPreset?.context?.ContextSessionId ?? "session uuid"}`}
              value={contextSessionId}
              onInput={(e) => setContextSessionId((e.target as HTMLInputElement).value)}
            />
          </label>
          <label class="block text-sm font-medium text-gray-700">
            コンテキストトークン ID（カンマ区切り）
            <input
              class="input-mono mt-1"
              type="text"
              placeholder={`例: ${selectedPreset?.context?.ContextTokenIds ?? "token uuid"}`}
              value={contextTokenIds}
              onInput={(e) => setContextTokenIds((e.target as HTMLInputElement).value)}
            />
          </label>
        </div>
      </details>

      <details class="mb-4 rounded border border-gray-200 p-3">
        <summary class="cursor-pointer text-sm font-medium text-gray-700">
          Advanced: SQL Attention projection
        </summary>
        <p class="mt-2 text-muted-xs">
          sourceSetId を直接指定して SQL Attention 投影を取得します（runtime dispatch とは別経路）。
          TODO: sourceSetId 候補一覧 API が無いため、現状は手入力のみ。候補選択 UI は API 追加後に実装。
        </p>
        <div class="mt-3">
          <label class="block text-sm font-medium text-gray-700">
            SQL Attention sourceSetId
            <input
              class="input-mono mt-1"
              type="text"
              value={projectionSourceSetId}
              onInput={(e) => setProjectionSourceSetId((e.target as HTMLInputElement).value)}
              placeholder="source_set_id"
            />
          </label>
          <button
            type="button"
            onClick={handleLoadProjection}
            disabled={!projectionSourceSetId.trim()}
            class="btn-secondary mt-2"
          >
            SQL Attention 投影をロード
          </button>
        </div>
        {projection && (
          <div class="mt-3">
            <SqlAttentionProjectionPanel
              status={projection.status}
              statusDetail={projection.statusDetail}
              candidates={projection.candidates}
            />
          </div>
        )}
      </details>

      {emission && (
        <details class="mb-4 rounded border border-gray-200 p-3">
          <summary class="cursor-pointer text-sm font-medium text-gray-700">
            Debug: emission 詳細（EmissionView）
          </summary>
          <div class="mt-3">
            <EmissionView emission={emission} />
          </div>
        </details>
      )}

      {(vectorPreview || rawEmissionJson) && (
        <details class="rounded border border-dashed border-gray-300 p-3">
          <summary class="cursor-pointer text-sm text-gray-600">
            Debug: raw JSON（OperationVector / emission）
          </summary>
          {vectorPreview && (
            <div class="mt-3">
              <h4 class="mb-1 text-xs font-semibold text-gray-500">OperationVector（直前）</h4>
              <pre class="pre-box text-xs">{vectorPreview}</pre>
            </div>
          )}
          {rawEmissionJson && (
            <div class="mt-3">
              <h4 class="mb-1 text-xs font-semibold text-gray-500">Raw emission JSON</h4>
              <pre class="pre-box text-xs">{rawEmissionJson}</pre>
            </div>
          )}
        </details>
      )}
    </div>
  );
}
