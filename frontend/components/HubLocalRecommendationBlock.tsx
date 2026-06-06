import { JSX } from "preact";
import type { RecommendProjectionSection } from "../api/dispatch.ts";

export function HubLocalRecommendationBlock(
  { sections }: { sections: RecommendProjectionSection[] },
): JSX.Element {
  const uiSections = sections.filter((section) => section.lane === "ui_pressure");
  const stateSections = sections.filter((section) => section.lane === "state_pressure");
  const renderSections = [
    ...(uiSections.length > 0 ? uiSections : [undefined]),
    ...(stateSections.length > 0 ? stateSections : [undefined]),
  ];

  return (
    <div class="grid gap-3 md:grid-cols-2" data-recommend-scope="hub-local">
      {renderSections.map((section, index) => (
        <RecommendationLaneSection
          key={section ? `${section.lane}:${section.candidateKind}` : `missing-${index}`}
          section={section}
          fallbackLane={index === 0 ? "ui_pressure" : "state_pressure"}
          fallbackTitle={index === 0 ? "UI / operation pressure" : "State / enum pressure"}
        />
      ))}
    </div>
  );
}

function RecommendationLaneSection(
  { section, fallbackLane, fallbackTitle }: {
    section?: RecommendProjectionSection;
    fallbackLane: "ui_pressure" | "state_pressure";
    fallbackTitle: string;
  },
): JSX.Element {
  const status = section?.status ?? "explicit_unavailable";
  const candidates = section?.candidates ?? [];
  return (
    <section class="rounded border border-gray-200 bg-white p-3" data-recommend-lane={fallbackLane}>
      <h4 class="m-0 text-sm font-semibold">{section?.title ?? fallbackTitle}</h4>
      <p class="mt-1 text-xs text-gray-500">
        lane: <code>{section?.lane ?? fallbackLane}</code>
        {section?.candidateKind && <> / kind: <code>{section.candidateKind}</code></>}
      </p>
      <p class="mt-1 text-xs">
        status: <code class={status === "ok" ? "text-green-700" : status === "explicit_error" ? "text-red-700" : "text-yellow-700"}>{status}</code>
        {section?.statusDetail && <span class="text-gray-500"> — {section.statusDetail}</span>}
      </p>
      {status === "ok" && candidates.length > 0 ? (
        <ol class="mt-2 list-decimal space-y-1 pl-5 text-sm">
          {candidates.map((candidate) => (
            <li key={`${candidate.lane}:${candidate.value}`}>
              <code>{candidate.value}</code> — score: {candidate.score.toFixed(3)}
              <small class="ml-1 text-gray-500">[{candidate.lane}]</small>
              {candidate.evidence.length > 0 && (
                <small class="ml-1 text-gray-500">({candidate.evidence.join(", ")})</small>
              )}
              {candidate.runtimeDispatchSpec ? (
                <small class="ml-1 text-green-700">runtimeDispatchSpec resolved</small>
              ) : (
                <small class="ml-1 text-gray-400">non-executable</small>
              )}
            </li>
          ))}
        </ol>
      ) : status === "ok" ? (
        <p class="mt-2 text-sm text-gray-400">候補はありません。</p>
      ) : (
        <p class="mt-2 text-sm text-gray-500">この lane は現在利用できません。</p>
      )}
    </section>
  );
}
