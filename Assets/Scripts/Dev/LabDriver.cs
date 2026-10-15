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

    IEnumerator Character()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
        GameState.IsCutscene = true;
        var cam = MakeCam(30f);
        Vector3 p = new Vector3(0f, 0f, 0f);
        var hero = CharacterLibrary.Spawn(false, p, Vector3.back);
        if (System.Environment.GetCommandLineArgs().Contains("-noSkirtSpring"))
        {
            foreach (var sp in hero.transform.root.GetComponentsInChildren<SkirtSpring>()) sp.enabled = false;
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
            CharacterLibrary.Walk(hero, a, b, t / dur);
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
#endif
