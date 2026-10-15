using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sharp shadows where she is. Every shadow-casting lamp in a level asked URP for its highest shadow
/// resolution (the "High" tier is the default; Light.shadowResolution is ignored by URP), so all those
/// shadow maps did not fit the atlas and URP shrank every one of them: her shadow became a soft oval
/// blob. This keeps far lamps at the Low tier and gives the lamp nearest to the player the High tier and
/// the next one Medium, re-checked as she walks.
/// Her body is also on its own rendering layer, and only the nearest lamp (and the sun) includes that layer
/// in its shadow layers: with every lamp casting her shadow she got a fan of shadows in all directions;
/// now she has one clear shadow, while the furniture keeps its shadows from every lamp. Added by PlayerBody.
/// </summary>
public class ShadowFocus : MonoBehaviour
{
    const int Low = 0, Medium = 1, High = 2;
    /// <summary>Rendering layer of the player's body (layer 0 is the default every lamp shadows).</summary>
    public const uint BodyLayer = 1u << 4;
    static readonly FieldInfo TierField = typeof(UniversalAdditionalLightData)
        .GetField("m_AdditionalLightsShadowResolutionTier", BindingFlags.Instance | BindingFlags.NonPublic);

    readonly List<(Light light, UniversalAdditionalLightData data)> lamps = new List<(Light, UniversalAdditionalLightData)>();
    float next;

    void Start()
    {
        if (TierField == null) { enabled = false; return; }
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional || l.shadows == LightShadows.None) continue;
            var data = Data(l);
            lamps.Add((l, data));
            TierField.SetValue(data, Low);
            SetBodyShadow(data, false);
        }
        // the sun (the white world) always draws her shadow
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.shadows != LightShadows.None) SetBodyShadow(Data(l), true);
        enabled = lamps.Count > 0;
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.25f;
        Vector3 p = transform.position + Vector3.up;
        lamps.Sort((a, b) => Score(a.light, p).CompareTo(Score(b.light, p)));
        for (int i = 0; i < lamps.Count; i++)
        {
            TierField.SetValue(lamps[i].data, i == 0 ? High : i == 1 ? Medium : Low);
            SetBodyShadow(lamps[i].data, i == 0 && Score(lamps[i].light, p) < 1000f);
        }
    }

    static UniversalAdditionalLightData Data(Light l) =>
        l.TryGetComponent(out UniversalAdditionalLightData d) ? d : l.gameObject.AddComponent<UniversalAdditionalLightData>();

    static void SetBodyShadow(UniversalAdditionalLightData data, bool on)
    {
        uint mask = data.customShadowLayers ? (uint)data.shadowRenderingLayers : (uint)data.renderingLayers;
        mask = on ? mask | BodyLayer : mask & ~BodyLayer;
        data.customShadowLayers = true;
        data.shadowRenderingLayers = mask;
    }

    // lamps that reach her and are close first; switched-off or out-of-range lamps last
    static float Score(Light l, Vector3 p)
    {
        if (l == null || !l.isActiveAndEnabled || l.intensity <= 0f) return float.MaxValue;
        float d = Vector3.Distance(l.transform.position, p);
        return d <= l.range ? d : 1000f + d;
    }
}
