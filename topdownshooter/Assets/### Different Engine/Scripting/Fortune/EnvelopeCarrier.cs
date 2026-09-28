using System.Collections.Generic;
using UnityEngine;

// the little envelope floating over an elite or a boss that carries one, so it's worth chasing
// down: dropped when it falls to the player, gone with it if it's swept away (a wave's end, the
// time running out). pooled enemies come back without it. a Final Rush's Magistrates all carry one,
// but it's one envelope a rush: only the last of them to fall drops it
public class EnvelopeCarrier : MonoBehaviour
{
    private static readonly List<EnvelopeCarrier> carrying = new List<EnvelopeCarrier>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => carrying.Clear();

    public EnvelopeSource source;
    private SpriteRenderer icon, host;
    private Sprite[] frames;
    private float clock;
    private const float WorldPpu = 37f / 1.3f;

    public static EnvelopeCarrier Attach(GameObject enemy, EnvelopeSource source)
    {
        if (enemy == null) return null;
        if (!enemy.TryGetComponent(out EnvelopeCarrier carrier)) carrier = enemy.AddComponent<EnvelopeCarrier>();
        carrier.source = source;
        return carrier;
    }

    private void OnEnable()
    {
        if (!carrying.Contains(this)) carrying.Add(this);
        host = GetComponent<SpriteRenderer>();
        var lib = VfxLibrary.Get;
        frames = lib != null ? lib.envelopeCarry : null;
        var go = new GameObject("Envelope Carried");
        go.transform.SetParent(transform, false);
        icon = go.AddComponent<SpriteRenderer>();
        bool art = frames != null && frames.Length > 0 && frames[0] != null;
        icon.sprite = art ? frames[0] : WeaponFx.Square;
        icon.color = art ? Color.white : new Color(0.85f, 0.15f, 0.2f);
        icon.sortingLayerName = "Aura";
        icon.sortingOrder = 19;
        if (!art) go.transform.localScale = Vector3.one * 0.25f;
    }

    private void LateUpdate()
    {
        if (icon == null) return;
        clock += Time.deltaTime;
        // over its head whatever its size, a pixel's bob, the right way up however it's flipped
        float top = host != null && host.sprite != null ? host.sprite.bounds.max.y : 0.6f;
        var s = transform.lossyScale;
        // a boss wears its health bar over its head: the envelope floats over that
        float clear = source == EnvelopeSource.Elite ? 0.35f : 0.95f;
        float up = top * Mathf.Abs(s.y) + clear + Mathf.Round(Mathf.Sin(clock * 4f)) / WorldPpu;
        icon.transform.position = transform.position + Vector3.up * up;
        icon.transform.rotation = Quaternion.identity;
        icon.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.01f, Mathf.Abs(s.y)), 1f);
        if (frames != null && frames.Length > 0 && frames[0] != null) icon.sprite = frames[(int)(clock * 8f) % frames.Length];
    }

    // the enemy fell to the player: the envelope falls out of it
    public void Drop()
    {
        if (source != EnvelopeSource.Boss || !OtherBossCarries()) FortuneEnvelope.Drop(transform.position, source);
        Remove();
    }

    // another boss still up with an envelope on it: this one's goes with the last of them
    private bool OtherBossCarries()
    {
        foreach (var c in carrying)
        {
            if (c == null || c == this || c.source != EnvelopeSource.Boss || !c.isActiveAndEnabled) continue;
            if (c.TryGetComponent(out EnemyHealth h) && h.Current <= 0f) continue;
            return true;
        }
        return false;
    }

    private void Remove()
    {
        carrying.Remove(this);
        if (icon != null) Destroy(icon.gameObject);
        icon = null;
        Destroy(this);
    }

    private void OnDisable() => Remove();
}
