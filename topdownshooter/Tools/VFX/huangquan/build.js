// Huangquan Road's art, all of it, written straight to Resources/Huangquan as PNG strips of square
// frames (the game slices them at run time: YamaArt.Strip, HqEnemyArt), with import settings for
// pixel art beside each, so there's nothing to set up in Unity; Meng Po's to Resources/MengPo and
// the end boss duel's (duel.js) to Resources/Duel:
//   node huangquan/build.js              all of it
//   node huangquan/build.js duel         one set: huangquan, mengpo or duel
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
const mengpo = require("./mengpo");
const mengpoFx = require("./mengpo_fx");
const duel = require("./duel");

const DEST = path.join(K.RES, "Huangquan");
const META = path.join(K.RES, "Yama", "bar.png.meta");
// tiled end to end by the game, or cut-ins filling their frame: drawn edge to edge on purpose
const TILED = new Set(["charge_lane", "river", "portrait", "portrait_true"]);
// Meng Po's own, in Resources/MengPo
const MENGPO = () => Object.assign({}, mengpo.makeMengPo(), mengpoFx.makeMengPoFx());

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

function folder(dest) {
  fs.mkdirSync(dest, { recursive: true });
  if (!fs.existsSync(dest + ".meta"))
    fs.writeFileSync(dest + ".meta", fs.readFileSync(path.join(K.RES, "UI.meta"), "utf8").replace(/guid: [0-9a-f]+/, "guid: " + require("crypto").randomBytes(16).toString("hex")));
}

function build() {
  const sets = [["huangquan", DEST, everything], ["mengpo", path.join(K.RES, "MengPo"), MENGPO], ["duel", path.join(K.RES, "Duel"), duel.makeDuel]];
  const only = process.argv[2];
  const problems = [];
  for (const [name, dest, make] of sets) if (!only || only === name) writeSet(dest, make(), problems);
  if (problems.length) { console.error("\n" + problems.join("\n")); process.exit(1); }
}

function writeSet(DEST, all, problems) {
  folder(DEST);
  const preview = path.join(__dirname, "out");
  fs.mkdirSync(preview, { recursive: true });
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
    console.log(`${path.basename(DEST)}/${name.padEnd(24)} ${frames[0].w}x${frames[0].h} x${frames.length}`);
  }
}

if (require.main === module) build();
module.exports = { everything };
