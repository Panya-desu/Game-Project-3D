using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renders the screenshots used in the project report (Docs/Report/images/game/*.jpg):
/// title screen, White Space, each room's lighting, the storage room with and without power,
/// Panya's puzzles (fuse box, levers, ending door), the ending, and a top-down level map.
/// Needs a GPU: run from the menu (Tools/Dev/Capture Report Images) or batch mode WITHOUT -nographics.
/// Scenes are only modified in memory; nothing is saved.
/// </summary>
public static class ReportCapture
{
    const int W = 1600, H = 900;
    static string outDir;

    [MenuItem("Tools/Dev/Capture Report Images")]
    public static void CaptureAll()
    {
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Report/images/game"));
        Directory.CreateDirectory(outDir);
        bool asyncBefore = EditorSettings.asyncShaderCompilation;
        EditorSettings.asyncShaderCompilation = false;
        try { CaptureScenes(); }
        finally { EditorSettings.asyncShaderCompilation = asyncBefore; }
    }

    static void CaptureScenes()
    {
        // ── Title ──
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        var mainCam = Camera.main;
        for (int i = 0; i < 2; i++) RenderCamera(mainCam, "_warmup", false);
        RenderCamera(mainCam, "title_world", false);
        RenderCamera(mainCam, "title_screen", true);
        var gate = GameObject.Find("__Environment/World/GateOfMemories");
        if (gate != null)
        {
            var p = gate.transform.Find("LeafPivotL"); if (p != null) p.localRotation *= Quaternion.Euler(0, -70, 0);
            p = gate.transform.Find("LeafPivotR"); if (p != null) p.localRotation *= Quaternion.Euler(0, 70, 0);
            Shot("title_gate_open", new Vector3(0.8f, 2.4f, -7.5f), gate.transform.position + new Vector3(0, 3.6f, 0), 55f);
        }

        // ── White Space ──
        EditorSceneManager.OpenScene("Assets/Scenes/WhiteSpace.unity", OpenSceneMode.Single);
        Shot("whitespace", new Vector3(0f, 1.7f, 0.5f), new Vector3(0f, 2.3f, 14f), 60f);
        Shot("whitespace_wide", new Vector3(-9f, 3.5f, -6f), new Vector3(0f, 2.5f, 12f), 60f);

        // ── Level_1 ──
        EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        for (int i = 0; i < 2; i++) Shot("_warmup", new Vector3(15f, 3f, 13f), new Vector3(15f, 1f, 20f));
        Shot("bedroom", new Vector3(6.3f, 3.0f, 9.8f), new Vector3(-3.5f, 1.2f, -0.5f));
        Shot("bedroom_clock", new Vector3(3.4f, 2.5f, 0.6f), new Vector3(3.4f, 1.8f, -2.6f), 50f);
        PauseMenuShot("pause_menu", new Vector3(6.3f, 3.0f, 9.8f), new Vector3(-3.5f, 1.2f, -0.5f));
        Shot("kitchen", new Vector3(10.5f, 3.0f, 2.5f), new Vector3(20f, 1.4f, 7.5f));
        Shot("living_tv", new Vector3(17.5f, 3.0f, 14.5f), new Vector3(9.5f, 1.6f, 22.5f));
        Shot("cabinet", new Vector3(13.6f, 3.3f, 21.5f), new Vector3(13.6f, 3.9f, 25.4f), 50f);
        Shot("storage", new Vector3(24f, 3.2f, 23.5f), new Vector3(33f, 1.2f, 6f));
        Shot("storeroom", new Vector3(34.5f, 3.0f, 26.6f), new Vector3(25f, 1.4f, 38f));
        Shot("library", new Vector3(40.5f, 3.0f, 22.5f), new Vector3(48f, 1.4f, 13f));
        Shot("levers", new Vector3(26.5f, 2.8f, 15.3f), new Vector3(22.9f, 2.9f, 15.3f), 50f);
        var ending = GameObject.Find("ToEnding");
        if (ending != null) foreach (Transform t in ending.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
        Shot("ending_door", new Vector3(30.2f, 2.4f, 15f), new Vector3(30.2f, 2.2f, 8f), 55f);

        // storage without power: dim lights + red emergency lights (the state at the start)
        var power = Object.FindAnyObjectByType<StoragePower>();
        if (power != null)
        {
            foreach (var l in power.roomLights) if (l != null) l.intensity *= power.unpoweredFactor;
            foreach (var l in power.emergencyLights) if (l != null) l.enabled = true;
            if (power.blackoutVolume != null) power.blackoutVolume.weight = 1f;
            LightGlow.RefreshAll();
            Shot("storage_unpowered", new Vector3(24f, 3.2f, 23.5f), new Vector3(33f, 1.2f, 6f));
            Shot("fusebox", new Vector3(27.2f, 2.6f, 8.6f), new Vector3(23.3f, 3.2f, 8.5f), 55f);
        }

        // top-down map
        EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == "Roof"))
            t.gameObject.SetActive(false);
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.bounds.min.y > 4.2f))
            r.enabled = false;
        var mapLight = new GameObject("MapLight").AddComponent<Light>();
        mapLight.type = LightType.Directional; mapLight.intensity = 0.55f;
        mapLight.transform.rotation = Quaternion.Euler(90, 0, 0); mapLight.shadows = LightShadows.None;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.32f);
        TopDown("map_level1", new Vector3(22.7f, 30f, 18.6f), 23.5f, 2000, 1500);

        // ── Confrontation ──
        EditorSceneManager.OpenScene("Assets/Scenes/Confrontation.unity", OpenSceneMode.Single);
        var dbl = GameObject.Find("__Generated/Double");
        if (dbl != null) { dbl.SetActive(true); dbl.transform.position = new Vector3(0f, 1.1f, 6f); dbl.transform.rotation = Quaternion.LookRotation(Vector3.back); }
        Shot("confrontation", new Vector3(0f, 2.4f, 0.3f), new Vector3(0f, 1.9f, 6f));

        // ── End screen ──
        EditorSceneManager.OpenScene("Assets/Scenes/DemoEnd.unity", OpenSceneMode.Single);
        RenderCamera(Object.FindAnyObjectByType<Camera>(), "demo_end", true);

        File.Delete(Path.Combine(outDir, "_warmup.jpg"));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("[ReportCapture] saved to " + outDir);
    }

    /// <summary>
    /// Logs where every puzzle object of Level_1 sits, as world position and as 0..1 coordinates on
    /// map_level1.jpg, so the report's map markers can be placed (see TopDown("map_level1", ...)).
    /// </summary>
    /// <summary>Walk from the living room into the unpowered storage room: the grading should fade in gently.</summary>
    [MenuItem("Tools/Dev/Capture Storage Entry")]
    public static void CaptureStorageEntry()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        string keep = outDir;
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../_work/lab"));
        Directory.CreateDirectory(outDir);
        var power = Object.FindAnyObjectByType<StoragePower>();
        float[] xs = { 17f, 19.5f, 21.5f, 23f, 24.5f, 26.5f, 29f };
        for (int i = 0; i < xs.Length; i++)
        {
            var pos = new Vector3(xs[i], 1.9f, 22.2f);
            if (power != null && power.blackoutVolume != null) power.blackoutVolume.weight = StorageZone.Weight(pos);
            Debug.Log($"[ReportCapture] storage entry x={xs[i]} weight={StorageZone.Weight(pos):F2}");
            Shot($"storage_entry_{i}", pos, pos + new Vector3(4f, -0.4f, 0f), 70f);
        }
        outDir = keep;
    }

    [MenuItem("Tools/Dev/Dump Puzzle Layout")]
    public static void DumpPuzzleLayout()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        var sb = new System.Text.StringBuilder();
        void Log(string what, Component c)
        {
            if (c == null) return;
            Vector3 p = c.transform.position;
            float u = (p.x - 22.7f + 23.5f * 4f / 3f) / (2f * 23.5f * 4f / 3f);
            float v = (p.z - 18.6f + 23.5f) / (2f * 23.5f);
            sb.AppendLine($"{what,-28} {Path(c.transform),-70} pos=({p.x:F1},{p.y:F1},{p.z:F1}) map=({u:F3},{v:F3})");
        }
        string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        T Get<T>(Component c, string field) where T : class =>
            c.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(c) as T;

        Log("player", Object.FindAnyObjectByType<Movement>());
        foreach (var c in Object.FindObjectsByType<PhoneInteractable>(FindObjectsSortMode.None)) Log("phone", c);
        foreach (var c in Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string type = c.GetType().Name;
            Log(type, c);
            switch (type)
            {
                case "RandomTimeCodeKeypad": Log("  clock text", Get<Component>(c, "hintTimeText")); break;
                case "RandomDigitHintKeypad":
                case "CombinedCodeKeypad":
                    foreach (var d in Get<Component[]>(c, "hintDigits") ?? new Component[0]) Log("  digit", d);
                    break;
                case "RabbitCipherKeypad":
                    Log("  paper A", Get<Component>(c, "evenLettersText"));
                    Log("  paper B", Get<Component>(c, "oddLettersText"));
                    Log("  riddle", Get<Component>(c, "riddleText"));
                    break;
                case "RandomCodeKeypadFormPile":
                    foreach (var d in Get<Component[]>(c, "pileHints") ?? new Component[0]) Log("  pile", d);
                    break;
            }
            var door = Get<Component>(c, "interactiveDoor") ?? Get<Component>(c, "targetDoor");
            if (door != null) Log("  opens", door);
        }
        foreach (var c in Object.FindObjectsByType<RandomLetterCodeDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Log("letter chart", c);
        foreach (var c in Object.FindObjectsByType<LeverPuzzle>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Log("lever puzzle", c);
        foreach (var c in Object.FindObjectsByType<AreaTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Log("area", c);
        foreach (var c in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.canvas != null && c.canvas.renderMode != RenderMode.WorldSpace) continue; // screen UI
            string text = c.text.Replace("\n", " / ");
            Log("text '" + (text.Length > 40 ? text.Substring(0, 40) : text) + "'", c);
        }
        Debug.Log("[PuzzleLayout]\n" + sb);
    }

    static Camera MakeCam(float fov)
    {
        var cam = new GameObject("CaptureCam").AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
        cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;
        cam.allowHDR = true;
        var data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        return cam;
    }

    static void Save(Camera cam, string name, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, name + ".jpg"), tex.EncodeToJPG(88));
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    static void Shot(string name, Vector3 pos, Vector3 lookAt, float fov = 65f)
    {
        Physics.SyncTransforms(); // local volumes find the camera through their colliders
        foreach (var z in Object.FindObjectsByType<StorageZone>(FindObjectsSortMode.None))
        {
            var v = z.GetComponent<UnityEngine.Rendering.Volume>();
            if (v != null) v.weight = StorageZone.Weight(pos); // the zone script only runs in play mode
        }
        var player = Object.FindAnyObjectByType<Movement>();
        bool had = player != null && player.gameObject.activeSelf;
        if (player != null) player.gameObject.SetActive(false);
        var cam = MakeCam(fov);
        cam.transform.position = pos;
        cam.transform.LookAt(lookAt);
        Save(cam, name, W, H);
        var ca = VolumeManager.instance.stack.GetComponent<ColorAdjustments>();
        if (ca != null) Debug.Log($"[ReportCapture] {name}: postExposure {ca.postExposure.value:F2}, saturation {ca.saturation.value:F0}");
        Object.DestroyImmediate(cam.gameObject);
        if (player != null) player.gameObject.SetActive(had);
    }

    /// <summary>Builds the runtime pause menu (normally created in play mode) and renders it over a view.</summary>
    static void PauseMenuShot(string name, Vector3 pos, Vector3 lookAt)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var pm = new GameObject("PauseCapture").AddComponent<PauseMenu>();
        typeof(PauseMenu).GetMethod("Build", flags).Invoke(pm, null);
        ((GameObject)typeof(PauseMenu).GetField("panel", flags).GetValue(pm)).SetActive(true);
        var canvas = pm.GetComponentInChildren<Canvas>();
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c != canvas) c.enabled = false;

        var player = Object.FindAnyObjectByType<Movement>();
        if (player != null) player.gameObject.SetActive(false);
        var cam = MakeCam(65f);
        cam.transform.position = pos;
        cam.transform.LookAt(lookAt);
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 0.5f;
        Canvas.ForceUpdateCanvases();
        Save(cam, name, W, H);
        Object.DestroyImmediate(cam.gameObject);
        Object.DestroyImmediate(pm.gameObject);
        if (player != null) player.gameObject.SetActive(true);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.enabled = true;
    }

    static void TopDown(string name, Vector3 pos, float halfHeight, int w, int h)
    {
        var player = Object.FindAnyObjectByType<Movement>();
        if (player != null) player.gameObject.SetActive(false);
        var cam = MakeCam(60f);
        cam.orthographic = true; cam.orthographicSize = halfHeight;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.white;
        cam.transform.position = pos; cam.transform.rotation = Quaternion.Euler(90, 0, 0);
        cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        Save(cam, name, w, h);
        Object.DestroyImmediate(cam.gameObject);
    }

    /// <summary>Render a scene camera; with UI, overlay canvases are switched to camera space so they show up.</summary>
    static void RenderCamera(Camera cam, string name, bool withUI)
    {
        if (cam == null) return;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas) continue;
            if (withUI) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; }
            else c.enabled = false;
        }
        Canvas.ForceUpdateCanvases();
        Save(cam, name, W, H);
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.enabled = true;
    }
}
