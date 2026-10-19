using EchoCharacters;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prologue of a new game, in the White Space before the player takes control:
/// out of the white, memory shards drift in the sky; she stands alone on the water and looks around;
/// the camera rises to show her in front of the three gates; then it slides into her eyes and the
/// Navigator starts talking. Narration appears as captions. Tab skips. Only on a new game.
/// </summary>
public class OpeningCutscene : MonoBehaviour
{
    const string SceneName = "WhiteSpace";

    StartDialogueOnScene firstDialogue;
    EchoCharacterView hero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != SceneName || FindAnyObjectByType<OpeningCutscene>() != null) return;
        var dm = FindAnyObjectByType<DialogueManager>();
        if (dm == null || dm.FirstDialogueDone) return; // only before the very first dialogue (new game)
        var go = new GameObject("[OpeningCutscene]");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<OpeningCutscene>();
    }

    void Awake()
    {
        // hold the Navigator's first lines until the prologue is over
        firstDialogue = FindAnyObjectByType<StartDialogueOnScene>();
        if (firstDialogue != null) firstDialogue.enabled = false;
    }

    void Start()
    {
        var player = FindAnyObjectByType<Movement>();
        var eye = Camera.main;
        if (player == null || eye == null) { Release(); return; }

        Vector3 feet = CharacterLibrary.GroundBelow(player.transform.position);
        Vector3 gates = new Vector3(0f, 0f, 14f);
        var openGate = GameObject.Find("Door1");
        if (openGate != null) gates = new Vector3(gates.x, 0f, openGate.transform.position.z);
        Vector3 fwd = Vector3.ProjectOnPlane(gates - feet, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 head = feet + Vector3.up * 1.4f;

        Vector3 walkEnd = feet + fwd * 3.2f;
        Vector3 eyeOffset = eye.transform.position - feet; // the player's eye above her feet

        hero = CharacterLibrary.Spawn(false, feet, fwd);
        var heroRoot = hero != null ? hero.transform.root.gameObject : null;
        if (heroRoot != null) heroRoot.transform.position = feet;
        // she sets off just after the cut and her walk fills most of the shot, stopping before the next one
        float walkFrom = 0.08f;

        // into her eyes where she stopped, as in the bedroom: from in front of her, a little to the side, the
        // camera swings past her shoulder into her eyes, so she slides out of the frame before it reaches
        // her (coming from behind, it went straight through her hair and face). The player is moved there
        // first, so control returns right where she stands and she never visibly vanishes.
        CutscenePlayer.Shot IntoHerEyes(Vector3 stand) =>
            new CutscenePlayer.Shot(stand + fwd * 1.2f + right * 1.1f + Vector3.up * 1.7f * PlayerAvatar.ModelScale, stand + eyeOffset,
                                    stand + Vector3.up * 1.4f * PlayerAvatar.ModelScale, stand + eyeOffset + fwd * 3f, 2.6f,
                                    onStart: () => MovePlayerTo(player, stand, fwd),
                                    onUpdate: k => { CutscenePlayer.HideWhenCameraReaches(heroRoot, 0.32f * PlayerAvatar.ModelScale); if (heroRoot != null && k > 0.9f) heroRoot.SetActive(false); });

        CutscenePlayer.Shot[] shots = null;
        shots = new[]
        {
            // the sky of drifting memories
            new CutscenePlayer.Shot(feet - fwd * 1.5f + Vector3.up * 1.2f, feet - fwd * 1.2f + Vector3.up * 1.6f,
                                    feet + fwd * 6f + Vector3.up * 10f, feet + fwd * 9f + Vector3.up * 6.5f, 5.5f,
                                    onStart: () => { if (hero != null) hero.GazeUp(); CutscenePlayer.Caption("Somewhere between sleeping and waking...", 5f); }),
            // she stands alone on the water
            new CutscenePlayer.Shot(feet + fwd * 2.3f + right * 0.5f + Vector3.up * 0.35f, feet + fwd * 2.7f - right * 0.3f + Vector3.up * 0.95f,
                                    head - Vector3.up * 0.1f, head, 6f,
                                    onStart: () =>
                                    {
                                        if (hero != null) hero.LookAround();
                                        CutscenePlayer.Caption("...the things I tried to forget are waiting for me.", 5.5f);
                                    }),
            // the three gates: she takes a few steps towards them
            new CutscenePlayer.Shot(feet - fwd * 1.6f + right * 0.6f + Vector3.up * 1.6f, feet - fwd * 7f + right * 2.5f + Vector3.up * 5f,
                                    feet + fwd * 4f + Vector3.up * 1.6f, gates + Vector3.up * 2.4f, 6f,
                                    onStart: () => CutscenePlayer.Caption("Three doors... and one of them is already open.", 5.5f),
                                    // she sets off partway through and has stopped before the next shot
                                    onUpdate: k =>
                                    {
                                        CharacterLibrary.Walk(hero, feet, walkEnd, Mathf.InverseLerp(walkFrom, 0.8f, k), 6f * 0.72f);
                                        // she stops on a planted step, which can leave her a little short of the
                                        // mark: the last shot goes into her eyes where she really stands
                                        if (k >= 1f && heroRoot != null)
                                        {
                                            Vector3 at = heroRoot.transform.position;
                                            shots[3] = IntoHerEyes(new Vector3(at.x, walkEnd.y, at.z));
                                        }
                                    }),
            IntoHerEyes(walkEnd),
        };
        CutscenePlayer.Play(shots, () =>
        {
            if (heroRoot != null) Destroy(heroRoot);
            Release();
        }, startFrom: Color.white, intoEyes: true);
    }

    static void MovePlayerTo(Movement player, Vector3 feet, Vector3 facing)
    {
        if (player == null) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        Vector3 p = player.transform.position;
        player.transform.position = new Vector3(feet.x, p.y, feet.z);
        player.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        if (cc != null) cc.enabled = true;
    }

    void Release()
    {
        if (firstDialogue != null)
        {
            firstDialogue.delay = 0.6f;
            firstDialogue.enabled = true; // its Start runs now and begins the Navigator's lines
        }
    }
}
