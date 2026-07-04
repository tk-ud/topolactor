# implementation_change / foundation_ssot_read_gate

Before judging or changing runtime/frontend/backend/db behavior, apply foundation SSOT read when work touches projection, dispatch, runtime lanes, DB-driven UI, pipeline identity, or completion judgment.

Read order:
1. `docs/framework-core.yaml`
2. `docs/design/runtime-orchestration-ssot.yaml`
3. `docs/design/pipeline-continuity-ssot.yaml`
4. target-specific SSOT / DB / implementation files

Do not treat this as always-read for unrelated typo/format-only edits. When skipped, record explicit `not_required` reason.
