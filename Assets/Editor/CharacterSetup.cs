using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates Resources/CharacterLibrary.asset pointing at the EchoCharacters prefabs, so the title screen,
/// the Level_1 cutscenes and the Confrontation can spawn the protagonist and The Double at runtime.
/// </summary>
public static class CharacterSetup
{
    const string LibraryPath = "Assets/Resources/CharacterLibrary.asset";

    [MenuItem("Tools/Game Setup/Characters")]
    public static void Build()
    {
        var lib = AssetDatabase.LoadAssetAtPath<CharacterLibrary>(LibraryPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<CharacterLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }
        lib.protagonist = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EchoCharacters/Prefabs/Protagonist.prefab");
        lib.theDouble = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EchoCharacters/Prefabs/TheDouble.prefab");
        lib.toonShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Characters/EchoToon.shader");
        // head/eye look-at needs the IK pass on the base layer (re-run after Tools > Echo Characters > Rebuild)
        foreach (var path in new[] { "Assets/EchoCharacters/Animations/Protagonist.controller", "Assets/EchoCharacters/Animations/TheDouble.controller" })
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            if (ctrl == null) continue;
            var layers = ctrl.layers;
            if (layers.Length > 0 && !layers[0].iKPass) { layers[0].iKPass = true; ctrl.layers = layers; EditorUtility.SetDirty(ctrl); }
        }
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CharacterSetup] library: protagonist={(lib.protagonist != null)} double={(lib.theDouble != null)}");
    }
}
