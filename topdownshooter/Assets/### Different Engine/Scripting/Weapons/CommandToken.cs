using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// an ability taken from a level up like a weapon (it doesn't fill a weapon slot): once it has
// charged, its key (E) stamps seals onto the screen one at a time, big to small, then they explode into a shockwave that hits every enemy
// on screen, clears every enemy shot on it (the danmaku, the corpse fire, the embers: anything
// signed up to EnemyShots), and nothing new spawns for a moment after. with a Cast Animation prefab set, that
// prefab plays instead and says when the hit lands. CommandTokenHUD shows the charge
public class CommandToken : Weapon<CommandTokenData>
{
    private readonly List<GameObject> snapshot = new List<GameObject>(256);
    private readonly List<SpriteRenderer> row = new List<SpriteRenderer>();
    private readonly List<Sprite> sprites = new List<Sprite>();
    private float timer;
    private bool primed, casting;

    // the one the player holds, for the on-screen prompt
    public static CommandToken Current { get; private set; }

    public float CooldownSeconds => Data != null ? Cooldown(Data.At(Level).cooldown) : 1f;
    // 0 just used, 1 charged
    public float Charge => casting ? 0f : Mathf.Clamp01(timer / Mathf.Max(0.01f, CooldownSeconds));
    public bool Ready => !casting && Level > 0 && Charge >= 1f;
    public bool Casting => casting;
    public float SecondsLeft => Mathf.Max(0f, CooldownSeconds - timer);
    public KeyCode Key => Data != null ? Data.activationKey : KeyCode.E;
    public Sprite Icon => Asset != null ? Asset.icon : null;

    // fired as the key is pressed and the cast begins
    public static System.Action OnUsed;

    protected override void OnEnable()
    {
        base.OnEnable();
        Current = this;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Current == this) Current = null;
    }

    protected override void OnLevelChanged()
    {
        Current = this;
        CommandTokenHUD.Ensure();

        // it's ready shortly after it's picked, not a whole cooldown later
        if (primed || Data == null) return;
        primed = true;
        timer = Mathf.Max(0f, Cooldown(Data.At(Level).cooldown) - Data.firstShotDelay);
    }

    private void Update()
    {
        if (Data == null || Level <= 0 || casting) return;
        if (Time.timeScale <= 0f) return;   // paused, or the level up menu is open

        var lv = Data.At(Level);
        timer = Mathf.Min(timer + Time.deltaTime, Cooldown(lv.cooldown));
        if (timer < Cooldown(lv.cooldown) || !Input.GetKeyDown(Data.activationKey)) return;

        timer = 0f;
        StartCoroutine(Cast(lv));
    }

    private IEnumerator Cast(CommandTokenData.LevelStats lv)
    {
        casting = true;
        OnUsed?.Invoke();

        // the horde stops arriving while the seals go down
        if (SpawnDirector.Active != null) SpawnDirector.Active.PauseSpawning(Data.spawnPauseSeconds);
        var cam = Camera.main;
        row.Clear();

        if (Data.castAnimation != null && cam != null)
        {
            yield return PlayOwnAnimation(cam, lv);
            casting = false;
            yield break;
        }

        if (cam != null)
        {
            // the seals ride the camera, so the row stays put on screen while everything moves
            float half = (Data.sealCount - 1) * 0.5f;
            for (int i = 0; i < Data.sealCount; i++)
            {
                var sr = WeaponFx.Make(cam.transform, "Seal", SealSprite(i), WeaponFx.Square, Data.sealColor,
                    Data.sortingLayer, Data.sortingOrder + 1);
                sr.transform.position = RowPoint(cam, (i - half) * Data.sealSpacing);
                row.Add(sr);

                yield return Stamp(sr);
                Juice.Shake(Data.stampShake);
                if (i < Data.sealCount - 1 && Data.stampGap > 0f) yield return new WaitForSeconds(Data.stampGap);
            }

            if (Data.holdSeconds > 0f) yield return new WaitForSeconds(Data.holdSeconds);
        }

        Vector3 centre = cam != null ? RowPoint(cam, 0f) : transform.position;
        HitEverythingOnScreen(cam, lv);
        Juice.Shake(Data.shake);
        if (Data.explosionFx != null)
            Destroy(Instantiate(Data.explosionFx, centre, Quaternion.identity), Data.explosionFxSeconds);

        StartCoroutine(Shockwave(cam, centre));
        yield return Explode();
        casting = false;
    }

    // the hand-made version: the prefab's (0, 0) sits on the centre of the screen and rides the
    // camera. it runs its own animation; this only waits for its Hit event and for it to finish
    private IEnumerator PlayOwnAnimation(Camera cam, CommandTokenData.LevelStats lv)
    {
        var cast = Instantiate(Data.castAnimation, cam.transform);
        cast.transform.localPosition = new Vector3(0f, 0f, -cam.transform.position.z);
        cast.transform.localRotation = Quaternion.identity;
        cast.Play(() => HitEverythingOnScreen(cam, lv));

        while (cast != null) yield return null;
    }

    // a seal starts big and faint and shrinks into place
    private IEnumerator Stamp(SpriteRenderer sr)
    {
        float seconds = Mathf.Max(0.02f, Data.stampSeconds);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            float eased = 1f - (1f - k) * (1f - k);
            WeaponFx.Resize(sr, Data.sealSize * Mathf.Lerp(Data.stampStartScale, 1f, eased));
            WeaponFx.SetAlpha(sr, Mathf.Clamp01(k * 1.5f));
            yield return null;
        }
        WeaponFx.Resize(sr, Data.sealSize);
        WeaponFx.SetAlpha(sr, 1f);
    }

    // the seals flare up and fade as the shockwave leaves them
    private IEnumerator Explode()
    {
        float seconds = Mathf.Max(0.02f, Data.explodeSeconds);
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            foreach (var sr in row)
            {
                if (sr == null) continue;
                WeaponFx.Resize(sr, Data.sealSize * (1f + 0.6f * k));
                WeaponFx.SetAlpha(sr, 1f - k);
            }
            yield return null;
        }

        foreach (var sr in row)
            if (sr != null) Destroy(sr.gameObject);
        row.Clear();
    }

    private IEnumerator Shockwave(Camera cam, Vector3 centre)
    {
        var sr = WeaponFx.Make(Fx, "Shockwave", Data.shockwaveSprite, WeaponFx.Ring, Data.shockwaveColor,
            Data.sortingLayer, Data.sortingOrder);
        sr.transform.position = centre;

        // wide enough to sweep past the corners of the screen
        float reach = cam != null ? 2.4f * cam.orthographicSize * Mathf.Sqrt(1f + cam.aspect * cam.aspect) : 30f;
        float seconds = Mathf.Max(0.05f, Data.shockwaveSeconds);

        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            WeaponFx.Resize(sr, Mathf.Lerp(0.5f, reach, 1f - (1f - k) * (1f - k)));
            WeaponFx.SetAlpha(sr, 1f - k);
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    private void HitEverythingOnScreen(Camera cam, CommandTokenData.LevelStats lv)
    {
        float damage = lv.damage * Might;

        // a bomb: every enemy shot on the screen wiped with it
        EnemyShots.ClearOnScreen(cam);

        // and the screen stays clear for a moment after the hit
        if (SpawnDirector.Active != null) SpawnDirector.Active.PauseSpawning(Data.spawnPauseSeconds);

        // a kill unregisters the enemy, so go through a copy of the list
        snapshot.Clear();
        snapshot.AddRange(EnemyRegistry.All);
        foreach (var go in snapshot)
        {
            if (go == null || !go.TryGetComponent(out EnemyHealth e)) continue;
            if (cam != null && !OnScreen(go.transform.position, cam)) continue;
            bool killed = Hit(e, damage);
            if (!killed && lv.stunSeconds > 0f && go.TryGetComponent(out EnemyMovement move))
                move.ApplySlow(0f, lv.stunSeconds);
        }

        if (lv.pullsWen) GameEvents.OnCollectAllWen?.Invoke();
    }

    // a point on the seal row, offset sideways in world units from its middle
    private Vector3 RowPoint(Camera cam, float offset)
    {
        var p = cam.ViewportToWorldPoint(new Vector3(0.5f, Data.sealRowHeight, -cam.transform.position.z));
        return p + cam.transform.right * offset;
    }

    // the i-th slot's sprite; empty slots reuse the ones that are set, in order
    private Sprite SealSprite(int i)
    {
        sprites.Clear();
        if (Data.sealSprites != null)
            foreach (var s in Data.sealSprites)
                if (s != null) sprites.Add(s);
        return sprites.Count > 0 ? sprites[i % sprites.Count] : null;
    }
}
