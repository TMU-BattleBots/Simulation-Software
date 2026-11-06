using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The second prompt: after a mode is picked, asks which gamepad driving layout
/// the player wants, then starts the match.
///
/// WHEN IT APPEARS
///   Only when a gamepad is actually connected. The choice is between two
///   two-stick layouts, so it is meaningless to a keyboard player - showing it
///   anyway would put a question with no real answer between them and the game.
///   With no pad connected, the match starts immediately.
///
/// MATCHING THE GAME'S LOOK
///   Two things make this read as part of the game rather than a default Unity
///   dialog:
///
///   1. The arena HUD font. The menu labels happen to use TMP's stock
///      LiberationSans, but the rest of the game uses E4DigitalV2, which is
///      what gives the HUD its blocky look. It sits under a Resources folder,
///      so it can be loaded by name at runtime - see GameFont. Everything here
///      is also UPPERCASE, to match the arena HUD and the menu labels. That is
///      a style choice, not a technical one: the font does carry a full
///      lowercase set, so mixed case would render fine if it were wanted.
///
///   2. The menu's own selection convention. The scene buttons are styled with
///      normalColor alpha 0 and selectedColor solid white - invisible until
///      chosen, then a white box. These choices copy that ColorBlock exactly,
///      so the selection box here behaves like the one on the menu behind it,
///      with a cyan frame added so it is obvious at a glance which option the
///      stick is sitting on.
///
/// WHY IT IS BUILT IN CODE
///   Same reason as HowToPlayPanel: everything here is created at runtime, so
///   there is nothing to wire in the Inspector and nothing that can be lost
///   when a scene fails to reimport.
/// </summary>
public class ControlSchemePrompt : MonoBehaviour
{
    /// <summary>True while the prompt is up, so Esc and the menu do not act underneath it.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>
    /// The HUD font, inside the TMP package's Resources folder. Loading by name
    /// is what lets a runtime-built panel use a project font at all - there is
    /// no Inspector slot to drag one into.
    /// </summary>
    const string GameFontResource = "Fonts & Materials/E4DigitalV2 Light SDF";

    // Palette lifted from the arena HUD so this does not invent its own colours.
    static readonly Color Backdrop = new Color(0.09f, 0.03f, 0.11f, 1f);
    static readonly Color Accent = new Color(0.21f, 0.84f, 0.86f);   // HUD cyan
    static readonly Color OnDark = Color.white;
    static readonly Color OnLight = new Color(0.07f, 0.02f, 0.09f);
    static readonly Color DetailOnDark = new Color(0.72f, 0.70f, 0.78f);
    static readonly Color DetailOnLight = new Color(0.25f, 0.20f, 0.28f);

    /// <summary>One selectable option and the bits of it that change on selection.</summary>
    class Choice
    {
        public Button Button;
        public Image Frame;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Detail;
    }

    readonly List<Choice> choices = new List<Choice>();

    Action<DriveSchemeKind> onChosen;
    GameObject panel;
    TMP_FontAsset font;

    /// <summary>
    /// Shows the prompt, and calls <paramref name="onChosen"/> with the picked
    /// scheme. With no gamepad connected it calls back immediately with the
    /// scheme already in force and never builds anything.
    /// </summary>
    public static void Ask(Action<DriveSchemeKind> onChosen)
    {
        if (onChosen == null) return;

        if (Gamepad.current == null)
        {
            // Keyboard only - nothing to choose between.
            onChosen(DriveScheme.Current);
            return;
        }

        var host = new GameObject("[ControlSchemePrompt]");
        var prompt = host.AddComponent<ControlSchemePrompt>();
        prompt.onChosen = onChosen;
        prompt.Build();
    }

    /// <summary>
    /// The game's HUD font, or null to fall back to whatever the scene uses.
    /// </summary>
    static TMP_FontAsset GameFont()
    {
        var loaded = Resources.Load<TMP_FontAsset>(GameFontResource);

        if (loaded == null)
        {
            Debug.LogWarning($"[ControlSchemePrompt] Could not load '{GameFontResource}'. " +
                             "Falling back to the scene's font.");
        }

        return loaded;
    }

    void Build()
    {
        Canvas canvas = FindObjectsOfType<Canvas>()
            .FirstOrDefault(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace);

        if (canvas == null)
        {
            // No canvas to draw on is not a reason to strand the player in the
            // menu - fall through to the match with the current scheme.
            Debug.LogWarning("[ControlSchemePrompt] No canvas found; starting with the current scheme.");
            Choose(DriveScheme.Current);
            return;
        }

        font = GameFont();

        panel = new GameObject("ControlSchemePanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, worldPositionStays: false);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        /*  Fully opaque, deliberately.
         *  At 96% the menu buttons behind were still legible through the
         *  backdrop, which made the prompt look like a bug rather than a
         *  screen. It is also a raycast target, so clicks cannot reach the
         *  menu buttons underneath.                                          */
        var bg = panel.AddComponent<Image>();
        bg.color = Backdrop;

        CreateText(panel.transform, "Title", "CHOOSE YOUR CONTROLS",
            new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(760f, 52f),
            38f, TextAlignmentOptions.Top, OnDark);

        CreateText(panel.transform, "Hint",
            $"CONTROLLER DETECTED: {Gamepad.current.displayName.ToUpperInvariant()}",
            new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(760f, 34f),
            20f, TextAlignmentOptions.Top, Accent);

        CreateChoice(DriveSchemeKind.EitherStick, -40f);
        CreateChoice(DriveSchemeKind.SplitSticks, -156f);

        CreateText(panel.transform, "Footer",
            "YOU CAN CHANGE THIS NEXT TIME YOU START A MATCH",
            new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(760f, 34f),
            17f, TextAlignmentOptions.Bottom, DetailOnDark);

        IsOpen = true;

        // Keep stick and arrow navigation inside the prompt rather than letting
        // it wander onto the menu buttons hidden behind the backdrop.
        MenuNavigation.SetModalRoot(panel.transform);

        if (EventSystem.current != null && choices.Count > 0)
        {
            EventSystem.current.SetSelectedGameObject(choices[0].Button.gameObject);
        }

        RefreshSelection();
    }

    /// <summary>One scheme as a button with its explanation underneath.</summary>
    void CreateChoice(DriveSchemeKind scheme, float y)
    {
        var size = new Vector2(620f, 96f);

        /*  The cyan frame is created FIRST so it sits behind the button.
         *  Sibling order is draw order in a canvas, and a frame drawn on top
         *  would cover the label.                                            */
        var frameGo = new GameObject($"Frame_{scheme}", typeof(RectTransform));
        frameGo.transform.SetParent(panel.transform, worldPositionStays: false);
        Place(frameGo, new Vector2(0f, y + 5f), size + new Vector2(10f, 10f));

        var frame = frameGo.AddComponent<Image>();
        frame.color = Accent;
        frame.raycastTarget = false;
        frame.enabled = false;   // only the selected option shows one

        var go = new GameObject($"Choice_{scheme}", typeof(RectTransform));
        go.transform.SetParent(panel.transform, worldPositionStays: false);
        Place(go, new Vector2(0f, y), size);

        var image = go.AddComponent<Image>();

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        /*  Copied from the scene's own menu buttons: invisible until it is the
         *  selection, then a solid white box. Matching the ColorBlock rather
         *  than inventing one is what makes this feel like the same menu.     */
        var colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 0.57f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        var label = CreateText(go.transform, "Label", DriveScheme.Title(scheme).ToUpperInvariant(),
            new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(600f, 38f),
            30f, TextAlignmentOptions.Top, OnDark);

        var detail = CreateText(go.transform, "Detail", DriveScheme.Description(scheme).ToUpperInvariant(),
            new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(600f, 30f),
            19f, TextAlignmentOptions.Top, DetailOnDark);

        button.onClick.AddListener(() => Choose(scheme));

        choices.Add(new Choice { Button = button, Frame = frame, Label = label, Detail = detail });
    }

    void Update()
    {
        if (IsOpen) RefreshSelection();
    }

    /// <summary>
    /// Shows the frame on the selected option and flips its text to dark.
    ///
    /// The flip is the point: the button's own ColorBlock paints a white box
    /// when selected, and white-on-white would make the chosen option the one
    /// you cannot read. Unity's ColorTint only tints the button graphic, not
    /// its label, so the label has to be handled here.
    /// </summary>
    void RefreshSelection()
    {
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        foreach (Choice choice in choices)
        {
            bool isSelected = selected == choice.Button.gameObject;

            choice.Frame.enabled = isSelected;
            choice.Label.color = isSelected ? OnLight : OnDark;
            choice.Detail.color = isSelected ? DetailOnLight : DetailOnDark;
        }
    }

    void Choose(DriveSchemeKind scheme)
    {
        // Setting Current also rebinds every loaded asset.
        DriveScheme.Current = scheme;
        Debug.Log($"[ControlSchemePrompt] Scheme set to {scheme} ({DriveScheme.Description(scheme)}).");

        IsOpen = false;
        MenuNavigation.SetModalRoot(null);

        Action<DriveSchemeKind> callback = onChosen;
        onChosen = null;

        if (panel != null) Destroy(panel);
        Destroy(gameObject);

        callback?.Invoke(scheme);
    }

    static void Place(GameObject go, Vector2 anchoredPos, Vector2 size)
    {
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;
    }

    TextMeshProUGUI CreateText(Transform parent, string name, string content,
        Vector2 anchor, Vector2 anchoredPos, Vector2 size, float fontSize,
        TextAlignmentOptions align, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = align;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.color = colour;

        if (font != null) text.font = font;

        return text;
    }
}
