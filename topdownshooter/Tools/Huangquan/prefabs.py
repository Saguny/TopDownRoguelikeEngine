"""Huangquan Road's enemies as Unity assets: a prefab and an archetype for each, written as YAML so
they can be made (and remade) without the editor.

every prefab starts from the Ghost Fire's (the Rigidbody2D, the damage scaler, contact damage,
movement, awareness, collider, health, sprite renderer and audio source every enemy has), loses its
Animator (HqEnemyArt draws them from Resources/Huangquan instead) and gains its art and behaviour;
the two guardians also get a BossMarker (the edge-of-screen medallion, the horde steering round
them) and a health bar. the archetypes hold the numbers the spawner works with: health, speed and
damage before the run's curve, what they take from each kind of weapon, and how they come
(SpawnPattern: Cluster, OnScreen, Burst, Edge, Rare).

    python prefabs.py

a prefab or archetype that's already there keeps its guid, so references to it survive a remake.
"""
import os
import re
import uuid

HERE = os.path.dirname(os.path.abspath(__file__))
ENGINE = os.path.join(HERE, "..", "..", "Assets", "### Different Engine")
TEMPLATE = os.path.join(ENGINE, "Prefabs", "Enemies", "Ghost Fire.prefab")
PREFABS = os.path.join(ENGINE, "Prefabs", "Enemies", "Huangquan")
ARCHETYPES = os.path.join(ENGINE, "Data", "Curves", "Archetypesd", "Huangquan")
SCRIPTS = os.path.join(ENGINE, "Scripting")
SFX = os.path.join(ENGINE, "Resources", "Sfx")

ROOT_ID = "1048871670121432657"          # the template's GameObject
ANIMATOR_ID = "4637487224447301170"
ARCHETYPE_SCRIPT = "48e0b7cb409370741a046cd96fa59353"
PATTERNS = {"Horde": 0, "Cluster": 1, "OnScreen": 2, "Burst": 3, "Edge": 4, "Rare": 5}


def guid_of(meta):
    with open(meta) as f:
        return re.search(r"guid: ([0-9a-f]+)", f.read()).group(1)


def script(name, folder="Enemy/Huangquan"):
    return guid_of(os.path.join(SCRIPTS, folder, name + ".cs.meta"))


def sound(name):
    return guid_of(os.path.join(SFX, name + ".wav.meta"))


def keep_or_new(meta):
    return guid_of(meta) if os.path.exists(meta) else uuid.uuid4().hex


def write_meta(path, guid, kind):
    if os.path.exists(path + ".meta"):
        return
    if kind == "prefab":
        body = f"fileFormatVersion: 2\nguid: {guid}\nPrefabImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif kind == "asset":
        body = f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        body = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    with open(path + ".meta", "w") as f:
        f.write(body)


def behaviour(file_id, guid, fields):
    lines = [f"--- !u!114 &{file_id}", "MonoBehaviour:", "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
             "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}", f"  m_GameObject: {{fileID: {ROOT_ID}}}",
             "  m_Enabled: 1", "  m_EditorHideFlags: 0", f"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}",
             "  m_Name: ", "  m_EditorClassIdentifier: "]
    lines += [f"  {k}: {v}" for k, v in fields.items()]
    return "\n".join(lines) + "\n"


# name, look, behaviour script, prefab speed, collider (radius, offset x, y), death sound, boss
ENEMIES = [
    dict(name="Wandering Soul", look="wandering_soul", script=None, speed=2, collider=(0.2, 0.05, 0.1), death="hq_soul_death",
         art=dict(fps=8, stateFps=14, deathScale=1),
         arch=dict(cost=1, weight=0.7, baseHealth=4, baseSpeed=0.42, baseDamage=8, armour=0, knockbackResist=0,
                   physicalTaken=0.85, magicalTaken=1.2, pattern="Cluster", group=(10, 16), maxAlive=0)),
    dict(name="Spider Lily Demon", look="spider_lily", script="SpiderLilyDemon", speed=0, collider=(0.34, 0, 0.1), death="hq_lily_death",
         art=dict(fps=7, stateFps=14, deathScale=1),
         arch=dict(cost=2, weight=0.35, baseHealth=9, baseSpeed=0, baseDamage=10, armour=0, knockbackResist=1,
                   physicalTaken=1.3, magicalTaken=0.9, pattern="OnScreen", group=(1, 1), maxAlive=10, maxAliveEarly=5, earlyUntilMinute=12)),
    dict(name="Paper Servant", look="paper_servant", script="PaperServant", speed=2, collider=(0.24, 0, -0.05), death="hq_paper_death",
         art=dict(fps=8, stateFps=14, deathScale=1),
         arch=dict(cost=1, weight=0.4, baseHealth=4, baseSpeed=1, baseDamage=12, armour=0, knockbackResist=0.2,
                   physicalTaken=1.2, magicalTaken=1.3, pattern="Burst", group=(3, 5), maxAlive=24)),
    dict(name="Hell Money Burner", look="hell_money_burner", script="HellMoneyBurner", speed=2, collider=(0.34, 0, -0.1), death="hq_burner_death",
         art=dict(fps=7, stateFps=12, deathScale=1),
         arch=dict(cost=3, weight=0.22, baseHealth=16, baseSpeed=0.8, baseDamage=10, armour=0.1, knockbackResist=0.5,
                   physicalTaken=1.0, magicalTaken=1.0, pattern="Edge", group=(1, 1), maxAlive=8, maxAliveEarly=4, earlyUntilMinute=12)),
    dict(name="Soul Guiding Lantern", look="soul_lantern", script="SoulLantern", speed=2, collider=(0.3, 0, 0.1), death="hq_soul_death",
         art=dict(fps=7, stateFps=14, deathScale=1), wen=(4, 8, 5),
         arch=dict(cost=6, weight=0.07, baseHealth=60, baseSpeed=0.55, baseDamage=5, armour=0, knockbackResist=0.8,
                   physicalTaken=1.0, magicalTaken=0.85, pattern="Rare", group=(1, 1), maxAlive=1)),
    dict(name="Ox-Head", look="bull_head", script="BullHead", speed=3, collider=(0.72, 0, -0.25), death="hq_bull_death", boss=True,
         art=dict(fps=8, stateFps=12, deathScale=1), bar=dict(width=2.4, height=2.2),
         arch=dict(cost=10, weight=1, baseHealth=900, baseSpeed=1, baseDamage=30, armour=0.15, knockbackResist=1,
                   physicalTaken=1.0, magicalTaken=1.0, pattern="Horde", group=(1, 1), maxAlive=0, contactTickInterval=1.0)),
    dict(name="Horse-Face", look="horse_face", script="HorseFace", speed=2.7, collider=(0.6, 0, 0.05), death="hq_horse_death", boss=True,
         art=dict(fps=7, stateFps=10, deathScale=1), bar=dict(width=2.2, height=2.2),
         arch=dict(cost=10, weight=1, baseHealth=700, baseSpeed=1, baseDamage=20, armour=0.05, knockbackResist=1,
                   physicalTaken=1.0, magicalTaken=1.0, pattern="Horde", group=(1, 1), maxAlive=0, contactTickInterval=1.0)),
]


def make_prefab(e, arch_guid):
    with open(TEMPLATE) as f:
        y = f.read()
    # the Animator goes: HqEnemyArt draws it
    y = re.sub(r"--- !u!95 &" + ANIMATOR_ID + r"\nAnimator:.*?(?=\n--- )", "", y, flags=re.S)
    y = y.replace(f"  - component: {{fileID: {ANIMATOR_ID}}}\n", "")
    y = y.replace("m_Name: Ghost Fire", f"m_Name: {e['name']}")
    y = re.sub(r"archetype: \{fileID: 11400000, guid: [0-9a-f]+, type: 2\}", f"archetype: {{fileID: 11400000, guid: {arch_guid}, type: 2}}", y)
    y = re.sub(r"_speed: [0-9.]+", f"_speed: {e['speed']}", y)
    r, ox, oy = e["collider"]
    y = re.sub(r"m_Offset: \{x: [-0-9.]+, y: [-0-9.]+\}\n  m_Radius: [0-9.]+", f"m_Offset: {{x: {ox}, y: {oy}}}\n  m_Radius: {r}", y)
    y = re.sub(r"deathSound: \{fileID: 8300000, guid: [0-9a-f]+, type: 3\}", f"deathSound: {{fileID: 8300000, guid: {sound(e['death'])}, type: 3}}", y)
    y = re.sub(r"deathVolume: [0-9.]+", f"deathVolume: {0.9 if e.get('boss') else 0.5}", y)
    # the sprite's set by its art at run time
    y = re.sub(r"m_Sprite: \{fileID: -?\d+, guid: [0-9a-f]+, type: 3\}", "m_Sprite: {fileID: 0}", y)
    if "wen" in e:
        lo, hi, kill = e["wen"]
        y = re.sub(r"minWen: \d+", f"minWen: {lo}", y)
        y = re.sub(r"maxWen: \d+", f"maxWen: {hi}", y)
        y = re.sub(r"baseWenOnKill: \d+", f"baseWenOnKill: {kill}", y)
        y = re.sub(r"wenDropChance: [0-9.]+", "wenDropChance: 1", y)
    if e.get("boss"):
        y = re.sub(r"m_Mass: [0-9.]+", "m_Mass: 400", y)
        y = re.sub(r"tickInterval: [0-9.]+", "tickInterval: 1", y)

    added = []
    added.append(("2100000000000000001", behaviour("2100000000000000001", script("HqEnemyArt"),
                  dict(look=e["look"], fps=e["art"]["fps"], stateFps=e["art"]["stateFps"], deathScale=e["art"]["deathScale"]))))
    if e["script"]:
        added.append(("2100000000000000002", behaviour("2100000000000000002", script(e["script"]), {})))
    if e.get("boss"):
        added.append(("2100000000000000003", behaviour("2100000000000000003", script("BossMarker", "Game Engine"), {})))
        added.append(("2100000000000000004", behaviour("2100000000000000004", script("WorldHealthBar"),
                      dict(width=e["bar"]["width"], height=e["bar"]["height"], fill="{r: 0.8156863, g: 0.15686275, b: 0.21960784, a: 1}"))))
    for fid, _ in added:
        y = y.replace("  m_Layer: 11", f"  - component: {{fileID: {fid}}}\n  m_Layer: 11", 1)
    y = y.rstrip("\n") + "\n" + "".join(block for _, block in added)
    return y


def make_archetype(e, prefab_guid):
    a = e["arch"]
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:", "  m_ObjectHideFlags: 0",
             "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}",
             "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             f"  m_Script: {{fileID: 11500000, guid: {ARCHETYPE_SCRIPT}, type: 3}}", f"  m_Name: {e['name']}", "  m_EditorClassIdentifier: ",
             f"  prefab: {{fileID: {ROOT_ID}, guid: {prefab_guid}, type: 3}}",
             f"  cost: {a['cost']}", f"  weight: {a['weight']}", f"  baseHealth: {a['baseHealth']}", f"  baseSpeed: {a['baseSpeed']}",
             f"  baseDamage: {a['baseDamage']}", f"  armour: {a['armour']}", f"  knockbackResist: {a['knockbackResist']}",
             f"  physicalTaken: {a['physicalTaken']}", f"  magicalTaken: {a['magicalTaken']}",
             f"  pattern: {PATTERNS[a['pattern']]}", "  group:", f"    x: {a['group'][0]}", f"    y: {a['group'][1]}", f"  maxAlive: {a['maxAlive']}", f"  maxAliveEarly: {a.get('maxAliveEarly', 0)}", f"  earlyUntilMinute: {a.get('earlyUntilMinute', 0)}",
             f"  contactTickInterval: {a.get('contactTickInterval', 0.5)}"]
    return "\n".join(lines) + "\n"


def main():
    for folder in (PREFABS, ARCHETYPES):
        os.makedirs(folder, exist_ok=True)
        write_meta(folder, uuid.uuid4().hex, "folder")
    out = {}
    for e in ENEMIES:
        prefab = os.path.join(PREFABS, e["name"] + ".prefab")
        arch = os.path.join(ARCHETYPES, e["name"] + ".asset")
        pg, ag = keep_or_new(prefab + ".meta"), keep_or_new(arch + ".meta")
        with open(prefab, "w") as f:
            f.write(make_prefab(e, ag))
        with open(arch, "w") as f:
            f.write(make_archetype(e, pg))
        write_meta(prefab, pg, "prefab")
        write_meta(arch, ag, "asset")
        out[e["name"]] = (pg, ag)
        print(f"{e['name']:22s} prefab {pg}  archetype {ag}")
    return out


if __name__ == "__main__":
    main()
