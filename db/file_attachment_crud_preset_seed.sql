-- =============================================================================
-- file_attachment_crud_preset_seed.sql
-- Canonical: UIBuilder preset ecosystem seed data — file_attachment_crud.v1 preset registration.
-- Derived from physical_search_crud_aggregate.v1 and projects file_storage attachment operations.
-- No dedicated UI runtime/component or credential plane is introduced.
-- =============================================================================

WITH upserted_preset AS (
    INSERT INTO topology.mock_preset_registry (
        preset_key, preset_label, source_kind, source_hash,
        source_snapshot_json, visual_tree_json, status
    )
    VALUES (
        'file_attachment_crud.v1',
        'File Attachment CRUD preset seed',
        'ui_builder_canvas',
        'file_attachment_crud.v1.seed.2026-06-18',
        $$
        {
          "seedKind": "ui_builder_canvas_preset_seed",
          "presetKey": "file_attachment_crud.v1",
          "bundle": "file-storage-port-consumer",
          "surface": "record_file_attachment_bindings",
          "derivedFrom": "physical_search_crud_aggregate.v1",
          "componentImplementation": "none; existing CRUD/component catalog composition only",
          "runtimeMutationAuthority": "external port substrate via portTargetRef and execute_db_function policy steps",
          "credentialPlane": "external_port_substrate reference_key resolution only",
          "forbiddenProjectionFields": ["storage_ref", "authorization_key", "signed_url", "credential", "bucket", "endpoint"],
          "payloadFromResolver": "frontend/runtime/payloadFromResolver.ts"
        }
        $$::jsonb,
        $$
        {"nodes":[
          {"nodeId":"file_attach_shell","componentKey":"section.alias","componentKind":"disclosure_structure/section","parentNodeId":null},
          {"nodeId":"file_attach_record_id","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"file_attach_shell"},
          {"nodeId":"file_attach_artifact_id","componentKey":"form_field.template","componentKind":"form_input/form_field","parentNodeId":"file_attach_shell"},
          {"nodeId":"file_attach_upload_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"file_attach_shell"},
          {"nodeId":"file_attach_bind_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"file_attach_shell"},
          {"nodeId":"file_attach_list_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"file_attach_shell"},
          {"nodeId":"file_attach_unbind_button","componentKey":"button.primitive","componentKind":"action/button","parentNodeId":"file_attach_shell"},
          {"nodeId":"file_attach_results","componentKey":"card_list.primitive","componentKind":"display/card_list","parentNodeId":"file_attach_shell"},
          {"nodeId":"file_attach_debug_json","componentKey":"json_viewer.template","componentKind":"data_display/json","parentNodeId":"file_attach_shell"}
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
        ((SELECT preset_id FROM preset), 'file_attach_shell', 'file_attach_shell', 'catalog_component', 'section.alias', 'disclosure_structure/section', NULL, 'root', 0, '{"x":0,"y":0,"width":1024,"height":720}'::jsonb, '{"title":"File Attachments","description":"Attach existing file artifacts to the current record through external port dispatch."}'::jsonb, '{"layoutIntent":"crud_derived_shell"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_attach_record_id', 'file_attach_record_id', 'catalog_component', 'form_field.template', 'form_input/form_field', 'file_attach_shell', 'fields', 1, '{"x":24,"y":72,"width":456,"height":64}'::jsonb, '{"label":"Record ID","required":true}'::jsonb, '{"layoutIntent":"record_ref_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_attach_artifact_id', 'file_attach_artifact_id', 'catalog_component', 'form_field.template', 'form_input/form_field', 'file_attach_shell', 'fields', 2, '{"x":496,"y":72,"width":456,"height":64}'::jsonb, '{"label":"File artifact ID","required":true}'::jsonb, '{"layoutIntent":"artifact_ref_input"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_attach_upload_button', 'file_attach_upload_button', 'catalog_component', 'button.primitive', 'action/button', 'file_attach_shell', 'actions', 3, '{"x":24,"y":152,"width":160,"height":48}'::jsonb, '{"label":"Create artifact","variant":"primary"}'::jsonb, '{"layoutIntent":"external_port_upload"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_attach_bind_button', 'file_attach_bind_button', 'catalog_component', 'button.primitive', 'action/button', 'file_attach_shell', 'actions', 4, '{"x":200,"y":152,"width":160,"height":48}'::jsonb, '{"label":"Bind file"}'::jsonb, '{"layoutIntent":"bind_attachment"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_attach_list_button', 'file_attach_list_button', 'catalog_component', 'button.primitive', 'action/button', 'file_attach_shell', 'actions', 5, '{"x":376,"y":152,"width":160,"height":48}'::jsonb, '{"label":"List files"}'::jsonb, '{"layoutIntent":"list_attachments"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_attach_unbind_button', 'file_attach_unbind_button', 'catalog_component', 'button.primitive', 'action/button', 'file_attach_shell', 'actions', 6, '{"x":552,"y":152,"width":160,"height":48}'::jsonb, '{"label":"Unbind file"}'::jsonb, '{"layoutIntent":"unbind_attachment"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_attach_results', 'file_attach_results', 'catalog_component', 'card_list.primitive', 'display/card_list', 'file_attach_shell', 'results', 7, '{"x":24,"y":224,"width":928,"height":320}'::jsonb, '{"emptyText":"No attached files."}'::jsonb, '{"layoutIntent":"attachment_cards"}'::jsonb, 'mapped'),
        ((SELECT preset_id FROM preset), 'file_attach_debug_json', 'file_attach_debug_json', 'catalog_component', 'json_viewer.template', 'data_display/json', 'file_attach_shell', 'debug', 8, '{"x":24,"y":568,"width":928,"height":120}'::jsonb, '{"title":"Attachment emission"}'::jsonb, '{"layoutIntent":"debug_projection"}'::jsonb, 'mapped')
    RETURNING mapping_id
), inserted_wiring AS (
    INSERT INTO topology.mock_preset_wiring_candidate (
        preset_id, source_object_id, node_id, capability_tag,
        wiring_kind, target_surface, target_ref, binding_json, status
    )
    VALUES
        ((SELECT preset_id FROM preset), 'file_attach_upload_button', 'file_attach_upload_button', 'requires_external_port_upload', 'dispatchExternalPort', 'external_port', 'external-port:access_port:00000000-0000-0000-0000-000000000f01',
            $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:access_port:00000000-0000-0000-0000-000000000f01","payloadFrom":{"record_id":"node:file_attach_record_id.value","file_name":"node:file_attach_artifact_id.value"},"payloadDefaults":{"record_table_ref":"author-selected-manifest-record","file_type":"json"},"outputProp":"fileAttachmentUploadResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts","note":"Uses existing file_storage access_port credential reference_key lane; author may adjust payload mapping after preset load."}$$::jsonb, 'pending'),
        ((SELECT preset_id FROM preset), 'file_attach_bind_button', 'file_attach_bind_button', 'requires_external_port_bind', 'dispatchExternalPort', 'external_port', 'external-port:response_port:00000000-0000-0000-0000-000000000f0a',
            $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0a","payloadFrom":{"record_id":"node:file_attach_record_id.value","file_artifact_id":"node:file_attach_artifact_id.value"},"payloadDefaults":{"record_table_ref":"author-selected-manifest-record","relation_kind":"attachment","function":"topology.fs_bind_record_file_attachment"},"outputProp":"fileAttachmentBindResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}$$::jsonb, 'pending'),
        ((SELECT preset_id FROM preset), 'file_attach_list_button', 'file_attach_list_button', 'requires_external_port_list', 'dispatchExternalPort', 'external_port', 'external-port:response_port:00000000-0000-0000-0000-000000000f0b',
            $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0b","payloadFrom":{"record_id":"node:file_attach_record_id.value"},"payloadDefaults":{"record_table_ref":"author-selected-manifest-record","function":"topology.fs_list_record_file_attachments"},"outputProp":"fileAttachmentListResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}$$::jsonb, 'pending'),
        ((SELECT preset_id FROM preset), 'file_attach_unbind_button', 'file_attach_unbind_button', 'requires_external_port_unbind', 'dispatchExternalPort', 'external_port', 'external-port:response_port:00000000-0000-0000-0000-000000000f0c',
            $${"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0c","payloadFrom":{"record_id":"node:file_attach_record_id.value","file_artifact_id":"node:file_attach_artifact_id.value"},"payloadDefaults":{"record_table_ref":"author-selected-manifest-record","relation_kind":"attachment","function":"topology.fs_unbind_record_file_attachment"},"outputProp":"fileAttachmentUnbindResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}$$::jsonb, 'pending')
    RETURNING wiring_candidate_id
)
INSERT INTO topology.mock_preset_compile_snapshot (
    preset_id, compiler_version, layout_patch_json,
    package_membership_candidate_json, wiring_candidate_json,
    style_candidate_json, unresolved_json
)
VALUES (
    (SELECT preset_id FROM preset),
    'file-attachment-crud-seed.v1',
    $${"nodes":[
      {"nodeId":"file_attach_shell","nodeKind":"catalog_component","componentKey":"section.alias","componentKind":"disclosure_structure/section","isDraftOnly":false,"slotKey":"root","orderIndex":0,"parentNodeId":null,"x":0,"y":0,"width":1024,"height":720,"propsJson":"{\"title\":\"File Attachments\",\"description\":\"CRUD-derived attachment surface. Configure record_table_ref and portTargetRef in PackageWiringEditor before apply.\"}"},
      {"nodeId":"file_attach_record_id","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":1,"parentNodeId":"file_attach_shell","x":24,"y":72,"width":456,"height":64,"propsJson":"{\"label\":\"Record ID\",\"required\":true}"},
      {"nodeId":"file_attach_artifact_id","nodeKind":"catalog_component","componentKey":"form_field.template","componentKind":"form_input/form_field","isDraftOnly":false,"slotKey":"fields","orderIndex":2,"parentNodeId":"file_attach_shell","x":496,"y":72,"width":456,"height":64,"propsJson":"{\"label\":\"File artifact ID / file name\",\"required\":true}"},
      {"nodeId":"file_attach_upload_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":3,"parentNodeId":"file_attach_shell","x":24,"y":152,"width":160,"height":48,"propsJson":"{\"label\":\"Create artifact\",\"variant\":\"primary\"}"},
      {"nodeId":"file_attach_bind_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":4,"parentNodeId":"file_attach_shell","x":200,"y":152,"width":160,"height":48,"propsJson":"{\"label\":\"Bind file\"}"},
      {"nodeId":"file_attach_list_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":5,"parentNodeId":"file_attach_shell","x":376,"y":152,"width":160,"height":48,"propsJson":"{\"label\":\"List files\"}"},
      {"nodeId":"file_attach_unbind_button","nodeKind":"catalog_component","componentKey":"button.primitive","componentKind":"action/button","isDraftOnly":false,"slotKey":"actions","orderIndex":6,"parentNodeId":"file_attach_shell","x":552,"y":152,"width":160,"height":48,"propsJson":"{\"label\":\"Unbind file\"}"},
      {"nodeId":"file_attach_results","nodeKind":"catalog_component","componentKey":"card_list.primitive","componentKind":"display/card_list","isDraftOnly":false,"slotKey":"results","orderIndex":7,"parentNodeId":"file_attach_shell","x":24,"y":224,"width":928,"height":320,"propsJson":"{\"emptyText\":\"No attached files.\"}","propBindings":{"items":{"source":"emission.data.fileAttachmentListResult.attachments"}}},
      {"nodeId":"file_attach_debug_json","nodeKind":"catalog_component","componentKey":"json_viewer.template","componentKind":"data_display/json","isDraftOnly":false,"slotKey":"debug","orderIndex":8,"parentNodeId":"file_attach_shell","x":24,"y":568,"width":928,"height":120,"propsJson":"{\"title\":\"Attachment emission\"}","propBindings":{"data":{"source":"emission.data"}}}
    ],"layoutClassRefs":[]}$$::jsonb,
    $${"activeTopologyWrite":false,"bindTarget":"selected_route_package_tmp_canvas_draft","derivedFrom":"physical_search_crud_aggregate.v1","requiresHumanAdjustmentBeforeApply":true}$$::jsonb,
    $$
    [
      {"nodeId":"file_attach_upload_button","sourceObjectId":"file_attach_upload_button","capabilityTag":"requires_external_port_upload","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:access_port:00000000-0000-0000-0000-000000000f01","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:access_port:00000000-0000-0000-0000-000000000f01","payloadFrom":{"record_id":"node:file_attach_record_id.value","file_name":"node:file_attach_artifact_id.value"},"payloadDefaults":{"record_table_ref":"author-selected-manifest-record","file_type":"json"},"outputProp":"fileAttachmentUploadResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"file_attach_bind_button","sourceObjectId":"file_attach_bind_button","capabilityTag":"requires_external_port_bind","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0a","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0a","payloadFrom":{"record_id":"node:file_attach_record_id.value","file_artifact_id":"node:file_attach_artifact_id.value"},"payloadDefaults":{"record_table_ref":"author-selected-manifest-record","relation_kind":"attachment"},"outputProp":"fileAttachmentBindResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"file_attach_list_button","sourceObjectId":"file_attach_list_button","capabilityTag":"requires_external_port_list","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0b","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0b","payloadFrom":{"record_id":"node:file_attach_record_id.value"},"payloadDefaults":{"record_table_ref":"author-selected-manifest-record"},"outputProp":"fileAttachmentListResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}},
      {"nodeId":"file_attach_unbind_button","sourceObjectId":"file_attach_unbind_button","capabilityTag":"requires_external_port_unbind","wiringKind":"dispatchExternalPort","targetSurface":"external_port","targetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0c","status":"pending","binding":{"event":"click","actionType":"dispatchExternalPort","portTargetRef":"external-port:response_port:00000000-0000-0000-0000-000000000f0c","payloadFrom":{"record_id":"node:file_attach_record_id.value","file_artifact_id":"node:file_attach_artifact_id.value"},"payloadDefaults":{"record_table_ref":"author-selected-manifest-record","relation_kind":"attachment"},"outputProp":"fileAttachmentUnbindResult","payloadResolverRef":"frontend/runtime/payloadFromResolver.ts"}}
    ]
    $$::jsonb,
    $$[{"nodeId":"file_attach_shell","styleIntent":"crud_derived_attachment_shell"},{"nodeId":"file_attach_results","styleIntent":"result_panel"}]$$::jsonb,
    $$[{"nodeId":"file_attach_shell","reason":"record_table_ref remains author-selected manifest record; seed does not invent a target business record table","knownGapRef":"author_selects_record_manifest_before_apply","status":"pending_author_selection_via_package_wiring_editor"}]$$::jsonb
);
