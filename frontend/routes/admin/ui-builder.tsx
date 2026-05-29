import { JSX } from "preact";
import AdminAuthGate from "../../islands/AdminAuthGate.tsx";
import UiBuilderAdmin from "../../islands/UiBuilderAdmin.tsx";

/**
 * /admin/ui-builder — UI component system and layout builder.
 * Interactive sections live in the UiBuilderAdmin island (Fresh requires hooks there).
 */
export default function UiBuilderAdminRoute(): JSX.Element {
  return (
    <AdminAuthGate>
      <UiBuilderAdmin />
    </AdminAuthGate>
  );
}