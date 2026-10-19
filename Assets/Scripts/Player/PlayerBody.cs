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
    float lastViewYaw, viewStill, facingYaw;
    // footstep timing from her feet: a foot coming down to the floor is a step
    Transform[] feet;
    readonly float[] footLow = new float[2], footHigh = new float[2], lastStep = new float[2];
    readonly bool[] planted = { true, true };
    bool previousWindup, previousAirborne;
    float launchFloor;
    Vector2 localVelocity;
    EchoAnimationEvents stepEvents;
    EchoLocomotion stepMotion;

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
        foreach (var s in root.GetComponentsInChildren<ActorFootsteps>(true)) Destroy(s); // her steps go through Movement
        // only her shadow is ever seen: her hair still sways, but without its collision checks against her
        // head and glasses (about half of the hair motion's cost every frame, for something nobody can see)
        foreach (var sm in root.GetComponentsInChildren<EchoSecondaryMotion>(true)) sm.hairCollision = false;
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
        bodyYaw = lastViewYaw = facingYaw = transform.eulerAngles.y;
        var bodyAnimator = view.GetComponentInChildren<Animator>();
        if (bodyAnimator == null) bodyAnimator = view.GetComponentInParent<Animator>();
        if (bodyAnimator != null && bodyAnimator.isHuman)
            feet = new[] { bodyAnimator.GetBoneTransform(HumanBodyBones.LeftFoot), bodyAnimator.GetBoneTransform(HumanBodyBones.RightFoot) };
        if (bodyAnimator != null)
        {
            stepEvents = bodyAnimator.GetComponent<EchoAnimationEvents>();
            stepMotion = view.GetComponent<EchoLocomotion>();
            if (stepEvents != null) { stepEvents.LeftFoot += OnFootContact; stepEvents.RightFoot += OnFootContact; }
        }
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
        if (stepEvents != null) { movement.StepsFromBody = true; return; }
        bool usable = feet != null && feet[0] != null && feet[1] != null;
        movement.StepsFromBody = usable;
        if (!usable) return;
        float scale = Mathf.Abs(view.transform.lossyScale.y);
        float rootY = view.transform.position.y, dt = Time.deltaTime;
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
        // one extra frame, drawn behind the black fade (into a texture: straight to the screen outside the
        // frame loop the UI overlay pass failed when the editor's game view was another size)
        var rt = RenderTexture.GetTemporary(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 24);
        var screenTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = screenTarget;
        RenderTexture.ReleaseTemporary(rt);
        foreach (var r in rends) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        root.localPosition = local;
    }

    float lastFootTime = -1f, lastMovingTime = -10f, landedAt = -10f;
    float letGoAt = -1f; Vector2 letGoVelocity;
    const float StopSeconds = 0.45f;   // how long her stride takes to settle into standing when she stops

    /// <summary>
    /// A foot comes down in her animation (FootL/FootR in the clips): one footstep. EchoCharacters v9 sends
    /// them from the clip that leads the blend only (v8 sent one from every clip, so they were filtered to
    /// one per 0.2 s); a short guard is kept for a cross-fade between two states. Running steps land a
    /// little heavier, the small steps of a turn on the spot softer, and the last step of a stop is heard
    /// too. Silent in the air, in the jump wind-up, while hidden, and just after a landing (Movement plays
    /// the landing itself, and her landing clip puts that same foot down).
    /// </summary>
    void OnFootContact()
    {
        if (movement == null || previousAirborne || movement.JumpWindup || !shown) return;
        bool turning = stepMotion != null && stepMotion.Turning;
        if (speedSmooth <= .3f && Time.time - lastMovingTime > 0.5f && !turning) return;
        if (Time.time - landedAt < 0.15f || Time.time - lastFootTime < 0.12f) return;
        lastFootTime = Time.time;
        movement.Step(movement.Running ? 1.15f : speedSmooth <= .3f && turning ? 0.75f : 1f);
    }
    void OnDestroy()
    {
        if (stepEvents != null) { stepEvents.LeftFoot -= OnFootContact; stepEvents.RightFoot -= OnFootContact; }
    }
    void LateUpdate()
    {
        // her body hangs under the player, which the mouse turns: put her back to the yaw her own turning
        // gave her (EchoLocomotion), so the view does not drag her round
        if (view == null || !shown) return;
        var motion = view.GetComponent<EchoLocomotion>();
        if (motion != null) view.transform.rotation = Quaternion.Euler(0f, motion.WorldYaw, 0f);
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
            HitchWatch.Note(show ? "body shown" : "body hidden");
            foreach (var r in view.transform.root.GetComponentsInChildren<Renderer>(true)) r.enabled = show;
        }
        if (!show) { lastPos = transform.position; speedSmooth = 0f; return; }
        // walk/run from the real speed (the clips are in place; the controller moves her)
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;
        float speed = Time.deltaTime > 0f ? new Vector2(delta.x, delta.z).magnitude / Time.deltaTime : 0f;
        speedSmooth = Mathf.Lerp(speedSmooth, speed, 1f - Mathf.Exp(-10f * Time.deltaTime));
        var motion = view.GetComponent<EchoLocomotion>();
        bool moving = speedSmooth > 0.3f;
        if (moving) lastMovingTime = Time.time;
        float modelScale = Mathf.Max(.001f, Mathf.Abs(view.transform.lossyScale.y));
        Vector3 worldVelocity = Time.deltaTime > 0 ? delta / Time.deltaTime : Vector3.zero;
        Vector3 local = Quaternion.Inverse(view.transform.rotation) * worldVelocity / modelScale;
        // steadied: the speed measured frame to frame wobbles a little, and the walk blend (brisk / power)
        // followed every wobble, which made the stride uneven, like a slight limp
        localVelocity = Vector2.Lerp(localVelocity, new Vector2(local.x, local.z), 1f - Mathf.Exp(-10f * Time.deltaTime));
        if (motion != null)
        {
            motion.SetRunning(movement != null && movement.Running && moving);
            // the moment the keys are let go she stops where she is: her stride eases into standing over a
            // quarter of a second, with no stop clip. The package's stops (WalkStop/RunStop) always bring the
            // back foot up beside the front one, one more step; fed her measured speed, the walk also wound
            // down through smaller and smaller strides first. Her start clips still play when she sets off.
            bool letGo = movement != null && movement.MoveInput.sqrMagnitude < 0.0004f;
            if (!letGo) { letGoAt = -1f; motion.useTransitionClips = true; motion.SetLocalVelocity(localVelocity); }
            else
            {
                if (letGoAt < 0f) { letGoAt = Time.time; letGoVelocity = localVelocity; }
                // no stop clip while she settles; afterwards her steps on the spot (turning to the view) need them back
                motion.useTransitionClips = Time.time - letGoAt > StopSeconds + 0.1f;
                // eased (fast at first, gently at the end) so the last part of the settle is soft, not a snap
                float u = Mathf.Clamp01((Time.time - letGoAt) / StopSeconds);
                motion.SetLocalVelocity(letGoVelocity * (1f - u) * (1f - u));
            }
        }
        else view.SetLocomotion(moving ? .5f : 0f);
        // she turns to face where the player looks. Walking, she follows the view at once; standing, her
        // steps turn her (EchoLocomotion's turn on the spot), and only once the view has settled, so one turn
        // of the right size (45/90/180) covers the whole change instead of a 45 that snaps the rest
        float viewYaw = transform.eulerAngles.y;
        float viewSpeed = Time.deltaTime > 0f ? Mathf.Abs(Mathf.DeltaAngle(lastViewYaw, viewYaw)) / Time.deltaTime : 0f;
        lastViewYaw = viewYaw;
        viewStill = viewSpeed < 25f ? viewStill + Time.deltaTime : 0f;
        if (motion != null)
        {
            if (moving || viewStill > 0.18f) facingYaw = viewYaw;
            motion.SetFacing(Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward);
        }
        else
        {
            bodyYaw = Mathf.SmoothDampAngle(bodyYaw, viewYaw, ref bodyYawVelocity, 0.12f);
            view.transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
        }
        if (controller != null)
        {
            // jump: the wind-up plays the take-off (crouch and push) while still on the ground, then the
            // fall loop in the air and a landing; a single frame off the ground on a step is ignored
            bool grounded = controller.isGrounded;
            airborneTime = grounded ? 0f : airborneTime + Time.deltaTime;
            bool windup = movement != null && movement.JumpWindup;
            bool rising = !grounded && controller.velocity.y > 0.5f;
            if (windup && !previousWindup)
            {
                launchFloor = view.transform.position.y;
                view.BeginJump(localVelocity);
                // from a stride she springs off one foot and lands on the leading one (one footfall each);
                // standing, both feet push off and come down together
                if (movement != null) movement.StrideJump = view.JumpLeadFoot != EchoFoot.None;
            }
            bool inAir = !windup && (rising || airborneTime > .08f);
            if (grounded && !previousAirborne && !windup) launchFloor = view.transform.position.y;
            if (inAir) view.SetAirborne(true, controller.velocity.y);
            else if (previousAirborne && grounded)
            {
                view.Landed(Mathf.Max(0, launchFloor - view.transform.position.y), localVelocity);
                landedAt = Time.time;
            }
            previousWindup = windup; previousAirborne = inAir;
            FootSteps(moving && grounded && !inAir);
        }
    }
}
