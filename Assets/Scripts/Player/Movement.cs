using UnityEngine;

public class Movement : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public float speed = 6f;
    public float gravity = -9.8f;
    public float jumpHeight = 2f;
    public float fallSpd = -2f;

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

        if (velocity.y < 0)
        {
            velocity.y = fallSpd;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * speed * Time.deltaTime);

        // Jump
        if (Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

}
