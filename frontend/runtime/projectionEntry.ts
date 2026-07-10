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
import type { Emission, HubNavigationSequenceItem } from "../api/dispatch.ts";

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

/**
 * A navigation sequence item resolved into a clickable entry href, or explicitly marked
 * unresolvable. "呼べる状態にする" — the projection becomes able to call the target manifest —
 * means a resolvable item's href drives the existing ?manifest= entry-selection path
 * (parseProjectionEntrySelection / resolveProjectionEntryAxes) via a normal navigation, matching
 * the existing route_navigation preset's globalThis.location.href pattern
 * (admin-console-workflow-ssot.yaml default_wiring_presets.route_navigation.runtime_execution).
 * No in-place re-dispatch is invented here — navigation reuses the same entry mount path.
 */
export type ResolvedHubNavigationLink =
  | { resolvable: true; href: string; label: string; sequencePosition: number }
  | { resolvable: false; label: string; sequencePosition: number };

/**
 * Resolves a manifest's NavigationSequence ("current hub relation" candidates) into navigable
 * links. targetManifestId is only present when the backend resolved exactly one topology_manifest
 * for the related hub (docs/design/db-schema.yaml no_implicit_join_nullable_fallback semantics) —
 * absent/null items are returned as explicitly unresolvable, never guessed or silently dropped.
 */
export function resolveHubNavigationLinks(
  navigationSequence: readonly HubNavigationSequenceItem[] | undefined,
): ResolvedHubNavigationLink[] {
  return (navigationSequence ?? [])
    .slice()
    .sort((a, b) => a.sequencePosition - b.sequencePosition)
    .map((item) =>
      item.targetManifestId
        ? {
          resolvable: true,
          href: `?manifest=${encodeURIComponent(item.targetManifestId)}`,
          label: item.relatedHubLabel,
          sequencePosition: item.sequencePosition,
        }
        : {
          resolvable: false,
          label: item.relatedHubLabel,
          sequencePosition: item.sequencePosition,
        }
    );
}

export type ProjectionEntryConfirmation =
  | { ok: true }
  | { ok: false; error: string };

export type ProjectionEntryConfirmationOptions = {
  /**
   * The manifest identity adopted as the current dispatch identity after the
   * initial dispatch resolved it (see adoptResolvedManifestIdentity) —
   * independent of whether the URL selection carried an explicit ?manifest=.
   * When present, a refresh must resolve the SAME manifest; drift is an
   * explicit error, never a silent re-render under a different manifest.
   */
  adoptedManifestId?: string;
};

/**
 * Confirms a dispatched emission against the explicit selection AND, when
 * provided, against the manifest identity previously adopted as the current
 * dispatch identity. Package/manifest resolution is backend authority — the
 * frontend only verifies the resolved identity matches what was explicitly
 * selected or previously adopted. Mismatch is an explicit error; rendering a
 * differently-packaged/differently-manifested projection silently is
 * prohibited.
 */
export function confirmProjectionEntryEmission(
  selection: ProjectionEntrySelection,
  emission: Emission,
  options?: ProjectionEntryConfirmationOptions,
): ProjectionEntryConfirmation {
  if (selection.packageId && emission.packageId !== selection.packageId) {
    return {
      ok: false,
      error: `PROJECTION_ENTRY_PACKAGE_MISMATCH: selected package ` +
        `"${selection.packageId}" but emission resolved package ` +
        `"${emission.packageId ?? "(absent)"}".`,
    };
  }
  const adoptedManifestId = options?.adoptedManifestId;
  if (adoptedManifestId && emission.manifestId !== adoptedManifestId) {
    return {
      ok: false,
      error: `PROJECTION_ENTRY_MANIFEST_MISMATCH: adopted manifest ` +
        `"${adoptedManifestId}" but this dispatch resolved manifest ` +
        `"${emission.manifestId ?? "(absent)"}".`,
    };
  }
  return { ok: true };
}

/**
 * Adopts the backend-resolved Emission.ManifestId as the current dispatch
 * identity for subsequent (e.g. SSE-triggered) refresh dispatches.
 *
 * Backend holds no shared "current manifest" state — every dispatch resolves
 * axes independently (see resolveProjectionEntryAxes). The frontend is
 * responsible for pinning WHICH manifest a refresh must target once the
 * initial dispatch has resolved one, so a refresh re-resolves the SAME
 * manifest rather than merely replaying the same pre-resolution axes (which
 * only coincidentally re-resolves the same manifest when resolution happens
 * to be stable).
 *
 * - When currentAxes already carries an explicit payload.target_ref (from an
 *   explicit ?manifest= URL selection), it is left unchanged — adoption must
 *   never override an explicit user selection.
 * - Otherwise, when emission.manifestId is present, it is merged in as
 *   payload.target_ref so refresh axes target that same manifest.
 * - When emission.manifestId is absent (e.g. dev bypass path), currentAxes is
 *   returned unchanged — there is nothing to adopt.
 */
export function adoptResolvedManifestIdentity(
  currentAxes: UserOperation,
  emission: Emission,
): UserOperation {
  const existingTargetRef = currentAxes.payload?.target_ref;
  if (typeof existingTargetRef === "string" && existingTargetRef.length > 0) {
    return currentAxes;
  }
  if (!emission.manifestId) return currentAxes;
  return {
    ...currentAxes,
    payload: {
      ...(currentAxes.payload ?? {}),
      target_ref: `manifest:${emission.manifestId}:projection_entry`,
    },
  };
}
