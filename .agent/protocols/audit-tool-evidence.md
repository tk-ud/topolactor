# audit tool evidence observation protocol

## workflow_guard
Use with worktype `audit` to report observed target-side Agent UI tool evidence.

## purpose
Define how an audit records whether the target PR / submitted summary already contains Agent UI tool evidence.

This protocol is observational only. Audit must not generate, append, rewrite, backfill, or require creation of target PR tool evidence.

## authority_boundary
- Implementation Agent / Agent UI tool may generate tool evidence during implementation flow.
- Audit may only observe existing target-side evidence and report present / absent / insufficient / not_applicable.
- Tool evidence is not SSOT authority, proof completion, semantic completion, or implemented / partial / not_started judgment.
- Missing tool evidence is reported separately from semantic merge readiness unless a target SSOT explicitly makes that evidence a completion condition.

## trigger_condition
Triggered for worktype `audit` output shape.

If the target PR / submitted summary has no Agent UI surface or tool-use claim, report `not_applicable` rather than forcing tool use.

## observation_scope
Observe existing evidence only from:
- target PR body / comments / completion summary,
- target PR diff when it changes `docs/governance/logs/tool.log`,
- target PR diff when it includes generated metadata fields such as `uuid`, `datetime`, `task_name`, `worktype`, `reference_basis`, or `senario_tmp_path`,
- tool output pasted by the implementation agent.

Do not inspect or require the audit Agent's own tool route as target PR evidence.

## required_observation_fields
Audit output should include:

```text
Agent UI tool evidence observed:
- evidence_present: yes/no/not_applicable
- observed_source:
- observed_fields:
- missing_fields:
- absence_reason:
- observation_judgment: present/absent/insufficient/not_applicable + evidence
- boundary_note: audit observes existing target evidence only; audit must not generate, append, or backfill tool evidence
```

## observation_judgment
- `present`: target-side evidence exists and the observed fields are listed.
- `absent`: tool evidence would be relevant because the PR claims tool use or touches Agent UI tool surfaces, but no target-side evidence is found.
- `insufficient`: partial evidence exists, but key claimed fields or source references are missing.
- `not_applicable`: the target PR / summary does not claim tool use and does not touch Agent UI evidence surfaces.

## misuse_conditions
The audit is invalid for this protocol if it:
- asks the auditor to create or append `tool.log` evidence,
- treats the audit Agent's own tool usage as target PR evidence,
- treats absence of target-side tool evidence as semantic implementation failure by itself,
- treats tool evidence as SSOT authority or implemented judgment,
- rewrites target history to manufacture evidence after the fact.

## pass_conditions
- Audit output records the observed target-side evidence state.
- Observation is separated from semantic implementation judgment.
- Absence or not_applicable state is explicitly stated without backfill.
