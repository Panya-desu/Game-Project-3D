using UnityEngine;

public class Movement : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public float speed = 6f;
    public float gravity = -9.8f;
    public float jumpHeight = 2f;
    public float fallMultiplier = 1.5f;   // ตกเร็วกว่าขาขึ้นเล็กน้อย (1 = เท่ากัน)
    public float maxFallSpeed = -20f;     // ความเร็วตกสูงสุด
    public float groundedForce = -2f;     // แรงกดให้ติดพื้นตอนยืนอยู่

    Vector3 velocity;

    CharacterController controller;

    public DialogueManager dialogueManager;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
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
    }

}
