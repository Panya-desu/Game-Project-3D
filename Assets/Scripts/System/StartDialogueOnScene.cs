using UnityEngine;
using System.Collections;

public class StartDialogueOnScene : MonoBehaviour, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool played;
    }

    public DialogueManager dialogueManager;

    public float delay = 3f;

    public DialogueLine[] lines;

    private bool played = false;

    void Start()
    {
        // เล่นไปแล้วก่อน save → ไม่เล่นซ้ำตอนโหลด
        if (SaveSystem.TryLoad(this, out SaveState state) && state.played)
        {
            played = true;
            return;
        }

        StartCoroutine(StartDialogueWithDelay());
    }

    public string CaptureState()
    {
        // นับว่าเล่นแล้วเมื่อจบบทสนทนาแรกจริง (ถ้า save กลางบทสนทนา จะเล่นใหม่ตอนโหลด)
        bool done = dialogueManager != null ? dialogueManager.FirstDialogueDone : played;
        return JsonUtility.ToJson(new SaveState { played = played && done });
    }

    IEnumerator StartDialogueWithDelay()
    {
        dialogueManager.dialoguePanel.SetActive(false);
        yield return new WaitForSeconds(delay);
        played = true;
        dialogueManager.StartDialogue(lines);
    }
}
