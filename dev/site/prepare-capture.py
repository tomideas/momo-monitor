"""Adapt the actual App.xaml for the read-only screenshot helper before building it."""
from pathlib import Path
import re
root = Path(__file__).resolve().parents[2]
text = (root / 'StatusMonitor/App.xaml').read_text(encoding='utf-8-sig')
text = text.replace('<Application x:Class="StatusMonitor.App"', '<ResourceDictionary')
text = text.replace('<Application.Resources>', '').replace('</Application.Resources>', '')
text = text.replace('</Application>', '</ResourceDictionary>')
text = re.sub(r'clr-namespace:(StatusMonitor[^\"]*)', r'clr-namespace:\1;assembly=MomoMonitor', text)
(root / 'dev/site/Capture/Resources.xaml').write_text('<!-- Generated from StatusMonitor/App.xaml; do not edit. -->\n' + text, encoding='utf-8')
print('Prepared capture resources from current App.xaml.')
