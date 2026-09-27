using UnityEngine;
using UnityEngine.UI;

// where a WenRain draws: one UI mesh holding every coin, put on the level up panel just over its
// dark backdrop by the WenRain itself
[RequireComponent(typeof(CanvasRenderer))]
public class WenRainLayer : MaskableGraphic
{
    [System.NonSerialized] public WenRain rain;

    public override Texture mainTexture => rain != null && rain.Texture != null ? rain.Texture : s_WhiteTexture;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (rain != null) rain.Fill(vh, rectTransform.rect, canvas, color);
    }
}
