"""B1 block acceptance (docs/ART_3D.md 2.5) on the stored tiles (art/3d/out/env/atlas):
top-face colour dE00 vs the palette, lip / base ratio, symbol contrast on the stud, colour-blind separation of the measured
top colours (Machado 2009, severity 1.0; CIEDE2000), seam continuity of 12 composed pieces.

    python3 art/3d/blender/env/measure_blocks.py [--atlas art/3d/out/env/atlas] [--out art/3d/blender/env/measure_blocks.txt]
"""
import json
import math
import os
import statistics
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
argv = sys.argv[1:]
A = argv[argv.index('--atlas') + 1] if '--atlas' in argv else os.path.join(ROOT, 'art', '3d', 'out', 'env', 'atlas')
OUTF = argv[argv.index('--out') + 1] if '--out' in argv else os.path.join(HERE, 'measure_blocks.txt')
PAL = {'w': '#764012', 'y': '#FFE23E', 'g': '#44D088', 'r': '#CF404E', 'o': '#EC8D20', 'c': '#C8CFDA', 'b': '#2984DE', 'p': '#5D3AAE'}
INK_WHITE = {'w', 'r', 'b', 'p'}


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def lin1(c):
    c /= 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def srgb1(v):
    v = min(1.0, max(0.0, v))
    return 255 * (12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055)


def lab(rgb):
    r, g, b = (lin1(c) for c in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    return 116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))


def de00(c1, c2):
    L1, a1, b1 = lab(c1)
    L2, a2, b2 = lab(c2)
    C1, C2 = math.hypot(a1, b1), math.hypot(a2, b2)
    Cb = (C1 + C2) / 2
    G = 0.5 * (1 - math.sqrt(Cb ** 7 / (Cb ** 7 + 25 ** 7)))
    a1p, a2p = a1 * (1 + G), a2 * (1 + G)
    C1p, C2p = math.hypot(a1p, b1), math.hypot(a2p, b2)
    h1p = math.degrees(math.atan2(b1, a1p)) % 360
    h2p = math.degrees(math.atan2(b2, a2p)) % 360
    dL, dC = L2 - L1, C2p - C1p
    dh = h2p - h1p
    if C1p * C2p == 0:
        dh = 0
    elif dh > 180:
        dh -= 360
    elif dh < -180:
        dh += 360
    dH = 2 * math.sqrt(C1p * C2p) * math.sin(math.radians(dh / 2))
    Lb, Cbp = (L1 + L2) / 2, (C1p + C2p) / 2
    hb = (h1p + h2p) / 2 if abs(h1p - h2p) <= 180 else (h1p + h2p + 360) / 2
    if C1p * C2p == 0:
        hb = h1p + h2p
    T = 1 - 0.17 * math.cos(math.radians(hb - 30)) + 0.24 * math.cos(math.radians(2 * hb)) + 0.32 * math.cos(math.radians(3 * hb + 6)) - 0.2 * math.cos(math.radians(4 * hb - 63))
    dth = 30 * math.exp(-(((hb - 275) / 25) ** 2))
    Rc = 2 * math.sqrt(Cbp ** 7 / (Cbp ** 7 + 25 ** 7))
    Sl = 1 + 0.015 * (Lb - 50) ** 2 / math.sqrt(20 + (Lb - 50) ** 2)
    Sc = 1 + 0.045 * Cbp
    Sh = 1 + 0.015 * Cbp * T
    Rt = -math.sin(math.radians(2 * dth)) * Rc
    return math.sqrt((dL / Sl) ** 2 + (dC / Sc) ** 2 + (dH / Sh) ** 2 + Rt * (dC / Sc) * (dH / Sh))


MACHADO = {  # severity 1.0 (Machado, Oliveira, Fernandes 2009)
    'protan': ((0.152286, 1.052583, -0.204868), (0.114503, 0.786281, 0.099216), (-0.003882, -0.048116, 1.051998)),
    'deutan': ((0.367322, 0.860646, -0.227968), (0.280085, 0.672501, 0.047413), (-0.011820, 0.042940, 0.968881)),
    'tritan': ((1.255528, -0.076749, -0.178779), (-0.078411, 0.930809, 0.147602), (0.004733, 0.691367, 0.303900)),
}


def sim(rgb, m):
    l = [lin1(c) for c in rgb]
    return tuple(srgb1(sum(m[i][j] * l[j] for j in range(3))) for i in range(3))


def lum(rgb):
    r, g, b = (lin1(c) for c in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def median_rgb(px):
    return tuple(statistics.median(p[i] for p in px) for i in range(3))


def patch(im, x0, x1, v0, v1):
    w, h = im.size
    out = []
    for y in range(round(h * (1 - v1)), round(h * (1 - v0))):
        for x in range(round(w * x0), round(w * x1)):
            p = im.getpixel((x, y))
            if p[3] > 250:
                out.append(p[:3])
    return out


def main():
    lines = []
    tops = {}
    worst = 0
    meta = json.load(open(os.path.join(A, 'meta.json')))
    for c, base in PAL.items():
        im = Image.open(os.path.join(A, 'blk_%s_00.png' % c)).convert('RGBA')
        top = median_rgb(patch(im, 0.08, 0.22, 0.38, 0.78))
        lip = median_rgb(patch(im, 0.3, 0.7, 0.04, 0.14))
        tops[c] = top
        d = de00(hx(base), top)
        worst = max(worst, d)
        ratio = sum(lip) / max(1, sum(hx(base)))
        # symbol contrast on the stud top (ellipse around the anchor): ink cluster vs stud base cluster
        m = meta['blk_%s_00' % c]
        ax, ay = m['anchorX'], m['anchorY']
        st = []
        w, h = im.size
        for y in range(h):
            for x in range(w):
                u, v = x / w, 1 - y / h
                if ((u - ax) / 0.17) ** 2 + ((v - ay) / 0.135) ** 2 <= 1:
                    st.append(im.getpixel((x, y))[:3])
        ls = sorted(lum(p) for p in st)
        n = len(ls)
        # thin strokes after the 150 px downsize: the ink core is the extreme 3 % of the stud top, the body its median
        li = ls[int(n * 0.97)] if c in INK_WHITE else ls[int(n * 0.03)]
        lb = lum(top)  # the stud cap shares the block material: its unprinted colour = the measured top face
        cr = (max(li, lb) + 0.05) / (min(li, lb) + 0.05)
        lines.append('%s base %s top #%02X%02X%02X dE00 %5.2f  lip/base %.2f  symbol contrast %.2f:1' % (c.upper(), base, *[round(t) for t in top], d, ratio, cr))
    lines.append('WORST_DE00 %.2f' % worst)
    for k, mm in MACHADO.items():
        sims = {c: sim(t, mm) for c, t in tops.items()}
        pairs = [(de00(sims[a], sims[b]), a, b) for i, a in enumerate(sims) for b in list(sims)[i + 1:]]
        mn = min(pairs)
        lines.append('CVD %s worst pair %s-%s dE00 %.1f' % (k, mn[1].upper(), mn[2].upper(), mn[0]))
    # seams: 12 pieces composed from the tiles; mean |delta| across seams vs inside tiles (opaque rows/cols only)
    shapes = {'I2': [(0, 0), (1, 0)], 'I3': [(0, 0), (1, 0), (2, 0)], 'L3': [(0, 0), (0, 1), (1, 0)], 'O4': [(0, 0), (1, 0), (0, 1), (1, 1)],
              'T4': [(0, 1), (1, 1), (2, 1), (1, 0)], 'S4': [(0, 0), (1, 0), (1, 1), (2, 1)], 'Z4': [(0, 1), (1, 1), (1, 0), (2, 0)],
              'L4': [(0, 0), (0, 1), (0, 2), (1, 0)], 'J4': [(1, 0), (1, 1), (1, 2), (0, 0)], 'I4': [(0, 0), (0, 1), (0, 2), (0, 3)],
              'P5': [(0, 0), (1, 0), (0, 1), (1, 1), (0, 2)], 'U5': [(0, 0), (1, 0), (2, 0), (0, 1), (2, 1)]}
    S = Image.open(os.path.join(A, 'blk_y_00.png')).size[0]  # stored tile size (scales.json)
    lo_, hi_ = round(S / 6), round(S * 5 / 6)
    seam_d, in_d, seam_max = [], [], 0
    for code in PAL:
        for name, cells in shapes.items():
            cs = set(cells)

            def mask(i, j):
                return (1 if (i - 1, j) in cs else 0) | (2 if (i + 1, j) in cs else 0) | (4 if (i, j - 1) in cs else 0) | (8 if (i, j + 1) in cs else 0)
            tiles = {(i, j): Image.open(os.path.join(A, 'blk_%s_%02d.png' % (code, mask(i, j)))).convert('RGBA') for i, j in cells}
            for (i, j), t in tiles.items():
                if (i + 1, j) in cs:
                    r = tiles[(i + 1, j)]
                    for y in range(lo_, hi_):
                        a, b = t.getpixel((S - 1, y)), r.getpixel((0, y))
                        a2, b2 = t.getpixel((S - 3, y)), t.getpixel((S - 2, y))
                        if a[3] > 250 and b[3] > 250:
                            d = sum(abs(a[k] - b[k]) for k in range(3)) / 3
                            seam_d.append(d)
                            seam_max = max(seam_max, d)
                            in_d.append(sum(abs(a2[k] - b2[k]) for k in range(3)) / 3)
                if (i, j + 1) in cs:
                    u = tiles[(i, j + 1)]
                    for x in range(lo_, hi_):
                        a, b = t.getpixel((x, 0)), u.getpixel((x, S - 1))
                        if a[3] > 250 and b[3] > 250:
                            d = sum(abs(a[k] - b[k]) for k in range(3)) / 3
                            seam_d.append(d)
                            seam_max = max(seam_max, d)
    lines.append('SEAM mean %.2f/255 (inside-tile neighbour columns %.2f/255), max %.0f/255 over %d seam pixels, 12 shapes x 8 colours'
                 % (statistics.mean(seam_d), statistics.mean(in_d), seam_max, len(seam_d)))
    text = '\n'.join(lines)
    print(text)
    with open(OUTF, 'w') as f:
        f.write(text + '\n')


if __name__ == '__main__':
    main()
