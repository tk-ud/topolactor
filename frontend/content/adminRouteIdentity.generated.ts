// GENERATED FILE -- do not hand-edit.
// Regenerate with: python3 .agent/scripts/generate_admin_route_identity.py
// Drift check (no write): python3 .agent/scripts/generate_admin_route_identity.py --check
//
// Source authority: docs/design/admin-console-workflow-ssot.yaml
//   authority.canonical_routes
//   other_admin_routes.master_roster_routes
//   canonical_authoring_order.{contents_pipeline,canvas_workspace_entry,post_contents_entry}
// See docs/design/admin-console-workflow-ssot.yaml
//   page_responsibility.admin_index.static_navigation_sourcing_contract for the sourcing
//   contract this file exists to satisfy.
//
// This file carries route IDENTITY only (href / category / order) -- never wording or copy.
// frontend/content/adminGuides.ts matches each entry here to hand-authored wording, or
// explicitly declares it wording-unresolved via ADMIN_ROUTE_IDENTITY_WITHOUT_WORDING; it never
// silently drops an entry present here.

export type AdminRouteIdentityCategory = "main_flow" | "master_roster" | "canonical_route";

export type AdminRouteIdentity = {
  href: string;
  category: AdminRouteIdentityCategory;
};

/** Full ordered admin route identity, derived from admin-console-workflow-ssot.yaml. */
export const ADMIN_CANONICAL_ROUTE_IDENTITY: readonly AdminRouteIdentity[] = [
  { href: "/admin/contents", category: "main_flow" },
  { href: "/admin/ui-builder", category: "main_flow" },
  { href: "/admin/manifests", category: "main_flow" },
  { href: "/admin/enums", category: "master_roster" },
  { href: "/admin/users", category: "master_roster" },
  { href: "/admin/team-dashboard", category: "canonical_route" },
  { href: "/admin/scheduler", category: "canonical_route" },
];

/** contents -> ui-builder -> manifests primary authoring order (canonical_authoring_order). */
export const ADMIN_MAIN_FLOW_ROUTE_ORDER: readonly string[] = [
  "/admin/contents",
  "/admin/ui-builder",
  "/admin/manifests",
];
