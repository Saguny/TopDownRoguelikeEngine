using System;
using System.Collections.Generic;
using UnityEngine;

// what the Electrical Aura looks like: a crackling ring at its radius, and a lightning strike on
// the enemies each damage tick hits. the ring is drawn at several radii and the one nearest the
// aura's size is shown, stretched only by what's left over, so the pixels stay the world's size as
// the aura grows. Aura drives it: Show every frame, Strike on every tick
public class AuraVisual : MonoBehaviour
{
    [Serializable]
    public struct RingSize
    {
        [Tooltip("the radius this drawing of the ring has, in world units")]
        public float radius;
        public Sprite[] ring;
    }

    public RingSize[] sizes = new RingSize[0];
    [Min(0.01f)] public float ringFps = 16f;

    [Tooltip("the strike on an enemy hit by a damage tick")]
    public GameObject zapFx;
    [Tooltip("strikes per tick at most; the rest of the hits still land, unseen")]
    [Min(0)] public int maxZapsPerTick = 6;

    [Tooltip("seconds the ring takes to grow in when the aura is switched on")]
    [Min(0f)] public float growSeconds = 0.25f;

    public string sortingLayer = "Aura";
    public int ringOrder = 1;

    private SpriteRenderer ring;
    private float ringTime, grown;
    private readonly List<Vector2> zapAt = new List<Vector2>();

    private void Awake()
    {
        var go = new GameObject("Ring");
        go.transform.SetParent(transform, false);
        ring = go.AddComponent<SpriteRenderer>();
        ring.sortingLayerName = sortingLayer;
        ring.sortingOrder = ringOrder;
    }

    private void OnEnable() => grown = growSeconds > 0f ? 0f : 1f;

    // the ring at this radius (world units), this frame
    public void Show(float radius)
    {
        if (sizes == null || sizes.Length == 0 || radius <= 0f) return;

        var s = sizes[Nearest(radius)];
        float dt = Time.deltaTime;
        if (grown < 1f) grown = Mathf.Min(1f, grown + dt / Mathf.Max(0.01f, growSeconds));
        float grow = 1f - (1f - grown) * (1f - grown);
        float scale = radius / Mathf.Max(0.01f, s.radius) * Mathf.Lerp(0.35f, 1f, grow);

        ringTime += dt;
        if (s.ring == null || s.ring.Length == 0) return;
        ring.sprite = s.ring[(int)(ringTime * ringFps) % s.ring.Length];
        ring.transform.localScale = Vector3.one * scale / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
    }

    // a damage tick: lightning lands on some of the enemies it hit
    public void Strike(List<Vector2> hits)
    {
        if (zapFx == null || hits == null || hits.Count == 0 || maxZapsPerTick <= 0) return;

        zapAt.Clear();
        zapAt.AddRange(hits);
        int n = Mathf.Min(maxZapsPerTick, zapAt.Count);
        for (int k = 0; k < n; k++)
        {
            // a random few when there are more hits than strikes
            int pick = UnityEngine.Random.Range(k, zapAt.Count);
            (zapAt[k], zapAt[pick]) = (zapAt[pick], zapAt[k]);
            FxOneShot.Play(zapFx, zapAt[k]);
        }
    }

    // the drawing whose radius is nearest, as a ratio, so the leftover stretch is smallest
    private int Nearest(float radius)
    {
        int best = 0;
        float bestErr = float.MaxValue;
        for (int i = 0; i < sizes.Length; i++)
        {
            if (sizes[i].radius <= 0f) continue;
            float err = Mathf.Abs(Mathf.Log(radius / sizes[i].radius));
            if (err < bestErr)
            {
                bestErr = err;
                best = i;
            }
        }
        return best;
    }
}
