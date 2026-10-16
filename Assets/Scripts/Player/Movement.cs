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

    public float speed = 6f;
    public float gravity = -9.8f;
    public float jumpHeight = 2f;
    public float fallMultiplier = 1.5f;   // ตกเร็วกว่าขาขึ้นเล็กน้อย (1 = เท่ากัน)
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

        Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);

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
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                Step(1.25f); // the push-off
            }
        }

        // Gravity: ขาลงเพิ่มแรงโน้มถ่วงทีละน้อยแทนการตั้งค่าความเร็วทันที
        float g = velocity.y < 0 ? gravity * fallMultiplier : gravity;
        velocity.y = Mathf.Max(velocity.y + g * Time.deltaTime, maxFallSpeed);

        controller.Move((move * speed + Vector3.up * velocity.y) * Time.deltaTime);

        UpdateFootsteps(grounded, move.magnitude);
        UpdateLanding(controller.isGrounded);
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

    /// <summary>One footstep on the current ground; strength above 1 for a push-off or a landing (louder).</summary>
    public void Step(float strength = 1f)
    {
        string step = StepSound();
        bool water = step == "water_step_";
        float volume = Mathf.Min(1f, (water ? 0.5f : 0.35f) * strength);
        GameAudio.PlaySfx(step + Random.Range(1, water ? 6 : 5), volume, 0.08f);
        if (strength > 1.3f) GameAudio.PlaySfx(step + Random.Range(1, water ? 6 : 5), volume * 0.7f, 0.12f); // both feet on a landing
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
            Step(Mathf.Lerp(1.4f, 2.2f, Mathf.InverseLerp(4f, 12f, fallSpeed)));
        }
        if (grounded) { airTime = 0f; fallSpeed = 0f; }
        wasGrounded = grounded;
    }


    /// <summary>Footsteps follow the ground: shallow water in the white world (recorded water footsteps,
    /// "Footsteps on different surfaces" by congusbongus, CC-BY 3.0, from Water Footsteps by EminYILDIRIM),
    /// wooden floor in the house.</summary>
    static string StepSound()
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene.StartsWith("WhiteSpace") || scene == "Confrontation" || scene == "Main") return "water_step_";
        return "wood_step_";
    }
}
