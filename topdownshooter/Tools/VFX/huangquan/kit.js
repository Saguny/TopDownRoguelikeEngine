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
const M = (w, h) => D.mask(w, h);
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

// everything drawn in an image, gone to nothing by `k` (0 all there, 1 all gone), dithered, and
// lifted `rise` pixels; `tint` recolours what's left (a flash white, a fade to ash)
function dissolve(src, k, rise = 0, tint = null, phase = 0) {
  const out = img(src.w, src.h);
  for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) {
    const c = D.get(src, x, y);
    if (!c) continue;
    if (bayer(x + phase, y) < k) continue;
    put(out, x, y - Math.round(rise * (0.6 + 0.4 * W.hash2(x, 0, phase))), tint || c);
  }
  return out;
}

// a flat silhouette of an image in one colour (a hit flash, a shadow)
function silhouette(src, col) {
  const out = img(src.w, src.h);
  for (let i = 0; i < src.w * src.h; i++) if (src.data[i * 4 + 3]) { out.data[i * 4] = col[0]; out.data[i * 4 + 1] = col[1]; out.data[i * 4 + 2] = col[2]; out.data[i * 4 + 3] = 255; }
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
  const F = D.field(im.w, im.h);
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++) {
    if (D.get(im, x, y)) continue;
    let d = 99;
    for (let dy = -radius; dy <= radius; dy++) for (let dx = -radius; dx <= radius; dx++)
      if (D.get(im, x + dx, y + dy)) d = Math.min(d, Math.hypot(dx, dy));
    if (d <= radius) F.f[y * im.w + x] = 1 - (d - 0.5) / (radius + 0.5);
  }
  const g = D.shade(D.image(im.w, im.h), F, [[0.35, far], [0.72, near]], { fadeBand: 0.55 });
  if (phase) for (let i = 0; i < g.data.length; i += 4) if (g.data[i + 3] && D.bayer((i / 4) % im.w + phase, ((i / 4) / im.w) | 0) < 0.15) g.data[i + 3] = 0;
  return D.over(g, im);
}

// a flat colour from a hex string, for drawing the flat sources restyle lights
const C = h => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16), 255];
const INK = C("#000000");         // the sources' outline, which restyle turns deep plum

// the flat source's black outline round everything drawn
function inked(im, diag = false) { return W.withOutline(im, INK, diag); }

function strip(frames) {
  const w = frames[0].w, h = frames[0].h, out = img(w * frames.length, h);
  frames.forEach((fr, i) => D.blit(out, fr, i * w, 0));
  return out;
}

// the colours restyle knows, as hex strings
const H = c => "#" + c.slice(0, 3).map(v => v.toString(16).padStart(2, "0")).join("");

module.exports = { glowAround, C, INK, inked, P, SOUL, RED, AZURE, FIRE, GOLD, JADE, DUST, ASH, RES, TAU, img, lerp, easeOut, M, fill, clamp01, pick, blob,
  stroke, dissolve, silhouette, motes, strip, H, D, W, K2, put, bayer, png };
