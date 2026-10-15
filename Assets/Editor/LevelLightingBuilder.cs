using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Gives Level_1 the same soft, dreamy look as the title screen: warm ambient bounce instead of
/// black ceilings, film-like grading with bloom, a light haze, ceiling halos and soft light shafts
/// under the lamps, floating dust in every room, soft shadows, and per-area grading
/// (warm home, cold storage, dark blackout while the power is out, warm library).
/// Everything is created under "__Atmosphere", so the step can be re-run (Tools/Game Setup/Level Lighting).
/// </summary>
public static class LevelLightingBuilder
{
    const string Root = "__Atmosphere";
    const string Dir = "Assets/Art/Generated/Level1";
    // storage + storeroom + library: from 0.8 m inside the storage wall (x 22.7) to the library's far wall
    static readonly Vector3 StorageCentre = new Vector3(38.1f, 3f, 20.8f);
    static readonly Vector3 StorageSize = new Vector3(28.8f, 7f, 40f);

    static Material rayMat, haloMat, moteMat;

    [MenuItem("Tools/Game Setup/Level Lighting")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        var old = GameObject.Find(Root);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(Root).transform;
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Art/Generated", "Level1");
        Materials();

        // ── ambient bounce and haze: ceilings and shadowed sides are no longer pitch black ──
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.41f, 0.38f);
        RenderSettings.ambientEquatorColor = new Color(0.32f, 0.28f, 0.25f);
        RenderSettings.ambientGroundColor = new Color(0.13f, 0.11f, 0.1f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.26f, 0.22f, 0.2f);
        RenderSettings.fogDensity = 0.022f;

        // ── grading volumes ──
        var global = Volume(root, "Global", true, Vector3.zero, Vector3.zero, Profile("Level1_Global", p =>
        {
            var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.75f); ca.contrast.Override(12f); ca.saturation.Override(10f);
            ca.colorFilter.Override(new Color(1f, 0.97f, 0.93f));
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.75f); bloom.intensity.Override(1.3f); bloom.scatter.Override(0.82f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.78f));
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.3f); vig.smoothness.Override(0.45f); vig.color.Override(new Color(0.12f, 0.07f, 0.05f));
            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.94f, 0.97f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1.0f, 0.93f, 0f));
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(6f);
        }));
        global.priority = 0;

        // storage, storeroom (x >= 22.7) and library: colder fluorescent mood
        var storageVol = Volume(root, "Storage", true, StorageCentre, StorageSize, Profile("Level1_Storage", p =>
        {
            var wb = p.Add<WhiteBalance>(false); wb.temperature.Override(-16f); wb.tint.Override(-4f);
            var ca = p.Add<ColorAdjustments>(false); ca.postExposure.Override(0.9f); ca.saturation.Override(-8f); ca.contrast.Override(18f);
        }), 1, 0.4f);
        storageVol.gameObject.AddComponent<StorageZone>(); // weight follows the rooms, see StorageZone
        Volume(root, "Library", false, new Vector3(45.2f, 3f, 18.3f), new Vector3(14.6f, 7f, 14.6f), Profile("Level1_Library", p =>
        {
            var wb = p.Add<WhiteBalance>(false); wb.temperature.Override(14f);
            var ca = p.Add<ColorAdjustments>(false); ca.saturation.Override(6f);
        }), 2, 1.5f);
        var blackout = Volume(root, "Blackout", true, StorageCentre, StorageSize, Profile("Level1_Blackout", p =>
        {
            var ca = p.Add<ColorAdjustments>(false);
            ca.postExposure.Override(0.35f); ca.saturation.Override(-40f); ca.contrast.Override(22f);
            var wb = p.Add<WhiteBalance>(false); wb.temperature.Override(-22f);
            var vig = p.Add<Vignette>(false);
            vig.intensity.Override(0.55f); vig.smoothness.Override(0.55f); vig.color.Override(new Color(0.2f, 0.01f, 0.01f));
            var bloom = p.Add<Bloom>(false); bloom.intensity.Override(1.6f); bloom.tint.Override(new Color(1f, 0.5f, 0.45f));
        }), 3, 0.4f);
        // red emergency lights must still read clearly through the dark grading
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.name.StartsWith("Emergency")) { l.intensity = 4.5f; l.range = l.transform.position.y < 4f ? 4f : 11f; l.shadows = LightShadows.Hard; l.shadowResolution = LightShadowResolution.Low; EditorUtility.SetDirty(l); }
        blackout.weight = 0f; // StoragePower raises it while the power is out
        var power = Object.FindAnyObjectByType<StoragePower>();
        if (power != null)
        {
            power.blackoutVolume = blackout;
            EditorUtility.SetDirty(power);
        }

        // ── lamps: soft shadows, ceiling halo and a soft light shaft under each one ──
        var glow = new GameObject("LampGlow").transform; glow.SetParent(root, false);
        int shafts = 0;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type != LightType.Point || l.name.StartsWith("Emergency") || l.name.Contains("Glow")) continue;
            if (l.transform.IsChildOf(root)) continue;
            Vector3 p = l.transform.position;
            if (p.y < 3.2f) continue; // table lamps and props: no ceiling glow

            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.7f;
            l.shadowResolution = LightShadowResolution.Low;
            l.range = Mathf.Max(l.range, 11f);
            EditorUtility.SetDirty(l);

            float ceiling = CeilingAbove(p);
            Color c = Color.Lerp(l.color, Color.white, 0.35f);
            var halo = Quad(glow, "CeilingHalo", new Vector3(p.x, ceiling - 0.03f, p.z), Quaternion.Euler(-90f, 0f, 0f), new Vector3(5.5f, 5.5f, 1f), haloMat);
            Link(halo, l, new Color(c.r, c.g, c.b, 0.6f));
            float h = Mathf.Max(2.4f, ceiling - 0.2f);
            for (int i = 0; i < 3; i++)
            {
                var ray = Quad(glow, "LightShaft", new Vector3(p.x, ceiling - h * 0.5f, p.z), Quaternion.Euler(0f, 30f + 60f * i, 0f), new Vector3(2.2f, h, 1f), rayMat);
                Link(ray, l, new Color(c.r, c.g, c.b, 0.035f));
            }
            shafts++;
        }

        // ── soft fill light in every room, like light bounced off the walls ──
        var fills = new GameObject("RoomFill").transform; fills.SetParent(root, false);
        var storageFills = new List<Light>
        {
            Fill(fills, "Storage_South", new Vector3(30.6f, 2.6f, 7.0f), new Color(0.8f, 0.88f, 1f), 1.3f, 7.4f),
            Fill(fills, "Storage_North", new Vector3(30.6f, 2.6f, 19.5f), new Color(0.8f, 0.88f, 1f), 1.3f, 7.4f),
            Fill(fills, "Storeroom", new Vector3(30.6f, 2.6f, 33.3f), new Color(0.82f, 0.88f, 1f), 0.8f, 7.4f),
            Fill(fills, "Library", new Vector3(45.2f, 2.6f, 18.3f), new Color(1f, 0.8f, 0.6f), 0.9f, 7.4f),
        };
        Fill(fills, "Bedroom", new Vector3(0.2f, 2.6f, 4.1f), new Color(1f, 0.82f, 0.64f), 1.05f, 7.4f);
        Fill(fills, "Kitchen", new Vector3(15.2f, 2.6f, 7.0f), new Color(1f, 0.9f, 0.78f), 1.45f, 7.4f);
        Fill(fills, "Living", new Vector3(15.2f, 2.6f, 20.0f), new Color(1f, 0.84f, 0.66f), 1.7f, 7.4f);
        if (power != null)
        {
            power.roomLights.RemoveAll(x => x == null);
            power.roomLights.AddRange(storageFills);
            EditorUtility.SetDirty(power);
        }

        // ── floating dust in every room ──
        var dust = new GameObject("Dust").transform; dust.SetParent(root, false);
        Dust(dust, "Bedroom", new Vector3(0.2f, 2.2f, 4.1f), new Vector3(13f, 3.5f, 13f), 10, new Color(1f, 0.85f, 0.65f));
        Dust(dust, "House", new Vector3(15.2f, 2.2f, 13.3f), new Vector3(13f, 3.5f, 23f), 14, new Color(1f, 0.88f, 0.7f));
        Dust(dust, "Storage", new Vector3(30.2f, 2.2f, 13.3f), new Vector3(13f, 3.5f, 23f), 16, new Color(0.8f, 0.88f, 1f));
        Dust(dust, "Storeroom", new Vector3(30.2f, 2.2f, 33.3f), new Vector3(13f, 3.5f, 13f), 10, new Color(0.85f, 0.9f, 1f));
        Dust(dust, "Library", new Vector3(45.2f, 2.2f, 18.3f), new Vector3(13f, 3.5f, 13f), 10, new Color(1f, 0.85f, 0.62f));

        // ── player camera: post-processing and anti-aliasing ──
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) continue;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            EditorUtility.SetDirty(data);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelLightingBuilder] Level_1 atmosphere built ({shafts} lamps with halo and shafts)");
    }

    // ───────────── helpers ─────────────

    static void Materials()
    {
        var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Generated/glow_dot.png");
        var rayTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Generated/light_ray.png");
        var baseRay = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/LightRay.mat");
        var baseMote = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Generated/Materials/Mote.mat");
        rayMat = Copy(baseRay, "RoomLightShaft", particles, rayTex);
        haloMat = Copy(baseRay, "RoomCeilingHalo", particles, glowTex);
        moteMat = Copy(baseMote, "RoomDust", particles, glowTex);
    }

    static Material Copy(Material from, string name, Shader fallback, Texture tex)
    {
        string path = $"{Dir}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        var m = from != null ? new Material(from) : new Material(fallback);
        m.name = name;
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static VolumeProfile Profile(string name, System.Action<VolumeProfile> fill)
    {
        string path = $"{Dir}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);
        fill(p);
        foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
        EditorUtility.SetDirty(p);
        return p;
    }

    static Volume Volume(Transform parent, string name, bool isGlobal, Vector3 centre, Vector3 size, VolumeProfile profile, float priority = 0, float blend = 0)
    {
        var go = new GameObject("Volume_" + name);
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        var v = go.AddComponent<Volume>();
        v.isGlobal = isGlobal;
        v.sharedProfile = profile;
        v.priority = priority;
        v.blendDistance = blend;
        if (!isGlobal)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            // stays on the Default layer: cameras only read volumes on their volume layer mask (Default);
            // PlayerInteract ignores trigger colliders, so the box never blocks interaction
        }
        return v;
    }

    static float CeilingAbove(Vector3 p)
    {
        foreach (var hit in Physics.RaycastAll(p + Vector3.down * 0.2f, Vector3.up, 4f).OrderBy(h => h.distance))
            if (!hit.collider.isTrigger) return hit.point.y;
        return p.y + 0.3f;
    }

    static GameObject Quad(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.SetPositionAndRotation(pos, rot);
        q.transform.localScale = scale;
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q;
    }

    static void Link(GameObject go, Light l, Color c)
    {
        var g = go.AddComponent<LightGlow>();
        g.source = l;
        g.color = c;
        g.referenceIntensity = Mathf.Max(0.5f, l.intensity);
    }

    static Light Fill(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject("Fill_" + name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    static void Dust(Transform parent, string name, Vector3 centre, Vector3 size, int rate, Color tint)
    {
        var go = new GameObject("Dust_" + name);
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(tint.r, tint.g, tint.b, 0.55f), new Color(tint.r, tint.g, tint.b, 0.25f));
        main.gravityModifier = -0.002f;
        main.maxParticles = 300;
        main.prewarm = true;
        main.loop = true;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.15f;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = moteMat;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = ShadowCastingMode.Off;
    }
}
