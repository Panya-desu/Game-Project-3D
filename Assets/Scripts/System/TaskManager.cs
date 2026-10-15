using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TaskManager : MonoBehaviour
{
    public TextMeshProUGUI taskText;
    public List<Quest> quests;

    private int currentQuestIndex = 0;
    private List<TaskData> currentTasks;

    public GameObject exitDoor;
    public DialogueManager dialogueManager;

    private bool allTasksDone = false;
    private int completedTasks = 0;
    private bool hasStartedTasks = false;
    private bool nextQuestPending = false;

    void Start()
    {
        taskText.text = "";
    }

    public void StartTask()
    {
        hasStartedTasks = true;
        currentQuestIndex = 0;

        LoadQuest(currentQuestIndex);
    }

    public void CompleteTask(TaskData task)
    {
        if (task.isCompleted) return;

        task.isCompleted = true;
        completedTasks++;

        UpdateTaskUI();

        if (completedTasks >= currentTasks.Count)
        {
            AllTasksCompleted();
        }
    }

    void UpdateTaskUI()
    {
        if (!hasStartedTasks || currentTasks == null) return;

        taskText.text = "Quest : " + quests[currentQuestIndex].questName + "\n";

        foreach (TaskData task in currentTasks)
        {
            if (task.isCompleted)
            {
                taskText.text += "<color=grey><s>- " + task.description + "</s></color>\n";
            }
            else
            {
                taskText.text += "- " + task.description + "\n";
            }
        }
    }

    void AllTasksCompleted()
    {
        if (allTasksDone) return;
        allTasksDone = true;

        DialogueLine[] after = quests[currentQuestIndex].afterDialogue;
        bool hasDialogue = dialogueManager != null && after != null && after.Length > 0;
        if (hasDialogue)
        {
            dialogueManager.StartDialogue(after);
        }

        currentQuestIndex++;

        if (currentQuestIndex < quests.Count)
        {
            allTasksDone = false;
            nextQuestPending = true;
            // The next quest is normally loaded when the dialogue ends; without dialogue load it now
            if (!hasDialogue)
            {
                LoadNextQuest();
            }
        }
        else
        {
            taskText.text = "Quest :\n- Go to the door";
            if (exitDoor != null)
            {
                Door oldDoor = exitDoor.GetComponent<Door>();
                if (oldDoor != null)
                {
                    oldDoor.Unlock();
                }
                else
                {
                    InteractiveDoor newDoor = exitDoor.GetComponent<InteractiveDoor>();
                    if (newDoor != null)
                    {
                        newDoor.Unlock();
                    }
                }
            }
        }
    }

    public void LoadNextQuest()
    {
        // Called after every dialogue; only advance when a quest was actually finished
        if (nextQuestPending && currentQuestIndex < quests.Count)
        {
            nextQuestPending = false;
            LoadQuest(currentQuestIndex);
        }
    }

    void LoadQuest(int index)
    {
        currentTasks = quests[index].tasks;
        completedTasks = 0;
        if(currentTasks.Count != 0) {
            foreach (TaskData task in currentTasks)
            {
                task.isCompleted = false;

                if (task.targetObject == null)
                {
                    Debug.LogWarning("[TaskManager] Task '" + task.taskName + "' has no target object");
                    continue;
                }

                InteractableTask interact = task.targetObject.GetComponent<InteractableTask>();
                if (interact != null)
                {
                    interact.SetTask(this, task);
                }
                else
                {
                    Debug.LogWarning("No InteractableTask on " + task.targetObject.name);
                }
            }
            UpdateTaskUI();
        }
    }
}