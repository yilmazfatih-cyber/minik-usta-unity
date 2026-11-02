"""Usta Kral — post-process the Blender master renders into trimmed atlas frames (package B1k).

    python3 art/3d/blender/king/post_king.py [--raw art/3d/out/king/raw] [--out art/3d/out/king/frames] [--div 2.3256]

Standard library + Pillow only. Per drawing (art/3d/out/king/raw/manifest.json):
  1. contact shadow: soft warm ellipse on the ground under the character (smaller and lighter while airborne),
     composited BEHIND the render (the render itself has no ground; long cast shadows are not baked);
  2. LANCZOS downsample by --div on premultiplied alpha (ART_3D 6.3: render at 2x; default 200 -> 86 px/BU);
  3. alpha bleed: fully transparent pixels get the colour of the nearest opaque pixels (about 4 px), so bilinear
     filtering in Unity has no dark fringe;
  4. trim to the alpha bounding box (+2 px), keeping the root (feet on the ground) inside the rectangle;
  5. writes frames/<name>.png and frames/frames.json {name: {w, h, pivotX, pivotY, ox, oy}} where pivot = the root
     point (0..1 from bottom-left of the trimmed rect, Unity Sprite.Create) and ox/oy = rect offset from the
     bottom-left of the full store canvas (canvasW x canvasH store px).
"""
import json
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))


def arg(name, default):
    a = sys.argv[1:]
    return a[a.index(name) + 1] if name in a else default


SHADOW_RGB = (59, 38, 20)  # #3B2614, warm dark brown (never pure black on the warm palette)


def contact_shadow(size, cx, cy, px_per_bu, height, w_bu=1.28, peak0=0.42):
    """Soft ellipse at (cx, cy): w_bu wide on the ground (king 1.28 BU; the crew passes 1.1 x its feet span), 0.24 of that
    tall (10 deg camera + softness); peak alpha peak0 (crew 0.35, art turn 1)."""
    k = max(0.0, min(1.0, height / 0.45))
    rx = 0.5 * w_bu * px_per_bu * (1.0 - 0.38 * k)
    ry = rx * 0.24
    peak = peak0 * (1.0 - 0.55 * k)
    w, h = size
    layer = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(layer)
    # three nested ellipses -> soft core + wide penumbra, then blur
    for f, a in ((1.0, 0.45), (0.78, 0.75), (0.55, 1.0)):
        d.ellipse((cx - rx * f, cy - ry * f, cx + rx * f, cy + ry * f), fill=int(255 * peak * a))
    layer = layer.filter(ImageFilter.GaussianBlur(radius=max(2.0, ry * 0.45)))
    sh = Image.new('RGBA', (w, h), SHADOW_RGB + (0,))
    sh.putalpha(layer)
    return sh


def bleed(im, steps=2, radius=2):
    """Fill RGB of alpha-0 pixels from neighbouring coloured pixels (premultiplied blur / blurred alpha)."""
    a = im.getchannel('A')
    solid = a.point(lambda v: 255 if v > 0 else 0)
    cur = im
    for _ in range(steps):
        pm = cur.convert('RGBa')
        rgb_a = Image.merge('RGBA', pm.split()[:3] + (solid,)).convert('RGBa')
        bl = rgb_a.filter(ImageFilter.GaussianBlur(radius)).convert('RGBA')
        filled = Image.composite(cur, bl, solid)
        solid = ImageChops.lighter(solid, bl.getchannel('A').point(lambda v: 255 if v > 8 else 0))
        cur = filled
    out = cur.copy()
    out.putalpha(a)
    return out


def process(src, div, shadow=None, root=None, keep_canvas=False):
    im = Image.open(src).convert('RGBA')
    if shadow:
        base = contact_shadow(im.size, *shadow)
        base.alpha_composite(im)
        im = base
    w, h = im.size
    sw, sh = int(round(w / div)), int(round(h / div))
    small = im.convert('RGBa').resize((sw, sh), Image.LANCZOS).convert('RGBA')
    a = small.getchannel('A').point(lambda v: 255 if v >= 2 else 0)
    bb = a.getbbox() or (0, 0, 1, 1)
    x0, y0, x1, y1 = bb[0] - 2, bb[1] - 2, bb[2] + 2, bb[3] + 2
    if root is not None:
        rx, ry = root[0] / div, root[1] / div
        x0, y0 = min(x0, int(math.floor(rx)) - 2), min(y0, int(math.floor(ry)) - 2)
        x1, y1 = max(x1, int(math.ceil(rx)) + 2), max(y1, int(math.ceil(ry)) + 2)
    else:
        rx, ry = sw / 2.0, sh / 2.0
    if keep_canvas:
        x0, y0, x1, y1 = 0, 0, sw, sh
    x0, y0, x1, y1 = max(0, x0), max(0, y0), min(sw, x1), min(sh, y1)
    crop = small.crop((x0, y0, x1, y1))
    # clean near-zero alpha (LANCZOS ringing) then bleed colour into the transparent border
    ca = crop.getchannel('A').point(lambda v: 0 if v < 2 else v)
    crop.putalpha(ca)
    crop = bleed(crop)
    cw, chh = crop.size
    meta = dict(w=cw, h=chh, pivotX=round((rx - x0) / cw, 6), pivotY=round((y1 - ry) / chh, 6), ox=x0,
                oy=sh - y1, canvasW=sw, canvasH=sh)
    return crop, meta


def main():
    raw = os.path.abspath(arg('--raw', os.path.join(REPO, 'art', '3d', 'out', 'king', 'raw')))
    out = os.path.abspath(arg('--out', os.path.join(REPO, 'art', '3d', 'out', 'king', 'frames')))
    # 200 px/BU master -> 86 px/BU store (art turn 1; B1k 94): with the 24-drawing idle and the 16-drawing cheer the king
    # pages (17.7 MB) keep atlas + king + fx <= 64 MB RGBA32 (AnimTests). The crew passes --div 2 (224 -> 112 px/BU).
    div = float(arg('--div', str(200.0 / 86.0)))
    os.makedirs(out, exist_ok=True)
    m = json.load(open(os.path.join(raw, 'manifest.json')))
    pxbu = m['pxPerBU']
    meta = {}
    for name, d in sorted(m['drawings'].items()):
        src = os.path.join(raw, d['file'])
        if not os.path.exists(src):
            print('missing', name)
            continue
        sx, sy = d['shadow']
        crop, mt = process(src, div, shadow=(sx, sy, pxbu, d.get('height', 0.0), d.get('shadowWBU', 1.28),
                                             d.get('shadowAlpha', 0.42)), root=d['root'])
        crop.save(os.path.join(out, name + '.png'), optimize=True)
        meta[name] = mt
    for name, d in sorted(m.get('busts', {}).items()):
        src = os.path.join(raw, d['file'])
        if not os.path.exists(src):
            continue
        bdiv = d['pxPerBU'] / d.get('storePxPerBU', d['pxPerBU'] / div)
        crop, mt = process(src, bdiv)
        crop.save(os.path.join(out, name + '.png'), optimize=True)
        mt['pxPerBU'] = d['pxPerBU'] / bdiv
        meta[name] = mt
    doc = dict(version=1, generator='art/3d/blender/king/post_king.py', div=div, pxPerBU=pxbu / div,
               canvas=dict(wBU=m['canvas']['wBU'], hBU=m['canvas']['hBU'], rootYBU=m['canvas']['rootYBU']),
               frames=meta, clips=m['clips'], stills=m.get('stills', {}), k0=m.get('k0'))
    for k in ('aliases', 'bustDesignPxPerBU'):  # crew sheet (art/3d/blender/crew/animate_crew.py)
        if m.get(k):
            doc[k] = m[k]
    with open(os.path.join(out, 'frames.json'), 'w') as fh:
        json.dump(doc, fh, indent=1, sort_keys=True)
    area = sum(v['w'] * v['h'] for v in meta.values())
    print('post: %d frames, %.2f Mpx trimmed (%.2f MB RGBA32)' % (len(meta), area / 1e6, area * 4 / 1048576.0))


if __name__ == '__main__':
    main()
