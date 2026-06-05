-- Migration: ui_topology_tensor unique constraint — NULLS NOT DISTINCT
-- Requires PostgreSQL 15+.  topolactor runs on postgres:16-alpine.
--
-- Problem: The previous UNIQUE (route_key, package_id, layout_id, wiring_id, slot_key, order_index)
-- constraint treated NULL slot_key as distinct from every other NULL, so ON CONFLICT DO NOTHING
-- was ineffective when slot_key was omitted.  Repeated promotes of the same route accumulated
-- duplicate tensor rows instead of being idempotent.
--
-- Fix: Replace the constraint with NULLS NOT DISTINCT so that two rows sharing the same
-- (route_key, package_id, layout_id, wiring_id, NULL slot_key, order_index) are treated
-- as a conflict.  The application now also writes slot_key = 'default' for normal
-- single-route promote, which satisfies both the old and the new constraint.

ALTER TABLE topology.ui_topology_tensor
    DROP CONSTRAINT IF EXISTS uq_ui_topology_tensor_route_slot_order;

ALTER TABLE topology.ui_topology_tensor
    ADD CONSTRAINT uq_ui_topology_tensor_route_slot_order
        UNIQUE NULLS NOT DISTINCT (route_key, package_id, layout_id, wiring_id, slot_key, order_index);
