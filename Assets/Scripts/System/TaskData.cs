using UnityEngine;

[System.Serializable]
public class TaskData
{
    public string taskName;
    [TextArea] public string description;

    public GameObject targetObject;
    public bool hideAfterInteract;

    [HideInInspector] public bool isCompleted = false;
}