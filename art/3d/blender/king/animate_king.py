"""Usta Kral — expressions, key poses, clips and the frame renderer (package B1k; D-100, D-101).

    blender -b --factory-startup -noaudio --python art/3d/blender/king/animate_king.py -- \
        --out art/3d/out/king/raw [--px 213.333] [--samples 64] [--clips king_idle,king_cheer] [--keys]

Every clip is a list of keys (frame, pose, ease) on a 24 fps timeline; numeric channels are eased between keys,
discrete channels (mouth, eyes, hand shapes) switch on their key. Procedural layers add the idle sway, the wave swing,
the victory sway / hops and the hard-hat follow-through (the hat lags the body by `hat_lag` frames: it presses down
on take-off and floats up on the way down, then lands late). Only the frames listed in `samples` are rendered
(drawings, about 12 fps); `timeline` maps all 24 fps frames to drawings. K0 (rest pose, idle drawing 0) is shared:
every clip starts and ends on it (docs/ART_3D.md 7.1).

Royal Match animation language (anticipation, overshoot, squash and stretch, follow-through, secondary motion) is
applied to an ORIGINAL character (art/3d/spec/king.md section 10 distinctness table); no reference pixels are used.

Outputs (git-ignored intermediates under art/3d/out/king/): raw/<drawing>.png (master renders, 2x store size) and
raw/manifest.json (drawings, clip timelines, events, shadow anchors). post_king.py turns them into trimmed frames.
"""
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

# ---------------------------------------------------------------------------------------------------------------
# expressions (king.md section 5); brow dy in BU (+ up), brow angle in degrees (+ = outer end up)
# ---------------------------------------------------------------------------------------------------------------
EXPR = {
    'neutral': dict(eyes='open', lid=0.06, lid_low=0.0, gaze_x=0.0, gaze_z=0.0, brow_dy=0.0, brow_ang=4.0,
                    mouth='smile', blush=0.25, cheek=1.0),
    'cheer': dict(eyes='happy', lid=0.0, lid_low=0.0, gaze_x=0.0, gaze_z=0.0, brow_dy=0.03, brow_ang=10.0,
                  mouth='grin', blush=0.45, cheek=1.08),
    'big': dict(eyes='happy', lid=0.0, lid_low=0.0, gaze_x=0.0, gaze_z=0.0, brow_dy=0.035, brow_ang=16.0,
                mouth='big', blush=0.55, cheek=1.12),
    'squeeze': dict(eyes='happy', lid=0.0, lid_low=0.0, gaze_x=0.0, gaze_z=0.0, brow_dy=-0.012, brow_ang=-4.0,
                    mouth='M', blush=0.4, cheek=1.1),
    'think': dict(eyes='open', lid=0.12, lid_low=0.08, gaze_x=12.0, gaze_z=16.0, brow_l_dy=-0.012, brow_l_ang=-8.0,
                  brow_r_dy=0.032, brow_r_ang=14.0, mouth='side', blush=0.18, cheek=1.0),
    'talk': dict(eyes='open', lid=0.04, lid_low=0.0, gaze_x=0.0, gaze_z=2.0, brow_dy=0.012, brow_ang=6.0,
                 mouth='A', blush=0.28, cheek=1.0),
    'point': dict(eyes='open', lid=0.10, lid_low=0.10, gaze_x=20.0, gaze_z=0.0, brow_dy=-0.006, brow_ang=-8.0,
                  mouth='smirk', blush=0.3, cheek=1.04),
    'encourage': dict(eyes='open', lid=0.22, lid_low=0.38, gaze_x=0.0, gaze_z=0.0, brow_dy=0.014, brow_ang=-12.0,
                      mouth='soft', blush=0.35, cheek=1.04),
    'wave': dict(eyes='open', lid=0.0, lid_low=0.0, gaze_x=0.0, gaze_z=4.0, brow_dy=0.024, brow_ang=8.0,
                 mouth='wave', blush=0.38, cheek=1.06),
    'surprised': dict(eyes='open', lid=-0.12, lid_low=0.0, gaze_x=0.0, gaze_z=0.0, brow_dy=0.035, brow_ang=2.0,
                      mouth='o_small', blush=0.2, cheek=1.0),
}

DISCRETE = {'eyes', 'mouth', 'hand_l', 'hand_r', 'trowel_hide'}
TUPLES = {'arm_l_ik', 'arm_l_pole', 'arm_r_ik', 'arm_r_pole', 'hand_l_n', 'hand_l_f', 'hand_r_n', 'hand_r_f'}

# arm IK targets in body space (rest world coordinates, BU); poles = where the elbow points
R_REST = (0.50, -0.34, 0.84)
R_POLE = (1.30, 0.35, 0.95)
L_REST = (-0.70, -0.08, 0.80)
L_POLE = (-1.30, 0.55, 1.10)

BASE = dict(
    view_yaw=12.0, root_x=0.0, root_y=0.0, root_z=0.0, squash=1.0, root_roll=0.0, root_yaw=0.0,
    body_x=0.0, body_drop=0.0, body_pitch=0.0, body_roll=0.0, body_yaw=0.0,
    leg_l_pitch=0.0, leg_l_out=0.0, leg_r_pitch=0.0, leg_r_out=0.0,
    arm_l_ik=L_REST, arm_l_pole=L_POLE, arm_r_ik=R_REST, arm_r_pole=R_POLE,
    arm_l_wpitch=0.0, arm_l_wroll=0.0, arm_l_wtwist=0.0, arm_r_wpitch=0.0, arm_r_wroll=0.0, arm_r_wtwist=0.0,
    hand_l='relax', hand_r='grip',
    hand_l_n=(1.0, 0.25, 0.0), hand_l_f=(0.12, -0.12, -1.0), hand_r_n=(-1.0, 0.0, 0.0), hand_r_f=(0.15, -1.0, -0.25),
    head_pitch=0.0, head_roll=0.0, head_yaw=0.0,
    hat_x=0.0, hat_lift=0.0, hat_pitch=0.0, hat_roll=0.0,
    trowel_tilt=7.0, trowel_lean=0.0, trowel_spin=-18.0, trowel_hide=False,
)


def expr(name):
    e = dict(EXPR[name])
    for side in ('l', 'r'):
        e.setdefault('brow_%s_dy' % side, e.get('brow_dy', 0.0))
        e.setdefault('brow_%s_ang' % side, e.get('brow_ang', 4.0))
    e.pop('brow_dy', None)
    e.pop('brow_ang', None)
    return e


def pose(face='neutral', base=None, **kw):
    d = dict(base or BASE)
    d.update(expr(face))
    d.update(kw)
    return d


K0 = pose('neutral')

# ---------------------------------------------------------------------------------------------------------------
# interpolation
# ---------------------------------------------------------------------------------------------------------------


def ease(t, kind):
    t = max(0.0, min(1.0, t))
    if kind == 'lin':
        return t
    if kind == 'in':
        return t * t * t
    if kind == 'out':
        return 1 - (1 - t) ** 3
    if kind == 'step':
        return 0.0 if t < 1 else 1.0
    if kind == 'back':  # ease-out with a small overshoot (Back.easeOut, s = 1.2)
        s = 1.2
        u = t - 1
        return u * u * ((s + 1) * u + s) + 1
    return t * t * (3 - 2 * t)  # 'io' smoothstep


def lerp(a, b, t):
    return a + (b - a) * t


def blend(p0, p1, t):
    out = {}
    for k, v0 in p0.items():
        v1 = p1.get(k, v0)
        if k in DISCRETE or isinstance(v0, str) or isinstance(v0, bool):
            out[k] = v0 if t < 1.0 else v1
        elif k in TUPLES:
            out[k] = tuple(lerp(a, b, t) for a, b in zip(v0, v1))
        else:
            out[k] = lerp(v0, v1, t)
    return out


def at_keys(keys, f):
    """keys: [(frame, pose, ease_into_this_key)], sorted. Holds before the first and after the last key."""
    if f <= keys[0][0]:
        return dict(keys[0][1])
    for (f0, p0, _e0), (f1, p1, e1) in zip(keys, keys[1:]):
        if f0 <= f < f1:
            t = ease((f - f0) / float(f1 - f0), e1)
            return blend(p0, p1, t)
    return dict(keys[-1][1])


def body_height(p):
    return p['root_z'] - p['body_drop'] * 0.5


def evaluate(clip, f):
    keys = clip['keys']
    p = at_keys(keys, f)
    fn = clip.get('fn')
    if fn:
        fn(p, f)
    lag = clip.get('hat_lag', 3)
    if lag and not clip.get('hat_manual'):
        q = at_keys(keys, f - lag) if f - lag >= 0 or not clip.get('loop') else at_keys(keys, f - lag + clip['n'])
        if fn:
            fn(q, f - lag if f - lag >= 0 or not clip.get('loop') else f - lag + clip['n'])
        dz = body_height(q) - body_height(p)
        p['hat_lift'] += max(-0.006, 0.7 * dz)
        p['hat_roll'] += 0.8 * ((q['body_roll'] + q['head_roll'] + q['root_roll']) -
                                (p['body_roll'] + p['head_roll'] + p['root_roll']))
        p['hat_pitch'] += 0.6 * ((q['body_pitch'] + q['head_pitch']) - (p['body_pitch'] + p['head_pitch']))
    return p


# ---------------------------------------------------------------------------------------------------------------
# clips (frame counts, fps, loops and event frames = art/3d/spec/frames.json; drawings on twos)
# ---------------------------------------------------------------------------------------------------------------


def _w(ph, off):
    """sin wave that is 0 at frame 0 (so idle frame 0 is exactly K0)."""
    return math.sin(ph + off) - math.sin(off)


def idle_fn(p, f):
    ph = 2 * math.pi * f / 48.0
    p['body_roll'] += 1.6 * _w(ph, 0.0)
    p['body_x'] += 0.016 * _w(ph, 0.0)
    p['head_roll'] += -0.9 * _w(ph, -0.6)
    p['head_pitch'] += 0.8 * _w(2 * ph, 0.0)
    p['trowel_tilt'] += 3.0 * _w(ph, -0.9)
    lx, ly, lz = p['arm_l_ik']
    p['arm_l_ik'] = (lx + 0.010 * _w(ph, -0.4), ly, lz + 0.006 * _w(2 * ph, 0.0))
    rx, ry, rz = p['arm_r_ik']
    p['arm_r_ik'] = (rx + 0.008 * _w(ph, -0.3), ry, rz + 0.006 * _w(2 * ph, 0.5))


IDLE = dict(
    n=48, fps=24, loop=(0, 47), events={30: 'blink'}, fn=idle_fn, hat_lag=4,
    samples=[0, 6, 12, 18, 24, 30, 32, 36, 42],
    timeline_override={30: 30, 31: 30, 34: 30, 35: 30},
)
# blink drawings: 30 = half lid, 32 = closed (on the pose of frame 30 / 32)
IDLE['keys'] = [(0, K0, 'io'), (29, K0, 'io'), (30, pose('neutral', lid=0.55), 'step'),
                (32, pose('neutral', eyes='closed'), 'step'), (34, pose('neutral', lid=0.55), 'step'),
                (36, K0, 'step')]

CROUCH_ARMS = dict(arm_r_ik=(0.62, -0.10, 0.74), arm_r_pole=(1.4, 0.4, 1.0), arm_l_ik=(-0.70, 0.02, 0.70),
                   arm_l_pole=(-1.4, 0.6, 1.0), hand_l='fist', hand_l_n=(1.0, 0.0, 0.0), hand_l_f=(0.0, 0.3, -1.0),
                   hand_r_n=(-1.0, 0.0, 0.0), hand_r_f=(0.1, -1.0, -0.4))
UP_HANDS = dict(hand_l='fist', hand_l_n=(0.5, -1.0, 0.0), hand_l_f=(0.0, -0.15, 1.0),
                hand_r_n=(-1.0, -0.1, 0.0), hand_r_f=(0.1, -1.0, 0.35))
FISTS_UP = dict(arm_r_ik=(0.70, -0.16, 1.80), arm_r_pole=(1.6, 0.1, 1.2), arm_l_ik=(-0.70, -0.16, 1.80),
                arm_l_pole=(-1.6, 0.1, 1.2), trowel_tilt=-4.0, trowel_spin=-10.0, **UP_HANDS)
FISTS_MID = dict(arm_r_ik=(0.74, -0.24, 1.50), arm_r_pole=(1.6, 0.1, 1.0), arm_l_ik=(-0.74, -0.24, 1.50),
                 arm_l_pole=(-1.6, 0.1, 1.0), trowel_tilt=6.0, **UP_HANDS)

CHEER = dict(
    n=24, fps=24, loop=None, events={4: 'lift_off', 10: 'apex', 14: 'land'}, hat_lag=2,
    keys=[(0, K0, 'io'),
          (3, pose('squeeze', squash=0.91, body_drop=0.045, body_pitch=4.0, head_pitch=4.0, **CROUCH_ARMS), 'out'),
          (4, pose('cheer', squash=1.05, root_z=0.02, **FISTS_MID), 'lin'),
          (7, pose('cheer', squash=1.08, root_z=0.10, head_pitch=-4.0, **FISTS_UP), 'out'),
          (10, pose('cheer', squash=1.02, root_z=0.13, head_pitch=-5.0, leg_l_pitch=-6, leg_r_pitch=-6, **FISTS_UP), 'out'),
          (13, pose('cheer', squash=1.04, root_z=0.03, head_pitch=-3.0, **FISTS_UP), 'in'),
          (14, pose('cheer', squash=0.90, body_drop=0.03, root_z=0.0, head_pitch=2.0, **FISTS_UP), 'lin'),
          (16, pose('cheer', squash=1.03, head_pitch=-2.0, **FISTS_MID), 'out'),
          (19, pose('cheer', squash=0.99, head_pitch=0.0, arm_r_ik=(0.60, -0.36, 1.05), arm_l_ik=(-0.78, -0.18, 1.00),
                    hand_l='fist', trowel_tilt=4.0), 'io'),
          (21, pose('neutral', mouth='grin', blush=0.35), 'io'),
          (23, K0, 'io')],
    samples=[0, 2, 4, 6, 8, 10, 12, 14, 16, 21, 23],
)

THINK_A = dict(arm_l_ik=(-0.22, -0.54, 1.30), arm_l_pole=(-1.0, -0.5, 0.5), hand_l='index',
               hand_l_n=(0.35, 1.0, 0.0), hand_l_f=(0.15, -0.1, 1.0), head_roll=3.0, head_yaw=4.0, body_roll=-1.0)
THINK = dict(
    n=36, fps=24, loop=(8, 35), events={12: 'tap', 24: 'tap', 28: 'blink'}, hat_lag=3,
    keys=[(0, K0, 'io'),
          (8, pose('think', **THINK_A), 'back'),
          (11, pose('think', **dict(THINK_A, arm_l_ik=(-0.22, -0.56, 1.32))), 'io'),
          (12, pose('think', **THINK_A), 'lin'),
          (16, pose('think', **dict(THINK_A, gaze_x=20.0, gaze_z=12.0, head_yaw=7.0)), 'io'),
          (23, pose('think', **dict(THINK_A, gaze_x=20.0, gaze_z=12.0, head_yaw=7.0,
                                    arm_l_ik=(-0.22, -0.56, 1.32))), 'io'),
          (24, pose('think', **dict(THINK_A, gaze_x=20.0, gaze_z=12.0, head_yaw=7.0)), 'lin'),
          (28, pose('think', eyes='closed', **dict(THINK_A, gaze_x=12.0, head_yaw=5.0)), 'step'),
          (30, pose('think', **THINK_A), 'step'),
          (35, pose('think', **THINK_A), 'io')],
    samples=[0, 2, 4, 6, 8, 11, 16, 23, 28],
    timeline_override={12: 8, 13: 8, 14: 8, 15: 8, 24: 16, 25: 16, 26: 16, 27: 16, 30: 8, 31: 8, 32: 8, 33: 8, 34: 8,
                       35: 8, 17: 16, 18: 16, 19: 16, 20: 16, 21: 16, 22: 16, 9: 8, 10: 8},
    out=['@6', '@6', '@4', '@4', '@2', '@2', '@0', '@0'],
)

PALM_UP = dict(hand_l='open', hand_l_n=(0.1, -0.2, 1.0), hand_l_f=(-0.35, -1.0, 0.2))
TALK_TOP = dict(arm_l_ik=(-0.64, -0.46, 1.06), arm_l_pole=(-1.3, 0.2, 0.8), **PALM_UP)
TALK = dict(
    n=24, fps=24, loop=(0, 23), events={0: 'beat', 12: 'beat'}, hat_lag=3,
    keys=[(0, K0, 'io'),
          (2, pose('talk', mouth='A', arm_l_ik=(-0.70, -0.30, 0.88), arm_l_pole=(-1.3, 0.3, 0.9), **PALM_UP), 'out'),
          (4, pose('talk', mouth='O', head_pitch=-2.0, arm_l_ik=(-0.68, -0.42, 1.00), arm_l_pole=(-1.3, 0.2, 0.8),
                   **PALM_UP), 'out'),
          (6, pose('talk', mouth='E', head_pitch=-1.0, **TALK_TOP), 'back'),
          (8, pose('talk', mouth='M', **dict(TALK_TOP, arm_l_ik=(-0.62, -0.47, 1.08))), 'io'),
          (10, pose('talk', mouth='A', head_roll=-1.5, **dict(TALK_TOP, arm_l_ik=(-0.60, -0.48, 1.10))), 'io'),
          (12, pose('talk', mouth='A2', head_pitch=4.0, head_roll=-2.0, body_pitch=1.5,
                    **dict(TALK_TOP, arm_l_ik=(-0.58, -0.50, 1.04))), 'out'),
          (14, pose('talk', mouth='O', head_pitch=2.0, head_roll=-1.0, body_pitch=1.0,
                    **dict(TALK_TOP, arm_l_ik=(-0.62, -0.48, 1.08))), 'io'),
          (18, pose('talk', mouth='half', head_pitch=0.0, arm_l_ik=(-0.68, -0.36, 0.95), arm_l_pole=(-1.3, 0.3, 0.9),
                    **PALM_UP), 'io'),
          (20, pose('talk', mouth='M', arm_l_ik=(-0.70, -0.20, 0.84), arm_l_pole=(-1.3, 0.4, 1.0), hand_l='relax'), 'io'),
          (22, K0, 'io'), (23, K0, 'io')],
    samples=[0, 2, 4, 6, 8, 10, 12, 14, 18, 22],
    timeline_override={16: 6, 17: 6, 22: 0, 23: 0},
)

PT_HAND = dict(hand_r_n=(0.0, 0.0, -1.0), hand_r_f=(0.35, -1.0, 0.0))
PT_HIT = dict(arm_r_ik=(1.13, -0.16, 1.30), arm_r_pole=(1.0, 0.8, 0.4), trowel_tilt=96.0, trowel_spin=-6.0,
              trowel_lean=-8.0, **PT_HAND)
PT_HOLD = dict(arm_r_ik=(1.08, -0.16, 1.27), arm_r_pole=(1.0, 0.8, 0.4), trowel_tilt=90.0, trowel_spin=-6.0,
               trowel_lean=-8.0, **PT_HAND)
POINT = dict(
    n=18, fps=24, loop=(12, 17), events={6: 'point_hit', 12: 'hold_start', 14: 'glint'}, hat_lag=2,
    keys=[(0, K0, 'io'),
          (3, pose('point', body_roll=-4.0, root_roll=-1.5, head_roll=-2.0, arm_r_ik=(0.36, -0.10, 1.12),
                   arm_r_pole=(1.2, 0.6, 0.8), trowel_tilt=-22.0, trowel_lean=6.0, squash=0.97), 'io'),
          (5, pose('point', body_roll=2.0, head_roll=1.0, arm_r_ik=(0.92, -0.20, 1.24), arm_r_pole=(1.0, 0.8, 0.5),
                   trowel_tilt=70.0, trowel_lean=-6.0, squash=1.02, **PT_HAND), 'in'),
          (6, pose('point', body_roll=3.5, root_roll=1.5, head_roll=2.0, **PT_HIT), 'out'),
          (9, pose('point', body_roll=1.0, root_roll=0.0, head_roll=0.5,
                   **dict(PT_HOLD, arm_r_ik=(1.05, -0.16, 1.25), trowel_tilt=87.0)), 'io'),
          (12, pose('point', body_roll=1.5, head_roll=1.0, **PT_HOLD), 'io'),
          (15, pose('point', body_roll=1.5, head_roll=1.5, head_pitch=1.5,
                    **dict(PT_HOLD, arm_r_ik=(1.08, -0.16, 1.29), trowel_tilt=92.0)), 'io'),
          (17, pose('point', body_roll=1.5, head_roll=1.0, **PT_HOLD), 'io')],
    samples=[0, 2, 4, 6, 8, 12, 15],
    timeline_override={14: 12, 17: 15},
    extra={'R': pose('point', arm_r_ik=(0.72, -0.30, 1.05), arm_r_pole=(1.2, 0.6, 0.7), trowel_tilt=48.0,
                     hand_r_n=(-0.6, 0.0, -0.6), hand_r_f=(0.25, -1.0, -0.1),
                     trowel_lean=-4.0, body_roll=0.5)},
    out=['@R', '@R', '@0', '@0'],
)

ENC_PALM = dict(arm_l_ik=(-0.50, -0.50, 1.18), arm_l_pole=(-1.0, -0.3, 0.6), hand_l='open',
                hand_l_n=(0.15, -1.0, 0.1), hand_l_f=(0.1, -0.15, 1.0))
ENC_THUMB = dict(arm_l_ik=(-0.58, -0.44, 1.30), arm_l_pole=(-1.4, 0.1, 0.6), hand_l='thumb',
                 hand_l_n=(1.0, 0.0, 0.0), hand_l_f=(0.0, -1.0, 0.15))
ENCOURAGE = dict(
    n=24, fps=24, loop=None, events={9: 'gesture', 15: 'pump'}, hat_lag=3,
    keys=[(0, K0, 'io'),
          (4, pose('encourage', head_roll=-5.0, head_pitch=3.0, arm_l_ik=(-0.66, -0.20, 0.86)), 'io'),
          (7, pose('encourage', head_roll=-4.0, head_pitch=-1.0, body_pitch=-4.0,
                   arm_l_ik=(-0.56, -0.30, 0.98), arm_l_pole=(-1.3, 0.2, 0.8), hand_l='open'), 'io'),
          (9, pose('encourage', head_roll=-3.0, head_pitch=2.0, body_pitch=3.0, mouth='soft',
                   arm_r_ik=(0.56, -0.40, 0.92), **ENC_PALM), 'back'),
          (12, pose('encourage', head_roll=-2.0, head_pitch=1.0, body_pitch=2.0, arm_r_ik=(0.56, -0.40, 0.92),
                    **dict(ENC_PALM, arm_l_ik=(-0.48, -0.58, 1.06))), 'io'),
          (15, pose('encourage', eyes='happy', mouth='grin', brow_l_ang=8.0, brow_r_ang=8.0, head_roll=2.0,
                    head_pitch=-3.0, squash=0.97, arm_r_ik=(0.54, -0.40, 0.96), **dict(ENC_THUMB,
                                                                                       arm_l_ik=(-0.58, -0.44, 1.36))),
           'back'),
          (17, pose('encourage', eyes='happy', mouth='grin', brow_l_ang=8.0, brow_r_ang=8.0, head_roll=1.0,
                    head_pitch=-1.0, squash=1.01, arm_r_ik=(0.54, -0.40, 0.94), **ENC_THUMB), 'io'),
          (20, pose('neutral', mouth='soft', blush=0.3, arm_l_ik=(-0.66, -0.18, 0.86), hand_l='relax'), 'io'),
          (23, K0, 'io')],
    samples=[0, 2, 4, 7, 9, 12, 15, 17, 20, 23],
)


def wave_phi(f):
    return 25.0 * math.cos(2 * math.pi * (f - 9) / 9.0)


WAVE_ELBOW = (-0.78, -0.16, 1.40)


def wave_fn(p, f):
    if f < 6:
        return
    phi = wave_phi(f)
    lag = wave_phi(f - 2)
    L2 = 0.25
    a = math.radians(-phi)
    p['arm_l_ik'] = (WAVE_ELBOW[0] + L2 * math.sin(a) - 0.04, WAVE_ELBOW[1] - 0.04, WAVE_ELBOW[2] + L2 * math.cos(a))
    h = math.radians(-(phi + 0.6 * (lag - phi)))
    p['hand_l_f'] = (math.sin(h), 0.0, math.cos(h))
    p['body_roll'] += -2.0 * math.cos(2 * math.pi * (f - 9) / 9.0)


WAVE_UP = dict(arm_l_ik=(-0.86, -0.20, 1.62), arm_l_pole=(-1.4, 0.3, 0.5), hand_l='open',
               hand_l_n=(0.2, -1.0, 0.0), hand_l_f=(0.0, 0.0, 1.0))
WAVE = dict(
    n=24, fps=24, loop=(6, 23), events={9: 'wave_peak', 18: 'wave_peak'}, fn=wave_fn, hat_lag=3, set='stage',
    keys=[(0, K0, 'io'),
          (2, pose('wave', body_roll=-2.0, arm_l_ik=(-0.80, -0.22, 1.08), arm_l_pole=(-1.4, 0.4, 0.6), hand_l='open',
                   hand_l_n=(0.5, -1.0, 0.3), hand_l_f=(-0.2, -0.6, 0.6)), 'out'),
          (4, pose('wave', body_roll=-3.0, head_roll=-2.0, **WAVE_UP), 'out'),
          (6, pose('wave', body_roll=-2.0, head_roll=-2.0, **WAVE_UP), 'io'),
          (23, pose('wave', body_roll=-2.0, head_roll=-2.0, **WAVE_UP), 'io')],
    samples=[0, 2, 4, 6, 8, 10, 12, 14],
    timeline_override={15: 6, 16: 6, 17: 8, 18: 8, 19: 10, 20: 10, 21: 12, 22: 12, 23: 14, 7: 6, 9: 8, 11: 10, 13: 12},
    out=['@4', '@4', '@2', '@2', '@0', '@0'],
)

BC_UP = dict(arm_r_ik=(0.62, -0.12, 1.86), arm_r_pole=(1.6, 0.2, 1.3), arm_l_ik=(-0.74, -0.14, 1.84),
             arm_l_pole=(-1.6, 0.2, 1.3), trowel_tilt=-8.0, trowel_spin=-14.0, **UP_HANDS)
BC_CROUCH = dict(arm_r_ik=(0.66, 0.0, 0.70), arm_r_pole=(1.4, 0.6, 1.0), arm_l_ik=(-0.72, 0.06, 0.66),
                 arm_l_pole=(-1.4, 0.7, 1.0), hand_l='fist', hand_l_n=(1.0, 0.0, 0.0), hand_l_f=(0.0, 0.3, -1.0),
                 hand_r_n=(-1.0, 0.0, 0.0), hand_r_f=(0.1, -1.0, -0.4))


def big_fn(p, f):
    if f < 24:
        return
    ph = 2 * math.pi * (f - 24) / 24.0
    sway = -math.cos(ph)  # -1 at 24 (left), +1 at 36 (right)
    p['body_roll'] += 6.0 * sway
    p['head_roll'] += 3.0 * sway
    p['root_x'] += 0.03 * sway
    for c in (28, 40):
        u = (f - c) / 3.0
        if -1.0 <= u <= 1.0:
            p['root_z'] += 0.075 * math.cos(u * math.pi / 2) ** 2
        if -2.0 <= u < -0.66:
            p['squash'] *= 0.95
    lx, ly, lz = p['arm_l_ik']
    rx, ry, rz = p['arm_r_ik']
    p['arm_l_ik'] = (lx, ly, lz + 0.05 * math.sin(2 * ph))
    p['arm_r_ik'] = (rx, ry, rz + 0.05 * math.sin(2 * ph + 1.2))
    p['trowel_tilt'] += 10.0 * sway


BIG = dict(
    n=48, fps=24, loop=(24, 47), events={6: 'jump', 15: 'land', 22: 'hat_land', 28: 'hop', 40: 'hop'}, fn=big_fn,
    hat_manual=True, set='stage',
    keys=[(0, K0, 'io'),
          (3, pose('squeeze', squash=0.88, body_drop=0.06, body_pitch=6.0, head_pitch=6.0, **BC_CROUCH), 'out'),
          (5, pose('squeeze', squash=0.85, body_drop=0.075, body_pitch=7.0, head_pitch=7.0, **BC_CROUCH), 'io'),
          (6, pose('big', squash=1.10, root_z=0.06, body_pitch=-2.0, head_pitch=-4.0, hat_lift=-0.004, **dict(
              BC_UP, arm_r_ik=(0.74, -0.30, 1.40), arm_l_ik=(-0.80, -0.30, 1.40))), 'lin'),
          (8, pose('big', squash=1.08, root_z=0.30, head_pitch=-6.0, hat_lift=0.10, hat_roll=6.0, leg_l_pitch=-10,
                   leg_r_pitch=8, leg_l_out=6, leg_r_out=6, **BC_UP), 'out'),
          (10, pose('big', squash=1.03, root_z=0.40, head_pitch=-7.0, hat_lift=0.26, hat_roll=12.0, hat_pitch=-8.0,
                    leg_l_pitch=-14, leg_r_pitch=10, leg_l_out=10, leg_r_out=10, **BC_UP), 'out'),
          (12, pose('big', squash=1.02, root_z=0.32, head_pitch=-5.0, hat_lift=0.36, hat_roll=16.0, hat_pitch=-10.0,
                    leg_l_pitch=-10, leg_r_pitch=8, leg_l_out=8, leg_r_out=8, **BC_UP), 'in'),
          (14, pose('big', squash=1.05, root_z=0.06, head_pitch=-3.0, hat_lift=0.38, hat_roll=12.0, hat_pitch=-6.0,
                    **BC_UP), 'in'),
          (15, pose('big', squash=0.86, body_drop=0.05, head_pitch=4.0, hat_lift=0.34, hat_roll=8.0, **BC_UP), 'lin'),
          (17, pose('big', squash=1.04, head_pitch=-3.0, hat_lift=0.22, hat_roll=-4.0, **BC_UP), 'out'),
          (19, pose('big', squash=0.99, head_pitch=-1.0, hat_lift=0.10, hat_roll=-6.0, **BC_UP), 'io'),
          (21, pose('big', squash=1.0, head_pitch=0.0, hat_lift=0.02, hat_roll=-3.0, **BC_UP), 'in'),
          (22, pose('big', squash=0.985, head_pitch=1.5, hat_lift=-0.004, hat_roll=1.5, **BC_UP), 'lin'),
          (24, pose('big', squash=1.0, **BC_UP), 'out'),
          (47, pose('big', squash=1.0, **BC_UP), 'io')],
    samples=[0, 3, 6, 8, 10, 12, 14, 15, 17, 22, 24, 28, 31, 36, 40, 43],
)

CLIPS = {
    'king_idle': dict(IDLE, set='corner'),
    'king_cheer': dict(CHEER, set='corner'),
    'king_think': dict(THINK, set='corner'),
    'king_talk': dict(TALK, set='corner'),
    'king_point': dict(POINT, set='corner'),
    'king_encourage': dict(ENCOURAGE, set='corner'),
    'king_wave': dict(WAVE, set='stage'),
    'king_big_cheer': dict(BIG, set='stage'),
}
# king_home_idle = the king_idle drawings at stage size (same frames, clip scale differs)
ALIAS_CLIPS = {'king_home_idle': ('king_idle', 'stage')}

# busts (256 x 320 design, head and shoulders): expression + small head turn
BUSTS = {
    'neutral': pose('neutral'),
    'happy': pose('cheer', mouth='wave', eyes='happy'),
    'think': pose('think', head_roll=3.0, head_yaw=4.0),
    'talk': pose('talk', mouth='A2'),
    'encourage': pose('encourage', head_roll=-4.0),
    'surprised': pose('surprised'),
}
# stills: key pose of each clip (alias of one drawing; frames.json chr_king_still_<pose>)
STILLS = {'idle': ('king_idle', 0), 'cheer': ('king_cheer', 10), 'big_cheer': ('king_big_cheer', 10),
          'think': ('king_think', 8), 'talk': ('king_talk', 12), 'point': ('king_point', 12),
          'encourage': ('king_encourage', 15), 'wave': ('king_wave', 8)}

# ---------------------------------------------------------------------------------------------------------------
# drawing plan: which frames are rendered, how 24 fps frames map to drawings
# ---------------------------------------------------------------------------------------------------------------
K0_NAME = 'chr_king_idle_00'


def same_pose(a, b):
    for k, v in a.items():
        w = b.get(k)
        if isinstance(v, tuple):
            if any(abs(x - y) > 1e-6 for x, y in zip(v, w)):
                return False
        elif isinstance(v, float):
            if abs(v - w) > 1e-6:
                return False
        elif v != w:
            return False
    return True


def plan():
    """Returns (drawings: {name: pose}, clips: {name: {frames: [drawing per frame], ...}})."""
    drawings = {}
    clips = {}
    k0 = dict(K0)
    drawings[K0_NAME] = k0
    for cname, c in CLIPS.items():
        short = cname[len('king_'):]
        names = {}
        idx = 0
        for s in c['samples']:
            p = evaluate(c, s)
            if s == 0 or same_pose(at_keys(c['keys'], s), K0) and not c.get('fn'):
                names[s] = K0_NAME
                continue
            idx += 1
            nm = 'chr_king_%s_%02d' % (short, idx)
            drawings[nm] = p
            names[s] = nm
        for key, p in c.get('extra', {}).items():
            idx += 1
            nm = 'chr_king_%s_%02d' % (short, idx)
            drawings[nm] = p
            names['@' + key] = nm
        tl = []
        smp = sorted(c['samples'])
        for f in range(c['n']):
            src = c.get('timeline_override', {}).get(f)
            if src is None:
                src = max(s for s in smp if s <= f)
            tl.append(names[src])
        out = None
        if c.get('out'):
            out = [names[int(o[1:])] if o[1:].isdigit() else names[o] for o in c['out']]
        clips[cname] = dict(frames=tl, fps=c['fps'], loop=({'start': c['loop'][0], 'end': c['loop'][1]}
                                                            if c.get('loop') else False),
                            out=out, events={str(k): v for k, v in sorted(c['events'].items())}, set=c['set'],
                            samples={str(k): v for k, v in names.items()})
    for alias, (src, st) in ALIAS_CLIPS.items():
        clips[alias] = dict(clips[src], set=st, aliasOf=src)
    return drawings, clips


# ---------------------------------------------------------------------------------------------------------------
# rendering
# ---------------------------------------------------------------------------------------------------------------
# master canvas (BU): wide enough for the point pose and the big-cheer hat; the root (feet on the ground) sits
# ROOT_Y_BU above the bottom edge, on the vertical centre line. Store scale = PX_STORE px per BU (post_king.py).
CANVAS_W_BU = 4.2
CANVAS_H_BU = 4.15
ROOT_Y_BU = 0.35
BUST_W_BU, BUST_H_BU, BUST_ROOT_BU = 1.28, 1.6, -1.15  # 256 x 320 design box = 1.28 x 1.6 BU, z 1.15 .. 2.75 (pivot = face)
STORE_PX_PER_BU = 100.0   # 2x master = 200 px/BU; post_king.py --div 200/94 stores 94 px/BU (16 MB budget)
BUST_PX_PER_BU = 150.0   # store; the tutorial portrait (circle of 132 design px) stays sharp (scale 0.75)


def project(scene, cam, co):
    from bpy_extras.object_utils import world_to_camera_view
    v = world_to_camera_view(scene, cam, co)
    return (v.x * scene.render.resolution_x, (1 - v.y) * scene.render.resolution_y)


def main():
    import bpy
    from mathutils import Vector
    import build_king as bk

    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []

    def arg(name, default):
        return argv[argv.index(name) + 1] if name in argv else default

    out = os.path.abspath(arg('--out', os.path.join(HERE, '..', '..', 'out', 'king', 'raw')))
    px = float(arg('--px', str(2 * STORE_PX_PER_BU)))
    samples = int(arg('--samples', '64'))
    only = arg('--only', '')
    clip_filter = arg('--clips', '')
    keys_only = '--keys' in argv
    os.makedirs(out, exist_ok=True)
    king = bk.build_scene()
    bk.setup_render(samples)
    scene = bpy.context.scene
    drawings, clips = plan()
    if clip_filter:
        want = set(clip_filter.split(','))
        keep = {K0_NAME}
        for cn in want:
            keep.update(clips[cn]['frames'])
            keep.update(clips[cn]['out'] or [])
        drawings = {k: v for k, v in drawings.items() if k in keep}
    if only:
        drawings = {k: v for k, v in drawings.items() if k in only.split(',')}
    manifest = dict(version=1, generator='art/3d/blender/king/animate_king.py', pxPerBU=px,
                    canvas=dict(wBU=CANVAS_W_BU, hBU=CANVAS_H_BU, rootYBU=ROOT_Y_BU), drawings={}, clips=clips,
                    busts={}, stills={k: [c, f] for k, (c, f) in STILLS.items()}, k0=K0_NAME)
    t0 = time.time()
    cam = bk.setup_camera(CANVAS_W_BU, CANVAS_H_BU, ROOT_Y_BU, px)
    bpy.context.view_layer.update()
    root_px = project(scene, cam, Vector((0, 0, 0)))
    for i, (name, p) in enumerate(sorted(drawings.items())):
        king.apply(p)
        gx = king.j['root'].matrix_world.translation
        ground = Vector((gx.x, gx.y, 0.0))
        path = os.path.join(out, name + '.png')
        manifest['drawings'][name] = dict(file=name + '.png', root=list(root_px), shadow=list(project(scene, cam, ground)),
                                          height=p['root_z'] + max(0.0, p.get('squash', 1.0) - 1.0) * 0.0)
        if not keys_only or name == K0_NAME:
            scene.render.filepath = path
            t1 = time.time()
            bpy.ops.render.render(write_still=True)
            print('RENDERED %s (%d/%d) %.1fs' % (name, i + 1, len(drawings), time.time() - t1), flush=True)
    # busts
    if not only or any(o.startswith('chr_king_bust') for o in only.split(',')):
        bpx = float(arg('--bust-px', str(2 * BUST_PX_PER_BU)))
        cam = bk.setup_camera(BUST_W_BU, BUST_H_BU, BUST_ROOT_BU, bpx)
        for name, p in BUSTS.items():
            fn = 'chr_king_bust_%s' % name
            if only and fn not in only.split(','):
                continue
            q = dict(p)
            q['view_yaw'] = 10.0
            q['trowel_hide'] = True
            q['arm_r_ik'] = (0.70, -0.06, 0.80)
            q['hand_r'] = 'relax'
            king.apply(q)
            scene.render.filepath = os.path.join(out, fn + '.png')
            t1 = time.time()
            bpy.ops.render.render(write_still=True)
            manifest['busts'][fn] = dict(file=fn + '.png', pxPerBU=bpx, storePxPerBU=BUST_PX_PER_BU,
                                         canvas=dict(wBU=BUST_W_BU, hBU=BUST_H_BU))
            print('RENDERED %s %.1fs' % (fn, time.time() - t1), flush=True)
    mpath = os.path.join(out, 'manifest.json')
    if os.path.exists(mpath) and (only or clip_filter):
        old = json.load(open(mpath))
        old['drawings'].update(manifest['drawings'])
        old['busts'].update(manifest['busts'])
        old['clips'] = manifest['clips']
        manifest = old
    with open(mpath, 'w') as fh:
        json.dump(manifest, fh, indent=1, sort_keys=True)
    print('DONE %d drawings in %.1f min' % (len(drawings), (time.time() - t0) / 60.0), flush=True)


if __name__ == '__main__':
    main()
