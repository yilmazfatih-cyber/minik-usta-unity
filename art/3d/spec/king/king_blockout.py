"""Usta Kral 3D blockout (D-100, D-101) — Blender 4.0.2, bpy only, headless:

    blender -b --factory-startup -noaudio --python art/3d/spec/king/king_blockout.py -- --out <dir> [--size 512] [--samples 96]

Builds the proportion blockout of art/3d/spec/king.md from primitives (1 BU = 1 head; total height 2.5 BU), applies
the material library and character light rig of docs/ART_3D.md section 6 (rig B), renders front / three-quarter /
side / back views with a transparent film and a shadow catcher. This is the reference the B1k model must match in
silhouette and proportion; it is NOT the final model (no sculpt, no rig, no face topology).
"""
import math
import os
import sys

import bpy

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


def arg(name, default):
    return argv[argv.index(name) + 1] if name in argv else default


OUT = os.path.abspath(arg('--out', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'render')))
SIZE = int(arg('--size', '512'))
SAMPLES = int(arg('--samples', '96'))
VT = arg('--view-transform', 'Standard')
LOOK = arg('--look', 'None')
VIEWS = arg('--views', 'front,34,side,back').split(',')
KEY = float(arg('--key', '2.6'))
SS = 2  # supersampling: render SIZE * SS, downsample later (Pillow LANCZOS) — ART_3D section 6.4


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_lin(h):
    h = h.lstrip('#')
    return tuple(srgb_to_lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


# ---- material library (ART_3D section 2.1; king.md section 4) --------------------------------------------------
MATS = {
    #  name        base hex   rough  coat  coatR  sss   sheen  metal
    'hat': ('#FFD43B', 0.28, 0.6, 0.06, 0.0, 0.0, 0.0),
    'gold': ('#FFD45A', 0.22, 0.0, 0.0, 0.0, 0.0, 1.0),
    'enamel': ('#2984DE', 0.18, 0.8, 0.04, 0.0, 0.0, 0.0),
    'suit': ('#EC8D20', 0.72, 0.0, 0.0, 0.0, 0.4, 0.0),
    'cuff': ('#F6AC55', 0.72, 0.0, 0.0, 0.0, 0.4, 0.0),
    'shirt': ('#FFF3D6', 0.7, 0.0, 0.0, 0.0, 0.3, 0.0),
    'sash': ('#2984DE', 0.35, 0.0, 0.0, 0.0, 0.8, 0.0),
    'leather': ('#8A4E1C', 0.5, 0.2, 0.2, 0.0, 0.0, 0.0),
    'boot': ('#5A3418', 0.45, 0.3, 0.15, 0.0, 0.0, 0.0),
    'sole': ('#F2E3C6', 0.6, 0.0, 0.0, 0.0, 0.0, 0.0),
    'skin': ('#E9A877', 0.45, 0.0, 0.0, 0.12, 0.0, 0.0),
    'hair': ('#7A4222', 0.6, 0.0, 0.0, 0.0, 0.5, 0.0),
    'eyewhite': ('#FFFFFF', 0.12, 0.5, 0.02, 0.0, 0.0, 0.0),
    'iris': ('#B57836', 0.1, 0.8, 0.02, 0.0, 0.0, 0.0),
    'pupil': ('#1E0F06', 0.1, 0.8, 0.02, 0.0, 0.0, 0.0),
    'mouth': ('#6A1F2A', 0.5, 0.0, 0.0, 0.0, 0.0, 0.0),
    'wood': ('#B8763E', 0.55, 0.0, 0.0, 0.0, 0.0, 0.0),
}


def make_mat(name):
    base, rough, coat, coat_r, sss, sheen, metal = MATS[name]
    m = bpy.data.materials.new('m_' + name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = hex_lin(base)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    b.inputs['Coat Weight'].default_value = coat
    b.inputs['Coat Roughness'].default_value = coat_r
    b.inputs['Subsurface Weight'].default_value = sss
    b.inputs['Subsurface Radius'].default_value = (1.0, 0.45, 0.25)
    b.inputs['Subsurface Scale'].default_value = 0.05
    b.inputs['Sheen Weight'].default_value = sheen
    return m


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def obj_finish(o, mat, bevel=0.0, segs=6, smooth=True, subsurf=0):
    if bevel > 0:
        md = o.modifiers.new('bevel', 'BEVEL')
        md.width = bevel
        md.segments = segs
        md.limit_method = 'NONE'
    if subsurf:
        md = o.modifiers.new('sub', 'SUBSURF')
        md.levels = subsurf
        md.render_levels = subsurf
    if smooth:
        for p in o.data.polygons:
            p.use_smooth = True
    o.data.materials.append(MAT[mat])
    o.parent = ROOT
    return o


def box(name, mat, loc, size, bevel=0.0, rot=(0, 0, 0), segs=6):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj_finish(o, mat, bevel, segs)


def ball(name, mat, loc, size, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=1, location=loc,
                                         rotation=[math.radians(a) for a in rot])
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj_finish(o, mat)


def cyl(name, mat, loc, r, depth, rot=(0, 0, 0), bevel=0.0, verts=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc,
                                        rotation=[math.radians(a) for a in rot])
    o = bpy.context.active_object
    o.name = name
    return obj_finish(o, mat, bevel, 4)


def capsule(name, mat, a, b, r):
    """Limb segment between points a and b (sphere-swept cylinder)."""
    ax, ay, az = a
    bx, by, bz = b
    dx, dy, dz = bx - ax, by - ay, bz - az
    L = math.sqrt(dx * dx + dy * dy + dz * dz)
    mid = ((ax + bx) / 2, (ay + by) / 2, (az + bz) / 2)
    o = cyl(name, mat, mid, r, L)
    o.rotation_mode = 'QUATERNION'
    from mathutils import Vector
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(Vector((dx, dy, dz)).normalized())
    ball(name + '_a', mat, a, (r, r, r))
    ball(name + '_b', mat, b, (r, r, r))
    return o


# ---- build ----------------------------------------------------------------------------------------------------
reset()
scene = bpy.context.scene
MAT = {k: make_mat(k) for k in MATS}
bpy.ops.object.empty_add(location=(0, 0, 0))
ROOT = bpy.context.active_object
ROOT.name = 'usta_kral_root'

# boots, soles, legs, hems (king.md section 2: legs 0.18-0.58, boots 0-0.22)
for sx in (-1, 1):
    x = 0.215 * sx
    box('boot', 'boot', (x, -0.04, 0.115), (0.40, 0.50, 0.17), bevel=0.07)
    box('sole', 'sole', (x, -0.04, 0.03), (0.42, 0.52, 0.06), bevel=0.025)
    cyl('leg', 'suit', (x, 0, 0.38), 0.13, 0.42, bevel=0.02)
    cyl('hem', 'cuff', (x, 0, 0.24), 0.145, 0.09, bevel=0.03)

# torso: rounded 'brick' (shoulders 1.48, bottom 0.48), belly slightly forward
box('torso', 'suit', (0, 0, 0.96), (1.08, 0.74, 1.00), bevel=0.26, segs=8)
box('belly', 'suit', (0, -0.06, 0.82), (1.04, 0.70, 0.56), bevel=0.26, segs=8)
box('vneck', 'shirt', (0, -0.33, 1.38), (0.30, 0.10, 0.22), bevel=0.05, rot=(0, 45, 0))
box('belt', 'leather', (0, 0, 0.70), (1.12, 0.80, 0.15), bevel=0.06)
box('buckle', 'gold', (0, -0.41, 0.70), (0.17, 0.05, 0.15), bevel=0.03)
box('pouch', 'leather', (-0.50, -0.20, 0.60), (0.22, 0.20, 0.24), bevel=0.05)
# royal sash: front and back slabs over the right shoulder (viewer left) to the left hip
box('sash_f', 'sash', (0.02, -0.395, 1.02), (0.17, 0.04, 1.02), bevel=0.015, rot=(0, -42, 0))
box('sash_b', 'sash', (0.02, 0.395, 1.02), (0.17, 0.04, 1.02), bevel=0.015, rot=(0, 42, 0))
for i, t in enumerate((-0.28, 0.0, 0.28)):
    cyl('pin', 'gold', (0.02 + t * math.sin(math.radians(42)) * 1.0, -0.43, 1.02 - t * math.cos(math.radians(42))),
        0.055, 0.03, rot=(90, 0, 0), bevel=0.01)

# arms (idle pose of king_poses.svg: right hand holds the golden trowel upright)
for sx in (-1, 1):
    sh = (0.56 * sx, 0, 1.30)
    el = (0.66 * sx, -0.04, 0.98)
    wr = (0.62 * sx, -0.16, 0.74)
    capsule('uparm', 'suit', sh, el, 0.115)
    cyl('cuff', 'cuff', el, 0.135, 0.10, rot=(0, 18 * sx, 0), bevel=0.03)
    capsule('forearm', 'skin', el, wr, 0.09)
    ball('hand', 'skin', (wr[0], wr[1] - 0.02, wr[2] - 0.08), (0.12, 0.10, 0.13))
    ball('thumb', 'skin', (wr[0] - 0.08 * sx, wr[1] - 0.08, wr[2] - 0.03), (0.05, 0.05, 0.06))
# golden trowel in the right hand (viewer right = +x)
cyl('trowel_handle', 'wood', (0.62, -0.20, 0.70), 0.035, 0.26, bevel=0.01)
cyl('trowel_ferrule', 'gold', (0.62, -0.20, 0.86), 0.045, 0.06, bevel=0.01)
bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.16, radius2=0.0, depth=0.42, location=(0.62, -0.20, 1.10))
blade = bpy.context.active_object
blade.scale = (1.0, 0.18, 1.0)
blade.rotation_euler = (0, 0, math.radians(45))
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
obj_finish(blade, 'gold', bevel=0.01, segs=2, smooth=False)

# head: squircle (rounded cube), ears, hair tufts, boxy chin beard, face features
HZ = 1.88
box('head', 'skin', (0, 0, HZ), (0.90, 0.80, 0.80), bevel=0.32, segs=10)
for sx in (-1, 1):
    ball('ear', 'skin', (0.46 * sx, 0.02, 1.86), (0.07, 0.05, 0.11))
    ball('hair', 'hair', (0.40 * sx, 0.04, 2.02), (0.09, 0.22, 0.12))
box('beard', 'hair', (0, 0.02, 1.62), (0.92, 0.78, 0.36), bevel=0.16, segs=8)
box('beard_l', 'hair', (-0.42, 0.0, 1.80), (0.10, 0.60, 0.40), bevel=0.05)
box('beard_r', 'hair', (0.42, 0.0, 1.80), (0.10, 0.60, 0.40), bevel=0.05)
box('mouth_skin', 'skin', (0, -0.30, 1.74), (0.40, 0.20, 0.16), bevel=0.07)
ball('mouth', 'mouth', (0, -0.395, 1.73), (0.11, 0.03, 0.045))
box('nose', 'skin', (0, -0.42, 1.83), (0.13, 0.10, 0.10), bevel=0.045)
for sx in (-1, 1):
    ex = 0.175 * sx
    ball('eye', 'eyewhite', (ex, -0.36, 1.95), (0.095, 0.06, 0.115))
    ball('iris', 'iris', (ex, -0.405, 1.94), (0.068, 0.02, 0.068))
    ball('pupil', 'pupil', (ex, -0.42, 1.94), (0.034, 0.012, 0.034))
    ball('glint', 'eyewhite', (ex - 0.025, -0.428, 1.97), (0.017, 0.006, 0.017))
    box('brow', 'hair', (ex, -0.40, 2.12), (0.24, 0.07, 0.07), bevel=0.03, rot=(0, -6 * sx, 0))

# hard hat: dome + ridge + brim + front visor + crown emblem plate (enamel) with gold crown (3 points with stud tips)
bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=32, radius=1, location=(0, 0, 2.10))
dome = bpy.context.active_object
dome.scale = (0.52, 0.50, 0.42)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='DESELECT')
bpy.ops.object.mode_set(mode='OBJECT')
for v in dome.data.vertices:
    v.select = v.co.z < -0.001
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.delete(type='VERT')
bpy.ops.object.mode_set(mode='OBJECT')
obj_finish(dome, 'hat')
box('ridge', 'hat', (0, 0, 2.44), (0.12, 0.80, 0.10), bevel=0.05)
cyl('brim', 'hat', (0, -0.04, 2.10), 0.60, 0.045, bevel=0.02, verts=64).scale = (1.0, 0.92, 1.0)
ball('visor', 'hat', (0, -0.50, 2.09), (0.34, 0.16, 0.035))
box('emblem', 'enamel', (0, -0.455, 2.30), (0.28, 0.04, 0.20), bevel=0.04, rot=(-24, 0, 0))
box('crown_band', 'gold', (0, -0.485, 2.255), (0.18, 0.03, 0.05), bevel=0.012, rot=(-24, 0, 0))
for (x, z) in ((-0.07, 2.31), (0.0, 2.335), (0.07, 2.31)):
    ball('crown_stud', 'gold', (x, -0.47 + (z - 2.30) * 0.45, z), (0.026, 0.026, 0.026))

# ---- rig B: character light rig (ART_3D section 6.2) ---------------------------------------------------------
world = bpy.data.worlds.new('world')
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes['Background']
bg.inputs['Color'].default_value = hex_lin('#5A6A96')
bg.inputs['Strength'].default_value = 0.25


def light(name, kind, color, energy, loc=None, rot=None, size=None, angle=None):
    ld = bpy.data.lights.new(name, kind)
    ld.color = hex_lin(color)[:3]
    ld.energy = energy
    if size is not None:
        ld.size = size
    if angle is not None:
        ld.angle = math.radians(angle)
    o = bpy.data.objects.new(name, ld)
    scene.collection.objects.link(o)
    if loc:
        o.location = loc
    if rot:
        o.rotation_euler = [math.radians(a) for a in rot]
    return o


def aim(o, target):
    from mathutils import Vector
    d = Vector(target) - o.location
    o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


light('key_sun', 'SUN', '#FFE6C4', KEY, rot=(50, 0, -35), angle=8)
fill = light('fill_area', 'AREA', '#9CC2FF', 180, loc=(3.0, -3.2, 1.6), size=3.0)
aim(fill, (0, 0, 1.3))
rim = light('rim_area', 'AREA', '#F2E8FF', 360, loc=(2.0, 3.0, 3.2), size=1.6)
aim(rim, (0, 0, 1.9))

# shadow catcher ground
bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
ground = bpy.context.active_object
ground.is_shadow_catcher = True

# orthographic camera, 10 degrees down (rig B)
cd = bpy.data.cameras.new('cam')
cd.type = 'ORTHO'
cd.ortho_scale = 3.0
cam = bpy.data.objects.new('cam', cd)
scene.collection.objects.link(cam)
scene.camera = cam
tilt = 10
cam.rotation_euler = (math.radians(90 - tilt), 0, 0)
cam.location = (0, -12 * math.cos(math.radians(tilt)), 1.30 + 12 * math.sin(math.radians(tilt)))

scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = False
scene.cycles.sample_clamp_indirect = 3.0
scene.cycles.blur_glossy = 1.0
scene.render.film_transparent = True
scene.render.resolution_x = SIZE * SS
scene.render.resolution_y = SIZE * SS
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = VT
scene.view_settings.look = LOOK
scene.view_settings.exposure = 0.0

os.makedirs(OUT, exist_ok=True)
for view, yaw in (('front', 0), ('34', -32), ('side', -90), ('back', 180)):
    if view not in VIEWS:
        continue
    ROOT.rotation_euler = (0, 0, math.radians(yaw))
    scene.render.filepath = os.path.join(OUT, f'king_blockout_{view}_{VT.lower()}.png')
    bpy.ops.render.render(write_still=True)
    print('RENDERED', view)
