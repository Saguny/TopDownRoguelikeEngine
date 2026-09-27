using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// every cooldown, seals stamp onto the screen one at a time, big to small, then explode into a
// shockwave that hits every enemy on screen. with a Cast Animation prefab set, that prefab plays
// instead and says when the hit lands
public class CommandToken : Weapon<CommandTokenData>
{
    private readonly List<GameObject> snapshot = new List<GameObject>(256);
    private readonly List<SpriteRenderer> row = new List<SpriteRenderer>();
    private readonly List<Sprite> sprites = new List<Sprite>();
    private float timer;
    private bool primed, casting;

    protected override void OnLevelChanged()
    {
        // the first shockwave comes shortly after the pickup, not a whole cooldown later
        if (primed || Data == null) return;
        primed = true;
        timer = Mathf.Max(0f, Cooldown(Data.At(Level).cooldown) - Data.firstShotDelay);
    }

    private void Update()
    {
        if (Data == null || Level <= 0 || casting) return;

        var lv = Data.At(Level);
        timer += Time.deltaTime;
        if (timer < Cooldown(lv.cooldown)) return;

        timer = 0f;
        StartCoroutine(Cast(lv));
    }

    private IEnumerator Cast(CommandTokenData.LevelStats lv)
    {
        casting = true;
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
