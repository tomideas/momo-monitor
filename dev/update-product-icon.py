"""Copy the approved PNG intact and create Windows icon sizes without distortion."""
import argparse
import shutil
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('source', type=Path)
args = parser.parse_args()
shutil.copyfile(args.source, 'StatusMonitor/Assets/momo.png')
with Image.open(args.source) as source:
    original = source.convert('RGBA')
    side = max(original.size)
    square = Image.new('RGBA', (side, side))
    square.paste(original, ((side - original.width) // 2, (side - original.height) // 2))
    # DIB entries are compatible with the System.Drawing.Icon tray path.
    square.save('StatusMonitor/app.ico', format='ICO', sizes=[(s, s) for s in (16, 24, 32, 48, 64, 128, 256)], bitmap_format='bmp')
print('Product PNG copied unchanged; 7 Windows icon sizes exported.')
