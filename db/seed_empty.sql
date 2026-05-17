-- =============================================================================
-- seed_empty.sql
-- Empty/default topology seed for the topolactor database.
--
-- PURPOSE:
--   Inserts the minimum set of well-known default rows needed to bootstrap
--   the topology space. Contains NO real business data — only structural
--   defaults that the backend runtime and topology tables reference.
--
-- SCOPE:
--   Real business data (domains, products, users, workflows, etc.) is
--   out of scope for this seed. That data is introduced through the
--   canonical flow: user_operation → operation_vector → attractor_resolve
--   → structure_map_resolve → … → emission_or_projection.
--
-- HOW TO RUN (after schema.sql):
--   psql -d <database> -f db/seed_empty.sql
-- =============================================================================


-- ---------------------------------------------------------------------------
-- state_registry defaults
-- Two baseline states are seeded:
--   system / 'active'     — the default system-managed operational state.
--   business / 'operating' — the default business-managed operational state.
-- All hubs and entities that do not yet have a specific state assigned will
-- implicitly relate to one of these.
-- ---------------------------------------------------------------------------
INSERT INTO state_registry (state_id, name, owner)
VALUES
    -- system 'active': the canonical "live and operational" state managed
    -- by the abstract backend runtime.
    (gen_random_uuid(), 'active',    'system'),

    -- business 'operating': the canonical "business-layer operational" state
    -- managed by the product/business logic layer.
    (gen_random_uuid(), 'operating', 'business')
ON CONFLICT DO NOTHING;


-- ---------------------------------------------------------------------------
-- relation_registry defaults
-- One baseline relation named 'default' is seeded.
-- This serves as the fallback relation_registry reference for hubs,
-- hub_relations, and structure_maps that are not yet bound to a specific
-- named relation.
-- ---------------------------------------------------------------------------
INSERT INTO relation_registry (
    relation_registry_id,
    name,
    master_ids,
    category,
    type,
    "order",
    weight,
    manifest_candidate,
    active
)
VALUES (
    gen_random_uuid(),
    'default',      -- fallback relation for unbound topology references
    '{}',           -- no master_registry linkage in default seed
    'default',
    'structural',
    0,
    1.0,
    false,
    true
)
ON CONFLICT DO NOTHING;


-- ---------------------------------------------------------------------------
-- package_registry defaults
-- One baseline package named 'default_package' is seeded.
-- Acts as the fallback package resolved during package_resolve when no
-- specific package_id is set on a structure_map.
-- ---------------------------------------------------------------------------
INSERT INTO package_registry (
    package_id,
    name,
    type,
    package_def,
    active
)
VALUES (
    gen_random_uuid(),
    'default_package',  -- fallback package for unbound structure_map references
    'core',
    '{}',               -- empty definition; no real package config in seed
    true
)
ON CONFLICT DO NOTHING;


-- ---------------------------------------------------------------------------
-- schema_registry defaults
-- One baseline schema named 'default_schema' is seeded.
-- Acts as the fallback schema resolved during schema_resolve when no
-- specific schema_id is set on a structure_map.
-- ---------------------------------------------------------------------------
INSERT INTO schema_registry (
    schema_id,
    name,
    schema_def,
    active
)
VALUES (
    gen_random_uuid(),
    'default_schema',   -- fallback schema for unbound structure_map references
    '{}',               -- empty definition; no real schema shape in seed
    true
)
ON CONFLICT DO NOTHING;
