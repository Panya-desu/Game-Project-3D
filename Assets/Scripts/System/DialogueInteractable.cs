using UnityEngine;

public class DialogueInteractable : Interactable
{
    public DialogueManager dialogueManager;
    public DialogueLine[] lines;

    public override void Interact()
    {
        if (!canInteract) return;

        dialogueManager.StartDialogue(lines);

        canInteract = false;
    }
}