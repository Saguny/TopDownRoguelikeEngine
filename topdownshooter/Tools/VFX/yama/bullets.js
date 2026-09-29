// the danmaku's bullets (Resources/Yama/bullets.png), drawn the way Touhou draws them so they read
// against the dark floors the end bosses fight on: a big white-hot core, a pale body, a rim of one
// vivid colour, and a soft glow of that colour round the outside (half see-through, the shader
// blends it) instead of a dark edge, which sank into the night. a 6x6 atlas of 32x32 cells: rows
// orb, rice, talisman, coin, flame, big orb; columns red, gold, violet, azure, jade, bone (see
// BulletColor). node yama/bullets.js writes it alone; yama.js uses it too
const path = require("path");
const png = require("../png");
const D = require("../draw");
const { hex, put, bayer } = D;

// [rim, body]; the core is white
const COLOURS = [
  ["#ff2447", "#ff9fb0"],       // red
  ["#ffb300", "#fff08a"],       // gold
  ["#c24dff", "#efbcff"],       // violet
  ["#1fa8ff", "#aef0ff"],       // azure
  ["#16e68a", "#b4ffda"],       // jade
  ["#b9c3ff", "#f4f6ff"],       // bone: the white bullets, a cool rim so they still glow
].map(([rim, body]) => ({ rim: hex(rim), body: hex(body) }));
const WHITE = hex("#ffffff");
const CELL = 32;

const withAlpha = (c, a) => [c[0], c[1], c[2], Math.round(255 * a)];

// each shape: the level of a point inside it, 1 at the core falling to 0 at its edge, or -1 outside
const SHAPES = [
  // orb: small and round
  (dx, dy) => { const d = Math.hypot(dx, dy); return d < 4.9 ? 1 - d / 4.9 : -1; },
  // rice: a grain along its flight
  (dx, dy) => { const d = Math.hypot(dx / 6.2, dy / 3.2); return d < 1 ? 1 - d : -1; },
  // talisman: a paper strip, handled on its own below
  null,
  // coin: cash with a square hole
  (dx, dy) => {
    const d = Math.hypot(dx, dy);
    if (d >= 5.6 || (Math.abs(dx) < 1.3 && Math.abs(dy) < 1.3)) return -1;
    return 1 - d / 5.6;
  },
  // flame: a soul flame streaming back from its head (+x is ahead)
  (dx, dy) => {
    const t = dx;
    const w = t > 0 ? Math.sqrt(Math.max(0, 1 - (t / 4.8) ** 2)) * 3.8 : 3.8 * Math.max(0, 1 + t / 10.5) + Math.sin(t * 1.3) * 0.5;
    if (Math.abs(dy) >= w || t <= -10.5 || t >= 4.8) return -1;
    const across = 1 - Math.abs(dy) / Math.max(0.6, w), back = Math.max(0, -t) / 10.5;
    return Math.max(0, across * (1 - back * 0.85));
  },
  // big orb: a white heart in a wide rim
  (dx, dy) => { const d = Math.hypot(dx, dy); return d < 10.8 ? 1 - d / 10.8 : -1; },
];

function bullet(type, col) {
  const im = D.image(CELL, CELL);
  const { rim, body } = COLOURS[col];
  const level = new Float32Array(CELL * CELL).fill(-1);

  for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
    const dx = x + 0.5 - 16, dy = y + 0.5 - 16;
    let v = -1;
    if (type === 2) {
      // talisman: a pale paper strip, a vivid border and glyph, a white shine down its middle
      if (Math.abs(dx) < 7.5 && Math.abs(dy) < 3.8) {
        const border = Math.abs(dx) > 6.4 || Math.abs(dy) > 2.7;
        const glyph = (Math.abs(dy) < 0.8 && Math.abs(dx) < 4.6) || (Math.abs(dx) < 0.8 && Math.abs(dy) < 2.6) || (Math.abs(dx - 3) < 0.8 && Math.abs(dy) < 1.8);
        v = border || glyph ? 0.2 : Math.abs(dy) < 1.2 && bayer(x, y) < 0.7 ? 1 : 0.6;
      }
    } else v = SHAPES[type](dx, dy);
    if (v < 0) continue;
    level[y * CELL + x] = v;
    // the core white, then the pale body, then the vivid rim; the big orb's core is wider
    const coreAt = type === 5 ? 0.45 : type === 1 ? 0.55 : 0.5;
    put(im, x, y, v >= coreAt ? WHITE : v >= 0.22 ? body : rim);
  }

  // the glow: a ring of the rim's colour a pixel out, half see-through, and a fainter one past it
  const inside = (x, y) => x >= 0 && y >= 0 && x < CELL && y < CELL && level[y * CELL + x] >= 0;
  for (let y = 0; y < CELL; y++) for (let x = 0; x < CELL; x++) {
    if (inside(x, y)) continue;
    let near = 99;
    for (let j = -2; j <= 2; j++) for (let i = -2; i <= 2; i++) if (inside(x + i, y + j)) near = Math.min(near, Math.hypot(i, j));
    if (near <= 1.01) put(im, x, y, withAlpha(rim, 0.55));
    else if (near <= 2.3) put(im, x, y, withAlpha(rim, 0.22));
  }
  return im;
}

function makeBullets() {
  const atlas = D.image(CELL * 6, CELL * 6);
  for (let t = 0; t < 6; t++) for (let c = 0; c < 6; c++) D.blit(atlas, bullet(t, c), c * CELL, t * CELL);
  return atlas;
}

module.exports = { makeBullets, CELL };

if (require.main === module) {
  const dest = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "Resources", "Yama", "bullets.png");
  const atlas = makeBullets();
  png.encode(atlas, dest);
  png.encode(png.preview([atlas], 4, [40, 34, 44], 1, 2), path.join(__dirname, "out", "sheet_bullets.png"));
  console.log("wrote " + dest);
}
