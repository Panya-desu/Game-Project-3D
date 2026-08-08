using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : Interactable
{
    [Header("Door Settings")]
    [SerializeField] private string sceneName;
    [SerializeField] private bool isUnlocked = false;

    public void Unlock()
    {
        isUnlocked = true;
        Debug.Log("[Door] Unlocked: " + gameObject.name);
    }

    public override void Interact()
    {
        if (!isUnlocked)
        {
            Debug.Log("[Door] Locked");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[Door] Scene name is EMPTY!");
            return;
        }

        Debug.Log("[Door] Loading scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }
}