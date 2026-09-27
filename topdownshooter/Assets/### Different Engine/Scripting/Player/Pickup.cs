using UnityEngine;
using UnityEngine.Serialization;

// what a wen pickup is worth and how it sounds. the prefab is only read for its settings and its
// look: drops go straight into PickupSystem as data (a position each), which draws them all as one
// mesh and collects them in one loop. one placed in a scene by hand turns itself into data too
public class Pickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField, FormerlySerializedAs("gears")] private int wen = 1;

    [Header("Pickup Sound")]
    [SerializeField] private AudioClip pickupSound;
    [Range(0f, 1f)][SerializeField] private float pickupVolume = 1f;

    public int Wen => wen;
    public AudioClip Sound => pickupSound;
    public float Volume => pickupVolume;

    private void OnEnable()
    {
        if (Application.isPlaying) PickupSystem.Adopt(this);
    }

    // for anything that still pulls a pickup in by hand
    public void PullTo(Transform _) { }
}
