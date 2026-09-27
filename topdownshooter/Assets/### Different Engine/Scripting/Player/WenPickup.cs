using UnityEngine;

public class WenPickup : MonoBehaviour
{
    public int amount = 1;                  // Anzahl Wen, die dieses Objekt gibt
    public AudioClip pickupSound;           // Sound beim Aufsammeln
    public float pickupSoundVolume = 1f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // Player Inventory
            PlayerInventory playerInventory = collision.GetComponent<PlayerInventory>();
            if (playerInventory != null)
            {
                playerInventory.AddWen(amount);
            }
            RunStats.PickedUpWen(amount);

            // Sound abspielen
            if (pickupSound != null)
            {
                SfxPlayer.PlayAt(pickupSound, transform.position, pickupSoundVolume);
            }

            // Pickup zerstören
            ObjectPool.Recycle(gameObject);
        }
    }
}
