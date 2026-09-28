// the Piercing passive's icon (projectiles go through more enemies): a spirit arrow shot clean
// through two red and white targets, sparks where it broke through, in the style of the other
// passive icons (NewSprites/Sprites/Icons: 32x32, dark plum outline). node icons/piercing.js
// writes NewSprites/Sprites/Icons/piercing.png and out/piercing_preview.png
const fs = require("fs");
const path = require("path");
const png = require("../png");
const D = require("../draw");
const { put, hex } = D;

const OUT = hex("#2e1c2c");
const C = {
  red: hex("#c42a36"), redD: hex("#8d1c30"), redL: hex("#f04a4a"), cream: hex("#fff0b0"), white: hex("#ffffff"),
  shaft: hex("#b47654"), shaftL: hex("#d8a070"), shaftD: hex("#8a5238"), gold: hex("#ffc300"), goldD: hex("#dc9b00"),
  steel: hex("#95adb4"), steelL: hex("#dfeff2"), jade: hex("#5ad2a0"),
};

function icon() {
  const S = 32, im = D.image(S, S), M = D.mask(S, S);
  const mark = (x, y) => D.mset(M, x, y);
  // the two targets, round, on the arrow's line
  const targets = [[10.5, 21.5], [19.5, 12.5]];
  for (const [cx, cy] of targets)
    for (let y = 0; y < S; y++) for (let x = 0; x < S; x++) {
      const d = Math.hypot(x + 0.5 - cx, y + 0.5 - cy);
      if (d <= 4.6) { mark(x, y); put(im, x, y, d > 3.6 ? C.red : d > 2.4 ? C.cream : d > 1.2 ? C.redL : C.red); }
    }
  // the arrow along the diagonal, over them: shaft, fletching at the tail, the head at the tip
  for (let i = 0; i <= 20; i++) {
    const x = 5 + i, y = 26 - i;
    mark(x, y); mark(x + 1, y);
    put(im, x, y, i % 5 === 0 ? C.shaftD : C.shaft); put(im, x + 1, y, C.shaftL);
  }
  for (const [x, y, c] of [[3, 25, C.redD], [4, 26, C.red], [3, 27, C.red], [4, 28, C.redL], [2, 26, C.redL], [5, 28, C.red], [2, 28, C.redD], [6, 27, C.redL]]) { mark(x, y); put(im, x, y, c); }
  for (const [x, y, c] of [[26, 5, C.steel], [27, 4, C.steelL], [28, 3, C.white], [26, 3, C.steelL], [28, 5, C.steel], [25, 4, C.steel], [27, 6, C.steel], [29, 2, C.white], [27, 2, C.steel], [29, 4, C.steel]]) { mark(x, y); put(im, x, y, c); }
  // the breach: a dark hole each side of the shaft where it went through, sparks flying
  for (const [cx, cy] of targets) {
    put(im, Math.round(cx) - 2, Math.round(cy), OUT); put(im, Math.round(cx), Math.round(cy) + 2, OUT);
  }
  for (const [x, y, c] of [[14, 13, C.gold], [15, 12, C.white], [23, 7, C.gold], [24, 9, C.goldD], [8, 17, C.gold], [22, 16, C.gold], [13, 24, C.goldD]]) put(im, x, y, c);
  // the dark outline round everything
  const edge = D.minus(D.dilate(M, 1, false), M);
  D.paint(im, edge, OUT);
  return im;
}

const im = icon();
const dest = path.join(__dirname, "..", "..", "..", "Assets", "### Different Engine", "NewSprites", "Sprites", "Icons", "piercing.png");
png.encode(im, dest);
fs.mkdirSync(path.join(__dirname, "out"), { recursive: true });
png.encode(png.preview([im], 10, [22, 18, 30], 1, 2), path.join(__dirname, "out", "piercing_preview.png"));
console.log("wrote " + dest);
