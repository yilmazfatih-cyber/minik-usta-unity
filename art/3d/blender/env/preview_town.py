"""Art turn 1 review sheets from the PACKED fx.json (so they also check the stage contract). Standard library + Pillow.

    python3 art/3d/blender/env/preview_town.py [--out art/3d/blender/env/preview]

Writes
  t1-town-stages.png   fx.json stages.town_ch1: base + parts[0 .. n-1] stacked at the shared pivot, n = 0, 1, ... 10
  t1-bld-pieces.png    the ten win-window pieces (levelPieces) at design size on the window colour
  t1-fx-reward.png     fx_star_burst and fx_segment_done drawings placed by the clip pivot
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
RES = os.path.join(ROOT, 'Assets', 'MinikUsta', 'Resources')
argv = sys.argv[1:]
OUT = argv[argv.index('--out') + 1] if '--out' in argv else os.path.join(HERE, 'preview')


def font(size):
    for p in ('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
              '/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf'):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def main():
    os.makedirs(OUT, exist_ok=True)
    j = json.load(open(os.path.join(RES, 'art3d', 'fx.json')))
    pages = [Image.open(os.path.join(RES, a['file'] + '.bytes')).convert('RGBA') for a in j['atlases']]

    def sprite(name, k=1.0):
        f = j['frames'][name]
        im = pages[f['atlas']].crop((f['x'], f['y'], f['x'] + f['w'], f['y'] + f['h']))
        s = k / f['scale']  # stored px -> design px x k
        im = im.resize((max(1, round(f['w'] * s)), max(1, round(f['h'] * s))), Image.LANCZOS)
        return im, f, s

    def place(canvas, name, px, py, k):
        """pivot of `name` at (px, py) canvas px (py from the top)."""
        im, f, s = sprite(name, k)
        canvas.alpha_composite(im, (round(px - f['pivotX'] * im.width), round(py - (1 - f['pivotY']) * im.height)))

    st = j['stages']['town_ch1']
    k = 0.36
    cw, chh = round(760 * k), round(820 * k)
    sheet = Image.new('RGBA', (20 + 6 * (cw + 10), 60 + 2 * (chh + 40)), (120, 186, 96, 255))
    d = ImageDraw.Draw(sheet)
    d.text((16, 14), 'Agac Ev stages (fx.json stages.town_ch1: base + parts[0..n-1] at one pivot)', font=font(22), fill=(30, 40, 20, 255))
    for n in range(11):
        r, c = divmod(n, 6)
        x0, y0 = 20 + c * (cw + 10), 60 + r * (chh + 40)
        place(sheet, st['base'], x0 + cw / 2, y0 + chh, k)
        for p in st['parts'][:n]:
            place(sheet, p, x0 + cw / 2, y0 + chh, k)
        d.text((x0 + 6, y0 + chh + 6), '%d / 10' % n, font=font(18), fill=(30, 40, 20, 255))
    sheet.save(os.path.join(OUT, 't1-town-stages.png'), optimize=True)

    pieces = st['levelPieces']
    k = 0.6
    s2 = Image.new('RGBA', (20 + 5 * (216 + 12), 60 + 2 * (216 + 40)), (82, 62, 213, 255))
    d = ImageDraw.Draw(s2)
    d.text((16, 14), 'Win-window structure pieces (levelPieces; 360 design px shown at 0.6)', font=font(20), fill=(255, 246, 227, 255))
    for i, (lv, nm) in enumerate(sorted(pieces.items(), key=lambda kv: int(kv[0]))):
        r, c = divmod(i, 5)
        x0, y0 = 20 + c * (216 + 12), 60 + r * (216 + 40)
        d.rounded_rectangle((x0, y0, x0 + 216, y0 + 216), 18, fill=(255, 246, 227, 255))
        place(s2, nm, x0 + 108, y0 + 108, k)
        d.text((x0 + 4, y0 + 220), 'L%s %s' % (lv, nm.replace('bld_', '').replace('_3d', '')), font=font(15), fill=(255, 246, 227, 255))
    s2.save(os.path.join(OUT, 't1-bld-pieces.png'), optimize=True)

    k = 0.42
    rows = ['fx_star_burst', 'fx_segment_done']
    n = max(len(set(j['clips'][c]['frames'])) for c in rows)
    cell = round(512 * k)
    s3 = Image.new('RGBA', (20 + n * (cell + 6), 50 + len(rows) * (cell + 10)), (40, 32, 96, 255))
    d = ImageDraw.Draw(s3)
    d.text((16, 12), 'Reward bursts (fx.json): fat gold / cyan rings + 3D candy stars, every drawing at its pivot', font=font(18), fill=(255, 246, 227, 255))
    for r, cn in enumerate(rows):
        seen = []
        for fnm in j['clips'][cn]['frames']:
            if fnm not in seen:
                seen.append(fnm)
        for i, fnm in enumerate(seen):
            x0, y0 = 20 + i * (cell + 6), 50 + r * (cell + 10)
            d.rectangle((x0, y0, x0 + cell, y0 + cell), outline=(70, 60, 140, 255))
            place(s3, fnm, x0 + cell / 2, y0 + cell / 2, k)
    s3.save(os.path.join(OUT, 't1-fx-reward.png'), optimize=True)
    print('preview_town ->', OUT)


if __name__ == '__main__':
    main()
