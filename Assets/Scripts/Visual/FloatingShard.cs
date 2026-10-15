using UnityEngine;

/// <summary>
/// Slow bobbing and spinning for the floating memory shards in the white world.
/// </summary>
public class FloatingShard : MonoBehaviour
{
    public float bobHeight = 0.35f;
    public float bobSpeed = 0.35f;
    public Vector3 spinSpeed = new Vector3(0f, 8f, 3f);
    public float phase;

    Vector3 startPos;
    Quaternion startRot;

    void Start()
    {
        startPos = transform.localPosition;
        startRot = transform.localRotation;
        if (phase == 0f) phase = Random.value * 10f;
    }

    void Update()
    {
        float t = Time.time + phase;
        transform.localPosition = startPos + Vector3.up * Mathf.Sin(t * bobSpeed * 2f * Mathf.PI) * bobHeight;
        transform.localRotation = startRot * Quaternion.Euler(spinSpeed * t);
    }
}
