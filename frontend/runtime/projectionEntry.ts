/**
 * projectionEntry — route/package/manifest-aware production projection entry selection.
 *
 * The production projection surface (ProjectionShell) must not be fixed to the
 * default/screen_list/Search axes: any UI Builder applied topology must be
 * selectable through it. Selection is expressed in the entry URL and translated
 * here into canonical dispatch axes — the frontend performs NO topology meaning
 * judgment; manifest resolution stays backend authority (ManifestDispatcher
 * axes resolution or payload.target_ref manifest resolution).
 *
 * URL selection vocabulary:
 *   ?route=<target>     — dispatcher_mapping target axis for axes resolution
 *   ?manifest=<uuid>    — explicit applied manifest via payload.target_ref
 *                         ("manifest:<uuid>:projection_entry")
 *   ?package=<uuid>     — expected package identity, confirmed against
 *                         emission.packageId after dispatch (backend package
 *                         resolution stays authoritative; mismatch is an
 *                         explicit error, never silently rendered)
 *
 * No selection keeps the existing default entry axes. Malformed selection is a
 * fail-close parse error — no silent fallback to the default axes.
 */

import type { UserOperation } from "./resolveOperationVector.ts";
import type { Emission } from "../api/dispatch.ts";

export type ProjectionEntrySelection = {
  /** dispatcher_mapping target axis (routeKey / topologySystemName). */
  routeTarget?: string;
  /** Applied manifest id — dispatched as payload.target_ref. */
  manifestId?: string;
  /** Expected packageId, confirmed against emission.packageId. */
  packageId?: string;
};

export type ProjectionEntryParseResult =
  | { ok: true; selection: ProjectionEntrySelection }
  | { ok: false; error: string };

const UUID_RE =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

/**
 * Parses the projection entry selection from a location.search string.
 * Absent params yield an empty selection (default entry).
 * Malformed manifest/package uuids fail close with an explicit error.
 */
export function parseProjectionEntrySelection(
  search: string,
): ProjectionEntryParseResult {
  const params = new URLSearchParams(search.startsWith("?") ? search.slice(1) : search);
  const selection: ProjectionEntrySelection = {};

  const route = params.get("route")?.trim();
  if (route) selection.routeTarget = route;

  const manifest = params.get("manifest")?.trim();
  if (manifest) {
    if (!UUID_RE.test(manifest)) {
      return {
        ok: false,
        error:
          `PROJECTION_ENTRY_MANIFEST_INVALID: manifest "${manifest}" is not a valid manifest id.`,
      };
    }
    selection.manifestId = manifest;
  }

  const pkg = params.get("package")?.trim();
  if (pkg) {
    if (!UUID_RE.test(pkg)) {
      return {
        ok: false,
        error:
          `PROJECTION_ENTRY_PACKAGE_INVALID: package "${pkg}" is not a valid package id.`,
      };
    }
    selection.packageId = pkg;
  }

  return { ok: true, selection };
}

/** True when the selection carries no route/package/manifest axes (default entry). */
export function isDefaultProjectionEntry(
  selection: ProjectionEntrySelection,
): boolean {
  return !selection.routeTarget && !selection.manifestId && !selection.packageId;
}

/**
 * Resolves the initial dispatch axes for the production projection entry.
 * - manifest selection → payload.target_ref (backend manifest resolution authority)
 * - route selection → target axis (backend axes resolution authority)
 * - no selection → existing default entry axes (default/screen_list/Search)
 * layer/action stay on the canonical screen read lane (ScreenDataShapeQueryRuntime).
 */
export function resolveProjectionEntryAxes(
  selection: ProjectionEntrySelection,
): UserOperation {
  const target = selection.routeTarget ?? "default";
  const axes: UserOperation = {
    operationType: "Search",
    target,
    layer: "screen_list",
    action: "Search",
  };
  if (selection.manifestId) {
    axes.payload = {
      target_ref: `manifest:${selection.manifestId}:projection_entry`,
    };
  }
  return axes;
}

export type ProjectionEntryConfirmation =
  | { ok: true }
  | { ok: false; error: string };

/**
 * Confirms a dispatched emission against the explicit selection.
 * Package resolution is backend authority — the frontend only verifies the
 * resolved identity matches an explicitly selected package. Mismatch is an
 * explicit error; rendering a differently-packaged projection silently under an
 * explicit package selection is prohibited.
 */
export function confirmProjectionEntryEmission(
  selection: ProjectionEntrySelection,
  emission: Emission,
): ProjectionEntryConfirmation {
  if (selection.packageId && emission.packageId !== selection.packageId) {
    return {
      ok: false,
      error: `PROJECTION_ENTRY_PACKAGE_MISMATCH: selected package ` +
        `"${selection.packageId}" but emission resolved package ` +
        `"${emission.packageId ?? "(absent)"}".`,
    };
  }
  return { ok: true };
}
