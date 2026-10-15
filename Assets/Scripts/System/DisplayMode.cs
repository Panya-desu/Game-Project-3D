using UnityEngine;

/// <summary>
/// Full screen by default: the game starts in borderless full screen at the monitor's resolution (Unity
/// otherwise remembers the last window mode, e.g. a windowed test run). F11 or Alt+Enter switches between
/// full screen and a window, and the choice is remembered. Start with -windowed to force a window.
/// </summary>
public class DisplayMode : MonoBehaviour
{
    const string Key = "FullScreen";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (Application.isEditor) return;
        bool windowed = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-windowed") >= 0;
        if (!windowed) Apply(PlayerPrefs.GetInt(Key, 1) == 1); // -windowed keeps the size given with -screen-width/-height
        var go = new GameObject("[DisplayMode]");
        DontDestroyOnLoad(go);
        go.AddComponent<DisplayMode>();
    }

    static void Apply(bool full)
    {
        if (full)
        {
            var r = Screen.currentResolution;
            Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            int w = Mathf.Min(1600, Screen.currentResolution.width - 100), h = w * 9 / 16;
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
        }
    }

    void Update()
    {
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (Input.GetKeyDown(KeyCode.F11) || (alt && Input.GetKeyDown(KeyCode.Return)))
        {
            bool full = Screen.fullScreenMode == FullScreenMode.Windowed;
            PlayerPrefs.SetInt(Key, full ? 1 : 0);
            PlayerPrefs.Save();
            Apply(full);
        }
    }
}
