"""B1 town structure (rig C prop): town_ch1_treehouse — the chapter 1 tree house on stilts, the same build as the v4
drawing (plank house, tiled roof with a roof ladder = under construction, porch railing, struts, hanging ladder, flag).

    blender -b --factory-startup -noaudio --python art/3d/blender/env/town.py -- --out art/3d/out/env/raw [--quick]

Frame 760 x 820 design px, pivot 0.5 0 (bottom centre = the lowest ladder rung); rendered at 1.2 px per design px.
"""
import math
import os
import sys

import bpy

sys.dont_write_bytecode = True  # no __pycache__ next to the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402


def build(sc):
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
    rope = C.principled('rope', '#7E8696', rough=0.4, metallic=0.8)
    parts = []
    # stilts, struts and the hanging ladder
    for x in (-1.1, 1.1):
        parts.append(C.superellipsoid(sc, 'strut', 0.07, 0.07, 0.75, n=3, loc=(x * 0.55, 0, -0.55), mat=trim))
        parts[-1].rotation_euler = (0, math.radians(-x * 26), 0)
    for k in range(5):
        parts.append(C.superellipsoid(sc, 'rung', 0.26, 0.07, 0.05, n=4, loc=(0, -0.05, -0.25 - k * 0.32), mat=deck))
    for x in (-0.24, 0.24):
        parts.append(C.cylinder(sc, 'rail', 0.025, 1.6, loc=(x, -0.05, -0.85), mat=rope, seg=12))
    # deck with railing
    parts.append(C.superellipsoid(sc, 'deck', 1.45, 0.8, 0.09, n=6, loc=(0, 0, 0.0), mat=deck))
    for x in (-1.35, -1.05, 1.05, 1.35):
        parts.append(C.cylinder(sc, 'post', 0.045, 0.42, loc=(x, -0.7, 0.28), mat=trim, seg=16))
    for x0, x1 in ((-1.4, -0.95), (0.95, 1.4)):
        parts.append(C.superellipsoid(sc, 'hand', (x1 - x0) / 2 + 0.05, 0.04, 0.04, n=4, loc=((x0 + x1) / 2, -0.7, 0.47), mat=trim))
    # house body
    parts.append(C.superellipsoid(sc, 'house', 0.92, 0.62, 0.78, n=10, loc=(0, 0.05, 0.86), mat=planks))
    for zz in (0.25, 1.45):
        parts.append(C.superellipsoid(sc, 'beam', 0.95, 0.65, 0.04, n=6, loc=(0, 0.05, zz), mat=trim))
    for x in (-0.94, 0.94):
        parts.append(C.superellipsoid(sc, 'corner', 0.05, 0.05, 0.8, n=4, loc=(x, -0.58, 0.86), mat=trim))
    # gable + roof slabs
    tri = [(-1.0, 0.0), (1.0, 0.0), (0.0, 0.85)]
    g = C.prism(sc, 'gable', C.ccw(tri), -0.6, 0.6, planks, planks)
    g.rotation_euler = (math.radians(90), 0, 0)
    g.location = (0, 0.05, 1.62)
    parts.append(g)
    for s in (1, -1):
        r = C.superellipsoid(sc, 'roof', 0.78, 0.82, 0.07, n=8, loc=(s * 0.58, 0.05, 2.08), mat=roof)
        r.rotation_euler = (0, math.radians(s * 40), 0)
        parts.append(r)
    parts.append(C.superellipsoid(sc, 'ridge', 0.1, 0.84, 0.08, n=3, loc=(0, 0.05, 2.5), mat=roof))
    parts.append(C.superellipsoid(sc, 'chimney', 0.1, 0.1, 0.14, n=4, loc=(-0.05, 0.05, 2.62), mat=C.principled('chim', '#C9563A', rough=0.4)))
    parts.append(C.uv_sphere(sc, 'attic', 0.1, loc=(0, -0.58, 1.95), mat=C.principled('dk', '#5A3A1E', rough=0.5), scale=(1, 0.3, 1)))
    # roof ladder (under construction)
    for k in range(5):
        parts.append(C.superellipsoid(sc, 'rr', 0.14, 0.025, 0.025, n=4, loc=(0.55 + k * 0.07, -0.3, 2.0 + k * 0.17), mat=deck))
    for dx in (-0.13, 0.13):
        t = C.superellipsoid(sc, 'rl', 0.025, 0.025, 0.5, n=4, loc=(0.69 + dx, -0.3, 2.35), mat=deck)
        t.rotation_euler = (0, math.radians(-22), 0)
        parts.append(t)
    # door, windows
    parts.append(C.superellipsoid(sc, 'door', 0.22, 0.04, 0.36, n=3.2, loc=(-0.15, -0.6, 0.6), mat=door))
    parts.append(C.uv_sphere(sc, 'knob', 0.035, loc=(0.0, -0.65, 0.6), mat=gold))
    parts.append(C.torus(sc, 'porth', 0.15, 0.035, loc=(-0.55, -0.6, 1.1), mat=trim, rot=(90, 0, 0)))
    parts.append(C.uv_sphere(sc, 'porthg', 0.13, loc=(-0.55, -0.59, 1.1), mat=glass, scale=(1, 0.3, 1)))
    parts.append(C.superellipsoid(sc, 'win', 0.26, 0.04, 0.2, n=5, loc=(0.45, -0.6, 1.08), mat=glass))
    for dx in (-0.17, 0.17):
        parts.append(C.superellipsoid(sc, 'cur', 0.08, 0.05, 0.19, n=3, loc=(0.45 + dx, -0.62, 1.08), mat=curtain))
    parts.append(C.superellipsoid(sc, 'wf', 0.3, 0.035, 0.035, n=4, loc=(0.45, -0.63, 0.86), mat=trim))
    # flag pole on the right with a pulley
    parts.append(C.cylinder(sc, 'pole', 0.03, 1.5, loc=(1.25, 0.1, 1.6), mat=trim, seg=12))
    fl = C.prism(sc, 'flag', C.ccw([(0, 0), (0.55, 0.12), (0.45, 0.22), (0.58, 0.34), (0, 0.4)]), -0.015, 0.015, flag, flag, bevel=0.01)
    fl.rotation_euler = (math.radians(90), 0, 0)
    fl.location = (1.27, 0.1, 1.95)
    parts.append(fl)
    parts.append(C.puffy(sc, 'star', C.ccw(C.star_pts(5, 0.08, 0.035)), 0.03, C.principled('st', '#FFFFFF', rough=0.3), bevel=0.008, sub=1))
    parts[-1].rotation_euler = (math.radians(90), 0, 0)
    parts[-1].location = (1.48, 0.07, 2.12)
    C.group(sc, parts, rot=(0, 0, -16))
    return parts


if __name__ == '__main__':
    if C.ARGS.wants('town_ch1_treehouse'):
        sc = C.reset()
        w, h = round(760 * 1.2), round(820 * 1.2)
        C.setup_render(sc, w, h, 96)
        C.rig_C(sc)
        cam = C.prop_camera(sc, w // (2 if C.ARGS.quick else 1), h // (2 if C.ARGS.quick else 1))
        build(sc)
        C.fit_camera(sc, cam, 0.92)
        # pivot 0.5 0: put the projected bottom of the model on the frame bottom (shift the camera up by the margin)
        bpy.context.view_layer.update()
        scale, (x0, x1, y0, y1) = C.fit_camera(sc, cam, 0.92)
        vh = scale if w < h else scale * h / w
        margin = (vh - (y1 - y0)) / 2
        R = cam.matrix_world.to_3x3().normalized()
        from mathutils import Vector
        cam.location = cam.location + (R @ Vector((0, 1, 0))) * (margin - 0.02)
        C.render(sc, os.path.join(C.ARGS.out, 'town_ch1_treehouse.png'))
    print('TOWN DONE', flush=True)
