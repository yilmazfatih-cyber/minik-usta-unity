"""Shared Blender 4.0.2 helpers for the B1 environment / UI / FX renders (docs: art/3d/blender/env/README.md).

Every family script (blocks.py, board.py, ui.py, icons.py, bg.py, fx.py) imports this module:

    blender -b --factory-startup -noaudio --python art/3d/blender/env/<family>.py -- --out art/3d/out/env/raw [--only a,b] [--quick]

Contract values (camera pitch, lights, colour management, samples) come from art/3d/spec/frames.json -> rigs /
colorManagement (docs/ART_3D.md section 6). Renders are written at 2x the stored size; post_env.py (system python + Pillow)
downsizes with LANCZOS on premultiplied alpha, bleeds the alpha and writes art/3d/out/env/<frame>.png.

Screen-space modelling (rigs A_tile and D_ui): the camera is orthographic with pitch p from straight down and no yaw, so a
world point (x, y, z) lands on screen at (x, y cos p + z sin p). `sy(v, z)` inverts that: the model y of a point that should
appear at screen height v when it sits at height z. A vertical face of height h shows h sin p on screen (the front lip).
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))


# ----------------------------------------------------------------------------------------------------------------- args
class Args:
    def __init__(self):
        a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []

        def opt(name, default=None):
            return a[a.index('--' + name) + 1] if '--' + name in a else default

        self.out = os.path.abspath(opt('out', os.path.join(ROOT, 'art', '3d', 'out', 'env', 'raw')))
        only = opt('only', '')
        self.only = [s for s in only.split(',') if s]
        self.quick = '--quick' in a  # 1/2 resolution, 1/8 samples: pose / layout check
        self.samples_mult = float(opt('samples-mult', '1'))
        self.extra = a

    def wants(self, name):
        if not self.only:
            return True
        return any(name == s or (s.endswith('*') and name.startswith(s[:-1])) for s in self.only)


ARGS = Args()


def frames_contract():
    with open(os.path.join(ROOT, 'art', '3d', 'spec', 'frames.json')) as f:
        return json.load(f)


# --------------------------------------------------------------------------------------------------------------- colour
def lin1(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def lin(h, a=1.0):
    h = h.lstrip('#')
    return (lin1(int(h[0:2], 16) / 255), lin1(int(h[2:4], 16) / 255), lin1(int(h[4:6], 16) / 255), a)


def mix_hex(a, b, t):
    a = a.lstrip('#')
    b = b.lstrip('#')
    ca = [int(a[i:i + 2], 16) for i in (0, 2, 4)]
    cb = [int(b[i:i + 2], 16) for i in (0, 2, 4)]
    return '#' + ''.join('%02X' % round(x + (y - x) * t) for x, y in zip(ca, cb))


def scale_hex(a, k):
    a = a.lstrip('#')
    return '#' + ''.join('%02X' % max(0, min(255, round(int(a[i:i + 2], 16) * k))) for i in (0, 2, 4))


# ---------------------------------------------------------------------------------------------------------------- scene
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    return sc


def setup_render(sc, w, h, samples, transparent=True, bounces=6):
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    s = samples * ARGS.samples_mult
    if ARGS.quick:
        s = max(4, s / 8)
        w, h = max(8, w // 2), max(8, h // 2)
    sc.cycles.samples = max(4, int(s))
    sc.cycles.use_adaptive_sampling = True
    sc.cycles.adaptive_threshold = 0.02
    sc.cycles.use_denoising = False  # apt Blender 4.0.2 has no OpenImageDenoise (ART_3D 6.3)
    sc.cycles.sample_clamp_indirect = 3.0
    sc.cycles.blur_glossy = 1.0
    sc.cycles.max_bounces = bounces
    sc.cycles.diffuse_bounces = 3
    sc.cycles.glossy_bounces = 3
    sc.cycles.transmission_bounces = 4
    sc.cycles.transparent_max_bounces = 8
    sc.render.film_transparent = transparent
    sc.render.resolution_x = int(w)
    sc.render.resolution_y = int(h)
    sc.render.resolution_percentage = 100
    sc.render.pixel_aspect_x = 1
    sc.render.pixel_aspect_y = 1
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    sc.view_settings.exposure = 0.0
    sc.view_settings.gamma = 1.0
    sc.display_settings.display_device = 'sRGB'
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '8'
    sc.render.image_settings.compression = 15
    sc.render.use_persistent_data = True


def render(sc, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print('RENDERED', os.path.basename(path), sc.render.resolution_x, sc.render.resolution_y, flush=True)


def world(sc, color, strength):
    w = bpy.data.worlds.new('world')
    sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = lin(color)
    bg.inputs['Strength'].default_value = strength
    return w


def world_glossy_studio(sc, color, strength, top='#FFF6E8', horizon='#8A94B8', gloss_strength=1.0):
    """World lighting: flat `color` x strength for diffuse, a warm-top / cool-horizon studio gradient for glossy rays
    only (coat and metal read as shiny; diffuse light stays the contract value). B1k README section 3."""
    w = bpy.data.worlds.new('world')
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    nodes, links = nt.nodes, nt.links
    bg = nodes['Background']
    bg.inputs['Color'].default_value = lin(color)
    bg.inputs['Strength'].default_value = strength
    out = nodes['World Output']
    tc = nodes.new('ShaderNodeTexCoord')
    sep = nodes.new('ShaderNodeSeparateXYZ')
    links.new(tc.outputs['Generated'], sep.inputs[0])
    # Generated z of the world = direction z (0..1 mapped from -1..1)
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.45
    ramp.color_ramp.elements[0].color = lin(horizon)
    ramp.color_ramp.elements[1].position = 0.9
    ramp.color_ramp.elements[1].color = lin(top)
    links.new(sep.outputs['Z'], ramp.inputs['Fac'])
    bg2 = nodes.new('ShaderNodeBackground')
    bg2.inputs['Strength'].default_value = gloss_strength
    links.new(ramp.outputs['Color'], bg2.inputs['Color'])
    lp = nodes.new('ShaderNodeLightPath')
    mix = nodes.new('ShaderNodeMixShader')
    links.new(lp.outputs['Is Glossy Ray'], mix.inputs['Fac'])
    links.new(bg.outputs[0], mix.inputs[1])
    links.new(bg2.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], out.inputs['Surface'])
    return w


def sun(sc, name, color, strength, rot_deg, angle_deg=6.0):
    ld = bpy.data.lights.new(name, 'SUN')
    ld.color = lin(color)[:3]
    ld.energy = strength
    ld.angle = math.radians(angle_deg)
    o = bpy.data.objects.new(name, ld)
    sc.collection.objects.link(o)
    o.rotation_euler = [math.radians(a) for a in rot_deg]
    return o


def area(sc, name, color, power, loc, aim, size=2.0):
    ld = bpy.data.lights.new(name, 'AREA')
    ld.color = lin(color)[:3]
    ld.energy = power
    ld.size = size
    o = bpy.data.objects.new(name, ld)
    sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return o


def point_from_dir(rot_deg):
    """Unit vector a SUN with rotation_euler `rot_deg` shines FROM (opposite of its -Z axis)."""
    m = Matrix.Rotation(math.radians(rot_deg[2]), 4, 'Z') @ Matrix.Rotation(math.radians(rot_deg[1]), 4, 'Y') @ Matrix.Rotation(math.radians(rot_deg[0]), 4, 'X')
    return (m @ Vector((0, 0, 1, 0))).to_3d().normalized()


# rigs (frames.json -> rigs; rim of A/D as SUN so every tile / 9-slice centre is position independent)
def rig_A(sc, rim=0.9):
    sun(sc, 'key', '#FFF1DC', 2.9, (-48, 0, 32), 6)
    sun(sc, 'fill', '#E4EAFF', 1.1, (60, 0, -15), 30)
    sun(sc, 'rim', '#FFFFFF', rim, (-62, 0, 160), 12)
    world_glossy_studio(sc, '#B4B8CC', 0.45, gloss_strength=0.55)


def rig_D(sc):
    # key: pitch-only direction from the top of the screen (x-uniform 9-slice centres), plus a weak left lift
    # an ortho camera + a SUN give every flat face ONE half-vector: a key near the mirror direction of the 20 deg view
    # (20 deg toward +y) would turn whole plates white. Key from slightly in front (pitch only: x-uniform), a weak
    # upper-left modelling light, a cool fill from below the view.
    sun(sc, 'key', '#FFFFFF', 2.4, (12, 0, 0), 10)
    sun(sc, 'key2', '#FFF3E0', 0.7, (-50, 0, 40), 10)
    sun(sc, 'fill', '#C8D8FF', 0.6, (70, 0, 0), 30)
    # glossy studio: bright top, DARK horizon -> the steeply seen front walls (lips) do not mirror a grey sky
    world_glossy_studio(sc, '#FFFFFF', 0.35, top='#FFFFFF', horizon='#3E4363', gloss_strength=0.8)


def rig_C(sc, rim=1.3):
    sun(sc, 'key', '#FFE6C4', 2.6, (50, 0, -35), 8)
    area(sc, 'fill', '#9CC2FF', 180, (3.0, -3.2, 1.6), (0, 0, 0.3), size=3.0)
    area(sc, 'rim', '#F2E8FF', 360 * rim, (2.0, 3.0, 3.2), (0, 0, 0.4), size=1.6)
    area(sc, 'rim2', '#FFE2B8', 160 * rim, (-2.6, 2.6, 2.2), (0, 0, 0.4), size=1.6)
    world_glossy_studio(sc, '#5A6A96', 0.30, gloss_strength=0.9)


# --------------------------------------------------------------------------------------------------------------- camera
def camera_pitch(sc, pitch_deg, cx, cv, ortho_w, res_w, res_h, dist=30.0):
    """Ortho camera, pitch from straight down, looking at screen point (cx, cv) (on the z = 0 plane); ortho_w = width of
    the view in BU. Returns the camera object."""
    p = math.radians(pitch_deg)
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'
    cd.ortho_scale = ortho_w if res_w >= res_h else ortho_w * res_h / res_w
    cd.clip_start = 0.01
    cd.clip_end = 200
    cam = bpy.data.objects.new('cam', cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.rotation_euler = (p, 0, 0)
    py = cv / math.cos(p)
    cam.location = (cx, py - dist * math.sin(p), dist * math.cos(p))
    return cam


def aim_pitch(cam, pitch_deg, cx, cv, dist=30.0):
    p = math.radians(pitch_deg)
    py = cv / math.cos(p)
    cam.location = (cx, py - dist * math.sin(p), dist * math.cos(p))


def camera_look(sc, loc, target, ortho_w, res_w, res_h, persp=False, lens=50):
    cd = bpy.data.cameras.new('cam')
    if persp:
        cd.type = 'PERSP'
        cd.lens = lens
    else:
        cd.type = 'ORTHO'
        cd.ortho_scale = ortho_w if res_w >= res_h else ortho_w * res_h / res_w
    cd.clip_start = 0.01
    cd.clip_end = 500
    cam = bpy.data.objects.new('cam', cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    return cam


def sy(v, z, pitch_deg):
    p = math.radians(pitch_deg)
    return (v - z * math.sin(p)) / math.cos(p)


# ------------------------------------------------------------------------------------------------------------ materials
def principled(name, base, rough=0.3, coat=0.4, coat_rough=0.06, spec=0.5, sss=0.0, metallic=0.0, emission=None,
               emission_strength=0.0, alpha=1.0, sheen=0.0, base_rgba=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = base_rgba if base_rgba is not None else lin(base)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metallic
    b.inputs['Coat Weight'].default_value = coat
    b.inputs['Coat Roughness'].default_value = coat_rough
    b.inputs['Specular IOR Level'].default_value = spec
    b.inputs['Subsurface Weight'].default_value = sss
    b.inputs['Subsurface Radius'].default_value = (1.0, 0.45, 0.25)
    b.inputs['Subsurface Scale'].default_value = 0.05
    b.inputs['Sheen Weight'].default_value = sheen
    if emission:
        b.inputs['Emission Color'].default_value = lin(emission)
        b.inputs['Emission Strength'].default_value = emission_strength
    if alpha < 1.0:
        b.inputs['Alpha'].default_value = alpha
        m.blend_method = 'BLEND'
    return m


def bsdf(m):
    return m.node_tree.nodes['Principled BSDF']


def gradient_input(m, stops, axis='Y', lo=0.0, hi=1.0, target='Base Color', space='Object', interp='EASE'):
    """Drives a Principled input with a colour ramp along an object / generated coordinate axis (lo..hi -> 0..1)."""
    nt = m.node_tree
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs[space], sep.inputs[0])
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = lo
    mr.inputs['From Max'].default_value = hi
    mr.clamp = True
    nt.links.new(sep.outputs[axis], mr.inputs['Value'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = interp
    els = ramp.color_ramp.elements
    while len(els) > 1:
        els.remove(els[-1])
    els[0].position = stops[0][0]
    els[0].color = lin(stops[0][1])
    for pos, col in stops[1:]:
        e = els.new(pos)
        e.color = lin(col)
    nt.links.new(mr.outputs['Result'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], bsdf(m).inputs[target])
    return ramp


def emission_mat(name, color, strength=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = lin(color)
    em.inputs['Strength'].default_value = strength
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    return m


def holdout_mat():
    m = bpy.data.materials.new('holdout')
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    h = nt.nodes.new('ShaderNodeHoldout')
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(h.outputs[0], out.inputs['Surface'])
    return m


# ----------------------------------------------------------------------------------------------------------- geometry
def link(sc, obj):
    sc.collection.objects.link(obj)
    return obj


def mesh_obj(sc, name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    sc.collection.objects.link(o)
    return o


def rrect_pts(x0, y0, x1, y1, r, seg=10):
    """Counter-clockwise rounded rectangle (r clamped), points in 2D."""
    r = max(0.0, min(r, (x1 - x0) / 2 - 1e-6, (y1 - y0) / 2 - 1e-6))
    pts = []
    corners = [(x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)]
    for cx, cy, a0 in corners:
        if r <= 1e-6:
            pts.append((cx, cy))
            continue
        for k in range(seg + 1):
            a = math.radians(a0 + 90 * k / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    out = []
    for p in pts:
        if not out or (abs(out[-1][0] - p[0]) > 1e-7 or abs(out[-1][1] - p[1]) > 1e-7):
            out.append(p)
    if abs(out[0][0] - out[-1][0]) < 1e-7 and abs(out[0][1] - out[-1][1]) < 1e-7:
        out.pop()
    return out


def prism(sc, name, pts2d, z0, z1, mat_top=None, mat_side=None, bevel=0.0, bevel_seg=4, profile=0.6, angle=35,
          smooth=True, yfun=None, harden=False):
    """Extruded polygon (pts in model x, y) from z0 to z1. mat_top on faces with normal z > 0.5, mat_side elsewhere.
    yfun(y, z) remaps y per vertex (screen-space modelling: lets the top and the bottom share screen rows)."""
    bm = bmesh.new()
    bot = [bm.verts.new((x, yfun(y, z0) if yfun else y, z0)) for x, y in pts2d]
    top = [bm.verts.new((x, yfun(y, z1) if yfun else y, z1)) for x, y in pts2d]
    n = len(pts2d)
    bm.faces.new(list(reversed(bot)))
    bm.faces.new(top)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bm.normal_update()
    o = mesh_obj(sc, name, bm)
    finish(o, mat_top, mat_side, bevel, bevel_seg, profile, angle, smooth)
    return o


def finish(o, mat_top=None, mat_side=None, bevel=0.0, bevel_seg=4, profile=0.6, angle=35, smooth=True):
    me = o.data
    if mat_top:
        me.materials.append(mat_top)
    if mat_side:
        me.materials.append(mat_side)
    if mat_top and mat_side:
        for p in me.polygons:
            p.material_index = 0 if p.normal.z > 0.5 else 1
    if bevel > 0:
        bv = o.modifiers.new('bevel', 'BEVEL')
        bv.width = bevel
        bv.segments = bevel_seg
        bv.profile = profile
        bv.limit_method = 'ANGLE'
        bv.angle_limit = math.radians(angle)
        bv.harden_normals = False
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
        me.use_auto_smooth = True  # Blender 4.0 API (removed in 4.1)
        me.auto_smooth_angle = math.radians(angle)
    return o


def ring_prism(sc, name, outer, inner, z_top, z_floor, z_bottom, mat_top=None, mat_side=None, mat_floor=None,
               bevel=0.0, bevel_seg=4, profile=0.6, angle=35, yfun=None):
    """A frame: ring top face between outer and inner loops at z_top, outer wall to z_bottom, inner wall down to z_floor
    and a floor face (inner loop) at z_floor. outer and inner must have the same point count (rrect_pts with equal seg)."""
    assert len(outer) == len(inner)
    Y = (lambda y, z: yfun(y, z)) if yfun else (lambda y, z: y)
    bm = bmesh.new()
    n = len(outer)
    ob = [bm.verts.new((x, Y(y, z_bottom), z_bottom)) for x, y in outer]
    ot = [bm.verts.new((x, Y(y, z_top), z_top)) for x, y in outer]
    it = [bm.verts.new((x, Y(y, z_top), z_top)) for x, y in inner]
    fl = [bm.verts.new((x, Y(y, z_floor), z_floor)) for x, y in inner]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ob[j], ob[i], fl[i], fl[j]))  # bottom ring (a full bottom face would close an open hole)
        bm.faces.new((ob[i], ob[j], ot[j], ot[i]))
        bm.faces.new((ot[i], ot[j], it[j], it[i]))
        bm.faces.new((it[i], it[j], fl[j], fl[i]))
    floor = bm.faces.new(fl)
    bm.normal_update()
    o = mesh_obj(sc, name, bm)
    me = o.data
    mats = [m for m in (mat_top, mat_side, mat_floor) if m]
    for m in mats:
        me.materials.append(m)
    if mat_top and mat_side:
        fidx = len(me.polygons) - 1
        for p in me.polygons:
            if p.index == fidx and mat_floor:
                p.material_index = 2
            else:
                p.material_index = 0 if p.normal.z > 0.5 else 1
    if bevel > 0:
        bv = o.modifiers.new('bevel', 'BEVEL')
        bv.width = bevel
        bv.segments = bevel_seg
        bv.profile = profile
        bv.limit_method = 'ANGLE'
        bv.angle_limit = math.radians(angle)
    for p in me.polygons:
        p.use_smooth = True
    me.use_auto_smooth = True
    me.auto_smooth_angle = math.radians(angle)
    return o


def superellipsoid(sc, name, rx, ry, rz, n=3.0, loc=(0, 0, 0), mat=None, sub=5):
    """Spherified cube pushed to |x|^n + |y|^n + |z|^n = 1 (no poles), then scaled."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges, cuts=sub, use_grid_fill=True)
    for v in bm.verts:
        d = v.co.normalized()
        s = (abs(d.x) ** n + abs(d.y) ** n + abs(d.z) ** n) ** (-1.0 / n)
        v.co = Vector((d.x * s * rx, d.y * s * ry, d.z * s * rz))
    o = mesh_obj(sc, name, bm)
    o.location = loc
    if mat:
        o.data.materials.append(mat)
    for p in o.data.polygons:
        p.use_smooth = True
    sd = o.modifiers.new('sub', 'SUBSURF')
    sd.levels = 1
    sd.render_levels = 2
    return o


def uv_sphere(sc, name, r, loc=(0, 0, 0), mat=None, seg=32, rings=16, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    o = mesh_obj(sc, name, bm)
    o.location = loc
    o.scale = scale
    if mat:
        o.data.materials.append(mat)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def cylinder(sc, name, r, depth, loc=(0, 0, 0), mat=None, seg=48, bevel=0.0, bevel_seg=3, rot=(0, 0, 0), r2=None,
             mat_side=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2,
                          depth=depth)
    o = mesh_obj(sc, name, bm)
    o.location = loc
    o.rotation_euler = [math.radians(a) for a in rot]
    finish(o, mat, mat_side, bevel, bevel_seg, 0.6, 35, True)
    if mat and not mat_side:
        pass
    return o


def torus(sc, name, R, r, loc=(0, 0, 0), mat=None, rot=(0, 0, 0), seg=48, mseg=16):
    bm = bmesh.new()
    verts = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring = []
        for j in range(mseg):
            b = 2 * math.pi * j / mseg
            ring.append(bm.verts.new(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b))))
        verts.append(ring)
    for i in range(seg):
        for j in range(mseg):
            a = verts[i][j]
            b = verts[(i + 1) % seg][j]
            c = verts[(i + 1) % seg][(j + 1) % mseg]
            d = verts[i][(j + 1) % mseg]
            bm.faces.new((a, b, c, d))
    bm.normal_update()
    o = mesh_obj(sc, name, bm)
    o.location = loc
    o.rotation_euler = [math.radians(x) for x in rot]
    if mat:
        o.data.materials.append(mat)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def puffy(sc, name, pts2d, depth, mat, bevel=None, loc=(0, 0, 0), rot=(0, 0, 0), sub=2, seg=6, mat_side=None):
    """Inflated 2D silhouette (icon style): extrude +-depth/2, round bevel on every edge, subdivision."""
    b = bevel if bevel is not None else depth * 0.45
    o = prism(sc, name, pts2d, -depth / 2, depth / 2, mat, mat_side, bevel=b, bevel_seg=seg, profile=0.5, angle=60)
    if sub:
        sd = o.modifiers.new('sub', 'SUBSURF')
        sd.levels = sub
        sd.render_levels = sub
    o.location = loc
    o.rotation_euler = [math.radians(a) for a in rot]
    return o


def star_pts(n=5, r_out=1.0, r_in=0.5, rot=90, round_k=0.0, seg=1):
    pts = []
    for i in range(2 * n):
        r = r_out if i % 2 == 0 else r_in
        a = math.radians(rot + 180 * i / n)
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def heart_pts(k=1.0, seg=64):
    pts = []
    for i in range(seg):
        t = 2 * math.pi * i / seg
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((x / 17 * k, (y + 2.5) / 17 * k))
    return list(reversed(pts)) if signed_area(pts) < 0 else pts


def circle_pts(r, seg=48, cx=0, cy=0):
    return [(cx + r * math.cos(2 * math.pi * i / seg), cy + r * math.sin(2 * math.pi * i / seg)) for i in range(seg)]


def signed_area(pts):
    return 0.5 * sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))


def ccw(pts):
    return pts if signed_area(pts) > 0 else list(reversed(pts))


def apply_all(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def shadow_catcher(sc, size=40, z=0.0, loc=(0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=size / 2)
    o = mesh_obj(sc, 'catcher', bm)
    o.location = (loc[0], loc[1], z)
    o.is_shadow_catcher = True
    o.data.materials.append(principled('catcher', '#FFFFFF', rough=0.8, coat=0.0))
    return o


def clear_objects(keep=('cam',), keep_lights=True):
    for o in list(bpy.data.objects):
        if o.name in keep or (keep_lights and o.type == 'LIGHT') or o.type == 'CAMERA':
            continue
        bpy.data.objects.remove(o, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)


def write_meta(name, meta):
    os.makedirs(ARGS.out, exist_ok=True)
    with open(os.path.join(ARGS.out, name + '.meta.json'), 'w') as f:
        json.dump(meta, f, indent=1, sort_keys=True)


# ------------------------------------------------------------------------------------- screen-space builders (A_tile, D_ui)
PX = 1 / 120.0  # 1 BU = one 120 design px cell


class Screen:
    """Screen-space modelling for a pitch-only ortho camera: points are given in DESIGN px of the frame (origin bottom-left,
    v up), heights in design px of model z. A prism's walls are vertical; its top face lands exactly on the given screen
    points and its front wall shows z_top * sin(pitch) design px under the top face (the lip)."""

    def __init__(self, sc, pitch_deg, frame_w, frame_h, res_scale, samples, transparent=True):
        self.sc = sc
        self.pitch = pitch_deg
        self.p = math.radians(pitch_deg)
        self.w = frame_w
        self.h = frame_h
        self.res_w = max(1, round(frame_w * res_scale))
        self.res_h = max(1, round(frame_h * res_scale))
        setup_render(sc, self.res_w, self.res_h, samples, transparent)
        self.cam = camera_pitch(sc, pitch_deg, frame_w / 2 * PX, frame_h / 2 * PX, frame_w * PX, self.res_w, self.res_h)

    def lip_z(self, lip_px):
        return lip_px / math.sin(self.p)

    def y(self, v_px, z_px):
        return (v_px * PX - z_px * PX * math.sin(self.p)) / math.cos(self.p)

    def prism(self, name, pts, z_top, z_bot=0.0, mat_top=None, mat_side=None, bevel_px=0.0, seg=4, profile=0.6, angle=35, wn=True):
        zt = z_top * PX
        zb = z_bot * PX
        ys = [self.y(v, z_top) for _, v in pts]
        pts_m = [(x * PX, yy) for (x, _), yy in zip(pts, ys)]
        o = prism(self.sc, name, ccw(pts_m), zb, zt, mat_top, mat_side, bevel=bevel_px * PX, bevel_seg=seg, profile=profile, angle=angle)
        if wn:
            m = o.modifiers.new('wn', 'WEIGHTED_NORMAL')
            m.mode = 'FACE_AREA'
            m.weight = 100
            m.keep_sharp = True
        return o

    def ring(self, name, outer, inner, z_top, z_bot=0.0, z_floor=None, mat_top=None, mat_side=None, mat_floor=None, bevel_px=0.0, seg=4,
             profile=0.6, open_hole=False):
        zt = z_top * PX
        zb = z_bot * PX
        zf = (z_floor if z_floor is not None else z_bot) * PX
        o_m = [(x * PX, self.y(v, z_top)) for x, v in outer]
        i_m = [(x * PX, self.y(v, z_top)) for x, v in inner]
        o = ring_prism(self.sc, name, o_m, i_m, zt, zf, zb, mat_top, mat_side, mat_floor, bevel=bevel_px * PX, bevel_seg=seg, profile=profile)
        if open_hole:
            bm = bmesh.new()
            bm.from_mesh(o.data)
            bm.faces.ensure_lookup_table()
            bmesh.ops.delete(bm, geom=[bm.faces[-1]], context='FACES_ONLY')
            bm.to_mesh(o.data)
            bm.free()
        m = o.modifiers.new('wn', 'WEIGHTED_NORMAL')
        m.mode = 'FACE_AREA'
        m.weight = 100
        m.keep_sharp = True
        return o

    def at(self, x_px, v_px, z_px):
        """Model location of the point shown at screen (x, v) at height z."""
        return (x_px * PX, self.y(v_px, z_px), z_px * PX)

    def frame(self, cx_px, cv_px, w_px, h_px, res_scale):
        """Re-aim the camera on another frame of the same scene (several frames per scene)."""
        self.res_w = max(1, round(w_px * res_scale))
        self.res_h = max(1, round(h_px * res_scale))
        q = 2 if ARGS.quick else 1
        self.sc.render.resolution_x = max(1, self.res_w // q)
        self.sc.render.resolution_y = max(1, self.res_h // q)
        self.cam.data.ortho_scale = (w_px if w_px >= h_px else h_px) * PX
        aim_pitch(self.cam, self.pitch, cx_px * PX, cv_px * PX)


def stripes_input(m, a_hex, b_hex, period_bu, angle_deg=45, target='Base Color', soft=0.06):
    """Hazard stripes (object space, 45 deg) into a Principled input."""
    nt = m.node_tree
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], sep.inputs[0])
    a = math.radians(angle_deg)
    mx = nt.nodes.new('ShaderNodeMath')
    mx.operation = 'MULTIPLY'
    mx.inputs[1].default_value = math.cos(a) / period_bu
    nt.links.new(sep.outputs['X'], mx.inputs[0])
    my = nt.nodes.new('ShaderNodeMath')
    my.operation = 'MULTIPLY'
    my.inputs[1].default_value = math.sin(a) / period_bu
    nt.links.new(sep.outputs['Y'], my.inputs[0])
    mz = nt.nodes.new('ShaderNodeMath')
    mz.operation = 'MULTIPLY'
    mz.inputs[1].default_value = math.sin(a) / period_bu
    nt.links.new(sep.outputs['Z'], mz.inputs[0])
    s1 = nt.nodes.new('ShaderNodeMath')
    s1.operation = 'ADD'
    nt.links.new(mx.outputs[0], s1.inputs[0])
    nt.links.new(my.outputs[0], s1.inputs[1])
    s2 = nt.nodes.new('ShaderNodeMath')
    s2.operation = 'ADD'
    nt.links.new(s1.outputs[0], s2.inputs[0])
    nt.links.new(mz.outputs[0], s2.inputs[1])
    fr = nt.nodes.new('ShaderNodeMath')
    fr.operation = 'FRACT'
    nt.links.new(s2.outputs[0], fr.inputs[0])
    # triangle wave -> soft step
    tri = nt.nodes.new('ShaderNodeMath')
    tri.operation = 'PINGPONG'
    tri.inputs[1].default_value = 0.5
    nt.links.new(fr.outputs[0], tri.inputs[0])
    ss = nt.nodes.new('ShaderNodeMapRange')
    ss.interpolation_type = 'SMOOTHSTEP'
    ss.inputs['From Min'].default_value = 0.25 - soft
    ss.inputs['From Max'].default_value = 0.25 + soft
    nt.links.new(tri.outputs[0], ss.inputs['Value'])
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['A'].default_value = lin(a_hex)
    mix.inputs['B'].default_value = lin(b_hex)
    nt.links.new(ss.outputs['Result'], mix.inputs['Factor'])
    nt.links.new(mix.outputs['Result'], bsdf(m).inputs[target])
    return mix


def noise_color(m, base_hex, var=0.08, scale=40.0, detail=4.0, target='Base Color', bump=0.0, wave=None):
    """Base colour * (1 +- var) from a noise (or wave bands along x when wave = (scale, distortion)); optional bump."""
    nt = m.node_tree
    tc = nt.nodes.new('ShaderNodeTexCoord')
    if wave:
        tex = nt.nodes.new('ShaderNodeTexWave')
        tex.wave_type = 'BANDS'
        tex.bands_direction = 'Y'
        tex.inputs['Scale'].default_value = wave[0]
        tex.inputs['Distortion'].default_value = wave[1]
        tex.inputs['Detail'].default_value = 3.0
        fac = tex.outputs['Fac']
    else:
        tex = nt.nodes.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = scale
        tex.inputs['Detail'].default_value = detail
        fac = tex.outputs['Fac']
    nt.links.new(tc.outputs['Object'], tex.inputs['Vector'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.3
    ramp.color_ramp.elements[0].color = lin(scale_hex(base_hex, 1 - var))
    ramp.color_ramp.elements[1].position = 0.7
    ramp.color_ramp.elements[1].color = lin(scale_hex(base_hex, 1 + var))
    nt.links.new(fac, ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], bsdf(m).inputs[target])
    if bump > 0:
        b = nt.nodes.new('ShaderNodeBump')
        b.inputs['Strength'].default_value = bump
        b.inputs['Distance'].default_value = 0.01
        nt.links.new(fac, b.inputs['Height'])
        nt.links.new(b.outputs['Normal'], bsdf(m).inputs['Normal'])
    return ramp


def curve_tube(sc, name, pts, radius, mat, closed=False, res=12, taper=None):
    """Poly-bezier tube through 3D points (auto handles), round profile."""
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = radius
    cu.bevel_resolution = res // 2
    cu.use_fill_caps = True
    sp = cu.splines.new('BEZIER')
    sp.bezier_points.add(len(pts) - 1)
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = p
        bp.handle_left_type = 'AUTO'
        bp.handle_right_type = 'AUTO'
    sp.use_cyclic_u = closed
    o = bpy.data.objects.new(name, cu)
    sc.collection.objects.link(o)
    cu.materials.append(mat)
    return o


# ------------------------------------------------------------------------------------------------- prop (rig C) helpers
def prop_camera(sc, res_w, res_h, pitch_down=28.0):
    """Rig C camera: ortho, 28 deg down from horizontal (rotation 62, 0, 0), aimed later by fit_camera()."""
    cd = bpy.data.cameras.new('cam')
    cd.type = 'ORTHO'
    cd.ortho_scale = 2.0
    cd.clip_start = 0.01
    cd.clip_end = 500
    cam = bpy.data.objects.new('cam', cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.rotation_euler = (math.radians(90 - pitch_down), 0, 0)
    cam['res'] = (res_w, res_h)
    return cam


def fit_camera(sc, cam, fill=0.86, objs=None, anchor=None, dist=30.0):
    """Centres the ortho camera on the projected bounding box of the meshes and scales it so the object fills `fill`
    of the frame's limiting side. anchor = (ax, ay): instead of centring, put the box's (ax, ay) corner fraction there."""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    R = cam.matrix_world.to_3x3().normalized()
    right = R @ Vector((1, 0, 0))
    up = R @ Vector((0, 1, 0))
    fwd = R @ Vector((0, 0, -1))
    xs, ys = [], []
    for o in (objs or [o for o in sc.objects if o.type in ('MESH', 'CURVE')]):
        if o.get('nofit') or getattr(o, 'is_shadow_catcher', False):
            continue
        ev = o.evaluated_get(dg)
        try:
            me = ev.to_mesh()
        except RuntimeError:
            continue
        mw = ev.matrix_world
        for v in me.vertices:
            p = mw @ v.co
            xs.append(p.dot(right))
            ys.append(p.dot(up))
        ev.to_mesh_clear()
    w, h = cam['res']
    ex, ey = max(xs) - min(xs), max(ys) - min(ys)
    aspect = w / h
    # ortho_scale spans the larger render side
    if w >= h:
        scale = max(ex, ey * aspect) / fill
    else:
        scale = max(ex / aspect, ey) / fill
    cam.data.ortho_scale = scale
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    if anchor is not None:
        vw = scale if w >= h else scale * aspect
        vh = scale / aspect if w >= h else scale
        cx = min(xs) + ex * anchor[0] - (anchor[0] - 0.5) * vw if False else cx
    centre = right * cx + up * cy
    cam.location = centre - fwd * dist
    return scale, (min(xs), max(xs), min(ys), max(ys))


def stand(o, yaw=-20.0, tilt=0.0):
    """A silhouette extruded along z made to stand facing the camera (-y), then yawed for the 3/4 look."""
    o.rotation_euler = (math.radians(90 + tilt), 0, math.radians(yaw))
    return o


def group(sc, objs, loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
    root = bpy.data.objects.new('grp', None)
    sc.collection.objects.link(root)
    for o in objs:
        o.parent = root
    root.location = loc
    root.rotation_euler = [math.radians(a) for a in rot]
    root.scale = (scale, scale, scale)
    return root


def mat_gold(name='gold', light=False):
    return principled(name, '#FFC93A' if not light else '#FFD95A', rough=0.2, coat=0.35, coat_rough=0.05, metallic=0.75, spec=0.6)


def mat_wood(name='wood', hex_='#B8763E'):
    m = principled(name, hex_, rough=0.45, coat=0.35, coat_rough=0.1, spec=0.4)
    noise_color(m, hex_, var=0.06, wave=(6.0, 3.0), bump=0.03)
    return m


def mat_candy(name, hex_, rough=0.22, coat=0.6, sss=0.08):
    return principled(name, hex_, rough=rough, coat=coat, coat_rough=0.05, sss=sss, spec=0.5)


def mat_chrome(name='chrome'):
    return principled(name, '#E3E8EE', rough=0.14, coat=0.0, metallic=1.0)
