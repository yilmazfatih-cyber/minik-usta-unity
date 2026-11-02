"""B1 review sheets from the PACKED resources (so the sheets also check the contract): reads
Resources/art/atlas.json + atlas_N.bytes and Resources/art3d/fx.json + fx_N.bytes and writes
  artifacts/preview/v5-blocks.png  (12 pieces x 8 colours composed from tiles + corner fills, holdable gloss, placed mortar)
  artifacts/preview/v5-fx.png      (every fx clip drawing placed by its PIVOT on its canvas: motion read-out)
  artifacts/preview/v5-kit.png     (UI kit, icons, board parts at design size)

    python3 art/3d/blender/env/preview_env.py [--out artifacts/preview]
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
RES = os.path.join(ROOT, 'Assets', 'MinikUsta', 'Resources')
argv = sys.argv[1:]
OUT = argv[argv.index('--out') + 1] if '--out' in argv else os.path.join(ROOT, 'artifacts', 'preview')
BG = (44, 38, 104, 255)


def load(json_rel, base):
    j = json.load(open(os.path.join(RES, json_rel)))
    pages = [Image.open(os.path.join(RES, a['file'] + '.bytes')).convert('RGBA') for a in j['atlases']]
    return j, pages


def frame(j, pages, name, design=True):
    f = j['frames'][name]
    im = pages[f['atlas']].crop((f['x'], f['y'], f['x'] + f['w'], f['y'] + f['h']))
    if design:
        s = f['scale']
        im = im.resize((max(1, round(f['w'] / s)), max(1, round(f['h'] / s))), Image.LANCZOS)
    return im, f


def label(d, xy, text):
    try:
        font = ImageFont.load_default(size=18)
    except TypeError:
        font = ImageFont.load_default()
    d.text(xy, text, fill=(255, 246, 210, 255), font=font)


def blocks_sheet(A, P):
    shapes = {'I2': [(0, 0), (1, 0)], 'L3': [(0, 0), (0, 1), (1, 0)], 'O4': [(0, 0), (1, 0), (0, 1), (1, 1)], 'T4': [(0, 1), (1, 1), (2, 1), (1, 0)],
              'S4': [(0, 0), (1, 0), (1, 1), (2, 1)], 'L4': [(0, 0), (0, 1), (0, 2), (1, 0)], 'P5': [(0, 0), (1, 0), (0, 1), (1, 1), (0, 2)]}
    C = 120
    rows = []
    for code in 'wygrocbp':
        row = []
        for name, cells in shapes.items():
            cs = set(cells)
            W = (max(i for i, j in cells) + 1) * C
            H = (max(j for i, j in cells) + 1) * C
            im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
            for (i, j) in cells:
                m = (1 if (i - 1, j) in cs else 0) | (2 if (i + 1, j) in cs else 0) | (4 if (i, j - 1) in cs else 0) | (8 if (i, j + 1) in cs else 0)
                t, f = frame(A, P, 'blk_%s_%02d' % (code, m))
                x, y = i * C, H - (j + 1) * C
                im.alpha_composite(t, (x, y))
                for k, (dx, dy) in {'bl': (-1, -1), 'br': (1, -1), 'tl': (-1, 1), 'tr': (1, 1)}.items():
                    if (i + dx, j) in cs and (i, j + dy) in cs and (i + dx, j + dy) in cs:
                        cf, _ = frame(A, P, 'blk_%s_%02d_cf_%s' % (code, m, k))
                        im.alpha_composite(cf, (x if dx < 0 else x + C - 32, y + C - 32 if dy < 0 else y))
                # holdable gloss on the stud (K-09) for the first three shapes, placed mortar (K-14) for the rest
                if name in ('I2', 'L3', 'O4'):
                    g, _ = frame(A, P, 'blk_gloss')
                    k_ = f['studD'] / 64
                    g = g.resize((round(72 * k_), round(72 * k_)), Image.LANCZOS)
                    ax, ay = x + f['anchorX'] * C, y + (1 - f['anchorY']) * C
                    im.alpha_composite(g, (round(ax - g.size[0] / 2), round(ay - g.size[1] / 2)))
                elif name in ('L4', 'P5'):
                    lk, _ = frame(A, P, 'blk_locked')
                    im.alpha_composite(lk, (x, y))
            row.append(im)
        rows.append(row)
    W = max(sum(i.size[0] + 24 for i in r) for r in rows) + 24
    H = sum(max(i.size[1] for i in r) + 24 for r in rows) + 60
    s = Image.new('RGBA', (W, H), BG)
    d = ImageDraw.Draw(s)
    label(d, (24, 16), 'v5 blocks (packed atlas): tiles + corner fills; gloss on I2 / L3 / O4 (K-09), mortar on L4 / P5 (K-14)')
    y = 50
    for r in rows:
        x = 24
        for i in r:
            s.alpha_composite(i, (x, y))
            x += i.size[0] + 24
        y += max(i.size[1] for i in r) + 24
    s.save(os.path.join(OUT, 'v5-blocks.png'))


def fx_sheet(F, P):
    clips = F['clips']
    cell = 150
    n = max(len(set(c['frames'])) for c in clips.values())
    W = 260 + n * (cell + 6)
    H = len(clips) * (cell + 10) + 60
    s = Image.new('RGBA', (W, H), BG)
    d = ImageDraw.Draw(s)
    label(d, (10, 16), 'v5 fx clips (fx.json): unique drawings in timeline order, placed by the clip pivot on the clip canvas (design px)')
    y = 50
    for cn, c in clips.items():
        label(d, (10, y + cell // 2 - 30), cn)
        label(d, (10, y + cell // 2 - 8), '%d fr @ %d fps' % (len(c['frames']), c['fps']))
        label(d, (10, y + cell // 2 + 14), 'loop' if c['loop'] else 'once')
        cw, ch = c['canvas']
        k = cell / max(cw, ch)
        seen = []
        for fn in c['frames']:
            if fn not in seen:
                seen.append(fn)
        for t, fn in enumerate(seen):
            f = F['frames'][fn]
            im = P[f['atlas']].crop((f['x'], f['y'], f['x'] + f['w'], f['y'] + f['h']))
            sc = f['scale']
            # canvas px of the trimmed rect: its bottom-left sits at (ox, oy) in the stored canvas
            canvas = Image.new('RGBA', (round(cw * k), round(ch * k)), (255, 255, 255, 18))
            kk = k / sc
            im = im.resize((max(1, round(f['w'] * kk)), max(1, round(f['h'] * kk))), Image.LANCZOS)
            px = f['ox'] * kk
            py = canvas.size[1] - (f['oy'] * kk) - im.size[1]
            canvas.alpha_composite(im, (round(px), round(py)))
            # pivot mark
            dd = ImageDraw.Draw(canvas)
            pvx, pvy = c['pivot'][0] * canvas.size[0], canvas.size[1] * (1 - c['pivot'][1])
            dd.ellipse((pvx - 2, pvy - 2, pvx + 2, pvy + 2), fill=(255, 80, 80, 200))
            s.alpha_composite(canvas, (250 + t * (cell + 6) + (cell - canvas.size[0]) // 2, y + (cell - canvas.size[1]) // 2))
        y += cell + 10
    s.save(os.path.join(OUT, 'v5-fx.png'))


def kit_sheet(A, P, F, FP):
    names = ['btn_primary', 'btn_primary_pressed', 'btn_primary_m', 'btn_secondary', 'btn_danger', 'btn_offer', 'btn_disabled', 'btn_tool', 'btn_tool_pressed',
             'btn_booster', 'btn_booster_pressed', 'btn_booster_off', 'btn_slot', 'btn_close', 'btn_plus', 'badge_count', 'badge_dot', 'bead_on', 'bead_off',
             'capsule_hud', 'capsule_home', 'hud_moves_plate', 'hud_well', 'panel_window', 'panel_plate', 'panel_card', 'ribbon_title', 'progress_track',
             'progress_fill', 'progress_track_s', 'progress_fill_s', 'toggle_on', 'toggle_off', 'bubble_speech', 'bubble_tail', 'nav_tab_on',
             'board_tray', 'floor_yard_a', 'floor_yard_b', 'wall_seg', 'wall_cap', 'gap_seg', 'gap_edge', 'rail_seg', 'scaffold_pipe', 'beam_seg', 'scaffold_clamp',
             'crane_hook', 'truck', 'deco_brick_1', 'deco_brick_i2', 'deco_brick_l3', 'deco_brick_o', 'town_ch1_treehouse', 'chr_dede_idle', 'chr_dede_talk', 'chr_dede_bust']
    names += sorted(n for n in A['frames'] if n.startswith('icon_')) + ['ui_glove', 'fx_chip', 'fx_confetti', 'fx_confetti_round', 'fx_dust', 'fx_ring', 'fx_spark', 'fx_star']
    ims = [frame(A, P, n)[0] for n in names]
    ims += [frame(F, FP, n)[0] for n in sorted(F['frames']) if n.endswith('_xl')]
    W = 2400
    x, y, rowh = 20, 50, 0
    pos = []
    for im in ims:
        if x + im.size[0] > W - 20:
            x, y = 20, y + rowh + 20
            rowh = 0
        pos.append((x, y))
        x += im.size[0] + 20
        rowh = max(rowh, im.size[1])
    s = Image.new('RGBA', (W, y + rowh + 30), BG)
    d = ImageDraw.Draw(s)
    label(d, (20, 16), 'v5 kit (packed atlas, design size): UI, board, icons, characters (Usta Kral aliases), particles, icon_*_xl (fx.json)')
    for im, p in zip(ims, pos):
        s.alpha_composite(im, p)
    s.save(os.path.join(OUT, 'v5-kit.png'))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    A, P = load('art/atlas.json', 'art')
    F, FP = load('art3d/fx.json', 'art3d')
    blocks_sheet(A, P)
    fx_sheet(F, FP)
    kit_sheet(A, P, F, FP)
    print('preview_env: v5-blocks.png, v5-fx.png, v5-kit.png ->', OUT)
