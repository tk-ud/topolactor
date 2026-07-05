import {
  type AdminManifestListFilter,
  getAdminManifest,
  listAdminManifests,
} from "../api/adminApi.ts";
import { getStoredScreenLabel } from "../runtime/screenAuthoringIntent.ts";
import { extractScreenDataShapeFromTopology } from "./manifestTopologyExtensions.ts";
import {
  isValidTopologySystemName,
  resolveVisibleTopologyName,
  topologySystemNameToUiBuilderKey,
} from "./topologySystemName.ts";

/** SSOT: manifest_list_and_contents_authoring_contract surface_filters.contents_draft_picker */
export const CONTENTS_ROUTE_MANIFEST_LIST_FILTER: AdminManifestListFilter = {
  contentsType: "contents",
  requiresTopologySystemName: true,
};

export type ManifestRouteOption = {
  manifestId: string;
  topologySystemName: string;
  label: string;
  derivedRouteKey: string;
  status: string;
};

/** Resolve manifests that have a valid topology.name and derive UI Builder routeKey. */
export async function loadManifestRouteOptions(): Promise<ManifestRouteOption[]> {
  const items = await listAdminManifests(CONTENTS_ROUTE_MANIFEST_LIST_FILTER);
  if (!items) return [];

  const resolved: ManifestRouteOption[] = [];
  for (const item of items) {
    let sysName = item.topologySystemName?.trim() ?? "";
    let userFacingLabel = item.userFacingTopologyLabel?.trim() ?? "";

    if (!sysName) {
      const detail = await getAdminManifest(item.manifestId);
      const shape = detail
        ? extractScreenDataShapeFromTopology(detail.topologyRawJson)
        : null;
      sysName = shape?.topologySystemName?.trim() ?? "";
      if (!userFacingLabel) {
        userFacingLabel = shape?.userFacingTopologyLabel?.trim() ?? "";
      }
    }

    if (!sysName || !isValidTopologySystemName(sysName)) continue;

    const derivedRouteKey = topologySystemNameToUiBuilderKey(sysName);
    const stored = getStoredScreenLabel(item.manifestId);
    const label = stored ||
      resolveVisibleTopologyName(userFacingLabel || null, sysName);
    resolved.push({
      manifestId: item.manifestId,
      topologySystemName: sysName,
      label,
      derivedRouteKey,
      status: item.status,
    });
  }
  return resolved;
}

/** Find the Contents manifest for the committed UI Builder routeKey (topology.name). */
export async function resolveManifestRouteOptionForRouteKey(
  routeKey: string,
): Promise<ManifestRouteOption | null> {
  const rk = routeKey.trim();
  if (!rk) return null;
  const options = await loadManifestRouteOptions();
  return options.find((o) => o.derivedRouteKey === rk) ?? null;
}
