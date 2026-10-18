import json
import sys

src = 'C:/Dennis/dev/work/novatek/nfh-a12-work/notch-c/pr113.json'
out = 'C:/Dennis/dev/work/novatek/nfh-a12-work/notch-c/pr113-body-new.md'
review_line = sys.argv[1] if len(sys.argv) > 1 else ''
head = '5af6584481ecc1c7031b7a5f463e1892b849c405'
assets = 'https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/5c9c5cf0774f6e72cfad06d3bc2a5e83451305c6/console-adoption/after-core-0.9.4/'

b = json.load(open(src, encoding='utf-8-sig'))['body'].replace('\r\n', '\n')

# 1. After images: the Core 0.9.4 toolbar.
for theme in ('light', 'dark'):
    for state in ('expanded', 'collapsed'):
        old = 'https://raw.githubusercontent.com/Dennis40816/nvt-freeform-helper/09303f76fa9af599dec8c7050dd03e34fe02f61a/console-adoption/after/%s-%s.png' % (theme, state)
        assert old in b, old
        b = b.replace(old, assets + '%s-%s.png' % (theme, state))
b = b.replace('Light and Dark, expanded and collapsed. The log lines are synthetic.',
              'Light and Dark, expanded and collapsed. The log lines are synthetic. The "after" images use Core 0.9.4. The "before" images are the old console on main.')

# 2. Core 0.9.4 paragraph after the summary.
anchor = '## Before and after'
add = ('## Core 0.9.4\n\n'
       'This PR pins `Nvt.Core` and `Nvt.Core.Avalonia` 0.9.4 (sha256 verified by `fetch-core-packages.ps1`; fonts stay 0.1.0). '
       'Core 0.9.3 reworked the console toolbar. Level filter, search, source filter and the copy, save and zoom actions now share one row, '
       'where 0.9.2 used two rows at this width. The console row height tokens are unchanged (300 DIP default, 232 DIP minimum). '
       'The list now shows all six sample rows with room to spare, so the owner can lower the default height later without losing rows. '
       'The rendered visual baseline of `MainWindow.ConsoleExpanded` was re-signed for this change.\n\n')
assert anchor in b
b = b.replace(anchor, add + anchor, 1)

# 3. Row height bullet: the toolbar numbers belong to 0.9.2.
old_row = 'The Core header takes 48 DIP and the filter row takes 88 DIP at this width, so 180 DIP left no room for the list.'
assert old_row in b
b = b.replace(old_row, 'The Core header and the toolbar need more room than the old text console, so 180 DIP left no room for the list.')

# 4. Ledgers table.
b = b.replace('| `eng/code-health/baseline.json` findings | 1842 | 1778 |', '| `eng/code-health/baseline.json` findings | 1752 | 1693 |')
b = b.replace('| `eng/code-health/baseline.json` entities | 143 | 140 |', '| `eng/code-health/baseline.json` entities | 141 | 138 |')
b = b.replace('| `eng/code-health/test-debt.json` findings | 148 | 144 |', '| `eng/code-health/test-debt.json` findings | 148 | 144 |')
b = b.replace('| `FreeformHelperViewModel` state members | 323 | 322 |',
              '| `debt-baseline.json` `freeformViewModelStateMembers` | 96 | 95 |\n'
              '| `debt-baseline.json` `processStart` | 4 | 1 |\n'
              '| `debt-baseline.json` `viewModelsAvalonia` offenders | 11 | 10 |')
b = b.replace('`LowerBaseline` passed: only existing debt was deleted or lowered. Nothing goes up.',
              '`LowerBaseline` passed on head `%s` against main `5d5dff58`: only existing debt was deleted or lowered. Nothing goes up. "Before" is main, "after" is this head.' % head[:7])

# 5. Verification: add the current head and mark the older heads as history.
v_anchor = '- CI runs the full test suite on this PR.'
assert v_anchor in b
v_new = ('- Head `%s`: main `5d5dff5` (PR 197 to 200) merged in, Core pinned to 0.9.4. `LowerBaseline` and `Verify` (base `origin/main`) exit 0, '
         '`./scripts/tests/lint.ps1 -UseNoAppHost` exit 0. The full local test run found one failure, `DebtBaselineTests` (the merge left the state member ceiling at 96, the offender list has 95). '
         'The ceiling is now 95 and the architecture tests pass (50 of 50). The other 2905 tests passed. '
         'Before that, the first run on this merge had found four failures that are fixed: two ledger counts, the console link offenders and the console visual baseline.\n') % head[:7]
b = b.replace(v_anchor, v_new + v_anchor, 1)
b = b.replace('`LowerBaseline` and `Verify` (base `33073ba`) exit 0 and lint exits 0 on head `4ef0a12`.',
              '`LowerBaseline` and `Verify` (base `33073ba`) exit 0 and lint exits 0 on head `4ef0a12`. This is history: the branch was merged with the current main afterwards (see the head `%s` line).' % head[:7])

# 6. Review: add the pending item, replace the record line.
r_anchor = '\nReview record: 4ef0a123fdb0d3c9d5beb476e03a5e02f5ce276f accept\n'
assert r_anchor in b
if review_line:
    b = b.replace(r_anchor, '\n' + review_line + '\n\nReview record: %s accept\n' % head)
else:
    b = b.replace(r_anchor, '\n9. Head `%s` (merge of main, Core 0.9.4, ledgers, re-signed console snapshot): review pending.\n' % head[:7])


# 7. Removed list: main's typed-catch slice added a link target search for the old code.
rm_anchor = '- The console tokens, styles and colors that no code uses any more.'
assert rm_anchor in b
b = b.replace(rm_anchor, rm_anchor + '\n- `ConsoleLinkTargetSearch` and `FreeformHelperViewConsoleLinkFailureTests`. Main added them after this branch was cut, for the old link code that this PR removes. The Core scanner and `ConsoleLinkOpener` replace them (declared differences 4, 8, 13 and 14).')

# 8. Text that Core 0.9.3 made stale (review of head 5af6584).
for old, new in [
    ('That uses the Core narrow layout (below 960 DIP) and is above the 640 DIP minimum.',
     'That is the middle toolbar tier of Core 0.9.3 (700 to 1179 DIP) and is above the 640 DIP minimum.'),
    ('It does not scale the header, the toolbar or the jump button.',
     'It does not scale the header or the toolbar. The toolbar has the zoom out, zoom value (reset to 100%), zoom in and Jump to latest buttons.'),
    ('The Core header and the toolbar need more room than the old text console,',
     'The Core header and the toolbar are 40 DIP high each and need more room than the old text console,'),
]:
    assert old in b, old
    b = b.replace(old, new)

# The tier line stays A.
open(out, 'w', encoding='utf-8', newline='\n').write(b)
print(len(b))
