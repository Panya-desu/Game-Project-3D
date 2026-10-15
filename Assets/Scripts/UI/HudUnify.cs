using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One HUD look in every scene. The White Space still had the original HUD: a canvas at constant pixel
/// size (small and misplaced on big screens), a plain-font quest text, and a grey controls strip that
/// ran across the dialogue box. On every scene load the main canvas scales like Level_1's (1920x1080,
/// matched on width), the quest text takes the game's font, and the controls strip is removed in all
/// scenes (the controls are listed in the pause menu).
/// </summary>
public static class HudUnify
{
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
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.name != "Canvas") continue;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0f;
            }
            var strip = canvas.transform.Find("control");
            if (strip != null) strip.gameObject.SetActive(false);
        }
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.text.StartsWith("WASD")) t.gameObject.SetActive(false);

        // the White Space quest text (TaskManager) in the same style as QuestManager's
        foreach (var task in Object.FindObjectsByType<TaskManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (task.taskText != null) UiTheme.Style(task.taskText, false, task.taskText.fontSize, UiTheme.Pearl, 0.65f);
    }
}
