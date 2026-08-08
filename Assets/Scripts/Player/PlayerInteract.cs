using TMPro;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    public TextMeshProUGUI interactText;
    public DialogueManager dialogueManager;

    void Update()
    {
        if (dialogueManager != null && dialogueManager.IsTalking())
        {
            interactText.text = "";
            return;
        }

        if (KeypadInteractable.IsEnteringKeypad)
        {
            interactText.text = "";
            return;
        }

        if (PhoneManager.IsPhoneOpen)
        {
            interactText.text = "";
            return;
        }

        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            Interactable[] interactables = hit.collider.GetComponents<Interactable>();
            if (interactables.Length > 0)
            {
                Interactable primaryInteractable = null;
                foreach (var interactable in interactables)
                {
                    if (interactable.canInteract)
                    {
                        primaryInteractable = interactable;
                        break;
                    }
                }

                if (primaryInteractable != null)
                {
                    interactText.text = primaryInteractable.interactText;

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        foreach (var interactable in interactables)
                        {
                            if (interactable.canInteract)
                            {
                                interactable.Interact();
                            }
                        }
                    }
                }
                else
                {
                    interactText.text = "";
                }

                return;
            }
        }
        interactText.text = "";
    }
}