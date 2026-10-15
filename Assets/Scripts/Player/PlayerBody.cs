using EchoCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// First person, but with her real body: the old test capsule of the player is switched off (it showed
/// as a round blob in shadows and reflections) and the protagonist's model is put on the player as a
/// shadow-only body, so the camera never sees it but its shadow walks with her. Cutscenes hide it with
/// the rest of the player's renderers (CutscenePlayer).
/// </summary>
public class PlayerBody : MonoBehaviour
{
    CharacterController controller;
    Movement movement;
    EchoCharacterView view;
    Vector3 lastPos;
    float speedSmooth;
    bool shown = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var mover = FindAnyObjectByType<Movement>();
        if (mover != null && mover.GetComponent<PlayerBody>() == null) mover.gameObject.AddComponent<PlayerBody>();
    }

    void Awake()
    {
        // remove the capsule's renderer outright: merely disabling it was undone when a cutscene that had
        // already hidden the player's renderers switched them all back on, and the capsule's oval shadow
        // came back
        foreach (var r in GetComponents<Renderer>()) Destroy(r);
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        movement = GetComponent<Movement>();
        if (GetComponent<ShadowFocus>() == null) gameObject.AddComponent<ShadowFocus>(); // a sharp shadow, not a blob

        float bottom = controller != null ? controller.center.y - controller.height / 2f : -1f;
        view = CharacterLibrary.Spawn(false, transform.TransformPoint(new Vector3(0f, bottom, 0f)), transform.forward);
        if (view == null) return;
        var root = view.transform.root;
        CharacterKeyLight.Remove(root.gameObject); // never lit (shadow only), and its light would fall on cutscene actors
        float scale = root.localScale.y;
        root.SetParent(transform, true);
        root.localPosition = new Vector3(0f, bottom, 0f);
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one * scale / Mathf.Max(0.001f, transform.lossyScale.y);
        root.name = "PlayerBody";
        // stand on the floor (the controller's capsule hovers by its skin width)
        float gap = root.position.y - CharacterLibrary.GroundBelow(root.position).y;
        if (gap > 0f && gap < 0.4f) root.position -= Vector3.up * gap;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            r.renderingLayerMask = ShadowFocus.BodyLayer; // only the nearest lamp draws her shadow
        }
        lastPos = transform.position;
        WarmUpShaders(root);
    }

    /// <summary>
    /// The first time the character is drawn under a level's lights, her toon shader variants are built,
    /// which stalled the first cutscene frame by ~0.4 s. While the scene still starts behind the black
    /// fade, she is drawn once in front of the camera so that work is done before anyone can see.
    /// </summary>
    void WarmUpShaders(Transform root)
    {
        var cam = GetComponentInChildren<Camera>();
        if (cam == null || !ScreenFader.Opaque) return;
        var rends = root.GetComponentsInChildren<Renderer>(true);
        Vector3 local = root.localPosition;
        root.position = cam.transform.position + cam.transform.forward * 2.5f - Vector3.up * 1.2f * root.lossyScale.y;
        foreach (var r in rends) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        cam.Render(); // one extra frame, drawn behind the black fade
        foreach (var r in rends) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        root.localPosition = local;
    }

    void Update()
    {
        if (view == null) return;
        // hidden during cutscenes: a cutscene's own copy of her stands right here, and when this body was
        // spawned after the cutscene had hidden the player's renderers, its shadow fell on that copy's
        // face (a second chin under hers)
        bool show = !CutscenePlayer.IsPlaying;
        if (show != shown)
        {
            shown = show;
            foreach (var r in view.transform.root.GetComponentsInChildren<Renderer>(true)) r.enabled = show;
        }
        // walk/run from the real speed (the clips are in place; the controller moves her)
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        float speed = Time.deltaTime > 0f ? new Vector2(delta.x, delta.z).magnitude / Time.deltaTime : 0f;
        speedSmooth = Mathf.Lerp(speedSmooth, speed, 1f - Mathf.Exp(-10f * Time.deltaTime));
        float full = movement != null ? movement.speed : 6f;
        view.SetLocomotion(speedSmooth < 0.2f ? 0f : Mathf.Clamp(0.45f + 0.45f * speedSmooth / full, 0f, 0.9f));
    }
}
