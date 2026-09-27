using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _smoothSpeed = 5f;
    [SerializeField] private float _cameraZ = -10f; // fixed z-distance from player
    [SerializeField] private Vector2 _offset = Vector2.zero; // optional x/y offset

    [Header("Shake")]
    [SerializeField] private float _shakeFalloff = 2.5f;

    private float _shakeAmplitude;
    private Vector2 _shakeSeed;

    // shake lives here rather than in its own component because LateUpdate writes
    // transform.position outright, so a separate shaker would be stomped depending on
    // which LateUpdate happened to run second
    public void Shake(float amplitude)
    {
        if (amplitude <= _shakeAmplitude) return;  // strongest wins, don't stack into nausea

        _shakeAmplitude = amplitude;
        _shakeSeed = new Vector2(Random.value * 100f, Random.value * 100f);
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 targetPos = new Vector3(
            _target.position.x + _offset.x,
            _target.position.y + _offset.y,
            _cameraZ // keep this constant!
        );

        transform.position = Vector3.Lerp(transform.position, targetPos, _smoothSpeed * Time.deltaTime);

        if (_shakeAmplitude <= 0.0001f) return;

        // unscaled so a shake still reads during hit stop, which is the whole point of pairing them
        float t = Time.unscaledTime * 18f;
        float x = (Mathf.PerlinNoise(_shakeSeed.x + t, 0f) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(0f, _shakeSeed.y + t) - 0.5f) * 2f;

        transform.position += new Vector3(x, y, 0f) * _shakeAmplitude;

        _shakeAmplitude = Mathf.MoveTowards(_shakeAmplitude, 0f, _shakeFalloff * Time.unscaledDeltaTime);
    }
}
