"""Turns a boss's cut-in close-up (any 2.4:1 anime image, e.g. 1536x640) into the game's pixel art:
the 384x160 band YamaScreen slams across the screen at the start of each phase.

    python pixelate.py source.png out.png [--colors 40] [--scale 4] [--preview preview.png]

1. a little more contrast and colour, so it holds up small
2. down to the band's size a 4x4 block at a time: a block that's mostly ink line becomes ink (the
   linework survives instead of greying out); any other block takes the median of its colours, so
   flat anime colour stays flat
3. a tight palette (median cut, no dithering): the pixel art look
4. the cleanup pass, which does most of the work of making it look intentional:
   - near-duplicate colours merged (closer than --merge in Lab space: the rarer takes the commoner)
   - orphans: a pixel with no neighbour of its own colour (all 8 round it) takes the colour most of
     them share; bright glints (the eyes' shine) are kept
   - jaggies: on a line a pixel wide, an "L" corner pixel (two neighbours of its colour at right
     angles, the diagonal between them open) is taken out, leaving a clean diagonal step
   - orphans again, as the first two can leave some
   every line pixel is the same ink
"""
import argparse

import numpy as np
from PIL import Image, ImageEnhance

INK = np.array([20, 10, 24], dtype=np.uint8)


def luminance(a):
    return (0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]) / 255.0


def reduce(img, k):
    a = np.asarray(img, dtype=np.uint8)
    h, w = a.shape[0] // k, a.shape[1] // k
    a = a[: h * k, : w * k]
    blocks = a.reshape(h, k, w, k, 3).transpose(0, 2, 1, 3, 4).reshape(h, w, k * k, 3)
    lum = luminance(blocks.astype(np.float32))
    line = lum < 0.17
    out = np.median(blocks, axis=2).astype(np.uint8)
    # mostly line: ink; a thinner line through the block: its darkest pixel, so it still reads
    ink = line.sum(axis=2) >= (k * k) // 3
    darkest = np.take_along_axis(blocks, lum.argmin(axis=2)[..., None, None].repeat(3, axis=3), axis=2)[:, :, 0]
    thin = (line.sum(axis=2) >= 2) & ~ink
    out[thin] = darkest[thin]
    out[ink] = INK
    return out, ink


# ---------------------------------------------------------------- the cleanup pass

def to_lab(rgb):
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    xyz = c @ np.array([[0.4124, 0.2126, 0.0193], [0.3576, 0.7152, 0.1192], [0.1805, 0.0722, 0.9505]])
    xyz /= np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def merge_colours(a, ink, threshold):
    """near-duplicates merged: every colour within `threshold` (Lab distance) of a commoner one
    becomes it, commonest first, so the palette's steps are all ones that read"""
    flat = a.reshape(-1, 3)
    cols, inv, counts = np.unique(flat, axis=0, return_inverse=True, return_counts=True)
    inv = inv.reshape(-1)
    lab = to_lab(cols)
    order = np.argsort(-counts)
    target = np.arange(len(cols))
    kept = []
    for i in order:
        if (cols[i] == INK).all():
            kept.append(i)
            continue
        near = [k for k in kept if not (cols[k] == INK).all() and np.linalg.norm(lab[i] - lab[k]) < threshold]
        if near:
            target[i] = min(near, key=lambda k: np.linalg.norm(lab[i] - lab[k]))
        else:
            kept.append(i)
    out = cols[target[inv]].reshape(a.shape)
    out[ink] = INK
    return out, len(cols), len(kept)


def same(a, y, x, c):
    h, w, _ = a.shape
    return 0 <= y < h and 0 <= x < w and (a[y, x] == c).all()


def orphans(a, ink):
    h, w, _ = a.shape
    out = a.copy()
    lum = luminance(a.astype(np.float32))
    n = 0
    for y in range(h):
        for x in range(w):
            c = a[y, x]
            if any(same(a, y + dy, x + dx, c) for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx):
                continue
            ns = [tuple(a[y + dy, x + dx]) for dy in (-1, 0, 1) for dx in (-1, 0, 1)
                  if (dy or dx) and 0 <= y + dy < h and 0 <= x + dx < w]
            # a glint: much brighter than everything round it, kept
            if lum[y, x] > 0.85 and lum[y, x] > max(luminance(np.array(n_, dtype=np.float32)) for n_ in ns) + 0.2:
                continue
            best = max(set(ns), key=ns.count)
            out[y, x] = best
            ink[y, x] = (np.array(best) == INK).all()
            n += 1
    return out, n


def jaggies(a, ink):
    """an L corner on a one pixel line: the pixel's only same-colour neighbours are one beside it
    and one above or below it, and the diagonal between those two is another colour. taking it out
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
            # the inside diagonal open (a thin line, not the corner of a solid shape), and the far
            # side too
            if same(a, y + dy, x + dx, c) or same(a, y - dy, x - dx, c):
                continue
            ns = [tuple(a[y - dy, x]), tuple(a[y, x - dx]), tuple(a[y - dy, x - dx]), tuple(a[y + dy, x + dx])]
            best = max(set(ns), key=ns.count)
            out[y, x] = best
            ink[y, x] = (np.array(best) == INK).all()
            n += 1
    return out, n


def cleanup(a, ink, merge=7.0, log=print):
    a, before, after = merge_colours(a, ink, merge)
    a, o1 = orphans(a, ink)
    a, j = jaggies(a, ink)
    a, o2 = orphans(a, ink)
    log(f"  colours {before} -> {after}, orphans {o1}+{o2}, jaggies {j}")
    return a


def convert(src, dst, colors=40, scale=4, preview=None, merge=7.0, raw=None):
    img = Image.open(src).convert("RGB")
    img = ImageEnhance.Contrast(img).enhance(1.12)
    img = ImageEnhance.Color(img).enhance(1.15)
    a, ink = reduce(img, scale)
    small = Image.fromarray(a).quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    a = np.asarray(small).copy()
    a[ink] = INK
    if raw:
        Image.fromarray(a).save(raw)
    a = cleanup(a, ink.copy(), merge)
    Image.fromarray(a).save(dst)
    if preview:
        Image.fromarray(a).resize((a.shape[1] * 3, a.shape[0] * 3), Image.NEAREST).save(preview)
    return a


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("src")
    p.add_argument("dst")
    p.add_argument("--colors", type=int, default=40)
    p.add_argument("--scale", type=int, default=4)
    p.add_argument("--preview")
    p.add_argument("--merge", type=float, default=7.0, help="Lab distance under which two colours are one")
    p.add_argument("--raw", help="also save it before the cleanup pass, to compare")
    a = p.parse_args()
    convert(a.src, a.dst, a.colors, a.scale, a.preview, a.merge, a.raw)
