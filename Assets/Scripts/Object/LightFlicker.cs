using UnityEngine;

/// <summary>
/// Makes a light feel alive: old fluorescent tube flicker, TV screen glow, or a slow emergency pulse.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    public enum Mode { Fluorescent, TvGlow, Pulse }

    public Mode mode = Mode.Fluorescent;
    [Range(0f, 1f)] public float strength = 0.25f;
    public float speed = 1f;

    Light lightSource;
    float baseIntensity;
    float seed;
    float dropTimer;

    void Awake()
    {
        lightSource = GetComponent<Light>();
        baseIntensity = lightSource.intensity;
        seed = Random.value * 100f;
    }

    /// <summary>Call after changing the intensity from code (PowerOutage does this).</summary>
    public void SetBaseIntensity(float value)
    {
        baseIntensity = value;
    }

    void Update()
    {
        float t = Time.time * speed + seed;
        float k = 1f;
        switch (mode)
        {
            case Mode.Fluorescent:
                // Mostly steady with a quick dip every few seconds
                dropTimer -= Time.deltaTime;
                if (dropTimer <= 0f) dropTimer = Random.Range(2f, 7f);
                k = dropTimer < 0.12f ? 1f - strength * 2.5f : 1f - strength * 0.15f * Mathf.PerlinNoise(t * 6f, 0f);
                break;
            case Mode.TvGlow:
                k = 1f - strength * Mathf.PerlinNoise(t * 3f, 1f);
                break;
            case Mode.Pulse:
                k = 1f - strength * (0.5f + 0.5f * Mathf.Sin(t * 2.5f));
                break;
        }
        lightSource.intensity = baseIntensity * Mathf.Max(0f, k);
    }
}
