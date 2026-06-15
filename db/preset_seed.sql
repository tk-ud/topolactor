-- =============================================================================
-- preset_seed.sql
-- Canonical: UIBuilder preset ecosystem seed data.
--
-- Contents (in order):
--   - hub_search.readonly.v1 preset registration
--   - physical_search_crud_aggregate.v1 preset registration
--   - physical_details_inline_editor_md_generator.v1 preset registration
--   - aggregate_dashboard.v1 preset registration
--   - ui_component_registry and components_bucket bootstrap rows
--
-- SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml
--       docs/design/mock-preset-intake-compiler-ssot.yaml
--
-- BOOTSTRAP POLICY: part of the canonical fresh bootstrap sequence in db/init.sql.
-- Applied via docker-entrypoint-initdb.d/00-init.sql on fresh compose volume.
-- Runs after seed_empty.sql and mock_preset_tables.sql.
-- IDEMPOTENT: all statements use ON CONFLICT DO UPDATE or ON CONFLICT DO NOTHING.
-- PRECONDITION: topology.mock_preset_* tables must exist (db/mock_preset_tables.sql).
-- =============================================================================


-- -- hub_search_preset_seed --

WITH upserted_preset AS (
    INSERT INTO topology.mock_preset_registry (
        preset_key,
        preset_label,
        source_kind,
        source_hash,  -- updated v2: wiring aligned to content_bundle:search
        source_snapshot_json,
        visual_tree_json,
        status
    )
    VALUES (
        'hub_search.readonly.v1',
        'Hub Search readonly preset seed',
        'ui_builder_canvas',
        'hub_search.readonly.v1.seed.2026-06-12.v2',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "hub_search.readonly.v1",
          "bundle": "ui-builder-preset-ecosystem",
          "surface": "hub_search",
          "role": "read-only hub / topology search view seed",
          "activeTopology": false,
          "componentImplementation": "none; existing component catalog composition only",
          "runtimeMutationAuthority": "none",
          "boundary": "load into selected route package tmp canvas draft; human edit; preview; validate; apply",
          "knownGapRefs": ["runtime_submit_payload_binding_from_node_values"],
          "wiringAlignment": "content_bundle:search (hub:search was not SSOT-authorized)"
        }
        $$::jsonb,
        $$
        {
          "nodes": [
            { "nodeId": "hub_search_shell", "componentKey": "section.alias", "componentKind": "disclosure_structure/section", "parentNodeId": null },
            { "nodeId": "hub_search_input", "componentKey": "search_input.alias", "componentKind": "form_input/search_input", "parentNodeId": "hub_search_shell" },
            { "nodeId": "hub_search_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "hub_search_shell" },
            { "nodeId": "hub_search_results_panel", "componentKey": "panel.alias", "componentKind": "disclosure_structure/panel", "parentNodeId": "hub_search_shell" },
            { "nodeId": "hub_search_results", "componentKey": "card_list.primitive", "componentKind": "display/card_list", "parentNodeId": "hub_search_results_panel" },
            { "nodeId": "hub_search_debug_json", "componentKey": "json_viewer.template", "componentKind": "data_display/json", "parentNodeId": "hub_search_results_panel" }
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
        preset_id,
        source_object_id,
        node_id,
        node_kind,
        component_key,
        component_kind,
        parent_source_object_id,
        slot_key,
        order_index,
        bbox_json,
        text_json,
        style_candidate_json,
        mapping_status
    )
    VALUES
        ((SELECT preset_id FROM preset), 'hub_search_shell', 'hub_search_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0,
            '{"x":0,"y":0,"width":960,"height":640}'::jsonb,
            '{"title":"Hub Search","description":"Read-only hub / topology search seed."}'::jsonb,
            '{"layoutIntent":"root_shell"}'::jsonb,
            'mapped'),
        ((SELECT preset_id FROM preset), 'hub_search_input', 'hub_search_input', 'catalog_component', 'search_input.alias', 'form_input/search_input', 'hub_search_shell', 'controls', 1,
            '{"x":24,"y":96,"width":560,"height":56}'::jsonb,
            '{"label":"Search query","placeholder":"Search hubs, manifests, topology..."}'::jsonb,
            '{"layoutIntent":"query_input"}'::jsonb,
            'mapped'),
        ((SELECT preset_id FROM preset), 'hub_search_button', 'hub_search_button', 'catalog_component', 'button.primitive', 'action/button', 'hub_search_shell', 'controls', 2,
            '{"x":608,"y":96,"width":160,"height":56}'::jsonb,
            '{"label":"Search"}'::jsonb,
            '{"layoutIntent":"readonly_search_trigger"}'::jsonb,
            'mapped'),
        ((SELECT preset_id FROM preset), 'hub_search_results_panel', 'hub_search_results_panel', 'catalog_component', 'panel.alias', 'disclosure_structure/panel', 'hub_search_shell', 'results', 3,
            '{"x":24,"y":184,"width":912,"height":408}'::jsonb,
            '{"title":"Search results","description":"Read-only projection area."}'::jsonb,
            '{"layoutIntent":"results_panel"}'::jsonb,
            'mapped'),
        ((SELECT preset_id FROM preset), 'hub_search_results', 'hub_search_results', 'catalog_component', 'card_list.primitive', 'display/card_list', 'hub_search_results_panel', 'content', 4,
            '{"x":48,"y":248,"width":560,"height":312}'::jsonb,
            '{"emptyText":"No hub search results yet."}'::jsonb,
            '{"layoutIntent":"result_cards"}'::jsonb,
            'mapped'),
        ((SELECT preset_id FROM preset), 'hub_search_debug_json', 'hub_search_debug_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'hub_search_results_panel', 'debug', 5,
            '{"x":632,"y":248,"width":280,"height":312}'::jsonb,
            '{"title":"Emission debug"}'::jsonb,
            '{"layoutIntent":"debug_projection"}'::jsonb,
            'mapped')
    RETURNING mapping_id
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id,
        source_object_id,
        node_id,
        capability_tag,
        wiring_kind,
        target_surface,
        target_ref,
        binding_json,
        status
    )
    VALUES
        ((SELECT preset_id FROM preset), 'hub_search_button', 'hub_search_button', 'requires_event_binding', 'search', 'content_bundle', 'content_bundle:search',
            $$
            {
              "event": "click",
              "wiringKind": "search",
              "targetSurface": "content_bundle",
              "targetRef": "content_bundle:search",
              "readOnly": true,
              "payloadFrom": { "keyword": "node:hub_search_input.value" },
              "payloadResolverRef": "frontend/runtime/payloadFromResolver.ts",
              "wiringAlignmentNote": "hub:search was not SSOT-authorized. Aligned to content_bundle:search (AdminRuntime DataContentBundleSearchAsync). payload field 'keyword' sourced from node:hub_search_input.value via payloadFromResolver."
            }
            $$::jsonb,
            'pending')
    RETURNING wiring_candidate_id
)
INSERT INTO topology.mock_preset_compile_snapshot (
    preset_id,
    compiler_version,
    layout_patch_json,
    package_membership_candidate_json,
    wiring_candidate_json,
    style_candidate_json,
    unresolved_json
)
VALUES (
    (SELECT preset_id FROM preset),
    'hub-search-seed.v2',
    $$
    {
      "nodes": [
        {
          "nodeId": "hub_search_shell",
          "nodeKind": "catalog_component",
          "componentKey": "section.alias",
          "componentKind": "disclosure_structure/section",
          "isDraftOnly": false,
          "slotKey": "root",
          "orderIndex": 0,
          "parentNodeId": null,
          "gridCol": 1,
          "gridRow": 1,
          "x": 0,
          "y": 0,
          "width": 960,
          "height": 640,
          "propsJson": "{\"title\":\"Hub Search\",\"description\":\"Read-only hub / topology search seed. Adjust layout and bindings before preview / validate / apply.\"}"
        },
        {
          "nodeId": "hub_search_input",
          "nodeKind": "catalog_component",
          "componentKey": "search_input.alias",
          "componentKind": "form_input/search_input",
          "isDraftOnly": false,
          "slotKey": "controls",
          "orderIndex": 1,
          "parentNodeId": "hub_search_shell",
          "gridCol": 1,
          "gridRow": 2,
          "x": 24,
          "y": 96,
          "width": 560,
          "height": 56,
          "propsJson": "{\"label\":\"Search query\",\"placeholder\":\"Search hubs, manifests, topology...\",\"value\":\"\"}"
        },
        {
          "nodeId": "hub_search_button",
          "nodeKind": "catalog_component",
          "componentKey": "button.primitive",
          "componentKind": "action/button",
          "isDraftOnly": false,
          "slotKey": "controls",
          "orderIndex": 2,
          "parentNodeId": "hub_search_shell",
          "gridCol": 8,
          "gridRow": 2,
          "x": 608,
          "y": 96,
          "width": 160,
          "height": 56,
          "propsJson": "{\"label\":\"Search\",\"variant\":\"primary\",\"disabled\":false}"
        },
        {
          "nodeId": "hub_search_results_panel",
          "nodeKind": "catalog_component",
          "componentKey": "panel.alias",
          "componentKind": "disclosure_structure/panel",
          "isDraftOnly": false,
          "slotKey": "results",
          "orderIndex": 3,
          "parentNodeId": "hub_search_shell",
          "gridCol": 1,
          "gridRow": 3,
          "x": 24,
          "y": 184,
          "width": 912,
          "height": 408,
          "propsJson": "{\"title\":\"Search results\",\"description\":\"Read-only hub/topology projection area.\"}"
        },
        {
          "nodeId": "hub_search_results",
          "nodeKind": "catalog_component",
          "componentKey": "card_list.primitive",
          "componentKind": "display/card_list",
          "isDraftOnly": false,
          "slotKey": "content",
          "orderIndex": 4,
          "parentNodeId": "hub_search_results_panel",
          "gridCol": 1,
          "gridRow": 4,
          "x": 48,
          "y": 248,
          "width": 560,
          "height": 312,
          "propsJson": "{\"title\":\"Results\",\"emptyText\":\"No hub search results yet.\"}",
          "propBindings": {
            "items": { "source": "emission.data.rows" }
          }
        },
        {
          "nodeId": "hub_search_debug_json",
          "nodeKind": "catalog_component",
          "componentKey": "json_viewer.template",
          "componentKind": "data_display/json",
          "isDraftOnly": false,
          "slotKey": "debug",
          "orderIndex": 5,
          "parentNodeId": "hub_search_results_panel",
          "gridCol": 8,
          "gridRow": 4,
          "x": 632,
          "y": 248,
          "width": 280,
          "height": 312,
          "propsJson": "{\"title\":\"Emission debug\"}",
          "propBindings": {
            "data": { "source": "emission.data" }
          }
        }
      ],
      "layoutClassRefs": []
    }
    $$::jsonb,
    $$
    {
      "activeTopologyWrite": false,
      "bindTarget": "selected_route_package_tmp_canvas_draft",
      "requiresHumanAdjustmentBeforeApply": true
    }
    $$::jsonb,
    $$
    [
      {
        "nodeId": "hub_search_button",
        "sourceObjectId": "hub_search_button",
        "capabilityTag": "requires_event_binding",
        "wiringKind": "search",
        "targetSurface": "content_bundle",
        "targetRef": "content_bundle:search",
        "status": "pending",
        "binding": {
          "event": "click",
          "payloadFrom": { "keyword": "node:hub_search_input.value" },
          "payloadResolverRef": "frontend/runtime/payloadFromResolver.ts"
        }
      }
    ]
    $$::jsonb,
    $$
    [
      { "nodeId": "hub_search_shell", "styleIntent": "section_shell" },
      { "nodeId": "hub_search_results_panel", "styleIntent": "result_panel" }
    ]
    $$::jsonb,
    $$
    []
    $$::jsonb
);


-- -- physical_search_crud_aggregate_preset_seed --

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


-- -- aggregate_dashboard_preset_seed --

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
        'aggregate_dashboard.v1',
        'Aggregate Dashboard preset seed',
        'ui_builder_canvas',
        'aggregate_dashboard.v1.seed.2026-06-12',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "aggregate_dashboard.v1",
          "bundle": "ui-builder-preset-ecosystem",
          "surface": "aggregate_dashboard",
          "role": "aggregation / grouped measure / dashboard card surface",
          "activeTopology": false,
          "componentImplementation": "none; existing component catalog composition only",
          "runtimeMutationAuthority": "none; UIBuilder author chooses manifest screenReadQueryWiring in PackageWiringEditor, then backend persists package wiring to topology.ui_wiring_registry.target_ref",
          "boundary": "load into selected route package tmp canvas draft; human edit; preview; validate; apply",
          "designIntent": "UI preset supplies filter inputs, run trigger, and result display only. After preset load, author wires the run button in PackageWiringEditor by selecting manifest screenReadQueryWiring candidates, normally aggregationMeasures.",
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts",
          "knownGaps": [
            {
              "gap": "aggregation_function_ref_wiring",
              "status": "pending_author_selection_via_package_wiring_editor",
              "note": "Author selects a manifest screenReadQueryWiring candidate in PackageWiringEditor. Candidate list action is manifest:list_screen_read_query_wiring; aggregation dashboard candidates normally come from screen_data_shape.screenReadQueryWiring.aggregationMeasures. The saved package wiring uses targetSurface=manifest and persists targetRef=manifest:<manifestId>:<wiringKey> to topology.ui_wiring_registry.target_ref via ui_topology:update_package_wiring. Seed does not invent a real manifestId before author selection."
            }
          ]
        }
        $$::jsonb,
        $$
        {
          "nodes": [
            { "nodeId": "dashboard_shell", "componentKey": "section.alias", "componentKind": "disclosure_structure/section", "parentNodeId": null },
            { "nodeId": "dashboard_start_date", "componentKey": "input.primitive", "componentKind": "form_input/input", "parentNodeId": "dashboard_shell" },
            { "nodeId": "dashboard_end_date", "componentKey": "input.primitive", "componentKind": "form_input/input", "parentNodeId": "dashboard_shell" },
            { "nodeId": "dashboard_status_filter", "componentKey": "select.template", "componentKind": "form_input/select", "parentNodeId": "dashboard_shell" },
            { "nodeId": "dashboard_run_button", "componentKey": "button.primitive", "componentKind": "action/button", "parentNodeId": "dashboard_shell" },
            { "nodeId": "dashboard_results_panel", "componentKey": "panel.alias", "componentKind": "disclosure_structure/panel", "parentNodeId": "dashboard_shell" },
            { "nodeId": "dashboard_aggregation_table", "componentKey": "aggregation_preview_table.primitive", "componentKind": "calc_topology/aggregation_preview_table", "parentNodeId": "dashboard_results_panel" },
            { "nodeId": "dashboard_stats_panel", "componentKey": "hub_statistics_panel.primitive", "componentKind": "calc_topology/hub_statistics_panel", "parentNodeId": "dashboard_results_panel" },
            { "nodeId": "dashboard_debug_json", "componentKey": "json_viewer.template", "componentKind": "data_display/json", "parentNodeId": "dashboard_shell" }
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
        ((SELECT preset_id FROM preset), 'dashboard_shell', 'dashboard_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0,
            '{"x":0,"y":0,"width":1024,"height":768}'::jsonb,
            '{"title":"Aggregate Dashboard","description":"Aggregation filter inputs and result display. Use PackageWiringEditor to select a manifest screenReadQueryWiring aggregationMeasures candidate for the run button."}'::jsonb,
            '{"layoutIntent":"root_shell"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dashboard_start_date', 'dashboard_start_date', 'catalog_component', 'input.primitive', 'form_input/input', 'dashboard_shell', 'filters', 1,
            '{"x":24,"y":64,"width":200,"height":48}'::jsonb,
            '{"label":"Start date","type":"date"}'::jsonb,
            '{"layoutIntent":"date_filter"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dashboard_end_date', 'dashboard_end_date', 'catalog_component', 'input.primitive', 'form_input/input', 'dashboard_shell', 'filters', 2,
            '{"x":240,"y":64,"width":200,"height":48}'::jsonb,
            '{"label":"End date","type":"date"}'::jsonb,
            '{"layoutIntent":"date_filter"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dashboard_status_filter', 'dashboard_status_filter', 'catalog_component', 'select.template', 'form_input/select', 'dashboard_shell', 'filters', 3,
            '{"x":456,"y":64,"width":200,"height":48}'::jsonb,
            '{"label":"Status","placeholder":"All"}'::jsonb,
            '{"layoutIntent":"status_filter"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dashboard_run_button', 'dashboard_run_button', 'catalog_component', 'button.primitive', 'action/button', 'dashboard_shell', 'filters', 4,
            '{"x":672,"y":64,"width":120,"height":48}'::jsonb,
            '{"label":"Run","variant":"primary"}'::jsonb,
            '{"layoutIntent":"run_trigger"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dashboard_results_panel', 'dashboard_results_panel', 'catalog_component', 'panel.alias', 'disclosure_structure/panel', 'dashboard_shell', 'results', 5,
            '{"x":24,"y":136,"width":976,"height":592}'::jsonb,
            '{"title":"Aggregation results"}'::jsonb,
            '{"layoutIntent":"results_panel"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dashboard_aggregation_table', 'dashboard_aggregation_table', 'catalog_component', 'aggregation_preview_table.primitive', 'calc_topology/aggregation_preview_table', 'dashboard_results_panel', 'main', 6,
            '{"x":48,"y":184,"width":656,"height":496}'::jsonb,
            '{"title":"Aggregation results","emptyText":"Run aggregation to see results."}'::jsonb,
            '{"layoutIntent":"aggregation_table"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dashboard_stats_panel', 'dashboard_stats_panel', 'catalog_component', 'hub_statistics_panel.primitive', 'calc_topology/hub_statistics_panel', 'dashboard_results_panel', 'sidebar', 7,
            '{"x":720,"y":184,"width":256,"height":496}'::jsonb,
            '{"title":"Summary stats"}'::jsonb,
            '{"layoutIntent":"stats_sidebar"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'dashboard_debug_json', 'dashboard_debug_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'dashboard_shell', 'debug', 8,
            '{"x":24,"y":744,"width":480,"height":16}'::jsonb,
            '{"title":"Emission debug"}'::jsonb,
            '{"layoutIntent":"debug_projection"}'::jsonb, 'mapped')
    RETURNING mapping_id
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id, source_object_id, node_id, capability_tag,
        wiring_kind, target_surface, target_ref, binding_json, status
    )
    VALUES
        -- run button → manifest screenReadQueryWiring aggregation measure (author selects after preset load)
        ((SELECT preset_id FROM preset), 'dashboard_run_button', 'dashboard_run_button', 'requires_event_binding', 'aggregate', 'manifest', '',
            $${"event":"click","wiringKind":"aggregate","targetSurface":"manifest","targetRef":"","targetRefFormat":"manifest:<manifestId>:<wiringKey>","authoringStatus":"pending_author_selection_via_package_wiring_editor","authoringSurface":"PackageWiringEditor","candidateListAction":"manifest:list_screen_read_query_wiring","candidateSource":"screen_data_shape.screenReadQueryWiring.aggregationMeasures","persistedField":"topology.ui_wiring_registry.target_ref","saveAction":"ui_topology:update_package_wiring","payloadFrom":{"startDate":"node:dashboard_start_date.value","endDate":"node:dashboard_end_date.value"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Author selects a manifest read/query wiringKey after preset load; seed keeps targetRef empty rather than inventing manifest:<manifestId>:<wiringKey> before author selection.","knownGapRef":"aggregation_function_ref_wiring"}$$::jsonb, 'pending')
    RETURNING wiring_candidate_id
)
INSERT INTO topology.mock_preset_compile_snapshot (
    preset_id, compiler_version, layout_patch_json,
    package_membership_candidate_json, wiring_candidate_json,
    style_candidate_json, unresolved_json
)
VALUES (
    (SELECT preset_id FROM preset),
    'aggregate-dashboard-seed.v1',
    $$
    {
      "nodes": [
        {"nodeId":"dashboard_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","isDraftOnly":false,"slotKey":"root","orderIndex":0,"parentNodeId":null,"x":0,"y":0,"width":1024,"height":768,"propsJson":"{\"title\":\"Aggregate Dashboard\",\"description\":\"Filter inputs and aggregation result display. Use PackageWiringEditor to select a manifest screenReadQueryWiring aggregationMeasures candidate after preset load.\"}"},
        {"nodeId":"dashboard_start_date","nodeKind":"catalog_component","componentKey":"input.primitive","componentKind":"form_input/input","isDraftOnly":false,"slotKey":"filters","orderIndex":1,"parentNodeId":"dashboard_shell","x":24,"y":64,"width":200,"height":48,"propsJson":"{\"label\":\"Start date\",\"type\":\"date\",\"value\":\"\"}"},
        {"nodeId":"dashboard_end_date","nodeKind":"catalog_component","componentKey":"input.primitive","componentKind":"form_input/input","isDraftOnly":false,"slotKey":"filters","orderIndex":2,"parentNodeId":"dashboard_shell","x":240,"y":64,"width":200,"height":48,"propsJson":"{\"label\":\"End date\",\"type\":\"date\",\"value\":\"\"}"},
        {"nodeId":"dashboard_status_filter","nodeKind":"catalog_component","componentKey":"select.template","componentKind":"form_input/select","isDraftOnly":false,"slotKey":"filters","orderIndex":3,"parentNodeId":"dashboard_shell","x":456,"y":64,"width":200,"height":48,"propsJson":"{\"label\":\"Status\",\"placeholder\":\"All\"}"},
        {"nodeId":"dashboard_run_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"filters","orderIndex":4,"parentNodeId":"dashboard_shell","x":672,"y":64,"width":120,"height":48,"propsJson":"{\"label\":\"Run\",\"variant\":\"primary\"}"},
        {"nodeId":"dashboard_results_panel","nodeKind":"catalog_component","componentKey":"panel.alias","componentKind":"disclosure_structure/panel","isDraftOnly":false,"slotKey":"results","orderIndex":5,"parentNodeId":"dashboard_shell","x":24,"y":136,"width":976,"height":592,"propsJson":"{\"title\":\"Aggregation results\"}"},
        {"nodeId":"dashboard_aggregation_table","nodeKind":"catalog_component","componentKey":"aggregation_preview_table.primitive","componentKind":"calc_topology/aggregation_preview_table","isDraftOnly":false,"slotKey":"main","orderIndex":6,"parentNodeId":"dashboard_results_panel","x":48,"y":184,"width":656,"height":496,"propsJson":"{\"title\":\"Aggregation results\",\"emptyText\":\"Run aggregation to see results.\"}","propBindings":{"data":{"source":"emission.data.aggregationResults"}}},
        {"nodeId":"dashboard_stats_panel","nodeKind":"catalog_component","componentKey":"hub_statistics_panel.primitive","componentKind":"calc_topology/hub_statistics_panel","isDraftOnly":false,"slotKey":"sidebar","orderIndex":7,"parentNodeId":"dashboard_results_panel","x":720,"y":184,"width":256,"height":496,"propsJson":"{\"title\":\"Summary stats\"}","propBindings":{"data":{"source":"emission.data"}}},
        {"nodeId":"dashboard_debug_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","isDraftOnly":false,"slotKey":"debug","orderIndex":8,"parentNodeId":"dashboard_shell","x":24,"y":744,"width":480,"height":16,"propsJson":"{\"title\":\"Emission debug\"}","propBindings":{"data":{"source":"emission.data"}}}
      ],
      "layoutClassRefs": []
    }
    $$::jsonb,
    $${"activeTopologyWrite":false,"bindTarget":"selected_route_package_tmp_canvas_draft","requiresHumanAdjustmentBeforeApply":true}$$::jsonb,
    $$
    [
      {"nodeId":"dashboard_run_button","sourceObjectId":"dashboard_run_button","capabilityTag":"requires_event_binding","wiringKind":"aggregate","targetSurface":"manifest","targetRef":"","status":"pending","binding":{"event":"click","targetRefFormat":"manifest:<manifestId>:<wiringKey>","authoringStatus":"pending_author_selection_via_package_wiring_editor","authoringSurface":"PackageWiringEditor","candidateListAction":"manifest:list_screen_read_query_wiring","candidateSource":"screen_data_shape.screenReadQueryWiring.aggregationMeasures","persistedField":"topology.ui_wiring_registry.target_ref","saveAction":"ui_topology:update_package_wiring","payloadFrom":{"startDate":"node:dashboard_start_date.value","endDate":"node:dashboard_end_date.value"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Author selects the manifest read/query wiringKey after preset load; no real manifestId is invented in the seed.","knownGapRef":"aggregation_function_ref_wiring"}}
    ]
    $$::jsonb,
    $$[{"nodeId":"dashboard_shell","styleIntent":"section_shell"},{"nodeId":"dashboard_results_panel","styleIntent":"result_panel"}]$$::jsonb,
    $$
    [
      {"nodeId":"dashboard_run_button","reason":"authoring pending — PackageWiringEditor must select manifest:list_screen_read_query_wiring candidate from screen_data_shape.screenReadQueryWiring.aggregationMeasures and save topology.ui_wiring_registry.target_ref as manifest:<manifestId>:<wiringKey> via ui_topology:update_package_wiring; seed intentionally does not invent a manifestId before author selection","knownGapRef":"aggregation_function_ref_wiring","status":"pending_author_selection_via_package_wiring_editor"}
    ]
    $$::jsonb
);


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
