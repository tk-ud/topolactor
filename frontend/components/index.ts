// Existing primitives (do not replace; extend via template layer)
export { Button } from "./Button.tsx";
export type { ButtonProps, ComponentDesignParams } from "./Button.tsx";
export { Input } from "./Input.tsx";
export type { InputProps } from "./Input.tsx";
export { Table } from "./Table.tsx";
export type { TableProps, TableColumn } from "./Table.tsx";
export { Card } from "./Card.tsx";
export type { CardProps } from "./Card.tsx";

// Shared types and helpers
export {
  mergeDesignClassName,
  mergeDesignStyle,
  computeDesignDisabled,
} from "./types.ts";
export type {
  ComponentIdentityProps,
  ComponentState,
  ComponentEventHandlers,
  ComponentTemplateProps,
} from "./types.ts";

// Priority A: component templates (code-only template candidates; not DB-registered)
export { FormField } from "./FormField.tsx";
export type { FormFieldProps } from "./FormField.tsx";
export { Select } from "./Select.tsx";
export type { SelectProps, SelectOption } from "./Select.tsx";
export { Checkbox } from "./Checkbox.tsx";
export type { CheckboxProps } from "./Checkbox.tsx";
export { Badge, StatusBadge } from "./Badge.tsx";
export type { BadgeProps, BadgeTone, StatusBadgeProps } from "./Badge.tsx";
export { Alert } from "./Alert.tsx";
export type { AlertProps, AlertTone } from "./Alert.tsx";
export { LoadingState } from "./LoadingState.tsx";
export type { LoadingStateProps } from "./LoadingState.tsx";
export { EmptyState } from "./EmptyState.tsx";
export type { EmptyStateProps } from "./EmptyState.tsx";
export { ErrorState } from "./ErrorState.tsx";
export type { ErrorStateProps } from "./ErrorState.tsx";
export { JsonViewer } from "./JsonViewer.tsx";
export type { JsonViewerProps } from "./JsonViewer.tsx";
export { AdminPageShell } from "./AdminPageShell.tsx";
export type { AdminPageShellProps, BreadcrumbItem } from "./AdminPageShell.tsx";
export { AdminSection } from "./AdminSection.tsx";
export type { AdminSectionProps } from "./AdminSection.tsx";
export { ValidationResultPanel } from "./ValidationResultPanel.tsx";
export type {
  ValidationResultPanelProps,
  ValidationResultPayload,
  ValidationResultItem,
  ValidationStatus,
} from "./ValidationResultPanel.tsx";

// Priority B: component templates (code-only template candidates; not DB-registered)
export { Textarea } from "./Textarea.tsx";
export type { TextareaProps } from "./Textarea.tsx";
export { Tabs } from "./Tabs.tsx";
export type { TabsProps, TabItem } from "./Tabs.tsx";
export { Modal } from "./Modal.tsx";
export type { ModalProps } from "./Modal.tsx";
export { Tree, TreeNode } from "./Tree.tsx";
export type { TreeProps, TreeNodeProps, TreeNodeData } from "./Tree.tsx";

// search_suggest components
export { SuggestInput } from "./SuggestInput.tsx";
export type { SuggestInputProps } from "./SuggestInput.tsx";
export { RecentInputSuggest } from "./RecentInputSuggest.tsx";
export type { RecentInputSuggestProps } from "./RecentInputSuggest.tsx";
export { RelationPathPreview } from "./RelationPathPreview.tsx";
export type { RelationPathPreviewProps, RelationPathSegment } from "./RelationPathPreview.tsx";
export { FieldResolverInspector } from "./FieldResolverInspector.tsx";
export type { FieldResolverInspectorProps } from "./FieldResolverInspector.tsx";
export { RelationCandidatePicker } from "./RelationCandidatePicker.tsx";
export type { RelationCandidatePickerProps, RelationCandidate } from "./RelationCandidatePicker.tsx";
export { DuplicateMergeCandidatePanel } from "./DuplicateMergeCandidatePanel.tsx";
export type { DuplicateMergeCandidatePanelProps, DuplicateMergeCandidate } from "./DuplicateMergeCandidatePanel.tsx";
export { SchemaPromotionCandidatePanel } from "./SchemaPromotionCandidatePanel.tsx";
export type { SchemaPromotionCandidatePanelProps, SchemaPromotionCandidate } from "./SchemaPromotionCandidatePanel.tsx";
export { SelectImportDialog } from "./SelectImportDialog.tsx";
export type { SelectImportDialogProps, SelectImportDialogCandidate } from "./SelectImportDialog.tsx";
export { KanbanBoard } from "./KanbanBoard.tsx";
export type { KanbanBoardProps, KanbanBoardColumn, KanbanBoardItem } from "./KanbanBoard.tsx";
export { LayoutGridEditor } from "./LayoutGridEditor.tsx";
export type { LayoutGridEditorProps } from "./LayoutGridEditor.tsx";
export { CalculationPreviewPanel } from "./CalculationPreviewPanel.tsx";
export type { CalculationPreviewPanelProps } from "./CalculationPreviewPanel.tsx";

// inline_edit components
export { InlineEditableJsonbField } from "./InlineEditableJsonbField.tsx";
export type { InlineEditableJsonbFieldProps } from "./InlineEditableJsonbField.tsx";
export { DiffStrikeText } from "./DiffStrikeText.tsx";
export type { DiffStrikeTextProps } from "./DiffStrikeText.tsx";
export { AuditDiffDrawer } from "./AuditDiffDrawer.tsx";
export type { AuditDiffDrawerProps, AuditDiffEntry } from "./AuditDiffDrawer.tsx";
export { OptimisticUpdateBoundary } from "./OptimisticUpdateBoundary.tsx";
export type { OptimisticUpdateBoundaryProps } from "./OptimisticUpdateBoundary.tsx";
export { ConfirmedUpdateButton } from "./ConfirmedUpdateButton.tsx";
export type { ConfirmedUpdateButtonProps } from "./ConfirmedUpdateButton.tsx";
export { UndoTimeline } from "./UndoTimeline.tsx";
export type { UndoTimelineProps, UndoTimelineItem } from "./UndoTimeline.tsx";
export { ConflictResolutionPanel } from "./ConflictResolutionPanel.tsx";
export type { ConflictResolutionPanelProps, ConflictEntry } from "./ConflictResolutionPanel.tsx";

export { COMPONENT_CATALOG_ENTRIES } from "./catalog.ts";
export type {
  ComponentFamily, ComponentSemanticRole, ComponentVisualRole, ComponentLifecycleStatus,
  ComponentCapabilityTag, ComponentCatalogClassification, ComponentCatalogEntry,
} from "./types.ts";


export { DragDropStateTransition } from "./DragDropStateTransition.tsx";
export { DragSortList } from "./DragSortList.tsx";
export { RelationDropZone } from "./RelationDropZone.tsx";
export { TreeReorderDropZone } from "./TreeReorderDropZone.tsx";
export { LayoutDropZone } from "./LayoutDropZone.tsx";
export { ComponentPlacementHandle } from "./ComponentPlacementHandle.tsx";
export { SnapGridOverlay } from "./SnapGridOverlay.tsx";
export { StateTransitionArrow } from "./StateTransitionArrow.tsx";
export { SlotPlaceholderPanel } from "./SlotPlaceholderPanel.tsx";
export { ResponsiveRuleEditor } from "./ResponsiveRuleEditor.tsx";
export { FormulaBuilder } from "./FormulaBuilder.tsx";
export { ComputedFieldPreview } from "./ComputedFieldPreview.tsx";
export { RelationScorePreview } from "./RelationScorePreview.tsx";
export { HubStatisticsPanel } from "./HubStatisticsPanel.tsx";
export { AggregationPreviewTable } from "./AggregationPreviewTable.tsx";
export { CrossEntityCalculationPanel } from "./CrossEntityCalculationPanel.tsx";
export { TopologyDistancePreview } from "./TopologyDistancePreview.tsx";
export { RouteCostPreview } from "./RouteCostPreview.tsx";
export { AttentionWeightPreview } from "./AttentionWeightPreview.tsx";
export { CooccurrenceMatrixPreview } from "./CooccurrenceMatrixPreview.tsx";
export { RankScorePreview } from "./RankScorePreview.tsx";
export { KanaAssistInput } from "./KanaAssistInput.tsx";
export { PostalAddressLookup } from "./PostalAddressLookup.tsx";
export { AddressPostalLookup } from "./AddressPostalLookup.tsx";
export { TelAddressCandidateLookup } from "./TelAddressCandidateLookup.tsx";
export { NormalizeAddressCandidate } from "./NormalizeAddressCandidate.tsx";
export { LookupCandidateConfirmPanel } from "./LookupCandidateConfirmPanel.tsx";
export { BulkImportCandidatePanel } from "./BulkImportCandidatePanel.tsx";

// document_canvas — projection scaffold only
export { DocumentCanvasTemplateEditor } from "./DocumentCanvasTemplateEditor.tsx";
export type {
  DocumentCanvasTemplateEditorProps,
  DocumentCanvasTemplateEditorField,
} from "./DocumentCanvasTemplateEditor.tsx";
