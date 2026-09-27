using UnityEngine;

public class FinalRushArenaController : MonoBehaviour
{
    public static FinalRushArenaController Instance { get; private set; }

    [SerializeField] private FinalRushArena arenaPrefab;
    [SerializeField] private Transform playerOverride;

    private FinalRushArena activeArena;

    public bool HasArena => activeArena != null;
    public Vector3 Center => activeArena != null ? activeArena.transform.position : Vector3.zero;
    // where the player is held: just inside the ring of seals
    public float Radius => activeArena != null ? activeArena.WalkRadius : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        GameEvents.OnFinalRushStarted += HandleFinalRushStarted;
        GameEvents.OnFinalRushEnded += HandleFinalRushEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnFinalRushStarted -= HandleFinalRushStarted;
        GameEvents.OnFinalRushEnded -= HandleFinalRushEnded;
    }

    private void HandleFinalRushStarted(int waveIndex, int quota)
    {
        if (arenaPrefab == null)
            return;

        if (activeArena != null)
        {
            Destroy(activeArena.gameObject);
            activeArena = null;
        }

        Transform playerTransform = playerOverride;

        if (playerTransform == null)
        {
            var player = FindFirstObjectByType<PlayerMovement>(); // or your player controller type
            if (player != null)
                playerTransform = player.transform;
        }

        if (playerTransform == null)
            return;

        var arenaInstance = Instantiate(arenaPrefab);
        arenaInstance.Initialize(playerTransform.position);
        activeArena = arenaInstance;
    }

    private void HandleFinalRushEnded(int waveIndex)
    {
        if (activeArena == null)
            return;

        Destroy(activeArena.gameObject);
        activeArena = null;
    }
}
