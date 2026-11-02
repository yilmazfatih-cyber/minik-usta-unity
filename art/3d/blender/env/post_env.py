"""B1 post-process (system python 3 + Pillow; no Blender): 2x renders -> stored frames.

    python3 art/3d/blender/env/post_env.py [--raw art/3d/out/env/raw] [--out art/3d/out/env] [--only name,prefix*]

For every <raw>/<frame>.png (+ optional <frame>.meta.json written by the family script):
  * stored size = design w x h (art/3d/spec/frames.json) x scale (art/3d/blender/env/scales.json, else frames.json);
  * LANCZOS downsize on PREMULTIPLIED alpha (no dark fringes), then un-premultiply;
  * meta.post ops: luma_alpha (white sprite, alpha = luminance; additive-looking highlight), shadow {dy, blur, alpha,
    color} (soft drop shadow composited under the frame, design px), white (keep alpha, RGB -> white: tinted fx);
  * 4 px alpha bleed: fully transparent pixels take the RGB of the nearest opaque neighbour (bilinear sampling never
    pulls black in);
  * v4 atlas names -> <out>/atlas/<frame>.png (exact stored size; the packer checks it) + <out>/atlas/meta.json;
    new names (fx sequences, icon_*_xl, parallax layers) -> <out>/fx/<frame>.png trimmed to the alpha box with the
    canvas offset (ox, oy) + <out>/fx/frames.json.
"""
import fnmatch
import json
import math
import os
import sys

from PIL import Image, ImageChops, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
argv = sys.argv[1:]


def opt(n, d=None):
    return argv[argv.index('--' + n) + 1] if '--' + n in argv else d


RAW = os.path.abspath(opt('raw', os.path.join(ROOT, 'art', '3d', 'out', 'env', 'raw')))
OUT = os.path.abspath(opt('out', os.path.join(ROOT, 'art', '3d', 'out', 'env')))
ONLY = [s for s in opt('only', '').split(',') if s]


def wants(n):
    return not ONLY or any(n == s or (s.endswith('*') and n.startswith(s[:-1])) for s in ONLY)


CONTRACT = json.load(open(os.path.join(ROOT, 'art', '3d', 'spec', 'frames.json')))['frames']
ATLAS = json.load(open(os.path.join(ROOT, 'Assets', 'MinikUsta', 'Resources', 'art', 'atlas.json')))['frames']
SCALES = json.load(open(os.path.join(HERE, 'scales.json')))
EXTRA = SCALES.get('newFrames', {})


def scale_of(name):
    for k, v in SCALES['frames'].items():
        if k == name or (k.endswith('*') and name.startswith(k[:-1])):
            return v
    if name in CONTRACT:
        return CONTRACT[name]['scale']
    raise KeyError(name)


def resize(im, w, h):
    """LANCZOS on premultiplied alpha (Pillow 'RGBa'), back to straight alpha."""
    if im.size == (w, h):
        return im
    return im.convert('RGBa').resize((w, h), Image.LANCZOS).convert('RGBA')


def bleed(im, n=4):
    """RGB of fully transparent pixels <- average of their opaque neighbours, grown n rings; alpha untouched."""
    alpha = im.getchannel('A')
    cur = im
    for _ in range(n):
        avg = cur.convert('RGBa').filter(ImageFilter.BoxBlur(1)).convert('RGBA')
        empty = cur.getchannel('A').point(lambda v: 255 if v == 0 else 0)
        nxt = Image.composite(avg, cur, empty)
        grown = Image.composite(avg.getchannel('A').point(lambda v: 255 if v > 0 else 0), cur.getchannel('A'), empty)
        nxt.putalpha(grown)
        cur = nxt
    cur.putalpha(alpha)
    return cur


def luma_alpha(im, lo=95, hi=215):
    """White highlight sprite: alpha = smooth ramp of the luminance between lo and hi (only the speculars survive)."""
    lum = im.convert('RGB').convert('L').point(lambda v: 0 if v <= lo else 255 if v >= hi else round(255 * ((v - lo) / (hi - lo)) ** 1.2))
    a = ImageChops.multiply(lum, im.getchannel('A'))
    out = Image.new('RGBA', im.size, (255, 255, 255, 0))
    out.putalpha(a)
    return out


def gloss_sprite(w, h, spec):
    """K-09 holdable gloss on the 3D stud: the rendered speculars + a clear 'candy' arc and dot on the stud-top ellipse
    (stud 64 design px wide in the 72 px frame, top ellipse 64 x 52 under the 35 deg pitch), white, normal alpha."""
    from PIL import ImageDraw
    k = w / 72.0
    lay = Image.new('L', (w * 4, h * 4), 0)
    d = ImageDraw.Draw(lay)
    cx, cy, rx, ry = 36 * k * 4, 36 * k * 4, 25 * k * 4, 20 * k * 4
    d.arc((cx - rx, cy - ry, cx + rx, cy + ry), 196, 262, fill=205, width=round(5.5 * k * 4))
    dx, dy, r = cx - 13 * k * 4, cy - 10 * k * 4, 3.6 * k * 4
    d.ellipse((dx - r, dy - r, dx + r, dy + r), fill=225)
    lay = lay.resize((w, h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(0.6 * k))
    a = ImageChops.lighter(lay, spec.getchannel('A'))
    out = Image.new('RGBA', (w, h), (255, 255, 255, 0))
    out.putalpha(a)
    return out


def to_white(im):
    r, g, b, a = im.split()
    lum = Image.merge('RGB', (r, g, b)).convert('L')
    return Image.merge('RGBA', (lum, lum, lum, a))


def drop_shadow(im, dy, blur, alpha, color=(10, 6, 40), dx=0):
    w, h = im.size
    a = im.split()[3]
    sh = Image.new('L', (w, h), 0)
    sh.paste(a, (round(dx), round(dy)))
    if blur > 0:
        sh = sh.filter(ImageFilter.GaussianBlur(blur))
    sh = sh.point(lambda v: round(v * alpha))
    base = Image.new('RGBA', (w, h), color + (0,))
    base.putalpha(sh)
    return Image.alpha_composite(base, im)


def contact_ellipse(im, cx, cy, rx, ry, alpha, blur, color=(20, 12, 40)):
    """Soft contact shadow ellipse under the object (cy from the bottom, design px x scale), composited behind."""
    from PIL import ImageDraw
    w, h = im.size
    m = Image.new('L', (w, h), 0)
    d = ImageDraw.Draw(m)
    d.ellipse((cx - rx, h - cy - ry, cx + rx, h - cy + ry), fill=round(255 * alpha))
    if blur > 0:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    base = Image.new('RGBA', (w, h), color + (0,))
    base.putalpha(m)
    return Image.alpha_composite(base, im)


LAYER_FULL = {}


def process_layer(name, im, e):
    """Parallax layer rendered on the full 1080 x 1920 canvas: resize to the layer scale, crop to the alpha box
    (sky: whole canvas), record where the crop sits (design px, bottom-left origin)."""
    s = e['scale']
    fw, fh = round(1080 * s), round(1920 * s)
    full = resize(im, fw, fh)
    LAYER_FULL[name] = im
    if name.endswith('_sky'):
        x0, y0, x1, y1 = 0, 0, fw, fh
    else:
        a = full.split()[3].point(lambda v: 255 if v > 3 else 0)
        x0, y0, x1, y1 = a.getbbox() or (0, 0, fw, fh)
        lay = e['layer']
        if 'x0' in lay:  # side layers keep their contract width from x0 (design px) so the pair mirrors
            x0 = round(lay['x0'] * s)
            x1 = min(fw, x0 + round(e['w'] * s))
        else:
            x0, x1 = 0, fw
        y0, y1 = max(0, y0 - 1), min(fh, y1 + 1)
    crop = bleed(full.crop((x0, y0, x1, y1)), 4)
    os.makedirs(os.path.join(OUT, 'fx'), exist_ok=True)
    crop.save(os.path.join(OUT, 'fx', name + '.png'))
    rw, rh = x1 - x0, y1 - y0
    px_, py_ = e.get('pivot', [0.5, 0.0])
    return ('fx', name, {'w': rw, 'h': rh, 'scale': s, 'ox': x0, 'oy': fh - y1, 'cw': fw, 'ch': fh, 'pivotX': px_, 'pivotY': py_,
                         'group': 'bg', 'layer': dict(e['layer'], x=round(x0 / s, 1), y=round((fh - y1) / s, 1), w=round(rw / s, 1), h=round(rh / s, 1))})


def flatten_home():
    """bg_home (v4 atlas name) = the parallax layers composited back to front at the atlas scale."""
    order = sorted((n for n in EXTRA if n.startswith('bg_home_')), key=lambda n: EXTRA[n]['layer']['z'])
    if not all(os.path.exists(os.path.join(RAW, n + '.png')) for n in order):
        return None
    s = scale_of('bg_home')
    W, H = round(1080 * s), round(1920 * s)
    base = None
    for n in order:
        im = resize(Image.open(os.path.join(RAW, n + '.png')).convert('RGBA'), W, H)
        base = im if base is None else Image.alpha_composite(base, im)
    base.putalpha(255)
    os.makedirs(os.path.join(OUT, 'atlas'), exist_ok=True)
    base.save(os.path.join(OUT, 'atlas', 'bg_home.png'))
    return ('atlas', 'bg_home', {'scale': s, 'w': W, 'h': H})


KING_ALIASES = ('chr_dede_idle', 'chr_dede_talk', 'chr_dede_bust')
CREW_ALIASES = ('chr_tuna_cheer', 'chr_tuna_idle', 'chr_tuna_think', 'chr_tuna_bust', 'chr_kepce_idle')


def king_aliases(sheet='king', names=KING_ALIASES):
    """chr_dede_idle / chr_dede_talk / chr_dede_bust (deprecated v4 names, ART_3D 9: rendered as Usta Kral) from the B1k
    sheet (Resources/art3d/king.json + king_N.bytes): the aliased drawing is placed by its pivot (feet / bust centre) on
    the v4 frame pivot, resampled to the atlas scale; the contact shadow under the feet fades out at the frame edge.
    Art turn 1: the same for the crew sheet (crew.json: chr_tuna_cheer / idle / think / bust, chr_kepce_idle in 3D)."""
    res = os.path.join(ROOT, 'Assets', 'MinikUsta', 'Resources', 'art3d')
    if not os.path.exists(os.path.join(res, sheet + '.json')):
        return {}
    kj = json.load(open(os.path.join(res, sheet + '.json')))
    pages = [Image.open(os.path.join(res, a['file'].split('/')[-1] + '.bytes')).convert('RGBA') for a in kj['atlases']]
    outm = {}
    for name in names:
        if not wants(name):
            continue
        al = kj['aliases'][name]
        f = kj['frames'][al['of']]
        src = pages[f['atlas']].crop((f['x'], f['y'], f['x'] + f['w'], f['y'] + f['h']))
        c = CONTRACT[name]
        s = scale_of(name)
        W, H = round(c['w'] * s), round(c['h'] * s)
        k = s / al['scale']
        sw, sh = max(1, round(f['w'] * k)), max(1, round(f['h'] * k))
        src = resize(src, sw, sh)
        px, py = c['pivot'][0] * W, c['pivot'][1] * H
        x0 = round(px - f['pivotX'] * sw)
        y0 = round(H - (py + (1 - f['pivotY']) * sh))  # top-left y of the pasted rect (image rows)
        canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        canvas.alpha_composite(src, (max(0, x0), max(0, y0)), (max(0, -x0), max(0, -y0)))
        if not name.endswith('_bust'):
            # fade the last 5 px rows (contact shadow cut by the v4 frame pivot)
            a = canvas.getchannel('A')
            px_ = a.load()
            for yy in range(H - 5, H):
                kf = (H - yy) / 6
                for xx in range(W):
                    px_[xx, yy] = round(px_[xx, yy] * kf)
            canvas.putalpha(a)
        canvas = bleed(canvas, 4)
        os.makedirs(os.path.join(OUT, 'atlas'), exist_ok=True)
        canvas.save(os.path.join(OUT, 'atlas', name + '.png'))
        outm[name] = {'scale': s, 'w': W, 'h': H, 'from': al['of']}
    return outm


def process(name):
    src = os.path.join(RAW, name + '.png')
    meta_p = os.path.join(RAW, name + '.meta.json')
    meta = json.load(open(meta_p)) if os.path.exists(meta_p) else {}
    im = Image.open(src).convert('RGBA')
    is_atlas = name in ATLAS
    if is_atlas:
        c = CONTRACT[name]
        dw, dh = c['w'], c['h']
    else:
        e = EXTRA.get(name) or next((v for k, v in EXTRA.items() if fnmatch.fnmatchcase(name, k)), None)
        if e is None:
            raise KeyError('no newFrames entry for ' + name)
        dw, dh = e['w'], e['h']
        if e.get('layer') is not None:
            return process_layer(name, im, e)
    s = scale_of(name) if is_atlas else e.get('scale', 1.0)
    tw, th = max(1, round(dw * s)), max(1, round(dh * s))
    ar_src = im.size[0] / im.size[1]
    if abs(ar_src - tw / th) > 0.02:
        raise ValueError('%s: render %dx%d aspect != %dx%d' % (name, im.size[0], im.size[1], tw, th))
    im = resize(im, tw, th)
    post = meta.get('post', [])
    if isinstance(post, str):
        post = [post]
    for op in post:
        if op == 'luma_alpha':
            im = gloss_sprite(tw, th, luma_alpha(im))
        elif op == 'white':
            im = to_white(im)
    if 'ellipse' in meta:
        e_ = meta['ellipse']
        im = contact_ellipse(im, e_['cx'] * s, e_['cy'] * s, e_['rx'] * s, e_['ry'] * s, e_.get('alpha', 0.35), e_.get('blur', 6) * s)
    if 'shadow' in meta:
        sdw = meta['shadow']
        im = drop_shadow(im, sdw.get('dy', 6) * s, sdw.get('blur', 4) * s, sdw.get('alpha', 0.3), tuple(sdw.get('color', (10, 6, 40))), sdw.get('dx', 0) * s)
    im = bleed(im, 4)
    if is_atlas:
        os.makedirs(os.path.join(OUT, 'atlas'), exist_ok=True)
        im.save(os.path.join(OUT, 'atlas', name + '.png'))
        keep = {k: meta[k] for k in ('anchorX', 'anchorY', 'studD', 'symD') if k in meta}
        return ('atlas', name, dict(keep, scale=s, w=tw, h=th))
    # new frame: trim to the alpha box (+1 px), keep the canvas offset
    bbox = im.split()[3].getbbox() or (0, 0, 1, 1)
    x0, y0, x1, y1 = max(0, bbox[0] - 1), max(0, bbox[1] - 1), min(tw, bbox[2] + 1), min(th, bbox[3] + 1)
    if e.get('trim', True) is False:
        x0, y0, x1, y1 = 0, 0, tw, th
    crop = im.crop((x0, y0, x1, y1))
    os.makedirs(os.path.join(OUT, 'fx'), exist_ok=True)
    crop.save(os.path.join(OUT, 'fx', name + '.png'))
    px_, py_ = e.get('pivot', [0.5, 0.5])
    # canvas pivot in canvas px (bottom-left origin) -> relative to the trimmed rect
    cpx, cpy = px_ * tw, py_ * th
    ox, oy = x0, th - y1  # rect offset from the canvas bottom-left
    rw, rh = x1 - x0, y1 - y0
    return ('fx', name, {'w': rw, 'h': rh, 'scale': s, 'ox': ox, 'oy': oy, 'cw': tw, 'ch': th,
                         'pivotX': round((cpx - ox) / rw, 5), 'pivotY': round((cpy - oy) / rh, 5), 'group': e.get('group', 'fx')})


def main():
    names = sorted(f[:-4] for f in os.listdir(RAW) if f.endswith('.png'))
    atlas_meta_p = os.path.join(OUT, 'atlas', 'meta.json')
    fx_meta_p = os.path.join(OUT, 'fx', 'frames.json')
    am = json.load(open(atlas_meta_p)) if os.path.exists(atlas_meta_p) else {}
    fm = json.load(open(fx_meta_p)) if os.path.exists(fx_meta_p) else {}
    n = 0
    for name in names:
        if not wants(name):
            continue
        kind, nm, m = process(name)
        (am if kind == 'atlas' else fm)[nm] = m
        n += 1
    if any(wants(n_) for n_ in KING_ALIASES):
        for k_, v_ in king_aliases().items():
            am[k_] = v_
            n += 1
    if any(wants(n_) for n_ in CREW_ALIASES):
        for k_, v_ in king_aliases('crew', CREW_ALIASES).items():
            am[k_] = v_
            n += 1
    if wants('bg_home') or any(n_.startswith('bg_home_') for n_ in ONLY):
        r = flatten_home()
        if r:
            am['bg_home'] = r[2]
            n += 1
    os.makedirs(os.path.join(OUT, 'atlas'), exist_ok=True)
    os.makedirs(os.path.join(OUT, 'fx'), exist_ok=True)
    json.dump(am, open(atlas_meta_p, 'w'), indent=1, sort_keys=True)
    json.dump(fm, open(fx_meta_p, 'w'), indent=1, sort_keys=True)
    print('post_env: %d frames (%d atlas, %d new in total)' % (n, len(am), len(fm)))


if __name__ == '__main__':
    main()
