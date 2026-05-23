# Design Change Prompt Router

Use this router for SSOT, docs/design contract, or external observable contract changes.

## Route

- Inspect SSOT dependency surfaces before making changes.
- If SSOT changes are in scope, apply `.agent/protocols/ssot-change-impact.md`.
- If new vocabulary affects shell/yaml required-path routing checks, update the referenced routing/check vocabulary.
- Keep protocol bodies and SSOT bodies in their source files; do not duplicate them here.
