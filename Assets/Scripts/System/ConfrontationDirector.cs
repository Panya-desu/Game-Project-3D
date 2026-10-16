using System.Collections;
using EchoCharacters;
using UnityEngine;

/// <summary>
/// Final scene of the prototype: the player meets a figure with their own face (the part of them
/// that wants to forget). Sequence: intro talk -> double appears -> argument -> it rushes the player
/// -> red hit, the world turns white -> end screen.
/// All lines are editable in the Inspector.
/// </summary>
public class ConfrontationDirector : MonoBehaviour
{
    [Header("References")]
    public DialogueManager dialogueManager;
    public Transform player;
    public Transform playerCamera;
    public Transform doppelganger;
    public Light doppelgangerLight;
    public Light worldLight;

    [Header("Dialogue")]
    public DialogueLine[] introLines;
    public DialogueLine[] confrontationLines;

    [Header("Timing")]
    public float startDelay = 2f;
    public float rushSeconds = 1.6f;
    public string nextScene = "DemoEnd";

    bool dialogueDone;
    EchoCharacterView doubleView;

    void Start()
    {
        if (dialogueManager == null) dialogueManager = FindAnyObjectByType<DialogueManager>();
        UseCharacterModel();
        if (doppelganger != null) doppelganger.gameObject.SetActive(false);
        StartCoroutine(Run());
    }

    /// <summary>
    /// Puts the rigged TheDouble model (Assets/EchoCharacters) inside the doppelganger object and hides
    /// the old placeholder meshes; the director keeps moving the doppelganger transform as before.
    /// </summary>
    void UseCharacterModel()
    {
        if (doppelganger == null) return;
        var lib = CharacterLibrary.Get();
        if (lib == null || lib.theDouble == null) return;
        foreach (var r in doppelganger.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        var model = Instantiate(lib.theDouble, doppelganger);
        model.name = "TheDouble";
        CharacterLook.Apply(model, true);
        Vector3 ground = CharacterLibrary.GroundBelow(doppelganger.position);
        model.transform.localRotation = Quaternion.identity;
        Vector3 ps = doppelganger.lossyScale; // keep the character at its real size whatever the placeholder's scale
        float s = PlayerAvatar.ModelScale;
        model.transform.localScale = new Vector3(s / Mathf.Max(0.001f, ps.x), s / Mathf.Max(0.001f, ps.y), s / Mathf.Max(0.001f, ps.z));
        model.transform.position = new Vector3(doppelganger.position.x, ground.y, doppelganger.position.z);
        doubleView = model.GetComponentInChildren<EchoCharacterView>();
    }

    IEnumerator Talk(DialogueLine[] lines)
    {
        if (dialogueManager == null || lines == null || lines.Length == 0) yield break;
        dialogueDone = false;
        void Done() => dialogueDone = true;
        dialogueManager.DialogueEnded += Done;
        dialogueManager.StartDialogue(lines);
        while (!dialogueDone) yield return null;
        dialogueManager.DialogueEnded -= Done;
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(startDelay);
        yield return Talk(introLines);

        // the world holds its breath: the ambience and the wind fall silent for a moment
        GameAudio.Hush(1.2f);
        WindDrift.Calm = true;
        yield return new WaitForSeconds(1.6f);
        WindDrift.Calm = false;

        // The double fades in a few metres in front of the player
        if (doppelganger != null && player != null)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            doppelganger.position = new Vector3(player.position.x, doppelganger.position.y, player.position.z) + fwd * 7f;
            doppelganger.rotation = Quaternion.LookRotation(-fwd, Vector3.up);
            doppelganger.gameObject.SetActive(true);
            if (doubleView != null) doubleView.Appear();
        }
        GameAudio.PlaySfx("reveal");
        GameAudio.PlayAmbient("amb_confront", 1f);
        StartCoroutine(TurnPlayerToward(doppelganger, 0.8f));
        yield return new WaitForSeconds(1.2f);

        if (doubleView != null) doubleView.SetTalking(true);
        yield return Talk(confrontationLines);
        if (doubleView != null) doubleView.SetTalking(false);

        // Ending cutscene with both characters (when the rigged models are available)
        if (doubleView != null && CharacterLibrary.Get() != null && CharacterLibrary.Get().protagonist != null)
        {
            PlayEnding();
            yield break;
        }

        // It rushes the player
        if (doubleView != null) doubleView.Rush();
        GameState.IsCutscene = true;
        GameAudio.PlaySfx("heartbeat");
        if (doppelganger != null && player != null)
        {
            Vector3 start = doppelganger.position;
            Vector3 end = new Vector3(player.position.x, start.y, player.position.z) + (start - player.position).normalized * 1.0f;
            float t = 0f;
            while (t < rushSeconds)
            {
                t += Time.deltaTime;
                float k = t / rushSeconds;
                doppelganger.position = Vector3.Lerp(start, end, k * k);
                if (worldLight != null) worldLight.color = Color.Lerp(Color.white, new Color(0.6f, 0.15f, 0.15f), k);
                yield return null;
            }
        }

        GameAudio.PlaySfx("impact");
        ScreenFader.Flash(new Color(0.8f, 0f, 0f, 0.8f), 0.5f);
        yield return Shake(0.45f, 0.25f);
        yield return new WaitForSeconds(0.4f);

        GameAudio.PlaySfx("memory_whoosh");
        GameState.IsCutscene = false;
        ScreenFader.LoadScene(nextScene, 2.5f, Color.white);
    }

    /// <summary>
    /// The ending, seen from outside: The Double rushes her and the world turns red; after the hit it is
    /// gone and she stands alone; the camera rises over the water while she promises to remember,
    /// then everything fades to white and the end screen.
    /// </summary>
    void PlayEnding()
    {
        Vector3 feet = CharacterLibrary.GroundBelow(player.position);
        Vector3 toDouble = Vector3.ProjectOnPlane(doppelganger.position - feet, Vector3.up);
        if (toDouble.sqrMagnitude < 0.01f) toDouble = player.forward;
        toDouble.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, toDouble);
        Vector3 head = feet + Vector3.up * 1.4f;

        var hero = CharacterLibrary.Spawn(false, feet, toDouble);
        if (hero != null) hero.transform.root.position = feet;
        var heroLook = hero != null ? hero.GetComponentInChildren<CharacterLook>() : null;
        if (heroLook != null) heroLook.lookTarget = doppelganger;

        Vector3 dStart = doppelganger.position;
        Vector3 dEnd = new Vector3(feet.x, dStart.y, feet.z) + toDouble * 0.9f;
        Color red = new Color(0.6f, 0.15f, 0.15f);
        bool reached = false;

        var shots = new[]
        {
            // it rushes her
            new CutscenePlayer.Shot(feet + right * 4.6f + toDouble * 2.6f + Vector3.up * 1.4f, feet + right * 3.6f + toDouble * 1.4f + Vector3.up * 1.25f,
                                    feet + toDouble * 3.2f + Vector3.up * 1.1f, feet + toDouble * 0.6f + Vector3.up * 1.2f, rushSeconds + 0.4f,
                                    onStart: () => { doubleView.Rush(); GameAudio.PlaySfx("heartbeat"); },
                                    onUpdate: k =>
                                    {
                                        float m = Mathf.Clamp01(k * 1.15f);
                                        doppelganger.position = Vector3.Lerp(dStart, dEnd, m * m);
                                        // she does not step back: she reaches out towards it
                                        if (!reached && k > 0.45f && hero != null) { reached = true; hero.ReachOut(); }
                                        WindDrift.Tension = k; // the wind rises as it rushes in
                                        if (worldLight != null) worldLight.color = Color.Lerp(Color.white, red, k);
                                    }),
            // the hit: it is gone, she is still standing
            new CutscenePlayer.Shot(feet + toDouble * 1.7f + right * 0.3f + Vector3.up * 1.45f, feet + toDouble * 2.3f + right * 0.6f + Vector3.up * 1.55f,
                                    head, head, 3.4f,
                                    onStart: () =>
                                    {
                                        GameAudio.PlaySfx("impact");
                                        WindDrift.Tension = 0f; // and falls still after the hit
                                        ScreenFader.Flash(new Color(0.8f, 0f, 0f, 0.8f), 0.6f);
                                        doubleView.StopRush();
                                        doppelganger.gameObject.SetActive(false);
                                        if (heroLook != null) heroLook.LookAt(null);
                                        if (hero != null) hero.SetTalking(true);
                                        CutscenePlayer.Caption("If remembering hurts... then let it hurt.", 3.2f);
                                    }),
            // alone on the water, the light comes back
            new CutscenePlayer.Shot(feet - toDouble * 2f + right * 0.8f + Vector3.up * 1.6f, feet - toDouble * 8f + right * 3f + Vector3.up * 8f,
                                    head, feet + toDouble * 2f, 6.5f,
                                    onStart: () =>
                                    {
                                        GameAudio.PlaySfx("memory_whoosh");
                                        if (hero != null) hero.SetTalking(false);
                                        CutscenePlayer.Caption("Wait for me, Evie. I'm going to remember.", 5.5f);
                                    },
                                    onUpdate: k =>
                                    {
                                        if (worldLight != null) worldLight.color = Color.Lerp(red, Color.white, k);
                                        // the last seconds before the end screen fall quiet
                                        if (k > 0.72f && GameAudio.CurrentAmbient != null) { GameAudio.Hush(1.6f); WindDrift.Calm = true; }
                                        // she walks on, towards where it stood
                                        if (k > 0.2f) CharacterLibrary.Walk(hero, feet, feet + toDouble * 3.5f, (k - 0.2f) / 0.8f);
                                    }),
        };
        CutscenePlayer.Play(shots, () => ScreenFader.LoadScene(nextScene, 2.5f, Color.white), returnControl: false);
    }

    IEnumerator TurnPlayerToward(Transform target, float seconds)
    {
        if (target == null || player == null) yield break;
        Quaternion from = player.rotation;
        Vector3 dir = Vector3.ProjectOnPlane(target.position - player.position, Vector3.up);
        if (dir.sqrMagnitude < 0.01f) yield break;
        Quaternion to = Quaternion.LookRotation(dir, Vector3.up);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            player.rotation = Quaternion.Slerp(from, to, t / seconds);
            yield return null;
        }
    }

    IEnumerator Shake(float seconds, float amount)
    {
        if (playerCamera == null) yield break;
        Vector3 basePos = playerCamera.localPosition;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float falloff = 1f - t / seconds;
            playerCamera.localPosition = basePos + Random.insideUnitSphere * amount * falloff;
            yield return null;
        }
        playerCamera.localPosition = basePos;
    }
}
