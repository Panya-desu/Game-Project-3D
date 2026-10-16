using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wind you can see and hear in the open, dreamlike places (the title, the White Space, the confrontation),
/// in the manner of Ghost of Tsushima: tiny crystal flakes broken off the memory shards drift across the water (there are no trees here for
/// petals), glinting gold now and then, all in the
/// same direction, carried in slow gusts, and a soft wind (amb_wind) rises and falls with them. In the
/// confrontation the wind is stronger and keeps rising while the Double rushes in.
/// The flakes live around the camera (world space, re-centred as it moves), so the field never runs out.
/// </summary>
public class WindDrift : MonoBehaviour
{
    static readonly Vector3 WindDirection = new Vector3(1f, 0f, 0.35f).normalized;
    float baseStrength = 1.2f, gustAmount = 0.7f;
    ParticleSystem flakes;
    AudioSource wind;
    bool confrontation;

    /// <summary>0..1, raised by the confrontation while the Double rushes in (stronger wind).</summary>
    public static float Tension;
    /// <summary>True while the world holds its breath before a big moment: the wind sound dies away.</summary>
    public static bool Calm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Tension = 0f;
        Calm = false;
        if (scene.name != "WhiteSpace" && scene.name != "Main" && scene.name != "Confrontation") return;
        if (FindAnyObjectByType<WindDrift>() != null) return;
        var go = new GameObject("[WindDrift]");
        SceneManager.MoveGameObjectToScene(go, scene);
        var w = go.AddComponent<WindDrift>();
        w.confrontation = scene.name == "Confrontation";
        if (w.confrontation) { w.baseStrength = 1.8f; w.gustAmount = 1f; }
    }

    void Start()
    {
        flakes = MakeFlakes();
        wind = gameObject.AddComponent<AudioSource>();
        wind.clip = Resources.Load<AudioClip>("Audio/amb_wind");
        wind.loop = true;
        wind.spatialBlend = 0f;
        wind.reverbZoneMix = 0f;
        wind.volume = 0f;
        if (wind.clip != null) { wind.time = Random.Range(0f, wind.clip.length); wind.Play(); }
    }

    void Update()
    {
        float t = Time.time;
        // slow gusts, never quite still
        float gust = Mathf.PerlinNoise(t * 0.12f, 0.3f) * 0.7f + Mathf.PerlinNoise(t * 0.45f, 2.1f) * 0.3f;
        float strength = baseStrength + gustAmount * gust + Tension * 3f;
        if (flakes != null)
        {
            var cam = Camera.main;
            if (cam != null) transform.position = cam.transform.position;
            var vel = flakes.velocityOverLifetime;
            vel.x = WindDirection.x * strength;
            vel.z = WindDirection.z * strength;
            var noise = flakes.noise;
            noise.strength = 0.35f + 0.35f * gust + Tension * 0.6f;
        }
        if (wind != null && wind.clip != null)
        {
            float target = Calm ? 0f : (confrontation ? 0.32f : 0.22f) * (0.55f + 0.6f * gust) + Tension * 0.35f;
            wind.volume = Mathf.MoveTowards(wind.volume, target, Time.deltaTime * (Calm ? 0.4f : 0.25f));
        }
    }

    ParticleSystem MakeFlakes()
    {
        var shader = Resources.Load<Shader>("Shaders/Flake");
        if (shader == null) return null;
        var go = new GameObject("Flakes");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.17f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 300;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.004f;
        var grad = new Gradient();
        // cool crystal flakes (the gold comes from their glints, see Echo/Flake)
        grad.SetKeys(new[] { new GradientColorKey(new Color(0.74f, 0.8f, 0.96f), 0f), new GradientColorKey(new Color(0.86f, 0.84f, 0.98f), 1f) },
                     new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.8f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
        var emission = ps.emission; emission.rateOverTime = 28f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 7f, 34f);
        shape.position = new Vector3(0f, 1.5f, 0f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = WindDirection.x * baseStrength; vel.y = 0f; vel.z = WindDirection.z * baseStrength;
        var noise = ps.noise;
        noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.35f; noise.scrollSpeed = 0.25f; noise.damping = true;
        var rot = ps.rotationOverLifetime;
        rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        // flakes flutter: they grow thin and wide as they turn edge-on to the eye
        var size = ps.sizeOverLifetime;
        size.enabled = true; size.separateAxes = true;
        size.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 0.35f), new Keyframe(0.5f, 1f), new Keyframe(0.75f, 0.35f), new Keyframe(1f, 1f)));
        size.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = new Material(shader) { name = "Flake (runtime)" };
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        ps.Play();
        return ps;
    }
}
