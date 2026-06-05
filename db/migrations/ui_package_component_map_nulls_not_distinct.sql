-- Migration: ui_package_component_map unique constraint — NULLS NOT DISTINCT
-- Requires PostgreSQL 15+.  topolactor runs on postgres:16-alpine.
--
-- Problem: The previous UNIQUE (package_id, component_id, slot_key) constraint treated
-- NULL slot_key as distinct from every other NULL, so ON CONFLICT DO NOTHING was
-- ineffective when slot_key was omitted.  Repeated promotes of the same component
-- accumulated duplicate rows.
--
-- Fix: Replace the constraint with NULLS NOT DISTINCT so that two rows with NULL
-- slot_key and the same (package_id, component_id) are treated as a conflict.
-- The application now also writes slot_key = 'default' for normal single-slot usage,
-- which satisfies both the old and new constraint.

ALTER TABLE topology.ui_package_component_map
    DROP CONSTRAINT IF EXISTS uq_ui_package_component_map;

ALTER TABLE topology.ui_package_component_map
    ADD CONSTRAINT uq_ui_package_component_map
        UNIQUE NULLS NOT DISTINCT (package_id, component_id, slot_key);
