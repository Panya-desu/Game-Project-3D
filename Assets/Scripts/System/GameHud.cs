using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Crosshair in the middle of the screen (grows when you look at something usable)
/// plus a one-line hint at the bottom (e.g. how to use the keypad). Created automatically.
/// </summary>
public class GameHud : MonoBehaviour
{
    public static GameHud Instance { get; private set; }

    Image dot;
    TextMeshProUGUI hint;
    GameObject root;
    PlayerInteract player;
    float nextPlayerSearch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[GameHud]");
        DontDestroyOnLoad(go);
        go.AddComponent<GameHud>();
    }

    /// <summary>Hides the crosshair and hints (cutscenes).</summary>
    public static void SetVisible(bool visible)
    {
        if (Instance != null && Instance.root != null) Instance.root.SetActive(visible);
    }

    public static void SetHint(string text)
    {
        if (Instance != null && Instance.hint != null) Instance.hint.text = text;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var canvas = RuntimeUI.CreateCanvas(transform, "HudCanvas", 900);
        root = canvas.gameObject;
        canvas.GetComponent<GraphicRaycaster>().enabled = false;

        dot = RuntimeUI.Panel(canvas.transform, "Crosshair", new Color(1, 1, 1, 0.6f));
        dot.raycastTarget = false;
        RuntimeUI.Rect(dot.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));

        hint = RuntimeUI.Text(canvas.transform, "Hint", "", 26, TextAlignmentOptions.Center);
        RuntimeUI.Rect(hint.gameObject, new Vector2(0.1f, 0f), new Vector2(0.9f, 0f), new Vector2(0, 165), new Vector2(0, 90));
        hint.color = new Color(1f, 1f, 1f, 0.9f);

        SceneManager.sceneLoaded += (s, m) => SetHint("");
    }

    void Update()
    {
        // Only gameplay scenes have a first-person player (searched at most twice a second)
        if (player == null && Time.unscaledTime >= nextPlayerSearch)
        {
            player = FindAnyObjectByType<PlayerInteract>();
            nextPlayerSearch = Time.unscaledTime + 0.5f;
        }
        bool gameplay = player != null && player.isActiveAndEnabled;
        bool show = gameplay && !GameState.InputBlockedOrTalking;
        dot.enabled = show;

        bool aiming = PlayerInteract.IsAimingAtInteractable;
        float size = aiming ? 14f : 6f;
        dot.rectTransform.sizeDelta = Vector2.Lerp(dot.rectTransform.sizeDelta, new Vector2(size, size), 15f * Time.unscaledDeltaTime);
        dot.color = aiming ? new Color(1f, 0.92f, 0.6f, 0.95f) : new Color(1, 1, 1, 0.6f);
    }
}
