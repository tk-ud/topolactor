---
title: "UI Topology Tensor and Admin Surface"
emoji: "🧩"
type: "tech"
topics: ["ui", "architecture", "database", "deno", "preact"]
published: false
---

# UI Topology Tensor and Admin Surface

## Summary

Topolactor’s admin UI is positioned as a **definition/relationship editor** for UI topology persistence, not as a component catalog alone.

## Core model

- **components bucket** is a staging area for unpackaged components,
- **package generator** promotes staged components by issuing:
  - `componentId`
  - `packageId`
  - `layoutId`
  - `wiringId`
- after persistence, these become relation targets that can participate in hub/vector surfaces when linked with physical table/record/route relations.

Code-only components are not automatically treated as runtime topology entities.

## Adapter stance

Frontend adapters are stable projection surfaces. New specifications should primarily be represented by topology data updates instead of repeatedly creating spec-by-spec code paths.

## Status

- **Implemented now (partial scope):** UI topology persistence tables and admin boundary specifications are present, and package generation can issue componentId/packageId/layoutId/wiringId.
- **Not completed yet:** primitive catalog still includes code-only drift entries and visual layout builder remains partial (mouse-driven editing and style/responsive rule authoring pending).
- **Design-guarded:** tensor projection interpretation and package/persistence semantics remain governed by SSOT docs.


## Relation-first interpretation

UI relation is a parent-child relation map.

`hubs.ui_relation` shape:
- `id`
- `parent_id` (physical_table_id or data_type_id)
- `child_ids[]` (package_ids[])

`package` means one UI payload bundle.
`physical_table : package` is not one-to-one.

This relation map shows which UI package payload groups are used by which data type/physical table.
If `parent_id` and `child_ids[]` columns are normalized column-wise, neighborhood search can compare UI usage patterns.

Boundary:
- Do not mix topology semantic definitions into the hubs relation-map body.
- Do not mix role/state/route/context axes into the UI relation matrix body itself.

