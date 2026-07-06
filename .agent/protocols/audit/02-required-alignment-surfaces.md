# audit / required_alignment_surfaces

- top-level semantic baseline SSOT
- PR diff or patch
- changed file list
- PR-side record when the audit target is a PR:
  - PR body / stated scope
  - PR comments
  - PR reviews
  - review thread status
- tool evidence log when `.agent/tools` / Agent UI run evidence is claimed, required, or relied on:
  - .agent/tools/logs/tool.log
  - referenced Agent UI uuid / datetime
  - senario-tmp.md or relevant tool summary when referenced
- TODO / roadmap / implementation registry surfaces
- diff-target implementation files and related tests

PR-side record and tool evidence log are trace/context surfaces only. They do not replace SSOT, implementation, or test evidence.
