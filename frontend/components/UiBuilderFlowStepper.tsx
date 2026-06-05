import { JSX } from "preact";

/**
 * /admin/ui-builder canvas workspace phase guide.
 * SSOT: admin-console-workflow-ssot.yaml §canvas_workspace_contract
 *
 * Authoring model: two phases (select+package, then canvas workspace edit).
 * No separate layout/design/visual surfaces — all editing in the unified canvas workspace.
 */

export type UiBuilderPhaseId = "package" | "canvas";

/** 通常表示用フェーズラベル（内部 id は主導線に出さない） */
export const UI_BUILDER_PHASE_LABELS: Record<UiBuilderPhaseId, string> = {
  package: "部品選択でパッケージ化",
  canvas: "canvas workspace で配置・デザインを編集",
};

type PhaseSpec = {
  id: number;
  phaseId: UiBuilderPhaseId;
  label: string;
  detail: string;
  note?: string;
};

/** SSOT: canvas_workspace_contract — 2 authoring phases. */
export const UI_BUILDER_CANVAS_PHASES: PhaseSpec[] = [
  {
    id: 1,
    phaseId: "package",
    label: "部品選択でパッケージ化",
    detail:
      "components_bucket から部品を選択し、1 回の package_generator でパッケージ化します。",
    note: "カタログ・CI は画面下部の参照専用セクションにあります。",
  },
  {
    id: 2,
    phaseId: "canvas",
    label: "canvas workspace で配置・デザインを編集",
    detail:
      "パッケージを選んで canvas workspace で配置・デザインを統合編集します。" +
      " 配置（parentNodeId / slotKey / orderIndex / layoutClassRefs）は layout_patch:apply で保存。" +
      " デザイントークン（cssTokenRefs / responsiveTokenRefs / inlineText / linkHref）は component_style_design:upsert で保存。" +
      " canvas が主対象で、tab 切り替えなし・separate surface なし。",
  },
];

export function getActivePhaseIds(phaseId: UiBuilderPhaseId): number[] {
  if (phaseId === "package") return [1];
  if (phaseId === "canvas") return [2];
  return [];
}

export default function UiBuilderFlowStepper({
  activePhase,
}: {
  activePhase: UiBuilderPhaseId;
}): JSX.Element {
  const activePhaseIds = getActivePhaseIds(activePhase);
  const activeLabel = UI_BUILDER_PHASE_LABELS[activePhase] ?? activePhase;

  return (
    <div
      class="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-3"
      role="navigation"
      aria-label="画面づくりの作業フロー"
    >
      <div class="mb-2.5 flex items-center gap-2">
        <span class="text-xs font-semibold text-blue-900">Step 4 — 画面づくり（canvas workspace）</span>
        <span class="text-[0.65rem] text-blue-600">
          — 現在: <strong>{activeLabel}</strong>
        </span>
      </div>

      <div class="flex items-start overflow-x-auto pb-1" role="list">
        {UI_BUILDER_CANVAS_PHASES.map((phase, i) => {
          const isActive = activePhaseIds.includes(phase.id);
          return (
            <div key={phase.id} class="flex items-center" role="listitem">
              {i > 0 && <div class="mx-1 mt-3 h-px w-4 shrink-0 bg-blue-200" aria-hidden="true" />}
              <div class="flex min-w-[120px] max-w-[180px] flex-col items-center">
                <div
                  class={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold ${
                    isActive
                      ? "bg-blue-600 text-white ring-2 ring-blue-300 ring-offset-1"
                      : "border-2 border-gray-300 bg-white text-gray-500"
                  }`}
                  aria-current={isActive ? "step" : undefined}
                >
                  {phase.id}
                </div>
                <div class={`mt-1 text-center text-[0.63rem] font-medium ${isActive ? "text-blue-800" : "text-gray-500"}`}>
                  {phase.label}
                </div>
                {phase.id === 2 && (
                  <a href="/demo" class="mt-1 text-[0.58rem] text-blue-600 underline">
                    確認 →
                  </a>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {UI_BUILDER_CANVAS_PHASES.filter((p) => activePhaseIds.includes(p.id)).map((phase) => (
        <div key={phase.id} class="mt-3 rounded border border-blue-200 bg-white p-2 text-xs">
          <p class="font-semibold text-blue-900">フェーズ {phase.id}: {phase.label}</p>
          <p class="text-gray-700">{phase.detail}</p>
          {phase.note && <p class="text-amber-700">{phase.note}</p>}
        </div>
      ))}
    </div>
  );
}
