using System.Collections.Generic;

[System.Serializable]
public class Quest
{
    public string questName;
    public List<TaskData> tasks;
    public DialogueLine[] afterDialogue;
}