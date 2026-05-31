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
--   Parent tables: ON CONFLICT on business-key unique constraint with DO NOTHING.
--   Canonical rows take precedence; duplicate public rows are silently skipped.
--
--   Child tables (ui_package_component_map, ui_topology_tensor):
--   Parent IDs are resolved from old public UUIDs to canonical UUIDs via business
--   key join before INSERT.  This handles the case where a canonical parent row has
--   the same business key but a different UUID than the old public parent row.
--   Unresolvable references (old parent UUID has no matching canonical business key)
--   raise RAISE EXCEPTION — no silent skip.
--   ui_topology_tensor.parent_tensor_id is resolved in a second UPDATE pass via
--   direct UUID match; unresolvable self-references are left NULL (FK ON DELETE SET NULL).
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
    v_count    integer;
    v_bad_refs integer;
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
    --
    -- Parent IDs (package_id, component_id) are remapped to canonical UUIDs via
    -- business key join.  When a parent's public UUID was displaced by a canonical
    -- row with the same business key, the canonical UUID is used instead.
    -- Unresolvable references raise an explicit exception (no silent skip).
    -- ON CONFLICT (map_id) covers idempotency on re-runs.
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_package_component_map'
    ) THEN
        -- Validate: all package_id references must resolve via business key
        SELECT COUNT(*) INTO v_bad_refs
        FROM public.ui_package_component_map m
        WHERE NOT EXISTS (
            SELECT 1
            FROM public.ui_component_package pp
            JOIN topology.ui_component_package cp ON cp.package_key = pp.package_key
            WHERE pp.package_id = m.package_id
        );
        IF v_bad_refs > 0 THEN
            RAISE EXCEPTION
                'ui_topology_to_canonical_schema: % ui_package_component_map row(s) have '
                'package_id values that cannot be resolved to a canonical ui_component_package row. '
                'Ensure ui_component_package migration completed successfully before retrying.',
                v_bad_refs;
        END IF;

        -- Validate: all component_id references must resolve via business key
        SELECT COUNT(*) INTO v_bad_refs
        FROM public.ui_package_component_map m
        WHERE NOT EXISTS (
            SELECT 1
            FROM public.ui_component_registry pr
            JOIN topology.ui_component_registry cr ON cr.component_key = pr.component_key
            WHERE pr.component_id = m.component_id
        );
        IF v_bad_refs > 0 THEN
            RAISE EXCEPTION
                'ui_topology_to_canonical_schema: % ui_package_component_map row(s) have '
                'component_id values that cannot be resolved to a canonical ui_component_registry row. '
                'Ensure ui_component_registry migration completed successfully before retrying.',
                v_bad_refs;
        END IF;

        INSERT INTO topology.ui_package_component_map
            (map_id, package_id, component_id, slot_key, order_index, props_override_json, created_at)
        SELECT
            m.map_id,
            cp.package_id,
            cr.component_id,
            m.slot_key,
            m.order_index,
            m.props_override_json,
            m.created_at
        FROM public.ui_package_component_map m
        JOIN public.ui_component_package pp ON pp.package_id = m.package_id
        JOIN topology.ui_component_package cp ON cp.package_key = pp.package_key
        JOIN public.ui_component_registry pr ON pr.component_id = m.component_id
        JOIN topology.ui_component_registry cr ON cr.component_key = pr.component_key
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
    --
    -- Parent IDs (package_id, layout_id, wiring_id) are remapped to canonical
    -- UUIDs via business key join.  Unresolvable FK references raise an exception.
    --
    -- parent_tensor_id is a self-reference handled in two passes:
    --   Pass 1: insert all rows with parent_tensor_id = NULL.
    --   Pass 2: UPDATE parent_tensor_id to the canonical tensor_id via direct
    --           UUID match.  Rows where the old parent UUID does not exist in
    --           canonical remain NULL (acceptable: FK is ON DELETE SET NULL;
    --           NULL = root-level tensor).
    --
    -- Logical duplicate detection uses WHERE NOT EXISTS with IS NOT DISTINCT FROM
    -- for nullable slot_key.  ON CONFLICT (tensor_id) handles re-runs.
    -------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'ui_topology_tensor'
    ) THEN
        -- Validate: all package_id references must resolve via business key
        SELECT COUNT(*) INTO v_bad_refs
        FROM public.ui_topology_tensor t
        WHERE NOT EXISTS (
            SELECT 1
            FROM public.ui_component_package pp
            JOIN topology.ui_component_package cp ON cp.package_key = pp.package_key
            WHERE pp.package_id = t.package_id
        );
        IF v_bad_refs > 0 THEN
            RAISE EXCEPTION
                'ui_topology_to_canonical_schema: % ui_topology_tensor row(s) have '
                'package_id values that cannot be resolved to a canonical ui_component_package row.',
                v_bad_refs;
        END IF;

        -- Validate: all layout_id references must resolve via business key
        SELECT COUNT(*) INTO v_bad_refs
        FROM public.ui_topology_tensor t
        WHERE NOT EXISTS (
            SELECT 1
            FROM public.ui_layout_registry pl
            JOIN topology.components_layout_design cl ON cl.layout_key = pl.layout_key
            WHERE pl.layout_id = t.layout_id
        );
        IF v_bad_refs > 0 THEN
            RAISE EXCEPTION
                'ui_topology_to_canonical_schema: % ui_topology_tensor row(s) have '
                'layout_id values that cannot be resolved to a canonical components_layout_design row.',
                v_bad_refs;
        END IF;

        -- Validate: all wiring_id references must resolve via business key
        SELECT COUNT(*) INTO v_bad_refs
        FROM public.ui_topology_tensor t
        WHERE NOT EXISTS (
            SELECT 1
            FROM public.ui_wiring_registry pw
            JOIN topology.ui_wiring_registry cw ON cw.wiring_key = pw.wiring_key
            WHERE pw.wiring_id = t.wiring_id
        );
        IF v_bad_refs > 0 THEN
            RAISE EXCEPTION
                'ui_topology_to_canonical_schema: % ui_topology_tensor row(s) have '
                'wiring_id values that cannot be resolved to a canonical ui_wiring_registry row.',
                v_bad_refs;
        END IF;

        -- Pass 1: insert all tensor rows with parent_tensor_id = NULL and remapped parent FKs.
        -- WHERE NOT EXISTS with IS NOT DISTINCT FROM handles nullable slot_key duplicate detection.
        -- ON CONFLICT (tensor_id) DO NOTHING handles re-runs.
        INSERT INTO topology.ui_topology_tensor
            (tensor_id, route_key, package_id, layout_id, wiring_id, parent_tensor_id, slot_key, order_index,
             visibility_rule_json, layout_patch_json, css_token_refs, responsive_token_refs, state_policy_json,
             created_at, updated_at)
        SELECT
            t.tensor_id,
            t.route_key,
            cp.package_id,
            cl.layout_id,
            cw.wiring_id,
            NULL,
            t.slot_key,
            t.order_index,
            t.visibility_rule_json,
            t.layout_patch_json,
            t.css_token_refs,
            t.responsive_token_refs,
            t.state_policy_json,
            t.created_at,
            t.updated_at
        FROM public.ui_topology_tensor t
        JOIN public.ui_component_package pp ON pp.package_id = t.package_id
        JOIN topology.ui_component_package cp ON cp.package_key = pp.package_key
        JOIN public.ui_layout_registry pl ON pl.layout_id = t.layout_id
        JOIN topology.components_layout_design cl ON cl.layout_key = pl.layout_key
        JOIN public.ui_wiring_registry pw ON pw.wiring_id = t.wiring_id
        JOIN topology.ui_wiring_registry cw ON cw.wiring_key = pw.wiring_key
        WHERE NOT EXISTS (
            SELECT 1 FROM topology.ui_topology_tensor ex
            WHERE ex.route_key   = t.route_key
              AND ex.package_id  = cp.package_id
              AND ex.layout_id   = cl.layout_id
              AND ex.wiring_id   = cw.wiring_id
              AND ex.slot_key    IS NOT DISTINCT FROM t.slot_key
              AND ex.order_index = t.order_index
        )
        ON CONFLICT (tensor_id) DO NOTHING;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_topology_tensor: inserted % row(s) (Pass 1, parent_tensor_id=NULL)', v_count;

        -- Pass 2: restore parent_tensor_id via direct UUID match.
        -- When the old public parent_tensor_id exists in canonical (no UUID displacement),
        -- set it.  Rows where old UUID is absent remain NULL (root-level tensor).
        UPDATE topology.ui_topology_tensor canonical
        SET parent_tensor_id = pub.parent_tensor_id
        FROM public.ui_topology_tensor pub
        WHERE canonical.tensor_id       = pub.tensor_id
          AND pub.parent_tensor_id      IS NOT NULL
          AND canonical.parent_tensor_id IS NULL
          AND EXISTS (
              SELECT 1 FROM topology.ui_topology_tensor parent_row
              WHERE parent_row.tensor_id = pub.parent_tensor_id
          );

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_topology_tensor: updated % row(s) with parent_tensor_id (Pass 2)', v_count;
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
