import { useEffect, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  buildDispatchContext,
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

type ScenarioOption = {
  id: string;
  title: string;
  description: string;
};

const DEMO_SCENARIOS: ScenarioOption[] = [
  {
    id: "demo_hub_overview",
    title: "全体を見る",
    description: "デモのトップ画面を表示します。",
  },
  {
    id: "demo_entity_list",
    title: "候補を選ぶ",
    description: "一覧から候補を探します。",
  },
  {
    id: "demo_hub_recommendation",
    title: "おすすめを見る",
    description: "あなたの履歴に基づいたレコメンドを確認します。",
  },
];

const STEP_LABELS: Record<Step, string> = {
  1: "目的を選ぶ",
  2: "結果を見る",
  3: "次にすること",
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

export default function UserDemoStepper(): JSX.Element {
  const [step, setStep] = useState<Step>(1);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [emission, setEmission] = useState<Emission | null>(null);
  const [token, setToken] = useState<string | null>(null);

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
  const userResult = emissionSummary ? toUserFacingResult(emissionSummary) : null;

  return (
    <div class="user-demo-stepper max-w-2xl">
      <StepBar current={step} />

      {step === 1 && (
        <section>
          <h2 class="mb-2 text-lg font-semibold text-gray-900">
            何を見たいですか？
          </h2>
          <p class="mb-5 text-sm text-gray-500">
            シナリオカードを選んでください。
          </p>
          {!token && (
            <div class="alert-warn mb-5">
              <strong>ログインが必要です。</strong>{" "}
              先に{" "}
              <a href="/auth" class="link font-semibold">
                ログイン
              </a>{" "}
              してください。
            </div>
          )}
          <div class="space-y-3">
            {DEMO_SCENARIOS.map((s) => (
              <button
                key={s.id}
                type="button"
                onClick={() => runScenario(s.id)}
                class="w-full rounded-lg border border-gray-200 bg-white p-5 text-left transition hover:border-blue-400 hover:shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <p class="font-semibold text-gray-900">{s.title}</p>
                <p class="mt-1 text-sm text-gray-500">{s.description}</p>
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
          <h2 class="mb-4 text-lg font-semibold text-gray-900">結果</h2>
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
