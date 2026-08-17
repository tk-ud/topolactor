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
  /**
   * Applied manifest identity by hubs.topology_manifests.manifest_key — dispatched as
   * payload.target_ref ("manifest_key:<key>:projection_entry"), resolved backend-side via the
   * generic manifest_key_target_ref_resolution_contract (ManifestDispatcher.cs
   * ContentBundleRepository.ResolveActiveManifestIdByManifestKeyAsync — same "exactly one active
   * row" fail-close discipline as canonical_default_entry resolution). Ignored when manifestId is
   * also present — an explicit UUID always wins over a key.
   */
  manifestKey?: string;
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
  return !selection.routeTarget && !selection.manifestId && !selection.manifestKey &&
    !selection.packageId;
}

/**
 * Resolves the initial dispatch axes for the production projection entry.
 * - manifest selection → payload.target_ref (backend manifest resolution authority)
 * - manifest KEY selection (no manifestId) → payload.target_ref, resolved backend-side by
 *   manifest_key instead of a raw UUID — see ProjectionEntrySelection.manifestKey
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
  } else if (selection.manifestKey) {
    axes.payload = {
      target_ref: `manifest_key:${selection.manifestKey}:projection_entry`,
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
  & {
    label: string;
    sequencePosition: number;
    /** hubs.hub_relations row id this link was resolved from — see
     * selected_link_payload_required (admin-normal-surface-projection-seed-ssot.yaml). */
    hubRelationId: string;
    /** Source topology_manifest_id this navigation sequence was resolved for. */
    topologyManifestId: string;
    /** related_hub_id — always present regardless of resolvability. */
    relatedHubId: string;
  }
  & ({ resolvable: true; href: string } | { resolvable: false });

/**
 * Resolves a manifest's NavigationSequence ("current hub relation" candidates) into navigable
 * links. targetManifestId is only present when the backend resolved exactly one topology_manifest
 * for the related hub (docs/design/db-schema.yaml no_implicit_join_nullable_fallback semantics) —
 * absent/null items are returned as explicitly unresolvable, never guessed or silently dropped.
 * hubRelationId/topologyManifestId/relatedHubId are carried through on every item (resolvable or
 * not) so consumers needing the full selected_link_payload_required identity (not just a
 * clickable href) have it without a second resolution path.
 */
export function resolveHubNavigationLinks(
  navigationSequence: readonly HubNavigationSequenceItem[] | undefined,
): ResolvedHubNavigationLink[] {
  return (navigationSequence ?? [])
    .slice()
    .sort((a, b) => a.sequencePosition - b.sequencePosition)
    .map((item) => {
      const base = {
        label: item.relatedHubLabel,
        sequencePosition: item.sequencePosition,
        hubRelationId: item.hubRelationId,
        topologyManifestId: item.topologyManifestId,
        relatedHubId: item.relatedHubId,
      };
      return item.targetManifestId
        ? {
          ...base,
          resolvable: true as const,
          href: `?manifest=${encodeURIComponent(item.targetManifestId)}`,
        }
        : { ...base, resolvable: false as const };
    });
}

/**
 * Round 28: structured discriminant for WHICH confirmation check failed —
 * every failure previously carried the same "PROJECTION_ENTRY_..." error-message
 * PREFIX shape regardless of cause (an explicit ?manifest= URL mismatch and an
 * adopted-identity mismatch both produced "PROJECTION_ENTRY_MANIFEST_MISMATCH:"),
 * so a caller could not distinguish them without substring-matching the message —
 * exactly the "error code文字列のsubstring判定を主authorityにしない" anti-pattern
 * this field exists to avoid. A caller that needs to treat "the response belongs to
 * a manifest OTHER than the one currently adopted" (the ordinary, expected shape of
 * any child-manifest write's own settled result — see ProjectionShell.tsx's
 * handleRuntimeDispatchResult) differently from "the caller's OWN explicit package/
 * manifest URL selection was violated" (a genuine anomaly, never expected for a
 * write override result) reads this field, never the error string.
 */
export type ProjectionEntryConfirmationMismatchReason =
  | "explicit_package_mismatch"
  | "explicit_manifest_mismatch"
  | "adopted_manifest_mismatch";

export type ProjectionEntryConfirmation =
  | { ok: true }
  | {
    ok: false;
    error: string;
    reason: ProjectionEntryConfirmationMismatchReason;
  };

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
 *
 * Two independent manifest checks apply: an explicit ?manifest= URL selection
 * is checked on EVERY call (including the very first, initial dispatch —
 * before any identity has been adopted, since payload.target_ref resolution
 * is still backend authority and must never be trusted unverified even for
 * an explicit selection); a previously-adopted identity (bare-entry / route
 * selection resolved on a prior dispatch, see adoptResolvedManifestIdentity)
 * is checked in addition, on refresh, when supplied via options.
 */
export function confirmProjectionEntryEmission(
  selection: ProjectionEntrySelection,
  emission: Emission,
  options?: ProjectionEntryConfirmationOptions,
): ProjectionEntryConfirmation {
  if (selection.packageId && emission.packageId !== selection.packageId) {
    return {
      ok: false,
      reason: "explicit_package_mismatch",
      error: `PROJECTION_ENTRY_PACKAGE_MISMATCH: selected package ` +
        `"${selection.packageId}" but emission resolved package ` +
        `"${emission.packageId ?? "(absent)"}".`,
    };
  }
  if (selection.manifestId && emission.manifestId !== selection.manifestId) {
    return {
      ok: false,
      reason: "explicit_manifest_mismatch",
      error: `PROJECTION_ENTRY_MANIFEST_MISMATCH: selected manifest ` +
        `"${selection.manifestId}" but this dispatch resolved manifest ` +
        `"${emission.manifestId ?? "(absent)"}".`,
    };
  }
  const adoptedManifestId = options?.adoptedManifestId;
  if (adoptedManifestId && emission.manifestId !== adoptedManifestId) {
    return {
      ok: false,
      reason: "adopted_manifest_mismatch",
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
