using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Quick check of a real build (the editor smoke test cannot run there). Only active when the game is
/// started with -selftest: title -> new game -> White Space -> Level_1 -> Confrontation, logging the frame
/// rate of each scene, any errors, and a screenshot of each into the build folder, then quits.
/// </summary>
public class BuildSelfTest : MonoBehaviour
{
    int errors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeStart()
    {
        if (!System.Environment.GetCommandLineArgs().Contains("-selftest")) return;
        var go = new GameObject("[BuildSelfTest]");
        DontDestroyOnLoad(go);
        go.AddComponent<BuildSelfTest>();
    }

    void Awake() => Application.logMessageReceived += (msg, stack, type) =>
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; Debug.Log("[SelfTest] ERROR " + msg); }
    };

    IEnumerator Start()
    {
        SceneManager.activeSceneChanged += (a, b) => Debug.Log($"[SelfTest] t={Time.realtimeSinceStartup:F1} active scene -> {b.name}");
        Application.wantsToQuit += () => { Debug.Log("[SelfTest] wantsToQuit from: " + System.Environment.StackTrace); return true; };
        yield return new WaitForSeconds(4f);
        yield return Sample("title");
        var title = FindAnyObjectByType<TitleScreen>();
        Debug.Log($"[SelfTest] t={Time.realtimeSinceStartup:F1} title found={title != null}, entering a new game");
        if (title != null) title.Enter(true);
        yield return WaitScene("WhiteSpace", 20f);
        yield return new WaitForSeconds(10f);
        yield return Sample("whitespace (opening cutscene / navigator)");
        while (DialogueManager.IsAnyTalking) { var d = FindAnyObjectByType<DialogueManager>(); if (d != null) d.SendMessage("NextLine", SendMessageOptions.DontRequireReceiver); yield return new WaitForSeconds(0.5f); }
        var door = FindObjectsByType<Door>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Door1");
        if (door != null) door.Interact();
        yield return WaitScene("Level_1", 20f);
        yield return Sample("level_1 (waking cutscene)");
        yield return new WaitForSeconds(8f);
        yield return Sample("level_1");
        // her body (shadow and hair springs) on, off and on again, in the same view: what the character costs
        var body = FindAnyObjectByType<Movement>()?.transform.Find("PlayerBody");
        if (body != null)
        {
            body.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.5f);
            yield return Sample("level_1, her body off");
            body.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            yield return Sample("level_1, her body on again");
        }
        yield return GlassViews();
        SceneManager.LoadScene("Confrontation");
        yield return WaitScene("Confrontation", 20f);
        yield return new WaitForSeconds(3f);
        yield return Sample("confrontation");
        Debug.Log($"[SelfTest] DONE errors={errors}");
        Application.Quit(errors == 0 ? 0 : 1);
    }

    // the glass wall between the living and dining rooms: frame times looking through it and away from it
    IEnumerator GlassViews()
    {
        var glass = FindObjectsByType<Renderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "WindowTallGlass4m");
        var mover = FindAnyObjectByType<Movement>();
        if (glass == null || mover == null) yield break;
        var cc = mover.GetComponent<CharacterController>();
        var look = mover.GetComponentInChildren<MouseLook>();
        Vector3 c = glass.bounds.center;
        foreach (float side in new[] { 1f, -1f })
            foreach (bool facing in new[] { true, false })
            {
                Vector3 feet = c + Vector3.forward * side * 3f;
                cc.enabled = false;
                mover.transform.position = new Vector3(feet.x, mover.transform.position.y, feet.z);
                mover.transform.rotation = Quaternion.LookRotation(Vector3.forward * (facing ? -side : side));
                cc.enabled = true;
                if (look != null) look.SetPitch(0f);
                yield return new WaitForSeconds(1f);
                yield return Sample($"glass side {side:F0} {(facing ? "facing" : "away")}");
                if (!facing) continue;
                // the same view without the pane: its own shading against what lies behind it
                glass.enabled = false;
                yield return new WaitForSeconds(0.5f);
                yield return Sample($"glass side {side:F0} facing, pane hidden");
                glass.enabled = true;
            }
    }

    IEnumerator WaitScene(string name, float timeout)
    {
        for (float t = 0; t < timeout && SceneManager.GetActiveScene().name != name; t += Time.unscaledDeltaTime) yield return null;
        Debug.Log($"[SelfTest] scene {SceneManager.GetActiveScene().name} (wanted {name})");
        yield return new WaitForSeconds(1f);
    }

    IEnumerator Sample(string label)
    {
        int frames = 0; float worst = 0f, start = Time.realtimeSinceStartup;
        var timing = new FrameTiming[1]; double cpu = 0, render = 0, gpu = 0; int timed = 0;
        while (Time.realtimeSinceStartup - start < 3f)
        {
            worst = Mathf.Max(worst, Time.unscaledDeltaTime); frames++;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0) { cpu += timing[0].cpuMainThreadFrameTime; render += timing[0].cpuRenderThreadFrameTime; gpu += timing[0].gpuFrameTime; timed++; }
            yield return null;
        }
        float avg = (Time.realtimeSinceStartup - start) / frames;
        timed = Mathf.Max(1, timed);
        Debug.Log($"[SelfTest] {label}: {1f / avg:F0} fps avg, worst frame {worst * 1000f:F0} ms, cpu main {cpu / timed:F1} ms, render thread {render / timed:F1} ms, gpu {gpu / timed:F1} ms");
        HitchWatch.Note("selftest screenshot");
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath, "..", "selftest_" + SceneManager.GetActiveScene().name + ".png"));
        yield return null;
    }
}
