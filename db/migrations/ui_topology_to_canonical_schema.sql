-- =============================================================================
-- ui_topology_to_canonical_schema.sql
-- Migration: public schema UI topology tables -> topology.* canonical schema.
--
-- APPLIES TO: existing DBs that have public.ui_component_bucket,
--   public.ui_topology_tensor, public.components, public.design, public.packages,
--   and related ui_* public tables from before the canonical schema migration.
--   Safe to run on fresh DBs (IF EXISTS guards skip missing old tables; DROP IF EXISTS
--   is a no-op when old tables are absent).
--
-- CANONICAL NAME MAPPING (per docs/design/db-schema.yaml):
--   public.ui_component_bucket      -> topology.components_bucket
--   public.ui_component_registry    -> topology.ui_component_registry
--   public.ui_component_package     -> topology.ui_component_package
--   public.ui_package_component_map -> topology.ui_package_component_map
--   public.ui_layout_registry       -> topology.components_layout_design
--   public.ui_wiring_registry       -> topology.ui_wiring_registry
--   public.ui_topology_tensor       -> topology.ui_topology_tensor
--   public.components               -> topology.ui_builder_components
--   public.design                   -> topology.components_style_design
--   public.packages                 -> topology.components_package_design
--
-- UPSERT POLICY:
--   Each INSERT uses ON CONFLICT on the business-key unique constraint with DO NOTHING.
--   Rows already present in the canonical table (by business key) are preserved as-is;
--   duplicate public rows are silently skipped.  This is correct: canonical-table rows
--   take precedence over old public rows with the same business key.
--
-- RETIREMENT POLICY:
--   After data copy, old public tables are DROPPED in reverse dependency order.
--   CASCADE is used to drop dependent FK constraints automatically.
--   Dropping on a fresh DB (where old tables were never created) is a safe no-op via
--   DROP TABLE IF EXISTS.
--
-- IDEMPOTENT: IF EXISTS guards on all reads; ON CONFLICT DO NOTHING on all inserts;
--   DROP IF EXISTS on all drops.  Re-running is safe.
-- =============================================================================

BEGIN;

DO $$
DECLARE
    v_count integer;
BEGIN
    -------------------------------------------------------------------------
    -- topology.components_bucket <- public.ui_component_bucket
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_component_bucket'
    ) THEN
        INSERT INTO topology.components_bucket
            (bucket_item_id, component_key, source_path, component_kind, status, metadata_json, created_at, updated_at)
        SELECT bucket_item_id, component_key, source_path, component_kind, status, metadata_json, created_at, updated_at
        FROM public.ui_component_bucket
        ON CONFLICT (component_key, source_path) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.components_bucket: inserted % row(s) from public.ui_component_bucket (conflicts skipped)', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.ui_component_registry <- public.ui_component_registry
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_component_registry'
    ) THEN
        INSERT INTO topology.ui_component_registry
            (component_id, component_key, component_kind, source_path, props_schema_json, status, created_at, updated_at)
        SELECT component_id, component_key, component_kind, source_path, props_schema_json, status, created_at, updated_at
        FROM public.ui_component_registry
        ON CONFLICT (component_key) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_component_registry: inserted % row(s) from public.ui_component_registry', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.ui_component_package <- public.ui_component_package
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_component_package'
    ) THEN
        INSERT INTO topology.ui_component_package
            (package_id, package_key, package_kind, status, package_schema_json, created_at, updated_at)
        SELECT package_id, package_key, package_kind, status, package_schema_json, created_at, updated_at
        FROM public.ui_component_package
        ON CONFLICT (package_key) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_component_package: inserted % row(s) from public.ui_component_package', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.ui_package_component_map <- public.ui_package_component_map
    -- Note: ON CONFLICT on PK (map_id) because the UNIQUE constraint includes
    -- nullable slot_key; PostgreSQL ON CONFLICT cannot reliably handle NULL in
    -- composite unique targets.  PK conflict covers idempotency on re-runs.
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_package_component_map'
    ) THEN
        INSERT INTO topology.ui_package_component_map
            (map_id, package_id, component_id, slot_key, order_index, props_override_json, created_at)
        SELECT map_id, package_id, component_id, slot_key, order_index, props_override_json, created_at
        FROM public.ui_package_component_map
        ON CONFLICT (map_id) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_package_component_map: inserted % row(s) from public.ui_package_component_map', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.components_layout_design <- public.ui_layout_registry
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_layout_registry'
    ) THEN
        INSERT INTO topology.components_layout_design
            (layout_id, layout_key, layout_kind, layout_schema_json, css_token_refs, responsive_token_refs, status, created_at, updated_at)
        SELECT layout_id, layout_key, layout_kind, layout_schema_json, css_token_refs, responsive_token_refs, status, created_at, updated_at
        FROM public.ui_layout_registry
        ON CONFLICT (layout_key) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.components_layout_design: inserted % row(s) from public.ui_layout_registry', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.ui_wiring_registry <- public.ui_wiring_registry
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_wiring_registry'
    ) THEN
        INSERT INTO topology.ui_wiring_registry
            (wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status, created_at, updated_at)
        SELECT wiring_id, wiring_key, wiring_kind, target_surface, target_ref, wiring_schema_json, status, created_at, updated_at
        FROM public.ui_wiring_registry
        ON CONFLICT (wiring_key) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_wiring_registry: inserted % row(s) from public.ui_wiring_registry', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.ui_topology_tensor <- public.ui_topology_tensor
    -- Note: ON CONFLICT on PK (tensor_id) because the UNIQUE constraint includes
    -- nullable slot_key; same reasoning as ui_package_component_map above.
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_topology_tensor'
    ) THEN
        INSERT INTO topology.ui_topology_tensor
            (tensor_id, route_key, package_id, layout_id, wiring_id, parent_tensor_id, slot_key, order_index,
             visibility_rule_json, layout_patch_json, css_token_refs, responsive_token_refs, state_policy_json,
             created_at, updated_at)
        SELECT tensor_id, route_key, package_id, layout_id, wiring_id, parent_tensor_id, slot_key, order_index,
               visibility_rule_json, layout_patch_json, css_token_refs, responsive_token_refs, state_policy_json,
               created_at, updated_at
        FROM public.ui_topology_tensor
        ON CONFLICT (tensor_id) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_topology_tensor: inserted % row(s) from public.ui_topology_tensor', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.ui_builder_components <- public.components
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'components'
    ) THEN
        INSERT INTO topology.ui_builder_components
            (component_id, state, name, event, created_at, updated_at)
        SELECT component_id, state, name, event, created_at, updated_at
        FROM public.components
        ON CONFLICT (name) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_builder_components: inserted % row(s) from public.components', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.components_style_design <- public.design
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'design'
    ) THEN
        INSERT INTO topology.components_style_design
            (design_id, name, design, created_at, updated_at)
        SELECT design_id, name, design, created_at, updated_at
        FROM public.design
        ON CONFLICT (name) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.components_style_design: inserted % row(s) from public.design', v_count;
    END IF;

    -------------------------------------------------------------------------
    -- topology.components_package_design <- public.packages
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'packages'
    ) THEN
        INSERT INTO topology.components_package_design
            (package_id, state, name, layout, created_at, updated_at)
        SELECT package_id, state, name, layout, created_at, updated_at
        FROM public.packages
        ON CONFLICT (name) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.components_package_design: inserted % row(s) from public.packages', v_count;
    END IF;

END;
$$;

-- =============================================================================
-- Retire old public tables.
--
-- Dropped in reverse dependency order (most-dependent first) with CASCADE to
-- automatically remove any remaining FK constraints pointing to these tables.
-- DROP IF EXISTS is a no-op on fresh DBs where old tables were never created.
--
-- Dependency order for old public tables:
--   public.ui_topology_tensor       references ui_component_package, ui_layout_registry, ui_wiring_registry
--   public.ui_package_component_map references ui_component_package, ui_component_registry
--   public.ui_wiring_registry       (leaf)
--   public.ui_layout_registry       (leaf)
--   public.ui_component_package     (leaf after map/tensor dropped)
--   public.ui_component_registry    (leaf after map dropped)
--   public.ui_component_bucket      (leaf)
--   public.packages                 (leaf)
--   public.design                   (leaf)
--   public.components               (leaf)
-- =============================================================================

DROP TABLE IF EXISTS public.ui_topology_tensor CASCADE;
DROP TABLE IF EXISTS public.ui_package_component_map CASCADE;
DROP TABLE IF EXISTS public.ui_wiring_registry CASCADE;
DROP TABLE IF EXISTS public.ui_layout_registry CASCADE;
DROP TABLE IF EXISTS public.ui_component_package CASCADE;
DROP TABLE IF EXISTS public.ui_component_registry CASCADE;
DROP TABLE IF EXISTS public.ui_component_bucket CASCADE;
DROP TABLE IF EXISTS public.packages CASCADE;
DROP TABLE IF EXISTS public.design CASCADE;
DROP TABLE IF EXISTS public.components CASCADE;

COMMIT;
