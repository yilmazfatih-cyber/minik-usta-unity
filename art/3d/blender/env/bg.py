"""B1 backgrounds (docs/ART_3D.md section 5, rig F_bg): the home screen 'Renkli Tepe' as separate parallax layers and the
calm level background.

    blender -b --factory-startup -noaudio --python art/3d/blender/env/bg.py -- --out art/3d/out/env/raw [--only bg_home_*] [--quick]

One perspective scene (camera 12 deg down, 62 deg vertical field of view, 1080 x 1920 design canvas). Every layer is
rendered on the FULL canvas with only its own objects visible to the camera; the other layers stay in the scene as
shadow / bounce casters (visible_camera False), so the layers composite back-to-front into the same picture. post_env.py
crops each layer to its contract size (frames.json), records where it sits (fx.json -> layers) and flattens them into
the v4 frame bg_home. bg_level: indigo backdrop + radial glow + faint blueprint grid + out-of-focus crane / scaffold
silhouettes at the edges (Cycles depth of field), nothing behind the tray.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.dont_write_bytecode = True  # no __pycache__ next to the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

W, H = 1080, 1920
LAYERS = {
    # name: (render px per design px, samples)
    'bg_home_sky': (0.5, 16),
    'bg_home_clouds': (0.75, 48),
    'bg_home_far': (0.75, 48),
    'bg_home_town': (1.0, 64),
    'bg_home_ground': (1.0, 64),
    'bg_home_fore_l': (1.0, 64),
    'bg_home_fore_r': (1.0, 64),
}
rnd = random.Random(7)


def out(name):
    return os.path.join(C.ARGS.out, name + '.png')


def coll(sc, name):
    c = bpy.data.collections.new(name)
    sc.collection.children.link(c)
    return c


def put(c, o):
    for uc in list(o.users_collection):
        uc.objects.unlink(o)
    c.objects.link(o)
    return o


def sky_world(sc):
    w = bpy.data.worlds.new('sky')
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes['Background']
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    els = ramp.color_ramp.elements
    # world 'Generated' = view direction; z = sin(elevation): horizon blush -> deep blue overhead
    els[0].position = 0.0
    els[0].color = C.lin('#F0D7D6')
    els[1].position = 0.6
    els[1].color = C.lin('#1B49A4')
    for pos, col in ((0.05, '#C9D3EC'), (0.13, '#8FB2E0'), (0.24, '#5C88CC'), (0.38, '#2B5FB6')):
        e = els.new(pos)
        e.color = C.lin(col)
    nt.links.new(sep.outputs['Z'], ramp.inputs['Fac'])
    # sun halo (upper right): a glow added by the direction to the sun
    sun_dir = Vector((0.55, 0.6, 0.58)).normalized()
    dot = nt.nodes.new('ShaderNodeVectorMath')
    dot.operation = 'DOT_PRODUCT'
    dot.inputs[1].default_value = sun_dir
    nt.links.new(tc.outputs['Generated'], dot.inputs[0])
    pw = nt.nodes.new('ShaderNodeMath')
    pw.operation = 'POWER'
    pw.inputs[1].default_value = 40.0
    nt.links.new(dot.outputs['Value'], pw.inputs[0])
    clampn = nt.nodes.new('ShaderNodeMath')
    clampn.operation = 'MAXIMUM'
    clampn.inputs[1].default_value = 0.0
    nt.links.new(pw.outputs[0], clampn.inputs[0])
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.blend_type = 'ADD'
    mix.inputs['B'].default_value = C.lin('#FFF3C4')
    nt.links.new(clampn.outputs[0], mix.inputs['Factor'])
    nt.links.new(ramp.outputs['Color'], mix.inputs['A'])
    nt.links.new(mix.outputs['Result'], bg.inputs['Color'])
    bg.inputs['Strength'].default_value = 1.0
    return w


def mat(name, hex_, rough=0.5, coat=0.15, sss=0.0, var=0.0, scale=8.0, bump=0.0):
    m = C.principled(name, hex_, rough=rough, coat=coat, sss=sss)
    if var:
        C.noise_color(m, hex_, var=var, scale=scale, bump=bump)
    return m


def blob(c, sc, name, r, loc, m, sx=1, sy=1, sz=1, seg=24):
    o = C.uv_sphere(sc, name, r, loc=loc, mat=m, seg=seg, rings=seg // 2, scale=(sx, sy, sz))
    return put(c, o)


def box(c, sc, name, size, loc, m, bevel=0.05, rot=(0, 0, 0)):
    o = C.superellipsoid(sc, name, size[0] / 2, size[1] / 2, size[2] / 2, n=14, loc=loc, mat=m, sub=4)
    o.rotation_euler = [math.radians(a) for a in rot]
    return put(c, o)


def terrain(c, sc, name, x0, x1, y0, y1, height_fn, m, res=80):
    bm = bmesh.new()
    verts = []
    for j in range(res + 1):
        row = []
        for i in range(res + 1):
            x = x0 + (x1 - x0) * i / res
            y = y0 + (y1 - y0) * j / res
            row.append(bm.verts.new((x, y, height_fn(x, y))))
        verts.append(row)
    for j in range(res):
        for i in range(res):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    o = C.mesh_obj(sc, name, bm)
    o.data.materials.append(m)
    for p in o.data.polygons:
        p.use_smooth = True
    return put(c, o)


def house(c, sc, x, y, z, w, d, h, roof_hex, wall_hex, yaw=0.0):
    wall = mat('wall' + wall_hex, wall_hex, rough=0.55, coat=0.1)
    roofm = mat('roof' + roof_hex, roof_hex, rough=0.35, coat=0.4, sss=0.05)
    win = mat('win', '#2E5FA8', rough=0.15, coat=0.8)
    door = mat('door', '#7A4A2A', rough=0.5)
    objs = [box(c, sc, 'wall', (w, d, h), (x, y, z + h / 2), wall)]
    # gable roof: two slanted slabs
    pitch = 34
    rl = (w / 2) / math.cos(math.radians(pitch)) + 0.18
    for s in (1, -1):
        o = box(c, sc, 'roof', (rl, d + 0.35, 0.16), (x + s * w / 4, y, z + h + (w / 4) * math.tan(math.radians(pitch)) + 0.02), roofm, rot=(0, s * pitch, 0))
        objs.append(o)
    objs.append(box(c, sc, 'door', (w * 0.22, 0.06, h * 0.45), (x - w * 0.1, y - d / 2 - 0.02, z + h * 0.225), door))
    for wx in (-0.3, 0.25):
        objs.append(box(c, sc, 'win', (w * 0.18, 0.06, h * 0.22), (x + wx * w + 0.1 * w, y - d / 2 - 0.02, z + h * 0.62), win))
    if yaw:
        piv = bpy.data.objects.new('hp', None)
        c.objects.link(piv)
        piv.location = (x, y, z)
        for o in objs:
            o.parent = piv
            o.location = Vector(o.location) - Vector((x, y, z))
        piv.rotation_euler = (0, 0, math.radians(yaw))
    return objs


def tree(c, sc, x, y, z, s=1.0, hex_='#5DB24A'):
    trunk = mat('trunk', '#8A5A33', rough=0.7)
    leaf = mat('leaf' + hex_, hex_, rough=0.55, coat=0.15, sss=0.05, var=0.08, scale=6.0, bump=0.1)
    put(c, C.cylinder(sc, 'trunk', 0.18 * s, 1.4 * s, loc=(x, y, z + 0.7 * s), mat=trunk, seg=12))
    blob(c, sc, 'crown', 0.9 * s, (x, y, z + 1.9 * s), leaf, sz=0.95)
    blob(c, sc, 'crown2', 0.6 * s, (x + 0.55 * s, y - 0.2 * s, z + 1.55 * s), leaf)
    blob(c, sc, 'crown3', 0.55 * s, (x - 0.55 * s, y - 0.1 * s, z + 1.6 * s), leaf)


def crane(c, sc, x, y, z, h=9.0):
    yel = mat('craneY', '#FFC21A', rough=0.35, coat=0.4)
    org = mat('craneO', '#EC8D20', rough=0.35, coat=0.4)
    steel = mat('cable', '#5A5F6E', rough=0.4)
    put(c, C.superellipsoid(sc, 'mast', 0.28, 0.28, h / 2, n=6, loc=(x, y, z + h / 2), mat=yel, sub=3))
    for k in range(int(h / 0.9)):
        o = box(c, sc, 'brace', (0.05, 0.62, 0.05), (x, y - 0.3, z + 0.45 + k * 0.9), org, rot=(45, 0, 0))
    put(c, C.superellipsoid(sc, 'jib', 4.2, 0.22, 0.2, n=6, loc=(x + 2.6, y, z + h + 0.2), mat=yel, sub=3))
    put(c, C.superellipsoid(sc, 'cjib', 1.2, 0.24, 0.22, n=6, loc=(x - 1.6, y, z + h + 0.2), mat=yel, sub=3))
    box(c, sc, 'weight', (0.8, 0.6, 0.6), (x - 2.4, y, z + h - 0.2), mat('cw', '#6E6A7A', rough=0.5))
    box(c, sc, 'cab', (0.7, 0.7, 0.6), (x + 0.45, y - 0.1, z + h - 0.25), org)
    put(c, C.cylinder(sc, 'cable', 0.025, 3.0, loc=(x + 5.2, y, z + h - 1.3), mat=steel, seg=8))
    box(c, sc, 'load', (0.9, 0.5, 0.45), (x + 5.2, y, z + h - 3.0), mat('loadR', '#CF404E', rough=0.3, coat=0.5))


def build_home(sc):
    cols = {k: coll(sc, k) for k in LAYERS if k != 'bg_home_sky'}
    sky_world(sc)
    C.sun(sc, 'sun', '#FFE9C8', 3.0, (38, 0, -32), 4)
    # meadow materials
    meadow = C.principled('meadow', '#6FBF52', rough=0.75, coat=0.05)
    C.noise_color(meadow, '#6FBF52', var=0.12, scale=0.6, bump=0.05)
    meadow_far = C.principled('meadow2', '#7CC75A', rough=0.75, coat=0.05)
    C.noise_color(meadow_far, '#7CC75A', var=0.1, scale=0.3)
    sand = C.principled('path', '#E9C891', rough=0.85, coat=0.0)
    C.noise_color(sand, '#E9C891', var=0.06, scale=3.0, bump=0.1)

    # GROUND (near meadow + the path), y -2 .. 22
    g = cols['bg_home_ground']

    def H(x, y):
        # ONE continuous meadow height for the near ground and the town hill (no crease where the layers meet)
        return 0.22 * math.sin(x * 0.35) * math.cos(y * 0.2) + 3.4 * math.exp(-((x - 2) ** 2) / 300 - ((y - 40) ** 2) / 260) + 0.12 * max(0.0, y - 46)

    def near_h(x, y):
        return H(x, y)

    terrain(g, sc, 'near', -40, 40, -16, 19.5, near_h, meadow, res=110)
    # path: a strip winding from the bottom centre to the town
    bm = bmesh.new()
    pts = []
    N = 40
    for k in range(N + 1):
        t = k / N
        y = -12 + t * 36
        x = 1.6 * math.sin(t * 3.2 + 0.6) * (1 - 0.6 * t) + 0.8
        wdt = 2.6 * (1 - 0.65 * t) + 0.4
        pts.append((x, y, wdt))
    rows = []
    for x, y, wdt in pts:
        rows.append([bm.verts.new((x + wdt * (i / 4 - 0.5), y, near_h(x + wdt * (i / 4 - 0.5), y) + 0.04)) for i in range(5)])
    for k in range(N):
        for i in range(4):
            bm.faces.new((rows[k][i], rows[k][i + 1], rows[k + 1][i + 1], rows[k + 1][i]))
    po = C.mesh_obj(sc, 'path', bm)
    po.data.materials.append(sand)
    for p in po.data.polygons:
        p.use_smooth = True
    put(g, po)
    # flowers dotted on the meadow
    fl = [mat('f%d' % i, c_, rough=0.4, coat=0.3, sss=0.1) for i, c_ in enumerate(('#FFF3F8', '#FFE23E', '#FF8AC4'))]
    for k in range(60):
        x = rnd.uniform(-12, 12)
        y = rnd.uniform(0, 16)
        if abs(x - 0.8) < 2.5:
            continue
        blob(g, sc, 'flower', 0.07, (x, y, near_h(x, y) + 0.06), fl[k % 3], seg=8)

    # TOWN (hill with houses, crane, trees), y 22 .. 60
    t = cols['bg_home_town']

    def hill_h(x, y):
        return H(x, y) + 0.01  # a hair above the near meadow in the 19.0-19.5 overlap: it occludes it cleanly

    terrain(t, sc, 'hill', -55, 55, 19.0, 80, hill_h, meadow, res=110)
    walls = ['#FFF1D2', '#FCE6C6', '#F5EEE4', '#FFF6E0']
    roofs = ['#CF404E', '#EC8D20', '#2984DE', '#44D088', '#FFE23E', '#CF404E', '#5D3AAE', '#EC8D20']
    spots = [(-13, 37), (-8.5, 42), (-4, 35), (-1, 46), (5, 40), (9.5, 44), (13.5, 37), (16, 48), (-15.5, 47), (2.5, 33)]
    for k, (x, y) in enumerate(spots):
        house(t, sc, x, y, hill_h(x, y) - 0.15, rnd.uniform(2.8, 3.6), rnd.uniform(2.4, 3.0), rnd.uniform(2.2, 3.0), roofs[k % len(roofs)], walls[k % 4], yaw=rnd.uniform(-18, 18))
    for k in range(18):
        x = rnd.uniform(-24, 24)
        y = rnd.uniform(30, 56)
        if any(abs(x - sx) < 3.2 and abs(y - sy) < 3.4 for sx, sy in spots):
            continue
        tree(t, sc, x, y, hill_h(x, y) - 0.2, s=rnd.uniform(1.1, 1.6), hex_=rnd.choice(['#4FA845', '#5DB24A', '#3F9A3E']))
    crane(t, sc, 12, 52, hill_h(12, 52) - 0.2, h=13.0)

    # FAR hills (air perspective: colours pre-mixed with the mist #C7D8F2)
    f = cols['bg_home_far']
    for k, (x, y, r, sz, col) in enumerate([(-60, 150, 55, 0.32, '#8FBFAE'), (10, 170, 70, 0.30, '#9CC6BA'), (75, 140, 50, 0.36, '#86B8A8'),
                                           (-20, 120, 40, 0.30, '#7DB49B'), (40, 115, 34, 0.32, '#78B096')]):
        m = mat('hill%d' % k, col, rough=0.8, coat=0.0)
        blob(f, sc, 'far', r, (x, y, -r * sz * 0.2), m, sz=sz * 1.35, seg=48)
    # CLOUDS
    cl = cols['bg_home_clouds']
    cm = C.principled('cloud', '#FFFFFF', rough=0.9, coat=0.0, sss=0.35)
    C.gradient_input(cm, [(0.0, '#E6D2DD'), (0.5, '#F4E3E8'), (1.0, '#FFFFFF')], axis='Z', lo=-3.0, hi=3.0, space='Object')
    for (x, y, z, s) in [(-34, 140, 36, 1.4), (30, 150, 44, 1.6), (58, 130, 30, 1.0), (-4, 170, 58, 1.2), (-62, 175, 60, 1.3)]:
        root = bpy.data.objects.new('cloud', None)
        cl.objects.link(root)
        root.location = (x, y, z)
        for k in range(7):
            o = blob(cl, sc, 'puff', (4.5 - abs(k - 3) * 0.8) * s, (0, 0, 0), cm, sz=0.75, seg=24)
            o.parent = root
            o.location = ((k - 3) * 3.6 * s, rnd.uniform(-1, 1), rnd.uniform(-0.5, 1.0) + (1.2 if k in (2, 3, 4) else 0) * s)
    # FOREGROUND left: bush cluster with flowers; right: pallet with block bricks + sand pile
    fl_l = cols['bg_home_fore_l']
    bush = C.principled('bush', '#3E8E3A', rough=0.55, coat=0.2, sss=0.05)
    C.noise_color(bush, '#3E8E3A', var=0.1, scale=3.0, bump=0.15)
    bush2 = C.principled('bush2', '#4FA845', rough=0.55, coat=0.2, sss=0.05)
    for (x, y, z, r, m_) in [(-4.4, 0.0, 0.6, 1.6, bush), (-3.0, -0.8, 0.3, 1.1, bush2), (-5.2, 1.2, 1.3, 1.3, bush2), (-3.6, 1.4, 1.6, 1.0, bush),
                             (-2.3, 0.6, 0.5, 0.8, bush2)]:
        blob(fl_l, sc, 'bush', r, (x, y, z), m_, sz=0.85, seg=32)
    for k in range(9):
        blob(fl_l, sc, 'bloom', 0.13, (-4.6 + rnd.uniform(-1.2, 1.6), -1.4 + rnd.uniform(-0.2, 1.4), 0.7 + rnd.uniform(0, 1.6)), fl[k % 3], seg=10)
    # art turn 1: the right foreground mirrors the left bush cluster (the half-cut brick pallet at the right edge read
    # as a rendering error in the review); organic shapes may run off the frame edge, a cut man-made object may not
    fl_r = cols['bg_home_fore_r']
    for (x, y, z, r, m_) in [(6.0, 0.0, 0.6, 1.6, bush2), (4.6, -0.8, 0.3, 1.1, bush), (6.8, 1.2, 1.3, 1.3, bush), (5.2, 1.4, 1.6, 1.0, bush2),
                             (3.9, 0.6, 0.5, 0.8, bush)]:
        blob(fl_r, sc, 'bush', r, (x, y, z), m_, sz=0.85, seg=32)
    for k in range(9):
        blob(fl_r, sc, 'bloom', 0.13, (6.2 + rnd.uniform(-1.6, 1.2), -1.4 + rnd.uniform(-0.2, 1.4), 0.7 + rnd.uniform(0, 1.6)), fl[(k + 1) % 3], seg=10)
    # art turn 1: a cobbled town square under the Play button (the green button no longer sits on green grass):
    # warm stone cobbles (Voronoi cells), a darker kerb ring, on the ground layer at the button's ground point
    stone = C.principled('cobble', '#D2BE9A', rough=0.8, coat=0.0, spec=0.25)
    nt = stone.node_tree
    tc = nt.nodes.new('ShaderNodeTexCoord')
    vor = nt.nodes.new('ShaderNodeTexVoronoi')
    vor.feature = 'DISTANCE_TO_EDGE'
    vor.inputs['Scale'].default_value = 5.5
    nt.links.new(tc.outputs['Object'], vor.inputs['Vector'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    cr = ramp.color_ramp
    cr.elements[0].position, cr.elements[0].color = 0.0, C.lin('#8C7658')
    cr.elements[1].position, cr.elements[1].color = 0.08, C.lin('#D8C6A2')
    nt.links.new(vor.outputs['Distance'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], nt.nodes['Principled BSDF'].inputs['Base Color'])
    bp = nt.nodes.new('ShaderNodeBump')
    bp.inputs['Strength'].default_value = 0.35
    nt.links.new(vor.outputs['Distance'], bp.inputs['Height'])
    nt.links.new(bp.outputs['Normal'], nt.nodes['Principled BSDF'].inputs['Normal'])
    kerb = C.principled('kerb', '#9C8768', rough=0.7, coat=0.05)
    pc = (0.8, 0.5)
    pz = near_h(*pc)
    put(g, C.cylinder(sc, 'plaza', 2.6, 0.40, loc=(pc[0], pc[1], pz - 0.05), mat=stone, seg=96, bevel=0.04, bevel_seg=3))
    put(g, C.torus(sc, 'kerb', 2.62, 0.09, loc=(pc[0], pc[1], pz + 0.15), mat=kerb, seg=96, mseg=12))
    return cols


def setup_cam(sc, res_scale, samples):
    C.setup_render(sc, round(W * res_scale), round(H * res_scale), samples, transparent=True)
    cd = bpy.data.cameras.new('cam')
    cd.type = 'PERSP'
    cd.sensor_fit = 'VERTICAL'
    cd.sensor_height = 24.0
    cd.lens = 24.0 / (2 * math.tan(math.radians(62) / 2))
    cd.clip_end = 1000
    cam = bpy.data.objects.new('cam', cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    # 6 deg down (rig F says 12): the town sits just under the horizon at ~45 % of the height, below the title ribbon and
    # the progress bar of the home UI and behind the character group (v4 home layout); the sky stays calm behind the UI
    cam.location = (0.8, -9.0, 4.2)
    cam.rotation_euler = (math.radians(84), 0, 0)
    return cam


def render_home():
    names = [n for n in LAYERS if C.ARGS.wants(n)]
    if not names:
        return
    sc = C.reset()
    cols = build_home(sc)
    setup_cam(sc, 1.0, 64)
    for name in names:
        rs, samples = LAYERS[name]
        q = 2 if C.ARGS.quick else 1
        sc.render.resolution_x = round(W * rs) // q
        sc.render.resolution_y = round(H * rs) // q
        sc.cycles.samples = max(4, int(samples * C.ARGS.samples_mult / (8 if C.ARGS.quick else 1)))
        sky = name == 'bg_home_sky'
        sc.render.film_transparent = not sky
        order = list(LAYERS)
        for cn, c in cols.items():
            on = (cn == name)
            farther = order.index(cn) < order.index(name)
            for o in c.all_objects:
                # this layer: visible. Nearer layers: invisible to the camera (this layer stays complete behind them) but
                # still cast shadows. Farther layers: HOLDOUT, so parts of this layer that run physically behind a farther
                # object (the meadow's far edge under the hill) are cut out instead of pasted over it.
                o.hide_render = sky
                o.is_holdout = (not on) and farther
                o.visible_camera = on or farther
        C.render(sc, out(name))
        C.write_meta(name, {'layer': True})


def render_level():
    if not C.ARGS.wants('bg_level'):
        return
    sc = C.reset()
    C.setup_render(sc, round(W * 0.75), round(H * 0.75), 64, transparent=False)
    w = bpy.data.worlds.new('w')
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs['Color'].default_value = C.lin('#1A173C')
    w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.0
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'
    cd.ortho_scale = 19.2
    cd.dof.use_dof = True
    cd.dof.focus_distance = 30.0
    cd.dof.aperture_fstop = 0.6
    cam = bpy.data.objects.new('cam', cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = (0, 0, 30)
    # backdrop: emission, indigo + radial glow at (540, 0.26 H from the top) + blueprint grid alpha 0.02 + vignette
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=12)
    bd = C.mesh_obj(sc, 'backdrop', bm)
    m = bpy.data.materials.new('bd')
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    outn = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(em.outputs[0], outn.inputs['Surface'])
    tc = nt.nodes.new('ShaderNodeTexCoord')
    # glow centre: x 0, y = (0.5 - 0.26) * 19.2 = 4.61
    vm = nt.nodes.new('ShaderNodeVectorMath')
    vm.operation = 'DISTANCE'
    vm.inputs[1].default_value = (0, 4.61, 0)
    nt.links.new(tc.outputs['Object'], vm.inputs[0])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    els = ramp.color_ramp.elements
    els[0].position = 0.0
    els[0].color = C.lin('#2B2661')
    els[1].position = 1.0
    els[1].color = C.lin('#151233')
    e = els.new(0.45)
    e.color = C.lin('#1A173C')
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = 0.0
    mr.inputs['From Max'].default_value = 13.0
    nt.links.new(vm.outputs['Value'], mr.inputs['Value'])
    nt.links.new(mr.outputs['Result'], ramp.inputs['Fac'])
    # grid lines (blueprint) every 1.2 BU, alpha 0.025 lighter
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], sep.inputs[0])
    grid_val = None
    for ax in ('X', 'Y'):
        md = nt.nodes.new('ShaderNodeMath')
        md.operation = 'PINGPONG'
        md.inputs[1].default_value = 0.6
        nt.links.new(sep.outputs[ax], md.inputs[0])
        lt = nt.nodes.new('ShaderNodeMath')
        lt.operation = 'LESS_THAN'
        lt.inputs[1].default_value = 0.012
        nt.links.new(md.outputs[0], lt.inputs[0])
        if grid_val is None:
            grid_val = lt
        else:
            mx = nt.nodes.new('ShaderNodeMath')
            mx.operation = 'MAXIMUM'
            nt.links.new(grid_val.outputs[0], mx.inputs[0])
            nt.links.new(lt.outputs[0], mx.inputs[1])
            grid_val = mx
    gm = nt.nodes.new('ShaderNodeMath')
    gm.operation = 'MULTIPLY'
    gm.inputs[1].default_value = 0.035
    nt.links.new(grid_val.outputs[0], gm.inputs[0])
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['B'].default_value = C.lin('#8FA8FF')
    nt.links.new(gm.outputs[0], mix.inputs['Factor'])
    nt.links.new(ramp.outputs['Color'], mix.inputs['A'])
    nt.links.new(mix.outputs['Result'], em.inputs['Color'])
    em.inputs['Strength'].default_value = 1.0
    bd.data.materials.append(m)
    # out-of-focus silhouettes (lighter indigo, low alpha via emission mix): crane arm bottom-left, scaffold top-right
    sil = C.emission_mat('sil', '#26225A', 1.0)  # ~alpha 0.07 over the indigo (ART_3D 5.2)
    for (x, y, sx, sy, rot) in [(-4.2, -7.6, 4.5, 0.25, 18), (-6.0, -6.0, 0.25, 4.0, 0), (-5.2, -5.0, 2.0, 0.12, -40), (4.8, 7.4, 0.2, 3.2, 0),
                                 (3.6, 7.4, 0.2, 3.2, 0), (4.2, 6.2, 1.6, 0.14, 0), (4.2, 8.2, 1.6, 0.14, 0), (4.2, 7.2, 1.7, 0.1, 35)]:
        o = C.superellipsoid(sc, 'sil', sx, sy, 0.1, n=6, loc=(x, y, 6.0), mat=sil, sub=2)
        o.rotation_euler = (0, 0, math.radians(rot))
    C.render(sc, out('bg_level'))


if __name__ == '__main__':
    render_home()
    render_level()
    print('BG DONE', flush=True)
