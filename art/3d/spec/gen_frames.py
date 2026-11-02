#!/usr/bin/env python3
"""Builds art/3d/spec/frames.json — the 3D frame and clip contract (docs/ART_3D.md section 9).

Python standard library only. Input: Assets/MinikUsta/Resources/art/atlas.json (the v4 atlas: 488 frames whose NAMES
stay and whose pixels are replaced by the 3D renders). Output: every frame the B packages render or keep, with design
size, store scale, pivot, 9-slice borders (design px), rig, pipeline and status; every clip with frame count, fps,
loop, unique drawings, timeline and event frames; tween clips (data only). Deterministic: run it again after editing.

    python3 art/3d/spec/gen_frames.py
"""
import json
import os
import sys
from collections import OrderedDict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
ATLAS = os.path.join(REPO, 'Assets', 'MinikUsta', 'Resources', 'art', 'atlas.json')
OUT = os.path.join(HERE, 'frames.json')


def r1(v):
    v = round(v, 2)
    return int(v) if v == int(v) else v


# ---- rigs (docs/ART_3D.md section 6) ---------------------------------------------------------------------------
RIGS = OrderedDict([
    ('A_tile', {
        'use': 'block cells, corner fills, studs, gloss, mortar, yard floor, wall, gap, rail, scaffold',
        'camera': {'type': 'ORTHO', 'rotationEulerDeg': [35, 0, 0], 'note': '35 deg from straight down, pitch only (no yaw): top face + front lip visible, seamless tiling'},
        'cellBU': 1.0, 'rowPitchBU': 1.2208, 'lipDesignPx': 24, 'slabThicknessBU': 0.3487,
        'renderPxPerCell': 360, 'storePxPerCell': 180, 'context': '3x3 cells rendered, centre cell cropped',
        'lights': [
            {'id': 'key', 'type': 'SUN', 'color': '#FFF1DC', 'strength': 2.9, 'rotationEulerDeg': [-48, 0, 32], 'angleDeg': 6, 'note': 'from screen upper-left: top bevel band light'},
            {'id': 'fill', 'type': 'SUN', 'color': '#E4EAFF', 'strength': 1.1, 'rotationEulerDeg': [60, 0, -15], 'angleDeg': 30, 'note': 'from the viewer side: front lip = base x 0.72..0.80'},
            {'id': 'rim', 'type': 'AREA', 'color': '#FFFFFF', 'power': 80, 'size': 2.0, 'location': 'scene centre + (-2.0, +5.5, +2.5)', 'aim': 'scene centre'},
        ],
        'world': {'color': '#B4B8CC', 'strength': 0.45},
        'validated': 'art/3d/spec/tile_rig_test.py + tile_rig_measure.py, 2026-11-01: top face dE00 <= 5.9 for all 8 colours, lip/base 0.72..0.80 (art/3d/spec/ref/tile_rig_measure.txt)',
        'union': 'join cell boxes, weld vertices (remove_doubles 1e-4), delete coincident face pairs, dissolve_limit 1 deg, then BEVEL (ANGLE 30 deg) + use_auto_smooth 30 deg; NO exact boolean, NO harden_normals (black faces in the test)',
    }),
    ('B_char', {
        'use': 'Usta Kral, Tuna, Kepce stills and clip frames, busts',
        'camera': {'type': 'ORTHO', 'rotationEulerDeg': [80, 0, 0], 'orthoScale': 3.0, 'target': [0, 0, 1.3], 'note': '10 deg down; three-quarter = character yaw -32 deg, light rig fixed'},
        'lights': [
            {'id': 'key', 'type': 'SUN', 'color': '#FFE6C4', 'strength': 2.6, 'rotationEulerDeg': [50, 0, -35], 'angleDeg': 8},
            {'id': 'fill', 'type': 'AREA', 'color': '#9CC2FF', 'power': 180, 'size': 3.0, 'location': [3.0, -3.2, 1.6], 'aim': [0, 0, 1.3]},
            {'id': 'rim', 'type': 'AREA', 'color': '#F2E8FF', 'power': 360, 'size': 1.6, 'location': [2.0, 3.0, 3.2], 'aim': [0, 0, 1.9]},
        ],
        'world': {'color': '#5A6A96', 'strength': 0.25},
        'shadowCatcher': True,
        'reference': 'art/3d/spec/king/king_blockout.py',
    }),
    ('C_prop', {
        'use': 'icons, boosters, coin, star, heart, chest, truck, crane hook, emblem, town buildings',
        'camera': {'type': 'ORTHO', 'rotationEulerDeg': [62, 0, 0], 'note': '28 deg down; object yaw -20 deg (three-quarter); object fills 86 % of the frame'},
        'lights': 'B_char lights (same directions and colours), rim power x 1.3',
        'world': {'color': '#5A6A96', 'strength': 0.30},
        'shadowCatcher': 'icons: no; town buildings: yes',
    }),
    ('D_ui', {
        'use': 'buttons, panels, ribbons, capsules, badges, toggles, progress, bubble, nav, tray',
        'camera': {'type': 'ORTHO', 'rotationEulerDeg': [20, 0, 0], 'note': '20 deg from straight down: thick bottom lip visible; 9-slice centre must be uniform along x'},
        'lights': [
            {'id': 'key', 'type': 'SUN', 'color': '#FFFFFF', 'strength': 2.2, 'rotationEulerDeg': [-25, 0, 30], 'angleDeg': 10},
            {'id': 'fill', 'type': 'AREA', 'color': '#C8D8FF', 'power': 100, 'size': 6.0, 'location': [0, -4.0, 4.0], 'aim': [0, 0, 0]},
        ],
        'world': {'color': '#FFFFFF', 'strength': 0.35},
    }),
    ('E_fx', {
        'use': 'particle and effect sequences',
        'camera': {'type': 'ORTHO', 'rotationEulerDeg': [90, 0, 0], 'note': 'front view; per-effect framing'},
        'lights': 'B_char lights; emission materials for glows; tintable effects rendered white / greyscale',
        'world': {'color': '#000000', 'strength': 0.0},
    }),
    ('F_bg', {
        'use': 'home parallax layers, level background',
        'camera': {'type': 'ORTHO', 'rotationEulerDeg': [78, 0, 0], 'note': '12 deg down; each parallax layer rendered alone with film transparent (except sky)'},
        'lights': [
            {'id': 'sun', 'type': 'SUN', 'color': '#FFE9C8', 'strength': 3.0, 'rotationEulerDeg': [55, 0, -30], 'angleDeg': 4},
        ],
        'world': {'sky': 'gradient #1B49A4 (zenith) -> #2B5FB6 -> #7AA3D8 -> #F0D7D6 (horizon)', 'strength': 1.0},
        'mist': {'start': 8.0, 'depth': 40.0, 'color': '#C7D8F2', 'note': 'air perspective: far layers mix 40 % towards the sky'},
    }),
])

COLOR_MANAGEMENT = {
    'viewTransform': 'Standard', 'look': 'None', 'exposure': 0.0, 'gamma': 1.0, 'displayDevice': 'sRGB',
    'file': 'PNG RGBA 8 bit, straight (unassociated) alpha, sRGB',
    'materialColor': 'Principled BSDF Base Color = srgb_to_linear(hex)',
    'why': 'AgX (all looks) and Filmic measurably desaturate the O/Y/G block hues; Standard keeps the D-083 palette (see ART_3D 6.3)',
    'engine': 'CYCLES, device CPU, use_denoising False (Ubuntu blender 4.0.2 has no OpenImageDenoise; EEVEE needs libEGL and aborts headless)',
    'samples': {'A_tile': 128, 'B_char': 96, 'C_prop': 128, 'D_ui': 96, 'E_fx': 64, 'F_bg': 64},
    'supersample': 2, 'downsample': 'Pillow Image.LANCZOS to the store size',
    'clampIndirect': 3.0, 'blurGlossy': 1.0,
    'alphaBleed': 'after downsampling copy the nearest opaque RGB into fully transparent pixels (4 px) so bilinear filtering has no dark fringe',
}

PALETTE = OrderedDict([
    # code: base (tokens color.block), symbol ink rule (ART_DIRECTION 2.5), Principled values validated in tile_rig_test
    ('W', {'base': '#764012', 'ink': 'white 92 %', 'roughness': 0.45, 'specularIorLevel': 0.2, 'coat': 0.0, 'coatRoughness': 0.06, 'subsurface': 0.04, 'measuredTop': '#794729', 'dE00': 4.71}),
    ('Y', {'base': '#FFE23E', 'ink': 'base x 0.40', 'roughness': 0.30, 'specularIorLevel': 0.5, 'coat': 0.40, 'coatRoughness': 0.06, 'subsurface': 0.12, 'measuredTop': '#FFDE49', 'dE00': 1.75}),
    ('G', {'base': '#44D088', 'ink': 'base x 0.40', 'roughness': 0.30, 'specularIorLevel': 0.5, 'coat': 0.40, 'coatRoughness': 0.06, 'subsurface': 0.12, 'measuredTop': '#50CB84', 'dE00': 1.53}),
    ('R', {'base': '#CF404E', 'ink': 'white 92 %', 'roughness': 0.32, 'specularIorLevel': 0.5, 'coat': 0.40, 'coatRoughness': 0.06, 'subsurface': 0.08, 'measuredTop': '#D84F59', 'dE00': 4.05}),
    ('O', {'base': '#EC8D20', 'ink': 'base x 0.40', 'roughness': 0.30, 'specularIorLevel': 0.5, 'coat': 0.40, 'coatRoughness': 0.06, 'subsurface': 0.10, 'measuredTop': '#FF9946', 'dE00': 5.87}),
    ('C', {'base': '#C8CFDA', 'ink': 'base x 0.40', 'roughness': 0.42, 'specularIorLevel': 0.5, 'coat': 0.25, 'coatRoughness': 0.06, 'subsurface': 0.06, 'measuredTop': '#DCDEE7', 'dE00': 4.34}),
    ('B', {'base': '#2984DE', 'ink': 'white 92 %', 'roughness': 0.30, 'specularIorLevel': 0.5, 'coat': 0.40, 'coatRoughness': 0.06, 'subsurface': 0.08, 'measuredTop': '#3F87E1', 'dE00': 2.15}),
    ('P', {'base': '#5D3AAE', 'ink': 'white 92 %', 'roughness': 0.32, 'specularIorLevel': 0.5, 'coat': 0.40, 'coatRoughness': 0.06, 'subsurface': 0.06, 'measuredTop': '#6649AB', 'dE00': 4.32}),
])


def classify(name):
    """(group, rig, pipeline, note) for an existing v4 frame name."""
    if name.startswith('blk_') and name not in ('blk_gloss', 'blk_locked'):
        return 'block', 'A_tile', 'blender', 'auto-tile: 3x3 context render, crop centre; cf = 2x2 context, crop corner 32 px'
    if name == 'blk_gloss':
        return 'block', 'A_tile', 'blender', 'stud specular highlight only (additive); holdable pieces only (K-09)'
    if name == 'blk_locked':
        return 'block', 'A_tile', 'blender', 'placed overlay: 4 mortar dots with contact shadow, transparent elsewhere (K-14)'
    if name.startswith('stud_'):
        return 'block', 'A_tile', 'blender', 'colour-blind stud cap without symbol'
    if name.startswith('sym_'):
        return 'block', None, 'svg', 'flat symbol stays vector-crisp (colour-blind overlay, chips, tutorial)'
    if name.startswith(('ghost_', 'shadow_')):
        return name.split('_')[0], None, 'svg', 'flat by rule (ART 4, K-18): printed plan / drop-shadow overlay, no 3D'
    if name in ('floor_site', 'floor_site_deep', 'crane_band', 'px_white', 'fx_glow', 'fx_rays', 'fx_sheen', 'fx_drop_blob',
                'logo_wordmark'):
        return 'flat', None, 'svg', 'kept 2D: flat / soft / text by design'
    if name in ('flag_tr', 'flag_en'):
        return 'icon', 'C_prop', 'blender', 'art turn 1: 3D round badge (domed enamel face, 6 px gold rim)'
    if name.startswith(('floor_', 'wall_', 'gap_', 'rail_', 'scaffold_', 'beam_')):
        return 'board', 'A_tile', 'blender', 'tiles seamlessly with the same pitch rig as blocks'
    if name in ('crane_hook', 'truck'):
        return 'board', 'C_prop', 'blender', ''
    if name == 'board_tray':
        return 'board', 'D_ui', 'blender', '9-slice tray bezel'
    if name.startswith(('btn_', 'badge_', 'bead_', 'capsule_', 'hud_', 'panel_', 'ribbon_', 'progress_', 'bubble_', 'toggle_', 'nav_')):
        return 'ui', 'D_ui', 'blender', ''
    if name.startswith(('icon_', 'ui_', 'deco_')):
        return 'icon', 'C_prop', 'blender', ''
    if name.startswith('fx_'):
        return 'fx', 'E_fx', 'blender', 'render white / greyscale where the code tints it'
    if name.startswith('chr_dede_'):
        return 'chr', 'B_char', 'blender', 'ALIAS: rendered as Usta Kral (D-101) until the code reads chr_king_*'
    if name.startswith('chr_'):
        return 'chr', 'B_char', 'blender', '3D re-model of the existing character, same pose'
    if name.startswith('bg_'):
        return 'bg', 'F_bg', 'blender', 'bg_home = flattened composite of the parallax layers (compatibility)'
    if name.startswith('town_'):
        return 'town', 'C_prop', 'blender', ''
    if name == 'logo_emblem':
        return 'logo', 'C_prop', 'blender', ''
    return 'other', None, 'svg', ''


ALIAS = {'chr_dede_idle': 'chr_king_still_idle', 'chr_dede_talk': 'chr_king_still_talk', 'chr_dede_bust': 'chr_king_bust_neutral'}


def existing_frames():
    with open(ATLAS, encoding='utf-8') as fh:
        atlas = json.load(fh)
    out = OrderedDict()
    for name in sorted(atlas['frames']):
        f = atlas['frames'][name]
        s = f['scale']
        group, rig, pipe, note = classify(name)
        e = OrderedDict()
        e['group'] = group
        e['w'] = r1(f['w'] / s)
        e['h'] = r1(f['h'] / s)
        e['scale'] = s
        e['pivot'] = [round(f['pivotX'], 4), round(f['pivotY'], 4)]
        if any(f.get(k, 0) for k in ('borderL', 'borderT', 'borderR', 'borderB')):
            e['border'] = [r1(f['borderL'] / s), r1(f['borderT'] / s), r1(f['borderR'] / s), r1(f['borderB'] / s)]
        if 'anchorX' in f:
            e['anchor'] = 'computed: centre of the stud on the rendered tile (0..1 from bottom-left), with studD and symD in design px'
        e['rig'] = rig
        e['pipeline'] = pipe
        e['status'] = 'replace3d' if pipe == 'blender' else 'keep2d'
        if name in ALIAS:
            e['aliasOf'] = ALIAS[name]
            e['deprecated'] = True
        if note:
            e['note'] = note
        out[name] = e
    return out


def frame(group, w, h, scale, pivot, rig, note='', border=None, status='new'):
    e = OrderedDict([('group', group), ('w', w), ('h', h), ('scale', scale), ('pivot', pivot)])
    if border:
        e['border'] = border
    e['rig'] = rig
    e['pipeline'] = 'blender'
    e['status'] = status
    if note:
        e['note'] = note
    return e


# ---- clips (docs/ART_3D.md section 7) ---------------------------------------------------------------------------
def tl(*runs):
    """timeline from (unique, count) runs"""
    out = []
    for u, n in runs:
        out += [u] * n
    return out


IDLE_TL = tl((0, 4), (1, 4), (2, 4), (3, 4), (4, 4), (5, 4), (6, 4), (7, 2), (12, 2), (13, 2), (12, 1), (8, 1), (9, 4), (10, 4), (11, 4))


def king_timeline(clip_name):
    """Art turn 1: the runtime timeline of art/3d/blender/king/animate_king.plan() (= king.json) as drawing numbers."""
    sys.path.insert(0, os.path.join(REPO, 'art', '3d', 'blender', 'king'))
    import animate_king
    _d, clips = animate_king.plan()
    return [int(n[-2:]) for n in clips[clip_name]['frames']]


# art turn 1: king_idle 24 drawings on twos (B1k 9 held 6), king_cheer 16 drawings; numbers = king.json drawing names
IDLE_TL = king_timeline('king_idle')
CHEER_TL = king_timeline('king_cheer')

KING_CORNER = {'canvas': [320, 320], 'scale': 1.0, 'pivot': [0.5, 0.0625], 'set': 'corner'}
KING_STAGE = {'canvas': [400, 480], 'scale': 1.0, 'pivot': [0.5, 0.05], 'set': 'stage'}

CLIPS = OrderedDict()


def clip(name, prefix, size, fps, timeline, loop=None, out=None, events=None, tween=None, use='', juice='', drawings=None, priority=1, extra=0):
    n_unique = max(timeline) + 1 + extra  # extra = drawings used only by 'out'
    c = OrderedDict()
    c['frames'] = len(timeline)
    c['fps'] = fps
    c['unique'] = n_unique
    c['frameName'] = prefix + '_{NN}'
    c['canvas'] = size['canvas']
    c['scale'] = size['scale']
    c['pivot'] = size['pivot']
    if 'set' in size:
        c['set'] = size['set']
    c['loop'] = loop if loop is not None else False
    if out:
        c['out'] = out
    c['timeline'] = timeline
    c['events'] = OrderedDict((str(k), v) for k, v in sorted((events or {}).items()))
    if tween:
        c['tween'] = tween
    if drawings:
        c['drawings'] = drawings
    if use:
        c['use'] = use
    if juice:
        c['juice'] = juice
    c['priority'] = priority
    CLIPS[name] = c


BREATH = {'scaleY': '1 + 0.015 * sin(2*pi*t / 2.0)', 'scaleX': '1 - 0.006 * sin(2*pi*t / 2.0)', 'pivot': 'feet', 'note': 'continuous per rendered frame, smooths the drawings held on fours'}

clip('king_idle', 'chr_king_idle', KING_CORNER, 24, IDLE_TL, loop={'start': 0, 'end': 47},
     events={30: 'blink'}, tween=BREATH, use='level screen corner, tutorial idle, popups',
     drawings='art turn 1: 24 drawings on twos (u1-u8 = the B1k samples 6..42, u9-u23 the in-betweens); u0 = K0 (shared rest pose: every clip starts and ends on it); weight shift with the hat 4 frames late, trowel sway; blink 30 half / 32 closed / 34 half')
clip('king_cheer', 'chr_king_cheer', KING_CORNER, 24,
     CHEER_TL,
     events={4: 'lift_off', 10: 'apex', 14: 'land'}, use='correct placement streak, booster success, goal done',
     juice='#12 / #15 / #54 accents (Tuna #82 reaction moves to the king)',
     drawings='0-3 anticipation crouch (squash sy 0.92 sx 1.05, eyes squeeze); 4-9 launch, fists up (stretch 1.08); 10-13 apex, hat lifts 6 px; 14-16 land (squash 0.90), hat lands 2 frames late; 17-23 overshoot 1.03 -> settle; u15 = K0')
clip('king_think', 'chr_king_think', KING_CORNER, 24,
     tl((0, 2), (1, 2), (2, 2), (3, 2), (4, 4), (5, 4), (6, 4), (7, 4), (8, 4), (11, 2), (9, 2), (10, 4)),
     loop={'start': 8, 'end': 35}, out='reverse 0-7', events={12: 'tap', 24: 'tap', 28: 'blink'},
     use='stuck hint (no useful move for a while), hint button, D1 state before truck help',
     drawings='0-7 in: hand rises to the beard, head tilts 3 deg, eyes look up-right, one brow up; 8-35 loop: finger taps the beard (u5, u8), eyes drift, hat tilts; u11 eyes closed')
clip('king_talk', 'chr_king_talk', KING_CORNER, 24,
     tl((0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (1, 2), (5, 2), (6, 2), (7, 2), (8, 2), (9, 2), (0, 2)),
     loop={'start': 0, 'end': 23}, events={0: 'beat', 12: 'beat'},
     use='tutorial bubble while the typewriter runs (JUICE #81), story panels',
     drawings='mouth shapes: u0 closed smile, u1 A, u2 O, u3 E, u4 M (lips pressed), u5 A wide, u6 O, u7 E, u8 M, u9 half open; free hand open palm sweeps +-8 deg; head nods on the beat frames; stop at the next beat, then K0')
clip('king_point', 'chr_king_point', KING_CORNER, 24,
     tl((0, 2), (1, 2), (2, 1), (3, 1), (4, 1), (5, 1), (6, 2), (7, 2), (8, 2), (9, 2), (10, 2)),
     loop={'start': 12, 'end': 17}, out='u11 x2, u0 x2', events={6: 'point_hit', 12: 'hold_start', 14: 'glint'}, extra=1,
     use='tutorial: points the golden trowel at the target (UX 13.1); mirror with scaleX -1 for targets on the left',
     juice='#103 / #104: point_hit starts the highlight pulse',
     drawings='0-3 anticipation: trowel pulls back, body leans away; 4-7 strike: trowel points right, 6 deg overshoot; 8-11 settle; 12-17 hold loop, glint runs along the blade; u11 = half-way return pose')
clip('king_encourage', 'chr_king_encourage', KING_CORNER, 24,
     tl((0, 2), (1, 2), (2, 2), (3, 1), (4, 2), (5, 2), (6, 1), (7, 2), (8, 1), (9, 1), (10, 2), (11, 2), (12, 2), (13, 2)),
     events={9: 'gesture', 15: 'pump'},
     use='out of moves (JUICE #57) and lose offer (UX 7): warm, never sad, never blaming (CLAUDE: no red cross, no shame)',
     drawings='0-5 head tilt, brows rise inward (empathy), soft eyes; 6-8 small lean back (anticipation); 9-14 both palms open forward ("again"); 15-19 small fist pump with overshoot ("you can"); 20-23 settle to K0 (u13)')
clip('king_home_idle', 'chr_king_home_idle', KING_STAGE, 24, IDLE_TL, loop={'start': 0, 'end': 47},
     events={30: 'blink'}, tween=BREATH, use='home screen (stage size)', drawings='same drawings as king_idle at stage size')
clip('king_wave', 'chr_king_wave', KING_STAGE, 24,
     tl((0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (6, 2), (7, 2), (8, 2), (9, 2), (10, 2), (11, 2)),
     loop={'start': 6, 'end': 23}, out='reverse 0-5', events={9: 'wave_peak', 18: 'wave_peak'},
     use='home screen greeting (first open of the day, return from a level)',
     drawings='0-5 in: right arm rises, body sways left; 6-23 loop: forearm swings +-25 deg, the hand drags 2 frames behind (overlap), body counter-sways 2 deg')
clip('king_big_cheer', 'chr_king_big_cheer', KING_STAGE, 24,
     tl((0, 2), (1, 2), (2, 2), (3, 1), (4, 1), (5, 1), (6, 1), (7, 1), (8, 1), (9, 1), (10, 1), (11, 1), (12, 1), (13, 1), (14, 1), (15, 1), (16, 1), (17, 2), (18, 2),
        (19, 4), (20, 4), (21, 4), (22, 4), (23, 4), (24, 4)),
     loop={'start': 24, 'end': 47}, events={6: 'jump', 15: 'land', 22: 'hat_land', 28: 'hop', 40: 'hop'},
     use='win (JUICE #55 / #95): replaces the Tuna joy pose on the win card', juice='#55 stage 4-5',
     drawings='0-5 deep crouch, eyes shut; 6-14 big jump, trowel and fist up, hat pops off the head (secondary); 15-19 land squash; 20-23 hat lands back; 24-47 victory loop: body sway +-6 deg, two small hops (28, 40)')
CREW_STAGE = {'canvas': [400, 480], 'scale': 0.84, 'pivot': [0.5, 0.05], 'set': 'stage'}
CREW_CORNER = {'canvas': [320, 320], 'scale': 1.05, 'pivot': [0.5, 0.0625], 'set': 'corner'}
clip('tuna_home_idle', 'chr_tuna_home_idle', CREW_STAGE, 24, tl(*[(i, 2) for i in range(24)]), loop={'start': 0, 'end': 47},
     events={3: 'wave_peak', 15: 'wave_peak', 27: 'wave_peak', 30: 'blink', 39: 'wave_peak'}, use='home screen beside Usta Kral (art turn 1, crew.json)',
     drawings='screen-left glove waves at 2 Hz, the glove 2 frames behind the forearm; other glove on the hip; weight shift, knee bounce, head counter-tilt; blink 30-35')
clip('kepce_home_idle', 'chr_kepce_idle', CREW_STAGE, 24, tl(*[(i, 2) for i in range(24)]), loop={'start': 0, 'end': 47},
     events={30: 'blink'}, use='home screen beside Tuna (art turn 1, crew.json)',
     drawings='tail wag 2 Hz, panting, breathing, slow head tilt, ears 3 frames late, hat follow-through; blink 30-35')
clip('kepce_corner_idle', 'chr_kepce_idle', CREW_CORNER, 24, tl(*[(i, 2) for i in range(24)]), loop={'start': 0, 'end': 47},
     events={30: 'blink'}, use='level screen corner beside the king (art turn 1; the kepce_home_idle drawings at corner size)')
clip('kepce_wag', 'chr_kepce_wag', {'canvas': [240, 168], 'scale': 1.0, 'pivot': [0.5, 0.06]}, 24,
     tl((0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (6, 2), (7, 2)), loop={'start': 0, 'end': 15},
     use='Kepce beside the king in the level corner (secondary)', drawings='tail wag cycle, ears bounce 1 frame late', priority=2)

FX_CLIPS = [
    # name, prefix, canvas, scale, pivot, fps, frames, loop, events, tint, use / juice
    ('fx_crane_grab', 'fx_crane_claw', [160, 200], 1.0, [0.5, 1.0], 24, 8, False, {5: 'grab'}, None, 'crane booster claw open -> closed; release = play backwards (event at frame 2: release); rope = crane_band style tiled sprite, descent / carry are tweens (JUICE #62, 900 ms total)'),
    ('fx_hammer_smash', 'fx_hammer', [320, 320], 1.0, [0.8, 0.1], 24, 11, False, {6: 'impact'}, None, 'hammer swings in from the right -60 -> +20 deg, impact at 6, recoil (JUICE #61, 450 ms)'),
    ('fx_impact_star', 'fx_impact_star', [256, 256], 1.0, [0.5, 0.5], 24, 8, False, {0: 'burst'}, None, 'comic impact burst (white core + gold) spawned at the hammer impact'),
    ('fx_paint_splash', 'fx_paint_splash', [256, 256], 1.0, [0.5, 0.5], 24, 12, False, {3: 'splat'}, 'new block colour', 'brush colour swap splash on each block (JUICE #63, 500 ms); rendered white'),
    ('fx_trowel_sparkle', 'fx_trowel_sparkle', [192, 192], 1.0, [0.5, 0.5], 24, 12, True, {0: 'glint', 6: 'glint'}, None, 'golden trowel turning +-15 deg with glints: combo icon (#16), trowel flight head (#17)'),
    ('fx_coin_spin', 'fx_coin', [128, 128], 1.5, [0.5, 0.5], 24, 12, True, {0: 'face'}, None, 'coin turning 360 deg around its vertical axis; frame 0 = icon_coin face (#56, #74)'),
    ('fx_star_burst', 'fx_star_burst', [512, 512], 0.5, [0.5, 0.5], 24, 14, False, {0: 'burst'}, None, 'ring + 16 small stars radial behind the earned star (#96, 600 ms)'),
    ('fx_confetti3d_rect', 'fx_confetti3d_rect', [24, 12], 1.5, [0.5, 0.5], 24, 8, True, {}, 'palette colour', '3D flip cycle of a paper rectangle; rendered white (#55, #18)'),
    ('fx_confetti3d_round', 'fx_confetti3d_round', [16, 16], 1.5, [0.5, 0.5], 24, 8, True, {}, 'palette colour', '3D flip cycle of a paper disc; rendered white'),
    ('fx_dust_puff', 'fx_dust_puff', [192, 128], 1.0, [0.5, 0.0], 24, 10, False, {0: 'puff'}, 'cream / grey', 'landing and booster dust (#11, #19, #62)'),
    ('fx_segment_done', 'fx_segment_done', [512, 512], 0.5, [0.5, 0.5], 24, 14, False, {0: 'flash', 6: 'fly'}, None, 'segment complete flare + ring + sparkles behind the site; the segment flight to the panorama is a tween (#18, #93, 600 ms)'),
    ('fx_lock_shine', 'fx_lock_shine', [72, 72], 1.5, [0.5, 0.5], 60, 12, False, {}, None, 'correct placement glint on each stud, additive white, staggered 40 ms bottom -> top over the cells (#12, #92, 200 ms)'),
]

for (name, prefix, canvas, scale, pivot, fps, n, loop, events, tint, use) in FX_CLIPS:
    c = OrderedDict()
    c['frames'] = n
    c['fps'] = fps
    c['unique'] = n
    c['frameName'] = prefix + '_{NN}'
    c['canvas'] = canvas
    c['scale'] = scale
    c['pivot'] = pivot
    c['loop'] = {'start': 0, 'end': n - 1} if loop else False
    c['timeline'] = list(range(n))
    c['events'] = OrderedDict((str(k), v) for k, v in sorted(events.items()))
    if tint:
        c['tint'] = tint
    c['use'] = use
    c['priority'] = 1
    CLIPS[name] = c

TWEEN_CLIPS = OrderedDict([
    ('blk_lift', OrderedDict([
        ('frames', 6), ('fps', 60), ('pivot', 'piece centre'), ('juice', '#1 (80 ms Back.easeOut) + #91'),
        ('note', 'shape of the curve; the code stretches it to the JUICE duration'),
        ('scale', [1.0, 1.035, 1.065, 1.085, 1.09, 1.08]),
        ('yPx', [0, -2, -4, -5.5, -6, -6]),
        ('shadowAlpha', [0, 0.12, 0.2, 0.26, 0.3, 0.3]),
        ('shadowOffsetYPx', [0, 6, 10, 14, 16, 18]),
    ])),
    ('blk_land_squash', OrderedDict([
        ('frames', 8), ('fps', 60), ('pivot', 'piece bottom centre'), ('juice', '#11 (120 ms Back.easeOut)'),
        ('scaleX', [1.0, 1.12, 1.1, 1.04, 0.98, 0.99, 1.005, 1.0]),
        ('scaleY', [1.0, 0.86, 0.89, 0.97, 1.03, 1.01, 0.995, 1.0]),
        ('events', OrderedDict([('1', 'dust_puff')])),
    ])),
    ('blk_bad_shake', OrderedDict([
        ('frames', 12), ('fps', 60), ('pivot', 'piece centre'), ('juice', '#13 (shake part; then the K-17 bounce arc)'),
        ('xPx', [0, 2, 0, -2, 0, 2, 0, -2, 0, 2, 0, 0]),
        ('tintRed', OrderedDict([('color', '#FF4A3D'), ('alpha', [0.5, 0.5, 0.45, 0.4, 0.3, 0.2, 0.1, 0, 0, 0, 0, 0])])),
        ('note', 'tween only, no rendered frames (reduced motion: no shake, tint only)'),
    ])),
])


def new_frames():
    out = OrderedDict()
    # Usta Kral stills (popups, story, store art) and busts (tutorial portrait, avatar)
    for pose in ('idle', 'cheer', 'big_cheer', 'think', 'talk', 'point', 'encourage', 'wave'):
        out[f'chr_king_still_{pose}'] = frame('chr', 300, 480, 1.25, [0.5, 0.02], 'B_char', 'key pose of the clip, full body (same box as chr_dede_idle)')
    for e in ('neutral', 'happy', 'think', 'talk', 'encourage', 'surprised'):
        out[f'chr_king_bust_{e}'] = frame('chr', 256, 320, 1.0, [0.5, 0.5], 'B_char', 'head + shoulders; the tutorial crops a circle of 132')
    # clip frames
    for cname, c in CLIPS.items():
        prefix = c['frameName'].replace('_{NN}', '')
        group = 'chr' if cname.startswith(('king_', 'kepce_', 'tuna_')) else 'fx'
        rig = 'B_char' if group == 'chr' else ('A_tile' if cname == 'fx_lock_shine' else 'E_fx')
        for i in range(c['unique']):
            out[f'{prefix}_{i:02d}'] = frame(group, c['canvas'][0], c['canvas'][1], c['scale'], c['pivot'], rig, f'clip {cname} drawing {i}')
    # large reward / popup objects (reward layer 240 px, level start window)
    for n in ('coin', 'star', 'heart', 'chest', 'crane', 'hammer', 'brush', 'gold_trowel'):
        out[f'icon_{n}_xl'] = frame('icon', 256, 256, 1.0, [0.5, 0.5], 'C_prop', 'same model as icon_' + n + ', reward layer and level start window')
    # home parallax layers (ART_3D 5.1); bg_home stays as the flattened composite
    layers = [
        ('bg_home_sky', 1080, 1920, 0.375, 0.0, 'gradient sky + sun glow, opaque'),
        ('bg_home_clouds', 1080, 640, 0.5, 0.06, 'cloud band, alpha; drifts 6 px/s'),
        ('bg_home_far', 1080, 720, 0.5, 0.15, 'far hills with air perspective, alpha'),
        ('bg_home_town', 1080, 900, 0.75, 0.35, 'Renkli Tepe houses, tower crane, trees, alpha'),
        ('bg_home_ground', 1080, 760, 0.75, 0.6, 'meadow + path the characters stand on, alpha'),
        ('bg_home_fore_l', 420, 760, 0.75, 1.0, 'left foreground bush / scaffold cluster, alpha'),
        ('bg_home_fore_r', 420, 760, 0.75, 1.0, 'right foreground bush / material pile, alpha'),
    ]
    for (n, w, h, s, par, note) in layers:
        e = frame('bg', w, h, s, [0.5, 0.0] if n != 'bg_home_sky' else [0.5, 0.5], 'F_bg', note)
        e['parallax'] = par
        out[n] = e
    # art turn 1: Tuna stills (crew.json drawings behind the v4 chr_tuna_* atlas names)
    for pose in ('cheer', 'idle', 'think'):
        out[f'chr_tuna_still_{pose}'] = frame('chr', 300, 480, 0.84, [0.5, 0.02], 'B_char', 'crew.json still; drawn into the v4 atlas name chr_tuna_' + pose)
    # art turn 1: chapter 1 tree house in stages (town.py): base + 10 pieces on one canvas and pivot, stacked by the code
    out['town_ch1_s00'] = frame('town', 760, 820, 0.6, [0.5, 0.0], 'C_prop', 'the bare tree (0 / 10); fx.json stages.town_ch1.base')
    pieces = ['tree_steps', 'platform', 'front_wall', 'window_sill', 'two_rooms', 'tall_trunk', 'under_roof', 'garden_fence', 'rope_ladder', 'treehouse']
    for k, p in enumerate(pieces, start=1):
        out['town_ch1_p%02d' % k] = frame('town', 760, 820, 0.6, [0.5, 0.0], 'C_prop', 'piece %d (%s) alone, earlier pieces as holdout; draw over s00 at the same pivot' % (k, p))
    for k, p in enumerate(pieces, start=1):
        out['bld_%s_3d' % p] = frame('reward', 360, 360, 0.6, [0.5, 0.5], 'C_prop', 'win window: the structure piece of level %d, soft contact ellipse' % k)
    return out


def main():
    doc = OrderedDict()
    doc['version'] = 5
    doc['doc'] = 'docs/ART_3D.md'
    doc['decisions'] = ['D-099', 'D-100', 'D-101']
    doc['generator'] = 'art/3d/spec/gen_frames.py'
    doc['units'] = ('w h border = design px (1080-wide canvas); scale = raster px per design px (store size = w*scale); '
                    'pivot = 0..1 from bottom-left (Unity Sprite.Create); border order L T R B; fps = timeline rate; '
                    'timeline[i] = unique drawing index shown at frame i; events keyed by timeline frame index')
    doc['naming'] = OrderedDict([
        ('existing', 'every v4 atlas name stays; status replace3d = same name, same design size, same pivot and border, new 3D pixels; keep2d = untouched SVG raster'),
        ('clipFrames', '<frameName with {NN}> = two-digit unique drawing index, e.g. chr_king_idle_00'),
        ('new', 'new names never reuse a v4 name; chr_dede_* stay as deprecated aliases rendered as Usta Kral'),
    ])
    doc['colorManagement'] = COLOR_MANAGEMENT
    doc['rigs'] = RIGS
    doc['blockMaterial'] = OrderedDict([
        ('palette', PALETTE),
        ('shared', OrderedDict([
            ('coatIor', 1.5), ('subsurfaceRadius', [1.0, 0.45, 0.25]), ('subsurfaceScale', 0.05),
            ('edgeBevelCell', 0.10), ('edgeBevelSegments', 5), ('edgeBevelProfile', 0.7),
            ('cornerRadiusCell', 0.20), ('innerConcaveFilletCell', 0.06), ('pillowDomeCell', 0.02),
            ('stud', OrderedDict([('diameterCell', 0.45), ('heightCell', 0.12), ('topBevelCell', 0.03), ('symbolEmbossCell', 0.015), ('symbolBoxCell', 0.37)])),
            ('lipDesignPx', 24),
        ])),
        ('variants', OrderedDict([
            ('holdable', 'tile + blk_gloss at the anchor (K-09)'),
            ('notHoldable', 'tile tinted #EBEBEB, no blk_gloss'),
            ('placed', 'tile tinted #EFEFEF + blk_locked (K-14)'),
            ('lifted', 'blk_lift tween + fx_drop_blob'),
            ('planGhost', 'ghost_<c> stays flat SVG (printed blueprint, ART 4)'),
        ])),
        ('fidelity', 'A_tile render, 1-cell tile: top-face pixel patch (stud excluded) within dE00 8 of the base hex; front lip = base x 0.70..0.85; symbol contrast >= 3:1 (WCAG 1.4.11) on the stud; measured by tile_rig_measure.py'),
    ])
    frames = existing_frames()
    added = new_frames()
    clash = set(frames) & set(added)
    if clash:
        raise SystemExit('name clash: ' + ', '.join(sorted(clash)))
    frames.update(added)
    doc['counts'] = OrderedDict([
        ('frames', len(frames)),
        ('existing', len(frames) - len(added)),
        ('new', len(added)),
        ('replace3d', sum(1 for f in frames.values() if f['status'] == 'replace3d')),
        ('keep2d', sum(1 for f in frames.values() if f['status'] == 'keep2d')),
        ('clips', len(CLIPS)),
        ('tweenClips', len(TWEEN_CLIPS)),
    ])
    doc['budget'] = OrderedDict([
        ('characterFramesLevelScreenMB', 32), ('characterFramesHomeMB', 24), ('unit', 'RGBA32 equivalent after trimming'),
        ('estimate', 'corner set 78 drawings x ~254 KB = 19.8 MB; stage set 51 x ~422 KB = 21.5 MB; win popup adds big_cheer 25 x 422 KB = 10.5 MB'),
        ('recommendation', 'import character and fx sheets as compressed Texture2D assets (ASTC / ETC2 / DXT via the Unity import pipeline in CI) instead of Resources .bytes + LoadImage: ~8x less GPU memory'),
    ])
    doc['clips'] = CLIPS
    doc['tweenClips'] = TWEEN_CLIPS
    doc['frames'] = frames
    with open(OUT, 'w', encoding='utf-8') as fh:
        json.dump(doc, fh, ensure_ascii=False, indent=1)
        fh.write('\n')
    print(json.dumps(doc['counts']))


if __name__ == '__main__':
    main()
