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

        if (dialogueManager != null && quests[currentQuestIndex].afterDialogue != null)
        {
            dialogueManager.StartDialogue(quests[currentQuestIndex].afterDialogue);
        }

        currentQuestIndex++;

        if (currentQuestIndex < quests.Count)
        {
            allTasksDone = false;
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
        if (currentQuestIndex < quests.Count)
        {
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