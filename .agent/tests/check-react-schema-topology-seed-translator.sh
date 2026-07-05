#!/usr/bin/env bash
# check-react-schema-topology-seed-translator.sh -- executable proof surface (CI entrypoint).
#
# SSOT: docs/design/react-schema-topology-seed-translator-ssot.yaml
# Verifies the generate-react-schema mode of
# .agent/tools/react-schema-topology-seed-translator against the
# credential-management-0092 fixture (auth.external.credential_management.projection,
# manifest.manifest_id = 00000000-0000-0000-0000-000000000092) plus negative-path
# scenarios (unknown tag, missing wiringLane, invalid targetRef, unknown
# componentKind, raw HTML emission, not-implemented modes).
#
# Structured JSON/subprocess assertions live in
# .agent/scripts/check_react_schema_topology_seed_translator.py (Python3 stdlib
# only); this shell script is only a CI entrypoint/orchestration wrapper (see
# docs/framework-policy.yaml repo_governance_tooling_dependency_policy).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

python3 "$REPO_ROOT/.agent/scripts/check_react_schema_topology_seed_translator.py"
