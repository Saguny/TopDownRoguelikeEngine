// the warnings: the medallion that points the way to a boss off the screen, and Heaven's thunder,
// the strike that falls on a player standing still in the endless mode (Bombardment). same kit and
// palette as the weapons and the bosses. written straight to Resources as PNG strips of square
// frames, which the game slices at run time (YamaArt.Strip), so there's nothing to set up in Unity:
//   node alerts.js
// Resources/UI (screen space, drawn at 3 screen pixels a pixel at 1080p):
//   boss_ring     the medallion, 36x36, 6 frames: a bronze-gold rim with eight studs and a red
//                 lacquer lining round an open window (the boss's own sprite shows through it),
//                 a glint running round the rim and a red glow breathing outside it
//   boss_window   the window behind the boss's sprite, 36x36: deep crimson going dark at its edge.
//                 also the mask the sprite is clipped to
//   boss_arrow    the arrowhead that orbits the medallion and points at the boss, 16x16, 4
//                 frames, pointing right: gold, a red heart, a spark flickering at its tip
// Resources/Hazards (world space, the enemies' pixel size):
//   strike_seal   the thunder seal marking where it will fall, 136x136 (radius 2.4 units), 8
//                 frames: a red double ring with the eight trigrams between, a gold bolt glyph in
//                 the middle, a bright arc sweeping round it
//   strike_fill   the countdown, 136x136: a dithered red disc with a hot rim, which the game grows
//                 from nothing to the full seal
//   strike_bolt   the bolt coming down, 48x192 frames laid in 192x192 cells (its foot at the bottom
//                 middle), 6 frames: a thin leader, the strike white-hot, branches, dying
//   strike_burst  where it lands, 136x136, 8 frames: a white flash, gold rays, an electric ring
//                 breaking up as it spreads, sparks thrown
// out/sheet_*.png are previews
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const K2 = require("../weapons2/weapons2");
const { put, bayer } = D;
const { TAU, easeOut, lerp, img, hash2, ringField, breakup, twinkle, GOLD } = W;
const P = K2.P;
const RES = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "Resources");
const RED = [[0.1, P.R0], [0.3, P.R1], [0.55, P.R2], [0.8, P.R3], [0.95, P.W]];
const THUNDER = [[0.08, P.V1], [0.18, P.V2], [0.3, P.V3], [0.45, P.V5], [0.62, P.G2], [0.78, P.G3], [0.9, P.W]];

// ================================================================ the boss medallion

const MS = 36, MC = 18;                  // its size and centre
const WINDOW = 10.5, RIM_IN = 11.2, RIM_OUT = 15.5;

function ring(f, N) {
  const im = img(MS, MS);
  const glint = f / N * TAU - Math.PI / 2;
  const glow = [0.15, 0.3, 0.45, 0.3, 0.15, 0.05][f];
  for (let y = 0; y < MS; y++) for (let x = 0; x < MS; x++) {
    const dx = x + 0.5 - MC, dy = y + 0.5 - MC, d = Math.hypot(dx, dy), a = Math.atan2(dy, dx);
    // the red glow breathing just outside it
    if (d > RIM_OUT + 0.5 && d <= RIM_OUT + 1.7) { if (bayer(x, y) < glow * 1.4) put(im, x, y, P.R2); continue; }
    if (d > RIM_OUT + 0.5 || d <= WINDOW) continue;
    // outline, gold rim lit from the top left, red lacquer lining, dark lip at the window
    if (d > RIM_OUT - 0.5) { put(im, x, y, P.V0); continue; }
    if (d <= WINDOW + 0.9) { put(im, x, y, P.K); continue; }
    if (d <= RIM_IN + 0.9) { put(im, x, y, (x + y) % 3 === 0 ? P.R1 : P.R2); continue; }
    const light = -(dx + dy) / (Math.SQRT2 * d);          // 1 at the top left, -1 at the bottom right
    let c = light > 0.45 ? P.G2 : light > -0.35 ? P.G1 : P.G0;
    if (d > RIM_OUT - 1.4 && light > 0.2) c = P.G3;        // the lit outer edge
    // the glint running round
    let da = Math.abs(((a - glint) % TAU + TAU + Math.PI) % TAU - Math.PI);
    if (da < 0.22) c = P.W; else if (da < 0.45 && bayer(x, y) < 0.6) c = P.G3;
    put(im, x, y, c);
  }
  // eight studs round the rim, the bagua's points
  for (let k = 0; k < 8; k++) {
    const a = k / 8 * TAU - Math.PI / 2, r = (RIM_IN + RIM_OUT) / 2 + 0.2;
    const x = Math.round(MC - 0.5 + Math.cos(a) * r), y = Math.round(MC - 0.5 + Math.sin(a) * r);
    put(im, x, y, P.R3); put(im, x + 1, y, P.R2); put(im, x, y + 1, P.R2); put(im, x + 1, y + 1, P.R1);
    put(im, x - 1, y, P.G0); put(im, x + 2, y + 1, P.G0); put(im, x, y - 1, P.G0); put(im, x + 1, y + 2, P.G0);
  }
  return im;
}

function windowArt() {
  const im = img(MS, MS);
  for (let y = 0; y < MS; y++) for (let x = 0; x < MS; x++) {
    const d = Math.hypot(x + 0.5 - MC, y + 0.5 - MC);
    if (d > WINDOW + 0.6) continue;
    const k = d / WINDOW;
    put(im, x, y, k > 0.85 ? P.V0 : k > 0.6 && bayer(x, y) < (k - 0.6) / 0.25 ? P.V0 : P.R0);
  }
  return im;
}

function arrow(f) {
  const S = 16, im = img(S, S);
  const head = K2.poly(D.mask(S, S), [[3, 2.5], [14.5, 8], [3, 13.5], [6, 8]]);
  D.paint(im, D.minus(D.dilate(head, 1, false), head), P.V0);
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    if (!D.mget(head, x, y)) continue;
    const upper = y < 8;
    put(im, x, y, upper ? P.G2 : P.G1);
  }
  // the red heart down its middle, and a lit upper edge
  for (let x = 6; x <= 11; x++) put(im, x, 8, x < 9 ? P.R1 : P.R2);
  for (let x = 5; x <= 10; x++) put(im, x, 7, P.R2);
  for (let x = 4; x <= 12; x++) { const y = Math.round(2.5 + (x - 3) * (5.5 / 11.5)); put(im, x, y + 1, P.G3); }
  // the glint and a spark at the tip
  const g = [5, 8, 11, -1][f];
  if (g > 0) { put(im, g, Math.round(2.5 + (g - 3) * 0.48) + 1, P.W); put(im, g + 1, Math.round(2.5 + (g - 2) * 0.48) + 1, P.W); }
  if (f === 1 || f === 2) twinkle(im, 15, 8, f === 1 ? 1 : 0, [P.W, P.G3]);
  return im;
}

// ================================================================ Heaven's thunder

const TS = 136, TC = 68, TR = 66;

// the eight trigrams, top to bottom a bar each: 1 whole, 0 broken
const TRIGRAMS = [[1, 1, 1], [0, 0, 0], [0, 1, 0], [1, 0, 1], [0, 1, 1], [1, 1, 0], [1, 0, 0], [0, 0, 1]];

function trigram(im, cx, cy, ang, bars, col, dark) {
  // three bars stacked outward from the centre, each 9 long, laid across the radius
  const tx = -Math.sin(ang), ty = Math.cos(ang), rx = Math.cos(ang), ry = Math.sin(ang);
  bars.forEach((whole, i) => {
    const o = (i - 1) * 2.4;
    for (let u = -4.5; u <= 4.5; u += 0.5) {
      if (!whole && Math.abs(u) < 1.2) continue;
      const x = cx + rx * o + tx * u, y = cy + ry * o + ty * u;
      put(im, x + ry * 0.6, y - rx * 0.6 + 1, dark);
      put(im, x, y, col);
    }
  });
}

function seal(f, N) {
  const im = img(TS, TS);
  const F = D.field(TS, TS);
  ringField(F, TC, TC, TR, 3, 0.62, null);                  // the outer ring
  ringField(F, TC, TC, TR - 12, 1.6, 0.5, null);            // the inner ring
  ringField(F, TC, TC, 18, 1.4, 0.5, null);                 // round the glyph
  // a bright arc sweeping round both rings
  const sweep = f / N * TAU;
  D.each(F, (x, y) => {
    const dx = x - TC, dy = y - TC, d = Math.hypot(dx, dy);
    if (d > TR + 0.5 || d < TR - 13) return 0;
    let da = ((Math.atan2(dy, dx) - sweep) % TAU + TAU) % TAU;
    da = TAU - da;                                           // trailing behind the sweep
    if (da > 1.1) return 0;
    const onRing = Math.abs(d - (TR - 1.5)) < 2 || Math.abs(d - (TR - 12.5)) < 1.2;
    return onRing ? 0.95 - da * 0.35 : bayer(Math.round(x), Math.round(y)) < 0.3 * (1 - da / 1.1) ? 0.4 : 0;
  });
  D.shade(im, F, RED);
  // the trigrams between the rings, gold on the red
  for (let k = 0; k < 8; k++) {
    const a = k / 8 * TAU - Math.PI / 2;
    const r = TR - 6.2;
    const lit = Math.abs((((a - sweep) % TAU) + TAU) % TAU - TAU) < 0.7 || (((a - sweep) % TAU) + TAU) % TAU < 0.15;
    trigram(im, TC + Math.cos(a) * r, TC + Math.sin(a) * r, a, TRIGRAMS[k], lit ? P.G3 : P.G1, P.R0);
  }
  // ticks round the outer ring
  for (let k = 0; k < 48; k++) {
    const a = k / 48 * TAU, x = TC + Math.cos(a) * (TR + 1.8), y = TC + Math.sin(a) * (TR + 1.8);
    if (k % 6 === 0) put(im, x, y, P.G2); else if (k % 2 === 0) put(im, x, y, P.R2);
  }
  // the bolt glyph in the middle: a gold zigzag with a dark edge
  const bolt = [[TC + 4, TC - 12], [TC - 4, TC + 1], [TC + 2, TC + 1], [TC - 5, TC + 13]];
  const M = D.mask(TS, TS);
  D.polyline(M, bolt);
  const thick = D.dilate(M, 1, false);
  D.paint(im, D.minus(D.dilate(thick, 1, true), thick), P.R0);
  D.paint(im, thick, f % 4 < 2 ? P.G2 : P.G3);
  D.paint(im, M, P.W, 0.5, f);
  return im;
}

function fill() {
  const im = img(TS, TS);
  for (let y = 0; y < TS; y++) for (let x = 0; x < TS; x++) {
    const d = Math.hypot(x + 0.5 - TC, y + 0.5 - TC);
    if (d > TR - 1) continue;
    if (d > TR - 4) put(im, x, y, d > TR - 2.5 ? P.R3 : P.R2);            // the hot rim
    else if (bayer(x, y) < 0.34 + 0.2 * (d / TR)) put(im, x, y, (x ^ y) & 4 ? P.R1 : P.R2);
  }
  return im;
}

// the bolt: from the top of a 192 tall cell down to its foot at the bottom middle
function strikeBolt(f) {
  const Wd = 192, H = 192, im = img(Wd, H), cx = Wd / 2;
  if (f === 5) {
    for (let k = 0; k < 10; k++) twinkle(im, cx + (hash2(k, 1, 3) - 0.5) * 30, H - 4 - hash2(k, 2, 3) * 40, 0, [k % 2 ? P.G3 : P.V5]);
    return im;
  }
  const r = D.rng(700 + [0, 1, 1, 2, 2][f] * 13);
  const pts = D.bolt(r, cx + (r() - 0.5) * 10, 0, cx, H - 3, 18, 5);
  const F = D.field(Wd, H);
  const width = [0.6, 3.2, 2.6, 1.6, 0.8][f], glow = [0, 3, 2.5, 1.5, 0][f], peak = [0.5, 1, 0.95, 0.7, 0.45][f];
  const draw = (pts, w, g, v) => {
    for (let i = 1; i < pts.length; i++) {
      const [ax, ay] = pts[i - 1], [bx, by] = pts[i], len = Math.hypot(bx - ax, by - ay);
      for (let s = 0; s <= len; s += 0.5) {
        const x = ax + (bx - ax) * s / len, y = ay + (by - ay) * s / len;
        for (let oy = -Math.ceil(w + g); oy <= Math.ceil(w + g); oy++) for (let ox = -Math.ceil(w + g); ox <= Math.ceil(w + g); ox++) {
          const dd = Math.hypot(ox, oy);
          if (dd <= w) D.fmax(F, Math.round(x + ox), Math.round(y + oy), v);
          else if (dd <= w + g && bayer(Math.round(x + ox), Math.round(y + oy)) < 0.5) D.fmax(F, Math.round(x + ox), Math.round(y + oy), v * 0.3);
        }
      }
    }
  };
  draw(pts, width, glow, peak);
  // branches off the main bolt
  if (f >= 1 && f <= 3) {
    for (let b = 0; b < 4; b++) {
      const i = 4 + Math.floor(hash2(b, f, 5) * (pts.length - 10)), [sx, sy] = pts[i];
      const side = hash2(b, 3, 5) < 0.5 ? -1 : 1;
      const br = D.bolt(r, sx, sy, sx + side * (14 + hash2(b, 4, 5) * 22), sy + 16 + hash2(b, 5, 5) * 26, 6, 3);
      draw(br, Math.max(0.5, width * 0.35), 0, peak * 0.6);
    }
  }
  D.shade(im, F, THUNDER);
  // the white-hot core
  if (f >= 1 && f <= 2) for (let i = 1; i < pts.length; i++) {
    const M = D.line(D.mask(Wd, H), pts[i - 1][0], pts[i - 1][1], pts[i][0], pts[i][1]);
    D.paint(im, M, P.W);
  }
  return im;
}

function burst(f, N) {
  const im = img(TS, TS);
  if (f === 0) {
    // the flash: a white-hot disc with a gold edge
    const F = D.field(TS, TS);
    D.each(F, (x, y) => { const d = Math.hypot(x - TC, y - TC); return d < 22 ? 1 : d < 30 ? 0.8 - (d - 22) / 30 : 0; });
    D.flare(F, TC, TC, 62, 6, 34, 3, 1, 0.4);
    return D.shade(im, F, GOLD);
  }
  const t = (f - 1) / (N - 2), R = lerp(20, 64, easeOut(t));
  const F = D.field(TS, TS);
  if (f <= 2) { D.flare(F, TC, TC, [56, 44][f - 1], [4, 3][f - 1], [30, 22][f - 1], 2, 0.9, 0.35, 0.3); }
  ringField(F, TC, TC, R, lerp(5, 1.2, t), lerp(0.95, 0.35, t), f > 2 ? breakup(R, f + 3, lerp(0.85, 0.35, t)) : null);
  if (f <= 3) D.each(F, (x, y) => { const d = Math.hypot(x - TC, y - TC); return d < lerp(16, 4, t) ? 0.7 : 0; });
  D.shade(im, F, THUNDER);
  // little arcs crawling out and sparks thrown
  const r = D.rng(90 + f);
  for (let k = 0; k < 7; k++) {
    const a = k / 7 * TAU + hash2(k, 1, 7), d0 = R * 0.5, d1 = R * (0.9 + r() * 0.25);
    if (t > 0.75 && k % 2) continue;
    const pts = D.bolt(r, TC + Math.cos(a) * d0, TC + Math.sin(a) * d0, TC + Math.cos(a) * d1, TC + Math.sin(a) * d1, 4, 3);
    D.paint(im, D.polyline(D.mask(TS, TS), pts), t < 0.5 ? P.V6 : P.V4);
  }
  for (let k = 0; k < 16; k++) {
    const a = k / 16 * TAU + hash2(k, 2, 7) * 0.5, d = (R + 6) * (0.8 + hash2(k, 3, 7) * 0.4);
    const x = TC + Math.cos(a) * d, y = TC + Math.sin(a) * d;
    if (bayer(Math.round(x), Math.round(y)) < 1.1 - t) twinkle(im, x, y, t < 0.35 ? 1 : 0, [P.W, k % 3 ? P.G3 : P.V5]);
  }
  return im;
}

// ---------------------------------------------------------------- write

function strip(frames) {
  const w = frames[0].w, h = frames[0].h, out = img(w * frames.length, h);
  frames.forEach((fr, i) => D.blit(out, fr, i * w, 0));
  return out;
}

function build() {
  const preview = path.join(__dirname, "out");
  fs.mkdirSync(preview, { recursive: true });
  const items = {
    "UI/boss_ring": Array.from({ length: 6 }, (_, f) => ring(f, 6)),
    "UI/boss_window": [windowArt()],
    "UI/boss_arrow": Array.from({ length: 4 }, (_, f) => arrow(f)),
    "Hazards/strike_seal": Array.from({ length: 8 }, (_, f) => seal(f, 8)),
    "Hazards/strike_fill": [fill()],
    "Hazards/strike_bolt": Array.from({ length: 6 }, (_, f) => strikeBolt(f)),
    "Hazards/strike_burst": Array.from({ length: 8 }, (_, f) => burst(f, 8)),
  };
  for (const [name, frames] of Object.entries(items)) {
    const dest = path.join(RES, name + ".png");
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    png.encode(strip(frames), dest);
    const base = path.basename(name);
    png.encode(png.preview(frames, frames[0].w <= 16 ? 10 : frames[0].w <= 48 ? 6 : 2, [22, 18, 30], Math.min(frames.length, 8), 2), path.join(preview, `sheet_${base}.png`));
    console.log(`${name} ${frames[0].w}x${frames[0].h} x${frames.length}`);
  }
}
if (require.main === module) build();
module.exports = { ring, windowArt, arrow, seal, fill, strikeBolt, burst };
