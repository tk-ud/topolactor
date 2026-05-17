export type StructureMapEntry = {
  attractorKey: string;
  packageId: string;
  schemaId: string;
  componentIds: string[];
  statePolicies?: Record<string, unknown>;
};

/**
 * The StructureMap is the central routing table of the topolactor frontend.
 * Each key is an attractorKey (`target:layer:action`) and its value is the
 * resolved set of pipeline nodes (package, schema, components) that should
 * handle that operation.
 *
 * The structure_map_resolve step in the canonical flow consults this table
 * after the attractor_resolve step has produced an attractorKey from the
 * OperationVector.
 */
export type StructureMap = Record<string, StructureMapEntry>;

/**
 * The default structure map shipped with the skeleton.
 * Real structure maps will be populated from backend topology data at
 * application startup (stored_topology_data → structure_map_resolve).
 */
export const defaultStructureMap: StructureMap = {
  "default:entity:search": {
    attractorKey: "default:entity:search",
    packageId: "00000000-0000-0000-0000-000000000001",
    schemaId: "00000000-0000-0000-0000-000000000002",
    componentIds: ["00000000-0000-0000-0000-000000000003"],
  },
  "demo:hub:overview": {
    attractorKey: "demo:hub:overview",
    packageId: "00000000-0000-0000-0000-000000000013",
    schemaId:  "00000000-0000-0000-0000-000000000012",
    componentIds: ["00000000-0000-0000-0000-000000000014"],
  },
  "demo:entity:list": {
    attractorKey: "demo:entity:list",
    packageId: "00000000-0000-0000-0000-000000000013",
    schemaId:  "00000000-0000-0000-0000-000000000012",
    componentIds: ["00000000-0000-0000-0000-000000000015"],
  },
  "demo:recommendation:view": {
    attractorKey: "demo:recommendation:view",
    packageId: "00000000-0000-0000-0000-000000000013",
    schemaId:  "00000000-0000-0000-0000-000000000012",
    componentIds: ["00000000-0000-0000-0000-000000000016"],
  },
};

/**
 * Look up a StructureMapEntry by attractorKey.
 * Returns null when no entry exists for that key.
 */
export function lookupStructureMap(
  map: StructureMap,
  attractorKey: string,
): StructureMapEntry | null {
  return map[attractorKey] ?? null;
}
