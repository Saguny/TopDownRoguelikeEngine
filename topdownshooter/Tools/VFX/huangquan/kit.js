// what the Huangquan Road art shares: the kit and palette of the weapons and the bosses, a few
// ramps of its own (the souls' pale blue, paper, ash, the guardians' hides and the statues'
// stone), and the helpers every piece is drawn with
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const K2 = require("../weapons2/weapons2");
const { hex, put, bayer } = D;

const P = Object.assign({}, K2.P, {
  // ash-grey hemp, the burners' mourning robes
  AS0: hex("#2a2428"), AS1: hex("#463e42"), AS2: hex("#6a6064"), AS3: hex("#948a8a"), AS4: hex("#bfb4ae"),
  // Ox-Head's hide
  BU0: hex("#2a1410"), BU1: hex("#4e2418"), BU2: hex("#7a3c24"), BU3: hex("#a8603a"),
  // horn and bone
  BN0: hex("#6e6250"), BN1: hex("#b0a284"), BN2: hex("#e0d4b8"),
  // Horse-Face's grey-blue hide
  HF0: hex("#1c2430"), HF1: hex("#34465a"), HF2: hex("#587690"), HF3: hex("#8eaec2"),
  // the statues' stone and the moss on it
  ST0: hex("#1b1722"), ST1: hex("#312a38"), ST2: hex("#4c4354"), ST3: hex("#6e6476"), ST4: hex("#978c9c"),
  MS1: hex("#2e4a26"), MS2: hex("#4e7236"),
});

const SOUL = [[0.1, P.I0], [0.2, P.I1], [0.34, P.I2], [0.52, P.I3], [0.7, P.I4], [0.86, P.I5]];
const RED = [[0.1, P.R0], [0.3, P.R1], [0.55, P.R2], [0.8, P.R3], [0.95, P.W]];
const AZURE = W.AZURE, FIRE = W.FIRE, GOLD = W.GOLD, JADE = K2.JADE;
const DUST = [[0.06, P.S1], [0.12, P.S2], [0.2, P.S3], [0.3, P.AS3]];
const ASH = [[0.06, P.S1], [0.12, P.AS1], [0.2, P.AS2], [0.32, P.AS3]];

const RES = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "Resources");
const TAU = Math.PI * 2;
const img = W.img, lerp = W.lerp, easeOut = W.easeOut;
// flat sources are drawn with this much room round their design canvas (see draw.js's origins),
// so a limb or a horn reaching past it isn't cut off while it's drawn; kit.fit crops it all back
const FM = 16;
const M = (w, h) => D.mask(w + 2 * FM, h + 2 * FM, FM, FM);
const canvas = (w, h) => Object.assign(D.image(w + 2 * FM, h + 2 * FM), { ox: FM, oy: FM });
const fill = (im, Mk, c) => D.paint(im, Mk, c);
const clamp01 = v => Math.max(0, Math.min(1, v));

// a ramp's colour for a value, or null below it
function pick(ramp, v) { let c = null; for (const [th, col] of ramp) if (v >= th) c = col; return c; }

// a disc of a field: 1 at its centre falling to `edge` at its rim
function blob(F, cx, cy, rx, ry, peak = 1, edge = 0.4) {
  D.each(F, (x, y) => {
    const d = Math.hypot((x - cx) / rx, (y - cy) / ry);
    return d < 1 ? peak * (1 - d * (1 - edge)) : 0;
  });
}

// a thick stroke along points, as a mask
function stroke(Mk, pts, width) { return K2.limb(Mk, pts, width); }

// ---- margins: a sprite is drawn at its design size, then given a margin (an origin offset, see
// draw.js) before its glow and light go on, so nothing it throws off is cut at the canvas edge

// the same picture on a canvas `m` bigger each side, drawn to at the same coordinates
function pad(src, m) {
  const out = { w: src.w + 2 * m, h: src.h + 2 * m, data: new Uint8Array((src.w + 2 * m) * (src.h + 2 * m) * 4), ox: (src.ox | 0) + m, oy: (src.oy | 0) + m };
  for (let y = 0; y < src.h; y++) out.data.set(src.data.subarray(y * src.w * 4, (y + 1) * src.w * 4), ((y + m) * out.w + m) * 4);
  return out;
}
// an empty canvas the size of another, with its origin
const blank = like => ({ w: like.w, h: like.h, data: new Uint8Array(like.w * like.h * 4), ox: like.ox | 0, oy: like.oy | 0 });
// every pixel of an image, raw: fn(rawX, rawY, colour); design coordinates are raw minus the origin
function eachPx(im, fn) {
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++) {
    const i = (y * im.w + x) * 4;
    if (im.data[i + 3]) fn(x, y, [im.data[i], im.data[i + 1], im.data[i + 2], im.data[i + 3]]);
  }
}
function rawPut(im, x, y, c) {
  x = Math.round(x); y = Math.round(y);
  if (x < 0 || y < 0 || x >= im.w || y >= im.h) return;
  const i = (y * im.w + x) * 4;
  im.data[i] = c[0]; im.data[i + 1] = c[1]; im.data[i + 2] = c[2]; im.data[i + 3] = c[3] === undefined ? 255 : c[3];
}
// turned by `ang` about the canvas's middle (a sprite leaning into a dash), same canvas
function turn(src, ang) {
  const out = blank(src), c = Math.cos(-ang), s = Math.sin(-ang), cx = src.w / 2, cy = src.h / 2;
  for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) {
    const dx = x + 0.5 - cx, dy = y + 0.5 - cy;
    const sx = Math.floor(cx + dx * c - dy * s), sy = Math.floor(cy + dx * s + dy * c);
    if (sx < 0 || sy < 0 || sx >= src.w || sy >= src.h) continue;
    const i = (sy * src.w + sx) * 4;
    if (src.data[i + 3]) rawPut(out, x, y, [src.data[i], src.data[i + 1], src.data[i + 2], 255]);
  }
  return out;
}

// everything drawn in an image, gone to nothing by `k` (0 all there, 1 all gone), dithered, and
// lifted `rise` pixels; `tint` recolours what's left (a flash white, a fade to ash)
function dissolve(src, k, rise = 0, tint = null, phase = 0) {
  const out = blank(src);
  eachPx(src, (x, y, c) => {
    if (bayer(x + phase, y) < k) return;
    rawPut(out, x, y - Math.round(rise * (0.6 + 0.4 * W.hash2(x, 0, phase))), tint || c);
  });
  return out;
}

// a flat silhouette of an image in one colour (a hit flash, a shadow)
function silhouette(src, col) {
  const out = blank(src);
  eachPx(src, (x, y) => rawPut(out, x, y, col));
  return out;
}

// motes drifting up and fading: n of them, seeded, over an animation of `N` frames
function motes(im, f, N, n, seed, x0, x1, yTop, yBot, cols) {
  const r = D.rng(seed);
  for (let k = 0; k < n; k++) {
    const x = x0 + r() * (x1 - x0), ph = r(), sp = 0.6 + r() * 0.8;
    const t = ((f / N) * sp + ph) % 1;
    const y = yBot - t * (yBot - yTop);
    if (t > 0.85 && bayer(Math.round(x), Math.round(y)) < (t - 0.85) / 0.15) continue;
    put(im, x + Math.sin(t * 6 + k) * 0.8, y, t < 0.5 ? cols[0] : cols[1] || cols[0]);
  }
}

// a glow round everything already drawn, the way the ghost fire has one: its distance falloff
// shaded through a two step ramp, solid close in and dithered out to nothing, under the sprite
function glowAround(im, radius, near, far, phase = 0) {
  const F = D.field(im.w, im.h), on = (x, y) => x >= 0 && y >= 0 && x < im.w && y < im.h && im.data[(y * im.w + x) * 4 + 3];
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++) {
    if (on(x, y)) continue;
    let d = 99;
    for (let dy = -radius; dy <= radius; dy++) for (let dx = -radius; dx <= radius; dx++)
      if (on(x + dx, y + dy)) d = Math.min(d, Math.hypot(dx, dy));
    if (d <= radius) F.f[y * im.w + x] = 1 - (d - 0.5) / (radius + 0.5);
  }
  const g = D.shade(D.image(im.w, im.h), F, [[0.35, far], [0.72, near]], { fadeBand: 0.55 });
  if (phase) for (let i = 0; i < g.data.length; i += 4) if (g.data[i + 3] && D.bayer((i / 4) % im.w + phase, ((i / 4) / im.w) | 0) < 0.15) g.data[i + 3] = 0;
  D.over(g, im);
  g.ox = im.ox | 0; g.oy = im.oy | 0;
  return g;
}

// a flat colour from a hex string, for drawing the flat sources restyle lights
const C = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16), 255];
const INK = C("#000000");         // the sources' outline, which restyle turns deep plum

// the flat source's black outline round everything drawn
function inked(im, diag = false) { return W.withOutline(im, INK, diag); }

// a strip's frames cropped to what's drawn in them, the same about the middle on every side (so
// its pivot, the middle, doesn't move) and square (the game slices strips into square frames),
// with a pixel's gap left all round: nothing touches an edge, nothing is cut off
function fit(frames, gap = 1) {
  const w = frames[0].w, h = frames[0].h, cx = w / 2, cy = h / 2;
  let half = 1;
  for (const im of frames) for (let y = 0; y < h; y++) for (let x = 0; x < w; x++)
    if (im.data[(y * w + x) * 4 + 3]) half = Math.max(half, Math.abs(x + 0.5 - cx) + 0.5, Math.abs(y + 0.5 - cy) + 0.5);
  half = Math.ceil(half) + gap;
  const size = 2 * half, x0 = Math.round(cx - half), y0 = Math.round(cy - half);
  return frames.map(im => {
    const out = img(size, size);
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
      const sx = x + x0, sy = y + y0;
      if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
      const i = (sy * w + sx) * 4;
      if (im.data[i + 3]) out.data.set(im.data.subarray(i, i + 4), (y * size + x) * 4);
    }
    return out;
  });
}

// would any frame be cut off at its edge? the names of those that would
function clipped(frames) {
  const bad = [];
  frames.forEach((im, f) => {
    const hit = (x, y) => im.data[(y * im.w + x) * 4 + 3];
    let edge = false;
    for (let x = 0; x < im.w && !edge; x++) edge = hit(x, 0) || hit(x, im.h - 1);
    for (let y = 0; y < im.h && !edge; y++) edge = hit(0, y) || hit(im.w - 1, y);
    if (edge) bad.push(f);
  });
  return bad;
}

function strip(frames) {
  const w = frames[0].w, h = frames[0].h, out = img(w * frames.length, h);
  frames.forEach((fr, i) => D.blit(out, fr, i * w, 0));
  return out;
}

// the colours restyle knows, as hex strings
const H = c => "#" + c.slice(0, 3).map(v => v.toString(16).padStart(2, "0")).join("");

module.exports = { FM, canvas, fit, clipped, pad, blank, eachPx, rawPut, turn, glowAround, C, INK, inked, P, SOUL, RED, AZURE, FIRE, GOLD, JADE, DUST, ASH, RES, TAU, img, lerp, easeOut, M, fill, clamp01, pick, blob,
  stroke, dissolve, silhouette, motes, strip, H, D, W, K2, put, bayer, png };
