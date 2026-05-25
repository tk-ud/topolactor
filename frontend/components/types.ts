import type { ComponentChildren } from "preact";
import type { ComponentDesignParams } from "./Button.tsx";

export type { ComponentDesignParams };

export type { ComponentChildren };

export type ComponentIdentityProps = {
  componentId?: string;
  packageId?: string | null;
  layoutId?: string | null;
  wiringId?: string | null;
};

export type ComponentState = "default" | "loading" | "success" | "error";

export type ComponentEventHandlers = {
  onClick?: () => void;
  onChange?: (value: string) => void;
  onSelect?: (value: string) => void;
  onSubmit?: () => void;
  onToggle?: (open: boolean) => void;
  onExpand?: () => void;
  onCollapse?: () => void;
  onFocus?: () => void;
  onBlur?: () => void;
};

export type ComponentTemplateProps = ComponentIdentityProps & {
  className?: string;
  design?: ComponentDesignParams;
  state?: ComponentState;
  disabled?: boolean;
  title?: string;
  description?: string;
  footer?: ComponentChildren;
  actions?: ComponentChildren;
  children?: ComponentChildren;
};

export function mergeDesignClassName(
  className?: string,
  design?: ComponentDesignParams,
): string | undefined {
  return (
    [className, design?.classname, design?.className, design?.tailwind]
      .filter(Boolean)
      .join(" ") || undefined
  );
}

export function mergeDesignStyle(
  baseStyle: string,
  design?: ComponentDesignParams,
): string {
  return design?.style ? `${baseStyle};${design.style}` : baseStyle;
}

export function computeDesignDisabled(
  disabled: boolean | undefined,
  design?: ComponentDesignParams,
): boolean {
  return disabled === true || design?.state === "loading";
}

// Candidate component_kind strings for future SUPPORTED_COMPONENT_KINDS registration.
// These are code-only template candidates; registration requires DB promotion
// (ui_component_bucket -> package_generator -> componentId/packageId/layoutId/wiringId).
//
// "form_input/select"      → Select
// "form_input/checkbox"    → Checkbox
// NOTE: "form_input/textarea" is already a live runtime alias in SUPPORTED_COMPONENT_KINDS
// and currently flows through the Input primitive adapter path (see runtimePrimitiveRenderer
// and /admin/ui-builder PrimitiveCatalog textarea.alias). Keep this candidate list scoped to
// new code-only templates that are NOT runtime-connected in this PR.
// "display/badge"          → Badge / StatusBadge
// "display/alert"          → Alert
// "feedback/loading"       → LoadingState
// "feedback/empty"         → EmptyState
// "feedback/error"         → ErrorState
// "data_display/json"      → JsonViewer
// "shell/admin_page"       → AdminPageShell
// "shell/admin_section"    → AdminSection
// "validation/result"      → ValidationResultPanel
// "disclosure/tabs"        → Tabs
// "disclosure/modal"       → Modal
// "data_display/tree"      → Tree / TreeNode


export type ComponentFamily = "primitive" | "template" | "composite" | "packaged" | "alias";
export type ComponentSemanticRole = "input" | "action" | "display" | "feedback" | "validation" | "diagnostic" | "layout_shell" | "navigation" | "data_viewer" | "admin_operation";
export type ComponentVisualRole = "field" | "button" | "badge" | "alert" | "panel" | "table" | "card" | "modal" | "tabs" | "tree" | "page_shell" | "json_viewer";
export type ComponentLifecycleStatus = "code_only_drift" | "alias_maintained" | "bucketed" | "packaging" | "promoted" | "registered" | "deprecated";
export type ComponentCapabilityTag = "accepts_children" | "accepts_actions" | "emits_event" | "requires_event_binding" | "accepts_design" | "accepts_layout" | "displays_backend_result" | "displays_json" | "admin_only" | "recursive" | "selectable" | "controlled_value" | "error_display" | "loading_display";

// runtimeConnected means runtime adapter/renderer support (code/runtime connection).
// It does NOT mean DB-registered topology entity.
// registrationRequired indicates DB registration/promotion is still required
// before treating the component as a topology entity.
export type ComponentCatalogClassification = {
  componentKey: string;
  componentKind: string;
  sourcePath: string;
  componentFamily: ComponentFamily;
  semanticRole: ComponentSemanticRole;
  visualRole: ComponentVisualRole;
  lifecycleStatus: ComponentLifecycleStatus;
  capabilityTags: ComponentCapabilityTag[];
  runtimeConnected: boolean;
  registrationRequired: boolean;
  notes?: string;
};

export type ComponentCatalogEntry = ComponentCatalogClassification;
