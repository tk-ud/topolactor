# audit tool evidence protocol

## workflow_guard
Use with worktype `audit` after `AGENTS.md` routes through Agent UI tool-first entry.

## purpose
Force audit outputs to report whether Agent UI tool evidence was present, absent with an explicit fallback reason, or insufficient.

This protocol checks route/provenance evidence only. Agent UI tool evidence is not SSOT authority, proof completion, semantic completion, or implemented / partial / not_started judgment.

## trigger_condition
Always triggered by worktype `audit`.

## evidence_scope
Audit must inspect Agent UI tool evidence for the current audit route and, when applicable, for the target PR or summary being audited.

Applicable target PR cases include:
- PR summary claims Agent UI tool use.
- PR touches `.agent/tools`, `.agent/rules`, `.agent/prompt`, `.agent/protocols`, `.agent/routes`, `.agent/checklists`, or `docs/governance/agent-ui-*` surfaces.
- PR completion summary includes generated `uuid`, `datetime`, `task_name`, `worktype`, `reference_basis`, or `senario_tmp_path` fields.

## required_evidence_fields
The audit output must include:

```text
Agent UI tool evidence checked:
- tool_used: yes/no/not_available
- datetime:
- uuid:
- task_name:
- worktype:
- reference_basis:
- senario_tmp_path:
- tool_log_entry_checked: yes/no/not_applicable
- fallback_reason_if_not_used:
- evidence_judgment: pass/partial/fail + evidence
```

## pass_axis
- `tool_used: yes` when structured Agent UI output exists and includes at least `datetime`, `uuid`, `task_name`, `worktype`, and `reference_basis`.
- `tool_log_entry_checked: yes` when a claimed tool use has a matching compact metadata record in `docs/governance/logs/tool.log`, or when the auditor explicitly explains why target-side log checking is not applicable.
- `senario_tmp_path` is present when the tool step reached scenario contract creation or local-test summary.
- `fallback_reason_if_not_used` is present when tool use is absent or unavailable.
- `evidence_judgment` explains whether the evidence is complete enough to prove tool route/provenance.

## partial_axis
Use `partial` when some structured evidence exists but any of the following are missing:
- `reference_basis`
- `task_name`
- `worktype`
- `senario_tmp_path` for a scenario-producing flow
- `tool_log_entry_checked` for a claimed tool-log-producing flow

Partial tool evidence does not by itself block or approve semantic implementation judgment. It must be reported separately from merge readiness.

## fail_axis
Use `fail` when:
- tool use is claimed but no structured metadata can be found.
- `uuid` / `datetime` / `worktype` appear to be hand-authored instead of tool-generated.
- `tool.log` is rewritten, truncated, cleaned up by tool flow, or contains verbose logs / senario body / checklist answers.
- audit output omits the Agent UI tool evidence block.

## fallback_rule
Fallback is allowed only when the Agent UI tool is not usable or the audited work predates the tool route.

Fallback must state:
- why the tool was not used,
- which legacy prompt/protocol/checklist route was used instead,
- why missing tool evidence does not invalidate the semantic audit evidence.

## blocking_conditions
- Missing `Agent UI tool evidence checked` block in audit output.
- Claimed tool use without structured metadata and no fallback explanation.
- Treating tool evidence as semantic completion or implemented judgment.

## pass_conditions
- Audit output contains the required Agent UI tool evidence block.
- Evidence status is separated from semantic implementation judgment.
- Missing or unavailable tool use has an explicit fallback reason.
