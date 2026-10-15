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

        // Jump (เฉพาะตอนอยู่บนพื้น)
        if (grounded && Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity: ขาลงเพิ่มแรงโน้มถ่วงทีละน้อยแทนการตั้งค่าความเร็วทันที
        float g = velocity.y < 0 ? gravity * fallMultiplier : gravity;
        velocity.y = Mathf.Max(velocity.y + g * Time.deltaTime, maxFallSpeed);

        controller.Move((move * speed + Vector3.up * velocity.y) * Time.deltaTime);

        UpdateFootsteps(grounded, move.magnitude);
    }

    void UpdateFootsteps(bool grounded, float moveAmount)
    {
        if (!grounded || moveAmount < 0.2f)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            stepTimer = stepInterval;
            string step = StepSound();
            GameAudio.PlaySfx(step + Random.Range(1, step == "water_step_" ? 6 : 5), step == "water_step_" ? 0.5f : 0.35f, 0.08f);
        }
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
