using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Small in-engine cutscene player: letterbox bars, a temporary camera that glides between shots,
/// the HUD hidden and the player frozen (GameState.IsCutscene). Tab skips to the end.
/// Dialogue can run at the same time (the dialogue box stays visible above the bottom bar).
/// The 40-minute clock does not count while a cutscene plays.
/// </summary>
public class CutscenePlayer : MonoBehaviour
{
    /// <summary>One camera move: from/to position and the point the camera looks at, over a duration.</summary>
    public struct Shot
    {
        public Vector3 from, to, lookFrom, lookTo;
        public float seconds;
        public Action onStart;
        public Action<float> onUpdate; // 0..1 progress of the shot

        public Shot(Vector3 from, Vector3 to, Vector3 lookFrom, Vector3 lookTo, float seconds, Action onStart = null, Action<float> onUpdate = null)
        {
            this.from = from; this.to = to; this.lookFrom = lookFrom; this.lookTo = lookTo;
            this.seconds = seconds; this.onStart = onStart; this.onUpdate = onUpdate;
        }
    }

    public static bool IsPlaying { get; private set; }
    /// <summary>How many cutscenes have started this session (for tests).</summary>
    public static int Played { get; private set; }
    static CutscenePlayer instance;

    /// <summary>
    /// For a shot that ends inside her eyes: hides the character the moment the camera comes close to her head,
    /// before it passes into it (going by how far along the shot is let it clip through her face first).
    /// </summary>
    public static void HideWhenCameraReaches(GameObject character, float distance)
    {
        if (character == null || !character.activeSelf || instance == null || instance.cam == null) return;
        var anim = character.GetComponentInChildren<Animator>();
        var head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        Vector3 h = head != null ? head.position : character.transform.position + Vector3.up * 1.4f * PlayerAvatar.ModelScale;
        if (Vector3.Distance(instance.cam.transform.position, h) < distance) character.SetActive(false);
    }

    RectTransform top, bottom;
    CanvasGroup fade, skipHint, captionGroup;
    TextMeshProUGUI captionText;
    UnityEngine.UI.Image fadeImage;
    Coroutine captionRoutine;
    Camera cam, playerCam;
    bool skip;
    readonly List<Renderer> hiddenPlayer = new List<Renderer>();
    Volume lens;
    DepthOfField dof;

    /// <summary>
    /// Plays the shots in order, then gives control back and calls onEnd.
    /// With returnControl false the last shot stays on screen and onEnd is expected to change scene.
    /// startFrom: colour the screen fades in from (null = no fade).
    /// intoEyes: the last shot ends exactly in the player's eyes; during it the lens (field of view, depth of
    /// field, vignette, hand-held drift) turns into the player's own, and control comes back without a cut.
    /// </summary>
    public static void Play(IList<Shot> shots, Action onEnd = null, bool returnControl = true, Color? startFrom = null, bool intoEyes = false)
    {
        if (IsPlaying || shots == null || shots.Count == 0) { onEnd?.Invoke(); return; }
        Ensure();
        instance.StartCoroutine(instance.Run(shots, onEnd, returnControl, startFrom, intoEyes && returnControl));
    }

    /// <summary>Narration in the middle of the screen, fading in and out (story captions).</summary>
    public static void Caption(string text, float seconds)
    {
        Ensure();
        if (instance.captionRoutine != null) instance.StopCoroutine(instance.captionRoutine);
        instance.captionRoutine = instance.StartCoroutine(instance.ShowCaption(text, seconds));
    }

    static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("[Cutscene]");
        instance = go.AddComponent<CutscenePlayer>();
        instance.Build();
    }

    IEnumerator ShowCaption(string text, float seconds)
    {
        captionText.text = text;
        for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime) { captionGroup.alpha = t / 0.8f; yield return null; }
        captionGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds - 1.6f));
        for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime) { captionGroup.alpha = 1f - t / 0.8f; yield return null; }
        captionGroup.alpha = 0f;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        if (IsPlaying) { IsPlaying = false; GameState.IsCutscene = false; GameHud.SetVisible(true); }
    }

    IEnumerator Run(IList<Shot> shots, Action onEnd, bool returnControl, Color? startFrom, bool intoEyes)
    {
        IsPlaying = true;
        Played++;
        skip = false;
        GameState.IsCutscene = true;
        GameHud.SetVisible(false);

        playerCam = Camera.main;
        cam = new GameObject("CutsceneCamera").AddComponent<Camera>();
        cam.transform.SetParent(transform, false);
        if (playerCam != null)
        {
            cam.CopyFrom(playerCam);
            var src = playerCam.GetUniversalAdditionalCameraData();
            var dst = cam.GetUniversalAdditionalCameraData();
            dst.renderPostProcessing = src.renderPostProcessing;
            dst.antialiasing = src.antialiasing;
            dst.antialiasingQuality = src.antialiasingQuality;
            playerCam.enabled = false;
        }
        cam.tag = "MainCamera";
        float eyeFov = playerCam != null ? playerCam.fieldOfView : 60f;
        cam.fieldOfView = CinemaFov;

        // the player's own body (a test capsule) must not block the cinematic shots
        hiddenPlayer.Clear();
        var mover = FindAnyObjectByType<Movement>();
        if (mover != null)
            foreach (var r in mover.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; hiddenPlayer.Add(r); }

        // cinematic lens: shallow depth of field focused on what the camera looks at, a softer vignette
        if (lens == null) BuildLens();
        lens.weight = 1f;

        if (startFrom.HasValue)
        {
            fadeImage.color = startFrom.Value;
            fade.alpha = 1f;
            StartCoroutine(Fade(0f, 1.6f));
        }
        // while the screen is still covered, look through every shot once: the first sight of a close-up
        // (the clock, her hair from behind) otherwise stalled a frame at each cut of the waking cutscene
        // (into a texture of the screen's size: rendered straight to the screen outside the frame loop, the
        // UI overlay pass failed whenever the editor's game view was another size)
        if (ScreenFader.Opaque)
        {
            var rt = RenderTexture.GetTemporary(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 24);
            var screenTarget = cam.targetTexture;
            foreach (var w in shots)
            {
                cam.targetTexture = rt;
                cam.transform.position = w.from; cam.transform.rotation = Quaternion.LookRotation(w.lookFrom - w.from);
                cam.Render();
                cam.transform.position = w.to; cam.transform.rotation = Quaternion.LookRotation(w.lookTo - w.to);
                cam.Render();
                cam.targetTexture = screenTarget;
                yield return null;
            }
            cam.targetTexture = screenTarget;
            RenderTexture.ReleaseTemporary(rt);
        }

        yield return Bars(1f, 0.5f);

        for (int i = 0; i < shots.Count && !skip; i++)
        {
            var s = shots[i];
            HitchWatch.Note("cutscene shot " + i);
            s.onStart?.Invoke();
            bool toEyes = intoEyes && i == shots.Count - 1;
            for (float t = 0; t < s.seconds && !skip; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / s.seconds);
                // into her eyes: the cinematic lens becomes the player's as the camera arrives, so the
                // hand-over has no jump in framing (50 to 60 degrees read as the camera leaping back)
                float cinema = toEyes ? 1f - k : 1f;
                cam.transform.position = Vector3.Lerp(s.from, s.to, k);
                Vector3 look = Vector3.Lerp(s.lookFrom, s.lookTo, k);
                cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position) * Quaternion.Slerp(Quaternion.identity, HandHeld(), cinema);
                dof.focusDistance.value = Mathf.Max(0.3f, Vector3.Distance(cam.transform.position, look));
                if (toEyes) { cam.fieldOfView = Mathf.Lerp(eyeFov, CinemaFov, cinema); lens.weight = cinema; }
                // her own (shadow-only) body takes over as the actor is hidden near the end, so her shadow
                // never blinks out on the floor
                if (toEyes && t / s.seconds > 0.9f) ShowPlayerBody();
                s.onUpdate?.Invoke(t / s.seconds);
                yield return null;
            }
            if (toEyes && !skip)
            {
                cam.transform.position = s.to;
                cam.transform.rotation = Quaternion.LookRotation(s.lookTo - s.to);
                cam.fieldOfView = eyeFov; lens.weight = 0f;
            }
            s.onUpdate?.Invoke(1f);
        }

        if (!returnControl)
        {
            onEnd?.Invoke(); // e.g. a scene change; the scene's objects (and this player) go with it
            yield break;
        }

        // back to the player's eyes: straight through when the camera is already there, otherwise (or
        // when skipped) a short dip to black
        bool cut = !intoEyes || skip;
        fadeImage.color = Color.black;
        if (cut) yield return Fade(1f, 0.25f);
        ShowPlayerBody();
        lens.weight = 0f;
        if (playerCam != null) playerCam.enabled = true;
        Destroy(cam.gameObject);
        onEnd?.Invoke();
        GameState.IsCutscene = false;
        GameHud.SetVisible(true);
        IsPlaying = false;
        if (cut) yield return Fade(0f, 0.35f);
        yield return Bars(0f, 0.4f);
    }

    const float CinemaFov = 50f;

    void ShowPlayerBody()
    {
        foreach (var r in hiddenPlayer) if (r != null) r.enabled = true;
        hiddenPlayer.Clear();
    }

    /// <summary>Very small, slow camera drift, like a camera operator holding the shot.</summary>
    static Quaternion HandHeld()
    {
        float t = Time.time * 0.35f;
        return Quaternion.Euler((Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 0.9f, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 1.2f, 0f);
    }

    void BuildLens()
    {
        var go = new GameObject("CutsceneLens");
        go.transform.SetParent(transform, false);
        lens = go.AddComponent<Volume>();
        lens.isGlobal = true;
        lens.priority = 100f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        dof = profile.Add<DepthOfField>(false);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(3f);
        dof.focalLength.Override(70f);
        dof.aperture.Override(2.4f);
        var vig = profile.Add<Vignette>(false);
        vig.intensity.Override(0.32f);
        vig.smoothness.Override(0.5f);
        lens.sharedProfile = profile;
        lens.weight = 0f;
    }

    void Update()
    {
        if (IsPlaying && Input.GetKeyDown(KeyCode.Tab)) skip = true;
        if (skipHint != null) skipHint.alpha = IsPlaying ? 0.7f : 0f;
    }

    IEnumerator Bars(float target, float seconds)
    {
        float start = top.localScale.y;
        for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(start, target, t / seconds);
            top.localScale = bottom.localScale = new Vector3(1f, k, 1f);
            yield return null;
        }
        top.localScale = bottom.localScale = new Vector3(1f, target, 1f);
    }

    IEnumerator Fade(float target, float seconds)
    {
        float start = fade.alpha;
        for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }
        fade.alpha = target;
    }

    void Build()
    {
        // below the dialogue box (sorting 0 in the scenes) would hide it, so the bars sit just above the world
        var canvas = RuntimeUI.CreateCanvas(transform, "CutsceneCanvas", -10);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;

        top = Bar(canvas.transform, "Top", new Vector2(0f, 1f));
        bottom = Bar(canvas.transform, "Bottom", new Vector2(0f, 0f));

        var hint = RuntimeUI.Text(canvas.transform, "SkipHint", "Tab  Skip", 24, TextAlignmentOptions.BottomRight);
        UiTheme.Style(hint, false, 24, UiTheme.Pearl, 0f);
        RuntimeUI.Rect(hint.gameObject, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-130, 30), new Vector2(220, 40));
        skipHint = hint.gameObject.AddComponent<CanvasGroup>();
        skipHint.alpha = 0f;

        // narration: soft night-blue band behind large pearl text, readable on the white world too
        var captionRoot = new GameObject("Caption", typeof(RectTransform));
        captionRoot.transform.SetParent(canvas.transform, false);
        RuntimeUI.Rect(captionRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -335), new Vector2(1700, 130)); // just above the lower bar, clear of faces
        var band = RuntimeUI.Panel(captionRoot.transform, "Band", new Color(UiTheme.Night.r, UiTheme.Night.g, UiTheme.Night.b, 0.5f));
        band.sprite = UiTheme.DialogueBand();
        band.raycastTarget = false;
        RuntimeUI.Rect(band.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        captionText = RuntimeUI.Text(captionRoot.transform, "Text", "", 50, TextAlignmentOptions.Center);
        UiTheme.Style(captionText, true, 50, UiTheme.Pearl, 0.9f);
        captionText.fontStyle = FontStyles.Italic;
        captionText.characterSpacing = 4f;
        RuntimeUI.Rect(captionText.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        captionGroup = captionRoot.AddComponent<CanvasGroup>();
        captionGroup.alpha = 0f;
        captionGroup.blocksRaycasts = false;

        var black = RuntimeUI.Panel(canvas.transform, "Fade", Color.black);
        fadeImage = black;
        RuntimeUI.Rect(black.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fade = black.gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0f;
        fade.blocksRaycasts = false;
    }

    static RectTransform Bar(Transform parent, string name, Vector2 pivot)
    {
        var img = RuntimeUI.Panel(parent, name, Color.black);
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = new Vector2(0f, pivot.y);
        r.anchorMax = new Vector2(1f, pivot.y);
        r.pivot = new Vector2(0.5f, pivot.y);
        r.sizeDelta = new Vector2(0f, 120f);
        r.anchoredPosition = Vector2.zero;
        r.localScale = new Vector3(1f, 0f, 1f);
        return r;
    }
}
