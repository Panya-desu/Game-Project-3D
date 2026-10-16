using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A key light for a character indoors, like the White Space sun gives her outdoors. Inside the house the
/// roof blocks the sun and she was lit only by room lamps from every side: her face and neck came out the
/// same flat colour with no shadow under the jaw, so her face looked long. This soft spot light sits
/// above and in front of her (a child of her root, so it follows her and goes away with her), lights and
/// shadows only the character rendering layer, and gives one clean chin shadow. Added by CharacterLibrary.Spawn.
/// </summary>
public static class CharacterKeyLight
{
    /// <summary>Rendering layer of cutscene characters (EchoToon checks it: this light's shadows always count).</summary>
    public const uint CharacterLayer = 1u << 5;
    const string Name = "KeyLight";

    static readonly FieldInfo TierField = typeof(UniversalAdditionalLightData)
        .GetField("m_AdditionalLightsShadowResolutionTier", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Add(GameObject character)
    {
        if (character == null) return;
        foreach (var r in character.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask |= CharacterLayer;

        // only under a roof: outdoors the sun already does this
        float s = character.transform.lossyScale.y;
        Vector3 head = character.transform.position + Vector3.up * 1.45f * s;
        if (!Physics.Raycast(head, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore)) return;

        var go = new GameObject(Name);
        go.transform.SetParent(character.transform, false);
        // above and in front of her face, about 50 degrees up
        go.transform.localPosition = new Vector3(0.35f, 2.9f, 1.25f);
        go.transform.LookAt(character.transform.TransformPoint(new Vector3(0f, 1.25f, 0f)));
        var light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = 55f;
        light.innerSpotAngle = 35f;
        light.range = 4f * s;
        light.intensity = 1.0f;
        light.color = new Color(1f, 0.93f, 0.84f);
        light.shadows = LightShadows.Soft;
        light.shadowBias = 0.02f;
        light.shadowNormalBias = 0.2f;
        light.shadowNearPlane = 0.2f;
        var data = go.AddComponent<UniversalAdditionalLightData>();
        data.renderingLayers = CharacterLayer;
        data.customShadowLayers = true;
        data.shadowRenderingLayers = CharacterLayer;
        if (TierField != null) TierField.SetValue(data, 2); // High: a sharp chin shadow
    }

    /// <summary>For the player's own shadow body: it is not lit, so it needs no key light.</summary>
    public static void Remove(GameObject character)
    {
        if (character == null) return;
        var key = character.transform.Find(Name);
        if (key != null) Object.Destroy(key.gameObject);
    }
}
