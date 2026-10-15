using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class QuestData
{
    public string questName;
    public List<QuestObjective> objectives;
    public DialogueLine[] afterCompleteDialogue;

    [UnityEngine.Tooltip("Runs when this quest becomes the active quest (e.g. enable a keypad, unlock a door)")]
    public UnityEvent onStart = new UnityEvent();

    [UnityEngine.Tooltip("Runs when every objective of this quest is done (e.g. start a blackout, open the exit)")]
    public UnityEvent onComplete = new UnityEvent();
}
