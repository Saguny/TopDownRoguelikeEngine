using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// opening a fortune envelope, the way Rainbow Six Siege opens an alpha pack: the game stops, the
// envelope drops in and waits, breathing in a plain warm light and twitching now and then, until the
// player clicks it open. then its seal charges while it shakes harder and harder over a building
// roar of drums, paper and a sizzling fuse (a riser as long as the charge); the light around it
// starts jade, and a rare one flares azure partway through, a legendary one gold on top of that,
// so the rarity is told by the light before it's said. the flap bursts open, a scroll rises out
// and unrolls, and the rewards land on it one by one, each with a burst in the rarity's colours:
// an evolution in a burning frame, then the coins counting up. a click hurries it along, then
// closes it. what it gives is taken the moment it opens (PlayerInventory.OpenEnvelope)
public class EnvelopeOpening : MonoBehaviour
{
    // screen pixels per art pixel at 1080p: the envelope and its light, the scroll and its rewards
    private const float Big = 5f, Small = 5f;
    private const float PaperWidth = 168f * Small;
    // the scroll as it rises out, still rolled: a sliver of paper between its rollers
    private const float Rolled = 8f * Small;
    private const float MaxStep = 1f / 30f;

    public static readonly Color Jade = new Color(0.33f, 0.85f, 0.6f);
    public static readonly Color Azure = new Color(0.35f, 0.78f, 0.94f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.28f);
    public static Color ColorOf(EnvelopeRarity r) => r == EnvelopeRarity.Legendary ? Gold : r == EnvelopeRarity.Rare ? Azure : Jade;
    public static string NameOf(EnvelopeRarity r) => r == EnvelopeRarity.Legendary ? "LEGENDARY" : r == EnvelopeRarity.Rare ? "RARE" : "COMMON";

    private static EnvelopeOpening instance;
    private static readonly Queue<(EnvelopeRarity rarity, EnvelopeSource source, bool evolves)> waiting = new Queue<(EnvelopeRarity, EnvelopeSource, bool)>();

    // an envelope is open or waiting to be: the game stays stopped and no level up opens meanwhile
    public static bool Busy => waiting.Count > 0 || (instance != null && instance.running);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        waiting.Clear();
    }

    // evolves: whether it can evolve a weapon (FortuneEnvelope.Evolves)
    public static void Open(EnvelopeRarity rarity, EnvelopeSource source, bool evolves = true)
    {
        // everything maxed: nothing to open, it's just gold, paid out where they stand
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent(out PlayerInventory inventory) && inventory.EnvelopeWouldBeEmpty(evolves))
        {
            PayOut(rarity, source, inventory);
            return;
        }
        waiting.Enqueue((rarity, source, evolves));
        if (instance == null) Build();
    }

    private static AudioClip payOutSound;

    // the String of Wen for every upgrade it held, and its own coins
    private static void PayOut(EnvelopeRarity rarity, EnvelopeSource source, PlayerInventory inventory)
    {
        int coins = inventory.GiftCoins * FortuneEnvelope.Upgrades(rarity) + FortuneEnvelope.RollCoins(rarity) + FortuneEnvelope.TakeBonus(source);
        int paid = Coins.FromEnvelope(coins);
        Vector2 at = inventory.transform.position;
        if (payOutSound == null) payOutSound = Resources.Load<AudioClip>("Sfx/coins_total");
        if (payOutSound != null) SfxPlayer.PlayAt(payOutSound, at, 0.9f);
        PixelNumbers.Show(at + Vector2.up * 1.3f, paid, true, new Color(1f, 0.85f, 0.3f), 2);
    }

    // ---------------------------------------------------------------- parts

    private bool running;
    private VfxLibrary lib;
    private TMP_FontAsset font;
    private CanvasGroup group;
    private RectTransform root, stage, envelopeBox, scrollBox, paperMask, rewardsRow, particles;
    private Image backdrop, flash, raysA, raysB, aura, big, flap, front, burst, rollerL, rollerR, paper, banner;
    private TMP_Text bannerText, coinsText, prompt;
    private Image coinsIcon;
    private AudioSource voice;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<Particle> motes = new List<Particle>();

    private class Particle
    {
        public Image image;
        public Vector2 velocity;
        public float life, age, spin, gravity;
        public Sprite[] frames;
    }

    private static void Build()
    {
        var go = new GameObject("Envelope Opening", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 480;                         // over the HUD and menus, under the item tooltip
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        instance = go.AddComponent<EnvelopeOpening>();
        instance.Make();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (running) Time.timeScale = 1f;
    }

    private void Make()
    {
        lib = VfxLibrary.Get;
        font = Resources.Load<TMP_FontAsset>("fonts/Pixelta");
        root = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        backdrop = Fill("Backdrop", root, new Color(0.04f, 0.02f, 0.06f, 0f));
        stage = Box("Stage", root);
        raysA = Pixel("Rays", stage, lib?.rays, Big * 1.2f, new Vector2(128f, 128f));
        raysB = Pixel("Rays Back", stage, lib?.rays, Big * 0.8f, new Vector2(128f, 128f));
        aura = Pixel("Aura", stage, lib?.aura, Big * 1.6f, new Vector2(64f, 64f));
        envelopeBox = Box("Envelope", stage);
        big = Pixel("Closed", envelopeBox, First(lib?.envelopeBig), Big, new Vector2(48f, 72f), new Color(0.8f, 0.12f, 0.18f));
        flap = Pixel("Open", envelopeBox, First(lib?.envelopeFlap), Big, new Vector2(48f, 72f), new Color(0.8f, 0.12f, 0.18f));

        // the scroll, which rises out between the flap and the front
        scrollBox = Box("Scroll", envelopeBox);
        banner = Pixel("Banner", scrollBox, lib?.banner, Small, new Vector2(80f, 16f));
        banner.rectTransform.anchoredPosition = new Vector2(0f, 52f * Small * 0.5f + 16f * Small * 0.5f + 6f);
        bannerText = Text("Rarity", banner.rectTransform, 44f, Color.white);
        bannerText.rectTransform.anchoredPosition = new Vector2(0f, 3f);
        var maskGo = new GameObject("Paper", typeof(RectTransform), typeof(RectMask2D));
        paperMask = (RectTransform)maskGo.transform;
        paperMask.SetParent(scrollBox, false);
        paperMask.sizeDelta = new Vector2(0f, 52f * Small);
        paper = Pixel("Sheet", paperMask, lib?.scroll, Small, new Vector2(168f, 52f), new Color(0.93f, 0.85f, 0.69f));
        rewardsRow = Box("Rewards", paperMask);
        rollerL = Pixel("Roller Left", scrollBox, lib?.scrollRoller, Small, new Vector2(10f, 60f), new Color(0.45f, 0.26f, 0.13f));
        rollerR = Pixel("Roller Right", scrollBox, lib?.scrollRoller, Small, new Vector2(10f, 60f), new Color(0.45f, 0.26f, 0.13f));
        coinsIcon = Pixel("Coins", scrollBox, First(lib?.coinIcon), Small, new Vector2(16f, 16f), new Color(1f, 0.8f, 0.3f));
        coinsText = Text("Coins Earned", scrollBox, 40f, new Color(1f, 0.88f, 0.5f));
        coinsText.alignment = TextAlignmentOptions.Left;
        coinsText.rectTransform.sizeDelta = new Vector2(300f, 60f);

        front = Pixel("Front", envelopeBox, lib?.envelopeFront, Big, new Vector2(48f, 72f), new Color(0.8f, 0.12f, 0.18f));
        burst = Pixel("Burst", stage, First(lib?.burstCommon), Big * 1.25f, new Vector2(96f, 96f));
        particles = Box("Particles", root);
        flash = Fill("Flash", root, new Color(1f, 1f, 1f, 0f));
        prompt = Text("Continue", root, 32f, new Color(1f, 0.95f, 0.85f));
        prompt.rectTransform.anchoredPosition = new Vector2(0f, -470f);
        prompt.text = "Click to continue";

        // one-shots go round a few voices: a voice's pitch bends everything still ringing on it
        for (int i = 0; i < voices.Length; i++) voices[i] = Voice("Envelope Voice " + i);
        voice = voices[0];
        // the idle loop and the charge's riser each have a voice of their own, so they can be cut
        hum = Voice("Envelope Hum");
        hum.loop = true;
        riser = Voice("Envelope Riser");
    }

    private AudioSource hum, riser;
    private readonly AudioSource[] voices = new AudioSource[6];
    private int nextVoice;

    private AudioSource Voice(string name)
    {
        var v = new GameObject(name).AddComponent<AudioSource>();
        v.transform.SetParent(transform, false);
        v.playOnAwake = false;
        v.spatialBlend = 0f;
        v.ignoreListenerPause = true;
        var groups = GameSettings.Mixer != null ? GameSettings.Mixer.FindMatchingGroups("SFX") : null;
        if (groups != null && groups.Length > 0) v.outputAudioMixerGroup = groups[0];
        return v;
    }

    // the charge per rarity, in seconds: its riser (Tools/SFX/fortune.py) is cut to exactly this
    public static float ChargeSeconds(EnvelopeRarity r) => r == EnvelopeRarity.Legendary ? 2.8f : r == EnvelopeRarity.Rare ? 2f : 1.4f;
    // the light turns azure this far through the charge, then gold
    private const float RareAt = 0.4f, LegendaryAt = 0.72f;
    // the rarity isn't told until it's opened: a plain warm light while it waits
    private static readonly Color Unopened = new Color(1f, 0.9f, 0.72f);

    private static Sprite First(Sprite[] frames) => frames != null && frames.Length > 0 ? frames[0] : null;

    private static RectTransform Box(string name, RectTransform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private static Image Fill(string name, RectTransform parent, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.sizeDelta = Vector2.zero;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // a sprite at a whole number of screen pixels per art pixel, so it stays crisp
    private static Image Pixel(string name, RectTransform parent, Sprite sprite, float scale, Vector2 artSize, Color? placeholder = null)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.sprite = sprite;
        img.color = sprite != null ? Color.white : placeholder ?? new Color(1f, 1f, 1f, 0.35f);
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = (sprite != null ? sprite.rect.size : artSize) * scale;
        return img;
    }

    private TMP_Text Text(string name, RectTransform parent, float size, Color color)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.outlineWidth = 0.18f;
        t.outlineColor = new Color32(0x1e, 0x0c, 0x3a, 0xff);
        t.rectTransform.sizeDelta = new Vector2(600f, 80f);
        return t;
    }

    // ---------------------------------------------------------------- running

    private void Update()
    {
        if (!running && waiting.Count > 0) StartCoroutine(Play(waiting.Dequeue()));
        StepParticles(Mathf.Min(Time.unscaledDeltaTime, MaxStep));
    }

    private float Dt => Mathf.Min(Time.unscaledDeltaTime, MaxStep);

    // a click, space, enter or the Command Token's key: hurry it along, or close it at the end
    private static bool Pressed() =>
        Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
        Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.E);

    private IEnumerator Play((EnvelopeRarity rarity, EnvelopeSource source, bool evolves) what)
    {
        running = true;
        var rarity = what.rarity;
        Juice.Yield();
        Time.timeScale = 0f;

        // what it gives, taken now while the game is stopped
        var player = GameObject.FindGameObjectWithTag("Player");
        var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        var rewards = inventory != null ? inventory.OpenEnvelope(FortuneEnvelope.Upgrades(rarity), what.evolves) : new List<EnvelopeReward>();
        int coins = Coins.FromEnvelope(FortuneEnvelope.RollCoins(rarity) + FortuneEnvelope.TakeBonus(what.source));
        foreach (var r in rewards) if (r.IsCoins) coins += r.coins;

        ResetStage();
        group.alpha = 1f;
        group.blocksRaycasts = true;
        Color shown = Jade;
        Tint(shown);

        // ---- in: the backdrop darkens and the envelope drops in, landing with a squash
        for (float t = 0f; t < 0.38f; t += Dt)
        {
            float k = t / 0.38f;
            backdrop.color = new Color(0.04f, 0.02f, 0.06f, 0.82f * Mathf.Clamp01(k * 2f));
            float y = Mathf.Lerp(700f, 0f, BackOut(k));
            PlaceEnvelope(new Vector2(0f, y));
            aura.color = WithAlpha(shown, 0.25f * k);
            yield return null;
        }
        PlaceEnvelope(Vector2.zero);
        yield return Squash(envelopeBox, 0.16f, 0.12f);
        Play(lib?.envelopeShake, 0.9f);

        // ---- waiting to be opened: floating, the light breathing, a twitch now and then as if
        // something inside wants out; a low hum under it. nothing is told until it's clicked
        prompt.text = "Click to open";
        prompt.fontSize = 40f;
        prompt.rectTransform.anchoredPosition = new Vector2(0f, -72f * Big * 0.5f - 70f);
        prompt.gameObject.SetActive(true);
        if (lib != null && lib.envelopeIdle != null) { hum.clip = lib.envelopeIdle; hum.volume = 0f; hum.Play(); }
        float mix = lib != null ? lib.envelopeVolume : 0.25f;
        float idle = 0f, nextTwitch = 0.9f, twitch = -1f;
        yield return null;
        while (!Pressed())
        {
            idle += Dt;
            if (hum.isPlaying) hum.volume = Mathf.Clamp01(idle / 0.5f) * mix;
            float breathe = 0.5f + 0.5f * Mathf.Sin(idle * 2.4f);
            aura.color = WithAlpha(Unopened, 0.16f + 0.1f * breathe);
            aura.rectTransform.localScale = Vector3.one * (0.85f + 0.08f * breathe);
            Vector2 at = new Vector2(0f, Mathf.Round(Mathf.Sin(idle * 1.8f) * 3f) * Big * 0.5f);
            float angle = 0f;
            if (idle >= nextTwitch)
            {
                twitch = 0f;
                nextTwitch = idle + UnityEngine.Random.Range(1.1f, 1.9f);
                Play(lib?.envelopeShake, UnityEngine.Random.Range(1.15f, 1.35f), 0.55f);
            }
            if (twitch >= 0f)
            {
                twitch += Dt;
                float k = 1f - twitch / 0.2f;
                if (k <= 0f) twitch = -1f;
                else { at += UnityEngine.Random.insideUnitCircle * 5f * k; angle = UnityEngine.Random.Range(-4f, 4f) * k; }
            }
            envelopeBox.anchoredPosition = at;
            envelopeBox.localRotation = Quaternion.Euler(0f, 0f, angle);
            prompt.alpha = 0.55f + 0.45f * Mathf.Sin(idle * 4f);
            if (UnityEngine.Random.value < 0.12f) Mote(Unopened, UnityEngine.Random.insideUnitCircle * 140f, 0.5f);
            yield return null;
        }
        prompt.gameObject.SetActive(false);
        hum.Stop();

        // ---- clicked: the seal knocked, a squash, and the charge begins
        Play(lib?.envelopeClick, 1f);
        envelopeBox.anchoredPosition = Vector2.zero;
        envelopeBox.localRotation = Quaternion.identity;
        StartCoroutine(Squash(envelopeBox, 0.12f, 0.1f));

        // ---- the charge: shaking harder and harder, the seal lighting up, the light turning; its
        // riser runs the whole length and cuts dead on the burst. a click after the first moment
        // hurries it
        float charge = ChargeSeconds(rarity);
        var riserClip = rarity == EnvelopeRarity.Legendary ? lib?.chargeLegendary : rarity == EnvelopeRarity.Rare ? lib?.chargeRare : lib?.chargeCommon;
        if (riserClip != null) { riser.clip = riserClip; riser.volume = lib != null ? lib.envelopeVolume : 0.25f; riser.Play(); }
        bool rareShown = false, legendaryShown = false, hurried = false;
        float jitterClock = 0f;
        Vector2 jitter = Vector2.zero;
        float jitterAngle = 0f;
        yield return null;
        for (float t = 0f; t < charge; t += Dt)
        {
            float k = t / charge;
            if (t > 0.35f && Pressed()) { hurried = true; break; }

            if (!rareShown && rarity >= EnvelopeRarity.Rare && k > RareAt)
            {
                rareShown = true;
                shown = Azure;
                TierUp(shown, 0.55f, 14f);
            }
            if (!legendaryShown && rarity == EnvelopeRarity.Legendary && k > LegendaryAt)
            {
                legendaryShown = true;
                shown = Gold;
                TierUp(shown, 0.85f, 24f);
            }

            big.sprite = Frame(lib?.envelopeBig, Mathf.Min(0.999f, k * 1.08f)) ?? big.sprite;
            // jitter re-picked 30 times a second, stronger as it goes, a jolt on each turn of the light
            jitterClock -= Dt;
            if (jitterClock <= 0f)
            {
                jitterClock = 1f / 30f;
                float amp = Mathf.Lerp(1f, 7f, k * k) * (legendaryShown ? 1.6f : rareShown ? 1.25f : 1f);
                jitter = UnityEngine.Random.insideUnitCircle * (amp + jolt);
                jitterAngle = UnityEngine.Random.Range(-1f, 1f) * Mathf.Lerp(1f, 6f, k);
            }
            jolt = Mathf.MoveTowards(jolt, 0f, Dt * 100f);
            envelopeBox.anchoredPosition = jitter;
            envelopeBox.localRotation = Quaternion.Euler(0f, 0f, jitterAngle);

            float pulse = 1f + 0.06f * Mathf.Sin(t * Mathf.Lerp(8f, 26f, k));
            aura.color = WithAlpha(Color.Lerp(Unopened, shown, Mathf.Clamp01(k * 4f)), Mathf.Lerp(0.18f, 0.5f, k));
            aura.rectTransform.localScale = Vector3.one * (Mathf.Lerp(0.8f, 1.35f, k) + auraKick) * pulse;
            auraKick = Mathf.MoveTowards(auraKick, 0f, Dt * 2.5f);
            if (rareShown)
            {
                raysA.color = WithAlpha(shown, Mathf.Lerp(0.15f, 0.4f, k));
                raysA.rectTransform.localRotation *= Quaternion.Euler(0f, 0f, -Dt * (legendaryShown ? 70f : 30f));
            }
            // motes drift up off it, more of them the closer it is
            if (UnityEngine.Random.value < Mathf.Lerp(0.15f, 0.9f, k)) Mote(shown, envelopeBox.anchoredPosition + UnityEngine.Random.insideUnitCircle * 120f);
            yield return null;
        }
        // hurried: the riser stops where it is, and the burst comes straight in
        if (hurried) riser.Stop();
        envelopeBox.anchoredPosition = Vector2.zero;
        envelopeBox.localRotation = Quaternion.identity;

        // ---- the burst: the flap flies open, a flash, the light blazes, red paper and gold thrown out
        shown = ColorOf(rarity);
        big.enabled = false;
        flap.enabled = true;
        front.enabled = false;
        Play(lib?.envelopeOpen, 1f);
        Play(rarity == EnvelopeRarity.Legendary ? lib?.revealLegendary : rarity == EnvelopeRarity.Rare ? lib?.revealRare : lib?.revealCommon, 1f);
        StartCoroutine(Flash(shown, rarity == EnvelopeRarity.Legendary ? 0.7f : 0.45f, 0.35f));
        StartCoroutine(Burst(rarity, stage, new Vector2(0f, 90f), Big * 1.25f));
        if (rarity == EnvelopeRarity.Legendary) StartCoroutine(Later(0.16f, () => StartCoroutine(Burst(rarity, stage, new Vector2(0f, 90f), Big * 1.6f))));
        Confetti(rarity, new Vector2(0f, 90f));
        raysA.color = WithAlpha(shown, 0.75f);
        raysB.color = WithAlpha(Color.Lerp(shown, Color.white, 0.4f), 0.5f);
        StartCoroutine(Spin());
        float open = 0.42f;
        for (float t = 0f; t < open; t += Dt)
        {
            float k = t / open;
            flap.sprite = Frame(lib?.envelopeFlap, k) ?? flap.sprite;
            stage.localScale = Vector3.one * (1f + 0.12f * (1f - EaseOut(k)));
            raysA.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.1f, EaseOut(k));
            raysB.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, EaseOut(k));
            aura.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1.2f, k);
            stage.anchoredPosition = UnityEngine.Random.insideUnitCircle * (1f - k) * (rarity == EnvelopeRarity.Legendary ? 16f : 8f);
            yield return null;
        }
        stage.localScale = Vector3.one;
        stage.anchoredPosition = Vector2.zero;
        flap.sprite = Frame(lib?.envelopeFlap, 0.999f) ?? flap.sprite;

        // ---- the scroll rises out of the pocket, rolled, from behind the front
        front.enabled = true;
        scrollBox.gameObject.SetActive(true);
        SetScrollWidth(Rolled);
        const float hinge = (36f - 18f) * Big;       // the pocket's lip above the envelope's middle
        Vector2 inPocket = new Vector2(0f, hinge - 150f), outOf = new Vector2(0f, hinge + 190f);
        for (float t = 0f; t < 0.34f; t += Dt)
        {
            scrollBox.anchoredPosition = Vector2.Lerp(inPocket, outOf, EaseOut(t / 0.34f));
            yield return null;
        }
        // it lifts free to the middle of the screen as the envelope sinks away
        Vector2 from = scrollBox.anchoredPosition, to = new Vector2(0f, 110f);
        scrollBox.SetParent(stage, true);
        from = scrollBox.anchoredPosition;
        for (float t = 0f; t < 0.36f; t += Dt)
        {
            float k = EaseInOut(t / 0.36f);
            scrollBox.anchoredPosition = Vector2.Lerp(from, to, k);
            envelopeBox.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -620f, k * k));
            SetAlpha(envelopeBox, 1f - k);
            // the light rises with the scroll and settles back behind it, so the rewards read over it
            aura.color = WithAlpha(shown, Mathf.Lerp(0.5f, 0.2f, k));
            Vector2 lit = new Vector2(0f, Mathf.Lerp(0f, to.y, k));
            aura.rectTransform.anchoredPosition = raysA.rectTransform.anchoredPosition = raysB.rectTransform.anchoredPosition = lit;
            yield return null;
        }
        envelopeBox.gameObject.SetActive(false);

        // ---- it unrolls, and the rarity's ribbon drops in over it
        Play(lib?.envelopeShake, 1.3f);
        for (float t = 0f; t < 0.4f; t += Dt)
        {
            float k = EaseOut(t / 0.4f);
            SetScrollWidth(Mathf.Lerp(Rolled, PaperWidth, k));
            yield return null;
        }
        SetScrollWidth(PaperWidth);
        banner.gameObject.SetActive(true);
        banner.color = shown;
        bannerText.text = NameOf(rarity);
        bannerText.color = rarity == EnvelopeRarity.Legendary ? new Color(1f, 0.97f, 0.8f) : Color.white;
        StartCoroutine(Squash(banner.rectTransform, 0.2f, 0.25f));

        // ---- the rewards land, one by one
        var showing = new List<EnvelopeReward>();
        foreach (var r in rewards) if (!r.IsCoins) showing.Add(r);
        float gap = 8f, slot = 26f * Small;
        float rowWidth = showing.Count * slot + Mathf.Max(0, showing.Count - 1) * gap;
        bool rushed = hurried;
        for (int i = 0; i < showing.Count; i++)
        {
            Vector2 at = new Vector2(-rowWidth * 0.5f + slot * 0.5f + i * (slot + gap), 8f);
            Reward(showing[i], at, rarity, i);
            if (!rushed)
            {
                float wait = showing[i].evolution ? 0.55f : rarity == EnvelopeRarity.Legendary ? 0.32f : 0.27f;
                for (float t = 0f; t < wait; t += Dt) { if (Pressed()) { rushed = true; break; } yield return null; }
            }
        }

        // ---- and the coins, counting up under the scroll
        coinsIcon.gameObject.SetActive(true);
        coinsText.gameObject.SetActive(true);
        coinsIcon.rectTransform.anchoredPosition = new Vector2(-48f, -52f * Small * 0.5f - 44f);
        coinsText.rectTransform.anchoredPosition = new Vector2(0f + 150f, -52f * Small * 0.5f - 44f);
        for (float t = 0f; t < 0.5f && !rushed; t += Dt)
        {
            if (Pressed()) break;
            coinsText.text = "+" + Mathf.RoundToInt(coins * EaseOut(t / 0.5f));
            coinsIcon.sprite = Frame(lib?.coinIcon, t * 3f % 1f) ?? coinsIcon.sprite;
            yield return null;
        }
        coinsText.text = "+" + coins;

        // ---- waiting on the player: the light turning slowly, the prompt breathing
        prompt.text = "Click to continue";
        prompt.fontSize = 32f;
        prompt.rectTransform.anchoredPosition = new Vector2(0f, -470f);
        prompt.gameObject.SetActive(true);
        yield return null;
        while (!Pressed())
        {
            prompt.alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            coinsIcon.sprite = Frame(lib?.coinIcon, Time.unscaledTime * 1.5f % 1f) ?? coinsIcon.sprite;
            Mote(shown, new Vector2(UnityEngine.Random.Range(-400f, 400f), UnityEngine.Random.Range(-100f, 200f)), 0.35f);
            yield return null;
        }

        // ---- out
        for (float t = 0f; t < 0.22f; t += Dt)
        {
            group.alpha = 1f - t / 0.22f;
            yield return null;
        }
        group.alpha = 0f;
        group.blocksRaycasts = false;
        Clear();
        running = false;

        // the game goes on, with a moment's grace, unless another envelope is waiting
        if (waiting.Count == 0)
        {
            Time.timeScale = 1f;
            if (player != null && player.TryGetComponent(out PlayerHealth health)) health.GrantInvulnerability(2f);
        }
    }

    // ---------------------------------------------------------------- the beats

    // a jolt added to the charge's shaking, and a kick to its light, both dying away
    private float jolt, auraKick;

    // the light turns up a rarity: a flash in the new colour, a jolt, a burst of motes. it doesn't
    // hold the charge up, so the charge keeps time with its riser
    private void TierUp(Color color, float strength, float kick)
    {
        Play(lib?.envelopeTier != null ? lib.envelopeTier : lib?.envelopeShake, 1f + strength * 0.4f);
        StartCoroutine(Flash(color, strength * 0.55f, 0.28f));
        Tint(color);
        for (int i = 0; i < 18; i++) Mote(color, UnityEngine.Random.insideUnitCircle * 60f, 1.3f);
        jolt = kick;
        auraKick = 0.5f;
    }

    private IEnumerator Flash(Color color, float alpha, float seconds)
    {
        for (float t = 0f; t < seconds; t += Dt)
        {
            flash.color = WithAlpha(Color.Lerp(color, Color.white, 0.35f), alpha * (1f - t / seconds));
            yield return null;
        }
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    private IEnumerator Burst(EnvelopeRarity rarity, RectTransform parent, Vector2 at, float scale)
    {
        var frames = rarity == EnvelopeRarity.Legendary ? lib?.burstLegendary : rarity == EnvelopeRarity.Rare ? lib?.burstRare : lib?.burstCommon;
        if (frames == null || frames.Length == 0 || frames[0] == null) yield break;
        var img = Pixel("Burst", parent, frames[0], scale, new Vector2(96f, 96f));
        img.rectTransform.anchoredPosition = at;
        spawned.Add(img.gameObject);
        for (float t = 0f; t < frames.Length / 25f; t += Dt)
        {
            if (img == null) yield break;
            img.sprite = frames[Mathf.Min(frames.Length - 1, (int)(t * 25f))];
            yield return null;
        }
        if (img != null) img.enabled = false;
    }

    private IEnumerator Spin()
    {
        while (running && raysA != null)
        {
            raysA.rectTransform.localRotation *= Quaternion.Euler(0f, 0f, -Dt * 14f);
            raysB.rectTransform.localRotation *= Quaternion.Euler(0f, 0f, Dt * 22f);
            yield return null;
        }
    }

    private IEnumerator Later(float seconds, Action then)
    {
        for (float t = 0f; t < seconds; t += Dt) yield return null;
        then();
    }

    private IEnumerator Squash(RectTransform rt, float seconds, float amount)
    {
        for (float t = 0f; t < seconds; t += Dt)
        {
            float k = t / seconds, s = Mathf.Sin(k * Mathf.PI) * amount * (1f - k);
            rt.localScale = new Vector3(1f + s, 1f - s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // one reward landing on the scroll: its frame pops in over a burst, its icon fades up, and a
    // line under it says what it did. an evolution burns, shakes the scroll and says so in gold
    private void Reward(EnvelopeReward r, Vector2 at, EnvelopeRarity rarity, int index)
    {
        StartCoroutine(Burst(rarity, rewardsRow, at, 2.2f));
        var frames = r.evolution ? lib?.rewardSlotEvolution : null;
        var slotImage = Pixel(r.evolution ? "Evolution" : "Reward", rewardsRow,
            r.evolution && frames != null && frames.Length > 0 ? frames[0] : lib?.rewardSlot, Small, new Vector2(26f, 26f), new Color(0.12f, 0.06f, 0.2f));
        // the burning frame sits low in its canvas: line its frame up with the others
        slotImage.rectTransform.anchoredPosition = at + (r.evolution ? new Vector2(0f, 4f * Small) : Vector2.zero);
        spawned.Add(slotImage.gameObject);

        var item = r.item;
        var iconSprite = item is WeaponData w && r.evolution && w.evolvedIcon != null ? w.evolvedIcon : item != null ? item.icon : null;
        var iconFrames = item is WeaponData w2 && r.evolution && w2.evolvedIconFrames != null && w2.evolvedIconFrames.Length > 0 ? w2.evolvedIconFrames : item != null ? item.iconFrames : null;
        var icon = Pixel("Icon", rewardsRow, iconSprite, Small, new Vector2(16f, 16f), new Color(1f, 1f, 1f, 0.6f));
        icon.rectTransform.anchoredPosition = at;
        if (iconSprite != null) icon.rectTransform.sizeDelta = FitIn(iconSprite.rect.size, 18f) * Small;
        icon.raycastTarget = true;
        var tip = icon.gameObject.AddComponent<TooltipTrigger>();
        tip.Item = item;
        spawned.Add(icon.gameObject);
        if (iconFrames != null && iconFrames.Length > 1) StartCoroutine(Loop(icon, iconFrames, item.iconFps));
        if (r.evolution && frames != null && frames.Length > 1) StartCoroutine(Loop(slotImage, frames, 12f));

        string line = r.evolution ? "EVOLVED" : item is WeaponData ? (r.level <= 1 ? "NEW" : "Lv " + r.level) : "Lv " + r.level;
        var label = Text("Label", rewardsRow, r.evolution ? 30f : 26f, r.evolution ? new Color(1f, 0.72f, 0.3f) : new Color(0.3f, 0.14f, 0.1f));
        label.outlineWidth = r.evolution ? 0.2f : 0f;
        label.text = line;
        label.rectTransform.sizeDelta = new Vector2(160f, 40f);
        label.rectTransform.anchoredPosition = at + new Vector2(0f, -26f * Small * 0.5f - 18f);
        spawned.Add(label.gameObject);

        StartCoroutine(Pop(slotImage.rectTransform, icon, 0.2f));
        Play(lib?.rewardPop, 1f + index * 0.08f);
        if (r.evolution)
        {
            Play(lib?.revealLegendary, 1.1f);
            StartCoroutine(Flash(new Color(1f, 0.5f, 0.2f), 0.4f, 0.3f));
            StartCoroutine(ShakeScroll(0.3f, 14f));
        }
    }

    private static Vector2 FitIn(Vector2 size, float most)
    {
        float k = Mathf.Min(1f, most / Mathf.Max(size.x, size.y));
        return new Vector2(Mathf.Round(size.x * k), Mathf.Round(size.y * k));
    }

    private IEnumerator Pop(RectTransform frame, Image icon, float seconds)
    {
        var iconRt = icon.rectTransform;
        for (float t = 0f; t < seconds; t += Dt)
        {
            if (frame == null) yield break;
            float k = BackOut(t / seconds);
            frame.localScale = Vector3.one * k;
            iconRt.localScale = Vector3.one * k;
            icon.color = WithAlpha(icon.color, Mathf.Clamp01(t / seconds * 1.5f));
            yield return null;
        }
        if (frame != null) frame.localScale = Vector3.one;
        if (iconRt != null) iconRt.localScale = Vector3.one;
    }

    private IEnumerator Loop(Image image, Sprite[] frames, float fps)
    {
        float t = 0f;
        while (image != null)
        {
            t += Dt;
            image.sprite = frames[(int)(t * Mathf.Max(1f, fps)) % frames.Length];
            yield return null;
        }
    }

    private IEnumerator ShakeScroll(float seconds, float amount)
    {
        Vector2 rest = scrollBox.anchoredPosition;
        for (float t = 0f; t < seconds; t += Dt)
        {
            scrollBox.anchoredPosition = rest + UnityEngine.Random.insideUnitCircle * amount * (1f - t / seconds);
            yield return null;
        }
        scrollBox.anchoredPosition = rest;
    }

    // ---------------------------------------------------------------- particles

    // a twinkle drifting up in the light's colour
    private void Mote(Color color, Vector2 at, float speed = 1f)
    {
        var p = MakeParticle(lib?.mote, Small, new Vector2(7f, 7f));
        p.image.color = Color.Lerp(color, Color.white, 0.3f);
        p.image.rectTransform.anchoredPosition = at;
        p.velocity = new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(80f, 220f)) * speed;
        p.life = UnityEngine.Random.Range(0.6f, 1.1f);
        p.gravity = -30f;
    }

    // red paper and gold thrown out of the opening: squares a few art pixels across, tumbling and
    // falling; a legendary one throws wen too
    private void Confetti(EnvelopeRarity rarity, Vector2 at)
    {
        int n = rarity == EnvelopeRarity.Legendary ? 60 : rarity == EnvelopeRarity.Rare ? 38 : 24;
        var reds = new[] { new Color32(0xd0, 0x28, 0x38, 0xff), new Color32(0xff, 0x6a, 0x5a, 0xff), new Color32(0x7e, 0x14, 0x26, 0xff) };
        var golds = new[] { new Color32(0xf8, 0xd0, 0x68, 0xff), new Color32(0xff, 0xf0, 0xb0, 0xff), new Color32(0xc8, 0x88, 0x30, 0xff) };
        for (int i = 0; i < n; i++)
        {
            bool coin = rarity == EnvelopeRarity.Legendary && i % 5 == 0 && lib != null && lib.coinIcon.Length > 0;
            var p = coin ? MakeParticle(lib.coinIcon, 3f, new Vector2(16f, 16f)) : MakeParticle(null, 1f, Vector2.one);
            if (!coin)
            {
                float size = Small * UnityEngine.Random.Range(1, 3);
                p.image.rectTransform.sizeDelta = new Vector2(size * UnityEngine.Random.Range(1, 3), size);
                p.image.color = i % 2 == 0 ? reds[i % 3] : golds[i % 3];
            }
            p.image.rectTransform.anchoredPosition = at + UnityEngine.Random.insideUnitCircle * 20f;
            float a = UnityEngine.Random.Range(20f, 160f) * Mathf.Deg2Rad;
            float v = UnityEngine.Random.Range(350f, 950f) * (rarity == EnvelopeRarity.Legendary ? 1.25f : 1f);
            p.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v;
            p.gravity = 1400f;
            p.spin = UnityEngine.Random.Range(-720f, 720f);
            p.life = UnityEngine.Random.Range(1f, 1.7f);
        }
    }

    private Particle MakeParticle(Sprite[] frames, float scale, Vector2 artSize)
    {
        var p = motes.Find(m => m.image != null && !m.image.gameObject.activeSelf);
        if (p == null)
        {
            p = new Particle { image = Pixel("Particle", particles, null, 1f, Vector2.one) };
            motes.Add(p);
        }
        p.image.gameObject.SetActive(true);
        p.frames = frames;
        p.image.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        p.image.rectTransform.sizeDelta = (p.image.sprite != null ? p.image.sprite.rect.size : artSize) * scale;
        p.image.rectTransform.localRotation = Quaternion.identity;
        p.image.color = Color.white;
        p.age = 0f;
        p.spin = 0f;
        return p;
    }

    private void StepParticles(float dt)
    {
        foreach (var p in motes)
        {
            if (p.image == null || !p.image.gameObject.activeSelf) continue;
            p.age += dt;
            if (p.age >= p.life) { p.image.gameObject.SetActive(false); continue; }
            p.velocity.y -= p.gravity * dt;
            var rt = p.image.rectTransform;
            rt.anchoredPosition += p.velocity * dt;
            if (p.spin != 0f) rt.localRotation *= Quaternion.Euler(0f, 0f, p.spin * dt);
            float k = p.age / p.life;
            if (p.frames != null && p.frames.Length > 1) p.image.sprite = p.frames[Mathf.Min(p.frames.Length - 1, (int)(k * p.frames.Length))];
            if (k > 0.7f) p.image.color = WithAlpha(p.image.color, 1f - (k - 0.7f) / 0.3f);
        }
    }

    // ---------------------------------------------------------------- setting up, clearing away

    private void ResetStage()
    {
        stage.localScale = Vector3.one;
        stage.anchoredPosition = Vector2.zero;
        envelopeBox.gameObject.SetActive(true);
        envelopeBox.localRotation = Quaternion.identity;
        SetAlpha(envelopeBox, 1f);
        big.enabled = true;
        big.sprite = First(lib?.envelopeBig) ?? big.sprite;
        flap.enabled = false;
        front.enabled = false;
        raysA.color = raysB.color = new Color(1f, 1f, 1f, 0f);
        raysA.rectTransform.localScale = raysB.rectTransform.localScale = Vector3.one;
        aura.rectTransform.localScale = Vector3.one;
        aura.rectTransform.anchoredPosition = raysA.rectTransform.anchoredPosition = raysB.rectTransform.anchoredPosition = Vector2.zero;
        burst.enabled = false;
        scrollBox.SetParent(envelopeBox, false);
        scrollBox.SetSiblingIndex(front.transform.GetSiblingIndex());
        scrollBox.anchoredPosition = Vector2.zero;
        scrollBox.localScale = Vector3.one;
        scrollBox.gameObject.SetActive(false);
        SetScrollWidth(0f);
        banner.gameObject.SetActive(false);
        coinsIcon.gameObject.SetActive(false);
        coinsText.gameObject.SetActive(false);
        prompt.gameObject.SetActive(false);
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    private void Clear()
    {
        if (hum != null) hum.Stop();
        if (riser != null) riser.Stop();
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear();
        foreach (var p in motes) if (p.image != null) p.image.gameObject.SetActive(false);
        ItemTooltip.Hide(null);
    }

    private void PlaceEnvelope(Vector2 at) => envelopeBox.anchoredPosition = at;

    // the rollers either side of the paper as much of it as is unrolled
    private void SetScrollWidth(float width)
    {
        paperMask.sizeDelta = new Vector2(width, 52f * Small);
        float half = width * 0.5f + 10f * Small * 0.5f - Small * 2f;
        rollerL.rectTransform.anchoredPosition = new Vector2(-Mathf.Max(half, 10f * Small * 0.5f), 0f);
        rollerR.rectTransform.anchoredPosition = new Vector2(Mathf.Max(half, 10f * Small * 0.5f), 0f);
    }

    private void Tint(Color color)
    {
        aura.color = WithAlpha(color, aura.color.a);
    }

    private static void SetAlpha(RectTransform box, float alpha)
    {
        foreach (var g in box.GetComponentsInChildren<Graphic>(true)) g.color = WithAlpha(g.color, alpha);
    }

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    private static Sprite Frame(Sprite[] frames, float k)
    {
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.Clamp((int)(k * frames.Length), 0, frames.Length - 1)];
    }

    private void Play(AudioClip clip, float pitch, float volume = 1f)
    {
        if (clip == null || voice == null) return;
        var v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        v.Stop();
        v.pitch = pitch;
        v.PlayOneShot(clip, volume * (lib != null ? lib.envelopeVolume : 0.25f));
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        t = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }
}
