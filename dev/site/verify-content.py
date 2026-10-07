"""Validate generated guide links, anchors, media, English copy and token ownership."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit, unquote
import json
import re
import hashlib
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
site = root / 'site'
version = ET.parse(root / 'StatusMonitor/StatusMonitor.csproj').findtext('./PropertyGroup/Version')

class Page(HTMLParser):
    def __init__(self, path):
        super().__init__()
        self.path, self.ids, self.links, self.images = path, set(), [], []
        self.feed(path.read_text(encoding='utf-8'))
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if 'id' in attrs:
            assert attrs['id'] not in self.ids, (self.path.name, 'duplicate id', attrs['id'])
            self.ids.add(attrs['id'])
        if 'href' in attrs:
            self.links.append(attrs['href'])
        if 'src' in attrs:
            self.links.append(attrs['src'])
        if tag == 'img':
            assert 'alt' in attrs and int(attrs['width']) > 0 and int(attrs['height']) > 0
            self.images.append(attrs['src'])

pages = {p.name: Page(p) for p in site.glob('*.html') if p.name != 'zh.html'}
checks = []
for name, page in pages.items():
    source = page.path.read_text(encoding='utf-8')
    assert f'<span>{version}</span>' in source, (name, 'stale version')
    assert not re.search(r'[\u3400-\u9fff]', source), (name, 'non-English copy')
    for link in page.links:
        parts = urlsplit(link)
        if parts.scheme or parts.netloc:
            continue
        dest = (page.path.parent / unquote(parts.path)).resolve() if parts.path else page.path
        assert dest.is_file(), (name, 'missing local resource', link)
        if parts.fragment and dest.name in pages:
            assert parts.fragment in pages[dest.name].ids, (name, 'missing anchor', link)
    if name != 'troubleshooting.html':
        assert 'assets/images/sensor-access-en.png' not in page.images
    checks.append(f'PASS links, anchors, media dimensions, English copy and version: {name}')

ds = json.loads((root / 'design-system/design-system.json').read_text(encoding='utf-8-sig'))
css = (site / 'guide-tokens.css').read_text(encoding='utf-8')
exported = dict(re.findall(r'(--[\w-]+):\s*([^;]+);', css))
for key, value in exported.items():
    assert value == ds['tokens'][key], (key, 'token drift')
checks.append(f'PASS {len(exported)} token exports match the canonical design-system JSON')
media = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (site / 'assets/images').glob('*en.png')}
report = dict(pages=len(pages), version=version, checks=checks, screenshotHashes=media)
out = root / 'dev/site/verification/content-checks.json'
out.parent.mkdir(exist_ok=True)
out.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(f'PASS {len(pages)} English pages, all local resources and anchors, {len(exported)} canonical tokens. Error-state screenshot is confined to Troubleshooting.')
