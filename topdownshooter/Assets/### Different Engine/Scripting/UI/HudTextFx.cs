using TMPro;
using UnityEngine;

// the HUD's words and numbers (the run clock, the wave, the kills, the coins), alive: a dark outline
// that breathes, a soft shadow under it, and every few seconds a glint of gold running across the
// letters, each text on its own beat. the run clock while it's stopped (Frozen: from a Final Rush
// won until the next wave, and through the final boss) wears an outline pulsing between ice blue
// and red instead, with a glow of the other colour behind it and the two rippling through its
// digits. added by UIWaveAndTimer and RunCounterText to their texts
[RequireComponent(typeof(TMP_Text))]
public class HudTextFx : MonoBehaviour
{
    public bool Frozen { get; set; }

    private static readonly Color Ink = new Color(0.09f, 0.04f, 0.13f, 1f);
    private static readonly Color Shadow = new Color(0.05f, 0.02f, 0.08f, 0.65f);
    private static readonly Color Glint = new Color(1f, 0.86f, 0.42f, 1f);
    private static readonly Color Ice = new Color(0.35f, 0.72f, 1f, 1f);
    private static readonly Color Ember = new Color(1f, 0.24f, 0.3f, 1f);

    private const float GlintEvery = 4.5f;      // seconds between glints
    private const float GlintSweep = 0.7f;      // seconds a glint takes to cross the text

    private TMP_Text text;
    private Material material;
    private float phase;

    // adds it to a text if it hasn't got one, and hands it back
    public static HudTextFx On(TMP_Text t)
    {
        if (t == null) return null;
        return t.TryGetComponent(out HudTextFx fx) ? fx : t.gameObject.AddComponent<HudTextFx>();
    }

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        phase = Random.value * GlintEvery;
        material = MakeMaterial(text);
        if (material == null) return;
        text.fontMaterial = material;
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
    }

    // its own copy of the font's material, so its outline can move alone. the game's font is drawn
    // with TMP's Bitmap shader, which has no outline or underlay; its atlas is a distance field, so
    // the copy is switched to a Distance Field shader, which has both
    private static Material MakeMaterial(TMP_Text t)
    {
        var shared = t.fontSharedMaterial;
        if (shared == null) return null;
        var m = new Material(shared) { name = shared.name + " (HUD)" };
        // the mobile one: TMP's default font (in Resources) uses it, so it's always in a build
        var sdf = Shader.Find("TextMeshPro/Mobile/Distance Field");
        if (sdf == null) sdf = Shader.Find("TextMeshPro/Distance Field");
        var font = t.font;
        if (sdf != null && m.shader != sdf && font != null)
        {
            m.shader = sdf;
            m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            m.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
            m.SetFloat(ShaderUtilities.ID_TextureWidth, font.atlasWidth);
            m.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasHeight);
            m.SetFloat(ShaderUtilities.ID_WeightNormal, font.normalStyle);
            m.SetFloat(ShaderUtilities.ID_WeightBold, font.boldStyle);
        }
        return m;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    private void LateUpdate()
    {
        if (text == null || !text.isActiveAndEnabled) return;
        float t = Time.unscaledTime;
        Outline(t);
        Letters(t);
    }

    private void Outline(float t)
    {
        if (material == null) return;
        if (Frozen)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.Lerp(Ice, Ember, k));
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.26f + 0.05f * Mathf.Sin(t * 9f));
            // a glow of the other colour behind it
            var glow = Color.Lerp(Ember, Ice, k);
            glow.a = 0.75f;
            material.SetColor(ShaderUtilities.ID_UnderlayColor, glow);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.55f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
        }
        else
        {
            // the dark outline breathing a little, a drop shadow under it
            material.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f + 0.03f * Mathf.Sin(t * 2.2f + phase));
            material.SetColor(ShaderUtilities.ID_UnderlayColor, Shadow);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.55f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        }
        // so the outline and the glow get room round each letter
        ShaderUtilities.UpdateShaderRatios(material);
    }

    // each letter's colour: the gold glint sweeping across, or the blue and red rippling through
    private void Letters(float t)
    {
        text.ForceMeshUpdate();
        var info = text.textInfo;
        if (info == null || info.characterCount == 0) return;

        Color32 baseColor = text.color;
        int count = info.characterCount;
        float cycle = (t + phase) % GlintEvery;
        float sweep = cycle < GlintSweep ? cycle / GlintSweep * (count + 3f) - 1.5f : -99f;

        for (int i = 0; i < count; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            Color c = baseColor;
            if (Frozen)
            {
                float w = 0.5f + 0.5f * Mathf.Sin(t * 6f - i * 0.9f);
                c = Color.Lerp(c, Color.Lerp(Ice, Ember, w), 0.35f);
            }
            else
            {
                float g = Mathf.Clamp01(1f - Mathf.Abs(i - sweep) / 1.4f);
                if (g > 0f) c = Color.Lerp(c, Glint, g * 0.85f);
            }
            c.a = baseColor.a / 255f;
            Color32 c32 = c;
            var colors = info.meshInfo[ch.materialReferenceIndex].colors32;
            int v = ch.vertexIndex;
            colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c32;
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
