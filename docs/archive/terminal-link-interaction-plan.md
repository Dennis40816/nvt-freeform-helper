# Terminal Link Interaction Plan
Last updated: 2026-02-20

## Goal
- Make Terminal link interaction stable and verifiable, close to the VSCode feel:
  - Normal state: blue text (no underline)
  - Hover: underline + hand cursor
  - Click: open directly (URL / file / path)

## Current Status
- URL, file, and path link parsing and click-to-open are supported.
- Hover hand cursor and hover-only underline are supported (controlled by the colorizer).
- A runtime command is provided:
  - `freeformhelper.exe query terminal-links --tail 300 --limit 300`
  - Use it to check the link spans parsed from the current console and their targets.

## Risk Points (why it may look inconsistent)
- AvaloniaEdit's `DocumentColorizingTransformer` belongs to the text redraw pipeline. Hover updates may be affected by redraw timing.
- Link hit-testing and link underline both depend on in-line positioning in TextView. If the mouse moves quickly, they may briefly fall out of sync.

## Testable Plan (verify first, then decide)
1. Parser correctness (no UI)
- Use `query terminal-links` to verify:
  - Whether `lineNumber/startOffset/length/target/isUrl` match expectations
  - Whether different types (URL, absolute path, relative path, trailing punctuation) are trimmed and parsed correctly

2. Open behavior correctness (semi-automatic)
- Click a known link in Terminal and check the log:
  - `Console link open requested`
  - `Console link resolved`
  - Success or failure reason

3. Hover visual consistency (manual)
- Focus on whether the underline always appears when the hand cursor appears:
  - High-frequency mouse movement
  - After horizontal scrolling
  - When search highlighting is active at the same time

## Low-Priority Fallback (if still unstable)
- Option A: Add a separate overlay underline renderer on TextView (does not depend on colorizer text decoration).
- Option B: Switch to Tokenized Run rendering (high control, but larger refactoring cost).
- Option C: Keep hand cursor and color, without relying strictly on underline (short-term UX downgrade).

## Acceptance Criteria
- `query terminal-links` reliably outputs the current list of clickable links.
- Click-to-open success is reproducible (URL / file / path).
- When hover hits a link, the underline no longer goes missing intermittently (or the fallback is explicitly adopted and documented).

