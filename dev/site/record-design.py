"""Merge documentation ownership into the existing canonical design JSON."""
from pathlib import Path
from datetime import datetime, timezone
import json
import os

root = Path(__file__).resolve().parents[2]
target = root / 'design-system/design-system.json'
data = json.loads(target.read_text(encoding='utf-8-sig'))
tokens = data['tokens'].copy()
themes = json.loads(json.dumps(data['themes']))
component = {
    'name': 'English user guide',
    'description': '15-page offline English documentation using the saved PAPER POP design, local Geist fonts, native English operation screenshots and a five-step user-paced walkthrough. Product screenshots show normal administrator sensor access; a restricted-access example appears only in troubleshooting.',
    'source': 'site/guide.css; site/guide.js; dev/site/build-guide.py; dev/site/capture-guide.ps1',
    'mapping': 'design-system/design-system.json tokens → dev/site/build-guide.py → generated site/guide-tokens.css → shared site/guide.css. Documentation preserves WPF token values and uses a 16px reading scale with 1.7 line height for prose; desktop sensor text sizes remain unchanged.',
    'variants': 'Desktop with topic sidebar and page outline / mobile drawer / print / no JavaScript; product illustrations in PAPER POP and VOLT',
    'states': 'Current topic / search results / no results / clear search / open and closed mobile navigation / walkthrough endpoints / clipboard fallback',
    'usage': 'Edit guide content in dev/site/build-guide.py and rebuild. Capture screenshots with administrator access through the installed PawnIO driver, always in read-only --render mode. Use unavailable-driver screenshots only to explain troubleshooting.',
    'verification': '2026-10-07: current WPF application and screenshot helper built; 13 native English views recaptured with administrator sensor access. dev/site/verification/browser-checks.json verifies all 15 pages offline, desktop and 390px widths, search, keyboard, mobile navigation, walkthrough, clipboard failure, print and no-JavaScript reading. dev/site/verification/content-checks.json checks local links, anchors, English copy, current version, image dimensions and canonical token drift. dev/site/premium-audit.json: zero findings.'
}
for index, old in enumerate(data['components']):
    if old.get('name') == component['name']:
        data['components'][index] = {**old, **component}
        break
else:
    data['components'].append(component)
data['updatedAt'] = datetime.now(timezone.utc).isoformat(timespec='milliseconds').replace('+00:00', 'Z')
data['updatedBy'] = 'Codex'
temp = target.with_name('design-system.guide.tmp')
temp.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
os.replace(temp, target)
verified = json.loads(target.read_text(encoding='utf-8'))
assert verified['tokens'] == tokens and verified['themes'] == themes
assert any(c.get('name') == component['name'] and c.get('mapping') == component['mapping'] for c in verified['components'])
print('Verified documentation component mapping in canonical design JSON; product tokens and themes preserved.')
