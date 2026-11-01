#!/usr/bin/env python3
"""Usta Kral design-sheet generator (D-101; docs/ART_3D.md, art/3d/spec/king.md).

Python standard library only. Writes hand-built SVG reference drawings (no filter / mask / image) into the folder of
this script: king_front.svg, king_expressions.svg, king_poses.svg, king_palette.svg, king_scale.svg. They are design
references for the Blender model (king_blockout.py), not game frames.
"""
import math
import os

OUT = os.path.dirname(os.path.abspath(__file__))

# ---- palette (art/3d/spec/king.md section 4; hex = sRGB) -------------------------------------------------------
P = {
    'hat': ('#FFF09A', '#FFD43B', '#E8A80C', '#B07A00'),        # light, base, shadow, deep (safety-yellow plastic)
    'gold': ('#FFF2B8', '#FFD45A', '#E0A21A', '#8A5A00'),       # crown emblem, buckle, pins, trowel blade (metal)
    'enamel': ('#7FB8F0', '#2984DE', '#1C5A97', '#11375D'),     # emblem plate (B family)
    'suit': ('#F7B866', '#EC8D20', '#C46A12', '#8E4A0A'),       # coverall (O family)
    'cuff': ('#FBD49A', '#F6AC55', '#D98A34', '#A86420'),       # rolled sleeve and hem
    'shirt': ('#FFFBEF', '#FFF3D6', '#E6CFA2', '#BFA676'),      # undershirt at the V-neck
    'sash': ('#9CC6F2', '#2984DE', '#1C5A97', '#11375D'),       # royal sash (B family, satin)
    'leather': ('#C08A52', '#8A4E1C', '#64360F', '#3E200A'),    # tool belt, pouch
    'boot': ('#9A6A40', '#5A3418', '#3E220E', '#24130A'),       # work boots
    'sole': ('#FFF8E6', '#F2E3C6', '#D4BE94', '#A8905E'),
    'skin': ('#F8CFA6', '#E9A877', '#C27F52', '#93583A'),
    'hair': ('#A8653A', '#7A4222', '#5A2E14', '#3A1A08'),       # hair, beard (chestnut)
    'wood': ('#D9A066', '#B8763E', '#8A5426', '#5A3412'),       # trowel handle
}
INK = '#2B1A10'          # sheet line work (reference only; the 3D model has no outline)
BLUSH = '#E8735A'
IRIS = ('#E0A45A', '#C88A3E', '#7A4512')
PUPIL = '#1E0F06'
MOUTH = '#6A1F2A'
TONGUE = '#FF7A7A'
TEETH = '#FFFDF6'
LACE = '#FFD43B'

_uid = [0]


def uid(prefix):
    _uid[0] += 1
    return f'{prefix}{_uid[0]}'


def defs_block():
    """Radial 4-stop volume gradients (ART_REFERENCE section 6.2 rule 1) for every material."""
    out = ['<defs>']
    for name, (light, base, shadow, deep) in P.items():
        out.append(
            f'<radialGradient id="g_{name}" cx="0.38" cy="0.32" r="0.78">'
            f'<stop offset="0" stop-color="{light}"/><stop offset="0.45" stop-color="{base}"/>'
            f'<stop offset="0.85" stop-color="{shadow}"/><stop offset="1" stop-color="{deep}"/></radialGradient>')
        out.append(
            f'<linearGradient id="l_{name}" x1="0" y1="0" x2="1" y2="0">'
            f'<stop offset="0" stop-color="{light}"/><stop offset="0.45" stop-color="{base}"/>'
            f'<stop offset="1" stop-color="{shadow}"/></linearGradient>')
    out.append(f'<radialGradient id="g_iris" cx="0.4" cy="0.4" r="0.7"><stop offset="0" stop-color="{IRIS[0]}"/>'
               f'<stop offset="0.5" stop-color="{IRIS[1]}"/><stop offset="1" stop-color="{IRIS[2]}"/></radialGradient>')
    out.append('<linearGradient id="g_eyewhite" x1="0" y1="0" x2="0" y2="1"><stop offset="0.6" stop-color="#FFFFFF"/>'
               '<stop offset="1" stop-color="#DCE6F2"/></linearGradient>')
    out.append('<radialGradient id="g_spec" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#FFFFFF" stop-opacity="0.75"/>'
               '<stop offset="1" stop-color="#FFFFFF" stop-opacity="0"/></radialGradient>')
    out.append('<radialGradient id="g_shadow" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#000000" stop-opacity="0.35"/>'
               '<stop offset="1" stop-color="#000000" stop-opacity="0"/></radialGradient>')
    out.append('</defs>')
    return '\n'.join(out)


def f(m):
    return f'url(#g_{m})'


def stroke(w=3):
    return f'stroke="{INK}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"'


# ---- parts ----------------------------------------------------------------------------------------------------

def trowel(angle):
    """Golden trowel (Altin Mala) held at the handle; local origin = grip, blade along -y (up) before rotation."""
    return (f'<g transform="rotate({angle})">'
            f'<rect x="-6" y="-8" width="12" height="34" rx="6" fill="{f("wood")}" {stroke(2.5)}/>'
            f'<rect x="-8" y="-16" width="16" height="10" rx="3" fill="{f("gold")}" {stroke(2.5)}/>'
            f'<path d="M0 -16 L0 -26" {stroke(4)}/>'
            f'<path d="M0 -24 L20 -40 L0 -86 L-20 -40 Z" fill="{f("gold")}" {stroke(3)}/>'
            f'<path d="M0 -30 L0 -78" stroke="#FFF7D0" stroke-width="3" stroke-linecap="round" opacity="0.8"/>'
            '</g>')


def hand(kind, item_angle=None):
    """Chunky mitten hand drawn around the wrist at (0, 0), pointing +y."""
    s = stroke(3)
    if kind == 'open':
        body = (f'<path d="M-18 2 Q-24 30 -12 44 Q4 52 18 42 Q26 26 20 2 Z" fill="{f("skin")}" {s}/>'
                f'<path d="M-18 10 Q-34 18 -30 30 Q-22 34 -14 26" fill="{f("skin")}" {s}/>'
                f'<path d="M-6 42 L-6 30 M4 44 L4 30 M13 40 L13 28" stroke="{P["skin"][3]}" stroke-width="2" stroke-linecap="round" opacity="0.6"/>')
    elif kind == 'fist':
        body = (f'<path d="M-18 2 Q-24 22 -14 34 Q2 42 16 34 Q24 20 18 2 Z" fill="{f("skin")}" {s}/>'
                f'<path d="M-14 20 Q-2 26 14 20" fill="none" stroke="{P["skin"][3]}" stroke-width="2" opacity="0.6"/>')
    elif kind == 'point':
        body = (f'<path d="M-18 2 Q-24 22 -14 32 Q2 40 16 32 Q24 20 18 2 Z" fill="{f("skin")}" {s}/>'
                f'<path d="M6 28 Q8 56 2 62 Q-6 62 -6 54 L-4 30" fill="{f("skin")}" {s}/>')
    elif kind == 'thumb':
        body = (f'<path d="M-18 2 Q-24 22 -14 34 Q2 42 16 34 Q24 20 18 2 Z" fill="{f("skin")}" {s}/>'
                f'<path d="M-16 12 Q-34 6 -34 -10 Q-28 -16 -22 -8 L-14 4" fill="{f("skin")}" {s}/>')
    else:  # hold
        body = (f'<path d="M-18 2 Q-24 22 -14 34 Q2 42 16 34 Q24 20 18 2 Z" fill="{f("skin")}" {s}/>')
    item = ''
    if item_angle is not None:
        item = f'<g transform="translate(0 20)">{trowel(item_angle)}</g>'
        # fingers wrap over the handle: redraw the knuckle band on top
        body = body + f'<path d="M-16 16 Q0 26 16 16 Q14 32 0 34 Q-14 32 -16 16 Z" fill="{f("skin")}" {s}/>'
    return item + body if item_angle is not None and kind == 'hold' else body + item


def arm(side, out_deg, bend_deg, hand_kind='open', item_angle=None):
    """side 'L' = viewer left (the king's right arm). out_deg: outward from straight down; bend_deg: elbow, inward +."""
    sx = 164 if side == 'L' else 348
    mirror = '' if side == 'L' else ' scale(-1 1)'
    s = stroke(3)
    upper = (f'<rect x="-21" y="-10" width="42" height="74" rx="21" fill="{f("suit")}" {s}/>'
             f'<rect x="-23" y="52" width="46" height="18" rx="9" fill="{f("cuff")}" {s}/>')
    fore = (f'<rect x="-16" y="-2" width="32" height="58" rx="16" fill="{f("skin")}" {s}/>')
    hand_svg = hand(hand_kind, item_angle)
    return (f'<g transform="translate({sx} 238){mirror} rotate({out_deg})">{upper}'
            f'<g transform="translate(0 62) rotate({-bend_deg})">{fore}<g transform="translate(0 50)">{hand_svg}</g></g></g>')


def eyes(kind, look=(0, 0)):
    out = []
    for ex in (222, 290):
        if kind in ('happy', 'closed'):
            out.append(f'<path d="M{ex-17} 142 Q{ex} {124 if kind=="happy" else 142} {ex+17} 142" fill="none" {stroke(5)}/>')
            continue
        ry = 24 if kind == 'wide' else 22
        out.append(f'<ellipse cx="{ex}" cy="138" rx="18" ry="{ry}" fill="url(#g_eyewhite)" {stroke(3)}/>')
        ir = 11 if kind == 'wide' else 14.5
        ix, iy = ex + look[0], 140 + look[1]
        out.append(f'<circle cx="{ix}" cy="{iy}" r="{ir}" fill="url(#g_iris)"/>')
        out.append(f'<circle cx="{ix}" cy="{iy}" r="{ir*0.5:.1f}" fill="{PUPIL}"/>')
        out.append(f'<circle cx="{ix-4}" cy="{iy-5}" r="4.5" fill="#FFFFFF"/>')
        out.append(f'<circle cx="{ix+4}" cy="{iy+4}" r="2" fill="#FFFFFF"/>')
        if kind == 'soft':  # lower lid raised (warm smile): skin crescent over the lower third + lid line
            out.append(f'<path d="M{ex-19} 162 L{ex-19} 147 Q{ex} 137 {ex+19} 147 L{ex+19} 162 Z" fill="{P["skin"][1]}"/>')
            out.append(f'<path d="M{ex-17} 147 Q{ex} 138 {ex+17} 147" fill="none" stroke="{INK}" stroke-width="3" stroke-linecap="round"/>')
    return ''.join(out)


def brows(l, r):
    """Each brow: (dy, angle). Thick rounded 'brick' bars; angle + lifts the outer end."""
    out = []
    for (ex, (dy, ang), sgn) in ((222, l, -1), (290, r, 1)):
        out.append(f'<g transform="translate({ex} {111+dy}) rotate({sgn*-ang})">'
                   f'<rect x="-24" y="-7" width="48" height="15" rx="7.5" fill="{f("hair")}" {stroke(2.5)}/></g>')
    return ''.join(out)


def mouth(kind):
    s = stroke(3)
    if kind == 'smile':
        return f'<path d="M232 182 Q256 200 280 182" fill="none" {stroke(4.5)}/>'
    if kind == 'open':
        return (f'<path d="M226 178 Q256 182 286 178 Q282 210 256 212 Q230 210 226 178 Z" fill="{MOUTH}" {s}/>'
                f'<path d="M230 180 Q256 184 282 180 L280 188 Q256 191 232 188 Z" fill="{TEETH}"/>'
                f'<path d="M240 204 Q256 196 272 204 Q264 211 256 211 Q248 211 240 204 Z" fill="{TONGUE}"/>')
    if kind == 'big':
        return (f'<path d="M220 174 Q256 180 292 174 Q290 222 256 226 Q222 222 220 174 Z" fill="{MOUTH}" {s}/>'
                f'<path d="M225 177 Q256 182 287 177 L285 187 Q256 191 227 187 Z" fill="{TEETH}"/>'
                f'<path d="M236 214 Q256 202 276 214 Q266 224 256 224 Q246 224 236 214 Z" fill="{TONGUE}"/>')
    if kind == 'side':
        return f'<path d="M238 190 Q248 184 262 188 Q270 190 274 186" fill="none" {stroke(4.5)}/>'
    if kind == 'talk':
        return (f'<ellipse cx="256" cy="192" rx="16" ry="12" fill="{MOUTH}" {s}/>'
                f'<path d="M246 198 Q256 192 266 198 Q262 203 256 203 Q250 203 246 198 Z" fill="{TONGUE}"/>')
    if kind == 'o':
        return f'<ellipse cx="256" cy="194" rx="10" ry="12" fill="{MOUTH}" {s}/>'
    if kind == 'grin':
        return (f'<path d="M230 182 Q256 204 284 178 Q282 192 256 200 Q234 196 230 182 Z" fill="{TEETH}" {s}/>')
    if kind == 'warm':
        return (f'<path d="M234 182 Q256 198 278 182 Q270 196 256 198 Q242 196 234 182 Z" fill="{MOUTH}" {s}/>')
    return ''


EXPR = {
    # name: eyes kind, look, brows (left, right), mouth, blush
    'neutral': ('open', (0, 0), ((0, 4), (0, 4)), 'smile', 0.25),
    'cheer': ('happy', (0, 0), ((-10, 10), (-10, 10)), 'open', 0.4),
    'big_cheer': ('happy', (0, 0), ((-16, 16), (-16, 16)), 'big', 0.5),
    'think': ('open', (6, -7), ((4, -8), (-12, 14)), 'side', 0.2),
    'talk': ('open', (0, 0), ((-4, 6), (-4, 6)), 'talk', 0.25),
    'point': ('open', (7, 0), ((2, -8), (2, -8)), 'grin', 0.3),
    'encourage': ('soft', (0, 2), ((-6, -12), (-6, -12)), 'warm', 0.35),
    'wave': ('open', (0, -2), ((-8, 8), (-8, 8)), 'open', 0.35),
    'surprised': ('wide', (0, 0), ((-16, 6), (-16, 6)), 'o', 0.2),
}


def head(expr, hat_tilt=0, hat_lift=0):
    e_kind, look, (bl, br), m_kind, blush = EXPR[expr]
    s = stroke(3)
    parts = []
    # ears
    for x in (168, 344):
        parts.append(f'<ellipse cx="{x}" cy="150" rx="16" ry="22" fill="{f("skin")}" {s}/>')
        parts.append(f'<ellipse cx="{x}" cy="152" rx="7" ry="11" fill="{P["skin"][2]}" opacity="0.7"/>')
    # head squircle (rounded square: the block shape language)
    parts.append(f'<rect x="170" y="62" width="172" height="150" rx="66" fill="{f("skin")}" {s}/>')
    # hair tufts under the hat (chestnut)
    parts.append(f'<path d="M172 104 Q166 130 178 140 Q186 128 192 106 Z" fill="{f("hair")}" {s}/>')
    parts.append(f'<path d="M340 104 Q346 130 334 140 Q326 128 320 106 Z" fill="{f("hair")}" {s}/>')
    # short boxy chin beard (no moustache): sideburn -> jaw -> squared chin
    parts.append(f'<path d="M174 128 L176 176 Q182 222 222 230 L290 230 Q330 222 336 176 L338 128 '
                 f'L326 132 Q324 178 300 192 Q280 202 256 202 Q232 202 212 192 Q188 178 186 132 Z" '
                 f'fill="{f("hair")}" {s}/>')
    parts.append(f'<path d="M206 214 Q230 222 256 222 M262 222 Q286 222 306 212" fill="none" stroke="{P["hair"][0]}" stroke-width="3" stroke-linecap="round" opacity="0.8"/>')
    # cheeks
    for x in (204, 308):
        parts.append(f'<ellipse cx="{x}" cy="170" rx="17" ry="10" fill="{BLUSH}" opacity="{blush}"/>')
    parts.append(eyes(e_kind, look))
    # button nose: small rounded square in skin tone
    parts.append(f'<rect x="244" y="152" width="24" height="20" rx="9" fill="{f("skin")}" {s}/>')
    parts.append(f'<circle cx="250" cy="157" r="3" fill="#FFFFFF" opacity="0.6"/>')
    parts.append(mouth(m_kind))
    # hard hat (safety yellow) + crown emblem plate (B enamel + gold 3-point crown with stud tips)
    hat = (f'<path d="M158 96 Q160 22 256 18 Q352 22 354 96 Z" fill="{f("hat")}" {s}/>'
           f'<path d="M240 20 Q256 16 272 20 L274 94 L238 94 Z" fill="{P["hat"][0]}" opacity="0.55"/>'
           f'<path d="M144 98 Q256 84 368 98 Q370 110 356 112 Q256 104 156 112 Q142 110 144 98 Z" fill="{f("hat")}" {s}/>'
           f'<path d="M206 104 Q256 98 306 104 Q300 120 256 120 Q212 120 206 104 Z" fill="{f("hat")}" {s}/>'
           f'<ellipse cx="210" cy="44" rx="26" ry="12" fill="url(#g_spec)" transform="rotate(-24 210 44)"/>'
           f'<rect x="226" y="44" width="60" height="44" rx="12" fill="{f("enamel")}" stroke="{P["gold"][2]}" stroke-width="4"/>'
           f'<path d="M236 78 L238 60 L247 70 L256 54 L265 70 L274 60 L276 78 Z" fill="{f("gold")}" {stroke(2.5)}/>'
           f'<circle cx="238" cy="58" r="4.5" fill="{f("gold")}" {stroke(2)}/><circle cx="256" cy="52" r="5" fill="{f("gold")}" {stroke(2)}/>'
           f'<circle cx="274" cy="58" r="4.5" fill="{f("gold")}" {stroke(2)}/>')
    parts.append(f'<g transform="translate(0 {-hat_lift}) rotate({hat_tilt} 256 100)">{hat}</g>')
    # brows are drawn over the brim so they always act (they never hide under the hat)
    parts.append(brows(bl, br))
    return ''.join(parts)


def body(squash=1.0):
    s = stroke(3)
    out = []
    # legs + rolled hem
    for x in (186, 270):
        out.append(f'<rect x="{x}" y="388" width="56" height="78" rx="22" fill="{f("suit")}" {s}/>')
        out.append(f'<rect x="{x-3}" y="446" width="62" height="20" rx="10" fill="{f("cuff")}" {s}/>')
    # boots
    for cx in (214, 298):
        out.append(f'<path d="M{cx-38} 498 L{cx-38} 476 Q{cx-36} 458 {cx-12} 458 L{cx+14} 458 Q{cx+42} 460 {cx+44} 482 L{cx+44} 498 Z" fill="{f("boot")}" {s}/>')
        out.append(f'<rect x="{cx-40}" y="488" width="86" height="12" rx="6" fill="{f("sole")}" {s}/>')
        out.append(f'<path d="M{cx-10} 464 L{cx+8} 474 M{cx+8} 464 L{cx-10} 474" stroke="{LACE}" stroke-width="3.5" stroke-linecap="round"/>')
    # torso: rounded 'brick' with a slight belly
    out.append(f'<path d="M150 278 Q150 216 214 214 L298 214 Q362 216 362 278 L366 356 Q366 408 318 408 L194 408 Q146 408 146 356 Z" fill="{f("suit")}" {s}/>')
    # V-neck: undershirt + collar flaps
    out.append(f'<path d="M222 216 L290 216 L256 262 Z" fill="{f("shirt")}" {s}/>')
    out.append(f'<path d="M214 214 L256 266 L232 270 L206 222 Z" fill="{f("cuff")}" {s}/>')
    out.append(f'<path d="M298 214 L256 266 L280 270 L306 222 Z" fill="{f("cuff")}" {s}/>')
    # zip + chest pocket with a pencil
    out.append(f'<path d="M256 266 L256 356" stroke="{P["suit"][3]}" stroke-width="3" stroke-dasharray="4 4"/>')
    out.append(f'<rect x="300" y="236" width="10" height="34" rx="3" fill="#FFD43B" {stroke(2)} transform="rotate(12 305 252)"/>')
    out.append(f'<rect x="290" y="258" width="44" height="40" rx="8" fill="{f("suit")}" {s}/>')
    out.append(f'<path d="M290 266 L334 266" stroke="{P["suit"][3]}" stroke-width="3"/>')
    # tool belt + buckle + pouch
    out.append(f'<rect x="146" y="352" width="220" height="28" rx="12" fill="{f("leather")}" {s}/>')
    out.append(f'<rect x="156" y="366" width="44" height="46" rx="10" fill="{f("leather")}" {s}/>')
    out.append(f'<path d="M156 378 L200 378" stroke="{P["leather"][3]}" stroke-width="3"/>')
    out.append(f'<rect x="172" y="350" width="8" height="22" rx="3" fill="#E8473B" {stroke(2)}/>')
    # royal sash over the right shoulder (viewer left) to the left hip, gold edges, three symbol pins
    out.append(f'<path d="M156 246 L190 216 L366 352 L344 388 Z" fill="{f("sash")}" {s}/>')
    out.append(f'<path d="M166 244 L193 222 M184 262 L356 372" fill="none" stroke="{P["gold"][1]}" stroke-width="3" opacity="0.9"/>')
    out.append(f'<path d="M178 230 L352 362" stroke="#FFFFFF" stroke-width="6" stroke-linecap="round" opacity="0.25"/>')
    for (x, y, sym) in ((214, 262, 'leaf'), (262, 298, 'spark'), (310, 334, 'diamond')):
        out.append(f'<circle cx="{x}" cy="{y}" r="12" fill="{f("gold")}" {stroke(2.5)}/>')
        if sym == 'leaf':
            out.append(f'<path d="M{x-6} {y+6} C{x-6} {y-2} {x-1} {y-7} {x+7} {y-7} C{x+7} {y+1} {x+2} {y+6} {x-6} {y+6} Z" fill="{P["gold"][3]}"/>')
        elif sym == 'spark':
            out.append(f'<path d="M{x} {y-8} Q{x+1} {y-1} {x+8} {y} Q{x+1} {y+1} {x} {y+8} Q{x-1} {y+1} {x-8} {y} Q{x-1} {y-1} {x} {y-8} Z" fill="{P["gold"][3]}"/>')
        else:
            out.append(f'<path d="M{x} {y-8} L{x+7} {y-1} L{x} {y+8} L{x-7} {y-1} Z" fill="{P["gold"][3]}"/>')
    out.append(f'<rect x="240" y="352" width="32" height="28" rx="7" fill="{f("gold")}" {stroke(2.5)}/>')
    out.append(f'<rect x="250" y="360" width="12" height="12" rx="3" fill="{P["leather"][2]}"/>')
    return ''.join(out)


POSES = {
    # name: (arm L (out, bend, hand, item), arm R (...), expr, squash(sy), y offset, body tilt, hat tilt, hat lift)
    'idle': ((14, 12, 'open', None), (12, 40, 'hold', 30), 'neutral', 1.0, 0, 0, 0, 0),
    'cheer': ((150, -10, 'fist', None), (150, -10, 'fist', None), 'cheer', 1.06, -26, 0, -4, 6),
    'big_cheer': ((160, -20, 'open', None), (165, -6, 'hold', 10), 'big_cheer', 1.08, -46, -4, -12, 26),
    'think': ((14, 12, 'open', None), (24, 150, 'point', None), 'think', 1.0, 0, 3, 4, 0),
    'talk': ((58, 96, 'open', None), (12, 40, 'hold', 30), 'talk', 1.0, 0, -2, -2, 0),
    'point': ((14, 12, 'open', None), (80, -4, 'hold', 180), 'point', 1.0, 0, 4, 3, 0),
    'encourage': ((52, 118, 'open', None), (34, 132, 'thumb', None), 'encourage', 0.98, 2, 0, 0, 0),
    'wave': ((14, 12, 'open', None), (130, 34, 'open', None), 'wave', 1.02, -4, -3, -3, 0),
}


def king(pose='idle', expr=None):
    aL, aR, e, sy, dy, tilt, hat_tilt, hat_lift = POSES[pose]
    e = expr or e
    shadow = f'<ellipse cx="256" cy="499" rx="{118 if dy > -10 else 92}" ry="12" fill="url(#g_shadow)"/>'
    inner = (arm('L', *aL) + arm('R', *aR) if pose in ('cheer', 'big_cheer', 'wave') else '')
    front_arms = '' if pose in ('cheer', 'big_cheer', 'wave') else arm('L', *aL) + arm('R', *aR)
    if pose == 'wave':
        inner = arm('R', *aR)
        front_arms = arm('L', *aL)
    g = (f'<g transform="translate(0 {dy}) translate(256 500) scale({2-sy:.3f} {sy:.3f}) translate(-256 -500) rotate({tilt} 256 470)">'
         f'{inner}{body()}{front_arms}<g transform="rotate({tilt*0.6} 256 220)">{head(e, hat_tilt, hat_lift)}</g></g>')
    return shadow + g


def svg(w, h, content, bg='#FBF6EA'):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">'
            f'{defs_block()}<rect width="{w}" height="{h}" fill="{bg}"/>{content}</svg>\n')


def label(x, y, text, size=22, anchor='middle', color='#3B2A1A', weight=700):
    return (f'<text x="{x}" y="{y}" font-family="&apos;Baloo 2&apos;, sans-serif" font-size="{size}" '
            f'font-weight="{weight}" fill="{color}" text-anchor="{anchor}">{text}</text>')


def write(name, text):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8') as fh:
        fh.write(text)


def sheet_front():
    """Front design with the 2.5-head proportion grid."""
    grid = []
    top, ground = 18, 500
    head_px = (ground - top) / 2.5
    for i in range(6):
        y = top + i * head_px / 2
        grid.append(f'<line x1="40" y1="{y:.1f}" x2="472" y2="{y:.1f}" stroke="#C9B48A" stroke-width="{2 if i % 2 == 0 else 1}" stroke-dasharray="{"" if i % 2 == 0 else "6 6"}"/>')
        if i % 2 == 0:
            grid.append(label(30, y + 6, f'{i // 2}', 18, 'end'))
    grid.append(label(30, ground + 6, '2,5', 18, 'end'))
    content = (''.join(grid) + f'<line x1="40" y1="{ground}" x2="472" y2="{ground}" stroke="#C9B48A" stroke-width="2"/>'
               + f'<g transform="translate(0 0)">{king("idle")}</g>'
               + label(256, 540, 'Usta Kral — ön görünüş, 2,5 baş (baret dahil)', 22))
    write('king_front.svg', svg(512, 560, content))


def sheet_expressions():
    names = ['neutral', 'cheer', 'big_cheer', 'think', 'talk', 'point', 'encourage', 'wave', 'surprised']
    cells = []
    for i, n in enumerate(names):
        cx, cy = (i % 3) * 300, (i // 3) * 300
        cells.append(f'<g transform="translate({cx} {cy})"><rect x="8" y="8" width="284" height="284" rx="24" fill="#FFFFFF" stroke="#E6D9B0" stroke-width="3"/>'
                     f'<g transform="translate(150 140) scale(1.12) translate(-256 -140)">{head(n)}</g>'
                     f'{label(150, 276, n, 22)}</g>')
    write('king_expressions.svg', svg(900, 900, ''.join(cells)))


def sheet_poses():
    names = ['idle', 'cheer', 'big_cheer', 'think', 'talk', 'point', 'encourage', 'wave']
    cells = []
    for i, n in enumerate(names):
        cx, cy = (i % 4) * 320, (i // 4) * 380
        cells.append(f'<g transform="translate({cx} {cy})"><rect x="8" y="8" width="304" height="364" rx="24" fill="#FFFFFF" stroke="#E6D9B0" stroke-width="3"/>'
                     f'<g transform="translate(160 330) scale(0.56) translate(-256 -500)">{king(n)}</g>'
                     f'{label(160, 358, n, 22)}</g>')
    write('king_poses.svg', svg(1280, 760, ''.join(cells)))


def sheet_palette():
    rows = []
    names = {'hat': 'baret (plastik)', 'gold': 'taç arması / mala (metal)', 'enamel': 'arma plakası (emaye)', 'suit': 'iş tulumu (pamuk)',
             'cuff': 'kol / paça kıvrımı', 'shirt': 'iç tişört', 'sash': 'kraliyet kuşağı (saten)', 'leather': 'alet kemeri (deri)',
             'boot': 'iş botu', 'sole': 'bot tabanı', 'skin': 'ten', 'hair': 'saç / sakal / kaş', 'wood': 'mala sapı (ahşap)'}
    for i, (k, (light, base, shadow, deep)) in enumerate(P.items()):
        y = 20 + i * 64
        rows.append(label(20, y + 38, names[k], 22, 'start'))
        for j, (hx, tag) in enumerate(((light, 'acik'), (base, 'taban'), (shadow, 'golge'), (deep, 'derin'))):
            x = 330 + j * 170
            rows.append(f'<rect x="{x}" y="{y}" width="56" height="52" rx="12" fill="{hx}" stroke="#3B2A1A" stroke-width="2"/>')
            rows.append(label(x + 64, y + 24, hx, 18, 'start'))
            rows.append(label(x + 64, y + 46, tag, 16, 'start', '#8A7A60', 600))
    write('king_palette.svg', svg(1020, 20 + len(P) * 64 + 10, ''.join(rows)))


def sheet_scale():
    """Readability at use sizes: 512 master, 296 HUD corner, 132 tutorial portrait (bust), 64 avatar chip."""
    parts = [f'<g transform="translate(0 0) scale(0.75)">{king("idle")}</g>',
             f'<g transform="translate(400 160) scale(0.44)">{king("idle")}</g>',
             label(512, 410, 'oyun köşesi 296', 18)]
    for (x, d) in ((680, 132), (860, 64)):
        r = d / 2
        cid = uid('c')
        parts.append(f'<clipPath id="{cid}"><circle cx="{x}" cy="{220}" r="{r}"/></clipPath>')
        parts.append(f'<circle cx="{x}" cy="220" r="{r+4}" fill="#2E9BF0"/>')
        sc = d / 190
        parts.append(f'<g clip-path="url(#{cid})"><rect x="{x-r}" y="{220-r}" width="{d}" height="{d}" fill="#FFF6D2"/>'
                     f'<g transform="translate({x} {220+r*0.18}) scale({sc:.3f}) translate(-256 -140)">{head("neutral")}</g></g>')
        parts.append(label(x, 220 + r + 34, f'portre {d}', 18))
    write('king_scale.svg', svg(980, 420, ''.join(parts)))


if __name__ == '__main__':
    sheet_front()
    sheet_expressions()
    sheet_poses()
    sheet_palette()
    sheet_scale()
    print('ok')
