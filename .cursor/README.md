# Cursor project configuration

This directory configures **Cursor IDE** agents for this repository. It does not replace `.agent/` governance; it adds IDE-local execution hints.

## Layout

| Path | Role |
|------|------|
| `rules/*.mdc` | Project rules loaded by Cursor (always-on and scoped) |
| `mcp.json` | Optional MCP server wiring (empty template until needed) |

## Relationship to `.agent/`

- **Entry contract**: [`top/AGENTS.md`](../AGENTS.md) (repository-root `AGENTS.md`) → `.agent/rules/rule.md` → work-type prompts/protocols.
- Cursor agents must read `top/AGENTS.md` first; see `.cursor/rules/topolactor-agent.mdc`.
- **Executable checks**: `.agent/tests/*.sh` (same scripts as CI where noted).
- **Last gate**: `bash .agent/tests/check-structure.sh`

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
