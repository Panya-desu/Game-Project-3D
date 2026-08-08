using UnityEngine;

/// <summary>
/// Attach this to any object that should be interactable as a quest objective.
/// Works with QuestManager for Interact and Collect objective types.
/// </summary>
public class QuestInteractable : Interactable
{
    private QuestManager questManager;
    private QuestObjective objective;
    private bool isSetUp = false;

    public void SetObjective(QuestManager manager, QuestObjective obj)
    {
        questManager = manager;
        objective = obj;
        isSetUp = true;

        // Set the interact text based on the objective type
        switch (obj.type)
        {
            case ObjectiveType.Collect:
                interactText = "press E to pick up";
                break;
            case ObjectiveType.Interact:
                interactText = "press E to interact";
                break;
            default:
                interactText = "press E";
                break;
        }
    }

    public override void Interact()
    {
        if (!isSetUp || objective == null || objective.isCompleted) return;

        canInteract = false;

        if (objective.hideOnComplete)
        {
            gameObject.SetActive(false);
        }

        questManager.CompleteObjective(objective);
    }
}
