// the Heaven-Piercing Bow's level up icon: the Bow's own icon (NewSprites/Sprites/Icons/Weapons/
// bow.aseprite, its first frame copied in below) made heavenly. gold limbs with red bindings, a
// string of light, the arrow turned to light with the evolved arrows' shimmer running round its
// outline (gold, white, azure, orange: BowData's outline colours), a streak of light behind it
// for the stream that never stops, and stars twinkling round it. 32x32, 4 frames:
//   node bow/bow_evolved.js
// writes NewSprites/Asesprites/VFX/Weapons/Bow/bow_icon_evolved.aseprite (Tools > VFX > Build Weapon
// FX points the Bow's Evolved Icon at it) and out/sheet_bow_icon_evolved.png, a preview
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const K2 = require("../weapons2/weapons2");
const { write } = require("../write-ase");
const { put, bayer, hex } = D;
const P = K2.P;

// the Bow's icon, frame 0: a letter a colour (see BOW_COLOURS), . empty
const BOW = [
  "................................",
  "................................",
  "................................",
  "................................",
  "..........................a.....",
  "........................aala....",
  ".....................aaanlla....",
  "...aaaa.aaaaaaaa....annnnla.....",
  "..aemmmabbbbbbbbaa...ahflla.....",
  ".aeeeeebbbbbbbbbboaaahfjla......",
  "..aacqedddddddddkbokhfjala......",
  ".....caaaaaaaapdkkkhfja.a.......",
  ".....c........aaprhfjka.........",
  "......c.........ahfjkoa.........",
  "......c........ahfjrkboa........",
  "......c.......ahfjapkkba........",
  ".......c.....ahfja.addbba.......",
  ".......c....ahfja..apdbba.......",
  ".......c...ahfja....adbba.......",
  ".......acaahfja.....adbba.......",
  "......aiiiifja......adbba.......",
  ".....aiiiifga.......adbba.......",
  "....aiiiiigga.......adbba.......",
  ".....aahggggc.......adbba.......",
  ".......aggggaccc....adba........",
  "........agga....ccc.aeema.......",
  "........aga........ccqema.......",
  ".........a...........cema.......",
  ".....................aeea.......",
  ".....................aea........",
  "......................a.........",
  "................................",
];

// the Bow's colours, letter by letter, and what each becomes in heaven
const BOW_COLOURS = {
  a: "#2e1c2c", b: "#c42a36", c: "#e8dcc8", d: "#8d1c30", e: "#fff0b0", f: "#b47654", g: "#cb1e31",
  h: "#d8a070", i: "#f04a4a", j: "#8a5238", k: "#ffc300", l: "#95adb4", m: "#fffcec", n: "#ffffff",
  o: "#fff0a0", p: "#5a0e22", q: "#e8cc80", r: "#dc9b00",
};
const HEAVEN = {
  a: hex("#2e1c2c"),                 // the outline stays
  b: P.G2, d: P.G1, p: P.G0,         // the limbs, gold
  e: P.W, m: P.W, q: P.A4,           // the tips, white hot
  c: P.A4,                           // the string, light
  k: P.R2, r: P.R1, o: P.R3,         // the bindings, red
  f: P.A4, h: P.W, j: P.A3,          // the shaft, light
  g: P.A2, i: P.A3,                  // the fletching, azure
  l: P.W, n: P.W,                    // the head, white
};
const ARROW = new Set("fhjglin");
// the evolved arrows' outline, running round frame by frame
const SHIMMER = [P.G2, P.W, P.A3, P.O2];

function frame(f) {
  const S = 32, im = D.image(S, S);
  const at = (x, y) => (y >= 0 && y < S && x >= 0 && x < S ? BOW[y][x] : ".");

  // the streak of light behind the arrow, down and to the left along its line
  for (let k = 0; k < 9; k++) {
    const x = 4 - k * 0.85, y = 22 + k * 0.85;
    for (let w = -1; w <= 1; w++) {
      const px = Math.round(x + w * 0.7), py = Math.round(y - w * 0.7);
      if (bayer(px + f, py) < 0.85 - k * 0.09 - Math.abs(w) * 0.25) put(im, px, py, k < 3 && w === 0 ? P.A4 : P.A2);
    }
  }

  for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
    const ch = at(x, y);
    if (ch === ".") continue;
    let c = HEAVEN[ch] || hex(BOW_COLOURS[ch]);
    // the outline round the arrow shimmers; the rest stays dark
    if (ch === "a") {
      let byArrow = false;
      for (const [u, v] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (ARROW.has(at(x + u, y + v))) byArrow = true;
      if (byArrow) c = SHIMMER[(f + Math.floor((x + (31 - y)) / 6)) % SHIMMER.length];
    }
    put(im, x, y, c);
  }

  // the tip piercing: a star burst at the head, bigger every other frame
  const tx = 27, ty = 4;
  put(im, tx, ty, P.W);
  const big = f % 2 === 0;
  for (const [u, v] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) put(im, tx + u, ty + v, P.G3);
  if (big) for (const [u, v] of [[2, 0], [0, -2], [2, -2], [3, -3]]) put(im, tx + u, ty + v, u === 3 ? P.G2 : P.W);

  // stars twinkling round it, each on its own beat
  const stars = [[4, 14, 0], [13, 28, 1], [28, 17, 2], [17, 3, 3], [30, 26, 1]];
  for (const [x, y, beat] of stars) {
    const phase = (f + beat) % 4;
    if (phase === 0) { put(im, x, y, P.W); for (const [u, v] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) put(im, x + u, y + v, SHIMMER[beat % 4]); }
    else if (phase === 1) put(im, x, y, SHIMMER[(beat + 1) % 4]);
  }
  return im;
}

function build() {
  const frames = [0, 1, 2, 3].map(frame);
  const dest = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "NewSprites", "Asesprites", "VFX", "Weapons", "Bow", "bow_icon_evolved.aseprite");
  write(dest, 32, 32, [{ name: "icon", frames }], [130, 130, 130, 130], [["idle", 0, 3]], Object.values(P));
  const out = path.join(__dirname, "out");
  fs.mkdirSync(out, { recursive: true });
  png.encode(png.preview(frames, 8, [22, 18, 30], 4, 2), path.join(out, "sheet_bow_icon_evolved.png"));
  console.log("wrote " + path.relative(process.cwd(), dest));
}
if (require.main === module) build();
