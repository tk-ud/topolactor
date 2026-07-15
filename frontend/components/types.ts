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

export type ComponentFamily =
  | "primitive"
  | "template"
  | "composite"
  | "packaged"
  | "alias";
export type ComponentSemanticRole =
  | "input"
  | "action"
  | "display"
  | "feedback"
  | "validation"
  | "diagnostic"
  | "layout_shell"
  | "navigation"
  | "data_viewer"
  | "admin_operation";
export type ComponentVisualRole =
  | "field"
  | "button"
  | "badge"
  | "alert"
  | "panel"
  | "table"
  | "card"
  | "modal"
  | "tabs"
  | "tree"
  | "page_shell"
  | "json_viewer"
  | "canvas"
  | "box";
export type ComponentLifecycleStatus =
  | "code_only_drift"
  | "alias_maintained"
  | "bucketed"
  | "packaging"
  | "promoted"
  | "registered"
  | "deprecated";
export type ComponentCapabilityTag =
  | "accepts_children"
  | "accepts_actions"
  | "emits_event"
  | "requires_event_binding"
  | "accepts_design"
  | "accepts_layout"
  | "displays_backend_result"
  | "displays_json"
  | "admin_only"
  | "recursive"
  | "selectable"
  | "controlled_value"
  | "error_display"
  | "loading_display"
  | "field_binding"
  | "preview_surface"
  | "export_snapshot"
  | "dashboard_placement_candidate";

// runtimeConnected means runtime factory/constructor is reachable via runtime registry.
// It does NOT mean DB-registered topology entity.
// registrationRequired indicates DB registration/promotion is still required
// before treating the component as a topology entity.
export type ComponentCatalogIdentity = {
  componentKey: string;
  componentKind: string;
  sourcePath: string;
};

// Separate from runtimeConnected (which strictly means "factory/constructor reachable via
// RUNTIME_COMPONENT_FACTORIES" — see SsotWiringAuditComponentRegistrationTests and
// runtimeComponentCatalogFullConnection.test.ts, and must never be redefined to mean this).
// "existing_route_composition" is the other real, SSOT-sanctioned way a component is reachable:
// a Fresh route (or a parent component already reachable from one) mounts it directly. An entry
// claiming this must set routeCompositionFile to the file that actually imports/mounts it —
// ComponentRegistrationLane_ExistingRouteCompositionEntries_MustBeMountedInClaimedFile (backend)
// and the frontend counterpart in runtimeComponentCatalogFullConnection.test.ts verify the claim
// against the real file contents, not just the prose in `notes`.
export type ComponentRuntimeReachability = "existing_route_composition";

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
  runtimeReachability?: ComponentRuntimeReachability;
  routeCompositionFile?: string;
  notes?: string;
};

export type ComponentCatalogEntry = ComponentCatalogClassification;

export type DesignTokenRefDraft = {
  cssTokenRefs?: string[];
  responsiveTokenRefs?: Record<string, string[]>;
  rawCssFallbackReason?:
    | "legacy_allowed"
    | "no_dictionary_token_exists_yet"
    | "follow_up_token_addition";
};
