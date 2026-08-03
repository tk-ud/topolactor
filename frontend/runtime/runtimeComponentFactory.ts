import { h, type JSX, type VNode } from "preact";
import { Modal } from "../components/Modal.tsx";
import { Button } from "../components/Button.tsx";
import { Card } from "../components/Card.tsx";
import { CardList } from "../components/CardList.tsx";
import { Input } from "../components/Input.tsx";
import { Table } from "../components/Table.tsx";
import { SelectImportDialog } from "../components/SelectImportDialog.tsx";
import { AutoCompleteInput } from "../components/AutoCompleteInput.tsx";
import { SearchCombobox } from "../components/SearchCombobox.tsx";
import { CandidateConfidenceBadge } from "../components/CandidateConfidenceBadge.tsx";
import { InlineEditableField } from "../components/InlineEditableField.tsx";
import { PatchPreviewPanel } from "../components/PatchPreviewPanel.tsx";
import { ApplyConfirmDialog } from "../components/ApplyConfirmDialog.tsx";
import { StyleTokenPicker } from "../components/StyleTokenPicker.tsx";
import { ThemePreviewPanel } from "../components/ThemePreviewPanel.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { SuggestInput } from "../components/SuggestInput.tsx";
import { RecentInputSuggest } from "../components/RecentInputSuggest.tsx";
import { RelationCandidatePicker } from "../components/RelationCandidatePicker.tsx";
import { DuplicateMergeCandidatePanel } from "../components/DuplicateMergeCandidatePanel.tsx";
import { RelationPathPreview } from "../components/RelationPathPreview.tsx";
import { FieldResolverInspector } from "../components/FieldResolverInspector.tsx";
import { SchemaPromotionCandidatePanel } from "../components/SchemaPromotionCandidatePanel.tsx";
import { InlineEditableJsonbField } from "../components/InlineEditableJsonbField.tsx";
import { DiffStrikeText } from "../components/DiffStrikeText.tsx";
import { AuditDiffDrawer } from "../components/AuditDiffDrawer.tsx";
import { OptimisticUpdateBoundary } from "../components/OptimisticUpdateBoundary.tsx";
import { ConfirmedUpdateButton } from "../components/ConfirmedUpdateButton.tsx";
import { UndoTimeline } from "../components/UndoTimeline.tsx";
import { ConflictResolutionPanel } from "../components/ConflictResolutionPanel.tsx";
import { FacetedFilterBar } from "../components/FacetedFilterBar.tsx";
import { ColumnFilter } from "../components/ColumnFilter.tsx";
import { ColumnVisibilityEditor } from "../components/ColumnVisibilityEditor.tsx";
import { SortControl } from "../components/SortControl.tsx";
import { GroupByControl } from "../components/GroupByControl.tsx";
import { SavedViewSelector } from "../components/SavedViewSelector.tsx";
import { BulkActionPanel } from "../components/BulkActionPanel.tsx";
import { VirtualizedDataTable } from "../components/VirtualizedDataTable.tsx";
import { RowDetailDrawer } from "../components/RowDetailDrawer.tsx";
import { PaginationControl } from "../components/PaginationControl.tsx";
import { ExportCandidatePanel } from "../components/ExportCandidatePanel.tsx";
import { FontTokenEditor } from "../components/FontTokenEditor.tsx";
import { BackgroundColorEditor } from "../components/BackgroundColorEditor.tsx";
import { TextColorEditor } from "../components/TextColorEditor.tsx";
import { SpacingTokenEditor } from "../components/SpacingTokenEditor.tsx";
import { BorderRadiusEditor } from "../components/BorderRadiusEditor.tsx";
import { CssVariablePreview } from "../components/CssVariablePreview.tsx";
import { ShadowTokenEditor } from "../components/ShadowTokenEditor.tsx";
import { AnimationTokenEditor } from "../components/AnimationTokenEditor.tsx";
import { CommandPalette } from "../components/CommandPalette.tsx";
import { EmptyStateActionPanel } from "../components/EmptyStateActionPanel.tsx";
import { OperationGuardBanner } from "../components/OperationGuardBanner.tsx";
import { MutationBoundaryInspector } from "../components/MutationBoundaryInspector.tsx";
import { PermissionHintPanel } from "../components/PermissionHintPanel.tsx";
import { DryRunResultPanel } from "../components/DryRunResultPanel.tsx";
import { RollbackCandidatePanel } from "../components/RollbackCandidatePanel.tsx";
import { OperationAuditLogPanel } from "../components/OperationAuditLogPanel.tsx";
import { FormField } from "../components/FormField.tsx";
import { KanbanBoard } from "../components/KanbanBoard.tsx";
import { LayoutGridEditor } from "../components/LayoutGridEditor.tsx";
import { CalculationPreviewPanel } from "../components/CalculationPreviewPanel.tsx";
import { DragDropStateTransition } from "../components/DragDropStateTransition.tsx";
import { DragSortList } from "../components/DragSortList.tsx";
import { RelationDropZone } from "../components/RelationDropZone.tsx";
import { TreeReorderDropZone } from "../components/TreeReorderDropZone.tsx";
import { LayoutDropZone } from "../components/LayoutDropZone.tsx";
import { ComponentPlacementHandle } from "../components/ComponentPlacementHandle.tsx";
import { SnapGridOverlay } from "../components/SnapGridOverlay.tsx";
import { StateTransitionArrow } from "../components/StateTransitionArrow.tsx";
import { SlotPlaceholderPanel } from "../components/SlotPlaceholderPanel.tsx";
import { ResponsiveRuleEditor } from "../components/ResponsiveRuleEditor.tsx";
import { FormulaBuilder } from "../components/FormulaBuilder.tsx";
import { ComputedFieldPreview } from "../components/ComputedFieldPreview.tsx";
import { RelationScorePreview } from "../components/RelationScorePreview.tsx";
import { HubStatisticsPanel } from "../components/HubStatisticsPanel.tsx";
import { AggregationPreviewTable } from "../components/AggregationPreviewTable.tsx";
import { CrossEntityCalculationPanel } from "../components/CrossEntityCalculationPanel.tsx";
import { TopologyDistancePreview } from "../components/TopologyDistancePreview.tsx";
import { RouteCostPreview } from "../components/RouteCostPreview.tsx";
import { AttentionWeightPreview } from "../components/AttentionWeightPreview.tsx";
import { CooccurrenceMatrixPreview } from "../components/CooccurrenceMatrixPreview.tsx";
import { RankScorePreview } from "../components/RankScorePreview.tsx";
import { KanaAssistInput } from "../components/KanaAssistInput.tsx";
import { PostalAddressLookup } from "../components/PostalAddressLookup.tsx";
import { AddressPostalLookup } from "../components/AddressPostalLookup.tsx";
import { TelAddressCandidateLookup } from "../components/TelAddressCandidateLookup.tsx";
import { NormalizeAddressCandidate } from "../components/NormalizeAddressCandidate.tsx";
import { LookupCandidateConfirmPanel } from "../components/LookupCandidateConfirmPanel.tsx";
import { BulkImportCandidatePanel } from "../components/BulkImportCandidatePanel.tsx";
import { DocumentCanvasTemplateEditor } from "../components/DocumentCanvasTemplateEditor.tsx";
import { Select } from "../components/Select.tsx";
import { Checkbox } from "../components/Checkbox.tsx";
import { Badge, StatusBadge } from "../components/Badge.tsx";
import { Alert } from "../components/Alert.tsx";
import { LoadingState } from "../components/LoadingState.tsx";
import { EmptyState } from "../components/EmptyState.tsx";
import { ErrorState } from "../components/ErrorState.tsx";
import { JsonViewer } from "../components/JsonViewer.tsx";
import { AdminPageShell } from "../components/AdminPageShell.tsx";
import { AdminSection } from "../components/AdminSection.tsx";
import { ValidationResultPanel } from "../components/ValidationResultPanel.tsx";
import { Textarea } from "../components/Textarea.tsx";
import { Tabs } from "../components/Tabs.tsx";
import { Tree } from "../components/Tree.tsx";
import { MdViewer } from "../components/MdViewer.tsx";
import { AudioPlayer } from "../components/AudioPlayer.tsx";
import { VideoPlayer } from "../components/VideoPlayer.tsx";
import type { MdViewerDisabledActionReasons } from "../components/MdViewer.tsx";
import type {
  CompletedPresetSeed,
  SavedViewDetail,
} from "../api/teamMarkdownApi.ts";
import Box from "../components/Box.tsx";
import type { RuntimeComponentFactory } from "../components/runtimeContract.ts";
import {
  emitComponentOperationEvent,
  enqueueExternalPortDispatchCommand,
  enqueueInstanceOperationDispatchCommand,
  enqueueRuntimeComponentCommand,
  type NormalizedComponentEventType,
  type RuntimeDispatchSpec,
} from "./frontendScheduler.ts";
import type { RuntimeComponentSpec } from "./runtimeComponentAdapter.ts";
import { resolvePayloadFrom } from "./payloadFromResolver.ts";
import { applyGuardedLocalStateMutation } from "./uiEventEffectRunner.ts";
import {
  appendResolvedPayloadToIdempotencyKey,
  isHighFrequencyTrigger,
  isValidDebounceMs,
} from "../lib/uiBuilderWiringProjection.ts";

type RenderResult = { ok: true; node: VNode<any> } | {
  ok: false;
  error: string;
};

type EventBindingValue = {
  eventType: NormalizedComponentEventType;
  actorOrSource?: string;
  payload?: Record<string, unknown>;
  /** component_wiring_execution_lane: when present, emitBoundEvent fires runtime dispatch via enqueueRuntimeComponentCommand. */
  runtimeDispatch?: RuntimeDispatchSpec;
  /** navigation_wiring_execution_lane: when present, emitBoundEvent navigates to route: targetRef locally — no backend dispatch. */
  routeNavigation?: { targetRef: string };
  /** runtime_ui_interaction_wiring: projection-local state mutation, separate from backend dispatch/navigation. */
  localStateMutation?: {
    targetNodeId: string;
    statePath: string;
    action: "set" | "toggle";
    value?: unknown;
  };
  externalPortDispatch?: {
    portTargetRef: string;
    payloadFrom: Record<string, string>;
    outputProp?: string;
    /** SSOT high_frequency_policy: required when the bound trigger is high-frequency. */
    debounceMs?: number;
    /** SSOT lifecycle_policy retry_safe_dispatch_idempotency: identity-only base key from binding-build time; extended with the resolved payload at event time. */
    idempotencyKeyBase?: string;
  };
  /** 外部インスタンス連携 runtime dispatch lane (instance-port target refs via api_command_lane). */
  instanceOperationDispatch?: {
    instanceTargetRef: string;
    payloadFrom: Record<string, string>;
    outputProp?: string;
    /** SSOT high_frequency_policy: required when the bound trigger is high-frequency. */
    debounceMs?: number;
    /** SSOT lifecycle_policy retry_safe_dispatch_idempotency: identity-only base key from binding-build time; extended with the resolved payload at event time. */
    idempotencyKeyBase?: string;
  };
};

function parseEventBinding(value: unknown): EventBindingValue | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }
  const eventType = (value as Record<string, unknown>).eventType;
  if (typeof eventType !== "string") return null;
  const valid: NormalizedComponentEventType[] = [
    "click",
    "change",
    "input",
    "select",
    "toggle",
    "expand",
    "collapse",
    "submit",
    "focus",
    "blur",
    "drag",
    "drop",
    "search",
  ];
  if (!valid.includes(eventType as NormalizedComponentEventType)) return null;
  const actorOrSource = (value as Record<string, unknown>).actorOrSource;
  if (actorOrSource !== undefined && typeof actorOrSource !== "string") {
    return null;
  }
  const payload = (value as Record<string, unknown>).payload;
  if (
    payload !== undefined &&
    (typeof payload !== "object" || payload === null || Array.isArray(payload))
  ) return null;
  const runtimeDispatchRaw = (value as Record<string, unknown>).runtimeDispatch;
  let runtimeDispatch: RuntimeDispatchSpec | undefined;
  if (runtimeDispatchRaw !== undefined) {
    if (
      typeof runtimeDispatchRaw !== "object" ||
      runtimeDispatchRaw === null ||
      Array.isArray(runtimeDispatchRaw)
    ) return null;
    const rd = runtimeDispatchRaw as Record<string, unknown>;
    const action = rd.action;
    const target = rd.target;
    const layer = rd.layer;
    if (typeof action !== "string" || !action.trim()) return null;
    if (typeof target !== "string" || !target.trim()) return null;
    if (typeof layer !== "string" || !layer.trim()) return null;
    const operationType = typeof rd.operationType === "string"
      ? rd.operationType.trim()
      : action.trim();
    // Own parse-boundary fail-close for payloadFrom — mirrors externalPortDispatch's
    // pre-existing contract below exactly (return null for the WHOLE binding on any
    // malformation), independent of whether the upstream builder
    // (renderEmission.ts buildAdminRuntimePayloadFromByTrigger) already validated
    // this shape. This function re-parses spec.eventBinding[trigger] — untyped JSON
    // that could reach here from any construction path, not only that builder — so
    // build-time validation existing elsewhere is never a substitute for this
    // boundary's own explicit fail-close. A present-but-malformed payloadFrom
    // (non-object, non-string value, or empty object) fails the whole binding
    // closed; it is never silently filtered down to a subset or dropped to
    // "unspecified" (which would fall back to raw event-time payload passthrough).
    const rdPayloadFromRaw = rd.payloadFrom;
    let rdPayloadFrom: Record<string, string> | undefined;
    if (rdPayloadFromRaw !== undefined) {
      if (
        typeof rdPayloadFromRaw !== "object" || rdPayloadFromRaw === null ||
        Array.isArray(rdPayloadFromRaw)
      ) return null;
      const entries = Object.entries(rdPayloadFromRaw);
      if (
        entries.length === 0 ||
        !entries.every(([, v]) => typeof v === "string")
      ) return null;
      rdPayloadFrom = Object.fromEntries(entries) as Record<string, string>;
    }
    runtimeDispatch = {
      operationType,
      target: target.trim(),
      layer: layer.trim(),
      action: action.trim(),
      wiringKey: typeof rd.wiringKey === "string" ? rd.wiringKey : undefined,
      wiringId: typeof rd.wiringId === "string" ? rd.wiringId : undefined,
      targetRef: typeof rd.targetRef === "string" ? rd.targetRef : undefined,
      payloadFrom: rdPayloadFrom,
    };
  }
  const routeNavigationRaw = (value as Record<string, unknown>).routeNavigation;
  let routeNavigation: { targetRef: string } | undefined;
  if (routeNavigationRaw !== undefined) {
    if (
      typeof routeNavigationRaw === "object" &&
      routeNavigationRaw !== null &&
      !Array.isArray(routeNavigationRaw)
    ) {
      const rn = routeNavigationRaw as Record<string, unknown>;
      const rnTargetRef = rn.targetRef;
      if (
        typeof rnTargetRef === "string" &&
        rnTargetRef.trim().startsWith("route:")
      ) {
        routeNavigation = { targetRef: rnTargetRef.trim() };
      }
    }
  }
  const externalPortDispatchRaw =
    (value as Record<string, unknown>).externalPortDispatch;
  let externalPortDispatch:
    | EventBindingValue["externalPortDispatch"]
    | undefined;
  if (externalPortDispatchRaw !== undefined) {
    if (
      typeof externalPortDispatchRaw !== "object" ||
      externalPortDispatchRaw === null ||
      Array.isArray(externalPortDispatchRaw)
    ) return null;
    const rawDispatch = externalPortDispatchRaw as Record<string, unknown>;
    const portTargetRef = rawDispatch.portTargetRef;
    const payloadFrom = rawDispatch.payloadFrom;
    const outputProp = rawDispatch.outputProp;
    if (
      typeof portTargetRef !== "string" ||
      !portTargetRef.trim().startsWith("external-port:")
    ) return null;
    if (payloadFrom !== undefined) {
      if (
        typeof payloadFrom !== "object" || payloadFrom === null ||
        Array.isArray(payloadFrom)
      ) return null;
      if (!Object.values(payloadFrom).every((v) => typeof v === "string")) {
        return null;
      }
    }
    if (
      outputProp !== undefined &&
      (typeof outputProp !== "string" || !outputProp.trim())
    ) return null;
    externalPortDispatch = {
      portTargetRef: portTargetRef.trim(),
      payloadFrom: (payloadFrom as Record<string, string> | undefined) ?? {},
      outputProp: typeof outputProp === "string"
        ? outputProp.trim()
        : undefined,
      debounceMs: typeof rawDispatch.debounceMs === "number"
        ? rawDispatch.debounceMs
        : undefined,
      idempotencyKeyBase: typeof rawDispatch.idempotencyKeyBase === "string"
        ? rawDispatch.idempotencyKeyBase
        : undefined,
    };
  }
  const instanceOperationDispatchRaw =
    (value as Record<string, unknown>).instanceOperationDispatch;
  let instanceOperationDispatch:
    | EventBindingValue["instanceOperationDispatch"]
    | undefined;
  if (instanceOperationDispatchRaw !== undefined) {
    if (
      typeof instanceOperationDispatchRaw !== "object" ||
      instanceOperationDispatchRaw === null ||
      Array.isArray(instanceOperationDispatchRaw)
    ) return null;
    const rawDispatch = instanceOperationDispatchRaw as Record<string, unknown>;
    const instanceTargetRef = rawDispatch.instanceTargetRef;
    const payloadFrom = rawDispatch.payloadFrom;
    const outputProp = rawDispatch.outputProp;
    if (
      typeof instanceTargetRef !== "string" ||
      !instanceTargetRef.trim().startsWith("instance-port:")
    ) return null;
    if (payloadFrom !== undefined) {
      if (
        typeof payloadFrom !== "object" || payloadFrom === null ||
        Array.isArray(payloadFrom)
      ) return null;
      if (!Object.values(payloadFrom).every((v) => typeof v === "string")) {
        return null;
      }
    }
    if (
      outputProp !== undefined &&
      (typeof outputProp !== "string" || !outputProp.trim())
    ) return null;
    instanceOperationDispatch = {
      instanceTargetRef: instanceTargetRef.trim(),
      payloadFrom: (payloadFrom as Record<string, string> | undefined) ?? {},
      outputProp: typeof outputProp === "string"
        ? outputProp.trim()
        : undefined,
      debounceMs: typeof rawDispatch.debounceMs === "number"
        ? rawDispatch.debounceMs
        : undefined,
      idempotencyKeyBase: typeof rawDispatch.idempotencyKeyBase === "string"
        ? rawDispatch.idempotencyKeyBase
        : undefined,
    };
  }
  const localStateMutationRaw =
    (value as Record<string, unknown>).localStateMutation;
  let localStateMutation: EventBindingValue["localStateMutation"] | undefined;
  if (localStateMutationRaw !== undefined) {
    if (
      typeof localStateMutationRaw !== "object" ||
      localStateMutationRaw === null ||
      Array.isArray(localStateMutationRaw)
    ) return null;
    const mutation = localStateMutationRaw as Record<string, unknown>;
    const targetNodeId = mutation.targetNodeId;
    const statePath = mutation.statePath;
    const action = mutation.action;
    if (typeof targetNodeId !== "string" || !targetNodeId.trim()) return null;
    if (typeof statePath !== "string" || !statePath.trim()) return null;
    if (action !== "set" && action !== "toggle") return null;
    localStateMutation = {
      targetNodeId: targetNodeId.trim(),
      statePath: statePath.trim(),
      action,
      value: mutation.value,
    };
  }
  return {
    eventType: eventType as NormalizedComponentEventType,
    actorOrSource,
    payload: (payload as Record<string, unknown> | undefined) ?? {},
    runtimeDispatch,
    routeNavigation,
    localStateMutation,
    externalPortDispatch,
    instanceOperationDispatch,
  };
}

function isPreviewMode(spec: RuntimeComponentSpec): boolean {
  return spec.previewMode === true;
}

/**
 * Still fire-and-forget from emitBoundEvent's own synchronous caller's perspective
 * (the FIFO queue in frontendScheduler still owns ordering/error propagation) --
 * but the settled result is now forwarded to spec.onRuntimeDispatchResult when the
 * caller wired one in, instead of being unconditionally discarded. See
 * ComponentDataHub.onRuntimeDispatchResult (projectionConstructor.ts) for why: no
 * admin_runtime dispatch's own response previously reached rendered state at all.
 * A rejected promise (queue-level failure, not a normal {success:false} response)
 * is logged, not re-thrown -- this call site has no synchronous caller left to
 * propagate it to by the time the promise settles.
 */
function dispatchRuntimeComponentCommandAndForwardResult(
  spec: RuntimeComponentSpec,
  dispatchSpec: Parameters<typeof enqueueRuntimeComponentCommand>[0],
): void {
  enqueueRuntimeComponentCommand(dispatchSpec)
    .then((result) => spec.onRuntimeDispatchResult?.(result))
    .catch((err) => {
      console.error(
        "[runtimeComponentFactory] admin_runtime dispatch queue rejected:",
        err,
      );
    });
}

function emitBoundEvent(
  spec: RuntimeComponentSpec,
  trigger: string,
  payload: Record<string, unknown>,
): { ok: true } | { ok: false; error: string } {
  // Lane 3: frontend_local_derived_calculation_binding — fires BEFORE previewMode gate.
  // No backend dispatch. No ManifestDispatcher. No queueAdminClientCommand.
  if (
    spec.calcTriggerCallback &&
    (trigger === "change" || trigger === "input" || trigger === "select")
  ) {
    spec.calcTriggerCallback(payload.value ?? payload.raw ?? payload);
  }
  // Lane 3 (search): search_suggest candidate boundary — fires BEFORE previewMode gate.
  // Island/page provides searchCallback to implement the debounce → read-only provider → suggestions update loop.
  // No mutation / DB write / apply. SSOT: candidate_source_boundary: debounce_backend_readonly_search
  if (spec.searchCallback && trigger === "search") {
    const q = typeof payload.query === "string" ? payload.query : "";
    spec.searchCallback(spec.componentId, q);
  }
  // Lane 3 (node value tracking): registers this node's own latest scalar value
  // under its stable nodeId — the write side of payloadFromNodeValues, the
  // read side dispatchExternalPort/dispatchInstanceOperation/admin_runtime
  // Lane 2 payloadFrom resolution (below) consumes via `node:<nodeId>.value`.
  // Fires BEFORE previewMode gate, unconditionally (independent of any
  // calc/dispatch wiring being configured for this node) — mirrors
  // calcTriggerCallback's own event-time-value extraction.
  // SSOT: admin-uibuilder-ui-structure-wiring-ssot.yaml
  // lane_storage_boundary.known_gaps.remaining_write_payload_capture_gap.
  if (
    spec.onNodeValueChange &&
    (trigger === "change" || trigger === "input" || trigger === "select") &&
    "value" in payload
  ) {
    spec.onNodeValueChange(payload.value);
  }
  if (isPreviewMode(spec)) return { ok: true };
  const binding = parseEventBinding(spec.eventBinding[trigger]);
  if (!binding) {
    return {
      ok: false,
      error: `RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING: ${trigger}`,
    };
  }
  // Lane 1: frontend_component_event_log_lane — observation log (always).
  const logResult = emitComponentOperationEvent({
    componentId: spec.componentId,
    packageId: spec.packageId,
    layoutId: spec.layoutId,
    wiringId: spec.wiringId,
    eventType: binding.eventType,
    actorOrSource: binding.actorOrSource ?? "runtime_primitive_renderer",
    payload: { ...binding.payload, ...payload },
  });
  if (!logResult.ok) return logResult;
  // Lane 2: component_wiring_execution_lane — runtime dispatch (when configured).
  // Fire-and-forget: the FIFO queue in frontendScheduler handles ordering and error propagation.
  // Event-time payload (the caller's form/field values, same argument Lane 1's
  // log entry already merges above) is forwarded verbatim -- this is what lets
  // an admin_runtime wiringKind=admin_runtime dispatch (see renderEmission.ts
  // mapWiringKindToLayer/mapWiringKindToAction) carry admin-typed values
  // (e.g. enum group name) to the backend action; the pre-existing search/
  // aggregate/create/update/delete wiringKinds ignore it (their payload comes
  // from wiring/screen_data_shape configuration instead).
  if (binding.runtimeDispatch) {
    const payloadFrom = binding.runtimeDispatch.payloadFrom;
    // Priority/conflict rule (SSOT remaining_write_payload_capture_gap): when a
    // payloadFrom map is authored, it is the SOLE payload authority for this
    // dispatch — same fail-close contract as dispatchExternalPort/
    // dispatchInstanceOperation below (unresolved node value / event path is a
    // hard error, never a silent partial payload). When no payloadFrom is
    // authored, the pre-existing raw event-time payload passthrough (static
    // config payload + Lane 1's merged payload) is unchanged.
    if (payloadFrom && Object.keys(payloadFrom).length > 0) {
      const resolved = resolvePayloadFrom(
        payloadFrom,
        spec.payloadFromNodeValues ?? {},
        payload,
      );
      if (!resolved.ok) {
        return { ok: false, error: resolved.errors.join("; ") };
      }
      dispatchRuntimeComponentCommandAndForwardResult(spec, {
        ...binding.runtimeDispatch,
        payload: resolved.payload,
      });
    } else {
      dispatchRuntimeComponentCommandAndForwardResult(spec, {
        ...binding.runtimeDispatch,
        payload: { ...binding.runtimeDispatch.payload, ...binding.payload, ...payload },
      });
    }
  }
  // Lane 2 (external_port): Design Inspector-authored dispatchExternalPort.
  // high_frequency_policy runtime guard: a high-frequency trigger without a
  // valid debounceMs fails close HERE, at dispatch time — authoring/apply
  // policy guard alone does not prevent an out-of-band persisted interaction
  // from reaching the dispatch lane at runtime. No silent default debounce.
  if (binding.externalPortDispatch) {
    if (
      isHighFrequencyTrigger(trigger) &&
      !isValidDebounceMs(binding.externalPortDispatch.debounceMs)
    ) {
      return {
        ok: false,
        error:
          `HIGH_FREQUENCY_DISPATCH_REQUIRES_DEBOUNCE — 高頻度トリガ "${trigger}" での外部送出には debounceMs（正の整数）が必要です`,
      };
    }
    // payloadFrom resolution is fail-close: unresolved refs return explicit error and no partial payload is sent.
    const resolved = resolvePayloadFrom(
      binding.externalPortDispatch.payloadFrom,
      spec.payloadFromNodeValues ?? {},
      payload,
    );
    if (!resolved.ok) {
      return { ok: false, error: resolved.errors.join("; ") };
    }
    void enqueueExternalPortDispatchCommand({
      portTargetRef: binding.externalPortDispatch.portTargetRef,
      payload: resolved.payload,
      outputProp: binding.externalPortDispatch.outputProp,
      // retry_safe_dispatch_idempotency: extends the build-time identity base key
      // with the event-time resolved payload, so a genuine retry of THIS firing
      // (communication loss, reload) presents the same key while a distinct
      // firing with different resolved payload content gets a distinct key.
      idempotencyKey: binding.externalPortDispatch.idempotencyKeyBase
        ? appendResolvedPayloadToIdempotencyKey(
          binding.externalPortDispatch.idempotencyKeyBase,
          resolved.payload,
        )
        : undefined,
    });
  }
  // Lane 2 (外部インスタンス連携): dispatchInstanceOperation through the same
  // api_command_lane. Same runtime high_frequency_policy guard as the external lane.
  if (binding.instanceOperationDispatch) {
    if (
      isHighFrequencyTrigger(trigger) &&
      !isValidDebounceMs(binding.instanceOperationDispatch.debounceMs)
    ) {
      return {
        ok: false,
        error:
          `HIGH_FREQUENCY_DISPATCH_REQUIRES_DEBOUNCE — 高頻度トリガ "${trigger}" でのインスタンス送出には debounceMs（正の整数）が必要です`,
      };
    }
    // payloadFrom resolution is fail-close like the external lane.
    const resolved = resolvePayloadFrom(
      binding.instanceOperationDispatch.payloadFrom,
      spec.payloadFromNodeValues ?? {},
      payload,
    );
    if (!resolved.ok) {
      return { ok: false, error: resolved.errors.join("; ") };
    }
    void enqueueInstanceOperationDispatchCommand({
      instanceTargetRef: binding.instanceOperationDispatch.instanceTargetRef,
      payload: resolved.payload,
      outputProp: binding.instanceOperationDispatch.outputProp,
      idempotencyKey: binding.instanceOperationDispatch.idempotencyKeyBase
        ? appendResolvedPayloadToIdempotencyKey(
          binding.instanceOperationDispatch.idempotencyKeyBase,
          resolved.payload,
        )
        : undefined,
    });
  }
  // Lane 2 (navigation): frontend-local route navigation — no backend dispatch.
  // route:<routeKey> must not reach ManifestDispatcher; navigation executes client-side only.
  if (binding.routeNavigation) {
    const ref = binding.routeNavigation.targetRef;
    const routeKey = ref.startsWith("route:")
      ? ref.slice("route:".length).trim()
      : null;
    if (routeKey) {
      const href = routeKey.startsWith("/") ? routeKey : `/${routeKey}`;
      globalThis.location.href = href;
    }
  }
  // Lane 2 (local UI state): projection-local state mutation for modal/drawer/dialog.
  // This is intentionally separate from routeNavigation and backend runtimeDispatch.
  // UI状態更新 writes through the SAME guarded dispatcher the lifecycle path uses
  // (applyGuardedLocalStateMutation) — there is no direct store.set() here, so
  // mutation authority is not duplicated across the event and lifecycle paths.
  // An undeclared target (e.g. a stale/deleted-node reference) fails close.
  if (binding.localStateMutation) {
    if (!spec.localStateStore) {
      return {
        ok: false,
        error: "RUNTIME_PRIMITIVE_RENDERER_MISSING_LOCAL_STATE_STORE",
      };
    }
    const result = applyGuardedLocalStateMutation(
      spec.localStateStore,
      binding.localStateMutation,
    );
    if (!result.ok) return result;
  }
  return { ok: true };
}

function requireBinding(
  spec: RuntimeComponentSpec,
  trigger: string,
): { ok: true } | { ok: false; error: string } {
  if (isPreviewMode(spec)) return { ok: true };
  if (!(trigger in spec.eventBinding)) {
    return {
      ok: false,
      error: `RUNTIME_PRIMITIVE_RENDERER_MISSING_EVENT_BINDING: ${trigger}`,
    };
  }
  const parsed = parseEventBinding(spec.eventBinding[trigger]);
  if (!parsed) {
    return {
      ok: false,
      error: `RUNTIME_PRIMITIVE_RENDERER_INVALID_EVENT_BINDING: ${trigger}`,
    };
  }
  return { ok: true };
}

function buttonFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (typeof data.label !== "string") {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_BUTTON_PROPS",
    };
  }
  const bindingCheck = requireBinding(spec, "click");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(Button, {
      label: data.label as string,
      disabled: data.disabled as boolean | undefined,
      variant: data.variant as "primary" | "secondary" | "danger" | undefined,
      type: data.type as "button" | "submit" | "reset" | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClick: () => {
        const result = emitBoundEvent(spec, "click", {});
        if (!result.ok) throw new Error(result.error);
      },
    }),
  };
}

function inputFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (data.value !== undefined && typeof data.value !== "string") {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_INPUT_PROPS",
    };
  }
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(Input, {
      value: (data.value as string | undefined) ?? "",
      placeholder: data.placeholder as string | undefined,
      disabled: data.disabled as boolean | undefined,
      label: data.label as string | undefined,
      type: (spec.componentType === "form_input/search_input"
        ? "text"
        : data.type) as "text" | "password" | "number" | "email" | undefined,
      onChange: (value: string) => {
        const result = emitBoundEvent(spec, "change", { value });
        if (!result.ok) {
          throw new Error(result.error);
        }
      },
      onFocus: spec.eventBinding.focus
        ? () => {
          const result = emitBoundEvent(spec, "focus", {});
          if (!result.ok) {
            throw new Error(result.error);
          }
        }
        : undefined,
      className: spec.className,
      design: spec.design ?? {},
      onBlur: spec.eventBinding.blur
        ? () => {
          const result = emitBoundEvent(spec, "blur", {});
          if (!result.ok) {
            throw new Error(result.error);
          }
        }
        : undefined,
    }),
  };
}

function cardFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(Card, {
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      variant: data.variant as
        | "default"
        | "info"
        | "warning"
        | "error"
        | undefined,
      children: h("div", null, (data.body as string | undefined) ?? ""),
      footer: data.footer as string | undefined,
      onClick: spec.eventBinding.click
        ? () => {
          const result = emitBoundEvent(spec, "click", {});
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function cardListFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const rawItems = Array.isArray(props.items) ? props.items : [];
  const items = rawItems.filter(
    (it): it is Record<string, unknown> =>
      typeof it === "object" && it !== null && !Array.isArray(it),
  );
  return {
    ok: true,
    node: h(CardList, {
      items: items.map((it) => ({
        // Spread first: preserves any extra passthrough fields a propBinding transform
        // attached to the resolved item (e.g. navigationLinksToCardItems' hubRelationId /
        // topologyManifestId / relatedHubId, admin-normal-surface-projection-seed-ssot.yaml
        // selected_link_payload_delivery) so they survive into the rendered CardList item and
        // the onSelect emission below, not just into propBindingResolver's own output.
        ...it,
        id: it.id as string | number | undefined,
        title: it.title as string | undefined,
        body: it.body as string | undefined,
        footer: it.footer as string | undefined,
        variant: it.variant as
          | "default"
          | "info"
          | "warning"
          | "error"
          | undefined,
      })),
      emptyMessage: props.emptyMessage as string | undefined,
      searchable: props.searchable as boolean | undefined,
      searchPlaceholder: props.searchPlaceholder as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (item, idx) => {
          // item is the exact rendered CardListItem (same object reference as this closure's
          // items[...] before any CardList-internal searchable filtering) — using it directly
          // instead of re-indexing into `items` by idx, since idx is CardList's own currently
          // *visible* (post-filter) index, not necessarily an index into this unfiltered array.
          const result = emitBoundEvent(spec, "select", {
            index: idx,
            item,
          });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function tableFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const table = (typeof props.table === "object" && props.table !== null &&
      !Array.isArray(props.table))
    ? props.table as Record<string, unknown>
    : props;
  if (!Array.isArray(table.columns) || !Array.isArray(table.rows)) {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_TABLE_PROPS",
    };
  }
  const columns = table.columns as Array<{ key: string; header: string }>;
  const rows = table.rows as Array<Record<string, unknown>>;
  return {
    ok: true,
    node: h(Table<Record<string, unknown>>, {
      columns,
      className: spec.className,
      design: spec.design ?? {},
      rows,
      rowKey: (row) => String(row.id ?? JSON.stringify(row)),
      emptyMessage: (table.emptyMessage as string | undefined) ?? "No data.",
      // `value: row` (in addition to `row`) is what makes emitBoundEvent's existing,
      // universal Lane 3 node-value tracking (the `"value" in payload` branch, shared
      // by every input/select-family component) also fire for a table's row select —
      // no table-specific tracking path, just supplying the same key every other
      // component's change/input/select event already relies on. This lets a LATER
      // node's payloadFrom reference the selected row via
      // `node:<thisTableNodeId>.value.<field>` (round 20 — SSOT:
      // docs/design/ui-builder-preset-ecosystem-ssot.yaml payloadFrom_resolver_contract
      // .recognized_source_patterns.node_value_path).
      onRowClick: spec.eventBinding.select
        ? (row) => {
          const result = emitBoundEvent(spec, "select", { row, value: row });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function autoCompleteInputFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (data.value !== undefined && typeof data.value !== "string") {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_AUTOCOMPLETE_INPUT_PROPS",
    };
  }
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const suggestions = Array.isArray(data.suggestions)
    ? data.suggestions.filter((s): s is string => typeof s === "string")
    : [];
  return {
    ok: true,
    node: h(AutoCompleteInput, {
      value: (data.value as string | undefined) ?? "",
      suggestions,
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const result = emitBoundEvent(spec, "change", { value });
        if (!result.ok) throw new Error(result.error);
      },
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const result = emitBoundEvent(spec, "select", { value });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
      // read-only backend search hook; caller (island/page) must debounce; no mutation during typing
      onSearch: spec.eventBinding.search
        ? (query: string) => {
          const result = emitBoundEvent(spec, "search", { query });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function searchComboboxFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (data.value !== undefined && typeof data.value !== "string") {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_SEARCH_COMBOBOX_PROPS",
    };
  }
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawOptions = Array.isArray(data.options) ? data.options : [];
  const options = rawOptions
    .filter((o): o is { label: string; value: string } =>
      typeof o === "object" && o !== null &&
      typeof (o as Record<string, unknown>).value === "string"
    )
    .map((o) => ({
      label: typeof o.label === "string" ? o.label : o.value,
      value: o.value,
    }));
  return {
    ok: true,
    node: h(SearchCombobox, {
      value: (data.value as string | undefined) ?? "",
      options,
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const result = emitBoundEvent(spec, "change", { value });
        if (!result.ok) throw new Error(result.error);
      },
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const result = emitBoundEvent(spec, "select", { value });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function candidateConfidenceBadgeFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (typeof data.label !== "string") {
    return {
      ok: false,
      error:
        "RUNTIME_PRIMITIVE_RENDERER_INVALID_CANDIDATE_CONFIDENCE_BADGE_PROPS",
    };
  }
  const rawConf = data.confidence;
  const confidence =
    rawConf === "high" || rawConf === "medium" || rawConf === "low"
      ? rawConf
      : "unknown";
  const score = typeof data.score === "number" ? data.score : undefined;
  return {
    ok: true,
    node: h(CandidateConfidenceBadge, {
      label: data.label as string,
      confidence,
      score,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function inlineEditableFieldFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  if (data.value !== undefined && typeof data.value !== "string") {
    return {
      ok: false,
      error: "RUNTIME_PRIMITIVE_RENDERER_INVALID_INLINE_EDITABLE_FIELD_PROPS",
    };
  }
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(InlineEditableField, {
      value: (data.value as string | undefined) ?? "",
      editing: data.editing as boolean | undefined,
      label: data.label as string | undefined,
      placeholder: data.placeholder as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const result = emitBoundEvent(spec, "change", { value });
        if (!result.ok) throw new Error(result.error);
      },
      onToggle: spec.eventBinding.toggle
        ? (editing: boolean) => {
          const result = emitBoundEvent(spec, "toggle", { editing });
          if (!result.ok) throw new Error(result.error);
        }
        : undefined,
    }),
  };
}

function patchPreviewPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawFields = Array.isArray(data.fields) ? data.fields : [];
  const fields = rawFields.filter(
    (f): f is { fieldLabel: string; before: string; after: string } =>
      typeof f === "object" && f !== null &&
      typeof (f as Record<string, unknown>).fieldLabel === "string",
  ).map((f) => ({
    fieldLabel: f.fieldLabel,
    before: typeof f.before === "string" ? f.before : "",
    after: typeof f.after === "string" ? f.after : "",
  }));
  return {
    ok: true,
    node: h(PatchPreviewPanel, {
      fields,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function applyConfirmDialogFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const submitCheck = requireBinding(spec, "submit");
  if (!submitCheck.ok) return submitCheck;
  const toggleCheck = requireBinding(spec, "toggle");
  if (!toggleCheck.ok) return toggleCheck;
  return {
    ok: true,
    node: h(ApplyConfirmDialog, {
      open: data.open as boolean ?? false,
      title: data.title as string | undefined,
      description: data.description as string | undefined,
      confirmLabel: data.confirmLabel as string | undefined,
      cancelLabel: data.cancelLabel as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onConfirm: () => {
        const result = emitBoundEvent(spec, "submit", {});
        if (!result.ok) throw new Error(result.error);
      },
      onCancel: () => {
        const result = emitBoundEvent(spec, "toggle", { open: false });
        if (!result.ok) throw new Error(result.error);
      },
    }),
  };
}

function styleTokenPickerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "select");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(StyleTokenPicker, {
      value: data.value as string | undefined,
      tokens,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: (token) => {
        const result = emitBoundEvent(spec, "select", { token });
        if (!result.ok) throw new Error(result.error);
      },
    }),
  };
}

function themePreviewPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; description?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(ThemePreviewPanel, {
      tokens,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function validationErrorPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawErrors = Array.isArray(data.errors) ? data.errors : [];
  const errors = rawErrors.filter(
    (e): e is { message: string; field?: string; code?: string } =>
      typeof e === "object" && e !== null &&
      typeof (e as Record<string, unknown>).message === "string",
  );
  return {
    ok: true,
    node: h(ValidationErrorPanel, {
      errors,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

// === Cat A: Search / Suggest / Candidate UI ===

function suggestInputFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(SuggestInput, {
      value: typeof data.value === "string" ? data.value : "",
      suggestions: Array.isArray(data.suggestions)
        ? data.suggestions.filter((s): s is string => typeof s === "string")
        : [],
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const r = emitBoundEvent(spec, "select", { value });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
      // read-only backend search hook; caller must debounce; no mutation during typing
      onSearch: spec.eventBinding.search
        ? (query: string) => {
          const r = emitBoundEvent(spec, "search", { query });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function recentInputSuggestFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(RecentInputSuggest, {
      value: typeof data.value === "string" ? data.value : "",
      recentItems: Array.isArray(data.recentItems)
        ? data.recentItems.filter((s): s is string => typeof s === "string")
        : [],
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const r = emitBoundEvent(spec, "select", { value });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function relationCandidatePickerFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "select");
  if (!bindingCheck.ok) return bindingCheck;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; score?: number } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(RelationCandidatePicker, {
      candidates,
      selectedId: data.selectedId as string | undefined,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: (id: string) => {
        const r = emitBoundEvent(spec, "select", { id });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function duplicateMergeCandidatePanelFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; similarity?: number } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(DuplicateMergeCandidatePanel, {
      candidates,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function relationPathPreviewFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawSegments = Array.isArray(data.segments) ? data.segments : [];
  const segments = rawSegments.filter(
    (s): s is { label: string; kind?: string } =>
      typeof s === "object" && s !== null &&
      typeof (s as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(RelationPathPreview, {
      segments,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function fieldResolverInspectorFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(FieldResolverInspector, {
      fieldName: data.fieldName as string | undefined,
      resolvedValue: data.resolvedValue as string | undefined,
      resolverKind: data.resolverKind as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function schemaPromotionCandidatePanelFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; kind?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(SchemaPromotionCandidatePanel, {
      candidates,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function selectImportDialogFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const submitCheck = requireBinding(spec, "submit");
  if (!submitCheck.ok) return submitCheck;
  const toggleCheck = requireBinding(spec, "toggle");
  if (!toggleCheck.ok) return toggleCheck;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { label: string; value: string; description?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).label === "string" &&
      typeof (c as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(SelectImportDialog, {
      open: Boolean(data.open),
      title: data.title as string | undefined,
      candidates,
      selectedValue: data.selectedValue as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (value: string) => {
          const r = emitBoundEvent(spec, "select", { value });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
      onSubmit: () => {
        const r = emitBoundEvent(spec, "submit", {});
        if (!r.ok) throw new Error(r.error);
      },
      onCancel: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

// === Cat B: Inline Edit / Preview Update / Audit UI ===

function inlineEditableJsonbFieldFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const value = (typeof data.value === "object" && data.value !== null &&
      !Array.isArray(data.value))
    ? data.value as Record<string, unknown>
    : undefined;
  return {
    ok: true,
    node: h(InlineEditableJsonbField, {
      value,
      editing: data.editing as boolean | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (raw: string) => {
        const r = emitBoundEvent(spec, "change", { raw });
        if (!r.ok) throw new Error(r.error);
      },
      onToggle: spec.eventBinding.toggle
        ? (editing: boolean) => {
          const r = emitBoundEvent(spec, "toggle", { editing });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function diffStrikeTextFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(DiffStrikeText, {
      before: data.before as string | undefined,
      after: data.after as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function auditDiffDrawerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "toggle");
  if (!bindingCheck.ok) return bindingCheck;
  const rawEntries = Array.isArray(data.entries) ? data.entries : [];
  const entries = rawEntries.filter(
    (
      e,
    ): e is {
      field: string;
      before: string;
      after: string;
      timestamp?: string;
    } =>
      typeof e === "object" && e !== null &&
      typeof (e as Record<string, unknown>).field === "string",
  );
  return {
    ok: true,
    node: h(AuditDiffDrawer, {
      open: typeof data.open === "boolean" ? data.open : false,
      entries,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClose: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function optimisticUpdateBoundaryFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(OptimisticUpdateBoundary, {
      pending: data.pending as boolean | undefined,
      error: data.error as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function confirmedUpdateButtonFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "submit");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(ConfirmedUpdateButton, {
      label: data.label as string | undefined,
      confirmLabel: data.confirmLabel as string | undefined,
      description: data.description as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onConfirm: () => {
        const r = emitBoundEvent(spec, "submit", {});
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function undoTimelineFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawItems = Array.isArray(data.items) ? data.items : [];
  const items = rawItems.filter(
    (it): it is { id: string; label: string; timestamp?: string } =>
      typeof it === "object" && it !== null &&
      typeof (it as Record<string, unknown>).id === "string" &&
      typeof (it as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(UndoTimeline, {
      items,
      selectedId: data.selectedId as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function conflictResolutionPanelFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawConflicts = Array.isArray(data.conflicts) ? data.conflicts : [];
  const conflicts = rawConflicts.filter(
    (c): c is { field: string; localValue: string; remoteValue: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).field === "string",
  );
  return {
    ok: true,
    node: h(ConflictResolutionPanel, {
      conflicts,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (field: string) => {
          const r = emitBoundEvent(spec, "select", { field });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

// === Cat C: Table / List / View Operation UI ===

function facetedFilterBarFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawFilters = Array.isArray(data.filters) ? data.filters : [];
  const filters = rawFilters.filter(
    (f): f is { key: string; label: string; value: string } =>
      typeof f === "object" && f !== null &&
      typeof (f as Record<string, unknown>).key === "string" &&
      typeof (f as Record<string, unknown>).label === "string",
  ).map((f) => ({
    key: f.key,
    label: f.label,
    value: typeof f.value === "string" ? f.value : "",
  }));
  return {
    ok: true,
    node: h(FacetedFilterBar, {
      filters,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (key: string, value: string) => {
        const r = emitBoundEvent(spec, "change", { key, value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function columnFilterFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(ColumnFilter, {
      column: data.column as string | undefined,
      value: data.value as string | undefined,
      placeholder: data.placeholder as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function columnVisibilityEditorFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawColumns = Array.isArray(data.columns) ? data.columns : [];
  const columns = rawColumns.filter(
    (c): c is { key: string; label: string; visible: boolean } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).key === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  ).map((c) => ({
    key: c.key,
    label: c.label,
    visible: typeof c.visible === "boolean" ? c.visible : true,
  }));
  return {
    ok: true,
    node: h(ColumnVisibilityEditor, {
      columns,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (key: string, visible: boolean) => {
        const r = emitBoundEvent(spec, "change", { key, visible });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function sortControlFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawFields = Array.isArray(data.fields) ? data.fields : [];
  const fields = rawFields.filter(
    (f): f is { key: string; label: string } =>
      typeof f === "object" && f !== null &&
      typeof (f as Record<string, unknown>).key === "string",
  ).map((f) => ({
    key: f.key,
    label: typeof f.label === "string" ? f.label : f.key,
  }));
  const rawDir = data.direction;
  const direction = rawDir === "asc" || rawDir === "desc" ? rawDir : null;
  return {
    ok: true,
    node: h(SortControl, {
      field: data.field as string | undefined,
      direction,
      fields,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (field: string, dir: "asc" | "desc") => {
        const r = emitBoundEvent(spec, "change", { field, direction: dir });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function groupByControlFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawOptions = Array.isArray(data.options) ? data.options : [];
  const options = rawOptions.filter(
    (o): o is { key: string; label: string } =>
      typeof o === "object" && o !== null &&
      typeof (o as Record<string, unknown>).key === "string",
  ).map((o) => ({
    key: o.key,
    label: typeof o.label === "string" ? o.label : o.key,
  }));
  return {
    ok: true,
    node: h(GroupByControl, {
      field: data.field as string | undefined,
      options,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (field: string) => {
        const r = emitBoundEvent(spec, "change", { field });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function savedViewSelectorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawViews = Array.isArray(data.views) ? data.views : [];
  const views = rawViews.filter(
    (v): v is { id: string; label: string } =>
      typeof v === "object" && v !== null &&
      typeof (v as Record<string, unknown>).id === "string" &&
      typeof (v as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(SavedViewSelector, {
      views,
      selectedId: data.selectedId as string | undefined,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function bulkActionPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "select");
  if (!bindingCheck.ok) return bindingCheck;
  const rawActions = Array.isArray(data.actions) ? data.actions : [];
  const actions = rawActions.filter(
    (a): a is { id: string; label: string } =>
      typeof a === "object" && a !== null &&
      typeof (a as Record<string, unknown>).id === "string" &&
      typeof (a as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(BulkActionPanel, {
      selectedCount: typeof data.selectedCount === "number"
        ? data.selectedCount
        : 0,
      actions,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: (id: string) => {
        const r = emitBoundEvent(spec, "select", { id });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function virtualizedDataTableFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawColumns = Array.isArray(data.columns) ? data.columns : [];
  const columns = rawColumns.filter(
    (c): c is { key: string; header: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).key === "string",
  ).map((c) => ({
    key: c.key,
    header: typeof c.header === "string" ? c.header : c.key,
  }));
  const rows = Array.isArray(data.rows)
    ? data.rows.filter((r): r is Record<string, unknown> =>
      typeof r === "object" && r !== null && !Array.isArray(r)
    )
    : [];
  return {
    ok: true,
    node: h(VirtualizedDataTable, {
      columns,
      rows,
      emptyMessage: data.emptyMessage as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onRowSelect: spec.eventBinding.select
        ? (row: Record<string, unknown>) => {
          const r = emitBoundEvent(spec, "select", { row });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function rowDetailDrawerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "toggle");
  if (!bindingCheck.ok) return bindingCheck;
  const row = (typeof data.row === "object" && data.row !== null &&
      !Array.isArray(data.row))
    ? data.row as Record<string, unknown>
    : undefined;
  return {
    ok: true,
    node: h(RowDetailDrawer, {
      open: typeof data.open === "boolean" ? data.open : false,
      row,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClose: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function paginationControlFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(PaginationControl, {
      page: typeof data.page === "number" ? data.page : 1,
      pageSize: typeof data.pageSize === "number" ? data.pageSize : 10,
      total: typeof data.total === "number" ? data.total : 0,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (page: number) => {
        const r = emitBoundEvent(spec, "change", { page });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function exportCandidatePanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; format?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(ExportCandidatePanel, {
      candidates,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

// === Cat E: Design Token / Style Token / Layout Token UI ===

function fontTokenEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(FontTokenEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function backgroundColorEditorFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(BackgroundColorEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function textColorEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(TextColorEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function spacingTokenEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(SpacingTokenEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function borderRadiusEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(BorderRadiusEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function cssVariablePreviewFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawVariables = Array.isArray(data.variables) ? data.variables : [];
  const variables = rawVariables.filter(
    (v): v is { key: string; value: string; description?: string } =>
      typeof v === "object" && v !== null &&
      typeof (v as Record<string, unknown>).key === "string" &&
      typeof (v as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(CssVariablePreview, {
      variables,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function shadowTokenEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(ShadowTokenEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function animationTokenEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  const rawTokens = Array.isArray(data.tokens) ? data.tokens : [];
  const tokens = rawTokens.filter(
    (t): t is { key: string; value: string; label?: string } =>
      typeof t === "object" && t !== null &&
      typeof (t as Record<string, unknown>).key === "string" &&
      typeof (t as Record<string, unknown>).value === "string",
  );
  return {
    ok: true,
    node: h(AnimationTokenEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      tokens,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

// === Cat H: Safety / Inspector / Operation Guard UI ===

function commandPaletteFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const toggleCheck = requireBinding(spec, "toggle");
  if (!toggleCheck.ok) return toggleCheck;
  const selectCheck = requireBinding(spec, "select");
  if (!selectCheck.ok) return selectCheck;
  const rawCommands = Array.isArray(data.commands) ? data.commands : [];
  const commands = rawCommands.filter(
    (c): c is { id: string; label: string; description?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(CommandPalette, {
      open: typeof data.open === "boolean" ? data.open : false,
      commands,
      placeholder: data.placeholder as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClose: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
      onSelect: (id: string) => {
        const r = emitBoundEvent(spec, "select", { id });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function emptyStateActionPanelFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawActions = Array.isArray(data.actions) ? data.actions : [];
  const actions = rawActions.filter(
    (a): a is { id: string; label: string } =>
      typeof a === "object" && a !== null &&
      typeof (a as Record<string, unknown>).id === "string" &&
      typeof (a as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(EmptyStateActionPanel, {
      title: data.title as string | undefined,
      description: data.description as string | undefined,
      actions,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function operationGuardBannerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawLevel = data.level;
  const level =
    rawLevel === "info" || rawLevel === "warning" || rawLevel === "error"
      ? rawLevel
      : undefined;
  return {
    ok: true,
    node: h(OperationGuardBanner, {
      message: data.message as string | undefined,
      level,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function mutationBoundaryInspectorFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(MutationBoundaryInspector, {
      operationKind: data.operationKind as string | undefined,
      fieldName: data.fieldName as string | undefined,
      currentValue: data.currentValue as string | undefined,
      pendingValue: data.pendingValue as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function permissionHintPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(PermissionHintPanel, {
      requiredPermission: data.requiredPermission as string | undefined,
      message: data.message as string | undefined,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function dryRunResultPanelFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawResults = Array.isArray(data.results) ? data.results : [];
  const results = rawResults.filter(
    (r): r is { id: string; label: string; kind?: string; impact?: string } =>
      typeof r === "object" && r !== null &&
      typeof (r as Record<string, unknown>).id === "string" &&
      typeof (r as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(DryRunResultPanel, {
      results,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function rollbackCandidatePanelFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawCandidates = Array.isArray(data.candidates) ? data.candidates : [];
  const candidates = rawCandidates.filter(
    (c): c is { id: string; label: string; timestamp?: string } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).id === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(RollbackCandidatePanel, {
      candidates,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function operationAuditLogPanelFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawEntries = Array.isArray(data.entries) ? data.entries : [];
  const entries = rawEntries.filter(
    (
      e,
    ): e is {
      id: string;
      operation: string;
      timestamp?: string;
      actor?: string;
    } =>
      typeof e === "object" && e !== null &&
      typeof (e as Record<string, unknown>).id === "string" &&
      typeof (e as Record<string, unknown>).operation === "string",
  );
  return {
    ok: true,
    node: h(OperationAuditLogPanel, {
      entries,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function formFieldFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(FormField, {
      label: data.label as string | undefined,
      required: data.required as boolean | undefined,
      error: data.error as string | undefined,
      help: data.help as string | undefined,
      disabled: data.disabled as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      children: h("span", null, ""),
    }),
  };
}

function kanbanBoardFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawColumns = Array.isArray(data.columns) ? data.columns : [];
  const columns = rawColumns.filter(
    (
      c,
    ): c is {
      key: string;
      label: string;
      items?: Array<{ id: string; label: string; description?: string }>;
    } =>
      typeof c === "object" && c !== null &&
      typeof (c as Record<string, unknown>).key === "string" &&
      typeof (c as Record<string, unknown>).label === "string",
  );
  return {
    ok: true,
    node: h(KanbanBoard, {
      title: data.title as string | undefined,
      columns,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (id: string) => {
          const r = emitBoundEvent(spec, "select", { id });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function layoutGridEditorFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(LayoutGridEditor, {
      value: data.value as string | undefined,
      label: data.label as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: spec.eventBinding.change
        ? (value: string) => {
          const r = emitBoundEvent(spec, "change", { value });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function calculationPreviewPanelFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(CalculationPreviewPanel, {
      title: data.title as string | undefined,
      result: data.result,
      status: data.status as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function documentCanvasTemplateEditorFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const backgroundImageUrl = typeof props.backgroundImageUrl === "string"
    ? props.backgroundImageUrl
    : undefined;
  const fields = Array.isArray(props.fields)
    ? (props.fields as Array<
      { key: string; label?: string; x?: number; y?: number; value?: string }
    >)
    : undefined;
  return {
    ok: true,
    node: h(DocumentCanvasTemplateEditor, {
      backgroundImageUrl,
      fields,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function selectFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  // No authored "change" binding at all renders within the existing runtime adapter contract
  // without requiring one — the same no-binding-required posture formFieldFactory already has
  // for form_input/form_field. An authored binding that IS present must still be validly shaped
  // (requireBinding), so a malformed authored binding still fails close, unchanged.
  const hasChangeBinding = "change" in spec.eventBinding;
  if (hasChangeBinding) {
    const bindingCheck = requireBinding(spec, "change");
    if (!bindingCheck.ok) return bindingCheck;
  }
  const rawOptions = Array.isArray(data.options) ? data.options : [];
  const options = rawOptions
    .filter((o): o is { label: string; value: string } =>
      typeof o === "object" && o !== null &&
      typeof (o as Record<string, unknown>).value === "string"
    )
    .map((o) => ({
      label: typeof o.label === "string" ? o.label : o.value,
      value: o.value,
      disabled: (o as Record<string, unknown>).disabled === true,
    }));
  return {
    ok: true,
    node: h(Select, {
      value: typeof data.value === "string" ? data.value : "",
      options,
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      required: data.required as boolean | undefined,
      error: data.error as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: hasChangeBinding ? (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      } : (() => {}),
      onFocus: spec.eventBinding.focus
        ? () => {
          const r = emitBoundEvent(spec, "focus", {});
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
      onBlur: spec.eventBinding.blur
        ? () => {
          const r = emitBoundEvent(spec, "blur", {});
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function checkboxFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(Checkbox, {
      checked: typeof data.checked === "boolean" ? data.checked : false,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      required: data.required as boolean | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (checked: boolean) => {
        const r = emitBoundEvent(spec, "change", { checked });
        if (!r.ok) throw new Error(r.error);
      },
    }),
  };
}

function badgeFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const label = typeof data.label === "string" ? data.label : "badge";
  const rawTone = data.tone;
  const tone =
    (rawTone === "neutral" || rawTone === "info" || rawTone === "success" ||
        rawTone === "warning" || rawTone === "error")
      ? rawTone
      : "neutral";
  return {
    ok: true,
    node: h(Badge, {
      label,
      tone,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function statusBadgeFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const label = typeof data.label === "string" ? data.label : "";
  const rawTone = data.tone;
  const tone =
    (rawTone === "neutral" || rawTone === "info" || rawTone === "success" ||
        rawTone === "warning" || rawTone === "error")
      ? rawTone
      : "neutral";
  return {
    ok: true,
    node: h(StatusBadge, {
      label,
      tone,
      status: data.status as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function alertFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const message = typeof data.message === "string" ? data.message : "Alert";
  const rawTone = data.tone;
  const tone =
    (rawTone === "info" || rawTone === "success" || rawTone === "warning" ||
        rawTone === "error")
      ? rawTone
      : "info";
  return {
    ok: true,
    node: h(Alert, {
      message,
      tone,
      title: data.title as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function loadingStateFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(LoadingState, {
      message: data.message as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function emptyStateFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(EmptyState, {
      message: data.message as string | undefined,
      description: data.description as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function errorStateFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(ErrorState, {
      errorCode: data.errorCode as string | undefined,
      message: data.message as string | undefined,
      description: data.description as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function jsonViewerFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const value = props.data !== undefined
    ? props.data
    : props.value !== undefined
    ? props.value
    : props;
  return {
    ok: true,
    node: h(JsonViewer, {
      value,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function adminPageShellFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(AdminPageShell, {
      title: typeof data.title === "string" ? data.title : "Admin Page",
      description: data.description as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      children: h(
        "div",
        { style: "color:#888;font-size:0.85rem;padding:8px" },
        "（コンテンツ）",
      ),
    }),
  };
}

function adminSectionFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(AdminSection, {
      title: data.title as string | undefined,
      description: data.description as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      children: h(
        "div",
        { style: "color:#888;font-size:0.85rem;padding:4px" },
        "（コンテンツ）",
      ),
    }),
  };
}

function validationResultPanelFactory(
  spec: RuntimeComponentSpec,
): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  return {
    ok: true,
    node: h(ValidationResultPanel, {
      title: data.title as string | undefined,
      result:
        data.result as Parameters<typeof ValidationResultPanel>[0]["result"] ??
          null,
      className: spec.className,
      design: spec.design ?? {},
    }),
  };
}

function textareaTemplateFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "change");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(Textarea, {
      value: typeof data.value === "string" ? data.value : "",
      placeholder: data.placeholder as string | undefined,
      label: data.label as string | undefined,
      disabled: data.disabled as boolean | undefined,
      required: data.required as boolean | undefined,
      rows: typeof data.rows === "number" ? data.rows : undefined,
      error: data.error as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onChange: (value: string) => {
        const r = emitBoundEvent(spec, "change", { value });
        if (!r.ok) throw new Error(r.error);
      },
      onFocus: spec.eventBinding.focus
        ? () => {
          const r = emitBoundEvent(spec, "focus", {});
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
      onBlur: spec.eventBinding.blur
        ? () => {
          const r = emitBoundEvent(spec, "blur", {});
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function tabsFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const rawItems = Array.isArray(data.items) ? data.items : [];
  const items = rawItems
    .filter((it): it is { key: string; label: string } =>
      typeof it === "object" && it !== null &&
      typeof (it as Record<string, unknown>).key === "string" &&
      typeof (it as Record<string, unknown>).label === "string"
    )
    .map((it) => ({
      key: it.key,
      label: it.label,
      disabled: (it as Record<string, unknown>).disabled === true,
      children: h("div", null, ""),
    }));
  const previewItems = items.length > 0 ? items : [
    { key: "tab1", label: "タブ 1", children: h("div", null, "") },
    { key: "tab2", label: "タブ 2", children: h("div", null, "") },
  ];
  const activeKey = typeof data.activeKey === "string" &&
      previewItems.some((it) => it.key === data.activeKey)
    ? (data.activeKey as string)
    : previewItems[0].key;
  return {
    ok: true,
    node: h(Tabs, {
      items: previewItems,
      activeKey,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (key: string) => {
          const r = emitBoundEvent(spec, "select", { key });
          if (!r.ok) throw new Error(r.error);
        }
        : () => {},
    }),
  };
}

function mediaPlayerFactory(kind: "audio" | "video") {
  return (spec: RuntimeComponentSpec): RenderResult => {
    const props = spec.props;
    const src = typeof props.src === "string" ? props.src.trim() : "";
    if (!src) return { ok: false, error: `${kind}_player requires src` };
    const common = {
      src,
      mimeType: typeof props.mimeType === "string" ? props.mimeType : undefined,
      title: typeof props.title === "string" ? props.title : undefined,
      controls: props.controls !== false,
      autoplay: props.autoplay === true,
      loop: props.loop === true,
      ariaLabel: typeof props.ariaLabel === "string"
        ? props.ariaLabel
        : undefined,
      className: spec.className,
      design: spec.design ?? {},
    };
    return {
      ok: true,
      node: kind === "audio" ? h(AudioPlayer, common) : h(VideoPlayer, {
        ...common,
        poster: typeof props.poster === "string" ? props.poster : undefined,
      }),
    };
  };
}

function treeFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  type TreeNodeInput = {
    key: string;
    label: string;
    children?: unknown[];
    disabled?: boolean;
  };
  function parseNode(n: unknown): TreeNodeInput | null {
    if (typeof n !== "object" || n === null) return null;
    const obj = n as Record<string, unknown>;
    if (typeof obj.key !== "string" || typeof obj.label !== "string") {
      return null;
    }
    return {
      key: obj.key,
      label: obj.label,
      children: Array.isArray(obj.children) ? obj.children : undefined,
      disabled: obj.disabled === true,
    };
  }
  const rawNodes = Array.isArray(data.nodes) ? data.nodes : [];
  const nodes = rawNodes.map(parseNode).filter((n): n is TreeNodeInput =>
    n !== null
  );
  const previewNodes = nodes.length > 0 ? nodes : [
    {
      key: "node1",
      label: "ノード 1",
      children: [{ key: "node1-1", label: "子ノード 1-1" }],
    },
    { key: "node2", label: "ノード 2" },
  ];
  return {
    ok: true,
    node: h(Tree, {
      nodes: previewNodes as Parameters<typeof Tree>[0]["nodes"],
      selectedKey: data.selectedKey as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onSelect: spec.eventBinding.select
        ? (key: string) => {
          const r = emitBoundEvent(spec, "select", { key });
          if (!r.ok) throw new Error(r.error);
        }
        : undefined,
    }),
  };
}

function isCompletedPresetSeedStructureValid(seed: unknown): boolean {
  if (!seed || typeof seed !== "object" || Array.isArray(seed)) return false;
  const s = seed as Record<string, unknown>;
  if (typeof s.seed_version !== "string" || !s.seed_version) return false;
  const requiredObjects = [
    "template_ref",
    "source_ref",
    "binding_ref",
    "render_ref",
    "adjustment_ref",
    "dashboard_ref",
    "lineage_ref",
  ] as const;
  for (const key of requiredObjects) {
    if (!s[key] || typeof s[key] !== "object" || Array.isArray(s[key])) {
      return false;
    }
  }
  const renderRef = s.render_ref as Record<string, unknown>;
  if (
    typeof renderRef.rendered_markdown_hash !== "string" ||
    !renderRef.rendered_markdown_hash
  ) return false;
  if (typeof renderRef.rendered_at !== "string") return false;
  if (typeof renderRef.renderer_version !== "string") return false;
  if (!Array.isArray(renderRef.unresolved_placeholder_keys)) return false;
  const bindingRef = s.binding_ref as Record<string, unknown>;
  if (!Array.isArray(bindingRef.required_placeholder_keys)) return false;
  if (!Array.isArray(bindingRef.optional_placeholder_keys)) return false;
  return true;
}

function normalizeMdViewerSavedView(
  raw: Record<string, unknown>,
): SavedViewDetail | null {
  if (typeof raw.savedViewId !== "string" || !raw.savedViewId) return null;
  if (typeof raw.title !== "string") return null;
  if (typeof raw.renderedMarkdown !== "string") return null;
  const completedPresetSeedJson = raw.completedPresetSeedJson;
  if (
    !completedPresetSeedJson || typeof completedPresetSeedJson !== "object" ||
    Array.isArray(completedPresetSeedJson)
  ) return null;
  if (!isCompletedPresetSeedStructureValid(completedPresetSeedJson)) {
    return null;
  }
  return {
    savedViewId: raw.savedViewId,
    title: raw.title,
    templateKey: typeof raw.templateKey === "string" ? raw.templateKey : "",
    templateId: typeof raw.templateId === "string" ? raw.templateId : "",
    sourceTableRef: typeof raw.sourceTableRef === "string"
      ? raw.sourceTableRef
      : "",
    sourceRecordRef: typeof raw.sourceRecordRef === "string"
      ? raw.sourceRecordRef
      : "",
    status: typeof raw.status === "string" ? raw.status : "active",
    updatedAt: typeof raw.updatedAt === "string" ? raw.updatedAt : "",
    createdAt: typeof raw.createdAt === "string" ? raw.createdAt : "",
    cardMetadataJson: (typeof raw.cardMetadataJson === "object" &&
        raw.cardMetadataJson !== null && !Array.isArray(raw.cardMetadataJson))
      ? raw.cardMetadataJson as Record<string, unknown>
      : {},
    bindingJson:
      (typeof raw.bindingJson === "object" && raw.bindingJson !== null &&
          !Array.isArray(raw.bindingJson))
        ? raw.bindingJson as Record<string, unknown>
        : {},
    renderedMarkdown: raw.renderedMarkdown,
    userAdjustmentPatchJson: (typeof raw.userAdjustmentPatchJson === "object" &&
        raw.userAdjustmentPatchJson !== null &&
        !Array.isArray(raw.userAdjustmentPatchJson))
      ? raw.userAdjustmentPatchJson as Record<string, unknown>
      : {},
    searchIndexText: typeof raw.searchIndexText === "string"
      ? raw.searchIndexText
      : "",
    completedPresetSeedJson: completedPresetSeedJson as CompletedPresetSeed,
  };
}

function mdViewerPreviewFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const savedViewRaw =
    (typeof props.savedView === "object" && props.savedView !== null &&
        !Array.isArray(props.savedView))
      ? props.savedView as Record<string, unknown>
      : null;
  if (!savedViewRaw) {
    return { ok: false, error: "RUNTIME_MD_VIEWER_MISSING_SAVED_VIEW_PROPS" };
  }
  const savedView = normalizeMdViewerSavedView(savedViewRaw);
  if (!savedView) {
    return { ok: false, error: "RUNTIME_MD_VIEWER_INVALID_SAVED_VIEW_PROPS" };
  }
  const seedValid = typeof props.seedValid === "boolean"
    ? props.seedValid
    : true;
  const seedError = typeof props.seedError === "string"
    ? props.seedError
    : undefined;
  // Mutation action callbacks are intentionally not provided.
  // Saved view refresh/clone/rebind/editAdjustment authority stays at /admin/team-dashboard.
  // In preview mode, explicit disabled reasons are shown; in runtime canvas, MdViewer
  // renders unbound action buttons as disabled naturally (no callback → disabled).
  const disabledActionReasons: MdViewerDisabledActionReasons | undefined =
    spec.previewMode
      ? {
        refresh:
          "canvas preview — refresh は /admin/team-dashboard で操作してください",
        clone:
          "canvas preview — clone は /admin/team-dashboard で操作してください",
        rebind:
          "canvas preview — rebind は /admin/team-dashboard で操作してください",
        editAdjustment:
          "canvas preview — 調整編集は /admin/team-dashboard で操作してください",
        openSourceRecord:
          "canvas preview — ソースレコードナビゲーションは canvas では利用不可",
        createTodoCandidate:
          "canvas preview — todo 候補作成は canvas では利用不可",
      }
      : undefined;
  return {
    ok: true,
    node: h(MdViewer, {
      savedView,
      seedValid,
      seedError,
      onClose: () => {},
      disabledActionReasons,
    }),
  };
}

function modalFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const data = (typeof props.data === "object" && props.data !== null &&
      !Array.isArray(props.data))
    ? props.data as Record<string, unknown>
    : props;
  const bindingCheck = requireBinding(spec, "toggle");
  if (!bindingCheck.ok) return bindingCheck;
  return {
    ok: true,
    node: h(Modal, {
      open: typeof data.open === "boolean" ? data.open : false,
      title: data.title as string | undefined,
      description: data.description as string | undefined,
      className: spec.className,
      design: spec.design ?? {},
      onClose: () => {
        const r = emitBoundEvent(spec, "toggle", { open: false });
        if (!r.ok) throw new Error(r.error);
      },
      children: h("div", null, (data.body as string | undefined) ?? ""),
    }),
  };
}

function boxFactory(spec: RuntimeComponentSpec): RenderResult {
  const props = spec.props;
  const style = (typeof props.style === "object" && props.style !== null &&
      !Array.isArray(props.style))
    ? props.style as Record<string, string>
    : undefined;
  const preview = isPreviewMode(spec);
  return {
    ok: true,
    node: h(Box, {
      className: spec.className ??
        (preview
          ? "h-full w-full min-h-[2rem] rounded border border-dashed border-slate-300 bg-slate-50/90"
          : undefined),
      style,
      role: props.role as JSX.HTMLAttributes<HTMLDivElement>["role"],
      "aria-label": props["aria-label"] as string | undefined,
      "data-layout-preview": preview ? "box" : undefined,
      children: preview
        ? h(
          "div",
          {
            class:
              "flex h-full w-full items-center justify-center text-[0.65rem] text-slate-400 select-none",
          },
          "Box",
        )
        : undefined,
    }),
  };
}

function thinPreviewFactory(
  component: any,
  requiredBinding?: string,
): (spec: RuntimeComponentSpec) => RenderResult {
  return (spec) => {
    if (requiredBinding) {
      const check = requireBinding(spec, requiredBinding);
      if (!check.ok) return check;
    }
    return {
      ok: true,
      node: h(component, {
        title: typeof spec.props.title === "string"
          ? spec.props.title
          : undefined,
        value: typeof spec.props.value === "string"
          ? spec.props.value
          : undefined,
        items: Array.isArray(spec.props.items) ? spec.props.items : undefined,
        preview: spec.props.preview ?? spec.props.result ?? spec.props.data,
        onChange: spec.eventBinding.change
          ? (value: string) => {
            const r = emitBoundEvent(spec, "change", { value });
            if (!r.ok) throw new Error(r.error);
          }
          : undefined,
        onSelect: spec.eventBinding.select
          ? (value: string) => {
            const r = emitBoundEvent(spec, "select", { value });
            if (!r.ok) throw new Error(r.error);
          }
          : undefined,
        onConfirm: spec.eventBinding.submit
          ? () => {
            const r = emitBoundEvent(spec, "submit", {});
            if (!r.ok) throw new Error(r.error);
          }
          : undefined,
        className: spec.className,
        design: spec.design ?? {},
      }),
    };
  };
}

export const RUNTIME_COMPONENT_FACTORIES: RuntimeComponentFactory[] = [
  { componentKinds: ["action/button"], render: buttonFactory },
  {
    componentKinds: [
      "form_input/input",
      "form_input/textarea",
      "form_input/search_input",
    ],
    render: inputFactory,
  },
  {
    componentKinds: [
      "display/card",
      "disclosure_structure/panel",
      "disclosure_structure/section",
    ],
    render: cardFactory,
  },
  { componentKinds: ["display/card_list"], render: cardListFactory },
  {
    componentKinds: [
      "data_display/table",
      "data_display/data_grid",
      "data_display/list",
    ],
    render: tableFactory,
  },
  {
    componentKinds: ["search_suggest/autocomplete_input"],
    render: autoCompleteInputFactory,
  },
  {
    componentKinds: ["search_suggest/search_combobox"],
    render: searchComboboxFactory,
  },
  {
    componentKinds: ["search_suggest/candidate_confidence_badge"],
    render: candidateConfidenceBadgeFactory,
  },
  {
    componentKinds: ["inline_edit/inline_editable_field"],
    render: inlineEditableFieldFactory,
  },
  {
    componentKinds: ["inline_edit/patch_preview_panel"],
    render: patchPreviewPanelFactory,
  },
  {
    componentKinds: ["safety_guard/apply_confirm_dialog"],
    render: applyConfirmDialogFactory,
  },
  {
    componentKinds: ["design_token/style_token_picker"],
    render: styleTokenPickerFactory,
  },
  {
    componentKinds: ["design_token/theme_preview_panel"],
    render: themePreviewPanelFactory,
  },
  {
    componentKinds: ["safety_guard/validation_error_panel"],
    render: validationErrorPanelFactory,
  },
  {
    componentKinds: ["search_suggest/suggest_input"],
    render: suggestInputFactory,
  },
  {
    componentKinds: ["search_suggest/recent_input_suggest"],
    render: recentInputSuggestFactory,
  },
  {
    componentKinds: ["search_suggest/relation_candidate_picker"],
    render: relationCandidatePickerFactory,
  },
  {
    componentKinds: ["search_suggest/duplicate_merge_candidate_panel"],
    render: duplicateMergeCandidatePanelFactory,
  },
  {
    componentKinds: ["search_suggest/relation_path_preview"],
    render: relationPathPreviewFactory,
  },
  {
    componentKinds: ["search_suggest/field_resolver_inspector"],
    render: fieldResolverInspectorFactory,
  },
  {
    componentKinds: ["search_suggest/schema_promotion_candidate_panel"],
    render: schemaPromotionCandidatePanelFactory,
  },
  {
    componentKinds: ["search_suggest/select_import_dialog"],
    render: selectImportDialogFactory,
  },
  {
    componentKinds: ["inline_edit/inline_editable_jsonb_field"],
    render: inlineEditableJsonbFieldFactory,
  },
  {
    componentKinds: ["inline_edit/diff_strike_text"],
    render: diffStrikeTextFactory,
  },
  {
    componentKinds: ["inline_edit/audit_diff_drawer"],
    render: auditDiffDrawerFactory,
  },
  {
    componentKinds: ["inline_edit/optimistic_update_boundary"],
    render: optimisticUpdateBoundaryFactory,
  },
  {
    componentKinds: ["inline_edit/confirmed_update_button"],
    render: confirmedUpdateButtonFactory,
  },
  {
    componentKinds: ["inline_edit/undo_timeline"],
    render: undoTimelineFactory,
  },
  {
    componentKinds: ["inline_edit/conflict_resolution_panel"],
    render: conflictResolutionPanelFactory,
  },
  {
    componentKinds: ["table_op/faceted_filter_bar"],
    render: facetedFilterBarFactory,
  },
  { componentKinds: ["table_op/column_filter"], render: columnFilterFactory },
  {
    componentKinds: ["table_op/column_visibility_editor"],
    render: columnVisibilityEditorFactory,
  },
  { componentKinds: ["table_op/sort_control"], render: sortControlFactory },
  {
    componentKinds: ["table_op/group_by_control"],
    render: groupByControlFactory,
  },
  {
    componentKinds: ["table_op/saved_view_selector"],
    render: savedViewSelectorFactory,
  },
  {
    componentKinds: ["table_op/bulk_action_panel"],
    render: bulkActionPanelFactory,
  },
  {
    componentKinds: ["table_op/virtualized_data_table"],
    render: virtualizedDataTableFactory,
  },
  {
    componentKinds: ["table_op/row_detail_drawer"],
    render: rowDetailDrawerFactory,
  },
  {
    componentKinds: ["table_op/pagination_control"],
    render: paginationControlFactory,
  },
  {
    componentKinds: ["table_op/export_candidate_panel"],
    render: exportCandidatePanelFactory,
  },
  {
    componentKinds: ["design_token/font_token_editor"],
    render: fontTokenEditorFactory,
  },
  {
    componentKinds: ["design_token/background_color_editor"],
    render: backgroundColorEditorFactory,
  },
  {
    componentKinds: ["design_token/text_color_editor"],
    render: textColorEditorFactory,
  },
  {
    componentKinds: ["design_token/spacing_token_editor"],
    render: spacingTokenEditorFactory,
  },
  {
    componentKinds: ["design_token/border_radius_editor"],
    render: borderRadiusEditorFactory,
  },
  {
    componentKinds: ["design_token/css_variable_preview"],
    render: cssVariablePreviewFactory,
  },
  {
    componentKinds: ["design_token/shadow_token_editor"],
    render: shadowTokenEditorFactory,
  },
  {
    componentKinds: ["design_token/animation_token_editor"],
    render: animationTokenEditorFactory,
  },
  {
    componentKinds: ["safety_guard/command_palette"],
    render: commandPaletteFactory,
  },
  {
    componentKinds: ["safety_guard/empty_state_action_panel"],
    render: emptyStateActionPanelFactory,
  },
  {
    componentKinds: ["safety_guard/operation_guard_banner"],
    render: operationGuardBannerFactory,
  },
  {
    componentKinds: ["safety_guard/mutation_boundary_inspector"],
    render: mutationBoundaryInspectorFactory,
  },
  {
    componentKinds: ["safety_guard/permission_hint_panel"],
    render: permissionHintPanelFactory,
  },
  {
    componentKinds: ["safety_guard/dry_run_result_panel"],
    render: dryRunResultPanelFactory,
  },
  {
    componentKinds: ["safety_guard/rollback_candidate_panel"],
    render: rollbackCandidatePanelFactory,
  },
  {
    componentKinds: ["safety_guard/operation_audit_log_panel"],
    render: operationAuditLogPanelFactory,
  },
  { componentKinds: ["form_input/form_field"], render: formFieldFactory },
  { componentKinds: ["kanban_drag/kanban_board"], render: kanbanBoardFactory },
  {
    componentKinds: ["kanban_drag/drag_drop_state_transition"],
    render: thinPreviewFactory(DragDropStateTransition),
  },
  {
    componentKinds: ["kanban_drag/drag_sort_list"],
    render: thinPreviewFactory(DragSortList),
  },
  {
    componentKinds: ["kanban_drag/relation_drop_zone"],
    render: thinPreviewFactory(RelationDropZone),
  },
  {
    componentKinds: ["kanban_drag/tree_reorder_drop_zone"],
    render: thinPreviewFactory(TreeReorderDropZone),
  },
  {
    componentKinds: ["kanban_drag/layout_drop_zone"],
    render: thinPreviewFactory(LayoutDropZone),
  },
  {
    componentKinds: ["kanban_drag/component_placement_handle"],
    render: thinPreviewFactory(ComponentPlacementHandle),
  },
  {
    componentKinds: ["kanban_drag/snap_grid_overlay"],
    render: thinPreviewFactory(SnapGridOverlay),
  },
  {
    componentKinds: ["kanban_drag/state_transition_arrow"],
    render: thinPreviewFactory(StateTransitionArrow),
  },
  {
    componentKinds: ["kanban_drag/slot_placeholder_panel"],
    render: thinPreviewFactory(SlotPlaceholderPanel),
  },
  {
    componentKinds: ["design_token/responsive_rule_editor"],
    render: thinPreviewFactory(ResponsiveRuleEditor, "change"),
  },
  {
    componentKinds: ["calc_topology/formula_builder"],
    render: thinPreviewFactory(FormulaBuilder, "change"),
  },
  {
    componentKinds: ["calc_topology/computed_field_preview"],
    render: thinPreviewFactory(ComputedFieldPreview),
  },
  {
    componentKinds: ["calc_topology/relation_score_preview"],
    render: thinPreviewFactory(RelationScorePreview),
  },
  {
    componentKinds: ["calc_topology/hub_statistics_panel"],
    render: thinPreviewFactory(HubStatisticsPanel),
  },
  {
    componentKinds: ["calc_topology/aggregation_preview_table"],
    render: thinPreviewFactory(AggregationPreviewTable),
  },
  {
    componentKinds: ["calc_topology/cross_entity_calculation_panel"],
    render: thinPreviewFactory(CrossEntityCalculationPanel),
  },
  {
    componentKinds: ["calc_topology/topology_distance_preview"],
    render: thinPreviewFactory(TopologyDistancePreview),
  },
  {
    componentKinds: ["calc_topology/route_cost_preview"],
    render: thinPreviewFactory(RouteCostPreview),
  },
  {
    componentKinds: ["calc_topology/attention_weight_preview"],
    render: thinPreviewFactory(AttentionWeightPreview),
  },
  {
    componentKinds: ["calc_topology/cooccurrence_matrix_preview"],
    render: thinPreviewFactory(CooccurrenceMatrixPreview),
  },
  {
    componentKinds: ["calc_topology/rank_score_preview"],
    render: thinPreviewFactory(RankScorePreview),
  },
  {
    componentKinds: ["external_lookup/kana_assist_input"],
    render: thinPreviewFactory(KanaAssistInput, "change"),
  },
  {
    componentKinds: ["external_lookup/postal_address_lookup"],
    render: thinPreviewFactory(PostalAddressLookup, "change"),
  },
  {
    componentKinds: ["external_lookup/address_postal_lookup"],
    render: thinPreviewFactory(AddressPostalLookup, "change"),
  },
  {
    componentKinds: ["external_lookup/tel_address_candidate_lookup"],
    render: thinPreviewFactory(TelAddressCandidateLookup, "change"),
  },
  {
    componentKinds: ["external_lookup/normalize_address_candidate"],
    render: thinPreviewFactory(NormalizeAddressCandidate),
  },
  {
    componentKinds: ["external_lookup/lookup_candidate_confirm_panel"],
    render: thinPreviewFactory(LookupCandidateConfirmPanel, "submit"),
  },
  {
    componentKinds: ["external_lookup/bulk_import_candidate_panel"],
    render: thinPreviewFactory(BulkImportCandidatePanel, "submit"),
  },
  {
    componentKinds: ["design_token/layout_grid_editor"],
    render: layoutGridEditorFactory,
  },
  {
    componentKinds: ["calc_topology/calculation_preview_panel"],
    render: calculationPreviewPanelFactory,
  },
  {
    componentKinds: ["document_canvas/document_canvas_template_editor"],
    render: documentCanvasTemplateEditorFactory,
  },
  { componentKinds: ["layout/box"], render: boxFactory },
  { componentKinds: ["disclosure/modal"], render: modalFactory },
  { componentKinds: ["form_input/select"], render: selectFactory },
  { componentKinds: ["form_input/checkbox"], render: checkboxFactory },
  { componentKinds: ["display/badge"], render: badgeFactory },
  { componentKinds: ["display/status_badge"], render: statusBadgeFactory },
  { componentKinds: ["display/alert"], render: alertFactory },
  { componentKinds: ["feedback/loading"], render: loadingStateFactory },
  { componentKinds: ["feedback/empty"], render: emptyStateFactory },
  { componentKinds: ["feedback/error"], render: errorStateFactory },
  { componentKinds: ["data_display/json"], render: jsonViewerFactory },
  { componentKinds: ["shell/admin_page"], render: adminPageShellFactory },
  { componentKinds: ["shell/admin_section"], render: adminSectionFactory },
  {
    componentKinds: ["validation/result"],
    render: validationResultPanelFactory,
  },
  {
    componentKinds: ["form_input/textarea_template"],
    render: textareaTemplateFactory,
  },
  { componentKinds: ["disclosure/tabs"], render: tabsFactory },
  { componentKinds: ["data_display/tree"], render: treeFactory },
  {
    componentKinds: ["data_display/md_viewer"],
    render: mdViewerPreviewFactory,
  },
  {
    componentKinds: ["media/audio_player"],
    render: mediaPlayerFactory("audio"),
  },
  {
    componentKinds: ["media/video_player"],
    render: mediaPlayerFactory("video"),
  },
];

export {
  buildLayoutPreviewPlaceholderProps,
  buildLayoutPreviewRuntimeSpec,
  renderLayoutComponentPreview,
  resolveComponentKindForLayoutPreview,
} from "./layoutComponentPreview.ts";
export type { LayoutPreviewRenderResult } from "./layoutComponentPreview.ts";

export const __testOnly = {
  parseEventBinding,
  emitBoundEvent,
};
