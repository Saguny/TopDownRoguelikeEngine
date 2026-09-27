using UnityEngine;

// marker dropped on pooled instances so they know which pool to return to
public class PooledObject : MonoBehaviour
{
    public ObjectPool Owner;
    public bool InPool;
}
