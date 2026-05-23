# Agent Governance Routing SSOT

Status: draft SSOT  
YAML body: `docs/governance/agent-governance-routing-ssot.yaml`

## Role of This Markdown

This Markdown is part of the SSOT set, but it is not the detailed rule body.

Use it as the agenda, resume, and index for humans and LLM agents.
The YAML is the detailed body for closed-world structure, authority boundaries, worktype definitions, CI checker contracts, and validation order.

## Agenda

1. Confirm the canonical governance route.
2. Identify the worktype.
3. Use the YAML to resolve directory responsibility, semantic closure, and authority boundary.
4. Open only the matching worktype prompt and triggered protocol surfaces.
5. Run required CI/checker contracts in the YAML-defined order.
6. Keep `.agent` files subordinate to this governance SSOT set.

## Resume

This SSOT set defines repository-side agent governance routing.

It exists to prevent `.agent` governance rules from being duplicated across README, rule files, prompt routers, protocols, checklists, and shell checks.

The intended route is:

```text
AGENTS.md
→ .agent/rules/rule.md
→ worktype decision
→ .agent/README.md
→ .agent/prompt/<worktype>.md
→ .agent/protocols/<worktype or triggered protocol>.md
→ checklist / tests
```

## YAML Index

Read the YAML sections for detail:

| Need | YAML section |
|---|---|
| Route order | `canonical_route` |
| Physical directory structure | `directory_structure` |
| Meaning closure / allowed edges | `semantic_closure` |
| Closed-world constraints | `closed_world_rules` |
| Authority boundaries | `authority_boundaries` |
| `.agent` responsibility split | `responsibility_split` |
| Worktype vocabulary | `worktypes` |
| Prompt contract | `prompt_contract` |
| Protocol contract | `protocol_contract` |
| Checklist contract | `checklist_contract` |
| Test/check contract | `test_contract` |
| CI checker list, meaning, order | `ci_check_contract` |
| Grep key policy | `grep_key_policy` |
| Route index policy | `routing_index_policy` |
| Token estimate placement | `token_estimate_policy` |
| Validation closure | `validation_closure` |
| Duplication prohibitions | `prohibited_duplication` |
| Migration target | `migration_target_state` |

## Worktype Index

Canonical worktype ids:

- `audit`
- `specific`
- `implementation_change`
- `design_change`
- `todo_maintenance`
- `existing_pr_update`

The YAML owns each worktype's prompt/protocol relation and required check scope.

## Reading Rule

- Use this Markdown to orient and navigate.
- Use the YAML to decide.
- Do not duplicate YAML body details into `.agent/README.md`, `.agent/rules/rule.md`, prompt files, protocol files, or check scripts.

If this Markdown and the YAML disagree, update this Markdown to match the YAML agenda/index intent; the YAML body remains the detailed decision source.
