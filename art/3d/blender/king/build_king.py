"""Usta Kral — procedural 3D model, part rig, materials and render rig (package B1k; D-100, D-101).

Blender 4.0.2, bpy + bmesh + mathutils only; no external assets, no generative AI. Source of truth for the design:
art/3d/spec/king.md (proportions section 2, palette section 4, face section 5, hard hat section 6, outfit section 7,
trowel section 8) and art/3d/spec/frames.json (rigs.B_char, colorManagement).

Importable (animate_king.py does `import build_king`) and runnable:

    blender -b --factory-startup -noaudio --python art/3d/blender/king/build_king.py -- --out <dir> [--px 160] [--samples 48]

renders a turnaround (front / three-quarter / side / back) of the rest pose K0 to <dir>/king_turn_<view>.png.

Model: rigid parts on a joint hierarchy (empties; "part-based hierarchy" of the B1k brief): view > root > legs, body >
head > hat, arms > elbow > wrist > hand parts, trowel. Face: eyeballs whose iris is shaded in the space of a gaze empty
(rotating the empty moves the iris), skin eyelid shells (blink, happy, soft), brick brows, 11 mouth shapes as projected
decals (cavity + teeth + tongue), cheek blush value. Units: 1 BU = 1 head; total height 2.5 BU; the character faces -Y.
Arm and hand naming is by SCREEN side: arm_r = screen right (+X, holds the Golden Trowel), arm_l = screen left (free hand).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree

# ---------------------------------------------------------------------------------------------------------------
# colour helpers and material library (king.md section 4; Principled values)
# ---------------------------------------------------------------------------------------------------------------


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_lin(h, a=1.0):
    h = h.lstrip('#')
    return tuple(srgb_to_lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (a,)


#        name        base       rough coat coatR sss  sheen metal spec
MATS = {
    'hat': ('#FFD43B', 0.28, 0.60, 0.06, 0.0, 0.0, 0.0, 0.5),
    'gold': ('#FFD45A', 0.24, 0.00, 0.00, 0.0, 0.0, 1.0, 0.5),
    'enamel': ('#2984DE', 0.18, 0.80, 0.04, 0.0, 0.0, 0.0, 0.5),
    'suit': ('#D0621A', 0.62, 0.00, 0.00, 0.0, 0.15, 0.0, 0.4),
    'cuff': ('#F6AC55', 0.62, 0.00, 0.00, 0.0, 0.15, 0.0, 0.4),
    'shirt': ('#FFF3D6', 0.70, 0.00, 0.00, 0.0, 0.10, 0.0, 0.35),
    'sash': ('#2984DE', 0.32, 0.15, 0.10, 0.0, 0.25, 0.0, 0.5),
    'leather': ('#8A4E1C', 0.50, 0.20, 0.20, 0.0, 0.0, 0.0, 0.5),
    'boot': ('#5A3418', 0.45, 0.30, 0.15, 0.0, 0.0, 0.0, 0.5),
    'sole': ('#F2E3C6', 0.60, 0.00, 0.00, 0.0, 0.0, 0.0, 0.4),
    'skin': ('#F4AF86', 0.45, 0.00, 0.00, 0.10, 0.0, 0.0, 0.45),
    'hair': ('#7A4222', 0.55, 0.00, 0.00, 0.0, 0.12, 0.0, 0.45),
    'brow': ('#5A2E14', 0.50, 0.00, 0.00, 0.0, 0.10, 0.0, 0.4),
    'lash': ('#2E1406', 0.50, 0.00, 0.00, 0.0, 0.0, 0.0, 0.3),
    'mouth': ('#5A1622', 0.60, 0.00, 0.00, 0.0, 0.0, 0.0, 0.25),
    'smile': ('#4A2210', 0.60, 0.00, 0.00, 0.0, 0.0, 0.0, 0.25),
    'teeth': ('#FFFDF6', 0.30, 0.30, 0.10, 0.0, 0.0, 0.0, 0.5),
    'tongue': ('#FF7A7A', 0.45, 0.00, 0.00, 0.0, 0.0, 0.0, 0.4),
    'wood': ('#B8763E', 0.55, 0.00, 0.00, 0.0, 0.0, 0.0, 0.4),
    'pencil': ('#FFD43B', 0.40, 0.20, 0.10, 0.0, 0.0, 0.0, 0.5),
    'red': ('#D8413A', 0.35, 0.40, 0.08, 0.0, 0.0, 0.0, 0.5),
    'zip': ('#C46A12', 0.60, 0.00, 0.00, 0.0, 0.0, 0.0, 0.4),
    'ink': ('#11375D', 0.40, 0.00, 0.00, 0.0, 0.0, 0.0, 0.4),
}


def principled(m):
    return m.node_tree.nodes['Principled BSDF']


def make_mat(name, spec=None):
    base, rough, coat, coat_r, sss, sheen, metal, specular = spec or MATS[name]
    m = bpy.data.materials.new('m_' + name)
    m.use_nodes = True
    b = principled(m)
    b.inputs['Base Color'].default_value = hex_lin(base)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    b.inputs['Coat Weight'].default_value = coat
    b.inputs['Coat Roughness'].default_value = coat_r
    b.inputs['Subsurface Weight'].default_value = sss
    b.inputs['Subsurface Radius'].default_value = (1.0, 0.45, 0.25)
    b.inputs['Subsurface Scale'].default_value = 0.05
    b.inputs['Sheen Weight'].default_value = sheen
    b.inputs['Sheen Roughness'].default_value = 0.5
    b.inputs['Sheen Tint'].default_value = tuple(min(1.0, 0.35 + c) for c in hex_lin(base)[:3]) + (1.0,)
    b.inputs['Specular IOR Level'].default_value = specular
    return m


def hair_bump(m, scale=34.0, strength=0.22, streak=None):
    """Hair / beard / brows: irregular strand clumps (noise stretched along Z) -> bump, plus optional light streaks.
    Noise (not regular wave bands) so the beard does not read as corduroy at sprite size."""
    nt = m.node_tree
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (scale, scale, scale * 0.16)
    nz = nt.nodes.new('ShaderNodeTexNoise')
    nz.inputs['Scale'].default_value = 1.0
    nz.inputs['Detail'].default_value = 3.0
    nz.inputs['Roughness'].default_value = 0.55
    nz.inputs['Distortion'].default_value = 0.3
    bp = nt.nodes.new('ShaderNodeBump')
    bp.inputs['Strength'].default_value = strength
    bp.inputs['Distance'].default_value = 0.01
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    nt.links.new(mp.outputs['Vector'], nz.inputs['Vector'])
    nt.links.new(nz.outputs['Fac'], bp.inputs['Height'])
    nt.links.new(bp.outputs['Normal'], principled(m).inputs['Normal'])
    if streak:
        lo, hi = streak
        ramp = nt.nodes.new('ShaderNodeValToRGB')
        cr = ramp.color_ramp
        cr.elements[0].position, cr.elements[0].color = 0.36, hex_lin(lo)
        cr.elements[1].position, cr.elements[1].color = 0.74, hex_lin(hi)
        nt.links.new(nz.outputs['Fac'], ramp.inputs['Fac'])
        nt.links.new(ramp.outputs['Color'], principled(m).inputs['Base Color'])


def emission_mat(name, color='#FFFFFF', strength=1.6):
    m = bpy.data.materials.new('m_' + name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = hex_lin(color)
    em.inputs['Strength'].default_value = strength
    nt.links.new(em.outputs['Emission'], out.inputs['Surface'])
    return m


def eye_mat(name, gaze_obj):
    """Sclera + amber iris + pupil, shaded in the gaze empty's space: radial distance from its -Y axis."""
    m = bpy.data.materials.new('m_' + name)
    m.use_nodes = True
    nt = m.node_tree
    b = principled(m)
    b.inputs['Roughness'].default_value = 0.30
    b.inputs['Coat Weight'].default_value = 0.10
    b.inputs['Coat Roughness'].default_value = 0.08
    b.inputs['Specular IOR Level'].default_value = 0.3
    tc = nt.nodes.new('ShaderNodeTexCoord')
    tc.object = gaze_obj
    nrm = nt.nodes.new('ShaderNodeVectorMath')
    nrm.operation = 'NORMALIZE'
    nt.links.new(tc.outputs['Object'], nrm.inputs[0])
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(nrm.outputs['Vector'], sep.inputs['Vector'])
    flat = nt.nodes.new('ShaderNodeVectorMath')
    flat.operation = 'MULTIPLY'
    flat.inputs[1].default_value = (1.0, 0.0, 1.0)
    nt.links.new(nrm.outputs['Vector'], flat.inputs[0])
    ln = nt.nodes.new('ShaderNodeVectorMath')
    ln.operation = 'LENGTH'
    nt.links.new(flat.outputs['Vector'], ln.inputs[0])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    cr = ramp.color_ramp
    cr.interpolation = 'LINEAR'
    # T1: iris 1.2x (boundary 0.655 -> 0.785 of the visible radius): less white ring, no 'spectacles' read
    stops = [(0.0, '#160A04'), (0.31, '#1E0F06'), (0.345, '#6E3C10'), (0.48, '#B5732E'), (0.60, '#E8AC5E'),
             (0.70, '#C88A3E'), (0.75, '#5A3010'), (0.785, '#FFFDF8'), (0.94, '#FBF7F0'), (1.0, '#EFE9E0')]
    cr.elements[0].position, cr.elements[0].color = stops[0][0], hex_lin(stops[0][1])
    cr.elements[1].position, cr.elements[1].color = stops[-1][0], hex_lin(stops[-1][1])
    for pos, col in stops[1:-1]:
        e = cr.elements.new(pos)
        e.color = hex_lin(col)
    nt.links.new(ln.outputs['Value'], ramp.inputs['Fac'])
    front = nt.nodes.new('ShaderNodeMath')
    front.operation = 'LESS_THAN'
    front.inputs[1].default_value = 0.0
    nt.links.new(sep.outputs['Y'], front.inputs[0])
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['A'].default_value = hex_lin('#F4F7FB')
    nt.links.new(front.outputs['Value'], mix.inputs['Factor'])
    nt.links.new(ramp.outputs['Color'], mix.inputs['B'])
    nt.links.new(mix.outputs['Result'], b.inputs['Base Color'])
    return m


def cheek_mat():
    """Skin with a blush mix; value node 'blush' (0..1) is set per drawing; facing falloff keeps the edge soft."""
    m = make_mat('cheek', MATS['skin'])
    nt = m.node_tree
    b = principled(m)
    val = nt.nodes.new('ShaderNodeValue')
    val.name = 'blush'
    val.outputs[0].default_value = 0.25
    lw = nt.nodes.new('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.5
    inv = nt.nodes.new('ShaderNodeMath')
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(lw.outputs['Facing'], inv.inputs[1])
    pw = nt.nodes.new('ShaderNodeMath')
    pw.operation = 'POWER'
    pw.inputs[1].default_value = 1.6
    nt.links.new(inv.outputs['Value'], pw.inputs[0])
    mul = nt.nodes.new('ShaderNodeMath')
    mul.operation = 'MULTIPLY'
    nt.links.new(pw.outputs['Value'], mul.inputs[0])
    nt.links.new(val.outputs['Value'], mul.inputs[1])
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['A'].default_value = hex_lin(MATS['skin'][0])
    mix.inputs['B'].default_value = hex_lin('#E8735A')
    nt.links.new(mul.outputs['Value'], mix.inputs['Factor'])
    nt.links.new(mix.outputs['Result'], b.inputs['Base Color'])
    return m


# ---------------------------------------------------------------------------------------------------------------
# geometry helpers
# ---------------------------------------------------------------------------------------------------------------


def se_bmesh(r, n=3.0, cuts=14, deform=None):
    """Superellipsoid |x|^n + |y|^n + |z|^n = 1 scaled by radii r, from a spherified subdivided cube (no poles)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    for v in bm.verts:
        x, y, z = v.co
        sx = x * math.sqrt(max(0.0, 1 - y * y / 2 - z * z / 2 + y * y * z * z / 3))
        sy = y * math.sqrt(max(0.0, 1 - z * z / 2 - x * x / 2 + z * z * x * x / 3))
        sz = z * math.sqrt(max(0.0, 1 - x * x / 2 - y * y / 2 + x * x * y * y / 3))
        d = Vector((sx, sy, sz))
        s = (abs(d.x) ** n + abs(d.y) ** n + abs(d.z) ** n) ** (1.0 / n)
        p = d / s if s > 0 else d
        p = Vector((p.x * r[0], p.y * r[1], p.z * r[2]))
        v.co = deform(p) if deform else p
    return bm


def bm_to_obj(name, bm, mat, loc=(0, 0, 0), smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    COLL.objects.link(o)
    o.location = loc
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    if mat is not None:
        me.materials.append(MAT[mat] if isinstance(mat, str) else mat)
    return o


def se(name, mat, loc, r, n=3.0, cuts=14, rot=(0, 0, 0), deform=None):
    o = bm_to_obj(name, se_bmesh(r, n, cuts, deform), mat, loc)
    o.rotation_euler = [math.radians(a) for a in rot]
    return o


def bisect_keep(bm, co, no, keep='inner'):
    """Cut along the plane (co, no); keep='inner' deletes the side the normal points to."""
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, clear_outer=(keep == 'inner'),
                           clear_inner=(keep == 'outer'), dist=1e-6)


def shell(name, src_bm_fn, planes, offset, thickness, mat, bevel=0.006):
    """Band cut from a base surface by half-planes, pushed out along normals, solidified (sash, belt, trims)."""
    bm = src_bm_fn()
    for co, no in planes:
        bisect_keep(bm, Vector(co), Vector(no), 'inner')
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * offset
    o = bm_to_obj(name, bm, mat)
    md = o.modifiers.new('solid', 'SOLIDIFY')
    md.thickness = thickness
    md.offset = 1.0
    md.use_even_offset = True
    if bevel:
        bv = o.modifiers.new('bevel', 'BEVEL')
        bv.width = bevel
        bv.segments = 2
        bv.limit_method = 'ANGLE'
        bv.angle_limit = math.radians(35)
    return o


def mesh_bvh(objs):
    """World-space BVH over the evaluated meshes of objs (for decal projection and surface placement)."""
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    for o in objs:
        oe = o.evaluated_get(dg)
        me = oe.to_mesh()
        tmp = bmesh.new()
        tmp.from_mesh(me)
        tmp.transform(o.matrix_world)
        me2 = bpy.data.meshes.new('tmp')
        tmp.to_mesh(me2)
        tmp.free()
        bm.from_mesh(me2)
        bpy.data.meshes.remove(me2)
        oe.to_mesh_clear()
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    return tree


def surface_hit(bvh, x, z, y0=-3.0, direction=(0, 1, 0)):
    loc, nrm, _i, _d = bvh.ray_cast(Vector((x, y0, z)), Vector(direction))
    return loc, nrm


def poly_bmesh(pts, subdiv=2):
    """Flat filled polygon in the XZ plane (pts = [(x, z)]), triangulated and subdivided for projection."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, 0.0, z)) for x, z in pts]
    f = bm.faces.new(vs)
    bmesh.ops.triangulate(bm, faces=[f], quad_method='BEAUTY', ngon_method='EAR_CLIP')
    if subdiv:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=subdiv, use_grid_fill=False)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
    return bm


def decal(name, pts, bvh, mat, offset=0.004, thickness=0.004, subdiv=2, solid=True):
    """Polygon projected along +Y onto the BVH surface (front decal), raised by offset, optionally solidified."""
    bm = poly_bmesh(pts, subdiv)
    for v in bm.verts:
        hit, _n = surface_hit(bvh, v.co.x, v.co.z)
        v.co.y = (hit.y if hit else 0.0) - offset
    for f in bm.faces:
        if f.normal.y > 0:
            f.normal_flip()
    bm.normal_update()
    o = bm_to_obj(name, bm, mat)
    if solid:
        md = o.modifiers.new('solid', 'SOLIDIFY')
        md.thickness = thickness
        md.offset = 1.0
    return o


def orient_to(o, point, normal, up=(0, 0, 1), spin=0.0):
    """Place o at point with its local -Y along the outward surface normal (local Z kept close to `up`)."""
    n = Vector(normal).normalized()
    yax = -n
    zax = Vector(up) - Vector(up).dot(yax) * yax
    zax.normalize()
    xax = yax.cross(zax)
    m = Matrix((xax, yax, zax)).transposed().to_4x4()
    if spin:
        m = m @ Matrix.Rotation(math.radians(spin), 4, 'Y')
    m.translation = point
    o.matrix_world = m


def polyline_curve(name, pts, radius, mat):
    cd = bpy.data.curves.new(name, 'CURVE')
    cd.dimensions = '3D'
    cd.bevel_depth = radius
    cd.bevel_resolution = 3
    cd.use_fill_caps = True
    sp = cd.splines.new('POLY')
    sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        sp.points[i].co = (p[0], p[1], p[2], 1.0)
    o = bpy.data.objects.new(name, cd)
    COLL.objects.link(o)
    cd.materials.append(MAT[mat])
    return o


def empty(name, loc, parent=None):
    o = bpy.data.objects.new(name, None)
    o.empty_display_size = 0.05
    COLL.objects.link(o)
    o.location = loc
    if parent is not None:
        attach(o, parent)
    return o


def attach(o, parent):
    """Parent keeping the current world transform (child basis stays in rest/world-aligned coordinates)."""
    bpy.context.view_layer.update()
    mw = o.matrix_world.copy()
    o.parent = parent
    o.matrix_parent_inverse = parent.matrix_world.inverted()
    o.matrix_world = mw


# ---------------------------------------------------------------------------------------------------------------
# mouth shapes (king.md section 5; talk A / O / E / M); outlines in the mouth plane, x right, z up, BU
# ---------------------------------------------------------------------------------------------------------------
MOUTH_Z = 1.705
N_T = 28


def _curve(fn, w, x0=0.0):
    return [(x0 + w * t, fn(t)) for t in [-1 + 2 * i / (N_T - 1) for i in range(N_T)]]


def mouth_shape(kind):
    """Returns (cavity_outline, teeth_outline | None, tongue_outline | None) as lists of (x, z) relative to MOUTH_Z."""
    def opened(w, top, bot, teeth_h=0.0, tongue=None, x0=0.0):
        up = _curve(top, w, x0)
        lo = _curve(bot, w, x0)
        cav = up + list(reversed(lo))[1:-1]
        teeth = None
        if teeth_h > 0:
            tl = [(x, max(z - teeth_h, zb)) for (x, z), (_x, zb) in zip(up, lo)]
            teeth = [(x, z - 0.002) for x, z in up] + list(reversed(tl))
        tg = None
        if tongue:
            tw, th = tongue
            pts = []
            for i in range(N_T):
                t = -1 + 2 * i / (N_T - 1)
                xb = x0 + w * tw * t
                tb = (xb - x0) / w
                pts.append((xb, bot(tb) + 0.002))
            for i in range(N_T):
                t = 1 - 2 * i / (N_T - 1)
                xb = x0 + w * tw * t
                tb = (xb - x0) / w
                pts.append((xb, min(bot(tb) + th * math.sqrt(max(0.0, 1 - t * t)) + 0.004, top(tb) - teeth_h - 0.004)))
            tg = pts
        return cav, teeth, tg

    def line(w, mid, th, x0=0.0, tilt=0.0):
        up = _curve(lambda t: mid(t) + tilt * t + th * math.sqrt(max(0.0, 1 - t * t)) + 0.0035, w, x0)
        lo = _curve(lambda t: mid(t) + tilt * t - th * math.sqrt(max(0.0, 1 - t * t)) - 0.0035, w, x0)
        return up + list(reversed(lo))[1:-1], None, None

    def ellipse(w, h, x0=0.0, z0=0.0):
        return [(x0 + w * math.cos(2 * math.pi * i / 40), z0 + h * math.sin(2 * math.pi * i / 40)) for i in range(40)]

    if kind == 'smile':
        return line(0.125, lambda t: 0.034 * t * t - 0.012, 0.013)
    if kind == 'grin':  # cheer, open laugh, upper teeth, tongue
        return opened(0.15, lambda t: 0.026 + 0.022 * t * t, lambda t: 0.048 - 0.112 * (1 - t * t) ** 0.75 - 0.0 * t,
                      0.026, (0.55, 0.05))
    if kind == 'big':  # big_cheer, very wide open D
        return opened(0.178, lambda t: 0.034 + 0.026 * t * t, lambda t: 0.060 - 0.150 * (1 - t * t) ** 0.7,
                      0.03, (0.58, 0.07))
    if kind == 'wave':  # open laugh, a little smaller than grin
        return opened(0.14, lambda t: 0.022 + 0.022 * t * t, lambda t: 0.044 - 0.095 * (1 - t * t) ** 0.75,
                      0.024, (0.55, 0.045))
    if kind == 'A':
        return opened(0.115, lambda t: 0.018 + 0.014 * t * t, lambda t: 0.032 - 0.085 * (1 - t * t) ** 0.8,
                      0.02, (0.5, 0.04))
    if kind == 'A2':  # wide A
        return opened(0.135, lambda t: 0.020 + 0.016 * t * t, lambda t: 0.036 - 0.10 * (1 - t * t) ** 0.8,
                      0.022, (0.52, 0.045))
    if kind == 'O':
        cav = ellipse(0.052, 0.062, 0.0, -0.012)
        return cav, None, ellipse(0.03, 0.022, 0.0, -0.05)
    if kind == 'E':
        return opened(0.12, lambda t: 0.014 + 0.008 * t * t, lambda t: 0.020 - 0.050 * (1 - t * t) ** 0.6,
                      0.024, None)
    if kind == 'M':
        return line(0.085, lambda t: 0.004 * t * t, 0.008)
    if kind == 'half':
        return opened(0.09, lambda t: 0.010 + 0.010 * t * t, lambda t: 0.018 - 0.045 * (1 - t * t) ** 0.8,
                      0.0, (0.5, 0.022))
    if kind == 'side':  # think: pursed, pushed to one side, slight wave
        return line(0.06, lambda t: 0.01 * math.sin(t * math.pi), 0.009, x0=0.045, tilt=0.012)
    if kind == 'smirk':  # point: closed toothy grin, one corner up
        return opened(0.125, lambda t: 0.012 + 0.024 * t * t + 0.016 * t, lambda t: 0.012 + 0.024 * t * t + 0.016 * t
                      - 0.042 * (1 - t * t) ** 0.6, 0.03, None)
    if kind == 'soft':  # encourage: small open warm smile
        return opened(0.11, lambda t: 0.012 + 0.03 * t * t, lambda t: 0.024 + 0.012 * t * t - 0.055 * (1 - t * t) ** 0.8,
                      0.0, (0.45, 0.022))
    if kind == 'o_small':  # surprised
        cav = ellipse(0.036, 0.046, 0.0, -0.01)
        return cav, None, ellipse(0.02, 0.014, 0.0, -0.04)
    raise KeyError(kind)


MOUTHS = ['smile', 'grin', 'big', 'wave', 'A', 'A2', 'O', 'E', 'M', 'half', 'side', 'smirk', 'soft', 'o_small']

# ---------------------------------------------------------------------------------------------------------------
# the model
# ---------------------------------------------------------------------------------------------------------------
COLL = None
MAT = {}

TORSO_C = Vector((0, 0, 0.98))
TORSO_R = (0.53, 0.37, 0.50)
HEAD_C = Vector((0, 0, 1.88))
HEAD_R = (0.45, 0.40, 0.40)
EYE_Z = 1.940
EYE_X = 0.186
EYE_R = (0.1225, 0.10, 0.144)  # T1: 1.25x the B1k eye (0.098, 0.08, 0.115)
EYE_Y = -0.352
TROWEL_GRIP = (0.56, -0.01, 0.61)
EMBLEM_SCALE = 1.45  # T1: hard-hat crown emblem 1.45x (B1k 1.0)
HAT_Z = 2.165       # T1: hard hat 0.045 higher (B1k 2.12) so the brick brows read in front of the visor
CORONET_Z = 0.405   # T1: coronet band height above the hat joint (dome top 0.45)


def torso_deform(p):
    z = p.z + TORSO_C.z
    belly = math.exp(-((z - 0.80) / 0.26) ** 2)
    x = p.x * (1 + 0.05 * math.exp(-((z - 0.80) / 0.32) ** 2))
    y = p.y * (1 + 0.14 * belly) if p.y < 0 else p.y
    return Vector((x, y, p.z))


def torso_bm():
    bm = se_bmesh(TORSO_R, 3.0, 22, torso_deform)
    bm.transform(Matrix.Translation(TORSO_C))
    return bm


class King:
    """Builds the character; apply(pose) sets every channel (see animate_king.POSE_DEFAULT for the channel list)."""

    def __init__(self):
        global COLL
        COLL = bpy.data.collections.new('king')
        bpy.context.scene.collection.children.link(COLL)
        for k in MATS:
            MAT[k] = make_mat(k)
        hair_bump(MAT['hair'], scale=14.0, strength=0.36, streak=('#6C3B1C', '#A4623A'))
        hair_bump(MAT['brow'], scale=16.0, strength=0.12)
        MAT['cheek'] = cheek_mat()
        MAT['glint'] = emission_mat('glint', '#FFFFFF', 1.8)
        self.j = {}
        self.parts = {}
        self._build()

    # -- joints --------------------------------------------------------------------------------------------------
    def joint(self, name, loc, parent):
        o = empty('j_' + name, loc, self.j[parent] if parent else None)
        self.j[name] = o
        return o

    def part(self, o, joint):
        attach(o, self.j[joint])
        self.parts[o.name] = o
        return o

    def _build(self):
        J = self.joint
        P = self.part
        J('view', (0, 0, 0), None)
        J('root', (0, 0, 0), 'view')
        J('body', (0, 0, 0.56), 'root')
        J('leg_l', (-0.215, 0, 0.56), 'root')
        J('leg_r', (0.215, 0, 0.56), 'root')
        J('head', (0, 0, 1.45), 'body')
        J('hat', (0, 0, HAT_Z), 'head')
        J('hat_tilt', (0, 0, HAT_Z), 'hat')
        self.j['hat_tilt'].rotation_euler = (math.radians(-10.0), 0, 0)
        for s, side in ((-1, 'l'), (1, 'r')):
            J('arm_' + side, (0.53 * s, 0.0, 1.30), 'body')
            J('elbow_' + side, (0.53 * s, 0.0, 0.97), 'arm_' + side)
            J('wrist_' + side, (0.53 * s, 0.0, 0.72), 'elbow_' + side)
        J('trowel', TROWEL_GRIP, 'wrist_r')

        # ---- legs and boots
        for s, side in ((-1, 'l'), (1, 'r')):
            x = 0.215 * s
            P(se('leg_' + side, 'suit', (x, 0, 0.40), (0.128, 0.128, 0.21), 2.2), 'leg_' + side)
            P(se('hem_' + side, 'cuff', (x, 0, 0.255), (0.148, 0.148, 0.05), 2.6), 'leg_' + side)
            P(se('shaft_' + side, 'boot', (x, -0.01, 0.17), (0.15, 0.16, 0.08), 2.6), 'leg_' + side)
            P(se('boot_' + side, 'boot', (x + 0.01 * s, -0.07, 0.12), (0.18, 0.245, 0.10), 3.0), 'leg_' + side)
            P(se('sole_' + side, 'sole', (x + 0.01 * s, -0.07, 0.035), (0.195, 0.26, 0.037), 4.0), 'leg_' + side)
            for a in (40, -40):
                lace = se('lace_%s_%d' % (side, a), 'pencil', (x + 0.01 * s, -0.255, 0.165), (0.055, 0.014, 0.014), 2.0)
                lace.rotation_euler = (math.radians(-35), math.radians(a), 0)
                P(lace, 'leg_' + side)

        # ---- torso, belt, sash, collar, pocket, pouch
        torso = P(bm_to_obj('torso', torso_bm(), 'suit'), 'body')
        bpy.context.view_layer.update()
        n = Vector((0.664, 0, 0.747)).normalized()
        d = n.dot(Vector((0, 0, 1.02)))
        hw = 0.088
        P(shell('sash', torso_bm, [((n * (d + hw)), n), ((n * (d - hw)), -n)], 0.012, 0.024, 'sash'), 'body')
        for k, (a, b) in enumerate(((d + hw - 0.016, d + hw - 0.004), (d - hw + 0.004, d - hw + 0.016))):
            P(shell('sash_trim%d' % k, torso_bm, [(n * b, n), (n * a, -n)], 0.030, 0.010, 'gold', 0.003), 'body')
        P(shell('belt', torso_bm, [((0, 0, 0.765), (0, 0, 1)), ((0, 0, 0.625), (0, 0, -1))], 0.010, 0.020, 'leather'),
          'body')
        bpy.context.view_layer.update()
        tb = mesh_bvh([torso])
        hit, _ = surface_hit(tb, 0.0, 0.695)
        P(se('buckle', 'gold', (0, hit.y - 0.03, 0.695), (0.085, 0.022, 0.075), 4.0), 'body')
        P(se('buckle_hole', 'leather', (0, hit.y - 0.046, 0.695), (0.042, 0.012, 0.033), 4.0), 'body')
        # collar: V of the cream shirt + two light lapels
        P(decal('vneck', [(-0.15, 1.50), (0.15, 1.50), (0.0, 1.27)], tb, 'shirt', 0.006, 0.012, 3), 'body')
        P(decal('lapel_l', [(-0.155, 1.50), (-0.005, 1.275), (-0.07, 1.25), (-0.24, 1.44)], tb, 'shirt', 0.012, 0.014, 3),
          'body')
        P(decal('lapel_r', [(0.155, 1.50), (0.005, 1.275), (0.07, 1.25), (0.24, 1.44)], tb, 'shirt', 0.012, 0.014, 3),
          'body')
        # zipper dashes
        z = 1.23
        k = 0
        while z > 0.80:
            P(decal('zip%d' % k, [(-0.008, z), (0.008, z), (0.008, z - 0.03), (-0.008, z - 0.03)], tb, 'zip', 0.004,
                    0.006, 1), 'body')
            z -= 0.055
            k += 1
        # chest pocket (screen right) + yellow pencil
        P(decal('pocket', [(0.17, 1.22), (0.35, 1.22), (0.35, 1.08), (0.33, 1.06), (0.19, 1.06), (0.17, 1.08)], tb, 'cuff',
                0.008, 0.016, 3), 'body')
        P(decal('pocket_flap', [(0.165, 1.235), (0.355, 1.235), (0.355, 1.20), (0.165, 1.20)], tb, 'zip', 0.022, 0.012,
                2), 'body')
        hit, _ = surface_hit(tb, 0.30, 1.22)
        pen = se('pencil', 'pencil', (0.30, hit.y - 0.035, 1.30), (0.02, 0.02, 0.085), 2.0, rot=(0, -10, 0))
        P(pen, 'body')
        P(se('pencil_tip', 'red', (0.315, hit.y - 0.035, 1.385), (0.021, 0.021, 0.02), 2.0), 'body')
        # pouch on the screen-left hip + red screwdriver handle
        P(se('pouch', 'leather', (-0.47, -0.16, 0.60), (0.11, 0.10, 0.12), 3.0), 'body')
        P(se('pouch_flap', 'leather', (-0.47, -0.21, 0.69), (0.115, 0.06, 0.04), 3.0, rot=(-20, 0, 0)), 'body')
        P(se('screwdriver', 'red', (-0.42, -0.12, 0.78), (0.026, 0.026, 0.06), 2.2), 'body')

        # sash medallions (leaf, sparkle, diamond) placed on the sash surface
        bpy.context.view_layer.update()
        sb = mesh_bvh([self.parts['sash']])
        tdir = Vector((0.747, 0, -0.664))
        for i, t in enumerate((-0.27, 0.0, 0.27)):
            c = Vector((0, 0, 1.02)) + tdir * t
            hit, nrm = surface_hit(sb, c.x, c.z)
            md = se('pin%d' % i, 'gold', (0, 0, 0), (0.052, 0.016, 0.052), 2.0)
            orient_to(md, hit, nrm)
            P(md, 'body')
            sym = se('pin%d_sym' % i, 'enamel' if i == 1 else ('sash' if i == 2 else 'enamel'), (0, 0, 0),
                     (0.026, 0.008, 0.026), 2.0 if i == 0 else (4.0 if i == 2 else 1.2))
            orient_to(sym, hit + nrm * 0.014, nrm, spin=45 if i != 0 else 30)
            if i == 0:
                sym.scale = (1.0, 1.0, 0.55)
            P(sym, 'body')

        # T1 (art turn 1, king identity): thick gold chain + medallion with the Golden Trowel on blue enamel, under the
        # beard and over the sash: a second royal sign that reads at sprite size (the coin emblem, not a crown)
        bpy.context.view_layer.update()
        tb = mesh_bvh([torso])
        k = 0
        x = -0.30
        while x <= 0.3001:
            zc = 1.322 + 1.25 * x * x
            hit, nrm = surface_hit(tb, x, zc)
            if hit is not None:
                P(se('chain%d' % k, 'gold', hit + nrm * 0.016, (0.026, 0.026, 0.026), 2.0, 6), 'body')
            x += 0.046
            k += 1
        hit, nrm = surface_hit(tb, 0.0, 1.215)
        md = se('medal', 'gold', (0, 0, 0), (0.112, 0.030, 0.112), 2.3, 12)
        orient_to(md, hit + nrm * 0.035, nrm)
        P(md, 'body')
        md2 = se('medal_enamel', 'enamel', (0, 0, 0), (0.080, 0.014, 0.080), 2.3, 10)
        orient_to(md2, hit + nrm * 0.058, nrm)
        P(md2, 'body')
        tw = se('medal_trowel', 'gold', (0, 0, 0), (0.040, 0.012, 0.050), 1.25, 8)
        orient_to(tw, hit + nrm * 0.068 + Vector((0, 0, 0.012)), nrm)
        P(tw, 'body')
        th = se('medal_handle', 'gold', (0, 0, 0), (0.012, 0.010, 0.026), 2.2, 6)
        orient_to(th, hit + nrm * 0.066 + Vector((0, 0, -0.052)), nrm)
        P(th, 'body')
        bail = se('medal_bail', 'gold', (0, 0, 0), (0.030, 0.016, 0.024), 2.4, 6)
        orient_to(bail, hit + nrm * 0.030 + Vector((0, 0, 0.115)), nrm)
        P(bail, 'body')

        # ---- arms and hands
        for s, side in ((-1, 'l'), (1, 'r')):
            sx = 0.53 * s
            taper = (lambda p: Vector((p.x * (1.0 + 0.10 * p.z / 0.21), p.y * (1.0 + 0.10 * p.z / 0.21), p.z)))
            P(se('uparm_' + side, 'suit', (sx, 0, 1.17), (0.128, 0.128, 0.21), 2.15, 14, deform=taper), 'arm_' + side)
            P(se('cuffring_' + side, 'cuff', (sx, 0, 0.995), (0.146, 0.146, 0.062), 2.6), 'arm_' + side)
            P(se('elbowball_' + side, 'skin', (sx, 0, 0.965), (0.10, 0.10, 0.10), 2.0), 'elbow_' + side)
            P(se('forearm_' + side, 'skin', (sx, 0, 0.845), (0.10, 0.10, 0.15), 2.15, 14,
                 deform=lambda p: Vector((p.x * (1.0 + 0.08 * p.z / 0.15), p.y * (1.0 + 0.08 * p.z / 0.15), p.z))),
              'elbow_' + side)
            # hand: palm block + 4 fingers + thumb (each with its pivot at the knuckle); palm faces the body (-s X)
            wz = 0.72
            P(se('palm_' + side, 'skin', (sx, 0, wz - 0.07), (0.072, 0.098, 0.082), 2.6), 'wrist_' + side)
            for k, fy in enumerate((-0.066, -0.022, 0.022, 0.066)):
                bm = se_bmesh((0.033, 0.029, 0.052), 2.2, 8)
                bm.transform(Matrix.Translation((0, 0, -0.042)))
                f = bm_to_obj('finger%d_%s' % (k, side), bm, 'skin', (sx, fy, wz - 0.125))
                P(f, 'wrist_' + side)
            bm = se_bmesh((0.036, 0.034, 0.058), 2.2, 8)
            bm.transform(Matrix.Translation((0, 0, -0.045)))
            P(bm_to_obj('thumb_' + side, bm, 'skin', (sx - 0.03 * s, -0.075, wz - 0.045)), 'wrist_' + side)
        # Golden Trowel (king.md section 8): handle along the trowel joint +Z, blade above, faces +-Y
        tj = Vector(TROWEL_GRIP)
        P(se('trowel_handle', 'wood', tj + Vector((0, 0, -0.02)), (0.036, 0.036, 0.13), 2.2), 'trowel')
        P(se('trowel_ferrule', 'gold', tj + Vector((0, 0, 0.13)), (0.046, 0.046, 0.032), 2.6), 'trowel')
        P(se('trowel_neck', 'gold', tj + Vector((0, 0, 0.18)), (0.018, 0.018, 0.04), 2.0), 'trowel')
        bm = bmesh.new()
        H, W, T = 0.22, 0.165, 0.028
        c0 = tj + Vector((0, 0, 0.42))
        vt = bm.verts.new(c0 + Vector((0, 0, H)))
        vb = bm.verts.new(c0 + Vector((0, 0, -H)))
        vl = bm.verts.new(c0 + Vector((-W, 0, -0.02)))
        vr = bm.verts.new(c0 + Vector((W, 0, -0.02)))
        vf = bm.verts.new(c0 + Vector((0, -T, -0.02)))
        vk = bm.verts.new(c0 + Vector((0, T, -0.02)))
        for a, b2, c in ((vt, vl, vf), (vt, vf, vr), (vb, vf, vl), (vb, vr, vf), (vt, vk, vl), (vt, vr, vk), (vb, vl, vk),
                         (vb, vk, vr)):
            bm.faces.new((a, b2, c))
        bm.normal_update()
        blade = bm_to_obj('trowel_blade', bm, 'gold', smooth=False)
        bv = blade.modifiers.new('bevel', 'BEVEL')
        bv.width = 0.008
        bv.segments = 2
        P(blade, 'trowel')

        # ---- head, face
        head = P(se('head', 'skin', HEAD_C, HEAD_R, 3.0, 22), 'head')
        for s in (-1, 1):
            P(se('ear_%d' % s, 'skin', (0.475 * s, 0.05, 1.89), (0.075, 0.05, 0.105), 2.0, rot=(0, 0, -18 * s)), 'head')
            P(se('earin_%d' % s, 'cheek', (0.49 * s, 0.03, 1.89), (0.045, 0.03, 0.065), 2.0, rot=(0, 0, -18 * s)), 'head')
        muzzle = P(se('muzzle', 'skin', (0, -0.335, 1.715), (0.215, 0.085, 0.12), 2.5, 16), 'head')
        P(se('nose', 'skin', (0, -0.445, 1.84), (0.072, 0.06, 0.062), 2.6), 'head')
        for s in (-1, 1):
            P(se('cheek_%d' % s, 'cheek', (0.235 * s, -0.33, 1.80), (0.105, 0.075, 0.088), 2.2), 'head')
        self._beard()
        # eyes
        self.eyes = []
        for s, side in ((-1, 'l'), (1, 'r')):
            c = Vector((EYE_X * s, EYE_Y, EYE_Z))
            gz = self.joint('gaze_' + side, c, 'head')
            gz.scale = EYE_R
            ball = se('eye_' + side, None, c, EYE_R, 2.0, 12)
            ball.data.materials.append(eye_mat('eye_' + side, gz))
            P(ball, 'head')
            lid_up = self.joint('lidup_' + side, c, 'head')
            lid_lo = self.joint('lidlo_' + side, c, 'head')
            for nm, jn, keep in (('lidU_', 'lidup_', 'outer'), ('lidL_', 'lidlo_', 'inner')):
                bm = se_bmesh((EYE_R[0] * 1.05, EYE_R[1] * 1.09, EYE_R[2] * 1.05), 2.0, 12)
                bisect_keep(bm, Vector((0, 0, 0)), Vector((0, 0, 1)), keep)
                o = bm_to_obj(nm + side, bm, 'skin', c)
                sd = o.modifiers.new('solid', 'SOLIDIFY')
                sd.thickness = 0.01
                P(o, jn + side)
            # highlights (fixed to the head; upper-left big, lower-right small)
            for k, (dx, dz, r) in enumerate(((-0.040, 0.056, 0.031), (0.045, -0.050, 0.015))):
                hx = c.x + dx
                hz = c.z + dz
                ny = 1 - (dx / EYE_R[0]) ** 2 - (dz / EYE_R[2]) ** 2
                hy = c.y - EYE_R[1] * math.sqrt(max(0.0, ny)) - 0.004
                g = se('glint_%s%d' % (side, k), 'glint', (hx, hy, hz), (r, 0.004, r * 1.15), 2.0, 6)
                P(g, 'head')
            # closed-eye lash arcs: blink (U) and happy (^)
            for kind in ('closed', 'happy', 'soft'):
                pts = []
                for i in range(15):
                    t = -1 + 2 * i / 14
                    x = c.x + EYE_R[0] * 0.92 * t
                    ke = EYE_R[2] / 0.115  # T1: arcs scale with the 1.25x eye
                    if kind == 'closed':
                        z = c.z - ke * (0.012 + 0.022 * (1 - t * t))
                    elif kind == 'happy':
                        z = c.z + ke * (-0.02 + 0.05 * (1 - t * t))
                    else:
                        z = c.z + ke * (-0.005 + 0.012 * (1 - t * t))
                    if kind == 'soft':
                        ny = 1 - ((x - c.x) / (EYE_R[0] * 1.05)) ** 2 - ((z - c.z) / (EYE_R[2] * 1.05)) ** 2
                        y = c.y - EYE_R[1] * 1.09 * math.sqrt(max(0.0, ny)) - 0.01
                    else:  # T1: on the CLOSED lid (shell turned ~88 deg: front extent = z radius, height = y radius)
                        ny = 1 - ((x - c.x) / (EYE_R[0] * 1.05)) ** 2 - ((z - c.z) / (EYE_R[1] * 1.09)) ** 2
                        y = c.y - EYE_R[2] * 1.05 * math.sqrt(max(0.0, ny)) - 0.008
                    pts.append((x, y, z))
                arc = polyline_curve('lash_%s_%s' % (kind, side), pts, 0.0135, 'lash')
                P(arc, 'head')
            self.eyes.append(side)
            # brick brow
            bj = self.joint('brow_' + side, (EYE_X * s * 1.03, -0.466, 2.088), 'head')
            P(se('brow_' + side, 'brow', bj.location, (0.118, 0.044, 0.040), 2.8), 'brow_' + side)
        # mouths
        bpy.context.view_layer.update()
        mb = mesh_bvh([head, muzzle])
        self.mouths = {}
        for kind in MOUTHS:
            cav, teeth, tongue = mouth_shape(kind)
            objs = [decal('mouth_%s' % kind, [(x, MOUTH_Z + z) for x, z in cav], mb,
                          'smile' if kind in ('smile', 'M', 'side') else 'mouth', 0.002, 0.003, 3)]
            if teeth:
                objs.append(decal('teeth_%s' % kind, [(x, MOUTH_Z + z) for x, z in teeth], mb, 'teeth', 0.0055, 0.002, 3))
            if tongue:
                objs.append(decal('tongue_%s' % kind, [(x, MOUTH_Z + z) for x, z in tongue], mb, 'tongue', 0.0045, 0.002,
                                  3))
            for o in objs:
                P(o, 'head')
            self.mouths[kind] = objs

        # ---- hard hat (king.md section 6): dome, ridge, brim, emblem plate, gold 3-point crown with stud tips
        hc = Vector((0, 0, HAT_Z))
        bm = se_bmesh((0.525, 0.505, 0.45), 2.35, 20)
        bisect_keep(bm, Vector((0, 0, 0)), Vector((0, 0, -1)), 'inner')
        dome = bm_to_obj('dome', bm, 'hat', hc)
        sd = dome.modifiers.new('solid', 'SOLIDIFY')
        sd.thickness = 0.03
        P(dome, 'hat_tilt')
        bm = se_bmesh((0.105, 0.522, 0.468), 2.8, 16)
        bisect_keep(bm, Vector((0, 0, 0.0)), Vector((0, 0, -1)), 'inner')
        P(bm_to_obj('ridge', bm, 'hat', hc), 'hat_tilt')
        bm = bmesh.new()
        ring = []
        for i in range(72):
            a = 2 * math.pi * i / 72
            ca, sa = math.cos(a), math.sin(a)
            r = 0.585 + 0.03 * max(0.0, -sa) ** 2.0 - 0.025 * max(0.0, sa)
            ring.append(bm.verts.new((r * ca, r * sa * 0.97, 0.0)))
        f = bm.faces.new(ring)
        ext = bmesh.ops.extrude_face_region(bm, geom=[f])
        for v in [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]:
            v.co.z += 0.045
        bm.normal_update()
        brim = bm_to_obj('brim', bm, 'hat', hc + Vector((0, 0, -0.02)), smooth=False)
        bv = brim.modifiers.new('bevel', 'BEVEL')
        bv.width = 0.02
        bv.segments = 3
        bv.limit_method = 'ANGLE'
        brim.data.polygons.foreach_set('use_smooth', [True] * len(brim.data.polygons))
        brim.data.use_auto_smooth = True
        brim.data.auto_smooth_angle = math.radians(40)
        P(brim, 'hat_tilt')
        bpy.context.view_layer.update()
        db = mesh_bvh([dome])
        hit, nrm = surface_hit(db, 0.0, HAT_Z + 0.195)
        plate = empty('emblem', (0, 0, 0))
        orient_to(plate, hit, nrm)
        attach(plate, self.j['hat_tilt'])

        def on_plate(o, dy=0.0):
            o.parent = plate
            o.matrix_parent_inverse = Matrix.Identity(4)
            o.location = (o.location.x, o.location.y - dy, o.location.z)
            self.parts[o.name] = o
            return o

        on_plate(se('emblem_frame', 'gold', (0, 0.004, 0), (0.162, 0.02, 0.118), 4.0), 0.0)
        on_plate(se('emblem_plate', 'enamel', (0, 0, 0), (0.145, 0.024, 0.102), 4.2), 0.006)
        # crown: band + three blunt points (convex pieces, same gold), a stud ball on every tip, no gem
        pieces = [[(-0.092, -0.062), (0.092, -0.062), (0.092, -0.022), (-0.092, -0.022)],
                  [(-0.092, -0.030), (-0.030, -0.030), (-0.074, 0.040)],
                  [(0.030, -0.030), (0.092, -0.030), (0.074, 0.040)],
                  [(-0.042, -0.030), (0.042, -0.030), (0.0, 0.058)]]
        for k, pc in enumerate(pieces):
            bm = poly_bmesh(pc, 0)
            ext = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
            for v in [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]:
                v.co.y -= 0.022
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            co = bm_to_obj('crown%d' % k, bm, 'gold', (0, 0, 0), smooth=False)
            bv = co.modifiers.new('bevel', 'BEVEL')
            bv.width = 0.004
            bv.segments = 2
            on_plate(co, 0.026)
        for k, (x, zz) in enumerate(((-0.074, 0.046), (0.0, 0.064), (0.074, 0.046))):
            on_plate(se('crown_stud%d' % k, 'gold', (x, 0, zz), (0.022, 0.022, 0.022), 2.0, 6), 0.042)
        plate.scale = (EMBLEM_SCALE, 1.2, EMBLEM_SCALE)
        # coronet (T1): a gold band round the top of the dome with three rounded teeth at the front (left, centre,
        # right; centre tallest), each with a stud ball like the emblem crown. Molded into the hard hat, not a crown on
        # a bare head; rounded, no gems, no spikes (king.md section 10.1)
        zc = hc.z + CORONET_Z
        u = (CORONET_Z / 0.45) ** 2.35
        rr = 0.525 * (1 - u) ** (1 / 2.35)
        pts = [((rr + 0.004) * math.cos(2 * math.pi * i / 48), (rr + 0.004) * 0.962 * math.sin(2 * math.pi * i / 48), zc)
               for i in range(49)]
        P(polyline_curve('coronet_band', pts, 0.030, 'gold'), 'hat_tilt')
        for k, (az, hh, ww) in enumerate(((-90 - 50, 0.088, 0.060), (-90, 0.112, 0.070), (-90 + 50, 0.088, 0.060))):
            a = math.radians(az)
            cx, cy = rr * math.cos(a), rr * 0.962 * math.sin(a)
            out = Vector((math.cos(a), math.sin(a), 0.0))
            tooth = se('coronet_tooth%d' % k, 'gold', (cx + out.x * 0.012, cy + out.y * 0.012, zc + hh * 0.55),
                       (ww, 0.034, hh), 2.4, 10)
            tooth.rotation_euler = (math.radians(14), 0.0, a + math.pi / 2)  # top leans outward 14 deg
            P(tooth, 'hat_tilt')
            P(se('coronet_stud%d' % k, 'gold', (cx + out.x * 0.022, cy + out.y * 0.022, zc + hh * 1.25),
                 (0.036, 0.036, 0.036), 2.0, 6), 'hat_tilt')
        bpy.context.view_layer.update()
        self.rest = {k: (v.location.copy(), v.rotation_euler.copy(), v.scale.copy()) for k, v in self.j.items()}
        self.rest_world = {k: v.matrix_world.copy() for k, v in self.j.items()}
        self.prest = {k: (v.location.copy(), v.rotation_euler.copy(), v.scale.copy())
                      for k, v in self.parts.items() if hasattr(v, 'location')}

    def _beard(self):
        """Hair + chin beard: enlarged head minus the face window, the crown and the lower back (EXACT boolean on
        closed shapes, then bevel); a blocky chin mass below the mouth."""
        P = self.part
        big = se('beard', 'hair', (0, 0.0, 1.875), (0.47, 0.425, 0.46), 3.3, 22)
        win = [(-0.31, 2.17), (-0.355, 2.13), (-0.37, 2.03), (-0.345, 1.90), (-0.315, 1.78), (-0.28, 1.68),
               (-0.225, 1.615), (-0.15, 1.578), (-0.07, 1.562), (0.0, 1.558), (0.07, 1.562), (0.15, 1.578),
               (0.225, 1.615), (0.28, 1.68), (0.315, 1.78), (0.345, 1.90), (0.37, 2.03), (0.355, 2.13), (0.31, 2.17),
               (0.15, 2.19), (0.0, 2.195), (-0.15, 2.19)]
        bm = poly_bmesh(win, 0)
        ext = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
        for v in [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]:
            v.co.y = -0.08
        for v in bm.verts:
            if abs(v.co.y) < 1e-9:
                v.co.y = -1.2
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.normal_update()
        cut_win = bm_to_obj('cut_win', bm, None, smooth=False)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0.0, 0, 1.0), rotation=(0, math.radians(38), 0))
        cut_top = bpy.context.active_object
        cut_top.scale = (1.2, 3, 1.2)
        cut_top.location = (0.70, 0, 1.22)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 1.0), rotation=(0, math.radians(-38), 0))
        cut_jaw = bpy.context.active_object
        cut_jaw.scale = (1.2, 3, 1.2)
        cut_jaw.location = (-0.70, 0, 1.22)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0.11 + 0.5, 1.64 - 0.5))
        cut_back = bpy.context.active_object
        cut_back.scale = (3, 1, 1)
        for c in (cut_win, cut_top, cut_back, cut_jaw):
            if c.name not in COLL.objects:
                COLL.objects.link(c)
        bpy.context.view_layer.update()
        for c in (cut_win, cut_top, cut_back, cut_jaw):
            md = big.modifiers.new('cut_' + c.name, 'BOOLEAN')
            md.operation = 'DIFFERENCE'
            md.solver = 'EXACT'
            md.object = c
        bpy.context.view_layer.objects.active = big
        for md in list(big.modifiers):
            bpy.ops.object.modifier_apply(modifier=md.name)
        for c in (cut_win, cut_top, cut_back, cut_jaw):
            bpy.data.objects.remove(c, do_unlink=True)
        big.data.use_auto_smooth = True
        big.data.auto_smooth_angle = math.radians(50)
        bv = big.modifiers.new('bevel', 'BEVEL')
        bv.width = 0.012
        bv.segments = 3
        bv.limit_method = 'ANGLE'
        bv.angle_limit = math.radians(50)
        P(big, 'head')
        chin_taper = (lambda p: Vector((p.x * (0.86 + 0.16 * (p.z / 0.16 + 1.0) * 0.5) * (1.0 - 0.10 * max(0.0, -p.y)
                                                                                         / 0.25),
                                        p.y - 0.035 * max(0.0, -p.z / 0.16) ** 2, p.z)))
        P(se('chin', 'hair', (0, -0.18, 1.49), (0.34, 0.255, 0.16), 2.7, 22, deform=chin_taper), 'head')
        for s in (-1, 1):
            P(se('tuft_%d' % s, 'hair', (0.43 * s, 0.10, 2.06), (0.09, 0.2, 0.08), 2.4), 'head')

    # -- posing ----------------------------------------------------------------------------------------------------
    def reset(self):
        for k, (l, r, s) in self.rest.items():
            o = self.j[k]
            o.location, o.rotation_euler, o.scale = l.copy(), r.copy(), s.copy()
        for k, (l, r, s) in self.prest.items():
            o = self.parts[k]
            o.location, o.rotation_euler, o.scale = l.copy(), r.copy(), s.copy()

    def apply(self, p):
        """Set every channel of pose dict p (degrees, BU). Missing keys keep the rest value."""
        R = math.radians
        self.reset()
        g = p.get
        j = self.j
        j['view'].rotation_euler = (0, 0, R(g('view_yaw', 0.0)))
        sq = g('squash', 1.0)
        side = 1.0 / math.sqrt(sq)
        j['root'].location = (g('root_x', 0.0), g('root_y', 0.0), g('root_z', 0.0))
        j['root'].scale = (side, side, sq)
        j['root'].rotation_euler = (0, R(g('root_roll', 0.0)), R(g('root_yaw', 0.0)))
        bl = self.rest['body'][0]
        j['body'].location = (bl.x + g('body_x', 0.0), bl.y, bl.z - g('body_drop', 0.0))
        j['body'].rotation_euler = (R(-g('body_pitch', 0.0)), R(g('body_roll', 0.0)), R(g('body_yaw', 0.0)))
        for s, side_ in ((-1, 'l'), (1, 'r')):
            j['leg_' + side_].rotation_euler = (R(-g('leg_%s_pitch' % side_, 0.0)), R(-s * g('leg_%s_out' % side_, 0.0)),
                                                0)
            arm = j['arm_' + side_]
            ik = g('arm_%s_ik' % side_)
            if ik is not None:
                q, bend = self._ik(side_, Vector(ik), Vector(g('arm_%s_pole' % side_)))
                arm.rotation_mode = 'QUATERNION'
                arm.rotation_quaternion = q
                j['elbow_' + side_].rotation_euler = (-bend, 0, 0)
            else:
                arm.rotation_mode = 'ZXY'
                arm.rotation_euler = (R(-g('arm_%s_fwd' % side_, 0.0)), R(-s * g('arm_%s_out' % side_, 0.0)),
                                      R(s * g('arm_%s_twist' % side_, 0.0)))
                j['elbow_' + side_].rotation_euler = (R(-g('arm_%s_elbow' % side_, 0.0)), 0, 0)
            wr = j['wrist_' + side_]
            hn, hf = g('hand_%s_n' % side_), g('hand_%s_f' % side_)
            if ik is not None and hn is not None and hf is not None:
                wr.rotation_mode = 'QUATERNION'
                wr.rotation_quaternion = self._hand_frame(side_, q, bend, hn, hf)
            else:
                wr.rotation_mode = 'ZXY'
                wr.rotation_euler = (R(-g('arm_%s_wpitch' % side_, 0.0)), R(-s * g('arm_%s_wroll' % side_, 0.0)),
                                     R(s * g('arm_%s_wtwist' % side_, 0.0)))
            self._hand(side_, s, g('hand_' + side_, 'relax'))
        j['head'].rotation_euler = (R(g('head_pitch', 0.0)), R(g('head_roll', 0.0)), R(g('head_yaw', 0.0)))
        hl = self.rest['hat'][0]
        j['hat'].location = (hl.x + g('hat_x', 0.0), hl.y, hl.z + g('hat_lift', 0.0))
        j['hat'].rotation_euler = (R(-g('hat_pitch', 0.0)), R(g('hat_roll', 0.0)), 0)
        self._face(p)
        bpy.context.view_layer.update()
        self._trowel(p)
        bpy.context.view_layer.update()

    def _ik(self, side, target, pole):
        """Two-bone IK in body space (rest world coordinates): shoulder quaternion + elbow bend (radians).
        Rest arm points -Z; the elbow bends toward local -Y."""
        S = self.rest['arm_' + side][0]
        L1 = (self.rest['arm_' + side][0] - self.rest['elbow_' + side][0]).length
        L2 = (self.rest['elbow_' + side][0] - self.rest['wrist_' + side][0]).length
        v = target - S
        d = max(abs(L1 - L2) + 1e-3, min(L1 + L2 - 1e-3, v.length))
        u = v.normalized()
        pv = (pole - S) - (pole - S).dot(u) * u
        if pv.length < 1e-6:
            pv = Vector((0, -1, 0)) - Vector((0, -1, 0)).dot(u) * u
        pv.normalize()
        ca = (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d)
        al = math.acos(max(-1.0, min(1.0, ca)))
        a = u * math.cos(al) + pv * math.sin(al)
        E = S + a * L1
        b = ((S + u * d) - E).normalized()
        bd = b - b.dot(a) * a
        if bd.length < 1e-6:
            bd = -pv
        bd.normalize()
        zl = -a
        yl = -bd
        xl = yl.cross(zl)
        m = Matrix((xl, yl, zl)).transposed()
        bend = math.acos(max(-1.0, min(1.0, a.dot(b))))
        return m.to_quaternion(), bend

    @staticmethod
    def _hand_frame(side, q, bend, n, f):
        """Wrist rotation that turns the palm normal to n and the fingers (wrist -> knuckles) to f, both in body
        space. Left (screen-left) palm = local +X, right palm = local -X, fingers = local -Z, thumb side = local -Y."""
        n = Vector(n).normalized()
        f = Vector(f).normalized()
        x = n if side == 'l' else -n
        z = -(f - f.dot(x) * x).normalized()
        y = z.cross(x)
        D = Matrix((x, y, z)).transposed()
        F = q.to_matrix() @ Matrix.Rotation(-bend, 3, 'X')
        return (F.inverted() @ D).to_quaternion()

    HAND = {
        #          finger curls (index..pinky)  thumb (rx, ry) deg  palm scale
        'relax': ((22, 26, 30, 34), (-22, 14), (1.0, 1.0, 1.0)),
        'open': ((2, 0, 2, 6), (-48, -18), (0.9, 1.04, 1.04)),
        'fist': ((150, 155, 158, 160), (-70, 62), (1.08, 1.0, 0.95)),
        'grip': ((128, 132, 136, 140), (-62, 58), (1.06, 1.0, 0.95)),
        'index': ((0, 155, 158, 160), (-70, 62), (1.08, 1.0, 0.95)),
        'thumb': ((150, 155, 158, 160), (-92, 0), (1.08, 1.0, 0.95)),
    }

    def _hand(self, side, s, state):
        """Hand shapes: relax, open, fist, grip (holds the trowel), index (points), thumb (thumbs-up)."""
        R = math.radians
        curls, (tx, ty), ps = self.HAND[state]
        for k, c in enumerate(curls):
            self.parts['finger%d_%s' % (k, side)].rotation_euler = (0, R(s * c), 0)
        self.parts['thumb_' + side].rotation_mode = 'YXZ'
        self.parts['thumb_' + side].rotation_euler = (R(tx), R(s * ty), 0)
        self.parts['palm_' + side].scale = ps

    def _face(self, p):
        g = p.get
        R = math.radians
        j = self.j
        for side in ('l', 'r'):
            gx, gz = g('gaze_x', 0.0), g('gaze_z', 0.0)
            gzo = j['gaze_' + side]
            gzo.rotation_euler = (R(-gz), 0, R(gx))
            lid = g('lid', 0.0)
            low = g('lid_low', 0.0)
            happy = g('eyes', 'open')
            if happy in ('happy', 'closed'):
                lid, low = 1.0, 0.0
            up_ang = -74 + 156 * lid  # 0 open (edge at +74 deg) .. 1 closed (edge at -82 deg); T1: wider open, no rim
            lo_ang = 76 - 84 * low     # 0 open (edge at -76 deg) .. 1 raised (edge at +8 deg)
            if happy in ('happy', 'closed'):
                up_ang, lo_ang = 88, 88
            j['lidup_' + side].rotation_euler = (R(up_ang), 0, 0)
            j['lidlo_' + side].rotation_euler = (R(lo_ang), 0, 0)
            for kind in ('closed', 'happy', 'soft'):
                self.parts['lash_%s_%s' % (kind, side)].hide_render = not (happy == kind)
            # brows: dy (BU, + up), ang (deg, + = outer end up)
            s = -1 if side == 'l' else 1
            bl = self.rest['brow_' + side][0]
            j['brow_' + side].location = (bl.x, bl.y, bl.z + g('brow_%s_dy' % side, g('brow_dy', 0.0)))
            ang = g('brow_%s_ang' % side, g('brow_ang', 4.0))
            j['brow_' + side].rotation_euler = (0, R(-s * ang), 0)
        kind = g('mouth', 'smile')
        for k, objs in self.mouths.items():
            for o in objs:
                o.hide_render = k != kind
        MAT['cheek'].node_tree.nodes['blush'].outputs[0].default_value = g('blush', 0.25)
        puff = g('cheek', 1.0)
        for s in (-1, 1):
            self.parts['cheek_%d' % s].scale = (puff, puff, puff)

    def _trowel(self, p):
        """Trowel world orientation from (tilt, lean, spin) in degrees: tilt = rotation in the screen plane
        (+ = blade toward screen right), lean = toward camera, spin = about its own axis; position = hand grip."""
        R = math.radians
        tj = self.j['trowel']
        hide = p.get('trowel_hide', False)
        for k in ('trowel_handle', 'trowel_ferrule', 'trowel_neck', 'trowel_blade'):
            self.parts[k].hide_render = hide
        wr = self.j['wrist_r'].matrix_world @ self.rest_world['wrist_r'].inverted()
        grip = wr @ Vector(TROWEL_GRIP)
        rot = (self.j['view'].matrix_world.to_3x3() @
               Euler((R(-p.get('trowel_lean', 0.0)), R(p.get('trowel_tilt', 0.0)), R(p.get('trowel_spin', 0.0))),
                     'YXZ').to_matrix())
        m = rot.to_4x4()
        m.translation = grip
        tj.matrix_world = m


# ---------------------------------------------------------------------------------------------------------------
# lights, world, camera, render settings (frames.json rigs.B_char + reflection environment + contact shadow)
# ---------------------------------------------------------------------------------------------------------------


def setup_world_and_lights(king):
    scene = bpy.context.scene
    world = bpy.data.worlds.new('world')
    scene.world = world
    world.use_nodes = True
    nt = world.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputWorld')
    amb = nt.nodes.new('ShaderNodeBackground')
    amb.inputs['Color'].default_value = hex_lin('#5A6A96')
    amb.inputs['Strength'].default_value = 0.25
    # glossy-ray-only studio environment: warm top, cool horizon, dark floor -> readable metal and coat highlights
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Generated'], sep.inputs['Vector'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    cr = ramp.color_ramp
    cr.elements[0].position, cr.elements[0].color = 0.0, hex_lin('#3A3028')
    cr.elements[1].position, cr.elements[1].color = 1.0, hex_lin('#FFF6E2')
    e = cr.elements.new(0.47)
    e.color = hex_lin('#6F6A70')
    e = cr.elements.new(0.53)
    e.color = hex_lin('#BFD0EE')
    mapr = nt.nodes.new('ShaderNodeMapRange')
    mapr.inputs['From Min'].default_value = -1.0
    mapr.inputs['From Max'].default_value = 1.0
    nt.links.new(sep.outputs['Z'], mapr.inputs['Value'])
    nt.links.new(mapr.outputs['Result'], ramp.inputs['Fac'])
    env = nt.nodes.new('ShaderNodeBackground')
    env.inputs['Strength'].default_value = 1.15
    nt.links.new(ramp.outputs['Color'], env.inputs['Color'])
    lp = nt.nodes.new('ShaderNodeLightPath')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(lp.outputs['Is Glossy Ray'], mix.inputs['Fac'])
    nt.links.new(amb.outputs['Background'], mix.inputs[1])
    nt.links.new(env.outputs['Background'], mix.inputs[2])
    nt.links.new(mix.outputs['Shader'], out.inputs['Surface'])

    def light(name, kind, color, energy, loc=None, rot=None, size=None, angle=None, receivers=None):
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
        if receivers is not None:
            o.light_linking.receiver_collection = receivers
        return o

    def aim(o, target):
        d = Vector(target) - o.location
        o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()

    light('key_sun', 'SUN', '#FFE6C4', 2.6, rot=(50, 0, -35), angle=8, receivers=COLL)
    fill = light('fill_area', 'AREA', '#9CC2FF', 180, loc=(3.0, -3.2, 1.6), size=3.0, receivers=COLL)
    aim(fill, (0, 0, 1.3))
    rim = light('rim_area', 'AREA', '#F2E8FF', 360, loc=(2.0, 3.0, 3.2), size=1.6, receivers=COLL)
    aim(rim, (0, 0, 1.9))
    rim2 = light('rim2_area', 'AREA', '#FFF0D8', 220, loc=(-2.4, 2.6, 2.6), size=1.6, receivers=COLL)
    aim(rim2, (0, 0, 1.6))
    # no ground / shadow catcher: with light linking the catcher counts excluded lights as occlusion (dark band);
    # the contact shadow is a soft ellipse baked by post_king.py (no long cast shadows in the sprite)
    return None


def setup_camera(width_bu, height_bu, root_y_bu, px_per_bu, tilt=10.0, target=(0.0, 0.0)):
    """Orthographic camera tilted `tilt` deg down; frame width_bu x height_bu; world point (target x, ., target z)
    lands root_y_bu above the frame bottom on the vertical centre line."""
    scene = bpy.context.scene
    cam = scene.camera
    if cam is None:
        cd = bpy.data.cameras.new('cam')
        cam = bpy.data.objects.new('cam', cd)
        scene.collection.objects.link(cam)
        scene.camera = cam
    cd = cam.data
    cd.type = 'ORTHO'
    cd.ortho_scale = max(width_bu, height_bu)
    cd.sensor_fit = 'AUTO'
    cd.clip_start = 0.1
    cd.clip_end = 100
    t = math.radians(tilt)
    cam.rotation_euler = (math.radians(90 - tilt), 0, 0)
    up = Vector((0, math.sin(t), math.cos(t)))
    fwd = Vector((0, math.cos(t), -math.sin(t)))
    origin = Vector((target[0], 0, target[1]))
    centre = origin + up * (height_bu / 2 - root_y_bu)
    cam.location = centre - fwd * 20.0
    scene.render.resolution_x = int(round(width_bu * px_per_bu))
    scene.render.resolution_y = int(round(height_bu * px_per_bu))
    scene.render.resolution_percentage = 100
    return cam


def setup_render(samples=48):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    c = scene.cycles
    c.device = 'CPU'
    c.samples = samples
    c.use_adaptive_sampling = True
    c.adaptive_threshold = 0.02
    c.use_denoising = False
    c.sample_clamp_indirect = 3.0
    c.blur_glossy = 1.0
    c.max_bounces = 6
    c.diffuse_bounces = 2
    c.glossy_bounces = 2
    c.transmission_bounces = 2
    c.volume_bounces = 0
    c.transparent_max_bounces = 8
    c.caustics_reflective = False
    c.caustics_refractive = False
    scene.render.film_transparent = True
    scene.render.use_persistent_data = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.display_settings.display_device = 'sRGB'
    scene.render.threads_mode = 'AUTO'


def build_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    king = King()
    setup_world_and_lights(king)
    return king


def _main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []

    def arg(name, default):
        return argv[argv.index(name) + 1] if name in argv else default

    out = os.path.abspath(arg('--out', 'art/3d/out/king/turn'))
    px = float(arg('--px', '160'))
    samples = int(arg('--samples', '48'))
    views = arg('--views', 'front,34,side,back').split(',')
    os.makedirs(out, exist_ok=True)
    king = build_scene()
    setup_render(samples)
    setup_camera(3.0, 3.0, 0.2, px)
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import animate_king as ak
    for view, yaw in (('front', 0), ('34', 32), ('side', 90), ('back', 180)):
        if view not in views:
            continue
        pose = dict(ak.K0)
        pose['view_yaw'] = yaw
        king.apply(pose)
        bpy.context.scene.render.filepath = os.path.join(out, 'king_turn_%s.png' % view)
        bpy.ops.render.render(write_still=True)
        print('RENDERED', view, flush=True)


if __name__ == '__main__':
    _main()
