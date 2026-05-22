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

- **Implemented now:** UI topology persistence tables and admin boundary specifications.
- **Design-guarded:** tensor projection interpretation and package/persistence semantics.
- **Future:** additional admin flows where explicitly documented as planned.


## Relation-first interpretation

UI package/layout/wiring definitions are not automatically topology meaning bodies by themselves.
They become hub/vector-treatable relation surfaces when relations are established, for example:

- `physical_table × ui_package`
- `record/entity × component`
- `route × layout/wiring`

So the tensor/vector interpretation is relation-first, not UI-definition-only.
