using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shows how to leave a match, in the empty space under the Richard logo in the
/// top-right of the vs-AI arena.
///
/// The bottom-right block already lists the driving controls, and stacking the
/// exit keys underneath pushed the last lines off the bottom of the screen -
/// which is why they were invisible even though they were switched on. This puts
/// them in the unused corner instead.
///
/// The text comes from ControlsHUD.SystemKeysText, so the key names are written
/// in exactly one place and both readouts follow a binding change.
///
/// SETUP
///   None - it builds its own text object and installs itself. It only appears
///   in a scene that has an AI, so the PVP arena is left alone (there the exit
///   keys live in the controls block on the left).
///
///   To position it by hand instead, add this component to any object in the
///   scene and it will use that instead of creating one.
/// </summary>
public class ExitHintHUD : MonoBehaviour
{
    [Tooltip("Heading above the exit keys.")]
    [SerializeField] string heading = "EXIT";

    [Tooltip("Also list each player's driving controls here. The bottom-right " +
             "block in the scene kept clipping off the screen edge, so they are " +
             "rendered in this column instead.")]
    [SerializeField] bool showPlayerControls = true;

    [Tooltip("Switch off the scene's own ControlsHUD so the controls are not " +
             "listed twice once that block is visible again.")]
    [SerializeField] bool hideSceneControlsBlock = true;

    [Tooltip("Distance in from the top-right corner, in canvas units.")]
    [SerializeField] Vector2 cornerOffset = new Vector2(-40f, -300f);

    [Tooltip("Size of the text block, in canvas units.")]
    [SerializeField] Vector2 blockSize = new Vector2(460f, 220f);

    [SerializeField] float fontSize = 24f;

    [Tooltip("Colour of the exit hint. White reads clearly against the purple surround.")]
    [SerializeField] Color textColour = Color.white;

    [Tooltip("How often to re-check, so plugging a controller in updates the text.")]
    [SerializeField] float refreshInterval = 2f;

    TextMeshProUGUI target;
    string lastText;
    float timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        EnsureExists();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    static void EnsureExists()
    {
        if (FindObjectOfType<ExitHintHUD>() != null) return;

        // Only the vs-AI arena has the Richard corner to fill.
        if (FindObjectOfType<AI>() == null) return;

        var host = new GameObject("[ExitHintHUD]");
        host.AddComponent<ExitHintHUD>();
    }

    IEnumerator Start()
    {
        yield return null;   // let the scene's canvas finish building

        target = GetComponent<TextMeshProUGUI>();
        if (target == null) target = BuildText();

        // The scene's own controls block is the one that keeps clipping. Now
        // that its content is rendered here, switch it off rather than leave a
        // half-visible duplicate in the corner.
        if (hideSceneControlsBlock && showPlayerControls)
        {
            foreach (ControlsHUD hud in FindObjectsOfType<ControlsHUD>())
            {
                hud.gameObject.SetActive(false);
                Debug.Log($"[ExitHintHUD] Hid the scene controls block '{hud.name}' - " +
                          $"its content is shown in the top-right column instead.", hud);
            }
        }

        Rebuild();
    }

    void Update()
    {
        if (refreshInterval <= 0f) return;

        timer += Time.unscaledDeltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;

        Rebuild();
    }

    void Rebuild()
    {
        if (target == null) return;

        var sb = new System.Text.StringBuilder();

        /*  The driving controls used to live in the bottom-right "Notes" block,
         *  which kept rendering off the edge of the screen. They are shown here
         *  instead, in the empty column under the Richard logo, where there is
         *  room for them and the position is set in code rather than in the
         *  scene.                                                              */
        if (showPlayerControls)
        {
            foreach (PlayerInputHandler h in FindObjectsOfType<PlayerInputHandler>()
                         .Where(x => x.BotIndex > 0 && x.IsPaired)
                         .OrderBy(x => x.BotIndex))
            {
                sb.Append(ControlsHUD.PlayerControlsText(h));
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrEmpty(heading)) sb.AppendLine(heading);
        sb.Append(ControlsHUD.SystemKeysText(includeAIKeys: false).TrimEnd());

        string text = sb.ToString().TrimEnd();

        // Setting text forces a mesh rebuild, and this runs on a timer.
        if (text == lastText) return;

        lastText = text;
        target.text = text;
    }

    /// <summary>
    /// Creates the text object in the top-right of the scene's canvas, copying
    /// the font off an existing label so it matches the rest of the HUD rather
    /// than falling back to Unity's default.
    /// </summary>
    TextMeshProUGUI BuildText()
    {
        Canvas canvas = FindObjectsOfType<Canvas>()
            .FirstOrDefault(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);

        if (canvas == null)
        {
            Debug.LogWarning("[ExitHintHUD] No canvas found; exit hint not shown.", this);
            enabled = false;
            return null;
        }

        // Borrow the look of whatever text is already on screen.
        TextMeshProUGUI sample = canvas.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();

        var go = new GameObject("ExitHintText", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, worldPositionStays: false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);   // top-right
        rect.sizeDelta = blockSize;
        rect.anchoredPosition = cornerOffset;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.TopRight;
        text.fontSize = fontSize;
        text.enableWordWrapping = false;
        text.raycastTarget = false;

        if (sample != null)
        {
            // Borrow the typeface, but NOT the colour. The first label found in
            // the canvas happens to be one of the dark magenta HUD readouts, so
            // copying its colour rendered this block nearly unreadable against
            // the purple background.
            text.font = sample.font;
            text.fontSharedMaterial = sample.fontSharedMaterial;
            text.fontStyle = sample.fontStyle;
        }

        text.color = textColour;

        return text;
    }
}
