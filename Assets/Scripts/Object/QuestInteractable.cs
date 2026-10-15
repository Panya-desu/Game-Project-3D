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

    private void Start()
    {
        // Not part of an active quest yet: don't show a prompt that does nothing
        // (e.g. the TV remote kept showing "Open TV" forever after it was used)
        if (!isSetUp) canInteract = false;
    }

    public void SetObjective(QuestManager manager, QuestObjective obj)
    {
        questManager = manager;
        objective = obj;
        isSetUp = true;

        // Set the interact text based on the objective type
        // Keep a custom prompt typed in the Inspector, otherwise use a default per objective type
        if (string.IsNullOrEmpty(interactText) || interactText == "Interact")
        {
            switch (obj.type)
            {
                case ObjectiveType.Collect:
                    interactText = "Press E to pick up";
                    break;
                case ObjectiveType.Interact:
                    interactText = "Press E to interact";
                    break;
                default:
                    interactText = "Press E";
                    break;
            }
        }
    }

    public override void Interact()
    {
        if (!isSetUp || objective == null || objective.isCompleted) return;

        canInteract = false;
        GameAudio.PlaySfx(objective.type == ObjectiveType.Collect ? "pickup" : "interact");

        if (objective.hideOnComplete)
        {
            gameObject.SetActive(false);
        }

        questManager.CompleteObjective(objective);
    }
}
