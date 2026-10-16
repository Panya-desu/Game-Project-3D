using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small touches that make the White Space answer the player:
///  - every footstep on the water leaves a ring that spreads and fades (Echo/Ripple), on the same step as the
///    sound (Movement.Stepped), a bigger one for a landing (also in the confrontation, on the same water);
///  - the open gate (Door1) breathes out slow golden motes from its threshold, more and brighter as the
///    player comes closer, drawing them in, with a faint shimmer heard from the gate now and then.
/// </summary>
public class WhiteSpaceFlair : MonoBehaviour
{
    Transform player;
    CharacterController controller;
    ParticleSystem ripples, motes;
    Transform gate;
    AudioSource shimmer;
    float nextShimmer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "WhiteSpace" && scene.name != "Confrontation") return;
        if (FindAnyObjectByType<WhiteSpaceFlair>() != null) return;
        var go = new GameObject("[WhiteSpaceFlair]");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<WhiteSpaceFlair>();
    }

    void Start()
    {
        var mover = FindAnyObjectByType<Movement>();
        if (mover != null) { player = mover.transform; controller = mover.GetComponent<CharacterController>(); }
        ripples = MakeRipples();
        foreach (var d in FindObjectsByType<Door>(FindObjectsSortMode.None)) if (d.name == "Door1") gate = d.transform;
        if (gate != null)
        {
            motes = MakeGateMotes(transform, gate.position);
            // a faint shimmer from the open gate, heard from where it stands
            var go = new GameObject("GateShimmer");
            go.transform.SetParent(transform, false);
            go.transform.position = gate.position + Vector3.up * 1.5f;
            shimmer = go.AddComponent<AudioSource>();
            shimmer.playOnAwake = false;
            shimmer.spatialBlend = 1f;
            shimmer.rolloffMode = AudioRolloffMode.Linear;
            shimmer.minDistance = 2f;
            shimmer.maxDistance = 16f;
            shimmer.clip = Resources.Load<AudioClip>("Audio/shimmer");
            nextShimmer = Time.time + 3f;
        }
        Movement.Stepped += OnStep;
    }

    void OnDestroy() => Movement.Stepped -= OnStep;

    // a ring under every footstep (Movement plays them on her steps), a bigger one for a landing
    void OnStep(Vector3 at, float strength)
    {
        if (GameState.IsCutscene) return;
        Ring(CharacterLibrary.GroundBelow(at) + Vector3.up * 0.03f, Mathf.Min(strength, 2f));
    }

    void Update()
    {
        if (player == null) return;
        Vector3 p = player.position;
        if (shimmer != null && shimmer.clip != null && Time.time >= nextShimmer && !PauseMenu.IsPaused)
        {
            nextShimmer = Time.time + Random.Range(5f, 9f);
            shimmer.pitch = Random.Range(0.92f, 1.08f);
            shimmer.volume = 0.22f;
            shimmer.Play();
        }

        // the gate breathes out more motes as the player comes near
        if (motes != null)
        {
            float dist = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(gate.position.x, 0f, gate.position.z));
            float k = Mathf.InverseLerp(14f, 3f, dist);
            var em = motes.emission;
            em.rateOverTime = Mathf.Lerp(3f, 14f, k);
            var main = motes.main;
            main.startColor = new Color(1f, 0.9f, 0.7f, Mathf.Lerp(0.35f, 0.8f, k));
        }
    }

    void Ring(Vector3 at, float strength)
    {
        if (ripples == null) return;
        var e = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = false };
        e.startColor = new Color(0.75f, 0.82f, 1f, Mathf.Min(1f, 0.5f * strength));
        e.startSize = 0.6f * strength;
        ripples.Emit(e, 1);
    }

    ParticleSystem MakeRipples()
    {
        var shader = Resources.Load<Shader>("Shaders/Ripple");
        if (shader == null) return null;
        var go = new GameObject("WaterRings");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false;
        main.startLifetime = 1.6f;
        main.startSpeed = 0f;
        main.startSize = 0.6f;
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(4.5f, new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(1f, 1f, 0.6f, 0f)));
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = new Material(shader) { name = "Ripple (runtime)" };
        rend.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        ps.Play();
        return ps;
    }

    /// <summary>Slow golden motes rising from a memory door's threshold (also the ending door in the house).</summary>
    public static ParticleSystem MakeGateMotes(Transform parent, Vector3 gatePos)
    {
        var shader = Resources.Load<Shader>("Shaders/Mist"); // a soft round glow, fading near the camera
        if (shader == null) return null;
        var go = new GameObject("GateMotes");
        go.transform.SetParent(parent, false);
        go.transform.position = CharacterLibrary.GroundBelow(gatePos + Vector3.up) + Vector3.up * 0.2f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(1f, 0.9f, 0.7f, 0.35f);
        var emission = ps.emission; emission.rateOverTime = 3f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1.8f, 0.1f, 0.6f);
        shape.rotation = new Vector3(-90f, 0f, 0f); // emit upwards
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.4f; noise.scrollSpeed = 0.2f;
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var m = new Material(shader) { name = "GateMotes (runtime)" };
        m.SetFloat("_NearFade", 0.3f); m.SetFloat("_FullAt", 1.2f);
        rend.sharedMaterial = m;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }
}
