using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool unlocked;
    }

    [Header("Door Settings")]
    [SerializeField] private string sceneName;
    [SerializeField] private bool isUnlocked = false;

    private void Awake()
    {
        if (SaveSystem.TryLoad(this, out SaveState state))
        {
            isUnlocked = state.unlocked;
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { unlocked = isUnlocked });
    }

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