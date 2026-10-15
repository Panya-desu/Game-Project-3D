using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dev tool: writes a text dump of every scene hierarchy (world position, bounds, components).
/// Run from menu Tools/Dump Scenes or batch mode: -executeMethod SceneDump.DumpAll
/// </summary>
public static class SceneDump
{
    [MenuItem("Tools/Dev/Dump Scenes")]
    public static void DumpAll()
    {
        string outDir = System.Environment.GetEnvironmentVariable("SCENE_DUMP_DIR");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Application.dataPath, "../Logs/SceneDump");
        Directory.CreateDirectory(outDir);

        foreach (var s in EditorBuildSettings.scenes.Select(x => x.path).Concat(new[] { "Assets/Scenes/WhiteSpace 2.unity", "Assets/Scenes/DemoEnd.unity" }).Distinct())
        {
            if (!File.Exists(s)) continue;
            var scene = EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
            var sb = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
                Dump(root.transform, 0, sb);
            File.WriteAllText(Path.Combine(outDir, Path.GetFileNameWithoutExtension(s) + ".txt"), sb.ToString());
        }
        Debug.Log("[SceneDump] done -> " + outDir);
    }

    static void Dump(Transform t, int depth, StringBuilder sb)
    {
        var p = t.position;
        string bounds = "";
        var rends = t.GetComponentsInChildren<Renderer>(true);
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            bounds = $" bounds[min=({b.min.x:F1},{b.min.y:F1},{b.min.z:F1}) max=({b.max.x:F1},{b.max.y:F1},{b.max.z:F1})]";
        }
        var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
        int missing = t.GetComponents<Component>().Count(c => c == null);
        sb.AppendLine($"{new string(' ', depth * 2)}{t.name}{(t.gameObject.activeSelf ? "" : " [inactive]")} pos=({p.x:F1},{p.y:F1},{p.z:F1}){bounds} {{{comps}}}{(missing > 0 ? " MISSING_SCRIPTS=" + missing : "")}");
        // Skip deep model internals to keep the dump readable
        if (depth > 6) return;
        foreach (Transform c in t) Dump(c, depth + 1, sb);
    }
}
