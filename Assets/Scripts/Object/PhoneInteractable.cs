using UnityEngine;

public class PhoneInteractable : Interactable
{
    private void Start()
    {
        // Default interaction text
        interactText = "press E to collect phone";
    }

    public override void Interact()
    {
        if (!canInteract) return;

        canInteract = false;
        Debug.Log("[PhoneInteractable] Player interacted with phone.");

        if (PhoneManager.Instance != null)
        {
            PhoneManager.Instance.CollectPhone();
        }
        else
        {
            Debug.LogError("[PhoneInteractable] PhoneManager.Instance is null! Please make sure a PhoneManager GameObject is active in your scene.");
        }

        // If there is a QuestInteractable component on this object, trigger it
        QuestInteractable questInteract = GetComponent<QuestInteractable>();
        if (questInteract != null)
        {
            Debug.Log("[PhoneInteractable] QuestInteractable found on this object. Triggering quest completion...");
            questInteract.Interact();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
