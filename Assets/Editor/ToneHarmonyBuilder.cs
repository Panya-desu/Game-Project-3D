using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds Resources/ToneHarmony.asset: the flat-coloured model materials used in the game's scenes whose
/// colour is too saturated for the game's muted palette, each with a toned-down colour (same hue, less
/// saturation, a little darker). Only materials inside imported model files (Assets/Models), without a
/// texture; keypads and the key keep their colours, and the puzzle colours in Assets/Materials are not
/// model materials. Batch: -executeMethod ToneHarmonyBuilder.Build -quit
/// </summary>
public static class ToneHarmonyBuilder
{
    static readonly string[] Scenes = { "Assets/Scenes/Level_1.unity", "Assets/Scenes/WhiteSpace.unity", "Assets/Scenes/Confrontation.unity" };
    const float SatLimit = 0.5f;     // saturation above this is toned down (wood and cardboard stay)
    const float SatKeep = 0.55f;     // ...to this share of it
    const float Darken = 0.85f;      // brightness for a fully saturated colour (less for milder ones)

    [MenuItem("Tools/Art/Build Tone Harmony")]
    public static void Build()
    {
        var found = new Dictionary<Material, Color>();
        foreach (var path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || found.ContainsKey(m)) continue;
                    string asset = AssetDatabase.GetAssetPath(m);
                    if (!asset.StartsWith("Assets/Models/") || asset.Contains("KeyPad") || asset.Contains("/Key/") || asset.Contains("/WhiteSpace/")) continue;
                    if (!m.shader.name.StartsWith("Universal Render Pipeline/Lit")) continue;
                    if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) continue;
                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    // the warehouse shelving's pale blue frame reads as plastic toy blue under the lamps: steel
                    if (m.name == "Blue" && asset.Contains("WarehouseShelving")) { found[m] = new Color(0.3f, 0.33f, 0.38f, c.a); continue; }
                    if (s <= SatLimit) continue;
                    Color t = Color.HSVToRGB(h, Mathf.Max(SatLimit * 0.8f, s * SatKeep), v * Mathf.Lerp(1f, Darken, (s - SatLimit) / (1f - SatLimit)));
                    t.a = c.a;
                    found[m] = t;
                }
        }
        var data = ScriptableObject.CreateInstance<ToneHarmony>();
        foreach (var kv in found.OrderBy(k => AssetDatabase.GetAssetPath(k.Key)))
        {
            data.entries.Add(new ToneHarmony.Entry { original = kv.Key, color = kv.Value });
            Debug.Log($"[Tone] {AssetDatabase.GetAssetPath(kv.Key)} :: {kv.Key.name} -> {kv.Value}");
        }
        Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.CreateAsset(data, "Assets/Resources/ToneHarmony.asset");
        AssetDatabase.SaveAssets();
        Debug.Log($"[Tone] {data.entries.Count} materials toned down");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
    }
}
