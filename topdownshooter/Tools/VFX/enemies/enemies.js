// the five enemies, restyled to sit with the weapons' VFX (see ../restyle.js), each with a touch of
// what it is: foxfire on the fox spirit's tail, a ghostly glow round the ghost fire, the hungry
// ghost's eyes lit, the jiangshi's seal flickering, embers off the nian's mane. works from the
// original frames in src/ and writes the same files back: same canvas, frames, timing, tags and
// layer name, so the game's animations keep pointing at the right frames
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const { restyle, hex } = require("../restyle");
const { P } = W;
const put = D.put;

const SRC = path.join(__dirname, "src");
const load = name => Array.from({ length: 6 }, (_, i) => png.decode(path.join(SRC, `${name}_${i}.png`)));
const c4 = h => [...hex(h), 255];
const is = (im, x, y, cols) => { const p = D.get(im, x, y); return p && cols.some(c => { const q = hex(c); return p[0] === q[0] && p[1] === q[1] && p[2] === q[2]; }); };
const empty = (im, x, y) => !D.get(im, x, y);

// a glow round everything already drawn: its distance falloff shaded through a two step ramp, solid
// close in and dithered out to nothing, drawn under the sprite
function glowAround(im, radius, near, far, phase = 0) {
  const F = D.field(im.w, im.h);
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++) {
    if (!empty(im, x, y)) continue;
    let d = 99;
    for (let dy = -radius; dy <= radius; dy++) for (let dx = -radius; dx <= radius; dx++)
      if (!empty(im, x + dx, y + dy)) d = Math.min(d, Math.hypot(dx, dy));
    if (d <= radius) F.f[y * im.w + x] = 1 - (d - 0.5) / (radius + 0.5);
  }
  const g = D.shade(D.image(im.w, im.h), F, [[0.35, far], [0.72, near]], { fadeBand: 0.55 });
  if (phase) for (let i = 0; i < g.data.length; i += 4) if (g.data[i + 3] && D.bayer((i / 4) % im.w + phase, ((i / 4) / im.w) | 0) < 0.15) g.data[i + 3] = 0;
  return D.over(g, im);
}

const ENEMIES = {
  fox_spirit: {
    durations: [80, 80, 80, 80, 350, 350],
    ramps: [["#7a3010", "#b04a18", "#d86a20", "#f07a28", "#ffb060"], ["#c8b090", "#f4e4c8", "#fffaf0"]],
    glow: ["#f8d068"], ao: 2,
    // foxfire burning off the tip of the tail
    accent(out, src, f) {
      const fur = ["#c8b090", "#f4e4c8", "#fffaf0"];
      let tip = null;
      for (let y = 0; y < src.h && !tip; y++) for (let x = 0; x < src.w * 0.35; x++) if (is(src, x, y, fur)) { tip = [x, y]; break; }
      if (!tip) return;
      // a teardrop of foxfire standing on the tip: wide and white at the root, a blue point on top
      const fire = D.image(out.w, out.h);
      const h = [7, 8, 6, 8, 7, 6][f], sway = [0, 1, 1, 0, -1, -1][f];
      for (let r = 0; r < h; r++) {
        const k = r / (h - 1), y = tip[1] + 1 - r, cx = tip[0] + 1 + Math.round(sway * k * k * 2);
        const half = k < 0.35 ? 1 : k < 0.8 ? 0.5 : 0;
        for (let dx = -Math.ceil(half); dx <= Math.floor(half + 0.5); dx++)
          put(fire, cx + dx, y, dx === 0 && k < 0.3 ? P.W : dx === 0 && k < 0.65 ? P.A5 : k < 0.85 ? P.A4 : P.A3);
      }
      if (f % 3 === 0) put(fire, tip[0] + 1 + sway, tip[1] - h - 1, P.A4);
      const lit = glowAround(fire, 1, P.A3, P.A2);
      D.over(out, lit);
    },
  },
  ghost_fire: {
    durations: [90, 90, 90, 90, 350, 350],
    ramps: [], ao: 0,
    // a dithered glow round the flame, and a mote drifting off the top
    accent(out, src, f) {
      const glowing = glowAround(out, 2, P.A2, P.A1, f);
      out.data.set(glowing.data);
      let top = out.h, cx = 0;
      for (let y = 0; y < out.h && top === out.h; y++) for (let x = 0; x < out.w; x++) if (!empty(src, x, y)) { top = y; cx = x; break; }
      put(out, cx + [0, 1, 1, 0, -1, 0][f], top - 2 - (f % 3), f % 2 ? P.A5 : P.W);
    },
  },
  hungry_ghost: {
    durations: [140, 140, 140, 140, 350, 350],
    ramps: [["#4a4666", "#6e6a8a", "#76719a", "#8e89ae", "#b0abc8", "#e6e2f2"], ["#5a3218", "#8a5028", "#b07848"]],
    glow: ["#f8d068", "#fff0b0"], ao: 3,
    // its eyes burn with hunger
    accent(out, src, f) {
      const eyes = D.image(out.w, out.h);
      for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) if (is(src, x, y, ["#fff0b0", "#f8d068"])) put(eyes, x, y, P.W);
      const halo = glowAround(eyes, 1, P.G2, P.G1);
      for (let i = 0; i < halo.data.length; i += 4) if (halo.data[i + 3] && !eyes.data[i + 3] && out.data[i + 3] && (f % 2 === 0 || D.bayer((i / 4) % out.w, ((i / 4) / out.w) | 0) < 0.5))
        out.data.set(halo.data.subarray(i, i + 4), i);
      for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) {
        if (is(src, x, y, ["#fff0b0"])) put(out, x, y, P.W);
        else if (is(src, x, y, ["#f8d068"])) {
          put(out, x, y, P.G3);
          if (f % 2 === 0 && !empty(out, x + 1, y) && !is(src, x + 1, y, ["#000000"])) put(out, x + 1, y, P.G2);
        }
      }
    },
  },
  jiangshi: {
    durations: [130, 130, 130, 130, 350, 350],
    ramps: [["#18234a", "#2c4274", "#4a6aa6"], ["#5f7f6b", "#93b39b", "#c9dcc0"], ["#7e1426", "#d02838", "#ff6a5a"],
      ["#c88830", "#d8b860", "#f8d068", "#f8e6a0", "#fff0b0", "#fff8d8"], ["#45404f", "#7e8298", "#c8ccdc", "#ffffff"]],
    ao: 3,
    // the seal's glyph pulses
    accent(out, src, f) {
      const paper = ["#f8e6a0", "#fff8d8"];
      let x0 = src.w, x1 = -1, y0 = src.h, y1 = -1;
      for (let y = 0; y < src.h; y++) for (let x = 0; x < src.w; x++) if (is(src, x, y, paper)) { x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y); }
      for (let y = y0 + 1; y < y1; y++) for (let x = x0 + 1; x < x1; x++)
        if (is(src, x, y, ["#d02838", "#ff6a5a", "#7e1426"])) put(out, x, y, f % 3 === 0 ? P.O2 : f % 3 === 1 ? P.R3 : P.R2);
    },
  },
  nian: {
    durations: [170, 170, 170, 170, 350, 350],
    ramps: [["#5a0e18", "#7e1426", "#8a1a28", "#b01e30", "#d02838", "#ff6a5a"], ["#c88830", "#f8d068", "#fff0b0"],
      ["#b0a080", "#c8b090", "#e8dcc0", "#f4e4c8", "#fffaf0"]],
    ao: 4,
    // embers lifting off the mane
    accent(out, src, f) {
      let gx = 0, gy = src.h;
      for (let y = 0; y < src.h && gy === src.h; y++) for (let x = 0; x < src.w; x++) if (is(src, x, y, ["#f8d068", "#fff0b0", "#c88830"])) { gx = x; gy = y; break; }
      const embers = [[5, 3, 0], [-6, 5, 2], [11, 7, 4]];
      embers.forEach(([dx, dy, ph]) => {
        const k = (f + ph) % 6;
        if (k > 3) return;
        put(out, gx + dx + (k === 2 ? 1 : 0), gy + dy - 3 - k * 2, k === 0 ? P.G3 : k === 1 ? P.O2 : P.R2);
      });
    },
  },
};

function build() {
  const out = path.join(__dirname, "out");
  fs.rmSync(out, { recursive: true, force: true });
  const manifest = [], compare = [];
  for (const [name, e] of Object.entries(ENEMIES)) {
    const src = load(name);
    const frames = restyle(src, { ramps: e.ramps, glow: e.glow, ao: e.ao });
    frames.forEach((im, f) => e.accent(im, src[f], f));
    const dir = path.join(out, name);
    fs.mkdirSync(dir, { recursive: true });
    frames.forEach((im, f) => png.encode(im, path.join(dir, `L0_${f}.png`)));
    manifest.push({ name, group: ".", w: src[0].w, h: src[0].h, frames: 6, layers: [name], durations: e.durations, tags: [["walk", 0, 3], ["idle", 4, 5]] });
    compare.push(png.preview([...src, ...frames], src[0].h < 40 ? 6 : 4, [70, 60, 72], 6, 2));
  }
  compare.forEach((im, i) => png.encode(im, path.join(out, `compare_${i}_${manifest[i].name}.png`)));
  fs.writeFileSync(path.join(out, "manifest.json"), JSON.stringify({ palette: P, elements: manifest }, null, 1));
  console.log(manifest.map(m => `${m.name} ${m.w}x${m.h}`).join("\n"));
}

build();
