using UnityEngine;

/// <summary>
/// วางบน Object B (ของที่เก็บได้ใน scene)
/// เมื่อ player interact จะเก็บของ (บันทึก itemID) แล้ว Object B จะหายไป
/// ใช้คู่กับ PlaceItemInteractable ที่วางบน Object A
/// </summary>
public class ItemPickup : Interactable, ISaveable
{
    [System.Serializable]
    private class SaveState
    {
        public bool active;
    }

    [Header("Item Settings")]
    [Tooltip("ID ของไอเทม ใช้จับคู่กับ PlaceItemInteractable")]
    [SerializeField] private string itemID = "item_default";

    [Header("UI Text")]
    [Tooltip("ข้อความที่แสดงเมื่อ player มองไอเทม")]
    [SerializeField] private string pickupText = "press E to pick up";

    /// <summary>
    /// เก็บ item ที่ player เก็บได้แล้ว (เช็คด้วย itemID)
    /// ใช้ static HashSet เพื่อให้เช็คข้าม script ได้ง่าย
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> collectedItems
        = new System.Collections.Generic.HashSet<string>();

    private void Awake()
    {
        // เก็บไปแล้วใน save → ซ่อน
        if (SaveSystem.TryLoad(this, out SaveState state) && !state.active)
        {
            gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        interactText = pickupText;
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveState { active = gameObject.activeSelf });
    }

    public override void Interact()
    {
        if (!canInteract) return;

        // เก็บไอเทม
        collectedItems.Add(itemID);
        Debug.Log("[ItemPickup] Collected item: " + itemID);

        GameAudio.PlaySfx("pickup");
        if (QuestManager.Instance != null) QuestManager.Instance.CompleteObjectiveByTarget(gameObject);

        // หายไปจาก scene
        gameObject.SetActive(false);
    }

    /// <summary>
    /// เช็คว่า player มีไอเทม ID นี้หรือยัง
    /// </summary>
    public static bool HasItem(string id)
    {
        return collectedItems.Contains(id);
    }

    /// <summary>
    /// ลบไอเทมออกจาก inventory (ใช้เมื่อวางของเรียบร้อยแล้ว)
    /// </summary>
    public static bool RemoveItem(string id)
    {
        return collectedItems.Remove(id);
    }

    /// <summary>
    /// ล้าง item ทั้งหมด (เช่น ตอนเริ่มเกมใหม่)
    /// </summary>
    public static void ClearAllItems()
    {
        collectedItems.Clear();
    }

    /// <summary>
    /// ดึง/ตั้งค่า item ทั้งหมด (ใช้กับ SaveSystem)
    /// </summary>
    public static System.Collections.Generic.List<string> GetAllItems()
    {
        return new System.Collections.Generic.List<string>(collectedItems);
    }

    public static void SetAllItems(System.Collections.Generic.IEnumerable<string> items)
    {
        collectedItems.Clear();
        if (items == null) return;
        collectedItems.UnionWith(items);
    }
}
