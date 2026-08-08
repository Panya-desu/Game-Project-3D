using UnityEngine;

public class DoorController : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    public void OpenDoor()
    {
        if (animator != null)
        {
            animator.enabled = true;
            animator.SetBool("isOpen", true);
        }
    }

    public void CloseDoor()
    {
        if (animator != null)
        {
            animator.enabled = true;
            animator.SetBool("isOpen", false);
        }
    }
}
