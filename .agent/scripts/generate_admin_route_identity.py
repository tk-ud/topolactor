#!/usr/bin/env python3
"""generate_admin_route_identity.py -- generates
frontend/content/adminRouteIdentity.generated.ts from
docs/design/admin-console-workflow-ssot.yaml.

SSOT: docs/design/admin-console-workflow-ssot.yaml
  page_responsibility.admin_index.static_navigation_sourcing_contract
  authority.canonical_routes
  other_admin_routes.master_roster_routes
  canonical_authoring_order.{contents_pipeline,canvas_workspace_entry,post_contents_entry}

Reads the SSOT's own route registry (raw-text regex extraction -- the same
pattern already used by frontend/tests/adminMainFlow.test.ts and
frontend/tests/adminUxGuard.test.ts to cross-check this file, and by
check_react_schema_topology_seed_translator.py's 36r1-36r3/7d checks for
cross-SSOT drift) and derives:

  - ADMIN_MAIN_FLOW_ROUTE_ORDER: the 3-route contents -> ui-builder ->
    manifests primary authoring order (canonical_authoring_order).
  - ADMIN_CANONICAL_ROUTE_IDENTITY: the full ordered admin route identity
    list this Bundle sources for /admin index page static navigation --
    main flow routes, then other_admin_routes.master_roster_routes (in
    SSOT order), then any remaining authority.canonical_routes entries not
    already covered (in SSOT order), excluding /admin itself (the index
    page is not a card pointing at itself).

This script only emits route identity (href + category + order) -- never
wording/copy. frontend/content/adminGuides.ts owns matching each identity
entry to hand-authored wording (or explicitly declaring it unresolved via
ADMIN_ROUTE_IDENTITY_WITHOUT_WORDING); this script has no way to know
which entries currently have wording and does not decide that.

Usage:
  python3 .agent/scripts/generate_admin_route_identity.py             # regenerate in place
  python3 .agent/scripts/generate_admin_route_identity.py --check     # exit 1 on drift, write nothing
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SSOT_PATH = REPO_ROOT / "docs" / "design" / "admin-console-workflow-ssot.yaml"
OUTPUT_PATH = REPO_ROOT / "frontend" / "content" / "adminRouteIdentity.generated.ts"


def extract_canonical_routes(ssot_text: str) -> list[str]:
    m = re.search(r"\n {4}canonical_routes:\n((?: {6}- /[^\n]+\n)+)", ssot_text)
    if not m:
        raise SystemExit("could not locate authority.canonical_routes in admin-console-workflow-ssot.yaml")
    return [mm.group(1) for mm in re.finditer(r"- (/\S+)", m.group(1))]


def extract_master_roster_routes(ssot_text: str) -> list[str]:
    m = re.search(r"\n {4}master_roster_routes:\n([\s\S]*?)\n {2}\S", ssot_text)
    if not m:
        raise SystemExit("could not locate other_admin_routes.master_roster_routes in admin-console-workflow-ssot.yaml")
    return [mm.group(1) for mm in re.finditer(r"route: (/\S+)", m.group(1))]


def _extract_subsection_route(block: str, key: str) -> str:
    """Extract the first `route: /...` inside a `    <key>:` subsection of a
    `  canonical_authoring_order:` block, where the subsection ends at the next
    line indented at exactly 4 spaces (a sibling key) or the block's end."""
    m = re.search(rf"\n {{4}}{re.escape(key)}:\n((?:.*\n)*?)(?=\n {{4}}\S|\Z)", block)
    if not m:
        raise SystemExit(f"could not locate canonical_authoring_order.{key}")
    route_m = re.search(r"route: (/\S+)", m.group(1))
    if not route_m:
        raise SystemExit(f"could not locate route in canonical_authoring_order.{key}")
    return route_m.group(1)


def extract_main_flow_route_order(ssot_text: str) -> list[str]:
    m = re.search(r"\n {2}canonical_authoring_order:\n([\s\S]*?)\n {2}\S", ssot_text)
    if not m:
        raise SystemExit("could not locate canonical_authoring_order in admin-console-workflow-ssot.yaml")
    block = "\n" + m.group(1)

    contents_route = _extract_subsection_route(block, "contents_pipeline")
    canvas_route = _extract_subsection_route(block, "canvas_workspace_entry")
    post_route = _extract_subsection_route(block, "post_contents_entry")

    return [contents_route, canvas_route, post_route]


def derive_identity(ssot_text: str) -> list[tuple[str, str]]:
    canonical_routes = extract_canonical_routes(ssot_text)
    master_roster_routes = extract_master_roster_routes(ssot_text)
    main_flow_routes = extract_main_flow_route_order(ssot_text)

    identity: list[tuple[str, str]] = []
    seen: set[str] = set()

    for href in main_flow_routes:
        if href not in seen:
            identity.append((href, "main_flow"))
            seen.add(href)

    for href in master_roster_routes:
        if href not in seen:
            identity.append((href, "master_roster"))
            seen.add(href)

    for href in canonical_routes:
        if href == "/admin":
            continue
        if href not in seen:
            identity.append((href, "canonical_route"))
            seen.add(href)

    return identity


def render_ts(identity: list[tuple[str, str]], main_flow_routes: list[str]) -> str:
    identity_lines = "\n".join(
        f'  {{ href: "{href}", category: "{category}" }},' for href, category in identity
    )
    main_flow_lines = "\n".join(f'  "{href}",' for href in main_flow_routes)
    return f"""// GENERATED FILE -- do not hand-edit.
// Regenerate with: python3 .agent/scripts/generate_admin_route_identity.py
// Drift check (no write): python3 .agent/scripts/generate_admin_route_identity.py --check
//
// Source authority: docs/design/admin-console-workflow-ssot.yaml
//   authority.canonical_routes
//   other_admin_routes.master_roster_routes
//   canonical_authoring_order.{{contents_pipeline,canvas_workspace_entry,post_contents_entry}}
// See docs/design/admin-console-workflow-ssot.yaml
//   page_responsibility.admin_index.static_navigation_sourcing_contract for the sourcing
//   contract this file exists to satisfy.
//
// This file carries route IDENTITY only (href / category / order) -- never wording or copy.
// frontend/content/adminGuides.ts matches each entry here to hand-authored wording, or
// explicitly declares it wording-unresolved via ADMIN_ROUTE_IDENTITY_WITHOUT_WORDING; it never
// silently drops an entry present here.

export type AdminRouteIdentityCategory = "main_flow" | "master_roster" | "canonical_route";

export type AdminRouteIdentity = {{
  href: string;
  category: AdminRouteIdentityCategory;
}};

/** Full ordered admin route identity, derived from admin-console-workflow-ssot.yaml. */
export const ADMIN_CANONICAL_ROUTE_IDENTITY: readonly AdminRouteIdentity[] = [
{identity_lines}
];

/** contents -> ui-builder -> manifests primary authoring order (canonical_authoring_order). */
export const ADMIN_MAIN_FLOW_ROUTE_ORDER: readonly string[] = [
{main_flow_lines}
];
"""


def main() -> int:
    check_only = "--check" in sys.argv[1:]
    ssot_text = SSOT_PATH.read_text(encoding="utf-8").replace("\r\n", "\n")
    identity = derive_identity(ssot_text)
    main_flow_routes = extract_main_flow_route_order(ssot_text)
    rendered = render_ts(identity, main_flow_routes)

    if check_only:
        current = OUTPUT_PATH.read_text(encoding="utf-8") if OUTPUT_PATH.is_file() else None
        if current != rendered:
            print(f"DRIFT: {OUTPUT_PATH} is out of sync with docs/design/admin-console-workflow-ssot.yaml", file=sys.stderr)
            print("Run: python3 .agent/scripts/generate_admin_route_identity.py", file=sys.stderr)
            return 1
        print(f"OK: {OUTPUT_PATH} matches docs/design/admin-console-workflow-ssot.yaml")
        return 0

    OUTPUT_PATH.write_text(rendered, encoding="utf-8")
    print(f"wrote {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
