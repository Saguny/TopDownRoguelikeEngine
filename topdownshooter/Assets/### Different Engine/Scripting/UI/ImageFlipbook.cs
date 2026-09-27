using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// plays a looping flipbook on a UI Image, e.g. the animated title screen behind the main menu.
// adding it fills Frames with every sprite in Frames Folder, sorted by name, and shows the first
[RequireComponent(typeof(Image))]
public class ImageFlipbook : MonoBehaviour
{
    [Tooltip("played in order, then from the start again")]
    [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
    [SerializeField, Min(0.1f)] private float framesPerSecond = 10f;
    [Tooltip("keeps playing while time is paused, so the menu never freezes")]
    [SerializeField] private bool unscaledTime = true;
    [Tooltip("editor only: where Frames is filled from when the component is added or the list is emptied")]
    [SerializeField] private string framesFolder = "Assets/### Different Engine/NewSprites/Sprites/UI/TitleLoop";

    private Image image;
    private float time;
    private int shown = -1;

    private void Awake() => image = GetComponent<Image>();

    private void OnEnable()
    {
        time = 0f;
        Show(0);
    }

    private void Update()
    {
        if (frames.Length == 0) return;

        time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        time %= frames.Length / framesPerSecond;
        int frame = Mathf.Min(frames.Length - 1, (int)(time * framesPerSecond));
        if (frame != shown) Show(frame);
    }

    private void Show(int frame)
    {
        if (frames.Length == 0 || image == null) return;
        shown = frame;
        image.sprite = frames[frame];
    }

#if UNITY_EDITOR
    private void Reset() => FillFromFolder();

    private void OnValidate()
    {
        if (frames != null && frames.Length > 0) return;
        UnityEditor.EditorApplication.delayCall -= FillIfStillEmpty;
        UnityEditor.EditorApplication.delayCall += FillIfStillEmpty;
    }

    private void FillIfStillEmpty()
    {
        if (this != null && (frames == null || frames.Length == 0)) FillFromFolder();
    }

    [ContextMenu("Fill Frames From Folder")]
    private void FillFromFolder()
    {
        if (string.IsNullOrEmpty(framesFolder) || !UnityEditor.AssetDatabase.IsValidFolder(framesFolder)) return;

        frames = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { framesFolder })
            .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(sprite => sprite != null)
            .ToArray();

        if (frames.Length > 0 && TryGetComponent(out Image target))
        {
            UnityEditor.Undo.RecordObject(target, "Show first flipbook frame");
            target.sprite = frames[0];
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
