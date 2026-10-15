using UnityEngine;

public class MouseLook : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public float mouseSensitivity = 1000f;
    public Transform playerBody;

    float xRotation = 0f;

    public float Pitch => xRotation;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetPitch(float pitch)
    {
        xRotation = Mathf.Clamp(pitch, -90f, 90f);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    // Update is called once per frame
    void Update()
    {
        if (PauseMenu.IsPaused || GameState.IsCutscene)
        {
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

        // Mouse delta is already "per frame": multiplying by Time.deltaTime made the camera slower at
        // high frame rates. Dividing by 60 keeps the old feel of the Inspector value at 60 FPS.
        float scale = mouseSensitivity / 60f * PauseMenu.SensitivityScale;
        float mouseX = Input.GetAxis("Mouse X") * scale;
        float mouseY = Input.GetAxis("Mouse Y") * scale;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
