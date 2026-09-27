// the fortune envelope and the end of a run, in the weapons' style (their palette, ramps, ordered
// dithering, plum outlines on solid things, and the attack language: a white-hot flare with thin
// rays, a ring breaking into dashes as it spreads, embers, smoke dithering out)
//   node fortune.js            draws everything into out/ (plus a sheet_*.png preview of each)
//   node ../write-ase.js out "../../../Assets/### Different Engine/NewSprites/Asesprites/VFX"
// then Tools > VFX > Build Weapon FX in Unity points the game at them
//
//   Fortune (a lacquered red envelope elites and bosses carry, and opening it)
//     fe_envelope  on the ground, glinting              fe_glow      the gold pool of light under it
//     fe_pillar    its beam of light, seen from afar     fe_marker    the chevron bobbing over it
//     fe_carry     over an elite or boss carrying one   fe_pickup    the flash as it's picked up
//     fe_big       the envelope on the opening screen, closed, its seal charging up
//     fe_flap      its flap lifting and the light pouring out    fe_front  its front, over the scroll
//     fe_aura, fe_rays, fe_mote    greyscale light the game tints by rarity
//     fe_burst_common, fe_burst_rare, fe_burst_legendary    a reward landing, in its rarity's ramp
//     fe_roller, fe_scroll         the scroll the rewards are shown on
//     fe_slot, fe_slot_evo, fe_banner     a reward's frame (an evolution's burns) and the rarity's ribbon
//     fe_coin, fe_peach            coins and a heal: a reward, and the two gifts offered once nothing
//                                  is left to level
//   Wuchang (the end of a normal run: they come for the player and take their soul)
//     wc_link      the soul-catching chain, a link each way      wc_hook     the shackle at its end
//     wc_soul      the soul drawn out of the body                wc_maw      the dark it's swallowed into
//     wc_ink       ink flooding the screen before the results
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const K2 = require("../weapons2/weapons2");
const { hex, put, bayer } = D;
const { FIRE, GOLD, AZURE, SMOKE, TAU, easeOut, lerp, img, fbm, vnoise, hash2, withOutline, halo, ringField, breakup, twinkle, el } = W;
const { JADE, ICE, mask, rect, ellipse, poly, limb, shade, shadeRows, px, rotate, thin } = K2;

const P = Object.assign({}, K2.P, {
  // paper, warm and aged
  PA0: hex("#8a6a4e"), PA1: hex("#c8a878"), PA2: hex("#ecd8b0"), PA3: hex("#fff4dc"),
  // lacquer: the envelope's deep crimson, a step below the fire reds
  LQ0: hex("#2a0610"), LQ1: hex("#5c0c1e"),
  // steel for the Wuchang's chain
  ST0: hex("#2a2436"), ST1: hex("#5a566e"), ST2: hex("#9a9ab0"), ST3: hex("#dcdcec"),
  // greys for the light the game tints by rarity
  GR1: hex("#6c6c6c"), GR2: hex("#a8a8a8"), GR3: hex("#d8d8d8"),
});
const OUT = P.V0;
const GREY = [[0.12, P.GR1], [0.3, P.GR2], [0.55, P.GR3], [0.8, P.W]];
const JADE_B = [[0.08, P.J0], [0.16, P.J1], [0.28, P.J2], [0.44, P.J3], [0.62, P.J4], [0.8, P.J5], [0.93, P.W]];
const AZURE_B = [[0.08, P.A0], [0.16, P.A1], [0.28, P.A2], [0.44, P.A3], [0.62, P.A4], [0.8, P.A5], [0.93, P.W]];
const IMPERIAL = [[0.08, P.R0], [0.16, P.R1], [0.28, P.R2], [0.42, P.O1], [0.56, P.G2], [0.74, P.G3], [0.9, P.W]];
const clamp01 = v => Math.max(0, Math.min(1, v));
const ms = n => new Array(n);

// ---------------------------------------------------------------- the lacquer and its gold

// crimson lacquer lit from the upper right: a dark left edge and foot, a sheen down the right edge
// and a soft diagonal gleam across the upper right
function lacquer(im, M, { sheen = true } = {}) {
  let l = M.w, r = -1, t = M.h, b = -1;
  for (let y = 0; y < M.h; y++) for (let x = 0; x < M.w; x++) if (D.mget(M, x, y)) { l = Math.min(l, x); r = Math.max(r, x); t = Math.min(t, y); b = Math.max(b, y); }
  const w = r - l + 1, h = b - t + 1;
  for (let y = t; y <= b; y++) for (let x = l; x <= r; x++) {
    if (!D.mget(M, x, y)) continue;
    const u = (x - l) / Math.max(1, w - 1), v = (y - t) / Math.max(1, h - 1);
    let c = P.R2;
    // the foot and the left side fall into shadow, one clean step, a dithered seam between
    if (u < 0.1 || v > 0.9) c = P.R1;
    if (u < 0.04 && h > 12) c = P.LQ1;
    // the sheen: a lit strip down the right edge, brightest at the top
    if (sheen && x === r - 1 && v < 0.85) c = P.R3;
    if (sheen && x === r - 2 && v < 0.35 && h > 12) c = P.R3;
    put(im, x, y, c);
  }
  return im;
}
// a gold line with a lit top and right, around a rectangle
function goldFrame(im, x0, y0, x1, y1, lit = P.G2, dark = P.G1) {
  for (let x = x0; x <= x1; x++) { put(im, x, y0, lit); put(im, x, y1, dark); }
  for (let y = y0; y <= y1; y++) { put(im, x0, y, dark); put(im, x1, y, lit); }
}
// a round gold seal with a red diamond knot in it, glowing `glow` (0 to 1)
function seal(im, cx, cy, r, glow = 0) {
  const M = ellipse(mask(im.w, im.h), cx, cy, r, r);
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++) {
    if (!D.mget(M, x, y)) continue;
    const dx = x + 0.5 - cx, dy = y + 0.5 - cy, d = Math.hypot(dx, dy) / r;
    const side = (dx - dy) / r;                    // + toward the light
    let c;
    if (d > 0.8) c = side > 0.35 ? P.G2 : side < -0.35 ? P.G0 : P.G1;
    else if (d > 0.62 && r > 4) c = P.G0;
    else c = dx > 0 && dy < 0 && d > 0.25 ? P.G3 : P.G2;
    if (glow > 0.3) { if (d > 0.8) c = side < -0.35 ? P.G1 : P.G2; else if (d > 0.62 && r > 4) c = P.G1; }
    if (glow > 0.55) { if (d > 0.8) c = side < -0.35 ? P.G2 : P.G3; else if (d > 0.62 && r > 4) c = P.G2; else c = P.G3; }
    if (glow > 0.8) { if (d > 0.8) c = P.G3; else if (d > 0.62 && r > 4) c = P.G3; else c = P.W; }
    put(im, x, y, c);
  }
  // the knot: a red diamond with a gold cross through it, the way a fortune knot is tied
  const k = Math.max(1, Math.round(r * 0.42));
  for (let dy = -k; dy <= k; dy++) for (let dx = -k; dx <= k; dx++) {
    if (Math.abs(dx) + Math.abs(dy) > k) continue;
    const edge = Math.abs(dx) + Math.abs(dy) === k;
    put(im, Math.floor(cx) + dx, Math.floor(cy) + dy, edge ? P.R1 : glow > 0.85 ? P.R3 : P.R2);
  }
  if (k >= 2) { put(im, Math.floor(cx), Math.floor(cy), P.G2); put(im, Math.floor(cx) - 1, Math.floor(cy), P.G1); put(im, Math.floor(cx) + 1, Math.floor(cy), P.G1); put(im, Math.floor(cx), Math.floor(cy) - 1, P.G3); put(im, Math.floor(cx), Math.floor(cy) + 1, P.G1); }
  return M;
}
// an auspicious cloud, xiangyun: a curl on a stem, in gold
function cloud(im, x, y, flip = 1, c = P.G1, hi = P.G2) {
  const pts = [[0, 0], [1, -1], [2, -1], [3, 0], [3, 1], [2, 1], [4, 1], [5, 0], [6, 0], [7, 1], [7, 2], [6, 3], [0, 2], [1, 2], [2, 3], [3, 3], [4, 3], [5, 3]];
  for (const [u, v] of pts) put(im, x + u * flip, y + v, v <= 0 ? hi : c);
}

// ================================================================ ON THE GROUND

// the envelope, 22x28: crimson lacquer in a gold frame, its flap closed on a round gold seal,
// clouds at its foot and a red knot with a gold tassel tied at its side, swaying. a glint runs
// across it every loop. the canvas centre is its middle
function envelopeSmall(f, N) {
  const Wd = 22, H = 28, im = img(Wd, H);
  const x0 = 3, x1 = 17, y0 = 4, y1 = 25;
  const body = rect(mask(Wd, H), x0, y0, x1, y1);
  lacquer(im, body);
  goldFrame(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1);
  // the flap: a V from the top corners down to the seal, a shade darker, edged in gold
  const flap = poly(mask(Wd, H), [[x0 + 1, y0 + 1], [x1, y0 + 1], [10.5, 13.5]]);
  for (let y = 0; y < H; y++) for (let x = 0; x < Wd; x++) if (D.mget(flap, x, y)) put(im, x, y, x < x0 + 3 ? P.LQ1 : P.R1);
  D.polyline(flap, []);
  const edge = D.minus(D.dilate(flap, 1, false), flap);
  for (let y = y0 + 2; y < H; y++) for (let x = x0 + 1; x <= x1 - 1; x++) if (D.mget(edge, x, y) && y <= 14) put(im, x, y, x > 10 ? P.G2 : P.G1);
  seal(im, 10.5, 13.5, 3.2, 0);
  // clouds at the foot
  for (let x = x0 + 3; x <= x1 - 3; x++) put(im, x, 21, x % 2 ? P.G1 : P.G2);
  px(im, [[7, 23], [10, 23], [13, 23]], P.G1);
  put(im, 10, 23, P.G2);
  // the glint: a thin bright band sweeping left to right over the first half of the loop
  if (f < N / 2) {
    const s = lerp(-8, 24, f / (N / 2 - 1));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const g = x - s + (y - y0) * 0.45;
      if (g >= 0 && g < 1) { const c = D.get(im, x, y); if (c) put(im, x, y, c[1] > 120 ? P.W : P.R3); }
    }
  }
  if (f === N - 2) twinkle(im, 12, 12, 1, [P.W, P.G3]);
  const out = withOutline(im, OUT);
  // the knot and tassel, tied at the right side, swaying a pixel either way
  const sway = [0, 1, 1, 0, 0, -1, -1, 0][f % 8];
  px(out, [[18, 15], [19, 15], [19, 16], [18, 16]], P.R2);
  put(out, 19, 15, P.R3);
  for (let y = 17; y <= 19; y++) put(out, 19 + (y > 18 ? sway : 0), y, P.G1);
  for (let y = 20; y <= 26; y++) {
    const x = 19 + sway + (y > 23 ? sway : 0);
    put(out, x, y, y > 24 ? P.G1 : P.G2);
    if (y > 21) put(out, x - 1, y, P.G1);
    if (y > 22) put(out, x + 1, y, y === 26 ? P.G0 : P.G1);
  }
  put(out, 19 + sway, 20, P.R2);
  return out;
}
function makeEnvelope() {
  const N = 8, frames = [];
  for (let f = 0; f < N; f++) frames.push(envelopeSmall(f, N));
  return el("fe_envelope", "Fortune", 22, 28, [{ name: "envelope", frames }], ms(N).fill(90), []);
}

// the pool of gold light it sits in, 40x16, breathing
function makeGlow() {
  const Wd = 40, H = 16, N = 8, frames = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(Wd, H), breathe = 0.82 + 0.18 * Math.sin(f / N * TAU);
    D.each(F, (x, y) => {
      const d = Math.hypot((x - Wd / 2) / (Wd / 2), (y - H / 2) / (H / 2));
      return d < 1 ? Math.pow(1 - d, 1.4) * 0.75 * breathe : 0;
    });
    frames.push(D.shade(img(Wd, H), F, [[0.2, P.G1], [0.42, P.G2], [0.62, P.G3]]));
  }
  return el("fe_glow", "Fortune", Wd, H, [{ name: "glow", frames }], ms(N).fill(90), []);
}

// the beam, 14x80: gold light rising out of it and thinning, motes drifting up through it, so it's
// seen from across the screen. its foot is the canvas's bottom
function makePillar() {
  const Wd = 14, H = 80, N = 8, frames = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(Wd, H);
    D.each(F, (x, y) => {
      const up = 1 - y / H;                                   // 0 at the foot, 1 at the top
      const half = lerp(5.5, 1.6, Math.pow(up, 0.7));
      const d = Math.abs(x - Wd / 2) / half;
      if (d > 1) return 0;
      const n = W.loopNoise ? W.loopNoise(x * 0.4, y * 0.12, f, N, -1.2, 11) : fbm(x * 0.4, y * 0.12 + f, 11);
      return (1 - d) * Math.pow(1 - up, 1.2) * (0.35 + 0.55 * n);
    });
    const im = D.shade(img(Wd, H), F, [[0.16, P.G1], [0.32, P.G2], [0.52, P.G3], [0.7, P.W]]);
    // motes: a few bright specks rising the height of the beam over the loop
    for (let k = 0; k < 5; k++) {
      const y = H - 1 - ((f / N + k / 5) % 1) * (H - 6), x = Wd / 2 + Math.round(Math.sin(k * 2.1 + f * 0.8) * 2.5);
      put(im, x, y, k % 2 ? P.G3 : P.W);
    }
    frames.push(im);
  }
  return el("fe_pillar", "Fortune", Wd, H, [{ name: "beam", frames }], ms(N).fill(80), []);
}

// the marker bobbing over it, 13x11: a gold arrowhead pointing down at it, a red jewel in its middle,
// light running down it
function makeMarker() {
  const Wd = 13, H = 11, N = 6, frames = [];
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H);
    const M = poly(mask(Wd, H), [[0.5, 0.5], [12.5, 0.5], [6.5, 9.5]]);
    const cut = poly(mask(Wd, H), [[4, 0.5], [9, 0.5], [6.5, 3.2]]);
    const shape = D.minus(M, cut);
    for (let y = 0; y < H; y++) for (let x = 0; x < Wd; x++) {
      if (!D.mget(shape, x, y)) continue;
      const band = (y - f * 2 + 20) % 12;
      let c = x > 6 ? P.G1 : P.G2;
      if (y === 0 || (x > 6 && !D.mget(shape, x + 1, y))) c = P.G3;
      if (band < 2) c = band < 1 ? P.W : P.G3;
      put(im, x, y, c);
    }
    px(im, [[6, 4], [5, 5], [6, 5], [7, 5], [6, 6]], P.R2);
    put(im, 6, 4, P.R3);
    frames.push(withOutline(im, OUT));
  }
  return el("fe_marker", "Fortune", Wd, H, [{ name: "marker", frames }], ms(N).fill(90), []);
}

// the little envelope over an elite or boss that carries one, 9x11, a glint on its seal
function makeCarry() {
  const Wd = 9, H = 11, N = 4, frames = [];
  for (let f = 0; f < N; f++) {
    const im = img(Wd, H);
    const body = rect(mask(Wd, H), 1, 1, 7, 9);
    lacquer(im, body, { sheen: true });
    goldFrame(im, 1, 1, 7, 9, P.G1, P.G0);
    const flap = poly(mask(Wd, H), [[1, 1], [8, 1], [4.5, 5.5]]);
    for (let y = 2; y < 6; y++) for (let x = 2; x < 7; x++) if (D.mget(flap, x, y)) put(im, x, y, P.R1);
    px(im, [[4, 5], [3, 5], [5, 5], [4, 4], [4, 6]], P.G2);
    put(im, 4, 5, f === 1 ? P.W : P.G3);
    if (f === 2) put(im, 5, 4, P.W);
    frames.push(withOutline(im, OUT));
  }
  return el("fe_carry", "Fortune", Wd, H, [{ name: "carry", frames }], ms(N).fill(120), []);
}

// picked up, 56x56: a white-hot flare, a gold ring breaking into dashes as it spreads, red paper
// and gold sparks thrown out, all dithering away
function makePickup() {
  const S = 56, c = S / 2, N = 9, frames = [];
  const bits = [];
  for (let k = 0; k < 14; k++) bits.push({ a: k / 14 * TAU + hash2(k, 1, 3) * 0.4, v: 0.55 + hash2(k, 2, 3) * 0.5, red: k % 3 === 0 });
  for (let f = 0; f < N; f++) {
    const t = f / (N - 1), im = img(S, S);
    if (f <= 2) {
      const F = D.field(S, S);
      D.flare(F, c, c, [9, 16, 12][f], [3, 3, 2][f], [0, 7, 5][f], 1, 1, 0.4);
      D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < [4, 5, 3][f] ? 1 : 0; });
      D.over(im, D.shade(img(S, S), F, GOLD));
    }
    if (f >= 1) {
      const R = lerp(5, 24, easeOut((f - 1) / (N - 2)));
      const F = D.field(S, S);
      ringField(F, c, c, R, lerp(3, 1.2, t), lerp(0.95, 0.35, t), f > 3 ? breakup(R, 5, lerp(0.8, 0.35, t)) : null);
      D.over(im, D.shade(img(S, S), F, GOLD));
    }
    for (const b of bits) {
      const d = lerp(4, 25, easeOut(t)) * b.v, x = c + Math.cos(b.a) * d, y = c + Math.sin(b.a) * d + t * t * 4;
      if (f > 0 && bayer(Math.round(x), Math.round(y)) < 1.1 - t) {
        if (b.red) { put(im, x, y, P.R2); put(im, x + 1, y, t > 0.5 ? P.R1 : P.R3); }
        else twinkle(im, x, y, f < 4 ? 1 : 0, [P.W, P.G2]);
      }
    }
    frames.push(im);
  }
  return el("fe_pickup", "Fortune", S, S, [{ name: "burst", frames }], ms(N).fill(40), []);
}

// ================================================================ THE OPENING

// the big envelope, 48x72, its body filling the lower 52 rows so the flap can open upward into the
// space above: crimson lacquer, a double gold frame, clouds in the corners and a gold lattice
// diamond, the flap edged in gold down to a big seal
const BX0 = 4, BX1 = 43, BY0 = 18, BY1 = 69, HINGE = BY0, SEAL_Y = 38;
function frontFace(im, { openTop = false } = {}) {
  const body = rect(mask(48, 72), BX0, BY0, BX1, BY1);
  lacquer(im, body);
  goldFrame(im, BX0 + 2, BY0 + 2, BX1 - 2, BY1 - 2, P.G2, P.G1);
  goldFrame(im, BX0 + 4, BY0 + 4, BX1 - 4, BY1 - 4, P.G1, P.G0);
  // corner knots where the two frames meet
  for (const [x, y] of [[BX0 + 3, BY0 + 3], [BX1 - 3, BY0 + 3], [BX0 + 3, BY1 - 3], [BX1 - 3, BY1 - 3]]) { put(im, x, y, P.G3); }
  // clouds in the lower corners and a lattice diamond between them
  cloud(im, BX0 + 7, BY1 - 11, 1, P.G1, P.G2);
  cloud(im, BX1 - 7, BY1 - 11, -1, P.G1, P.G2);
  const cx = 24, cy = BY1 - 13;
  for (let dy = -6; dy <= 6; dy++) for (let dx = -6; dx <= 6; dx++) {
    const m = Math.abs(dx) + Math.abs(dy);
    if (m === 6) put(im, cx + dx, cy + dy, dy < 0 ? P.G2 : P.G1);
    else if (m < 6 && (dx + dy) % 3 === 0 && (dx - dy) % 3 === 0) put(im, cx + dx, cy + dy, P.G1);
  }
  put(im, cx, cy, P.G3);
  if (openTop) {
    // the pocket's lip: its top edge folded over, lit
    for (let x = BX0; x <= BX1; x++) { put(im, x, BY0, P.R3); put(im, x, BY0 + 1, P.R2); }
  }
  return im;
}
// the flap closed over the front, from the hinge down to the seal, with its gold edge
function flapClosed(im, glow) {
  const flap = poly(mask(48, 72), [[BX0 + 1, HINGE + 1], [BX1, HINGE + 1], [24, SEAL_Y + 0.5]]);
  const edge = D.minus(D.dilate(flap, 1, false), flap);
  for (let y = 0; y < 72; y++) for (let x = 0; x < 48; x++) {
    if (D.mget(flap, x, y)) {
      const u = (x - BX0) / (BX1 - BX0);
      put(im, x, y, u < 0.1 ? P.LQ1 : P.R1);
    } else if (D.mget(edge, x, y) && y > HINGE + 1 && y <= SEAL_Y + 1 && x > BX0 && x < BX1) {
      put(im, x, y, glow > 0.55 ? (glow > 0.8 ? P.W : P.G3) : x > 24 ? P.G2 : P.G1);
    }
  }
  // light leaking along the flap's seam as it gives
  if (glow > 0.55) {
    const leak = D.minus(D.dilate(edge, 1, true), D.dilate(flap, 1, false));
    for (let y = 0; y < 72; y++) for (let x = 0; x < 48; x++) if (y <= HINGE + 2 || x <= BX0 + 1 || x >= BX1 - 1) leak.m[y * 48 + x] = 0;
    D.paint(im, leak, glow > 0.8 ? P.G3 : P.G2, glow > 0.8 ? 0.6 : 0.3, 1);
  }
  seal(im, 24, SEAL_Y, 6.5, glow);
  return im;
}
// closed, the seal charging: rest, the seal warming, light breaking along the seam, blazing
function makeBig() {
  const N = 8, frames = [];
  const glows = [0, 0.32, 0.45, 0.56, 0.66, 0.76, 0.86, 0.96];
  for (let f = 0; f < N; f++) {
    const im = img(48, 72), g = glows[f];
    frontFace(im);
    flapClosed(im, g);
    const out = withOutline(im, OUT);
    // a halo around the seal and, blazing, thin rays out of it
    if (g > 0.3) {
      const F = D.field(48, 72);
      D.each(F, (x, y) => { const d = Math.hypot(x - 24, y - SEAL_Y); return d > 6.5 && d < 6.5 + g * 6 ? (1 - (d - 6.5) / (g * 6)) * g * 0.8 : 0; });
      if (g > 0.8) D.flare(F, 24, SEAL_Y, 14, 2, 7, 1, 0.95, 0.35, 0);
      const glowIm = D.shade(img(48, 72), F, [[0.18, P.G1], [0.36, P.G2], [0.6, P.G3], [0.85, P.W]]);
      // the halo sits over the lacquer but under the seal
      const sealMask = ellipse(mask(48, 72), 24, SEAL_Y, 6.5, 6.5);
      for (let y = 0; y < 72; y++) for (let x = 0; x < 48; x++) { const c = D.get(glowIm, x, y); if (c && !D.mget(sealMask, x, y)) put(out, x, y, c); }
    }
    frames.push(out);
  }
  return el("fe_big", "Fortune", 48, 72, [{ name: "envelope", frames }], ms(N).fill(90), []);
}
// opening: the flap folds up about the hinge and stands open, its gold-patterned lining showing,
// and light pours up out of the pocket. the front is drawn in (the flap covers it at first), and
// fe_front is laid over the scroll once it starts to rise
function makeFlap() {
  const N = 6, frames = [];
  // the flap's tip, from closed (down at the seal) to standing open above the hinge
  const tips = [SEAL_Y, 28, HINGE + 3, HINGE - 6, HINGE - 14, HINGE - 17];
  for (let f = 0; f < N; f++) {
    const im = img(48, 72), tip = tips[f];
    frontFace(im, { openTop: f >= 3 });
    if (f < 3) {
      // folding up over the front: shorter each frame, the seal riding its tip
      const flap = poly(mask(48, 72), [[BX0 + 1, HINGE + 1], [BX1, HINGE + 1], [24, tip + 0.5]]);
      for (let y = 0; y < 72; y++) for (let x = 0; x < 48; x++) if (D.mget(flap, x, y)) put(im, x, y, f === 2 ? P.R2 : P.R1);
      const edge = D.minus(D.dilate(flap, 1, false), flap);
      for (let y = HINGE + 2; y < 72; y++) for (let x = BX0 + 1; x < BX1; x++) if (D.mget(edge, x, y) && y <= tip + 1) put(im, x, y, P.G3);
      seal(im, 24, tip - (f === 2 ? 1 : 0), f === 2 ? 4.5 : 6, 1);
    } else {
      // standing open above the hinge: its lining faces us, dark lacquer with gold dots
      const flap = poly(mask(48, 72), [[BX0, HINGE], [BX1 + 1, HINGE], [24, tip]]);
      for (let y = 0; y < 72; y++) for (let x = 0; x < 48; x++) {
        if (!D.mget(flap, x, y) || y >= HINGE) continue;
        let c = P.LQ1;
        if ((x + y * 2) % 6 === 0 && (y % 3 === 0)) c = P.G0;
        if (x > 30 && bayer(x, y) < 0.4) c = P.R1;
        put(im, x, y, c);
      }
      const edge = D.minus(D.dilate(flap, 1, false), flap);
      for (let y = 0; y < HINGE; y++) for (let x = 0; x < 48; x++) if (D.mget(edge, x, y)) put(im, x, y, x > 24 ? P.G2 : P.G1);
      seal(im, 24, tip + 5, 4.5, 0.4);
    }
    const out = withOutline(im, OUT);
    // light pouring up out of the open pocket
    if (f >= 2) {
      const F = D.field(48, 72), k = (f - 1) / (N - 2);
      D.each(F, (x, y) => {
        if (y > HINGE + 1) return 0;
        const up = (HINGE + 1 - y) / (HINGE + 1), spread = Math.abs(x - 24) / (18 + up * 6);
        if (spread > 1) return 0;
        // thin shafts fanning out of the pocket, so the flap still shows between them
        const a = (x - 24) / (HINGE + 2 - y + 2);
        const shaft = Math.pow(Math.max(0, Math.cos(a * 7.5 + 0.4)), 6);
        return (1 - spread) * Math.pow(1 - up, 1.3) * shaft * k * 1.2;
      });
      for (let x = BX0 + 1; x < BX1; x++) D.fmax(F, x, HINGE, 0.95 * k + 0.1);
      const L = D.shade(img(48, 72), F, [[0.14, P.G1], [0.3, P.G2], [0.52, P.G3], [0.78, P.W]]);
      for (let y = 0; y <= HINGE + 1; y++) for (let x = 0; x < 48; x++) { const c = D.get(L, x, y); if (c) put(out, x, y, c); }
    }
    frames.push(out);
  }
  return el("fe_flap", "Fortune", 48, 72, [{ name: "envelope", frames }], [70, 60, 60, 70, 80, 90], []);
}
function makeFront() {
  const im = img(48, 72);
  frontFace(im, { openTop: true });
  // only the part below the hinge: the scroll rises from behind it
  const out = withOutline(im, OUT);
  return el("fe_front", "Fortune", 48, 72, [{ name: "front", frames: [out] }], [100], []);
}

// soft round light, 64x64, greyscale for the game to tint by rarity
function makeAura() {
  const S = 64, F = D.field(S, S);
  D.each(F, (x, y) => { const d = Math.hypot(x - S / 2, y - S / 2) / (S / 2); return d < 1 ? Math.pow(1 - d, 1.6) : 0; });
  return el("fe_aura", "Fortune", S, S, [{ name: "light", frames: [D.shade(img(S, S), F, GREY)] }], [100], []);
}
// god rays, 128x128: sixteen tapering blades of light of uneven length, greyscale, turned by the game
function makeRays() {
  const S = 128, c = S / 2, F = D.field(S, S);
  for (let k = 0; k < 16; k++) {
    const a = k / 16 * TAU + (hash2(k, 7, 1) - 0.5) * 0.12;
    const len = (k % 2 ? 38 : 60) * (0.8 + hash2(k, 3, 2) * 0.25);
    D.ray(F, c, c, a, len, k % 2 ? 5 : 9, 0.95, 0.12);
  }
  D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < 8 ? 1 - d / 16 : 0; });
  return el("fe_rays", "Fortune", S, S, [{ name: "rays", frames: [D.shade(img(S, S), F, GREY)] }], [100], []);
}
// a twinkle, 7x7, greyscale: rising, a four-point star, falling
function makeMote() {
  const S = 7, frames = [];
  for (let f = 0; f < 4; f++) {
    const im = img(S, S);
    const size = [0, 1, 2, 1][f];
    twinkle(im, 3, 3, size, [P.W, P.GR3, P.GR2]);
    frames.push(im);
  }
  return el("fe_mote", "Fortune", S, S, [{ name: "mote", frames }], [70, 70, 70, 70], []);
}
// a reward landing on the scroll, 96x96: flare, a broken ring spreading, sparks, in the rarity's ramp.
// the legendary one throws embers and a second ring
function makeBurst(name, ramp, extra) {
  const S = 96, c = S / 2, N = 10, frames = [];
  const sparks = [];
  for (let k = 0; k < 18; k++) sparks.push({ a: k / 18 * TAU + hash2(k, 5, 9) * 0.3, v: 0.5 + hash2(k, 6, 9) * 0.6 });
  for (let f = 0; f < N; f++) {
    const t = f / (N - 1), im = img(S, S);
    if (f <= 3) {
      const F = D.field(S, S);
      D.flare(F, c, c, [18, 30, 24, 14][f], [4, 4, 3, 2][f], [0, 14, 10, 6][f], 2, 1, 0.4, f * 0.08);
      D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < [7, 9, 6, 3][f] ? 1 - d / 20 : 0; });
      D.over(im, D.shade(img(S, S), F, ramp));
    }
    if (f >= 1) {
      const R = lerp(8, 44, easeOut((f - 1) / (N - 2)));
      const F = D.field(S, S);
      ringField(F, c, c, R, lerp(5, 1.5, t), lerp(0.95, 0.3, t), f > 3 ? breakup(R, 3, lerp(0.85, 0.3, t)) : null);
      if (extra && f >= 3) {
        const R2 = lerp(4, 30, easeOut((f - 3) / (N - 4)));
        ringField(F, c, c, R2, 2, lerp(0.8, 0.25, t), breakup(R2, 8, 0.6));
      }
      D.over(im, D.shade(img(S, S), F, ramp));
    }
    for (const s of sparks) {
      const d = lerp(6, 44, easeOut(t)) * s.v, x = c + Math.cos(s.a) * d, y = c + Math.sin(s.a) * d + (extra ? -t * 6 : 0);
      if (f > 0 && bayer(Math.round(x), Math.round(y)) < 1.15 - t) twinkle(im, x, y, f < 5 ? 1 : 0, [ramp[ramp.length - 1][1], ramp[ramp.length - 3][1]]);
    }
    frames.push(im);
  }
  return el(name, "Fortune", S, S, [{ name: "burst", frames }], ms(N).fill(40), []);
}

// the scroll's roller, 10x60: dark wood with gold caps, a red cord and tassel off the bottom cap
function makeRoller() {
  const Wd = 10, H = 60, im = img(Wd, H);
  const rod = rect(mask(Wd, H), 3, 5, 6, 50);
  shade(im, rod, [P.WD0, P.WD1, P.WD2, P.WD3], { dark: 0.3, light: 0.7 });
  for (const y0 of [2, 49]) {
    const cap = rect(mask(Wd, H), 1, y0, 8, y0 + 3);
    shade(im, cap, [P.G0, P.G1, P.G2, P.G3], { dark: 0.25, light: 0.65 });
  }
  px(im, [[4, 1], [5, 1], [4, 53], [5, 53]], P.G1);
  // the cord and tassel
  put(im, 5, 54, P.R2); put(im, 5, 55, P.R2); put(im, 4, 55, P.R1);
  for (let y = 56; y < 60; y++) { put(im, 4, y, P.R2); put(im, 5, y, y > 57 ? P.R1 : P.R3); if (y > 56) put(im, 6, y, P.R1); }
  return el("fe_roller", "Fortune", Wd, H, [{ name: "roller", frames: [withOutline(im, OUT)] }], [100], []);
}
// the scroll, 168x52: aged paper in a red double rule, clouds in its corners, its edges a little
// uneven. the rewards are laid across its middle by the game
function makeScroll() {
  const Wd = 168, H = 52, im = img(Wd, H);
  for (let y = 2; y < H - 2; y++) for (let x = 0; x < Wd; x++) {
    const n = fbm(x * 0.09, y * 0.12, 21);
    // deckled top and bottom edges
    if ((y === 2 || y === H - 3) && hash2(x, y, 4) < 0.35) continue;
    let c = n > 0.68 && bayer(x, y) < 0.5 ? P.PA3 : P.PA2;
    if (n < 0.3 && bayer(x, y) < 0.35) c = P.PA1;
    put(im, x, y, c);
  }
  // shading at the ends where it curls onto the rollers
  for (let y = 2; y < H - 2; y++) for (let k = 0; k < 4; k++) { if (bayer(k, y) < 0.8 - k * 0.2) { put(im, k, y, P.PA0); put(im, Wd - 1 - k, y, P.PA1); } }
  // the red double rule
  for (let x = 6; x < Wd - 6; x++) { put(im, x, 5, P.R2); put(im, x, 7, P.R1); put(im, x, H - 6, P.R2); put(im, x, H - 8, P.R1); }
  for (let y = 5; y <= H - 6; y++) { put(im, 6, y, P.R2); put(im, 8, y, P.R1); put(im, Wd - 7, y, P.R2); put(im, Wd - 9, y, P.R1); }
  // clouds in the corners, in red ink
  cloud(im, 11, 10, 1, P.R1, P.R2);
  cloud(im, Wd - 12, 10, -1, P.R1, P.R2);
  cloud(im, 11, H - 14, 1, P.R1, P.R2);
  cloud(im, Wd - 12, H - 14, -1, P.R1, P.R2);
  return el("fe_scroll", "Fortune", Wd, H, [{ name: "paper", frames: [withOutline(im, P.PA0)] }], [100], []);
}
// a reward's frame, 26x26: a dark well in a gold frame with knots at its corners. the evolution's,
// 34x40 with the same frame low in it, burns: fire climbing its sides and licking up past its top
function slot(f, evo) {
  const S = 26, im = img(S, S);
  for (let y = 3; y < S - 3; y++) for (let x = 3; x < S - 3; x++) put(im, x, y, (x + y) % 2 && bayer(x, y) < 0.3 ? P.V1 : P.V0);
  goldFrame(im, 2, 2, S - 3, S - 3, P.G2, P.G1);
  goldFrame(im, 3, 3, S - 4, S - 4, P.G0, P.G0);
  for (const [x, y] of [[2, 2], [S - 3, 2], [2, S - 3], [S - 3, S - 3]]) {
    px(im, [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]], P.G1);
    put(im, x, y, P.G3);
  }
  const frame = withOutline(im, OUT);
  if (!evo) return frame;
  const Wd = 34, H = 40, ox = 4, oy = 11, out = img(Wd, H);
  const F = D.field(Wd, H);
  D.each(F, (x, y) => {
    // distance outside the frame's square, and how high up it is
    const fx = x - ox, fy = y - oy;
    const outX = Math.max(0, -fx, fx - S), outY = Math.max(0, fy - S);
    const above = Math.max(0, -fy);
    const outside = outX > 0 || outY > 0 || fy < 0;
    if (!outside || outX > 4 || outY > 3 || fx < -4 || fx > S + 4) return 0;
    const n = W.loopNoise(x * 0.32, y * 0.26, f, 4, -2.4, 31);
    if (n < 0.38) return 0;                     // gaps, so it's flames and not a wall
    if (above > 0) {
      // tongues of uneven height licking up off the top edge, swaying as they go
      const col = Math.floor((fx + 8) / 5), tall = 4 + hash2(col, 0, 5) * 10;
      const sway = Math.sin(y * 0.6 + f * 1.57 + col) * 1.2;
      const inTongue = Math.abs(((fx + 8 + sway) % 5) - 2.5) < 2.2 * (1 - above / tall);
      return inTongue ? (1 - above / tall) * (0.4 + n * 0.7) : 0;
    }
    // hugging the sides, strongest right against the frame, rising: brighter higher up
    return (0.3 + n * 0.7) * (1 - (outX + outY) / 4.5) * (0.7 + 0.4 * (1 - fy / S));
  });
  D.over(out, D.shade(img(Wd, H), F, FIRE));
  D.blit(out, frame, ox, oy);
  // the frame's gold turns white-hot where the fire licks it
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const c = D.get(frame, x, y);
    if (c && c[0] === P.G2[0] && c[1] === P.G2[1] && F.f[(y + oy) * Wd + x + ox] > 0.55) put(out, x + ox, y + oy, P.G3);
  }
  return out;
}
function makeSlots() {
  return [
    el("fe_slot", "Fortune", 26, 26, [{ name: "frame", frames: [slot(0, false)] }], [100], []),
    el("fe_slot_evo", "Fortune", 34, 40, [{ name: "frame", frames: [0, 1, 2, 3].map(f => slot(f, true)) }], [80, 80, 80, 80], []),
  ];
}
// the rarity's ribbon, 80x16, greyscale for the game to tint: a banner with swallow-tailed ends
function makeBanner() {
  const Wd = 80, H = 16, im = img(Wd, H);
  const M = poly(mask(Wd, H), [[0, 2], [8, 2], [8, 13], [0, 13], [4, 7.5]]);
  poly(M, [[Wd, 2], [Wd - 8, 2], [Wd - 8, 13], [Wd, 13], [Wd - 4, 7.5]]);
  rect(M, 8, 1, Wd - 9, 13);
  for (let y = 0; y < H; y++) for (let x = 0; x < Wd; x++) {
    if (!D.mget(M, x, y)) continue;
    const tail = x < 8 || x >= Wd - 8;
    let c = tail ? P.GR2 : P.GR3;
    if (y === 1 || (tail && y === 2)) c = P.W;
    if (y >= 12) c = tail ? P.GR1 : P.GR2;
    put(im, x, y, c);
  }
  // the folds where the tails tuck behind
  for (let y = 2; y <= 13; y++) { put(im, 8, y, P.GR1); put(im, Wd - 9, y, P.GR1); }
  return el("fe_banner", "Fortune", Wd, H, [{ name: "ribbon", frames: [withOutline(im, OUT)] }], [100], []);
}
// coins, 16x16: three wen fanned out on a red cord, like the bronze wen dropped in a run (round,
// a dark hole in the middle), the light running across them in turn
function makeCoin() {
  const S = 16, N = 4, frames = [];
  const coins = [[11, 4.5], [7.5, 8], [4, 11.5]];
  for (let f = 0; f < N; f++) {
    const out = img(S, S);
    coins.forEach(([cx, cy], i) => {
      // each coin drawn and outlined on its own, back to front, so they read as three
      const im = img(S, S);
      const M = ellipse(mask(S, S), cx, cy, 3.9, 3.9);
      for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
        if (!D.mget(M, x, y)) continue;
        const dx = x + 0.5 - cx, dy = y + 0.5 - cy, d = Math.hypot(dx, dy) / 3.9, side = (dx - dy) / 3.9;
        let c = d > 0.72 ? (side > 0.3 ? P.G2 : side < -0.3 ? P.G0 : P.G1) : (side > 0.15 ? P.G2 : P.G1);
        if (i === f % 3 && d > 0.72 && side > 0.55) c = P.G3;
        put(im, x, y, c);
      }
      // the hole, square, with the cord running through it
      const hx = Math.floor(cx - 0.5), hy = Math.floor(cy - 0.5);
      px(im, [[hx, hy], [hx + 1, hy], [hx, hy + 1], [hx + 1, hy + 1]], P.V0);
      put(im, hx + 1, hy, P.R2);
      put(im, hx, hy + 1, P.R1);
      D.blit(out, withOutline(im, OUT), 0, 0);
    });
    // the cord's ends: a knot off the top coin and a tassel off the bottom one
    px(out, [[14, 1], [14, 2], [15, 1]], P.R2); put(out, 15, 1, P.R3);
    for (let y = 13; y < 16; y++) { put(out, 1, y, P.R2); put(out, 2, y, y > 13 ? P.R1 : P.R3); }
    if (f === 3) twinkle(out, 12, 3, 1, [P.W, P.G3]);
    frames.push(out);
  }
  return el("fe_coin", "Fortune", S, S, [{ name: "coins", frames }], ms(N).fill(140), []);
}
// a heal, 16x16: a peach of immortality like the longevity peach dropped in a run: round and pink,
// flushed red toward its foot, a pointed tip, a leaf, a glint travelling over it
function makePeach() {
  const S = 16, N = 4, frames = [];
  for (let f = 0; f < N; f++) {
    const im = img(S, S);
    const M = ellipse(mask(S, S), 8, 9, 6, 5.6);
    poly(M, [[5.5, 5], [9.5, 1.8], [11, 5]]);
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      if (!D.mget(M, x, y)) continue;
      const dx = x + 0.5 - 8, dy = y + 0.5 - 9;
      const side = (dx - dy) / 7;
      let c = side > 0.35 ? P.K3 : side > -0.3 ? P.K2 : P.K1;
      // the red flush at its foot, dithered in
      if (dy > 1.5 && dx > -1 && bayer(x, y) < (dy - 1.5) / 4) c = P.R2;
      put(im, x, y, c);
    }
    // the crease, curving down from the tip
    for (let y = 4; y < 13; y++) put(im, Math.round(9 - (y - 4) * 0.28), y, P.K1);
    // the leaf, off the stem to the left
    const leaf = poly(mask(S, S), [[8, 3.5], [3.5, 1], [1.5, 3], [5, 4.8]]);
    shade(im, leaf, [P.J1, P.J2, P.J3], { dark: 0.3 });
    put(im, 8, 3, P.WD1);
    const out = withOutline(im, OUT);
    const glint = [[11, 6], [12, 7], [11, 8], [10, 6]][f];
    put(out, glint[0], glint[1], P.W);
    if (f === 1) put(out, glint[0] - 1, glint[1], P.K3);
    frames.push(out);
  }
  return el("fe_peach", "Fortune", S, S, [{ name: "peach", frames }], ms(N).fill(140), []);
}

// ================================================================ THE WUCHANG

// the soul-catching chain: one link lying flat and one turned edge on, 8x6, cold iron
function makeLink() {
  const Wd = 8, H = 6, frames = [];
  {
    const im = img(Wd, H);
    const M = ellipse(mask(Wd, H), 4, 3, 3.8, 2.6);
    const hole = ellipse(mask(Wd, H), 4, 3, 1.8, 0.8);
    const ring = D.minus(M, hole);
    for (let y = 0; y < H; y++) for (let x = 0; x < Wd; x++) if (D.mget(ring, x, y)) put(im, x, y, y <= 1 ? P.ST3 : y >= 4 ? P.ST1 : P.ST2);
    frames.push(withOutline(im, P.ST0));
  }
  {
    const im = img(Wd, H);
    for (let x = 0; x < Wd; x++) { put(im, x, 2, P.ST3); put(im, x, 3, P.ST2); }
    put(im, 0, 3, P.ST1); put(im, Wd - 1, 3, P.ST1);
    frames.push(withOutline(im, P.ST0));
  }
  return el("wc_link", "Wuchang", Wd, H, [{ name: "link", frames }], [100, 100], []);
}
// the shackle at the chain's end, 11x11: an iron collar, open, a talisman strip tied to it
function makeHook() {
  const S = 11, im = img(S, S);
  const M = ellipse(mask(S, S), 5.5, 5.5, 5, 5);
  const hole = ellipse(mask(S, S), 5.5, 5.5, 3, 3);
  const ring = D.minus(M, hole);
  // opened at the front
  for (let y = 4; y <= 7; y++) for (let x = 8; x < S; x++) ring.m[y * S + x] = 0;
  shade(im, ring, [P.ST1, P.ST2, P.ST3], { dark: 0.3 });
  put(im, 8, 3, P.ST3); put(im, 8, 8, P.ST1);
  const out = withOutline(im, P.ST0);
  // the yellow talisman strip tied at the back, fluttering
  for (let y = 5; y < 11; y++) { put(out, 0, y, P.TL1); if (y > 6) put(out, 1, y, P.TL0); }
  put(out, 0, 7, P.R2);
  return el("wc_hook", "Wuchang", S, S, [{ name: "shackle", frames: [out] }], [100], []);
}
// the soul, 16x22: a pale flame of the player drawn out, two dark eyes, its tail streaming up
function makeSoul() {
  const Wd = 16, H = 22, N = 6, frames = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(Wd, H);
    D.each(F, (x, y) => {
      // a teardrop: round at the bottom, drawn up into a flickering tail
      const cy = 15, r = 5.2;
      const up = clamp01((cy - y) / 13);
      const sway = Math.sin(up * 3.4 - f / N * TAU) * up * 2.6;
      const w = y > cy ? Math.sqrt(Math.max(0, r * r - (y - cy) ** 2)) : r * (1 - Math.pow(up, 0.8));
      const d = Math.abs(x - (Wd / 2 + sway));
      if (d > w) return 0;
      const n = W.loopNoise ? W.loopNoise(x * 0.4, y * 0.35, f, N, 1.2, 41) : fbm(x * 0.4, y * 0.35, 41);
      return (1 - d / (w + 0.01)) * 0.7 + 0.3 * n - up * 0.25 + 0.15;
    });
    const im = D.shade(img(Wd, H), F, [[0.12, P.I1], [0.26, P.I2], [0.42, P.I3], [0.6, P.I4], [0.78, P.I5], [0.9, P.W]]);
    // the eyes, blinking once
    if (f !== 4) { px(im, [[6, 14], [6, 15], [9, 14], [9, 15]], P.I0); }
    else { px(im, [[6, 15], [9, 15]], P.I0); }
    frames.push(im);
  }
  return el("wc_soul", "Wuchang", Wd, H, [{ name: "soul", frames }], ms(N).fill(80), []);
}
// the dark it's swallowed into, 64x64: an ink whirl of plum and black, a red rim burning at its lip,
// turning
function makeMaw() {
  const S = 64, H = 40, c = S / 2, cy = H / 2, N = 8, frames = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(S, H), Rm = D.field(S, H);
    D.each(F, (x, y) => {
      const dx = x - c, dy = (y - cy) * 1.6, d = Math.hypot(dx, dy) / 30;
      if (d > 1) return 0;
      const a = Math.atan2(dy, dx) + d * 4.2 - f / N * TAU / 3;
      const arms = 0.5 + 0.5 * Math.cos(a * 3);
      return (1 - d) * 0.35 + arms * (1 - d) * 0.75 + 0.2 * (1 - d);
    });
    D.each(Rm, (x, y) => {
      const dx = x - c, dy = (y - cy) * 1.6, d = Math.hypot(dx, dy) / 30;
      if (d > 1 || d < 0.72) return 0;
      const a = Math.atan2(dy, dx) - f / N * TAU / 3;
      return (1 - Math.abs(d - 0.86) / 0.14) * (0.55 + 0.45 * vnoise(a * 3 + 10, f * 0.5, 7));
    });
    const im = D.shade(img(S, H), F, [[0.15, P.V2], [0.3, P.V1], [0.5, P.V0], [0.75, P.K]]);
    D.over(im, D.shade(img(S, H), Rm, [[0.35, P.R0], [0.55, P.R1], [0.8, P.R2]]));
    frames.push(im);
  }
  return el("wc_maw", "Wuchang", S, H, [{ name: "whirl", frames }], ms(N).fill(70), []);
}
// ink flooding the screen, 96x54 (the screen's shape): blots bloom from a few points and run
// together until it's black, a red seal pressed in the middle last
function makeInk() {
  const Wd = 96, H = 54, N = 9, frames = [];
  const seeds = [[48, 27, 0], [18, 12, 0.14], [80, 42, 0.22], [72, 8, 0.36], [12, 46, 0.42], [36, 52, 0.52], [92, 22, 0.58]];
  for (let f = 0; f < N; f++) {
    const t = Math.min(1, (f + 1) / (N - 1));
    const F = D.field(Wd, H);
    D.each(F, (x, y) => {
      let v = 0;
      for (const [sx, sy, delay] of seeds) {
        const k = clamp01((t - delay) / (1 - delay));
        if (k <= 0) continue;
        const r = Math.pow(k, 1.25) * 58 * (0.8 + 0.4 * fbm(x * 0.09, y * 0.09, 55));
        const d = Math.hypot(x - sx, y - sy);
        v = Math.max(v, clamp01((r - d) / 6 + 0.5));
      }
      return v;
    });
    const im = D.shade(img(Wd, H), F, [[0.3, P.V1], [0.55, P.V0], [0.8, P.K]]);
    if (f === N - 1) {
      // the seal: 回, return, pressed in red, its strokes broken like a real impression
      for (let y = 18; y < 36; y++) for (let x = 39; x < 57; x++) {
        const u = x - 39, v = y - 18;
        const outer = u < 2 || u > 15 || v < 2 || v > 15;
        const ring = !outer && (u === 4 || u === 13 || v === 4 || v === 13) && u >= 4 && u <= 13 && v >= 4 && v <= 13;
        const inner = (u === 7 || u === 10 || v === 7 || v === 10) && u >= 7 && u <= 10 && v >= 7 && v <= 10;
        if (!(outer || ring || inner)) continue;
        if (hash2(x, y, 9) < (outer ? 0.12 : 0.06)) continue;
        put(im, x, y, hash2(x, y, 3) < 0.22 ? P.R1 : P.R2);
      }
    }
    frames.push(im);
  }
  return el("wc_ink", "Wuchang", Wd, H, [{ name: "ink", frames }], [60, 60, 60, 60, 60, 60, 60, 80, 400], []);
}

// ================================================================ write everything
const BUILDERS = {
  ground: () => [makeEnvelope(), makeGlow(), makePillar(), makeMarker(), makeCarry(), makePickup()],
  opening: () => [makeBig(), makeFlap(), makeFront(), makeAura(), makeRays(), makeMote(),
    makeBurst("fe_burst_common", JADE_B, false), makeBurst("fe_burst_rare", AZURE_B, false), makeBurst("fe_burst_legendary", IMPERIAL, true),
    makeRoller(), makeScroll(), ...makeSlots(), makeBanner(), makeCoin(), makePeach()],
  wuchang: () => [makeLink(), makeHook(), makeSoul(), makeMaw(), makeInk()],
};

function build() {
  const only = process.argv.slice(2);
  const elements = [];
  for (const [name, make] of Object.entries(BUILDERS)) if (!only.length || only.includes(name)) elements.push(...make());
  const out = path.join(__dirname, "out");
  if (!only.length) fs.rmSync(out, { recursive: true, force: true });
  fs.mkdirSync(out, { recursive: true });
  const manifestFile = path.join(out, "manifest.json");
  const old = only.length && fs.existsSync(manifestFile) ? JSON.parse(fs.readFileSync(manifestFile, "utf8")).elements : [];
  const manifest = old.filter(m => !elements.some(e => e.name === m.name));
  for (const e of elements) {
    const dir = path.join(out, e.name);
    fs.rmSync(dir, { recursive: true, force: true });
    fs.mkdirSync(dir, { recursive: true });
    const n = e.layers[0].frames.length, flat = [];
    if (e.durations.length !== n) throw new Error(`${e.name}: ${e.durations.length} durations for ${n} frames`);
    for (let f = 0; f < n; f++) {
      const im = img(e.w, e.h);
      e.layers.forEach(l => D.over(im, l.frames[f]));
      let lit = 0; for (let i = 3; i < im.data.length; i += 4) if (im.data[i]) lit++;
      if (!lit) throw new Error(`${e.name} frame ${f} is empty; Unity's importer would drop it`);
      flat.push(im);
      e.layers.forEach((l, li) => png.encode(l.frames[f], path.join(dir, `L${li}_${f}.png`)));
    }
    const { layers, ...meta } = e;
    manifest.push(Object.assign(meta, { frames: n, layers: layers.map(l => l.name) }));
    const scale = e.w <= 24 ? 8 : e.w <= 48 ? 6 : e.w <= 100 ? 4 : 3;
    png.encode(png.preview(flat, scale, [22, 18, 30], Math.min(flat.length, e.w <= 48 ? 8 : 5), 2), path.join(out, `sheet_${e.name}.png`));
  }
  fs.writeFileSync(manifestFile, JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(elements.map(m => `${m.group}/${m.name} ${m.w}x${m.h} x${m.layers[0].frames.length}`).join("\n"));
}

module.exports = { P };
if (require.main === module) build();
