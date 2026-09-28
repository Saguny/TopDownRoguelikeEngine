// Yama, King of the Underworld: the Courtyard's final boss, and everything his fight is drawn with.
// same kit and style as the Magistrate (../boss) and the weapons (../weapons, ../weapons2): flat
// shapes shaded by ../restyle.js, with the fire, the eyes and the ink laid on after as light.
// written straight to Resources/Yama as PNG strips of square frames, which the game slices at run
// time (YamaArt), so there's nothing to set up in Unity:
//   node yama.js
//   yama          the King, 112x112, 14 frames: idle 0-3, cast 4-6 (raise, open, strike), and the
//                 same torn open with rage below a fifth of his health, 7-13. he faces the player:
//                 the flat crown with its bead strings, a dark crimson face, a black beard, the
//                 judge's robes with a jade belt, the brush of judgement in his right hand and the
//                 Ledger of Life and Death in his left, and hellfire where his robes should end
//   portrait      the spell card cut-in, 112x112: his head and shoulders, twice the size
//   halo          the flame aureole behind him, 128x128, 6 frames
//   bullets       the danmaku, a 6x6 atlas of 32x32 cells: rows orb, rice, talisman, coin, flame,
//                 big orb; columns red, gold, violet, azure, jade, bone
//   gate          the gate of hell opening under him, 160x160, 12 frames
//   wheel         the spell card backdrop turning behind him, 192x192: the wheel of the six realms
//   charge        a spell being gathered, 64x64, 6 frames
//   cancel        a bullet cancelled, 16x16, 5 frames
//   graze         a bullet grazed, 12x12, 4 frames
//   blast         one of the blasts going off in him as he dies, 64x64, 8 frames
//   death         his end, 192x192, 10 frames
//   bar           the boss bar's frame, 256x16
// out/sheet_*.png are previews
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const K2 = require("../weapons2/weapons2");
const { restyle } = require("../restyle");
const { hex, put, bayer } = D;
const { TAU, easeOut, lerp, img, fbm, vnoise, hash2, withOutline, ringField, breakup, twinkle, FIRE, GOLD } = W;
const P = Object.assign({}, K2.P, {
  F0: hex("#4a1418"), F1: hex("#7a2622"), F2: hex("#a8402e"), F3: hex("#d0654a"),   // his face
  H0: hex("#12040c"), H1: hex("#2a0818"),                                          // the pit
});
const DEST = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "Resources", "Yama");
const HELL = [[0.08, P.V0], [0.16, P.R0], [0.28, P.R1], [0.42, P.R2], [0.58, P.O1], [0.72, P.O2], [0.86, P.G3], [0.95, P.W]];
const RAGE = [[0.08, P.R0], [0.2, P.R1], [0.36, P.R2], [0.55, P.R3], [0.75, P.G3], [0.92, P.W]];
const VIOLET = [[0.1, P.V1], [0.22, P.V2], [0.36, P.V3], [0.52, P.V4], [0.7, P.V5], [0.9, P.W]];
const M = (w, h) => D.mask(w, h);
const fill = (im, Mk, c) => D.paint(im, Mk, c);

// ================================================================ the King
const S = 112;

// his body, flat, in the colours restyle shades. f sways the beard and the beads; cast 0 idle,
// 1 raising the brush and opening the ledger, 2 striking with it
function body(f, cast) {
  const im = img(S, S);
  const sway = [0, 1, 0, -1][f];

  // the robe: broad shoulders, a waist, flaring to the hem
  fill(im, K2.poly(M(S, S), [[46, 35], [66, 35], [80, 39], [85, 45], [77, 62], [83, 90], [29, 90], [35, 62], [27, 45], [32, 39]]), P.R1);
  // folds down the skirt and the hem's gold band
  for (let y = 66; y <= 87; y++) { put(im, 41 - (y - 66) / 8, y, P.R0); put(im, 71 + (y - 66) / 8, y, P.R0); }
  for (let x = 30; x <= 82; x++) { put(im, x, 88, P.G1); put(im, x, 89, P.G2); }
  // the apron hanging in front, black with a gold border and a flame worked on it
  const apron = K2.poly(M(S, S), [[49, 65], [63, 65], [64, 87], [48, 87]]);
  fill(im, apron, P.S1);
  for (let y = 65; y <= 87; y++) { put(im, 48 + (y - 65) / 22, y, P.G1); put(im, 63 + (y - 65) / 22, y, P.G1); }
  for (let x = 48; x <= 64; x++) put(im, x, 86, P.G2);
  [[56, 72], [55, 73], [57, 73], [55, 74], [56, 74], [57, 74], [54, 75], [56, 75], [58, 75], [55, 76], [56, 76], [57, 76], [56, 77], [53, 79], [59, 79], [56, 80], [54, 82], [58, 82]]
    .forEach(([x, y]) => put(im, x, y, y < 77 ? P.G2 : P.G1));
  // the jade belt, stiff and wider than he is
  for (let x = 31; x <= 81; x++) for (let y = 61; y <= 64; y++) {
    const plaque = (x - 31) % 6 >= 1 && (x - 31) % 6 <= 4;
    put(im, x, y, y === 61 || y === 64 ? P.G1 : plaque ? (y === 62 ? P.J3 : P.J2) : P.J1);
  }
  // the cloud collar over his shoulders: black lobes edged in gold, a sun and a moon on it
  const collar = K2.poly(M(S, S), [[40, 36], [72, 36], [81, 42], [75, 49], [66, 46], [56, 51], [46, 46], [37, 49], [31, 42]]);
  fill(im, collar, P.S1);
  for (let y = 36; y <= 51; y++) for (let x = 30; x <= 82; x++) if (D.mget(collar, x, y) && !D.mget(collar, x, y + 1)) put(im, x, y, P.G2);
  for (const [cx, cy, c] of [[38, 43, P.G2], [74, 43, P.CR2]]) for (let y = -2; y <= 2; y++) for (let x = -2; x <= 2; x++) if (x * x + y * y <= 5) put(im, cx + x, cy + y, c);
  put(im, 73, 42, P.S1); put(im, 73, 43, P.S1);                      // the moon's crescent
  // the crossed collar under the beard
  for (let i = 0; i <= 8; i++) { put(im, 50 + i * 0.7, 36 + i, P.CR2); put(im, 62 - i * 0.7, 36 + i, P.CR2); }

  // ---- the sleeves, the brush and the ledger
  const sleeveL = cast === 0 ? [[28, 40], [37, 44], [41, 58], [39, 66], [31, 79], [17, 77], [19, 58], [23, 46]]
    : cast === 1 ? [[28, 40], [37, 45], [33, 34], [26, 22], [17, 24], [16, 36], [20, 52], [24, 46]]
    : [[28, 40], [37, 45], [34, 56], [26, 66], [16, 70], [14, 60], [20, 50], [24, 44]];
  const sL = K2.poly(M(S, S), sleeveL);
  fill(im, sL, P.R1);
  // its gold cuff and black lining
  const cuffL = cast === 0 ? [[17, 77], [31, 79]] : cast === 1 ? [[17, 24], [26, 22]] : [[16, 70], [26, 66]];
  const cm = D.line(M(S, S), cuffL[0][0], cuffL[0][1], cuffL[1][0], cuffL[1][1]);
  fill(im, D.dilate(cm, 1, false), P.G2);
  fill(im, cm, P.S1);

  const sleeveR = cast === 0 ? [[84, 40], [75, 44], [71, 58], [73, 66], [81, 79], [95, 77], [93, 58], [89, 46]]
    : [[84, 40], [75, 45], [70, 54], [72, 60], [86, 62], [96, 58], [93, 48], [89, 44]];
  const sR = K2.poly(M(S, S), sleeveR);
  fill(im, sR, P.R1);
  const cuffR = cast === 0 ? [[81, 79], [95, 77]] : [[86, 62], [96, 58]];
  const cr = D.line(M(S, S), cuffR[0][0], cuffR[0][1], cuffR[1][0], cuffR[1][1]);
  fill(im, D.dilate(cr, 1, false), P.G2);
  fill(im, cr, P.S1);

  // the brush of judgement: dark wood, a gold ferrule, black bristles (the red ink is light, laid on after)
  const brush = cast === 0 ? [[33, 72], [33, 44], [33, 36]] : cast === 1 ? [[22, 26], [16, 12], [13, 5]] : [[26, 60], [14, 74], [9, 80]];
  const [b0, b1, b2] = brush;
  const handle = D.line(M(S, S), b0[0], b0[1], b1[0], b1[1]);
  fill(im, D.dilate(handle, 1, false), P.WD1);
  fill(im, handle, P.WD2);
  const fer = D.line(M(S, S), b1[0], b1[1], lerp(b1[0], b2[0], 0.3), lerp(b1[1], b2[1], 0.3));
  fill(im, D.dilate(fer, 1, false), P.G2);
  const bristle = D.line(M(S, S), lerp(b1[0], b2[0], 0.3), lerp(b1[1], b2[1], 0.3), b2[0], b2[1]);
  fill(im, D.dilate(bristle, 1, true), P.S1);
  // the hand round it
  const hand = cast === 0 ? [31, 60] : cast === 1 ? [19, 21] : [23, 61];
  for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) put(im, hand[0] + x, hand[1] + y, y === 3 ? P.F1 : P.F2);

  // the Ledger of Life and Death: shut at his side, or held open before him
  if (cast === 0) {
    for (let y = 54; y <= 70; y++) for (let x = 70; x <= 84; x++) {
      const rim = x === 70 || x === 84 || y === 54 || y === 70;
      put(im, x, y, x <= 71 ? P.G2 : rim ? P.G1 : P.A1);
    }
    for (let y = 57; y <= 67; y++) for (let x = 76; x <= 79; x++) put(im, x, y, P.CR2);
    [58, 60, 62, 64, 66].forEach(y => { put(im, 77, y, P.S1); put(im, 78, y, P.S1); });
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) put(im, 72 + x, 66 + y, y === 3 ? P.F1 : P.F2);
  } else {
    // open, two pages in a blue cover
    for (let y = 44; y <= 60; y++) for (let x = 62; x <= 92; x++) {
      const coverEdge = y === 44 || y === 60 || x === 62 || x === 92;
      put(im, x, y, coverEdge ? P.A1 : x === 77 ? P.G1 : P.CR2);
    }
    for (let y = 47; y <= 57; y += 2) for (let x = 65; x <= 89; x++) if (x !== 77 && x !== 76 && x !== 78 && hash2(x, y, 5) < 0.55) put(im, x, y, P.S2);
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) put(im, 74 + x, 59 + y, y === 3 ? P.F1 : P.F2);
  }

  // ---- the head
  const beardTip = 57 + (cast === 2 ? 1 : 0);
  const beard = K2.poly(M(S, S), [[47, 31], [65, 31], [67, 38], [63, 48], [58 + sway, beardTip - 2], [56 + sway, beardTip], [54 + sway, beardTip - 2], [49, 48], [45, 38]]);
  fill(im, beard, P.S2);
  for (let k = 0; k < 5; k++) {               // strands
    const x0 = 49 + k * 3.5, M2 = D.line(M(S, S), x0, 36, lerp(x0, 56 + sway, 0.6), 50 + (k % 2) * 3);
    fill(im, M2, k % 2 ? P.S1 : P.S3);
  }
  // the face: dark crimson, fierce
  fill(im, K2.poly(M(S, S), [[47, 19], [65, 19], [67, 24], [66, 31], [62, 35], [50, 35], [46, 31], [45, 24]]), P.F1);
  for (let y = 24; y <= 29; y++) { put(im, 44, y, P.F0); put(im, 45, y, P.F1); put(im, 67, y, P.F1); put(im, 68, y, P.F0); }
  for (let y = 21; y <= 23; y++) for (let x = 47; x <= 65; x++) if (hash2(x, y, 3) < 0.3) put(im, x, y, P.F2);   // the brow's bulk
  // brows sweeping up and out, the eye sockets under them, the nose
  for (let i = 0; i <= 6; i++) { put(im, 54 - i, 24 - i * 0.4, P.S1); put(im, 54 - i, 25 - i * 0.4, P.S1); put(im, 58 + i, 24 - i * 0.4, P.S1); put(im, 58 + i, 25 - i * 0.4, P.S1); }
  put(im, 47, 21, P.S1); put(im, 65, 21, P.S1);
  for (let x = 48; x <= 54; x++) put(im, x, 27, P.F0);
  for (let x = 58; x <= 64; x++) put(im, x, 27, P.F0);
  for (let x = 49; x <= 53; x++) put(im, x, 26, P.F0);          // the eyes: dark here, lit after
  for (let x = 59; x <= 63; x++) put(im, x, 26, P.F0);
  put(im, 56, 27, P.F2); put(im, 56, 28, P.F3); put(im, 56, 29, P.F2); put(im, 55, 30, P.F0); put(im, 57, 30, P.F0);
  put(im, 50, 30, P.F2); put(im, 62, 30, P.F2);                 // cheekbones
  // the moustache sweeping down past the beard
  for (let i = 0; i <= 7; i++) {
    put(im, 54 - i * 0.9, 31 + i * 0.7, P.S1); put(im, 54 - i * 0.9, 32 + i * 0.7, P.S1);
    put(im, 58 + i * 0.9, 31 + i * 0.7, P.S1); put(im, 58 + i * 0.9, 32 + i * 0.7, P.S1);
  }
  for (let x = 54; x <= 58; x++) put(im, x, 31, P.S1);

  // ---- the crown: a black cap with 王 in gold, the flat board over it, beads hanging from it
  fill(im, K2.poly(M(S, S), [[46, 10], [66, 10], [67, 20], [45, 20]]), P.S1);
  for (let x = 45; x <= 67; x++) { put(im, x, 18, P.G1); put(im, x, 19, P.G2); }
  for (let y = 11; y <= 17; y++) for (let x = 52; x <= 60; x++) put(im, x, y, x === 52 || x === 60 || y === 11 || y === 17 ? P.G1 : P.G2);
  for (let x = 54; x <= 58; x++) { put(im, x, 12, P.R1); put(im, x, 14, P.R1); put(im, x, 16, P.R1); }
  put(im, 56, 13, P.R1); put(im, 56, 15, P.R1);
  for (let x = 31; x <= 81; x++) { put(im, x, 6, P.G2); put(im, x, 7, P.S1); put(im, x, 8, x % 4 ? P.S1 : P.R1); put(im, x, 9, P.G1); }
  put(im, 30, 7, P.G1); put(im, 82, 7, P.G1); put(im, 30, 8, P.G1); put(im, 82, 8, P.G1);
  const beads = [P.R2, P.G2, P.J3, P.G2];
  for (const sx of [33, 37, 41, 71, 75, 79]) for (let k = 0; k < 5; k++) {
    const y = 11 + k * 3, x = sx + Math.round((k / 4) * (k >= 2 ? sway : 0));
    put(im, x, y - 1, P.S2);                                  // the string
    for (let v = 0; v < 2; v++) for (let u = 0; u < 2; u++) put(im, x + u, y + v, beads[(k + sx) % 4]);
  }
  return withOutline(im, P.S0);
}

// the light laid over him after shading: eyes, the red ink on the brush, the ledger's glow, and the
// hellfire his robes end in
function light(im, f, cast, rage) {
  const eye = rage ? [P.R3, P.W] : [P.G3, P.W];
  for (let x = 49; x <= 53; x++) put(im, x, 26, x === 51 ? eye[1] : eye[0]);
  for (let x = 59; x <= 63; x++) put(im, x, 26, x === 61 ? eye[1] : eye[0]);
  if (rage) { put(im, 48, 26, P.R2); put(im, 64, 26, P.R2); if (f % 2) { put(im, 51, 25, P.R3); put(im, 61, 25, P.R3); } }

  // the ink on the brush's tip
  const tip = cast === 0 ? [33, 36] : cast === 1 ? [13, 5] : [9, 80];
  const drop = cast === 0 ? [0, -1] : cast === 1 ? [-0.5, -1] : [-0.6, 0.8];
  for (let i = 0; i < 3; i++) put(im, tip[0] + drop[0] * i, tip[1] + drop[1] * i, i === 0 ? P.R3 : P.R2);
  if (cast === 2) {
    // the stroke: a red crescent of ink swept from above his head down past his side
    const F = D.field(S, S);
    D.each(F, (x, y) => {
      // kept inside the frame: its leftmost reach is a few pixels in from the edge
      const d = Math.hypot(x - 40, y - 46), a = Math.atan2(y - 46, x - 40);
      if (a < 1.25 || a > 3.25 || Math.abs(d - 30) > 3.5) return 0;
      return 0.9 - Math.abs(d - 30) * 0.18 - (3.25 - a) * 0.1;
    });
    D.shade(im, F, RAGE);
  }
  // the ledger's pages alight, glyphs rising off them
  if (cast >= 1) {
    const hot = cast === 2 ? P.W : P.G3;
    for (let y = 46; y <= 58; y++) for (let x = 64; x <= 90; x++) if (x !== 77 && D.get(im, x, y) && bayer(x + f, y) < (cast === 2 ? 0.35 : 0.18)) put(im, x, y, hot);
    const r = D.rng(90 + f + cast * 7);
    for (let k = 0; k < 6; k++) {
      const x = 66 + r() * 24, y = 42 - r() * (cast === 2 ? 26 : 16);
      twinkle(im, x, y, k % 3 === 0 ? 1 : 0, [P.W, P.G2]);
    }
    if (cast === 2) for (const [x0, y0] of [[94, 36], [98, 26], [90, 22]]) for (let y = 0; y < 4; y++) for (let x = 0; x < 3; x++) put(im, x0 + x + y * 0.5, y0 + y, x === 1 ? P.S2 : P.CR2);
  }

  // hellfire where his robes end: his robes burn away into tongues of it trailing under him,
  // each tongue its own length, swaying, dark red at the tips, taller in his rage
  const tall = rage ? 20 : 15, base = 89, ramp = rage ? RAGE : HELL;
  const pick = v => { let c = null; for (const [th, col] of ramp) if (v >= th) c = col; return c; };
  for (let x = 28; x <= 84; x++) {
    const edgeFade = Math.min(1, Math.min(x - 28, 84 - x) / 7 + 0.25);
    const tongue = Math.pow(Math.abs(Math.sin((x + f * 1.5) * 0.42 + Math.sin(x * 0.17) * 1.3)), 0.7);
    const h = tall * edgeFade * (0.3 + 0.7 * tongue) * (0.8 + 0.4 * fbm(x / 4, f * 0.7, 31));
    for (let y = base; y <= base + h; y++) {
      const k = (y - base) / Math.max(1, h);             // 0 at the hem, 1 at the tip
      const wob = Math.round(Math.sin(y * 0.45 + f * 1.6 + x * 0.2) * k * 1.5);
      const v = (rage ? 0.86 : 0.74) - k * 0.72 + (bayer(x, y) - 0.5) * 0.12;
      const c = pick(v);
      if (!c) continue;
      put(im, x + wob, y, c);
    }
  }
  // the hem catching: a line of heat along it, and licks climbing up over the robe
  for (let x = 29; x <= 83; x++) {
    put(im, x, base - 1, bayer(x, f) < 0.5 ? (rage ? P.R3 : P.O1) : P.R2);
    const lick = Math.round(3 * Math.max(0, Math.sin(x * 0.9 + f * 2.1)));
    for (let y = base - 1 - lick; y < base - 1; y++) put(im, x, y, rage ? P.R3 : P.R2);
  }
  // embers drifting up off it
  const r = D.rng(300 + f + (rage ? 50 : 0));
  for (let k = 0; k < (rage ? 14 : 8); k++) {
    const x = 26 + r() * 60, y = 100 - ((r() * 30 + f * 6) % 34);
    put(im, x, y, r() < 0.5 ? P.O2 : P.G3);
  }
  return im;
}

function makeYama() {
  const plan = [];
  for (const rage of [false, true]) {
    for (let f = 0; f < 4; f++) plan.push({ f, cast: 0, rage });
    for (let c = 1; c <= 3; c++) plan.push({ f: c, cast: c === 3 ? 2 : 1, rage, open: c });
  }
  const flat = plan.map(p => body(p.f, p.cast));
  const lit = restyle(flat, {
    outline: "#17111d",
    ramps: [
      ["#3e0812", "#7e1426", "#d02838", "#ff6a5a"],             // robe
      ["#6e3e14", "#c88830", "#f8d068", "#fff0b0"],             // gold
      ["#2b2232", "#463a4b", "#6b5b6c"],                        // black lacquer and beard
      ["#155c44", "#26986a", "#54d898", "#aef5cc"],             // jade
      ["#0e1830", "#1d3a6b", "#2f6fb0"],                        // the ledger's cover
      ["#c9bcc0", "#ece4dc"],                                   // paper, the inner collar
      ["#4a2412", "#7e4220", "#b8703a"],                        // the brush's wood
      ["#4a1418", "#7a2622", "#a8402e", "#d0654a"],             // his face
    ],
    ao: 0, minArea: 16,
  });
  return plan.map((p, i) => light(lit[i], p.f, p.cast, p.rage));
}

// his head and shoulders at twice the size, for the spell card cut-in
function makePortrait(yama) {
  const src = yama[0], out = img(S, S);
  for (let y = 0; y < 56; y++) for (let x = 0; x < 56; x++) {
    const c = D.get(src, 28 + x, 2 + y);
    if (!c) continue;
    for (let v = 0; v < 2; v++) for (let u = 0; u < 2; u++) put(out, x * 2 + u, y * 2 + v, c);
  }
  return out;
}

// ================================================================ his aureole
// a tall ring of flame behind him, the way a wrathful king is painted: tongues climbing its edge
function makeHalo() {
  const Z = 128, c = 64, frames = [];
  for (let f = 0; f < 6; f++) {
    const F = D.field(Z, Z);
    D.each(F, (x, y) => {
      const dx = (x - c) / 42, dy = (y - c - 8) / 54, d = Math.hypot(dx, dy);
      const a = Math.atan2(dy, dx);
      const up = Math.max(0, -Math.sin(a));                       // the top burns tallest
      // tongues: a ragged outer edge rising and flickering frame to frame
      const tongues = Math.pow(Math.abs(Math.sin(a * 9 + f * 1.05 + Math.sin(a * 3 + f) * 0.8)), 2);
      const reach = 0.06 + (0.1 + 0.22 * up) * tongues * (0.6 + 0.4 * fbm(a * 4, f * 0.6, 71));
      if (d < 0.8 || d > 0.86 + reach) return 0;
      if (d < 0.86) return 0.75 + (d - 0.8) * 3;                  // the ring itself, hottest
      return 0.72 * (1 - (d - 0.86) / reach) + 0.12;
    });
    const im = img(Z, Z);
    for (let y = 0; y < Z; y++) for (let x = 0; x < Z; x++) {
      const d = Math.hypot((x - c) / 40, (y - c - 8) / 52);
      if (d < 0.8 && bayer(x, y) < 0.2 * (1 - d)) put(im, x, y, P.V0);
    }
    D.shade(im, F, HELL);
    // sparks flung off the top
    const r = D.rng(500 + f);
    for (let k = 0; k < 6; k++) { const a = -Math.PI / 2 + (r() - 0.5) * 2.2; put(im, c + Math.cos(a) * 42 * (1.1 + r() * 0.2), c + 8 + Math.sin(a) * 54 * (1.1 + r() * 0.2), r() < 0.5 ? P.G3 : P.O2); }
    frames.push(im);
  }
  return frames;
}

// ================================================================ the danmaku
// every bullet has a dark rim so it reads over any floor, a lit body, and a white-hot core
const COLOURS = [
  [P.R0, P.R2, P.R3, P.W],       // red
  [P.G0, P.G1, P.G2, P.W],       // gold
  [P.V1, P.V3, P.V5, P.W],       // violet
  [P.A1, P.A2, P.A4, P.W],       // azure
  [P.J1, P.J2, P.J4, P.W],       // jade
  [P.S2, P.CR1, P.CR2, P.W],     // bone
];
const CELL = 32;

function bullet(type, col) {
  const im = img(CELL, CELL), c = 15.5, [dk, md, lt, wh] = COLOURS[col];
  const dot = (x, y, v) => put(im, x, y, v > 0.8 ? wh : v > 0.55 ? lt : v > 0.25 ? md : dk);
  for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
    const dx = x + 0.5 - 16, dy = y + 0.5 - 16;
    let v = -1;
    if (type === 0) {               // orb: small and round
      const d = Math.hypot(dx, dy);
      if (d < 5) v = d < 1.8 ? 1 : d < 3 ? 0.7 : d < 4.1 ? 0.4 : 0.1;
    } else if (type === 1) {        // rice: a grain along its flight
      const d = Math.hypot(dx / 5.5, dy / 2.8);
      if (d < 1) v = d < 0.35 ? 1 : d < 0.62 ? 0.7 : d < 0.82 ? 0.4 : 0.1;
    } else if (type === 2) {        // talisman: a paper strip with a glyph on it
      if (Math.abs(dx) < 7 && Math.abs(dy) < 3.5) {
        const rim = Math.abs(dx) > 6 || Math.abs(dy) > 2.5;
        const glyph = Math.abs(dy) < 0.8 && Math.abs(dx) < 4.5 || (Math.abs(dx) < 0.8 && Math.abs(dy) < 2.5) || (Math.abs(dx - 3) < 0.8 && Math.abs(dy) < 1.8);
        v = rim ? 0.1 : glyph ? 0.4 : 0.9;
        if (!rim && !glyph && bayer(x, y) < 0.3) v = 0.7;
      }
    } else if (type === 3) {        // coin: cash with a square hole
      const d = Math.hypot(dx, dy);
      if (d < 5.5 && !(Math.abs(dx) < 1.2 && Math.abs(dy) < 1.2)) {
        const hole = Math.abs(dx) < 2.2 && Math.abs(dy) < 2.2;
        v = d > 4.6 ? 0.1 : hole ? 0.4 : dx + dy < -2 ? 1 : 0.7;
      }
    } else if (type === 4) {        // flame: a soul flame streaming back from its head
      const t = dx;                                        // +x is ahead
      const w = t > 0 ? Math.sqrt(Math.max(0, 1 - (t / 4.5) ** 2)) * 3.6 : 3.6 * Math.max(0, 1 + t / 10) + Math.sin(t * 1.3) * 0.6;
      if (Math.abs(dy) < w && t > -10 && t < 4.5) {
        const k = Math.abs(dy) / Math.max(0.5, w), back = Math.max(0, -t) / 10;
        v = (1 - k) * 0.9 + 0.25 - back * 0.7;
        if (Math.abs(dy) > w - 1) v = 0.1;
      }
    } else if (type === 5) {        // big orb: a white heart in a wide glow
      const d = Math.hypot(dx, dy);
      if (d < 12.5) v = d < 5 ? 1 : d < 7.5 ? 0.7 : d < 10.5 ? 0.4 : 0.1;
      if (d >= 10.5 && d < 12.5 && bayer(x, y) > 0.55) v = -1;
    }
    if (v >= 0) dot(x, y, v);
  }
  return im;
}

function makeBullets() {
  const atlas = img(CELL * 6, CELL * 6);
  for (let t = 0; t < 6; t++) for (let c = 0; c < 6; c++) D.blit(atlas, bullet(t, c), c * CELL, t * CELL);
  return atlas;
}

// ================================================================ the gate of hell
// cracks racing round in a circle, the ground inside falling away into a pit of red dark, fire
// lapping up round its rim
function makeGate() {
  const Z = 160, c = 80, N = 12, R = 58, frames = [];
  const r = D.rng(6606);
  const cracks = Array.from({ length: 14 }, (_, i) => ({ a: i * TAU / 14 + (r() - 0.5) * 0.3, len: 14 + r() * 26, seed: 700 + i }));
  const crackM = D.mask(Z, Z);
  for (const k of cracks) D.polyline(crackM, D.bolt(D.rng(k.seed), c + Math.cos(k.a) * (R - 6), c + Math.sin(k.a) * (R - 6) * 0.6, c + Math.cos(k.a) * (R + k.len), c + Math.sin(k.a) * (R + k.len) * 0.6, 5, 3));
  for (let f = 0; f < N; f++) {
    const im = img(Z, Z);
    const open = Math.min(1, f / 5);
    // the pit: an ellipse (the ground seen at a slant) of red dark, swirling
    const pr = R * easeOut(open);
    for (let y = 0; y < Z; y++) for (let x = 0; x < Z; x++) {
      const d = Math.hypot((x - c) / pr, (y - c) / (pr * 0.6));
      if (pr < 1 || d > 1) continue;
      const a = Math.atan2((y - c) / 0.6, x - c);
      const swirl = fbm(a * 2 + d * 4 - f * 0.5, d * 3, 55);
      put(im, x, y, d < 0.35 ? (swirl > 0.55 ? P.R1 : P.H1) : swirl > 0.62 ? P.R0 : swirl > 0.45 ? P.H1 : P.H0);
    }
    // the cracks, glowing as they split the ground
    if (f >= 1) {
      const reach = Math.min(1, f / 3);
      const Cm = D.mask(Z, Z);
      for (let i = 0; i < crackM.m.length; i++) if (crackM.m[i]) {
        const x = i % Z, y = (i / Z) | 0, d = Math.hypot((x - c) / R, (y - c) / (R * 0.6));
        if (d < 0.8 + reach * 0.9) Cm.m[i] = 1;
      }
      D.paint(im, D.minus(D.dilate(Cm, 1, false), Cm), f < 6 ? P.O1 : P.R1, f < 6 ? 0.8 : 0.5, f);
      D.paint(im, Cm, f < 4 ? P.G3 : f < 8 ? P.O2 : P.R2);
    }
    // fire round the rim
    if (f >= 2) {
      const F = D.field(Z, Z);
      D.each(F, (x, y) => {
        const dx = (x - c) / pr, dy = (y - c) / (pr * 0.6);
        const d = Math.hypot(dx, dy);
        if (pr < 1) return 0;
        const a = Math.atan2(dy, dx), above = y < c + Math.sin(a) * pr * 0.6 ? 1 : 0;
        const h = 0.12 + 0.2 * fbm(a * 4, f * 0.7, 77);
        // flames rise from the rim: up the screen (-y) from where the rim is
        const rimY = c + Math.sin(a) * pr * 0.6, rimX = c + Math.cos(a) * pr;
        const up = (rimY - y) / (pr * 0.6), side = Math.abs(x - rimX) / 3;
        if (up < -0.06 || up > h * 1.8 || side > 1.2) return 0;
        return (1 - up / (h * 1.8)) * 0.9 * (1 - side * 0.5) * (0.6 + 0.4 * above);
      });
      D.shade(im, F, HELL);
    }
    // sparks thrown up when it bursts open
    if (f >= 3 && f <= 8) {
      const t = (f - 3) / 5;
      for (let k = 0; k < 18; k++) {
        const a = k / 18 * TAU + hash2(k, 1, 3), d = R * (0.6 + t * 0.5) * (0.7 + hash2(k, 2, 3) * 0.4);
        const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d * 0.6 - t * 30 * hash2(k, 3, 3);
        if (bayer(Math.round(x), Math.round(y)) < 1.1 - t) twinkle(im, x, y, t < 0.5 ? 1 : 0, [P.W, P.O2]);
      }
    }
    frames.push(im);
  }
  return frames;
}

// ================================================================ the wheel of the six realms
// his spell cards turn this behind him: rings, six spokes, the realms' glyph marks and a ring of
// tallies round the rim. drawn in two colours the game tints and fades
function makeWheel() {
  const Z = 192, c = 96, im = img(Z, Z);
  const ring = (R, th, col, keep = 1, seed = 0) => {
    for (let y = 0; y < Z; y++) for (let x = 0; x < Z; x++) {
      const d = Math.hypot(x + 0.5 - c, y + 0.5 - c);
      if (Math.abs(d - R) > th / 2) continue;
      if (keep < 1 && hash2(Math.floor(Math.atan2(y - c, x - c) * R / 3), seed, 9) > keep) continue;
      put(im, x, y, col);
    }
  };
  ring(92, 2, P.V3); ring(88, 1, P.V2); ring(70, 2, P.R2); ring(66, 1, P.V2, 0.7, 3); ring(40, 2, P.R2); ring(20, 1, P.V3);
  // tallies round the rim
  for (let k = 0; k < 72; k++) {
    const a = k / 72 * TAU, len = k % 6 === 0 ? 7 : 3;
    for (let t = 0; t < len; t++) put(im, c + Math.cos(a) * (87 - t), c + Math.sin(a) * (87 - t), k % 6 === 0 ? P.V5 : P.V3);
  }
  // six spokes, and a realm's mark between each pair on the middle ring
  for (let k = 0; k < 6; k++) {
    const a = k / 6 * TAU;
    for (let t = 20; t <= 88; t++) { put(im, c + Math.cos(a) * t, c + Math.sin(a) * t, t > 66 && t < 70 ? P.R3 : P.V3); }
    const b = a + TAU / 12, mx = c + Math.cos(b) * 54, my = c + Math.sin(b) * 54;
    for (let y = -5; y <= 5; y++) for (let x = -5; x <= 5; x++) {
      const on = (Math.abs(x) === 5 || Math.abs(y) === 5) || (k % 3 === 0 ? (y === 0 || x === 0) : k % 3 === 1 ? (Math.abs(x) === Math.abs(y)) : (y === -2 || y === 2));
      if (on && Math.hypot(x, y) < 7.2) put(im, mx + x, my + y, Math.abs(x) === 5 || Math.abs(y) === 5 ? P.R2 : P.V5);
    }
  }
  // the hub: a small eight petalled mark
  for (let k = 0; k < 8; k++) { const a = k / 8 * TAU; for (let t = 4; t <= 14; t++) put(im, c + Math.cos(a) * t, c + Math.sin(a) * t, P.R3); }
  return im;
}

// ================================================================ small FX
function makeCharge() {
  const Z = 64, c = 32, frames = [];
  for (let f = 0; f < 6; f++) {
    const t = f / 5, im = img(Z, Z);
    const F = D.field(Z, Z);
    ringField(F, c, c, lerp(30, 6, easeOut(t)), lerp(2, 3, t), lerp(0.5, 1, t), breakup(30, f + 3, 0.6 + t * 0.4));
    D.shade(im, F, VIOLET);
    for (let k = 0; k < 12; k++) {
      const a = k / 12 * TAU + f * 0.3, d = lerp(30, 4, t) * (0.7 + hash2(k, 5, 1) * 0.5);
      const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d;
      put(im, x, y, P.W); put(im, x - Math.cos(a), y - Math.sin(a), k % 2 ? P.R3 : P.V5);
    }
    if (f >= 4) twinkle(im, c, c, 2, [P.W, P.V6, P.V4]);
    frames.push(im);
  }
  return frames;
}

function makeCancel() {
  const frames = [];
  for (let f = 0; f < 5; f++) {
    const im = img(16, 16), c = 7.5, s = [2, 2, 1, 1, 0][f];
    twinkle(im, c, c, s, f < 2 ? [P.W, P.G3, P.G2] : [P.G3, P.G2]);
    if (f >= 1 && f <= 3) for (let k = 0; k < 4; k++) {
      const a = k / 4 * TAU + 0.8, d = 2 + f * 1.5;
      put(im, c + Math.cos(a) * d, c + Math.sin(a) * d, f < 3 ? P.G3 : P.G1);
    }
    frames.push(im);
  }
  return frames;
}

function makeGraze() {
  const frames = [];
  for (let f = 0; f < 4; f++) {
    const im = img(12, 12), c = 5.5;
    for (let k = 0; k < 3; k++) {
      const a = k / 3 * TAU + f, d = 1 + f * 1.3;
      put(im, c + Math.cos(a) * d, c + Math.sin(a) * d, f < 2 ? P.W : P.CR1);
    }
    if (f === 0) twinkle(im, c, c, 1, [P.W, P.CR2]);
    frames.push(im);
  }
  return frames;
}

function makeBlast() {
  const Z = 64, c = 32, N = 8, frames = [];
  for (let f = 0; f < N; f++) {
    const im = img(Z, Z);
    if (f === 0) { frames.push(K2.flareFrame(Z, c, 20, FIRE, 8)); continue; }
    const t = (f - 1) / (N - 2);
    const F = D.field(Z, Z);
    const R = lerp(8, 22, easeOut(t));
    D.each(F, (x, y) => {
      const d = Math.hypot(x - c, y - c) / R;
      if (d > 1) return 0;
      const n = fbm(x / 5, y / 5 + f, 88);
      return Math.max(0, (1 - d * d) * (1.05 - t * 0.9) + (n - 0.5) * 0.5);
    });
    D.shade(im, F, FIRE);
    const G = D.field(Z, Z);
    ringField(G, c, c, R + 5, 2, lerp(0.8, 0.2, t), breakup(R + 5, f + 11, lerp(0.9, 0.4, t)));
    D.shade(im, G, RAGE);
    frames.push(im);
  }
  return frames;
}

function makeDeath() {
  const Z = 192, c = 96, N = 10, frames = [];
  for (let f = 0; f < N; f++) {
    const im = img(Z, Z), F = D.field(Z, Z);
    const t = f / (N - 1);
    if (f <= 2) {
      D.flare(F, c, c, [60, 80, 90][f], [7, 5, 4][f], [30, 40, 44][f], 2, 1, 0.3);
      for (let k = 0; k < 16; k++) D.ray(F, c, c, k / 16 * TAU + 0.1, 40 + (k % 4) * 14, 2, 0.8, 0.2);
      D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < [18, 26, 30][f] ? 1 : d < [26, 34, 38][f] ? 0.75 : 0; });
      D.shade(im, F, GOLD);
    } else {
      const R = lerp(34, 92, easeOut((f - 2) / (N - 3)));
      ringField(F, c, c, R, lerp(6, 2, t), lerp(1, 0.35, t), f > 4 ? breakup(R, f, lerp(0.85, 0.35, t)) : null);
      ringField(F, c, c, R * 0.72, 2, lerp(0.7, 0.2, t), breakup(R * 0.72, f + 40, 0.6));
      D.shade(im, F, GOLD);
      // the core, fading
      const G = D.field(Z, Z);
      D.each(G, (x, y) => { const d = Math.hypot(x - c, y - c); const r0 = lerp(26, 4, t); return d < r0 ? 1 - d / r0 * 0.5 : 0; });
      D.shade(im, G, [[0.3, P.G2], [0.6, P.G3], [0.85, P.W]]);
      for (let k = 0; k < 24; k++) {
        const a = k / 24 * TAU + hash2(k, 7, 7), d = (R + 8) * (0.6 + hash2(k, 8, 7) * 0.5);
        const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d;
        if (bayer(Math.round(x), Math.round(y)) < 1.1 - t) twinkle(im, x, y, t < 0.5 ? 1 : 0, [P.W, k % 3 ? P.G3 : P.R3]);
      }
    }
    frames.push(im);
  }
  return frames;
}

// the boss bar's frame: black lacquer with gold rails and red seal corners
function makeBar() {
  const w = 256, h = 16, im = img(w, h);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const corner = x < 10 || x >= w - 10;
    if (corner) {
      const cx = x < 10 ? x : w - 1 - x;
      if (cx < 1 || y < 1 || y > h - 2) { put(im, x, y, P.S0); continue; }
      put(im, x, y, cx === 1 || y === 1 || y === h - 2 ? P.G2 : (cx + y) % 4 === 0 ? P.R3 : P.R1);
      continue;
    }
    if (y === 0 || y === h - 1) put(im, x, y, P.S0);
    else if (y === 1 || y === h - 2) put(im, x, y, y === 1 ? P.G2 : P.G1);
    else if (y === 2 || y === h - 3) put(im, x, y, P.S1);
    // inside is left clear for the fill
  }
  for (const x0 of [3, w - 7]) for (let y = 5; y <= 10; y++) for (let x = x0; x < x0 + 4; x++) put(im, x, y, P.G2);
  for (const x0 of [4, w - 6]) { put(im, x0, 6, P.R2); put(im, x0 + 1, 6, P.R2); put(im, x0, 8, P.R2); put(im, x0 + 1, 8, P.R2); put(im, x0, 9, P.R2); }
  return im;
}

// ---------------------------------------------------------------- write
function strip(frames) {
  const w = frames[0].w, h = frames[0].h, out = img(w * frames.length, h);
  frames.forEach((fr, i) => D.blit(out, fr, i * w, 0));
  return out;
}

function build() {
  fs.mkdirSync(DEST, { recursive: true });
  const preview = path.join(__dirname, "out");
  fs.mkdirSync(preview, { recursive: true });
  const yama = makeYama();
  const items = {
    yama, portrait: [makePortrait(yama)], halo: makeHalo(), gate: makeGate(), wheel: [makeWheel()],
    charge: makeCharge(), cancel: makeCancel(), graze: makeGraze(), blast: makeBlast(), death: makeDeath(),
  };
  for (const [name, frames] of Object.entries(items)) {
    png.encode(strip(frames), path.join(DEST, name + ".png"));
    const z = frames[0].w <= 16 ? 10 : frames[0].w <= 64 ? 5 : frames[0].w <= 128 ? 3 : 2;
    png.encode(png.preview(frames, z, [40, 34, 44], Math.min(frames.length, 7), 2), path.join(preview, `sheet_${name}.png`));
    console.log(`${name} ${frames[0].w}x${frames[0].h} x${frames.length}`);
  }
  const atlas = makeBullets();
  png.encode(atlas, path.join(DEST, "bullets.png"));
  png.encode(png.preview([atlas], 4, [40, 34, 44], 1, 2), path.join(preview, "sheet_bullets.png"));
  const bar = makeBar();
  png.encode(bar, path.join(DEST, "bar.png"));
  png.encode(png.preview([bar], 4, [40, 34, 44], 1, 2), path.join(preview, "sheet_bar.png"));
  console.log("bullets 192x192 atlas, bar 256x16");
}
if (require.main === module) build();
