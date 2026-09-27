using UnityEngine;

// the character's weapon, worn on their back like the Cinnabar Ink Brush hangs off it: nobody holds
// a weapon in their hands any more, so the weapon a character starts with rides slung across their
// back instead, from the weapon's own Back Frames (Tools/VFX/weapons2, back_*). it rides the body
// sprite, so it turns with the player, and bobs with the idle and the walk
[DisallowMultipleComponent]
public class BackWeapon : MonoBehaviour
{
    // where on the character's 48x48 drawing the weapon's centre sits, in its pixels from the
    // drawing's centre (x right, y up): on the back, between the shoulder blades
    private static readonly Vector2 BackPoint = new Vector2(-6f, -1f);

    // the body's bob per frame of the character art (Frame_0-3 the idle, 4-7 the walk), in its
    // pixels, up positive: the same bob the character generator draws them with
    private static readonly int[] FrameBob = { 0, 0, -1, -1, 0, 1, 0, 1 };

    private SpriteRenderer body, worn;
    private Sprite[] frames;
    private float fps = 6f;

    // puts the weapon on the player's back; a weapon without back art leaves the back empty
    public static void Wear(GameObject player, WeaponData weapon)
    {
        if (player == null) return;
        if (!player.TryGetComponent(out BackWeapon back)) back = player.AddComponent<BackWeapon>();
        back.Show(weapon);
    }

    private void Show(WeaponData weapon)
    {
        if (body == null)
        {
            var health = GetComponent<PlayerHealth>();
            body = health != null && health.spriteRenderer != null ? health.spriteRenderer : GetComponentInChildren<SpriteRenderer>();
        }
        frames = weapon != null ? weapon.backFrames : null;
        fps = weapon != null ? Mathf.Max(0.1f, weapon.backFps) : 6f;
        bool any = body != null && frames != null && frames.Length > 0 && frames[0] != null;
        if (!any)
        {
            if (worn != null) worn.enabled = false;
            return;
        }

        if (worn == null)
        {
            var go = new GameObject("Back Weapon");
            go.transform.SetParent(body.transform, false);
            worn = go.AddComponent<SpriteRenderer>();
        }
        worn.enabled = true;
        worn.sharedMaterial = body.sharedMaterial;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (worn == null || !worn.enabled || body == null) return;

        // drawn over the body, the way a quiver on a character's back is
        worn.sortingLayerID = body.sortingLayerID;
        worn.sortingOrder = body.sortingOrder + 1;
        worn.sprite = frames[(int)(Time.time * fps) % frames.Length];
        worn.flipX = body.flipX;
        // hidden with the body, e.g. while the player is down and flattened
        worn.enabled = body.enabled;

        float ppu = body.sprite != null ? body.sprite.pixelsPerUnit : 33.88956f;
        var at = BackPoint + new Vector2(0f, Bob());
        if (body.flipX) at.x = -at.x;
        worn.transform.localPosition = at / ppu;
    }

    private int Bob()
    {
        var s = body.sprite;
        if (s == null || !s.name.StartsWith("Frame_") || !int.TryParse(s.name.Substring(6), out int i)) return 0;
        return i >= 0 && i < FrameBob.Length ? FrameBob[i] : 0;
    }
}
