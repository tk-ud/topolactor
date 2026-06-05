import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  buildDispatchContext,
  demoPreviewOptions,
  presetById,
} from "../runtime/operationPresets.ts";
import {
  summarizeEmission,
  toUserFacingResult,
} from "../runtime/emissionSummary.ts";
import {
  queueClientCommand,
  startComponentEventRuntime,
} from "../runtime/frontendScheduler.ts";
import type { Emission } from "../api/dispatch.ts";
import { UserDemoResultCard } from "../components/UserDemoResultCard.tsx";
import { UserDemoNextActions } from "../components/UserDemoNextActions.tsx";

const SESSION_TOKEN_KEY = "demo_jwt_token";

type Step = 1 | 2 | 3;

const STEP_LABELS: Record<Step, string> = {
  1: "投影を選ぶ",
  2: "結果を見る",
  3: "次の確認へ",
};

function StepBar({ current }: { current: Step }): JSX.Element {
  return (
    <ol class="mb-8 flex flex-wrap items-center gap-1" aria-label="手順">
      {([1, 2, 3] as Step[]).map((n, i) => {
        const done = n < current;
        const active = n === current;
        return (
          <li key={n} class="flex items-center">
            <span
              class={`flex h-7 w-7 items-center justify-center rounded-full text-sm font-semibold ${
                done
                  ? "bg-blue-600 text-white"
                  : active
                  ? "bg-blue-100 text-blue-700 ring-2 ring-blue-500"
                  : "bg-gray-100 text-gray-400"
              }`}
              aria-current={active ? "step" : undefined}
            >
              {done ? "✓" : n}
            </span>
            <span
              class={`ml-2 text-sm ${
                active ? "font-semibold text-gray-900" : "text-gray-400"
              }`}
            >
              {STEP_LABELS[n]}
            </span>
            {i < 2 && (
              <span class="mx-3 text-gray-300" aria-hidden="true">
                →
              </span>
            )}
          </li>
        );
      })}
    </ol>
  );
}

/**
 * /demo preview surface の audit navigation wrapper。
 *
 * UX 定義 authority は持たない。選択肢は operationPresets.ts の demo group preset
 * (previewLabel を持つエントリ) から取得する。admin で構築・seed 済みの project
 * projection を選択・実行・監査するだけであり、topology / manifest / projection
 * の構築・変更は行わない。
 *
 * raw runtime 検証は /demo/debug、admin 構築面は /admin を利用。
 */
export default function UserDemoStepper(): JSX.Element {
  const [step, setStep] = useState<Step>(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [emission, setEmission] = useState<Emission | null>(null);
  const [token, setToken] = useState<string | null>(null);

  // Preview options are derived from operationPresets; not defined here.
  const previewOptions = demoPreviewOptions();

  useEffect(() => {
    startComponentEventRuntime();
    setToken(sessionStorage.getItem(SESSION_TOKEN_KEY));
  }, []);

  async function runScenario(scenarioId: string): Promise<void> {
    const preset = presetById(scenarioId);
    if (!preset) return;

    setSelectedId(scenarioId);
    setLoading(true);
    setStep(2);
    setEmission(null);

    const currentToken = sessionStorage.getItem(SESSION_TOKEN_KEY);
    setToken(currentToken);

    const context = buildDispatchContext(preset);
    const response = await queueClientCommand(
      preset.operation,
      currentToken ?? undefined,
      context,
    );

    setLoading(false);

    if (response.emission) {
      setEmission(response.emission);
    } else {
      setEmission({
        errors: response.errors ?? [{ message: "結果を取得できませんでした" }],
      });
    }
    setStep(3);
  }

  function handleReset(): void {
    setStep(1);
    setSelectedId(null);
    setEmission(null);
  }

  const emissionSummary = emission ? summarizeEmission(emission) : null;
  const userResult = emissionSummary
    ? toUserFacingResult(emissionSummary)
    : null;

  return (
    <div class="user-demo-stepper max-w-2xl">
      <StepBar current={step} />

      {step === 1 && (
        <section>
          <h2 class="mb-2 text-lg font-semibold text-gray-900">
            確認したい画面を選んでください
          </h2>
          <p class="mb-5 text-sm text-gray-500">
            設定済みページの表示と操作を確認できます。
          </p>
          {!token && (
            <div class="alert-warn mb-5">
              <strong>ログインが必要です。</strong> 先に{" "}
              <a href="/auth" class="link font-semibold">
                ログイン
              </a>{" "}
              してください。
            </div>
          )}
          <div class="space-y-3">
            {previewOptions.map((opt) => (
              <button
                key={opt.id}
                type="button"
                onClick={() => runScenario(opt.id)}
                class="w-full rounded-lg border border-gray-200 bg-white p-5 text-left transition hover:border-blue-400 hover:shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <p class="font-semibold text-gray-900">{opt.previewLabel}</p>
                <p class="mt-1 text-sm text-gray-500">
                  {opt.previewDescription}
                </p>
              </button>
            ))}
          </div>
        </section>
      )}

      {step === 2 && loading && (
        <div
          class="py-12 text-center"
          aria-live="polite"
          aria-busy="true"
        >
          <p class="text-gray-500">取得中...</p>
        </div>
      )}

      {step === 3 && userResult && !loading && (
        <section>
          <h2 class="mb-4 text-lg font-semibold text-gray-900">確認結果</h2>
          <UserDemoResultCard result={userResult} />
          <UserDemoNextActions
            result={userResult}
            currentScenarioId={selectedId}
            onRelated={(id) => runScenario(id)}
            onRetry={handleReset}
          />
        </section>
      )}
    </div>
  );
}
