-- =============================================================================
-- enum_seed.sql
-- Minimal demo enum dictionary for admin Step 2/3 select regression.
-- SSOT: docs/design/enum-dictionary-ssot.yaml
-- =============================================================================

INSERT INTO enum.items (enum_id, index_num, name) VALUES
    ('11111111-1111-1111-1111-111111111101'::uuid, 1, 'demo_active'),
    ('11111111-1111-1111-1111-111111111102'::uuid, 2, 'demo_inactive'),
    ('11111111-1111-1111-1111-111111111103'::uuid, 3, 'demo_pending')
ON CONFLICT (enum_id) DO NOTHING;

INSERT INTO enum.groups (group_id, index_num, group_name) VALUES
    ('22222222-2222-2222-2222-222222222201'::uuid, 1, 'demo_status')
ON CONFLICT (group_id) DO NOTHING;

INSERT INTO enum.group_items (group_id, position, enum_index_num) VALUES
    ('22222222-2222-2222-2222-222222222201'::uuid, 0, 1),
    ('22222222-2222-2222-2222-222222222201'::uuid, 1, 2),
    ('22222222-2222-2222-2222-222222222201'::uuid, 2, 3)
ON CONFLICT (group_id, position) DO NOTHING;

-- user_status — auth.users.status select (admin-master-roster-management-ssot)
INSERT INTO enum.items (enum_id, index_num, name) VALUES
    ('31111111-1111-1111-1111-311111111101'::uuid, 10, 'active'),
    ('31111111-1111-1111-1111-311111111102'::uuid, 11, 'inactive'),
    ('31111111-1111-1111-1111-311111111103'::uuid, 12, 'suspended'),
    ('31111111-1111-1111-1111-311111111104'::uuid, 13, 'archived')
ON CONFLICT (enum_id) DO NOTHING;

INSERT INTO enum.groups (group_id, index_num, group_name) VALUES
    ('33333333-3333-3333-3333-333333333301'::uuid, 2, 'user_status')
ON CONFLICT (group_id) DO NOTHING;

INSERT INTO enum.group_items (group_id, position, enum_index_num) VALUES
    ('33333333-3333-3333-3333-333333333301'::uuid, 0, 10),
    ('33333333-3333-3333-3333-333333333301'::uuid, 1, 11),
    ('33333333-3333-3333-3333-333333333301'::uuid, 2, 12),
    ('33333333-3333-3333-3333-333333333301'::uuid, 3, 13)
ON CONFLICT (group_id, position) DO NOTHING;
