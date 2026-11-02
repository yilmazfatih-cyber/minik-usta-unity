"""Tuna and Kepche: poses, clips and the drawing renderer (art turn 1; D-100, D-101).

    blender -b --factory-startup -noaudio --python art/3d/blender/crew/animate_crew.py -- \
        --out art/3d/out/crew/raw [--px 224] [--samples 64] [--only name,name] [--clips tuna_home_idle]

Same timeline language as Usta Kral (art/3d/blender/king/animate_king.py): 24 fps, drawings on twos (12 per second),
loops start and end on the rest drawing, procedural layers (sway, wave, wag, pant) are phase-continuous so the loop has
no seam. Outputs (git-ignored, art/3d/out/crew/raw): <drawing>.png (2x master) + manifest.json in the B1k format, so
art/3d/blender/king/post_king.py trims them (--raw/--out/--div) and tools/art/pack3d.mjs --name crew packs them.

Clips (crew.json):
  tuna_home_idle   48 frames, loop 0-47, 24 drawings: waves with the screen-left glove, the other glove on the hip,
                   weight shift, knee bounce, head counter-tilt, blink at 30; events 3 / 15 / 27 / 39 wave_peak, 30 blink
  kepce_home_idle  48 frames, loop 0-47, 24 drawings: tail wag 2 Hz, panting, breathing, slow head tilt, ear follow,
                   blink at 30; events 30 blink
  kepce_corner_idle the kepce_home_idle drawings at the corner set scale (the level screen corner, ART_3D 13.3)
Stills (v4 atlas names, placed by post_env.py): chr_tuna_cheer, chr_tuna_idle, chr_tuna_think, chr_tuna_bust,
chr_kepce_idle.
"""
import json
import math
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
for p in (HERE, os.path.join(HERE, '..', 'king')):
    if p not in sys.path:
        sys.path.insert(0, p)

TAU = 2 * math.pi

T0 = dict(view_yaw=14.0, arm_l_out=9.0, arm_r_out=9.0, elbow_l=14.0, elbow_r=14.0, hand_l='relax', hand_r='relax',
          wrist_l_twist=0.0, wrist_r_twist=0.0, mouth='smile', eyes='open', lid=0.08, brow_ang=-4.0, hat_pitch=-4.0)

K_T0 = dict(view_yaw=34.0, tail_wag=0.0, tail_lift=8.0, jaw=6.0, breath=0.0)


def tuna(**kw):
    d = dict(T0)
    d.update(kw)
    return d


def kepce(**kw):
    d = dict(K_T0)
    d.update(kw)
    return d


def _w(ph, off=0.0):
    """sin wave that is 0 at phase 0 (frame 0 = the rest drawing)."""
    return math.sin(ph + off) - math.sin(off)


def tuna_home(f):
    """Friendly wave from the home screen: the screen-left glove high (waving 2 Hz), the other glove on the hip."""
    ph = TAU * f / 48.0
    wv = math.sin(TAU * f / 12.0)          # 4 waves per loop, phase-continuous
    lag = math.sin(TAU * (f - 2) / 12.0)   # the glove follows the forearm 2 frames late (overlapping action)
    p = tuna(mouth='wave', brow_ang=6.0, brow_dy=0.006)
    p.update(arm_l_out=112.0 + 3.0 * _w(ph), arm_l_fwd=8.0, elbow_l=10.0, elbow_l_out=70.0 + 22.0 * wv,
             hand_l='open', wrist_l_twist=90.0, wrist_l_roll=12.0 * lag,
             arm_r_out=34.0, arm_r_fwd=-12.0, elbow_r=62.0, elbow_r_out=-48.0, hand_r='fist', wrist_r_twist=30.0,
             wrist_r_pitch=-10.0)
    p['hips_x'] = 0.012 * _w(ph)
    p['chest_roll'] = 2.2 * _w(ph)
    p['hips_roll'] = -1.2 * _w(ph)
    p['head_roll'] = -2.6 * _w(ph, -0.5) + 1.2 * wv
    p['head_pitch'] = 1.0 * _w(2 * ph)
    p['squash'] = 1.0 + 0.010 * math.sin(2 * ph)
    p['root_z'] = 0.0
    p['knee_l'] = 3.0 * max(0.0, -math.sin(2 * ph))
    p['knee_r'] = 3.0 * max(0.0, -math.sin(2 * ph))
    p['hat_roll'] = -1.0 * _w(ph, -1.0)
    # blink: 30 half, 32 closed, 34 half
    if f in (30, 31, 34, 35):
        p['lid'] = 0.55
    elif f in (32, 33):
        p['eyes'] = 'closed'
    return p


def kepce_home(f):
    """Happy dog: 2 Hz tail wag, panting, breathing, slow head tilt, ears follow the head 2 frames late."""
    ph = TAU * f / 48.0
    p = kepce()
    p['tail_wag'] = 30.0 * math.sin(TAU * f / 12.0)
    p['tail_lift'] = 10.0 + 4.0 * math.sin(TAU * f / 24.0)
    pant = math.sin(TAU * f / 16.0)
    p['jaw'] = 7.0 + 6.0 * pant
    p['breath'] = 0.018 * pant
    p['head_roll'] = 5.0 * _w(ph)
    p['head_pitch'] = -2.5 * _w(ph, 1.0)
    p['ear_l'] = 7.0 * _w(TAU * (f - 3) / 48.0)
    p['ear_r'] = 7.0 * _w(TAU * (f - 3) / 48.0) - 3.0 * pant
    p['hat_roll'] = 2.0 * _w(TAU * (f - 2) / 48.0)
    p['body_roll'] = 1.2 * _w(ph)
    if f in (30, 31, 34, 35):
        p['blink'] = 0.5
    elif f in (32, 33):
        p['blink'] = 0.92
    return p


CLIPS = {
    'tuna_home_idle': dict(who='tuna', n=48, fps=24, loop=(0, 47), fn=tuna_home, set='stage',
                           events={3: 'wave_peak', 15: 'wave_peak', 27: 'wave_peak', 30: 'blink', 39: 'wave_peak'},
                           samples=list(range(0, 48, 2)), prefix='chr_tuna_home_idle'),
    'kepce_home_idle': dict(who='kepce', n=48, fps=24, loop=(0, 47), fn=kepce_home, set='stage',
                            events={30: 'blink'}, samples=list(range(0, 48, 2)), prefix='chr_kepce_idle'),
}
ALIAS_CLIPS = {'kepce_corner_idle': ('kepce_home_idle', 'corner')}

STILLS = {
    'chr_tuna_still_cheer': ('tuna', tuna(arm_l_out=148.0, arm_r_out=148.0, arm_l_fwd=10.0, arm_r_fwd=10.0, elbow_l=22.0,
                                          elbow_r=22.0, elbow_l_out=8.0, elbow_r_out=8.0, hand_l='open', hand_r='open',
                                          wrist_l_twist=-84.0, wrist_r_twist=-84.0, mouth='grin', eyes='happy',
                                          brow_ang=10.0, brow_dy=0.012, squash=1.02, head_pitch=-4.0, view_yaw=10.0)),
    'chr_tuna_still_idle': ('tuna', tuna()),
    'chr_tuna_still_think': ('tuna', tuna(arm_r_fwd=58.0, arm_r_out=-4.0, elbow_r=132.0, elbow_r_out=-26.0,
                                          hand_r='fist', wrist_r_twist=40.0, wrist_r_pitch=10.0, gaze_x=-14.0,
                                          gaze_z=16.0, brow_l_dy=0.012, brow_l_ang=8.0, brow_r_ang=-8.0, mouth='O',
                                          head_roll=5.0, head_yaw=-6.0, view_yaw=16.0)),
}
BUSTS = {'chr_tuna_bust_neutral': ('tuna', tuna(mouth='wave', brow_ang=4.0, view_yaw=10.0))}
# v4 atlas frames drawn from the crew drawings (post_env.py places them by pivot): design px per BU of each still
ALIASES = {
    'chr_tuna_cheer': ('chr_tuna_still_cheer', 133.333),
    'chr_tuna_idle': ('chr_tuna_still_idle', 160.0),
    'chr_tuna_think': ('chr_tuna_still_think', 160.0),
    'chr_tuna_bust': ('chr_tuna_bust_neutral', 256.0),
    'chr_kepce_idle': ('chr_kepce_idle_00', 150.0),
}

CANVAS_W_BU = 2.6
CANVAS_H_BU = 3.0
ROOT_Y_BU = 0.25
STORE_PX_PER_BU = 112.0   # 2x master = 224 px/BU; the crew sheet stores 112 px/BU (home: 133 design px/BU => 0.84)
BUST_W_BU, BUST_H_BU, BUST_ROOT_BU = 1.0, 1.25, -1.30   # 256 x 320 design box; head + shoulders
BUST_PX_PER_BU = 192.0
SHADOW_W_BU = {'tuna': 0.62, 'kepce': 1.30}   # contact ellipse width = 1.1 x the feet / paws span (art turn 1)


def plan():
    drawings = {}
    clips = {}
    for cname, c in CLIPS.items():
        names = {}
        idx = 0
        for s in c['samples']:
            nm = '%s_%02d' % (c['prefix'], idx)
            idx += 1
            drawings[nm] = (c['who'], c['fn'](s))
            names[s] = nm
        tl = []
        for f in range(c['n']):
            src = max(s for s in c['samples'] if s <= f)
            tl.append(names[src])
        clips[cname] = dict(frames=tl, fps=c['fps'], loop={'start': c['loop'][0], 'end': c['loop'][1]}, out=None,
                            events={str(k): v for k, v in sorted(c['events'].items())}, set=c['set'],
                            samples={str(k): v for k, v in names.items()})
    for alias, (src, st) in ALIAS_CLIPS.items():
        clips[alias] = dict(clips[src], set=st, aliasOf=src)
    for nm, (who, p) in STILLS.items():
        drawings[nm] = (who, p)
    return drawings, clips


def project(scene, cam, co):
    from bpy_extras.object_utils import world_to_camera_view
    v = world_to_camera_view(scene, cam, co)
    return (v.x * scene.render.resolution_x, (1 - v.y) * scene.render.resolution_y)


def main():
    import bpy
    from mathutils import Vector
    import build_crew as bc
    import build_king as bk

    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []

    def arg(name, default):
        return argv[argv.index(name) + 1] if name in argv else default

    out = os.path.abspath(arg('--out', os.path.join(HERE, '..', '..', 'out', 'crew', 'raw')))
    px = float(arg('--px', str(2 * STORE_PX_PER_BU)))
    samples = int(arg('--samples', '64'))
    only = [s for s in arg('--only', '').split(',') if s]
    clip_filter = [s for s in arg('--clips', '').split(',') if s]
    os.makedirs(out, exist_ok=True)
    chars = bc.build_scene()
    bk.setup_render(samples)
    scene = bpy.context.scene
    drawings, clips = plan()
    if clip_filter:
        keep = set()
        for cn in clip_filter:
            keep.update(clips[cn]['frames'])
        drawings = {k: v for k, v in drawings.items() if k in keep}
    if only:
        drawings = {k: v for k, v in drawings.items() if k in only}
    manifest = dict(version=1, generator='art/3d/blender/crew/animate_crew.py', pxPerBU=px,
                    canvas=dict(wBU=CANVAS_W_BU, hBU=CANVAS_H_BU, rootYBU=ROOT_Y_BU), drawings={}, clips=clips,
                    busts={}, stills={}, k0=None,
                    aliases={k: {'of': v[0], 'designPxPerBU': v[1]} for k, v in ALIASES.items()},
                    bustDesignPxPerBU=256.0 / BUST_W_BU)
    t0 = time.time()
    cam = bk.setup_camera(CANVAS_W_BU, CANVAS_H_BU, ROOT_Y_BU, px)
    bpy.context.view_layer.update()
    root_px = project(scene, cam, Vector((0, 0, 0)))
    cur = None
    for i, (name, (who, p)) in enumerate(sorted(drawings.items())):
        if who != cur:
            for c2 in chars.values():
                c2.hide(c2 is chars[who])
            cur = who
        chars[who].apply(p)
        rt = chars[who].j['root'].matrix_world.translation
        manifest['drawings'][name] = dict(file=name + '.png', root=list(root_px),
                                          shadow=list(project(scene, cam, Vector((rt.x, rt.y, 0.0)))),
                                          height=p.get('root_z', 0.0), shadowWBU=SHADOW_W_BU[who], shadowAlpha=0.35)
        scene.render.filepath = os.path.join(out, name + '.png')
        t1 = time.time()
        bpy.ops.render.render(write_still=True)
        print('RENDERED %s (%d/%d) %.1fs' % (name, i + 1, len(drawings), time.time() - t1), flush=True)
    if not only or any(o in BUSTS for o in only):
        bpx = float(arg('--bust-px', str(2 * BUST_PX_PER_BU)))
        bk.setup_camera(BUST_W_BU, BUST_H_BU, BUST_ROOT_BU, bpx)
        for name, (who, p) in BUSTS.items():
            for c2 in chars.values():
                c2.hide(c2 is chars[who])
            chars[who].apply(p)
            scene.render.filepath = os.path.join(out, name + '.png')
            bpy.ops.render.render(write_still=True)
            manifest['busts'][name] = dict(file=name + '.png', pxPerBU=bpx, storePxPerBU=BUST_PX_PER_BU,
                                           canvas=dict(wBU=BUST_W_BU, hBU=BUST_H_BU))
            print('RENDERED %s' % name, flush=True)
    mpath = os.path.join(out, 'manifest.json')
    if os.path.exists(mpath) and (only or clip_filter):
        old = json.load(open(mpath))
        old['drawings'].update(manifest['drawings'])
        old['busts'].update(manifest['busts'])
        old['clips'] = manifest['clips']
        old['aliases'] = manifest['aliases']
        manifest = old
    with open(mpath, 'w') as fh:
        json.dump(manifest, fh, indent=1, sort_keys=True)
    print('DONE %d drawings in %.1f min' % (len(drawings), (time.time() - t0) / 60.0), flush=True)


if __name__ == '__main__':
    main()
