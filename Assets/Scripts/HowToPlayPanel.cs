using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A "?" button in the top-right of the main menu that opens a how-to-play
/// overlay explaining the two modes, the combat system, and the controls for
/// whatever input is currently plugged in.
///
/// Everything is built in code, so there is nothing to lay out in the Editor.
/// The font is borrowed from the menu's own text so it does not fall back to
/// Unity's default.
///
/// The controls section is generated, not written out by hand: it reads the
/// connected pad through ControllerType and the hotkeys through
/// ControlsHUD.SystemKeysText, so it cannot drift out of date when a binding
/// changes.
/// </summary>
public class HowToPlayPanel : MonoBehaviour
{
    [SerializeField] float titleFontSize = 46f;
    [SerializeField] float bodyFontSize = 24f;
    [SerializeField] KeyCode openKey = KeyCode.H;

    /*  BUTTON PLACEMENT  ---------------------------------------------------
     *  The CanvasScaler here is 800x600 referenced and matches WIDTH, so the
     *  canvas is always 800 units wide and its height follows the aspect -
     *  about 450 units at 16:9, i.e. y runs roughly -225..225. An earlier
     *  attempt placed this at y = 484 on the assumption of a 1920x1080 canvas,
     *  which put it far outside the visible area and it simply never appeared.
     *
     *  The grey slab is 750 wide and centred, so its right edge sits at x = 375
     *  against the canvas edge at x = 400.
     *
     *  Anchored to top-centre rather than the screen corner: x stays measured
     *  from the middle so the button tracks the slab's edge, while y hangs off
     *  the top so it stays on screen whatever the aspect ratio.
     * -------------------------------------------------------------------- */
    [Header("Open button")]
    [Tooltip("x is measured from the canvas centre (slab edge is at 375); " +
             "y hangs down from the top of the screen.")]
    [SerializeField] Vector2 buttonPosition = new Vector2(335f, -50f);

    [SerializeField] Vector2 buttonSize = new Vector2(70f, 70f);

    [Tooltip("Sprite name inside Assets/Resources. Leave the file out and the " +
             "button falls back to a drawn '?'.")]
    [SerializeField] string iconResourceName = "HowToPlayIcon";

    /// <summary>
    /// True while the overlay is up. GameOrganizer checks this so Esc closes the
    /// panel instead of quitting the game - otherwise one Esc press would do
    /// both in the same frame.
    /// </summary>
    public static bool IsOpen { get; private set; }

    GameObject panel;
    TextMeshProUGUI bodyText;
    Button openButton;
    Button closeButton;

    readonly List<Button> menuButtons = new List<Button>();
    GameObject selectionBeforeOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        EnsureExists();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsOpen = false;   // a scene change always dismisses the overlay
        EnsureExists();
    }

    static void EnsureExists()
    {
        if (FindObjectOfType<HowToPlayPanel>() != null) return;

        // Menu only. An arena scene has no MainMenu component.
        if (FindObjectOfType<MainMenu>() == null) return;

        var host = new GameObject("[HowToPlayPanel]");
        host.AddComponent<HowToPlayPanel>();
    }

    IEnumerator Start()
    {
        yield return null;   // let the menu canvas finish building

        Canvas canvas = FindObjectsOfType<Canvas>()
            .FirstOrDefault(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);

        if (canvas == null)
        {
            Debug.LogWarning("[HowToPlayPanel] No canvas found; how-to-play not available.", this);
            enabled = false;
            yield break;
        }

        TextMeshProUGUI sample = canvas.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();

        BuildOpenButton(canvas, sample);
        BuildPanel(canvas, sample);

        SetOpen(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(openKey) && !IsOpen) SetOpen(true);

        if (!IsOpen) return;

        // Esc, or the pad's East button, backs out.
        bool cancel = Input.GetKeyDown(KeyCode.Escape);
        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) cancel = true;

        if (cancel) SetOpen(false);
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        if (panel != null) panel.SetActive(open);

        EventSystem es = EventSystem.current;

        if (open)
        {
            Refresh();

            // Stop arrow keys wandering onto the menu buttons behind the overlay.
            menuButtons.Clear();
            menuButtons.AddRange(FindObjectsOfType<Button>()
                .Where(b => b != closeButton && b.interactable));

            if (es != null) selectionBeforeOpen = es.currentSelectedGameObject;
            foreach (Button b in menuButtons) b.interactable = false;

            if (es != null && closeButton != null) es.SetSelectedGameObject(closeButton.gameObject);
        }
        else
        {
            foreach (Button b in menuButtons) if (b != null) b.interactable = true;
            menuButtons.Clear();

            // Put the highlight back where the player left it.
            if (es != null && selectionBeforeOpen != null && selectionBeforeOpen.activeInHierarchy)
            {
                es.SetSelectedGameObject(selectionBeforeOpen);
            }
        }
    }

    void Refresh()
    {
        if (bodyText != null) bodyText.text = BuildBody();
    }

    // ---------------------------------------------------------------- content

    static string BuildBody()
    {
        var sb = new StringBuilder();

        sb.AppendLine("<b>MODES</b>");
        sb.AppendLine("2 PLAYER (PVP)  -  two human bots fight each other.");
        sb.AppendLine("VS AI  -  you against the automated bot. Easy, Medium and Hard change how");
        sb.AppendLine("quickly it reacts, how hard it drives, and how well it predicts your movement.");
        sb.AppendLine();

        sb.AppendLine("<b>WINNING</b>");
        sb.AppendLine("Drain the other bot's health bar to take one of its lives.");
        sb.AppendLine("Lose all 3 lives and the match is over.");
        sb.AppendLine();

        sb.AppendLine("<b>COMBAT  -  TIME YOUR HITS</b>");
        sb.AppendLine("Your spinner has to wind up. The ATK POWER bar is how much damage your next");
        sb.AppendLine("hit will do, and it climbs the longer you go without connecting.");
        sb.AppendLine("Hit early and you barely scratch them. Hit near the top of the bar and it hurts.");
        sb.AppendLine("Landing a hit costs you your spin, so you start winding up again - trading");
        sb.AppendLine("blows favours whoever is more patient.");
        sb.AppendLine();

        sb.AppendLine("<b>CONTROLS</b>");
        sb.Append(ControlsSection());

        return sb.ToString();
    }

    /// <summary>
    /// Built from what is actually connected, so the labels match the pad in the
    /// player's hands rather than naming buttons they do not have.
    /// </summary>
    static string ControlsSection()
    {
        var sb = new StringBuilder();
        Gamepad pad = Gamepad.current;

        if (pad != null)
        {
            GamepadFamily family = ControllerType.Detect(pad);

            sb.AppendLine($"Controller detected: {pad.displayName} ({family})");
            // The full description lives here rather than in the arena HUD,
            // which has no room for it. The triggers deliberately do not drive
            // in either scheme - see the note in DriveScheme.
            sb.AppendLine($"   Layout: {DriveScheme.Title(DriveScheme.Current)}" +
                          $" - {DriveScheme.Description(DriveScheme.Current)}");
            sb.AppendLine($"   (pick this when you start a match)");
            sb.AppendLine($"   Boost: {ControllerType.SouthButtonLabel(family)}");
            sb.AppendLine($"   Back to menu: {ControllerType.EastButtonLabel(family)} or Start" +
                          $"      Restart: Select");
            sb.AppendLine();
        }

        sb.AppendLine("Keyboard, player 1:   drive W A S D      boost G");
        sb.AppendLine("Keyboard, player 2:   drive Arrow Keys   boost Right Ctrl");
        sb.AppendLine();
        sb.AppendLine("Devices are handed out automatically - controllers first, then the two");
        sb.AppendLine("keyboard halves. Two players can share one keyboard.");
        sb.AppendLine();
        sb.Append(ControlsHUD.SystemKeysText(includeAIKeys: true));

        return sb.ToString();
    }

    // ---------------------------------------------------------------- building

    void BuildOpenButton(Canvas canvas, TextMeshProUGUI sample)
    {
        openButton = canvas.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.name == "HowToPlayButton");

        if (openButton == null)
        {
            // Top-centre anchor: see the note on buttonPosition for why this is
            // not anchored to the screen corner.
            openButton = CreateButton(canvas.transform, "HowToPlayButton", "?",
                anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
                anchoredPos: buttonPosition, size: buttonSize,
                fontSize: 44f, sample: sample);
        }

        ApplyIcon();

        // Keeps the corner "?" from being chosen as the menu's opening selection.
        if (openButton.GetComponent<SkipAutoSelect>() == null)
        {
            openButton.gameObject.AddComponent<SkipAutoSelect>();
        }

        openButton.onClick.AddListener(() => SetOpen(true));
    }

    /// <summary>
    /// Swaps the drawn "?" for an image asset, if one is present.
    ///
    /// Drop a sprite at Assets/Resources/HowToPlayIcon.png and it is picked up
    /// automatically. Resources is used because this button is built at runtime,
    /// so there is no Inspector slot to assign a sprite into.
    ///
    /// Falls back to the text "?" when the file is absent, so a missing asset
    /// degrades to something usable rather than an empty button.
    /// </summary>
    void ApplyIcon()
    {
        if (openButton == null) return;

        Sprite icon = Resources.Load<Sprite>(iconResourceName);
        if (icon == null) return;

        var image = openButton.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = icon;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;   // full alpha: the art carries its own shape
        }

        /*  Setting image.color is not enough on its own.
         *  A Button with ColorTint transition writes its normalColor into the
         *  target graphic every time the state changes, overwriting whatever the
         *  Image was set to. The menu buttons here are styled with normalColor
         *  alpha 0 - invisible until highlighted - so a button cloned from one
         *  showed up in the Editor and then vanished the moment Play started.
         *  Force the normal and selected states opaque so the icon is always
         *  visible, while leaving hover and press feedback intact.             */
        ColorBlock colors = openButton.colors;
        if (colors.normalColor.a < 1f || colors.selectedColor.a < 1f)
        {
            colors.normalColor = new Color(colors.normalColor.r, colors.normalColor.g, colors.normalColor.b, 1f);
            colors.selectedColor = new Color(colors.selectedColor.r, colors.selectedColor.g, colors.selectedColor.b, 1f);
            openButton.colors = colors;
        }

        // The drawn "?" would sit on top of the icon.
        var label = openButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.gameObject.SetActive(false);
    }

    void BuildPanel(Canvas canvas, TextMeshProUGUI sample)
    {
        panel = new GameObject("HowToPlayPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, worldPositionStays: false);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;   // fill the screen

        // Dark backdrop. Also a raycast target, so clicks cannot reach the menu
        // buttons sitting underneath.
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.02f, 0.08f, 0.95f);

        CreateText(panel.transform, "Title", "HOW TO PLAY",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -50f), size: new Vector2(1600f, 90f),
            fontSize: titleFontSize, align: TextAlignmentOptions.Top, sample: sample);

        bodyText = CreateText(panel.transform, "Body", "",
            anchor: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -160f), size: new Vector2(1500f, 740f),
            fontSize: bodyFontSize, align: TextAlignmentOptions.TopLeft, sample: sample);

        closeButton = CreateButton(panel.transform, "CloseButton", "CLOSE  (Esc)",
            anchor: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f),
            anchoredPos: new Vector2(0f, 40f), size: new Vector2(420f, 80f),
            fontSize: 32f, sample: sample);

        closeButton.onClick.AddListener(() => SetOpen(false));
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string content,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
        float fontSize, TextAlignmentOptions align, TextMeshProUGUI sample)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = align;
        text.enableWordWrapping = false;
        text.richText = true;
        text.raycastTarget = false;
        text.color = Color.white;

        if (sample != null)
        {
            text.font = sample.font;
            text.fontSharedMaterial = sample.fontSharedMaterial;
        }

        return text;
    }

    static Button CreateButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
        float fontSize, TextMeshProUGUI sample)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.82f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        CreateText(go.transform, "Label", label,
            anchor: new Vector2(0.5f, 0.5f), pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: Vector2.zero, size: size,
            fontSize: fontSize, align: TextAlignmentOptions.Center, sample: sample)
            .color = Color.black;

        return button;
    }
}
