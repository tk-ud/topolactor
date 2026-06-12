-- =============================================================================
-- aggregate_dashboard_preset_seed.sql
-- Seed: topology.mock_preset_* registration for the UIBuilder preset ecosystem.
--
-- SSOT: docs/design/ui-builder-preset-ecosystem-ssot.yaml
--       aggregate_dashboard_preset
-- Registry table authority: db/migrations/mock_preset_registry_tables.sql
--
-- Registers `aggregate_dashboard.v1` as a reusable aggregation / dashboard
-- canvas draft built from existing component catalog entries only. It is NOT
-- a new component implementation and is NOT active topology.
-- Aggregation logic stays in contents-side topology/function wiring; this preset
-- supplies only filter inputs, run trigger, and result display nodes.
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
          "runtimeMutationAuthority": "none; aggregation logic in contents-side topology function wiring",
          "boundary": "load into selected route package tmp canvas draft; human edit; preview; validate; apply",
          "designIntent": "UI preset supplies filter inputs, run trigger, and result display only. Author wires run button to the contents topology assigned aggregation operation ref after preset load.",
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts",
          "knownGaps": [
            {
              "gap": "aggregation_function_ref_wiring",
              "status": "ssot_ambiguity",
              "note": "Contents-side aggregation function ref wiring surface is not yet specified in a dedicated SSOT. Author selects the aggregation operation ref manually after preset load."
            },
            {
              "gap": "aggregation_preview_table_prop_binding_capability",
              "status": "pending_catalog_validation",
              "note": "aggregation_preview_table and hub_statistics_panel capability tags for propBindings data target need validation."
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
            '{"title":"Aggregate Dashboard","description":"Aggregation filter inputs and result display. Wire run button to the contents topology aggregation operation ref."}'::jsonb,
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
        -- run button → contents aggregation operation (author selects after preset load)
        ((SELECT preset_id FROM preset), 'dashboard_run_button', 'dashboard_run_button', 'requires_event_binding', 'aggregate', 'content_bundle', '',
            $${"event":"click","wiringKind":"aggregate","targetSurface":"content_bundle","targetRef":"","payloadFrom":{"startDate":"node:dashboard_start_date.value","endDate":"node:dashboard_end_date.value"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Author sets targetRef to the contents topology assigned aggregation operation ref after preset load.","knownGapRef":"aggregation_function_ref_wiring"}$$::jsonb, 'pending')
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
        {"nodeId":"dashboard_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","isDraftOnly":false,"slotKey":"root","orderIndex":0,"parentNodeId":null,"x":0,"y":0,"width":1024,"height":768,"propsJson":"{\"title\":\"Aggregate Dashboard\",\"description\":\"Filter inputs and aggregation result display. Wire the run button to the contents topology aggregation operation ref after preset load.\"}"},
        {"nodeId":"dashboard_start_date","nodeKind":"catalog_component","componentKey":"input.primitive","componentKind":"form_input/input","isDraftOnly":false,"slotKey":"filters","orderIndex":1,"parentNodeId":"dashboard_shell","x":24,"y":64,"width":200,"height":48,"propsJson":"{\"label\":\"Start date\",\"type\":\"date\",\"value\":\"\"}"},
        {"nodeId":"dashboard_end_date","nodeKind":"catalog_component","componentKey":"input.primitive","componentKind":"form_input/input","isDraftOnly":false,"slotKey":"filters","orderIndex":2,"parentNodeId":"dashboard_shell","x":240,"y":64,"width":200,"height":48,"propsJson":"{\"label\":\"End date\",\"type\":\"date\",\"value\":\"\"}"},
        {"nodeId":"dashboard_status_filter","nodeKind":"catalog_component","componentKey":"select.template","componentKind":"form_input/select","isDraftOnly":false,"slotKey":"filters","orderIndex":3,"parentNodeId":"dashboard_shell","x":456,"y":64,"width":200,"height":48,"propsJson":"{\"label\":\"Status\",\"placeholder\":\"All\"}"},
        {"nodeId":"dashboard_run_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"filters","orderIndex":4,"parentNodeId":"dashboard_shell","x":672,"y":64,"width":120,"height":48,"propsJson":"{\"label\":\"Run\",\"variant\":\"primary\"}"},
        {"nodeId":"dashboard_results_panel","nodeKind":"catalog_component","componentKey":"panel.alias","componentKind":"disclosure_structure/panel","isDraftOnly":false,"slotKey":"results","orderIndex":5,"parentNodeId":"dashboard_shell","x":24,"y":136,"width":976,"height":592,"propsJson":"{\"title\":\"Aggregation results\"}"},
        {"nodeId":"dashboard_aggregation_table","nodeKind":"catalog_component","componentKey":"aggregation_preview_table.primitive","componentKind":"calc_topology/aggregation_preview_table","isDraftOnly":false,"slotKey":"main","orderIndex":6,"parentNodeId":"dashboard_results_panel","x":48,"y":184,"width":656,"height":496,"propsJson":"{\"title\":\"Aggregation results\",\"emptyText\":\"Run aggregation to see results.\"}"},
        {"nodeId":"dashboard_stats_panel","nodeKind":"catalog_component","componentKey":"hub_statistics_panel.primitive","componentKind":"calc_topology/hub_statistics_panel","isDraftOnly":false,"slotKey":"sidebar","orderIndex":7,"parentNodeId":"dashboard_results_panel","x":720,"y":184,"width":256,"height":496,"propsJson":"{\"title\":\"Summary stats\"}"},
        {"nodeId":"dashboard_debug_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","isDraftOnly":false,"slotKey":"debug","orderIndex":8,"parentNodeId":"dashboard_shell","x":24,"y":744,"width":480,"height":16,"propsJson":"{\"title\":\"Emission debug\"}","propBindings":{"data":{"source":"emission.data"}}}
      ],
      "layoutClassRefs": []
    }
    $$::jsonb,
    $${"activeTopologyWrite":false,"bindTarget":"selected_route_package_tmp_canvas_draft","requiresHumanAdjustmentBeforeApply":true}$$::jsonb,
    $$
    [
      {"nodeId":"dashboard_run_button","sourceObjectId":"dashboard_run_button","capabilityTag":"requires_event_binding","wiringKind":"aggregate","targetSurface":"content_bundle","targetRef":"","status":"pending","binding":{"event":"click","payloadFrom":{"startDate":"node:dashboard_start_date.value","endDate":"node:dashboard_end_date.value"},"payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","knownGapRef":"aggregation_function_ref_wiring"}}
    ]
    $$::jsonb,
    $$[{"nodeId":"dashboard_shell","styleIntent":"section_shell"},{"nodeId":"dashboard_results_panel","styleIntent":"result_panel"}]$$::jsonb,
    $$
    [
      {"nodeId":"dashboard_run_button","reason":"targetRef is empty — author must set the contents topology assigned aggregation operation ref after preset load","knownGapRef":"aggregation_function_ref_wiring"},
      {"nodeId":"dashboard_aggregation_table","reason":"propBindings.data capability validation pending for calc_topology/aggregation_preview_table","knownGapRef":"aggregation_preview_table_prop_binding_capability"},
      {"nodeId":"dashboard_stats_panel","reason":"propBindings.data capability validation pending for calc_topology/hub_statistics_panel","knownGapRef":"aggregation_preview_table_prop_binding_capability"}
    ]
    $$::jsonb
);

COMMIT;
