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

export { COMPONENT_CATALOG_ENTRIES } from "./catalog.ts";
export type {
  ComponentFamily, ComponentSemanticRole, ComponentVisualRole, ComponentLifecycleStatus,
  ComponentCapabilityTag, ComponentCatalogClassification, ComponentCatalogEntry,
} from "./types.ts";
