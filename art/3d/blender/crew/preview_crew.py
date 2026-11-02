"""Tuna and Kepche — review sheets from the trimmed frames (art turn 1). Standard library + Pillow.

    python3 art/3d/blender/crew/preview_crew.py [--frames art/3d/out/crew/frames] [--out art/3d/blender/crew/preview]

Writes crew-sheet.png (6 drawings of every clip, root-aligned at store size, plus the stills and the bust) and
crew-strip-<clip>.png (every 24 fps timeline frame of the clip in one row; event frames marked).
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
BG = (246, 240, 226, 255)
CARD = (255, 251, 242, 255)
EDGE = (226, 212, 182, 255)
INK = (74, 46, 20, 255)
ACCENT = (41, 132, 222, 255)


def arg(name, default):
    a = sys.argv[1:]
    return a[a.index(name) + 1] if name in a else default


def font(size):
    for p in ('/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
              '/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf'):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def main():
    fr = os.path.abspath(arg('--frames', os.path.join(REPO, 'art', '3d', 'out', 'crew', 'frames')))
    out = os.path.abspath(arg('--out', os.path.join(HERE, 'preview')))
    os.makedirs(out, exist_ok=True)
    doc = json.load(open(os.path.join(fr, 'frames.json')))
    meta = doc['frames']
    cache = {}

    def img(n):
        if n not in cache:
            cache[n] = Image.open(os.path.join(fr, n + '.png')).convert('RGBA')
        return cache[n]

    def place(canvas, n, cx, by):
        """draw frame n with its pivot at (cx, by) (canvas px, by = baseline from the top)."""
        m = meta[n]
        im = img(n)
        x = round(cx - m['pivotX'] * m['w'])
        y = round(by - (1 - m['pivotY']) * m['h'])
        canvas.alpha_composite(im, (x, y))

    f_lab, f_small = font(26), font(18)
    clips = [c for c in doc['clips'] if not doc['clips'][c].get('aliasOf')]
    cw, ch = 300, 320
    rows = len(clips) + 1
    sheet = Image.new('RGBA', (60 + 6 * (cw + 16) + 300, 90 + rows * (ch + 20)), BG)
    dr = ImageDraw.Draw(sheet)
    dr.text((40, 26), 'Tuna + Kepce - art turn 1 (Blender 4.0.2, Cycles, rig B; store size 112 px/BU)', font=f_lab, fill=INK)
    for r, cn in enumerate(clips):
        c = doc['clips'][cn]
        y0 = 80 + r * (ch + 20)
        seen = []
        for n in c['frames']:
            if n not in seen:
                seen.append(n)
        pick = [seen[round(i * (len(seen) - 1) / 5)] for i in range(6)]
        for i, n in enumerate(pick):
            x0 = 40 + i * (cw + 16)
            dr.rounded_rectangle((x0, y0, x0 + cw, y0 + ch), 16, fill=CARD, outline=EDGE, width=3)
            place(sheet, n, x0 + cw / 2, y0 + ch - 30)
            dr.text((x0 + 12, y0 + 8), '%s [%d]' % (n.replace('chr_', ''), c['frames'].index(n)), font=f_small, fill=INK)
        tx = 40 + 6 * (cw + 16) + 10
        dr.text((tx, y0 + 10), cn, font=f_lab, fill=ACCENT)
        dr.text((tx, y0 + 46), '%d fr @ %d fps, %d drawings' % (len(c['frames']), c['fps'], len(seen)), font=f_small, fill=INK)
        dr.text((tx, y0 + 70), ', '.join('%s %s' % (k, v) for k, v in c['events'].items()), font=f_small, fill=INK)
    y0 = 80 + len(clips) * (ch + 20)
    stills = [n for n in meta if n.startswith('chr_tuna_still_')] + [n for n in meta if '_bust' in n]
    for i, n in enumerate(stills):
        x0 = 40 + i * (cw + 16)
        dr.rounded_rectangle((x0, y0, x0 + cw, y0 + ch), 16, fill=CARD, outline=EDGE, width=3)
        if '_bust' in n:
            im = img(n)
            sheet.alpha_composite(im, (round(x0 + cw / 2 - im.width / 2), round(y0 + ch / 2 - im.height / 2)))
        else:
            place(sheet, n, x0 + cw / 2, y0 + ch - 30)
        dr.text((x0 + 12, y0 + 8), n.replace('chr_', ''), font=f_small, fill=INK)
    sheet.save(os.path.join(out, 'crew-sheet.png'), optimize=True)
    for cn in clips:
        c = doc['clips'][cn]
        n = len(c['frames'])
        sw, sh = 130, 250
        st = Image.new('RGBA', (20 + n * sw, sh + 40), BG)
        d2 = ImageDraw.Draw(st)
        for i, nm in enumerate(c['frames']):
            x0 = 10 + i * sw
            d2.rounded_rectangle((x0, 30, x0 + sw - 6, 30 + sh), 10, fill=CARD, outline=ACCENT if str(i) in c['events'] else EDGE,
                                 width=3)
            im = img(nm)
            k = min(1.0, (sw - 12) / im.width, (sh - 20) / im.height)
            small = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
            m = meta[nm]
            st.alpha_composite(small, (round(x0 + (sw - 6) / 2 - m['pivotX'] * small.width), round(30 + sh - 10 - (1 - m['pivotY']) * small.height)))
            d2.text((x0 + 6, 6), str(i), font=f_small, fill=INK)
        st.save(os.path.join(out, 'crew-strip-%s.png' % cn), optimize=True)
    print('preview ->', out)


if __name__ == '__main__':
    main()
