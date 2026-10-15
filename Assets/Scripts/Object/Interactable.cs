using UnityEngine;

public class Interactable : MonoBehaviour
{
    public string interactText = "Interact";
    public bool canInteract = true;

    public virtual void Interact()
    {
        if (!canInteract) return;

        Debug.Log("Interacted with " + gameObject.name);
    }

    /// <summary>Lets UnityEvents (quest start/complete) switch an object on or off.</summary>
    public void SetInteractable(bool value)
    {
        canInteract = value;
    }
}
