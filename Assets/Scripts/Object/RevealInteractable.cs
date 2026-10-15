using UnityEngine;

public class RevealInteractable : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool revealed;
    }

    [Header("Reveal Settings")]
    [SerializeField] private GameObject targetObject;
    [SerializeField] private bool disableAfterInteract = true;

    private bool isRevealed = false;

    private void Start()
    {
        interactText = "press E to interact";

        if (SaveSystem.TryLoad(this, out SaveState state) && state.revealed)
        {
            isRevealed = true;
            if (disableAfterInteract)
            {
                canInteract = false;
            }
        }

        // Hide the target object at start (unless already revealed in save)
        if (targetObject != null)
        {
            targetObject.SetActive(isRevealed);
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { revealed = isRevealed });
    }

    public override void Interact()
    {
        if (!canInteract) return;

        if (targetObject != null)
        {
            targetObject.SetActive(true);
            isRevealed = true;
            GameAudio.PlaySfx("reveal");
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
