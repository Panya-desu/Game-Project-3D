using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Smooth first sight of every place. In the built game a material, light setup or post effect is made
/// ready for the graphics card the first time it is seen, and that stalled a frame (50-100 ms) as the
/// camera first looked somewhere new: early in the waking cutscene, entering a room for the first time.
/// While a scene still starts behind the black fade, this renders each room once in four directions
/// (and her shadow-only body) with the game camera's settings, off screen, then lets the fade go on.
/// </summary>
public class SceneWarmup : MonoBehaviour
{
    static readonly Dictionary<string, Vector3[]> Spots = new Dictionary<string, Vector3[]>
    {
        // Level_1: the rooms (x, z), the storage wing in three places
        { "Level_1", new[] { new Vector3(0.2f, 0f, 4.1f), new Vector3(15.2f, 0f, 7f), new Vector3(15.2f, 0f, 20f), new Vector3(30.2f, 0f, 6f),
                              new Vector3(30.2f, 0f, 19f), new Vector3(30.2f, 0f, 33.3f), new Vector3(45.2f, 0f, 18.3f) } },
        { "WhiteSpace", new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f) } },
        { "Confrontation", new[] { new Vector3(0f, 0f, 0f) } },
        { "Main", new[] { new Vector3(0f, 0f, 0f) } },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Spots.ContainsKey(scene.name) || Application.isBatchMode) return;
        var go = new GameObject("[SceneWarmup]");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<SceneWarmup>();
    }

    IEnumerator Start()
    {
        var main = Camera.main;
        if (main == null) { Destroy(gameObject); yield break; }
        ScreenFader.Hold(); // stay covered while this runs
        var rt = new RenderTexture(640, 360, 24);
        var cam = new GameObject("WarmupCamera").AddComponent<Camera>();
        cam.CopyFrom(main);
        cam.enabled = false;
        cam.targetTexture = rt;
        var data = cam.GetUniversalAdditionalCameraData();
        var mainData = main.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = mainData.renderPostProcessing;
        data.antialiasing = mainData.antialiasing;
        float eye = main.transform.position.y - CharacterLibrary.GroundBelow(main.transform.position).y;
        if (eye < 0.5f || eye > 6f) eye = 2.8f;

        // her shadow-only body is hidden during the opening cutscene: show it for these renders
        var player = FindAnyObjectByType<Movement>();
        var body = player != null ? player.transform.Find("PlayerBody") : null;
        var hidden = new List<Renderer>();
        if (body != null) foreach (var r in body.GetComponentsInChildren<Renderer>(true)) if (!r.enabled) { r.enabled = true; hidden.Add(r); }

        foreach (var spot in Spots[SceneManager.GetActiveScene().name])
        {
            Vector3 floor = CharacterLibrary.GroundBelow(spot + Vector3.up * 3f);
            cam.transform.position = new Vector3(spot.x, floor.y + eye, spot.z);
            for (int i = 0; i < 4; i++)
            {
                cam.transform.rotation = Quaternion.Euler(12f, i * 90f, 0f);
                cam.Render();
                if (i == 0 && body != null && player != null) { cam.transform.LookAt(player.transform.position); cam.Render(); }
                yield return null;
            }
        }
        foreach (var r in hidden) if (r != null) r.enabled = false;
        cam.targetTexture = null;
        Destroy(cam.gameObject);
        rt.Release();
        Destroy(rt);
        ScreenFader.Release(1f);
        Destroy(gameObject);
    }
}
