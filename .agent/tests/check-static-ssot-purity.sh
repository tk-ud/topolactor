#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

TARGETS=(
  "docs/framework-core.yaml"
  "docs/framework-policy.yaml"
  "docs/file-structure.yaml"
  "docs/registrar-admin-ui-specification.md"
  "docs/promotion-manifest-editor-specification.md"
  "docs/design/db-schema.yaml"
  "docs/design/runtime-orchestration-ssot.yaml"
  "docs/design/pipeline-continuity-ssot.yaml"
  "docs/design/sql-attention-logs-ssot.md"
  "docs/design/sql-attention-logs-ssot.yaml"
  "docs/design/context-route-recommendation.md"
  "docs/design/context-route-recommendation.yaml"
  "docs/design/relation-registry-fk-audit-and-abstract-migration.md"
  "docs/design/commit-inference-engine.yaml"
  "docs/design/commit-inference-engine.md"
  "docs/design/topology-recommendation-ci-runtime.yaml"
  "docs/design/topology-recommendation-ci-runtime.md"
)

PATTERNS=(
  "out_of_scope_not_implemented"
  "future_migration_task"
  "no_runtime_or_ddl_change_in_this_pr"
  "this PR"
  "in this PR"
  "current implementation"
  "initial_implementation"
  "already implemented"
  "not implemented"
  "not_implemented"
  "not yet implemented"
  "known_gap"
  "gap_tracking"
  "future scope"
  "\\bimplemented\\b"
  "\\bpartial\\b"
  "\\bskeleton\\b"
  "\\bTODO\\b"
  "\\bremaining\\b"
  "\\broadmap\\b"
  "\\bcurrently\\b"
  "\\bGap-"
  "\\bgap-"
  "Implementation Work Items"
  "Runtime Contract Work Items"
  "Implement C#"
  "Implement "
  "After this specification is accepted"
  "No implementation files are added"
  "Out of Scope"
  "Current boundary"
  "Full canonical import requires"
  "対応実装予定"
  "Production-ready"
  "future work"
  "実装予定"
  "未実装"
)

for file in "${TARGETS[@]}"; do
  [ -f "$file" ] || { echo "FAIL: missing target $file" >&2; exit 1; }
  for p in "${PATTERNS[@]}"; do
    if rg -n -i -e "$p" "$file" >/dev/null; then
      echo "FAIL: forbidden progress vocabulary '$p' in $file" >&2
      rg -n -i -e "$p" "$file" >&2
      exit 1
    fi
  done
done

echo "OK: static SSOT purity check passed"
