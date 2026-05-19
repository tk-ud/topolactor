# UI Topology Tensor and Admin Surface

## Summary

Topolactor’s admin UI is positioned as a **semantic matrix / tensor coordinate editor** for UI topology persistence, not as a component catalog alone.

## Core model

- **components bucket** is a staging area for unpackaged components,
- **package generator** promotes staged components by issuing:
  - `componentId`
  - `packageId`
  - `layoutId`
  - `wiringId`
- after persistence, these become UI topology tensor entities.

Code-only components are not automatically treated as runtime topology entities.

## Adapter stance

Frontend adapters are stable projection surfaces. New specifications should primarily be represented by topology data updates instead of repeatedly creating spec-by-spec code paths.

## Status

- **Implemented now:** UI topology persistence tables and admin boundary specifications.
- **Design-guarded:** tensor projection interpretation and package/persistence semantics.
- **Future:** additional admin flows where explicitly documented as planned.
