"""B1 FX (docs/ART_3D.md sections 7.3-7.4; rig E_fx: front view): the clip drawings of frames.json -> clips (fx_*) and the
v4 particle frames in 3D (fx_chip, fx_confetti, fx_confetti_round, fx_dust, fx_ring, fx_spark, fx_star).

    blender -b --factory-startup -noaudio --python art/3d/blender/env/fx.py -- --out art/3d/out/env/raw [--only fx_hammer_*] [--quick]

Canvas units: 1 BU = 100 design px, the camera frames the clip canvas exactly, so the clip pivot (frames.json) is a fixed
point of every drawing. Glows are emission planes with a radial alpha falloff (no compositor, no bloom pass); tinted
effects (paint, confetti, dust, chip, ring, spark, star) render white / light grey so the code tint keeps the shading.
Timing comes from JUICE (the code stretches the drawings to the JUICE duration); drawings follow the 12 principles:
anticipation, overshoot and settle, ease in / out, arcs.
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
import icons as I  # noqa: E402

U = 0.01  # BU per design px
RS = 2.0  # render px per design px at store scale 1 (post resizes to the stored scale)


def out(name):
    return os.path.join(C.ARGS.out, name + '.png')


def ease_out(t):
    return 1 - (1 - t) ** 3


def ease_in_out(t):
    return 3 * t * t - 2 * t * t * t


def scene(w, h, res_scale=RS, samples=48, lights='C'):
    sc = C.reset()
    C.setup_render(sc, round(w * res_scale), round(h * res_scale), samples)
    if lights == 'C':
        C.rig_C(sc)
    else:
        C.world(sc, '#000000', 0.0)
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'
    cd.ortho_scale = max(w, h) * U
    cam = bpy.data.objects.new('cam', cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.rotation_euler = (math.radians(90), 0, 0)
    cam.location = (w * U / 2, -20, h * U / 2)  # canvas bottom-left = world (0, 0, 0); x right, z up
    return sc


def glow_mat(name, color, strength=2.0, power=2.0, core=None):
    """Emission with a radial alpha falloff on a 1 x 1 plane (object space): alpha = (1 - 2 r)^power."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.blend_method = 'BLEND'
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    outn = nt.nodes.new('ShaderNodeOutputMaterial')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    ln = nt.nodes.new('ShaderNodeVectorMath')
    ln.operation = 'LENGTH'
    nt.links.new(tc.outputs['Object'], ln.inputs[0])
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = 0.0
    mr.inputs['From Max'].default_value = 0.5
    mr.inputs['To Min'].default_value = 1.0
    mr.inputs['To Max'].default_value = 0.0
    nt.links.new(ln.outputs['Value'], mr.inputs['Value'])
    pw = nt.nodes.new('ShaderNodeMath')
    pw.operation = 'POWER'
    pw.inputs[1].default_value = power
    nt.links.new(mr.outputs['Result'], pw.inputs[0])
    fac = nt.nodes.new('ShaderNodeMath')
    fac.operation = 'MULTIPLY'
    fac.inputs[1].default_value = 1.0
    fac.name = 'opacity'
    nt.links.new(pw.outputs[0], fac.inputs[0])
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Strength'].default_value = strength
    if core:
        ramp = nt.nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position = 0.0
        ramp.color_ramp.elements[0].color = C.lin(color)
        ramp.color_ramp.elements[1].position = 0.8
        ramp.color_ramp.elements[1].color = C.lin(core)
        nt.links.new(pw.outputs[0], ramp.inputs['Fac'])
        nt.links.new(ramp.outputs['Color'], em.inputs['Color'])
    else:
        em.inputs['Color'].default_value = C.lin(color)
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(fac.outputs[0], mix.inputs['Fac'])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], outn.inputs['Surface'])
    return m


def ring_mat(name, color, strength):
    """Emission x opacity (fades to transparent, never to a dark opaque ring)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.blend_method = 'BLEND'
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    outn = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = C.lin(color)
    em.inputs['Strength'].default_value = strength
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    fac = nt.nodes.new('ShaderNodeValue')
    fac.name = 'opacity'
    fac.outputs[0].default_value = 1.0
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(fac.outputs[0], mix.inputs['Fac'])
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], outn.inputs['Surface'])
    return m


def set_ring(m, a):
    m.node_tree.nodes['opacity'].outputs[0].default_value = max(0.0, min(1.0, a))


def set_opacity(m, a):
    m.node_tree.nodes['opacity'].inputs[1].default_value = a


def plane(sc, name, size, loc, mat, rot=0.0, sx=1.0, sy=1.0):
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
    o = C.mesh_obj(sc, name, bm)
    o.data.materials.append(mat)
    o.rotation_euler = (math.radians(90), math.radians(rot), 0)
    o.scale = (size * sx, size * sy, 1)
    o.location = loc
    return o


def star_glint_pts(n=4, r_in=0.16):
    return C.star_pts(n, 1.0, r_in, rot=90)


def flat_shape(sc, name, pts, mat, loc, scale, rot=0.0):
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, 0)) for x, y in C.ccw(pts)]
    bm.faces.new(vs)
    o = C.mesh_obj(sc, name, bm)
    o.data.materials.append(mat)
    o.rotation_euler = (math.radians(90), math.radians(rot), 0)
    o.scale = (scale, scale, scale)
    o.location = loc
    return o


def white(name='white', rough=0.45, coat=0.2, alpha=1.0, sss=0.0, glow=0.0):
    return C.principled(name, '#F4F4F4', rough=rough, coat=coat, alpha=alpha, sss=sss, emission='#FFFFFF' if glow else None, emission_strength=glow)


def want(prefix, n):
    return any(C.ARGS.wants('%s_%02d' % (prefix, k)) for k in range(n))


# ------------------------------------------------------------------------------------------------------------- clips
def crane_claw():
    if not want('fx_crane_claw', 8):
        return
    w, h = 160, 200
    sc = scene(w, h)
    org = C.mat_candy('org', '#EC8D20')
    chrome = C.mat_chrome()
    steel = C.principled('steel', '#7E8696', rough=0.35, metallic=0.9)
    cx = w / 2 * U
    C.cylinder(sc, 'cable', 0.03, 0.6, loc=(cx, 0, (h - 30) * U + 0.3), mat=steel, seg=12)
    hub = C.superellipsoid(sc, 'hub', 0.36, 0.28, 0.24, n=3.0, loc=(cx, 0, (h - 52) * U), mat=org)
    C.cylinder(sc, 'bolt', 0.09, 0.06, loc=(cx, -0.28, (h - 52) * U), mat=chrome, rot=(90, 0, 0), bevel=0.015)
    prongs = []
    for k in range(3):
        piv = bpy.data.objects.new('piv', None)
        sc.collection.objects.link(piv)
        piv.location = (cx + (k - 1) * 0.16, (0.0 if k != 1 else 0.12), (h - 72) * U)
        t = C.curve_tube(sc, 'prong', [(0, 0, 0), (0.06 * (k - 1) * 3 if k != 1 else 0, 0, -0.45), ((k - 1) * 0.05, 0, -0.85), (-(k - 1) * 0.1 if k != 1 else 0.0, 0, -1.05)], 0.05, chrome)
        t.parent = piv
        C.uv_sphere(sc, 'tip', 0.06, loc=(-(k - 1) * 0.1 if k != 1 else 0.0, 0, -1.05), mat=chrome).parent = piv
        prongs.append((piv, k - 1))
    for d in range(8):
        name = 'fx_crane_claw_%02d' % d
        t = ease_in_out(d / 7)
        spread = 34 * (1 - t) + 3  # open -> closed (grab at 5)
        for piv, s in prongs:
            piv.rotation_euler = (0, math.radians(-s * spread), 0)
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def hammer():
    if not want('fx_hammer', 11):
        return
    w, h = 320, 320
    sc = scene(w, h)
    rubber = C.principled('rub', '#E8302F', rough=0.42, coat=0.35, sss=0.05)
    cap = C.mat_chrome('cap')
    wood = C.mat_wood('wood', '#C98A4B')
    grip = C.principled('g', '#3A3346', rough=0.6)
    piv = bpy.data.objects.new('piv', None)
    sc.collection.objects.link(piv)
    piv.location = (0.8 * w * U, 0, 0.1 * h * U)
    L = 2.3
    parts = [C.superellipsoid(sc, 'handle', 0.08, 0.08, L / 2, n=2.2, loc=(0, 0, L / 2), mat=wood),
             C.cylinder(sc, 'grip', 0.095, 0.4, loc=(0, 0, 0.2), mat=grip, bevel=0.03),
             C.cylinder(sc, 'head', 0.3, 0.86, loc=(0, 0, L), mat=rubber, rot=(0, 90, 0), bevel=0.09, bevel_seg=5, seg=48),
             C.cylinder(sc, 'c1', 0.25, 0.05, loc=(0.45, 0, L), mat=cap, rot=(0, 90, 0), bevel=0.015),
             C.cylinder(sc, 'c2', 0.25, 0.05, loc=(-0.45, 0, L), mat=cap, rot=(0, 90, 0), bevel=0.015)]
    for p_ in parts:
        p_.parent = piv
    # spec angles (ART_3D 7.4): -60 -> +20 deg, impact at drawing 6, recoil; mapped to a swing about the handle end
    spec = [-56, -63, -65, -50, -24, 4, 20, 11, 17, 14, 15]
    for d, a in enumerate(spec):
        name = 'fx_hammer_%02d' % d
        th = a + 62  # 0 = handle straight up; wind-up stays inside the canvas, impact lands bottom-left
        piv.rotation_euler = (0, math.radians(-th), 0)
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def impact_star():
    if not want('fx_impact_star', 8):
        return
    w, h = 256, 256
    sc = scene(w, h, lights='none', samples=32)
    c = (w / 2 * U, 0, h / 2 * U)
    core = glow_mat('core', '#FFE7A3', 3.0, 1.6, core='#FFFFFF')
    halo = glow_mat('halo', '#FFC21A', 1.6, 2.2)
    starm = glow_mat('star', '#FFD45A', 2.2, 0.6, core='#FFFFFF')
    pc = plane(sc, 'core', 1.0, c, core)
    ph = plane(sc, 'halo', 2.4, (c[0], 0.01, c[2]), halo)
    st = flat_shape(sc, 'spikes', C.star_pts(8, 1.0, 0.32, rot=90), starm, (c[0], -0.01, c[2]), 1.0)
    for d in range(8):
        name = 'fx_impact_star_%02d' % d
        t = d / 7
        s = 0.25 + 0.95 * ease_out(min(1, t * 1.6))
        fade = 1.0 if t < 0.35 else max(0.0, 1 - (t - 0.35) / 0.65)
        pc.scale = (s * 1.2, s * 1.2, 1)
        ph.scale = (s * 2.3, s * 2.3, 1)
        st.scale = (s * 1.05, s * 1.05, 1)
        st.rotation_euler = (math.radians(90), math.radians(8 * t), 0)
        set_opacity(core, fade)
        set_opacity(halo, fade * 0.7)
        set_opacity(starm, fade)
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def paint_splash():
    if not want('fx_paint_splash', 12):
        return
    w, h = 256, 256
    sc = scene(w, h)
    m = C.principled('paint', '#F2F2F2', rough=0.12, coat=0.8, coat_rough=0.04, sss=0.1)
    c = Vector((w / 2 * U, 0, h / 2 * U))
    rnd = random.Random(3)
    drops = []
    for k in range(9):
        a = 2 * math.pi * k / 9 + rnd.uniform(-0.2, 0.2)
        drops.append((a, rnd.uniform(0.75, 1.0), rnd.uniform(0.06, 0.11)))
    blob = C.uv_sphere(sc, 'blob', 0.3, loc=c, mat=m, seg=48, rings=24)
    ds = [C.uv_sphere(sc, 'drop', r, loc=c, mat=m, seg=24, rings=12) for a, dist, r in drops]
    for d in range(12):
        name = 'fx_paint_splash_%02d' % d
        if d < 3:  # anticipation: the drop falls in, stretching
            t = d / 3
            blob.location = c + Vector((0, 0, 0.55 * (1 - t)))
            blob.scale = (0.7, 0.7, 1.25 - 0.2 * t)
            for o in ds:
                o.hide_render = True
        else:  # splat at 3, spread with overshoot, settle
            t = (d - 3) / 8
            sp = 1.0 + 0.12 * math.sin(min(1.0, t * 1.3) * math.pi) if t > 0 else 1.0
            blob.location = c
            blob.scale = (1.7 * sp, 1.0, 1.35 * sp * (0.92 if d == 3 else 1.0))
            for o, (a, dist, r) in zip(ds, drops):
                o.hide_render = False
                rr = (0.45 + dist * 0.55 * ease_out(min(1.0, t * 1.5)))
                o.location = c + Vector((math.cos(a) * rr, 0, math.sin(a) * rr))
                st = 1.0 + 0.8 * (1 - min(1.0, t * 2))
                o.scale = (st, 1, 1 / st)
                o.rotation_euler = (0, -a, 0)
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def trowel_sparkle():
    if not want('fx_trowel_sparkle', 12):
        return
    w, h = 192, 192
    sc = scene(w, h)
    parts = I.b_trowel(sc, yaw=-15)
    root = bpy.data.objects.new('root', None)
    sc.collection.objects.link(root)
    for p_ in parts:
        if p_.parent is None:
            p_.parent = root
        elif p_.parent.parent is None and p_.parent.name.startswith('grp'):
            p_.parent.parent = root
    grp = [o for o in sc.objects if o.name.startswith('grp')][0]
    grp.parent = root
    grp.scale = (0.62, 0.62, 0.62)
    root.location = (w / 2 * U, 0, h / 2 * U)
    gm = glow_mat('spk', '#FFF6D0', 3.0, 0.8, core='#FFFFFF')
    sp = flat_shape(sc, 'spark', star_glint_pts(4, 0.14), gm, (w / 2 * U + 0.42, -0.5, h / 2 * U + 0.48), 0.3)
    for d in range(12):
        name = 'fx_trowel_sparkle_%02d' % d
        ph = 2 * math.pi * d / 12
        root.rotation_euler = (0, math.radians(15 * math.sin(ph)), 0)
        gl = max(0.0, math.cos(ph * 2))  # glints at 0 and 6
        sp.scale = (0.08 + 0.28 * gl, 0.08 + 0.28 * gl, 1)
        set_opacity(gm, 0.25 + 0.75 * gl)
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def coin_spin():
    if not want('fx_coin', 12):
        return
    w, h = 128, 128
    sc = scene(w, h, samples=64)
    parts = I.b_coin(sc, yaw=0.0)
    grp = [o for o in sc.objects if o.name.startswith('grp')][0]
    grp.location = (w / 2 * U, 0, h / 2 * U)
    grp.scale = (0.5, 0.5, 0.5)
    for d in range(12):
        name = 'fx_coin_%02d' % d
        grp.rotation_euler = (math.radians(90), 0, math.radians(-30 * d))
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def star_burst():
    if not want('fx_star_burst', 14):
        return
    w, h = 512, 512
    sc = scene(w, h, lights='none', samples=32)
    c = (w / 2 * U, 0, h / 2 * U)
    ring_m = bpy.data.materials.new('ring')
    ringg = glow_mat('ringg', '#FFE7A3', 2.2, 1.0, core='#FFFFFF')
    # ring = torus with emission + small stars
    ring = C.torus(sc, 'ring', 1.0, 0.05, loc=c, mat=ringg, rot=(90, 0, 0), seg=96, mseg=8)
    ring_em = ring_mat('ringem', '#FFF2C0', 2.5)
    ring.data.materials[0] = ring_em
    flash = glow_mat('flash', '#FFD45A', 2.4, 2.0, core='#FFFFFF')
    pf = plane(sc, 'flash', 2.0, (c[0], 0.02, c[2]), flash)
    stars = []
    sm = C.emission_mat('stars', '#FFE680', 2.2)
    rnd = random.Random(5)
    for k in range(16):
        a = 2 * math.pi * k / 16 + rnd.uniform(-0.1, 0.1)
        o = flat_shape(sc, 'st', C.star_pts(5, 1.0, 0.45, rot=90), sm, c, 0.12)
        stars.append((o, a, rnd.uniform(0.8, 1.15), rnd.uniform(0.7, 1.2)))
    for d in range(14):
        name = 'fx_star_burst_%02d' % d
        t = d / 13
        R = 0.3 + 1.9 * ease_out(t)
        ring.scale = (R, R, R)
        ring.hide_render = t > 0.85
        rs = max(0.0, 1 - t) * 1.0
        set_ring(ring_em, rs)
        pf.scale = (1.6 + 1.4 * t, 1.6 + 1.4 * t, 1)
        set_opacity(flash, max(0.0, 1 - t * 2.2))
        for o, a, sp, sz in stars:
            rr = (0.4 + 1.9 * ease_out(t)) * sp
            o.location = (c[0] + math.cos(a) * rr, -0.01, c[2] + math.sin(a) * rr)
            s = 0.13 * sz * (1 - t ** 3)
            o.scale = (s, s, s)
            o.rotation_euler = (math.radians(90), math.radians(40 * t * sp), 0)
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def confetti():
    for kind, (w, h), n in (('rect', (24, 12), 8), ('round', (16, 16), 8)):
        prefix = 'fx_confetti3d_' + kind
        if not want(prefix, n):
            continue
        sc = scene(w, h, res_scale=6.0, samples=32)
        m = white('paper', rough=0.5, coat=0.15, glow=0.35)
        if kind == 'rect':
            pts = C.rrect_pts(-0.5, -0.25, 0.5, 0.25, 0.04, 2)
        else:
            pts = C.circle_pts(0.5, 32)
        o = C.prism(sc, 'c', C.ccw(pts), -0.03, 0.03, m, m, bevel=0.0)
        piv = bpy.data.objects.new('p', None)
        sc.collection.objects.link(piv)
        o.parent = piv
        piv.location = (w / 2 * U, 0, h / 2 * U)
        s = (w if kind == 'rect' else h) * U * 0.98
        piv.scale = (s, s, s)
        for d in range(n):
            name = '%s_%02d' % (prefix, d)
            a = 2 * math.pi * d / n
            piv.rotation_euler = (math.radians(90) + a, a * 0.5 if kind == 'rect' else a, 0)
            if C.ARGS.wants(name):
                C.render(sc, out(name))


def dust_puff():
    if not want('fx_dust_puff', 10):
        return
    w, h = 192, 128
    sc = scene(w, h, samples=48)
    rnd = random.Random(11)
    puffs = []
    for k in range(7):
        m = C.principled('dust%d' % k, '#F2EEE6', rough=0.9, coat=0.0, sss=0.2, alpha=1.0)
        a = math.radians(rnd.uniform(8, 172))
        puffs.append((C.uv_sphere(sc, 'puff', 1.0, loc=(w / 2 * U, 0, 0.12), mat=m, seg=24, rings=12), m, a, rnd.uniform(0.6, 1.0), rnd.uniform(0.16, 0.26)))
    for d in range(10):
        name = 'fx_dust_puff_%02d' % d
        t = d / 9
        for o, m, a, sp, r in puffs:
            rr = 0.15 + 0.62 * sp * ease_out(t)
            o.location = (w / 2 * U + math.cos(a) * rr * 1.1, 0, 0.14 + math.sin(a) * rr * 0.45)
            s = r * (0.55 + 0.9 * ease_out(t))
            o.scale = (s, s, s * 0.85)
            m.node_tree.nodes['Principled BSDF'].inputs['Alpha'].default_value = max(0.0, 1 - t ** 1.6)
            m.blend_method = 'BLEND'
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def segment_done():
    if not want('fx_segment_done', 14):
        return
    w, h = 512, 512
    sc = scene(w, h, lights='none', samples=32)
    c = (w / 2 * U, 0, h / 2 * U)
    flare = glow_mat('flare', '#BFE8FF', 2.6, 1.6, core='#FFFFFF')
    pf = plane(sc, 'flare', 2.0, c, flare)
    ring_em = ring_mat('ring', '#9FE0FF', 2.4)
    ring = C.torus(sc, 'ring', 1.0, 0.045, loc=c, mat=ring_em, rot=(90, 0, 0), seg=96, mseg=8)
    sm = glow_mat('spk', '#FFF6D0', 3.0, 0.7, core='#FFFFFF')
    rnd = random.Random(9)
    sparks = []
    for k in range(10):
        a = 2 * math.pi * k / 10 + rnd.uniform(-0.2, 0.2)
        o = flat_shape(sc, 'spk', star_glint_pts(4, 0.16), sm, c, 0.2)
        sparks.append((o, a, rnd.uniform(0.7, 1.1)))
    for d in range(14):
        name = 'fx_segment_done_%02d' % d
        t = d / 13
        pf.scale = (1.0 + 2.2 * ease_out(min(1, t * 2)), 1.0 + 2.2 * ease_out(min(1, t * 2)), 1)
        set_opacity(flare, max(0.0, 1 - t * 1.8))
        R = 0.4 + 2.0 * ease_out(t)
        ring.scale = (R, R, R)
        set_ring(ring_em, max(0.0, 1 - t) ** 0.8)
        for o, a, sp in sparks:
            tt = max(0.0, (d - 4) / 9)  # fly at 6
            rr = (0.5 + 1.6 * ease_out(tt)) * sp
            o.location = (c[0] + math.cos(a) * rr, -0.02, c[2] + math.sin(a) * rr)
            s = 0.22 * (1 - tt ** 2) * (0.4 if d < 4 else 1.0)
            o.scale = (s, s, s)
        if C.ARGS.wants(name):
            C.render(sc, out(name))


def lock_shine():
    if not want('fx_lock_shine', 12):
        return
    w, h = 72, 72
    sc = scene(w, h, res_scale=3.0, lights='none', samples=32)
    c = (w / 2 * U, 0, h / 2 * U)
    gm = glow_mat('g', '#FFFFFF', 3.0, 0.9, core='#FFFFFF')
    halo = glow_mat('h', '#FFF6D0', 1.4, 2.4)
    st = flat_shape(sc, 'glint', star_glint_pts(4, 0.12), gm, c, 0.3)
    ph = plane(sc, 'halo', 0.5, (c[0], 0.02, c[2]), halo)
    for d in range(12):
        name = 'fx_lock_shine_%02d' % d
        t = d / 11
        k = math.sin(math.pi * min(1.0, t * 1.15)) if t < 0.87 else 0.0
        s = 0.06 + 0.3 * k
        st.scale = (s, s, s)
        st.rotation_euler = (math.radians(90), math.radians(45 * t), 0)
        ph.scale = (0.2 + 0.5 * k, 0.2 + 0.5 * k, 1)
        set_opacity(gm, k)
        set_opacity(halo, k * 0.8)
        if C.ARGS.wants(name):
            C.render(sc, out(name))


# ------------------------------------------------------------------------------------------------- v4 particle frames
def particles():
    if C.ARGS.wants('fx_chip'):
        sc = scene(32, 24, res_scale=4.5)
        m = white('chip', rough=0.35, coat=0.4)
        o = C.superellipsoid(sc, 'chip', 0.13, 0.08, 0.09, n=4, loc=(0.16, 0, 0.12), mat=m)
        o.rotation_euler = (math.radians(20), math.radians(-25), math.radians(30))
        C.render(sc, out('fx_chip'))
    if C.ARGS.wants('fx_confetti'):
        sc = scene(24, 12, res_scale=4.5)
        m = white('paper', rough=0.5, coat=0.15)
        o = C.prism(sc, 'c', C.ccw(C.rrect_pts(-0.5, -0.25, 0.5, 0.25, 0.04, 2)), -0.03, 0.03, m, m)
        o.scale = (0.235, 0.235, 0.235)
        o.location = (0.12, 0, 0.06)
        o.rotation_euler = (math.radians(70), math.radians(12), 0)
        C.render(sc, out('fx_confetti'))
    if C.ARGS.wants('fx_confetti_round'):
        sc = scene(16, 16, res_scale=4.5)
        m = white('paper', rough=0.5, coat=0.15)
        o = C.prism(sc, 'c', C.ccw(C.circle_pts(0.5, 32)), -0.03, 0.03, m, m)
        o.scale = (0.155, 0.155, 0.155)
        o.location = (0.08, 0, 0.08)
        o.rotation_euler = (math.radians(65), 0, math.radians(20))
        C.render(sc, out('fx_confetti_round'))
    if C.ARGS.wants('fx_dust'):
        sc = scene(96, 96, res_scale=2.0)
        rnd = random.Random(2)
        m = C.principled('dust', '#F2EEE6', rough=0.9, coat=0.0, sss=0.25, alpha=0.85)
        for k in range(5):
            C.uv_sphere(sc, 'p', rnd.uniform(0.16, 0.26), loc=(0.48 + rnd.uniform(-0.18, 0.18), 0, 0.46 + rnd.uniform(-0.12, 0.12)), mat=m, seg=24, rings=12)
        C.render(sc, out('fx_dust'))
    if C.ARGS.wants('fx_ring'):
        sc = scene(128, 128, res_scale=2.0, lights='none', samples=32)
        em = C.emission_mat('r', '#FFFFFF', 1.0)
        C.torus(sc, 'ring', 0.52, 0.055, loc=(0.64, 0, 0.64), mat=em, rot=(90, 0, 0), seg=96, mseg=12)
        g = glow_mat('g', '#FFFFFF', 0.6, 3.0)
        plane(sc, 'halo', 1.25, (0.64, 0.05, 0.64), g)
        C.render(sc, out('fx_ring'))
    if C.ARGS.wants('fx_spark'):
        sc = scene(64, 64, res_scale=4.5, lights='none', samples=32)
        gm = glow_mat('g', '#FFFFFF', 1.0, 0.7)
        flat_shape(sc, 's', star_glint_pts(4, 0.14), gm, (0.32, 0, 0.32), 0.31)
        hm = glow_mat('h', '#FFFFFF', 0.7, 2.5)
        plane(sc, 'h', 0.42, (0.32, 0.02, 0.32), hm)
        C.render(sc, out('fx_spark'))
    if C.ARGS.wants('fx_star'):
        sc = scene(64, 64, res_scale=4.5)
        m = white('star', rough=0.25, coat=0.6, sss=0.05)
        o = C.puffy(sc, 'star', C.ccw(C.star_pts(5, 1.0, 0.5, rot=90)), 0.4, m, bevel=0.16, sub=2)
        o.rotation_euler = (math.radians(90), 0, math.radians(-15))
        o.scale = (0.29, 0.29, 0.29)
        o.location = (0.32, 0, 0.31)
        C.render(sc, out('fx_star'))


if __name__ == '__main__':
    crane_claw()
    hammer()
    impact_star()
    paint_splash()
    trowel_sparkle()
    coin_spin()
    star_burst()
    confetti()
    dust_puff()
    segment_done()
    lock_shine()
    particles()
    print('FX DONE', flush=True)
