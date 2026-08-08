using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages a sequential list of quests for Level_1.
/// Each quest has objectives (collect items, unlock doors, interact with objects).
/// Once all objectives in a quest are done, the next quest starts automatically.
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI questText;

    [Header("Quest List")]
    public List<QuestData> quests;



    [Header("References")]
    [SerializeField] private DialogueManager dialogueManager;

    private int currentQuestIndex = -1;
    private int completedObjectives = 0;
    private bool hasStarted = false;
    private bool allQuestsComplete = false;

    void Start()
    {
        if (questText != null)
        {
            questText.text = "";
        }
    }

    void Update()
    {
        if (!hasStarted || allQuestsComplete) return;

        // Check UnlockDoor objectives automatically each frame
        CheckDoorObjectives();

        // Check UnlockPhone objectives automatically each frame
        CheckPhoneObjectives();
    }

    /// <summary>
    /// Call this to start the quest chain (e.g., after an intro dialogue).
    /// </summary>
    public void StartQuests()
    {
        hasStarted = true;
        currentQuestIndex = 0;
        LoadCurrentQuest();
    }

    /// <summary>
    /// Called by QuestInteractable when the player completes an Interact/Collect objective.
    /// Can also be called from any other script for Custom objectives.
    /// </summary>
    public void CompleteObjective(QuestObjective objective)
    {
        if (objective.isCompleted) return;

        objective.isCompleted = true;
        completedObjectives++;

        Debug.Log("[QuestManager] Objective completed: " + objective.description);

        UpdateUI();

        if (completedObjectives >= quests[currentQuestIndex].objectives.Count)
        {
            OnQuestComplete();
        }
    }

    /// <summary>
    /// Complete an objective by finding it via its target GameObject.
    /// Useful for completing objectives from external scripts (e.g., KeypadInteractable).
    /// </summary>
    public void CompleteObjectiveByTarget(GameObject target)
    {
        if (currentQuestIndex < 0 || currentQuestIndex >= quests.Count) return;

        foreach (QuestObjective obj in quests[currentQuestIndex].objectives)
        {
            if (!obj.isCompleted && obj.targetObject == target)
            {
                CompleteObjective(obj);
                return;
            }
        }
    }

    // ───────────── Private Methods ─────────────

    private void LoadCurrentQuest()
    {
        if (currentQuestIndex >= quests.Count)
        {
            OnAllQuestsComplete();
            return;
        }

        QuestData quest = quests[currentQuestIndex];
        completedObjectives = 0;

        foreach (QuestObjective obj in quest.objectives)
        {
            obj.isCompleted = false;

            if (obj.targetObject == null) continue;

            // Set up interactable components for Interact and Collect types
            if (obj.type == ObjectiveType.Interact || obj.type == ObjectiveType.Collect)
            {
                QuestInteractable interactable = obj.targetObject.GetComponent<QuestInteractable>();
                if (interactable != null)
                {
                    interactable.canInteract = true;
                    interactable.SetObjective(this, obj);
                }
                else
                {
                    Debug.LogWarning("[QuestManager] No QuestInteractable on: " + obj.targetObject.name
                        + " (needed for " + obj.type + " objective)");
                }
            }
            // UnlockDoor and Custom types don't need setup — they're detected automatically or completed externally
        }

        UpdateUI();
        Debug.Log("[QuestManager] Started quest: " + quest.questName);
    }

    private void CheckDoorObjectives()
    {
        if (currentQuestIndex < 0 || currentQuestIndex >= quests.Count) return;

        foreach (QuestObjective obj in quests[currentQuestIndex].objectives)
        {
            if (obj.isCompleted || obj.type != ObjectiveType.UnlockDoor) continue;
            if (obj.targetObject == null) continue;

            // Check if the InteractiveDoor on the target has been unlocked
            InteractiveDoor door = obj.targetObject.GetComponent<InteractiveDoor>();
            if (door != null && !door.IsLocked())
            {
                CompleteObjective(obj);
            }
        }
    }

    private void CheckPhoneObjectives()
    {
        if (currentQuestIndex < 0 || currentQuestIndex >= quests.Count) return;

        foreach (QuestObjective obj in quests[currentQuestIndex].objectives)
        {
            if (obj.isCompleted || obj.type != ObjectiveType.UnlockPhone) continue;

            // Check if PhoneManager says it's unlocked
            if (PhoneManager.Instance != null && !PhoneManager.Instance.IsLocked)
            {
                CompleteObjective(obj);
            }
        }
    }

    private void OnQuestComplete()
    {
        QuestData quest = quests[currentQuestIndex];

        Debug.Log("[QuestManager] Quest complete: " + quest.questName);

        // Play after-complete dialogue if available
        if (dialogueManager != null && quest.afterCompleteDialogue != null && quest.afterCompleteDialogue.Length > 0)
        {
            dialogueManager.StartDialogue(quest.afterCompleteDialogue);
        }

        // Move to next quest
        currentQuestIndex++;
        LoadCurrentQuest();
    }

    private void OnAllQuestsComplete()
    {
        allQuestsComplete = true;

        Debug.Log("[QuestManager] All quests complete!");

        if (questText != null)
        {
            questText.text = "";
        }
    }

    private void UpdateUI()
    {
        if (questText == null || !hasStarted) return;
        if (currentQuestIndex < 0 || currentQuestIndex >= quests.Count) return;

        QuestData quest = quests[currentQuestIndex];
        string text = "Quest : " + quest.questName + "\n";

        foreach (QuestObjective obj in quest.objectives)
        {
            if (obj.isCompleted)
            {
                text += "<color=grey><s>- " + obj.description + "</s></color>\n";
            }
            else
            {
                text += "- " + obj.description + "\n";
            }
        }

        questText.text = text;
    }
}
