using UnityEngine;

// whether an enemy knows where the player is, and which way that is. worked out when asked
// instead of every frame on every enemy; EnemySwarm reads the player's position directly
public class PlayerAwareness : MonoBehaviour
{
    [SerializeField]
    private float _playerAwarenessDistance;

    // found once and shared by every enemy, instead of a scene search per spawn
    private static Transform s_player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_player = null;

    public static Transform Player
    {
        get
        {
            if (s_player == null)
            {
                var player = FindFirstObjectByType<PlayerMovement>();
                if (player != null) s_player = player.transform;
            }
            return s_player;
        }
    }

    public bool AwareOfPlayer
    {
        get
        {
            var p = Player;
            return p != null && ((Vector2)(p.position - transform.position)).sqrMagnitude <= _playerAwarenessDistance * _playerAwarenessDistance;
        }
    }

    public Vector2 DirectionToPlayer
    {
        get
        {
            var p = Player;
            return p != null ? ((Vector2)(p.position - transform.position)).normalized : Vector2.zero;
        }
    }
}
