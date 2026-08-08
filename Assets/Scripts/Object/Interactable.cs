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
}