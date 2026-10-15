using System.Collections;
using UnityEngine;

/// <summary>
/// Put on a door leaf that stands in front of a scene-change Door (the ending door in Level_1):
/// the leaf used to be a locked InteractiveDoor, so aiming at the doorway only ever said "locked".
/// Now pressing E on the leaf swings it open and steps through the Door behind it.
/// </summary>
public class EnterThrough : Interactable
{
    public Door target;
    public DoorController leaf;
    bool used;

    public static void Wire(Door door)
    {
        if (door == null) return;
        Transform root = door.transform.parent != null ? door.transform.parent : door.transform;
        foreach (var leafDoor in root.GetComponentsInChildren<InteractiveDoor>(true))
        {
            leafDoor.canInteract = false; // never unlocked by anything: hand the interaction to the Door
            var e = leafDoor.GetComponent<EnterThrough>();
            if (e == null) e = leafDoor.gameObject.AddComponent<EnterThrough>();
            e.target = door;
            e.leaf = leafDoor.GetComponentInChildren<DoorController>();
            if (e.leaf == null) e.leaf = root.GetComponentInChildren<DoorController>(true);
            e.interactText = "Press E to step through";
            e.canInteract = true;
        }
    }

    public override void Interact()
    {
        if (used || target == null) return;
        used = true;
        canInteract = false;
        StartCoroutine(Open());
    }

    IEnumerator Open()
    {
        if (leaf != null)
        {
            GameAudio.PlaySfx("door_open");
            leaf.OpenDoor();
            yield return new WaitForSeconds(0.6f);
        }
        target.Interact();
    }
}
