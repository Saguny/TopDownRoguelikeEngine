// the end of a normal run: at 45 minutes the Wuchang, the Black and White Impermanence who escort
// the dead down Huangquan Road, come for the player the way Vampire Survivors' Reaper does. a tall
// figure drifting a hand above the ground, its robe fraying into mist, the tall hat, the long red
// tongue, a mourning staff hung with paper streamers and an iron chain. white first, then black.
// 48x72, four frames of drifting. and the lock for whatever hasn't been earned yet
// node wuchang.js
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const W = require("../weapons/weapons");
const { hex, put, bayer } = D;
const { img, withOutline, el } = W;

const OUTLINE = hex("#1a0c26");
const WW = 48, WH = 72;

// white: bone white robe, pale green-grey skin. black: soot robe, ash-grey skin. the rest is shared
const LOOKS = {
  wuchang_bai: { robe: ["#6d6478", "#b4aabb", "#e6e0e8", "#ffffff"], skin: ["#7f9a8c", "#c3d6c8", "#eef7ee"], hat: ["#9d94a6", "#e6e0e8", "#ffffff"], ink: "#1a0c26" },
  wuchang_hei: { robe: ["#0e0b16", "#241c33", "#3e3252", "#62557a"], skin: ["#4b4a5c", "#7c7a90", "#a9a8ba"], hat: ["#1c1628", "#342a48", "#524568"], ink: "#e6e0e8" },
};

const mask = () => D.mask(WW, WH);
const has = (M, x, y) => D.mget(M, x, y);
function poly(M, pts) {
  const ys = pts.map(p => p[1]);
  for (let y = Math.floor(Math.min(...ys)); y <= Math.ceil(Math.max(...ys)); y++) {
    const xs = [];
    for (let i = 0; i < pts.length; i++) {
      const [x0, y0] = pts[i], [x1, y1] = pts[(i + 1) % pts.length];
      if ((y0 <= y + 0.5 && y1 > y + 0.5) || (y1 <= y + 0.5 && y0 > y + 0.5)) xs.push(x0 + (y + 0.5 - y0) / (y1 - y0) * (x1 - x0));
    }
    xs.sort((a, b) => a - b);
    for (let i = 0; i + 1 < xs.length; i += 2) for (let x = Math.round(xs[i]); x <= Math.round(xs[i + 1]) - 1; x++) D.mset(M, x, y);
  }
  return M;
}
function shade(im, M, ramp, dark = 0.34, light = 0.78) {
  for (let y = 0; y < WH; y++) {
    let l = -1, r = -1;
    for (let x = 0; x < WW; x++) if (has(M, x, y)) { if (l < 0) l = x; r = x; }
    if (l < 0) continue;
    for (let x = l; x <= r; x++) {
      if (!has(M, x, y)) continue;
      const t = r > l ? (x - l) / (r - l) : 0.6;
      let c = t < dark ? 0 : t > light ? 2 : 1;
      if (!has(M, x, y - 1) && c > 0) c = 2;
      if (ramp[3] && c === 2 && !has(M, x, y - 1) && !has(M, x + 1, y)) c = 3;
      put(im, x, y, ramp[c]);
    }
  }
}

function wuchang(look, f) {
  const L = {};
  for (const k of ["robe", "skin", "hat"]) L[k] = look[k].map(hex);
  const ink = hex(look.ink);
  const im = img(WW, WH);
  const bob = [0, -1, -2, -1][f], U = y => y + bob;

  // the chain trailing behind, link by link, swinging
  for (let i = 0; i < 9; i++) {
    const x = 16 - i * 1.4, y = U(42) + i * 2.2 + Math.sin(f * 1.57 + i * 0.7) * 1.2;
    const c = i % 2 ? hex("#5a5566") : hex("#8d879a");
    put(im, Math.round(x), Math.round(y), c); put(im, Math.round(x) + (i % 2), Math.round(y) + 1, c);
  }

  // the robe: long, wide sleeved, its hem fraying into mist that thins toward the ground
  const robe = mask();
  poly(robe, [[19, U(24)], [30, U(24)], [33, U(40)], [35, U(58)], [15, U(58)], [16, U(40)]]);
  shade(im, robe, L.robe);
  for (let y = U(58); y <= U(66); y++) for (let x = 14; x <= 36; x++) {
    const k = (y - U(58)) / 8, wave = Math.sin(x * 0.9 + f * 1.3 + y * 0.4) * 0.5 + 0.5;
    if (x < 15 + k * 4 || x > 35 - k * 3) continue;
    if (bayer(x, y + f) < (1 - k) * 0.9 * wave + 0.05) put(im, x, y, k < 0.4 ? L.robe[1] : L.robe[0]);
  }
  // a belt of hemp rope, the robe's front fold
  for (let x = 17; x <= 32; x++) { put(im, x, U(37), hex("#8c7a58")); put(im, x, U(38), hex("#5e5038")); }
  for (let y = U(39); y <= U(57); y++) put(im, 27, y, L.robe[0]);

  // the head, long and pale, the tongue hanging to the chest
  const face = mask();
  poly(face, [[21, U(15)], [29, U(15)], [30, U(20)], [29, U(25)], [22, U(25)], [21, U(20)]]);
  shade(im, face, L.skin, 0.3, 0.8);
  put(im, 27, U(19), ink); put(im, 28, U(19), ink); put(im, 26, U(18), ink);            // a narrow eye
  put(im, 28, U(19), hex("#ff4a3a"));                                                    // lit red
  for (let y = U(23); y <= U(33); y++) {                                                  // the tongue
    const x = 27 + (y > U(29) ? 1 : 0);
    put(im, x, y, y === U(33) ? hex("#7e1426") : hex("#d02838")); put(im, x + 1, y, hex("#ff6a5a"));
  }

  // the tall hat, narrowing to a flat top, a column of characters down its front
  const hat = mask();
  poly(hat, [[20, U(15)], [31, U(15)], [29, U(1)], [23, U(1)]]);
  shade(im, hat, L.hat, 0.3, 0.75);
  for (let x = 19; x <= 32; x++) put(im, x, U(15), L.hat[0]);
  for (let y = U(4); y <= U(13); y += 3) { put(im, 26, y, ink); put(im, 27, y, ink); put(im, 26, y + 1, ink); }

  // the sleeves: the near arm holds the mourning staff out front, paper streamers hanging from it
  const sleeve = mask();
  poly(sleeve, [[27, U(26)], [31, U(26)], [37, U(33)], [37, U(38)], [32, U(38)], [28, U(31)]]);
  shade(im, sleeve, L.robe);
  put(im, 38, U(35), L.skin[1]); put(im, 38, U(36), L.skin[0]);
  for (let y = U(12); y <= U(62); y++) put(im, 39, y, y < U(14) ? hex("#c8b88a") : hex("#7e4220"));    // the staff
  for (let s = 0; s < 4; s++) for (let y = U(14); y <= U(22) + s * 2; y++) {                             // streamers
    const x = 40 + s + Math.round(Math.sin(y * 0.6 + f + s) * 0.6);
    put(im, x, y, s % 2 ? hex("#e6e0e8") : hex("#b4aabb"));
  }
  return withOutline(im, OUTLINE);
}

// a padlock for what isn't earned yet: a gold shackle over a dark iron body, a keyhole
function lock() {
  const im = img(14, 16);
  const gold = [hex("#6e3e14"), hex("#c88830"), hex("#f8d068"), hex("#fff0b0")];
  const iron = [hex("#17111d"), hex("#2b2232"), hex("#463a4b"), hex("#6b5b6c")];
  // shackle: an arch 2px thick
  for (let y = 1; y <= 7; y++) for (let x = 3; x <= 10; x++) {
    const d = Math.hypot((x - 6.5) / 3.6, (y - 6) / 4.8);
    if (d > 1 || d < 0.52) continue;
    put(im, x, y, x < 5 ? gold[0] : x > 8 ? gold[2] : y < 3 ? gold[2] : gold[1]);
  }
  put(im, 9, 3, gold[3]);
  // body
  for (let y = 7; y <= 14; y++) for (let x = 1; x <= 12; x++) {
    let c = x <= 2 ? iron[0] : x >= 11 ? iron[2] : iron[1];
    if (y === 7) c = x >= 10 ? iron[3] : iron[2];
    if (y === 14) c = iron[0];
    put(im, x, y, c);
  }
  for (let x = 2; x <= 11; x++) put(im, x, 9, x > 8 ? gold[2] : gold[1]);      // a gold band
  put(im, 6, 11, gold[3]); put(im, 7, 11, gold[2]); put(im, 6, 12, gold[1]); put(im, 6, 13, gold[1]);   // keyhole
  return withOutline(im, OUTLINE);
}

function build() {
  const out = path.join(__dirname, "out-wuchang");
  fs.rmSync(out, { recursive: true, force: true });
  const manifest = [], sheet = [];
  const elements = [
    ...Object.entries(LOOKS).map(([name, look]) => el(name, "Enemies/Boss", WW, WH, [{ name, frames: [0, 1, 2, 3].map(f => wuchang(look, f)) }], [140, 140, 140, 140], [["drift", 0, 3]])),
    el("lock", "VFX/UI", 14, 16, [{ name: "lock", frames: [lock()] }], [100], []),
  ];
  for (const e of elements) {
    const dir = path.join(out, e.name);
    fs.mkdirSync(dir, { recursive: true });
    e.layers[0].frames.forEach((im, i) => { png.encode(im, path.join(dir, `L0_${i}.png`)); sheet.push(im); });
    const { layers, ...meta } = e;
    manifest.push(Object.assign(meta, { frames: e.layers[0].frames.length, layers: layers.map(l => l.name) }));
  }
  png.encode(png.preview(sheet, 6, [58, 50, 62], 9, 2), path.join(out, "sheet.png"));
  fs.writeFileSync(path.join(out, "manifest.json"), JSON.stringify({ palette: W.P, elements: manifest }, null, 1));
  console.log(manifest.map(m => `${m.group}/${m.name} ${m.w}x${m.h} x${m.frames}`).join("\n"));
}
build();
