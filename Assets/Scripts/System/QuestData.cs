using System.Collections.Generic;

[System.Serializable]
public class QuestData
{
    public string questName;
    public List<QuestObjective> objectives;
    public DialogueLine[] afterCompleteDialogue;
}
