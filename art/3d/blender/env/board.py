"""B1 board parts (docs/ART_3D.md section 3): yard floor, wall, wall cap, gap, rail, scaffold, crane hook, truck, tray.

    blender -b --factory-startup -noaudio --python art/3d/blender/env/board.py -- --out art/3d/out/env/raw [--only wall_*] [--quick]

Tiles (floor, wall, gap, rail, pipe, beam) use rig A (35 deg pitch, same light as the blocks) and are rendered as the centre
of a 3-tile context so the bevels and shadows run on across the seams; the tray uses rig D (20 deg, 9-slice: x-uniform
centre); the crane hook and the truck use rig C (3/4 prop view). Frames are rendered at 2 x 1.5 = 3 raster px per design px.
"""
import math
import os
import sys

import bpy

sys.dont_write_bytecode = True  # no __pycache__ next to the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

RS = 3.0  # render px per design px (2 x the 1.5 store scale)
OUT = C.ARGS.out


def out(name):
    return os.path.join(OUT, name + '.png')


def wood_mat(name, hex_):
    m = C.principled(name, hex_, rough=0.62, coat=0.12, coat_rough=0.3, spec=0.35, sss=0.0)
    C.noise_color(m, hex_, var=0.045, wave=(7.0, 4.0), bump=0.05)
    return m


def floor_yard():
    for name, col, other in [('floor_yard_a', '#E2B97A', '#D4A867'), ('floor_yard_b', '#D4A867', '#E2B97A')]:
        if not C.ARGS.wants(name):
            continue
        sc = C.reset()
        S = C.Screen(sc, 35, 120, 120, RS, 128)
        C.rig_A(sc)
        sand = C.principled('sand', '#E9C891', rough=0.9, coat=0.0, spec=0.2)
        C.noise_color(sand, '#E9C891', var=0.10, scale=180.0, bump=0.25)
        nail = C.principled('nail', '#B9C0C8', rough=0.3, coat=0.0, metallic=0.9)
        S.prism('sand', C.rrect_pts(-240, -240, 360, 360, 0), 0.0, -6, sand, sand)
        mats = {}
        gap = 4.0
        plank = (120 - 3 * gap) / 3
        for i in (-1, 0, 1):
            for j in (-1, 0, 1):
                c = col if (i + j) % 2 == 0 else other
                m = mats.setdefault(c, wood_mat('wood' + c, c))
                for k in range(3):
                    v0 = j * 120 + gap / 2 + k * (plank + gap)
                    x0, x1 = i * 120 + 3, i * 120 + 117
                    S.prism('plank', C.rrect_pts(x0, v0, x1, v0 + plank, 3), 7, 0, m, m, bevel_px=2.2, seg=3)
                    for xn in (x0 + 9, x1 - 9):
                        loc = S.at(xn, v0 + plank / 2, 7)
                        C.uv_sphere(sc, 'nail', 3.2 * C.PX, loc=loc, mat=nail, scale=(1, 1, 0.45))
        sc.render.resolution_x = round(120 * RS) // (2 if C.ARGS.quick else 1)
        sc.render.resolution_y = round(120 * RS) // (2 if C.ARGS.quick else 1)
        sc.render.film_transparent = False
        C.render(sc, out(name))


def concrete(name, hex_):
    m = C.principled(name, hex_, rough=0.82, coat=0.05, spec=0.3)
    C.noise_color(m, hex_, var=0.06, scale=90.0, bump=0.2)
    return m


def wall_seg():
    if not C.ARGS.wants('wall_seg'):
        return
    sc = C.reset()
    S = C.Screen(sc, 35, 60, 120, RS, 128)
    C.rig_A(sc)
    mortar = concrete('mortar', '#8C8579')
    b1 = concrete('brick1', '#C9C2B4')
    b2 = concrete('brick2', '#B3AB9C')
    S.prism('mortar', C.rrect_pts(0, -200, 60, 320, 0), 4, -4, mortar, mortar)
    g = 4.0
    k = 0
    for row in range(-4, 7):
        v0 = row * 40 + g / 2
        off = [0, 20, 40][row % 3]
        edges = sorted(set([0, 60] + [e for e in (off, off - 60, off + 60) if 0 < e < 60]))
        for a, b in zip(edges, edges[1:]):
            m = b1 if (k % 3) else b2
            k += 1
            S.prism('brick', C.rrect_pts(a + g / 2, v0, b - g / 2, v0 + 40 - g, 4), 16, 0, m, m, bevel_px=3.5, seg=4)
    C.render(sc, out('wall_seg'))


def hazard_mat(name, rough=0.28, period=26):
    m = C.principled(name, '#FFD43B', rough=rough, coat=0.45, coat_rough=0.08, spec=0.5)
    C.stripes_input(m, '#FFD43B', '#3A3346', period * C.PX)
    return m


def wall_cap_and_edges():
    if C.ARGS.wants('wall_cap'):
        sc = C.reset()
        S = C.Screen(sc, 35, 64, 36, RS, 128)
        C.rig_A(sc)
        hz = hazard_mat('hz')
        dark = C.principled('dark', '#2B2536', rough=0.4, coat=0.3)
        lip = 12
        S.prism('cap', C.rrect_pts(2, lip, 62, 35, 7), S.lip_z(lip), 0, hz, dark, bevel_px=4, seg=4)
        C.render(sc, out('wall_cap'))
    if C.ARGS.wants('gap_edge'):
        sc = C.reset()
        S = C.Screen(sc, 35, 60, 22, RS, 128)
        C.rig_A(sc)
        hz = hazard_mat('hz', period=20)
        dark = C.principled('dark', '#2B2536', rough=0.4, coat=0.3)
        lip = 7
        S.prism('edge', C.rrect_pts(-30, lip, 90, 21, 0), S.lip_z(lip), 0, hz, dark, bevel_px=2.5, seg=3)
        C.render(sc, out('gap_edge'))


def gap_seg():
    if not C.ARGS.wants('gap_seg'):
        return
    sc = C.reset()
    S = C.Screen(sc, 35, 60, 120, RS, 128)
    C.rig_A(sc)
    floor = C.principled('gfloor', '#2A2670', rough=0.7, coat=0.0, spec=0.3)
    C.gradient_input(floor, [(0.0, '#1D1A55'), (0.5, '#2E2A78'), (1.0, '#1D1A55')], axis='X', lo=0.0, hi=0.5)
    S.prism('floor', C.rrect_pts(0, -200, 60, 320, 0), -20, -30, floor, floor)
    # the cut faces of the wall at both sides of the opening: thin concrete jambs
    jamb = concrete('jamb', '#7E776C')
    for x0, x1 in ((-30, 3), (57, 90)):
        S.prism('jamb', C.rrect_pts(x0, -200, x1, 320, 0), 4, -30, jamb, jamb, bevel_px=2, seg=3)
    C.render(sc, out('gap_seg'))


def galv(name='galv'):
    return C.principled(name, '#C9D1DA', rough=0.33, coat=0.2, metallic=0.8, spec=0.5)


def rail_pipe_beam():
    if C.ARGS.wants('rail_seg'):
        sc = C.reset()
        S = C.Screen(sc, 35, 120, 16, RS, 128)
        C.rig_A(sc)
        g = galv()
        tie = C.principled('tie', '#5A4F78', rough=0.5, coat=0.2)
        # rail: a rounded bar along x on two low ties per tile
        r = 4.2
        for i in (-1, 0, 1):
            for xt in (30, 90):
                S.prism('tie', C.rrect_pts(i * 120 + xt - 6, 2, i * 120 + xt + 6, 14, 3), 5, 0, tie, tie, bevel_px=2, seg=3)
        y = S.y(8, 9)
        C.cylinder(sc, 'rail', r * C.PX, 4 * 120 * C.PX, loc=(60 * C.PX, y, 9 * C.PX), mat=g, rot=(0, 90, 0), seg=32)
        C.render(sc, out('rail_seg'))
    if C.ARGS.wants('scaffold_pipe'):
        sc = C.reset()
        S = C.Screen(sc, 35, 16, 120, RS, 128)
        C.rig_A(sc)
        g = galv()
        r = 7.0
        # a pipe along the screen vertical: axis along model y at height r
        y0 = S.y(-200, 0)
        y1 = S.y(320, 0)
        C.cylinder(sc, 'pipe', r * C.PX, (y1 - y0), loc=(8 * C.PX, (y0 + y1) / 2, r * C.PX), mat=g, rot=(90, 0, 0), seg=40)
        C.render(sc, out('scaffold_pipe'))
    if C.ARGS.wants('beam_seg'):
        sc = C.reset()
        S = C.Screen(sc, 35, 120, 16, RS, 128)
        C.rig_A(sc)
        hz = hazard_mat('hz', period=22)
        dark = C.principled('dark', '#2B2536', rough=0.4, coat=0.3)
        lip = 5
        S.prism('beam', C.rrect_pts(-240, lip, 360, 15, 0), S.lip_z(lip), 0, hz, dark, bevel_px=2.5, seg=3)
        C.render(sc, out('beam_seg'))
    if C.ARGS.wants('scaffold_clamp'):
        sc = C.reset()
        S = C.Screen(sc, 35, 28, 24, RS * 1.5, 128)
        C.rig_A(sc)
        org = C.principled('clamp', '#EC8D20', rough=0.3, coat=0.5, sss=0.08)
        bolt = galv('bolt')
        lip = 6
        S.prism('clamp', C.rrect_pts(1.5, lip, 26.5, 23, 6), S.lip_z(lip), 0, org, org, bevel_px=3, seg=4)
        loc = S.at(14, 14.5, S.lip_z(lip))
        C.cylinder(sc, 'bolt', 3.4 * C.PX, 4 * C.PX, loc=(loc[0], loc[1], loc[2] + 1 * C.PX), mat=bolt, bevel=1 * C.PX)
        C.render(sc, out('scaffold_clamp'))


def board_tray():
    if not C.ARGS.wants('board_tray'):
        return
    sc = C.reset()
    S = C.Screen(sc, 20, 240, 240, RS, 96)
    C.rig_D(sc)
    face = C.principled('tray', '#5247B8', rough=0.28, coat=0.3, coat_rough=0.1)
    C.gradient_input(face, [(0.0, '#3F35A0'), (1.0, '#5247B8')], axis='Y', lo=-0.2, hi=2.0)
    rim = C.principled('rim', '#221B66', rough=0.35, coat=0.3)
    groove = C.principled('groove', '#2E2778', rough=0.4, coat=0.2)
    lip = 16
    zt = S.lip_z(lip)
    seg = 12
    outer = C.rrect_pts(0, lip, 240, 240, 40, seg)
    inner = C.rrect_pts(48, 64, 192, 192, 20, seg)
    # outer dark rim (2 px wider, a hair lower) + face ring + groove ring inside
    S.ring('rim', C.rrect_pts(-1, lip - 3, 241, 240.5, 41, seg), C.rrect_pts(50, 66, 190, 190, 18, seg), zt - 3, 0, mat_top=rim, mat_side=rim, bevel_px=3, open_hole=True)
    S.ring('face', outer, inner, zt, 0, mat_top=face, mat_side=rim, bevel_px=6, seg=5, open_hole=True)
    S.ring('groove', C.rrect_pts(44, 60, 196, 196, 24, seg), C.rrect_pts(48, 64, 192, 192, 20, seg), zt + 0.5, 0, mat_top=groove, mat_side=groove,
           bevel_px=1.5, open_hole=True)
    screw = galv('screw')
    for x, v in ((20, 220), (220, 220), (20, 36), (220, 36)):
        loc = S.at(x, v, zt)
        C.cylinder(sc, 'screw', 5.5 * C.PX, 3 * C.PX, loc=(loc[0], loc[1], loc[2] + 1 * C.PX), mat=screw, bevel=1.4 * C.PX, bevel_seg=3)
    C.write_meta('board_tray', {'shadow': {'dy': 0, 'blur': 0, 'alpha': 0}})
    C.render(sc, out('board_tray'))


def crane_hook():
    if not C.ARGS.wants('crane_hook'):
        return
    sc = C.reset()
    C.setup_render(sc, round(96 * RS), round(160 * RS), 128)
    C.rig_C(sc)
    orange = C.principled('org', '#EC8D20', rough=0.28, coat=0.5, sss=0.08)
    chrome = C.principled('chrome', '#D8DEE6', rough=0.16, coat=0.0, metallic=1.0)
    steel = C.principled('steel', '#8E96A3', rough=0.35, metallic=0.9)
    hz = hazard_mat('hz', period=16)
    hz.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.3
    # cable from the top
    C.cylinder(sc, 'cable', 0.018, 1.6, loc=(0, 0, 1.75), mat=steel, seg=16)
    # pulley block: rounded box + side wheel bolt
    blk = C.superellipsoid(sc, 'block', 0.26, 0.17, 0.2, n=3.2, loc=(0, 0, 0.82), mat=orange)
    band = C.superellipsoid(sc, 'band', 0.268, 0.178, 0.06, n=3.2, loc=(0, 0, 0.7), mat=hz)
    C.cylinder(sc, 'axle', 0.085, 0.06, loc=(0, -0.17, 0.84), mat=chrome, rot=(90, 0, 0), bevel=0.012)
    C.cylinder(sc, 'axle_in', 0.04, 0.07, loc=(0, -0.18, 0.84), mat=steel, rot=(90, 0, 0), bevel=0.01)
    # shank + J hook (tube)
    C.cylinder(sc, 'shank', 0.035, 0.22, loc=(0, 0, 0.53), mat=chrome, seg=24)
    C.torus(sc, 'eye', 0.06, 0.022, loc=(0, 0, 0.62), mat=chrome, rot=(90, 0, 0))
    pts = [(0, 0, 0.44), (0, 0, 0.2), (-0.04, 0, 0.05), (-0.16, 0, -0.02), (-0.27, 0, 0.06), (-0.28, 0, 0.18)]
    C.curve_tube(sc, 'hook', pts, 0.045, chrome)
    C.uv_sphere(sc, 'tip', 0.05, loc=(-0.28, 0, 0.2), mat=chrome)
    # frame: 0.78 BU wide x 1.30 tall; the cable leaves the top edge (pivot 0.5 1), the hook tip ends ~6 % above the bottom
    C.camera_look(sc, (0, -10 * math.cos(math.radians(28)), 10 * math.sin(math.radians(28)) + 0.5), (0, 0, 0.5), 0.78, round(96 * RS), round(160 * RS))
    # place the top of the cable at the top edge (pivot 0.5 1)
    C.render(sc, out('crane_hook'))


def truck():
    if not C.ARGS.wants('truck'):
        return
    sc = C.reset()
    C.setup_render(sc, round(360 * RS / 1.5 * 1.0 * 2), round(200 * RS / 1.5 * 1.0 * 2), 128)
    C.rig_C(sc)
    cab_c = C.principled('cab', '#FFC21A', rough=0.25, coat=0.6, sss=0.06)
    C.gradient_input(cab_c, [(0.0, '#F4A30E'), (1.0, '#FFD43B')], axis='Z', lo=0.3, hi=1.3, space='Object')
    bed_c = C.principled('bed', '#EC6A2B', rough=0.28, coat=0.55, sss=0.06)
    dark = C.principled('chassis', '#3A3346', rough=0.45, coat=0.2)
    glass = C.principled('glass', '#8FD3FF', rough=0.05, coat=1.0, spec=0.8)
    tyre = C.principled('tyre', '#2A2632', rough=0.6, coat=0.1)
    hub = C.principled('hub', '#E6EAF0', rough=0.25, metallic=0.8)
    lamp = C.principled('lamp', '#FFF6C8', rough=0.1, coat=1.0, emission='#FFF2B0', emission_strength=1.5)
    beacon = C.principled('beacon', '#FF7A1A', rough=0.1, coat=1.0, sss=0.3, emission='#FF8A2A', emission_strength=0.6)
    root = bpy.data.objects.new('root', None)
    sc.collection.objects.link(root)
    parts = []
    parts.append(C.superellipsoid(sc, 'chassis', 1.55, 0.42, 0.1, n=4, loc=(0.0, 0, 0.36), mat=dark))
    parts.append(C.superellipsoid(sc, 'bed', 0.95, 0.46, 0.34, n=7, loc=(-0.55, 0, 0.78), mat=bed_c))
    rib = C.principled('rib', '#D9551E', rough=0.3, coat=0.5)
    for k in range(3):
        parts.append(C.superellipsoid(sc, 'rib', 0.958, 0.468, 0.022, n=7, loc=(-0.55, 0, 0.64 + k * 0.15), mat=rib))
    parts.append(C.superellipsoid(sc, 'rim', 0.97, 0.48, 0.035, n=7, loc=(-0.55, 0, 1.11), mat=C.principled('rimc', '#FFC21A', rough=0.25, coat=0.6)))
    parts.append(C.superellipsoid(sc, 'cab', 0.5, 0.44, 0.5, n=3.6, loc=(0.92, 0, 0.92), mat=cab_c))
    parts.append(C.superellipsoid(sc, 'hood', 0.32, 0.42, 0.22, n=3.6, loc=(1.32, 0, 0.62), mat=cab_c))
    parts.append(C.superellipsoid(sc, 'win', 0.04, 0.34, 0.2, n=4, loc=(1.38, 0, 1.08), mat=glass))
    parts.append(C.superellipsoid(sc, 'winside', 0.3, 0.02, 0.18, n=4, loc=(0.95, -0.44, 1.1), mat=glass))
    parts.append(C.superellipsoid(sc, 'bumper', 0.08, 0.44, 0.07, n=4, loc=(1.66, 0, 0.42), mat=hub))
    parts.append(C.uv_sphere(sc, 'lamp', 0.07, loc=(1.63, -0.3, 0.62), mat=lamp))
    parts.append(C.uv_sphere(sc, 'lamp2', 0.07, loc=(1.63, 0.3, 0.62), mat=lamp))
    parts.append(C.superellipsoid(sc, 'beacon', 0.1, 0.1, 0.09, n=2.4, loc=(0.92, 0, 1.47), mat=beacon))
    for x in (-1.0, -0.35, 1.2):
        for yy in (-0.42, 0.42):
            parts.append(C.cylinder(sc, 'tyre', 0.27, 0.2, loc=(x, yy, 0.27), mat=tyre, rot=(90, 0, 0), bevel=0.07, bevel_seg=5, seg=48))
            parts.append(C.cylinder(sc, 'hub', 0.12, 0.22, loc=(x, yy, 0.27), mat=hub, rot=(90, 0, 0), bevel=0.03, bevel_seg=3))
    for p_ in parts:
        p_.parent = root
    root.rotation_euler = (0, 0, math.radians(-18))
    w = round(360 * 3)
    h = round(200 * 3)
    sc.render.resolution_x = w // (2 if C.ARGS.quick else 1)
    sc.render.resolution_y = h // (2 if C.ARGS.quick else 1)
    # pivot 0.5 0.05: the wheel bottoms at 5 % of the frame height
    C.camera_look(sc, (0.1, -10 * math.cos(math.radians(24)), 10 * math.sin(math.radians(24)) + 0.75), (0.1, 0, 0.75), 3.75, w, h)
    C.write_meta('truck', {'ellipse': {'cx': 180, 'cy': 14, 'rx': 150, 'ry': 12, 'alpha': 0.35, 'blur': 6}})
    C.render(sc, out('truck'))


if __name__ == '__main__':
    floor_yard()
    wall_seg()
    wall_cap_and_edges()
    gap_seg()
    rail_pipe_beam()
    board_tray()
    crane_hook()
    truck()
    print('BOARD DONE', flush=True)
