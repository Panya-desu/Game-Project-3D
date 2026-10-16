using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// An invisible fence around the part of the White Space the player needs (the start and the three
/// doors), so a player strolling over the endless water cannot wander off across the 200 m floor. It is
/// an oval of wall segments on the Ignore Raycast layer: the character controller stops at it, while
/// interaction rays and the cutscene cameras pass through.
/// </summary>
public static class WhiteSpaceBounds
{
    const string SceneName = "WhiteSpace";
    static readonly Vector2 Centre = new Vector2(0f, 6f);   // x, z: between the start (0, 0) and the doors (z 14)
    static readonly Vector2 Radius = new Vector2(12f, 14f);  // oval radii in x and z
    const int Segments = 32;
    const float Height = 8f, Thickness = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName || GameObject.Find("[WhiteSpaceBounds]") != null) return;
        var root = new GameObject("[WhiteSpaceBounds]");
        SceneManager.MoveGameObjectToScene(root, scene);
        for (int i = 0; i < Segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / Segments, a1 = (i + 1) * Mathf.PI * 2f / Segments;
            Vector3 p0 = Point(a0), p1 = Point(a1);
            var seg = new GameObject("Fence") { layer = 2 }; // Ignore Raycast
            seg.transform.SetParent(root.transform, false);
            seg.transform.position = (p0 + p1) * 0.5f + Vector3.up * Height * 0.5f;
            seg.transform.rotation = Quaternion.LookRotation(p1 - p0, Vector3.up);
            var box = seg.AddComponent<BoxCollider>();
            box.size = new Vector3(Thickness, Height, Vector3.Distance(p0, p1) + Thickness);
        }
    }

    static Vector3 Point(float a) => new Vector3(Centre.x + Mathf.Cos(a) * Radius.x, 0f, Centre.y + Mathf.Sin(a) * Radius.y);
}
