-- =============================================================================
-- hub_search_preset_seed.sql
-- Seed: topology.mock_preset_* registration for the UIBuilder preset ecosystem.
--
-- SSOT: docs/design/admin-console-workflow-ssot.yaml
-- Registry table authority: db/migrations/mock_preset_registry_tables.sql
--
-- This seed registers `hub_search.readonly.v1` as a read-only hub/topology
-- search canvas draft built from existing component catalog entries only. It is
-- NOT a new component implementation and is NOT active topology: UIBuilder may
-- load it into a selected route package tmp canvas draft, after which the normal
-- human edit -> preview -> validate -> apply boundary remains mandatory.
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
        'hub_search.readonly.v1',
        'Hub Search readonly preset seed',
        'ui_builder_canvas',
        'hub_search.readonly.v1.seed.2026-06-12',
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
          "knownGapRefs": ["runtime_submit_payload_binding_from_node_values"]
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
        ((SELECT preset_id FROM preset), 'hub_search_button', 'hub_search_button', 'requires_event_binding', 'search', 'hub', 'hub:search',
            $$
            {
              "event": "click",
              "wiringKind": "search",
              "targetSurface": "hub",
              "targetRef": "hub:search",
              "readOnly": true,
              "payloadBindingStatus": "not_implemented",
              "knownGapRef": "runtime_submit_payload_binding_from_node_values",
              "payloadFrom": { "query": "node:hub_search_input.value" },
              "note": "Candidate only. Runtime node-value payload resolver is not implemented by this seed."
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
    'hub-search-seed.v1',
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
        "targetSurface": "hub",
        "targetRef": "hub:search",
        "status": "pending",
        "binding": {
          "event": "click",
          "payloadFrom": { "query": "node:hub_search_input.value" },
          "payloadBindingStatus": "not_implemented",
          "knownGapRef": "runtime_submit_payload_binding_from_node_values"
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

COMMIT;
