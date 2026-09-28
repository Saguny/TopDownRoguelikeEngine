# a fortune envelope's opening (EnvelopeOpening) rendered outside Unity, to look at and listen to:
# the real sprites (fortune.js's out/, run it first) and sounds (Tools/SFX/fortune.py) on the code's
# timeline, its beats, eases and timings copied from the C#. UI space is 1920x1080, y up; the video
# is 0.6 of that. writes a silent .gif and an .mp4 with the sound
#   pip install pillow numpy imageio-ffmpeg
#   python3 preview.py <common|rare|legendary> out/envelope_legendary.gif
import sys, os, math, random, glob
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(__file__))
from aseread import read as ase_read

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = ROOT + "/Tools/VFX/fortune/out"
WEAP = ROOT + "/Assets/### Different Engine/NewSprites/Asesprites/VFX/Weapons"
FONT = ROOT + "/Assets/### Different Engine/Resources/fonts/Pixelta.ttf"
W, H = 1152, 648
S = 0.6                     # output pixels per UI unit
BIG, SMALL = 3, 3           # output pixels per art pixel (the game's 5 and 4 at this size, envelope a touch larger)
FPS = 25
rarity = sys.argv[1] if len(sys.argv) > 1 else "legendary"
random.seed(7)

def load(name):
    d = os.path.join(OUT, name)
    n = len(set(f.split("_")[1] for f in os.listdir(d)))
    layers = sorted(set(f.split("_")[0] for f in os.listdir(d)))
    frames = []
    for i in range(n):
        im = None
        for l in layers:
            p = Image.open(os.path.join(d, f"{l}_{i}.png")).convert("RGBA")
            im = p if im is None else Image.alpha_composite(im, p)
        frames.append(im)
    return frames

def ase(path):
    a = ase_read(path)
    out = []
    for dur, px in a["frames"]:
        im = Image.new("RGBA", (a["w"], a["h"]))
        im.putdata([tuple(p) if p else (0, 0, 0, 0) for p in px])
        out.append(im)
    return out

F = {n: load(n) for n in ["fe_big", "fe_flap", "fe_front", "fe_aura", "fe_rays", "fe_mote", "fe_burst_common", "fe_burst_rare",
                          "fe_burst_legendary", "fe_roller", "fe_scroll", "fe_slot", "fe_slot_evo", "fe_banner", "fe_coin"]}
ICON = {k: ase(f"{WEAP}/{p}") for k, p in {
    "sword_evo": "FlyingSword/fs_icon_evolved.aseprite", "dragon": "DragonLine/dl_icon.aseprite",
    "ink": "CinnabarInkBrush/ink_icon.aseprite", "ice": "IceCloud/ic_icon.aseprite", "sword": "FlyingSword/fs_icon.aseprite"}.items()}

JADE, AZURE, GOLD = (84, 216, 152), (90, 200, 240), (255, 204, 72)
COLOR = {"common": JADE, "rare": AZURE, "legendary": GOLD}
RANK = {"common": 0, "rare": 1, "legendary": 2}[rarity]
font_big = ImageFont.truetype(FONT, 26)
font_small = ImageFont.truetype(FONT, 17)
font_prompt = ImageFont.truetype(FONT, 19)
font_open = ImageFont.truetype(FONT, 24)

cache = {}
def scaled(im, k, key=None):
    kk = (id(im), k)
    if kk not in cache: cache[kk] = im.resize((im.width * k, im.height * k), Image.NEAREST)
    return cache[kk]

def tinted(im, color, alpha=1.0):
    r, g, b, a = im.split()
    c = [x / 255 for x in color]
    r = r.point(lambda v: int(v * c[0])); g = g.point(lambda v: int(v * c[1])); b = b.point(lambda v: int(v * c[2]))
    a = a.point(lambda v: int(v * alpha))
    return Image.merge("RGBA", (r, g, b, a))

def fade(im, alpha):
    if alpha >= 0.999: return im
    r, g, b, a = im.split()
    return Image.merge("RGBA", (r, g, b, a.point(lambda v: int(v * max(0, alpha)))))

def draw(canvas, im, ui_xy, k=1, rot=0.0, sx=1.0, sy=1.0, alpha=1.0, color=None):
    """a sprite with its centre at a UI point, k output px per art px"""
    if im is None: return
    img = scaled(im, k)
    if sx != 1.0 or sy != 1.0:
        img = img.resize((max(1, int(img.width * sx)), max(1, int(img.height * sy))), Image.NEAREST)
    if color is not None: img = tinted(img, color, alpha)
    elif alpha < 0.999: img = fade(img, alpha)
    if abs(rot) > 0.01: img = img.rotate(rot, resample=Image.NEAREST, expand=True)
    x = int(W / 2 + ui_xy[0] * S - img.width / 2); y = int(H / 2 - ui_xy[1] * S - img.height / 2)
    canvas.alpha_composite(img, (x, y)) if x >= 0 and y >= 0 and x + img.width <= W and y + img.height <= H else paste_clip(canvas, img, x, y)

def paste_clip(canvas, img, x, y):
    L, T = max(0, -x), max(0, -y)
    R, B = min(img.width, W - x), min(img.height, H - y)
    if R <= L or B <= T: return
    canvas.alpha_composite(img.crop((L, T, R, B)), (x + L, y + T))

def text(canvas, s, ui_xy, font, fill, outline=(30, 12, 58), anchor="mm"):
    d = ImageDraw.Draw(canvas)
    x, y = W / 2 + ui_xy[0] * S, H / 2 - ui_xy[1] * S
    for ox, oy in [(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1)]:
        d.text((x + ox, y + oy), s, font=font, fill=outline, anchor=anchor)
    d.text((x, y), s, font=font, fill=fill, anchor=anchor)

ease_out = lambda t: 1 - (1 - t) ** 2
ease_io = lambda t: 2 * t * t if t < 0.5 else 1 - (-2 * t + 2) ** 2 / 2
def back_out(t):
    c1 = 1.70158; c3 = c1 + 1; t = min(1, max(0, t)) - 1
    return 1 + c3 * t ** 3 + c1 * t * t
def frame(frames, k): return frames[max(0, min(len(frames) - 1, int(k * len(frames))))]

# ------------------------------------------------------------------ the floor under it all
bg = Image.new("RGBA", (W, H), (38, 30, 44, 255))
bd = ImageDraw.Draw(bg)
for y in range(0, H, 24):
    for x in range(0, W, 24):
        if (x // 24 + y // 24) % 2: bd.rectangle([x, y, x + 23, y + 23], fill=(44, 35, 50, 255))
for i in range(40):
    x, y = random.randrange(W), random.randrange(H)
    bd.rectangle([x, y, x + 6, y + 6], fill=(120, 30, 40, 255))

# ------------------------------------------------------------------ the timeline
state = dict(backdrop=0.0, env_pos=(0, 0), env_rot=0.0, env_sx=1.0, env_sy=1.0, env_alpha=1.0, env_frame=F["fe_big"][0],
             show_big=True, show_flap=False, show_front=False, aura_a=0.0, aura_s=1.0, color=JADE,
             raysA_a=0.0, raysA_rot=0.0, raysA_s=1.0, raysB_a=0.0, raysB_rot=0.0, raysB_s=1.0, flash=None, flash_a=0.0,
             stage_off=(0, 0), stage_s=1.0, scroll=False, scroll_pos=(0, 0), scroll_w=0.0, banner=False, banner_sy=1.0,
             bursts=[], rewards=[], coins=None, prompt=None, prompt_text="Click to continue", click=None, fade=1.0, parts=[])
frames_out = []
t_global = 0.0
sounds = []
def sfx(name, pitch=1.0): sounds.append((t_global, name, pitch))
state['light_y'] = 0.0

def emit():
    canvas = bg.copy()
    # the backdrop
    ov = Image.new("RGBA", (W, H), (10, 5, 15, int(255 * 0.82 * state["backdrop"])))
    canvas.alpha_composite(ov)
    ox, oy = state["stage_off"]
    so = state["stage_s"]
    c = state["color"]
    ly = state["light_y"]
    draw(canvas, F["fe_rays"][0], (ox, oy + ly), 6, rot=state["raysA_rot"], sx=state["raysA_s"] * so, sy=state["raysA_s"] * so, alpha=state["raysA_a"], color=c)
    draw(canvas, F["fe_rays"][0], (ox, oy + ly), 4, rot=state["raysB_rot"], sx=state["raysB_s"] * so, sy=state["raysB_s"] * so, alpha=state["raysB_a"], color=tuple(int(v + (255 - v) * 0.4) for v in c))
    draw(canvas, F["fe_aura"][0], (ox, oy + ly), 8, sx=state["aura_s"] * so, sy=state["aura_s"] * so, alpha=state["aura_a"], color=c)
    ex, ey = state["env_pos"]
    ea = state["env_alpha"]
    kw = dict(rot=state["env_rot"], sx=state["env_sx"] * so, sy=state["env_sy"] * so, alpha=ea)
    if state["show_big"]: draw(canvas, state["env_frame"], (ox + ex, oy + ey), BIG, **kw)
    if state["show_flap"]: draw(canvas, state["env_frame"], (ox + ex, oy + ey), BIG, **kw)
    scroll_inside = state["scroll"] and state["show_front"]
    if state["scroll"]: draw_scroll(canvas)
    if state["show_front"]: draw(canvas, F["fe_front"][0], (ox + ex, oy + ey), BIG, **kw)
    for b in state["bursts"]:
        fr = int(b["t"] * 25)
        if fr < len(b["frames"]): draw(canvas, b["frames"][fr], b["at"], b["k"])
    for p in state["parts"]:
        if p["img"] is None:
            x = int(W / 2 + p["x"] * S); y = int(H / 2 - p["y"] * S)
            a = 1 - max(0, (p["age"] / p["life"] - 0.7) / 0.3)
            sq = Image.new("RGBA", (p["w"], p["h"]), p["col"] + (int(255 * a),))
            sq = sq.rotate(p["rot"], expand=True)
            paste_clip(canvas, sq, x, y)
        else:
            a = 1 - max(0, (p["age"] / p["life"] - 0.7) / 0.3)
            im = p["img"][min(len(p["img"]) - 1, int(p["age"] / p["life"] * len(p["img"])))]
            draw(canvas, im, (p["x"], p["y"]), p["k"], rot=p["rot"], alpha=a, color=p.get("col"))
    if state["flash"] is not None and state["flash_a"] > 0:
        col = tuple(int(v + (255 - v) * 0.35) for v in state["flash"])
        canvas.alpha_composite(Image.new("RGBA", (W, H), col + (int(255 * state["flash_a"]),)))
    if state["prompt"] is not None:
        opening = state["prompt_text"] == "Click to open"
        text(canvas, state["prompt_text"], (0, -72 * 5 * 0.5 - 70) if opening else (0, -470), font_open if opening else font_prompt, (255, 242, 216, int(255 * state["prompt"])))
    if state["click"] is not None:
        # the viewer's click, shown as a ring opening where the pointer is (this is a preview)
        age = state["click"]
        if age < 0.35:
            d = ImageDraw.Draw(canvas)
            cx, cy = W / 2 + 60 * S, H / 2 + 40 * S
            r = 6 + age * 90
            d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(255, 255, 255, int(255 * (1 - age / 0.35))), width=3)
    if state["fade"] < 1:
        canvas = Image.blend(bg, canvas, state["fade"])
    frames_out.append(canvas.convert("RGB"))

def draw_scroll(canvas):
    sx, sy = state["scroll_pos"]
    ox, oy = state["stage_off"]
    sx += ox; sy += oy
    w = state["scroll_w"]
    paper = scaled(F["fe_scroll"][0], SMALL)
    ww = int(w * S)
    if ww > 0:
        crop = paper.crop((paper.width // 2 - ww // 2, 0, paper.width // 2 - ww // 2 + ww, paper.height))
        draw_img(canvas, crop, (sx, sy))
        for r in state["rewards"]:
            draw_reward(canvas, r, sx, sy)
    half = w * 0.5 + 10 * 5 * 0.5 - 5 * 2
    half = max(half, 10 * 5 * 0.5)
    draw(canvas, F["fe_roller"][0], (sx - half, sy), SMALL)
    draw(canvas, F["fe_roller"][0], (sx + half, sy), SMALL)
    if state["banner"]:
        by = sy + 52 * 5 * 0.5 + 16 * 5 * 0.5 + 6
        draw(canvas, F["fe_banner"][0], (sx, by), SMALL, sy=state["banner_sy"], color=state["color"])
        name = {"common": "COMMON", "rare": "RARE", "legendary": "LEGENDARY"}[rarity]
        text(canvas, name, (sx, by + 3), font_big, (255, 248, 204) if rarity == "legendary" else (255, 255, 255))
    if state["coins"] is not None:
        cy = sy - 52 * 5 * 0.5 - 44
        draw(canvas, frame(F["fe_coin"], (t_global * 1.5) % 1), (sx - 48, cy), SMALL)
        text(canvas, "+" + str(state["coins"]), (sx - 16, cy), font_big, (255, 224, 128), anchor="lm")

def draw_img(canvas, img, ui_xy):
    x = int(W / 2 + ui_xy[0] * S - img.width / 2); y = int(H / 2 - ui_xy[1] * S - img.height / 2)
    paste_clip(canvas, img, x, y)

def draw_reward(canvas, r, sx, sy):
    k = r["pop"]
    at = (sx + r["at"][0], sy + r["at"][1])
    if r["evo"]:
        fr = frame(F["fe_slot_evo"], (r["age"] * 12 / 4) % 1)
        draw(canvas, fr, (at[0], at[1] + 4 * 5), SMALL, sx=k, sy=k)
    else:
        draw(canvas, F["fe_slot"][0], at, SMALL, sx=k, sy=k)
    ic = r["icon"][int(r["age"] * 8) % len(r["icon"])]
    fit = min(1, 18 / max(ic.width, ic.height))
    draw(canvas, ic, at, SMALL, sx=k * fit, sy=k * fit, alpha=min(1, r["age"] / 0.2 * 1.5))
    if k > 0.6:
        text(canvas, r["label"], (at[0], at[1] - 26 * 5 * 0.5 - 18), font_small,
             (255, 184, 76) if r["evo"] else (76, 36, 26), outline=(30, 12, 58) if r["evo"] else (236, 216, 176))

def step_world(dt):
    global t_global
    t_global += dt
    for b in state["bursts"]: b["t"] += dt
    state["bursts"] = [b for b in state["bursts"] if b["t"] * 25 < len(b["frames"])]
    for p in state["parts"]:
        p["age"] += dt
        p["vy"] -= p["g"] * dt
        p["x"] += p["vx"] * dt; p["y"] += p["vy"] * dt
        p["rot"] += p["spin"] * dt
    state["parts"] = [p for p in state["parts"] if p["age"] < p["life"]]
    for r in state["rewards"]:
        r["age"] += dt
        r["pop"] = back_out(min(1, r["age"] / 0.2))
    if state.get("spinning"):
        state["raysA_rot"] += dt * 14; state["raysB_rot"] -= dt * 22

def tick(seconds, fn=None):
    n = max(1, round(seconds * FPS))
    for i in range(n):
        k = (i + 1) / n
        if fn: fn(k)
        step_world(1 / FPS)
        emit()

def mote(color, x, y, speed=1.0):
    state["parts"].append(dict(img=F["fe_mote"], k=SMALL, x=x, y=y, vx=random.uniform(-40, 40) * speed, vy=random.uniform(80, 220) * speed,
                               g=-30, life=random.uniform(0.6, 1.1), age=0, rot=0, spin=0, col=tuple(int(v + (255 - v) * 0.3) for v in color)))

def burst(at, k):
    fr = F["fe_burst_" + rarity]
    state["bursts"].append(dict(frames=fr, at=at, k=k, t=0.0))

def confetti(at):
    n = {0: 24, 1: 38, 2: 60}[RANK]
    reds = [(208, 40, 56), (255, 106, 90), (126, 20, 38)]; golds = [(248, 208, 104), (255, 240, 176), (200, 136, 48)]
    for i in range(n):
        coin = RANK == 2 and i % 5 == 0
        a = math.radians(random.uniform(20, 160)); v = random.uniform(350, 950) * (1.25 if RANK == 2 else 1)
        p = dict(x=at[0] + random.uniform(-20, 20), y=at[1] + random.uniform(-20, 20), vx=math.cos(a) * v, vy=math.sin(a) * v, g=1400,
                 spin=random.uniform(-720, 720), life=random.uniform(1, 1.7), age=0, rot=0)
        if coin: p.update(img=F["fe_coin"], k=1)
        else:
            s = int(4 * S * random.choice([1, 2]))
            p.update(img=None, w=max(2, s * random.choice([1, 2])), h=max(2, s), col=(reds if i % 2 == 0 else golds)[i % 3])
        state["parts"].append(p)

# ---- in (the envelope was just walked into)
sfx("fe_pickup", 1.0)
def k_in(k):
    state["backdrop"] = min(1, k * 2)
    state["env_pos"] = (0, 700 + (0 - 700) * back_out(k))
    state["aura_a"] = 0.25 * k
tick(0.38, k_in)
def k_squash(k):
    s = math.sin(k * math.pi) * 0.12 * (1 - k)
    state["env_sx"], state["env_sy"] = 1 + s, 1 - s
tick(0.16, k_squash)
sfx("fe_shake", 0.9)
state["env_sx"] = state["env_sy"] = 1

# ---- waiting to be opened: floating, the light breathing, a twitch now and then, a hum
UNOPENED = (255, 230, 184)
state["prompt_text"] = "Click to open"
sfx("fe_idle", 1.0)
wait = 2.6
twitches = [0.9, 2.1]
tw = {"t": -1.0}
def k_idle(k):
    tt = k * wait
    breathe = 0.5 + 0.5 * math.sin(tt * 2.4)
    state["color"] = UNOPENED
    state["aura_a"] = 0.16 + 0.1 * breathe
    state["aura_s"] = 0.85 + 0.08 * breathe
    y = round(math.sin(tt * 1.8) * 3) * 5 * 0.5
    x, rot = 0.0, 0.0
    for at in twitches:
        if at <= tt < at + 1 / FPS and tw["t"] < 0:
            tw["t"] = 0.0; sfx("fe_shake", random.uniform(1.15, 1.35))
    if tw["t"] >= 0:
        tw["t"] += 1 / FPS
        kk = 1 - tw["t"] / 0.2
        if kk <= 0: tw["t"] = -1.0
        else: x += random.uniform(-1, 1) * 5 * kk; y += random.uniform(-1, 1) * 5 * kk; rot = random.uniform(-4, 4) * kk
    state["env_pos"] = (x, y); state["env_rot"] = rot
    state["prompt"] = 0.55 + 0.45 * math.sin(tt * 4)
    if random.random() < 0.12: mote(UNOPENED, random.uniform(-140, 140), random.uniform(-140, 140), 0.5)
tick(wait, k_idle)
state["prompt"] = None
sfx("fe_idle_stop", 1.0)
sfx("fe_click", 1.0)
state["click"] = 0.0
state["env_pos"] = (0, 0); state["env_rot"] = 0
def k_click(k):
    s_ = math.sin(k * math.pi) * 0.1 * (1 - k)
    state["env_sx"], state["env_sy"] = 1 + s_, 1 - s_
    state["click"] = k * 0.12
tick(0.12, k_click)
state["env_sx"] = state["env_sy"] = 1

# ---- the charge
charge = {0: 1.4, 1: 2.0, 2: 2.8}[RANK]
sfx("fe_charge_" + rarity, 1.0)
shown = [JADE]
flags = dict(rare=False, leg=False)
def tier_up(color, strength, jolt):
    sfx("fe_tier", 1 + strength * 0.4)
    state["color"] = color
    shown[0] = color
    for i in range(18): mote(color, random.uniform(-60, 60), random.uniform(-60, 60), 1.3)
    # it doesn't hold the charge up: a jolt and a flash that die away while it goes on
    kick["jolt"] = jolt; kick["aura"] = 0.5; kick["flash"] = strength * 0.55
kick = dict(jolt=0.0, aura=0.0, flash=0.0)
jit = {"c": 0}
t = 0.0
n = round(charge * FPS)
for i in range(n):
    k = i / n
    if not flags["rare"] and RANK >= 1 and k > 0.4:
        flags["rare"] = True; tier_up(AZURE, 0.55, 14)
    if not flags["leg"] and RANK == 2 and k > 0.72:
        flags["leg"] = True; tier_up(GOLD, 0.85, 24)
    state["flash"] = shown[0]; state["flash_a"] = kick["flash"]; kick["flash"] = max(0.0, kick["flash"] - 2.0 / FPS)
    if state["click"] is not None: state["click"] += 1 / FPS
    state["env_frame"] = frame(F["fe_big"], min(0.999, k * 1.08))
    amp = (1 + 6 * k * k) * (1.6 if flags["leg"] else 1.25 if flags["rare"] else 1) + kick["jolt"]
    kick["jolt"] = max(0.0, kick["jolt"] - 100.0 / FPS)
    state["env_pos"] = (random.uniform(-1, 1) * amp, random.uniform(-1, 1) * amp)
    state["env_rot"] = random.uniform(-1, 1) * (1 + 5 * k)
    pulse = 1 + 0.06 * math.sin(i / FPS * (8 + 18 * k))
    state["aura_a"] = 0.18 + 0.32 * k
    mix_ = min(1, k * 4)
    state["color"] = tuple(int(UNOPENED[c] + (shown[0][c] - UNOPENED[c]) * mix_) for c in range(3))
    state["aura_s"] = (0.8 + 0.55 * k + kick["aura"]) * pulse
    kick["aura"] = max(0.0, kick["aura"] - 2.5 / FPS)
    if flags["rare"]:
        state["raysA_a"] = 0.15 + 0.25 * k
        state["raysA_rot"] += (70 if flags["leg"] else 30) / FPS
    for _ in range(2):
        if random.random() < 0.15 + 0.75 * k: mote(shown[0], random.uniform(-120, 120) + state["env_pos"][0], random.uniform(-120, 120))
    step_world(1 / FPS); emit()
state["env_pos"] = (0, 0); state["env_rot"] = 0; state["flash_a"] = 0; state["click"] = None

# ---- the burst
state["color"] = COLOR[rarity]
state["show_big"] = False; state["show_flap"] = True
sfx("fe_open"); sfx("fe_reveal_" + rarity)
flash_len = 0.35
burst((0, 90), 6)
if RANK == 2: burst_later = 0.16
confetti((0, 90))
state["raysA_a"], state["raysB_a"] = 0.75, 0.5
state["spinning"] = True
fired = {"b2": False}
def k_open(k):
    tt = k * 0.42
    state["env_frame"] = frame(F["fe_flap"], k)
    state["stage_s"] = 1 + 0.12 * (1 - ease_out(k))
    state["raysA_s"] = 0.5 + 0.6 * ease_out(k); state["raysB_s"] = 0.3 + 0.7 * ease_out(k)
    state["aura_s"] = 1.6 + (1.2 - 1.6) * k
    j = (1 - k) * (16 if RANK == 2 else 8)
    state["stage_off"] = (random.uniform(-1, 1) * j, random.uniform(-1, 1) * j)
    state["flash"] = COLOR[rarity]; state["flash_a"] = max(0, (0.7 if RANK == 2 else 0.45) * (1 - tt / flash_len))
    if RANK == 2 and not fired["b2"] and tt >= 0.16:
        fired["b2"] = True; burst((0, 90), 8)
tick(0.42, k_open)
state["stage_s"] = 1; state["stage_off"] = (0, 0); state["flash_a"] = 0
state["env_frame"] = F["fe_flap"][-1]

# ---- the scroll rises from the pocket
state["show_front"] = True; state["scroll"] = True; state["scroll_w"] = 8 * 5
hinge = (36 - 18) * 5
def k_rise(k):
    y0, y1 = hinge - 150, hinge + 190
    state["scroll_pos"] = (0, y0 + (y1 - y0) * ease_out(k))
tick(0.34, k_rise)
fx, fy = state["scroll_pos"]
def k_lift(k):
    e = ease_io(k)
    state["scroll_pos"] = (fx, fy + (110 - fy) * e)
    state["env_pos"] = (0, -620 * e * e)
    state["env_alpha"] = 1 - e
    state["aura_a"] = 0.5 + (0.2 - 0.5) * k
    state["light_y"] = 110 * k
tick(0.36, k_lift)
sfx("fe_shake", 1.3)
state["show_flap"] = state["show_front"] = False
def k_unroll(k):
    state["scroll_w"] = 8 * 5 + (168 * 5 - 8 * 5) * ease_out(k)
tick(0.4, k_unroll)
state["banner"] = True
def k_banner(k):
    s = math.sin(k * math.pi) * 0.25 * (1 - k)
    state["banner_sy"] = 1 - s
tick(0.2, k_banner)
state["banner_sy"] = 1

# ---- the rewards
rewards = {
    0: [("dragon", False, "Lv 5")],
    1: [("sword_evo", True, "EVOLVED"), ("dragon", False, "Lv 6"), ("ink", False, "Lv 4")],
    2: [("sword_evo", True, "EVOLVED"), ("dragon", False, "Lv 6"), ("ink", False, "Lv 4"), ("dragon", False, "Lv 7"), ("ice", False, "Lv 3")],
}[RANK]
gap, slot = 8, 26 * 5
row = len(rewards) * slot + (len(rewards) - 1) * gap
for i, (ic, evo, label) in enumerate(rewards):
    at = (-row / 2 + slot / 2 + i * (slot + gap), 8)
    sx, sy = state["scroll_pos"]
    state["bursts"].append(dict(frames=F["fe_burst_" + rarity], at=(sx + at[0], sy + at[1]), k=2, t=0.0))
    state["rewards"].append(dict(icon=ICON[ic], evo=evo, label=label, at=at, age=0.0, pop=0.0))
    sfx("fe_reward", 1 + i * 0.08)
    if evo: sfx("fe_reveal_legendary", 1.1)
    wait = 0.55 if evo else 0.32 if RANK == 2 else 0.27
    if evo:
        base = state["scroll_pos"]
        def k_evo(k, base=base):
            state["flash"] = (255, 128, 50); state["flash_a"] = 0.4 * max(0, 1 - k * wait / 0.3)
            j = 14 * max(0, 1 - k * wait / 0.3)
            state["scroll_pos"] = (base[0] + random.uniform(-1, 1) * j, base[1] + random.uniform(-1, 1) * j)
        tick(wait, k_evo)
        state["scroll_pos"] = base; state["flash_a"] = 0
    else:
        tick(wait)

# ---- the coins
coins = {0: 15, 1: 40, 2: 100}[RANK]
def k_coins(k):
    state["coins"] = round(coins * ease_out(k))
tick(0.5, k_coins)
state["coins"] = coins

# ---- waiting
state["prompt_text"] = "Click to continue"
def k_wait(k):
    state["prompt"] = 0.55 + 0.45 * math.sin(t_global * 4)
    if random.random() < 0.5: mote(COLOR[rarity], random.uniform(-400, 400), random.uniform(-100, 200), 0.35)
tick(1.4, k_wait)
def k_out(k):
    state["fade"] = 1 - k
tick(0.22, k_out)

out = sys.argv[2] if len(sys.argv) > 2 else f"envelope_{rarity}.gif"
pal_frames = [f.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) for f in frames_out]
pal_frames[0].save(out, save_all=True, append_images=pal_frames[1:], duration=int(1000 / FPS), loop=0, optimize=True, disposal=1)
print(out, len(frames_out), "frames", os.path.getsize(out) // 1024, "KB")

# ---- the same with its sound, as an mp4: the clips mixed where the code plays them
import wave, subprocess, numpy as np, imageio_ffmpeg
SND = ROOT + "/Assets/### Different Engine/Sounds/Fortune/"
SR = 48000
total = np.zeros((int((len(frames_out) / FPS + 3.5) * SR), 2))
stops = {name[:-5]: at for at, name, _ in sounds if name.endswith("_stop")}
for at, name, pitch in sounds:
    if name.endswith("_stop"): continue
    w = wave.open(SND + name + ".wav"); x = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(float) / 32768
    x = x.reshape(-1, w.getnchannels())
    if name in stops:
        # a loop: repeated until it's stopped, a short fade at the end
        n = int((stops[name] - at) * SR)
        x = np.tile(x, (n // len(x) + 1, 1))[:n]
        f = min(n, int(0.03 * SR)); x[-f:] *= np.linspace(1, 0, f)[:, None]
    if x.shape[1] == 1: x = np.repeat(x, 2, axis=1)
    if pitch != 1.0:
        n = int(len(x) / pitch)
        x = np.stack([np.interp(np.arange(n) * pitch, np.arange(len(x)), x[:, c]) for c in range(2)], axis=1)
    st = int(at * SR)
    total[st:st + len(x)] += x[:max(0, len(total) - st)]
total = total[:int(len(frames_out) / FPS * SR) + SR]
total *= 0.8 / max(1e-6, np.abs(total).max())
wav = out.replace(".gif", ".wav")
with wave.open(wav, "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes(np.round(total * 32767).astype("<i2").tobytes())
mp4 = out.replace(".gif", ".mp4")
ff = imageio_ffmpeg.get_ffmpeg_exe()
proc = subprocess.Popen([ff, "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                         "-i", wav, "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", "-tune", "animation", "-c:a", "aac", "-b:a", "192k",
                         "-shortest", mp4], stdin=subprocess.PIPE)
for f in frames_out + [frames_out[-1]] * FPS: proc.stdin.write(f.tobytes())
proc.stdin.close(); proc.wait()
print(mp4, os.path.getsize(mp4) // 1024, "KB")
