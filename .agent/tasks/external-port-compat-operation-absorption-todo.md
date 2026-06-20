# External port compatibility operation absorption todo

Status: `implemented`

Implemented in this branch. The runtime path is wired through `ExternalPortPolicyStepExecutorCompatibilityShim`, DB bootstrap applies `db/external_port_compat_absorption_seed.sql`, and `.agent/tests/check-external-port-compat-absorption.sh` guards the absorption contract.

This file should be removed when the PR is merged or when a maintainer performs final todo cleanup.
