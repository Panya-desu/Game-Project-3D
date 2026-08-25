using UnityEngine;

public class RevealInteractable : Interactable
{
    [Header("Reveal Settings")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private bool disableAfterInteract = true;

    private void Start()
    {
        interactText = "press E to interact";

        // Hide the target object at start
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }

    public override void Interact()
    {
        if (!canInteract) return;

        if (targetObject != null)
        {
            targetObject.SetActive(true);
            Debug.Log("[RevealInteractable] Revealed: " + targetObject.name);
        }
        else
        {
            Debug.LogWarning("[RevealInteractable] No target object assigned!");
        }

        if (disableAfterInteract)
        {
            canInteract = false;
        }
    }
}
