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
    [Tooltip("เสียงตอนเข้าประตู (ประตูไม้ = door_open, ประตูแห่งความทรงจำใน White Space = memory_whoosh)")]
    [SerializeField] private string openSound = "door_open";

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

    public override void Interact() => Enter(true);

    /// <summary>Goes through the door; playSound false when the caller already played the opening (EnterThrough).</summary>
    public void Enter(bool playSound)
    {
        if (!isUnlocked)
        {
            Debug.Log("[Door] Locked");
            GameAudio.PlaySfx("door_locked");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[Door] Scene name is EMPTY!");
            return;
        }

        Debug.Log("[Door] Loading scene: " + sceneName);
        canInteract = false;
        if (playSound) GameAudio.PlaySfx(openSound);
        // Fade instead of an instant cut
        ScreenFader.LoadScene(sceneName);
    }
}