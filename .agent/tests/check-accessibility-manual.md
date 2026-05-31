# Repeatable Accessibility Manual Check — Frontend Projection Surface

**Scope**: `frontend/islands/UiBuilderAdmin.tsx` — UI Builder レイアウトキャンバス
**Standards**: WCAG 2.2 SC 2.1.1, 2.4.3, 2.4.7, 2.5.7, 2.5.8, 3.3.1, 3.3.3, 4.1.3
**Frequency**: Run on every PR that modifies `UiBuilderAdmin.tsx` or accessibility-related components

---

## Prerequisites

- Open the admin UI builder at `/admin/ui-builder` → "レイアウトビルダー" tab
- Use a keyboard-only session (disconnect or ignore mouse during checks)
- Use a screen reader for status message checks (NVDA/JAWS on Windows, VoiceOver on Mac)

---

## Check 1 — Focus Order (WCAG 2.4.3)

**Procedure**:
1. Press Tab from the top of the page.
2. Step through all interactive elements in the Layout Builder tab.
3. Verify the tab order follows reading order: tab bar → undo/redo buttons → palette → canvas nodes → layer tree → inspector → action buttons (Preview → Validate → Apply).

**Pass**: Every interactive element is reachable by Tab/Shift+Tab, in logical reading order.
**Fail**: Any focusable element is skipped, or focus jumps out of logical order.

---

## Check 2 — Focus Visible (WCAG 2.4.7)

**Procedure**:
1. Tab through all elements listed in Check 1.
2. At each stop, verify a visible focus ring is rendered.
   - Canvas nodes: blue ring (`ring-2 ring-blue-300`).
   - Resize handles: blue outline ring (`focus-visible:ring-2 focus-visible:ring-blue-400`).
   - Palette buttons, action buttons, layer tree buttons: `focus-visible` ring styles.

**Pass**: Every focused element shows a clearly visible focus indicator.
**Fail**: Any element receives focus with no visible indicator.

---

## Check 3 — Target Size Minimum (WCAG 2.5.8)

**Procedure**:
1. Inspect resize handles on a selected canvas node.
   - Expected: outer hit area is `h-6 w-6` (24×24 CSS px).
   - Visual indicator inside is `h-3 w-3` (12×12px dot), centered within the 24px area.
2. Inspect palette "+ 追加" buttons and layer tree action buttons.
   - Expected: minimum 24px tall (standard button height).

**Pass**: All interactive targets meet 24×24px minimum.
**Fail**: Any interactive target is smaller than 24×24px and lacks ≥12px offset spacing.

---

## Check 4 — Keyboard Alternatives for Pointer Operations (WCAG 2.1.1, 2.5.7)

**Procedure — Move**:
1. Add a node to the canvas. Select it via Tab/Enter.
2. Press Arrow keys: node must move 10px per step, 50px with Shift.

**Procedure — Resize**:
1. Select a node. Tab to a resize handle (e.g., se).
2. Press Enter or Space: node must resize by +10px in that direction.

**Procedure — Palette Add (non-drag)**:
1. In the palette list, Tab to a "+ 追加" button and press Enter.
2. A new node must appear on the canvas.

**Procedure — Layer Reorder**:
1. In the layer tree, Tab to ↑/↓ buttons and press Enter to reorder.

**Procedure — Delete**:
1. Select a node. Press Delete or Backspace. Node must be removed.

**Pass**: All five operations succeed without using a pointer device.
**Fail**: Any operation requires mouse/touch to complete.

---

## Check 5 — Status Messages (WCAG 4.1.3)

**Procedure**:
1. Enable screen reader.
2. Perform: add node → validate → apply (or trigger an error).
3. Without moving focus, verify the screen reader announces:
   - "プレビューを実行中..." (during preview)
   - The result message (e.g. "成功" or error text) on completion.
   - "〇〇をキャンバスに追加しました" on node add.

**Pass**: Status messages are announced without requiring focus movement.
**Fail**: Status messages are not announced, or require focus to move.

---

## Check 6 — Error Identification and Suggestion (WCAG 3.3.1, 3.3.3)

**Procedure**:
1. Add a draft-only node (未登録). Attempt to apply.
2. Verify the error panel shows:
   - Error reason in plain language (not just the code `DRAFT_ONLY_NODES`).
   - The affected component name.
   - A repair suggestion.
3. The panel must use `role="alert"`.

**Pass**: Error panel renders cause + componentKey + repair suggestion; uses role="alert".
**Fail**: Only raw error code shown, or no repair suggestion, or missing role="alert".

---

## Pass/Fail Record

| Check | Date | Tester | Result | Notes |
|-------|------|--------|--------|-------|
| Focus Order | — | — | — | — |
| Focus Visible | — | — | — | — |
| Target Size | — | — | — | — |
| Keyboard Alternatives | — | — | — | — |
| Status Messages | — | — | — | — |
| Error Identification | — | — | — | — |

All six checks must pass before marking `accessibility_observability: status: implemented`.
