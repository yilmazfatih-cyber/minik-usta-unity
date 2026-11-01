#!/usr/bin/env python3
"""Measures tile_rig_test.png (docs/ART_3D.md section 2.5): top-face colour vs base hex (CIEDE2000), lip colour ratio.
Needs Pillow. Usage: python3 art/3d/spec/tile_rig_measure.py <dir> [--px 180]"""
import math
import sys

from PIL import Image

D = sys.argv[1]
PX = int(sys.argv[sys.argv.index('--px') + 1]) if '--px' in sys.argv else 180
PALETTE = {'W': '#764012', 'Y': '#FFE23E', 'G': '#44D088', 'R': '#CF404E', 'O': '#EC8D20', 'C': '#C8CFDA', 'B': '#2984DE', 'P': '#5D3AAE'}


def to_lab(rgb):
    def lin(c):
        c /= 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(c) for c in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = (0.2126 * r + 0.7152 * g + 0.0722 * b)
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 216 / 24389 else (24389 / 27 * t + 16) / 116
    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def de2000(l1, l2):
    L1, a1, b1 = l1
    L2, a2, b2 = l2
    C1, C2 = math.hypot(a1, b1), math.hypot(a2, b2)
    Cb = (C1 + C2) / 2
    G = 0.5 * (1 - math.sqrt(Cb ** 7 / (Cb ** 7 + 25 ** 7)))
    a1p, a2p = (1 + G) * a1, (1 + G) * a2
    C1p, C2p = math.hypot(a1p, b1), math.hypot(a2p, b2)
    h1p = math.degrees(math.atan2(b1, a1p)) % 360
    h2p = math.degrees(math.atan2(b2, a2p)) % 360
    dLp, dCp = L2 - L1, C2p - C1p
    dh = h2p - h1p
    if C1p * C2p == 0:
        dh = 0
    elif dh > 180:
        dh -= 360
    elif dh < -180:
        dh += 360
    dHp = 2 * math.sqrt(C1p * C2p) * math.sin(math.radians(dh / 2))
    Lbp, Cbp = (L1 + L2) / 2, (C1p + C2p) / 2
    if C1p * C2p == 0:
        hbp = h1p + h2p
    elif abs(h1p - h2p) <= 180:
        hbp = (h1p + h2p) / 2
    else:
        hbp = (h1p + h2p + 360) / 2 if h1p + h2p < 360 else (h1p + h2p - 360) / 2
    Tt = 1 - 0.17 * math.cos(math.radians(hbp - 30)) + 0.24 * math.cos(math.radians(2 * hbp)) + 0.32 * math.cos(math.radians(3 * hbp + 6)) - 0.20 * math.cos(math.radians(4 * hbp - 63))
    dth = 30 * math.exp(-(((hbp - 275) / 25) ** 2))
    Rc = 2 * math.sqrt(Cbp ** 7 / (Cbp ** 7 + 25 ** 7))
    Sl = 1 + 0.015 * (Lbp - 50) ** 2 / math.sqrt(20 + (Lbp - 50) ** 2)
    Sc, Sh = 1 + 0.045 * Cbp, 1 + 0.015 * Cbp * Tt
    Rt = -math.sin(math.radians(2 * dth)) * Rc
    return math.sqrt((dLp / Sl) ** 2 + (dCp / Sc) ** 2 + (dHp / Sh) ** 2 + Rt * (dCp / Sc) * (dHp / Sh))


def hexrgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def patch(px, x0, y0, n=6):
    vals = [px[x, y][:3] for x in range(x0 - n, x0 + n) for y in range(y0 - n, y0 + n)]
    return tuple(sorted(v[k] for v in vals)[len(vals) // 2] for k in range(3))


im = Image.open(f'{D}/tile_rig_test.png').convert('RGBA')
px = im.load()
H = im.size[1]
worst = 0
for k, (code, hx) in enumerate(PALETTE.items()):
    i = k * 2
    # screen row 1 (one cell), image y grows downwards; top face spans v in [1.2, 2.0]
    top = patch(px, int((i + 0.22) * PX), int(H - 1.62 * PX))
    lip = patch(px, int((i + 0.5) * PX), int(H - 1.10 * PX), 3)
    base = hexrgb(hx)
    de = de2000(to_lab(base), to_lab(top))
    worst = max(worst, de)
    ratio = sum(lip) / max(1, sum(base))
    print(f'{code} base {hx} top #{top[0]:02X}{top[1]:02X}{top[2]:02X} dE00 {de:5.2f}  lip #{lip[0]:02X}{lip[1]:02X}{lip[2]:02X} lip/base {ratio:4.2f}')
print(f'WORST_DE00 {worst:.2f}')
