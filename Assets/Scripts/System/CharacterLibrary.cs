using EchoCharacters;
using UnityEngine;

/// <summary>
/// Points at the character prefabs (Assets/EchoCharacters, made in Blender/Codex) so runtime code
/// can spawn them without scene references. The asset lives at Resources/CharacterLibrary and is
/// created by Tools/Game Setup/Characters.
/// </summary>
[CreateAssetMenu(menuName = "Echo/Character Library")]
public class CharacterLibrary : ScriptableObject
{
    public GameObject protagonist;
    public GameObject theDouble;
    public Shader toonShader; // referenced here so builds keep the Echo/Toon shader

    static CharacterLibrary cached;

    public static CharacterLibrary Get()
    {
        if (cached == null) cached = Resources.Load<CharacterLibrary>("CharacterLibrary");
        return cached;
    }

    /// <summary>Spawns a character standing on the ground below/at <paramref name="position"/>, facing <paramref name="forward"/>.</summary>
    public static EchoCharacterView Spawn(bool isDouble, Vector3 position, Vector3 forward, Transform parent = null)
    {
        var lib = Get();
        var prefab = lib == null ? null : (isDouble ? lib.theDouble : lib.protagonist);
        if (prefab == null) return null;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        var go = Instantiate(prefab, GroundBelow(position), Quaternion.LookRotation(forward.normalized), parent);
        go.name = prefab.name;
        go.transform.localScale = Vector3.one * PlayerAvatar.ModelScale; // same size as the playable character
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        CharacterLook.Apply(go, isDouble);
        CharacterKeyLight.Add(go);
        var view = go.GetComponentInChildren<EchoCharacterView>();
        ActorFootsteps.Attach(view, isDouble); // her steps are heard in the cutscenes
        return view;
    }

    /// <summary>
    /// Moves a character from a to b with the walk cycle (the clips are in place, so the root is moved here).
    /// Call every frame with k = 0..1 over a walk lasting <paramref name="seconds"/>. She sets off over half
    /// a second, walks at an even pace and settles into standing where she is as she arrives (under half a
    /// second, as the player does). The old ease slowed her over the last second: she shuffled on at a
    /// crawl and then made a stop step after she had arrived, like one step too many.
    /// </summary>
    public static void Walk(EchoCharacterView view, Vector3 a, Vector3 b, float k, float seconds)
    {
        if (view == null) return;
        var motion = view.GetComponent<EchoLocomotion>();
        float T = Mathf.Max(0.5f, seconds);
        float ta = Mathf.Min(0.5f, T * 0.3f), td = Mathf.Min(0.45f, T * 0.2f);
        float full = T - ta * 0.5f - td * 0.5f;
        float t = Mathf.Clamp01(k) * T;
        float s = t < ta ? 0.5f * t * t / ta
                : t < T - td ? ta * 0.5f + (t - ta)
                : full - (T - t) * (T - t) / (2f * td);
        Vector3 next = Vector3.Lerp(a, b, Mathf.Clamp01(s / full));
        var state = view.GetComponent<CutsceneWalkState>() ?? view.gameObject.AddComponent<CutsceneWalkState>();
        bool reset = state.lastK < 0f || k < state.lastK;
        // as the player's body: when she slows down her stride eases into standing where she is, with no stop
        // clip (the package's stops always bring the back foot up beside the front one, one more step)
        bool stopping = t >= T - td;
        Vector3 velocity = !reset && Time.deltaTime > 0f && k < 1f ? (next - view.transform.position) / Time.deltaTime : Vector3.zero;
        view.transform.position = next;
        if (motion != null)
        {
            motion.useTransitionClips = !stopping;   // her start clip still plays when she sets off
            motion.SetVelocity(velocity);
            motion.SetFacing(b - a);
        }
        state.lastK = k;
    }

    class CutsceneWalkState : MonoBehaviour { public float lastK = -1f; }

    /// <summary>The floor under a point (ignores triggers and the player capsule).</summary>
    public static Vector3 GroundBelow(Vector3 p)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 ground = p;
        foreach (var h in hits)
        {
            if (h.collider is CharacterController) continue;
            if (h.distance < best) { best = h.distance; ground = h.point; }
        }
        return ground;
    }
}
