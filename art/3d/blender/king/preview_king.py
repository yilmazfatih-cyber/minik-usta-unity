"""Usta Kral — review sheets from the packed frames (package B1k). Standard library + Pillow.

    python3 art/3d/blender/king/preview_king.py [--frames art/3d/out/king/frames] [--turn art/3d/out/king/turn]
                                                [--out artifacts/preview]

Writes
  king-sheet.png          4 drawings of every clip (root-aligned, store size) + the 6 busts
  king-turn.png           K0 turnaround: front, three-quarter, side, back (build_king.py renders)
  king-strip-<clip>.png   every 24 fps timeline frame of the clip in one row (a frame strip instead of a GIF);
                          event frames are marked, loop range underlined
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


def unique(seq):
    out = []
    for s in seq:
        if s not in out:
            out.append(s)
    return out


def place(canvas, im, meta, cx, base_y, scale=1.0):
    """Paste im so that its root pivot lands on (cx, base_y)."""
    if scale != 1.0:
        im = im.convert('RGBa').resize((max(1, int(im.width * scale)), max(1, int(im.height * scale))),
                                       Image.LANCZOS).convert('RGBA')
    x = int(round(cx - meta['pivotX'] * im.width))
    y = int(round(base_y - (1 - meta['pivotY']) * im.height))
    canvas.alpha_composite(im, (max(0, x), max(0, y)))


def card(dr, box, r=18):
    dr.rounded_rectangle(box, radius=r, fill=CARD, outline=EDGE, width=3)


def main():
    frames_dir = os.path.abspath(arg('--frames', os.path.join(REPO, 'art', '3d', 'out', 'king', 'frames')))
    turn_dir = os.path.abspath(arg('--turn', os.path.join(REPO, 'art', '3d', 'out', 'king', 'turn')))
    out = os.path.abspath(arg('--out', os.path.join(REPO, 'artifacts', 'preview')))
    os.makedirs(out, exist_ok=True)
    doc = json.load(open(os.path.join(frames_dir, 'frames.json')))
    fr = doc['frames']
    k0 = doc['k0']
    img = {}

    def load(n):
        if n not in img:
            img[n] = Image.open(os.path.join(frames_dir, n + '.png')).convert('RGBA')
        return img[n]

    clips = [c for c in doc['clips'] if not doc['clips'][c].get('aliasOf')]
    order = ['king_idle', 'king_cheer', 'king_think', 'king_talk', 'king_point', 'king_encourage', 'king_wave',
             'king_big_cheer']
    clips = [c for c in order if c in clips] + [c for c in clips if c not in order]
    f_title, f_lab, f_small = font(30), font(18), font(14)

    # ---- king-sheet.png: 4 drawings per clip ------------------------------------------------------------------
    TW, TH = 300, 400
    rows = [(c, doc['clips'][c]) for c in clips]
    sheet = Image.new('RGBA', (40 + 4 * TW + 300, 90 + len(rows) * (TH + 20) + TH + 40), BG)
    dr = ImageDraw.Draw(sheet)
    dr.text((40, 26), 'Usta Kral - B1k clips (store size, 4 drawings per clip; 24 fps timeline frame in brackets)',
            font=f_title, fill=INK)
    for r, (cn, c) in enumerate(rows):
        tl = c['frames']
        u = [n for n in unique(tl) if n != k0] or [k0]
        if len(u) >= 4:
            pick = [u[int(round(i * (len(u) - 1) / 3.0))] for i in range(4)]
        else:
            pick = (u + [k0] * 4)[:4]
        y0 = 90 + r * (TH + 20)
        dr.text((40 + 4 * TW + 20, y0 + 20), cn, font=f_lab, fill=INK)
        meta = ('%d fr @ %d fps, %d drawings' % (len(tl), c['fps'], len(unique(tl + (c.get('out') or [])))))
        dr.text((40 + 4 * TW + 20, y0 + 48), meta, font=f_small, fill=INK)
        loop = c['loop']
        dr.text((40 + 4 * TW + 20, y0 + 70), 'loop %s' % ('%d-%d' % (loop['start'], loop['end']) if loop else 'no'),
                font=f_small, fill=INK)
        ev = ', '.join('%s %s' % (k, v) for k, v in sorted(c['events'].items(), key=lambda t: int(t[0])))
        dr.text((40 + 4 * TW + 20, y0 + 92), ev[:40], font=f_small, fill=INK)
        if len(ev) > 40:
            dr.text((40 + 4 * TW + 20, y0 + 110), ev[40:80], font=f_small, fill=INK)
        for i, n in enumerate(pick):
            x0 = 40 + i * TW
            card(dr, (x0 + 6, y0, x0 + TW - 6, y0 + TH))
            place(sheet, load(n), fr[n], x0 + TW / 2, y0 + TH - 22)
            fi = tl.index(n) if n in tl else -1
            dr.text((x0 + 16, y0 + 8), '%s [%d]' % (n.replace('chr_king_', ''), fi), font=f_small, fill=INK)
    # busts row
    busts = sorted(n for n in fr if '_bust_' in n)
    y0 = 90 + len(rows) * (TH + 20)
    dr.text((40 + 4 * TW + 20, y0 + 20), 'busts (256 x 320 design)', font=f_lab, fill=INK)
    bw = (4 * TW) // max(1, len(busts))
    for i, n in enumerate(busts):
        x0 = 40 + i * bw
        card(dr, (x0 + 4, y0, x0 + bw - 4, y0 + TH))
        im = load(n)
        s = min((bw - 20) / im.width, (TH - 40) / im.height)
        im2 = im.convert('RGBa').resize((int(im.width * s), int(im.height * s)), Image.LANCZOS).convert('RGBA')
        sheet.alpha_composite(im2, (int(x0 + (bw - im2.width) / 2), int(y0 + TH - 10 - im2.height)))
        dr.text((x0 + 12, y0 + 8), n.replace('chr_king_bust_', ''), font=f_small, fill=INK)
    sheet.save(os.path.join(out, 'king-sheet.png'), optimize=True)

    # ---- king-turn.png ----------------------------------------------------------------------------------------
    views = [('front', 'front 0'), ('34', 'three-quarter 32'), ('side', 'side 90'), ('back', 'back 180')]
    tiles = []
    for v, lab in views:
        p = os.path.join(turn_dir, 'king_turn_%s.png' % v)
        if os.path.exists(p):
            im = Image.open(p).convert('RGBA')
            im = im.convert('RGBa').resize((im.width // 2, im.height // 2), Image.LANCZOS).convert('RGBA')
            tiles.append((lab, im.crop(im.getbbox())))
    if tiles:
        W = sum(t.width for _, t in tiles) + 60 * (len(tiles) + 1)
        H = max(t.height for _, t in tiles) + 140
        tsheet = Image.new('RGBA', (W, H), BG)
        d2 = ImageDraw.Draw(tsheet)
        d2.text((30, 20), 'Usta Kral - K0 turnaround (Blender 4.0.2, Cycles CPU, rig B, Standard view)', font=f_lab,
                fill=INK)
        x = 60
        for lab, t in tiles:
            card(d2, (x - 24, 60, x + t.width + 24, H - 20))
            tsheet.alpha_composite(t, (x, H - 50 - t.height))
            d2.text((x, 70), lab, font=f_small, fill=INK)
            x += t.width + 60
        tsheet.save(os.path.join(out, 'king-turn.png'), optimize=True)

    # ---- king-strip-<clip>.png: every timeline frame ------------------------------------------------------------
    for cn in clips:
        c = doc['clips'][cn]
        tl = c['frames'] + (c.get('out') or [])
        n_main = len(c['frames'])
        sc = 0.5
        tw, th = 130, 200
        st = Image.new('RGBA', (20 + len(tl) * tw, th + 70), BG)
        d3 = ImageDraw.Draw(st)
        d3.text((10, 4), '%s  (%d fps; frames 0-%d%s)' % (cn, c['fps'], n_main - 1,
                                                          ', then out' if c.get('out') else ''), font=f_small, fill=INK)
        for i, n in enumerate(tl):
            x0 = 10 + i * tw
            col = CARD if i < n_main else (236, 244, 255, 255)
            d3.rectangle((x0 + 2, 26, x0 + tw - 2, 26 + th), fill=col, outline=EDGE)
            place(st, load(n), fr[n], x0 + tw / 2, 26 + th - 10, sc)
            lab = str(i) if i < n_main else 'o%d' % (i - n_main)
            d3.text((x0 + 6, 28), lab, font=f_small, fill=INK)
            ev = c['events'].get(str(i)) if i < n_main else None
            if ev:
                d3.text((x0 + 6, 26 + th + 4), ev, font=f_small, fill=ACCENT)
            loop = c['loop']
            if loop and loop['start'] <= i <= loop['end'] and i < n_main:
                d3.line((x0, 26 + th + 26, x0 + tw, 26 + th + 26), fill=ACCENT, width=4)
        st.save(os.path.join(out, 'king-strip-%s.png' % cn.replace('king_', '')), optimize=True)
    print('preview ->', out)


if __name__ == '__main__':
    main()
