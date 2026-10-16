using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// A soft, glowing atmosphere for every place, on top of the scenes' own colour grading (left as it was).
/// Only bloom (its tint, spread and strength) and the haze (fog colour and thickness) change, through one
/// extra global volume per scene (Mist() is kept for a drifting mist, currently not used):
///  - in the house each room has its own mood and the camera eases between them as it walks through:
///    a peach glow in the bedroom, golden in the kitchen, amber in the living room, a cold misty
///    blue-grey in the fuse/storage room, dusty pale in the storeroom and candle-warm in the library
///    (the power-cut grading, priority 3, still takes over while the power is out);
///  - the White Space, the title screen, the cutscenes there and the ending: a soft white bloom.
/// </summary>
public class AtmosphereLook : MonoBehaviour
{
    struct Room
    {
        public string name; public Bounds area; public Color glow, haze; public float density, bloom;
        public Room(string n, Vector3 centre, Vector3 size, Color glow, Color haze, float density, float bloom)
        { name = n; area = new Bounds(centre, size); this.glow = glow; this.haze = haze; this.density = density; this.bloom = bloom; }
    }

    // room boxes in Level_1 (x, z from the level layout; y covers floor to ceiling)
    static readonly Room[] Rooms =
    {
        // each room leans a little more into its own colour (a clear key colour per place, as in Ghost of Tsushima)
        new Room("Bedroom",   new Vector3(0.2f, 3f, 4.1f),   new Vector3(13f, 8f, 13f),   new Color(1f, 0.76f, 0.66f),  new Color(0.36f, 0.24f, 0.23f), 0.045f, 2.3f),
        new Room("Kitchen",   new Vector3(15.2f, 3f, 7f),    new Vector3(15f, 8f, 12.5f), new Color(1f, 0.86f, 0.6f),   new Color(0.36f, 0.28f, 0.16f), 0.042f, 2.2f),
        new Room("Living",    new Vector3(15.2f, 3f, 20f),   new Vector3(15f, 8f, 13.5f), new Color(1f, 0.74f, 0.5f),   new Color(0.34f, 0.22f, 0.13f), 0.048f, 2.4f),
        new Room("Storage",   new Vector3(30.2f, 3f, 13.3f), new Vector3(15f, 8f, 25f),   new Color(0.64f, 0.78f, 1f),  new Color(0.1f, 0.14f, 0.22f),  0.065f, 2.1f),
        new Room("Storeroom", new Vector3(30.2f, 3f, 33.3f), new Vector3(15f, 8f, 15f),   new Color(0.8f, 0.86f, 1f),   new Color(0.16f, 0.18f, 0.24f), 0.058f, 2f),
        new Room("Library",   new Vector3(45.2f, 3f, 18.3f), new Vector3(15f, 8f, 15f),   new Color(1f, 0.68f, 0.44f),  new Color(0.29f, 0.17f, 0.1f),  0.052f, 2.3f),
    };

    Bloom bloom;
    bool house;
    Color glow, haze;
    float density, strength;
    bool first = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<AtmosphereLook>() != null) return;
        bool isHouse = scene.name == "Level_1";
        bool isWhite = scene.name == "WhiteSpace" || scene.name == "Main" || scene.name == "Confrontation";
        if (!isHouse && !isWhite) return;
        var go = new GameObject("[AtmosphereLook]");
        SceneManager.MoveGameObjectToScene(go, scene);
        var look = go.AddComponent<AtmosphereLook>();
        look.house = isHouse;
        // the same soft amount of glow and haze everywhere; no drifting mist (it read as too much)
        if (isHouse) look.bloom = look.Make(1.5f, 0.62f, 2.3f, 0.93f, new Color(1f, 0.8f, 0.62f));
        else
        {
            look.Make(11f, 0.92f, 1.05f, 0.93f, new Color(1f, 0.93f, 0.84f));
            // a touch of golden hour in the sun over the water (with the wind of WindDrift)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.color = Color.Lerp(l.color, new Color(1f, 0.84f, 0.62f), 0.35f);
        }
    }

    static Material mistMaterial;

    void Mist(string name, Bounds area, Color color, float alpha, int count, float minSize, float maxSize)
    {
        if (mistMaterial == null)
        {
            var shader = Resources.Load<Shader>("Shaders/Mist");
            if (shader == null) return;
            mistMaterial = new Material(shader) { name = "Mist (runtime)" };
        }
        var go = new GameObject("Mist_" + name);
        go.transform.SetParent(transform, false);
        go.transform.position = area.center;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 20f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 26f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(color.r, color.g, color.b, alpha);
        main.maxParticles = count;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission;
        emission.rateOverTime = count / 21f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area.size;
        shape.randomDirectionAmount = 1f;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mistMaterial;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.maxParticleSize = 2f;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        ps.Play();
    }

    Bloom Make(float priority, float threshold, float intensity, float scatter, Color tint)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var b = profile.Add<Bloom>();
        b.threshold.Override(threshold);
        b.intensity.Override(intensity);
        b.scatter.Override(scatter);
        b.tint.Override(tint);
        b.highQualityFiltering.Override(true);
        var v = gameObject.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = priority;
        v.sharedProfile = profile;
        return b;
    }

    void LateUpdate()
    {
        if (!house || bloom == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        int room = 2; // the living room's mood for the halls in between
        for (int i = 0; i < Rooms.Length; i++) if (Rooms[i].area.Contains(p)) { room = i; break; }
        var r = Rooms[room];
        float k = first ? 1f : 1f - Mathf.Exp(-1.5f * Time.deltaTime); // eases over a second or two
        first = false;
        glow = Color.Lerp(glow, r.glow, k);
        haze = Color.Lerp(haze, r.haze, k);
        density = Mathf.Lerp(density, r.density, k);
        strength = Mathf.Lerp(strength, r.bloom, k);
        bloom.tint.value = glow;
        bloom.intensity.value = strength;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = haze;
        RenderSettings.fogDensity = density;
    }
}
