"""Huangquan Road's spawn timeline, recast with its own dead. the beats keep their timing and their
numbers (the run's pacing, tuned in playtests); what walks the road changes:

  0:00  wandering souls, in crawling crowds of 14 to 24
  1:30  paper servants join, dashing in knots of three to five
  3:00  spider lilies spring up on the screen
  4:30  the hell money burners take the edges
  7:30  the rare soul lantern, hastening the rest
  9:00  jiangshi, the heavies, carried down the road with the rest
  12:00 on: the same cast, the servants and lilies thicker

the ones that shoot (lilies, burners, lanterns) come sparingly until 12:00, the slack going to
the servants and the jiangshi that only close in, so the middle of a run pressures without
turning into a bullet hell before the build can take it; 12:00 is halfway back, and from 13:30
they come as thick as before. their archetypes' Max Alive Early holds them down until 12:00 too
(prefabs.py)

a soul's pick brings a whole crowd while the others come one to a few, so the weights are per
pick: with souls at 1 against the rest, about two in three of the bodies on the road are souls.
the events are recast too: souls drifting across, a ring of paper servants all dashing in at once,
a paper funeral procession sweeping the screen, elite burners and an elite lantern.

    python timeline.py      (after prefabs.py)
"""
import os
import re

from prefabs import ENGINE, ARCHETYPES, guid_of

TIMELINE = os.path.join(ENGINE, "Data", "Spawning", "HuangquanTimeline.asset")
JIANGSHI = "187c5bf6e8c4b3f43a3d9d6137a16095"


def arch(name):
    return guid_of(os.path.join(ARCHETYPES, name + ".asset.meta"))


def main():
    soul, lily, servant = arch("Wandering Soul"), arch("Spider Lily Demon"), arch("Paper Servant")
    burner, lantern = arch("Hell Money Burner"), arch("Soul Guiding Lantern")

    ROSTER = [
        ("Souls on the road", [(soul, 1)]),
        ("Paper servants", [(soul, 1), (servant, 1.2)]),
        ("The lilies bloom", [(soul, 1), (servant, 1.5), (lily, 0.8)]),
        ("Spirit money burns", [(soul, 1), (servant, 1.9), (lily, 0.9), (burner, 0.4)]),
        ("Lanterns on the road", [(soul, 1), (servant, 2), (lily, 0.9), (burner, 0.5), (lantern, 0.12)]),
        ("Coffin bearers", [(soul, 1.1), (servant, 2.3), (lily, 0.9), (burner, 0.45), (lantern, 0.12), (JIANGSHI, 1.2)]),
        ("Ghost festival", [(soul, 1.2), (servant, 2.2), (lily, 1.5), (burner, 0.8), (lantern, 0.25), (JIANGSHI, 1.1)]),
        ("The river rises", [(soul, 1.2), (servant, 2.2), (lily, 2.4), (burner, 1.2), (lantern, 0.35), (JIANGSHI, 1)]),
        ("The guardians stir", [(soul, 1.3), (servant, 2.4), (lily, 2.6), (burner, 1.3), (lantern, 0.4), (JIANGSHI, 1.2)]),
        ("Paper and fire", [(soul, 1.3), (servant, 2.8), (lily, 2.6), (burner, 1.4), (lantern, 0.4), (JIANGSHI, 1.2)]),
        ("Endless procession", [(soul, 1.4), (servant, 3), (lily, 2.8), (burner, 1.4), (lantern, 0.45), (JIANGSHI, 1.3)]),
        ("Forgetting", [(soul, 1.4), (servant, 3), (lily, 3), (burner, 1.5), (lantern, 0.45), (JIANGSHI, 1.4)]),
        ("The sealed bridge", [(soul, 1.5), (servant, 3.2), (lily, 3), (burner, 1.5), (lantern, 0.5), (JIANGSHI, 1.5)]),
        ("The Yellow Springs", [(soul, 1.5), (servant, 3.4), (lily, 3.2), (burner, 1.6), (lantern, 0.5), (JIANGSHI, 1.6)]),
    ]

    # old events' archetypes to the road's own, and their names
    EVENT = {
        "a6c44b6c397bceb489f23c104d9c121c": soul,            # ghost fire
        "323e5f2d423cbb649a9be83bf08aa13d": servant,         # hungry ghost
        "84107f1b74d690b408e7810cdc3ac5c3": servant,         # fox spirit
        "2414b3e704988664dafe7a81e0d0da74": soul,            # nian
    }
    NAMES = {
        "The hungry circle": "Paper servants close in", "Elite: Jiangshi": "Elite: Money Burner",
        "Fox fire!": "Paper streamers", "Will-o-wisps close in": "The souls close in", "Elite hunger": "Elite burners",
        "Funeral procession": "Paper funeral procession", "Elite: Nian": "Elite: Soul Lantern", "Fox run": "Paper run",
        "The dead surround you": "The dead surround you", "Elite foxes": "Elite servants", "Nian stampede!": "Souls stampede!",
    }

    with open(TIMELINE) as f:
        y = f.read()
    head, rest = y.split("  beats:\n", 1)
    beats, events = rest.split("  events:\n", 1)

    # each beat keeps its numbers; its label and enemies are recast
    blocks = re.split(r"(?=  - label: )", beats)
    blocks = [b for b in blocks if b.strip()]
    out = []
    for i, b in enumerate(blocks):
        label, picks = ROSTER[min(i, len(ROSTER) - 1)]
        b = re.sub(r"  - label: '[^']*'", f"  - label: '{label}'", b, count=1)
        b = b[:b.index("    enemies:\n")] + "    enemies:\n" + "".join(
            f"    - archetype: {{fileID: 11400000, guid: {g}, type: 2}}\n      weight: {w}\n" for g, w in picks)
        out.append(b)
    beats = "".join(out)

    def recast(m):
        block = m.group(0)
        for old, new in EVENT.items():
            block = block.replace(old, new)
        name = re.search(r"label: '([^']*)'", block).group(1)
        if "Elite" in name and "Jiangshi" in name or name == "Elite hunger":
            block = re.sub(r"guid: [0-9a-f]+, type: 2", f"guid: {burner}, type: 2", block)
        if name == "Elite: Nian":
            block = re.sub(r"guid: [0-9a-f]+, type: 2", f"guid: {lantern}, type: 2", block)
        if name == "Funeral procession":
            block = re.sub(r"guid: [0-9a-f]+, type: 2", f"guid: {servant}, type: 2", block)
        if name == "The dead surround you":
            block = re.sub(r"guid: [0-9a-f]+, type: 2", f"guid: {soul}, type: 2", block)
        return block.replace(f"label: '{name}'", f"label: '{NAMES.get(name, name)}'")

    events = re.sub(r"  - label: '[^']*'\n(?:    [^\n]*\n)+", recast, events)
    with open(TIMELINE, "w") as f:
        f.write(head + "  beats:\n" + beats + "  events:\n" + events)
    print(f"{len(out)} beats, events recast")


if __name__ == "__main__":
    main()
