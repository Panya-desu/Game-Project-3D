using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dev: logs every frame slower than 50 ms with what was going on (scene, cutscene, the last sounds played,
/// a garbage collection in that frame), to find where the game stutters. On with -hitchlog or -selftest,
/// or from the smoke test (Enable).
/// </summary>
public class HitchWatch : MonoBehaviour
{
    const float Limit = 0.05f;
    static HitchWatch instance;
    int lastGc;
    public static string LastEvent = "";
    public static int Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Auto()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(args, "-hitchlog") >= 0 || System.Array.IndexOf(args, "-selftest") >= 0) Enable();
    }

    public static void Enable()
    {
        if (instance != null) return;
        var go = new GameObject("[HitchWatch]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<HitchWatch>();
        SceneManager.sceneLoaded += (s, m) => LastEvent = "scene " + s.name;
    }

    /// <summary>Notes something that happened this frame (the last few are reported with a hitch).</summary>
    public static void Note(string what) { if (instance != null) LastEvent = what; }

    readonly FrameTiming[] timing = new FrameTiming[1];

    void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        float dt = Time.unscaledDeltaTime;
        int gc = System.GC.CollectionCount(0);
        if (dt > Limit && Time.frameCount > 5)
        {
            Count++;
            string split = FrameTimingManager.GetLatestTimings(1, timing) > 0
                ? $" cpuMain={timing[0].cpuMainThreadFrameTime:F0} cpuRender={timing[0].cpuRenderThreadFrameTime:F0} gpu={timing[0].gpuFrameTime:F0}"
                : "";
            Debug.Log($"[Hitch] {dt * 1000f:F0} ms at t={Time.realtimeSinceStartup:F1} scene={SceneManager.GetActiveScene().name} cutscene={CutscenePlayer.IsPlaying} black={ScreenFader.Opaque} gc={(gc != lastGc ? "yes" : "no")} last={LastEvent}{split}");
        }
        lastGc = gc;
    }
}
