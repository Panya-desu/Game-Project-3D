using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small light fixes so every room of the house sits in the same warm, readable range. The living
/// room's sofa and TV corner was outside the reach of its fill light (LevelLightingBuilder puts it in the
/// middle of the room) and looked much darker and muddier than the bedroom and kitchen; a soft warm fill
/// without shadows now lights that corner as if from the lamps bouncing off the walls. The red emergency
/// light beside the fuse box is softened so the box does not blow out to a flat pink panel.
/// </summary>
public static class RoomFillTweaks
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Level_1" || GameObject.Find("[RoomFillTweaks]") != null) return;
        var root = new GameObject("[RoomFillTweaks]");
        SceneManager.MoveGameObjectToScene(root, scene);
        Fill(root.transform, "LivingCorner", new Vector3(11.2f, 2.7f, 20.8f), new Color(1f, 0.85f, 0.68f), 1.6f, 7.5f);

        // the red emergency light right beside the fuse box (and the box's own highlight) blew its door out
        // into a flat pink panel: softer, so the box and its fuse slots read under the warning red
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector3 p = l.transform.position;
            bool nearBox = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(23.5f, 8.5f)) < 2.5f;
            if (!nearBox) continue;
            if (l.name.StartsWith("Emergency")) l.intensity = Mathf.Min(l.intensity, 2.2f);
            else if (l.name == "FuseBox_Highlight") l.intensity = Mathf.Min(l.intensity, 1.8f);
        }
    }

    static void Fill(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject("Fill_" + name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }
}
