-- =============================================================================
-- auth_seed.sql
-- Demo credentials in auth.* (canonical). Applied after auth_tables.sql.
-- Passwords (demo only): demo_public_password, demo_admin_password
-- =============================================================================

INSERT INTO auth.roles (role_name, description) VALUES
    ('user', 'Standard application user'),
    ('admin', 'Registrar admin console operator')
ON CONFLICT (role_name) DO NOTHING;

INSERT INTO auth.scopes (scope_name, description) VALUES
    ('user_app', 'User-facing application'),
    ('admin_console', 'Registrar admin console')
ON CONFLICT (scope_name) DO NOTHING;

-- demo_public — user realm
INSERT INTO auth.users (user_id, username, active, approve, status)
VALUES ('00000000-0000-0000-0000-0000000000a1', 'demo_public', true, true, 'active')
ON CONFLICT (username) DO UPDATE SET approve = EXCLUDED.approve, status = EXCLUDED.status;

INSERT INTO auth.credentials (user_id, password_hash)
VALUES (
    '00000000-0000-0000-0000-0000000000a1',
    '$2b$12$8FMsfslui6GEEmi7YRb8UeuH1IOJcye0R3s/4fSYHmYJIzHrOwKo.'
)
ON CONFLICT (user_id) DO UPDATE SET password_hash = EXCLUDED.password_hash;

INSERT INTO auth.grants (user_id, role_name, realm)
VALUES ('00000000-0000-0000-0000-0000000000a1', 'user', 'user')
ON CONFLICT (user_id, role_name, realm) DO NOTHING;

-- demo_admin — admin realm
INSERT INTO auth.users (user_id, username, active, approve, status)
VALUES ('00000000-0000-0000-0000-0000000000a2', 'demo_admin', true, true, 'active')
ON CONFLICT (username) DO UPDATE SET approve = EXCLUDED.approve, status = EXCLUDED.status;

INSERT INTO auth.credentials (user_id, password_hash)
VALUES (
    '00000000-0000-0000-0000-0000000000a2',
    '$2b$12$E5kCP8.xxEW.yYdCow49DebJ1sRmjx5ihWTnhJ32iViIoP7Seclx2'
)
ON CONFLICT (user_id) DO UPDATE SET password_hash = EXCLUDED.password_hash;

INSERT INTO auth.grants (user_id, role_name, realm)
VALUES ('00000000-0000-0000-0000-0000000000a2', 'admin', 'admin/system')
ON CONFLICT (user_id, role_name, realm) DO NOTHING;
