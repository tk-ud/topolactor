# Cursor project configuration

This directory configures **Cursor IDE** agents for this repository. It does not replace `.agent/` governance; it adds IDE-local execution hints.

## Layout

| Path | Role |
|------|------|
| `rules/*.mdc` | Project rules loaded by Cursor (always-on and scoped) |
| `mcp.json` | Optional MCP server wiring (empty template until needed) |

## Relationship to `.agent/`

- **Entry contract**: [`top/AGENTS.md`](../AGENTS.md) → `.agent/rules/rule.md` → `.agent/README.md` → tool-first (`initial_contract` → implement → `local_test`) or fallback worktype route.
- Cursor agents must read `top/AGENTS.md` first; see `.cursor/rules/topolactor-agent.mdc`.
- **Verification close (tool-first)**: `agent-ui-local-test` through `summary` (`pass_or_fail`). **Fallback**: routed `required_checks` with `check-structure.sh` last. Windows local policy: see `.cursor/rules/local-verification.mdc`.

## Local environment (Cursor)

On a typical developer machine running Cursor for this repo:

- `dotnet` — .NET 8 SDK (backend build/tests)
- `deno` — v2.x (frontend tests, Fresh)
- `docker` — optional runtime/DB checks via `.agent/tests/check-runtime-environment.sh`

Agents should run verification **on the host shell**, not assume tools are missing. Do not substitute Docker for `dotnet test` / `deno test` when host tools are available.

## Quick verification

```bash
# Runtime semantics (matches GitHub workflow runtime-semantics)
bash .agent/tests/check-runtime-semantics.sh

# Broader local CI slice (unified gate + runtime env + structure last)
bash .agent/tests/check-local-ci.sh
```
