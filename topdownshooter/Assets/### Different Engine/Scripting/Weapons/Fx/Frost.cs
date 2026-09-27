using UnityEngine;

// an enemy frozen solid: it stops where it stands, its animation stops with it, and a crust of ice
// sits over it until it thaws. Frost.Apply is the way in; a longer freeze already on it is kept.
// lives on the pooled enemy and switches itself off when it isn't frozen
[DisallowMultipleComponent]
public class Frost : MonoBehaviour
{
    private static readonly Color PlainIce = new Color(0.65f, 0.9f, 1f, 0.55f);

    private EnemyMovement move;
    private Animator animator;
    private SpriteRenderer body, crust;
    private Sprite[] frames;
    private float fps = 8f, until, animatorSpeed = 1f, born;
    private bool frozen;

    public bool Frozen => frozen && Time.time < until;

    public static bool IsFrozen(Component enemy) => enemy != null && enemy.TryGetComponent(out Frost f) && f.Frozen;

    // freezes the enemy for this long. ice: the crust's frames, drawn at the world's pixel size for
    // an enemy about 24 pixels across; empty draws a plain icy disc
    public static void Apply(EnemyHealth enemy, float seconds, Sprite[] ice = null, float iceFps = 8f)
    {
        if (enemy == null || !enemy.isActiveAndEnabled || enemy.Current <= 0f || seconds <= 0f) return;
        if (!enemy.TryGetComponent(out Frost f)) f = enemy.gameObject.AddComponent<Frost>();
        f.Freeze(seconds, ice, iceFps);
    }

    private void Awake()
    {
        move = GetComponent<EnemyMovement>();
        animator = GetComponent<Animator>();
        body = GetComponentInChildren<SpriteRenderer>();
        enabled = false;
    }

    private void Freeze(float seconds, Sprite[] ice, float iceFps)
    {
        until = Mathf.Max(until, Time.time + seconds);
        if (move != null) move.ApplySlow(0f, until - Time.time);
        if (ice != null && ice.Length > 0 && ice[0] != null) { frames = ice; fps = Mathf.Max(0.01f, iceFps); }

        if (!frozen)
        {
            frozen = true;
            born = Time.time;
            if (animator != null)
            {
                animatorSpeed = animator.speed;
                animator.speed = 0f;
            }
        }
        ShowCrust();
        enabled = true;
    }

    private void ShowCrust()
    {
        if (body == null) return;
        if (crust == null)
        {
            var go = new GameObject("Frost");
            go.transform.SetParent(body.transform, false);
            crust = go.AddComponent<SpriteRenderer>();
        }
        crust.sortingLayerID = body.sortingLayerID;
        crust.sortingOrder = body.sortingOrder + 1;
        bool art = frames != null && frames.Length > 0 && frames[0] != null;
        crust.sprite = art ? frames[0] : WeaponFx.Disc;
        crust.color = art ? Color.white : PlainIce;
        crust.enabled = true;

        // over the enemy's drawing, a little wider than it
        if (body.sprite != null && crust.sprite != null)
        {
            var b = body.sprite.bounds;
            float wanted = Mathf.Max(b.size.x, b.size.y * 0.8f) * 1.1f;
            float have = Mathf.Max(0.01f, crust.sprite.bounds.size.x);
            crust.transform.localPosition = b.center;
            crust.transform.localScale = Vector3.one * (wanted / have);
        }
    }

    private void Update()
    {
        if (Time.time >= until)
        {
            Thaw();
            return;
        }
        if (crust != null && frames != null && frames.Length > 1)
        {
            // the freeze plays through once and holds its last frame
            int i = Mathf.Min(frames.Length - 1, (int)((Time.time - born) * fps));
            crust.sprite = frames[i];
        }
        // the last moments: the ice flickers to say it's about to break
        if (crust != null) crust.enabled = until - Time.time > 0.4f || (int)(Time.time * 16f) % 2 == 0;
    }

    private void Thaw()
    {
        if (frozen && animator != null) animator.speed = animatorSpeed;
        frozen = false;
        until = 0f;
        if (crust != null) crust.enabled = false;
        enabled = false;
    }

    // back to the pool thawed
    private void OnDisable()
    {
        if (frozen) Thaw();
    }
}
