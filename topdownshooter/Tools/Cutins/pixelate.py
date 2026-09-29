"""Turns a boss's cut-in close-up (any 2.4:1 anime image, e.g. 1536x640) into the game's pixel art:
the 384x160 band YamaScreen slams across the screen at the start of each phase.

    python pixelate.py                      every cut-in in CUTINS below, to their outputs
    python pixelate.py src.png out.png [--scale 4 --colors 40 --darks 8 --lift 0 --merge 7]

at full size:
1. more contrast and colour; for a dark face (--lift), its midtones lifted and its local contrast
   pushed, so brow, nose and cheek stand apart from the shadows
2. the lineart pulled out (the ink-dark pixels) and thickened a pixel, so thin strands and wrinkles
   survive the downscale as lines instead of dotted fragments
down to the band, a 4x4 block at a time:
3. the colour layer: each block's median, the line pixels left out of it (no muddying)
4. the line layer: a block the thickened lineart covers enough becomes ink, composited on top;
   one pixel gaps along a line are bridged
5. a palette with its own budget for the darks (--darks), so the steps between the darkest colour
   and black stay distinct; no dithering
then the cleanup pass, which does most of the work of making it look intentional:
6. the background: flood-filled from the edges, the specks inside it swallowed, one flat colour
7. near-duplicate colours merged (Lab distance under --merge, half that in the darks)
8. despeckle: islands of one or two pixels (any colour, ink too) merged into the colour round
   them; bright glints (the eyes' shine) are kept
9. jaggies: on a line a pixel wide, an "L" corner pixel is taken out, leaving a clean diagonal
10. despeckle again
"""
import argparse
import os

import numpy as np
from PIL import Image, ImageEnhance, ImageFilter
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "..", "..", "Assets", "### Different Engine", "Resources")
INK = np.array([20, 10, 24], dtype=np.uint8)

# each cut-in: its source, where it goes, and how it's treated
CUTINS = {
    # bg: points in the band that are background, to flood it from
    # ink/grow: how dark a pixel has to be to count as lineart, and how much it's thickened. his
    # lines are bold already and his shadows near black, so only the darkest, left as they are
    "yama": dict(src="source/yama.png", dst="Yama/cutin.png", lift=0.72, darks=10, bg=[(377, 10), (20, 150)], ink=0.1, grow=0),
    "mengpo": dict(src="source/mengpo.png", dst="MengPo/cutin.png", bg=[(5, 60), (380, 60)]),
    "mengpo_true": dict(src="source/mengpo_true.png", dst="MengPo/cutin_true.png", bg=[(5, 5), (380, 5), (380, 80), (5, 100)], bgTolerance=22),
}


def luminance(a):
    a = np.asarray(a, dtype=np.float32)
    return (0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]) / 255.0


def to_lab(rgb):
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    xyz = c @ np.array([[0.4124, 0.2126, 0.0193], [0.3576, 0.7152, 0.1192], [0.1805, 0.0722, 0.9505]])
    xyz /= np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


# ---------------------------------------------------------------- at full size

def prepare(img, lift):
    img = ImageEnhance.Contrast(img).enhance(1.12)
    img = ImageEnhance.Color(img).enhance(1.15)
    if lift:
        a = np.asarray(img, dtype=np.float32) / 255.0
        lum = luminance(a * 255.0)
        # midtones up (a gamma on the value, the black point and the ink left where they are)
        target = np.where(lum > 0.14, lum ** lift, lum)
        a *= (target / np.maximum(lum, 1e-4))[..., None]
        img = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
        # local contrast: planes pulled apart from the shadows round them
        img = img.filter(ImageFilter.UnsharpMask(radius=12, percent=60, threshold=2))
    return img


def lineart(img, dark=0.18, grow=1):
    lum = luminance(np.asarray(img))
    line = lum < dark
    return ndimage.binary_dilation(line, iterations=grow) if grow else line


# ---------------------------------------------------------------- down to the band

def reduce(img, line, k):
    a = np.asarray(img, dtype=np.uint8)
    h, w = a.shape[0] // k, a.shape[1] // k
    a, line = a[: h * k, : w * k], line[: h * k, : w * k]
    blocks = a.reshape(h, k, w, k, 3).transpose(0, 2, 1, 3, 4).reshape(h, w, k * k, 3).astype(np.float32)
    lb = line.reshape(h, k, w, k).transpose(0, 2, 1, 3).reshape(h, w, k * k)
    # the colour: the block's median, its line pixels left out (all line: the plain median)
    masked = np.where(lb[..., None], np.nan, blocks)
    with np.errstate(all="ignore"), __import__("warnings").catch_warnings():
        __import__("warnings").simplefilter("ignore")
        colour = np.nanmedian(masked, axis=2)
    plain = np.median(blocks, axis=2)
    colour = np.where(np.isnan(colour), plain, colour).astype(np.uint8)
    ink = lb.mean(axis=2) >= 0.3
    return colour, ink


def bridge(ink):
    """one pixel gaps along a line: a pixel between two ink pixels on opposite sides of it (across,
    up and down, or on a diagonal) that has no other way through becomes ink"""
    p = np.pad(ink, 1)
    n = ink.copy()
    for (ay, ax), (by, bx) in [((0, 1), (2, 1)), ((1, 0), (1, 2)), ((0, 0), (2, 2)), ((0, 2), (2, 0))]:
        a = p[ay: ay + ink.shape[0], ax: ax + ink.shape[1]]
        b = p[by: by + ink.shape[0], bx: bx + ink.shape[1]]
        n |= a & b
    # only fill where it closes a break: the new pixel's ink neighbours are few (a line, not a blob)
    count = ndimage.convolve(ink.astype(np.int32), np.ones((3, 3), np.int32), mode="constant") - ink
    return np.where(~ink & n & (count <= 3), True, ink)


def palette(colour, ink, colors, darks):
    """the colours: the darks get their own share of the palette so their steps stay apart"""
    lum = luminance(colour)
    dark = (lum < 0.28) & ~ink
    out = colour.copy()
    for sel, n in [(dark, darks), (~dark & ~ink, colors - darks)]:
        if sel.sum() == 0:
            continue
        px = colour[sel].reshape(-1, 1, 3)
        q = Image.fromarray(px).quantize(colors=max(2, n), method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
        out[sel] = np.asarray(q).reshape(-1, 3)
    out[ink] = INK
    return out


# ---------------------------------------------------------------- the cleanup pass

def flatten_background(a, ink, seeds, tolerance=16.0, speck=16):
    """the background, flooded from `seeds` (points known to be background) through anything near
    their colour; specks enclosed in it (droplets, dots) are swallowed; all of it one flat colour,
    the commonest the flood reached"""
    if not seeds:
        return a, 0
    ref = to_lab(np.array([a[y, x] for x, y in seeds])).mean(axis=0)
    near = (np.linalg.norm(to_lab(a) - ref, axis=-1) < tolerance) & ~ink
    lab, _ = ndimage.label(near)
    ids = {lab[y, x] for x, y in seeds} - {0}
    region = np.isin(lab, list(ids))
    if not region.any():
        return a, 0
    keys, counts = np.unique(a[region], axis=0, return_counts=True)
    bg = keys[counts.argmax()]
    # specks inside it: small pieces of anything not connected to the figure
    holes, _ = ndimage.label(~region)
    sizes = ndimage.sum(np.ones_like(holes), holes, index=np.arange(holes.max() + 1))
    small = sizes[holes] <= speck
    region |= small & (holes > 0)
    out = a.copy()
    out[region] = bg
    return out, int(region.sum())


def merge_colours(a, ink, threshold):
    """near-duplicates merged, commonest first: in the darks only the very closest, so the dark
    steps survive"""
    flat = a.reshape(-1, 3)
    cols, inv, counts = np.unique(flat, axis=0, return_inverse=True, return_counts=True)
    inv = inv.reshape(-1)
    lab = to_lab(cols)
    target = np.arange(len(cols))
    kept = []
    for i in np.argsort(-counts):
        if (cols[i] == INK).all():
            kept.append(i)
            continue
        limit = threshold * (0.5 if lab[i][0] < 32 else 1.0)
        near = [k for k in kept if not (cols[k] == INK).all() and np.linalg.norm(lab[i] - lab[k]) < limit]
        if near:
            target[i] = min(near, key=lambda k: np.linalg.norm(lab[i] - lab[k]))
        else:
            kept.append(i)
    out = cols[target[inv]].reshape(a.shape)
    return out, len(cols), len(kept)


def despeckle(a, largest=2):
    """islands of `largest` pixels or fewer, of any colour, merged into the colour that surrounds
    them most; bright glints (much brighter than all round them) are kept"""
    h, w, _ = a.shape
    out = a.copy()
    keys = a[..., 0].astype(np.int64) << 16 | a[..., 1].astype(np.int64) << 8 | a[..., 2].astype(np.int64)
    lum = luminance(a)
    n = 0
    eight = np.ones((3, 3), bool)
    for key in np.unique(keys):
        lab, count = ndimage.label(keys == key, structure=eight)
        if count == 0:
            continue
        sizes = ndimage.sum(np.ones_like(lab), lab, index=np.arange(count + 1))
        for i in np.nonzero((sizes <= largest) & (np.arange(count + 1) > 0))[0]:
            ys, xs = np.nonzero(lab == i)
            ring = ndimage.binary_dilation(lab == i, structure=eight) & (lab != i)
            ry, rx = np.nonzero(ring)
            if len(ry) == 0:
                continue
            if lum[ys, xs].min() > 0.85 and lum[ys, xs].min() > lum[ry, rx].max() + 0.2:
                continue
            around = keys[ry, rx]
            vals, cnt = np.unique(around, return_counts=True)
            best = vals[cnt.argmax()]
            out[ys, xs] = [(best >> 16) & 255, (best >> 8) & 255, best & 255]
            n += len(ys)
    return out, n


def same(a, y, x, c):
    h, w, _ = a.shape
    return 0 <= y < h and 0 <= x < w and (a[y, x] == c).all()


def jaggies(a):
    """an L corner on a one pixel line: the pixel's only same-colour neighbours are one beside it and
    one above or below it, and the diagonal between those two is another colour. taking it out
    leaves the two touching corner to corner: a clean diagonal"""
    h, w, _ = a.shape
    out = a.copy()
    n = 0
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            c = a[y, x]
            hs = [dx for dx in (-1, 1) if same(a, y, x + dx, c)]
            vs = [dy for dy in (-1, 1) if same(a, y + dy, x, c)]
            if len(hs) != 1 or len(vs) != 1:
                continue
            dx, dy = hs[0], vs[0]
            if same(a, y + dy, x + dx, c) or same(a, y - dy, x - dx, c):
                continue
            ns = [tuple(a[y - dy, x]), tuple(a[y, x - dx]), tuple(a[y - dy, x - dx]), tuple(a[y + dy, x + dx])]
            out[y, x] = max(set(ns), key=ns.count)
            n += 1
    return out, n


# ---------------------------------------------------------------- the whole of it

def convert(src, dst, scale=4, colors=40, darks=8, lift=0.0, merge=7.0, preview=None, raw=None, log=print, bg=None, bgTolerance=16.0, ink=0.18, grow=1):
    img = prepare(Image.open(src).convert("RGB"), lift)
    line = lineart(img, ink, grow)
    colour, ink = reduce(img, line, scale)
    ink = bridge(ink)
    a = palette(colour, ink, colors, darks)
    if raw:
        Image.fromarray(a).save(raw)
    a, flat = flatten_background(a, ink, bg, bgTolerance)
    a, before, after = merge_colours(a, ink, merge)
    a, s1 = despeckle(a)
    a, j = jaggies(a)
    a, s2 = despeckle(a)
    log(f"  background {flat} px flat, colours {before} -> {after}, specks {s1}+{s2}, jaggies {j}")
    os.makedirs(os.path.dirname(os.path.abspath(dst)), exist_ok=True)
    Image.fromarray(a).save(dst)
    if preview:
        Image.fromarray(a).resize((a.shape[1] * 3, a.shape[0] * 3), Image.NEAREST).save(preview)
    return a


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("src", nargs="?")
    p.add_argument("dst", nargs="?")
    p.add_argument("--scale", type=int, default=4)
    p.add_argument("--colors", type=int, default=40)
    p.add_argument("--darks", type=int, default=8, help="of the colours, how many for the darks")
    p.add_argument("--lift", type=float, default=0.0, help="a gamma lifting a dark face's midtones, e.g. 0.75")
    p.add_argument("--merge", type=float, default=7.0, help="Lab distance under which two colours are one")
    p.add_argument("--preview")
    p.add_argument("--raw", help="also save it before the cleanup pass, to compare")
    p.add_argument("--out", help="with no src: write every cut-in here instead of into Resources")
    a = p.parse_args()
    if a.src:
        convert(a.src, a.dst, a.scale, a.colors, a.darks, a.lift, a.merge, a.preview, a.raw)
    else:
        for name, c in CUTINS.items():
            print(name)
            dst = os.path.join(a.out, name + ".png") if a.out else os.path.join(RES, c["dst"])
            convert(os.path.join(HERE, c["src"]), dst, a.scale, a.colors, c.get("darks", a.darks), c.get("lift", 0.0), a.merge,
                    preview=os.path.join(a.out, name + "_3x.png") if a.out else None, bg=c.get("bg"), bgTolerance=c.get("bgTolerance", 16.0),
                    ink=c.get("ink", 0.18), grow=c.get("grow", 1))
