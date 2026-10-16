using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One colour palette for the whole house. Several bought/downloaded models came with flat, fully
/// saturated "toy" colours (bright blue warehouse shelving, orange beams, pink and purple mini shelves,
/// cyan paint buckets, a bright blue clock, rainbow books) that looked like another game next to the
/// muted, warm rooms. Their materials are listed with toned-down colours in Resources/ToneHarmony
/// (made by Tools/Art/Build Tone Harmony from the scenes, see ToneHarmonyBuilder). On every scene load
/// each listed material is swapped for a copy in its new colour; the model files stay untouched.
/// Puzzle hint colours (Assets/Materials/Color), keypads and the key are never listed.
/// </summary>
[CreateAssetMenu(menuName = "Echo/Tone Harmony")]
public class ToneHarmony : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public Material original;
        public Color color = Color.white;
    }

    public List<Entry> entries = new List<Entry>();

    static Dictionary<Material, Material> swaps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    static void Apply()
    {
        if (swaps == null)
        {
            swaps = new Dictionary<Material, Material>();
            var data = Resources.Load<ToneHarmony>("ToneHarmony");
            if (data != null)
                foreach (var e in data.entries)
                {
                    if (e.original == null || swaps.ContainsKey(e.original)) continue;
                    var m = new Material(e.original) { name = e.original.name + " (tone)" };
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", e.color);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", e.color);
                    swaps[e.original] = m;
                }
        }
        if (swaps.Count == 0) return;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && swaps.TryGetValue(mats[i], out var m)) { mats[i] = m; changed = true; }
            if (changed) r.sharedMaterials = mats;
        }
    }
}
