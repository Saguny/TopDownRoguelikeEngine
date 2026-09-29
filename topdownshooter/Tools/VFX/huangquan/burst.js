// the end bosses' phase bursts (PhaseBurst): what fills the screen as each phase begins, the way a
// Genshin Impact burst does. two sets, each in its boss's colours: yama_ (hellfire: blood red,
// ember and gold) and mengpo_ (the soup of forgetting: violet, lavender and azure). written by
// build.js to Resources/Burst:
//   <boss>_mote     a mote of power drawn in to the boss as it gathers, a streak with a bright head
//   <boss>_spark    a shard flung out as it goes off
//   <boss>_core     the power gathered at the boss: a knot of light swelling, its rays turning
//   <boss>_wave     the release: a flash, then a great ring tearing outward, a second inside it,
//                   chunks of light flung off
//   <boss>_ember    a flake of light drifting up after (the aftermath)
const K = require("./kit");
const { P, D, W, put, bayer, img, TAU, lerp, blob } = K;
const { ringField, breakup, easeOut, twinkle } = W;
const { Q } = require("./mengpo");

const THEMES = {
  yama: {
    ramp: [[0.1, P.R0], [0.22, P.R1], [0.38, P.R2], [0.55, P.O1], [0.7, P.O2], [0.84, P.G3], [0.95, P.W]],
    ring: [[0.1, P.R1], [0.3, P.R2], [0.5, P.O2], [0.72, P.G3], [0.9, P.W]],
    hi: [P.W, P.G3, P.O2, P.R2, P.R1],
  },
  mengpo: {
    ramp: [[0.1, Q.SP0], [0.22, Q.SP1], [0.38, Q.SP2], [0.55, Q.SP3], [0.72, P.A4], [0.86, Q.SP4], [0.95, P.W]],
    ring: [[0.1, Q.SP1], [0.3, Q.SP2], [0.5, Q.SP3], [0.72, P.A4], [0.9, P.W]],
    hi: [P.W, Q.SP4, P.A4, Q.SP3, Q.SP2],
  },
};

function shaded(S, ramp, fill, opts) { const F = D.field(S, S); fill(F); return D.shade(img(S, S), F, ramp, opts); }

// a streak with a bright head, pointing right: drawn in toward the boss
function mote(t) {
  const out = [], S = 40, c = 20;
  for (let f = 0; f < 4; f++) {
    out.push(shaded(S, t.ramp, F => {
      D.each(F, (x, y) => {
        const u = x - c, v = Math.abs(y - c);
        if (u > 4 || u < -16) return 0;
        const w = u > 0 ? 3.2 - u * 0.5 : 3.2 * (1 + u / 17);
        if (v > w) return 0;
        return (u > 0 ? 1 : 0.95 + u / 22) * (1 - v / (w + 0.5) * 0.5) + (f % 2) * 0.05;
      });
    }));
    twinkle(out[f], c + 1, c, f % 2, [P.W, t.hi[1]]);
  }
  return out;
}

// a shard flung outward: a thin blade of light, pointing right, fading
function spark(t) {
  const out = [], S = 36, c = 18;
  for (let f = 0; f < 5; f++) {
    const k = f / 4;
    out.push(shaded(S, t.ramp, F => {
      D.each(F, (x, y) => {
        const u = x - c, v = Math.abs(y - c), len = 12 - k * 5;
        if (u > len || u < -len * 0.6) return 0;
        const w = 2.2 * (1 - Math.abs(u) / len) * (1 - k * 0.4);
        if (v > w + 0.3) return 0;
        return (1 - k * 0.55) * (u > 0 ? 1 : 0.8);
      });
    }));
  }
  return out;
}

// the power gathered: a knot of light swelling, rays turning round it, a ring pulled in to it
function core(t) {
  const out = [], S = 112, c = 56, N = 10;
  for (let f = 0; f < N; f++) {
    const k = f / (N - 1), im = img(S, S), R = 6 + k * 14;
    // the ring pulled in
    const Rr = lerp(46, R + 6, k);
    D.over(im, shaded(S, t.ring, F => ringField(F, c, c, Rr, 2.2, 0.55 + 0.4 * k, breakup(Rr, f, 0.7 + 0.25 * k))));
    // rays
    D.over(im, shaded(S, t.ramp, F => {
      for (let r = 0; r < 8; r++) D.ray(F, c, c, r * TAU / 8 + f * 0.22, R * (1.6 + (r % 2) * 0.6), 2.6, 0.9, 0.2);
    }));
    // the knot
    D.over(im, shaded(S, t.ramp, F => blob(F, c, c, R, R, 1, 0.45)));
    for (let y = -3; y <= 3; y++) for (let x = -3; x <= 3; x++) if (x * x + y * y <= 9 + k * 8) put(im, c + x, c + y, P.W);
    // sparks drawn in round it
    const r = D.rng(f + 5);
    for (let q = 0; q < 10; q++) {
      const a = r() * TAU, d = lerp(50, R + 4, (k + r() * 0.5) % 1);
      put(im, c + Math.cos(a) * d, c + Math.sin(a) * d, d < R + 14 ? P.W : t.hi[2]);
    }
    out.push(im);
  }
  return out;
}

// the release: a white flash, then a thick ring tearing out, a thinner one inside it, rays, chunks
function wave(t) {
  const out = [], S = 320, c = 160, N = 12;
  const r = D.rng(91);
  const chunks = Array.from({ length: 22 }, () => ({ a: r() * TAU, s: 0.6 + r() * 0.6, big: r() < 0.4 }));
  for (let f = 0; f < N; f++) {
    const k = f / (N - 1), im = img(S, S);
    if (f <= 1) {
      // the flash: a disc of white and the burst's rays
      D.over(im, shaded(S, t.ramp, F => {
        blob(F, c, c, 30 + f * 16, 30 + f * 16, 1, 0.7);
        for (let q = 0; q < 14; q++) D.ray(F, c, c, q * TAU / 14 + 0.1, 90 + (q % 3) * 20, 7 - f * 2, 1, 0.3);
      }));
    }
    if (f >= 1) {
      const t2 = (f - 1) / (N - 2);
      const R = 18 + easeOut(t2) * 108, th = Math.max(2, 16 * (1 - t2));
      D.over(im, shaded(S, t.ring, F => ringField(F, c, c, R, th, 1 - t2 * 0.6, t2 > 0.35 ? breakup(R, f, 1.15 - t2 * 0.8) : null)));
      if (f >= 2) {
        const R2 = 10 + easeOut((f - 2) / (N - 3)) * 80;
        D.over(im, shaded(S, t.ramp, F => ringField(F, c, c, R2, Math.max(1.4, 6 * (1 - t2)), 0.9 - t2 * 0.5, breakup(R2, f + 3, 1.05 - t2 * 0.6))));
      }
      // chunks of light flung out ahead of the ring
      for (const ch of chunks) {
        const d = R * ch.s * 1.1, x = c + Math.cos(ch.a) * d, y = c + Math.sin(ch.a) * d;
        if (bayer(Math.round(x), Math.round(y)) < t2 - 0.35) continue;
        const col = t.hi[Math.min(t.hi.length - 1, Math.floor(t2 * t.hi.length))];
        put(im, x, y, col); if (ch.big) { put(im, x + 1, y, col); put(im, x, y + 1, col); put(im, x - Math.cos(ch.a) * 2, y - Math.sin(ch.a) * 2, t.hi[3]); }
      }
      // the inner glow dying
      if (t2 < 0.4) D.over(im, shaded(S, t.ramp, F => blob(F, c, c, 26 * (1 - t2), 26 * (1 - t2), 0.8 - t2, 0.3)));
    }
    out.push(im);
  }
  return out;
}

// a flake of light drifting up in the aftermath, flickering
function ember(t) {
  const out = [], S = 12, c = 6;
  for (let f = 0; f < 4; f++) {
    const im = img(S, S);
    twinkle(im, c, c, f % 3 === 0 ? 1 : 0, [P.W, t.hi[1], t.hi[2]]);
    if (f === 1) put(im, c, c, t.hi[1]);
    out.push(im);
  }
  return out;
}

function makeBurst() {
  const all = {};
  for (const [name, t] of Object.entries(THEMES)) {
    all[name + "_mote"] = mote(t);
    all[name + "_spark"] = spark(t);
    all[name + "_core"] = core(t);
    all[name + "_wave"] = wave(t);
    all[name + "_ember"] = ember(t);
  }
  return all;
}

module.exports = { makeBurst };
