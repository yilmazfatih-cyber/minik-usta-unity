"""Tuna and Kepche in 3D (art turn 1; D-100, D-101) — the same Blender kit, lights and materials as Usta Kral.

Blender 4.0.2, bpy + bmesh + mathutils only; no external assets, no generative AI, no reference pixels. Design source:
web repo docs/ART_DIRECTION.md section 11.1 (Tuna, D-087 proportions: >= 3.6 heads, eye <= 30 % of the visible face,
flat confident brows, short nose, small side smile, freckles, mint hard hat with a white star, orange vest with two
yellow reflective bands, cream / teal striped shirt, navy trousers with a sand knee patch, HUGE yellow work gloves,
red trainers, pencil behind the ear) and section 11.3 (Kepche: dachshund, oversized orange hard hat tilted to one
side, one ear hanging under it, mint collar with a bone tag, long body, very short legs, wagging tail).
Tuna's age is never written anywhere (D-044).

Importable (animate_crew.py) and runnable for a quick turnaround:

    blender -b --factory-startup -noaudio --python art/3d/blender/crew/build_crew.py -- --out <dir> [--px 160] [--samples 24]

Units: 1 BU = Usta Kral's head (the king is 2.5 BU tall), so the three characters share one world scale (the home
screen draws them side by side). Characters face -Y, z up; arm / hand naming is by SCREEN side (r = screen right).
Helpers, lights (frames.json rigs.B_char), camera and render settings come from art/3d/blender/king/build_king.py.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
KING = os.path.join(HERE, '..', 'king')
for p in (HERE, KING):
    if p not in sys.path:
        sys.path.insert(0, p)
import build_king as bk  # noqa: E402

from build_king import (attach, bisect_keep, bm_to_obj, decal, empty, hex_lin, make_mat, mesh_bvh,  # noqa: E402
                        orient_to, poly_bmesh, polyline_curve, principled, se, se_bmesh, surface_hit)

#        name          base       rough coat coatR sss  sheen metal spec
CREW_MATS = {
    't_hat': ('#7FE0C4', 0.26, 0.60, 0.06, 0.0, 0.0, 0.0, 0.5),
    't_brim': ('#5CC9AB', 0.28, 0.60, 0.06, 0.0, 0.0, 0.0, 0.5),
    't_star': ('#FFFFFF', 0.30, 0.40, 0.06, 0.0, 0.0, 0.0, 0.5),
    't_vest': ('#FF8A1F', 0.55, 0.10, 0.10, 0.0, 0.10, 0.0, 0.45),
    't_band': ('#FFF06A', 0.30, 0.50, 0.05, 0.0, 0.0, 0.0, 0.6),
    't_pants': ('#2E3A59', 0.65, 0.00, 0.00, 0.0, 0.25, 0.0, 0.35),
    't_patch': ('#E9C891', 0.70, 0.00, 0.00, 0.0, 0.10, 0.0, 0.35),
    't_stitch': ('#8A6A3A', 0.60, 0.00, 0.00, 0.0, 0.00, 0.0, 0.30),
    't_glove': ('#FFC93C', 0.50, 0.15, 0.20, 0.0, 0.20, 0.0, 0.45),
    't_glove_cuff': ('#F2A818', 0.55, 0.10, 0.20, 0.0, 0.20, 0.0, 0.45),
    't_shoe': ('#E8473B', 0.35, 0.40, 0.08, 0.0, 0.0, 0.0, 0.5),
    't_sole': ('#FFFFFF', 0.55, 0.00, 0.00, 0.0, 0.0, 0.0, 0.4),
    't_skin': ('#F2BC93', 0.45, 0.00, 0.00, 0.10, 0.0, 0.0, 0.45),
    't_hair': ('#4A2C1D', 0.50, 0.00, 0.00, 0.0, 0.15, 0.0, 0.45),
    't_brow': ('#3B2214', 0.50, 0.00, 0.00, 0.0, 0.10, 0.0, 0.4),
    't_freckle': ('#B5652B', 0.60, 0.00, 0.00, 0.0, 0.0, 0.0, 0.3),
    't_pencil': ('#FFD23F', 0.40, 0.20, 0.10, 0.0, 0.0, 0.0, 0.5),
    't_eraser': ('#FF8FA3', 0.50, 0.00, 0.00, 0.0, 0.0, 0.0, 0.4),
    't_lead': ('#3A2A20', 0.40, 0.00, 0.00, 0.0, 0.0, 0.0, 0.4),
    'k_fur': ('#B5652B', 0.55, 0.00, 0.00, 0.06, 0.25, 0.0, 0.4),
    'k_belly': ('#D99A5E', 0.60, 0.00, 0.00, 0.06, 0.25, 0.0, 0.4),
    'k_ear': ('#8A4A1F', 0.55, 0.00, 0.00, 0.04, 0.30, 0.0, 0.4),
    'k_nose': ('#2A1A14', 0.25, 0.50, 0.05, 0.0, 0.0, 0.0, 0.6),
    'k_hat': ('#FF9A1F', 0.26, 0.60, 0.06, 0.0, 0.0, 0.0, 0.5),
    'k_hat_brim': ('#F07E0E', 0.28, 0.60, 0.06, 0.0, 0.0, 0.0, 0.5),
    'k_collar': ('#7FE0C4', 0.35, 0.40, 0.08, 0.0, 0.0, 0.0, 0.5),
    'k_tag': ('#FFD45A', 0.24, 0.00, 0.00, 0.0, 0.0, 1.0, 0.5),
    'k_paw': ('#E2A86C', 0.60, 0.00, 0.00, 0.0, 0.15, 0.0, 0.4),
    'k_tongue': ('#FF7A8A', 0.40, 0.20, 0.10, 0.0, 0.0, 0.0, 0.5),
}


def stripe_mat(name, a='#F7F3EA', b='#2A9D8F', period=0.075):
    """Cream / teal horizontal stripes (object Z), soft edges, cotton sheen."""
    m = make_mat(name, (a, 0.70, 0.0, 0.0, 0.0, 0.25, 0.0, 0.35))
    nt = m.node_tree
    tc = nt.nodes.new('ShaderNodeTexCoord')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], sep.inputs['Vector'])
    mul = nt.nodes.new('ShaderNodeMath')
    mul.operation = 'MULTIPLY'
    mul.inputs[1].default_value = 2 * math.pi / period
    nt.links.new(sep.outputs['Z'], mul.inputs[0])
    sn = nt.nodes.new('ShaderNodeMath')
    sn.operation = 'SINE'
    nt.links.new(mul.outputs['Value'], sn.inputs[0])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    cr = ramp.color_ramp
    cr.elements[0].position, cr.elements[0].color = 0.40, hex_lin(a)
    cr.elements[1].position, cr.elements[1].color = 0.60, hex_lin(b)
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = -1.0
    mr.inputs['From Max'].default_value = 1.0
    nt.links.new(sn.outputs['Value'], mr.inputs['Value'])
    nt.links.new(mr.outputs['Result'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], principled(m).inputs['Base Color'])
    return m


def blush_mat(name, skin, blush='#E8735A', amount=0.22):
    m = make_mat(name, CREW_MATS[skin])
    nt = m.node_tree
    lw = nt.nodes.new('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.5
    inv = nt.nodes.new('ShaderNodeMath')
    inv.operation = 'SUBTRACT'
    inv.inputs[0].default_value = 1.0
    nt.links.new(lw.outputs['Facing'], inv.inputs[1])
    mul = nt.nodes.new('ShaderNodeMath')
    mul.operation = 'MULTIPLY'
    mul.inputs[1].default_value = amount * 2.0
    nt.links.new(inv.outputs['Value'], mul.inputs[0])
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['A'].default_value = hex_lin(CREW_MATS[skin][0])
    mix.inputs['B'].default_value = hex_lin(blush)
    nt.links.new(mul.outputs['Value'], mix.inputs['Factor'])
    nt.links.new(mix.outputs['Result'], principled(m).inputs['Base Color'])
    return m


def dark_eye_mat(name, gaze_obj):
    """Dog eye: almost all dark brown iris with a warm ring, a thin white rim at the edge (gaze space, like the king)."""
    m = bpy.data.materials.new('m_' + name)
    m.use_nodes = True
    nt = m.node_tree
    b = principled(m)
    b.inputs['Roughness'].default_value = 0.18
    b.inputs['Coat Weight'].default_value = 0.4
    b.inputs['Coat Roughness'].default_value = 0.05
    tc = nt.nodes.new('ShaderNodeTexCoord')
    tc.object = gaze_obj
    nrm = nt.nodes.new('ShaderNodeVectorMath')
    nrm.operation = 'NORMALIZE'
    nt.links.new(tc.outputs['Object'], nrm.inputs[0])
    flat = nt.nodes.new('ShaderNodeVectorMath')
    flat.operation = 'MULTIPLY'
    flat.inputs[1].default_value = (1.0, 0.0, 1.0)
    nt.links.new(nrm.outputs['Vector'], flat.inputs[0])
    ln = nt.nodes.new('ShaderNodeVectorMath')
    ln.operation = 'LENGTH'
    nt.links.new(flat.outputs['Vector'], ln.inputs[0])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    cr = ramp.color_ramp
    stops = [(0.0, '#120A06'), (0.45, '#1C0E08'), (0.62, '#5A3018'), (0.80, '#3A1E0E'), (0.86, '#F4EEE6'),
             (1.0, '#E6DCD0')]
    cr.elements[0].position, cr.elements[0].color = stops[0][0], hex_lin(stops[0][1])
    cr.elements[1].position, cr.elements[1].color = stops[-1][0], hex_lin(stops[-1][1])
    for pos, col in stops[1:-1]:
        e = cr.elements.new(pos)
        e.color = hex_lin(col)
    nt.links.new(ln.outputs['Value'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], b.inputs['Base Color'])
    return m


def ring_brim(name, mat, center, r, depth=0.035, front=0.03, back=0.02, squash=0.97):
    bm = bmesh.new()
    ring = []
    for i in range(72):
        a = 2 * math.pi * i / 72
        ca, sa = math.cos(a), math.sin(a)
        rr = r + front * max(0.0, -sa) ** 2.0 - back * max(0.0, sa)
        ring.append(bm.verts.new((rr * ca, rr * sa * squash, 0.0)))
    f = bm.faces.new(ring)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]:
        v.co.z += depth
    bm.normal_update()
    o = bm_to_obj(name, bm, mat, center, smooth=False)
    bv = o.modifiers.new('bevel', 'BEVEL')
    bv.width = depth * 0.42
    bv.segments = 3
    bv.limit_method = 'ANGLE'
    o.data.polygons.foreach_set('use_smooth', [True] * len(o.data.polygons))
    o.data.use_auto_smooth = True
    o.data.auto_smooth_angle = math.radians(40)
    return o


def dome(name, mat, center, r, n=2.35, thick=0.025):
    bm = se_bmesh(r, n, 20)
    bisect_keep(bm, Vector((0, 0, 0)), Vector((0, 0, -1)), 'inner')
    o = bm_to_obj(name, bm, mat, center)
    sd = o.modifiers.new('solid', 'SOLIDIFY')
    sd.thickness = thick
    return o


def star_pts(cx, cz, ro, ri, rot=0.0):
    pts = []
    for i in range(10):
        a = math.radians(90 + rot) + math.pi * i / 5
        rr = ro if i % 2 == 0 else ri
        pts.append((cx + rr * math.cos(a), cz + rr * math.sin(a)))
    return pts


class Rig:
    """Joint hierarchy (empties) + parts, the same scheme as build_king.King."""

    def __init__(self, coll_name):
        self.coll = bpy.data.collections.new(coll_name)
        bpy.context.scene.collection.children.link(self.coll)
        bk.COLL = self.coll
        self.j = {}
        self.parts = {}

    def joint(self, name, loc, parent):
        o = empty('j_' + name, loc, self.j[parent] if parent else None)
        self.j[name] = o
        return o

    def part(self, o, joint):
        attach(o, self.j[joint])
        self.parts[o.name] = o
        return o

    def freeze(self):
        bpy.context.view_layer.update()
        self.rest = {k: (v.location.copy(), v.rotation_euler.copy(), v.scale.copy()) for k, v in self.j.items()}
        self.prest = {k: (v.location.copy(), v.rotation_euler.copy(), v.scale.copy())
                      for k, v in self.parts.items() if hasattr(v, 'location')}

    def reset(self):
        for k, (l, r, s) in self.rest.items():
            o = self.j[k]
            o.rotation_mode = 'XYZ'
            o.location, o.rotation_euler, o.scale = l.copy(), r.copy(), s.copy()
        for k, (l, r, s) in self.prest.items():
            o = self.parts[k]
            o.location, o.rotation_euler, o.scale = l.copy(), r.copy(), s.copy()

    def hide(self, visible):
        for o in self.coll.objects:
            o.hide_render = not visible


# ---------------------------------------------------------------------------------------------------------------
# Tuna
# ---------------------------------------------------------------------------------------------------------------
T_HEAD = Vector((0.0, 0.0, 1.76))
T_HEAD_R = (0.272, 0.252, 0.285)   # skull 0.57 BU; feet to skull top 2.045 BU = 3.6 heads (D-087)
T_EYE_R = (0.060, 0.048, 0.071)    # eye height 0.142 = 30 % of the visible face (brim to chin), D-087
T_EYE_X = 0.108
T_EYE_Z = 1.772
T_EYE_Y = -0.222
T_HAT = Vector((0.0, 0.0, 1.935))
T_MOUTH_Z = 1.628
T_SHOULDER = 0.285
T_SHOULDER_Z = 1.355
T_UPPER = 0.25
T_FORE = 0.22


def tuna_head_deform(p):
    t = max(0.0, -p.z / T_HEAD_R[2])  # 0 at the equator .. 1 at the chin
    k = 1.0 - 0.20 * t ** 1.6
    return Vector((p.x * k, p.y * (1.0 - 0.08 * t ** 2), p.z))


class Tuna(Rig):
    def __init__(self):
        super().__init__('tuna')
        M = bk.MAT
        for k, v in CREW_MATS.items():
            if k not in M:
                M[k] = make_mat(k, v)
        bk.hair_bump(M['t_hair'], scale=16.0, strength=0.30, streak=('#3E2416', '#6A4430'))
        M['t_shirt'] = stripe_mat('t_shirt')
        M['t_cheek'] = blush_mat('t_cheek', 't_skin', amount=0.20)
        M['glint'] = M.get('glint') or bk.emission_mat('glint', '#FFFFFF', 1.8)
        M['smile'] = M.get('smile') or make_mat('smile', bk.MATS['smile'])
        M['mouth'] = M.get('mouth') or make_mat('mouth', bk.MATS['mouth'])
        M['teeth'] = M.get('teeth') or make_mat('teeth', bk.MATS['teeth'])
        M['tongue'] = M.get('tongue') or make_mat('tongue', bk.MATS['tongue'])
        M['lash'] = M.get('lash') or make_mat('lash', bk.MATS['lash'])
        self._build()
        self.freeze()

    def _build(self):
        J, P = self.joint, self.part
        J('view', (0, 0, 0), None)
        J('root', (0, 0, 0), 'view')
        J('hips', (0, 0, 0.86), 'root')
        J('leg_l', (-0.115, 0, 0.86), 'root')
        J('leg_r', (0.115, 0, 0.86), 'root')
        J('knee_l', (-0.115, 0, 0.50), 'leg_l')
        J('knee_r', (0.115, 0, 0.50), 'leg_r')
        J('chest', (0, 0, 1.00), 'hips')
        J('head', (0, 0, 1.48), 'chest')
        J('hat', tuple(T_HAT), 'head')
        for s, side in ((-1, 'l'), (1, 'r')):
            J('arm_' + side, (T_SHOULDER * s, 0.0, T_SHOULDER_Z), 'chest')
            J('elbow_' + side, (T_SHOULDER * s, 0.0, T_SHOULDER_Z - T_UPPER), 'arm_' + side)
            J('wrist_' + side, (T_SHOULDER * s, 0.0, T_SHOULDER_Z - T_UPPER - T_FORE), 'elbow_' + side)

        # legs: navy trousers (knee split), sand knee patch on the screen-left knee, red trainers
        for s, side in ((-1, 'l'), (1, 'r')):
            x = 0.115 * s
            P(se('thigh_' + side, 't_pants', (x, 0, 0.69), (0.112, 0.112, 0.205), 2.3), 'leg_' + side)
            P(se('shin_' + side, 't_pants', (x, 0, 0.33), (0.104, 0.104, 0.21), 2.3), 'knee_' + side)
            P(se('cuffp_' + side, 't_pants', (x, 0, 0.165), (0.112, 0.112, 0.042), 2.6), 'knee_' + side)
            P(se('shoe_' + side, 't_shoe', (x + 0.008 * s, -0.05, 0.085), (0.110, 0.165, 0.080), 2.8), 'knee_' + side)
            P(se('sole_' + side, 't_sole', (x + 0.008 * s, -0.05, 0.026), (0.116, 0.173, 0.029), 3.6), 'knee_' + side)
            P(se('toecap_' + side, 't_sole', (x + 0.008 * s, -0.185, 0.062), (0.078, 0.044, 0.042), 2.4), 'knee_' + side)
            for k in range(2):
                lace = se('tlace_%s%d' % (side, k), 't_sole', (x + 0.008 * s, -0.085 + 0.045 * k, 0.142 - 0.012 * k),
                          (0.052, 0.012, 0.010), 2.0, rot=(-25, 0, 0))
                P(lace, 'knee_' + side)
        bpy.context.view_layer.update()
        kb = mesh_bvh([self.parts['thigh_l'], self.parts['shin_l']])
        patch = [(-0.115 + 0.058 * math.copysign(abs(math.cos(2 * math.pi * i / 32)) ** 0.6, math.cos(2 * math.pi * i / 32)),
                  0.445 + 0.060 * math.copysign(abs(math.sin(2 * math.pi * i / 32)) ** 0.6, math.sin(2 * math.pi * i / 32)))
                 for i in range(32)]
        P(decal('knee_patch', patch, kb, 't_patch', 0.004, 0.012, 2), 'knee_l')  # sand patch below the knee
        for d in (-1, 1):
            st = [(-0.115 - 0.024, 0.445 - 0.024 * d), (-0.115 + 0.024, 0.445 + 0.024 * d)]
            pts = []
            for (x0, z0), (x1, z1) in [(st[0], st[1])]:
                for i in range(6):
                    t = i / 5
                    xx, zz = x0 + (x1 - x0) * t, z0 + (z1 - z0) * t
                    hit, _ = surface_hit(kb, xx, zz)
                    pts.append((xx, hit.y - 0.018, zz))
            P(polyline_curve('stitch%d' % d, pts, 0.006, 't_stitch'), 'knee_l')

        # torso: striped long-sleeve shirt, open orange safety vest with two yellow reflective bands
        def torso_bm(r=(0.228, 0.165, 0.272), grow=0.0):
            def deform(p):
                z = p.z
                taper = 1.0 + 0.06 * (z / 0.272)  # a little wider at the shoulders
                return Vector((p.x * taper, p.y, p.z))
            bm = se_bmesh((r[0] + grow, r[1] + grow, r[2] + grow * 0.5), 2.7, 18, deform)
            bm.transform(Matrix.Translation((0, 0, 1.12)))
            return bm
        torso = P(bm_to_obj('torso', torso_bm(), 't_shirt'), 'chest')
        P(se('neck', 't_skin', (0, 0.005, 1.455), (0.066, 0.062, 0.08), 2.0), 'chest')
        P(se('collar', 't_shirt', (0, 0.0, 1.398), (0.112, 0.090, 0.03), 2.4), 'chest')

        def vest_band(name, mat, z0, z1, grow, opening=0.052):
            bm = torso_bm(grow=grow)
            bisect_keep(bm, Vector((0, 0, z1)), Vector((0, 0, 1)), 'inner')
            bisect_keep(bm, Vector((0, 0, z0)), Vector((0, 0, -1)), 'inner')
            # front opening: drop faces in the front strip |x| < opening
            kill = [f for f in bm.faces if f.calc_center_median().y < -0.04 and abs(f.calc_center_median().x) < opening]
            bmesh.ops.delete(bm, geom=kill, context='FACES')
            o = bm_to_obj(name, bm, mat)
            sd = o.modifiers.new('solid', 'SOLIDIFY')
            sd.thickness = 0.022
            sd.offset = 1.0
            bv = o.modifiers.new('bevel', 'BEVEL')
            bv.width = 0.008
            bv.segments = 2
            bv.limit_method = 'ANGLE'
            return o
        P(vest_band('vest', 't_vest', 0.885, 1.36, 0.016), 'chest')
        P(vest_band('vband_hi', 't_band', 1.105, 1.155, 0.040, 0.058), 'chest')
        P(vest_band('vband_lo', 't_band', 0.975, 1.025, 0.040, 0.058), 'chest')
        # vest shoulder straps over the shirt
        for s in (-1, 1):
            P(se('vstrap_%d' % s, 't_vest', (0.13 * s, 0.0, 1.345), (0.075, 0.150, 0.035), 2.6, rot=(0, 12 * s, 0)), 'chest')
        # a pocket with the pencil tip peeking (screen right, lower vest)
        bpy.context.view_layer.update()
        vb = mesh_bvh([self.parts['vest']])
        P(decal('vpocket', [(0.08, 0.96), (0.17, 0.96), (0.17, 0.90), (0.08, 0.90)], vb, 't_vest', 0.006, 0.012, 2),
          'chest')

        # arms: striped sleeves (upper + fore), HUGE yellow work gloves (palm, four fingers, thumb, flared cuff)
        for s, side in ((-1, 'l'), (1, 'r')):
            sx = T_SHOULDER * s
            P(se('shoulder_' + side, 't_shirt', (sx, 0, T_SHOULDER_Z), (0.092, 0.092, 0.09), 2.2), 'arm_' + side)
            P(se('upper_' + side, 't_shirt', (sx, 0, T_SHOULDER_Z - T_UPPER * 0.5), (0.076, 0.076, 0.15), 2.2),
              'arm_' + side)
            P(se('elbowb_' + side, 't_shirt', (sx, 0, T_SHOULDER_Z - T_UPPER), (0.072, 0.072, 0.072), 2.0),
              'elbow_' + side)
            P(se('fore_' + side, 't_shirt', (sx, 0, T_SHOULDER_Z - T_UPPER - T_FORE * 0.45), (0.068, 0.068, 0.13), 2.2),
              'elbow_' + side)
            wz = T_SHOULDER_Z - T_UPPER - T_FORE
            # glove: a flared cuff at the wrist, a big rounded palm, four fat fingers, a thumb (palm faces the body)
            P(se('gcuff_' + side, 't_glove_cuff', (sx, 0, wz + 0.01), (0.118, 0.118, 0.068), 2.6), 'wrist_' + side)
            P(se('gpalm_' + side, 't_glove', (sx, 0, wz - 0.13), (0.105, 0.158, 0.135), 2.5), 'wrist_' + side)
            for k, fy in enumerate((-0.105, -0.036, 0.036, 0.105)):
                bm = se_bmesh((0.052, 0.044, 0.092), 2.2, 8)
                bm.transform(Matrix.Translation((0, 0, -0.076)))
                f = bm_to_obj('gfinger%d_%s' % (k, side), bm, 't_glove', (sx, fy, wz - 0.245))
                P(f, 'wrist_' + side)
            bm = se_bmesh((0.056, 0.050, 0.095), 2.2, 8)
            bm.transform(Matrix.Translation((0, 0, -0.072)))
            P(bm_to_obj('gthumb_' + side, bm, 't_glove', (sx - 0.05 * s, -0.135, wz - 0.085)), 'wrist_' + side)

        # head: slightly pointed chin, ears, small nose, freckles, cheeks, brows, eyes, mouths
        head = P(bm_to_obj('head', se_bmesh(T_HEAD_R, 2.5, 20, tuna_head_deform), 't_skin', tuple(T_HEAD)), 'head')
        for s in (-1, 1):
            P(se('ear_%d' % s, 't_skin', (0.270 * s, 0.02, 1.75), (0.048, 0.034, 0.068), 2.0, rot=(0, 0, -15 * s)), 'head')
            P(se('earin_%d' % s, 't_cheek', (0.283 * s, 0.005, 1.75), (0.028, 0.02, 0.042), 2.0, rot=(0, 0, -15 * s)),
              'head')
        P(se('nose', 't_skin', (0.004, -0.262, 1.690), (0.032, 0.030, 0.034), 2.4), 'head')
        for s in (-1, 1):
            P(se('cheek_%d' % s, 't_cheek', (0.145 * s, -0.196, 1.648), (0.07, 0.05, 0.055), 2.2), 'head')
        bpy.context.view_layer.update()
        hb = mesh_bvh([head])
        for s in (-1, 1):
            for k, (dx, dz) in enumerate(((0.098, 1.688), (0.13, 1.672), (0.16, 1.69), (0.116, 1.656))):
                hit, nrm = surface_hit(hb, dx * s, dz)
                fr = se('freckle_%d_%d' % (s, k), 't_freckle', (0, 0, 0), (0.0085, 0.003, 0.0085), 2.0, 4)
                orient_to(fr, hit + nrm * 0.001, nrm)
                P(fr, 'head')
        self.eyes = []
        for s, side in ((-1, 'l'), (1, 'r')):
            c = Vector((T_EYE_X * s, T_EYE_Y, T_EYE_Z))
            gz = self.joint('gaze_' + side, c, 'head')
            gz.scale = T_EYE_R
            ball = se('eye_' + side, None, c, T_EYE_R, 2.0, 12)
            ball.data.materials.append(bk.eye_mat('t_eye_' + side, gz))
            P(ball, 'head')
            self.joint('lidup_' + side, c, 'head')
            bm = se_bmesh((T_EYE_R[0] * 1.06, T_EYE_R[1] * 1.10, T_EYE_R[2] * 1.06), 2.0, 12)
            bisect_keep(bm, Vector((0, 0, 0)), Vector((0, 0, 1)), 'outer')
            o = bm_to_obj('lidU_' + side, bm, 't_skin', c)
            sd = o.modifiers.new('solid', 'SOLIDIFY')
            sd.thickness = 0.008
            P(o, 'lidup_' + side)
            for k, (dx, dz, r) in enumerate(((-0.020, 0.028, 0.017), (0.022, -0.026, 0.008))):
                hx, hz = c.x + dx, c.z + dz
                ny = 1 - (dx / T_EYE_R[0]) ** 2 - (dz / T_EYE_R[2]) ** 2
                hy = c.y - T_EYE_R[1] * math.sqrt(max(0.0, ny)) - 0.003
                P(se('glint_%s%d' % (side, k), 'glint', (hx, hy, hz), (r, 0.003, r * 1.15), 2.0, 6), 'head')
            # happy (^) and closed arcs on the closed lid
            for kind in ('closed', 'happy'):
                pts = []
                for i in range(13):
                    t = -1 + 2 * i / 12
                    x = c.x + T_EYE_R[0] * 0.9 * t
                    z = c.z + (-0.008 - 0.014 * (1 - t * t) if kind == 'closed' else -0.012 + 0.028 * (1 - t * t))
                    ny = 1 - ((x - c.x) / (T_EYE_R[0] * 1.06)) ** 2 - ((z - c.z) / (T_EYE_R[1] * 1.10)) ** 2
                    y = c.y - T_EYE_R[2] * 1.06 * math.sqrt(max(0.0, ny)) - 0.006
                    pts.append((x, y, z))
                P(polyline_curve('lash_%s_%s' % (kind, side), pts, 0.0085, 'lash'), 'head')
            bj = self.joint('brow_' + side, (T_EYE_X * s * 1.02, -0.240, 1.858), 'head')
            P(se('brow_' + side, 't_brow', bj.location, (0.058, 0.020, 0.016), 2.6), 'brow_' + side)
            self.eyes.append(side)
        self.mouths = {}
        for kind, sc in (('smile', 0.55), ('grin', 0.55), ('wave', 0.52), ('O', 0.5)):
            cav, teeth, tongue = bk.mouth_shape(kind)
            # small side smile (D-087): right corner a touch up
            tf = (lambda x, z: (x * sc + 0.006, T_MOUTH_Z + z * sc + 0.012 * x))
            objs = [decal('t_mouth_%s' % kind, [tf(x, z) for x, z in cav], hb, 'smile' if kind == 'smile' else 'mouth',
                          0.002, 0.003, 3)]
            if teeth:
                objs.append(decal('t_teeth_%s' % kind, [tf(x, z) for x, z in teeth], hb, 'teeth', 0.0045, 0.002, 3))
            if tongue:
                objs.append(decal('t_tongue_%s' % kind, [tf(x, z) for x, z in tongue], hb, 'tongue', 0.0038, 0.002, 3))
            for o in objs:
                P(o, 'head')
            self.mouths[kind] = objs

        # hair: chestnut cap at the back and sides + messy tufts that spill out under the hat to chin level + fringe
        bm = se_bmesh((0.300, 0.282, 0.315), 2.4, 18, tuna_head_deform)
        bisect_keep(bm, Vector((0, -0.10, 0)), Vector((0, -1, 0.0)), 'inner')
        bisect_keep(bm, Vector((0, 0, -0.17)), Vector((0, 0, -1)), 'inner')
        cap = bm_to_obj('hair_cap', bm, 't_hair', tuple(T_HEAD + Vector((0, 0.012, 0.012))))
        P(cap, 'head')
        tufts = [  # (x, y, z, rx, ry, rz, rot_y, rot_x)
            # messy locks spilling out under the brim down to the chin, ends flicking outward (signature silhouette)
            (-0.262, -0.10, 1.69, 0.060, 0.07, 0.125, -10, 8), (0.262, -0.10, 1.69, 0.060, 0.07, 0.125, 10, 8),
            (-0.272, -0.15, 1.58, 0.046, 0.05, 0.090, -34, 16), (0.272, -0.15, 1.58, 0.046, 0.05, 0.090, 34, 16),
            (-0.300, -0.02, 1.62, 0.058, 0.08, 0.130, -22, 0), (0.300, -0.02, 1.62, 0.058, 0.08, 0.130, 22, 0),
            (-0.318, 0.07, 1.555, 0.050, 0.07, 0.085, -48, 0), (0.318, 0.07, 1.555, 0.050, 0.07, 0.085, 48, 0),
            (-0.22, 0.18, 1.60, 0.075, 0.07, 0.12, -18, -10), (0.22, 0.18, 1.60, 0.075, 0.07, 0.12, 18, -10),
            (0.0, 0.25, 1.62, 0.14, 0.07, 0.13, 0, -14), (-0.11, 0.24, 1.55, 0.06, 0.05, 0.08, -26, -18),
            (0.12, 0.24, 1.55, 0.06, 0.05, 0.08, 26, -18),
        ]
        for k, (x, y, z, rx, ry, rz, ay, ax) in enumerate(tufts):
            P(se('tuft%d' % k, 't_hair', (x, y, z), (rx * 0.92, ry, rz * 1.12), 1.75, 10, rot=(ax, ay, 0)), 'head')
        for k, (x, ang, w) in enumerate(((-0.16, 28, 0.072), (-0.06, 10, 0.08), (0.05, -14, 0.078), (0.16, -30, 0.07))):
            fr = se('fringe%d' % k, 't_hair', (x, -0.222, 1.900), (w, 0.050, 0.040), 2.2, 10, rot=(-22, ang * 0.3, ang))
            P(fr, 'head')

        # pencil behind the screen-right ear (yellow, pink eraser)
        pen = empty('pencil_j', (0.276, 0.03, 1.86))
        attach(pen, self.j['head'])
        pen.rotation_euler = (math.radians(12), math.radians(-28), 0)
        for nm, mat, z, r in (('pen_body', 't_pencil', 0.0, (0.016, 0.016, 0.10)), ('pen_eraser', 't_eraser', 0.105,
                                                                                   (0.0165, 0.0165, 0.018)),
                              ('pen_tip', 't_skin', -0.11, (0.012, 0.012, 0.02)), ('pen_lead', 't_lead', -0.128,
                                                                                  (0.005, 0.005, 0.008))):
            o = se(nm, mat, (0, 0, z), r, 2.6 if nm == 'pen_body' else 2.0, 8)
            o.parent = pen
            o.matrix_parent_inverse = Matrix.Identity(4)
            self.parts[o.name] = o

        # mint hard hat: dome, ridge, brim, white star on the front
        P(dome('t_dome', 't_hat', tuple(T_HAT), (0.338, 0.322, 0.262), 2.35, 0.022), 'hat')
        bm = se_bmesh((0.07, 0.334, 0.274), 2.8, 14)
        bisect_keep(bm, Vector((0, 0, 0)), Vector((0, 0, -1)), 'inner')
        bisect_keep(bm, Vector((0, -0.05, 0)), Vector((0, -1, 0)), 'inner')  # back half only: the star owns the front
        P(bm_to_obj('t_ridge', bm, 't_hat', tuple(T_HAT)), 'hat')
        P(ring_brim('t_brim', 't_brim', T_HAT + Vector((0, 0, -0.018)), 0.372, 0.032, 0.035, 0.02), 'hat')
        bpy.context.view_layer.update()
        db = mesh_bvh([self.parts['t_dome']])
        star = decal('t_star', star_pts(0.0, T_HAT.z + 0.125, 0.085, 0.038), db, 't_star', 0.003, 0.016, 3)
        P(star, 'hat')

    HAND = {  # finger curl (deg, all four), thumb (rx, ry), spread (deg)
        'relax': (28, (-24, 12), 4),
        'open': (4, (-50, -20), 10),
        'fist': (140, (-70, 60), 0),
        'thumb': (140, (-95, 0), 0),
    }

    def apply(self, p):
        R = math.radians
        self.reset()
        g = p.get
        j = self.j
        j['view'].rotation_euler = (0, 0, R(g('view_yaw', 0.0)))
        sq = g('squash', 1.0)
        side = 1.0 / math.sqrt(sq)
        j['root'].location = (g('root_x', 0.0), 0.0, g('root_z', 0.0))
        j['root'].scale = (side, side, sq)
        j['root'].rotation_euler = (0, R(g('root_roll', 0.0)), 0)
        hl = self.rest['hips'][0]
        j['hips'].location = (hl.x + g('hips_x', 0.0), hl.y, hl.z - g('hips_drop', 0.0))
        j['hips'].rotation_euler = (0, R(g('hips_roll', 0.0)), 0)
        j['chest'].rotation_euler = (R(-g('chest_pitch', 0.0)), R(g('chest_roll', 0.0)), R(g('chest_yaw', 0.0)))
        for s, sd in ((-1, 'l'), (1, 'r')):
            j['leg_' + sd].rotation_euler = (R(-g('leg_%s_pitch' % sd, 0.0)), R(-s * g('leg_%s_out' % sd, 0.0)), 0)
            j['knee_' + sd].rotation_euler = (R(g('knee_%s' % sd, 0.0)), 0, 0)
            arm = j['arm_' + sd]
            arm.rotation_mode = 'ZXY'
            arm.rotation_euler = (R(-g('arm_%s_fwd' % sd, 0.0)), R(-s * g('arm_%s_out' % sd, 6.0)),
                                  R(s * g('arm_%s_twist' % sd, 0.0)))
            el = j['elbow_' + sd]
            el.rotation_mode = 'XYZ'
            el.rotation_euler = (R(-g('elbow_%s' % sd, 8.0)), R(-s * g('elbow_%s_out' % sd, 0.0)), 0)
            wr = j['wrist_' + sd]
            wr.rotation_mode = 'ZXY'
            wr.rotation_euler = (R(-g('wrist_%s_pitch' % sd, 0.0)), R(-s * g('wrist_%s_roll' % sd, 0.0)),
                                 R(s * g('wrist_%s_twist' % sd, 0.0)))
            curl, (tx, ty), spread = self.HAND[g('hand_' + sd, 'relax')]
            for k in range(4):
                fo = self.parts['gfinger%d_%s' % (k, sd)]
                fo.rotation_euler = (R(spread * (k - 1.5) * 0.5), R(s * curl), 0)
            th = self.parts['gthumb_' + sd]
            th.rotation_mode = 'YXZ'
            th.rotation_euler = (R(tx), R(s * ty), 0)
        j['head'].rotation_euler = (R(g('head_pitch', 0.0)), R(g('head_roll', 0.0)), R(g('head_yaw', 0.0)))
        hl = self.rest['hat'][0]
        j['hat'].location = (hl.x, hl.y, hl.z + g('hat_lift', 0.0))
        j['hat'].rotation_euler = (R(-g('hat_pitch', -4.0)), R(g('hat_roll', 0.0)), 0)
        # face
        for sd in ('l', 'r'):
            s = -1 if sd == 'l' else 1
            j['gaze_' + sd].rotation_euler = (R(-g('gaze_z', 0.0)), 0, R(g('gaze_x', 0.0)))
            eyes = g('eyes', 'open')
            lid = g('lid', 0.08)
            ang = -76 + 160 * lid
            if eyes in ('happy', 'closed'):
                ang = 90
            j['lidup_' + sd].rotation_euler = (R(ang), 0, 0)
            for kind in ('closed', 'happy'):
                self.parts['lash_%s_%s' % (kind, sd)].hide_render = eyes != kind
            bl = self.rest['brow_' + sd][0]
            j['brow_' + sd].location = (bl.x, bl.y, bl.z + g('brow_%s_dy' % sd, g('brow_dy', 0.0)))
            j['brow_' + sd].rotation_euler = (0, R(-s * g('brow_%s_ang' % sd, g('brow_ang', -4.0))), 0)
        kind = g('mouth', 'smile')
        for k, objs in self.mouths.items():
            for o in objs:
                o.hide_render = k != kind
        bpy.context.view_layer.update()


# ---------------------------------------------------------------------------------------------------------------
# Kepche (dachshund, faces screen-left; the camera sees it in 3/4)
# ---------------------------------------------------------------------------------------------------------------
K_BODY = Vector((0.10, 0.0, 0.40))
K_HEAD = Vector((-0.54, -0.02, 0.69))


class Kepce(Rig):
    def __init__(self):
        super().__init__('kepce')
        M = bk.MAT
        for k, v in CREW_MATS.items():
            if k not in M:
                M[k] = make_mat(k, v)
        bk.hair_bump(M['k_ear'], scale=10.0, strength=0.18)
        M['glint'] = M.get('glint') or bk.emission_mat('glint', '#FFFFFF', 1.8)
        M['mouth'] = M.get('mouth') or make_mat('mouth', bk.MATS['mouth'])
        M['lash'] = M.get('lash') or make_mat('lash', bk.MATS['lash'])
        self._build()
        self.freeze()

    def _build(self):
        J, P = self.joint, self.part
        J('view', (0, 0, 0), None)
        J('root', (0, 0, 0), 'view')
        J('body', tuple(K_BODY), 'root')
        J('neck', (-0.34, -0.01, 0.50), 'body')
        J('head', tuple(K_HEAD), 'neck')
        J('hat', (-0.50, -0.02, 0.93), 'head')
        J('ear_l', (-0.48, 0.20, 0.79), 'head')
        J('ear_r', (-0.50, -0.22, 0.78), 'head')
        J('tail', (0.60, 0.0, 0.50), 'body')
        J('jaw', (-0.74, -0.02, 0.60), 'head')
        for k, (x, y) in enumerate(((-0.30, -0.12), (-0.30, 0.12), (0.46, -0.12), (0.46, 0.12))):
            J('leg%d' % k, (x, y, 0.27), 'body')

        def body_deform(p):
            # long sausage: slight belly sag, chest a touch deeper at the front
            t = p.x / 0.62
            z = p.z - 0.03 * (1 - t * t) * (1 if p.z < 0 else 0.2)
            y = p.y * (1.0 + 0.08 * max(0.0, -t))
            return Vector((p.x, y, z))
        P(bm_to_obj('k_body', se_bmesh((0.62, 0.215, 0.20), 2.5, 22, body_deform), 'k_fur', tuple(K_BODY)), 'body')
        P(se('k_belly', 'k_belly', (0.04, -0.02, 0.29), (0.46, 0.15, 0.08), 2.2), 'body')
        P(se('k_chest', 'k_belly', (-0.40, -0.04, 0.42), (0.14, 0.15, 0.15), 2.2), 'body')
        P(se('k_neckm', 'k_fur', (-0.40, -0.015, 0.53), (0.16, 0.165, 0.17), 2.3, rot=(0, -30, 0)), 'neck')
        # collar (mint ring) + bone tag
        ax = Vector((0.66, 0.0, 0.75))  # in-plane direction perpendicular to the neck axis (body -> head)
        col = polyline_curve('k_collar', [tuple(Vector((-0.43, -0.015, 0.585)) + 0.172 * math.cos(2 * math.pi * i / 40) * ax
                                                + Vector((0, 0.178 * math.sin(2 * math.pi * i / 40), 0)))
                                          for i in range(41)], 0.026, 'k_collar')
        P(col, 'neck')
        P(se('k_tag_a', 'k_tag', (-0.50, -0.175, 0.515), (0.034, 0.012, 0.016), 2.2), 'neck')
        for dx in (-0.032, 0.032):
            for dz in (-0.011, 0.011):
                P(se('k_tag_k%d%d' % (dx > 0, dz > 0), 'k_tag', (-0.50 + dx, -0.175, 0.515 + dz),
                     (0.013, 0.012, 0.013), 2.0, 6), 'neck')
        # legs: very short stubs with light paws
        for k in range(4):
            jl = self.j['leg%d' % k]
            x, y = jl.location.x, jl.location.y
            P(se('k_leg%d' % k, 'k_fur', (x, y, 0.17), (0.065, 0.065, 0.12), 2.2), 'leg%d' % k)
            P(se('k_paw%d' % k, 'k_paw', (x - 0.025, y, 0.045), (0.078, 0.068, 0.045), 2.4), 'leg%d' % k)
        # tail: tapered curve, wags about the tail joint
        pts = []
        for i in range(12):
            t = i / 11
            pts.append((0.60 + 0.24 * t, 0.0, 0.50 + 0.24 * t * t + 0.06 * t))
        tail = polyline_curve('k_tail', pts, 0.048, 'k_fur')
        tail.data.bevel_resolution = 4
        tp = tail.data.splines[0].points
        for i, pt in enumerate(tp):
            pt.radius = 1.0 - 0.75 * i / (len(tp) - 1)
        P(tail, 'tail')
        P(se('k_tailtip', 'k_belly', (0.84, 0.0, 0.80), (0.024, 0.024, 0.030), 2.0, 6), 'tail')

        # head: round skull, long muzzle, black nose, smile, tongue, eyes with brows
        P(se('k_skull', 'k_fur', tuple(K_HEAD), (0.245, 0.232, 0.225), 2.3), 'head')
        P(se('k_muzzle', 'k_belly', (-0.79, -0.03, 0.615), (0.150, 0.125, 0.100), 2.4, rot=(0, 8, 0)), 'head')
        P(se('k_muzzle_top', 'k_fur', (-0.75, -0.025, 0.685), (0.13, 0.108, 0.05), 2.4, rot=(0, 10, 0)), 'head')
        P(se('k_nose', 'k_nose', (-0.93, -0.035, 0.665), (0.055, 0.058, 0.046), 2.3), 'head')
        P(se('k_nose_hl', 'glint', (-0.945, -0.072, 0.690), (0.014, 0.004, 0.009), 2.0, 4), 'head')
        P(se('k_chin', 'k_belly', (-0.75, -0.03, 0.535), (0.10, 0.09, 0.038), 2.4), 'jaw')
        P(se('k_tongue', 'k_tongue', (-0.77, -0.085, 0.522), (0.040, 0.026, 0.046), 2.2, rot=(18, 0, 10)), 'jaw')
        bpy.context.view_layer.update()
        mb = mesh_bvh([self.parts['k_muzzle']])
        smile = [(-0.86 + 0.13 * t, 0.575 + 0.028 * (2 * t - 1) ** 2 - 0.002) for t in [i / 12 for i in range(13)]]
        pts = []
        for x, z in smile:
            hit, _ = surface_hit(mb, x, z)
            if hit is not None:
                pts.append((x, hit.y - 0.006, z))
        if len(pts) > 2:
            P(polyline_curve('k_smile', pts, 0.008, 'lash'), 'head')
        for s, side, ex, ey in ((-1, 'l', -0.728, -0.110), (1, 'r', -0.715, 0.104)):
            c = Vector((ex, ey, 0.765))
            r = (0.058, 0.052, 0.070)
            gz = self.joint('gaze_' + side, c, 'head')
            gz.scale = r
            gz.rotation_euler = (0, 0, math.radians(-50))  # looks out of the face toward the viewer
            ball = se('k_eye_' + side, None, c, r, 2.0, 12)
            ball.data.materials.append(dark_eye_mat('k_eye_' + side, gz))
            P(ball, 'head')
            for k, (dx, dz, rr) in enumerate(((-0.016, 0.020, 0.0135), (0.016, -0.018, 0.0065))):
                P(se('k_glint_%s%d' % (side, k), 'glint', (c.x + dx, c.y - r[1] * 0.92, c.z + dz), (rr, 0.003, rr * 1.15),
                     2.0, 6), 'head')
            P(se('k_brow_' + side, 'k_ear', (c.x + 0.012, c.y, c.z + 0.074), (0.016, 0.036, 0.012), 2.4,
                 rot=(-12 * s, 0, 0)), 'head')
        # ears: long floppy; the screen-near ear hangs out under the hat brim
        P(se('k_ear_l', 'k_ear', (-0.48, 0.225, 0.61), (0.072, 0.034, 0.185), 2.2, rot=(14, -6, 0)), 'ear_l')
        P(se('k_ear_r', 'k_ear', (-0.50, -0.255, 0.595), (0.074, 0.034, 0.200), 2.2, rot=(-16, 6, 0)), 'ear_r')
        # oversized orange hard hat, tilted to one side
        hc = Vector((-0.50, -0.02, 0.93))
        P(dome('k_dome', 'k_hat', tuple(hc), (0.285, 0.272, 0.21), 2.35, 0.02), 'hat')
        bm = se_bmesh((0.06, 0.28, 0.22), 2.8, 12)
        bisect_keep(bm, Vector((0, 0, 0)), Vector((0, 0, -1)), 'inner')
        P(bm_to_obj('k_ridge', bm, 'k_hat', tuple(hc)), 'hat')
        P(ring_brim('k_brim', 'k_hat_brim', hc + Vector((0, 0, -0.016)), 0.318, 0.028, 0.035, 0.018), 'hat')
        self.j['hat'].rotation_euler = (math.radians(16), math.radians(14), math.radians(10))  # slid back and to the near side

    def apply(self, p):
        R = math.radians
        self.reset()
        g = p.get
        j = self.j
        j['view'].rotation_euler = (0, 0, R(g('view_yaw', 30.0)))
        sq = g('squash', 1.0)
        side = 1.0 / math.sqrt(sq)
        j['root'].location = (g('root_x', 0.0), 0.0, g('root_z', 0.0))
        j['root'].scale = (side, side, sq)
        j['body'].rotation_euler = (R(g('body_roll', 0.0)), R(g('body_pitch', 0.0)), 0)
        j['body'].scale = (1.0, 1.0 + g('breath', 0.0), 1.0 + g('breath', 0.0))
        j['neck'].rotation_euler = (0, R(g('neck_pitch', 0.0)), 0)
        j['head'].rotation_euler = (R(g('head_roll', 0.0)), R(g('head_pitch', 0.0)), R(g('head_yaw', 0.0)))
        j['jaw'].rotation_euler = (0, R(g('jaw', 0.0)), 0)
        j['tail'].rotation_euler = (R(g('tail_wag', 0.0) * 0.35), R(-g('tail_lift', 0.0)), R(g('tail_wag', 0.0)))
        j['ear_l'].rotation_euler = (R(g('ear_l', 0.0)), 0, 0)
        j['ear_r'].rotation_euler = (R(g('ear_r', 0.0)), 0, 0)
        hr = self.rest['hat']
        j['hat'].rotation_euler = (hr[1].x + R(g('hat_pitch', 0.0)), hr[1].y + R(g('hat_roll', 0.0)), hr[1].z)
        j['hat'].location = (hr[0].x, hr[0].y, hr[0].z + g('hat_lift', 0.0))
        for k in range(4):
            j['leg%d' % k].rotation_euler = (0, R(g('leg%d' % k, 0.0)), 0)
        for sd in ('l', 'r'):
            gr = self.rest['gaze_' + sd][1]
            j['gaze_' + sd].rotation_euler = (gr.x - R(g('gaze_z', 0.0)), 0, gr.z + R(g('gaze_x', 0.0)))
        blink = g('blink', 0.0)
        for sd in ('l', 'r'):
            e = self.parts['k_eye_' + sd]
            e.scale = (1.0, 1.0, max(0.12, 1.0 - blink))
        bpy.context.view_layer.update()


def build_scene(which=('tuna', 'kepce')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bk.MAT.clear()
    for k in bk.MATS:
        bk.MAT[k] = make_mat(k)
    chars = {}
    if 'tuna' in which:
        chars['tuna'] = Tuna()
    if 'kepce' in which:
        chars['kepce'] = Kepce()
    # one light rig for every character: link the lights to a parent collection that holds both
    allc = bpy.data.collections.new('crew')
    bpy.context.scene.collection.children.link(allc)
    for c in chars.values():
        bpy.context.scene.collection.children.unlink(c.coll)
        allc.children.link(c.coll)
    bk.COLL = allc
    bk.setup_world_and_lights(None)
    return chars


def _main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []

    def arg(name, default):
        return argv[argv.index(name) + 1] if name in argv else default

    out = os.path.abspath(arg('--out', 'art/3d/out/crew/turn'))
    px = float(arg('--px', '160'))
    samples = int(arg('--samples', '24'))
    os.makedirs(out, exist_ok=True)
    chars = build_scene()
    bk.setup_render(samples)
    bk.setup_camera(3.0, 3.0, 0.2, px)
    for name, ch in chars.items():
        for c2 in chars.values():
            c2.hide(c2 is ch)
        for view, yaw in (('front', 0), ('34', 30)):
            ch.apply({'view_yaw': yaw if name == 'tuna' else yaw + 10})
            bpy.context.scene.render.filepath = os.path.join(out, '%s_turn_%s.png' % (name, view))
            bpy.ops.render.render(write_still=True)
            print('RENDERED', name, view, flush=True)


if __name__ == '__main__':
    _main()
