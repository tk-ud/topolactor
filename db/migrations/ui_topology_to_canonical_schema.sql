-- =============================================================================
-- ui_topology_to_canonical_schema.sql
-- Migration: public schema UI topology tables to topology.* canonical schema.
--
-- APPLIES TO: existing DBs that have public.ui_component_bucket,
--   public.ui_topology_tensor, public.components, public.design, public.packages,
--   and related ui_* tables in the public schema.
--
-- CANONICAL NAME MAPPING (per docs/design/db-schema.yaml):
--   public.ui_component_bucket     -> topology.components_bucket
--   public.ui_component_registry   -> topology.ui_component_registry
--   public.ui_component_package    -> topology.ui_component_package
--   public.ui_package_component_map -> topology.ui_package_component_map
--   public.ui_layout_registry      -> topology.components_layout_design
--   public.ui_wiring_registry      -> topology.ui_wiring_registry
--   public.ui_topology_tensor      -> topology.ui_topology_tensor
--   public.components              -> topology.ui_builder_components
--   public.design                  -> topology.components_style_design
--   public.packages                -> topology.components_package_design
--
-- SAFE TO RUN: uses IF EXISTS guards; skips already-migrated tables.
-- NOTE: Run this AFTER applying ui_topology_tables.sql on the same DB (creates
--   new canonical tables if they don't exist). If tables already exist from
--   ui_topology_tables.sql, this migration moves data and drops old tables.
-- =============================================================================

BEGIN;

-- Helper: migrate table if old exists and new is empty
-- For each table: INSERT INTO new SELECT * FROM old, then DROP old.
-- If old doesn't exist, skip. If new already has rows, skip to avoid duplicate data.

DO $$
BEGIN
    -- topology.components_bucket <- public.ui_component_bucket
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ui_component_bucket')
       AND (SELECT COUNT(*) FROM topology.components_bucket) = 0
    THEN
        INSERT INTO topology.components_bucket
            (bucket_item_id, component_key, source_path, component_kind, status, metadata_json, created_at, updated_at)
        SELECT bucket_item_id, component_key, source_path, component_kind, status, metadata_json, created_at, updated_at
        FROM public.ui_component_bucket;
        RAISE NOTICE 'Migrated ui_component_bucket -> topology.components_bucket';
    END IF;

    -- topology.ui_component_registry <- public.ui_component_registry
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ui_component_registry')
       AND (SELECT COUNT(*) FROM topology.ui_component_registry) = 0
    THEN
        INSERT INTO topology.ui_component_registry
            (component_id, component_key, component_kind, source_path, props_schema_json, status, created_at, updated_at)
        SELECT component_id, component_key, component_kind, source_path, props_schema_json, status, created_at, updated_at
        FROM public.ui_component_registry;
        RAISE NOTICE 'Migrated ui_component_registry -> topology.ui_component_registry';
    END IF;

    -- topology.ui_component_package <- public.ui_component_package
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ui_component_package')
       AND (SELECT COUNT(*) FROM topology.ui_component_package) = 0
    THEN
        INSERT INTO topology.ui_component_package
            (package_id, package_key, package_kind, status, package_schema_json, created_at, updated_at)
        SELECT package_id, package_key, package_kind, status, package_schema_json, created_at, updated_at
        FROM public.ui_component_package;
        RAISE NOTICE 'Migrated ui_component_package -> topology.ui_component_package';
    END IF;

    -- topology.ui_package_component_map <- public.ui_package_component_map
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ui_package_component_map')
       AND (SELECT COUNT(*) FROM topology.ui_package_component_map) = 0
    THEN
        INSERT INTO topology.ui_package_component_map
            (map_id, package_id, component_id, slot_key, order_index, props_override_json, created_at)
        SELECT map_id, package_id, component_id, slot_key, order_index, props_override_json, created_at
        FROM public.ui_package_component_map;
        RAISE NOTICE 'Migrated ui_package_component_map -> topology.ui_package_component_map';
    END IF;

    -- topology.components_layout_design <- public.ui_layout_registry
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ui_layout_registry')
       AND (SELECT COUNT(*) FROM topology.components_layout_design) = 0
    THEN
        INSERT INTO topology.components_layout_design
            (layout_id, layout_key, layout_kind, layout_schema_json, css_token_refs, responsive_token_refs, status, created_at, updated_at)
        SELECT layout_id, layout_key, layout_kind, layout_schema_json, css_token_refs, responsive_token_refs, status, created_at, updated_at
        FROM public.ui_layout_registry;
        RAISE NOTICE 'Migrated ui_layout_registry -> topology.components_layout_design';
    END IF;

    -- topology.ui_wiring_registry <- public.ui_wiring_registry
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ui_wiring_registry')
       AND (SELECT COUNT(*) FROM topology.ui_wiring_registry) = 0
    THEN
        INSERT INTO topology.ui_wiring_registry
            (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status, created_at, updated_at)
        SELECT wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status, created_at, updated_at
        FROM public.ui_wiring_registry;
        RAISE NOTICE 'Migrated ui_wiring_registry -> topology.ui_wiring_registry';
    END IF;

    -- topology.ui_topology_tensor <- public.ui_topology_tensor
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ui_topology_tensor')
       AND (SELECT COUNT(*) FROM topology.ui_topology_tensor) = 0
    THEN
        INSERT INTO topology.ui_topology_tensor
            (tensor_id, route_key, package_id, layout_id, wiring_id, parent_tensor_id, slot_key, order_index,
             visibility_rule_json, layout_patch_json, css_token_refs, responsive_token_refs, state_policy_json,
             created_at, updated_at)
        SELECT tensor_id, route_key, package_id, layout_id, wiring_id, parent_tensor_id, slot_key, order_index,
               visibility_rule_json, layout_patch_json, css_token_refs, responsive_token_refs, state_policy_json,
               created_at, updated_at
        FROM public.ui_topology_tensor;
        RAISE NOTICE 'Migrated ui_topology_tensor -> topology.ui_topology_tensor';
    END IF;

    -- topology.ui_builder_components <- public.components
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'components')
       AND (SELECT COUNT(*) FROM topology.ui_builder_components) = 0
    THEN
        INSERT INTO topology.ui_builder_components
            (component_id, state, name, event, created_at, updated_at)
        SELECT component_id, state, name, event, created_at, updated_at
        FROM public.components;
        RAISE NOTICE 'Migrated public.components -> topology.ui_builder_components';
    END IF;

    -- topology.components_style_design <- public.design
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'design')
       AND (SELECT COUNT(*) FROM topology.components_style_design) = 0
    THEN
        INSERT INTO topology.components_style_design
            (design_id, name, design, created_at, updated_at)
        SELECT design_id, name, design, created_at, updated_at
        FROM public.design;
        RAISE NOTICE 'Migrated public.design -> topology.components_style_design';
    END IF;

    -- topology.components_package_design <- public.packages
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'packages')
       AND (SELECT COUNT(*) FROM topology.components_package_design) = 0
    THEN
        INSERT INTO topology.components_package_design
            (package_id, state, name, layout, created_at, updated_at)
        SELECT package_id, state, name, layout, created_at, updated_at
        FROM public.packages;
        RAISE NOTICE 'Migrated public.packages -> topology.components_package_design';
    END IF;

END;
$$;

-- Drop old public tables (only after successful data migration above)
-- Commented out by default — uncomment to execute destructive DROP after verifying data migration.
-- DROP TABLE IF EXISTS public.ui_topology_tensor CASCADE;
-- DROP TABLE IF EXISTS public.ui_package_component_map CASCADE;
-- DROP TABLE IF EXISTS public.ui_wiring_registry CASCADE;
-- DROP TABLE IF EXISTS public.ui_layout_registry CASCADE;
-- DROP TABLE IF EXISTS public.ui_component_package CASCADE;
-- DROP TABLE IF EXISTS public.ui_component_registry CASCADE;
-- DROP TABLE IF EXISTS public.ui_component_bucket CASCADE;
-- DROP TABLE IF EXISTS public.packages CASCADE;
-- DROP TABLE IF EXISTS public.design CASCADE;
-- DROP TABLE IF EXISTS public.components CASCADE;

COMMIT;
