#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using EchoCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only look-dev captures in play mode (Lab.Keypads / Lab.Character in batch mode): renders views
/// that need runtime state (keypad screens after their Start, the character animating) into ../_work/lab.
/// </summary>
public class LabDriver : MonoBehaviour
{
    string task;
    string OutDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../_work/lab"));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void MaybeStart()
    {
        string t = UnityEditor.SessionState.GetString("LabTask", "");
        if (t == "") return;
        UnityEditor.SessionState.SetString("LabTask", "");
        var go = new GameObject("[Lab]");
        DontDestroyOnLoad(go);
        go.AddComponent<LabDriver>().task = t;
    }

    IEnumerator Start()
    {
        System.IO.Directory.CreateDirectory(OutDir);
        yield return new WaitForSeconds(1.5f);
        if (task == "keypads") yield return Keypads();
        else if (task == "character") yield return Character();
        else if (task == "player") yield return Player();
        else if (task == "palettes") yield return Palettes();
        else if (task == "perf") yield return Perf();
        else if (task == "spawn") yield return SpawnCost();
        else if (task == "ui") yield return UiDump();
        else if (task == "shadowwalk") yield return ShadowWalk();
        else if (task == "chin") yield return Chin();
        else if (task == "wakeface") yield return WakeFace();
        else if (task == "rooms") yield return Rooms();
        else if (task == "reverb") yield return Reverb();
        else if (task == "steps") yield return StepEvents();
        else if (task == "mix") yield return MixTest();
        else if (task == "mixall") yield return MixAll();
        else if (task == "enddoor") yield return EndDoor();
        else if (task == "stride") yield return StrideJump();
        else if (task == "cutsounds") yield return CutsceneSounds();
        UnityEditor.EditorApplication.Exit(0);
    }

    Camera MakeCam(float fov)
    {
        var cam = new GameObject("LabCam").AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f;
        var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
        data.renderPostProcessing = task == "keypads" || task == "player"; // look-dev on the plain stage without depth of field
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.32f, 0.34f, 0.38f);
        return cam;
    }

    void Shot(Camera cam, string name, int w = 1200, int h = 900)
    {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, name + ".jpg"), tex.EncodeToJPG(90));
        Destroy(tex); rt.Release(); Destroy(rt);
    }

    IEnumerator Keypads()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        var cam = MakeCam(40f);
        int i = 0;
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            var f = mb.GetType().GetField("displayText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (f == null) continue;
            var text = f.GetValue(mb) as TMPro.TMP_Text;
            if (text == null) continue;
            var t = text.transform;
            Debug.Log($"[Lab] keypad {mb.GetType().Name} {mb.transform.parent?.name}/{mb.name} pad={mb.transform.position} text='{text.text}' at {t.position} fwd={t.forward} lossy={t.lossyScale} canvas={(text.canvas != null ? text.canvas.name : "none")} canvasEnabled={(text.canvas != null && text.canvas.enabled)} alpha={text.color.a} font={text.font?.name} size={text.fontSize} bounds={text.textBounds.size}");
            foreach (var side in new[] { -1f, 1f })
            {
                cam.transform.position = t.position + t.forward * side * 0.7f;
                cam.transform.LookAt(t.position);
                Shot(cam, $"keypad{i}_{mb.GetType().Name}_{(side < 0 ? "front" : "back")}", 800, 600);
            }
            i++;
            yield return null;
        }
    }

    /// <summary>Every sound at the volume the game plays it, heard in the bedroom and in the fuse room:
    /// loudest 300 ms of the output (dB) and how far above that room's ambience.</summary>
    IEnumerator MixAll()
    {
        for (float t = 0f; t < 60f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        var mover = FindAnyObjectByType<Movement>();
        var cc = mover.GetComponent<CharacterController>();
        var tap = FindAnyObjectByType<AudioListener>().gameObject.AddComponent<AudioTap>();
        float Loudest(System.Collections.Generic.List<(float t, float e)> e, float window)
        {
            float best = 1e-12f;
            for (int i = 0; i < e.Count; i++) { float sum = 0; int n = 0; for (int j = i; j < e.Count && e[j].t - e[i].t < window; j++) { sum += e[j].e; n++; } best = Mathf.Max(best, sum / Mathf.Max(1, n)); }
            return 10f * Mathf.Log10(best);
        }
        // (name, volume the game uses)
        var sounds = new (string n, float v)[]
        {
            ("wood_step_2", 1f), ("water_step_3", 1f), ("dialogue_blip", 0.5f), ("ui_click", 1f), ("keypad_beep", 0.7f),
            ("door_open", 1f), ("door_close", 1f), ("door_creak", 1f), ("door_locked", 0.6f), ("cabinet_open", 1f), ("cabinet_close", 1f),
            ("metal_open", 1f), ("metal_close", 1f), ("pickup", 1f), ("interact", 1f), ("fuse_insert", 1f), ("lever_pull", 1f),
            ("unlock", 1f), ("error", 1f), ("phone_open", 1f), ("phone_close", 1f), ("quest_complete", 0.6f), ("puzzle_solved", 1f),
            ("reveal", 1f), ("memory_whoosh", 1f), ("door_reveal", 0.85f), ("power_up", 1f), ("power_down", 1f), ("impact", 1f),
            ("heartbeat", 0.8f), ("title_enter", 1f), ("shimmer", 0.7f),
        };
        string only = System.Environment.GetEnvironmentVariable("LAB_MIX_ONLY");
        if (only == "diag2")
        {
            foreach (var (room, pos) in new[] { ("bedroom", new Vector3(0.2f, 0f, 4.1f)), ("fuse room", new Vector3(27f, 0f, 9f)), ("bedroom again", new Vector3(0.2f, 0f, 4.1f)) })
            {
                cc.enabled = false; mover.transform.position = new Vector3(pos.x, mover.transform.position.y, pos.z); cc.enabled = true;
                yield return new WaitForSeconds(3f);
                GameAudio.Hush(0.1f);
                yield return new WaitForSeconds(0.5f);
                tap.Begin(); GameAudio.PlaySfx("power_up"); yield return new WaitForSeconds(1.5f);
                float l = Loudest(tap.End(), 0.3f);
                var playing = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(a => a.isPlaying).Select(a => $"{a.name}:{(a.clip ? a.clip.name : "-")} v{a.volume:F2} sb{a.spatialBlend:F1} pri{a.priority}");
                var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Select(x => $"{x.name}@{x.transform.position} en{x.enabled}");
                Debug.Log($"[Lab] mixall diag2 {room}: power_up {l:F1} dB | listener vol {AudioListener.volume:F2} | listeners {string.Join(";", listeners)} | lowpass {FindObjectsByType<AudioLowPassFilter>(FindObjectsSortMode.None).Length} | playing {string.Join(", ", playing)}");
            }
            yield break;
        }
        if (only == "diag")
        {
            cc.enabled = false; mover.transform.position = new Vector3(27f, mover.transform.position.y, 9f); cc.enabled = true;
            yield return new WaitForSeconds(4f);
            var setEcho = typeof(AudioMood).GetProperty("EchoAmount").GetSetMethod(true);
            var mood = FindAnyObjectByType<AudioMood>();
            foreach (var mode in new[] { "normal", "no echo voice", "no echo, no zones", "no ambience" })
            {
                if (mode == "no echo voice") { mood.enabled = false; setEcho.Invoke(null, new object[] { 0f }); }
                if (mode == "no echo, no zones") foreach (var z in FindObjectsByType<AudioReverbZone>(FindObjectsSortMode.None)) z.enabled = false;
                if (mode == "no ambience") GameAudio.Hush(0.1f);
                yield return new WaitForSeconds(0.6f);
                tap.Begin(); GameAudio.PlaySfx("power_up"); yield return new WaitForSeconds(1.5f);
                float l = Loudest(tap.End(), 0.3f);
                tap.Begin(); GameAudio.PlaySfx("quest_complete", 0.6f); yield return new WaitForSeconds(1.2f);
                float q = Loudest(tap.End(), 0.3f);
                Debug.Log($"[Lab] mixall diag {mode}: power_up {l:F1} dB, quest_complete {q:F1} dB");
            }
            yield break;
        }
        foreach (var (room, pos) in new[] { ("bedroom", new Vector3(0.2f, 0f, 4.1f)), ("fuse room", new Vector3(27f, 0f, 9f)) })
        {
            cc.enabled = false; mover.transform.position = new Vector3(pos.x, mover.transform.position.y, pos.z); cc.enabled = true;
            yield return new WaitForSeconds(4f);
            tap.Begin(); yield return new WaitForSeconds(1.5f);
            float amb = Loudest(tap.End(), 0.3f);
            Debug.Log($"[Lab] mixall {room}: ambience {amb:F1} dB ({GameAudio.CurrentAmbient}), echo {AudioMood.EchoAmount:F2}");
            foreach (var (n, v) in sounds)
            {
                tap.Begin();
                GameAudio.PlaySfx(n, v);
                yield return new WaitForSeconds(1.0f);
                float l = Loudest(tap.End(), 0.3f);
                Debug.Log($"[Lab] mixall {room} | {n,-15} {l,6:F1} dB  {l - amb,5:+0.0;-0.0} over ambience");
                yield return new WaitForSeconds(0.3f);
            }
        }
    }

    /// <summary>In the fuse room (power out, its ambience on): how loud each sound comes out over the room's
    /// own sound (loudest 100 ms of the output while it plays vs the ambience alone).</summary>
    IEnumerator MixTest()
    {
        for (float t = 0f; t < 60f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        var mover = FindAnyObjectByType<Movement>();
        var cc = mover.GetComponent<CharacterController>();
        var tap = FindAnyObjectByType<AudioListener>().gameObject.AddComponent<AudioTap>();
        cc.enabled = false; mover.transform.position = new Vector3(27f, mover.transform.position.y, 9f); cc.enabled = true;
        yield return new WaitForSeconds(4f); // the storage ambience fades in
        float Loudest(System.Collections.Generic.List<(float t, float e)> e)
        {
            float best = 1e-12f;
            for (int i = 0; i < e.Count; i++) { float sum = 0; int n = 0; for (int j = i; j < e.Count && e[j].t - e[i].t < 0.1f; j++) { sum += e[j].e; n++; } best = Mathf.Max(best, sum / Mathf.Max(1, n)); }
            return 10f * Mathf.Log10(best);
        }
        tap.Begin(); yield return new WaitForSeconds(1.5f);
        float amb = Loudest(tap.End());
        Debug.Log($"[Lab] mix ambience alone {amb:F1} dB (ambient={GameAudio.CurrentAmbient}) echo={AudioMood.EchoAmount:F2}");
        foreach (var name in new[] { "wood_step_2", "fuse_insert", "pickup", "lever_pull", "unlock", "door_open", "keypad_beep", "error", "power_up" })
        {
            tap.Begin();
            GameAudio.PlaySfx(name);
            yield return new WaitForSeconds(1.2f);
            Debug.Log($"[Lab] mix {name}: {Loudest(tap.End()) - amb:+0.0;-0.0} dB over the ambience");
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>Walks the player at game pace and logs the body's foot events and animator state.</summary>
    IEnumerator StepEvents()
    {
        for (float t = 0f; t < 60f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        var mover = FindAnyObjectByType<Movement>();
        var cc = mover.GetComponent<CharacterController>();
        var body = mover.transform.Find("PlayerBody");
        var anim = body != null ? body.GetComponentInChildren<Animator>() : null;
        var ev = anim != null ? anim.GetComponent<EchoCharacters.EchoAnimationEvents>() : null;
        int l = 0, r = 0, steps = 0;
        if (ev != null) { ev.LeftFoot += () => l++; ev.RightFoot += () => r++; }
        System.Action<Vector3, float> onStep = (p, k) => steps++;
        Movement.Stepped += onStep;
        Debug.Log($"[Lab] steps: body={body != null} animator={anim != null} events={ev != null} cull={anim?.cullingMode} fireEvents={anim?.fireEvents} controller={anim?.runtimeAnimatorController?.name}");
        for (float t = 0f; t < 4f; t += Time.deltaTime)
        {
            cc.Move(mover.transform.forward * 2.5f * Time.deltaTime + Vector3.down * 0.5f * Time.deltaTime);
            if (Mathf.Repeat(t, 1f) < Time.deltaTime)
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                var clips = anim.GetCurrentAnimatorClipInfo(0);
                Debug.Log($"[Lab] steps t={t:F1} grounded={cc.isGrounded} state={st.shortNameHash} norm={st.normalizedTime:F2} clips={string.Join(",", clips.Select(c => c.clip.name + ":" + c.weight.ToString("F2")))} L={l} R={r} sounds={steps}");
            }
            yield return null;
        }
        Debug.Log($"[Lab] steps done: L={l} R={r} sounds={steps}");
        Movement.Stepped -= onStep;
    }

    /// <summary>Every sound heard while a cutscene plays in this scene (the opening, or the confrontation and its ending).</summary>
    IEnumerator CutsceneSounds()
    {
        var log = new System.Text.StringBuilder();
        float t0 = Time.time;
        GameAudio.Played += n => { if (CutscenePlayer.IsPlaying) log.Append($" {Time.time - t0:F1}:{n}"); };
        int rings = 0;
        ActorFootsteps.Stepped += (p, k) => rings++;
        string scene = SceneManager.GetActiveScene().name;
        for (float t = 0f; t < 120f && SceneManager.GetActiveScene().name == scene; t += Time.deltaTime)
        {
            var dm = FindAnyObjectByType<DialogueManager>();
            if (dm != null && dm.IsTalking() && Mathf.Repeat(t, 1.5f) < Time.deltaTime) dm.SendMessage("NextLine", SendMessageOptions.DontRequireReceiver);
            if (scene == "WhiteSpace" && t > 5f && !CutscenePlayer.IsPlaying) break;
            // her walk in the cutscene: where she is and what she plays, five times a second
            if (Mathf.Repeat(t, 0.2f) < Time.deltaTime)
            {
                var actor = FindObjectsByType<EchoCharacterView>(FindObjectsSortMode.None).FirstOrDefault(v => v.transform.root.GetComponent<Movement>() == null && v.gameObject.activeInHierarchy);
                var an = actor != null ? actor.GetComponentInChildren<Animator>() : null;
                if (an != null) Debug.Log($"[Lab] actor t={Time.time - t0:F1} pos={actor.transform.position.x:F2},{actor.transform.position.z:F2} clips={string.Join(",", an.GetCurrentAnimatorClipInfo(0).Where(c => c.weight > 0.05f).Select(c => c.clip.name + ":" + c.weight.ToString("F1")))}");
            }
            yield return null;
        }
        Debug.Log($"[Lab] cutscene sounds in {scene} (actor steps {rings}):{log}");
        Debug.Log("[Lab] cutsounds done");
    }

    /// <summary>
    /// Walk, jump from the stride, land and walk on, then stop: every footstep sound with its time, the
    /// clips playing around the jump, the lead foot and the time in the air (EchoCharacters v9).
    /// </summary>
    IEnumerator StrideJump()
    {
        for (float t = 0f; t < 60f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        yield return new WaitForSeconds(1f);
        var dm = FindAnyObjectByType<DialogueManager>();
        for (float t = 0f; t < 30f && dm != null && dm.IsTalking(); t += 0.3f) { dm.SendMessage("NextLine", SendMessageOptions.DontRequireReceiver); yield return new WaitForSeconds(0.3f); }
        var mover = FindAnyObjectByType<Movement>();
        var cc = mover.GetComponent<CharacterController>();
        var body = mover.transform.Find("PlayerBody");
        var anim = body != null ? body.GetComponentInChildren<Animator>() : null;
        var view = body != null ? body.GetComponentInChildren<EchoCharacterView>() : null;
        float t0 = Time.time;
        var log = new System.Text.StringBuilder();
        GameAudio.Played += n => { if (n.Contains("step")) log.Append($" {Time.time - t0:F2}:{n}"); };
        Movement.Stepped += (p, k) => log.Append($"[{k:F2}]");
        float walk = EchoLocomotion.PowerReference(Vector2.up) * PlayerAvatar.ModelScale;
        // open floor (run it in the White Space)
        if (SceneManager.GetActiveScene().name == "Level_1") { cc.enabled = false; mover.transform.position = new Vector3(10.5f, mover.transform.position.y, 15f); mover.transform.rotation = Quaternion.Euler(0f, 49f, 0f); cc.enabled = true; }
        yield return new WaitForSeconds(0.5f);
        Vector3 dir = mover.transform.forward;
        string Clips() => string.Join(",", anim.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name + ":" + c.weight.ToString("F1")));
        // walk 3 s
        for (float t = 0f; t < 2.5f; t += Time.deltaTime) { cc.Move((dir * walk + Vector3.down * 2f) * Time.deltaTime); yield return null; }
        Debug.Log($"[Lab] stride walking: grounded={cc.isGrounded} clips={Clips()} pos={mover.transform.position}");
        Debug.Log($"[Lab] stride walk: speed {walk:F2} m/s, steps so far:{log}");
        log.Clear();
        // jump while walking (as if Space was pressed)
        typeof(Movement).GetProperty("JumpWindup").GetSetMethod(true).Invoke(mover, new object[] { true });
        typeof(Movement).GetField("windupLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(mover, Movement.JumpWindupTime);
        float jumpAt = Time.time, leftGround = -1f, landed = -1f, peak = mover.transform.position.y, y0 = mover.transform.position.y;
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            cc.Move((dir * walk + (cc.isGrounded ? Vector3.down * 2f : Vector3.zero)) * Time.deltaTime);
            if (!cc.isGrounded && leftGround < 0f && Time.time - jumpAt > 0.05f) leftGround = Time.time;
            if (leftGround > 0f && cc.isGrounded && landed < 0f) landed = Time.time;
            peak = Mathf.Max(peak, mover.transform.position.y);
            if (Mathf.Repeat(t, 0.1f) < Time.deltaTime) Debug.Log($"[Lab] stride t={Time.time - jumpAt:F2} grounded={cc.isGrounded} lead={view.JumpLeadFoot} clips={Clips()}");
            yield return null;
        }
        Debug.Log($"[Lab] stride jump: lead={view.JumpLeadFoot} airborne {(landed - leftGround):F2} s, peak {peak - y0:F2} m; sounds after pressing:{log}");
        log.Clear();
        // stop
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            if (Mathf.Repeat(t, 0.25f) < Time.deltaTime) Debug.Log($"[Lab] stride stop t={t:F2} clips={Clips()}");
            yield return null;
        }
        Debug.Log($"[Lab] stride stop sounds:{log}");
        log.Clear();
        // standing jump
        yield return new WaitForSeconds(1f);
        typeof(Movement).GetProperty("JumpWindup").GetSetMethod(true).Invoke(mover, new object[] { true });
        typeof(Movement).GetField("windupLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(mover, Movement.JumpWindupTime);
        yield return new WaitForSeconds(0.3f);
        string leadStanding = view.JumpLeadFoot.ToString();
        yield return new WaitForSeconds(1.7f);
        Debug.Log($"[Lab] stride standing jump: lead={leadStanding} sounds:{log}");
        Debug.Log("[Lab] stride done");
    }

    /// <summary>Records what the listener hears after a door sound at several spots: the echo left 0.3-1.2 s
    /// after the sound, relative to the sound itself (more negative = drier).</summary>
    IEnumerator Reverb()
    {
        for (float t = 0f; t < 60f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        var mover = FindAnyObjectByType<Movement>();
        var listener = FindAnyObjectByType<AudioListener>();
        var tap = listener.gameObject.AddComponent<AudioTap>();
        var cc = mover.GetComponent<CharacterController>();
        var spots = new (string name, Vector3 pos)[]
        {
            ("bedroom", new Vector3(0.2f, 0f, 4.1f)), ("living", new Vector3(15.2f, 0f, 20f)),
            ("storage_door_from_living", new Vector3(23.4f, 0f, 22.8f)), ("storage_middle", new Vector3(30.2f, 0f, 13.3f)),
            ("storeroom_door", new Vector3(34.6f, 0f, 26.6f)), ("storeroom_middle", new Vector3(30.2f, 0f, 33.3f)),
            ("library_door", new Vector3(38.6f, 0f, 21.8f)), ("library_middle", new Vector3(45.2f, 0f, 18.3f)),
        };
        float y = mover.transform.position.y;
        foreach (var (name, pos) in spots)
        {
            cc.enabled = false; mover.transform.position = new Vector3(pos.x, y, pos.z); cc.enabled = true;
            yield return new WaitForSeconds(0.6f); // let the room's ambience trigger fire, then silence it
            GameAudio.Hush(0.1f);
            yield return new WaitForSeconds(2.4f);
            float[] tails = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var z in FindObjectsByType<AudioReverbZone>(FindObjectsSortMode.None)) z.enabled = pass == 0;
                var mood = FindAnyObjectByType<AudioMood>();
                if (mood != null) mood.enabled = pass == 0;
                if (pass == 1) typeof(AudioMood).GetProperty("EchoAmount").GetSetMethod(true).Invoke(null, new object[] { 0f }); // echo off
                yield return new WaitForSeconds(0.3f);
                tap.Begin();
                GameAudio.PlaySfx("wood_step_2", 0.9f); // a footstep, the sound that matters most here
                yield return new WaitForSeconds(1.4f);
                var e = tap.End();
                float tail = 1e-12f;
                foreach (var b in e) if (b.t > 0.12f && b.t < 0.8f) tail += b.e;
                tails[pass] = tail;
            }
            foreach (var z in FindObjectsByType<AudioReverbZone>(FindObjectsSortMode.None)) z.enabled = true;
            var m2 = FindAnyObjectByType<AudioMood>(); if (m2 != null) m2.enabled = true;
            Debug.Log($"[Lab] reverb {name}: echo after the sound {10f * Mathf.Log10(tails[0] / tails[1]):F1} dB louder than without reverb");
        }
    }

    /// <summary>Photos of every room in play mode (runtime lighting, gradings, HUD hidden) into ../_work/lab/rooms.</summary>
    IEnumerator Rooms()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene == "Main")
        {
            // the title screen as the player sees it, from its own camera
            yield return new WaitForSeconds(5f);
            var titleCam = FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.enabled);
            string tdir = System.IO.Path.Combine(OutDir, "rooms");
            System.IO.Directory.CreateDirectory(tdir);
            var trt = new RenderTexture(1920, 1080, 24) { antiAliasing = 8 };
            titleCam.targetTexture = trt; titleCam.Render(); titleCam.targetTexture = null;
            RenderTexture.active = trt;
            var ttex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            ttex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); ttex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(tdir, "00_title.jpg"), ttex.EncodeToJPG(93));
            Debug.Log("[Lab] room shot 00_title");
            yield break;
        }
        for (float t = 0f; t < 60f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        yield return new WaitForSeconds(1f);
        var mover = FindAnyObjectByType<Movement>();
        if (mover != null) { var body = mover.transform.Find("PlayerBody"); if (body != null) body.gameObject.SetActive(false); }
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) { c.enabled = false; if (c.CompareTag("MainCamera")) c.tag = "Untagged"; }
        var cam = MakeCam(60f);
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
        data.renderPostProcessing = true;
        data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        string dir = System.IO.Path.Combine(OutDir, "rooms");
        System.IO.Directory.CreateDirectory(dir);

        IEnumerator Take(string name, Vector3 pos, Vector3 look, float fov = 60f, float settle = 2.5f)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
            for (float t = 0f; t < settle; t += Time.deltaTime) yield return null; // the zone gradings ease in
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".jpg"), tex.EncodeToJPG(93));
            Destroy(tex); rt.Release(); Destroy(rt);
            Debug.Log($"[Lab] room shot {name} zone={StorageZone.Current:F2} spill={StorageZone.Spill:F2}");
        }

        if (scene == "Level_1")
        {
            yield return Take("01_bedroom", new Vector3(6.3f, 3.0f, 9.8f), new Vector3(-3.5f, 1.2f, -0.5f));
            yield return Take("02_bedroom_desk", new Vector3(1.2f, 2.7f, 6.5f), new Vector3(3.6f, 1.6f, -2.4f), 55f);
            yield return Take("03_kitchen", new Vector3(10.5f, 3.0f, 2.5f), new Vector3(20f, 1.4f, 7.5f));
            yield return Take("04_living_room", new Vector3(17.5f, 3.0f, 14.5f), new Vector3(9.5f, 1.6f, 22.5f));
            yield return Take("05_living_cabinet", new Vector3(13.6f, 3.3f, 21.5f), new Vector3(13.6f, 3.9f, 25.4f), 50f);
            // the storage door from the living room: shut, then while and after it opens
            DoorController door = null; float best = 3f;
            foreach (var d in FindObjectsByType<DoorController>(FindObjectsSortMode.None))
            {
                float dist = Vector2.Distance(new Vector2(d.Hinge.position.x, d.Hinge.position.z), new Vector2(22.6f, 22.8f));
                if (dist < best) { best = dist; door = d; }
            }
            Debug.Log($"[Lab] storage door = {(door != null ? door.transform.root.name + "/" + door.name : "none")} at {best:F2} m");
            Vector3 spillPos = new Vector3(17.2f, 2.8f, 21.0f), spillLook = new Vector3(23.5f, 1.8f, 22.8f);
            yield return Take("06_storage_door_shut", spillPos, spillLook, 60f, 3f);
            if (door != null) door.OpenDoor();
            yield return Take("07_storage_door_opening", spillPos, spillLook, 60f, 1.2f);
            yield return Take("08_storage_door_open", spillPos, spillLook, 60f, 4f);
            yield return Take("08b_storage_door_open_near", new Vector3(19.6f, 2.8f, 21.6f), spillLook, 60f, 2.5f);
            yield return Take("09_storage_doorway", new Vector3(21.2f, 2.8f, 22.6f), new Vector3(30f, 1.4f, 17f), 60f, 3f);
            yield return Take("10_fuse_room", new Vector3(36.6f, 3.3f, 24.6f), new Vector3(25f, 1.2f, 6f), 62f);
            yield return Take("11_fusebox", new Vector3(27.2f, 2.6f, 8.6f), new Vector3(23.3f, 3.2f, 8.5f), 55f);
            yield return Take("12_storeroom", new Vector3(34.5f, 3.0f, 26.6f), new Vector3(25f, 1.4f, 38f));
            yield return Take("13_library", new Vector3(40.5f, 3.0f, 22.5f), new Vector3(48f, 1.4f, 13f));
            yield return Take("14_levers", new Vector3(26.5f, 2.8f, 15.3f), new Vector3(22.9f, 2.9f, 15.3f), 50f);
            // with the power back on (the fuses picked up and placed, as in the smoke test)
            foreach (var f in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)) { f.Interact(); yield return new WaitForSeconds(0.2f); }
            var key = FindAnyObjectByType<KeyPickup>(); if (key != null) key.Interact();
            yield return null;
            foreach (var k in FindObjectsByType<KeyDoorInteractable>(FindObjectsSortMode.None)) { if (k.canInteract) k.Interact(); yield return new WaitForSeconds(0.2f); }
            foreach (var p in FindObjectsByType<PlaceItemInteractable>(FindObjectsSortMode.None)) { p.Interact(); yield return new WaitForSeconds(0.2f); }
            for (float t = 0f; t < 25f && CutscenePlayer.IsPlaying; t += Time.deltaTime) yield return null;
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c != cam) c.enabled = false;
            cam.enabled = true;
            Debug.Log($"[Lab] powered={FindAnyObjectByType<LeverPuzzle>()?.IsPowered}");
            yield return Take("15_fuse_room_powered", new Vector3(36.6f, 3.3f, 24.6f), new Vector3(25f, 1.2f, 6f), 62f, 4f);
            yield return Take("16_storeroom_powered", new Vector3(34.5f, 3.0f, 26.6f), new Vector3(25f, 1.4f, 38f));
            yield return Take("17_library_powered", new Vector3(40.5f, 3.0f, 22.5f), new Vector3(48f, 1.4f, 13f));
            yield return Take("18_doorway_powered", new Vector3(21.2f, 2.8f, 22.6f), new Vector3(30f, 1.4f, 17f));
        }
        else if (scene == "WhiteSpace")
        {
            yield return Take("20_white_space", new Vector3(0f, 1.7f, 0.5f), new Vector3(0f, 2.3f, 14f));
            yield return Take("21_white_space_wide", new Vector3(-9f, 3.5f, -6f), new Vector3(0f, 2.5f, 12f));
        }
        else
        {
            yield return Take("30_confrontation", new Vector3(0f, 2.4f, 0.3f), new Vector3(0f, 1.9f, 6f));
            yield return Take("31_confrontation_wide", new Vector3(-6f, 3.5f, -5f), new Vector3(0f, 1.5f, 6f));
        }
    }

    /// <summary>The ending door after the levers: what is lit around it and how it looks shut, opening and open.</summary>
    IEnumerator EndDoor()
    {
        for (float t = 0f; t < 60f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        foreach (var f in FindObjectsByType<ItemPickup>(FindObjectsSortMode.None)) { f.Interact(); yield return new WaitForSeconds(0.2f); }
        var key = FindAnyObjectByType<KeyPickup>(); if (key != null) key.Interact();
        yield return null;
        foreach (var k in FindObjectsByType<KeyDoorInteractable>(FindObjectsSortMode.None)) { if (k.canInteract) k.Interact(); yield return new WaitForSeconds(0.2f); }
        foreach (var p in FindObjectsByType<PlaceItemInteractable>(FindObjectsSortMode.None)) { p.Interact(); yield return new WaitForSeconds(0.2f); }
        for (float t = 0f; t < 25f && CutscenePlayer.IsPlaying; t += Time.deltaTime) yield return null;
        yield return new WaitForSeconds(1f);
        foreach (var l in FindObjectsByType<Lever>(FindObjectsSortMode.None).OrderBy(l => l.symbolIndex)) { l.Interact(); yield return new WaitForSeconds(0.4f); }
        yield return new WaitForSeconds(1f);
        for (float t = 0f; t < 25f && CutscenePlayer.IsPlaying; t += Time.deltaTime) yield return null;
        yield return new WaitForSeconds(1f);
        var ending = GameObject.Find("ToEnding");
        Debug.Log($"[Lab] solved={FindAnyObjectByType<LeverPuzzle>()?.IsSolved} ending={(ending != null)}");
        if (ending == null) yield break;
        var door = ending.GetComponentInChildren<Door>(true);
        Vector3 at = door.transform.position;
        foreach (var r in ending.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
                if (m != null) Debug.Log($"[Lab] renderer {r.name} active={r.gameObject.activeInHierarchy} mat={m.name} shader={m.shader.name} base={(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")} emis={(m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor").ToString() : "-")}");
        foreach (var li in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (Vector3.Distance(li.transform.position, at) < 8f) Debug.Log($"[Lab] light {li.name} parent={li.transform.parent?.name} type={li.type} col={li.color} int={li.intensity:F2} range={li.range:F1} on={li.enabled && li.gameObject.activeInHierarchy} dist={Vector3.Distance(li.transform.position, at):F1}");
        Debug.Log($"[Lab] door at {at} forward {door.transform.forward} root fwd {ending.transform.forward}");
        var mover = FindAnyObjectByType<Movement>();
        if (mover != null) { var body = mover.transform.Find("PlayerBody"); if (body != null) body.gameObject.SetActive(false); }
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) { c.enabled = false; if (c.CompareTag("MainCamera")) c.tag = "Untagged"; }
        var cam = MakeCam(60f);
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam).renderPostProcessing = true;
        string dir = System.IO.Path.Combine(OutDir, "enddoor");
        System.IO.Directory.CreateDirectory(dir);
        IEnumerator Take(string name, Vector3 pos, Vector3 look, float settle)
        {
            cam.transform.position = pos; cam.transform.LookAt(look);
            for (float t = 0f; t < settle; t += Time.deltaTime) yield return null;
            var rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".jpg"), tex.EncodeToJPG(90));
            Destroy(tex); rt.Release(); Destroy(rt);
            Debug.Log($"[Lab] shot {name}");
        }
        Vector3 eye = at + Vector3.up * 1.6f;
        Vector3 f1 = door.transform.forward; f1.y = 0f; f1.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, f1);
        yield return Take("a_front", eye + f1 * 3.5f, eye, 2f);
        yield return Take("b_back", eye - f1 * 3.5f, eye, 0.5f);
        var through = FindObjectsByType<EnterThrough>(FindObjectsSortMode.None).FirstOrDefault();
        Debug.Log($"[Lab] enterThrough={(through != null)} leaf={(through != null && through.leaf != null)}");
        // what the player's aim hits from in front of the door, as PlayerInteract sees it
        foreach (var h in Physics.RaycastAll(new Ray(eye + f1 * 2f, -f1), 4f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            Debug.Log($"[Lab] aim hit {h.collider.name} (root {h.collider.transform.root.name}) d={h.distance:F2} interactables=[{string.Join(",", h.collider.GetComponents<Interactable>().Select(i => i.GetType().Name + ":" + i.canInteract))}]");
        if (System.Environment.GetEnvironmentVariable("LAB_ENTER") == "1")
        {
            var heard = new System.Collections.Generic.List<string>();
            GameAudio.Played += n => heard.Add($"{Time.time:F2}:{n}");
            var hitFront = Physics.RaycastAll(new Ray(eye + f1 * 2f, -f1), 4f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).First();
            foreach (var i in hitFront.collider.GetComponents<Interactable>()) if (i.canInteract) i.Interact();
            Debug.Log($"[Lab] pressed E at {Time.time:F2}");
            for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                var playing = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(s => s.isPlaying && s.clip != null && s.clip.name.StartsWith("door")).Select(s => $"{s.clip.name} vol={s.volume:F2} t={s.time:F2}");
                if ((int)(t * 10) % 3 == 0) Debug.Log($"[Lab] t={t:F1} scene={SceneManager.GetActiveScene().name} listener={AudioListener.volume:F2} door sources: {string.Join("; ", playing)}");
            }
            Debug.Log($"[Lab] heard: {string.Join(" ", heard)}");
            Debug.Log("[Lab] shot h_back_near");
            yield break;
        }
        if (through != null && through.leaf != null) through.leaf.OpenDoor();
        yield return Take("c_front_opening", eye + f1 * 3.5f, eye, 0.35f);
        yield return Take("d_front_open", eye + f1 * 3.5f, eye, 1.5f);
        yield return Take("e_back_open", eye - f1 * 3.5f, eye, 0.3f);
        yield return Take("f_front_near", eye + f1 * 1.6f, eye, 0.3f);
        yield return Take("g_side", eye + f1 * 2.5f + side * 2.5f, eye, 0.3f);
        yield return Take("h_back_near", eye - f1 * 1.6f, eye, 0.3f);
    }

    /// <summary>The real waking cutscene: frames from its own camera every 0.4 s, cropped around her head.</summary>
    IEnumerator WakeFace()
    {
        // the driver starts after the cutscene began; grab while it plays
        int n = 0;
        for (float t = 0f; t < 12f && n < 24; t += 0.4f)
        {
            yield return new WaitForSeconds(0.4f);
            var cam = FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null);
            var view = FindObjectsByType<EchoCharacterView>(FindObjectsSortMode.None).FirstOrDefault(v => v.transform.root.GetComponent<Movement>() == null);
            if (cam == null || view == null) continue;
            // LAB_KEY=off|side|<x,y,z>: try another place for her key light
            string keyVariant = System.Environment.GetEnvironmentVariable("LAB_KEY");
            var key = view.transform.root.Find("KeyLight");
            if (key != null && !string.IsNullOrEmpty(keyVariant))
            {
                if (keyVariant == "off") key.gameObject.SetActive(false);
                else
                {
                    var f = keyVariant.Split(',');
                    key.localPosition = new Vector3(float.Parse(f[0]), float.Parse(f[1]), float.Parse(f[2]));
                    key.LookAt(view.transform.root.TransformPoint(new Vector3(0f, 1.25f, 0f)));
                }
            }
            var anim = view.GetComponentInParent<Animator>() ?? view.transform.root.GetComponentInChildren<Animator>();
            var headBone = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (headBone == null) continue;
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            var prev = cam.targetTexture;
            // LAB_TELE=k: the same framing from k times further with a narrower lens
            Vector3 camPos0 = cam.transform.position; float fov0 = cam.fieldOfView;
            if (float.TryParse(System.Environment.GetEnvironmentVariable("LAB_TELE"), out float tele) && tele > 1f)
            {
                float dist = Vector3.Dot(headBone.position - camPos0, cam.transform.forward);
                cam.transform.position = camPos0 - cam.transform.forward * dist * (tele - 1f);
                cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(fov0 * 0.5f * Mathf.Deg2Rad) / tele) * Mathf.Rad2Deg;
            }
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            cam.transform.position = camPos0; cam.fieldOfView = fov0;
            RenderTexture.active = rt;
            var full = new Texture2D(w, h, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, w, h), 0, 0); full.Apply();
            RenderTexture.active = null; rt.Release();
            Vector3 sp = cam.WorldToViewportPoint(headBone.position);
            float headPx = Mathf.Abs(cam.WorldToViewportPoint(headBone.position + cam.transform.up * 0.5f * view.transform.root.lossyScale.y).y - sp.y) * h;
            int size = Mathf.Clamp(Mathf.RoundToInt(headPx * 1.6f), 64, h);
            int cx = Mathf.Clamp(Mathf.RoundToInt(sp.x * w) - size / 2, 0, w - size), cy = Mathf.Clamp(Mathf.RoundToInt(sp.y * h) - size / 2, 0, h - size);
            if (sp.z <= 0f) continue;
            var crop = new Texture2D(size, size, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(cx, cy, size, size)); crop.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, $"wake_{n:00}.jpg"), crop.EncodeToJPG(92));
            Debug.Log($"[Lab] wake frame {n} t={Time.time:F1} cam={cam.name} fov={cam.fieldOfView:F0} camPos={cam.transform.position} head={headBone.position} headLocalRot={headBone.localEulerAngles} size={size}");
            n++;
            Destroy(full); Destroy(crop); Destroy(rt);
        }
    }

    /// <summary>Close-ups of her face where the waking cutscene stands her, under the bedroom lamps, with variants.</summary>
    IEnumerator Chin()
    {
        yield return new WaitForSeconds(2f);
        for (float t = 0f; t < 40f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        var mover = FindAnyObjectByType<Movement>();
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        var cam = MakeCam(30f);
        cam.clearFlags = CameraClearFlags.Skybox;
        UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam).renderPostProcessing = true;
        Vector3 feet = CharacterLibrary.GroundBelow(new Vector3(0.6f, 2f, 1.5f));
        if (mover != null) feet = CharacterLibrary.GroundBelow(mover.transform.position);
        Vector3 toRoom = Vector3.ProjectOnPlane(new Vector3(0.2f, 0f, 4.1f) - feet, Vector3.up).normalized;
        var hero = CharacterLibrary.Spawn(false, feet, toRoom);
        hero.transform.root.position = feet;
        float sc = PlayerAvatar.ModelScale;
        Vector3 head = feet + Vector3.up * 1.4f * sc;
        cam.transform.position = head + toRoom * 1.6f + Vector3.up * 0.05f;
        cam.transform.LookAt(head - Vector3.up * 0.12f * sc);
        yield return new WaitForSeconds(1f);
        var rends = hero.transform.root.GetComponentsInChildren<Renderer>();
        Shot(cam, "chin_a_now", 700, 700);
        foreach (var r in rends) foreach (var m in r.materials) if (m.name.ToLower().Contains("body")) m.SetFloat("_ShadowStrength", 0f);
        yield return null;
        Shot(cam, "chin_b_skin_no_shadow", 700, 700);
        foreach (var r in rends) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        yield return null;
        Shot(cam, "chin_c_no_self_shadow", 700, 700);
        foreach (var r in rends) foreach (var m in r.materials) if (m.name.ToLower().Contains("body")) m.SetFloat("_ShadeAmount", 0.35f);
        yield return null;
        Shot(cam, "chin_d_less_shade", 700, 700);
        foreach (var r in rends) Debug.Log($"[Lab] hero renderer {r.name} mats={string.Join(",", r.sharedMaterials.Select(m => m.name))}");
    }

    /// <summary>Walks the player across the living room and photographs her shadow at each step.</summary>
    IEnumerator ShadowWalk()
    {
        yield return new WaitForSeconds(4f);
        for (float t = 0f; t < 40f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        var mover = FindAnyObjectByType<Movement>();
        var focus = mover != null ? mover.GetComponent<ShadowFocus>() : null;
        if (focus == null) { Debug.Log("[Lab] no player/ShadowFocus"); yield break; }
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        var cam = MakeCam(60f);
        cam.clearFlags = CameraClearFlags.Skybox;
        var cc = mover.GetComponent<CharacterController>();
        float y = mover.transform.position.y;
        for (int i = 0; i < 6; i++)
        {
            if (cc != null) cc.enabled = false;
            mover.transform.position = new Vector3(8f + i * 1.6f, y, 12.5f);
            mover.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            if (cc != null) cc.enabled = true;
            for (float t = 0f; t < 2f; t += Time.deltaTime) yield return null;
            Vector3 feet = mover.transform.position;
            cam.transform.position = new Vector3(feet.x - 0.5f, feet.y + 2.9f, feet.z - 3.2f);
            cam.transform.LookAt(new Vector3(feet.x, CharacterLibrary.GroundBelow(feet).y, feet.z));
            Debug.Log($"[Lab] shadow step {i} at {feet} planar={focus.Planar} slope={focus.Slope} alpha={focus.Alpha:F2}");
            Shot(cam, $"shadowwalk_{i}", 800, 600);
        }
    }

    IEnumerator Player()
    {
        yield return new WaitForSeconds(4f);
        // wait for any opening/waking cutscene, so the player's own body (not a cutscene actor) is shown
        for (float t = 0f; t < 40f && (CutscenePlayer.IsPlaying || GameState.IsCutscene); t += Time.deltaTime) yield return null;
        yield return new WaitForSeconds(0.5f);
        var mover = FindAnyObjectByType<Movement>();
        if (mover == null) { Debug.Log("[Lab] no player"); yield break; }
        {
            var cc0 = mover.GetComponent<CharacterController>();
            var eyeCam = mover.GetComponentInChildren<Camera>(true);
            float floorY = CharacterLibrary.GroundBelow(mover.transform.position).y;
            var bodyR = mover.GetComponentsInChildren<Renderer>().Where(r => r.transform.root == mover.transform && r.name != mover.name).ToList();
            float top = bodyR.Count > 0 ? bodyR.Max(r => r.bounds.max.y) : 0f, bottom = bodyR.Count > 0 ? bodyR.Min(r => r.bounds.min.y) : 0f;
            Debug.Log($"[Lab] SIZE player scale={mover.transform.lossyScale} cc h={cc0?.height} c={cc0?.center} r={cc0?.radius} eye above floor={(eyeCam != null ? eyeCam.transform.position.y - floorY : -1):F2} ModelScale={PlayerAvatar.ModelScale:F2} body height={top - bottom:F2} (feet {bottom - floorY:F2} above floor)");
            foreach (var d in FindObjectsByType<InteractiveDoor>(FindObjectsSortMode.None).Take(6))
            {
                var rs = d.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                Debug.Log($"[Lab] SIZE door {d.name} height={b.size.y:F2} width={Mathf.Max(b.size.x, b.size.z):F2}");
            }
            foreach (var name in new[] { "Bed", "bed", "Table", "table", "Chair", "chair", "Desk", "desk", "Sofa", "sofa", "Fridge", "fridge" })
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.name.Contains(name)).Take(2))
                    Debug.Log($"[Lab] SIZE {r.name} height={r.bounds.size.y:F2} top={r.bounds.max.y - floorY:F2}");
        }
        foreach (var r in mover.GetComponentsInChildren<Renderer>(true))
            Debug.Log($"[Lab] player renderer {r.name} type={r.GetType().Name} enabled={r.enabled} active={r.gameObject.activeInHierarchy} shadows={r.shadowCastingMode} mesh={(r is MeshRenderer ? r.GetComponent<MeshFilter>()?.sharedMesh?.name : (r as SkinnedMeshRenderer)?.sharedMesh?.name)}");
        foreach (var c in mover.GetComponentsInChildren<Component>(true))
            if (c is Renderer || c is Projector || c.GetType().Name.Contains("Decal") || c.GetType().Name.Contains("Shadow")) Debug.Log($"[Lab] player component {c.name}:{c.GetType().Name}");
        // look at her shadow: from the player's eye down at the floor, and from above
        var eye = Camera.main;
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        var cam = MakeCam(70f);
        cam.clearFlags = CameraClearFlags.Skybox;
        var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
        data.renderPostProcessing = true;
        Vector3 feet = mover.transform.position;
        cam.transform.position = eye != null ? eye.transform.position : feet + Vector3.up * 1.6f;
        cam.transform.rotation = Quaternion.LookRotation(mover.transform.forward * 0.6f + Vector3.down, mover.transform.forward);
        Shot(cam, "player_lookdown");
        cam.transform.position = feet + Vector3.up * 3.2f - mover.transform.forward * 1.2f;
        cam.transform.LookAt(feet);
        Shot(cam, "player_above");

        // stand her in the middle of the living room, under its lamps
        var cc = mover.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        mover.transform.position = new Vector3(12f, mover.transform.position.y, 12.5f);
        if (cc != null) cc.enabled = true;
        feet = mover.transform.position;
        yield return new WaitForSeconds(0.6f);
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || Vector3.Distance(r.bounds.center, feet + Vector3.up) > 1.6f) continue;
            string path = r.name; for (var t = r.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            Debug.Log($"[Lab] near renderer {path} type={r.GetType().Name} shadows={r.shadowCastingMode} layer={r.gameObject.layer} bounds={r.bounds.center} {r.bounds.size} mats={string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"))} comps={string.Join(",", r.GetComponents<Component>().Select(c => c.GetType().Name))}");
        }
        // her shadow on the floor, with and without ShadowFocus (the nearest lamp gets sharp shadows)
        var lamps = FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type != LightType.Directional && l.shadows != LightShadows.None && l.isActiveAndEnabled)
            .OrderBy(l => Vector3.Distance(l.transform.position, feet)).ToList();
        var near = lamps.FirstOrDefault();
        if (near != null)
        {
            Vector3 away = Vector3.ProjectOnPlane(feet - near.transform.position, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.01f) away = mover.transform.forward;
            Vector3 shadowAt = feet + away * 1.2f;
            Debug.Log($"[Lab] nearest lamp {near.name} at {Vector3.Distance(near.transform.position, feet):F1} m range {near.range}");
            cam.transform.position = feet + Vector3.up * 2.3f - away * 0.6f;
            cam.transform.LookAt(shadowAt);
            cam.fieldOfView = 75f;
            var focus = mover.GetComponent<ShadowFocus>();
            if (focus != null)
            {
                focus.enabled = false;
                var field = typeof(UnityEngine.Rendering.Universal.UniversalAdditionalLightData).GetField("m_AdditionalLightsShadowResolutionTier", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                foreach (var l in lamps) { var d = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(); if (d != null) field.SetValue(d, 2); }
                yield return null; yield return null;
                Shot(cam, "shadow_before");
                focus.enabled = true;
                foreach (var l in lamps) { var d = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(); if (d != null) field.SetValue(d, 0); }
                yield return new WaitForSecondsRealtime(0.4f);
                yield return null;
                foreach (var l in lamps.Take(3)) { var d = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(); Debug.Log($"[Lab] tier {l.name} = {field.GetValue(d)}"); }
            }
            Shot(cam, "shadow_after");
            // straight down on her: one shadow now; for comparison, every lamp drawing her shadow (as before)
            cam.fieldOfView = 70f;
            cam.transform.position = feet + Vector3.up * 3.6f;
            cam.transform.rotation = Quaternion.LookRotation(Vector3.down, mover.transform.forward);
            yield return new WaitForSecondsRealtime(0.3f); yield return null;
            Shot(cam, "shadow_top_one");
            if (focus != null)
            {
                focus.enabled = false;
                foreach (var l in lamps) { var d = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(); if (d != null) d.shadowRenderingLayers = (uint)d.shadowRenderingLayers | ShadowFocus.BodyLayer; }
                yield return null; yield return null;
                Shot(cam, "shadow_top_all");
            }
        }
    }

    /// <summary>Colour palettes from ../_work/palettes/NAME/{Hair,Eye,Ribbon}.png, shown on her in game.</summary>
    IEnumerator Palettes()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        GameState.IsCutscene = true;
        var cam = MakeCam(30f);
        var hero = CharacterLibrary.Spawn(false, Vector3.zero, Vector3.back);
        yield return new WaitForSeconds(0.6f);
        var root = hero.transform.root;
        float sc = root.localScale.y;
        var rends = root.GetComponentsInChildren<Renderer>();
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../_work/palettes"));
        var names = new System.Collections.Generic.List<string> { "0_Current" };
        names.AddRange(System.IO.Directory.GetDirectories(dir).Select(System.IO.Path.GetFileName).OrderBy(n => n));
        // hair shade / highlight / outline tones per palette (tone.txt: 9 numbers)
        (Color shade, Color spec, Color outline)? Tone(string pal)
        {
            string f = System.IO.Path.Combine(dir, pal, "tone.txt");
            if (!System.IO.File.Exists(f)) return null;
            var v = System.IO.File.ReadAllText(f).Split(' ').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return (new Color(v[0], v[1], v[2]), new Color(v[3], v[4], v[5]), new Color(v[6], v[7], v[8]));
        }
        Texture2D Load(string pal, string file)
        {
            string f = System.IO.Path.Combine(dir, pal, file + ".png");
            if (!System.IO.File.Exists(f)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            t.LoadImage(System.IO.File.ReadAllBytes(f));
            return t;
        }
        void View(string name, Vector3 offset, Vector3 look, float fov)
        {
            cam.fieldOfView = fov;
            cam.transform.position = root.position + root.rotation * offset * sc;
            cam.transform.LookAt(root.position + root.rotation * look * sc);
            Shot(cam, name, 900, 900);
        }
        foreach (var pal in names)
        {
            var hairTex = Load(pal, "Hair"); var eyeTex = Load(pal, "Eye"); var ribbonTex = Load(pal, "Ribbon");
            var parts = new System.Collections.Generic.Dictionary<string, Texture2D>
                { { "Glasses", Load(pal, "Glasses") }, { "Earrings", Load(pal, "Earring") }, { "Shoes", Load(pal, "Shoe") }, { "Body", Load(pal, "Skin") } };
            string skinTone = System.IO.Path.Combine(dir, pal, "skintone.txt");
            Color? bodyShade = null;
            if (System.IO.File.Exists(skinTone))
            {
                var v = System.IO.File.ReadAllText(skinTone).Split(' ').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                bodyShade = new Color(v[0], v[1], v[2]);
            }
            foreach (var r in rends)
            {
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                if ((r.name == "Hair" || r.name == "HairBase") && hairTex != null)
                {
                    mpb.SetTexture("_BaseMap", hairTex);
                    var tone = Tone(pal);
                    if (tone.HasValue)
                    {
                        mpb.SetColor("_ShadeColor", tone.Value.shade);
                        mpb.SetColor("_SpecColor", tone.Value.spec);
                        mpb.SetColor("_OutlineColor", tone.Value.outline);
                        mpb.SetFloat("_SpecStrength", 0.55f);
                    }
                }
                if (r.name == "Eyes" && eyeTex != null) mpb.SetTexture("_BaseMap", eyeTex);
                if (r.name == "Ribbon" && ribbonTex != null) mpb.SetTexture("_BaseMap", ribbonTex);
                if (parts.TryGetValue(r.name, out var pt) && pt != null) mpb.SetTexture("_BaseMap", pt);
                if (r.name == "Body" && bodyShade.HasValue) mpb.SetColor("_ShadeColor", bodyShade.Value);
                r.SetPropertyBlock(mpb);
            }
            yield return null;
            View($"pal_{pal}_face", new Vector3(0.12f, 1.42f, 0.8f), new Vector3(0, 1.36f, 0), 30f);
            View($"pal_{pal}_full", new Vector3(0.9f, 1.0f, 3.2f), new Vector3(0, 0.82f, 0), 32f);
            View($"pal_{pal}_side", new Vector3(0.62f, 1.4f, 0.42f), new Vector3(0, 1.36f, 0), 30f);
            View($"pal_{pal}_shoes", new Vector3(0.45f, 0.3f, 0.75f), new Vector3(0, 0.12f, 0), 32f);
            foreach (var r in rends) r.SetPropertyBlock(null);
        }
    }

    /// <summary>Frame cost of the current scene (CPU + GPU, rendered at 1920x1080), with things switched off one by one.</summary>
    IEnumerator Perf()
    {
        yield return new WaitForSeconds(3f);
        var cam = Camera.main;
        if (cam == null) { Debug.Log("[Lab] perf: no camera"); yield break; }
        var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 1 };
        var tex1 = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        var camData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
        var chars = FindObjectsByType<EchoCharacters.EchoCharacterView>(FindObjectsSortMode.None).Select(v => v.transform.root.gameObject).Distinct().ToList();
        Debug.Log($"[Lab] perf scene={SceneManager.GetActiveScene().name} characters={chars.Count} lights={FindObjectsByType<Light>(FindObjectsSortMode.None).Length} shadowLights={FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.shadows != LightShadows.None)} renderers={FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length} atlas={urp?.additionalLightsShadowmapResolution}");

        IEnumerator Measure(string label)
        {
            for (int i = 0; i < 10; i++) { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; yield return null; }
            var sw = new System.Diagnostics.Stopwatch();
            int n = 60; double total = 0, worst = 0;
            for (int i = 0; i < n; i++)
            {
                sw.Restart();
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                RenderTexture.active = rt; tex1.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = null; // wait for the GPU
                double ms = sw.Elapsed.TotalMilliseconds;
                total += ms; worst = System.Math.Max(worst, ms);
                yield return null;
            }
            Debug.Log($"[Lab] perf {label}: avg {total / n:F1} ms, worst {worst:F1} ms (render + GPU) | frame {Time.unscaledDeltaTime * 1000f:F1} ms");
        }

        yield return Measure("baseline");
        var springs = FindObjectsByType<EchoCharacters.EchoSecondaryMotion>(FindObjectsSortMode.None);
        foreach (var sp in springs) sp.enabled = false;
        yield return Measure("no secondary motion");
        foreach (var sp in springs) sp.enabled = true;
        foreach (var c in chars) c.SetActive(false);
        yield return Measure("characters hidden");
        foreach (var c in chars) c.SetActive(true);
        if (urp != null)
        {
            int keep = urp.additionalLightsShadowmapResolution;
            urp.additionalLightsShadowmapResolution = 2048;
            yield return Measure("shadow atlas 2048");
            urp.additionalLightsShadowmapResolution = keep;
        }
        bool pp = camData.renderPostProcessing;
        camData.renderPostProcessing = false;
        yield return Measure("no post-processing");
        camData.renderPostProcessing = pp;
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.shadows != LightShadows.None).ToList();
        var modes = lights.Select(l => l.shadows).ToList();
        foreach (var l in lights) l.shadows = LightShadows.None;
        yield return Measure("no shadows");
        for (int i = 0; i < lights.Count; i++) lights[i].shadows = modes[i];
        rt.Release();
    }

    /// <summary>How long spawning a character takes (the hitch when a cutscene starts), and the frames after.</summary>
    IEnumerator SpawnCost()
    {
        yield return new WaitForSeconds(2f);
        var cam = Camera.main;
        var rt = new RenderTexture(1920, 1080, 24);
        for (int i = 0; i < 3; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var v = CharacterLibrary.Spawn(false, new Vector3(5f + i, 0f, 4f), Vector3.forward);
            double spawn = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            if (cam != null) { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            double firstRender = sw.Elapsed.TotalMilliseconds;
            Debug.Log($"[Lab] spawn #{i}: Spawn {spawn:F1} ms, first render after it {firstRender:F1} ms");
            yield return null;
            Destroy(v.transform.root.gameObject);
            yield return null;
        }
        rt.Release();
    }

    /// <summary>The phone's hierarchy and every visible UI graphic near the screen edges.</summary>
    IEnumerator UiDump()
    {
        yield return new WaitForSeconds(5f);
        string Path(Transform t) { string p = t.name; for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p; return p; }
        var pm = FindAnyObjectByType<PhoneManager>();
        if (pm != null)
            foreach (var f in new[] { "phonePanel", "lockScreenPanel", "notificationText" })
            {
                var v = pm.GetType().GetField(f, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(pm);
                var tr = v is Component c ? c.transform : v is GameObject g ? g.transform : null;
                Debug.Log($"[Lab] ui phone field {f} = {(tr != null ? Path(tr) + " active=" + tr.gameObject.activeInHierarchy : "null")}");
            }
        foreach (var q in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x.name == "QuestText"))
        {
            var rt = q.rectTransform; var cs = q.canvas != null ? q.canvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
            Debug.Log($"[Lab] ui QUEST anchorMin={rt.anchorMin} anchorMax={rt.anchorMax} pivot={rt.pivot} pos={rt.anchoredPosition} size={rt.sizeDelta} font={q.font?.name} size={q.fontSize} auto={q.enableAutoSizing} align={q.alignment} color={q.color} scaler={cs?.uiScaleMode} scale={cs?.scaleFactor} ref={cs?.referenceResolution} canvasScale={q.canvas?.scaleFactor} parent={rt.parent?.name}");
        }
        foreach (var g in FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsSortMode.None))
        {
            if (!g.isActiveAndEnabled || g.canvas == null || g.color.a < 0.05f) continue;
            var corners = new Vector3[4]; g.rectTransform.GetWorldCorners(corners);
            if (g.canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            float minY = corners.Min(c => c.y), maxX = corners.Max(c => c.x);
            string txt = g is TMPro.TMP_Text t ? " text='" + t.text.Replace('\n', ' ').Substring(0, Mathf.Min(40, t.text.Length)) + "'" : "";
            Debug.Log($"[Lab] ui graphic {Path(g.transform)} rect=({corners[0].x:F0},{corners[0].y:F0})-({corners[2].x:F0},{corners[2].y:F0}) screen={Screen.width}x{Screen.height}{txt}");
        }
    }

    IEnumerator Character()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        GameState.IsCutscene = true;
        var cam = MakeCam(30f);
        Vector3 p = new Vector3(0f, 0f, 0f);
        var hero = CharacterLibrary.Spawn(false, p, Vector3.back);
        if (System.Environment.GetCommandLineArgs().Contains("-noSkirtSpring"))
        {
            foreach (var sp in hero.transform.root.GetComponentsInChildren<EchoSecondaryMotion>()) sp.enabled = false;
        }
        yield return new WaitForSeconds(0.5f);
        var root = hero.transform.root;
        float s = root.localScale.y;
        Vector3 head = root.position + Vector3.up * 1.45f * s;

        void View(string name, Vector3 offset, Vector3 look, float fov)
        {
            cam.fieldOfView = fov;
            cam.transform.position = root.position + root.rotation * offset * s;
            cam.transform.LookAt(root.position + root.rotation * look * s);
            Shot(cam, name);
        }

        View("char_face", new Vector3(0.1f, 1.42f, 0.75f), new Vector3(0, 1.38f, 0), 30f);
        View("char_face_side", new Vector3(0.7f, 1.42f, 0.25f), new Vector3(0, 1.38f, 0), 30f);
        View("char_full", new Vector3(0.3f, 0.9f, 3.6f), new Vector3(0, 0.8f, 0), 30f);
        View("stand_head_side", new Vector3(1.2f, 1.35f, 0f), new Vector3(0, 1.3f, 0), 30f);
        View("char_chin", new Vector3(0.0f, 1.3f, 0.45f), new Vector3(0, 1.36f, 0), 30f);
        // which mesh is which: flat colours per part, and the same view without outlines
        var rends = root.GetComponentsInChildren<Renderer>();
        var hues = new System.Collections.Generic.Dictionary<string, Color> {
            {"Body", new Color(1f,0.8f,0.6f)}, {"Shirt", Color.white}, {"Hair", Color.green}, {"HairBase", Color.yellow},
            {"Skirt", Color.blue}, {"Ribbon", Color.red}, {"Eyes", Color.magenta}, {"Glasses", Color.cyan}, {"GlassesLens", Color.cyan},
            {"Buttons", Color.black}, {"Earrings", Color.black}, {"Shoes", Color.gray} };
        foreach (var r in rends)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_BaseMap", Texture2D.whiteTexture);
            mpb.SetColor("_BaseColor", hues.TryGetValue(r.name, out var hc) ? hc : new Color(1f, 0.5f, 0f));
            r.SetPropertyBlock(mpb);
            Debug.Log($"[Lab] renderer {r.name} mats={string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"))} bounds={r.bounds.center} {r.bounds.size}");
        }
        View("char_ids", new Vector3(0.1f, 1.42f, 0.75f), new Vector3(0, 1.38f, 0), 30f);
        View("char_ids_side", new Vector3(0.6f, 1.3f, 0.45f), new Vector3(0, 1.3f, 0), 30f);
        foreach (var r in rends) r.SetPropertyBlock(null);
        var widths = new System.Collections.Generic.Dictionary<Material, float>();
        foreach (var r in rends) foreach (var m in r.sharedMaterials) if (m != null && m.HasProperty("_OutlineWidth") && !widths.ContainsKey(m)) { widths[m] = m.GetFloat("_OutlineWidth"); m.SetFloat("_OutlineWidth", 0f); }
        View("char_face_nooutline", new Vector3(0.1f, 1.42f, 0.75f), new Vector3(0, 1.38f, 0), 30f);
        View("char_chin_nooutline", new Vector3(0.0f, 1.3f, 0.45f), new Vector3(0, 1.36f, 0), 30f);
        foreach (var kv in widths) kv.Key.SetFloat("_OutlineWidth", kv.Value);
        // a dim room, like the house at night
        var sun = FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        float sunI = sun != null ? sun.intensity : 1f;
        if (sun != null) sun.intensity = 0.15f;
        View("char_face_dim", new Vector3(0.1f, 1.42f, 0.75f), new Vector3(0, 1.38f, 0), 30f);
        if (sun != null) sun.intensity = sunI;

        // walk past the camera: legs, skirt and hair in motion
        Vector3 a = root.position, b = root.position + root.forward * 4f;
        float dur = 4f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            CharacterLibrary.Walk(hero, a, b, t / dur, dur);
            if (Mathf.Abs(t - 1.2f) < Time.deltaTime)
            {
                View("walk_side", new Vector3(2.4f, 0.8f, 0f), new Vector3(0, 0.7f, 0), 30f);
                View("walk_head_side", new Vector3(1.2f, 1.35f, 0f), new Vector3(0, 1.3f, 0), 30f);
                var hair = root.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Hair");
                var mpb = new MaterialPropertyBlock(); if (hair != null) hair.GetPropertyBlock(mpb);
                Debug.Log($"[Lab] hair sway offset while walking: {mpb.GetVector("_SwayOffset")} anchor {mpb.GetVector("_SwayAnchor")}");
            }
            if (Mathf.Abs(t - 1.6f) < Time.deltaTime)
            {
                View("walk_low", new Vector3(1.2f, 0.35f, 0.9f), new Vector3(0, 0.6f, 0), 35f);
                View("walk_thigh_close", new Vector3(0.55f, 0.55f, 0.55f), new Vector3(0, 0.6f, 0), 35f);
            }
            if (Mathf.Abs(t - 2.0f) < Time.deltaTime) View("walk_back", new Vector3(0.4f, 1.2f, -2.4f), new Vector3(0, 0.9f, 0), 30f);
            if (Mathf.Abs(t - 2.4f) < Time.deltaTime) View("walk_front", new Vector3(0.3f, 1.0f, 2.6f), new Vector3(0, 0.9f, 0), 30f);
            yield return null;
        }
        yield return new WaitForSeconds(1.5f);
        // a quick turn: hair and skirt should swing
        Quaternion r0 = root.rotation;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            root.rotation = r0 * Quaternion.Euler(0, 160f * t / 0.35f, 0);
            yield return null;
        }
        View("turn_swing", new Vector3(1.8f, 1.1f, 1.8f), new Vector3(0, 1.0f, 0), 32f);
        View("turn_hair_back", new Vector3(0.0f, 1.35f, -1.1f), new Vector3(0, 1.3f, 0), 30f);
        yield return new WaitForSeconds(0.15f);
        View("turn_swing2", new Vector3(1.8f, 1.1f, 1.8f), new Vector3(0, 1.0f, 0), 32f);
        yield return new WaitForSeconds(1.5f);
        View("rest_hair_back", new Vector3(0.0f, 1.35f, -1.1f), new Vector3(0, 1.3f, 0), 30f);
    }
}
/// <summary>Taps the final mix on the listener (lab only).</summary>
public class AudioTap : MonoBehaviour
{
    readonly System.Collections.Generic.List<(float t, float e)> blocks = new System.Collections.Generic.List<(float, float)>();
    bool recording; double started; int rate;
    void Awake() => rate = AudioSettings.outputSampleRate;
    public void Begin() { lock (blocks) { blocks.Clear(); started = AudioSettings.dspTime; recording = true; } }
    public System.Collections.Generic.List<(float t, float e)> End() { lock (blocks) { recording = false; return new System.Collections.Generic.List<(float, float)>(blocks); } }
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!recording) return;
        double sum = 0; for (int i = 0; i < data.Length; i++) sum += data[i] * data[i];
        lock (blocks) blocks.Add(((float)(AudioSettings.dspTime - started), (float)sum));
    }
}
#endif
