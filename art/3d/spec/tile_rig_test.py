"""Rig A_tile test (docs/ART_3D.md sections 2.2 and 6.1) — Blender 4.0.2 headless:

    blender -b --factory-startup -noaudio --python art/3d/spec/tile_rig_test.py -- --out <dir> [--px 180] [--samples 64]

Builds 8 one-cell blocks (W Y G R O C B P), an O L-piece and a B vertical pair with the A_tile geometry rules (pitch-only
orthographic camera at 35 deg from straight down, 24 design px front lip, row pitch 1 / cos 35, cell union by exact
boolean, angle-limited bevel, stud per cell) and renders one strip with transparent film. tile_rig_measure.py then
checks the top-face colour against the base hex (dE00) and the lip / stud geometry. Test scene only: the B1 renderer
builds the 16 masks x 8 colours + corner fills with the same functions.
"""
import math
import os
import sys

import bpy

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.abspath(argv[argv.index('--out') + 1]) if '--out' in argv else os.getcwd()
PX = int(argv[argv.index('--px') + 1]) if '--px' in argv else 180
SAMPLES = int(argv[argv.index('--samples') + 1]) if '--samples' in argv else 64

PITCH = math.radians(35)
LIP = 24 / 120  # screen fraction of a cell
T = LIP / math.sin(PITCH)  # slab thickness (BU)
C35 = math.cos(PITCH)
PALETTE = {'W': '#764012', 'Y': '#FFE23E', 'G': '#44D088', 'R': '#CF404E', 'O': '#EC8D20', 'C': '#C8CFDA', 'B': '#2984DE', 'P': '#5D3AAE'}
ROUGH = {'W': 0.45, 'C': 0.42, 'R': 0.32, 'P': 0.32}
SSS = {'Y': 0.12, 'G': 0.12, 'O': 0.10, 'R': 0.08, 'B': 0.08, 'C': 0.06, 'P': 0.06, 'W': 0.04}


def lin(h):
    h = h.lstrip('#')
    f = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(f(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def mat(code):
    m = bpy.data.materials.new('blk_' + code)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = lin(PALETTE[code])
    b.inputs['Roughness'].default_value = ROUGH.get(code, 0.30)
    b.inputs['Coat Weight'].default_value = {'W': 0.0, 'C': 0.25}.get(code, 0.40)
    b.inputs['Coat Roughness'].default_value = 0.06
    b.inputs['Subsurface Weight'].default_value = SSS[code]
    b.inputs['Specular IOR Level'].default_value = 0.2 if code == 'W' else 0.5  # dark warm W: less white specular
    b.inputs['Subsurface Radius'].default_value = (1.0, 0.45, 0.25)
    b.inputs['Subsurface Scale'].default_value = 0.05
    return m


def cell_box(i, j, has_down):
    """Model-space box of screen cell (i, j): x [i, i+1]; y from the lip / row rule; z [0, T]."""
    y_lo = (j - LIP) / C35 if has_down else j / C35
    y_hi = (j + 1 - LIP) / C35
    bpy.ops.mesh.primitive_cube_add(size=1, location=(i + 0.5, (y_lo + y_hi) / 2, T / 2))
    o = bpy.context.active_object
    o.scale = (1.0, y_hi - y_lo, T)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return o


def piece(cells, code, ox=0):
    cs = set(cells)
    objs = [cell_box(ox + i, j, (i, j - 1) in cs) for (i, j) in cells]
    # union WITHOUT booleans (EXACT boolean + harden normals left black faces in the first test): join the cell boxes,
    # weld shared vertices, delete the coincident face pairs between neighbouring cells -> one manifold slab
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    base = bpy.context.active_object
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(base.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    seen = {}
    for fc in bm.faces:
        key = tuple(sorted(v.index for v in fc.verts))
        seen.setdefault(key, []).append(fc)
    dup = [fc for fs in seen.values() if len(fs) > 1 for fc in fs]
    bmesh.ops.delete(bm, geom=dup, context='FACES_ONLY')
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1), verts=bm.verts, edges=bm.edges)
    bm.to_mesh(base.data)
    bm.free()
    bv = base.modifiers.new('bevel', 'BEVEL')
    bv.width = 0.10
    bv.segments = 5
    bv.profile = 0.7
    bv.limit_method = 'ANGLE'
    bv.angle_limit = math.radians(30)
    m = mat(code)
    base.data.materials.append(m)
    for p in base.data.polygons:
        p.use_smooth = True
    base.data.shade_smooth()
    base.data.use_auto_smooth = True  # Blender 4.0 API (removed in 4.1): keeps boolean seams flat
    base.data.auto_smooth_angle = math.radians(30)
    # stud per cell, centred on the cell's visible top-face span
    for (i, j) in cells:
        has_down = (i, j - 1) in cs
        v_lo = j if has_down else j + LIP
        v_mid = (v_lo + j + 1) / 2
        y = (v_mid - T * math.sin(PITCH)) / C35
        bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=0.225, depth=0.12, location=(ox + i + 0.5, y, T + 0.06))
        s = bpy.context.active_object
        b2 = s.modifiers.new('bevel', 'BEVEL')
        b2.width = 0.03
        b2.segments = 3
        s.data.materials.append(m)
        s.data.shade_smooth()
    return base


scene = bpy.context.scene
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
codes = list(PALETTE)
for k, code in enumerate(codes):
    piece([(0, 1)], code, ox=k * 2 - 0)
piece([(0, 0), (0, 1), (1, 0)], 'O', ox=16)
piece([(0, 0), (0, 1)], 'B', ox=19)

# camera: orthographic, pitch 35 deg from straight down; screen v=0 at the strip bottom
W_CELLS, H_CELLS = 21, 3
cd = bpy.data.cameras.new('cam')
cd.type = 'ORTHO'
cd.ortho_scale = W_CELLS
cam = bpy.data.objects.new('cam', cd)
scene.collection.objects.link(cam)
scene.camera = cam
cam.rotation_euler = (PITCH, 0, 0)
# camera axis: forward (0, sin p, -cos p), up (0, cos p, sin p); centre the view on screen point (W/2, H/2)
d = 20.0
cx, vc = W_CELLS / 2, H_CELLS / 2
# point at screen v: y*cos + z*sin = v -> choose z = 0 plane point y = v / cos
py = vc / C35
cam.location = (cx, py - d * math.sin(PITCH), d * math.cos(PITCH))
scene.render.resolution_x = W_CELLS * PX
scene.render.resolution_y = H_CELLS * PX


def light(name, kind, color, energy, rot=None, loc=None, size=None, angle=None, aim=None):
    ld = bpy.data.lights.new(name, kind)
    ld.color = lin(color)[:3]
    ld.energy = energy
    if size:
        ld.size = size
    if angle:
        ld.angle = math.radians(angle)
    o = bpy.data.objects.new(name, ld)
    scene.collection.objects.link(o)
    if rot:
        o.rotation_euler = [math.radians(a) for a in rot]
    if loc:
        o.location = loc
    if aim:
        from mathutils import Vector
        o.rotation_euler = (Vector(aim) - o.location).to_track_quat('-Z', 'Y').to_euler()
    return o


KEY = float(argv[argv.index('--key') + 1]) if '--key' in argv else 2.9
FILL = float(argv[argv.index('--fill') + 1]) if '--fill' in argv else 1.1
light('key', 'SUN', '#FFF1DC', KEY, rot=(-48, 0, 32), angle=6)
light('fill', 'SUN', '#E4EAFF', FILL, rot=(60, 0, -15), angle=30)  # from the viewer side: lifts the front lip
light('rim', 'AREA', '#FFFFFF', 80, loc=(cx - 2.0, 3.5 + 2, 2.5), size=2.0, aim=(cx, 1.5, 0))
world = bpy.data.worlds.new('w')
scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = lin('#B4B8CC')
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.45

scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = False
scene.cycles.sample_clamp_indirect = 3.0
scene.cycles.blur_glossy = 1.0
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.render.image_settings.color_mode = 'RGBA'
os.makedirs(OUT, exist_ok=True)
scene.render.filepath = os.path.join(OUT, 'tile_rig_test.png')
bpy.ops.render.render(write_still=True)
print('RENDERED tile_rig_test', W_CELLS * PX, H_CELLS * PX)
