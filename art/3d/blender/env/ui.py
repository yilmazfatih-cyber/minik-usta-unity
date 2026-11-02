"""B1 UI kit in 3D (docs/ART_3D.md section 4; rig D_ui: 20 deg pitch, pitch-only key light): buttons (normal / pressed),
round close and plus, badges, beads, capsules, HUD plates, windows, ribbon, progress, toggles, speech bubble, nav bar.

    blender -b --factory-startup -noaudio --python art/3d/blender/env/ui.py -- --out art/3d/out/env/raw [--only btn_*] [--quick]

Anatomy (ART_REFERENCE 3.1 as 3D steps, measured from the v4 SVGs): a dark OUTLINE slab (full frame, low), the BODY slab
on it (inset by the outline width; its front wall is the coloured lip), a raised PLATE (lighter gradient + gloss band)
on the body. Under the 20 deg ortho camera the front walls show as lips, so the v4 layer heights come out of real
geometry: outline lip ow_b, body lip L, plate lip dz. Every gradient runs along the screen vertical only and the key light
has no yaw, so the 9-slice centre rows / columns are uniform. Drop shadows are added in post_env.py (meta.shadow).
Frames render at 3 px per design px (post downsizes to the stored scale).
"""
import math
import os
import sys

import bpy

sys.dont_write_bytecode = True  # no __pycache__ next to the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C  # noqa: E402

RS = 3.0
PITCH = 20.0
SP = math.sin(math.radians(PITCH))


def out(name):
    return os.path.join(C.ARGS.out, name + '.png')


def gmat(name, stops, S, v0, v1, z, rough=0.26, coat=0.5, coat_rough=0.06, sss=0.0, spec=0.5, alpha=1.0, metallic=0.0, sheen=0.0):
    """Principled with a vertical (screen v) colour ramp from v0 (bottom, ramp 0) to v1 (top, ramp 1), evaluated at height z."""
    m = C.principled(name, stops[0][1], rough=rough, coat=coat, coat_rough=coat_rough, sss=sss, spec=spec, alpha=alpha, metallic=metallic, sheen=sheen)
    if len(stops) > 1:
        C.gradient_input(m, stops, axis='Y', lo=S.y(v0, z), hi=S.y(v1, z), space='Object', interp='LINEAR')
    return m


def flat(name, hex_, rough=0.3, coat=0.4, alpha=1.0, sss=0.0, spec=0.5, sheen=0.0):
    return C.principled(name, hex_, rough=rough, coat=coat, alpha=alpha, sss=sss, spec=spec, sheen=sheen)


def plate_stops(top, mid, bot, hi):
    """bottom -> top ramp with a soft gloss band under the top edge (v4 'bb' band)."""
    return [(0.0, bot), (0.45, mid), (0.70, top), (0.78, C.mix_hex(top, hi, 0.6)), (0.86, C.mix_hex(top, '#FFFFFF', 0.45)), (0.94, C.mix_hex(top, hi, 0.3)), (1.0, top)]


PAL = {
    'green': dict(plate=('#5CE83A', '#2BCA0A', '#19B205'), hi='#B8FF9A', ring=('#28BA09', '#148E04'), lip='#02630F', outline='#0A5A12'),
    'cream': dict(plate=('#FFF6D2', '#FAEDC4', '#F4E3B6'), hi='#FFFFFF', ring=('#E6DAB4', '#C3B692'), lip='#D9B878', outline='#6B4A26'),
    'red': dict(plate=('#FF6B6B', '#FE3232', '#E01F22'), hi='#FFC2C2', ring=('#EA2E2E', '#B3191B'), lip='#7C0000', outline='#A30101'),
    'orange': dict(plate=('#FFB32A', '#FF9A10', '#F08A00'), hi='#FFE6A0', ring=('#EB8E0F', '#C06E00'), lip='#B35A00', outline='#7A3A00'),
    'grey': dict(plate=('#B9B2A5', '#AFA79A', '#A39B8E'), hi='#E6E2DA', ring=('#A19A8E', '#827C72'), lip='#8C8579', outline='#5C564D'),
    'purple': dict(plate=('#6A58F8', '#5B48F7', '#4F3DE0'), hi='#B0A6FF', ring=('#5442E3', '#3F31B3'), lip='#2A1D8B', outline='#16133C'),
    'blue': dict(plate=('#2A9AE6', '#1C89D2', '#1670C0'), hi='#8FDcFF', ring=('#1A7EC1', '#125A9A'), lip='#0E489B', outline='#5343B8'),
}


def new_frame(w, h, samples=96):
    sc = C.reset()
    S = C.Screen(sc, PITCH, w, h, RS, samples)
    C.rig_D(sc)
    return sc, S


def button(name, w, h, pal, sb=6, ow=6, owb=6, L=40, R=48, ps=24, pt=18, pb=10, pr=None, dz=5, pressed=False, Lp=None):
    if not C.ARGS.wants(name):
        return
    P = PAL[pal]
    sc, S = new_frame(w, h)
    if pressed:
        Lp = Lp if Lp is not None else round(L * 0.35)
        drop = L - Lp
        top = h - drop
        L = Lp
        sb = 0 if sb <= 8 else sb
        sb = h - top - 0 if False else sb
    else:
        top = h
    # frame rows (screen v): outline lip [sb, sb+owb], outline top band, body lip, body top, plate
    zt_o = owb / SP
    zt_b = zt_o + L / SP
    zt_p = zt_b + dz / SP
    out_rect = (0, sb + owb, w, top)
    body_rect = (ow, sb + owb + ow + L, w - ow, top - ow)
    plate_rect = (ow + ps, body_rect[1] + pb, w - ow - ps, body_rect[3] - pt)
    # vertical faces are seen at ~70 deg from their normal: Fresnel would mirror the studio and wash the lip out ->
    # lips and outline sides nearly non-specular
    m_out = flat(name + '_out', P['outline'], rough=0.55, coat=0.0, spec=0.12)
    m_lip = flat(name + '_lip', P['lip'], rough=0.55, coat=0.0, spec=0.12)
    m_ring = gmat(name + '_ring', [(0.0, P['ring'][1]), (1.0, P['ring'][0])], S, body_rect[1], body_rect[3], zt_b)
    m_plate = gmat(name + '_plate', plate_stops(*P['plate'], P['hi']), S, plate_rect[1], plate_rect[3], zt_p, rough=0.22, coat=0.6, sss=0.04)
    S.prism('outline', C.rrect_pts(*out_rect, R, 12), zt_o, 0, m_out, m_out, bevel_px=2.5, seg=3)
    S.prism('body', C.rrect_pts(*body_rect, R - ow, 12), zt_b, 0, m_ring, m_lip, bevel_px=4, seg=4)
    S.prism('plate', C.rrect_pts(*plate_rect, pr if pr is not None else max(6, R - ow - ps * 0.6), 12), zt_p, zt_b - 2, m_plate, m_ring, bevel_px=min(6, dz * 1.4), seg=5)
    if not pressed:
        C.write_meta(name, {'shadow': {'dy': 7, 'blur': 4, 'alpha': 0.30}})
    C.render(sc, out(name))


def round_button(name, w, h, pal, glyph, pressed=False):
    """btn_close / btn_plus: domed disc with an embossed cream glyph (x or +)."""
    if not C.ARGS.wants(name):
        return
    P = {'red': dict(top='#FF6B6B', mid='#FE3232', bot='#E01F22', lip='#7C0000', out='#A30101', glyph='#FFF3E6'),
         'green': dict(top='#8DF06C', mid='#5BE03A', bot='#22B018', lip='#0E7A0E', out='#0C8312', glyph='#FFFFFF')}[pal]
    sc, S = new_frame(w, h)
    r_out = w / 2 - 1
    sb = 6 if w > 100 else 3
    L = (10 if w > 100 else 6) * (0.4 if pressed else 1.0)
    drop = (10 if w > 100 else 6) - L
    cx = w / 2
    cv = sb + r_out + 2 + 4 - drop if w > 100 else sb + r_out - drop
    owb = 4 if w > 100 else 2.5
    zt_o = owb / SP
    zt_b = zt_o + L / SP
    m_out = flat(name + '_o', P['out'], rough=0.55, coat=0.0, spec=0.12)
    m_lip = flat(name + '_l', P['lip'], rough=0.55, coat=0.0, spec=0.12)
    m_dome = gmat(name + '_d', [(0.0, P['bot']), (0.5, P['mid']), (0.85, P['top']), (1.0, C.mix_hex(P['top'], '#FFFFFF', 0.25))], S, cv - r_out, cv + r_out, zt_b + 20,
                  rough=0.2, coat=0.7, sss=0.06)
    S.prism('out', C.circle_pts(r_out, 72, cx, cv - L * 0.2), zt_o, 0, m_out, m_out, bevel_px=3, seg=3)
    S.prism('body', C.circle_pts(r_out - (6 if w > 100 else 3), 72, cx, cv), zt_b, 0, m_dome, m_lip, bevel_px=r_out * 0.22, seg=8, profile=0.5)
    # glyph: two crossed rounded bars (x) or a plus, embossed in cream
    g = flat(name + '_g', P['glyph'], rough=0.3, coat=0.5)
    gl = r_out * (0.95 if glyph == 'x' else 1.0)
    gw = r_out * (0.30 if glyph == 'x' else 0.32)
    for k, ang in enumerate((45, -45) if glyph == 'x' else (0, 90)):
        a = math.radians(ang)
        pts = C.rrect_pts(-gl / 2, -gw / 2, gl / 2, gw / 2, gw / 2, 8)
        pts = [(cx + x * math.cos(a) - y * math.sin(a), cv + x * math.sin(a) + y * math.cos(a)) for x, y in pts]
        # the second bar sits 1.5 px higher: coincident top faces shadow each other in Cycles (black cross centre)
        S.prism('glyph', pts, zt_b + max(6.0, r_out * 0.22) + 1.5 * k, zt_b - 2, g, g, bevel_px=min(gw * 0.3, r_out * 0.1), seg=4, profile=0.5)
    if not pressed:
        C.write_meta(name, {'shadow': {'dy': 7 if w > 100 else 3, 'blur': 4 if w > 100 else 2, 'alpha': 0.3}})
    C.render(sc, out(name))


def badges():
    if C.ARGS.wants('badge_count'):
        sc, S = new_frame(72, 76)
        m_o = flat('o', '#9C0B00', rough=0.3, coat=0.5)
        m_l = flat('l', '#A80E07', rough=0.3, coat=0.5)
        m_t = gmat('t', [(0.0, '#C8140F'), (0.55, '#DA201A'), (0.85, '#F04A3C'), (0.92, '#FF8A80'), (1.0, '#F04A3C')], S, 15, 71, 30, rough=0.18, coat=0.7, sss=0.05)
        S.prism('o', C.rrect_pts(0, 4 + 4, 72, 76, 20, 10), 4 / SP, 0, m_o, m_o, bevel_px=2)
        S.prism('b', C.rrect_pts(5, 4 + 4 + 5 + 6, 67, 71, 16, 10), (4 + 6) / SP, 0, m_t, m_l, bevel_px=5, seg=5)
        C.write_meta('badge_count', {'shadow': {'dy': 4, 'blur': 2.5, 'alpha': 0.3}})
        C.render(sc, out('badge_count'))
    if C.ARGS.wants('badge_dot'):
        sc, S = new_frame(56, 60)
        m_o = flat('o', '#9C0B00', rough=0.3, coat=0.5)
        m_t = gmat('t', [(0.0, '#C8140F'), (0.55, '#DA201A'), (0.85, '#F04A3C'), (0.93, '#FF9A90'), (1.0, '#F04A3C')], S, 6, 58, 30, rough=0.18, coat=0.7)
        S.prism('o', C.circle_pts(27.5, 64, 28, 32), 3 / SP, 0, m_o, m_o, bevel_px=2)
        S.prism('b', C.circle_pts(23, 64, 28, 33), 8 / SP, 0, m_t, m_o, bevel_px=9, seg=6, profile=0.5)
        C.write_meta('badge_dot', {'shadow': {'dy': 3, 'blur': 2, 'alpha': 0.3}})
        C.render(sc, out('badge_dot'))
    if C.ARGS.wants('bead_on'):
        sc, S = new_frame(56, 56)
        m_o = flat('o', '#7A3A00', rough=0.3, coat=0.5)
        gold = gmat('g', [(0.0, '#B35A00'), (0.2, '#F08A00'), (0.6, '#FFC21A'), (1.0, '#FFF3A0')], S, 6, 50, 20, rough=0.2, coat=0.6, metallic=0.35)
        S.prism('o', C.circle_pts(25, 64, 28, 29), 3 / SP, 0, m_o, m_o, bevel_px=2)
        S.prism('b', C.circle_pts(21, 64, 28, 30), 7 / SP, 0, gold, m_o, bevel_px=10, seg=6, profile=0.5)
        star = flat('s', '#FFFFFF', rough=0.2, coat=0.6)
        pts = [(28 + x * 13, 30 + y * 13) for x, y in C.star_pts(4, 1.0, 0.28, rot=90)]
        S.prism('star', pts, 7 / SP + 7, 7 / SP, star, star, bevel_px=1.2, seg=2)
        C.write_meta('bead_on', {'shadow': {'dy': 3, 'blur': 2, 'alpha': 0.3}})
        C.render(sc, out('bead_on'))
    if C.ARGS.wants('bead_off'):
        sc, S = new_frame(56, 56)
        rim = flat('r', '#5B4CC8', rough=0.35, coat=0.3)
        hole = flat('h', '#0B0920', rough=0.6, coat=0.0, alpha=0.85)
        S.ring('ring', C.circle_pts(24, 64, 28, 28), C.circle_pts(20.5, 64, 28, 28), 4 / SP, 0, z_floor=-2, mat_top=rim, mat_side=rim, mat_floor=hole, bevel_px=1.4)
        C.render(sc, out('bead_off'))


def capsule(name, w, h, border_r, fill_hex, alpha):
    if not C.ARGS.wants(name):
        return
    sc, S = new_frame(w, h)
    rim = gmat(name + '_r', [(0.0, '#2A2468'), (1.0, '#120F33')], S, 0, h, 3 / SP, rough=0.35, coat=0.3)
    fl = C.scale_hex(fill_hex, 1.12)
    # matte, non-reflective well floor (a glossy floor mirrors the white studio top and turns grey)
    floor = gmat(name + '_f', [(0.0, C.scale_hex(fl, 1.25)), (0.6, fl), (1.0, C.scale_hex(fl, 0.85))], S, 0, h, -6, rough=0.8, coat=0.0, spec=0.08, alpha=alpha)
    inner = flat(name + '_i', '#0A0822', rough=0.6, coat=0.0, alpha=min(1.0, alpha + 0.08))
    r = h / 2
    S.ring('cap', C.rrect_pts(0, 0, w, h, r, 12), C.rrect_pts(3, 3, w - 3, h - 3, r - 3, 12), 3 / SP, -6, z_floor=-6, mat_top=rim, mat_side=inner, mat_floor=floor, bevel_px=1.5, seg=3)
    C.render(sc, out(name))


def hud_plates():
    if C.ARGS.wants('hud_moves_plate'):
        w, h = 300, 136
        sc, S = new_frame(w, h)
        navy = flat('n', '#0F1A33', rough=0.35, coat=0.3)
        band = gmat('band', [(0.0, '#26355A'), (1.0, '#33456E')], S, 84, 130, 20)
        cream = gmat('cream', [(0.0, '#EADFA4'), (0.5, '#F9F3C3'), (0.85, '#FFFBE0'), (0.92, '#FFFFFF'), (1.0, '#FFFBE0')], S, 22, 86, 14, rough=0.25, coat=0.5, sss=0.05)
        cream_lip = flat('cl', '#C9B97A', rough=0.3, coat=0.3)
        gold = flat('gold', '#F7CD02', rough=0.25, coat=0.5, sss=0.05)
        org = flat('org', '#E07500', rough=0.3, coat=0.4)
        S.prism('outer', C.rrect_pts(0, 4 + 4, 300, 136, 30, 12), 4 / SP, 0, navy, navy, bevel_px=2.5)
        S.prism('band', C.rrect_pts(6, 88, 294, 130, 23, 12), 4 / SP + 4, 0, band, navy, bevel_px=3)
        S.prism('gold', C.rrect_pts(6, 84, 294, 92, 2, 4), 4 / SP + 2, 0, gold, org, bevel_px=1)
        S.prism('cream', C.rrect_pts(6, 16 + 6, 294, 84, 24, 12), (4 + 6) / SP, 0, cream, cream_lip, bevel_px=4, seg=4)
        C.write_meta('hud_moves_plate', {'shadow': {'dy': 6, 'blur': 4, 'alpha': 0.3}})
        C.render(sc, out('hud_moves_plate'))
    if C.ARGS.wants('hud_well'):
        w, h = 200, 136
        sc, S = new_frame(w, h)
        rim = gmat('r', [(0.0, '#3A2F9A'), (0.15, '#1A1548'), (1.0, '#0E0B2A')], S, 0, h, 4 / SP, rough=0.35, coat=0.3)
        floor = gmat('f', [(0.0, '#221C5C'), (0.7, '#161236'), (1.0, '#100D2C')], S, 0, h, -8, rough=0.8, coat=0.0, spec=0.08, alpha=0.88)
        inner = flat('i', '#07051A', rough=0.6, coat=0.0, alpha=0.92)
        S.ring('well', C.rrect_pts(0, 0, w, h, 28, 12), C.rrect_pts(4, 4, w - 4, h - 4, 24, 12), 4 / SP, -8, z_floor=-8, mat_top=rim, mat_side=inner, mat_floor=floor, bevel_px=1.5)
        C.render(sc, out('hud_well'))


def panels():
    if C.ARGS.wants('panel_window'):
        w, h = 320, 420
        sc, S = new_frame(w, h)
        sb = 8
        outl = flat('o', '#221380', rough=0.35, coat=0.3)
        body = gmat('b', [(0.0, '#33229E'), (1.0, '#3F2CBA')], S, sb, h, 20, rough=0.3, coat=0.45)
        lipm = flat('l', '#2A1C8F', rough=0.3, coat=0.35)
        groove = flat('g', '#27198A', rough=0.5, coat=0.2)
        inner = gmat('in', [(0.0, '#3726A2'), (1.0, '#4231B8')], S, sb + 30, h - 31, 30, rough=0.3, coat=0.45)
        card_lip = flat('cl', '#261883', rough=0.35, coat=0.3)
        card = gmat('card', [(0.0, '#4A3AC7'), (0.85, '#523ED5'), (0.93, '#6A58E8'), (1.0, '#523ED5')], S, 143, 367, 34, rough=0.26, coat=0.5)
        owb = 6
        zt_o = owb / SP
        L = 12
        zt_b = zt_o + L / SP
        S.prism('outline', C.rrect_pts(0, sb + owb, w, h, 64, 14), zt_o, 0, outl, outl, bevel_px=3)
        S.ring('body', C.rrect_pts(7, sb + owb + 7 + L, w - 7, h - 7, 58, 14), C.rrect_pts(23, sb + owb + 7 + L + 16, w - 23, h - 23, 44, 14), zt_b, 0,
               z_floor=zt_b - 6 / SP * 0.5, mat_top=body, mat_side=lipm, mat_floor=groove, bevel_px=4, seg=4)
        S.prism('inner', C.rrect_pts(31, sb + owb + 7 + L + 24, w - 31, h - 31, 38, 14), zt_b + 2 / SP, zt_b - 8, inner, card_lip, bevel_px=3, seg=3)
        S.prism('card', C.rrect_pts(53, 143, w - 53, 367, 28, 12), zt_b + 2 / SP + 14 / SP, zt_b, card, card_lip, bevel_px=5, seg=4)
        # top edge highlight strip (v4: #7B6BF2 line under the top edge)
        hl = C.emission_mat('hl', '#8E80FF', 0.6)
        S.prism('hl', C.rrect_pts(40, h - 13, w - 40, h - 10, 1.5, 4), zt_b + 0.6, zt_b - 1, hl, hl)
        C.write_meta('panel_window', {'shadow': {'dy': 10, 'blur': 7, 'alpha': 0.4}})
        C.render(sc, out('panel_window'))
    if C.ARGS.wants('panel_plate'):
        w, h = 200, 200
        sc, S = new_frame(w, h)
        outl = flat('o', '#352496', rough=0.35, coat=0.3)
        lipm = flat('l', '#6B5DE0', rough=0.3, coat=0.35)
        top = gmat('t', [(0.0, '#8173F4'), (0.8, '#8C7DFE'), (0.9, '#A99EFF'), (1.0, '#8C7DFE')], S, 16, 194, 30, rough=0.26, coat=0.5)
        S.prism('o', C.rrect_pts(0, 6, w, h, 36, 12), 6 / SP, 0, outl, outl, bevel_px=2.5)
        S.prism('b', C.rrect_pts(6, 6 + 6 + 10, w - 6, h - 6, 30, 12), 16 / SP, 0, top, lipm, bevel_px=5, seg=4)
        C.render(sc, out('panel_plate'))
    if C.ARGS.wants('panel_card'):
        w, h = 200, 200
        sc, S = new_frame(w, h)
        outl = flat('o', '#D9B878', rough=0.35, coat=0.3)
        lipm = flat('l', '#E9D9A6', rough=0.3, coat=0.35)
        top = gmat('t', [(0.0, '#F2E6BA'), (0.8, '#F7EDC6'), (0.9, '#FFF9E0'), (1.0, '#F7EDC6')], S, 12, 190, 20, rough=0.3, coat=0.4, sss=0.04)
        S.prism('o', C.rrect_pts(0, 6, w, h, 36, 12), 4 / SP, 0, outl, outl, bevel_px=2)
        S.prism('b', C.rrect_pts(4, 6 + 4 + 6, w - 4, h - 4, 32, 12), 10 / SP, 0, top, lipm, bevel_px=4, seg=4)
        C.render(sc, out('panel_card'))


def ribbon():
    if not C.ARGS.wants('ribbon_title'):
        return
    w, h = 520, 150
    sc, S = new_frame(w, h)
    tail = gmat('tail', [(0.0, '#C46A00'), (1.0, '#F0A000')], S, 4, 120, 10, rough=0.35, coat=0.3, sheen=0.6)
    tail_side = flat('ts', '#8A3A06', rough=0.4, coat=0.2)
    fold = flat('fold', '#7A2E06', rough=0.5, coat=0.1)
    edge = flat('edge', '#B8430A', rough=0.35, coat=0.3)
    lipm = flat('lip', '#D96A00', rough=0.3, coat=0.35, sheen=0.4)
    face = gmat('face', [(0.0, '#FFAD00'), (0.5, '#FFD119'), (0.8, '#FFE96A'), (0.88, '#FFF6B8'), (1.0, '#FFE96A')], S, 46, 130, 30, rough=0.3, coat=0.35, sheen=0.5, sss=0.05)
    # tails (v4: notched flags behind the band, folds under its ends)
    for sgn in (1, -1):
        def X(x):
            return x if sgn == 1 else w - x
        pts = [(X(84), 10), (X(8), 10), (X(34), 62), (X(8), 114), (X(84), 114)]
        S.prism('tail', C.ccw(pts), 8 / SP, 0, tail, tail_side, bevel_px=3, seg=3)
        S.prism('fold', C.ccw([(X(64), 32), (X(86), 10), (X(86), 32)]), 8 / SP + 2, 0, fold, fold)
    S.prism('edge', C.rrect_pts(64, 32, 456, 136, 18, 10), 6 / SP, 0, edge, edge, bevel_px=2.5)
    S.prism('band', C.rrect_pts(70, 38 + 8 + 6, 450, 130, 13, 10), (6 + 8) / SP, 0, face, lipm, bevel_px=4, seg=4)
    C.write_meta('ribbon_title', {'shadow': {'dy': 6, 'blur': 4, 'alpha': 0.28}})
    C.render(sc, out('ribbon_title'))


def progress():
    if C.ARGS.wants('progress_track'):
        w, h = 200, 84
        sc, S = new_frame(w, h)
        org = gmat('o', [(0.0, '#D97A00'), (0.8, '#F79900'), (1.0, '#FFD36B')], S, 4, 84, 6 / SP, rough=0.25, coat=0.55)
        well = gmat('w', [(0.0, '#0A5A8E'), (0.6, '#06436D'), (1.0, '#032B48')], S, 4, 84, -4, rough=0.5, coat=0.2)
        inner = flat('i', '#021E33', rough=0.6, coat=0.0)
        S.ring('t', C.rrect_pts(0, 4, w, h, 40, 14), C.rrect_pts(8, 12, w - 8, h - 8, 32, 14), 6 / SP, 0, z_floor=-4, mat_top=org, mat_side=inner, mat_floor=well, bevel_px=3, seg=4)
        C.write_meta('progress_track', {'shadow': {'dy': 4, 'blur': 3, 'alpha': 0.25}})
        C.render(sc, out('progress_track'))
    if C.ARGS.wants('progress_track_s'):
        w, h = 120, 48
        sc, S = new_frame(w, h)
        rim = flat('r', '#3B2A1A', rough=0.4, coat=0.3)
        well = gmat('w', [(0.0, '#4A3826'), (1.0, '#2A1D10')], S, 0, h, -3, rough=0.5, coat=0.2)
        S.ring('t', C.rrect_pts(0, 0, w, h, 24, 12), C.rrect_pts(6, 6, w - 6, h - 6, 18, 12), 4 / SP, 0, z_floor=-3, mat_top=rim, mat_side=rim, mat_floor=well, bevel_px=2)
        C.render(sc, out('progress_track_s'))
    for name, w, h, stops, outl in [('progress_fill', 120, 64, [(0.0, '#4E9300'), (0.6, '#7FC21A'), (0.82, '#A4E03A'), (0.9, '#D8FF9A'), (1.0, '#A4E03A')], '#1E6B00'),
                                    ('progress_fill_s', 80, 36, [(0.0, '#FFAD00'), (0.6, '#FFD119'), (0.85, '#FFE96A'), (0.92, '#FFF6C0'), (1.0, '#FFE96A')], '#B87900')]:
        if not C.ARGS.wants(name):
            continue
        sc, S = new_frame(w, h)
        r = h / 2
        o = flat('o', outl, rough=0.3, coat=0.4)
        jel = gmat('j', stops, S, 2, h - 2, h * 0.5, rough=0.15, coat=0.75, sss=0.15)
        S.prism('o', C.rrect_pts(0, 0, w, h, r, 14), 3 / SP, 0, o, o, bevel_px=2)
        S.prism('j', C.rrect_pts(2.5, 2.5 + 3, w - 2.5, h - 2, r - 3, 14), (3 + 6) / SP, 0, jel, o, bevel_px=r * 0.55, seg=6, profile=0.5)
        C.render(sc, out(name))


def toggles():
    for name, on in (('toggle_on', True), ('toggle_off', False)):
        if not C.ARGS.wants(name):
            continue
        w, h = 176, 88
        sc, S = new_frame(w, h)
        o = flat('o', '#2E7426' if on else '#5E5A6D', rough=0.3, coat=0.4)
        tr = gmat('t', [(0.0, C.scale_hex('#4CC23F' if on else '#9C96B5', 0.9)), (1.0, '#4CC23F' if on else '#9C96B5')], S, 4, 88, -3, rough=0.35, coat=0.3)
        inner = flat('i', '#1F5A19' if on else '#4A465A', rough=0.5)
        S.ring('track', C.rrect_pts(0, 4, w, 88, 42, 14), C.rrect_pts(5, 9, w - 5, 83, 37, 14), 4 / SP, 0, z_floor=-3, mat_top=o, mat_side=inner, mat_floor=tr, bevel_px=2.5)
        knob = gmat('k', [(0.0, '#D6D0E8'), (0.5, '#F2EFFA'), (1.0, '#FFFFFF')], S, 10, 82, 30, rough=0.2, coat=0.7)
        kx = 132 if on else 44
        S.prism('knob', C.circle_pts(35, 64, kx, 46), 22 / SP, 0, knob, flat('kl', '#BDB6D2', rough=0.3, coat=0.5), bevel_px=16, seg=8, profile=0.5)
        C.write_meta(name, {'shadow': {'dy': 4, 'blur': 3, 'alpha': 0.25}})
        C.render(sc, out(name))


def bubble():
    if C.ARGS.wants('bubble_speech'):
        w, h = 240, 180
        sc, S = new_frame(w, h)
        o = flat('o', '#0B3A6E', rough=0.3, coat=0.4)
        mid = flat('m', '#2E9BF0', rough=0.25, coat=0.5)
        cream = gmat('c', [(0.0, '#F6EBC0'), (0.8, '#FFF6D2'), (0.9, '#FFFFFF'), (1.0, '#FFF6D2')], S, 22, 166, 20, rough=0.3, coat=0.4, sss=0.04)
        cl = flat('cl', '#E2D3A0', rough=0.35, coat=0.3)
        S.prism('o', C.rrect_pts(0, 4 + 6, w, h, 40, 12), 4 / SP, 0, o, o, bevel_px=2.5)
        S.prism('m', C.rrect_pts(6, 4 + 6 + 6, w - 6, h - 6, 34, 12), 9 / SP, 0, mid, o, bevel_px=3)
        S.prism('c', C.rrect_pts(14, 4 + 6 + 6 + 8 + 6, w - 14, h - 14, 28, 12), (9 + 7) / SP, 0, cream, cl, bevel_px=4, seg=4)
        C.write_meta('bubble_speech', {'shadow': {'dy': 6, 'blur': 4, 'alpha': 0.25}})
        C.render(sc, out('bubble_speech'))
    if C.ARGS.wants('bubble_tail'):
        w, h = 64, 56
        sc, S = new_frame(w, h)
        o = flat('o', '#0B3A6E', rough=0.3, coat=0.4)
        mid = flat('m', '#2E9BF0', rough=0.25, coat=0.5)
        cream = flat('c', '#FFF6D2', rough=0.3, coat=0.4)
        S.prism('o', C.ccw([(6, 56), (58, 56), (20, 4)]), 4 / SP, 0, o, o, bevel_px=1.5)
        S.prism('m', C.ccw([(14, 56), (48, 56), (22, 20)]), 9 / SP, 0, mid, o, bevel_px=1.5)
        S.prism('c', C.ccw([(20, 56), (40, 56), (24, 34)]), 16 / SP, 0, cream, cream, bevel_px=1.5)
        C.render(sc, out('bubble_tail'))


def nav():
    if C.ARGS.wants('nav_bar'):
        w, h = 216, 216
        sc, S = new_frame(w, h)
        body = gmat('b', [(0.0, '#4E3CDB'), (1.0, '#533FE9')], S, 0, 200, 10, rough=0.35, coat=0.3)
        edge = flat('e', '#31229C', rough=0.35, coat=0.3)
        hl = flat('h', '#4634CE', rough=0.3, coat=0.4)
        S.prism('bar', C.rrect_pts(-40, -40, w + 40, h - 10, 0, 4), 10 / SP, 0, body, edge, bevel_px=0)
        S.prism('edge', C.rrect_pts(-40, h - 10, w + 40, h, 0, 4), 4 / SP, 0, edge, edge)
        S.prism('hl', C.rrect_pts(-40, h - 21, w + 40, h - 10, 0, 4), 10 / SP + 1.5, 0, hl, hl)
        C.render(sc, out('nav_bar'))
    if C.ARGS.wants('nav_tab_on'):
        w, h = 300, 300
        sc, S = new_frame(w, h)
        o = flat('o', '#2C1E94', rough=0.35, coat=0.3)
        tab = gmat('t', [(0.0, '#5747E8'), (0.9, '#6A5CFF'), (0.95, '#9C92FF'), (1.0, '#6A5CFF')], S, -40, h - 12, 20, rough=0.28, coat=0.5)
        S.prism('o', C.rrect_pts(0, -40, w, h, 44, 12), 6 / SP, 0, o, o, bevel_px=2.5)
        S.prism('t', C.rrect_pts(12, -40, w - 12, h - 12, 34, 12), 14 / SP, 0, tab, o, bevel_px=4, seg=4)
        C.render(sc, out('nav_tab_on'))


if __name__ == '__main__':
    L = dict(sb=6, ow=6, owb=6, L=40, R=48, ps=24, pt=18, pb=10, dz=6)
    M = dict(sb=6, ow=5, owb=5, L=28, R=36, ps=18, pt=12, pb=8, dz=5)
    T = dict(sb=6, ow=5, owb=5, L=28, R=36, ps=14, pt=12, pb=8, dz=5)
    B = dict(sb=6, ow=8, owb=8, L=20, R=40, ps=14, pt=12, pb=8, dz=5)
    SL = dict(sb=6, ow=8, owb=8, L=20, R=44, ps=14, pt=12, pb=8, dz=5)
    button('btn_primary', 300, 212, 'green', **L)
    button('btn_primary_pressed', 300, 212, 'green', pressed=True, Lp=14, **L)
    for nm, pal in (('btn_primary_m', 'green'), ('btn_secondary', 'cream'), ('btn_danger', 'red'), ('btn_offer', 'orange')):
        button(nm, 240, 136, pal, **M)
        button(nm + '_pressed', 240, 136, pal, pressed=True, Lp=10, **M)
    button('btn_disabled', 240, 136, 'grey', **M)
    button('btn_tool', 136, 136, 'purple', **T)
    button('btn_tool_pressed', 136, 136, 'purple', pressed=True, Lp=10, **T)
    button('btn_booster', 160, 160, 'green', **B)
    button('btn_booster_pressed', 160, 160, 'green', pressed=True, Lp=8, **B)
    button('btn_booster_off', 160, 160, 'grey', **B)
    button('btn_slot', 184, 184, 'blue', **SL)
    round_button('btn_close', 132, 132, 'red', 'x')
    round_button('btn_close_pressed', 132, 132, 'red', 'x', pressed=True)
    round_button('btn_plus', 64, 64, 'green', '+')
    badges()
    capsule('capsule_hud', 180, 60, 30, '#14113A', 0.86)
    capsule('capsule_home', 264, 88, 44, '#23164D', 0.82)
    hud_plates()
    panels()
    ribbon()
    progress()
    toggles()
    bubble()
    nav()
    print('UI DONE', flush=True)
