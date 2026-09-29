using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// the spell card's cut-in, Persona's カットイン: a ragged white tear zips across the screen, splits
// open into a lens of white, and in one frame the boss's eyes are there in a torn band, a wedge
// of their colour to one side; a brushed character is written beside them stroke by stroke as the
// screen flashes white; the band holds, pushing in; then it's sliced into strips that slide apart,
// shred, and fly off the top right in shards. about a second, on unscaled time, gameplay going on
// behind it. whatever should happen unseen (the duel's regroup) happens as the eyes land, under
// the flash
//
// it's drawn as pixel art, 480x270 frames shown over the whole screen, point filtered. painting a
// frame is too slow to do while it plays, so the boss has it baked when the fight starts (Prepare):
// every frame painted on a worker thread, then handed to the GPU a few at a time, and the cut-in
// is only a flipbook. asked for one that isn't ready, it paints live instead. the eyes come from Resources/CutIn/<name> (any size, PNG; it's
// fitted to cover the band, the face a little right of centre), so art can be dropped in without
// touching code. without one, the boss's own 112px portrait stands in, cropped to its eyes
public class PersonaCutIn : MonoBehaviour
{
    public struct Theme
    {
        public string art;               // Resources/CutIn/<art>
        public string fallback;          // a portrait strip under Resources, its first frame the face
        public float fallbackEyes;       // how far up the fallback portrait its eyes are (0 bottom, 1 top)
        public Color wedge, wedgeDark;   // the band's colour to one side
        public Glyph glyph;              // the character written beside the eyes
        public string sound;
    }

    public enum Glyph { Judge, Forget }
    public const float Seconds = CutInPainter.End;

    private const int W = CutInPainter.W, H = CutInPainter.H, FW = CutInPainter.FW, FH = CutInPainter.FH;

    public const int Fps = 30;
    private static int FrameCount => Mathf.CeilToInt(CutInPainter.End * Fps);

    private static PersonaCutIn instance;
    private RawImage image;
    private Texture2D tex;
    private readonly CutInPainter painter = new CutInPainter();

    // a cut-in baked ahead: its frames painted (off the main thread), then uploaded one by one
    private class Baked
    {
        public Color32[][] pixels;
        public Texture2D[] frames;
        public volatile bool painted;
        public bool Ready => frames != null && frames[frames.Length - 1] != null;
    }
    private readonly System.Collections.Generic.Dictionary<string, Baked> baked = new System.Collections.Generic.Dictionary<string, Baked>();
    private static string Key(Theme t) => t.art + "|" + t.fallback + "|" + t.glyph + "|" + ColorUtility.ToHtmlStringRGB(t.wedge) + ColorUtility.ToHtmlStringRGB(t.wedgeDark);
    private static readonly System.Collections.Generic.Dictionary<string, Color32[]> faces = new System.Collections.Generic.Dictionary<string, Color32[]>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; faces.Clear(); }

    // bakes a cut-in ahead of its first card; called as the fight starts
    public static void Prepare(Theme theme)
    {
        if (instance == null) instance = Build();
        string key = Key(theme);
        if (instance.baked.ContainsKey(key)) return;
        var bake = new Baked();
        instance.baked[key] = bake;
        var face = Face(theme);                   // reading the art back needs the main thread
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var p = new CutInPainter();
                var all = new Color32[FrameCount][];
                for (int i = 0; i < all.Length; i++)
                {
                    p.Draw(theme.glyph, theme.wedge, theme.wedgeDark, face, i / (float)Fps);
                    all[i] = (Color32[])p.Pixels.Clone();
                }
                bake.pixels = all;
            }
            catch (System.Exception e) { Debug.LogException(e); }   // no flipbook: it'll paint live
            bake.painted = true;
        });
        instance.StartCoroutine(instance.Upload(bake));
    }

    // the painted frames onto the GPU, a few a frame so it doesn't hitch; their CPU copies let go
    private IEnumerator Upload(Baked bake)
    {
        while (!bake.painted) yield return null;
        if (bake.pixels == null) yield break;
        var frames = new Texture2D[bake.pixels.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            var f = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Cut-In " + i };
            f.SetPixels32(bake.pixels[i]);
            f.Apply(false, true);
            bake.pixels[i] = null;
            frames[i] = f;
            if (i % 4 == 3) yield return null;
        }
        bake.pixels = null;
        bake.frames = frames;
    }

    public static void Play(Theme theme, System.Action onLand = null)
    {
        if (instance == null) instance = Build();
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.Run(theme, onLand));
    }

    private static PersonaCutIn Build()
    {
        var go = new GameObject("Persona Cut-In", typeof(RectTransform), typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35;                // over the boss screen's dim, under the card's name (36)
        var ci = go.AddComponent<PersonaCutIn>();
        var imgGo = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
        var rt = (RectTransform)imgGo.transform;
        rt.SetParent(go.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        ci.image = imgGo.GetComponent<RawImage>();
        ci.image.raycastTarget = false;
        ci.tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Cut-In" };
        ci.image.texture = ci.tex;
        ci.image.enabled = false;
        return ci;
    }

    private void OnDestroy()
    {
        if (tex != null) Destroy(tex);
        foreach (var b in baked.Values)
            if (b.frames != null) foreach (var f in b.frames) if (f != null) Destroy(f);
        if (instance == this) instance = null;
    }

    // the frame covers the screen whatever its shape, cropping the excess (the overlay canvas works
    // in screen pixels)
    private void Cover()
    {
        float a = (float)W / H, sw = Screen.width, sh = Screen.height;
        image.rectTransform.sizeDelta = sw / sh > a ? new Vector2(sw, sw / a) : new Vector2(sh * a, sh);
    }

    // ---------------------------------------------------------------- the eyes

    // the face as a FW x FH buffer: the dropped-in art fitted to cover, or the portrait's eyes
    private static Color32[] Face(Theme t)
    {
        string key = t.art + "|" + t.fallback;
        if (faces.TryGetValue(key, out var cached)) return cached;
        Texture src = Resources.Load<Texture2D>("CutIn/" + t.art);
        Vector2 scale = Vector2.one, offset = Vector2.zero;
        float aspect = (float)FW / FH;
        if (src != null)
        {
            // cover: fill the band, cropping the art's excess top and bottom (or sides)
            float a = (float)src.width / src.height;
            if (a > aspect) { scale = new Vector2(aspect / a, 1f); offset = new Vector2((1f - scale.x) * 0.5f, 0f); }
            else { scale = new Vector2(1f, a / aspect); offset = new Vector2(0f, (1f - scale.y) * 0.5f); }
        }
        else
        {
            var frames = YamaArt.Strip(t.fallback, 100f);
            if (frames == null || frames.Length == 0) { faces[key] = null; return null; }
            var s = frames[0];
            src = s.texture;
            // the first frame of the strip, a band across it round the eyes as wide as the band's aspect
            var r = s.textureRect;
            float w = r.width / src.width, h = r.width / aspect / src.height;
            float cy = (r.y + r.height * t.fallbackEyes) / src.height;
            scale = new Vector2(w, h);
            offset = new Vector2(r.x / src.width, cy - h * 0.5f);
        }
        var rt = RenderTexture.GetTemporary(FW, FH, 0, RenderTextureFormat.ARGB32);
        var mode = src.filterMode;
        src.filterMode = FilterMode.Point;
        Graphics.Blit(src, rt, scale, offset);
        src.filterMode = mode;
        var was = RenderTexture.active;
        RenderTexture.active = rt;
        var read = new Texture2D(FW, FH, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, FW, FH), 0, 0);
        read.Apply();
        RenderTexture.active = was;
        RenderTexture.ReleaseTemporary(rt);
        var face = read.GetPixels32();
        Destroy(read);
        faces[key] = face;
        return face;
    }

    // ---------------------------------------------------------------- the run

    private IEnumerator Run(Theme t, System.Action onLand)
    {
        baked.TryGetValue(Key(t), out var bake);
        bool flipbook = bake != null && bake.Ready;
        var face = flipbook ? null : Face(t);
        image.texture = tex;
        image.enabled = true;
        if (!string.IsNullOrEmpty(t.sound)) YamaArt.Play(t.sound, Camera.main != null ? (Vector2)Camera.main.transform.position : Vector2.zero, 0.9f);
        for (float time = 0f; time < CutInPainter.End; time += Time.unscaledDeltaTime)
        {
            Cover();
            if (onLand != null && time >= CutInPainter.Lands) { onLand(); onLand = null; }
            if (flipbook) image.texture = bake.frames[Mathf.Min(bake.frames.Length - 1, (int)(time * Fps))];
            else
            {
                painter.Draw(t.glyph, t.wedge, t.wedgeDark, face, time);
                tex.SetPixels32(painter.Pixels);
                tex.Apply(false);
            }
            yield return null;
        }
        onLand?.Invoke();
        image.enabled = false;
    }

}
