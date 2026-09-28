// the Treasure Gourd (Bao Hulu) and its evolution, the Gourd of Heaven and Earth (Qiankun Hulu), in
// the Flying Sword's and the Command Token's style: the same palette, ramps and ordered dithering,
// plum outlines on solid things, and their attack language (a white-hot flare with thin rays, a
// ring breaking into dashes as it spreads, violet smoke dithering out). the gourd pulls with the
// Command Token's violet (it traps spirits), burns with holy gold-white fire, and evolved it fires
// back what it swallowed as a violet plasma sphere wrapped in a gold ring (heaven and earth).
// sizes in world pixels (28.46 per unit):
//   node gourd.js          draws everything into out/ (plus a sheet_*.png preview of each)
//   node ../write-ase.js out "../../../Assets/### Different Engine/NewSprites/Asesprites/VFX/Weapons"
// then Tools > VFX > Build Weapon FX in Unity points the weapon at them
//   tg_gourd         hovering at the player's shoulder, corked, upright (18x28, 4 frames)
//   tg_gourd_aim     uncorked and aimed, pointing right (30x20): 0-3 pulling, 4-7 spraying
//   tg_pop           the cork popping out (24x24, 5)
//   tg_suck          the pull, a cone of spirit wind into the mouth at (2, 32), 92px long (96x64, 6, loops)
//   tg_flame         the holy fire, a cone out of the mouth at (3, 32), 104px long (112x64): 0-1 catch,
//                    2-7 loop, 8-10 die
//   tg_burn          an enemy burning (12x16, 6, loops)
//   tg_orb           the plasma sphere, 9px radius (32x32, 6, loops); also the charge in the mouth
//   tg_blast         the sphere bursting, drawn for a 60px radius (144x144, 12)
//   tg_absorb        a bullet swallowed (16x16, 5)
//   tg_icon, tg_icon_evolved   the level up icons (32x32, 4)
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const K2 = require("../weapons2/weapons2");
const { put, bayer, hex } = D;
const { GOLD, TAU, easeOut, lerp, img, fbm, vnoise, hash2, withOutline, ringField, breakup, twinkle, el } = W;
const { mask, poly, px, flareFrame, brokenRing } = K2;

const P = Object.assign({}, K2.P, {
  // the gourd's skin: a calabash dried golden, lacquered
  H0: hex("#4a2a14"), H1: hex("#8a5424"), H2: hex("#c8883a"), H3: hex("#eab860"), H4: hex("#fff0b0"),
});
const OUT = P.V0;
// holy fire: a white-gold heart, gold, orange, and red only at its very edges
const HOLY = [[0.06, P.R1], [0.13, P.R2], [0.22, P.O1], [0.34, P.O2], [0.5, P.G2], [0.68, P.G3], [0.86, P.W]];
const ELECTRO = D.ELECTRO, SMOKE = D.SMOKE;
const clamp01 = v => Math.max(0, Math.min(1, v));
const frac = v => v - Math.floor(v);
const GROUP = "TreasureGourd";

// ================================================================ the gourd itself

// the gourd's outline along its axis, from its foot (u 0) to the top of its stopper: the big lower
// bulb, a pinched waist, the small upper bulb, a neck, a lip and the cork
const LEN = 23.6, LIP = 21.2;
function radius(u, corked) {
  let r = 0;
  if (u >= 0 && u <= 12) r = Math.max(r, Math.sqrt(Math.max(0, 36 - (u - 6) ** 2)));          // lower bulb
  if (u >= 11.4 && u <= 19.2) r = Math.max(r, Math.sqrt(Math.max(0, 13.7 - (u - 15.4) ** 2)));  // upper bulb
  if (u >= 11 && u <= LIP) r = Math.max(r, 1.8);                                              // waist and neck
  if (u > LIP - 0.9 && u <= LIP) r = Math.max(r, 2.4);                                        // the lip
  if (corked && u > LIP && u <= LEN) r = Math.max(r, 1.6);                                    // the cork
  return r;
}

// the gourd on a canvas, its foot at (bx, by) and its axis `a` pointing at the mouth. lit from the
// side of the axis that faces up and right. opts: corked, glint (where along the axis the travelling
// highlight is, -1 for none), mouth ("dark" the swallowing hole, "hot" the rim glowing), tassel phase
function gourd(Wd, H, bx, by, ax, ay, opts = {}) {
  const { corked = true, glint = -1, mouth = null, tassel = 0 } = opts;
  const im = img(Wd, H);
  let pxv = -ay, pyv = ax;
  if (pxv - pyv < 0) { pxv = -pxv; pyv = -pyv; }            // the lit side: up and to the right
  for (let y = 0; y < H; y++) for (let x = 0; x < Wd; x++) {
    const dx = x + 0.5 - bx, dy = y + 0.5 - by;
    const u = dx * ax + dy * ay, v = dx * pxv + dy * pyv;
    const r = radius(u, corked);
    if (r <= 0 || Math.abs(v) > r) continue;
    const k = v / r;
    let c;
    if (u > LIP) {                                            // the cork: rough wood, a red string tied round it
      c = u < LIP + 0.7 ? (k > 0.3 ? P.R3 : P.R2) : k > 0.35 ? P.WD3 : k < -0.4 ? P.WD1 : P.WD2;
    } else if (u > 11 && u < 12.6) {                          // the red cord round the waist
      c = k > 0.35 ? P.R3 : k < -0.4 ? P.R1 : P.R2;
    } else if (u > 2.8 && u < 9.8 && v > 0.4 && v < 3.4) {    // a talisman pasted on the lit side of the lower bulb
      const stroke = Math.abs(v - 1.9) < 0.5 && u > 3.8 && u < 8.9, bar = Math.abs(u - 7.2) < 0.5 && Math.abs(v - 1.9) < 1.1;
      c = stroke || bar ? P.R2 : u < 3.6 || u > 9 ? P.TL0 : v > 2.6 ? P.TL2 : P.TL1;
    } else {
      c = k > 0.42 ? P.H3 : k < -0.5 ? P.H1 : P.H2;
      if (u < 1.2 && k < 0.42) c = P.H1;                      // the foot sits in shadow
      if (u > LIP - 0.9 && mouth === "hot") c = k > -0.2 ? P.W : P.G3;
      // a bright spot on each bulb, and the glint running up it
      if (k > 0.55 && k < 0.85 && (Math.abs(u - 7.5) < 1.2 || Math.abs(u - 16.2) < 0.8)) c = P.H4;
      if (glint >= 0 && Math.abs(u - glint) < 0.7 && k > 0.2) c = P.H4;
    }
    put(im, x, y, c);
  }
  // uncorked, the mouth: a dark hole where the pull goes in
  if (!corked && mouth === "dark") {
    const mx = bx + ax * (LIP + 0.2), my = by + ay * (LIP + 0.2);
    put(im, mx, my, P.V0); put(im, mx - ax, my - ay, P.V1);
  }
  const out = withOutline(im, OUT);
  // a red tassel hanging from the cord on the shaded side, swaying
  const wx = bx + ax * 11.8 - pxv * 2.4, wy = by + ay * 11.8 - pyv * 2.4;
  const T = img(Wd, H), sway = [0, 1, 1, 0, 0, -1, -1, 0];
  put(T, wx, wy, P.R3);
  for (let i = 1; i <= 5; i++) {
    const s = i > 2 ? sway[(tassel + i) % 8] : 0;
    put(T, wx + s, wy + i, i < 3 ? P.R2 : P.R1);
    if (i > 3) put(T, wx + s + 1, wy + i, P.R1);
  }
  D.over(out, withOutline(T, OUT));
  return out;
}

// hovering at the player's shoulder, corked and upright: the tassel swaying, a glint climbing it and
// a thread of trapped spirit seeping out past the cork
function makeGourd() {
  const Wd = 18, H = 28, N = 4, frames = [], wisps = [];
  for (let f = 0; f < N; f++) {
    frames.push(gourd(Wd, H, 9, 26.5, 0, -1, { corked: true, glint: [4, 8, 14, -1][f], tassel: f * 2 }));
    const Wp = img(Wd, H), rise = [0, 1, 2, 3][f];
    put(Wp, 10 + (f % 2), 2 - rise * 0.5, f < 3 ? P.V4 : P.V3);
    if (f < 2) put(Wp, 9, 1 - f, P.V2);
    wisps.push(Wp);
  }
  return el("tg_gourd", GROUP, Wd, H, [{ name: "wisp", frames: wisps }, { name: "gourd", frames }], Array(N).fill(140), [["idle", 0, N - 1]]);
}

// uncorked and aimed, pointing right, the mouth at (24, 10). pulling (0-3) it trembles, the mouth
// a dark hole with a violet swirl turning at its lip and motes being drawn in; spraying (4-7) it
// kicks back a pixel with every gout, its lip white hot and a flare at the mouth
function makeGourdAim() {
  const Wd = 30, H = 20, N = 8, frames = [], fx = [];
  const bx = 2, by = 10.5, mx = 2 + LIP + 0.8, my = 10;
  for (let f = 0; f < N; f++) {
    const pulling = f < 4, k = f % 4;
    const shake = pulling ? [0, 1, 0, -1][k] : 0, kick = pulling ? 0 : [-1, 0, -1, 0][k];
    frames.push(gourd(Wd, H, bx + kick, by + shake * 0.5, 1, 0, { corked: false, mouth: pulling ? "dark" : "hot", tassel: f * 2 }));
    const E = img(Wd, H);
    if (pulling) {
      // a swirl at the lip: four dots turning inward, and motes streaming into the mouth
      for (let i = 0; i < 4; i++) {
        const a = (i / 4 + k / 16) * TAU, r = 3.2 - (k % 2) * 0.6;
        put(E, mx + 1 + Math.cos(a) * r * 0.6, my + Math.sin(a) * r, i % 2 ? P.V5 : P.V4);
      }
      for (let i = 0; i < 3; i++) {
        const d = 5 - ((k + i * 1.3) % 4) * 1.2;
        put(E, mx + d, my + [-2, 1, 3][i] * (d / 5), d < 2.5 ? P.V6 : P.V3);
      }
    } else {
      // a flare at the mouth, bigger on the gouts
      const F = D.field(Wd, H);
      D.flare(F, mx + 1, my + 0.5, [5, 3, 5, 3][k], 2, [2, 0, 2, 0][k], 1, 1, 0.45);
      D.each(F, (x, y) => Math.hypot(x - mx - 1, y - my - 0.5) < 1.6 ? 1 : 0);
      D.shade(E, F, HOLY);
    }
    fx.push(E);
  }
  return el("tg_gourd_aim", GROUP, Wd, H, [{ name: "gourd", frames }, { name: "mouth", frames: fx }], Array(N).fill(60),
    [["pull", 0, 3], ["spray", 4, 7]]);
}

// the cork popping, 24x24: a small gold flare, a ring snapping out and breaking up, the cork thrown
// up and away, a puff of trapped spirit escaping
function makePop() {
  const S = 24, c = 12, N = 5, frames = [];
  for (let f = 0; f < N; f++) {
    let im;
    if (f === 0) im = flareFrame(S, c, 8, GOLD, 3);
    else im = brokenRing(S, c, [0, 4, 7, 9, 10][f], [0, 1.8, 1.4, 1.1, 1][f], [0, 0.95, 0.7, 0.5, 0.35][f], GOLD, 31 + f, f >= 2 ? 0.6 : 1);
    // the puff of spirit: violet smoke curling off
    if (f >= 1) {
      const F = D.field(S, S);
      D.each(F, (x, y) => {
        const d = Math.hypot(x - c + 2, y - c + 1 + f * 0.6);
        return d < 2 + f * 1.3 ? Math.max(0, fbm(x * 0.45, y * 0.45, 5 + f) - 0.42) * (0.3 - f * 0.05) : 0;
      });
      D.over(im, D.shade(img(S, S), F, SMOKE));
    }
    // the cork, tumbling up and off to the right
    if (f >= 1) {
      const x = c + 2 + f * 2.2, y = c - 1 - f * 2.4 + f * f * 0.35, turn = f % 2;
      const C = img(S, S);
      px(C, turn ? [[x, y], [x + 1, y], [x, y + 1], [x + 1, y + 1], [x + 2, y]] : [[x, y], [x, y + 1], [x + 1, y + 1], [x, y + 2], [x + 1, y]], P.WD2);
      put(C, x, y, P.WD3); put(C, x + 1, y + 1, P.R2);
      D.over(im, withOutline(C, OUT));
    }
    frames.push(im);
  }
  return el("tg_pop", GROUP, S, S, [{ name: "pop", frames }], Array(N).fill(40), [["pop", 0, N - 1]]);
}

// ================================================================ the pull

// a cone of spirit wind pulling into the mouth at (2, 32), 92px long and 36 degrees either side,
// read at a glance: speed lines converging on the mouth, stretching as they speed up toward it, a
// small vortex turning at the lip, motes swept along, and a faint dotted arc marching inward along
// the edge of its reach. loops over its 6 frames
const SUCK_W = 96, SUCK_H = 64, SUCK_X = 2, SUCK_Y = 32, SUCK_L = 92, SUCK_A = 0.63;
function makeSuck() {
  const N = 6, edge = [], streaks = [], motes = [], vortex = [];
  const r = D.rng(1717);
  const lines = Array.from({ length: 22 }, (_, i) => ({ a0: ((i + 0.2 + r() * 0.6) / 22 * 2 - 1) * SUCK_A, p0: r() }));
  const specks = Array.from({ length: 34 }, () => ({ a0: (r() * 2 - 1) * SUCK_A, p0: r() }));
  const angleAt = (a0, d) => a0 * Math.pow(Math.min(1, d / SUCK_L), 0.3);
  for (let f = 0; f < N; f++) {
    // the reach: a dotted arc, its dots marching in toward the middle
    const E = img(SUCK_W, SUCK_H), R = SUCK_L - 3;
    for (let k = -40; k <= 40; k++) {
      const a = k / 40 * SUCK_A;
      if (((k + 40) * 3 + (k < 0 ? f : -f) + 120) % 6 >= 2) continue;
      put(E, SUCK_X + Math.cos(a) * R, SUCK_Y + Math.sin(a) * R, Math.abs(k) > 30 ? P.V1 : P.V2);
    }
    edge.push(E);

    // speed lines, radial, longer and brighter the closer they are: things speed up as they go in
    const S = img(SUCK_W, SUCK_H);
    for (const l of lines) {
      const t = frac(l.p0 - f / N), d0 = 7 + t * (SUCK_L - 10), len = lerp(15, 4, t);
      for (let s = 0; s <= len; s += 0.5) {
        const d = d0 + s;
        if (d > SUCK_L - 5) break;
        const a = angleAt(l.a0, d), x = SUCK_X + Math.cos(a) * d, y = SUCK_Y + Math.sin(a) * d;
        const tail = s / len;
        if (tail > 0.55 && bayer(Math.round(x), Math.round(y)) < (tail - 0.55) * 2) continue;
        const near = d / SUCK_L;
        put(S, x, y, s < 1 ? (near < 0.3 ? P.W : P.V6) : near < 0.25 ? P.V5 : tail < 0.5 ? P.V4 : P.V3);
      }
    }
    streaks.push(S);

    // the vortex at the lip: three arms turning in, squashed against the mouth
    const V = img(SUCK_W, SUCK_H), vx = SUCK_X + 4, vy = SUCK_Y;
    for (let y = vy - 11; y <= vy + 11; y++) for (let x = SUCK_X; x <= vx + 8; x++) {
      const dx = (x + 0.5 - vx) / 0.7, dy = y + 0.5 - vy, d = Math.hypot(dx, dy);
      if (d > 11 || d < 1.5) continue;
      const arm = Math.cos(3 * (Math.atan2(dy, dx) + d * 0.28 + f / N * TAU / 3));
      if (arm < 0.72) continue;
      if (bayer(x, y) > 1.15 - d / 11) continue;
      put(V, x, y, d < 4 ? P.W : d < 7 ? P.V6 : P.V4);
    }
    vortex.push(V);

    // motes: loose specks of spirit swept in, faster near the mouth
    const M = img(SUCK_W, SUCK_H);
    for (const sp of specks) {
      const t = Math.pow(frac(sp.p0 - f / N), 1.4), d = 6 + t * (SUCK_L - 10);
      const a = angleAt(sp.a0, d);
      put(M, SUCK_X + Math.cos(a) * d, SUCK_Y + Math.sin(a) * d, t < 0.25 ? P.V6 : t < 0.6 ? P.V4 : P.V2);
    }
    motes.push(M);
  }
  return el("tg_suck", GROUP, SUCK_W, SUCK_H, [{ name: "reach", frames: edge }, { name: "motes", frames: motes }, { name: "streaks", frames: streaks }, { name: "vortex", frames: vortex }],
    Array(N).fill(50), [["pull", 0, N - 1]], { pivot: "left" });
}

// ================================================================ the holy fire

// a cone of holy fire out of the mouth at (3, 32), 104px long: a white-gold heart at the mouth,
// rolling tongues scrolling outward, red only at its ragged edges, violet smoke and scraps of
// burning talisman paper thrown off the far end. 0-1 catch, 2-7 loop, 8-10 die
const FL_W = 112, FL_H = 64, FL_X = 3, FL_Y = 32, FL_L = 104;
function flameField(f, reach, fade, from, seed) {
  const F = D.field(FL_W, FL_H), n = f >= 2 && f <= 7 ? f - 2 : f;
  D.each(F, (x, y) => {
    const dx = x - FL_X, dy = y - FL_Y;
    if (dx < 0) return 0;
    const u = dx / FL_L;
    if (u < from) return 0;
    const half = 2.4 + dx * 0.27;
    const wob = (loopNoise(x * 0.05, y * 0.05, n, 6, 0.9, seed + 5) - 0.5) * 7 * u;
    const v = Math.abs(dy + wob) / half;
    if (v > 1.25) return 0;
    const turb = loopNoise(x * 0.085, y * 0.11, n, 6, 1.4, seed);
    let I = (1 - v * v * 0.85) * lerp(1.0, 0.36, Math.pow(u, 0.8)) * (0.3 + 1.2 * Math.pow(turb, 1.3));
    I -= Math.max(0, u - 0.6) * 1.9 * (1.05 - turb);           // the far end breaks into tongues
    I -= Math.max(0, u - reach + 0.18) * 5 * (1.15 - turb);     // the front, ragged while it catches
    I -= Math.max(0, v - 0.75) * 0.9;
    I = Math.max(I, 1.0 - u / 0.22 - v * 0.9);                   // the white heart, tapering out of the mouth
    if (from > 0) I -= Math.max(0, from + 0.12 - u) * 3;          // dying from the mouth out
    return Math.max(0, I * fade);
  });
  return F;
}
function loopNoise(x, y, f, n, step, seed) {
  const w = f / n;
  return (1 - w) * fbm(x - f * step, y, seed) + w * fbm(x - (f - n) * step, y, seed);
}
function makeFlame() {
  const N = 11, fire = [], smoke = [], embers = [];
  const r = D.rng(909);
  const scraps = Array.from({ length: 14 }, () => ({ a: (r() * 2 - 1) * 0.3, p: r(), paper: r() < 0.45 }));
  for (let f = 0; f < N; f++) {
    const catching = f < 2, dying = f >= 8;
    const reach = catching ? [0.4, 0.78][f] : 1.0;
    const fade = catching ? 1.15 : dying ? [0.8, 0.55, 0.3][f - 8] : 1;
    const from = dying ? [0.12, 0.32, 0.55][f - 8] : 0;
    const F = flameField(f, reach, fade, from, 41);
    fire.push(D.shade(img(FL_W, FL_H), F, HOLY));

    // smoke off the tips and the edges, thickest as it dies
    const Sm = D.field(FL_W, FL_H), n = f >= 2 && f <= 7 ? f - 2 : f;
    D.each(Sm, (x, y) => {
      const dx = x - FL_X, u = dx / FL_L;
      if (u < 0.45 || u > reach + 0.05) return 0;
      const half = 2.4 + dx * 0.27, v = Math.abs(y - FL_Y) / half;
      if (v > 1.3) return 0;
      const n2 = loopNoise(x * 0.07, y * 0.09, n, 6, 1.1, 77);
      const edge = clamp01((FL_W - 4 - x) / 14);                  // thinning out before the canvas ends
      const v2 = Math.max(0, n2 - 0.45) * (u - 0.45) * 0.55 * (dying ? 1.5 : 1) * (v > 0.55 || u > 0.8 ? 1 : 0.3) * edge;
      return v2;
    });
    smoke.push(D.shade(img(FL_W, FL_H), Sm, SMOKE));

    // gold motes and scraps of talisman paper flung out the far end
    const E = img(FL_W, FL_H);
    if (!catching) for (const s of scraps) {
      const t = frac(s.p + (f >= 2 && f <= 7 ? (f - 2) / 6 : f / 6)), d = (0.45 + t * 0.6) * FL_L;
      if (d / FL_L > reach + 0.08 || (dying && bayer(f, Math.round(s.p * 7)) < (f - 7) * 0.3)) continue;
      const x = FL_X + Math.cos(s.a) * d, y = FL_Y + Math.sin(s.a) * d * 1.2 - t * 4;
      if (s.paper) { put(E, x, y, P.TL1); put(E, x + 1, y, t < 0.5 ? P.TL2 : P.O2); }
      else twinkle(E, x, y, t < 0.3 ? 1 : 0, [P.W, P.G3]);
    }
    embers.push(E);
  }
  const d = [45, 45, 55, 55, 55, 55, 55, 55, 60, 60, 60];
  return el("tg_flame", GROUP, FL_W, FL_H, [{ name: "smoke", frames: smoke }, { name: "fire", frames: fire }, { name: "embers", frames: embers }],
    d, [["catch", 0, 1], ["spray", 2, 7], ["die", 8, 10]], { pivot: "left" });
}

// an enemy burning in holy fire, 12x16, a small fire licking up from its feet, loops
function makeBurn() {
  const Wd = 12, H = 16, N = 6, frames = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(Wd, H);
    D.each(F, (x, y) => {
      const h = 1 - y / H, half = 4.6 * Math.pow(1 - h, 0.55) + 0.4;
      const wob = (loopNoise(x * 0.2, y * 0.25, f, N, -0.8, 13) - 0.5) * 3 * h;
      const v = Math.abs(x - 6 + wob) / half;
      if (v > 1) return 0;
      const n = loopNoise(x * 0.3, y * 0.22 + 0.0, f, N, 1.2, 9);
      return (1 - v) * (1.1 - h * 1.25) * (0.55 + 0.9 * n) + (h < 0.25 && v < 0.35 ? 0.3 : 0);
    });
    const im = D.shade(img(Wd, H), F, HOLY);
    const sy = 4 - (f % 3) * 1.4;
    put(im, 6 + [0, 1, -1, 0, 1, -1][f], sy, f % 2 ? P.G3 : P.W);
    frames.push(im);
  }
  return el("tg_burn", GROUP, Wd, H, [{ name: "burn", frames }], Array(N).fill(70), [["burn", 0, N - 1]], { pivot: "bottom" });
}

// ================================================================ the evolution

// the plasma sphere, 32x32, 9px radius: a white core, a violet shell darkening to its rim, a gold
// taiji swirl turning inside it, a gold ring tilted round it (heaven and earth) with its dashes
// running round, and arcs crackling off its surface. loops
function makeOrb() {
  const S = 32, c = 16, R = 9, N = 6, back = [], sphere = [], front = [], arcs = [];
  const ring = (f, frontHalf) => {
    const im = img(S, S), tilt = -0.35, rx = 14, ry = 4.4;
    for (let i = 0; i < 90; i++) {
      const a = i / 90 * TAU, dash = (i + f * 5) % 15 < 9;
      if (!dash) continue;
      const ex = Math.cos(a) * rx, ey = Math.sin(a) * ry;
      const x = c + ex * Math.cos(tilt) - ey * Math.sin(tilt), y = c + ex * Math.sin(tilt) + ey * Math.cos(tilt);
      if ((Math.sin(a) > 0) !== frontHalf) continue;
      put(im, x, y, Math.sin(a) > 0.5 ? P.G3 : P.G2);
    }
    return im;
  };
  for (let f = 0; f < N; f++) {
    back.push(ring(f, false));
    const F = D.field(S, S);
    D.each(F, (x, y) => {
      const d = Math.hypot(x - c, y - c);
      if (d > R + 0.5) return 0;
      return d < 2.6 ? 1 : lerp(0.82, 0.2, (d - 2.6) / (R - 2.6));
    });
    const sp = D.shade(img(S, S), F, ELECTRO);
    // the taiji swirl: two gold commas turning
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const dx = x + 0.5 - c, dy = y + 0.5 - c, d = Math.hypot(dx, dy);
      if (d < 3 || d > R - 1.2) continue;
      const a = Math.atan2(dy, dx) - f / N * TAU / 2 - d * 0.32;
      const s = Math.sin(a * 2);
      if (s > 0.86) put(sp, x, y, d < 5.5 ? P.G3 : P.G2);
    }
    sphere.push(sp);
    front.push(ring(f, true));
    // arcs off the surface, struck anew every frame
    const Ar = img(S, S), rr = D.rng(500 + f * 11);
    for (let k = 0; k < 3; k++) {
      const a = rr() * TAU, a2 = a + (rr() - 0.5) * 0.9, r1 = R + 3 + rr() * 3;
      const M = D.polyline(mask(S, S), D.bolt(rr, c + Math.cos(a) * (R - 0.5), c + Math.sin(a) * (R - 0.5), c + Math.cos(a2) * r1, c + Math.sin(a2) * r1, 1.8, 2));
      D.paint(Ar, M, k === 0 ? P.W : P.V5);
    }
    // a dithered glow just outside the shell
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const d = Math.hypot(x + 0.5 - c, y + 0.5 - c);
      if (d > R + 0.5 && d < R + 2.5 && bayer(x + f, y) < 0.35) put(Ar, x, y, P.V3);
    }
    arcs.push(Ar);
  }
  return el("tg_orb", GROUP, S, S, [{ name: "ring back", frames: back }, { name: "glow", frames: arcs }, { name: "sphere", frames: sphere }, { name: "ring front", frames: front }],
    Array(N).fill(60), [["spin", 0, N - 1]]);
}

// the sphere bursting, 144x144, drawn for a 60px radius, in the Command Token's detonation's
// language: a white flare with long thin rays, a violet sphere with a dark heart and crescents
// whipping round it, a gold ring of broken trigrams bursting out, a violet ring breaking into
// dashes, smoke billowing and dithering away, sparks
function makeBlast() {
  const S = 144, c = 72, N = 12, burst = [], smoke = [], gold = [], sparks = [];
  const r = D.rng(6060);
  const flecks = Array.from({ length: 22 }, () => ({ a: r() * TAU, v: 0.6 + r() * 0.8 }));
  for (let f = 0; f < N; f++) {
    const t = f / (N - 1);
    const B = img(S, S), F = D.field(S, S);
    if (f === 0) {
      D.flare(F, c, c, 62, 5, 36, 2, 1, 0.3);
      for (let k = 0; k < 10; k++) D.ray(F, c, c, k / 10 * TAU + 0.15, 30 + (k % 3) * 8, 1.4, 0.8, 0.3);
      D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < 11 ? 1 : d < 15 ? 0.75 : 0; });
      D.shade(B, F, ELECTRO);
    } else if (f <= 2) {
      // the sphere, its heart gone dark, its rim white
      const R = [0, 22, 27][f];
      D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); if (d > R) return 0; const k = d / R; return k > 0.86 ? 1 : lerp(0.14, 0.62, k * k); });
      D.shade(B, F, ELECTRO);
      // crescents whipping round it
      for (let k = 0; k < 7; k++) {
        const a0 = k / 7 * TAU + f * 0.35, CF = D.field(S, S);
        D.each(CF, (x, y) => {
          const dx = x - c, dy = y - c, d = Math.hypot(dx, dy);
          let a = Math.atan2(dy, dx) - a0; a = ((a % TAU) + TAU) % TAU;
          if (a > 1.1) return 0;
          const mid = R + 4 + a * (10 + f * 5), w = 4.5 * (1 - a / 1.1);
          return Math.abs(d - mid) < w ? 0.5 + 0.35 * (1 - a / 1.1) : 0;
        });
        D.over(B, D.shade(img(S, S), CF, ELECTRO));
      }
    } else {
      // the ring breaking into dashes as it spreads
      const R = lerp(34, 64, easeOut((f - 3) / (N - 4)));
      ringField(F, c, c, R, lerp(3, 1, t), lerp(0.9, 0.3, t), breakup(R, f + 3, lerp(0.8, 0.35, t)));
      D.shade(B, F, ELECTRO);
      // the last crescents, thinning
      if (f <= 5) for (let k = 0; k < 7; k++) {
        const a0 = k / 7 * TAU + f * 0.35, CF = D.field(S, S), R2 = 30 + f * 6;
        D.each(CF, (x, y) => {
          const dx = x - c, dy = y - c, d = Math.hypot(dx, dy);
          let a = Math.atan2(dy, dx) - a0; a = ((a % TAU) + TAU) % TAU;
          if (a > 0.8) return 0;
          return Math.abs(d - R2 - a * 12) < 1.6 * (1 - a / 0.8) + 0.3 ? 0.5 : 0;
        });
        D.over(B, D.shade(img(S, S), CF, ELECTRO));
      }
    }
    burst.push(B);

    // the gold ring of heaven and earth: trigram bars bursting out and breaking, frames 1-6
    const G = img(S, S);
    if (f >= 1 && f <= 6) {
      const R = lerp(30, 58, easeOut((f - 1) / 5));
      for (let k = 0; k < 8; k++) {
        const a = k / 8 * TAU + f * 0.08;
        for (let bar = 0; bar < 3; bar++) {
          const broken = (k >> bar) & 1, rr2 = Math.round(R) + (bar - 1) * 2;
          if (f >= 4 && hash2(k, bar, f) < (f - 3) * 0.3) continue;       // whole bars go, not pixels
          // a straight bar across the radius, 7px, split in the middle for a broken (yin) line
          const tx = -Math.sin(a), ty = Math.cos(a), bx0 = c + Math.cos(a) * rr2, by0 = c + Math.sin(a) * rr2;
          for (let s = -3; s <= 3; s++) {
            if (broken && Math.abs(s) < 1) continue;
            put(G, bx0 + tx * s, by0 + ty * s, f < 3 ? P.G3 : P.G2);
          }
        }
      }
    }
    gold.push(G);

    // smoke billowing up where it burst, dithering away
    const Sm = D.field(S, S);
    if (f >= 2) {
      const k = (f - 2) / (N - 3);
      D.each(Sm, (x, y) => {
        const d = Math.hypot(x - c, y - c + k * 6), mid = 24 + k * 16, band = 11 + k * 7;
        const ring = 1 - Math.abs(d - mid) / band;
        if (ring <= 0) return 0;
        const n = fbm(x * 0.07 + k * 0.6, y * 0.07 - k * 0.4, 11);
        return Math.max(0, n * ring - 0.3) * 0.62 * (1 - k * 0.8);
      });
    }
    smoke.push(D.shade(img(S, S), Sm, SMOKE));

    // sparks thrown out
    const Sp = img(S, S);
    if (f >= 1) for (const fl of flecks) {
      const d = (12 + f * 7) * fl.v, x = c + Math.cos(fl.a) * d, y = c + Math.sin(fl.a) * d + f * f * 0.15;
      if (f > 7 && bayer(Math.round(x), Math.round(y)) < (f - 7) * 0.25) continue;
      twinkle(Sp, x, y, f < 4 ? 1 : 0, [P.W, fl.v > 1 ? P.G3 : P.V6]);
    }
    sparks.push(Sp);
  }
  const d = [40, 40, 45, 45, 50, 50, 55, 55, 60, 60, 65, 70];
  return el("tg_blast", GROUP, S, S, [{ name: "smoke", frames: smoke }, { name: "burst", frames: burst }, { name: "trigrams", frames: gold }, { name: "sparks", frames: sparks }],
    d, [["burst", 0, N - 1]]);
}

// a bullet swallowed at the mouth, 16x16: a violet ring closing onto a point, which flares and goes
function makeAbsorb() {
  const S = 16, c = 8, N = 5, frames = [];
  for (let f = 0; f < N; f++) {
    let im;
    if (f < 3) {
      const F = D.field(S, S);
      ringField(F, c, c, [7, 4.5, 2.2][f], 1.2, [0.5, 0.7, 0.95][f], f === 0 ? breakup(7, 3, 0.6) : null);
      im = D.shade(img(S, S), F, ELECTRO);
    } else im = img(S, S);
    if (f >= 2) twinkle(im, c, c, f === 3 ? 2 : f === 2 ? 1 : 0, [P.W, P.V6, P.V4]);
    frames.push(im);
  }
  return el("tg_absorb", GROUP, S, S, [{ name: "absorb", frames }], Array(N).fill(40), [["absorb", 0, N - 1]]);
}

// ================================================================ the icons

// the gourd aimed up and to the right, holy fire pouring out of it at the corner, spirit streaks
// being drawn in round it; evolved, a violet sphere hanging at its mouth in its gold ring
function makeIcons() {
  const S = 32, N = 4, a = [], b = [];
  const ax = Math.cos(-0.72), ay = Math.sin(-0.72);
  for (let f = 0; f < N; f++) {
    const im = img(S, S);
    // the fire, out toward the top right corner
    const F = D.field(S, S), mx = 3 + ax * 21.5, my = 29 + ay * 21.5;
    D.each(F, (x, y) => {
      const dx = x - mx, dy = y - my, u = dx * ax + dy * ay, v = Math.abs(-dx * ay + dy * ax);
      if (u < 0 || u > 16) return 0;
      const half = 1.3 + u * 0.42, k = v / half;
      if (k > 1) return 0;
      const n = loopNoise(u * 0.4, v * 0.4, f, N, 0.8, 21);
      return (1 - k * k) * lerp(1.1, 0.35, u / 16) * (0.6 + 0.8 * n) + (u < 4 && k < 0.4 ? 0.5 : 0);
    });
    D.shade(im, F, HOLY);
    D.over(im, gourd(S, S, 3, 29, ax, ay, { corked: false, mouth: "hot", glint: [5, 9, 15, -1][f], tassel: f * 2 }));
    twinkle(im, 27 - f, 5 + (f % 2), 0, [P.W]);
    a.push(im);

    const ev = img(S, S), ox = 3 + ax * 26, oy = 29 + ay * 26;
    // the sphere at the mouth, spinning in its ring, arcs licking off it
    const sphere = makeOrbAt(S, ox, oy, 6.2, f);
    D.over(ev, gourd(S, S, 2, 30, ax, ay, { corked: false, mouth: "dark", tassel: f * 2 }));
    D.over(ev, sphere);
    b.push(ev);
  }
  return [
    el("tg_icon", GROUP, S, S, [{ name: "icon", frames: a }], Array(N).fill(130), [["idle", 0, N - 1]]),
    el("tg_icon_evolved", GROUP, S, S, [{ name: "icon", frames: b }], Array(N).fill(130), [["idle", 0, N - 1]]),
  ];
}
// a small plasma sphere with its gold ring, centred at (cx, cy), for the evolved icon
function makeOrbAt(S, cx, cy, R, f) {
  const im = img(S, S), tilt = -0.35, rx = R + 4, ry = 2.6;
  const ring = front => {
    for (let i = 0; i < 60; i++) {
      const a = i / 60 * TAU;
      if ((i + f * 4) % 12 >= 8 || (Math.sin(a) > 0) !== front) continue;
      const ex = Math.cos(a) * rx, ey = Math.sin(a) * ry;
      put(im, cx + ex * Math.cos(tilt) - ey * Math.sin(tilt), cy + ex * Math.sin(tilt) + ey * Math.cos(tilt), front ? P.G3 : P.G1);
    }
  };
  ring(false);
  const F = D.field(S, S);
  D.each(F, (x, y) => { const d = Math.hypot(x - cx, y - cy); return d > R ? 0 : d < 1.8 ? 1 : lerp(0.8, 0.22, (d - 1.8) / (R - 1.8)); });
  D.shade(im, F, ELECTRO);
  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const dx = x + 0.5 - cx, dy = y + 0.5 - cy, d = Math.hypot(dx, dy);
    if (d < 2 || d > R - 1) continue;
    if (Math.sin((Math.atan2(dy, dx) - f / N4 * Math.PI - d * 0.45) * 2) > 0.8) put(im, x, y, P.G3);
  }
  ring(true);
  const rr = D.rng(90 + f);
  const a0 = rr() * TAU;
  D.paint(im, D.polyline(mask(S, S), D.bolt(rr, cx + Math.cos(a0) * R, cy + Math.sin(a0) * R, cx + Math.cos(a0 + 0.5) * (R + 4), cy + Math.sin(a0 + 0.5) * (R + 4), 1.2, 2)), P.V6);
  return im;
}
const N4 = 4;

// ================================================================ write everything
function build() {
  const elements = [makeGourd(), makeGourdAim(), makePop(), makeSuck(), makeFlame(), makeBurn(), makeOrb(), makeBlast(), makeAbsorb(), ...makeIcons()];
  const out = path.join(__dirname, "out");
  fs.rmSync(out, { recursive: true, force: true });
  fs.mkdirSync(out, { recursive: true });
  const manifest = [];
  for (const e of elements) {
    const dir = path.join(out, e.name);
    fs.mkdirSync(dir, { recursive: true });
    const n = e.layers[0].frames.length, flat = [];
    for (const l of e.layers) if (l.frames.length !== n) throw new Error(`${e.name}: layer ${l.name} has ${l.frames.length} frames, not ${n}`);
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
    png.encode(png.preview(flat, scale, [22, 18, 30], Math.min(flat.length, e.w <= 48 ? 8 : 6), 2), path.join(out, `sheet_${e.name}.png`));
  }
  fs.writeFileSync(path.join(out, "manifest.json"), JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(elements.map(m => `${m.group}/${m.name} ${m.w}x${m.h} x${m.layers[0].frames.length}`).join("\n"));
}

module.exports = { gourd, HOLY, P };
if (require.main === module) build();
