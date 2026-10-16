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
        return go.GetComponentInChildren<EchoCharacterView>();
    }

    /// <summary>
    /// Moves a character from a to b with the walk cycle (the clips are in place, so the root is moved here).
    /// Call every frame with k = 0..1; she slows into idle over the last part.
    /// </summary>
    public static void Walk(EchoCharacterView view, Vector3 a, Vector3 b, float k)
    {
        if (view == null) return;
        // v7: reaches the end, eases in and out, and paces the steps to the real speed
        var motion = view.GetComponent<EchoCinematicMotion>();
        if (motion == null) motion = view.gameObject.AddComponent<EchoCinematicMotion>();
        motion.Walk(a, b, k);
    }

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
