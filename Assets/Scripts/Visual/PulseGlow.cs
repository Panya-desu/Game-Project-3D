using UnityEngine;

/// <summary>
/// Gentle breathing of a light ray, a glowing portal or a light: changes alpha / emission / intensity.
/// </summary>
public class PulseGlow : MonoBehaviour
{
    public float speed = 0.25f;
    [Range(0f, 1f)] public float amount = 0.3f;
    public Light targetLight;

    Renderer rend;
    MaterialPropertyBlock block;
    Color baseColor;
    float baseIntensity;
    float phase;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(BaseColor))
        {
            block = new MaterialPropertyBlock();
            baseColor = rend.sharedMaterial.GetColor(BaseColor);
        }
        if (targetLight != null) baseIntensity = targetLight.intensity;
        phase = Random.value * 6f;
    }

    void Update()
    {
        float k = 1f - amount * (0.5f + 0.5f * Mathf.Sin((Time.time + phase) * speed * 2f * Mathf.PI));
        if (block != null)
        {
            Color c = baseColor;
            c.a = baseColor.a * k;
            block.SetColor(BaseColor, c);
            rend.SetPropertyBlock(block);
        }
        if (targetLight != null) targetLight.intensity = baseIntensity * k;
    }
}
