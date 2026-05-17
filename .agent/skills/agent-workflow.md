# Skill: Agent Workflow

## Purpose

This skill defines the minimal execution order for agents working in this repository.

It does not replace `AGENTS.md`, `.agent/rules/rule.md`, or `.agent/docs/required-paths.yaml`.
Use those files as the source of rules and required structure.

## Execution Order

```text
READ_RULES
→ INSPECT_TARGET
→ DEFINE_SCOPE
→ EDIT_OR_SPECIFY
→ RUN_LOCAL_CI
→ FIX_IF_RED
→ UPDATE_TASK_SURFACE
→ COMMIT_OR_PR
```

## Step Meaning

### READ_RULES

Read the repository entrypoints before changing files:

```text
AGENTS.md
.agent/rules/rule.md
.agent/docs/required-paths.yaml
```

### INSPECT_TARGET

Open the files directly related to the task.
Do not infer architecture from file names alone.

### DEFINE_SCOPE

Keep the change scoped to the issue or request.
Do not add implementation surfaces when the task is documentation-only.
Do not turn topology runtime work into CRUD, MVC, or frontend-owned state.

### EDIT_OR_SPECIFY

Make the smallest coherent change that preserves the canonical route:

```text
stored_topology_data
→ user_operation
→ operation_vector
→ attractor_resolve
→ structure_map_resolve
→ package_resolve
→ schema_resolve
→ component_expand
→ emission_or_projection
```

### RUN_LOCAL_CI

Run the relevant local CI script from `.agent/tests/`.
At minimum, run structure check for repository-shape changes.

### FIX_IF_RED

If local CI is red, fix the failure and rerun the same check.
Do not treat missing tools as a passing result.

### UPDATE_TASK_SURFACE

Update `.agent/tasks/todo.md` or `.agent/reports/` only when the task explicitly creates or closes task/report state.
Do not add new TODO items casually.

### COMMIT_OR_PR

Commit or open/update a PR only after the relevant local CI is green, or clearly report that execution was not possible due to environment limitations.

## Use This Skill For

- deciding task execution order
- avoiding scope creep
- preventing duplicate rule text across agent docs
- keeping agent work aligned with the topology runtime route
