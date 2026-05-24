# Registry Semantic Matrix

## Summary

In topolactor, the registry table is not described as a plain config table. It is treated as a **semantic matrix** of definitions/configuration that defines topology vocabulary and axes used by runtime and projection surfaces. Registry defines targets (hub/topology/UI/state/relation), but registry itself is not a hub, attractor, Tensor target, or Attention entity.

## Matrix interpretation

- **Row**: `registryId` as basis vocabulary.
- **Column**: semantic/projection/wiring axis.
- **Cell value**: weight, state, relation, coordinate, or connection.
- **Coordinate**: combinations of registry IDs can form sparse vector/tensor coordinates when bound into relation-bearing topology or hub surfaces.

This is why “data-driven” here is more than externalized settings: runtime behavior is expanded from matrix coordinates through abstract execution surfaces.

## Why this is not a normal config table

A typical config table toggles static branches in code. In contrast, this architecture treats DB/UI/endpoint/runtime/scheduler/function/CI surfaces as projection or expansion surfaces of the same registry-defined topology coordinate space.

## Status

- **Implemented now (partial platform):** registry-driven topology and policy surfaces exist, but platform milestones remain mixed across implemented/partial/skeleton/not_started in the public roadmap.
- **Design discipline:** projection/expansion interpretation across surfaces.
- **Future:** additional topology analyses remain roadmap-managed planned work, not current production-ready capability.


## Registry and hub boundary

- **registry/registrar** stores definitions and configuration for target surfaces (hub/topology/UI/schema/state/relation).
- **hub** is a relation node where relations are established.
- A **relation-bearing hub** can be treated as a tensor/vector-readable coordinate surface.
- As vectors, hub relations support neighborhood search.
- Attractor is a vector convergence point; Attention is observation/observation-operation.


## Relation map shapes

- Equal relation: `id`, `related_ids[]`.
- Parent-child relation: `id`, `parent_id`, `child_ids[]`.

These relation-map ID columns can be normalized column-wise for neighborhood search.
