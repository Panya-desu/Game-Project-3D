using EchoCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// First person, but with her real body: the old test capsule of the player is switched off (it showed
/// as a round blob in shadows and reflections) and the protagonist's model is put on the player as a
/// shadow-only body, so the camera never sees it but its shadow walks with her. Cutscenes hide it with
/// the rest of the player's renderers (CutscenePlayer).
/// </summary>
[DefaultExecutionOrder(10)]
public class PlayerBody : MonoBehaviour
{
    CharacterController controller;
    Movement movement;
    EchoCharacterView view;
    Vector3 lastPos;
    float speedSmooth;
    bool shown = true;
    float airborneTime;
    float bodyYaw, bodyYawVelocity;
    // footstep timing from her feet: a foot coming down to the floor is a step
    Transform[] feet;
    readonly float[] footLow = new float[2], footHigh = new float[2], lastStep = new float[2];
    readonly bool[] planted = { true, true };
    const float WalkCadenceCap = 1.35f;

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
        bodyYaw = transform.eulerAngles.y;
        var bodyAnimator = view.GetComponentInChildren<Animator>();
        if (bodyAnimator == null) bodyAnimator = view.GetComponentInParent<Animator>();
        if (bodyAnimator != null && bodyAnimator.isHuman)
            feet = new[] { bodyAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), bodyAnimator.GetBoneTransform(HumanBodyBones.RightFoot) };
        WarmUpShaders(root);
    }

    /// <summary>
    /// Footsteps on her steps: each foot's height above her root is tracked (its lowest and highest point
    /// over the last strides), and a foot dropping into the bottom third of that range after being lifted
    /// is a heel strike, which plays the footstep (Movement.Step). While she stands still or is in the air
    /// the steps stay quiet (the jump has its own push-off and landing sounds).
    /// </summary>
    void FootSteps(bool walking)
    {
        if (movement == null) return;
        bool usable = feet != null && feet[0] != null && feet[1] != null;
        movement.StepsFromBody = usable;
        if (!usable) return;
        float scale = Mathf.Abs(view.transform.root.lossyScale.y);
        float rootY = view.transform.root.position.y, dt = Time.deltaTime;
        for (int i = 0; i < 2; i++)
        {
            float h = feet[i].position.y - rootY;
            footLow[i] = h < footLow[i] ? h : Mathf.Lerp(footLow[i], h, dt * 0.4f);
            footHigh[i] = h > footHigh[i] ? h : Mathf.Lerp(footHigh[i], h, dt * 0.4f);
            float range = footHigh[i] - footLow[i];
            bool down = h < footLow[i] + range * 0.3f;
            if (walking && down && !planted[i] && range > 0.025f * scale && Time.time - lastStep[i] > 0.22f)
            {
                movement.Step();
                lastStep[i] = Time.time;
            }
            planted[i] = down;
        }
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
        if (!show) { lastPos = transform.position; speedSmooth = 0f; return; }
        // walk/run from the real speed (the clips are in place; the controller moves her)
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        float speed = Time.deltaTime > 0f ? new Vector2(delta.x, delta.z).magnitude / Time.deltaTime : 0f;
        speedSmooth = Mathf.Lerp(speedSmooth, speed, 1f - Mathf.Exp(-10f * Time.deltaTime));
        float full = movement != null ? movement.speed : 6f;
        var motion = view.GetComponent<EchoLocomotion>();
        bool moving = speedSmooth > 0.3f;
        if (motion != null)
        {
            // a walk, never a run: the player moves faster than a real walk at her scale, so the walk cycle
            // plays a little quicker (capped) instead of switching to the run
            motion.maxPlaybackRate = WalkCadenceCap;
            motion.ReleaseFacing();
            motion.SetSpeed(moving ? 0.5f : 0f);
        }
        else view.SetLocomotion(moving ? 0.5f : 0f);
        // she turns to face where the player looks, smoothly (her shadow follows the view)
        float targetYaw = transform.eulerAngles.y;
        bodyYaw = Mathf.SmoothDampAngle(bodyYaw, targetYaw, ref bodyYawVelocity, 0.12f);
        view.transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
        if (controller != null)
        {
            // jump: the wind-up plays the take-off (crouch and push) while still on the ground, then the
            // fall loop in the air and a landing; a single frame off the ground on a step is ignored
            bool grounded = controller.isGrounded;
            airborneTime = grounded ? 0f : airborneTime + Time.deltaTime;
            bool windup = movement != null && movement.JumpWindup;
            bool rising = !grounded && controller.velocity.y > 0.5f;
            bool inAir = windup || rising || airborneTime > 0.08f;
            view.SetAirborne(inAir, windup || rising ? 1f : controller.velocity.y);
            FootSteps(moving && grounded && !inAir);
        }
    }
}
