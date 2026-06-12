-- =============================================================================
-- physical_search_crud_aggregate_preset_seed.sql
-- Seed: topology.mock_preset_* registration for the UIBuilder preset ecosystem.
--
-- SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml
--       physical_search_crud_aggregate_preset
-- Registry table authority: db/migrations/mock_preset_registry_tables.sql
--
-- Registers `physical_search_crud_aggregate.v1` as a reusable search / CRUD /
-- aggregation canvas draft built from existing component catalog entries only.
-- It is NOT a new component implementation and is NOT active topology.
-- All mutation flows use contents topology assigned content_bundle:* operation refs.
-- UIBuilder loads this into a selected route package tmp canvas draft;
-- human edit → preview → validate → apply boundary remains mandatory.
-- =============================================================================

BEGIN;

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
        'physical_search_crud_aggregate.v1',
        'Physical Search / CRUD / Aggregate preset seed',
        'ui_builder_canvas',
        'physical_search_crud_aggregate.v1.seed.2026-06-12',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "physical_search_crud_aggregate.v1",
          "bundle": "ui-builder-preset-ecosystem",
          "surface": "physical_search_crud_aggregate",
          "role": "physical table / jsonb record search, CRUD, aggregation surface",
          "activeTopology": false,
          "componentImplementation": "none; existing component catalog composition only",
          "runtimeMutationAuthority": "none; contents topology content_bundle:* operation refs only",
          "boundary": "load into selected route package tmp canvas draft; human edit; preview; validate; apply",
          "contentsTopologyBinding": {
            "search": "content_bundle:search",
            "create": "content_bundle:create_entity_draft",
            "update": "content_bundle:update_entity_draft",
            "validate": "content_bundle:validate_draft",
            "promote": "content_bundle:promote_draft",
            "get": "content_bundle:get_entity",
            "listStates": "content_bundle:list_states"
          },
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts"
        }
        $$::jsonb,
        $$
        {
          "nodes": [
            { "nodeId": "crud_shell", "componentKey": "section.alias", "componentKind": "disclosure_structure/section", "parentNodeId": null },
            { "nodeId": "crud_search_input", "componentKey": "search_input.alias", "componentKind": "form_input/search_input", "parentNodeId": "crud_shell" },
            { "nodeId": "crud_search_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "crud_shell" },
            { "nodeId": "crud_status_filter", "componentKey": "select.template", "componentKind": "form_input/select", "parentNodeId": "crud_shell" },
            { "nodeId": "crud_add_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "crud_shell" },
            { "nodeId": "crud_results_panel", "componentKey": "panel.alias", "componentKind": "disclosure_structure/panel", "parentNodeId": "crud_shell" },
            { "nodeId": "crud_result_list", "componentKey": "card_list.primitive", "componentKind": "display/card_list", "parentNodeId": "crud_results_panel" },
            { "nodeId": "crud_create_modal", "componentKey": "modal.template", "componentKind": "disclosure/modal", "parentNodeId": "crud_shell" },
            { "nodeId": "crud_form_name_field", "componentKey": "form_field.template", "componentKind": "form_input/form_field", "parentNodeId": "crud_create_modal" },
            { "nodeId": "crud_submit_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "crud_create_modal" },
            { "nodeId": "crud_cancel_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "crud_create_modal" },
            { "nodeId": "crud_confirm_dialog", "componentKey": "apply_confirm_dialog.primitive", "componentKind": "safety_guard/apply_confirm_dialog", "parentNodeId": "crud_shell" },
            { "nodeId": "crud_detail_drawer", "componentKey": "row_detail_drawer.primitive", "componentKind": "table_op/row_detail_drawer", "parentNodeId": "crud_shell" },
            { "nodeId": "crud_debug_json", "componentKey": "json_viewer.template", "componentKind": "data_display/json", "parentNodeId": "crud_shell" }
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
        ((SELECT preset_id FROM preset), 'crud_shell', 'crud_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0,
            '{"x":0,"y":0,"width":1024,"height":768}'::jsonb,
            '{"title":"CRUD Surface","description":"Search, create, update, remove, and detail view."}'::jsonb,
            '{"layoutIntent":"root_shell"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_search_input', 'crud_search_input', 'catalog_component', 'search_input.alias', 'form_input/search_input', 'crud_shell', 'toolbar', 1,
            '{"x":24,"y":64,"width":480,"height":48}'::jsonb,
            '{"label":"Search","placeholder":"Search records..."}'::jsonb,
            '{"layoutIntent":"search_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_search_button', 'crud_search_button', 'catalog_component', 'button.primitive', 'action/button', 'crud_shell', 'toolbar', 2,
            '{"x":520,"y":64,"width":120,"height":48}'::jsonb,
            '{"label":"Search"}'::jsonb,
            '{"layoutIntent":"search_trigger"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_status_filter', 'crud_status_filter', 'catalog_component', 'select.template', 'form_input/select', 'crud_shell', 'toolbar', 3,
            '{"x":656,"y":64,"width":160,"height":48}'::jsonb,
            '{"label":"Status","placeholder":"All statuses"}'::jsonb,
            '{"layoutIntent":"status_filter"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_add_button', 'crud_add_button', 'catalog_component', 'button.primitive', 'action/button', 'crud_shell', 'toolbar', 4,
            '{"x":832,"y":64,"width":120,"height":48}'::jsonb,
            '{"label":"Add","variant":"primary"}'::jsonb,
            '{"layoutIntent":"add_trigger"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_results_panel', 'crud_results_panel', 'catalog_component', 'panel.alias', 'disclosure_structure/panel', 'crud_shell', 'results', 5,
            '{"x":24,"y":136,"width":976,"height":400}'::jsonb,
            '{"title":"Results"}'::jsonb,
            '{"layoutIntent":"results_panel"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_result_list', 'crud_result_list', 'catalog_component', 'card_list.primitive', 'display/card_list', 'crud_results_panel', 'content', 6,
            '{"x":48,"y":184,"width":928,"height":320}'::jsonb,
            '{"emptyText":"No records found."}'::jsonb,
            '{"layoutIntent":"result_cards"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_create_modal', 'crud_create_modal', 'catalog_component', 'modal.template', 'disclosure/modal', 'crud_shell', 'modals', 7,
            '{"x":256,"y":200,"width":512,"height":400}'::jsonb,
            '{"title":"Create record"}'::jsonb,
            '{"layoutIntent":"create_modal"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_form_name_field', 'crud_form_name_field', 'catalog_component', 'form_field.template', 'form_input/form_field', 'crud_create_modal', 'fields', 8,
            '{"x":280,"y":280,"width":464,"height":64}'::jsonb,
            '{"label":"Name","required":true}'::jsonb,
            '{"layoutIntent":"form_field"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_submit_button', 'crud_submit_button', 'catalog_component', 'button.primitive', 'action/button', 'crud_create_modal', 'actions', 9,
            '{"x":424,"y":536,"width":120,"height":48}'::jsonb,
            '{"label":"Save","variant":"primary"}'::jsonb,
            '{"layoutIntent":"submit_action"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_cancel_button', 'crud_cancel_button', 'catalog_component', 'button.primitive', 'action/button', 'crud_create_modal', 'actions', 10,
            '{"x":296,"y":536,"width":120,"height":48}'::jsonb,
            '{"label":"Cancel"}'::jsonb,
            '{"layoutIntent":"cancel_action"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_confirm_dialog', 'crud_confirm_dialog', 'catalog_component', 'apply_confirm_dialog.primitive', 'safety_guard/apply_confirm_dialog', 'crud_shell', 'dialogs', 11,
            '{"x":288,"y":280,"width":448,"height":240}'::jsonb,
            '{"title":"Confirm action","message":"Are you sure?"}'::jsonb,
            '{"layoutIntent":"confirm_dialog"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_detail_drawer', 'crud_detail_drawer', 'catalog_component', 'row_detail_drawer.primitive', 'table_op/row_detail_drawer', 'crud_shell', 'drawers', 12,
            '{"x":680,"y":136,"width":320,"height":608}'::jsonb,
            '{"title":"Record detail"}'::jsonb,
            '{"layoutIntent":"detail_drawer"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'crud_debug_json', 'crud_debug_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'crud_shell', 'debug', 13,
            '{"x":24,"y":560,"width":640,"height":192}'::jsonb,
            '{"title":"Emission debug"}'::jsonb,
            '{"layoutIntent":"debug_projection"}'::jsonb, 'mapped')
    RETURNING mapping_id
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id, source_object_id, node_id, capability_tag,
        wiring_kind, target_surface, target_ref, binding_json, status
    )
    VALUES
        -- search button → content_bundle:search with keyword from search input
        ((SELECT preset_id FROM preset), 'crud_search_button', 'crud_search_button', 'requires_event_binding', 'search', 'content_bundle', 'content_bundle:search',
            $${"event":"click","wiringKind":"search","targetSurface":"content_bundle","targetRef":"content_bundle:search","payloadFrom":{"keyword":"node:crud_search_input.value"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}$$::jsonb, 'pending'),
        -- result list item click → content_bundle:get_entity with entityId from event.item.id
        ((SELECT preset_id FROM preset), 'crud_result_list', 'crud_result_list', 'requires_event_binding', 'get', 'content_bundle', 'content_bundle:get_entity',
            $${"event":"item.click","wiringKind":"get","targetSurface":"content_bundle","targetRef":"content_bundle:get_entity","payloadFrom":{"entityId":"event.item.id"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}$$::jsonb, 'pending'),
        -- submit button → content_bundle:create_entity_draft
        ((SELECT preset_id FROM preset), 'crud_submit_button', 'crud_submit_button', 'requires_event_binding', 'create', 'content_bundle', 'content_bundle:create_entity_draft',
            $${"event":"click","wiringKind":"create","targetSurface":"content_bundle","targetRef":"content_bundle:create_entity_draft","payloadFrom":{},"note":"Author maps form field values to payload after preset load"}$$::jsonb, 'pending')
    RETURNING wiring_candidate_id
)
INSERT INTO topology.mock_preset_compile_snapshot (
    preset_id, compiler_version, layout_patch_json,
    package_membership_candidate_json, wiring_candidate_json,
    style_candidate_json, unresolved_json
)
VALUES (
    (SELECT preset_id FROM preset),
    'physical-crud-aggregate-seed.v1',
    $$
    {
      "nodes": [
        {"nodeId":"crud_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","isDraftOnly":false,"slotKey":"root","orderIndex":0,"parentNodeId":null,"x":0,"y":0,"width":1024,"height":768,"propsJson":"{\"title\":\"CRUD Surface\",\"description\":\"Search, create, update, remove, and detail view. Wire each action to the contents topology assigned operation refs.\"}"},
        {"nodeId":"crud_search_input","nodeKind":"catalog_component","componentKey":"search_input.alias","componentKind":"form_input/search_input","isDraftOnly":false,"slotKey":"toolbar","orderIndex":1,"parentNodeId":"crud_shell","x":24,"y":64,"width":480,"height":48,"propsJson":"{\"label\":\"Search\",\"placeholder\":\"Search records...\",\"value\":\"\"}"},
        {"nodeId":"crud_search_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"toolbar","orderIndex":2,"parentNodeId":"crud_shell","x":520,"y":64,"width":120,"height":48,"propsJson":"{\"label\":\"Search\",\"variant\":\"default\"}"},
        {"nodeId":"crud_status_filter","nodeKind":"catalog_component","componentKey":"select.template","componentKind":"form_input/select","isDraftOnly":false,"slotKey":"toolbar","orderIndex":3,"parentNodeId":"crud_shell","x":656,"y":64,"width":160,"height":48,"propsJson":"{\"label\":\"Status\",\"placeholder\":\"All statuses\"}"},
        {"nodeId":"crud_add_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"toolbar","orderIndex":4,"parentNodeId":"crud_shell","x":832,"y":64,"width":120,"height":48,"propsJson":"{\"label\":\"Add\",\"variant\":\"primary\"}"},
        {"nodeId":"crud_results_panel","nodeKind":"catalog_component","componentKey":"panel.alias","componentKind":"disclosure_structure/panel","isDraftOnly":false,"slotKey":"results","orderIndex":5,"parentNodeId":"crud_shell","x":24,"y":136,"width":976,"height":400,"propsJson":"{\"title\":\"Results\"}"},
        {"nodeId":"crud_result_list","nodeKind":"catalog_component","componentKey":"card_list.primitive","componentKind":"display/card_list","isDraftOnly":false,"slotKey":"content","orderIndex":6,"parentNodeId":"crud_results_panel","x":48,"y":184,"width":928,"height":320,"propsJson":"{\"emptyText\":\"No records found.\"}","propBindings":{"items":{"source":"emission.data.rows"}}},
        {"nodeId":"crud_create_modal","nodeKind":"catalog_component","componentKey":"modal.template","componentKind":"disclosure/modal","isDraftOnly":false,"slotKey":"modals","orderIndex":7,"parentNodeId":"crud_shell","x":256,"y":200,"width":512,"height":400,"propsJson":"{\"title\":\"Create record\"}"},
        {"nodeId":"crud_form_name_field","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":8,"parentNodeId":"crud_create_modal","x":280,"y":280,"width":464,"height":64,"propsJson":"{\"label\":\"Name\",\"required\":true}"},
        {"nodeId":"crud_submit_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":9,"parentNodeId":"crud_create_modal","x":424,"y":536,"width":120,"height":48,"propsJson":"{\"label\":\"Save\",\"variant\":\"primary\"}"},
        {"nodeId":"crud_cancel_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":10,"parentNodeId":"crud_create_modal","x":296,"y":536,"width":120,"height":48,"propsJson":"{\"label\":\"Cancel\"}"},
        {"nodeId":"crud_confirm_dialog","nodeKind":"catalog_component","componentKey":"apply_confirm_dialog.primitive","componentKind":"safety_guard/apply_confirm_dialog","isDraftOnly":false,"slotKey":"dialogs","orderIndex":11,"parentNodeId":"crud_shell","x":288,"y":280,"width":448,"height":240,"propsJson":"{\"title\":\"Confirm action\",\"message\":\"Are you sure?\"}"},
        {"nodeId":"crud_detail_drawer","nodeKind":"catalog_component","componentKey":"row_detail_drawer.primitive","componentKind":"table_op/row_detail_drawer","isDraftOnly":false,"slotKey":"drawers","orderIndex":12,"parentNodeId":"crud_shell","x":680,"y":136,"width":320,"height":608,"propsJson":"{\"title\":\"Record detail\"}","propBindings":{"data":{"source":"emission.data"}}},
        {"nodeId":"crud_debug_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","isDraftOnly":false,"slotKey":"debug","orderIndex":13,"parentNodeId":"crud_shell","x":24,"y":560,"width":640,"height":192,"propsJson":"{\"title\":\"Emission debug\"}","propBindings":{"data":{"source":"emission.data"}}}
      ],
      "layoutClassRefs": []
    }
    $$::jsonb,
    $${"activeTopologyWrite":false,"bindTarget":"selected_route_package_tmp_canvas_draft","requiresHumanAdjustmentBeforeApply":true}$$::jsonb,
    $$
    [
      {"nodeId":"crud_search_button","sourceObjectId":"crud_search_button","capabilityTag":"requires_event_binding","wiringKind":"search","targetSurface":"content_bundle","targetRef":"content_bundle:search","status":"pending","binding":{"event":"click","payloadFrom":{"keyword":"node:crud_search_input.value"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"crud_result_list","sourceObjectId":"crud_result_list","capabilityTag":"requires_event_binding","wiringKind":"get","targetSurface":"content_bundle","targetRef":"content_bundle:get_entity","status":"pending","binding":{"event":"item.click","payloadFrom":{"entityId":"event.item.id"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"crud_submit_button","sourceObjectId":"crud_submit_button","capabilityTag":"requires_event_binding","wiringKind":"create","targetSurface":"content_bundle","targetRef":"content_bundle:create_entity_draft","status":"pending","binding":{"event":"click","payloadFrom":{},"note":"Author maps form field values to payload after preset load"}}
    ]
    $$::jsonb,
    $$[{"nodeId":"crud_shell","styleIntent":"section_shell"},{"nodeId":"crud_results_panel","styleIntent":"result_panel"},{"nodeId":"crud_create_modal","styleIntent":"create_modal"}]$$::jsonb,
    $$
    [
      {"nodeId":"crud_status_filter","reason":"propBindings.options source emission.data.states is pending — requires content_bundle:list_states to populate state options","knownGapRef":"enum_status_select_options_from_content_bundle_list_states"},
      {"nodeId":"crud_submit_button","reason":"payloadFrom is empty — author must map form field node values to entity draft fields after preset load","knownGapRef":"form_field_values_to_create_entity_draft_payload"}
    ]
    $$::jsonb
);

COMMIT;
