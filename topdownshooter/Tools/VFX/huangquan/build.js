// Huangquan Road's art, all of it, written straight to Resources/Huangquan as PNG strips of square
// frames (the game slices them at run time: YamaArt.Strip, HqEnemyArt), with import settings for
// pixel art beside each, so there's nothing to set up in Unity:
//   node huangquan/build.js
// every strip is cropped to what's drawn in it, the same about its middle on every side (its
// pivot), and the build stops if anything would be cut off at a frame's edge. out/ has previews
const fs = require("fs");
const path = require("path");
const png = require("../png");
const K = require("./kit");
const mobs = require("./mobs");
const guardians = require("./guardians");
const fx = require("./fx");
const statues = require("./statues");

const DEST = path.join(K.RES, "Huangquan");
const META = path.join(K.RES, "Yama", "bar.png.meta");
// tiled end to end by the game, so drawn edge to edge on purpose
const TILED = new Set(["charge_lane"]);

function everything() {
  const m = mobs.makeMobs(), bull = guardians.makeBull(), horse = guardians.makeHorse();
  const e = guardians.eyes(bull.bull_head[0], horse.horse_face[0]);
  return Object.assign({}, m, bull, horse,
    fx.makeFx({ bull: bull.bull_head_charge[1], servant: m.paper_servant_dash[0] }),
    statues.makeStatue("ox", bull.bull_head[0], e.ox, 31),
    statues.makeStatue("horse", horse.horse_face[0], e.horse, 37));
}

// pixel-art import settings, from an existing strip's, with a fresh guid; kept if it's there
function meta(file) {
  const m = file + ".meta";
  if (fs.existsSync(m)) return;
  const guid = require("crypto").randomBytes(16).toString("hex");
  const text = fs.readFileSync(META, "utf8").replace(/guid: [0-9a-f]+/, "guid: " + guid).replace(/maxTextureSize: 2048/g, "maxTextureSize: 8192");
  fs.writeFileSync(m, text);
}

function build() {
  fs.mkdirSync(DEST, { recursive: true });
  const preview = path.join(__dirname, "out");
  fs.mkdirSync(preview, { recursive: true });
  if (!fs.existsSync(DEST + ".meta")) fs.copyFileSync(path.join(K.RES, "UI.meta"), DEST + ".meta"), fs.writeFileSync(DEST + ".meta",
    fs.readFileSync(DEST + ".meta", "utf8").replace(/guid: [0-9a-f]+/, "guid: " + require("crypto").randomBytes(16).toString("hex")));
  const all = everything(), problems = [];
  for (const [name, raw] of Object.entries(all)) {
    const frames = TILED.has(name) ? raw : K.fit(raw);
    if (!TILED.has(name)) {
      const cut = K.clipped(raw);
      if (cut.length) problems.push(`${name}: cut off while drawn, frames ${cut}`);
    }
    const strip = K.strip(frames);
    if (strip.w > 8192) problems.push(`${name}: ${strip.w}px wide, too wide for one texture`);
    const file = path.join(DEST, name + ".png");
    png.encode(strip, file);
    meta(file);
    const scale = frames[0].w <= 24 ? 8 : frames[0].w <= 56 ? 5 : frames[0].w <= 100 ? 3 : 2;
    png.encode(png.preview(frames, scale, [40, 34, 46], Math.min(frames.length, 8), 2), path.join(preview, `sheet_${name}.png`));
    console.log(`${name.padEnd(28)} ${frames[0].w}x${frames[0].h} x${frames.length}`);
  }
  if (problems.length) { console.error("\n" + problems.join("\n")); process.exit(1); }
}

if (require.main === module) build();
module.exports = { everything };
