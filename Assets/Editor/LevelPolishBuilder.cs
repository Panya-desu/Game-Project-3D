using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Polish pass on top of Panya's Level_1 (puzzles are not changed):
///   * colliders for meshes the player could walk through
///   * lighting mood per room (warm home, cold storage) + accent lights that point at clues
///   * storage power: dim + red emergency lights until the three fuses are in (LeverPuzzle.IsPowered)
///   * room ambience triggers
///   * quest list continued through the house and storage room, with hint dialogue
///   * the ending door leads to the Confrontation scene, DemoEnd text, build settings
/// Everything it adds lives under "__Polish" so it can be re-run.  Menu: Tools/Game Setup/Polish Level_1
/// </summary>
public static class LevelPolishBuilder
{
    const string Root = "__Polish";

    [MenuItem("Tools/Game Setup/Polish All")]
    public static void PolishAll()
    {
        PolishLevel1();
        LevelLightingBuilder.Build(); // atmosphere, grading and lamp glow on top of the polished lights
        CharacterSetup.Build();
        PolishWhiteSpaceText();
        PolishDemoEnd();
        SetupBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[LevelPolishBuilder] All done");
    }

    [MenuItem("Tools/Game Setup/Polish Level_1")]
    public static void PolishLevel1()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        var old = GameObject.Find(Root);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(Root).transform;

        EnsureColliders(GameObject.Find("Building"));
        var puzzle = Object.FindAnyObjectByType<LeverPuzzle>();

        // ── lighting ──
        var lights = new GameObject("Lighting").transform; lights.SetParent(root);
        var storageLights = new List<Light>();
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type != LightType.Point || l.transform.IsChildOf(root)) continue;
            Vector3 p = l.transform.position;
            if (InRoom1(p)) { l.color = new Color(1f, 0.8f, 0.6f); l.intensity = 3.2f; }
            else if (InRoom2(p)) { l.color = p.z > 15.8f ? new Color(1f, 0.82f, 0.62f) : new Color(1f, 0.93f, 0.82f); l.intensity = 3.4f; }
            else if (InStorage(p))
            {
                bool library = p.x > 37.6f;
                l.color = library ? new Color(1f, 0.8f, 0.58f) : new Color(0.78f, 0.88f, 1f);
                l.intensity = library ? 3.0f : 3.6f;
                if (!library && l.GetComponent<LightFlicker>() == null)
                {
                    var f = l.gameObject.AddComponent<LightFlicker>(); f.mode = LightFlicker.Mode.Fluorescent; f.strength = 0.3f;
                }
                storageLights.Add(l);
            }
            EditorUtility.SetDirty(l);
        }
        Spot(lights, "Clock_Highlight", new Vector3(3.4f, 4.2f, -0.6f), new Vector3(3.4f, 1.8f, -2.6f), 28f, 5f, 7f, new Color(0.85f, 0.92f, 1f));
        Spot(lights, "Cabinet_Highlight", new Vector3(13.6f, 5.6f, 23.0f), new Vector3(13.6f, 3.8f, 25.4f), 35f, 3f, 6f, new Color(1f, 0.94f, 0.84f));
        Spot(lights, "FuseBox_Highlight", new Vector3(25.5f, 5.0f, 8.5f), new Vector3(23.4f, 3.2f, 8.5f), 30f, 3f, 6f, new Color(1f, 0.6f, 0.4f));
        var tv = GameObject.Find("Building/Room2/QuestObject/Canvas/TV");
        if (tv != null)
        {
            var glow = Point(tv.transform, "TV_Glow", new Vector3(9.4f, 2.6f, 21.9f), new Color(0.5f, 0.7f, 1f), 3f, 5f);
            var f = glow.gameObject.AddComponent<LightFlicker>(); f.mode = LightFlicker.Mode.TvGlow; f.strength = 0.35f;
        }
        var emergency = new List<Light>();
        foreach (var p in new[] { new Vector3(30f, 5.2f, 14f), new Vector3(30f, 5.2f, 33f), new Vector3(45f, 5.2f, 18f), new Vector3(24.5f, 3.6f, 8.5f) })
        {
            var l = Point(lights, "Emergency_Red", p, new Color(1f, 0.12f, 0.08f), 2.2f, p.y < 4f ? 6f : 18f);
            var f = l.gameObject.AddComponent<LightFlicker>(); f.mode = LightFlicker.Mode.Pulse; f.strength = 0.6f; f.speed = 0.8f;
            l.enabled = false;
            emergency.Add(l);
        }
        var power = new GameObject("StoragePower").AddComponent<StoragePower>();
        power.transform.SetParent(root);
        power.puzzle = puzzle; power.roomLights = storageLights; power.emergencyLights = emergency;

        // ── ambience ──
        var triggers = new GameObject("AreaTriggers").transform; triggers.SetParent(root);
        Trigger(triggers, "Area_Bedroom", new Vector3(0.2f, 3f, 4.1f), new Vector3(14.8f, 6f, 14.8f), "amb_home", null);
        Trigger(triggers, "Area_House", new Vector3(15.2f, 3f, 13.3f), new Vector3(14.8f, 6f, 24.8f), "amb_home", null);
        var storage = Trigger(triggers, "Area_Storage", new Vector3(30.2f, 3f, 13.3f), new Vector3(14.6f, 6f, 24.6f), "amb_storage", puzzle);
        Trigger(triggers, "Area_Storeroom", new Vector3(30.2f, 3f, 33.3f), new Vector3(14.6f, 6f, 14.6f), "amb_storage", puzzle);
        Trigger(triggers, "Area_Library", new Vector3(45.2f, 3f, 18.3f), new Vector3(14.6f, 6f, 14.6f), "amb_storage", puzzle);
        storage.fireWhileInside = true; // the quest may become active after the player is already inside

        // ── ending door -> Confrontation ──
        var toEnding = GameObject.Find("ToEnding");
        GameObject endingDoor = null;
        if (toEnding != null)
        {
            var door = toEnding.GetComponentInChildren<Door>(true);
            if (door != null)
            {
                endingDoor = door.gameObject;
                var so = new SerializedObject(door);
                so.FindProperty("sceneName").stringValue = "Confrontation";
                so.FindProperty("interactText").stringValue = "Press E to step through";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ── quests ──
        var qm = Object.FindAnyObjectByType<QuestManager>();
        BuildQuests(qm, storage.gameObject, endingDoor, puzzle);
        storage.onEnter = new UnityEvent();
        UnityEventTools.AddObjectPersistentListener<GameObject>(storage.onEnter, qm.CompleteObjectiveForTarget, storage.gameObject);
        EditorUtility.SetDirty(storage);

        // Hint text on the Room 2 cabinet keypad was a placeholder
        foreach (var tmp in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmp.text == "Nothing rn") { tmp.text = "Wash, eat, read, keep cold."; EditorUtility.SetDirty(tmp); }
            if (tmp.text.StartsWith("WASD / Walk")) { tmp.text = "WASD Walk  |  Mouse Look  |  E Interact / Next  |  F Phone  |  Esc Close / Pause"; EditorUtility.SetDirty(tmp); }
        }

        KeyOnlyOpensMainFuseBox();
        GiveKeypadsDisplays();

        var intro = Object.FindAnyObjectByType<StartDialogueOnScene>();
        if (intro != null)
        {
            intro.delay = 1f;
            intro.lines = new[]
            {
                L("??? (You)", "Where have I been transferred to?\nWhy does this feel so strangely familiar?"),
                L("??? (You)", "A bedroom... Is this my room?"),
                L("??? (You)", "Maybe there's something here that tells me who I am."),
            };
            EditorUtility.SetDirty(intro);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[LevelPolishBuilder] Level_1 polished");
    }

    [MenuItem("Tools/Game Setup/Key Only Opens Main Fuse Box")]
    public static void FixKeyLocks()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        KeyOnlyOpensMainFuseBox();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Game Setup/Fix Keypad Displays")]
    public static void FixKeypadDisplays()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level_1.unity", OpenSceneMode.Single);
        GiveKeypadsDisplays();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>
    /// A keypad whose screen text was never wired showed nothing at all, not even the "_ _" slots
    /// (the storage-room door keypad). It gets a copy of the screen of the most similar keypad
    /// (same model, same facing), placed on it the same way.
    /// </summary>
    static void GiveKeypadsDisplays()
    {
        // screens copied by an earlier run must not keep the name of the screen they came from
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == "KeypadScreen" && t.parent != null)
                foreach (var label in t.GetComponentsInChildren<TMP_Text>(true))
                    label.gameObject.name = "KeypadScreen_" + (t.parent.parent != null ? t.parent.parent.name : t.parent.name);

        var wired = new List<(Transform pad, TMP_Text text)>();
        var missing = new List<(MonoBehaviour pad, SerializedObject so)>();
        var copied = new List<(Transform pad, Transform screen)>();
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null) continue;
            var so = new SerializedObject(mb);
            var prop = so.FindProperty("displayText");
            if (prop == null || prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (prop.objectReferenceValue is TMP_Text t)
            {
                if (t.canvas == null) continue;
                if (t.canvas.name == "KeypadScreen") copied.Add((mb.transform, t.canvas.transform)); // placed by an earlier run
                else wired.Add((mb.transform, t));
            }
            else missing.Add((mb, so));
        }

        string Model(Transform pad)
        {
            var mf = pad.GetComponentInChildren<MeshFilter>(true);
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "";
        }

        (Transform pad, TMP_Text text) Source(Transform pad) => wired
            .OrderBy(w => Model(w.pad) == Model(pad) ? 0 : 1)
            .ThenBy(w => Quaternion.Angle(w.pad.rotation, pad.rotation))
            .FirstOrDefault();

        // new screens hang under an unscaled holder (the keypad parts have rotated, non-uniform scales)
        var holder = GameObject.Find(Root + "/KeypadScreens")?.transform;
        if (holder == null)
        {
            var rootGo = GameObject.Find(Root) ?? new GameObject(Root);
            holder = new GameObject("KeypadScreens").transform;
            holder.SetParent(rootGo.transform, false);
        }

        // screens copied by earlier runs of this step are rebuilt
        foreach (var (pad, screen) in copied)
        {
            var mb = pad.GetComponents<MonoBehaviour>().First(m => m != null && new SerializedObject(m).FindProperty("displayText") != null);
            Object.DestroyImmediate(screen.gameObject);
            missing.Add((mb, new SerializedObject(mb)));
        }

        foreach (var (pad, so) in missing)
        {
            var src = Source(pad.transform);
            if (src.text == null) continue;
            var text = BuildScreen(pad.transform, src.pad, src.text, holder);
            so.Update();
            so.FindProperty("displayText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[LevelPolishBuilder] keypad screen for {pad.transform.parent.name}/{pad.name} ({pad.GetType().Name}) at {text.transform.position}, styled after {src.pad.parent.name}");
        }
    }

    /// <summary>
    /// A small world-space canvas with one text, at the spot (in metres, turned with the keypad) where the
    /// source keypad shows its code, in the same font, size and colour. The source screens hang off huge
    /// canvases ~600 m away with large anchored offsets, so copying their hierarchy does not work.
    /// </summary>
    static TMP_Text BuildScreen(Transform pad, Transform srcPad, TMP_Text src, Transform holder)
    {
        Quaternion turn = pad.rotation * Quaternion.Inverse(srcPad.rotation);
        var go = new GameObject("KeypadScreen", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(holder, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        rt.pivot = src.rectTransform.pivot;
        rt.sizeDelta = src.rectTransform.rect.size;
        rt.SetPositionAndRotation(pad.position + turn * (src.transform.position - srcPad.position), turn * src.transform.rotation);
        rt.localScale = src.transform.lossyScale;

        var tgo = new GameObject("KeypadScreen_" + pad.parent.name, typeof(RectTransform));
        var trt = (RectTransform)tgo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = tgo.AddComponent<TextMeshProUGUI>();
        t.font = src.font;
        t.fontSharedMaterial = src.fontSharedMaterial;
        t.fontSize = src.fontSize;
        t.enableAutoSizing = src.enableAutoSizing;
        t.fontSizeMin = src.fontSizeMin; t.fontSizeMax = src.fontSizeMax;
        t.fontStyle = src.fontStyle;
        t.color = src.color;
        t.alignment = src.alignment;
        t.characterSpacing = src.characterSpacing;
        t.textWrappingMode = src.textWrappingMode;
        t.margin = src.margin;
        t.raycastTarget = false;
        t.text = "";
        return t;
    }

    /// <summary>
    /// The storeroom key (0300) also fitted the small fuse boxes in the storeroom and the library,
    /// which skipped the box-count and rabbit-cipher puzzles. Those boxes now open only with their keypads.
    /// </summary>
    static void KeyOnlyOpensMainFuseBox()
    {
        foreach (var k in Object.FindObjectsByType<KeyDoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Under(k.transform, "FuseBox")) continue; // the main fuse box in the storage room
            k.canInteract = false;
            k.enabled = false;
            EditorUtility.SetDirty(k);
            Debug.Log("[LevelPolishBuilder] key lock removed from " + k.transform.parent.parent.name);
        }
    }

    static bool InRoom1(Vector3 p) => p.x < 7.7f;
    static bool InRoom2(Vector3 p) => p.x >= 7.7f && p.x < 22.7f;
    static bool InStorage(Vector3 p) => p.x >= 22.7f;

    static DialogueLine L(string who, string text) => new DialogueLine { characterName = who, text = text };
    static QuestObjective Obj(string desc, ObjectiveType type, GameObject target, bool hide = false) =>
        new QuestObjective { description = desc, type = type, targetObject = target, hideOnComplete = hide };

    static GameObject FindByPath(string path) => GameObject.Find(path);

    static T Find<T>(System.Func<T, bool> pred) where T : Component =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(pred);

    static bool Under(Transform t, string ancestor)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.name == ancestor) return true;
        return false;
    }

    static void BuildQuests(QuestManager qm, GameObject storageTrigger, GameObject endingDoor, LeverPuzzle puzzle)
    {
        // Keep Panya's first quests (phone, password, bedroom door) and give them better dialogue
        var q = qm.quests.Take(3).ToList();
        if (q.Count > 0)
        {
            q[0].questName = "A familiar room";
            q[0].afterCompleteDialogue = new[] { L("You", "This is... my phone? It's locked."), L("You", "The lock screen shows a calendar. One day is circled.") };
        }
        if (q.Count > 1)
        {
            q[1].questName = "Unlock the phone";
            q[1].afterCompleteDialogue = new[]
            {
                L("You", "13 May... That day is my birthday?"),
                L("You", "There's a Note app. I should write down anything strange I find."),
                L("You", "That door has a keypad. The clock on the desk... it stopped at a strange time."),
            };
        }
        if (q.Count > 2)
        {
            q[2].questName = "Leave the bedroom";
            q[2].afterCompleteDialogue = new[] { L("You", "Why does that time feel so heavy?"), L("You", "Let's see what's behind the door.") };
        }

        var doors = Object.FindObjectsByType<InteractiveDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject cabinetDoor = doors.FirstOrDefault(d => Under(d.transform, "realDoor") && d.transform.position.x < 22f)?.gameObject;
        GameObject storageDoor = doors.FirstOrDefault(d => Under(d.transform, "Door R2"))?.gameObject;
        GameObject remote = FindByPath("Building/Room2/QuestObject/remote");
        var fuses = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(i => i.name).Select(i => i.gameObject).ToList();
        var slots = Object.FindObjectsByType<PlaceItemInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(i => i.name).Select(i => i.gameObject).ToList();
        var key = Object.FindAnyObjectByType<KeyPickup>(FindObjectsInactive.Include)?.gameObject;
        var fuseBoxDoor = Find<KeyDoorInteractable>(k => Under(k.transform, "FuseBox") && !Under(k.transform, "FuseBox R3_1"))?.gameObject;

        q.Add(new QuestData
        {
            questName = "Explore the house",
            objectives = new List<QuestObjective> { Obj("Open the locked cabinet in the living room", ObjectiveType.UnlockDoor, cabinetDoor) },
            afterCompleteDialogue = new[] { L("You", "A chart that turns letters into numbers... but the numbers are scrambled."), L("You", "I'd better note this down. A word might become a code.") }
        });
        q.Add(new QuestData
        {
            questName = "Find a way out",
            objectives = new List<QuestObjective>
            {
                Obj("Turn on the TV", ObjectiveType.Interact, remote),
                Obj("Unlock the door to the storage room", ObjectiveType.UnlockDoor, storageDoor),
            },
            afterCompleteDialogue = new[] { L("You", "\"Evie\"... The TV said it like it was calling me."), L("You", "The door is open. It feels cold in there.") }
        });
        q.Add(new QuestData
        {
            questName = "The storage room",
            objectives = new List<QuestObjective> { Obj("Enter the storage room", ObjectiveType.Custom, storageTrigger) },
            afterCompleteDialogue = new[] { L("You", "The lights are so dim... the power is almost gone."), L("You", "That fuse box by the wall is empty. I need to find fuses.") }
        });
        var fuseObjs = new List<QuestObjective>();
        for (int i = 0; i < fuses.Count; i++) fuseObjs.Add(Obj($"Find a spare fuse ({i + 1}/{fuses.Count})", ObjectiveType.Custom, fuses[i]));
        q.Add(new QuestData
        {
            questName = "Find the spare fuses",
            objectives = fuseObjs,
            afterCompleteDialogue = new[] { L("You", "That's all the fuses."), L("You", "The fuse box has a keyhole. Where is the key?") }
        });
        var powerObjs = new List<QuestObjective>();
        if (key != null) powerObjs.Add(Obj("Find the key", ObjectiveType.Custom, key));
        if (fuseBoxDoor != null) powerObjs.Add(Obj("Open the fuse box", ObjectiveType.Custom, fuseBoxDoor));
        for (int i = 0; i < slots.Count; i++) powerObjs.Add(Obj($"Insert a fuse ({i + 1}/{slots.Count})", ObjectiveType.Custom, slots[i]));
        q.Add(new QuestData
        {
            questName = "Restore the power",
            objectives = powerObjs,
            afterCompleteDialogue = new[] { L("You", "The power is back!"), L("You", "The levers have symbols on them... sun, moon, star. There must be an order.") }
        });
        if (puzzle != null)
        {
            q.Add(new QuestData
            {
                questName = "Wake the levers",
                objectives = new List<QuestObjective> { Obj("Pull the levers in the right order", ObjectiveType.Custom, puzzle.gameObject) },
                afterCompleteDialogue = new[] { L("You", "A door... it wasn't there before."), L("You", "It feels like something is trying to stop me. It doesn't matter. I have to know who I am.") }
            });
        }
        if (endingDoor != null)
        {
            q.Add(new QuestData
            {
                questName = "Go deeper",
                objectives = new List<QuestObjective> { Obj("Go through the new door", ObjectiveType.Custom, endingDoor) },
                afterCompleteDialogue = new DialogueLine[0]
            });
        }
        qm.quests = q;
        EditorUtility.SetDirty(qm);
    }

    // ───────────── helpers ─────────────

    /// <summary>Adds MeshColliders to solid-looking meshes without one (skips interactables, animated doors, UI, small props).</summary>
    static void EnsureColliders(GameObject root)
    {
        if (root == null) return;
        int added = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var go = mf.gameObject;
            var rend = go.GetComponent<MeshRenderer>();
            if (rend == null || mf.sharedMesh == null) continue;
            if (go.GetComponent<Collider>() != null) continue;
            if (go.GetComponentInParent<Interactable>(true) != null) continue;
            if (go.GetComponentInParent<Animator>(true) != null) continue;
            if (go.GetComponentInParent<Canvas>(true) != null) continue;
            if (go.GetComponentInParent<ObjectPileHint>(true) != null) continue; // boxes switched on/off by the puzzle
            if (go.name.StartsWith("hintNum")) continue;
            var b = rend.bounds;
            if (b.min.y > 3.5f) continue;
            if (Mathf.Max(b.size.x, b.size.y, b.size.z) < 0.35f) continue;
            if (b.max.y < 0.12f) continue;
            go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            added++;
        }
        Debug.Log($"[LevelPolishBuilder] Added {added} colliders");
    }

    static Light Point(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        return l;
    }

    static void Spot(Transform parent, string name, Vector3 pos, Vector3 target, float angle, float intensity, float range, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.LookAt(target);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot; l.spotAngle = angle; l.intensity = intensity; l.range = range; l.color = color; l.shadows = LightShadows.None;
    }

    static AreaTrigger Trigger(Transform parent, string name, Vector3 center, Vector3 size, string ambient, LeverPuzzle power)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = center;
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = size;
        var t = go.AddComponent<AreaTrigger>();
        t.ambient = ambient; t.powerSource = power; t.once = false;
        return t;
    }

    // ───────────── other scenes ─────────────

    static void PolishWhiteSpaceText()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/WhiteSpace.unity", OpenSceneMode.Single);
        var tm = Object.FindAnyObjectByType<TaskManager>();
        if (tm != null && tm.quests.Count > 0)
        {
            tm.quests[0].questName = "Begin exploring your memories";
            if (tm.quests[0].tasks.Count > 0) tm.quests[0].tasks[0].description = "Walk through the glowing gate";
            EditorUtility.SetDirty(tm);
        }
        foreach (var door in Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(door);
            so.FindProperty("interactText").stringValue = "Press E to enter the memory";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void PolishDemoEnd()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/DemoEnd.unity", OpenSceneMode.Single);
        var name = GameObject.Find("Canvas/Name");
        if (name != null)
        {
            var tmp = name.GetComponent<TMP_Text>();
            tmp.text = "End of the prototype\n<size=45%>You were pushed out of your memories... for now.\nTo be continued.</size>";
            EditorUtility.SetDirty(tmp);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void SetupBuildSettings()
    {
        string[] order =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/WhiteSpace.unity", "Assets/Scenes/Level_1.unity",
            "Assets/Scenes/Confrontation.unity", "Assets/Scenes/DemoEnd.unity",
            "Assets/Scenes/Bedroom.unity", "Assets/Scenes/House.unity", "Assets/Scenes/WhiteSpace 2.unity",
        };
        var list = new List<EditorBuildSettingsScene>();
        for (int i = 0; i < order.Length; i++)
            if (File.Exists(order[i])) list.Add(new EditorBuildSettingsScene(order[i], i < 5));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
