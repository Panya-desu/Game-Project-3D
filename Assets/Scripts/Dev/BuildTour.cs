using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dev: a look round the real build through the player's own eyes (started with -tour). Title -> White
/// Space -> every room of the house (the storage wing dark, then powered, then the door that appears) ->
/// the confrontation -> the end screen. At each stop the player stands still for a moment, a screenshot is
/// saved into the build folder (tour/), and the frame rate, the worst frame and the CPU/GPU times are
/// logged; HitchWatch logs every frame over 50 ms on the way. Then it quits.
/// </summary>
public class BuildTour : MonoBehaviour
{
    string dir;
    int shots;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeStart()
    {
        if (!System.Environment.GetCommandLineArgs().Contains("-tour")) return;
        var go = new GameObject("[BuildTour]");
        DontDestroyOnLoad(go);
        go.AddComponent<BuildTour>();
    }

    IEnumerator Start()
    {
        HitchWatch.Enable();
        Application.logMessageReceived += (msg, stack, type) => { if (type == LogType.Error || type == LogType.Exception) Debug.Log("[Tour] ERROR " + msg); };
        dir = System.IO.Path.Combine(Application.dataPath, "..", "tour");
        System.IO.Directory.CreateDirectory(dir);
        yield return new WaitForSeconds(4f);
        yield return Stop("title");
        var title = FindAnyObjectByType<TitleScreen>();
        if (title != null) title.Enter(true);
        yield return Scene("WhiteSpace");
        yield return Settle(60f);
        yield return Stop("white_start");
        yield return View("white_gates", new Vector3(0f, 2.8f, 7.5f), new Vector3(0f, 2.2f, 14f));
        yield return WalkHere("white_gates");
        yield return View("white_side", new Vector3(7f, 2.8f, 5f), new Vector3(0f, 1.8f, 14f));
        yield return View("white_back", new Vector3(0f, 2.8f, 4f), new Vector3(0f, 1.2f, -14f));
        var door = FindObjectsByType<Door>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Door1");
        if (door != null) door.Interact();
        yield return Scene("Level_1");
        yield return Settle(60f);
        yield return Stop("house_wake");
        yield return View("bedroom", new Vector3(6.3f, 3.0f, 9.8f), new Vector3(-3.5f, 1.2f, -0.5f));
        yield return WalkHere("bedroom");
        yield return View("bedroom_desk", new Vector3(1.2f, 2.7f, 6.5f), new Vector3(3.6f, 1.6f, -2.4f));
        yield return View("kitchen", new Vector3(10.5f, 3.0f, 2.5f), new Vector3(20f, 1.4f, 7.5f));
        yield return WalkHere("kitchen");
        yield return View("living_room", new Vector3(17.5f, 3.0f, 14.5f), new Vector3(9.5f, 1.6f, 22.5f));
        yield return WalkHere("living_room");
        yield return Split();
        yield return View("glass_from_dining", new Vector3(13.3f, 3.0f, 12.8f), new Vector3(13.3f, 2.4f, 25f));
        yield return View("glass_from_living", new Vector3(13.3f, 3.0f, 18.8f), new Vector3(13.3f, 2.4f, 5f));
        yield return View("storage_doorway_dark", new Vector3(21.2f, 2.8f, 22.6f), new Vector3(30f, 1.4f, 17f));
        yield return WalkHere("storage_doorway_dark");
        yield return View("fuse_room_dark", new Vector3(36.6f, 3.3f, 24.6f), new Vector3(25f, 1.2f, 6f));
        yield return View("fusebox_dark", new Vector3(27.2f, 2.6f, 8.6f), new Vector3(23.3f, 3.2f, 8.5f));
        yield return View("storeroom_dark", new Vector3(34.5f, 3.0f, 26.6f), new Vector3(25f, 1.4f, 38f));
        yield return View("library_dark", new Vector3(40.5f, 3.0f, 22.5f), new Vector3(48f, 1.4f, 13f));
        // the puzzle as the player solves it: the fuses and the key, then the power comes back
        foreach (var f in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)) { f.Interact(); yield return new WaitForSeconds(0.2f); }
        var key = FindAnyObjectByType<KeyPickup>(); if (key != null) key.Interact();
        yield return null;
        foreach (var k in FindObjectsByType<KeyDoorInteractable>(FindObjectsSortMode.None)) { if (k.canInteract) k.Interact(); yield return new WaitForSeconds(0.2f); }
        yield return View("fusebox_before", new Vector3(27.2f, 2.6f, 8.6f), new Vector3(23.3f, 3.2f, 8.5f));
        foreach (var p in FindObjectsByType<PlaceItemInteractable>(FindObjectsSortMode.None)) { p.Interact(); yield return new WaitForSeconds(0.2f); }
        yield return Cutscene("power_cutscene");
        yield return View("fuse_room_powered", new Vector3(36.6f, 3.3f, 24.6f), new Vector3(25f, 1.2f, 6f));
        yield return WalkHere("fuse_room_powered");
        yield return View("storeroom_powered", new Vector3(34.5f, 3.0f, 26.6f), new Vector3(25f, 1.4f, 38f));
        yield return View("library_powered", new Vector3(40.5f, 3.0f, 22.5f), new Vector3(48f, 1.4f, 13f));
        yield return WalkHere("library_powered");
        yield return View("levers", new Vector3(26.5f, 2.8f, 15.3f), new Vector3(22.9f, 2.9f, 15.3f));
        foreach (var l in FindObjectsByType<Lever>(FindObjectsSortMode.None).OrderBy(l => l.symbolIndex)) { l.Interact(); yield return new WaitForSeconds(0.4f); }
        yield return Cutscene("door_cutscene");
        yield return View("ending_door", new Vector3(30.2f, 2.9f, 14f), new Vector3(30.2f, 2.2f, 8f));
        SceneManager.LoadScene("Confrontation");
        yield return Scene("Confrontation");
        yield return new WaitForSeconds(3f);
        yield return Stop("confrontation");
        yield return WalkHere("confrontation");
        yield return Turn("confrontation_back", 180f);
        SceneManager.LoadScene("DemoEnd");
        yield return Scene("DemoEnd");
        yield return new WaitForSeconds(3f);
        yield return Stop("end_screen");
        Debug.Log($"[Tour] DONE hitches over 50 ms: {HitchWatch.Count}");
        Application.Quit(0);
    }

    IEnumerator Scene(string name)
    {
        for (float t = 0; t < 30f && SceneManager.GetActiveScene().name != name; t += Time.unscaledDeltaTime) yield return null;
        Debug.Log($"[Tour] scene {SceneManager.GetActiveScene().name}");
        yield return new WaitForSeconds(1f);
    }

    // until the scene's cutscene and its talk are over (the lines are clicked through)
    IEnumerator Settle(float timeout)
    {
        for (float t = 0; t < timeout; t += 0.25f)
        {
            if (DialogueManager.IsAnyTalking) { var d = FindAnyObjectByType<DialogueManager>(); if (d != null) d.SendMessage("NextLine", SendMessageOptions.DontRequireReceiver); }
            else if (!CutscenePlayer.IsPlaying && !GameState.IsCutscene && t > 2f) break;
            yield return new WaitForSeconds(0.25f);
        }
        yield return new WaitForSeconds(1f);
    }

    IEnumerator Cutscene(string label)
    {
        for (float t = 0; t < 5f && !CutscenePlayer.IsPlaying; t += Time.unscaledDeltaTime) yield return null;
        yield return Measure(label + " (playing)", false);
        yield return Settle(30f);
    }

    // the player stands at an eye position looking at a point (the player's own camera and post effects)
    IEnumerator View(string label, Vector3 eye, Vector3 look)
    {
        var mover = FindAnyObjectByType<Movement>();
        if (mover != null)
        {
            var cc = mover.GetComponent<CharacterController>();
            var cam = mover.GetComponentInChildren<Camera>();
            float eyeAbove = cam != null ? cam.transform.position.y - mover.transform.position.y : 1.6f;
            float floor = CharacterLibrary.GroundBelow(eye + Vector3.up * 0.5f).y;
            float bodyAbove = mover.transform.position.y - CharacterLibrary.GroundBelow(mover.transform.position).y;
            if (cc != null) cc.enabled = false;
            mover.transform.position = new Vector3(eye.x, floor + bodyAbove, eye.z);
            Vector3 d = look - new Vector3(eye.x, floor + bodyAbove + eyeAbove, eye.z);
            mover.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(d, Vector3.up).normalized);
            if (cc != null) cc.enabled = true;
            var ml = mover.GetComponentInChildren<MouseLook>();
            if (ml != null) ml.SetPitch(-Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg);
        }
        yield return new WaitForSeconds(1.5f); // zone gradings and lights ease in
        yield return Stop(label);
    }

    // where the house's GPU time goes, in the living room view: the lamps' shadows, the post effects, the
    // soft shadow filtering and the resolution, each switched off (or down) on its own and back
    IEnumerator Split()
    {
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        var cam = Camera.main;
        var camData = cam != null ? UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam) : null;
        var lamps = FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type != LightType.Directional && l.shadows != LightShadows.None).ToArray();
        Debug.Log($"[Tour] split: {lamps.Length} lamps drawing shadows now; render scale {urp?.renderScale}; soft shadows {urp?.supportsSoftShadows}");
        yield return Measure("split_baseline", false);
        foreach (var l in lamps) l.shadows = LightShadows.None;
        yield return new WaitForSeconds(0.5f);
        yield return Measure("split_no_lamp_shadows", false);
        foreach (var l in lamps) l.shadows = LightShadows.Hard;
        yield return new WaitForSeconds(0.5f);
        yield return Measure("split_hard_lamp_shadows", false);
        foreach (var l in lamps) l.shadows = LightShadows.Soft;
        if (camData != null) { camData.renderPostProcessing = false; yield return new WaitForSeconds(0.5f); yield return Measure("split_no_post", false); camData.renderPostProcessing = true; }
        if (urp != null) { float rs = urp.renderScale; urp.renderScale = 0.75f; yield return new WaitForSeconds(0.5f); yield return Measure("split_render_scale_0.75", false); urp.renderScale = rs; }
        yield return new WaitForSeconds(0.5f);
        yield return Measure("split_baseline_again", false);
    }

    // the player walks from the view's spot, swinging the view left and right, then lets go and stands:
    // every frame over 25 ms (where a stutter starts to show) is counted
    IEnumerator WalkHere(string label)
    {
        var mover = FindAnyObjectByType<Movement>();
        if (mover == null) yield break;
        float yaw0 = mover.transform.eulerAngles.y, worst = 0f; int slow = 0, frames = 0;
        for (float t = 0f; t < 4.5f; t += Time.unscaledDeltaTime)
        {
            Movement.LabInput = t < 3f ? new Vector2(0f, 1f) : Vector2.zero;
            if (t < 3f) mover.transform.rotation = Quaternion.Euler(0f, yaw0 + 40f * Mathf.Sin(t * 1.6f), 0f);
            yield return null;
            float dt = Time.unscaledDeltaTime; frames++;
            if (dt > 0.025f) slow++;
            worst = Mathf.Max(worst, dt);
        }
        Movement.LabInput = null;
        Debug.Log($"[Tour] walk {label}: {frames} frames, {slow} over 25 ms, worst {worst * 1000f:F0} ms");
    }

    IEnumerator Turn(string label, float degrees)
    {
        var mover = FindAnyObjectByType<Movement>();
        if (mover != null) mover.transform.Rotate(Vector3.up * degrees);
        yield return new WaitForSeconds(1.5f);
        yield return Stop(label);
    }

    IEnumerator Stop(string label) => Measure(label, true);

    IEnumerator Measure(string label, bool shot)
    {
        int frames = 0; float worst = 0f, start = Time.realtimeSinceStartup;
        var timing = new FrameTiming[1]; double cpu = 0, gpu = 0; int timed = 0;
        while (Time.realtimeSinceStartup - start < 2.5f)
        {
            worst = Mathf.Max(worst, Time.unscaledDeltaTime); frames++;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0) { cpu += timing[0].cpuMainThreadFrameTime; gpu += timing[0].gpuFrameTime; timed++; }
            yield return null;
        }
        float avg = (Time.realtimeSinceStartup - start) / frames; timed = Mathf.Max(1, timed);
        string name = $"{shots++:00}_{label}";
        Debug.Log($"[Tour] {name}: {1f / avg:F0} fps, worst {worst * 1000f:F0} ms, cpu {cpu / timed:F1} ms, gpu {gpu / timed:F1} ms");
        if (shot)
        {
            HitchWatch.Note("tour screenshot");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, name + ".png"));
            yield return null; yield return null;
        }
    }
}
