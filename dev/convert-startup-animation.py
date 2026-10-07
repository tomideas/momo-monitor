"""Remove only edge-connected white, export WebP and WPF-compatible frames.

Requires Pillow, NumPy and opencv-python-headless. WPF has no built-in animated
WebP decoder, so its lossless PNG frames are generated from the delivered WebP.
"""
import argparse
import io
import json
import sys
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python-packages'))
import cv2
import numpy as np
from PIL import Image, ImageSequence

parser = argparse.ArgumentParser()
parser.add_argument('source', type=Path)
parser.add_argument('--output', type=Path, default=Path('StatusMonitor/Assets/startup.webp'))
parser.add_argument('--square-frames', action='store_true', help='Keep a fixed square WPF playback canvas without stretching the character.')
args = parser.parse_args()
frames, durations = [], []
with Image.open(args.source) as source:
    for frame in ImageSequence.Iterator(source):
        rgba = np.array(frame.convert('RGBA'))
        rgb = rgba[:, :, :3].astype(np.float32)
        neutral = rgb.max(2) - rgb.min(2) < 24
        white = ((rgb.min(2) >= 225) & neutral) | (rgba[:, :, 3] == 0)
        _, labels = cv2.connectedComponents(white.astype(np.uint8), connectivity=4)
        edge_labels = np.unique(np.concatenate((labels[0], labels[-1], labels[:, 0], labels[:, -1])))
        background = np.isin(labels, edge_labels[edge_labels != 0])
        # Feather the outside outline by one pixel and remove its white matte.
        fringe = cv2.dilate(background.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & ~background & neutral
        alpha = np.ones(background.shape, dtype=np.float32)
        alpha[background] = 0
        alpha[fringe] = 1 - rgb.min(2)[fringe] / 255
        unmatte = np.clip((rgb - 255 * (1 - alpha[:, :, None])) / np.maximum(alpha[:, :, None], 1 / 255), 0, 255)
        rgba[fringe, :3] = unmatte[fringe].astype(np.uint8)
        rgba[:, :, 3] = np.minimum(rgba[:, :, 3], np.rint(alpha * 255).astype(np.uint8))
        rgba[background, :3] = 0
        frames.append(Image.fromarray(rgba))
        durations.append(frame.info.get('duration', 100))

# One crop across all frames prevents the mascot moving due to per-frame bounds.
bounds = [frame.getbbox() for frame in frames]
left = max(0, min(b[0] for b in bounds) - 12)
top = max(0, min(b[1] for b in bounds) - 12)
right = min(frames[0].width, max(b[2] for b in bounds) + 12)
bottom = min(frames[0].height, max(b[3] for b in bounds) + 12)
frames = [frame.crop((left, top, right, bottom)) for frame in frames]
args.output.parent.mkdir(parents=True, exist_ok=True)
frames[0].save(args.output, format='WEBP', save_all=True, append_images=frames[1:],
               duration=durations, loop=1, lossless=True, method=6, exact=True)

with Image.open(args.output) as webp, zipfile.ZipFile(args.output.with_suffix('.frames.zip'), 'w', zipfile.ZIP_DEFLATED) as archive:
    decoded_durations = []
    for index in range(webp.n_frames):
        webp.seek(index)
        decoded = webp.convert('RGBA')
        decoded_durations.append(webp.info['duration'])
        decoded.thumbnail((256, 256), Image.Resampling.LANCZOS)
        if args.square_frames:
            canvas = Image.new('RGBA', (256, 256))
            canvas.paste(decoded, ((256 - decoded.width) // 2, (256 - decoded.height) // 2))
            decoded = canvas
        buffer = io.BytesIO()
        decoded.save(buffer, format='PNG')
        archive.writestr(f'{index:03}.png', buffer.getvalue())
    assert webp.info['loop'] == 1
    assert sum(decoded_durations) == sum(durations)
    archive.writestr('durations.json', json.dumps(decoded_durations))
    report = dict(source=str(args.source), frameCount=webp.n_frames, durationMs=sum(decoded_durations),
                  loop=1, size=webp.size, crop=[left, top, right, bottom], transparentBackground=True,
                  squarePlaybackCanvas=args.square_frames)
    args.output.with_suffix('.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report))
