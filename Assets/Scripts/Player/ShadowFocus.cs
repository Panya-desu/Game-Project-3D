using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Her shadow, and sharp lamp shadows where she is.
/// Tiers: every shadow-casting lamp asked URP for its highest shadow resolution, so the shadow maps did
/// not fit the atlas and URP shrank them all. Far lamps are kept at the Low tier, the nearest lamp gets
/// High and the next Medium, re-checked as she walks (the furniture keeps sharp shadows near her).
/// Her shadow indoors: a real lamp shadow follows a point light, so it swung around her as she walked
/// under a lamp and jumped to a new direction whenever another lamp became the nearest. Indoors her body
/// is now drawn flattened onto the floor in one fixed direction (a locked planar shadow, as many games
/// do for the player character), so it never turns or jumps; no lamp draws her body any more. Where there
/// are no lamps (the White Space sun) she keeps the real shadow. Added by PlayerBody.
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

    // the flattened shadow
    Material planar;
    Transform body;
    float bodyHeight = 3f;
    // horizontal shadow length per metre of height, in the (world) direction it falls: always the same
    static readonly Vector2 LockedSlope = new Vector2(0.28f, -0.4f);
    const float ShadowAlpha = 0.36f;
    public Vector2 Slope => LockedSlope;
    public float Alpha => ShadowAlpha;
    public bool Planar => planar != null;
    static readonly int PlaneY = Shader.PropertyToID("_PlaneY"), LightDir = Shader.PropertyToID("_LightDir"),
        Origin = Shader.PropertyToID("_Origin"), MaxLength = Shader.PropertyToID("_MaxLength"), ShadowColor = Shader.PropertyToID("_ShadowColor");

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
        bool indoor = lamps.Count > 0 && UsePlanarShadow();
        // the sun (the white world) draws her real shadow; indoors the flattened one replaces it
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.shadows != LightShadows.None) SetBodyShadow(Data(l), !indoor);
        enabled = lamps.Count > 0;
    }

    bool UsePlanarShadow()
    {
        body = transform.Find("PlayerBody");
        var shader = Resources.Load<Shader>("Shaders/PlayerPlanarShadow");
        if (body == null || shader == null) return false;
        planar = new Material(shader) { name = "PlayerPlanarShadow (runtime)" };
        var rends = body.GetComponentsInChildren<Renderer>(true);
        float top = float.MinValue, bottom = float.MaxValue;
        foreach (var r in rends)
        {
            top = Mathf.Max(top, r.bounds.max.y);
            bottom = Mathf.Min(bottom, r.bounds.min.y);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = planar;
            r.sharedMaterials = mats;
        }
        if (top > bottom) bodyHeight = top - bottom;
        // her renderers sit around the camera: with their own bounds they were culled whenever she looked
        // ahead, and parts of the flattened shadow went missing; bounds that cover the shadow keep it whole
        float reach = (bodyHeight * 2f + 2f) / Mathf.Max(0.001f, body.lossyScale.y);
        foreach (var r in rends)
        {
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            r.localBounds = new Bounds(Vector3.zero, Vector3.one * reach);
        }
        planar.SetVector(LightDir, new Vector4(LockedSlope.x, -1f, LockedSlope.y, 0f));
        planar.SetFloat(MaxLength, bodyHeight * LockedSlope.magnitude + 0.4f);
        planar.SetColor(ShadowColor, new Color(0.02f, 0.02f, 0.05f, ShadowAlpha));
        return true;
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
            // without the flattened shadow (no shader / no body), the nearest lamp still draws her
            SetBodyShadow(lamps[i].data, planar == null && i == 0 && Score(lamps[i].light, p) < 1000f);
        }
    }

    void LateUpdate()
    {
        if (planar == null || body == null) return;
        Vector3 feet = body.position;
        planar.SetFloat(PlaneY, CharacterLibrary.GroundBelow(feet + Vector3.up * 0.3f).y);
        planar.SetVector(Origin, feet);
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
