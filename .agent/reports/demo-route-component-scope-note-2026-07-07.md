# Demo route component scope note

```text
repo:
  github.com/tk-ud/topolactor

parent_active_report:
  .agent/reports/frontend-ui-audit-bundle-semantic-frame.md

parent_gap_report:
  .agent/reports/ui-projection-surface-gap-audit-2026-07-07.md

status:
  audit_note
```

```text
finding:
  Existing /demo route ownership can cause semantic misclassification.

active_report_boundary:
  /demo is non-canonical.
  /demo has no seed replacement.

preferred_direction:
  Keep reusable projection inspection as UI Builder component scope.

reason:
  Component scope provides inspection without independent route/domain meaning.
  Component scope aligns with UI Builder as an authoring/projection aid.

before_implementation:
  Search SSOT/docs for /demo references.
  If a design document treats /demo as canonical route or product projection surface, handle that as design alignment work before implementation.

OK:
  Projection inspection is read-only UI Builder component/panel/tab.
  It confirms applied topology, route, package, manifest, read-query, propBindings, rows, activeColumns, and displayColumnMode.

NG:
  Standalone /demo route is treated as product projection proof.
  Old wording is used to keep /demo as product route.
  A demo seed is added as replacement.
```
