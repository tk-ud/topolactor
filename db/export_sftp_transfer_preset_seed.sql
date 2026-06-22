-- =============================================================================
-- export_sftp_transfer_preset_seed.sql
-- UIBuilder preset ecosystem seed data — export_sftp_transfer.v1 preset.
-- Derived from physical_search_crud_aggregate.v1 and dispatches through the
-- existing generic external_port response_port lane. No dedicated SFTP UI runtime,
-- route, provider client, credential, endpoint, host, user, key, or remote path is
-- introduced or projected.
-- =============================================================================

WITH upserted_preset AS (
    INSERT INTO topology.mock_preset_registry (
        preset_key, preset_label, source_kind, source_hash,
        source_snapshot_json, visual_tree_json, status
    ) VALUES (
        'export_sftp_transfer.v1',
        'Export SFTP Transfer preset seed',
        'ui_builder_canvas',
        'export_sftp_transfer.v1.seed.2026-06-22.r1',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "export_sftp_transfer.v1",
          "bundle": "export-sftp-port-consumer",
          "surface": "approved_export_job_sftp_transfer",
          "derivedFrom": "physical_search_crud_aggregate.v1",
          "componentImplementation": "none; existing CRUD/component catalog composition only",
          "runtimeMutationAuthority": "external port substrate via portTargetRef and generic policy steps",
          "credentialPlane": "external_port_substrate reference_key resolution only",
          "fileStorageAuthority": ["topology.export_jobs", "topology.file_artifacts", "topology.export_manifests", "topology.file_checksum_records"],
          "checksumBoundary": "manifest and checksum required before transfer; checksum_mismatch is explicit failure",
          "forbiddenProjectionFields": ["credential", "host", "user", "username", "private_key", "endpoint", "remote_path", "signed_url", "raw_provider_response"],
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts",
          "projectionResponseFields": ["transfer_status", "failure_reason", "checksum_before", "checksum_after", "manifest_ref", "retry_evidence_json"]
        }
        $$::jsonb,
        $$
        {"nodes":[
          {"nodeId":"sftp_transfer_shell","componentKey":"section.alias","componentKind":"disclosure_structure/section","parentNodeId":null},
          {"nodeId":"export_job_id_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"sftp_transfer_shell"},
          {"nodeId":"transfer_request_ref_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"sftp_transfer_shell"},
          {"nodeId":"requested_by_input","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"sftp_transfer_shell"},
          {"nodeId":"sftp_transfer_submit_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"sftp_transfer_shell"},
          {"nodeId":"sftp_transfer_result_json","componentKey":"json_viewer.template","componentKind":"data_display/json","parentNodeId":"sftp_transfer_shell"}
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
), preset AS (SELECT preset_id FROM upserted_preset),
cleanup_compile AS (DELETE FROM topology.mock_preset_compile_snapshot WHERE preset_id = (SELECT preset_id FROM preset)),
cleanup_wiring AS (DELETE FROM topology.mock_preset_wiring_candidate WHERE preset_id = (SELECT preset_id FROM preset)),
cleanup_mapping AS (DELETE FROM topology.mock_preset_object_mapping WHERE preset_id = (SELECT preset_id FROM preset)),
inserted_mappings AS (
    INSERT INTO topology.mock_preset_object_mapping (
        preset_id, source_object_id, node_id, node_kind, component_key, component_kind,
        parent_source_object_id, slot_key, order_index, bbox_json, text_json,
        style_candidate_json, mapping_status
    ) VALUES
        ((SELECT preset_id FROM preset), 'sftp_transfer_shell', 'sftp_transfer_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0, '{"x":0,"y":0,"width":960,"height":520}'::jsonb, '{"title":"SFTP transfer","description":"Transfer an approved export job package through the configured SFTP response port."}'::jsonb, '{"layout":"stack"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'export_job_id_input', 'export_job_id_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'sftp_transfer_shell', 'body', 1, '{"x":24,"y":80,"width":420,"height":56}'::jsonb, '{"label":"Export job ID","required":true}'::jsonb, '{}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'transfer_request_ref_input', 'transfer_request_ref_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'sftp_transfer_shell', 'body', 2, '{"x":24,"y":148,"width":420,"height":56}'::jsonb, '{"label":"Transfer request ref","required":true}'::jsonb, '{}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'requested_by_input', 'requested_by_input', 'catalog_component', 'form_field.template', 'form_input/form_field', 'sftp_transfer_shell', 'body', 3, '{"x":24,"y":216,"width":420,"height":56}'::jsonb, '{"label":"Requested by","required":true}'::jsonb, '{}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'sftp_transfer_submit_button', 'sftp_transfer_submit_button', 'catalog_component', 'button.primitive', 'action/button', 'sftp_transfer_shell', 'body', 4, '{"x":24,"y":288,"width":240,"height":48}'::jsonb, '{"label":"Transfer via SFTP"}'::jsonb, '{}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'sftp_transfer_result_json', 'sftp_transfer_result_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'sftp_transfer_shell', 'body', 5, '{"x":24,"y":356,"width":880,"height":132}'::jsonb, '{"title":"Transfer status projection"}'::jsonb, '{}'::jsonb, 'mapped')
    RETURNING 1
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id, node_id, source_object_id, capability_tag, wiring_kind,
        target_surface, target_ref, binding_json, status
    ) VALUES (
        (SELECT preset_id FROM preset),
        'sftp_transfer_submit_button',
        'sftp_transfer_submit_button',
        'requires_event_binding',
        'response_port',
        'external_port',
        'external-port:response_port:00000000-0000-0000-0000-000000000f08',
        $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f08","payloadFrom":{"export_job_id":"node:export_job_id_input.value","transfer_request_ref":"node:transfer_request_ref_input.value","requested_by":"node:requested_by_input.value"},"outputProp":"sftpTransferResult","projectionTableRef":"topology.sftp_transfer_log","forbiddenProjectionFields":["credential","host","user","username","private_key","endpoint","remote_path","signed_url","raw_provider_response"]}$$::jsonb,
        'confirmed'
    )
    RETURNING 1
)
INSERT INTO topology.mock_preset_compile_snapshot (
    preset_id, layout_patch_json, package_membership_candidate_json,
    wiring_candidate_json, style_candidate_json, unresolved_json, compile_status
) VALUES (
    (SELECT preset_id FROM preset),
    $${"nodes":[
      {"nodeId":"sftp_transfer_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","parentNodeId":null,"slotKey":"root","orderIndex":0},
      {"nodeId":"export_job_id_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"sftp_transfer_shell","slotKey":"body","orderIndex":1},
      {"nodeId":"transfer_request_ref_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"sftp_transfer_shell","slotKey":"body","orderIndex":2},
      {"nodeId":"requested_by_input","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"sftp_transfer_shell","slotKey":"body","orderIndex":3},
      {"nodeId":"sftp_transfer_submit_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"sftp_transfer_shell","slotKey":"body","orderIndex":4},
      {"nodeId":"sftp_transfer_result_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","parentNodeId":"sftp_transfer_shell","slotKey":"body","orderIndex":5,"propBindings":{"data":{"source":"emission.data.sftpTransferResult"}}}
    ]}$$::jsonb,
    $${"activeTopologyWrite":false,"activeTopology":false}$$::jsonb,
    $$[{"nodeId":"sftp_transfer_submit_button","sourceObjectId":"sftp_transfer_submit_button","capabilityTag":"requires_event_binding","wiringKind":"response_port","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f08","status":"confirmed","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f08","payloadFrom":{"export_job_id":"node:export_job_id_input.value","transfer_request_ref":"node:transfer_request_ref_input.value","requested_by":"node:requested_by_input.value"},"outputProp":"sftpTransferResult","projectionTableRef":"topology.sftp_transfer_log","forbiddenProjectionFields":["credential","host","user","username","private_key","endpoint","remote_path","signed_url","raw_provider_response"]}}]$$::jsonb,
    $$[]$$::jsonb,
    $$[]$$::jsonb,
    'compiled'
);
