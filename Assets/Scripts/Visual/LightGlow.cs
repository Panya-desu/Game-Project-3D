using UnityEngine;

/// <summary>
/// Fake volumetric glow (light shaft or ceiling halo) tied to a real Light: its brightness follows
/// the light's intensity, so it flickers with LightFlicker and dims when the storage power is out.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class LightGlow : MonoBehaviour
{
    public Light source;
    public Color color = Color.white;
    [Tooltip("Light intensity at which the glow shows its full colour")]
    public float referenceIntensity = 3f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    Renderer rend;
    MaterialPropertyBlock block;
    float last = -1f;

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        last = -1f;
        Refresh();
    }

    void LateUpdate()
    {
        Refresh();
    }

    /// <summary>Re-apply every glow now (used by the report capture after changing lights in the editor).</summary>
    public static void RefreshAll()
    {
        foreach (var g in FindObjectsByType<LightGlow>(FindObjectsSortMode.None)) { g.last = -1f; g.Refresh(); }
    }

    void Refresh()
    {
        if (rend == null) return;
        float k = source == null || !source.enabled || !source.gameObject.activeInHierarchy
            ? 0f
            : Mathf.Clamp01(source.intensity / referenceIntensity);
        if (Mathf.Abs(k - last) < 0.002f) return;
        last = k;
        block.SetColor(BaseColor, new Color(color.r, color.g, color.b, color.a * k));
        rend.SetPropertyBlock(block);
    }
}
