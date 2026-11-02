"""B1 blocks (docs/ART_3D.md section 2; rig A_tile): 16 neighbour-mask tiles x 8 colours, 16 corner fills x 8 colours,
colour-blind stud caps, the holdable gloss, the placed mortar overlay and the four loading-screen bricks.

    blender -b --factory-startup -noaudio --python art/3d/blender/env/blocks.py -- --out art/3d/out/env/raw [--only blk_y_*] [--quick]

Mask bits (v4, Presentation.Core Atlas.Body / BoardPose.NeighbourMask): 1 left, 2 right, 4 down (y - 1), 8 up (y + 1).
Every cell is built in screen space: the cell's top face covers screen rows [j + 0.2, j + 1] and its front lip [j, j + 0.2]
when it has no neighbour below; with one below the top face reaches down to j and the lip belongs to the lower cell. Cells
are voxel-split (main box + 0.2-row extension) so that every shared face between two cells is coincident: join, weld,
delete the coincident pairs, dissolve, then round the vertical corners (bevel weight) and every outer edge (angle bevel).
No exact boolean, no harden normals (ART_3D 2.3). Tiles are rendered from a 3 x 3 context with the camera framing only the
centre cell (360 px = 2 x 180), corner fills from the 2 x 2 context incl. the diagonal (96 px = 2 x 48).
Writes <out>/<frame>.png (2x) and <out>/<frame>.meta.json (anchor, studD, symD for tiles).
"""
import math
import os
import sys

import bmesh
import bpy

sys.dont_write_bytecode = True  # no __pycache__ next to the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

PITCH = 35.0
P = math.radians(PITCH)
LIP = 24 / 120
T = LIP / math.sin(P)
C35 = math.cos(P)
CF = 32 / 120
STUD_R = 0.225
STUD_H = 0.12
SYM_BOX = 0.37
PALETTE = {'w': '#764012', 'y': '#FFE23E', 'g': '#44D088', 'r': '#CF404E', 'o': '#EC8D20', 'c': '#C8CFDA', 'b': '#2984DE', 'p': '#5D3AAE'}
ROUGH = {'w': 0.45, 'c': 0.42, 'r': 0.32, 'p': 0.32}
SSS = {'y': 0.12, 'g': 0.12, 'o': 0.10, 'r': 0.08, 'b': 0.08, 'c': 0.06, 'p': 0.06, 'w': 0.04}
INK_WHITE = {'w', 'r', 'b', 'p'}
INK_BOOST = {'r': 0.3}
CORNERS = {'bl': (-1, -1), 'br': (1, -1), 'tl': (-1, 1), 'tr': (1, 1)}
TILE_PX = 360
CF_PX = 96
SYM_DIR = os.path.join(C.ROOT, 'art', '3d', 'out', 'env', 'sym')


def mask_cells(m):
    cells = [(0, 0)]
    if m & 1:
        cells.append((-1, 0))
    if m & 2:
        cells.append((1, 0))
    if m & 4:
        cells.append((0, -1))
    if m & 8:
        cells.append((0, 1))
    return cells


def cf_corners(m):
    out = []
    for k, (dx, dy) in CORNERS.items():
        hx = (m & 1) if dx < 0 else (m & 2)
        hy = (m & 4) if dy < 0 else (m & 8)
        if hx and hy:
            out.append(k)
    return out


def block_mats(code):
    base = PALETTE[code]
    body = C.principled('blk_' + code, base, rough=ROUGH.get(code, 0.30), coat={'w': 0.0, 'c': 0.25}.get(code, 0.40),
                        coat_rough=0.06, spec=0.2 if code == 'w' else 0.5, sss=SSS[code])
    stud = body.copy()
    stud.name = 'stud_' + code
    ink = C.mix_hex(base, '#FFFFFF', 0.92) if code in INK_WHITE else C.scale_hex(base, 0.40)
    if code in INK_BOOST:  # R measured 2.99:1 with 92 % white ink: pure white + a faint ink glow -> >= 3:1 (ART_3D 2.5)
        ink = '#FFFFFF'
    nt = stud.node_tree
    b = nt.nodes['Principled BSDF']
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Location'].default_value = (0.5, 0.5, 0)
    mp.inputs['Scale'].default_value = (1 / SYM_BOX, 1 / SYM_BOX, 1)
    # Mapping (POINT): out = (in * scale) + location -> object xy / box + 0.5
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    img = nt.nodes.new('ShaderNodeTexImage')
    img.image = bpy.data.images.load(os.path.join(SYM_DIR, 'sym_' + code + '.png'))
    img.image.colorspace_settings.name = 'Non-Color'
    img.extension = 'CLIP'
    img.interpolation = 'Cubic'
    nt.links.new(mp.outputs['Vector'], img.inputs['Vector'])
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['A'].default_value = C.lin(base)
    mix.inputs['B'].default_value = C.lin(ink)
    nt.links.new(img.outputs['Color'], mix.inputs['Factor'])
    nt.links.new(mix.outputs['Result'], b.inputs['Base Color'])
    if code in INK_BOOST:
        b.inputs['Emission Color'].default_value = (1, 1, 1, 1)
        em = nt.nodes.new('ShaderNodeMath')
        em.operation = 'MULTIPLY'
        em.inputs[1].default_value = INK_BOOST[code]
        nt.links.new(img.outputs['Color'], em.inputs[0])
        nt.links.new(em.outputs[0], b.inputs['Emission Strength'])
    # ink: no subsurface glow, a touch rougher (printed / moulded symbol)
    bump = nt.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.55
    bump.inputs['Distance'].default_value = 0.012
    nt.links.new(img.outputs['Color'], bump.inputs['Height'])
    nt.links.new(bump.outputs['Normal'], b.inputs['Normal'])
    return body, stud


def union_boxes(bm):
    """Deletes coincident face pairs (shared walls between cells) BEFORE welding (weld_verts would silently keep one
    of the pair as an internal face), then welds and dissolves."""
    seen = {}
    for fc in bm.faces:
        key = tuple(sorted((round(v.co.x, 4), round(v.co.y, 4), round(v.co.z, 4)) for v in fc.verts))
        seen.setdefault(key, []).append(fc)
    dup = [fc for fs in seen.values() if len(fs) > 1 for fc in fs]
    bmesh.ops.delete(bm, geom=dup, context='FACES_ONLY')
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    loose = [e for e in bm.edges if not e.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='EDGES')
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1), verts=bm.verts, edges=bm.edges)
    bm.normal_update()


def box_faces(bm, x0, x1, y0, y1, z0, z1):
    v = [bm.verts.new(c) for c in [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]]
    for f in [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]:
        bm.faces.new([v[i] for i in f])


def slab(sc, name, cells, ox, oy, mat, z0=0.0, t=T):
    cs = set(cells)
    bm = bmesh.new()
    for (i, j) in cells:
        x0, x1 = ox + i, ox + i + 1
        y_main0, y_main1 = (oy + j) / C35, (oy + j + 1 - LIP) / C35
        box_faces(bm, x0, x1, y_main0, y_main1, z0, z0 + t)
        if (i, j - 1) in cs:
            box_faces(bm, x0, x1, (oy + j - LIP) / C35, y_main0, z0, z0 + t)
    union_boxes(bm)
    bw = bm.edges.layers.float.new('bevel_weight_edge')
    for e in bm.edges:
        d = (e.verts[1].co - e.verts[0].co)
        if d.length > 1e-6 and abs(d.normalized().z) > 0.99:
            e[bw] = 1.0
    o = C.mesh_obj(sc, name, bm)
    o.data.materials.append(mat)
    b1 = o.modifiers.new('round_corners', 'BEVEL')
    b1.limit_method = 'WEIGHT'
    b1.width = 0.11
    b1.segments = 6
    b1.profile = 0.5
    b2 = o.modifiers.new('bevel', 'BEVEL')
    b2.limit_method = 'ANGLE'
    b2.angle_limit = math.radians(30)
    b2.width = 0.10
    b2.segments = 5
    b2.profile = 0.7
    wn = o.modifiers.new('wn', 'WEIGHTED_NORMAL')  # big flat faces keep flat normals (no n-gon shading streaks)
    wn.mode = 'FACE_AREA'
    wn.weight = 100
    wn.keep_sharp = True
    for p in o.data.polygons:
        p.use_smooth = True
    o.data.use_auto_smooth = True
    o.data.auto_smooth_angle = math.radians(30)
    return o


def stud(sc, name, x, v_base, mat, z_top, r=STUD_R, h=STUD_H):
    """Stud whose base centre shows at screen (x, v_base) on a face at height z_top. Object origin = top cap centre."""
    y = (v_base - z_top * math.sin(P)) / C35
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=64, radius1=r, radius2=r * 0.97, depth=h)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, -h / 2))  # origin at the top cap
    o = C.mesh_obj(sc, name, bm)
    o.location = (x, y, z_top + h)
    bv = o.modifiers.new('bevel', 'BEVEL')
    bv.width = 0.035
    bv.segments = 4
    bv.profile = 0.6
    bv.limit_method = 'ANGLE'
    bv.angle_limit = math.radians(30)
    o.data.materials.append(mat)
    for p in o.data.polygons:
        p.use_smooth = True
    o.data.use_auto_smooth = True
    o.data.auto_smooth_angle = math.radians(30)
    return o


def stud_screen_v(j, has_down):
    v_lo = j if has_down else j + LIP
    return (v_lo + j + 1) / 2


def piece(sc, cells, ox, oy, body, studm, with_studs=True):
    o = slab(sc, 'slab', cells, ox, oy, body)
    cs = set(cells)
    if with_studs:
        for (i, j) in cells:
            stud(sc, 'stud', ox + i + 0.5, oy + stud_screen_v(j, (i, j - 1) in cs), studm, T)
    return o


def tile_meta(m):
    has_down = bool(m & 4)
    vb = stud_screen_v(0, has_down)
    v_top = vb + STUD_H * math.sin(P)
    return {'anchorX': 0.5, 'anchorY': round(v_top, 4), 'studD': round(STUD_R * 2 * 120, 1), 'symD': round(SYM_BOX * 120, 1)}


def run_colour(code):
    names = ['blk_%s_%02d' % (code, m) for m in range(16)] + ['blk_%s_%02d_cf_%s' % (code, m, k) for m in range(16) for k in cf_corners(m)] + ['stud_' + code]
    if not any(C.ARGS.wants(n) for n in names):
        return
    sc = C.reset()
    C.setup_render(sc, TILE_PX, TILE_PX, 128)
    C.rig_A(sc)
    body, studm = block_mats(code)
    cam = C.camera_pitch(sc, PITCH, 0.5, 0.5, 1.0, TILE_PX, TILE_PX)
    jobs = []
    k = 0
    for m in range(16):
        ox = 6 * k
        piece(sc, mask_cells(m), ox, 0, body, studm)
        jobs.append(('blk_%s_%02d' % (code, m), ox + 0.5, 0.5, 1.0, TILE_PX, tile_meta(m)))
        k += 1
        for cn in cf_corners(m):
            ox = 6 * k
            dx, dy = CORNERS[cn]
            piece(sc, mask_cells(m) + [(dx, dy)], ox, 0, body, studm)
            cx = ox + (CF / 2 if dx < 0 else 1 - CF / 2)
            cv = CF / 2 if dy < 0 else 1 - CF / 2
            jobs.append(('blk_%s_%02d_cf_%s' % (code, m, cn), cx, cv, CF, CF_PX, None))
            k += 1
    # colour-blind stud cap: the same stud at 64 / 54 scale on a holdout slab, top-cap centre at the frame centre
    ox = 6 * k
    hold = C.holdout_mat()
    slab(sc, 'hold', [(0, 0)], ox - 2, -2, hold, t=T)  # a big holdout floor under the cap
    s = 64 / 54
    st = stud(sc, 'cap', ox + 0.5, 0.5, studm, T, r=STUD_R * s, h=STUD_H * s)
    # aim: the cap top centre on screen = (ox + 0.5, v)
    v_cap = st.location.y * C35 + st.location.z * math.sin(P)
    # the cap shows no symbol: swap to the plain body material
    st.data.materials[0] = body
    jobs.append(('stud_' + code, ox + 0.5, v_cap, 72 / 120 * 1.0, 216, None))
    for name, cx, cv, w, px, meta in jobs:
        if not C.ARGS.wants(name):
            continue
        cam.data.ortho_scale = w
        C.aim_pitch(cam, PITCH, cx, cv)
        q = px // 2 if C.ARGS.quick else px
        sc.render.resolution_x = q
        sc.render.resolution_y = q
        C.render(sc, os.path.join(C.ARGS.out, name + '.png'))
        if meta:
            C.write_meta(name, meta)


def run_overlays():
    # blk_gloss: the stud's specular highlight only (white, alpha from luminance in post_env)
    if C.ARGS.wants('blk_gloss'):
        sc = C.reset()
        C.setup_render(sc, 216, 216, 128)
        C.rig_A(sc, rim=0.0)
        g = C.principled('gloss', '#000000', rough=0.12, coat=1.0, coat_rough=0.03, spec=1.0)
        hold = C.holdout_mat()
        slab(sc, 'hold', [(0, 0)], -2, -2, hold)
        s = 64 / 54
        st = stud(sc, 'cap', 0.5, 0.5, g, T, r=STUD_R * s, h=STUD_H * s)
        v_cap = st.location.y * C35 + st.location.z * math.sin(P)
        C.camera_pitch(sc, PITCH, 0.5, v_cap, 72 / 120, 216, 216)
        C.render(sc, os.path.join(C.ARGS.out, 'blk_gloss.png'))
        C.write_meta('blk_gloss', {'post': 'luma_alpha'})
    # blk_locked: 4 rivets on the top face with their contact shadow (shadow catcher), transparent elsewhere
    if C.ARGS.wants('blk_locked'):
        sc = C.reset()
        C.setup_render(sc, TILE_PX, TILE_PX, 128)
        C.rig_A(sc)
        # art turn 1: small steel rivets instead of cream mortar dots (the cream dots read as a white dot grid, review)
        mortar = C.principled('rivet', '#8E96A3', rough=0.28, coat=0.3, spec=0.6, metallic=0.7)
        cat = slab(sc, 'catch', [(0, 0), (0, -1)], 0, 0, C.principled('c', '#FFFFFF', coat=0))
        cat.is_shadow_catcher = True
        for x in (0.115, 0.885):
            for v in (0.33, 0.885):
                y = (v - T * math.sin(P)) / C35
                C.uv_sphere(sc, 'dot', 0.034, loc=(x, y, T + 0.006), mat=mortar, scale=(1, 1, 0.6))
        C.camera_pitch(sc, PITCH, 0.5, 0.5, 1.0, TILE_PX, TILE_PX)
        C.render(sc, os.path.join(C.ARGS.out, 'blk_locked.png'))
    # deco bricks (loading screen): 3/4 view pieces without symbols, rig C light
    for name, code, cells in [('deco_brick_1', 'g', [(0, 0)]), ('deco_brick_i2', 'b', [(0, 0), (0, 1)]),
                              ('deco_brick_l3', 'y', [(0, 0), (0, 1), (1, 0)]), ('deco_brick_o', 'r', [(0, 0), (1, 0), (0, 1), (1, 1)])]:
        if not C.ARGS.wants(name):
            continue
        sc = C.reset()
        C.setup_render(sc, 400, 400, 128)
        C.rig_C(sc)
        body, _ = block_mats(code)
        # a real (non screen-space) brick: cells 1 x 1 x 0.42 with studs, rotated for a 3/4 view
        bm = bmesh.new()
        for (i, j) in cells:
            box_faces(bm, i, i + 1, j, j + 1, 0, 0.42)
        union_boxes(bm)
        bw = bm.edges.layers.float.new('bevel_weight_edge')
        for e in bm.edges:
            d = e.verts[1].co - e.verts[0].co
            if d.length > 1e-6 and abs(d.normalized().z) > 0.99:
                e[bw] = 1.0
        o = C.mesh_obj(sc, name, bm)
        o.data.materials.append(body)
        b1 = o.modifiers.new('rc', 'BEVEL')
        b1.limit_method = 'WEIGHT'
        b1.width = 0.12
        b1.segments = 6
        b2 = o.modifiers.new('bv', 'BEVEL')
        b2.limit_method = 'ANGLE'
        b2.width = 0.09
        b2.segments = 5
        b2.profile = 0.7
        for p in o.data.polygons:
            p.use_smooth = True
        o.data.use_auto_smooth = True
        o.data.auto_smooth_angle = math.radians(30)
        parts = [o]
        for (i, j) in cells:
            parts.append(C.cylinder(sc, 'stud', STUD_R * 1.05, 0.14, loc=(i + 0.5, j + 0.5, 0.42 + 0.07), mat=body, seg=64, bevel=0.035, bevel_seg=4))
        xs = [i for i, j in cells]
        ys = [j for i, j in cells]
        cx, cy = (min(xs) + max(xs) + 1) / 2, (min(ys) + max(ys) + 1) / 2
        root = bpy.data.objects.new('root', None)
        sc.collection.objects.link(root)
        for p_ in parts:
            p_.parent = root
            p_.location.x -= cx
            p_.location.y -= cy
        root.rotation_euler = (math.radians(0), 0, math.radians(-20))
        span = max(max(xs) - min(xs) + 1, max(ys) - min(ys) + 1)
        C.camera_look(sc, (0, -14 * math.cos(math.radians(28)), 14 * math.sin(math.radians(28)) + 0.2), (0, 0, 0.2), span * 1.42 + 0.35, 400, 400)
        C.render(sc, os.path.join(C.ARGS.out, name + '.png'))


if __name__ == '__main__':
    for code in PALETTE:
        run_colour(code)
    run_overlays()
    print('BLOCKS DONE', flush=True)
