using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HealPickup : MonoBehaviour
{
    [Header("Heal Settings")]
    [SerializeField] private float healAmount = 10f; // 💚 Wieviel HP geheilt werden
    [SerializeField] private float moveSpeed = 8f;   // Bewegungsgeschwindigkeit beim Einsaugen

    [Header("Sound Settings")]
    [SerializeField] private AudioClip pickupSound;          // 🎵 Sound beim Aufheben
    [Range(0f, 1f)][SerializeField] private float pickupVolume = 1f;

    private bool pulling;
    private Transform target;

    public void PullTo(Transform t)
    {
        pulling = true;
        target = t;
    }

    private void Update()
    {
        if (!pulling || target == null) return;
        transform.position = Vector3.Lerp(transform.position, target.position, Time.deltaTime * moveSpeed);
    }

    private void OnDisable()
    {
        // pooled pickups must forget last life's magnet target or they fly off on spawn
        pulling = false;
        target = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // 🎵 Sound abspielen
        if (pickupSound != null)
            SfxPlayer.PlayAt(pickupSound, transform.position, pickupVolume);

        // 💚 Spieler heilen
        if (other.TryGetComponent(out PlayerHealth playerHealth))
        {
            playerHealth.Heal(healAmount);
            RunStats.PickedUpHeal();
            playerHealth.StartHealFlash(); //  visuelles Feedback
        }

        ObjectPool.Recycle(gameObject);
    }
}
