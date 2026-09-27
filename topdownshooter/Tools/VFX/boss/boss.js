// the Jiangshi Magistrate, the Final Rush boss: a giant hopping vampire in a Qing official's
// crimson robes, a talisman sealing its face, and what it throws: corpse-fire orbs and the
// shockwave of its slam. same kit and style as the weapons (weapons/weapons.js), same pixel size
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const { restyle } = require("../restyle");
const { hex, put } = D;
const { TAU, easeOut, img, fbm, vnoise, withOutline, ringField, breakup, twinkle, el } = W;

const P = Object.assign({}, W.P, {
  J0: hex("#3c5a48"), J1: hex("#6f9a78"), J2: hex("#a6c9a0"),     // corpse skin
  T1: hex("#1f6b5c"), T2: hex("#3fa38a"), T3: hex("#8ee0c0"),     // peacock plume
  N0: hex("#0f2a14"), N1: hex("#1e6b2a"), N2: hex("#3fbf3a"), N3: hex("#9dff6a"), // corpse fire
});
const CORPSE = [[0.1, P.N0], [0.25, P.N1], [0.45, P.N2], [0.7, P.N3], [0.9, P.W]];
const STONE = [[0.06, P.S1], [0.1, P.S2], [0.14, P.S3]];

// ---------------------------------------------------------------- the Magistrate, 72x96
// faces right. pose: hop (arms out, the jiangshi way) or cast (arms raised). torn: the seal on its
// face ripped half away and the eyes lit, below half health
const BW = 72, BH = 96;
function magistrate({ dy = 0, sq = 0, cast = false, torn = false, flick = 0 }) {
  const im = img(BW, BH);
  const P_ = (x, y, c) => put(im, x, y + dy, c);
  const up = sq;                              // a crouch lowers the upper body
  const U = (x, y, c) => P_(x, y + up, c);

  // boots with white soles, under the hem
  for (const bx of [27, 37]) for (let y = 88; y <= 91; y++) for (let x = bx; x <= bx + 5; x++)
    P_(x, y, y === 91 ? P.S3 : y === 88 ? P.S2 : P.S1);

  // the gown, widening to the hem
  for (let y = 44; y <= 87; y++) {
    const yy = y + (y < 62 ? up : Math.round(up * (87 - y) / 25));
    const w = 13 + (y - 44) * 0.14 + (y > 70 ? sq * 0.5 : 0);
    const l = Math.round(34 - w), r = Math.round(34 + w - 2);
    for (let x = l; x <= r; x++) {
      let c = x < l + 4 ? P.R0 : x > r - 3 ? P.R2 : P.R1;
      if (y > 64 && (x === 26 || x === 43) && y % 3 !== 0) c = P.R0;
      if (y >= 84) c = y === 84 ? P.G2 : y === 85 ? P.G1 : P.R0;
      if (y > 58 && y < 84 && (x === 34 || x === 35)) c = x === 34 ? P.G2 : P.G1;
      P_(x, yy, c);
    }
  }
  // belt with gold plaques
  for (let x = 21; x <= 47; x++) for (let y = 62; y <= 64; y++) U(x, y, [26, 30, 38, 42].includes(x) ? (y === 62 ? P.G3 : P.G2) : y === 62 ? P.S2 : P.S1);
  // the mandarin square: a gold badge with a red dragon coiled on it
  for (let y = 47; y <= 58; y++) for (let x = 28; x <= 40; x++) {
    const edge = x === 28 || x === 40 || y === 47 || y === 58;
    U(x, y, edge ? P.G1 : y === 48 ? P.G3 : P.G2);
  }
  [[33, 50], [34, 50], [35, 50], [36, 51], [36, 52], [35, 53], [34, 53], [33, 52], [32, 51], [31, 52], [31, 54], [32, 55], [34, 55], [36, 55], [37, 54], [38, 53], [30, 50]]
    .forEach(([x, y]) => U(x, y, P.R2));
  U(35, 51, P.R3);
  // collar and neck
  for (let x = 28; x <= 40; x++) { U(x, 43, P.G1); U(x, 44, P.G2); }
  for (let x = 31; x <= 37; x++) { U(x, 41, P.J0); U(x, 42, P.J0); }

  // the head
  for (let y = 25; y <= 40; y++) for (let x = 27; x <= 41; x++) {
    if ((y === 25 || y === 40) && (x <= 28 || x >= 40)) continue;
    U(x, y, x <= 29 ? P.J0 : x >= 39 && y > 30 ? P.J2 : P.J1);
  }
  const eye = torn ? (flick ? P.W : P.R3) : P.S0;
  U(30, 31, eye); U(31, 31, torn ? P.R3 : P.S0); U(30, 32, torn ? P.R2 : P.J0);
  for (let x = 34; x <= 38; x++) U(x, 38, P.S0);
  U(35, 39, P.W); U(38, 39, P.W);             // fangs

  // the seal: yellow talisman hanging over the face from the hat brim
  const glyph = [".#..", "####", ".#..", ".##.", "#..#", ".##.", "####", ".#.#", "#.#.", ".##.", "#..#", ".##."];
  const sealBottom = torn ? 29 : 38;
  for (let y = 21; y <= sealBottom; y++) for (let x = 33; x <= 40; x++) {
    if (torn && y >= 27 && ((x + y) % 3 === 0 || y === sealBottom && x % 2)) continue;
    U(x, y, x === 40 ? P.G2 : P.G3);
  }
  glyph.forEach((row, i) => [...row].forEach((ch, j) => {
    const y = 24 + i;
    if (ch !== "#" || y > sealBottom - 1) return;
    U(35 + j, y, cast || torn ? (flick ? P.W : P.O2) : (i % 4 === 0 ? P.R2 : P.R1));
  }));
  if (torn) { U(39, 31, P.G3); U(40, 32, P.G2); U(38, 33, P.G3); }   // a shred still hanging

  // the hat: black brim, red-fringed dome, gold finial, a peacock plume sweeping back
  const plume = [[31, 8], [27, 7], [23, 8], [19, 10], [16, 13], [14, 16], [13, 19 + flick]];
  for (let i = 1; i < plume.length; i++) {
    const M = D.line(D.mask(BW, BH), plume[i - 1][0], plume[i - 1][1] + up + dy, plume[i][0], plume[i][1] + up + dy);
    D.paint(im, D.minus(D.dilate(M, 1, false), M), P.T1);
    D.paint(im, M, P.T2);
  }
  const [ex, ey] = plume[plume.length - 1];
  [[0, 0, P.A2], [1, 0, P.G2], [-1, 0, P.G2], [0, 1, P.G2], [0, -1, P.T3]].forEach(([u, v, c]) => U(ex + u, ey + v, c));
  for (let y = 9; y <= 19; y++) for (let x = 26; x <= 42; x++) {
    const d = Math.hypot((x - 34) / 8.5, (y - 19) / 10.5);
    if (d > 1) continue;
    U(x, y, x < 29 ? P.R1 : x > 38 && y < 14 ? P.R3 : x % 2 ? P.R2 : P.R1);
  }
  for (let y = 19; y <= 23; y++) for (let x = 22; x <= 46; x++) {
    if ((y === 19 || y === 23) && (x < 24 || x > 44)) continue;
    U(x, y, y === 19 ? P.S2 : y === 23 ? P.S0 : P.S1);
  }
  for (let y = 3; y <= 8; y++) for (let x = 32; x <= 36; x++) {
    if ((y === 3 || y === 8) && (x === 32 || x === 36)) continue;
    U(x, y, x === 35 && y < 6 ? P.G3 : x === 32 ? P.G1 : P.G2);
  }
  U(34, 2, P.R3);

  // the arms
  // a sleeve: its centre line swollen to its width, lit along the top
  const sleeve = (pts, width, far) => {
    const C = D.polyline(D.mask(BW, BH), pts.map(([x, y]) => [x, y + up + dy]));
    const M = D.dilate(C, Math.floor(width / 2), true);
    D.paint(im, M, far ? P.R0 : P.R1);
    const top = D.mask(BW, BH);
    for (let i = BW; i < M.m.length; i++) if (M.m[i] && !M.m[i - BW]) top.m[i] = 1;
    D.paint(im, top, far ? P.R1 : P.R2);
  };
  const hand = (x, y, dirX, dirY, far) => {
    for (let v = 0; v < 3; v++) for (let u = 0; u < 4; u++) U(x + u * dirX + v * (dirY ? 1 : 0), y + v * (dirY ? 0 : 1) + u * dirY, far ? P.J0 : v === 2 ? P.J0 : P.J1);
    for (let k = 0; k < 2; k++) for (let t = 1; t <= 3; t++)   // long nails
      U(x + (3 + t) * dirX + (dirY ? k * 2 : 0), y + (dirY ? 0 : k * 2) + (3 + t) * dirY, t === 3 ? P.S3 : P.W);
  };
  if (!cast) {
    sleeve([[42, 44], [55, 44]], 5, true); hand(56, 42, 1, 0, true);
    sleeve([[40, 48], [57, 48]], 7, false);
    for (let y = 45; y <= 51; y++) U(57, y, P.G2);
    hand(58, 46 + (flick ? 1 : 0), 1, 0, false);
  } else {
    sleeve([[28, 46], [20, 30], [18, 20]], 6, true); hand(17, 16, 0, -1, true);
    sleeve([[40, 46], [48, 30], [50, 20]], 7, false);
    for (let x = 47; x <= 53; x++) U(x, 17, P.G2);
    hand(49 + flick, 16, 0, -1, false);
  }
  return withOutline(im, P.S0);
}

function makeBoss() {
  // hop: stand, crouch, rise, peak, fall, land. then two frames of casting. then all of it torn
  const hop = [{ dy: 0 }, { sq: 2 }, { dy: -3 }, { dy: -6 }, { dy: -3, flick: 1 }, { sq: 1 }];
  const frames = [];
  for (const torn of [false, true]) {
    hop.forEach((h, i) => frames.push(magistrate(Object.assign({ torn, flick: i % 2 }, h))));
    frames.push(magistrate({ cast: true, torn, flick: 0 }), magistrate({ cast: true, torn, flick: 1, dy: -1 }));
  }
  // lit and rimmed like the enemies it leads (see ../restyle.js)
  const lit = restyle(frames, {
    outline: "#17111d",
    ramps: [["#3e0812", "#7e1426", "#d02838", "#ff6a5a"], ["#6e3e14", "#c88830", "#f8d068", "#fff0b0"],
      ["#3c5a48", "#6f9a78", "#a6c9a0"], ["#2b2232", "#463a4b", "#6b5b6c"], ["#1f6b5c", "#3fa38a", "#8ee0c0"]],
    glow: ["#ffa040", "#ffffff"], ao: 4, minArea: 20,
  });
  return el("boss_magistrate", "Boss", BW, BH, [{ name: "magistrate", frames: lit }], Array(16).fill(100),
    [["hop", 0, 5], ["cast", 6, 7], ["torn_hop", 8, 13], ["torn_cast", 14, 15]]);
}

function makeShadow() {
  const im = img(48, 14);
  for (let y = 0; y < 14; y++) for (let x = 0; x < 48; x++) {
    const d = Math.hypot((x + 0.5 - 24) / 21, (y + 0.5 - 7) / 5.5);
    if (d < 0.75) put(im, x, y, P.S0);
    else if (d < 1 && D.bayer(x, y) < 0.55) put(im, x, y, P.S0);
  }
  return el("boss_shadow", "Boss", 48, 14, [{ name: "shadow", frames: [im] }], [100], []);
}

// corpse fire: what the Magistrate shoots. green, so it can't be mistaken for anything of the
// player's (gold, azure, violet) or for blood
function makeOrb() {
  const S = 12, c = 5.5, frames = [];
  const licks = [[[6, 0], [5, 1]], [[7, 0], [7, 1]], [[5, 0], [6, 1]], [[4, 1], [4, 0]]];
  for (let f = 0; f < 4; f++) {
    const im = img(S, S);
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const d = Math.hypot(x - c, y - c + 0.5);
      if (d < 1.3) put(im, x, y, P.W);
      else if (d < 2.6) put(im, x, y, P.N3);
      else if (d < 4) put(im, x, y, f % 2 && d > 3.5 ? P.N1 : P.N2);
    }
    licks[f].forEach(([x, y], i) => put(im, x, y + 1, i ? P.N2 : P.N3));
    frames.push(withOutline(im, P.N0));
  }
  return el("boss_orb", "Boss", S, S, [{ name: "orb", frames }], Array(4).fill(80), [["burn", 0, 3]]);
}

// the slam: a flash, a corpse-fire ring out to 57px (2 units, what the code scales from), the
// ground cracking and dust rolling out
function makeSlam() {
  const S = 160, c = 80, N = 10, r = D.rng(7171);
  const cracks = Array.from({ length: 8 }, (_, i) => ({ a: i * TAU / 8 + (r() - 0.5) * 0.5, reach: 26 + r() * 20, seed: 900 + i }));
  const rocks = Array.from({ length: 10 }, () => ({ a: r() * TAU, s: 0.5 + r() * 0.6, big: r() < 0.5 }));
  const puffs = Array.from({ length: 14 }, (_, i) => ({ a: i * TAU / 14 + (r() - 0.5) * 0.3, s: 0.8 + r() * 0.3, size: 0.6 + r() * 0.6 }));
  const crackM = D.mask(S, S);
  for (const k of cracks) D.polyline(crackM, D.bolt(D.rng(k.seed), c + Math.cos(k.a) * 6, c + Math.sin(k.a) * 6, c + Math.cos(k.a) * k.reach, c + Math.sin(k.a) * k.reach, 6, 3));
  const ringL = [], crackL = [], dustL = [], rockL = [];
  for (let f = 0; f < N; f++) {
    const F = D.field(S, S);
    if (f === 0) {
      D.each(F, (x, y) => { const d = Math.hypot(x - c, y - c); return d < 16 ? 1 : d < 24 ? 0.8 - (d - 16) / 8 * 0.3 : 0; });
      D.flare(F, c, c, 44, 5, 22, 3, 0.95, 0.4);
    }
    if (f >= 1 && f <= 6) {
      const R = [0, 30, 42, 50, 55, 57, 59][f], th = [0, 7, 5, 4, 3, 2, 2][f], pk = [0, 1, 0.88, 0.72, 0.56, 0.42, 0.3][f];
      ringField(F, c, c, R, th, pk, f >= 3 ? breakup(R, f + 20, [0, 0, 0, 0.85, 0.7, 0.55, 0.4][f]) : null);
    }
    ringL.push(D.shade(img(S, S), F, CORPSE));

    const Cr = img(S, S);
    if (f >= 1) {
      const glow = [null, P.N3, P.N2, P.N2, P.N1, P.N1, null, null, null, null][f];
      if (glow) D.paint(Cr, D.minus(D.dilate(crackM, 1, false), crackM), glow, 0.7, f);
      D.paint(Cr, crackM, f <= 2 ? P.N2 : P.S0);
    }
    crackL.push(Cr);

    const Du = D.field(S, S);
    if (f >= 2) {
      const i = f - 2, pr = 40 + i * 3, rad = 7 + i * 1.2, val = [0.14, 0.13, 0.12, 0.11, 0.1, 0.085, 0.072, 0.062][i];
      for (const p of puffs) {
        const px = c + Math.cos(p.a) * pr * p.s, py = c + Math.sin(p.a) * pr * p.s - i * 1.2, pr2 = rad * p.size;
        D.each(Du, (x, y) => { const d = Math.hypot(x - px, y - py), e = pr2 * (0.8 + 0.4 * vnoise(x / 2, y / 2, 4)); return d < e ? val * (1 - 0.45 * d / e) : 0; });
      }
    }
    dustL.push(D.shade(img(S, S), Du, STONE, { bands: false }));

    const Rk = img(S, S);
    if (f >= 1 && f <= 7) for (const k of rocks) {
      const t = f / 7, d = 14 + 50 * k.s * easeOut(t), x = Math.round(c + Math.cos(k.a) * d), y = Math.round(c + Math.sin(k.a) * d - Math.sin(t * Math.PI) * 10);
      const s = k.big ? 2 : 1;
      for (let v = 0; v < s; v++) for (let u = 0; u < s; u++) put(Rk, x + u, y + v, u + v === 0 ? P.S3 : P.S2);
    }
    rockL.push(Rk);
  }
  return el("boss_slam", "Boss", S, S, [{ name: "cracks", frames: crackL }, { name: "dust", frames: dustL }, { name: "ring", frames: ringL }, { name: "rocks", frames: rockL }],
    Array(N).fill(40), [["slam", 0, N - 1]], { radius: 57 });
}

function build() {
  const elements = [makeBoss(), makeShadow(), makeOrb(), makeSlam()];
  const out = path.join(__dirname, "out");
  fs.rmSync(out, { recursive: true, force: true });
  const manifest = [];
  for (const e of elements) {
    const dir = path.join(out, e.name);
    fs.mkdirSync(dir, { recursive: true });
    const n = e.layers[0].frames.length, flat = [];
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
    const scale = e.w <= 24 ? 8 : e.w <= 100 ? 4 : 2;
    png.encode(png.preview(flat, scale, [58, 50, 60], Math.min(flat.length, e.w <= 100 ? 8 : 5), 2), path.join(out, `sheet_${e.name}.png`));
  }
  fs.writeFileSync(path.join(out, "manifest.json"), JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(manifest.map(m => `${m.name} ${m.w}x${m.h} x${m.frames}`).join("\n"));
}

build();
