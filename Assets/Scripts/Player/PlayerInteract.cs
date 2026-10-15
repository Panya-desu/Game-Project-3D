using TMPro;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    public TextMeshProUGUI interactText;
    public DialogueManager dialogueManager;

    Camera cam;

    /// <summary>True while the player is looking at something they can press E on (used by the crosshair).</summary>
    public static bool IsAimingAtInteractable { get; private set; }

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void OnDisable()
    {
        IsAimingAtInteractable = false;
    }

    void SetPrompt(string text)
    {
        IsAimingAtInteractable = !string.IsNullOrEmpty(text);
        if (interactText != null) interactText.text = text;
    }

    void Update()
    {
        if (GameState.InputBlockedOrTalking || (dialogueManager != null && dialogueManager.IsTalking()))
        {
            SetPrompt("");
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        // nearest hit that is not the player itself
        Transform self = cam.transform.root;
        RaycastHit hit = default;
        bool found = false;
        // Ignore trigger volumes (area triggers would otherwise block the ray)
        var hits = Physics.RaycastAll(ray, interactDistance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (var h in hits)
        {
            if (h.collider.transform.root == self) continue;
            hit = h; found = true;
            break;
        }

        if (found)
        {
            Interactable[] interactables = hit.collider.GetComponents<Interactable>();
            if (interactables.Length > 0)
            {
                Interactable primaryInteractable = null;
                foreach (var interactable in interactables)
                {
                    if (interactable.canInteract)
                    {
                        primaryInteractable = interactable;
                        break;
                    }
                }

                if (primaryInteractable != null)
                {
                    SetPrompt(primaryInteractable.interactText);

                    if (Input.GetKeyDown(KeyCode.E))
                    {
                        foreach (var interactable in interactables)
                        {
                            if (interactable.canInteract)
                            {
                                interactable.Interact();
                            }
                        }
                    }
                }
                else
                {
                    SetPrompt("");
                }

                return;
            }
        }
        SetPrompt("");
    }
}
