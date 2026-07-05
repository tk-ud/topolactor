import { JSX } from "preact";
import { useState } from "preact/hooks";
import type { AggregateTriggerProcessingFunctionCandidate } from "../api/adminApi.ts";
import {
  UX_AGGREGATE_TRIGGER_CONFIGURED_SUMMARY,
  UX_AGGREGATE_TRIGGER_GATE_HINT,
  UX_AGGREGATE_TRIGGER_MODAL_CLOSE,
  UX_AGGREGATE_TRIGGER_MODAL_DONE,
  UX_AGGREGATE_TRIGGER_OPEN_SETTINGS,
  UX_AGGREGATE_TRIGGER_SECTION_INTRO,
  UX_AGGREGATE_TRIGGER_SECTION_TITLE,
} from "../content/adminUxTerms.ts";
import type { AggregateTriggerDefinitionPayload, StepTarget } from "../lib/aggregateTriggerAuthoring.ts";
import { hasConfiguredAggregationLogic } from "../lib/aggregationMeasures.ts";
import type { AggregationBlock } from "../lib/aggregationMeasures.ts";
import AggregateTriggerAuthoringPanel from "./AggregateTriggerAuthoringPanel.tsx";
import { Modal } from "./Modal.tsx";

type Props = {
  aggregationBlocks: AggregationBlock[];
  step2LogicalEntityDefinitions: StepTarget[];
  step25RelationDefinitions: StepTarget[];
  topologySystemName?: string;
  processingFunctionCandidates?: AggregateTriggerProcessingFunctionCandidate[];
  processingFunctionCandidatesError?: string | null;
  aggregateTriggerDefinitions: AggregateTriggerDefinitionPayload[];
  onPayloadChange: (payload: AggregateTriggerDefinitionPayload[]) => void;
};

export default function AggregateTriggerAuthoringSurface({
  aggregationBlocks,
  step2LogicalEntityDefinitions,
  step25RelationDefinitions,
  topologySystemName,
  processingFunctionCandidates,
  processingFunctionCandidatesError,
  aggregateTriggerDefinitions,
  onPayloadChange,
}: Props): JSX.Element {
  const aggregationReady = hasConfiguredAggregationLogic(aggregationBlocks);
  const [modalOpen, setModalOpen] = useState(false);
  const hasDraftPayload = aggregateTriggerDefinitions.length > 0;

  if (!aggregationReady) {
    return (
      <p class="mt-3 rounded border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-600">
        {UX_AGGREGATE_TRIGGER_GATE_HINT}
      </p>
    );
  }

  return (
    <div class="mt-3 rounded border border-indigo-100 bg-white p-3 text-xs">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p class="font-semibold text-indigo-950">
            {UX_AGGREGATE_TRIGGER_SECTION_TITLE}
          </p>
          <p class="mt-1 text-slate-600">
            {hasDraftPayload
              ? UX_AGGREGATE_TRIGGER_CONFIGURED_SUMMARY
              : "集計実行時のイベント記録・閾値・実体化を詳細設定できます（任意）。"}
          </p>
          {processingFunctionCandidatesError && (
            <p class="mt-2 text-amber-800" role="alert">
              {processingFunctionCandidatesError}
            </p>
          )}
        </div>
        <button
          type="button"
          class="btn-secondary shrink-0 text-xs"
          onClick={() => setModalOpen(true)}
        >
          {UX_AGGREGATE_TRIGGER_OPEN_SETTINGS}
        </button>
      </div>

      <Modal
        open={modalOpen}
        title={UX_AGGREGATE_TRIGGER_SECTION_TITLE}
        description={UX_AGGREGATE_TRIGGER_SECTION_INTRO}
        onClose={() => setModalOpen(false)}
        design={{
          style:
            "background:#fff;border-radius:8px;padding:20px;max-width:min(56rem,95vw);width:100%;max-height:90vh;overflow-y:auto;box-shadow:0 4px 24px rgba(0,0,0,0.18);font-family:inherit",
        }}
        footer={
          <div class="flex justify-end gap-2">
            <button
              type="button"
              class="btn-secondary text-xs"
              onClick={() => setModalOpen(false)}
            >
              {UX_AGGREGATE_TRIGGER_MODAL_CLOSE}
            </button>
            <button
              type="button"
              class="btn-primary text-xs"
              onClick={() => setModalOpen(false)}
            >
              {UX_AGGREGATE_TRIGGER_MODAL_DONE}
            </button>
          </div>
        }
      >
        <AggregateTriggerAuthoringPanel
          embedded
          step2LogicalEntityDefinitions={step2LogicalEntityDefinitions}
          step25RelationDefinitions={step25RelationDefinitions}
          topologySystemName={topologySystemName}
          processingFunctionCandidates={processingFunctionCandidates}
          onPayloadChange={onPayloadChange}
        />
      </Modal>
    </div>
  );
}
