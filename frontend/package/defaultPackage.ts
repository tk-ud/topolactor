export type PackageDef = {
  packageId: string;
  name: string;
  componentIds: string[];
  schemaDef?: Record<string, unknown>;
};

/**
 * The default package is the fallback node in the package_resolve step of the
 * canonical pipeline.  It references the skeleton default-view component and
 * carries no schemaDef override, deferring schema resolution to the
 * schema_resolve step.
 *
 * Real packages will be registered per-hub and will reference their own
 * component and schema definitions.
 */
export const defaultPackage: PackageDef = {
  packageId: "default-package",
  name: "default_package",
  componentIds: ["default-view"],
};
