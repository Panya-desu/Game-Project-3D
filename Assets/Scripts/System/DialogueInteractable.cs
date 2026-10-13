using UnityEngine;

public class DialogueInteractable : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool used;
    }

    public DialogueManager dialogueManager;
    public DialogueLine[] lines;

    private void Awake()
    {
        if (SaveSystem.TryLoad(this, out SaveState state) && state.used)
        {
            canInteract = false;
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { used = !canInteract });
    }

    public override void Interact()
    {
        if (!canInteract) return;

        dialogueManager.StartDialogue(lines);

        canInteract = false;
    }
}
