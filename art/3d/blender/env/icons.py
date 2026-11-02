"""B1 icons (docs/ART_3D.md section 4, rig C_prop: 28 deg down, 3/4 yaw, object fills 86 % of the frame).

    blender -b --factory-startup -noaudio --python art/3d/blender/env/icons.py -- --out art/3d/out/env/raw [--only icon_star*] [--quick]

Every icon is an original procedural toy object (bpy primitives, inflated silhouettes, bezier tubes); the concepts follow the
v4 icon set (ART_UNITY Ek A) so the code meaning is unchanged. The coin carries the trowel emblem, never a crown (ART_3D
11). `icon_*_xl` (new, 256 design px at scale 1) are the same models rendered bigger for reward layers.
Frames render at 3 px per design px for 128 px icons (store 1.0-1.5) and 2 px for the xl set.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True  # no __pycache__ next to the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402


def out(name):
    return os.path.join(C.ARGS.out, name + '.png')


def scene(px_w, px_h=None):
    sc = C.reset()
    px_h = px_h or px_w
    C.setup_render(sc, px_w, px_h, 128)
    C.rig_C(sc)
    cam = C.prop_camera(sc, px_w // (2 if C.ARGS.quick else 1), px_h // (2 if C.ARGS.quick else 1))
    return sc, cam


def puffy(sc, pts, depth, mat, bevel=None, sub=2, loc=(0, 0, 0), yaw=-20.0, tilt=0.0, mat_side=None):
    o = C.puffy(sc, 'p', C.ccw(pts), depth, mat, bevel=bevel, sub=sub, mat_side=mat_side)
    C.stand(o, yaw, tilt)
    o.location = loc
    return o


# ------------------------------------------------------------------------------------------------------- shapes
def trowel_pts(s=1.0):
    """Kite blade (point up) + neck: the Golden Trowel silhouette (king.md section 8), x/y in BU."""
    return [(0, 1.0 * s), (0.42 * s, 0.25 * s), (0.30 * s, 0.0), (0.07 * s, -0.05 * s), (0.07 * s, -0.35 * s), (-0.07 * s, -0.35 * s), (-0.07 * s, -0.05 * s),
            (-0.30 * s, 0.0), (-0.42 * s, 0.25 * s)]


def check_pts():
    return [(-0.95, 0.15), (-0.55, 0.52), (-0.18, 0.15), (0.62, 0.95), (1.0, 0.55), (-0.18, -0.65)]


def gear_pts(n=8, r_out=1.0, r_in=0.78, seg=6):
    pts = []
    for i in range(n):
        a0 = 2 * math.pi * i / n
        w = 2 * math.pi / n
        for k, (f, r) in enumerate([(0.0, r_in), (0.18, r_in), (0.28, r_out), (0.72, r_out), (0.82, r_in)]):
            a = a0 + f * w
            pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def tri_pts(r=1.0):
    return [(r * math.cos(math.radians(a)), r * math.sin(math.radians(a))) for a in (0, 120, 240)]


def arc_pts(r, a0, a1, n=24, cx=0.0, cy=0.0, z=0.0):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), 0, cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


# ----------------------------------------------------------------------------------------------------- builders
def b_star(sc, empty=False):
    if empty:
        m = C.principled('glass', '#262163', rough=0.12, coat=0.8, coat_rough=0.04, spec=0.6, sss=0.0)
        C.gradient_input(m, [(0.0, '#1B174A'), (1.0, '#35307E')], axis='Z', lo=-1.0, hi=1.0, space='Object')
    else:
        m = C.principled('star', '#FFD119', rough=0.2, coat=0.65, coat_rough=0.05, sss=0.1, spec=0.6)
        C.gradient_input(m, [(0.0, '#FFAD00'), (0.5, '#FFD119'), (1.0, '#FFF08A')], axis='Y', lo=-1.0, hi=1.0, space='Object')
    pts = C.star_pts(5, 1.0, 0.52, rot=90)
    return [puffy(sc, pts, 0.42, m, bevel=0.17, sub=2)]


def b_heart(sc):
    m = C.principled('heart', '#FF3B3B', rough=0.18, coat=0.7, coat_rough=0.04, sss=0.15, spec=0.6)
    C.gradient_input(m, [(0.0, '#B80E12'), (0.5, '#F0262A'), (1.0, '#FF6A5E')], axis='Y', lo=-1.0, hi=1.0, space='Object')
    return [puffy(sc, C.heart_pts(1.0, 72), 0.5, m, bevel=0.22, sub=2)]


def b_coin(sc, yaw=-20.0, spin=0.0):
    """Thick gold coin: rim ring, recessed field, raised Golden Trowel emblem (never a crown, ART_3D 11)."""
    # candy gold (not mirror metal): bright on the dark HUD capsules too
    gold = C.principled('coin', '#FFC21A', rough=0.24, coat=0.55, coat_rough=0.05, metallic=0.45, spec=0.6, sss=0.04)
    C.gradient_input(gold, [(0.0, '#F09A00'), (0.55, '#FFC82A'), (1.0, '#FFEE90')], axis='Y', lo=-1.0, hi=1.0, space='Object')
    rim = C.principled('rim', '#F5A800', rough=0.25, coat=0.4, metallic=0.5)
    field = C.principled('field', '#FFB81C', rough=0.3, coat=0.45, metallic=0.4)
    emb_m = C.principled('emb', '#FFF0A0', rough=0.18, coat=0.6, metallic=0.4)
    parts = [C.cylinder(sc, 'disc', 1.0, 0.3, mat=gold, seg=96, bevel=0.08, bevel_seg=5, mat_side=rim)]
    parts.append(C.torus(sc, 'ring', 0.82, 0.06, loc=(0, 0, 0.15), mat=rim))
    parts.append(C.cylinder(sc, 'field', 0.76, 0.02, loc=(0, 0, 0.152), mat=field, seg=96))
    for side in (1, -1):  # both faces carry the emblem (the spin clip shows the back)
        if side < 0:
            parts.append(C.torus(sc, 'ring_b', 0.82, 0.06, loc=(0, 0, -0.15), mat=rim))
            parts.append(C.cylinder(sc, 'field_b', 0.76, 0.02, loc=(0, 0, -0.152), mat=field, seg=96))
        emb = C.puffy(sc, 'emb', C.ccw(trowel_pts(0.78)), 0.16, emb_m, bevel=0.05, sub=2)
        emb.location = (0, 0.08, 0.17 * side)
        if side < 0:
            emb.rotation_euler = (0, math.radians(180), 0)
        parts.append(emb)
    # knurled edge: 40 shallow ribs
    for k in range(40):
        a = 2 * math.pi * k / 40
        o = C.superellipsoid(sc, 'kn', 0.03, 0.06, 0.13, n=2.5, loc=(math.cos(a) * 1.0, math.sin(a) * 1.0, 0), mat=rim, sub=2)
        o.rotation_euler = (0, 0, a)
        parts.append(o)
    g = C.group(sc, parts)
    g.rotation_euler = (math.radians(90), math.radians(spin), math.radians(yaw))
    return parts


def b_trowel(sc, yaw=-25.0):
    blade = C.principled('blade', '#FFD45A', rough=0.22, coat=0.6, metallic=0.35, spec=0.6, sss=0.04)
    C.gradient_input(blade, [(0.0, '#E7A200'), (0.6, '#FFD45A'), (1.0, '#FFF2A8')], axis='Y', lo=-0.2, hi=1.0, space='Object')
    wood = C.mat_wood('h', '#B8763E')
    ferr = C.mat_gold('ferr')
    parts = []
    b = C.puffy(sc, 'blade', C.ccw([(0, 1.0), (0.48, 0.2), (0.34, -0.02), (-0.34, -0.02), (-0.48, 0.2)]), 0.08, blade, bevel=0.035, sub=2)
    parts.append(b)
    # centre ridge
    r = C.puffy(sc, 'ridge', C.ccw([(0, 0.92), (0.05, 0.1), (-0.05, 0.1)]), 0.1, blade, bevel=0.03, sub=1)
    parts.append(r)
    neck = C.curve_tube(sc, 'neck', [(0, -0.02, 0), (0, -0.16, 0.12), (0, -0.3, 0.14)], 0.045, ferr)
    parts.append(neck)
    fe = C.cylinder(sc, 'ferrule', 0.075, 0.1, loc=(0, -0.36, 0.14), mat=ferr, rot=(90, 0, 0), bevel=0.02)
    parts.append(fe)
    hd = C.superellipsoid(sc, 'handle', 0.085, 0.3, 0.085, n=2.4, loc=(0, -0.7, 0.14), mat=wood)
    parts.append(hd)
    g = C.group(sc, parts, rot=(90, 0, 0))
    g.rotation_euler = (math.radians(90), math.radians(-38), math.radians(yaw))
    return parts


def sparkle(sc, loc, s, color='#FFFFFF', strength=3.0):
    m = C.emission_mat('spk', color, strength)
    o = C.puffy(sc, 'spark', C.ccw(C.star_pts(4, 1.0, 0.22, rot=90)), 0.1, m, bevel=0.03, sub=1)
    o.scale = (s, s, s)
    C.stand(o, -20)
    o.location = loc
    return o


def b_gold_trowel(sc):
    parts = b_trowel(sc)
    parts.append(sparkle(sc, (0.55, -0.6, 0.75), 0.18))
    parts.append(sparkle(sc, (-0.55, -0.6, -0.35), 0.11))
    return parts


def b_hammer(sc):
    rubber = C.principled('rub', '#E8302F', rough=0.42, coat=0.35, sss=0.05)
    C.gradient_input(rubber, [(0.0, '#B5191B'), (1.0, '#FF5A4C')], axis='Z', lo=-0.3, hi=0.4, space='Object')
    cap = C.mat_chrome('cap')
    wood = C.mat_wood('wood', '#C98A4B')
    head = C.cylinder(sc, 'head', 0.36, 0.95, mat=rubber, seg=64, bevel=0.1, bevel_seg=5, rot=(0, 90, 0))
    c1 = C.cylinder(sc, 'c1', 0.3, 0.06, loc=(0.5, 0, 0), mat=cap, rot=(0, 90, 0), bevel=0.02)
    c2 = C.cylinder(sc, 'c2', 0.3, 0.06, loc=(-0.5, 0, 0), mat=cap, rot=(0, 90, 0), bevel=0.02)
    handle = C.superellipsoid(sc, 'h', 0.1, 0.1, 0.78, n=2.2, loc=(0, 0, -0.95), mat=wood)
    grip = C.cylinder(sc, 'grip', 0.115, 0.42, loc=(0, 0, -1.4), mat=C.principled('g', '#3A3346', rough=0.6), bevel=0.04)
    parts = [head, c1, c2, handle, grip]
    C.group(sc, parts, rot=(0, -35, -25))
    return parts


def b_brush(sc):
    wood = C.mat_wood('wood', '#C98A4B')
    ferr = C.mat_chrome('ferr')
    brist = C.principled('br', '#F4E6C8', rough=0.6, coat=0.0)
    paint = C.mat_candy('paint', '#2E9BF0', rough=0.12, coat=0.8, sss=0.1)
    mint = C.mat_candy('mint', '#44D088', rough=0.12, coat=0.8, sss=0.1)
    handle = C.superellipsoid(sc, 'h', 0.13, 0.09, 0.85, n=2.4, loc=(0, 0, -0.75), mat=wood)
    fe = C.superellipsoid(sc, 'f', 0.27, 0.12, 0.2, n=4, loc=(0, 0, 0.18), mat=ferr)
    br = C.superellipsoid(sc, 'b', 0.26, 0.11, 0.3, n=3.5, loc=(0, 0, 0.6), mat=brist)
    tip = C.superellipsoid(sc, 't', 0.27, 0.12, 0.17, n=2.6, loc=(0, 0, 0.86), mat=paint)
    drop = C.uv_sphere(sc, 'drop', 0.12, loc=(0.2, -0.02, 0.98), mat=mint, scale=(1, 1, 1.3))
    parts = [handle, fe, br, tip, drop]
    C.group(sc, parts, rot=(0, 40, -20))
    return parts


def b_crane(sc):
    org = C.mat_candy('org', '#EC8D20')
    chrome = C.mat_chrome()
    hz = C.principled('hz', '#FFD43B', rough=0.3, coat=0.45)
    C.stripes_input(hz, '#FFD43B', '#3A3346', 0.12)
    steel = C.principled('steel', '#8E96A3', rough=0.35, metallic=0.9)
    blk = C.mat_candy('blk', '#CF404E')
    parts = [C.cylinder(sc, 'cable', 0.035, 0.5, loc=(0, 0, 1.1), mat=steel, seg=16)]
    parts.append(C.superellipsoid(sc, 'block', 0.42, 0.28, 0.32, n=3.2, loc=(0, 0, 0.62), mat=org))
    parts.append(C.superellipsoid(sc, 'band', 0.43, 0.29, 0.08, n=3.2, loc=(0, 0, 0.42), mat=hz))
    parts.append(C.cylinder(sc, 'axle', 0.13, 0.08, loc=(0, -0.28, 0.66), mat=chrome, rot=(90, 0, 0), bevel=0.02))
    parts.append(C.cylinder(sc, 'shank', 0.075, 0.3, loc=(0, 0, 0.2), mat=chrome, seg=24))
    parts.append(C.curve_tube(sc, 'hook', [(0, 0, 0.12), (0, 0, -0.3), (-0.1, 0, -0.62), (-0.42, 0, -0.7), (-0.64, 0, -0.48), (-0.62, 0, -0.22)], 0.1, chrome))
    parts.append(C.uv_sphere(sc, 'tip', 0.11, loc=(-0.62, 0, -0.18), mat=chrome))
    C.group(sc, parts, rot=(0, 0, -25))
    return parts


def b_chest(sc):
    wood = C.mat_wood('wood', '#A0612E')
    gold = C.mat_gold('band')
    dark = C.principled('d', '#3A2414', rough=0.6)
    body = C.superellipsoid(sc, 'body', 0.95, 0.6, 0.45, n=5, loc=(0, 0, -0.25), mat=wood)
    lid = C.cylinder(sc, 'lid', 0.6, 1.9, loc=(0, 0, 0.2), mat=wood, rot=(0, 90, 0), seg=48, bevel=0.06, bevel_seg=3)
    parts = [body, lid]
    for x in (-0.62, 0.62):
        parts.append(C.superellipsoid(sc, 'strap', 0.09, 0.625, 0.47, n=5, loc=(x, 0, -0.25), mat=gold))
        parts.append(C.cylinder(sc, 'lstrap', 0.62, 0.18, loc=(x, 0, 0.2), mat=gold, rot=(0, 90, 0), seg=48, bevel=0.03))
    parts.append(C.superellipsoid(sc, 'lock', 0.18, 0.08, 0.22, n=3, loc=(0, -0.62, 0.05), mat=gold))
    parts.append(C.superellipsoid(sc, 'hole', 0.04, 0.03, 0.07, n=2, loc=(0, -0.7, 0.03), mat=dark))
    C.group(sc, parts, rot=(0, 0, -22))
    return parts


def b_lock(sc):
    gold = C.mat_gold('g')
    chrome = C.mat_chrome('sh')
    dark = C.principled('d', '#5A3A06', rough=0.5)
    body = C.superellipsoid(sc, 'body', 0.75, 0.32, 0.6, n=3.4, loc=(0, 0, -0.3), mat=gold)
    sh = C.curve_tube(sc, 'shackle', [(-0.42, 0, 0.2), (-0.42, 0, 0.6), (0, 0, 0.92), (0.42, 0, 0.6), (0.42, 0, 0.2)], 0.11, chrome)
    kh = C.puffy(sc, 'kh', C.ccw(C.circle_pts(0.14, 32, 0, 0.0) ), 0.1, dark, bevel=0.03, sub=1)
    kh.rotation_euler = (math.radians(90), 0, 0)
    kh.location = (0, -0.31, -0.22)
    kslot = C.puffy(sc, 'ks', C.ccw([(-0.06, 0), (0.06, 0), (0.09, -0.3), (-0.09, -0.3)]), 0.1, dark, bevel=0.03, sub=1)
    kslot.rotation_euler = (math.radians(90), 0, 0)
    kslot.location = (0, -0.31, -0.24)
    parts = [body, sh, kh, kslot]
    C.group(sc, parts, rot=(0, 0, -20))
    return parts


def b_check(sc):
    m = C.mat_candy('chk', '#2BCA0A')
    C.gradient_input(m, [(0.0, '#148E04'), (0.6, '#2BCA0A'), (1.0, '#7FF060')], axis='Y', lo=-0.7, hi=1.0, space='Object')
    return [puffy(sc, check_pts(), 0.36, m, bevel=0.14)]


def b_pause(sc):
    m = C.mat_candy('p', '#F2F0FF', sss=0.05)
    C.gradient_input(m, [(0.0, '#C9C2F4'), (1.0, '#FFFFFF')], axis='Y', lo=-1.0, hi=1.0, space='Object')
    return [puffy(sc, C.rrect_pts(-0.62, -0.95, -0.12, 0.95, 0.22, 8), 0.36, m, bevel=0.14),
            puffy(sc, C.rrect_pts(0.12, -0.95, 0.62, 0.95, 0.22, 8), 0.36, m, bevel=0.14)]


def b_play(sc):
    m = C.mat_candy('p', '#F2F0FF', sss=0.05)
    C.gradient_input(m, [(0.0, '#C9C2F4'), (1.0, '#FFFFFF')], axis='Y', lo=-1.0, hi=1.0, space='Object')
    return [puffy(sc, tri_pts(1.0), 0.4, m, bevel=0.2)]


def b_settings(sc):
    m = C.principled('gear', '#C9C6E6', rough=0.22, coat=0.5, metallic=0.3)
    C.gradient_input(m, [(0.0, '#8E89B8'), (1.0, '#F2F0FF')], axis='Y', lo=-1.0, hi=1.0, space='Object')
    org = C.mat_candy('o', '#FFB32A')
    g = puffy(sc, gear_pts(8, 1.0, 0.76), 0.4, m, bevel=0.1, sub=1)
    hole = puffy(sc, C.circle_pts(0.32, 48), 0.5, org, bevel=0.15, loc=(0, -0.05, 0))
    return [g, hole]


def b_music(sc):
    m = C.mat_candy('n', '#F0509C')
    C.gradient_input(m, [(0.0, '#B81E6A'), (1.0, '#FF8AC4')], axis='Y', lo=-1.0, hi=1.0, space='Object')
    head = [(x * 0.42 * math.cos(math.radians(-20)) - y * 0.3 * math.sin(math.radians(-20)) - 0.35, x * 0.42 * math.sin(math.radians(-20)) + y * 0.3 * math.cos(math.radians(-20)) - 0.65)
            for x, y in C.circle_pts(1.0, 40)]
    stem = C.rrect_pts(-0.02, -0.65, 0.14, 0.95, 0.06, 4)
    flag = [(0.06, 0.95), (0.62, 0.62), (0.66, 0.25), (0.5, 0.42), (0.06, 0.62)]
    return [puffy(sc, head, 0.36, m, bevel=0.14), puffy(sc, stem, 0.3, m, bevel=0.07), puffy(sc, flag, 0.28, m, bevel=0.08)]


def b_speaker(sc, on=True):
    m = C.mat_candy('s', '#2E9BF0')
    C.gradient_input(m, [(0.0, '#1767B8'), (1.0, '#6CC4FF')], axis='Y', lo=-1.0, hi=1.0, space='Object')
    pts = [(-0.9, -0.32), (-0.45, -0.32), (0.05, -0.75), (0.05, 0.75), (-0.45, 0.32), (-0.9, 0.32)]
    parts = [puffy(sc, pts, 0.4, m, bevel=0.12, loc=(-0.2, 0, 0))]
    if on:
        w = C.mat_candy('w', '#6CC4FF')
        for r in (0.45, 0.8):
            pp = arc_pts(r, -50, 50, 20, cx=0.0)
            t = C.curve_tube(sc, 'wave', [(x, y, z) for x, y, z in pp], 0.08, w)
            t.rotation_euler = (0, 0, math.radians(-20))
            t.location = (0.05, 0, 0)
            parts.append(t)
    else:
        r = C.mat_candy('x', '#FE3232')
        for a in (45, -45):
            o = puffy(sc, C.rrect_pts(-0.42, -0.1, 0.42, 0.1, 0.1, 6), 0.26, r, bevel=0.08, loc=(0.55, -0.1, 0))
            o.rotation_euler = (math.radians(90), math.radians(a), math.radians(-20))
            parts.append(o)
    return parts


def b_vibration(sc):
    body = C.mat_candy('ph', '#3A3B8E')
    screen = C.principled('sc', '#7AD0FF', rough=0.08, coat=1.0, emission='#7AD0FF', emission_strength=0.4)
    zz = C.mat_candy('z', '#FFD119')
    ph = puffy(sc, C.rrect_pts(-0.42, -0.85, 0.42, 0.85, 0.16, 8), 0.24, body, bevel=0.08)
    scr = puffy(sc, C.rrect_pts(-0.32, -0.62, 0.32, 0.68, 0.06, 6), 0.06, screen, bevel=0.02, loc=(0, -0.11, 0))
    parts = [ph, scr]
    for sx in (-1, 1):
        pts = [(sx * 0.62, 0, 0.5), (sx * 0.78, 0, 0.25), (sx * 0.62, 0, 0.0), (sx * 0.78, 0, -0.25), (sx * 0.62, 0, -0.5)]
        t = C.curve_tube(sc, 'z', pts, 0.06, zz)
        t.rotation_euler = (0, 0, math.radians(-20))
        parts.append(t)
    return parts


def b_motion(sc):
    ball = C.mat_candy('b', '#44D088')
    C.gradient_input(ball, [(0.0, '#1F9A55'), (1.0, '#8FF0B8')], axis='Z', lo=-0.5, hi=0.5, space='Object')
    lines = C.mat_candy('l', '#C9F5DC')
    parts = [C.uv_sphere(sc, 'ball', 0.55, loc=(0.42, 0, 0), mat=ball, seg=48, rings=24)]
    for k, (y, ln) in enumerate(((0.32, 0.55), (0.0, 0.75), (-0.32, 0.55))):
        o = puffy(sc, C.rrect_pts(-ln / 2, -0.07, ln / 2, 0.07, 0.07, 6), 0.14, lines, bevel=0.06, loc=(-0.55 - (0.75 - ln) / 2, 0, y))
        parts.append(o)
    return parts


def b_undo(sc):
    m = C.mat_candy('u', '#2BCA0A')
    C.gradient_input(m, [(0.0, '#148E04'), (1.0, '#7FF060')], axis='Z', lo=-0.8, hi=0.8, space='Object')
    cx, cy, r = 0.15, -0.15, 0.62
    pp = arc_pts(r, 150, -40, 28, cx=cx, cy=cy)
    t = C.curve_tube(sc, 'arc', pp, 0.17, m)
    # arrow head: base centre on the arc start (150 deg), tip along the tangent past the start (down-left)
    a = math.radians(150)
    sx, sy = cx + r * math.cos(a), cy + r * math.sin(a)
    tx, ty = -math.sin(a), math.cos(a)
    nx, ny = -ty, tx
    tri = [(sx + tx * 0.5, sy + ty * 0.5), (sx + nx * 0.36, sy + ny * 0.36), (sx - nx * 0.36, sy - ny * 0.36)]
    head = C.puffy(sc, 'head', C.ccw(tri), 0.36, m, bevel=0.12, sub=2)
    head.rotation_euler = (math.radians(90), 0, 0)
    g = C.group(sc, [t, head], rot=(0, 0, -20))
    return [t, head]


def b_blocks(sc):
    cream = C.principled('cr', '#F2E6C8', rough=0.3, coat=0.4, sss=0.06)
    parts = []
    cells = [(0, 0), (1, 0), (0, 1)]
    for (i, j) in cells:
        parts.append(C.superellipsoid(sc, 'c', 0.5, 0.5, 0.22, n=6, loc=(i - 0.5, j - 0.5, 0), mat=cream))
        parts.append(C.cylinder(sc, 's', 0.22, 0.12, loc=(i - 0.5, j - 0.5, 0.26), mat=cream, seg=48, bevel=0.03))
    C.group(sc, parts, rot=(58, 0, -20))
    return parts


def b_house(sc):
    wall = C.principled('w', '#FFF1D2', rough=0.35, coat=0.3)
    roof = C.mat_candy('r', '#EC6A2B')
    door = C.mat_candy('d', '#2E7FE0')
    win = C.principled('win', '#9FD8FF', rough=0.08, coat=1.0)
    chim = C.principled('ch', '#B44A2A', rough=0.4)
    parts = [C.superellipsoid(sc, 'body', 0.62, 0.5, 0.48, n=5, loc=(0, 0, -0.3), mat=wall)]
    for s in (1, -1):
        o = C.superellipsoid(sc, 'roof', 0.5, 0.62, 0.07, n=5, loc=(s * 0.36, 0, 0.42), mat=roof)
        o.rotation_euler = (0, math.radians(s * 38), 0)
        parts.append(o)
    parts.append(C.superellipsoid(sc, 'chim', 0.09, 0.09, 0.16, n=4, loc=(0.36, 0.2, 0.62), mat=chim))
    parts.append(C.superellipsoid(sc, 'door', 0.16, 0.03, 0.25, n=4, loc=(0.05, -0.51, -0.52), mat=door))
    parts.append(C.superellipsoid(sc, 'win', 0.13, 0.03, 0.13, n=4, loc=(-0.33, -0.51, -0.2), mat=win))
    C.group(sc, parts, rot=(0, 0, -22))
    return parts


def b_trophy(sc):
    gold = C.mat_gold('g')
    blue = C.mat_candy('b', '#2E7FE0')
    cup = C.cylinder(sc, 'cup', 0.62, 0.8, loc=(0, 0, 0.35), mat=gold, seg=64, bevel=0.12, bevel_seg=5, r2=None)
    # taper: scale the bottom verts
    cup.data.vertices.foreach_get
    for v in cup.data.vertices:
        if v.co.z < 0:
            v.co.x *= 0.55
            v.co.y *= 0.55
    parts = [cup]
    for s in (1, -1):
        parts.append(C.torus(sc, 'hdl', 0.24, 0.06, loc=(s * 0.62, 0, 0.42), mat=gold, rot=(90, 0, 0)))
    parts.append(C.cylinder(sc, 'stem', 0.12, 0.35, loc=(0, 0, -0.25), mat=gold, seg=32))
    parts.append(C.cylinder(sc, 'base', 0.45, 0.22, loc=(0, 0, -0.52), mat=blue, seg=48, bevel=0.06))
    parts.append(C.puffy(sc, 'emb', C.ccw(C.star_pts(5, 0.26, 0.12)), 0.06, C.mat_candy('s', '#FFF3A0'), bevel=0.02, sub=1))
    parts[-1].rotation_euler = (math.radians(90), 0, 0)
    parts[-1].location = (0, -0.6, 0.38)
    C.group(sc, parts, rot=(0, 0, -20))
    return parts


def b_shop(sc):
    wood = C.mat_wood('w', '#B8763E')
    pink = C.mat_candy('p', '#F0509C')
    white = C.principled('wt', '#FFF6E6', rough=0.3, coat=0.4)
    goods = [C.mat_candy('g%d' % i, c) for i, c in enumerate(('#FFE23E', '#44D088', '#2984DE', '#EC8D20'))]
    parts = [C.superellipsoid(sc, 'counter', 0.95, 0.45, 0.3, n=5, loc=(0, 0, -0.55), mat=wood)]
    for x in (-0.85, 0.85):
        parts.append(C.cylinder(sc, 'post', 0.06, 1.0, loc=(x, -0.3, 0.1), mat=wood, seg=16))
    for k in range(6):
        x = -0.85 + k * 0.34
        parts.append(C.cylinder(sc, 'aw', 0.17, 0.9, loc=(x, 0, 0.62), mat=pink if k % 2 == 0 else white, rot=(90, 0, 0), seg=32, bevel=0.03))
    for k, gm in enumerate(goods):
        parts.append(C.superellipsoid(sc, 'good', 0.13, 0.13, 0.11, n=4, loc=(-0.55 + k * 0.37, -0.1, -0.13), mat=gm))
    C.group(sc, parts, rot=(0, 0, -20))
    return parts


def hardhat(sc, color, loc, s=1.0, tilt=-18):
    m = C.mat_candy('hat' + color, color)
    dome = C.uv_sphere(sc, 'dome', 0.5 * s, loc=(0, 0, 0), mat=m, scale=(1, 0.92, 1.0))
    for v in dome.data.vertices:
        if v.co.z < 0:
            v.co.z = 0
    brim = C.cylinder(sc, 'brim', 0.56 * s, 0.07 * s, loc=(0, -0.06 * s, 0.03 * s), mat=m, seg=48, bevel=0.03 * s)
    rib = C.superellipsoid(sc, 'rib', 0.07 * s, 0.42 * s, 0.5 * s, n=2.5, loc=(0, 0, 0.0), mat=m)
    for v in rib.data.vertices:
        if v.co.z < 0.05:
            v.co.z = 0.05
    parts = [dome, brim, rib]
    C.group(sc, parts, loc=loc, rot=(tilt, 0, 0))
    return parts


def b_team(sc):
    a = hardhat(sc, '#FFD43B', (0.42, 0.35, 0.05))
    b = hardhat(sc, '#EC8D20', (-0.35, -0.25, -0.12))
    return a + b


def b_album(sc):
    cover = C.mat_candy('c', '#6A4FD8')
    pages = C.principled('pg', '#FFF6E6', rough=0.5)
    pic = C.principled('pic', '#7AD0FF', rough=0.2, coat=0.6)
    hill = C.mat_candy('h', '#44D088')
    parts = [C.superellipsoid(sc, 'cover', 0.72, 0.9, 0.12, n=5, loc=(0, 0, 0), mat=cover),
             C.superellipsoid(sc, 'pages', 0.66, 0.86, 0.09, n=6, loc=(0.06, 0, -0.06), mat=pages),
             C.superellipsoid(sc, 'pic', 0.42, 0.4, 0.03, n=6, loc=(0.05, 0.05, 0.12), mat=pic),
             C.superellipsoid(sc, 'hill', 0.3, 0.12, 0.035, n=2.2, loc=(0.1, -0.2, 0.14), mat=hill)]
    C.group(sc, parts, rot=(62, 0, -18))
    return parts


def b_glove(sc):
    skin = C.principled('skin', '#FFFFFF', rough=0.45, coat=0.15, sss=0.1)
    C.gradient_input(skin, [(0.0, '#E6E0F0'), (1.0, '#FFFFFF')], axis='Z', lo=-1.0, hi=1.0, space='Object')
    cuff = C.mat_candy('cuff', '#EC8D20')
    stripe = C.principled('st', '#FFF6E6', rough=0.4, coat=0.2)
    parts = [C.superellipsoid(sc, 'palm', 0.42, 0.26, 0.42, n=2.6, loc=(0, 0, -0.1), mat=skin),
             C.superellipsoid(sc, 'index', 0.12, 0.12, 0.42, n=2.2, loc=(-0.12, -0.02, 0.62), mat=skin)]
    for k, x in enumerate((0.08, 0.24, 0.36)):
        parts.append(C.superellipsoid(sc, 'f%d' % k, 0.11, 0.13, 0.13, n=2.2, loc=(x - 0.04, -0.2, 0.22 - k * 0.04), mat=skin))
    parts.append(C.superellipsoid(sc, 'thumb', 0.11, 0.11, 0.24, n=2.2, loc=(-0.42, -0.08, 0.02), mat=skin))
    parts[-1].rotation_euler = (0, math.radians(35), 0)
    parts.append(C.cylinder(sc, 'cuff', 0.4, 0.28, loc=(0, 0, -0.62), mat=cuff, seg=48, bevel=0.08, bevel_seg=4))
    parts.append(C.torus(sc, 'st', 0.4, 0.04, loc=(0, 0, -0.62), mat=stripe))
    C.group(sc, parts, rot=(0, 0, -18))
    return parts


ICONS = {
    'icon_star': lambda sc: b_star(sc), 'icon_star_empty': lambda sc: b_star(sc, empty=True), 'icon_heart': b_heart, 'icon_coin': b_coin,
    'icon_gold_trowel': b_gold_trowel, 'icon_hammer': b_hammer, 'icon_brush': b_brush, 'icon_crane': b_crane, 'icon_chest': b_chest,
    'icon_lock': b_lock, 'icon_check': b_check, 'icon_pause': b_pause, 'icon_play': b_play, 'icon_settings': b_settings, 'icon_music': b_music,
    'icon_sound_on': lambda sc: b_speaker(sc, True), 'icon_sound_off': lambda sc: b_speaker(sc, False), 'icon_vibration': b_vibration,
    'icon_motion': b_motion, 'icon_undo': b_undo, 'icon_blocks': b_blocks, 'icon_nav_home': b_house, 'icon_nav_league': b_trophy,
    'icon_nav_shop': b_shop, 'icon_nav_team': b_team, 'icon_nav_album': b_album,
}
XL = ['icon_coin', 'icon_star', 'icon_heart', 'icon_chest', 'icon_crane', 'icon_hammer', 'icon_brush', 'icon_gold_trowel']
FILL = {'icon_pause': 0.80, 'icon_play': 0.80, 'icon_check': 0.84, 'icon_coin': 0.86}


def render_icon(name, builder, px, fill=0.86, w=None, h=None):
    sc, cam = scene(w or px, h or px)
    builder(sc)
    C.fit_camera(sc, cam, fill)
    C.render(sc, out(name))


if __name__ == '__main__':
    for name, b in ICONS.items():
        if C.ARGS.wants(name):
            render_icon(name, b, 384, FILL.get(name, 0.86))
        if name in XL and C.ARGS.wants(name + '_xl'):
            render_icon(name + '_xl', b, 512, FILL.get(name, 0.86))
    if C.ARGS.wants('ui_glove'):
        render_icon('ui_glove', b_glove, 0, 0.9, w=420, h=480)
    print('ICONS DONE', flush=True)
