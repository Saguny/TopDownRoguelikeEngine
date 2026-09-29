// the qi the dead leave behind: the pickups that fill the level bar (and only that; coins come from
// fortune envelopes). three tiers by worth, like Vampire Survivors' blue, green and red gems:
//   wen_bronze    a qi spark: a small azure wisp of soul-fire, white at its heart
//   wen_jade      a qi bead: a jade bead glowing from inside, a swirl of qi in it
//   wen_envelope  a dragon pearl: the flaming pearl dragons chase, gold-white, three tongues of fire
//                 wheeling round it. where qi goes when too much is lying about
//   wen_spin      the level up's rain and the loading screen: the spark flickering (frames 0-7,
//                 tag bronze) and the bead's swirl turning (8-15, tag jade), 15x15
// the files keep their old names (and one layer and frame each, named as before) so the sprites
// Unity already knows keep their ids and every prefab keeps pointing at them.
// node pickups/qi.js writes them into Asesprites/VFX/Pickups and out/qi_preview.png
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const { write } = require("../write-ase");
const { hex, put } = D;

const OUTLINE = [0x1a, 0x0c, 0x26, 255];
const W = hex("#ffffff");
const AZURE = ["#16306e", "#2a5ab0", "#4aa8f0", "#a8e6ff"].map(hex);
const JADE = ["#0e3a34", "#1e7a5e", "#3cc08c", "#a8f5d0"].map(hex);
const GOLD = ["#6e2e1c", "#dc9b00", "#ffd24a", "#fff4c0"].map(hex);
const FIRE = ["#8d1c30", "#e0402c", "#ff8a24", "#ffd24a"].map(hex);

// fills a shape (x, y at pixel centres -> 0..1 inside, <0 outside) from a ramp by that level
function fill(im, shape, ramp, cuts = [0.2, 0.45, 0.75]) {
  const mask = [];
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++) {
    const v = shape(x + 0.5, y + 0.5);
    if (v < 0) continue;
    put(im, x, y, v > cuts[2] ? ramp[3] : v > cuts[1] ? ramp[2] : v > cuts[0] ? ramp[1] : ramp[0]);
    mask.push([x, y]);
  }
  return mask;
}

// a dark rim round everything drawn so far
function outline(im) {
  const on = (x, y) => x >= 0 && y >= 0 && x < im.w && y < im.h && im.data[(y * im.w + x) * 4 + 3] > 0;
  const edge = [];
  for (let y = 0; y < im.h; y++) for (let x = 0; x < im.w; x++)
    if (!on(x, y) && (on(x - 1, y) || on(x + 1, y) || on(x, y - 1) || on(x, y + 1))) edge.push([x, y]);
  for (const [x, y] of edge) put(im, x, y, OUTLINE);
  return im;
}

// tier 1: a wisp of soul-fire, a teardrop licking upward, lit from inside
function spark(f = 0) {
  const S = 13, im = D.image(S, S), cx = 6.5, cy = 8, ph = f / 8 * Math.PI * 2;
  fill(im, (x, y) => {
    const dx = x - cx, dy = y - cy;
    // round below, drawn up into a flickering point
    const r = dy >= 0 ? 3.6 : 3.6 * Math.max(0, 1 + dy / 6.2);
    const bend = dy < 0 ? Math.sin(dy * 0.7 + ph) * 0.6 : 0;
    const d = Math.abs(dx - bend) / Math.max(0.01, r);
    const inside = dy >= 0 ? Math.hypot(dx, dy) <= 3.6 : d <= 1 && dy > -6.2;
    if (!inside) return -1;
    const k = dy >= 0 ? 1 - Math.hypot(dx, dy) / 3.6 : 1 - d;
    return Math.min(1, k * 1.1 + (dy > -1 && dy < 2 ? 0.25 : 0));
  }, AZURE, [0.12, 0.35, 0.62]);
  put(im, 6, 8, W); put(im, 6, 7, W);
  outline(im);
  put(im, 10, 3, AZURE[3]); put(im, 2, 5, AZURE[2]);
  return im;
}

// tier 2: a jade bead, glowing, a swirl of qi turning in it
function bead(f = 0) {
  const S = 14, im = D.image(S, S), c = 7, turn = f / 8 * Math.PI * 2;
  fill(im, (x, y) => {
    const d = Math.hypot(x - c, y - c);
    if (d > 5) return -1;
    // lit from the upper left, bright at the heart
    const lx = (x - c + 1.6), ly = (y - c + 1.6);
    return Math.min(1, (1 - d / 5) * 0.8 + Math.max(0, 1 - Math.hypot(lx, ly) / 4) * 0.5);
  }, JADE, [0.12, 0.3, 0.62]);
  // the swirl: a comma of pale qi curling round the centre
  for (let i = 0; i <= 10; i++) {
    const a = i / 10 * Math.PI * 1.3 + 0.6 + turn, r = 1 + i * 0.22;
    put(im, Math.round(c + Math.cos(a) * r - 0.5), Math.round(c + Math.sin(a) * r - 0.5), i < 4 ? W : JADE[3]);
  }
  put(im, 4, 4, W);
  outline(im);
  put(im, 12, 2, JADE[3]); put(im, 1, 11, JADE[2]);
  return im;
}

// tier 3: the flaming pearl: gold-white, three tongues of fire wheeling round it
function pearl() {
  const S = 16, im = D.image(S, S), c = 8;
  // the fire first, behind the pearl
  fill(im, (x, y) => {
    const dx = x - c, dy = y - c, d = Math.hypot(dx, dy), a = Math.atan2(dy, dx);
    if (d < 4 || d > 7.4) return -1;
    // three tongues, swept round like a pinwheel
    const tongue = Math.pow(Math.max(0, Math.cos((a - d * 0.45) * 3)), 1.6);
    const reach = 4.6 + 2.8 * tongue;
    if (d > reach) return -1;
    return 1 - (d - 4) / (reach - 4 + 0.01);
  }, FIRE, [0.15, 0.4, 0.75]);
  fill(im, (x, y) => {
    const d = Math.hypot(x - c, y - c);
    if (d > 4.4) return -1;
    return Math.min(1, (1 - d / 4.4) * 0.7 + Math.max(0, 1 - Math.hypot(x - c + 1.4, y - c + 1.4) / 3) * 0.6);
  }, GOLD, [0.1, 0.3, 0.6]);
  put(im, 6, 6, W); put(im, 7, 6, W); put(im, 6, 7, W);
  outline(im);
  put(im, 14, 2, GOLD[3]); put(im, 1, 13, FIRE[3]);
  return im;
}

const DEST = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "NewSprites", "Asesprites", "VFX", "Pickups");
const TIERS = [["wen_bronze", spark], ["wen_jade", bead], ["wen_envelope", pearl]];

if (require.main === module) {
  const all = [];
  const palette = [OUTLINE, W, ...AZURE, ...JADE, ...GOLD, ...FIRE];
  for (const [name, make] of TIERS) {
    const im = make();
    write(path.join(DEST, name + ".aseprite"), im.w, im.h, [{ name, frames: [im] }], [100], [], palette);
    all.push(im);
  }
  // the rain's frames: each tier centred on a 15x15 canvas
  const onCanvas = src => { const im = D.image(15, 15); D.blit(im, src, Math.floor((15 - src.w) / 2), Math.floor((15 - src.h) / 2)); return im; };
  const spin = [...Array.from({ length: 8 }, (_, f) => onCanvas(spark(f))), ...Array.from({ length: 8 }, (_, f) => onCanvas(bead(f)))];
  write(path.join(DEST, "wen_spin.aseprite"), 15, 15, [{ name: "wen", frames: spin }], Array(16).fill(80), [["bronze", 0, 7], ["jade", 8, 15]], palette);
  all.push(...spin);
  fs.mkdirSync(path.join(__dirname, "out"), { recursive: true });
  png.encode(png.preview(all, 8, [30, 26, 34], 8, 4), path.join(__dirname, "out", "qi_preview.png"));
  console.log("wrote " + TIERS.map(t => t[0]).join(", ") + ", wen_spin");
}

module.exports = { spark, bead, pearl };
