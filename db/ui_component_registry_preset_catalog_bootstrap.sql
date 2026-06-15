-- =============================================================================
-- ui_component_registry_preset_catalog_bootstrap.sql
-- Canonical: UIBuilder preset ecosystem seed data — ui_component_registry and components_bucket bootstrap rows.
--
-- SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml
--       docs/design/mock-preset-intake-compiler-ssot.yaml
--
-- BOOTSTRAP POLICY: part of the canonical fresh bootstrap sequence in db/init.sql.
-- Applied via docker-entrypoint-initdb.d/00-init.sql on fresh compose volume.
-- IDEMPOTENT: all statements use ON CONFLICT DO UPDATE or ON CONFLICT DO NOTHING.
-- PRECONDITION: topology.mock_preset_* tables must exist (db/mock_preset_tables.sql).
-- =============================================================================

-- -- ui_component_registry_preset_catalog_bootstrap --

-- Runtime alias catalog entries (preset shells use section/search_input/panel.alias)
INSERT INTO topology.components_bucket (component_key, source_path, component_kind, status, metadata_json)
VALUES
    ('section.alias', 'frontend/runtime/runtimeComponentFactory.ts', 'disclosure_structure/section', 'bucketed', '{"classification":{"runtimeConnected":true,"registrationRequired":false,"lifecycleStatus":"alias_maintained","componentFamily":"alias","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_children","accepts_layout","accepts_design"]}}'::jsonb),
    ('search_input.alias', 'frontend/runtime/runtimeComponentFactory.ts', 'form_input/search_input', 'bucketed', '{"classification":{"runtimeConnected":true,"registrationRequired":false,"lifecycleStatus":"alias_maintained","componentFamily":"alias","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('panel.alias', 'frontend/runtime/runtimeComponentFactory.ts', 'disclosure_structure/panel', 'bucketed', '{"classification":{"runtimeConnected":true,"registrationRequired":false,"lifecycleStatus":"alias_maintained","componentFamily":"alias","semanticRole":"layout_shell","visualRole":"panel","capabilityTags":["accepts_children","accepts_layout","accepts_design"]}}'::jsonb),
    ('textarea.alias', 'frontend/runtime/runtimeComponentFactory.ts', 'form_input/textarea', 'bucketed', '{"classification":{"runtimeConnected":true,"registrationRequired":false,"lifecycleStatus":"alias_maintained","componentFamily":"alias","semanticRole":"input","visualRole":"field","capabilityTags":["controlled_value","emits_event","accepts_design"]}}'::jsonb),
    ('data_grid.alias', 'frontend/runtime/runtimeComponentFactory.ts', 'data_display/data_grid', 'bucketed', '{"classification":{"runtimeConnected":true,"registrationRequired":false,"lifecycleStatus":"alias_maintained","componentFamily":"alias","semanticRole":"data_viewer","visualRole":"table","capabilityTags":["selectable","accepts_design"]}}'::jsonb),
    ('list.alias', 'frontend/runtime/runtimeComponentFactory.ts', 'data_display/list', 'bucketed', '{"classification":{"runtimeConnected":true,"registrationRequired":false,"lifecycleStatus":"alias_maintained","componentFamily":"alias","semanticRole":"data_viewer","visualRole":"panel","capabilityTags":["selectable","accepts_design"]}}'::jsonb)
ON CONFLICT (component_key, source_path) DO UPDATE
    SET component_kind = EXCLUDED.component_kind,
        status = EXCLUDED.status,
        metadata_json = EXCLUDED.metadata_json,
        updated_at = now();

-- Preset ecosystem component keys → active registry rows (componentId enrichment source)
INSERT INTO topology.ui_component_registry (component_id, component_key, component_kind, source_path, status)
VALUES
    ('00000000-0000-0000-0001-000000000001', 'section.alias', 'disclosure_structure/section', 'frontend/runtime/runtimeComponentFactory.ts', 'active'),
    ('00000000-0000-0000-0001-000000000002', 'search_input.alias', 'form_input/search_input', 'frontend/runtime/runtimeComponentFactory.ts', 'active'),
    ('00000000-0000-0000-0001-000000000003', 'panel.alias', 'disclosure_structure/panel', 'frontend/runtime/runtimeComponentFactory.ts', 'active'),
    ('00000000-0000-0000-0001-000000000004', 'textarea.alias', 'form_input/textarea', 'frontend/runtime/runtimeComponentFactory.ts', 'active'),
    ('00000000-0000-0000-0001-000000000005', 'data_grid.alias', 'data_display/data_grid', 'frontend/runtime/runtimeComponentFactory.ts', 'active'),
    ('00000000-0000-0000-0001-000000000006', 'list.alias', 'data_display/list', 'frontend/runtime/runtimeComponentFactory.ts', 'active'),
    ('00000000-0000-0000-0001-000000000010', 'button.primitive', 'action/button', 'frontend/components/Button.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000011', 'input.primitive', 'form_input/input', 'frontend/components/Input.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000012', 'select.template', 'form_input/select', 'frontend/components/Select.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000013', 'form_field.template', 'form_input/form_field', 'frontend/components/FormField.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000014', 'card_list.primitive', 'display/card_list', 'frontend/components/CardList.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000015', 'modal.template', 'disclosure/modal', 'frontend/components/Modal.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000016', 'tabs.template', 'disclosure/tabs', 'frontend/components/Tabs.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000017', 'json_viewer.template', 'data_display/json', 'frontend/components/JsonViewer.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000018', 'apply_confirm_dialog.primitive', 'safety_guard/apply_confirm_dialog', 'frontend/components/ApplyConfirmDialog.tsx', 'active'),
    ('00000000-0000-0000-0001-000000000019', 'row_detail_drawer.primitive', 'table_op/row_detail_drawer', 'frontend/components/RowDetailDrawer.tsx', 'active'),
    ('00000000-0000-0000-0001-00000000001a', 'inline_editable_field.primitive', 'inline_edit/inline_editable_field', 'frontend/components/InlineEditableField.tsx', 'active'),
    ('00000000-0000-0000-0001-00000000001b', 'confirmed_update_button.primitive', 'inline_edit/confirmed_update_button', 'frontend/components/ConfirmedUpdateButton.tsx', 'active'),
    ('00000000-0000-0000-0001-00000000001c', 'audit_diff_drawer.primitive', 'inline_edit/audit_diff_drawer', 'frontend/components/AuditDiffDrawer.tsx', 'active'),
    ('00000000-0000-0000-0001-00000000001d', 'aggregation_preview_table.primitive', 'calc_topology/aggregation_preview_table', 'frontend/components/AggregationPreviewTable.tsx', 'active'),
    ('00000000-0000-0000-0001-00000000001e', 'hub_statistics_panel.primitive', 'calc_topology/hub_statistics_panel', 'frontend/components/HubStatisticsPanel.tsx', 'active')
ON CONFLICT (component_key) DO UPDATE
    SET component_kind = EXCLUDED.component_kind,
        source_path    = EXCLUDED.source_path,
        status         = EXCLUDED.status,
        updated_at     = now();

