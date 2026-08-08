using UnityEngine;
using System.Collections;

public class StartDialogueOnScene : MonoBehaviour
{
    public DialogueManager dialogueManager;

    public float delay = 3f;

    public DialogueLine[] lines;

    void Start()
    {
        StartCoroutine(StartDialogueWithDelay());
    }

    IEnumerator StartDialogueWithDelay()
    {
        dialogueManager.dialoguePanel.SetActive(false);
        yield return new WaitForSeconds(delay);
        dialogueManager.StartDialogue(lines);
    }
}