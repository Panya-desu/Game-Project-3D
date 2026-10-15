using UnityEngine;

public class InteractableTask : Interactable
{
    private TaskManager taskManager;
    private TaskData taskData;

    public void SetTask(TaskManager manager, TaskData data)
    {
        taskManager = manager;
        taskData = data;
    }

    public override void Interact()
    {
        // Not assigned yet (the task list starts after the intro dialogue) or already done
        if (taskData == null || taskManager == null || taskData.isCompleted) return;

        canInteract = false;

        Debug.Log("Interact with: " + taskData.taskName);

        if (taskData.hideAfterInteract)
        {
            gameObject.SetActive(false);
        }

        taskManager.CompleteTask(taskData);
    }
}