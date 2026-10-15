using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Component ที่ต้องการเก็บสถานะลง save ให้ implement interface นี้
/// CaptureState() คืนค่าสถานะเป็น JSON (ใช้ JsonUtility.ToJson)
/// ตอนโหลด ให้ component เรียก SaveSystem.TryLoad() เองใน Awake/Start
/// </summary>
public interface ISaveable
{
    string CaptureState();
}

/// <summary>
/// สถานะที่ keypad ทุกแบบใช้ร่วมกัน
/// </summary>
[Serializable]
public class KeypadSaveState
{
    public bool solved;
    public string code;
}

[Serializable]
public class SaveEntry
{
    public string id;
    public string json;
}

[Serializable]
public class SaveData
{
    public string sceneName;
    public string savedAt;
    public List<string> items = new List<string>();
    public List<string> keys = new List<string>();
    public List<SaveEntry> entries = new List<SaveEntry>();
    public float runTime = -1f; // seconds used of the 40-minute limit (RunTimer), -1 = not started
}

/// <summary>
/// ระบบ save/load ความคืบหน้าของเกม
/// - Save(): เก็บ scene ปัจจุบัน + ไอเทม/กุญแจ + สถานะของทุก ISaveable ใน scene ลงไฟล์ JSON
/// - LoadGame(): อ่านไฟล์แล้วโหลด scene นั้น แต่ละ component จะดึงสถานะของตัวเองผ่าน TryLoad()
/// แต่ละ component ถูกระบุด้วย path ใน hierarchy ดังนั้นถ้าแก้ hierarchy ใน scene ไฟล์ save เก่าอาจใช้ไม่ได้
/// </summary>
public static class SaveSystem
{
    private const string FileName = "savegame.json";

    private static SaveData pendingData;
    private static Dictionary<string, string> pendingStates;

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static bool HasSave => File.Exists(SavePath);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        pendingData = null;
        pendingStates = null;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        // ใช้สถานะที่โหลดมาแค่ครั้งเดียวกับ scene ที่ save ไว้
        if (pendingData != null && scene.name == pendingData.sceneName)
        {
            pendingData = null;
            pendingStates = null;
        }
    }

    // ───────────── Save / Load ─────────────

    public static void Save()
    {
        Scene scene = SceneManager.GetActiveScene();

        SaveData data = new SaveData
        {
            sceneName = scene.name,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            items = ItemPickup.GetAllItems(),
            keys = KeyPickup.GetAllKeys(),
            runTime = RunTimer.Elapsed
        };

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ISaveable saveable && behaviour.gameObject.scene == scene)
            {
                data.entries.Add(new SaveEntry
                {
                    id = GetId(behaviour),
                    json = saveable.CaptureState()
                });
            }
        }

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        Debug.Log($"[SaveSystem] Saved {data.entries.Count} objects in '{data.sceneName}' → {SavePath}");
    }

    /// <summary>
    /// โหลด save แล้วไปที่ scene ที่ save ไว้ (คืน false ถ้าไม่มีไฟล์หรือไฟล์เสีย)
    /// </summary>
    public static bool LoadGame()
    {
        if (!HasSave) return false;

        SaveData data;
        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] Failed to read save: " + e.Message);
            return false;
        }

        if (data == null || string.IsNullOrEmpty(data.sceneName)) return false;

        ResetRuntimeState();

        RunTimer.Restore(data.runTime);
        ItemPickup.SetAllItems(data.items);
        KeyPickup.SetAllKeys(data.keys);

        pendingData = data;
        pendingStates = new Dictionary<string, string>();
        foreach (SaveEntry entry in data.entries)
        {
            pendingStates[entry.id] = entry.json;
        }

        Debug.Log($"[SaveSystem] Loading '{data.sceneName}' (saved {data.savedAt})");
        SceneManager.LoadScene(data.sceneName);
        return true;
    }

    /// <summary>
    /// เริ่มเกมใหม่ (ล้างไอเทม/สถานะที่ค้างอยู่ แต่ไม่ลบไฟล์ save)
    /// </summary>
    public static void StartNewGame(string sceneName)
    {
        ResetRuntimeState();
        // Notes from an earlier play-through must not appear in a new game
        PlayerPrefs.DeleteKey(PhoneNoteApp.NotePrefsKey);
        SceneManager.LoadScene(sceneName);
    }

    public static void ReturnToMainMenu(string mainMenuScene)
    {
        ResetRuntimeState();
        SceneManager.LoadScene(mainMenuScene);
    }

    public static void DeleteSave()
    {
        if (HasSave) File.Delete(SavePath);
    }

    /// <summary>
    /// ดึงสถานะที่ save ไว้ของ component นี้ (ใช้ใน Awake/Start ของ component)
    /// </summary>
    public static bool TryLoad<T>(Component component, out T state)
    {
        state = default;

        if (pendingStates == null || component.gameObject.scene.name != pendingData.sceneName)
        {
            return false;
        }

        if (!pendingStates.TryGetValue(GetId(component), out string json))
        {
            return false;
        }

        state = JsonUtility.FromJson<T>(json);
        return state != null;
    }

    // ───────────── Helpers ─────────────

    private static void ResetRuntimeState()
    {
        pendingData = null;
        pendingStates = null;
        ItemPickup.ClearAllItems();
        KeyPickup.ClearAllKeys();
        KeypadInteractable.IsEnteringKeypad = false;
        GameState.IsCutscene = false;
        RunTimer.ResetRun();
        Time.timeScale = 1f;
    }

    /// <summary>
    /// ID ของ component = scene + path ใน hierarchy (รวมลำดับ sibling) + ชนิด component
    /// </summary>
    private static string GetId(Component component)
    {
        string path = "";
        for (Transform t = component.transform; t != null; t = t.parent)
        {
            path = "/" + t.name + "#" + t.GetSiblingIndex() + path;
        }

        Component[] sameType = component.GetComponents(component.GetType());
        int index = Array.IndexOf(sameType, component);

        return component.gameObject.scene.name + path + ":" + component.GetType().Name + "#" + index;
    }
}
