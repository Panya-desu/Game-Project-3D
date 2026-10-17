using UnityEngine;

public class Movement : MonoBehaviour, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public Vector3 position;
        public float yaw;
        public float pitch;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [Tooltip("Only used when the character's walk speeds are not available (see WalkSpeed)")]
    public float speed = 6f;
    [Tooltip("Seconds to reach full walking speed / to come to a stop")]
    public float accelerationTime = 0.3f, decelerationTime = 0.22f;
    Vector3 planarVelocity, planarVelocityRef;
    /// <summary>Running (Shift, forwards only: her run is a forward run; backwards and side steps stay a walk).</summary>
    public bool Running { get; private set; }
    static float RunModelSpeed => EchoCharacters.EchoLocomotion.RunReference; // her run's own pace at scale 1
    public float gravity = -9.8f;
    // The jump is as high and as long in the air as her jump animation (EchoCharacters v9: 0.35 m at
    // scale 1, the fall 1.2 times faster): rise, apex and fall then match her pose instead of the fall
    // pose looping through a long drop
    const float JumpModelHeight = 0.35f, FallMultiplier = 1.2f;
    public float maxFallSpeed = -20f;     // ความเร็วตกสูงสุด
    public float groundedForce = -2f;     // แรงกดให้ติดพื้นตอนยืนอยู่

    Vector3 velocity;

    // the jump starts with a short wind-up (she crouches and pushes off, see PlayerBody) before leaving the ground
    public const float JumpWindupTime = 0.1f;
    /// <summary>True during the wind-up between pressing jump and leaving the ground.</summary>
    public bool JumpWindup { get; private set; }
    float windupLeft;

    /// <summary>A footstep sound was played (position, strength: 1 a step, more for a push-off or a landing).</summary>
    public static event System.Action<Vector3, float> Stepped;
    /// <summary>Set by PlayerBody while her walking body times the footsteps (each foot coming down);
    /// otherwise the steps keep the plain timer.</summary>
    public bool StepsFromBody { get; set; }
    /// <summary>Set by PlayerBody when a jump starts from a stride (she springs off one foot and lands on
    /// the other) rather than from standing (both feet).</summary>
    public bool StrideJump { get; set; }
    bool wasGrounded = true;
    float airTime, fallSpeed;

    [Header("Footsteps")]
    public float stepInterval = 0.5f;
    float stepTimer;

    CharacterController controller;

    public DialogueManager dialogueManager;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // กลับไปตำแหน่งที่ save ไว้ (ต้องปิด CharacterController ก่อนย้ายตำแหน่ง)
        if (SaveSystem.TryLoad(this, out SaveState state))
        {
            controller.enabled = false;
            transform.position = state.position;
            transform.rotation = Quaternion.Euler(0f, state.yaw, 0f);
            controller.enabled = true;

            MouseLook mouseLook = GetComponentInChildren<MouseLook>();
            if (mouseLook != null)
            {
                mouseLook.SetPitch(state.pitch);
            }
        }
    }

    public string CaptureState()
    {
        MouseLook mouseLook = GetComponentInChildren<MouseLook>();
        return JsonUtility.ToJson(new SaveState
        {
            position = transform.position,
            yaw = transform.eulerAngles.y,
            pitch = mouseLook != null ? mouseLook.Pitch : 0f
        });
    }

    // Update is called once per frame
    void Update()
    {
        if (PauseMenu.IsPaused || GameState.IsCutscene)
        {
            return;
        }

        if (dialogueManager != null && dialogueManager.IsTalking())
        {
            //velocity = Vector3.zero;
            return;
        }

        if (KeypadInteractable.IsEnteringKeypad)
        {
            return;
        }

        if (PhoneManager.IsPhoneOpen)
        {
            return;
        }

        bool grounded = controller.isGrounded;

        // ติดพื้น: รีเซ็ตความเร็วแกน y เฉพาะตอนอยู่บนพื้น
        if (grounded && velocity.y < 0)
        {
            velocity.y = groundedForce;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // walking speed follows her walk animation (EchoCharacters v8): forward fastest, backwards slower,
        // a side step slowest, each at the pace her steps really cover, so her feet never slide; it builds
        // up and settles smoothly instead of jumping to full speed
        Vector2 input = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        Running = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && input.y > 0.4f && Mathf.Abs(input.x) < 0.75f;
        float pace = Running ? RunModelSpeed * PlayerAvatar.ModelScale : WalkSpeed(input) * input.magnitude;
        Vector3 targetVelocity = move.sqrMagnitude > 0.0001f ? move.normalized * pace : Vector3.zero;
        bool speedingUp = targetVelocity.sqrMagnitude > planarVelocity.sqrMagnitude;
        planarVelocity = Vector3.SmoothDamp(planarVelocity, targetVelocity, ref planarVelocityRef, speedingUp ? accelerationTime : decelerationTime);

        // Jump (เฉพาะตอนอยู่บนพื้น): a short wind-up, then the push off the ground
        if (grounded && !JumpWindup && Input.GetButtonDown("Jump"))
        {
            JumpWindup = true;
            windupLeft = JumpWindupTime;
        }
        if (JumpWindup)
        {
            windupLeft -= Time.deltaTime;
            if (windupLeft <= 0f)
            {
                JumpWindup = false;
                velocity.y = Mathf.Sqrt(JumpModelHeight * PlayerAvatar.ModelScale * -2f * gravity);
                Step(StrideJump ? 1f : 1.25f, StrideJump); // the push-off: one foot from a stride, both standing
            }
        }

        // Gravity: ขาลงเพิ่มแรงโน้มถ่วงทีละน้อยแทนการตั้งค่าความเร็วทันที
        float g = velocity.y < 0 ? gravity * FallMultiplier : gravity;
        velocity.y = Mathf.Max(velocity.y + g * Time.deltaTime, maxFallSpeed);

        controller.Move((planarVelocity + Vector3.up * velocity.y) * Time.deltaTime);

        UpdateFootsteps(grounded, move.magnitude);
        UpdateLanding(controller.isGrounded);
    }

    /// <summary>World walking speed in a direction (x right, y forward): her fastest walk for that direction
    /// at her size in this level.</summary>
    float WalkSpeed(Vector2 direction)
    {
        float model = EchoCharacters.EchoLocomotion.PowerReference(direction.normalized);
        return model > 0.01f ? model * PlayerAvatar.ModelScale : speed;
    }

    void UpdateFootsteps(bool grounded, float moveAmount)
    {
        if (StepsFromBody || !grounded || moveAmount < 0.2f)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            stepTimer = stepInterval;
            Step();
        }
    }

    /// <summary>One footstep on the current ground; strength above 1 for a running step, a push-off or a
    /// landing (a little louder, up to 1.2; a landing above 1.3 also puts the second foot down unless she
    /// lands on one foot).</summary>
    public void Step(float strength = 1f, bool oneFoot = false)
    {
        string step = StepSound();
        bool water = step == "water_step_";
        float volume = strength <= 1.2f ? strength : 1f; // the mix (GameAudio) sets each surface's step level
        GameAudio.PlaySfx(step + Random.Range(1, water ? 6 : 5), volume, 0.08f);
        if (strength > 1.3f && !oneFoot) GameAudio.PlaySfx(step + Random.Range(1, water ? 6 : 5), volume * 0.7f, 0.12f); // both feet on a landing
        Stepped?.Invoke(transform.position, strength);
    }

    // a landing after a jump or a drop: louder the faster she came down
    void UpdateLanding(bool grounded)
    {
        if (!grounded)
        {
            airTime += Time.deltaTime;
            fallSpeed = Mathf.Max(fallSpeed, -velocity.y);
        }
        else if (!wasGrounded && airTime > 0.2f)
        {
            // after a jump from a stride she lands on her leading foot and walks on (one footfall)
            Step(Mathf.Lerp(1.4f, 2.2f, Mathf.InverseLerp(4f, 12f, fallSpeed)), StrideJump && fallSpeed < 8f);
            StrideJump = false;
        }
        if (grounded) { airTime = 0f; fallSpeed = 0f; }
        wasGrounded = grounded;
    }


    /// <summary>Footsteps follow the ground: shallow water in the white world (recorded water footsteps,
    /// "Footsteps on different surfaces" by congusbongus, CC-BY 3.0, from Water Footsteps by EminYILDIRIM),
    /// wooden floor in the house.</summary>
    public static string StepSound()
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene.StartsWith("WhiteSpace") || scene == "Confrontation" || scene == "Main") return "water_step_";
        return "wood_step_";
    }
}
