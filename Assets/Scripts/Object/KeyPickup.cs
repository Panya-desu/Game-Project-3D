using UnityEngine;

/// <summary>
/// วางบน object กุญแจที่อยู่ใน scene
/// เมื่อ player interact จะเก็บกุญแจ (set hasKey = true) แล้วกุญแจจะหายไป
/// </summary>
public class KeyPickup : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool active;
    }

    [Header("Key Settings")]
    [Tooltip("ID ของกุญแจ ใช้จับคู่กับ KeyDoorInteractable")]
    [SerializeField] private string keyID = "key_default";

    /// <summary>
    /// เก็บ key ที่ player เก็บได้แล้ว (เช็คด้วย keyID)
    /// ใช้ static HashSet เพื่อให้เช็คข้าม script ได้ง่าย
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> collectedKeys
        = new System.Collections.Generic.HashSet<string>();

    private void Awake()
    {
        // เก็บไปแล้วใน save → ซ่อน
        if (SaveSystem.TryLoad(this, out SaveState state) && !state.active)
        {
            GameAudio.PlaySfx("pickup");
        if (QuestManager.Instance != null) QuestManager.Instance.CompleteObjectiveByTarget(gameObject);
        gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        interactText = "press E to pick up key";
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { active = gameObject.activeSelf });
    }

    public override void Interact()
    {
        if (!canInteract) return;

        // เก็บกุญแจ
        collectedKeys.Add(keyID);
        Debug.Log("[KeyPickup] Collected key: " + keyID);

        // หายไปจาก scene
        gameObject.SetActive(false);
    }

    /// <summary>
    /// เช็คว่า player มีกุญแจ ID นี้หรือยัง
    /// </summary>
    public static bool HasKey(string id)
    {
        return collectedKeys.Contains(id);
    }

    /// <summary>
    /// ล้าง key ทั้งหมด (เช่น ตอนเริ่มเกมใหม่)
    /// </summary>
    public static void ClearAllKeys()
    {
        collectedKeys.Clear();
    }

    /// <summary>
    /// ดึง/ตั้งค่า key ทั้งหมด (ใช้กับ SaveSystem)
    /// </summary>
    public static System.Collections.Generic.List<string> GetAllKeys()
    {
        return new System.Collections.Generic.List<string>(collectedKeys);
    }

    public static void SetAllKeys(System.Collections.Generic.IEnumerable<string> keys)
    {
        collectedKeys.Clear();
        if (keys == null) return;
        collectedKeys.UnionWith(keys);
    }
}
