-- =============================================================================
-- file_storage_export_job_preset_seed.sql
-- Canonical: UIBuilder preset ecosystem seed data — file_storage_export_job.v1 preset registration.
-- Derived from physical_search_crud_aggregate.v1 and projects export_job initiation
-- through the file_storage access_port (external-port:access_port:00000000-0000-0000-0000-000000000f01).
-- No dedicated UI runtime/component or credential plane is introduced.
-- Payload fields: idempotency_key, export_format, period, requested_by, file_name, file_type.
-- file_name (required) and file_type (required) align with af02 record_file_artifact step 1
-- input bindings (c009/c00a) which pull these from payload with required=true.
-- Projection response: file_artifact_id, file_name, file_type (via af02 step 2 OutputProp),
--   authorization_key (opaque reference via af04 step 2 OutputProp). No signed_url / credential.
-- =============================================================================

WITH upserted_preset AS (
    INSERT INTO topology.mock_preset_registry (
        preset_key, preset_label, source_kind, source_hash,
        source_snapshot_json, visual_tree_json, status
    )
    VALUES (
        'file_storage_export_job.v1',
        'File Storage Export Job preset seed',
        'ui_builder_canvas',
        'file_storage_export_job.v1.seed.2026-06-21.r2',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "file_storage_export_job.v1",
          "bundle": "file-storage-port-consumer",
          "surface": "export_job_initiation",
          "derivedFrom": "physical_search_crud_aggregate.v1",
          "componentImplementation": "none; existing CRUD/component catalog composition only",
          "runtimeMutationAuthority": "external port substrate via portTargetRef and execute_abstract_function policy steps (manifests af01-af04)",
          "credentialPlane": "external_port_substrate reference_key resolution only",
          "forbiddenProjectionFields": ["signed_url", "storage_ref", "credential", "bucket", "endpoint", "raw_storage_ref"],
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts",
          "projectionResponseFields": ["file_artifact_id", "file_name", "file_type", "authorization_key"],
          "note": "authorization_key is an opaque DB reference (not the signed URL). Actual signed URL resolution is runtime secret store concern. file_name and file_type are required in the form to align with af02 record_file_artifact step 1 input bindings (c009/c00a, required=true from payload)."
        }
        $$::jsonb,
        $$
        {"nodes":[
          {"nodeId":"export_job_shell","componentKey":"section.alias","componentKind":"disclosure_structure/section","parentNodeId":null},
          {"nodeId":"export_job_id_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"export_job_shell"},
          {"nodeId":"export_format_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"export_job_shell"},
          {"nodeId":"period_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"export_job_shell"},
          {"nodeId":"requested_by_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"export_job_shell"},
          {"nodeId":"file_name_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"export_job_shell"},
          {"nodeId":"file_type_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"export_job_shell"},
          {"nodeId":"export_job_submit_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"export_job_shell"},
          {"nodeId":"export_job_result_json","componentKey":"json_viewer.template","componentKind":"data_display/json","parentNodeId":"export_job_shell"}
        ]}
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
    DELETE FROM topology.mock_preset_compile_snapshot WHERE preset_id = (SELECT preset_id FROM preset)
), cleanup_wiring AS (
    DELETE FROM topology.mock_preset_wiring_candidate WHERE preset_id = (SELECT preset_id FROM preset)
), cleanup_mapping AS (
    DELETE FROM topology.mock_preset_object_mapping WHERE preset_id = (SELECT preset_id FROM preset)
), inserted_mappings AS (
    INSERT INTO topology.mock_preset_object_mapping (
        preset_id, source_object_id, node_id, node_kind, component_key, component_kind,
        parent_source_object_id, slot_key, order_index, bbox_json, text_json,
        style_candidate_json, mapping_status
    )
    VALUES
        ((SELECT preset_id FROM preset), 'export_job_shell', 'export_job_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0, '{"x":0,"y":0,"width":1024,"height":800}'::jsonb, '{"title":"Export Job","description":"Initiate an export job through the file_storage access port. Configure portTargetRef and payload mapping before apply."}'::jsonb, '{"layoutIntent":"crud_derived_shell"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'export_job_id_input', 'export_job_id_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'export_job_shell', 'fields', 1, '{"x":24,"y":72,"width":456,"height":64}'::jsonb, '{"label":"Export job ID (idempotency key)","required":true}'::jsonb, '{"layoutIntent":"idempotency_key_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'export_format_input', 'export_format_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'export_job_shell', 'fields', 2, '{"x":496,"y":72,"width":456,"height":64}'::jsonb, '{"label":"Export format","required":true,"hint":"pdf / csv / json / zip"}'::jsonb, '{"layoutIntent":"export_format_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'period_input', 'period_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'export_job_shell', 'fields', 3, '{"x":24,"y":152,"width":456,"height":64}'::jsonb, '{"label":"Period (YYYY-MM)","required":false}'::jsonb, '{"layoutIntent":"period_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'requested_by_input', 'requested_by_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'export_job_shell', 'fields', 4, '{"x":496,"y":152,"width":456,"height":64}'::jsonb, '{"label":"Requested by","required":true}'::jsonb, '{"layoutIntent":"requested_by_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_name_input', 'file_name_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'export_job_shell', 'fields', 5, '{"x":24,"y":232,"width":456,"height":64}'::jsonb, '{"label":"File name","required":true,"hint":"e.g. export-2026-06.pdf"}'::jsonb, '{"layoutIntent":"file_name_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_type_input', 'file_type_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'export_job_shell', 'fields', 6, '{"x":496,"y":232,"width":456,"height":64}'::jsonb, '{"label":"File type","required":true,"hint":"pdf / csv / json / zip"}'::jsonb, '{"layoutIntent":"file_type_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'export_job_submit_button', 'export_job_submit_button', 'catalog_component', 'button.primitive', 'action/button', 'export_job_shell', 'actions', 7, '{"x":24,"y":312,"width":200,"height":48}'::jsonb, '{"label":"Start export job","variant":"primary"}'::jsonb, '{"layoutIntent":"external_port_dispatch"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'export_job_result_json', 'export_job_result_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'export_job_shell', 'results', 8, '{"x":24,"y":376,"width":928,"height":392}'::jsonb, '{"title":"Export job result"}'::jsonb, '{"layoutIntent":"result_projection"}'::jsonb, 'mapped')
    RETURNING mapping_id
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id, source_object_id, node_id, capability_tag,
        wiring_kind, target_surface, target_ref, binding_json, status
    )
    VALUES
        ((SELECT preset_id FROM preset), 'export_job_submit_button', 'export_job_submit_button', 'requires_external_port_export_job', 'dispatchExternalPort', 'external_port', 'external-port:access_port:00000000-0000-0000-0000-000000000f01',
            $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:access_port:00000000-0000-0000-0000-000000000f01","payloadFrom":{"idempotency_key":"node:export_job_id_input.value","export_format":"node:export_format_input.value","period":"node:period_input.value","requested_by":"node:requested_by_input.value","file_name":"node:file_name_input.value","file_type":"node:file_type_input.value"},"outputProp":"exportJobResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Dispatches to file_storage access_port via portTargetRef. file_name and file_type align with af02 record_file_artifact step 1 required payload bindings (c009/c00a). Projection response: file_artifact_id / authorization_key (opaque reference) / file_name / file_type. No signed_url or credential in response."}$$::jsonb, 'pending')
    RETURNING wiring_candidate_id
)
INSERT INTO topology.mock_preset_compile_snapshot (
    preset_id, compiler_version, layout_patch_json,
    package_membership_candidate_json, wiring_candidate_json,
    style_candidate_json, unresolved_json
)
VALUES (
    (SELECT preset_id FROM preset),
    'file-storage-export-job-seed.v2',
    $${"nodes":[
      {"nodeId":"export_job_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","isDraftOnly":false,"slotKey":"root","orderIndex":0,"parentNodeId":null,"x":0,"y":0,"width":1024,"height":800,"propsJson":"{\"title\":\"Export Job\",\"description\":\"CRUD-derived export job surface. Configure portTargetRef payload mapping in PackageWiringEditor before apply.\"}"},
      {"nodeId":"export_job_id_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":1,"parentNodeId":"export_job_shell","x":24,"y":72,"width":456,"height":64,"propsJson":"{\"label\":\"Export job ID (idempotency key)\",\"required\":true}"},
      {"nodeId":"export_format_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":2,"parentNodeId":"export_job_shell","x":496,"y":72,"width":456,"height":64,"propsJson":"{\"label\":\"Export format\",\"required\":true,\"hint\":\"pdf / csv / json / zip\"}"},
      {"nodeId":"period_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":3,"parentNodeId":"export_job_shell","x":24,"y":152,"width":456,"height":64,"propsJson":"{\"label\":\"Period (YYYY-MM)\",\"required\":false}"},
      {"nodeId":"requested_by_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":4,"parentNodeId":"export_job_shell","x":496,"y":152,"width":456,"height":64,"propsJson":"{\"label\":\"Requested by\",\"required\":true}"},
      {"nodeId":"file_name_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":5,"parentNodeId":"export_job_shell","x":24,"y":232,"width":456,"height":64,"propsJson":"{\"label\":\"File name\",\"required\":true,\"hint\":\"e.g. export-2026-06.pdf\"}"},
      {"nodeId":"file_type_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":6,"parentNodeId":"export_job_shell","x":496,"y":232,"width":456,"height":64,"propsJson":"{\"label\":\"File type\",\"required\":true,\"hint\":\"pdf / csv / json / zip\"}"},
      {"nodeId":"export_job_submit_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":7,"parentNodeId":"export_job_shell","x":24,"y":312,"width":200,"height":48,"propsJson":"{\"label\":\"Start export job\",\"variant\":\"primary\"}"},
      {"nodeId":"export_job_result_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","isDraftOnly":false,"slotKey":"results","orderIndex":8,"parentNodeId":"export_job_shell","x":24,"y":376,"width":928,"height":392,"propsJson":"{\"title\":\"Export job result\"}","propBindings":{"data":{"source":"emission.data.exportJobResult"}}}
    ],"layoutClassRefs":[]}$$::jsonb,
    $${"activeTopologyWrite":false,"bindTarget":"selected_route_package_tmp_canvas_draft","derivedFrom":"physical_search_crud_aggregate.v1","requiresHumanAdjustmentBeforeApply":true}$$::jsonb,
    $$
    [
      {"nodeId":"export_job_submit_button","sourceObjectId":"export_job_submit_button","capabilityTag":"requires_external_port_export_job","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:access_port:00000000-0000-0000-0000-000000000f01","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:access_port:00000000-0000-0000-0000-000000000f01","payloadFrom":{"idempotency_key":"node:export_job_id_input.value","export_format":"node:export_format_input.value","period":"node:period_input.value","requested_by":"node:requested_by_input.value","file_name":"node:file_name_input.value","file_type":"node:file_type_input.value"},"outputProp":"exportJobResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}}
    ]
    $$::jsonb,
    $$[{"nodeId":"export_job_shell","styleIntent":"crud_derived_export_job_shell"},{"nodeId":"export_job_result_json","styleIntent":"result_projection_panel"}]$$::jsonb,
    $$[]$$::jsonb
);
