// Meng Po's world and what she does in it, at the world's pixel size, centred:
//   steam_halo      her old form's aureole: steam off the soup curling round her in a slow ring, 6
//   lotus_halo      her true form's: a ring of lotus petals turning in azure light, 6
//   river           the Wangchuan, the river of forgetting: a 64px tile the game repeats across
//                   the arena, dark water flowing, lotus lanterns riding it, 8 (it loops)
//   bridge          Naihe Bridge: a stone arch over the river, red-lacquered posts capped with
//                   lotus buds, lanterns hung from its ends, 1
//   mandala         a spell card's backdrop turning behind her: rings of lotus petals round the
//                   character 忘, forget, 1 (the game turns it)
//   mist_puff       fog rolling (her entrance, her glides), 7
//   ladle_splash    a ladleful of soup flung: a splash of violet, 7
//   bowl_shatter    a thrown bowl breaking: porcelain shards and soup, 8
//   forget_wave     the forgetting: a pale ring sweeping out with fragments of memory dissolving, 9
//   mp_charge       a card being gathered: violet motes drawn in to a point, 6
//   soup_blast      a phase broken: a burst of violet light and spray, 9
//   transform       her old form cracking off her: white light and lavender shards, petals, 12
//   death_bloom     her end: a great lotus of light opening, petals and mist, 12
//   petal           a lotus petal drifting (a rain of them), 4
//   current         the river's pull on the road: ripples streaming along it, 4
const K = require("./kit");
const { P, D, W, K2, put, bayer, img, TAU, lerp, blob, pick, glowAround } = K;
const { Q } = require("./mengpo");
const { ringField, breakup, easeOut } = W;

const SOUP = [[0.1, Q.SP0], [0.25, Q.SP1], [0.45, Q.SP2], [0.68, Q.SP3], [0.88, Q.SP4], [0.96, Q.W]];
const MIST = [[0.06, Q.LV1], [0.12, Q.LV2], [0.2, Q.LV3], [0.32, Q.LV4]];
const AZ = W.AZURE;
const LOTUSR = [[0.12, Q.LO0], [0.3, Q.LO1], [0.55, Q.LO2], [0.8, Q.LO3], [0.95, Q.W]];
const burst = require("./fx").burst;
function shaded(S, ramp, fill, opts) { const F = D.field(S, S); fill(F); return D.shade(img(S, S), F, ramp, opts); }

// ---------------------------------------------------------------- halos
function steamHalo() {
  const S = 176, c = 88, out = [];
  for (let f = 0; f < 6; f++) {
    const t = f / 6;
    out.push(shaded(S, MIST, F => {
      // wisps curling round her in a slow ring, thickest behind her shoulders
      // a continuous ring of steam, broken by noise, thickest behind her shoulders, curling
      D.each(F, (x, y) => {
        const dx = x - c, dy = (y - c + 6) / 0.85, d = Math.hypot(dx, dy), a = Math.atan2(dy, dx);
        const n = W.fbm(Math.cos(a) * 2 + t * 3, Math.sin(a) * 2 + d / 14 - t * 2, 7);
        const R = 58 + (n - 0.5) * 18, band = 10 + n * 8;
        if (Math.abs(d - R) > band) return 0;
        return (0.34 - Math.max(0, Math.sin(a)) * 0.14) * (1 - Math.abs(d - R) / band) * (n > 0.35 ? 1 : 0.4);
      });
    }, { bands: false, fadeBand: 0.8 }));
  }
  return out;
}

function lotusHalo() {
  const S = 176, c = 88, out = [];
  for (let f = 0; f < 6; f++) {
    const t = f / 6, im = img(S, S);
    D.over(im, shaded(S, AZ, F => { ringField(F, c, c, 62, 3, 0.75, a => Math.sin(a * 12 + t * TAU) > -0.3); ringField(F, c, c, 54, 1.4, 0.45, null); }, { fadeBand: 0.8 }));
    // petals round the ring, turning
    for (let k = 0; k < 12; k++) {
      const a = k / 12 * TAU + t * TAU / 12, R0 = 62, len = 20;
      for (let i = 0; i <= len; i++) {
        const w = Math.sin(i / len * Math.PI) * 4.5;
        for (let j = -w; j <= w; j++) {
          const x = c + Math.cos(a) * (R0 + i) - Math.sin(a) * j, y = c + Math.sin(a) * (R0 + i) + Math.cos(a) * j;
          put(im, x, y, i > len * 0.7 ? Q.LO3 : Math.abs(j) > w - 1 ? Q.LO1 : Q.LO2);
        }
      }
    }
    out.push(im);
  }
  return out;
}

// ---------------------------------------------------------------- the river and the bridge
function river() {
  const S = 64, out = [];
  for (let f = 0; f < 8; f++) {
    const im = img(S, S), sh = f * 8;
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      // dark water, streaks of light flowing along it (they wrap every 64px, so the tile repeats)
      const u = ((x - sh) % S + S) % S;
      const n = W.loopNoise ? W.fbm(u / 9, y / 5, 4) : 0.5;
      const streak = Math.sin((u / S) * TAU * 2 + y * 0.35) * 0.5 + 0.5;
      const v = 0.35 * n + 0.4 * Math.pow(streak, 6) * (0.5 + 0.5 * Math.sin(y * 0.5));
      put(im, x, y, v > 0.5 ? Q.SP3 : v > 0.38 ? Q.SP2 : v > 0.26 ? Q.A1 : bayer(x, y) < 0.5 ? Q.SP0 : Q.V0);
    }
    // a lotus lantern riding it, a gold light in its heart
    const lx = ((20 + sh) % S), ly = 30;
    for (let k = 0; k < 5; k++) {
      const a = Math.PI + k / 4 * Math.PI;
      for (let i = 1; i <= 4; i++) put(im, (lx + Math.cos(a) * i + S) % S, ly + Math.sin(a) * i * 0.6, i > 3 ? Q.LO3 : Q.LO2);
    }
    put(im, lx, ly - 1, Q.G3); put(im, lx, ly - 2, Q.W);
    for (let i = 1; i <= 3; i++) if (bayer(lx, ly + i) < 0.5) put(im, (lx + i + S) % S, ly + 2 + i, Q.G2);    // its reflection
    out.push(im);
  }
  return out;
}

function bridge() {
  const Wd = 288, Hh = 120, im = img(Wd, Hh), c = Wd / 2;
  const deck = x => 58 - 30 * Math.cos((x - c) / (Wd * 0.5) * Math.PI / 2);   // the arch's hump
  // the arch: stone, the opening under it dark
  for (let x = 12; x < Wd - 12; x++) {
    const top = Math.round(deck(x)), open = 70 + 44 * Math.pow(Math.abs((x - c) / (Wd * 0.34)), 2.2);
    for (let y = top; y < Hh - 2; y++) {
      if (y > open && Math.abs(x - c) < Wd * 0.34) { if (y < open + 3) put(im, x, y, Q.ST0); continue; }
      const brick = ((y >> 2) + (((x + (y >> 2) * 3) >> 3) & 1)) % 2;
      const n = W.fbm(x / 6, y / 6, 3);
      put(im, x, y, y === top ? Q.ST4 : y < top + 3 ? Q.ST3 : brick && n > 0.55 ? Q.ST1 : n > 0.62 ? Q.ST3 : Q.ST2);
    }
  }
  // the balustrade: red-lacquered posts capped with lotus buds, a rail between
  for (let x = 16; x < Wd - 16; x++) { const y = Math.round(deck(x)) - 8; put(im, x, y, Q.R2); put(im, x, y + 1, Q.R1); }
  for (let k = 0; k <= 12; k++) {
    const x = Math.round(lerp(18, Wd - 18, k / 12)), top = Math.round(deck(x));
    for (let y = top - 12; y < top; y++) { put(im, x, y, Q.R2); put(im, x + 1, y, Q.R1); }
    put(im, x, top - 13, Q.LO2); put(im, x + 1, top - 13, Q.LO1); put(im, x, top - 14, Q.LO3); put(im, x + 1, top - 14, Q.LO2);
  }
  // lanterns hung at its ends
  for (const x of [22, Wd - 24]) {
    const top = Math.round(deck(x)) - 12;
    for (let y = top; y < top + 4; y++) put(im, x, y, Q.S2);
    for (let y = 0; y < 8; y++) for (let dx = -3; dx <= 3; dx++) if (Math.abs(dx) < 3 + (y > 1 && y < 6 ? 1 : 0)) put(im, x + dx, top + 4 + y, y === 0 || y === 7 ? Q.G1 : Math.abs(dx) < 2 ? Q.R3 : Q.R2);
  }
  return [glowAround(im, 1, Q.V1, Q.V0)];
}

function mandala() {
  const S = 224, c = 112, im = img(S, S);
  D.over(im, shaded(S, [[0.1, Q.SP0], [0.22, Q.SP1], [0.38, Q.SP2], [0.6, Q.SP3]], F => {
    for (const [R, th, pk] of [[104, 2, 0.5], [92, 1.2, 0.35], [60, 2, 0.45], [40, 1.2, 0.3]]) ringField(F, c, c, R, th, pk, null);
  }));
  // rings of lotus petals
  for (const [R0, n, len, col] of [[62, 16, 26, Q.SP2], [42, 10, 16, Q.SP3]]) for (let k = 0; k < n; k++) {
    const a = k / n * TAU;
    for (let i = 0; i <= len; i++) {
      const w = Math.sin(i / len * Math.PI) * (len * 0.22);
      for (let j = -w; j <= w; j += 0.5) if (Math.abs(Math.abs(j) - w) < 0.8 || i === len) put(im, c + Math.cos(a) * (R0 + i) - Math.sin(a) * j, c + Math.sin(a) * (R0 + i) + Math.cos(a) * j, col);
    }
  }
  // 忘 in the middle, and memories round the outer ring: small glyph-like marks
  const WANG = ["...X...", "XXXXXXX", ".X.....", ".XXXXX.", ".......", ".X.X..X", "X..X.X.", "X...XX.", ".XXX..X"];
  WANG.forEach((row, j) => [...row].forEach((ch, i) => { if (ch === "X") for (let v = 0; v < 3; v++) for (let u = 0; u < 3; u++) put(im, c - 10 + i * 3 + u, c - 13 + j * 3 + v, Q.SP4); }));
  const r = D.rng(5);
  for (let k = 0; k < 24; k++) {
    const a = k / 24 * TAU, x = c + Math.cos(a) * 98, y = c + Math.sin(a) * 98;
    for (let q = 0; q < 5; q++) put(im, x + Math.round(r() * 4 - 2), y + Math.round(r() * 4 - 2), Q.SP3);
  }
  return [im];
}

// ---------------------------------------------------------------- effects
function mistPuff() {
  const S = 96, c = 48, out = [];
  for (let f = 0; f < 7; f++) {
    const t = f / 6;
    out.push(shaded(S, MIST, F => {
      const r = D.rng(3);
      for (let k = 0; k < 6; k++) {
        const x = c + (r() - 0.5) * 20 * (1 + t), y = c + (r() - 0.5) * 12 * (1 + t) - t * 6, rad = 7 + t * 10 + r() * 4;
        blob(F, x, y, rad, rad * 0.7, 0.3 * (1 - t) + 0.06, 0.4);
      }
    }, { bands: false, fadeBand: 0.8 }));
  }
  return out;
}

const ladleSplash = () => burst({ S: 64, N: 7, R: 10, ringR: 22, seed: 31, ramp: SOUP, ringRamp: SOUP, debris: 8,
  hot: [Q.W, Q.SP4, Q.SP3, Q.SP2], cool: [Q.SP2, Q.SP1], embers: 10, emberCols: [Q.W, Q.SP4, Q.SP3], flare: false, gravity: 8 });

function bowlShatter() {
  const S = 80, c = 40;
  const frames = burst({ S, N: 8, R: 11, ringR: 26, seed: 33, ramp: SOUP, ringRamp: SOUP, embers: 10, emberCols: [Q.W, Q.SP4, Q.SP3], flare: true });
  return frames.map((im, f) => {
    if (f === 0) return im;
    const t = f / 7, r = D.rng(34);
    for (let k = 0; k < 10; k++) {
      const a = r() * TAU, d = easeOut(t) * (10 + r() * 20);
      const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d + t * t * 10;
      if (bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.6) * 2.5) continue;
      // a porcelain shard: white, a blue rim line
      put(im, x, y, Q.W); put(im, x + 1, y, P.CR2); put(im, x, y + 1, Q.A3);
    }
    return im;
  });
}

function forgetWave() {
  const S = 256, c = 128, out = [];
  for (let f = 0; f < 9; f++) {
    const t = f / 8, R = 12 + easeOut(t) * 112;
    const im = shaded(S, [[0.1, Q.LV2], [0.25, Q.LV3], [0.45, Q.LV4], [0.7, Q.W]], F => {
      ringField(F, c, c, R, 6 * (1 - t) + 2, 0.9 * (1 - t) + 0.15, t > 0.4 ? breakup(R, f, 1.1 - t * 0.6) : null);
      if (f < 2) blob(F, c, c, 18, 18, 0.8, 0.2);
    });
    // fragments of memory dissolving off its edge: little glyphs breaking up
    const r = D.rng(50 + f);
    for (let k = 0; k < 44; k++) {
      const a = r() * TAU, d = R - 4 - r() * 26;
      const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d - t * 6;
      if (bayer(Math.round(x), Math.round(y)) < t * 0.9) continue;
      // a fragment of a glyph: a little stroke or a cross, drifting off as it goes
      const g = k % 3;
      for (let q = -1; q <= 1; q++) put(im, x + (g === 0 ? q : 0), y + (g === 1 ? q : 0), k % 4 ? Q.LV4 : Q.SP4);
      if (g === 2) { put(im, x - 1, y - 1, Q.LV4); put(im, x + 1, y + 1, Q.LV4); }
    }
    out.push(im);
  }
  return out;
}

function mpCharge() {
  const S = 96, c = 48, out = [];
  for (let f = 0; f < 6; f++) {
    const t = f / 5, im = img(S, S), r = D.rng(9);
    for (let k = 0; k < 36; k++) {
      const a = r() * TAU, d0 = 20 + r() * 26, d = d0 * (1 - easeOut(t));
      const x = c + Math.cos(a + t) * d, y = c + Math.sin(a + t) * d;
      put(im, x, y, k % 3 ? Q.SP3 : Q.W); put(im, x - Math.cos(a) * 2, y - Math.sin(a) * 2, Q.SP1);
    }
    D.over(im, shaded(S, SOUP, F => blob(F, c, c, 3 + t * 9, 3 + t * 9, 0.4 + t * 0.6, 0.2)));
    out.push(im);
  }
  return out;
}

const soupBlast = () => burst({ S: 128, N: 9, R: 22, ringR: 56, seed: 37, ramp: SOUP, ringRamp: SOUP, debris: 12,
  hot: [Q.W, Q.SP4, Q.SP3, Q.SP2], cool: [Q.LV3, Q.LV2], embers: 22, emberCols: [Q.W, Q.SP4, Q.SP3, Q.LO2], smoke: MIST });

function transform() {
  const S = 224, c = 112;
  const frames = burst({ S, N: 12, R: 34, ringR: 100, seed: 41, ramp: [[0.1, Q.LV2], [0.3, Q.LV4], [0.55, Q.A4], [0.8, Q.W]],
    ringRamp: AZ, embers: 30, emberCols: [Q.W, Q.A5, Q.LO2, Q.A3], smoke: MIST, flare: true });
  return frames.map((im, f) => {
    const t = f / 11, r = D.rng(42);
    // her old form's lavender robe, cracking off her in shards
    for (let k = 0; k < 26; k++) {
      const a = r() * TAU, d = easeOut(t) * (30 + r() * 70);
      const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d + t * t * 30;
      if (f === 0 || bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.6) * 2.4) continue;
      put(im, x, y, Q.LV3); put(im, x + 1, y, Q.LV2); put(im, x, y + 1, Q.LV1); put(im, x + 1, y + 1, Q.LV2);
    }
    // petals whirling out
    for (let k = 0; k < 18; k++) {
      const a = k / 18 * TAU + t * 3, d = 20 + easeOut(t) * 80;
      const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d;
      if (f < 2 || bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.7) * 3) continue;
      put(im, x, y, Q.LO2); put(im, x + 1, y, Q.LO3); put(im, x, y + 1, Q.LO1);
    }
    return im;
  });
}

function deathBloom() {
  const S = 256, c = 128, out = [];
  for (let f = 0; f < 12; f++) {
    const t = f / 11, im = img(S, S);
    // a great lotus of light opening
    const open = easeOut(Math.min(1, t * 1.6)), fade = Math.max(0, (t - 0.55) / 0.45);
    for (const [n, len, col, off] of [[8, 100, LOTUSR, 0], [8, 70, [[0.12, Q.A1], [0.3, Q.A2], [0.55, Q.A4], [0.85, Q.W]], 0.5]]) {
      const F = D.field(S, S);
      for (let k = 0; k < n; k++) {
        const a = (k + off) / n * TAU - Math.PI / 2, L = len * open;
        D.ray(F, c, c, a, L, L * 0.42, 0.95 - fade * 0.6, 0.35 - fade * 0.2);
      }
      D.over(im, D.shade(img(S, S), F, col));
    }
    if (f < 3) D.over(im, shaded(S, [[0.2, Q.LV4], [0.6, Q.W]], F => blob(F, c, c, 40 + f * 20, 40 + f * 20, 1, 0.3)));
    // petals and motes lifting away
    const r = D.rng(60);
    for (let k = 0; k < 50; k++) {
      const a = r() * TAU, d = 20 + easeOut(t) * (60 + r() * 60);
      const x = c + Math.cos(a) * d, y = c + Math.sin(a) * d - t * 30;
      if (bayer(Math.round(x), Math.round(y)) < Math.max(0, t - 0.6) * 2.4) continue;
      put(im, x, y, k % 4 === 0 ? Q.W : k % 4 === 1 ? Q.LO2 : k % 4 === 2 ? Q.A4 : Q.SP3);
    }
    out.push(im);
  }
  return out;
}

function petal() {
  const S = 12, out = [];
  for (let f = 0; f < 4; f++) {
    const im = img(S, S), a = f / 4 * Math.PI;
    for (let i = -3; i <= 3; i++) {
      const w = Math.round(Math.cos(i / 3.5 * Math.PI / 2) * 2 * Math.abs(Math.cos(a)) + 0.5);
      for (let j = -w; j <= w; j++) put(im, 6 + Math.cos(a) * i - Math.sin(a) * j * 0.6, 6 + Math.sin(a) * i + Math.cos(a) * j * 0.6, Math.abs(j) === w ? Q.LO1 : i > 1 ? Q.LO3 : Q.LO2);
    }
    out.push(im);
  }
  return out;
}

function current() {
  const S = 48, c = 24, out = [];
  for (let f = 0; f < 4; f++) {
    const im = img(S, S);
    for (let k = 0; k < 4; k++) {
      const y = 12 + k * 8, x0 = ((f * 6 + k * 13) % 36) + 2;
      for (let x = x0; x < x0 + 12; x++) if (bayer(x, y) < 0.9 - (x - x0) / 12 * 0.6) put(im, x, y + Math.round(Math.sin(x * 0.5) * 1), x > x0 + 8 ? Q.A4 : Q.A2);
    }
    out.push(im);
  }
  return out;
}

function makeMengPoFx() {
  return {
    steam_halo: steamHalo(), lotus_halo: lotusHalo(), river: river(), bridge: bridge(), mandala: mandala(),
    mist_puff: mistPuff(), ladle_splash: ladleSplash(), bowl_shatter: bowlShatter(), forget_wave: forgetWave(),
    mp_charge: mpCharge(), soup_blast: soupBlast(), transform: transform(), death_bloom: deathBloom(), petal: petal(), current: current(),
  };
}

module.exports = { makeMengPoFx };
