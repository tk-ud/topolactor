# Read Route Load Inspection — 2026-05-21

## Summary

Inspected the `.agent` read route to measure read load reduction compared to the
protocol bulk-read era, then designed and applied a small protocol direct-read exception.

## Inspection Method

- Full-read of always-read baseline files and all protocol files
- Character count via `wc -m`; section-level counts via `awk` section extraction
- Token estimate formula: English-heavy Markdown `chars / 4.0`; Japanese/Markdown mixed `chars / 3.0`
- Conservative estimate used throughout: `chars / 4.0`
- Tokenizer not used; estimates carry ±15-20% margin

## File Sizes (at time of inspection)

### Always-Read Baseline

| File | chars | est. tokens |
|---|---:|---:|
| `AGENTS.md` | 1,345 | ~336 |
| `.agent/rules/rule.md` | 7,163 | ~1,791 |
| `.agent/README.md` | 5,191 | ~1,298 |
| `.agent/skills/agent-workflow.md` | 5,349 | ~1,337 |
| **Baseline total** | **19,048** | **~4,762** |

### Protocol Files

| File | chars | est. tokens |
|---|---:|---:|
| `completion.md` | 9,140 | ~2,285 |
| `reports-and-todos.md` | 5,933 | ~1,483 |
| `registry-tensor-policy.md` | 6,535 | ~1,634 |
| `scenario-contract.md` | 1,875 | ~469 |
| `policy-judgment.md` | 1,250 | ~313 |
| `runtime-boundary-matrix.md` | 1,160 | ~290 |
| **Protocols total** | **25,893** | **~6,473** |

### Routing Overhead

| File | chars | est. tokens |
|---|---:|---:|
| `index.yaml` | 7,785 | ~1,946 |

## Old Route vs New Route

### Old Route (protocol bulk-read era)

```
old_bulk_route = baseline(~4,762) + all_protocols(~6,473) = ~11,235 tokens
```

No index.yaml existed; protocols were read in full on each task.

### New Routes

| Route | est. tokens | vs old bulk | reduction |
|---|---:|---|---:|
| Light task — baseline only | ~4,762 | 11,235 | ~58% |
| Small protocol direct-read | ~5,000-5,500 | 11,235 | ~51-55% |
| Baseline + index + 1 section | ~7,000-7,500 | 11,235 | ~33-38% |
| Baseline + index + completion/report sections | ~8,100-8,500 | 11,235 | ~24-28% |
| SSOT heavy | ~13,600-17,000 | ~18,440 | ~8-26% |

## Section-Level Measurements (key sections)

| Section | chars (awk) | est. tokens |
|---|---:|---:|
| completion.md / `## Trigger condition` | 273 | ~68 |
| completion.md / `## Completion / failure decision` | 3,843 | ~961 |
| reports-and-todos.md / `## PR body and follow-up comment policy` | 1,860 | ~465 |
| reports-and-todos.md / `## TODO carry-over rules` | 1,583 | ~396 |
| registry-tensor-policy.md / `## Trigger scope` | 216 | ~54 |
| policy-judgment.md / `Violation table:` | 390 | ~98 |

## Representative Case Route Verification

All 5 cases verified: index.yaml `grep_keys` → section `marker` → protocol body chain intact.

| Case | Expected route | grep_key present | marker present | Result |
|---|---|---|---|---|
| `follow-up comment` | reports-and-todo-surfaces / `## PR body and follow-up comment policy` | line 69 | line 31 of reports-and-todos.md | PASS |
| `TODO [x]` | completion-governance / Trigger condition + Completion/failure decision | lines 18, 43 | lines 12, 93 of completion.md | PASS |
| `Roadmap Status Gate` | completion-governance / Completion/failure decision | line 41 | line 108 of completion.md | PASS |
| `Violation table` | policy-judgment / `Violation table:` | line 192 | line 25 of policy-judgment.md | PASS |
| `registry tensor semantics` | registry-topology-semantics / `## Trigger scope` | line 208 | line 10 of registry-tensor-policy.md | PASS |

## Key Finding: Index Overhead Reversal for Small Protocols

index.yaml (~1,946 tokens) is larger than three protocol files combined:

| Protocol | tokens | index route penalty |
|---|---:|---:|
| policy-judgment.md | ~313 | +34% worse than direct read |
| runtime-boundary-matrix.md | ~290 | +33% worse than direct read |
| scenario-contract.md | ~469 | +28% worse than direct read |

Reading index.yaml before a small protocol adds ~1,500-1,700 tokens of unnecessary overhead.

## README Baseline Estimate Gap

| Item | README value (pre-fix) | Inspection estimate | Gap |
|---|---|---|---|
| Baseline | ~3,200-4,000 | ~4,762 | +762 to +1,562 tokens (+19-39%) |
| Baseline + index | ~5,200-6,300 | ~6,708 | +408 to +1,508 tokens |

The README estimates were materially below actual file sizes.
README itself notes: "Update them when token accounting or file size changes materially."

## Changes Applied

### `.agent/rules/rule.md`
- Added `## Small Protocol Direct-Read` section (names 3 direct-read protocols explicitly)
- Added `agent-workflow.md` as item 4 in `## Always-Read Baseline` (was missing; present in README and Read Route)

### `.agent/skills/agent-workflow.md`
- Updated READ_TARGET_SURFACES step to distinguish direct-read vs index route
- Updated JUDGMENT step to reflect same direct-read exception

### `.agent/README.md`
- Updated `## Estimated Token Consumption` table: baseline row corrected to ~4,500-5,000
- Added new row for small protocol direct-read route (~5,000-5,500)
- Updated all downstream rows to reflect correct baseline
- Updated practical formula (4 tiers: light / small-protocol / protocol / ssot-heavy)

## Conclusion

| Question | Answer |
|---|---|
| Read load reduced vs bulk-read era? | **YES** |
| Where it worked | Light tasks (-58%); large protocol section routing (-25-40%) |
| Where it was counter-productive | Small protocol index routing (+28-34% overhead) — now fixed |
| README token table accurate? | **No — corrected in this PR** |

## Remaining TODO

- After file growth from these changes, re-measure baseline to confirm it stays within ~4,500-5,000
- index.yaml `usage` note could be updated to reflect that small protocols are out of scope for index routing (low priority — index.yaml is metadata only)
- completion.md single-section case: even large protocol index routing can be marginally more expensive than direct full-read for certain section combinations; acceptable given multi-section use cases are the common case
