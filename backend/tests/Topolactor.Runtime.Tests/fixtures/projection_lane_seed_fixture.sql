-- =============================================================================
-- projection_lane_seed_fixture.sql
-- Dedicated, canonical-proof-owned representative fixture for the projection_seed_collapse
-- lane test (frontend_payload -> dispatch -> runtime -> db_notify -> sse -> frontend, per
-- docs/design/pipeline-continuity-ssot.yaml test_tier_policy representative_route).
--
-- This is NOT applied by db/init.sql and is NOT canonical business/topology data — it is a
-- test-only fixture, decoupled from db/demo_seed.sql (which is public scaffold/demo-only data
-- and is intentionally excluded from the canonical bootstrap chain). Moved out of
-- db/demo_seed.sql so this proof does not depend on a non-canonical file that may change or be
-- removed independent of proof needs.
--
-- Manifest id and topology entries are copied verbatim from the former db/demo_seed.sql manifest
-- '00000000-0000-0000-0000-000000000085' tuple (dispatcher_mapping target=demo/layer=screen_list/
-- action=Read, tableRef=seed.projection_lane) so the proof's assertions are unchanged.
-- =============================================================================

INSERT INTO manifest (manifest_id, relation_registry_id, topology, status)
VALUES
    (
        '00000000-0000-0000-0000-000000000085',
        NULL,
        ARRAY[
            '{"type":"dispatcher_mapping","role":"admin","target":"demo","layer":"screen_list","action":"Read"}'::jsonb,
            '{"type":"db_notify_projection_mapping","runtime_destination":"sse_projection_runtime"}'::jsonb,
            '{"type":"runtime_mapping","runtime_destination":"topology_transform_runtime"}'::jsonb,
            '{"type":"projection_constructor_mapping","projection_definition":{"constructorKey":"seed-projection-lane","packageIds":["00000000-0000-0000-0000-000000000001"],"outputKind":"form_inputs","inputMapping":"single_row","fieldDefs":[{"key":"seedLabel","label":"Seed label","kind":"text","required":true}]}}'::jsonb,
            '{"type":"screen_data_shape","contentsType":"runtime_seed","tableRef":"seed.projection_lane","initialDataRows":[{"values":{"seedLabel":"projection-lane-seed"}}],"displayColumnMode":"all"}'::jsonb
        ]::jsonb[],
        'active'
    )
ON CONFLICT (manifest_id) DO NOTHING;

-- Physical table + package wiring seed for tableRef=seed.projection_lane, copied verbatim from
-- the former db/demo_seed.sql "projection lane seed hardening fixture" block — needed by the
-- DB-free static test asserting screen_data_shape.tableRef has physical table + wiring seed.
INSERT INTO topology.physical_tables (table_ref, schema_name, category, active)
VALUES ('seed.projection_lane', 'seed', 'projection_lane_test', true)
ON CONFLICT (table_ref) DO NOTHING;

INSERT INTO topology.wiring_physical_to_package (physical_table_id, package_id, wiring_def, active)
SELECT physical_table_id,
       '00000000-0000-0000-0000-000000000001'::uuid,
       '{"seedContract":"projection-lane-seed-test-hardening"}'::jsonb,
       true
FROM topology.physical_tables
WHERE table_ref = 'seed.projection_lane'
ON CONFLICT DO NOTHING;
