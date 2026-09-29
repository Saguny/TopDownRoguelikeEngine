using UnityEngine;

// one of the empowered horde: from late in the run (SpawnTimeline.empoweredFromMinute, 24:00 by
// default) every ordinary enemy comes in as a stronger version of itself, the way an elite does,
// only without the envelope: a little bigger, several times as tough, harder hitting, steadier on
// its feet, and ringed in crimson so the change reads at a glance. by then most players are maxed
// out, and the horde has to keep up. the numbers are applied as it spawns (SpawnDirector); this
// is its mark and its outline
public class Empowered : SilhouetteOutline
{
    public const float Size = 1.15f, Health = 3f, Damage = 1.5f, KnockbackResist = 0.4f;

    public static readonly Color Tint = new Color32(0xff, 0x3c, 0x52, 0xff);
    protected override Color OutlineColor => Tint;
}
