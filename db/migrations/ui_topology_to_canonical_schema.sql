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
--   the parent's remapped canonical logical tuple.  A non-NULL legacy parent reference
--   must resolve; dangling legacy parent rows and unresolved logical parents raise
--   RAISE EXCEPTION rather than silently degrading the relationship to NULL.
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
    --   Pass 2: resolve the old public parent row, remap its package/layout/wiring
    --           business keys to the canonical tuple, then UPDATE parent_tensor_id
    --           to the matching canonical tensor_id.  This also handles displaced
    --           child tensor UUIDs by resolving the child through its logical tuple.
    --
    -- Explicit dangling-reference policy: when a non-NULL legacy parent_tensor_id
    -- has no public parent row, abort with RAISE EXCEPTION.  ON DELETE SET NULL is a
    -- post-migration deletion policy; it is not permission to discard legacy data.
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

        -- Pass 2 precondition: a non-NULL legacy parent reference must point to
        -- an old public parent row.  A dangling legacy reference is data loss risk,
        -- so abort explicitly instead of treating the child as a root tensor.
        SELECT COUNT(*) INTO v_bad_refs
        FROM public.ui_topology_tensor child_pub
        WHERE child_pub.parent_tensor_id IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM public.ui_topology_tensor parent_pub
              WHERE parent_pub.tensor_id = child_pub.parent_tensor_id
          );
        IF v_bad_refs > 0 THEN
            RAISE EXCEPTION
                'ui_topology_to_canonical_schema: % ui_topology_tensor row(s) have dangling '
                'parent_tensor_id values with no matching public parent tensor row. '
                'Repair the legacy tensor hierarchy before retrying.',
                v_bad_refs;
        END IF;

        -- Pass 2 precondition: remap each public parent row's package/layout/wiring
        -- UUIDs through business keys and require a matching canonical logical tuple.
        SELECT COUNT(*) INTO v_bad_refs
        FROM public.ui_topology_tensor child_pub
        JOIN public.ui_topology_tensor parent_pub ON parent_pub.tensor_id = child_pub.parent_tensor_id
        WHERE child_pub.parent_tensor_id IS NOT NULL
          AND NOT EXISTS (
              SELECT 1
              FROM public.ui_component_package parent_pp
              JOIN topology.ui_component_package parent_cp ON parent_cp.package_key = parent_pp.package_key
              JOIN public.ui_layout_registry parent_pl ON parent_pl.layout_id = parent_pub.layout_id
              JOIN topology.components_layout_design parent_cl ON parent_cl.layout_key = parent_pl.layout_key
              JOIN public.ui_wiring_registry parent_pw ON parent_pw.wiring_id = parent_pub.wiring_id
              JOIN topology.ui_wiring_registry parent_cw ON parent_cw.wiring_key = parent_pw.wiring_key
              JOIN topology.ui_topology_tensor parent_canonical
                ON parent_canonical.route_key   = parent_pub.route_key
               AND parent_canonical.package_id  = parent_cp.package_id
               AND parent_canonical.layout_id   = parent_cl.layout_id
               AND parent_canonical.wiring_id   = parent_cw.wiring_id
               AND parent_canonical.slot_key    IS NOT DISTINCT FROM parent_pub.slot_key
               AND parent_canonical.order_index = parent_pub.order_index
              WHERE parent_pp.package_id = parent_pub.package_id
          );
        IF v_bad_refs > 0 THEN
            RAISE EXCEPTION
                'ui_topology_to_canonical_schema: % ui_topology_tensor row(s) have '
                'parent_tensor_id values whose logical parent tuple cannot be resolved '
                'to a canonical ui_topology_tensor row.',
                v_bad_refs;
        END IF;

        -- Restore parent_tensor_id by resolving both sides through canonical logical
        -- tuples.  Existing canonical parent links take precedence; only NULL links
        -- are filled by legacy migration data.
        UPDATE topology.ui_topology_tensor child_canonical
        SET parent_tensor_id = parent_canonical.tensor_id
        FROM public.ui_topology_tensor child_pub
        JOIN public.ui_component_package child_pp ON child_pp.package_id = child_pub.package_id
        JOIN topology.ui_component_package child_cp ON child_cp.package_key = child_pp.package_key
        JOIN public.ui_layout_registry child_pl ON child_pl.layout_id = child_pub.layout_id
        JOIN topology.components_layout_design child_cl ON child_cl.layout_key = child_pl.layout_key
        JOIN public.ui_wiring_registry child_pw ON child_pw.wiring_id = child_pub.wiring_id
        JOIN topology.ui_wiring_registry child_cw ON child_cw.wiring_key = child_pw.wiring_key
        JOIN public.ui_topology_tensor parent_pub ON parent_pub.tensor_id = child_pub.parent_tensor_id
        JOIN public.ui_component_package parent_pp ON parent_pp.package_id = parent_pub.package_id
        JOIN topology.ui_component_package parent_cp ON parent_cp.package_key = parent_pp.package_key
        JOIN public.ui_layout_registry parent_pl ON parent_pl.layout_id = parent_pub.layout_id
        JOIN topology.components_layout_design parent_cl ON parent_cl.layout_key = parent_pl.layout_key
        JOIN public.ui_wiring_registry parent_pw ON parent_pw.wiring_id = parent_pub.wiring_id
        JOIN topology.ui_wiring_registry parent_cw ON parent_cw.wiring_key = parent_pw.wiring_key
        JOIN topology.ui_topology_tensor parent_canonical
          ON parent_canonical.route_key   = parent_pub.route_key
         AND parent_canonical.package_id  = parent_cp.package_id
         AND parent_canonical.layout_id   = parent_cl.layout_id
         AND parent_canonical.wiring_id   = parent_cw.wiring_id
         AND parent_canonical.slot_key    IS NOT DISTINCT FROM parent_pub.slot_key
         AND parent_canonical.order_index = parent_pub.order_index
        WHERE child_pub.parent_tensor_id    IS NOT NULL
          AND child_canonical.route_key     = child_pub.route_key
          AND child_canonical.package_id    = child_cp.package_id
          AND child_canonical.layout_id     = child_cl.layout_id
          AND child_canonical.wiring_id     = child_cw.wiring_id
          AND child_canonical.slot_key      IS NOT DISTINCT FROM child_pub.slot_key
          AND child_canonical.order_index   = child_pub.order_index
          AND child_canonical.parent_tensor_id IS NULL;

        GET DIAGNOSTICS v_count = ROW_COUNT;
        RAISE NOTICE 'topology.ui_topology_tensor: updated % row(s) with logical parent_tensor_id remap (Pass 2)', v_count;
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
