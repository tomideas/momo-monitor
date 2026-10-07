"""Register both clips by resting-pose height, horizontal center and foot baseline.

One fixed affine transform per clip preserves its motion. The shared crop fits all
frames, rather than recentering each frame and cancelling the actual performance.
"""
import io
import json
import zipfile
from pathlib import Path
from PIL import Image

clips = {}
for name, source in [('mini-click', 'mini'), ('mini-drag', 'startup')]:
    frames, durations = [], []
    with Image.open(f'StatusMonitor/Assets/{source}.webp') as image:
        for index in range(image.n_frames):
            image.seek(index)
            frames.append(image.convert('RGBA'))
            durations.append(image.info['duration'])
    left, top, right, bottom = frames[0].getbbox()
    center, height = (left + right) / 2, bottom - top
    bounds = [f.getbbox() for f in frames]
    clips[name] = dict(frames=frames, durations=durations, center=center,
                       height=height, bottom=bottom, bounds=bounds)

all_bounds = [( (b[0]-c['center'])/c['height'], (b[1]-c['bottom'])/c['height'],
                (b[2]-c['center'])/c['height'], (b[3]-c['bottom'])/c['height'])
              for c in clips.values() for b in c['bounds']]
min_x, min_y = min(b[0] for b in all_bounds), min(b[1] for b in all_bounds)
max_x, max_y = max(b[2] for b in all_bounds), max(b[3] for b in all_bounds)
baseline = 236
rest_height = min(240 / (max_x-min_x), 228 / -min_y)
if max_y > 0:
    rest_height = min(rest_height, (248-baseline)/max_y)
anchor_x = (256 - (max_x-min_x)*rest_height)/2 - min_x*rest_height
report = dict(canvas=[256,256], centerX=anchor_x, footBaseline=baseline, restHeight=rest_height, clips={})
for name, clip in clips.items():
    scale = rest_height / clip['height']
    matrix = (1/scale, 0, clip['center']-anchor_x/scale,
              0, 1/scale, clip['bottom']-baseline/scale)
    frames = [f.transform((256,256), Image.Transform.AFFINE, matrix, Image.Resampling.BICUBIC) for f in clip['frames']]
    # All original nontransparent bounds fit the shared canvas with safety padding.
    for left, top, right, bottom in clip['bounds']:
        assert 0 <= anchor_x+(left-clip['center'])*scale < anchor_x+(right-clip['center'])*scale <= 256
        assert 0 <= baseline+(top-clip['bottom'])*scale < baseline+(bottom-clip['bottom'])*scale <= 256
    output = Path(f'StatusMonitor/Assets/{name}.webp')
    frames[0].save(output, save_all=True, append_images=frames[1:], duration=clip['durations'], loop=1, lossless=True, exact=True, method=6)
    with Image.open(output) as decoded, zipfile.ZipFile(output.with_suffix('.frames.zip'), 'w', zipfile.ZIP_DEFLATED) as archive:
        timing = []
        for index in range(decoded.n_frames):
            decoded.seek(index)
            frame = decoded.convert('RGBA')
            timing.append(decoded.info['duration'])
            buffer = io.BytesIO(); frame.save(buffer, format='PNG')
            archive.writestr(f'{index:03}.png', buffer.getvalue())
        archive.writestr('durations.json', json.dumps(timing))
        assert sum(timing) == sum(clip['durations']) and decoded.info['loop'] == 1
    report['clips'][name] = dict(scale=scale, firstBounds=frames[0].getbbox(), lastBounds=frames[-1].getbbox(), durationMs=sum(clip['durations']))
Path('dev/verification/mini-animation/alignment.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report))
