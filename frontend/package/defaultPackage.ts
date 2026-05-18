export type PackageDef = {
  packageId: string;
  name: string;
  componentIds: string[];
  schemaDef?: Record<string, unknown>;
};

/**
 * Static test fixture package.
 * Canonical runtime package resolution is backend/DB-backed.
 *
 * Real packages will be registered per-hub and will reference their own
 * component and schema definitions.
 */
export const defaultPackage: PackageDef = {
  packageId: "default-package",
  name: "default_package",
  componentIds: ["default-view"],
};
