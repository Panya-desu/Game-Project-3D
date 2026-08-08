using UnityEngine;

public enum ObjectiveType
{
    Interact,       // Player presses E on the target object
    Collect,        // Player collects the item (hides it after)
    UnlockDoor,     // Completed when an InteractiveDoor is unlocked
    Custom,         // Completed manually via script (e.g., enter keypad code)
    UnlockPhone     // Completed when the player unlocks the phone
}

[System.Serializable]
public class QuestObjective
{
    public string description;
    public ObjectiveType type = ObjectiveType.Interact;

    [Tooltip("The target GameObject for this objective (item to collect, door to unlock, object to interact with)")]
    public GameObject targetObject;

    [Tooltip("Hide the target object after completing this objective (useful for collectibles)")]
    public bool hideOnComplete = false;

    [HideInInspector] public bool isCompleted = false;
}
