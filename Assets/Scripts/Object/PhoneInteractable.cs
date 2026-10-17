using UnityEngine;

public class PhoneInteractable : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool collected;
        public bool active;
    }

    private void Start()
    {
        // Default interaction text
        interactText = "press E to collect phone";

        // Already collected in save
        if (SaveSystem.TryLoad(this, out SaveState state) && state.collected)
        {
            canInteract = false;
            if (!state.active)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { collected = !canInteract, active = gameObject.activeSelf });
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
            GameAudio.PlaySfx("pickup"); // (with a quest, QuestInteractable plays it)
            gameObject.SetActive(false);
        }
    }
}
