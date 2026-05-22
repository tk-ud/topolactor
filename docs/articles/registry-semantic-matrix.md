# Registry Semantic Matrix

## Summary

In topolactor, the registry table is not described as a plain config table. It is treated as a **semantic matrix** of definitions/configuration that defines topology vocabulary and axes used by runtime and projection surfaces. Registry defines targets (hub/topology/UI/state/relation), but registry itself is not a hub, attractor, or Attention entity.

## Matrix interpretation

- **Row**: `registryId` as basis vocabulary.
- **Column**: semantic/projection/wiring axis.
- **Cell value**: weight, state, relation, coordinate, or connection.
- **Coordinate**: combinations of registry IDs act as sparse vector/tensor coordinates.

This is why “data-driven” here is more than externalized settings: runtime behavior is expanded from matrix coordinates through abstract execution surfaces.

## Why this is not a normal config table

A typical config table toggles static branches in code. In contrast, this architecture treats DB/UI/endpoint/runtime/scheduler/function/CI surfaces as tensor projections/expansions of the same registry space.

## Status

- **Implemented now:** registry-driven topology surfaces and policy docs.
- **Design discipline:** projection/expansion interpretation across surfaces.
- **Future:** additional topology analyses described as planned in design docs.


## Registry and hub boundary

- **registry/registrar** stores definitions and configuration for target surfaces (hub/topology/UI/schema/state/relation).
- **hub** is a relation node where relations are established.
- A **relation-bearing hub** can be treated as tensor/vector coordinates.
- As vectors, hub relations support neighborhood search.
- Attractor is a vector convergence point; Attention is observation/observation-operation.
