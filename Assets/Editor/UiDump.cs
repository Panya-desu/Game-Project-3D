using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Dev helper: logs the hierarchy of the phone, dialogue and quest UI in Level_1.</summary>
public static class UiDump
{
    [MenuItem("Tools/Dev/Dump Level_1 UI")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        var sb = new StringBuilder();
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.isRootCanvas) Walk(c.transform, 0, sb);
        Debug.Log("[UiDump]\n" + sb);
    }

    static void Walk(Transform t, int depth, StringBuilder sb)
    {
        var rt = t as RectTransform;
        sb.Append(new string(' ', depth * 2)).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)");
        if (rt != null) sb.Append($" size={rt.rect.width:F0}x{rt.rect.height:F0} pos={rt.anchoredPosition}");
        var img = t.GetComponent<Image>();
        if (img != null) sb.Append($" IMG sprite={(img.sprite ? img.sprite.name : "-")} col={ColorUtility.ToHtmlStringRGBA(img.color)}");
        var raw = t.GetComponent<RawImage>();
        if (raw != null) sb.Append($" RAW tex={(raw.texture ? raw.texture.name : "-")}");
        var tmp = t.GetComponent<TMPro.TMP_Text>();
        if (tmp != null) sb.Append($" TMP size={tmp.fontSize} col={ColorUtility.ToHtmlStringRGBA(tmp.color)} '{tmp.text.Replace("\n", " ").Substring(0, Mathf.Min(30, tmp.text.Length))}'");
        foreach (var m in t.GetComponents<MonoBehaviour>())
            if (m != null && !(m is Graphic) && !(m is CanvasScaler) && !(m is GraphicRaycaster) && !(m is LayoutElement)) sb.Append(" [").Append(m.GetType().Name).Append("]");
        sb.AppendLine();
        if (depth > 7) return;
        foreach (Transform ch in t) Walk(ch, depth + 1, sb);
    }
}
