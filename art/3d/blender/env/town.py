"""Chapter 1 tree house in 3D, built in ten level pieces (art turn 1; rig C prop; D-100).

    blender -b --factory-startup -noaudio --python art/3d/blender/env/town.py -- --out art/3d/out/env/raw [--only town_*,bld_*] [--quick]

The ten levels of chapter 1 each produce one piece of the tree house (level names, levels/level_001..010.json):
  1 Tree Steps, 2 Platform, 3 Front Wall, 4 Window Sill, 5 Two-Room House, 6 Tall Trunk, 7 Under the Roof,
  8 Garden Fence, 9 Rope Ladder, 10 Treehouse Done.
One model, every object tagged with its piece (0 = the tree itself). Renders (same camera for all stage frames):
  town_ch1_s00            the bare tree on its patch of earth (progress 0 / 10)
  town_ch1_p01 .. p10     piece k alone, the tree and pieces 1..k-1 as HOLDOUT (they cut it where they are in front),
                          so the home screen stacks s00 + p01 .. pN (same pivot, same canvas) = the house after N levels
  town_ch1_treehouse      v4 atlas name: the finished house (every piece), frame 760 x 820, pivot 0.5 0
  bld_<piece>_3d          piece k on its own, framed for the win window (360 x 360 design, pivot 0.5 0.5) with a soft
                          contact shadow: the "structure piece" the level built (ART turn 1, win window)
Frames 760 x 820 design px, pivot 0.5 0 (bottom centre = the lowest point of the finished model), 1.2 px per design px.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True  # no __pycache__ next to the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

PIECES = ['tree_steps', 'platform', 'front_wall', 'window_sill', 'two_rooms', 'tall_trunk', 'under_roof',
          'garden_fence', 'rope_ladder', 'treehouse']


def tag(o, k):
    o['piece'] = k
    return o


def build(sc):
    M = {}
    bark = C.principled('bark', '#8A5530', rough=0.7, coat=0.05, spec=0.3)
    C.noise_color(bark, '#8A5530', var=0.10, wave=(18.0, 0.6), bump=0.25)
    leaf = C.principled('leaf', '#5BBF3A', rough=0.5, coat=0.25, coat_rough=0.2, sss=0.08, spec=0.4)
    C.noise_color(leaf, '#5BBF3A', var=0.08, scale=14.0, bump=0.12)
    leaf2 = C.principled('leaf2', '#47A92E', rough=0.5, coat=0.25, coat_rough=0.2, sss=0.08, spec=0.4)
    C.noise_color(leaf2, '#47A92E', var=0.08, scale=14.0, bump=0.12)
    earth = C.principled('earth', '#9A6A3E', rough=0.9, coat=0.0, spec=0.2)
    C.noise_color(earth, '#9A6A3E', var=0.12, scale=60.0, bump=0.2)
    grass = C.principled('grass', '#6FC24A', rough=0.6, coat=0.1, spec=0.3)
    planks = C.principled('planks', '#F3D59A', rough=0.5, coat=0.15)
    C.noise_color(planks, '#F3D59A', var=0.05, wave=(14.0, 1.0), bump=0.05)
    trim = C.mat_wood('trim', '#B9824A')
    deck = C.mat_wood('deck', '#D9A766')
    roof = C.principled('roof', '#D9603A', rough=0.45, coat=0.15, sss=0.03, spec=0.35)
    C.noise_color(roof, '#D9603A', var=0.06, wave=(10.0, 0.5), bump=0.12)
    door = C.mat_candy('door', '#2E7FE0')
    glass = C.principled('glass', '#BFE6FF', rough=0.05, coat=1.0, spec=0.8)
    curtain = C.mat_candy('curtain', '#D8354A')
    flag = C.mat_candy('flag', '#2FB36A')
    gold = C.mat_gold('knob')
    rope = C.principled('rope', '#D8B47A', rough=0.7, coat=0.0, spec=0.3)
    white = C.principled('picket', '#FFF6E6', rough=0.4, coat=0.3)
    nail = C.principled('nail', '#B9C0C8', rough=0.3, coat=0.0, metallic=0.9)
    flower_r = C.mat_candy('fl_r', '#FF4F6A')
    flower_y = C.mat_candy('fl_y', '#FFD23F')
    pot = C.principled('pot', '#C9663A', rough=0.5, coat=0.2)
    bunting = [C.mat_candy('bt%d' % i, h) for i, h in enumerate(('#CF404E', '#FFE23E', '#2984DE', '#44D088', '#EC8D20'))]
    parts = []

    def add(o, k):
        parts.append(tag(o, k))
        return o

    # ---- 0: the tree on its patch of earth (always there)
    add(C.superellipsoid(sc, 'patch', 1.25, 0.85, 0.06, n=2.4, loc=(0.05, 0.15, -1.86), mat=earth), 0)
    for k, (x, y, s) in enumerate(((-0.9, -0.35, 0.16), (1.0, -0.2, 0.13), (-0.4, -0.62, 0.11), (0.7, -0.6, 0.12))):
        add(C.superellipsoid(sc, 'tuft', s, s, s * 0.7, n=2.0, loc=(x, y, -1.8), mat=grass), 0)
    trunk = C.cylinder(sc, 'trunk', 0.30, 3.3, loc=(0.0, 0.25, -0.2), mat=bark, seg=24, r2=0.21, bevel=0.06, bevel_seg=3)
    add(trunk, 0)
    for a in (0, 120, 240):
        r = C.superellipsoid(sc, 'root', 0.26, 0.10, 0.12, n=2.2, loc=(0.28 * math.cos(math.radians(a)), 0.25 + 0.28 * math.sin(math.radians(a)), -1.78), mat=bark)
        r.rotation_euler = (0, 0, math.radians(a))
        add(r, 0)
    for s in (-1, 1):  # branches that cradle the platform
        b = C.cylinder(sc, 'branch', 0.11, 1.25, loc=(s * 0.48, 0.25, 0.28), mat=bark, seg=16, r2=0.07, bevel=0.03)
        b.rotation_euler = (0, math.radians(s * 52), 0)
        add(b, 0)
    for k, (x, y, z, r, m) in enumerate(((0.0, 0.55, 2.55, 1.05, leaf), (-1.05, 0.5, 2.05, 0.78, leaf2),
                                          (1.08, 0.5, 2.1, 0.8, leaf), (-0.55, 0.7, 3.05, 0.72, leaf2),
                                          (0.62, 0.7, 3.1, 0.7, leaf2), (-1.5, 0.6, 1.4, 0.5, leaf),
                                          (1.5, 0.6, 1.45, 0.52, leaf2), (0.05, 0.85, 1.65, 0.85, leaf2))):
        add(C.superellipsoid(sc, 'leaves', r, r * 0.9, r * 0.82, n=2.0, loc=(x, y, z), mat=m, sub=5), 0)

    # ---- 1: tree steps nailed to the trunk
    for k in range(5):
        z = -1.45 + k * 0.34
        st = C.superellipsoid(sc, 'step', 0.26, 0.06, 0.045, n=4, loc=(0.0, -0.02, z), mat=deck)
        st.rotation_euler = (0, math.radians((-6, 4, -3, 5, -4)[k]), 0)
        add(st, 1)
        for dx in (-0.17, 0.17):
            add(C.uv_sphere(sc, 'nail', 0.022, loc=(dx, -0.08, z + 0.01), mat=nail, scale=(1, 0.5, 1)), 1)

    # ---- 2: platform (deck + railing at the front corners)
    add(C.superellipsoid(sc, 'deck', 1.45, 0.82, 0.09, n=6, loc=(0, 0.05, 0.0), mat=deck), 2)
    for x in (-1.35, -1.05, 1.05, 1.35):
        add(C.cylinder(sc, 'post', 0.045, 0.42, loc=(x, -0.72, 0.28), mat=trim, seg=16), 2)
    for x0, x1 in ((-1.4, -0.95), (0.95, 1.4)):
        add(C.superellipsoid(sc, 'hand', (x1 - x0) / 2 + 0.05, 0.04, 0.04, n=4, loc=((x0 + x1) / 2, -0.72, 0.47), mat=trim), 2)
    for k in range(7):
        add(C.superellipsoid(sc, 'board', 0.012, 0.8, 0.012, n=2, loc=(-1.2 + k * 0.4, 0.05, 0.088), mat=trim), 2)

    # ---- 3: front wall (plank facade, door, frame)
    add(C.superellipsoid(sc, 'front', 0.92, 0.05, 0.78, n=10, loc=(0, -0.55, 0.86), mat=planks), 3)
    for x in (-0.94, 0.94):
        add(C.superellipsoid(sc, 'corner', 0.05, 0.05, 0.8, n=4, loc=(x, -0.58, 0.86), mat=trim), 3)
    add(C.superellipsoid(sc, 'beamf', 0.95, 0.05, 0.04, n=6, loc=(0, -0.6, 0.25), mat=trim), 3)
    add(C.superellipsoid(sc, 'beamf2', 0.95, 0.05, 0.04, n=6, loc=(0, -0.6, 1.45), mat=trim), 3)
    add(C.superellipsoid(sc, 'door', 0.22, 0.04, 0.36, n=3.2, loc=(-0.15, -0.62, 0.62), mat=door), 3)
    add(C.uv_sphere(sc, 'knob', 0.035, loc=(0.0, -0.67, 0.6), mat=gold), 3)
    add(C.superellipsoid(sc, 'doorframe', 0.26, 0.03, 0.03, n=4, loc=(-0.15, -0.63, 0.99), mat=trim), 3)

    # ---- 4: window sill (window, curtains, sill with a flower box, porthole)
    add(C.superellipsoid(sc, 'win', 0.26, 0.04, 0.2, n=5, loc=(0.45, -0.62, 1.08), mat=glass), 4)
    for dx in (-0.17, 0.17):
        add(C.superellipsoid(sc, 'cur', 0.08, 0.05, 0.19, n=3, loc=(0.45 + dx, -0.64, 1.08), mat=curtain), 4)
    add(C.superellipsoid(sc, 'sill', 0.33, 0.07, 0.035, n=4, loc=(0.45, -0.66, 0.85), mat=trim), 4)
    add(C.superellipsoid(sc, 'box', 0.28, 0.07, 0.06, n=4, loc=(0.45, -0.7, 0.77), mat=pot), 4)
    for k in range(5):
        add(C.uv_sphere(sc, 'flower', 0.055, loc=(0.25 + k * 0.1, -0.74, 0.86), mat=flower_r if k % 2 == 0 else flower_y), 4)
    add(C.torus(sc, 'porth', 0.15, 0.035, loc=(-0.6, -0.62, 1.15), mat=trim, rot=(90, 0, 0)), 4)
    add(C.uv_sphere(sc, 'porthg', 0.13, loc=(-0.6, -0.61, 1.15), mat=glass, scale=(1, 0.3, 1)), 4)

    # ---- 5: two-room house (the house body behind the facade + a smaller side room on the right)
    add(C.superellipsoid(sc, 'house', 0.92, 0.6, 0.78, n=10, loc=(0, 0.08, 0.86), mat=planks), 5)
    add(C.superellipsoid(sc, 'beam', 0.95, 0.65, 0.04, n=6, loc=(0, 0.08, 1.45), mat=trim), 5)
    add(C.superellipsoid(sc, 'room2', 0.42, 0.45, 0.5, n=10, loc=(1.2, 0.18, 0.58), mat=planks), 5)
    add(C.superellipsoid(sc, 'room2c', 0.04, 0.04, 0.52, n=4, loc=(1.6, -0.27, 0.58), mat=trim), 5)
    add(C.superellipsoid(sc, 'room2w', 0.14, 0.03, 0.12, n=5, loc=(1.22, -0.28, 0.7), mat=glass), 5)

    # ---- 6: tall trunk supports (two log posts from the ground to the deck + braces)
    for x in (-1.15, 1.15):
        add(C.cylinder(sc, 'stilt', 0.09, 1.95, loc=(x, 0.3, -0.92), mat=bark, seg=16, bevel=0.03), 6)
        br = C.cylinder(sc, 'brace', 0.05, 0.95, loc=(x * 0.82, 0.3, -0.42), mat=trim, seg=12, bevel=0.02)
        br.rotation_euler = (0, math.radians(-x / abs(x) * 38), 0)
        add(br, 6)

    # ---- 7: under the roof (gable, roof slabs, ridge, side-room roof)
    tri = [(-1.0, 0.0), (1.0, 0.0), (0.0, 0.85)]
    g = C.prism(sc, 'gable', C.ccw(tri), -0.6, 0.6, planks, planks)
    g.rotation_euler = (math.radians(90), 0, 0)
    g.location = (0, 0.05, 1.62)
    add(g, 7)
    for s in (1, -1):
        r = C.superellipsoid(sc, 'roof', 0.78, 0.82, 0.07, n=8, loc=(s * 0.58, 0.05, 2.08), mat=roof)
        r.rotation_euler = (0, math.radians(s * 40), 0)
        add(r, 7)
    add(C.superellipsoid(sc, 'ridge', 0.1, 0.84, 0.08, n=3, loc=(0, 0.05, 2.5), mat=roof), 7)
    rr = C.superellipsoid(sc, 'roof2', 0.5, 0.52, 0.06, n=8, loc=(1.22, 0.18, 1.16), mat=roof)
    rr.rotation_euler = (0, math.radians(14), 0)
    add(rr, 7)
    add(C.uv_sphere(sc, 'attic', 0.1, loc=(0, -0.58, 1.95), mat=C.principled('dk', '#5A3A1E', rough=0.5), scale=(1, 0.3, 1)), 7)

    # ---- 8: garden fence (white pickets on an arc in front of the tree)
    for k in range(13):
        a = math.radians(200 + k * 11.5)
        x, y = 1.35 * math.cos(a) * 1.05, 0.15 + 0.85 * math.sin(a)
        pk = C.superellipsoid(sc, 'picket', 0.055, 0.03, 0.2, n=3, loc=(x, y, -1.6), mat=white)
        pk.rotation_euler = (0, 0, a + math.pi / 2)
        add(pk, 8)
        add(C.superellipsoid(sc, 'pickettip', 0.05, 0.028, 0.05, n=1.6, loc=(x, y, -1.38), mat=white), 8)
    pts = [(1.35 * 1.05 * math.cos(math.radians(200 + t * 138 / 20)), 0.15 + 0.85 * math.sin(math.radians(200 + t * 138 / 20)), -1.55)
           for t in range(21)]
    add(C.curve_tube(sc, 'rail', pts, 0.025, white), 8)

    # ---- 9: rope ladder hanging from the deck on the left
    for s in (-1, 1):
        add(C.curve_tube(sc, 'rope', [(-0.75 + s * 0.17, -0.6, 0.0), (-0.75 + s * 0.17, -0.66, -0.9), (-0.72 + s * 0.17, -0.62, -1.8)],
                         0.022, rope), 9)
    for k in range(6):
        z = -0.2 - k * 0.28
        add(C.superellipsoid(sc, 'rung', 0.2, 0.04, 0.03, n=4, loc=(-0.745, -0.65, z), mat=deck), 9)

    # ---- 10: done (flag on the roof, chimney, bunting along the railing, lantern)
    add(C.cylinder(sc, 'pole', 0.025, 1.1, loc=(1.42, 0.05, 1.7), mat=trim, seg=12), 10)  # on the side-room roof
    fl = C.prism(sc, 'flag', C.ccw([(0, 0), (0.55, 0.12), (0.45, 0.22), (0.58, 0.34), (0, 0.4)]), -0.015, 0.015, flag, flag, bevel=0.01)
    fl.rotation_euler = (math.radians(90), 0, 0)
    fl.location = (1.44, 0.05, 1.86)
    add(fl, 10)
    st = C.puffy(sc, 'star', C.ccw(C.star_pts(5, 0.08, 0.035)), 0.03, C.principled('st', '#FFFFFF', rough=0.3), bevel=0.008, sub=1)
    st.rotation_euler = (math.radians(90), 0, 0)
    st.location = (1.64, 0.02, 2.03)
    add(st, 10)
    add(C.superellipsoid(sc, 'chimney', 0.1, 0.1, 0.16, n=4, loc=(-0.5, 0.05, 2.4), mat=C.principled('chim', '#C9563A', rough=0.4)), 10)
    for k in range(9):
        x = -1.4 + k * 0.35
        z = 0.42 - 0.06 * math.sin(math.pi * ((x + 1.4) % 0.7) / 0.7)
        tri_ = C.prism(sc, 'pennant', C.ccw([(-0.08, 0.0), (0.08, 0.0), (0.0, -0.16)]), -0.01, 0.01, bunting[k % 5], bunting[k % 5], bevel=0.005)
        tri_.rotation_euler = (math.radians(90), 0, 0)
        tri_.location = (x, -0.76, z)
        add(tri_, 10)
    add(C.curve_tube(sc, 'string', [(-1.42, -0.76, 0.44), (-0.7, -0.77, 0.36), (0.0, -0.77, 0.44), (0.7, -0.77, 0.36), (1.42, -0.76, 0.44)],
                     0.008, trim), 10)
    lan = C.principled('lantern', '#FFE7A3', rough=0.3, coat=0.4, emission='#FFD36A', emission_strength=1.2)
    add(C.uv_sphere(sc, 'lantern', 0.08, loc=(1.0, -0.6, 1.25), mat=lan), 10)
    root = C.group(sc, parts, rot=(0, 0, -16))
    return parts, root


def set_stage(parts, show, holdout=()):
    for o in parts:
        k = o['piece']
        o.hide_render = k not in show and k not in holdout
        o.is_holdout = k in holdout


if __name__ == '__main__':
    out = C.ARGS.out
    sc = C.reset()
    w, h = round(760 * 1.2), round(820 * 1.2)
    C.setup_render(sc, w, h, 96)
    C.rig_C(sc)
    cam = C.prop_camera(sc, w // (2 if C.ARGS.quick else 1), h // (2 if C.ARGS.quick else 1))
    parts, root = build(sc)
    # one camera for every stage frame, fitted on the finished model; pivot 0.5 0 = the lowest point on the frame bottom
    bpy.context.view_layer.update()
    scale, (x0, x1, y0, y1) = C.fit_camera(sc, cam, 0.92)
    vh = scale if w < h else scale * h / w
    margin = (vh - (y1 - y0)) / 2
    R = cam.matrix_world.to_3x3().normalized()
    cam_full = cam.location + (R @ Vector((0, 1, 0))) * (margin - 0.02)
    cam.location = cam_full
    full_scale = cam.data.ortho_scale
    all_k = set(range(11))
    if C.ARGS.wants('town_ch1_treehouse'):
        set_stage(parts, all_k)
        C.render(sc, os.path.join(out, 'town_ch1_treehouse.png'))
    if C.ARGS.wants('town_ch1_s00'):
        set_stage(parts, {0})
        C.render(sc, os.path.join(out, 'town_ch1_s00.png'))
    for k in range(1, 11):
        if C.ARGS.wants('town_ch1_p%02d' % k):
            set_stage(parts, {k}, holdout=set(range(k)))
            C.render(sc, os.path.join(out, 'town_ch1_p%02d.png' % k))
    # win-window pieces: piece k alone, framed square, soft contact ellipse (post_env meta)
    sc.render.resolution_x = 720 // (2 if C.ARGS.quick else 1)
    sc.render.resolution_y = 720 // (2 if C.ARGS.quick else 1)
    cam['res'] = (720, 720)
    for k, nm in enumerate(PIECES, start=1):
        name = 'bld_%s_3d' % nm
        if not C.ARGS.wants(name):
            continue
        show = all_k if k == 10 else {k}
        set_stage(parts, show)
        objs = [o for o in parts if o['piece'] in show and o.type in ('MESH', 'CURVE')]
        fs, (bx0, bx1, by0, by1) = C.fit_camera(sc, cam, 0.80, objs=objs)
        bottom = 360 * (0.5 - (by1 - by0) / (2 * fs))
        C.write_meta(name, {'ellipse': {'cx': 180, 'cy': round(bottom + 4, 1), 'rx': round(min(150, 360 * (bx1 - bx0) / fs * 0.42), 1),
                                        'ry': 14, 'alpha': 0.26, 'blur': 7}})
        C.render(sc, os.path.join(out, name + '.png'))
    print('TOWN DONE', flush=True)
