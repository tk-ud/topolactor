-- =============================================================================
-- physical_details_inline_editor_md_generator_preset_seed.sql
-- Canonical: UIBuilder preset ecosystem seed data — physical_details_inline_editor_md_generator.v1 preset registration.
--
-- SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml
--       docs/design/mock-preset-intake-compiler-ssot.yaml
--
-- BOOTSTRAP POLICY: part of the canonical fresh bootstrap sequence in db/init.sql.
-- Applied via docker-entrypoint-initdb.d/00-init.sql on fresh compose volume.
-- IDEMPOTENT: all statements use ON CONFLICT DO UPDATE or ON CONFLICT DO NOTHING.
-- PRECONDITION: topology.mock_preset_* tables must exist (db/mock_preset_tables.sql).
-- =============================================================================

-- -- physical_details_inline_editor_md_generator_preset_seed --

WITH upserted_preset AS (
    INSERT INTO topology.mock_preset_registry (
        preset_key,
        preset_label,
        source_kind,
        source_hash,
        source_snapshot_json,
        visual_tree_json,
        status
    )
    VALUES (
        'physical_details_inline_editor_md_generator.v1',
        'Physical Details / Inline Editor / Markdown Generator preset seed',
        'ui_builder_canvas',
        'physical_details_inline_editor_md_generator.v1.seed.2026-06-12',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "physical_details_inline_editor_md_generator.v1",
          "bundle": "ui-builder-preset-ecosystem",
          "surface": "physical_details_inline_editor_md_generator",
          "role": "physical record detail view, inline edit, and Markdown saved view generation surface",
          "activeTopology": false,
          "componentImplementation": "none; existing component catalog composition only",
          "runtimeMutationAuthority": "none; contents topology content_bundle:* operation refs only",
          "boundary": "load into selected route package tmp canvas draft; human edit; preview; validate; apply",
          "contentsTopologyBinding": {
            "getDetail": "content_bundle:get_entity",
            "update": "content_bundle:update_entity_draft",
            "validate": "content_bundle:validate_draft",
            "promote": "content_bundle:promote_draft"
          },
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts",
          "knownGaps": []
        }
        $$::jsonb,
        $$
        {
          "nodes": [
            { "nodeId": "details_shell", "componentKey": "section.alias", "componentKind": "disclosure_structure/section", "parentNodeId": null },
            { "nodeId": "details_back_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "details_shell" },
            { "nodeId": "details_pdf_export_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "details_shell" },
            { "nodeId": "details_tabs", "componentKey": "tabs.template", "componentKind": "disclosure/tabs", "parentNodeId": "details_shell" },
            { "nodeId": "details_field_label_1", "componentKey": "inline_editable_field.primitive", "componentKind": "inline_edit/inline_editable_field", "parentNodeId": "details_tabs" },
            { "nodeId": "details_status_select", "componentKey": "select.template", "componentKind": "form_input/select", "parentNodeId": "details_tabs" },
            { "nodeId": "details_save_button", "componentKey": "confirmed_update_button.primitive", "componentKind": "inline_edit/confirmed_update_button", "parentNodeId": "details_tabs" },
            { "nodeId": "details_history_list", "componentKey": "audit_diff_drawer.primitive", "componentKind": "inline_edit/audit_diff_drawer", "parentNodeId": "details_tabs" },
            { "nodeId": "details_history_drawer_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "details_tabs" },
            { "nodeId": "details_full_history_drawer", "componentKey": "row_detail_drawer.primitive", "componentKind": "table_op/row_detail_drawer", "parentNodeId": "details_shell" },
            { "nodeId": "details_debug_json", "componentKey": "json_viewer.template", "componentKind": "data_display/json", "parentNodeId": "details_shell" }
          ]
        }
        $$::jsonb,
        'active'
    )
    ON CONFLICT (preset_key) DO UPDATE SET
        preset_label = EXCLUDED.preset_label,
        source_kind = EXCLUDED.source_kind,
        source_hash = EXCLUDED.source_hash,
        source_snapshot_json = EXCLUDED.source_snapshot_json,
        visual_tree_json = EXCLUDED.visual_tree_json,
        status = EXCLUDED.status,
        updated_at = now()
    RETURNING preset_id
), preset AS (
    SELECT preset_id FROM upserted_preset
), cleanup_compile AS (
    DELETE FROM topology.mock_preset_compile_snapshot
    WHERE preset_id = (SELECT preset_id FROM preset)
), cleanup_wiring AS (
    DELETE FROM topology.mock_preset_wiring_candidate
    WHERE preset_id = (SELECT preset_id FROM preset)
), cleanup_mapping AS (
    DELETE FROM topology.mock_preset_object_mapping
    WHERE preset_id = (SELECT preset_id FROM preset)
), inserted_mappings AS (
    INSERT INTO topology.mock_preset_object_mapping (
        preset_id, source_object_id, node_id, node_kind, component_key, component_kind,
        parent_source_object_id, slot_key, order_index, bbox_json, text_json,
        style_candidate_json, mapping_status
    )
    VALUES
        ((SELECT preset_id FROM preset), 'details_shell', 'details_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0,
            '{"x":0,"y":0,"width":1024,"height":768}'::jsonb,
            '{"title":"Record Details","description":"Detail view, inline edit, and field history."}'::jsonb,
            '{"layoutIntent":"root_shell"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_back_button', 'details_back_button', 'catalog_component', 'button.primitive', 'action/button', 'details_shell', 'header', 1,
            '{"x":24,"y":16,"width":96,"height":40}'::jsonb,
            '{"label":"← Back"}'::jsonb,
            '{"layoutIntent":"back_nav"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_pdf_export_button', 'details_pdf_export_button', 'catalog_component', 'button.primitive', 'action/button', 'details_shell', 'header', 2,
            '{"x":896,"y":16,"width":128,"height":40}'::jsonb,
            '{"label":"Export PDF","variant":"secondary"}'::jsonb,
            '{"layoutIntent":"export_action"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_tabs', 'details_tabs', 'catalog_component', 'tabs.template', 'disclosure/tabs', 'details_shell', 'content', 3,
            '{"x":24,"y":72,"width":976,"height":640}'::jsonb,
            '{"tabs":[{"key":"details","label":"Details"},{"key":"history","label":"Field History"}]}'::jsonb,
            '{"layoutIntent":"main_tabs"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_field_label_1', 'details_field_label_1', 'catalog_component', 'inline_editable_field.primitive', 'inline_edit/inline_editable_field', 'details_tabs', 'details', 4,
            '{"x":48,"y":136,"width":448,"height":64}'::jsonb,
            '{"label":"Field 1","placeholder":"Enter value..."}'::jsonb,
            '{"layoutIntent":"editable_field"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_status_select', 'details_status_select', 'catalog_component', 'select.template', 'form_input/select', 'details_tabs', 'details', 5,
            '{"x":512,"y":136,"width":240,"height":48}'::jsonb,
            '{"label":"Status"}'::jsonb,
            '{"layoutIntent":"status_select"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_save_button', 'details_save_button', 'catalog_component', 'confirmed_update_button.primitive', 'inline_edit/confirmed_update_button', 'details_tabs', 'details', 6,
            '{"x":864,"y":680,"width":136,"height":40}'::jsonb,
            '{"label":"Save changes"}'::jsonb,
            '{"layoutIntent":"save_action"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_history_list', 'details_history_list', 'catalog_component', 'audit_diff_drawer.primitive', 'inline_edit/audit_diff_drawer', 'details_tabs', 'history', 7,
            '{"x":48,"y":136,"width":880,"height":440}'::jsonb,
            '{"title":"Field history","emptyText":"No history yet."}'::jsonb,
            '{"layoutIntent":"history_list"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_history_drawer_button', 'details_history_drawer_button', 'catalog_component', 'button.primitive', 'action/button', 'details_tabs', 'history', 8,
            '{"x":48,"y":600,"width":160,"height":40}'::jsonb,
            '{"label":"Full history","variant":"secondary"}'::jsonb,
            '{"layoutIntent":"full_history_trigger"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_full_history_drawer', 'details_full_history_drawer', 'catalog_component', 'row_detail_drawer.primitive', 'table_op/row_detail_drawer', 'details_shell', 'drawers', 9,
            '{"x":680,"y":72,"width":320,"height":680}'::jsonb,
            '{"title":"Full field history"}'::jsonb,
            '{"layoutIntent":"full_history_drawer"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'details_debug_json', 'details_debug_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'details_shell', 'debug', 10,
            '{"x":24,"y":720,"width":640,"height":40}'::jsonb,
            '{"title":"Emission debug"}'::jsonb,
            '{"layoutIntent":"debug_projection"}'::jsonb, 'mapped')
    RETURNING mapping_id
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id, source_object_id, node_id, capability_tag,
        wiring_kind, target_surface, target_ref, binding_json, status
    )
    VALUES
        -- back button → route navigation (author selects CRUD surface route)
        ((SELECT preset_id FROM preset), 'details_back_button', 'details_back_button', 'requires_event_binding', 'navigation', 'route', '',
            $${"event":"click","wiringKind":"navigation","targetSurface":"route","targetRef":"","note":"Author sets targetRef to route:<crudRouteKey> after preset load"}$$::jsonb, 'pending'),
        -- save button → content_bundle:update_entity_draft + validate + promote
        ((SELECT preset_id FROM preset), 'details_save_button', 'details_save_button', 'requires_event_binding', 'update', 'content_bundle', 'content_bundle:update_entity_draft',
            $${"event":"click","wiringKind":"update","targetSurface":"content_bundle","targetRef":"content_bundle:update_entity_draft","payloadFrom":{"entityId":"event.record.id"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Followed by validate_draft then promote_draft in the mutation sequence"}$$::jsonb, 'pending')
    RETURNING wiring_candidate_id
)
INSERT INTO topology.mock_preset_compile_snapshot (
    preset_id, compiler_version, layout_patch_json,
    package_membership_candidate_json, wiring_candidate_json,
    style_candidate_json, unresolved_json
)
VALUES (
    (SELECT preset_id FROM preset),
    'physical-details-seed.v1',
    $$
    {
      "nodes": [
        {"nodeId":"details_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","isDraftOnly":false,"slotKey":"root","orderIndex":0,"parentNodeId":null,"x":0,"y":0,"width":1024,"height":768,"propsJson":"{\"title\":\"Record Details\",\"description\":\"Detail view, inline edit, and field history. Wire each action to the contents topology assigned operation refs.\"}"},
        {"nodeId":"details_back_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"header","orderIndex":1,"parentNodeId":"details_shell","x":24,"y":16,"width":96,"height":40,"propsJson":"{\"label\":\"← Back\"}"},
        {"nodeId":"details_pdf_export_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"header","orderIndex":2,"parentNodeId":"details_shell","x":896,"y":16,"width":128,"height":40,"propsJson":"{\"label\":\"Export PDF\",\"variant\":\"secondary\"}"},
        {"nodeId":"details_tabs","nodeKind":"catalog_component","componentKey":"tabs.template","componentKind":"disclosure/tabs","isDraftOnly":false,"slotKey":"content","orderIndex":3,"parentNodeId":"details_shell","x":24,"y":72,"width":976,"height":640,"propsJson":"{\"tabs\":[{\"key\":\"details\",\"label\":\"Details\"},{\"key\":\"history\",\"label\":\"Field History\"}]}"},
        {"nodeId":"details_field_label_1","nodeKind":"catalog_component","componentKey":"inline_editable_field.primitive","componentKind":"inline_edit/inline_editable_field","isDraftOnly":false,"slotKey":"details","orderIndex":4,"parentNodeId":"details_tabs","x":48,"y":136,"width":448,"height":64,"propsJson":"{\"label\":\"Field 1\",\"placeholder\":\"Enter value...\"}"},
        {"nodeId":"details_status_select","nodeKind":"catalog_component","componentKey":"select.template","componentKind":"form_input/select","isDraftOnly":false,"slotKey":"details","orderIndex":5,"parentNodeId":"details_tabs","x":512,"y":136,"width":240,"height":48,"propsJson":"{\"label\":\"Status\"}"},
        {"nodeId":"details_save_button","nodeKind":"catalog_component","componentKey":"confirmed_update_button.primitive","componentKind":"inline_edit/confirmed_update_button","isDraftOnly":false,"slotKey":"details","orderIndex":6,"parentNodeId":"details_tabs","x":864,"y":680,"width":136,"height":40,"propsJson":"{\"label\":\"Save changes\"}"},
        {"nodeId":"details_history_list","nodeKind":"catalog_component","componentKey":"audit_diff_drawer.primitive","componentKind":"inline_edit/audit_diff_drawer","isDraftOnly":false,"slotKey":"history","orderIndex":7,"parentNodeId":"details_tabs","x":48,"y":136,"width":880,"height":440,"propsJson":"{\"title\":\"Field history\",\"emptyText\":\"No history yet.\"}","propBindings":{"entries":{"source":"emission.data.history"}}},
        {"nodeId":"details_history_drawer_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"history","orderIndex":8,"parentNodeId":"details_tabs","x":48,"y":600,"width":160,"height":40,"propsJson":"{\"label\":\"Full history\",\"variant\":\"secondary\"}"},
        {"nodeId":"details_full_history_drawer","nodeKind":"catalog_component","componentKey":"row_detail_drawer.primitive","componentKind":"table_op/row_detail_drawer","isDraftOnly":false,"slotKey":"drawers","orderIndex":9,"parentNodeId":"details_shell","x":680,"y":72,"width":320,"height":680,"propsJson":"{\"title\":\"Full field history\"}"},
        {"nodeId":"details_debug_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","isDraftOnly":false,"slotKey":"debug","orderIndex":10,"parentNodeId":"details_shell","x":24,"y":720,"width":640,"height":40,"propsJson":"{\"title\":\"Emission debug\"}","propBindings":{"data":{"source":"emission.data"}}}
      ],
      "layoutClassRefs": []
    }
    $$::jsonb,
    $${"activeTopologyWrite":false,"bindTarget":"selected_route_package_tmp_canvas_draft","requiresHumanAdjustmentBeforeApply":true}$$::jsonb,
    $$
    [
      {"nodeId":"details_back_button","sourceObjectId":"details_back_button","capabilityTag":"requires_event_binding","wiringKind":"navigation","targetSurface":"route","targetRef":"","status":"pending","binding":{"event":"click","note":"Author sets targetRef to route:<crudRouteKey> after preset load"}},
      {"nodeId":"details_save_button","sourceObjectId":"details_save_button","capabilityTag":"requires_event_binding","wiringKind":"update","targetSurface":"content_bundle","targetRef":"content_bundle:update_entity_draft","status":"pending","binding":{"event":"click","payloadFrom":{"entityId":"event.record.id"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}}
    ]
    $$::jsonb,
    $$[{"nodeId":"details_shell","styleIntent":"section_shell"},{"nodeId":"details_tabs","styleIntent":"main_tabs"}]$$::jsonb,
    $$
    [
      {"nodeId":"details_back_button","reason":"targetRef is empty — author must set route:<crudRouteKey> after preset load","knownGapRef":"back_button_route_navigation_target"}
    ]
    $$::jsonb
);

