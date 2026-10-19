#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Editor-only automated play-through (Tools/Dev/Run Smoke Test, or SmokeTest.Run in batch mode).
/// Plays from the title screen to the end screen by calling the same methods the player triggers,
/// walks the character through doorways, reads the random codes like a player reading the clues,
/// and fails on any error in the log. Not included in builds.
/// </summary>
public class SmokeTestDriver : MonoBehaviour
{
    int errors, checks, failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeStart()
    {
        if (!UnityEditor.SessionState.GetBool("SmokeTest", false)) return;
        UnityEditor.SessionState.SetBool("SmokeTest", false);
        var go = new GameObject("[SmokeTest]");
        DontDestroyOnLoad(go);
        go.AddComponent<SmokeTestDriver>();
    }

    void Awake() { Application.logMessageReceived += OnLog; }

    void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (msg.Contains("[Licensing") || msg.Contains("Unity VCS") || stack.Contains("UnityEditor.Search") || stack.Contains("UnityEditor.WindowLayout")) return; // the editor itself (saving its window layout on exit)
        errors++;
        Debug.Log("[Smoke] CAPTURED ERROR: " + msg + "\n" + stack);
    }

    void Check(bool ok, string what)
    {
        checks++;
        if (!ok) failures++;
        Debug.Log((ok ? "[Smoke] PASS " : "[Smoke] FAIL ") + what);
    }

    static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    static FieldInfo Field(Type t, string name)
    {
        for (; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) return f;
        }
        return null;
    }

    static T Find<T>() where T : UnityEngine.Object => FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

    IEnumerator Start()
    {
        var routine = Run();
        while (true)
        {
            bool more;
            try { more = routine.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); failures++; break; }
            if (!more) break;
            yield return routine.Current;
        }
        RestoreSave();
        Debug.Log($"[Smoke] RESULT checks={checks} failures={failures} errors={errors}");
        UnityEditor.EditorApplication.Exit(failures == 0 && errors == 0 ? 0 : 1);
    }

    /// <summary>Screenshot of the game view including overlay UI (for the report), saved as Docs/Report/images/game/name.jpg.</summary>
    void UiShot(string name)
    {
        var cam = Camera.main;
        if (cam == null) return;
        const int w = 1600, h = 900;
        const int uiLayer = 31;

        // 1) the 3D view with post-processing
        var scene = Grab(cam, w, h);

        // 2) the overlay UI alone, through a camera without post-processing (as in the game),
        //    once over black and once over white: B = a*C and W - B = 1 - a, so the UI can be
        //    blended over the scene exactly: result = B + (W - B) * scene
        var uiCam = new GameObject("UiShotCam").AddComponent<Camera>();
        uiCam.CopyFrom(cam);
        uiCam.clearFlags = CameraClearFlags.SolidColor;
        uiCam.cullingMask = 1 << uiLayer;
        var data = uiCam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        var changed = new List<Canvas>();
        var layers = new Dictionary<Transform, int>();
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = uiCam;
            c.planeDistance = uiCam.nearClipPlane + 0.05f;
            foreach (var t in c.GetComponentsInChildren<Transform>(true)) { layers[t] = t.gameObject.layer; t.gameObject.layer = uiLayer; }
            changed.Add(c);
        }
        Canvas.ForceUpdateCanvases();
        uiCam.backgroundColor = Color.black;
        var onBlack = Grab(uiCam, w, h);
        uiCam.backgroundColor = Color.white;
        var onWhite = Grab(uiCam, w, h);
        foreach (var c in changed) c.renderMode = RenderMode.ScreenSpaceOverlay;
        foreach (var kv in layers) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;
        Destroy(uiCam.gameObject);

        var outPx = new Color32[w * h];
        for (int i = 0; i < outPx.Length; i++)
        {
            Color b = onBlack[i], wh = onWhite[i], sc = scene[i];
            outPx[i] = (Color)new Color(
                Mathf.Clamp01(b.r + (wh.r - b.r) * sc.r),
                Mathf.Clamp01(b.g + (wh.g - b.g) * sc.g),
                Mathf.Clamp01(b.b + (wh.b - b.b) * sc.b), 1f);
        }
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.SetPixels32(outPx);
        tex.Apply();
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Docs/Report/images/game"));
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".jpg"), tex.EncodeToJPG(88));
        Destroy(tex);
    }

    static Color32[] Grab(Camera cam, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        var px = tex.GetPixels32();
        Destroy(tex);
        rt.Release();
        Destroy(rt);
        return px;
    }

    // The test saves the game, so the player's real save file is put aside and restored afterwards
    bool saveBackedUp, hadSave;
    string BackupPath => SaveSystem.SavePath + ".smoketest";

    void BackupSave()
    {
        if (saveBackedUp) return;
        hadSave = SaveSystem.HasSave;
        if (hadSave) System.IO.File.Copy(SaveSystem.SavePath, BackupPath, true);
        saveBackedUp = true;
    }

    void RestoreSave()
    {
        if (!saveBackedUp) return;
        if (hadSave) System.IO.File.Copy(BackupPath, SaveSystem.SavePath, true);
        else SaveSystem.DeleteSave();
        if (System.IO.File.Exists(BackupPath)) System.IO.File.Delete(BackupPath);
        saveBackedUp = false;
    }

    void SkipLine()
    {
        var dm = Find<DialogueManager>();
        if (dm != null && dm.IsTalking()) Call(dm, "NextLine");
    }

    IEnumerator WaitFor(Func<bool> cond, float seconds, bool skipLines = true)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!cond() && Time.realtimeSinceStartup < end) { if (skipLines) SkipLine(); yield return null; }
    }

    IEnumerator Settle(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) { SkipLine(); yield return null; }
    }

    string Quest => QuestManager.Instance != null ? QuestManager.Instance.CurrentQuestName : "";
    string Scene => SceneManager.GetActiveScene().name;

    /// <summary>Solve any of the keypad types: read its (random) code like a player who found the clues.</summary>
    IEnumerator Solve(Interactable keypad, string forceCode = null)
    {
        string code = forceCode ?? (string)Field(keypad.GetType(), "correctCode").GetValue(keypad);
        keypad.Interact();
        yield return null;
        Field(keypad.GetType(), "currentInput").SetValue(keypad, code);
        keypad.StartCoroutine((IEnumerator)Call(keypad, "CheckCode"));
        yield return new WaitForSeconds(2.6f);
    }

    static Interactable KeypadUnder(string ancestor) =>
        FindObjectsByType<Interactable>(FindObjectsSortMode.None).FirstOrDefault(i => Field(i.GetType(), "correctCode") != null && HasAncestor(i.transform, ancestor));

    static Interactable KeypadByDisplay(string displayName) =>
        FindObjectsByType<Interactable>(FindObjectsSortMode.None).FirstOrDefault(i =>
        {
            var f = Field(i.GetType(), "displayText");
            var d = f != null ? f.GetValue(i) as UnityEngine.Object : null;
            return d != null && d.name == displayName;
        });

    static bool HasAncestor(Transform t, string name)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.name == name) return true;
        return false;
    }

    static InteractiveDoor DoorUnder(string ancestor) =>
        FindObjectsByType<InteractiveDoor>(FindObjectsSortMode.None).FirstOrDefault(d => HasAncestor(d.transform, ancestor));

    static string PathOf(Transform t) => t.parent == null ? t.name + "#" + t.GetSiblingIndex() : PathOf(t.parent) + "/" + t.name + "#" + t.GetSiblingIndex();

    static Transform Hinge(InteractiveDoor door) =>
        ((DoorController)Field(typeof(InteractiveDoor), "doorController").GetValue(door)).Hinge;

    bool walkOk; Vector3 walkEnd; float walkPath; // walkPath: the distance she really covered (the path turns)
    IEnumerator Walk(params Vector3[] points)
    {
        var player = Find<Movement>();
        walkOk = true; walkPath = 0f; Vector3 prev = player.transform.position;
        foreach (var target in points)
        {
            float last = Time.time, best = float.MaxValue;
            while (true)
            {
                SkipLine();
                Vector3 d = target - player.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 0.5f) break;
                if (dist < best - 0.2f) { best = dist; last = Time.time; }
                if (Time.time - last > 2f) { Movement.LabInput = null; walkOk = false; walkEnd = player.transform.position; yield break; }
                // with the real controls (the movement keys held towards the target), as a player walks her
                Vector3 local = player.transform.InverseTransformDirection(d.normalized);
                Movement.LabInput = new Vector2(local.x, local.z);
                yield return null;
                walkPath += Vector3.ProjectOnPlane(player.transform.position - prev, Vector3.up).magnitude; prev = player.transform.position;
            }
        }
        Movement.LabInput = null;
        walkEnd = player.transform.position;
    }

    IEnumerator Run()
    {
        HitchWatch.Enable();
        var everyClip = new System.Collections.Generic.HashSet<string>();
        GameAudio.Played += n => everyClip.Add(n);
        // ── Title screen ──
        Check(Scene == "Main", "starts on the title screen");
        var title = Find<TitleScreen>();
        Check(title != null, "animated title screen exists");
        Check(GameObject.Find("Protagonist") != null && GameObject.Find("Heroine_Reflection") != null, "title screen shows the protagonist and her reflection on the water");
        yield return new WaitForSeconds(3f);
        UiShot("title_character");
        yield return new WaitForSeconds(2.5f);
        UiShot("title_menu"); // the menu buttons fully faded in
        Check(GameAudio.Instance != null && ScreenFader.Instance != null && GameHud.Instance != null, "runtime systems bootstrapped");
        yield return new WaitForSeconds(1.5f);
        Check(GameAudio.CurrentAmbient == "amb_title", "title theme playing");
        title.Enter(true); // "New Game" (does not touch an existing save file)
        yield return WaitFor(() => Scene == "WhiteSpace", 12f);
        Check(Scene == "WhiteSpace", "a new game starts in the White Space");

        // ── White space: opening cutscene, then the Navigator ──
        yield return new WaitForSeconds(3.2f);
        Check(CutscenePlayer.IsPlaying && !DialogueManager.IsAnyTalking,
            "opening cutscene plays before the Navigator speaks");
        UiShot("opening_sky");
        yield return new WaitForSeconds(6f);
        UiShot("opening_hero");
        yield return new WaitForSeconds(5.5f);
        UiShot("opening_gates");
        yield return WaitFor(() => !CutscenePlayer.IsPlaying, 20f, skipLines: false);
        yield return WaitFor(() => DialogueManager.IsAnyTalking, 5f, skipLines: false);
        Check(DialogueManager.IsAnyTalking && !GameState.IsCutscene, "after the opening the Navigator speaks and the player is back in control");
        yield return new WaitForSeconds(1.2f);
        UiShot("ui_dialogue_white");
        yield return new WaitForSeconds(1f);
        yield return WaitFor(() => !DialogueManager.IsAnyTalking, 10f);
        // the invisible fence: walking off across the water stops near the start
        {
            var wp = Find<Movement>();
            Vector3 start = wp.transform.position;
            yield return Walk(new Vector3(0f, 0f, -40f));
            Vector3 stop = wp.transform.position;
            Check(!walkOk && stop.z > -12f, $"White Space fence stops the player wandering off (stopped at z={stop.z:F1})");
            var wcc = wp.GetComponent<CharacterController>();
            wcc.enabled = false; wp.transform.position = start; wcc.enabled = true;
            yield return null;
        }
        var door = FindObjectsByType<Door>(FindObjectsSortMode.None).FirstOrDefault(d => d.name == "Door1");
        Check(door != null && GameObject.Find("__Environment/World/Gate_Door1") != null, "White Space has the gate for the first memory");
        foreach (var i in door.GetComponents<Interactable>()) if (i.canInteract) i.Interact();
        yield return WaitFor(() => Scene == "Level_1", 12f);
        Check(Scene == "Level_1", "gate leads to Level_1");
        yield return new WaitForSeconds(3.5f);
        UiShot("cutscene_wake");
        yield return new WaitForSeconds(3.2f);
        UiShot("cutscene_clock");
        yield return WaitFor(() => !CutscenePlayer.IsPlaying, 12f, skipLines: false);
        yield return new WaitForSeconds(0.8f);
        UiShot("ui_dialogue");

        // ── Bedroom ──
        yield return WaitFor(() => Quest == "A familiar room", 15f);
        Check(Quest == "A familiar room", "quest 1 starts after the intro dialogue");
        Check(CutscenePlayer.Played >= 1, "waking-up cutscene plays on the first visit");
        // every keypad must show its empty slots ("_ _ _ _") before the player types
        var blank = new System.Text.StringBuilder();
        foreach (var kp in FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var df = Field(kp.GetType(), "displayText");
            var cf = Field(kp.GetType(), "correctCode");
            if (df == null || cf == null) continue;
            var dt = df.GetValue(kp) as TMPro.TMP_Text;
            string code = cf.GetValue(kp) as string;
            string shown = dt != null ? dt.text : "(no display)";
            Debug.Log($"[Smoke] keypad {kp.GetType().Name} on {PathOf(kp.transform)}: code '{code}' display '{shown}' active={kp.gameObject.activeInHierarchy} displayActive={(dt != null && dt.gameObject.activeInHierarchy)}");
            if (dt == null || string.IsNullOrWhiteSpace(dt.text) || (!dt.text.Contains("_") && dt.text != "UNLOCKED")) blank.Append(kp.GetType().Name + " ");
        }
        Check(blank.Length == 0, "every keypad shows its empty code slots (" + (blank.Length == 0 ? "ok" : blank.ToString()) + ")");
        yield return WaitFor(() => !CutscenePlayer.IsPlaying, 20f);
        Check(!GameState.IsCutscene && Camera.main != null && Camera.main.GetComponentInParent<Movement>() != null, "cutscene gives the camera back to the player");
        Find<PhoneInteractable>().Interact();
        yield return WaitFor(() => Quest == "Unlock the phone", 5f);
        Check(Quest == "Unlock the phone", "phone collected");
        var pm = PhoneManager.Instance;
        Call(pm, "OpenPhone");
        yield return null;
        Check(PhoneManager.IsPhoneOpen && GameState.InputBlocked, "phone opens and blocks movement");
        yield return new WaitForSeconds(0.8f);
        UiShot("ui_phone_lock");
        Call(pm, "OnPasswordSubmitted", "0000");
        Check(pm.IsLocked, "wrong phone password rejected");
        Call(pm, "OnPasswordSubmitted", (string)Field(pm.GetType(), "lockPassword").GetValue(pm));
        Check(!pm.IsLocked, "birthday password unlocks the phone");
        yield return new WaitForSeconds(0.5f);
        UiShot("ui_phone_home");
        Call(pm, "ClosePhone");
        yield return WaitFor(() => Quest == "Leave the bedroom", 5f);

        var clockKeypad = KeypadUnder("Room1") ?? KeypadByDisplay("Room1_Door");
        yield return Solve(clockKeypad, "9999");
        Check(Quest == "Leave the bedroom", "wrong clock code keeps the door locked");
        yield return Solve(clockKeypad);
        yield return WaitFor(() => Quest == "Explore the house", 5f);
        Check(Quest == "Explore the house", "random clock time opens the bedroom door");
        var bedDoor = FindObjectsByType<InteractiveDoor>(FindObjectsSortMode.None).OrderBy(d => Vector3.Distance(d.transform.position, clockKeypad.transform.position)).First();
        var hinge = Hinge(bedDoor);
        string bedDoorPath = PathOf(bedDoor.transform);
        var closedRot = hinge.localRotation;
        bedDoor.ToggleDoor();
        yield return new WaitForSeconds(0.5f);
        float midOpen = Quaternion.Angle(closedRot, hinge.localRotation);
        bedDoor.ToggleDoor();
        yield return new WaitForSeconds(0.1f);
        float afterReverse = Quaternion.Angle(closedRot, hinge.localRotation);
        Check(midOpen > 5f && afterReverse > 1f && afterReverse < midOpen,
            $"door swings open from closed and reverses smoothly mid-swing ({midOpen:F0} deg -> {afterReverse:F0} deg)");
        bool slammed = false;
        var ctrl = (DoorController)Field(typeof(InteractiveDoor), "doorController").GetValue(bedDoor);
        ctrl.Closed += () => slammed = true;
        yield return new WaitForSeconds(0.8f);
        Check(slammed && Quaternion.Angle(closedRot, hinge.localRotation) < 0.5f, "door reaches the frame and fires the slam sound at that moment");
        bedDoor.ToggleDoor();
        yield return new WaitForSeconds(1.6f);
        float fullOpen = Quaternion.Angle(closedRot, hinge.localRotation);
        Check(fullOpen > 60f, $"bedroom door ends fully open ({fullOpen:F0} deg)");
        int steps = 0; float walkedFrom = 0f;
        System.Action<Vector3, float> countStep = (pos, k) => { if (k <= 1f) steps++; };
        Movement.Stepped += countStep;
        var walker = Find<Movement>(); Vector3 walkStart = walker.transform.position;
        yield return Walk(new Vector3(5.5f, 0, 7.6f), new Vector3(9.8f, 0, 7.6f), new Vector3(12f, 0, 12.5f));
        Movement.Stepped -= countStep;
        walkedFrom = Vector3.Distance(new Vector3(walkStart.x, 0, walkStart.z), new Vector3(walker.transform.position.x, 0, walker.transform.position.z));
        Debug.Log($"[Smoke] footsteps: {steps} over {walkPath:F1} m walked ({walkedFrom:F1} m from the start), from her feet={walker.StepsFromBody}");
        // one footstep per step of her walk (about a metre at her size in the house), not per frame or per clip
        Check(walker.StepsFromBody && steps >= 4 && walkPath / Mathf.Max(1, steps) > 0.5f && walkPath / Mathf.Max(1, steps) < 2f,
            $"footsteps follow her walking feet ({steps} steps over {walkPath:F1} m)");
        Check(walkOk, "walk bedroom -> house (stopped at " + walkEnd + ")");

        // ── House ──
        yield return Solve(KeypadByDisplay("Room2_Shelf"));
        yield return WaitFor(() => Quest == "Find a way out", 5f);
        Check(Quest == "Find a way out", "random kitchen digits open the cabinet");
        var remote = GameObject.Find("Building/Room2/QuestObject/remote");
        foreach (var i in remote.GetComponents<Interactable>()) if (i.canInteract) i.Interact();
        yield return Solve(KeypadByDisplay("Room2_Door"));
        yield return WaitFor(() => Quest == "The storage room", 6f);
        Check(Quest == "The storage room", "TV + EVIE + digits open the storage door");
        DoorUnder("Door R2").ToggleDoor();
        yield return new WaitForSeconds(1.5f);
        yield return Walk(new Vector3(12f, 0, 13.2f), new Vector3(20.6f, 0, 13.2f), new Vector3(20.6f, 0, 21.9f), new Vector3(25f, 0, 21.9f), new Vector3(30f, 0, 14f));
        Check(walkOk, "walk house -> storage (stopped at " + walkEnd + ")");
        yield return WaitFor(() => Quest == "Find the spare fuses", 6f);
        Check(Quest == "Find the spare fuses", "entering the storage room starts the fuse quest");
        var power = Find<StoragePower>();
        Check(power != null && power.emergencyLights.Any(l => l.enabled), "no power yet: emergency lights on");
        Check(GameAudio.CurrentAmbient == "amb_blackout", "unpowered storage plays the uneasy drone");

        // ── Storage puzzles ──
        yield return Walk(new Vector3(33f, 0, 17.5f), new Vector3(42f, 0, 17.5f));
        Check(!walkOk && walkEnd.x < 37.7f, "storage east wall blocks the player (stopped at " + walkEnd + ")");

        yield return Solve(KeypadByDisplay("DoorR3_1"));
        DoorUnder("Door R3_1").ToggleDoor();
        yield return new WaitForSeconds(1.5f);
        yield return Walk(new Vector3(34.6f, 0, 23.5f), new Vector3(34.6f, 0, 27.5f), new Vector3(33.5f, 0, 33f));
        Check(walkOk, "EVIE letter code opens the storeroom and the player walks in (stopped at " + walkEnd + ")");

        var pile = FindObjectsByType<Interactable>(FindObjectsSortMode.None).FirstOrDefault(i => i.GetType().Name == "RandomCodeKeypadFormPile");
        yield return Solve(pile);
        var rabbit = FindObjectsByType<Interactable>(FindObjectsSortMode.None).FirstOrDefault(i => i.GetType().Name == "RabbitCipherKeypad");
        yield return Solve(rabbit);
        var libKeypad = FindObjectsByType<KeypadInteractable>(FindObjectsSortMode.None).FirstOrDefault(k => HasAncestor(k.transform, "Room3_2"));
        if (libKeypad != null) yield return Solve(libKeypad);
        DoorUnder("Door R3_2").ToggleDoor();
        yield return new WaitForSeconds(1.5f);
        yield return Walk(new Vector3(34.6f, 0, 27.5f), new Vector3(34.6f, 0, 23.5f), new Vector3(36f, 0, 21.8f), new Vector3(40f, 0, 21.8f), new Vector3(43f, 0, 19f));
        Check(walkOk, "library door opens and the player walks in (stopped at " + walkEnd + ")");

        var heard = new System.Collections.Generic.List<string>();
        System.Action<string> listen = n => heard.Add(n);
        GameAudio.Played += listen;
        foreach (var f in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)) { f.Interact(); yield return new WaitForSeconds(0.3f); }
        Check(heard.Contains("pickup"), "picking up a fuse plays its sound");
        yield return WaitFor(() => Quest == "Restore the power", 6f);
        Check(Quest == "Restore the power", "three fuses found");
        heard.Clear();
        Find<KeyPickup>().Interact();
        yield return null;
        Check(heard.Contains("key_pickup"), "picking up the key plays its sound (real key jingle)");
        foreach (var k in FindObjectsByType<KeyDoorInteractable>(FindObjectsSortMode.None)) { if (k.canInteract) k.Interact(); yield return new WaitForSeconds(0.3f); }
        int cutscenesBefore = CutscenePlayer.Played;
        heard.Clear();
        foreach (var s in FindObjectsByType<PlaceItemInteractable>(FindObjectsSortMode.None)) { s.Interact(); yield return new WaitForSeconds(0.3f); }
        Check(heard.FindAll(n => n == "fuse_insert").Count >= 3, $"placing each fuse plays its sound ({heard.FindAll(n => n == "fuse_insert").Count} of 3)");
        var puzzle = Find<LeverPuzzle>();
        Check(puzzle.IsPowered, "all fuses placed: power on");
        yield return WaitFor(() => CutscenePlayer.Played > cutscenesBefore, 3f);
        Check(CutscenePlayer.Played > cutscenesBefore, "power-restored cutscene plays");
        yield return WaitFor(() => !CutscenePlayer.IsPlaying, 20f);
        yield return WaitFor(() => Quest == "Wake the levers", 6f);
        Check(Quest == "Wake the levers", "power quest complete");
        yield return new WaitForSeconds(1.0f);
        Check(!power.emergencyLights.Any(l => l.enabled), "power restored: emergency lights off");

        var levers = FindObjectsByType<Lever>(FindObjectsSortMode.None).OrderBy(l => l.symbolIndex).ToArray();
        // one wrong pull first: the puzzle must reset
        var wrong = levers.Last();
        wrong.Interact();
        yield return new WaitForSeconds(1.5f);
        Check(!puzzle.IsSolved, "wrong lever order resets the puzzle");
        heard.Clear();
        foreach (var l in levers) { l.Interact(); yield return new WaitForSeconds(0.4f); }
        Check(heard.FindAll(n => n == "lever_pull").Count >= levers.Length, "every lever plays its sound");
        GameAudio.Played -= listen;
        yield return WaitFor(() => Quest == "Go deeper", 6f);
        Check(puzzle.IsSolved && Quest == "Go deeper", "symbol order solves the lever puzzle");
        Check(CutscenePlayer.Played >= 3, "door cutscene plays when the levers are solved");
        yield return new WaitForSeconds(2.2f);
        UiShot("cutscene_door");
        yield return WaitFor(() => !CutscenePlayer.IsPlaying, 20f);
        // the v4 rig's skirt/hair/ribbon chains, simulated by EchoSecondaryMotion, on a spawned protagonist
        // (the door cutscene no longer shows her, and the player body is shadow-only)
        var player = Find<Movement>();
        Vector3 standAt = CharacterLibrary.GroundBelow(player.transform.position + player.transform.forward * 2f);
        var testHero = CharacterLibrary.Spawn(false, standAt, -player.transform.forward);
        if (testHero != null) testHero.transform.root.position = standAt;
        yield return new WaitForSeconds(0.5f);
        var skirt = testHero != null ? testHero.transform.root.GetComponentInChildren<EchoCharacters.EchoSecondaryMotion>() : null;
        var skirtBone = skirt != null ? skirt.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("SkirtChain")) : null;
        float swing = -1f;
        if (skirt != null)
        {
            // a sudden sideways step: only a simulated skirt lags behind her body
            var body = skirt.transform.root;
            body.position += body.right * 0.4f;
            yield return null; yield return null;
            swing = skirt.LastBend;
            body.position -= body.right * 0.4f;
        }
        Debug.Log($"[Smoke] skirt: spring={(skirt != null)} bone={(skirtBone != null)} swing={swing:F1}");
        Check(swing > 2f, $"the skirt lags and swings with spring physics ({swing:F1} deg after a sudden step)");
        if (testHero != null) Destroy(testHero.transform.root.gameObject);

        // ── Save and Exit from the pause menu, then Continue: doors must come back already open ──
        BackupSave();
        float timeBeforeSave = RunTimer.Elapsed;
        Check(RunTimer.Running && timeBeforeSave > 10f, $"40-minute clock is running in the house ({timeBeforeSave:F0} s used)");
        var dlg = Find<DialogueManager>();
        Check(dlg != null && dlg.dialoguePanel.GetComponent<DialogueSkin>() != null, "dialogue box uses the game's style");
        var phoneFrame = FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(i => i.name == "I16");
        Check(phoneFrame != null && phoneFrame.texture != null && phoneFrame.texture.name == "phone_home", "phone uses the new pearl and gold skin");
        PauseMenu.Instance.Pause();
        Check(PauseMenu.IsPaused && Time.timeScale == 0f, "Esc menu pauses the game");
        float pausedAt = RunTimer.Elapsed;
        yield return new WaitForSecondsRealtime(1.5f);
        Check(RunTimer.Elapsed - pausedAt > 1f, $"40-minute clock keeps running in the pause menu (+{RunTimer.Elapsed - pausedAt:F1} s)");
        PauseMenu.Instance.SaveAndExit();
        yield return WaitFor(() => Scene == "Main", 8f);
        Check(Scene == "Main" && !PauseMenu.IsPaused, "Save and Exit returns to the title screen");
        SaveSystem.LoadGame();
        yield return WaitFor(() => Scene == "Level_1", 8f);
        yield return null;
        yield return null;
        hinge = Hinge(FindObjectsByType<InteractiveDoor>(FindObjectsSortMode.None).First(d => PathOf(d.transform) == bedDoorPath));
        float loadedAngle = Quaternion.Angle(closedRot, hinge.localRotation);
        Check(loadedAngle > 60f, $"loaded save: bedroom door is already open, no replayed animation ({loadedAngle:F0} deg)");
        Check(Quest == "Go deeper", "loaded save keeps the quest (" + Quest + ")");
        Check(Mathf.Abs(RunTimer.Elapsed - timeBeforeSave) < 20f, $"loaded save keeps the time used ({timeBeforeSave:F0} s -> {RunTimer.Elapsed:F0} s)");
        yield return Settle(0.5f);

        var endDoor = GameObject.Find("ToEnding").GetComponentInChildren<Door>(true);
        Check(endDoor != null && endDoor.gameObject.activeInHierarchy, "ending door appeared");
        yield return Settle(1.5f);
        foreach (var i in endDoor.GetComponents<Interactable>()) if (i.canInteract) i.Interact();
        yield return WaitFor(() => Scene == "Confrontation", 12f);
        Check(Scene == "Confrontation", "ending door leads to the Confrontation");
        yield return null;
        var director = Find<ConfrontationDirector>();
        Check(director != null && director.doppelganger != null && director.doppelganger.Find("TheDouble") != null, "The Double uses the rigged character model");
        yield return WaitFor(() => director != null && director.doppelganger.gameObject.activeInHierarchy, 40f);
        yield return new WaitForSeconds(1.6f);
        UiShot("confrontation_double");

        // ── Ending cutscene ──
        int playedBeforeEnding = CutscenePlayer.Played;
        yield return WaitFor(() => CutscenePlayer.Played > playedBeforeEnding, 40f);
        Check(CutscenePlayer.Played > playedBeforeEnding, "ending cutscene plays after the confrontation");
        yield return new WaitForSeconds(1.4f);
        UiShot("ending_rush");
        yield return new WaitForSeconds(2.0f);
        UiShot("ending_hit");
        yield return new WaitForSeconds(4.0f);
        UiShot("ending_alone");
        yield return WaitFor(() => Scene == "DemoEnd", 60f);
        Check(Scene == "DemoEnd", "confrontation ends on the end screen");
        yield return new WaitForSeconds(1.5f);
        Check(Cursor.lockState == CursorLockMode.None, "cursor free on the end screen");
        Check(!RunTimer.Running && RunTimer.LastRunTime > 0f, $"timer stopped at the end ({RunTimer.LastRunTime:F0} s)");
        yield return new WaitForSeconds(5f);
        UiShot("ui_end_screen");

        // ── 40-minute limit: run out of time in a fresh house ──
        SaveSystem.StartNewGame("Level_1");
        yield return WaitFor(() => Scene == "Level_1", 8f);
        yield return Settle(1f);
        Check(RunTimer.Running && RunTimer.Elapsed < 5f, "new game restarts the 40-minute clock");
        yield return WaitFor(() => !CutscenePlayer.IsPlaying, 20f); // the clock does not run during the waking-up cutscene
        RunTimer.SetElapsed(RunTimer.LimitSeconds - 75f);
        yield return Settle(1.5f);
        UiShot("ui_timer_warning");
        int stuttersBefore = LowTimeFlicker.Stutters;
        yield return WaitFor(() => LowTimeFlicker.Stutters > stuttersBefore, 14f);
        Check(LowTimeFlicker.Stutters > stuttersBefore, "lamps stutter in the last minutes of the clock");
        Check(Resources.Load<AudioClip>("Audio/door_reveal") != null && Resources.Load<AudioClip>("Audio/amb_wind") != null, "door reveal and wind sounds present");
        RunTimer.SetElapsed(RunTimer.LimitSeconds - 70f);
        RunTimer.SetElapsed(RunTimer.LimitSeconds - 1f);
        yield return new WaitForSeconds(3.5f);
        var timeUp = RunTimer.Instance.transform.Find("TimerCanvas/TimeUp");
        Check(RunTimer.TimedOut && timeUp != null && timeUp.gameObject.activeSelf && GameState.IsCutscene, "time out shows the Time's up screen and freezes the player");
        UiShot("ui_timeout");
        RunTimer.Instance.TryAgain();
        yield return WaitFor(() => Scene == "Level_1", 8f);
        yield return Settle(1f);
        Check(!RunTimer.TimedOut && RunTimer.Running && RunTimer.Elapsed < 5f, "Try Again starts the house over with a full clock");
        var all = new System.Collections.Generic.List<string>();
        foreach (var c in Resources.LoadAll<AudioClip>("Audio")) if (!c.name.StartsWith("amb_")) all.Add(c.name);
        all.Sort();
        Debug.Log("[Smoke] sounds heard during the run: " + string.Join(", ", everyClip));
        Debug.Log("[Smoke] sounds never heard: " + string.Join(", ", all.FindAll(n => !everyClip.Contains(n))));
    }
}
#endif
