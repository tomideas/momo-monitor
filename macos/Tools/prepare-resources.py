#!/usr/bin/env python3
"""Copy approved assets unchanged and generate a runtime snapshot from the canonical JSON."""
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

root = Path(__file__).resolve().parents[2]
out = root / 'macos/Sources/MomoMac/Resources'
out.mkdir(parents=True, exist_ok=True)
design_path = root / 'design-system/design-system.json'
design = json.loads(design_path.read_text())
snapshot = {k: design[k] for k in ['project', 'tokens', 'themes']}
(out / 'DesignTokens.json').write_text(json.dumps(snapshot, ensure_ascii=False, indent=2) + '\n')
manifest = {'designSource': 'design-system/design-system.json', 'assets': {}}
for clip in ['startup', 'mini-click', 'mini-drag']:
    source = root / f'StatusMonitor/Assets/{clip}.frames.zip'
    dest = out / 'Animations' / clip
    dest.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(source) as archive:
        durations = json.loads(archive.read('durations.json'))
        assert len(durations) == 66 and sum(durations) == 6600
        for name in ['durations.json'] + [f'{i:03}.png' for i in range(len(durations))]:
            (dest / name).write_bytes(archive.read(name))
    manifest['assets'][clip] = {'source': str(source.relative_to(root)), 'sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'frames': len(durations), 'milliseconds': sum(durations)}
shutil.copyfile(root / 'StatusMonitor/Assets/momo.png', out / 'momo.png')
fonts = out / 'Fonts'
fonts.mkdir(exist_ok=True)
for name in ['Geist-Regular.ttf', 'Geist-Medium.ttf', 'Geist-Bold.ttf', 'GeistMono-Regular.ttf', 'BarlowCondensed-BlackItalic.ttf', 'NotoSansTC-Regular.otf']:
    shutil.copyfile(root / 'StatusMonitor/Fonts' / name, fonts / name)
(out / 'AssetProvenance.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
license_dir = out / 'Licenses'
license_dir.mkdir(exist_ok=True)
shutil.copyfile(root / 'LICENSE', license_dir / 'Momo-GPLv3.txt')
for license_path in (root / 'macos/Licenses').glob('*.txt'):
    shutil.copyfile(license_path, license_dir / license_path.name)
print('Prepared PAPER POP / VOLT tokens, approved fonts and three unchanged 66-frame animations.')
