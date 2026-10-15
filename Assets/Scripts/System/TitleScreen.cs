using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Animated title screen in the style of modern story games:
///   1. The camera slowly drifts in front of the Gate of Memories on a sea of water while shards float.
///   2. The logo fades in, then "Click to Start" breathes at the bottom.
///   3. A click (or any key) opens the gate, the camera flies into the light, the screen turns white,
///      and the game continues from the save (if there is one) or starts a new game.
/// Small buttons at the bottom right: New Game (only when a save exists) and Exit.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    [Header("Scene")]
    public Transform cameraRig;
    public Transform gateLeafLeft;
    public Transform gateLeafRight;
    public Light gateLight;
    public Transform flyTarget;

    [Header("UI")]
    public CanvasGroup logoGroup;
    public CanvasGroup clickGroup;
    public CanvasGroup buttonsGroup;
    public TMP_Text clickText;
    public Button newGameButton;
    public Button exitButton;

    [Header("Timing")]
    public float logoDelay = 0.8f;
    public float logoFade = 2.0f;
    public float enterSeconds = 2.8f;
    public string newGameScene = "WhiteSpace";

    enum State { Intro, Ready, Entering }
    State state = State.Intro;
    Vector3 camStartPos;
    Quaternion camStartRot;
    bool startNewGame;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        GameState.IsCutscene = false;

        if (cameraRig != null) { camStartPos = cameraRig.position; camStartRot = cameraRig.rotation; }
        SetAlpha(logoGroup, 0f);
        SetAlpha(clickGroup, 0f);
        SetAlpha(buttonsGroup, 0f);

        if (newGameButton != null)
        {
            newGameButton.gameObject.SetActive(SaveSystem.HasSave);
            newGameButton.onClick.AddListener(() => Enter(true));
        }
        if (exitButton != null) exitButton.onClick.AddListener(Application.Quit);
        if (clickText != null) clickText.text = SaveSystem.HasSave ? "Click to Continue" : "Click to Start";

        SpawnHeroine();
        StartCoroutine(Intro());
    }

    /// <summary>
    /// The protagonist stands on the water in front of the gate with her back to the camera, like the
    /// cover art, with a mirrored copy under the water as her reflection.
    /// </summary>
    void SpawnHeroine()
    {
        if (gateLeafLeft == null || gateLeafLeft.parent == null) return;
        Transform gate = gateLeafLeft.parent;
        Vector3 toCamera = cameraRig != null ? Vector3.ProjectOnPlane(cameraRig.position - gate.position, Vector3.up).normalized : -gate.forward;
        Vector3 spot = gate.position + toCamera * 7.5f + Vector3.Cross(Vector3.up, toCamera) * 2.4f; // out on the water, left of the gate
        spot.y = 0f;
        Vector3 facing = Vector3.ProjectOnPlane(gate.position - spot, Vector3.up);
        var hero = CharacterLibrary.Spawn(false, spot + Vector3.up * 0.01f, facing);
        if (hero == null) return;
        hero.transform.root.position = spot;
        var look = hero.GetComponentInChildren<CharacterLook>();
        if (look != null) look.LookAt(gate.position + Vector3.up * 5.5f); // she looks up at the gate
        var reflection = CharacterLibrary.Spawn(false, spot, facing);
        if (reflection != null)
        {
            var root = reflection.transform.root;
            root.position = spot;
            root.localScale = new Vector3(1f, -1f, 1f);
            root.name = "Heroine_Reflection";
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    static void SetAlpha(CanvasGroup g, float a)
    {
        if (g == null) return;
        g.alpha = a;
        g.interactable = a > 0.5f;
        g.blocksRaycasts = a > 0.5f;
    }

    IEnumerator Intro()
    {
        yield return new WaitForSeconds(logoDelay);
        GameAudio.PlaySfx("shimmer", 0.7f);
        for (float t = 0; t < logoFade; t += Time.deltaTime)
        {
            SetAlpha(logoGroup, Mathf.SmoothStep(0, 1, t / logoFade));
            yield return null;
        }
        SetAlpha(logoGroup, 1f);
        for (float t = 0; t < 0.8f; t += Time.deltaTime)
        {
            SetAlpha(buttonsGroup, t / 0.8f);
            yield return null;
        }
        SetAlpha(buttonsGroup, 1f);
        state = State.Ready;
    }

    void Update()
    {
        // Idle camera drift (breathing), only before the player clicks
        if (cameraRig != null && state != State.Entering)
        {
            float t = Time.time;
            cameraRig.position = camStartPos + new Vector3(Mathf.Sin(t * 0.13f) * 0.35f, Mathf.Sin(t * 0.21f) * 0.12f, 0f);
            cameraRig.rotation = camStartRot * Quaternion.Euler(Mathf.Sin(t * 0.17f) * 0.6f, Mathf.Sin(t * 0.11f) * 1.2f, 0f);
        }

        if (state == State.Ready)
        {
            if (clickGroup != null) clickGroup.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.2f);
            bool pointerOnButton = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool clicked = Input.GetMouseButtonDown(0) && !pointerOnButton;
            bool key = Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetKeyDown(KeyCode.Escape);
            if (clicked || key) Enter(false);
        }
        else if (state == State.Intro && (Input.GetMouseButtonDown(0) || Input.anyKeyDown))
        {
            // Impatient players can skip the logo fade
            StopAllCoroutines();
            SetAlpha(logoGroup, 1f);
            SetAlpha(buttonsGroup, 1f);
            state = State.Ready;
        }
    }

    public void Enter(bool newGame)
    {
        if (state == State.Entering) return;
        state = State.Entering;
        startNewGame = newGame || !SaveSystem.HasSave;
        StartCoroutine(EnterRoutine());
    }

    IEnumerator EnterRoutine()
    {
        GameAudio.PlaySfx("title_enter");
        Vector3 fromPos = cameraRig != null ? cameraRig.position : Vector3.zero;
        Quaternion fromRot = cameraRig != null ? cameraRig.rotation : Quaternion.identity;
        Quaternion leftFrom = gateLeafLeft != null ? gateLeafLeft.localRotation : Quaternion.identity;
        Quaternion rightFrom = gateLeafRight != null ? gateLeafRight.localRotation : Quaternion.identity;
        float lightFrom = gateLight != null ? gateLight.intensity : 0f;
        bool fading = false;

        for (float t = 0; t < enterSeconds; t += Time.deltaTime)
        {
            float k = t / enterSeconds;
            float ui = 1f - Mathf.Clamp01(k * 4f);
            SetAlpha(logoGroup, ui);
            SetAlpha(clickGroup, ui);
            SetAlpha(buttonsGroup, ui);

            // Gate opens during the first half
            float open = Mathf.SmoothStep(0, 1, Mathf.Clamp01(k * 2f));
            if (gateLeafLeft != null) gateLeafLeft.localRotation = leftFrom * Quaternion.Euler(0, -95f * open, 0);
            if (gateLeafRight != null) gateLeafRight.localRotation = rightFrom * Quaternion.Euler(0, 95f * open, 0);
            if (gateLight != null) gateLight.intensity = Mathf.Lerp(lightFrom, lightFrom * 4f, open);

            // Camera flies through the gate during the second half
            if (cameraRig != null && flyTarget != null)
            {
                float fly = Mathf.SmoothStep(0, 1, Mathf.Clamp01((k - 0.3f) / 0.7f));
                fly *= fly;
                cameraRig.position = Vector3.Lerp(fromPos, flyTarget.position, fly);
                cameraRig.rotation = Quaternion.Slerp(fromRot, flyTarget.rotation, fly);
            }

            if (!fading && k > 0.6f)
            {
                fading = true;
                ScreenFader.FadeTo(Color.white, enterSeconds * 0.4f);
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);
        if (startNewGame || !SaveSystem.LoadGame())
            SaveSystem.StartNewGame(newGameScene);
    }
}
