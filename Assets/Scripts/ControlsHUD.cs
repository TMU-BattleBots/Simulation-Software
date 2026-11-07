using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// On-screen movement legend, meant to sit bottom-left as a mirror of the debug
/// hotkey list bottom-right.
///
/// It builds its text from the live input setup rather than a hard-coded string,
/// so it always matches reality: if a player is on WASD it says WASD, if they
/// are on a DualShock it says R2 / L2 / Cross, and if a controller is plugged in
/// or pulled out mid-match the text follows.
///
/// SETUP (about 30 seconds in the Editor)
///   1. In the arena scene, find the existing hotkey text bottom-right
///      ("Manual switch to Idle: C ...") in the Canvas.
///   2. Duplicate it (Ctrl+D). Duplicating rather than making a fresh Text is
///      the point - the font, size, colour and outline all come along for free.
///   3. Rename it something like "ControlsLegend", and move it to the
///      bottom-LEFT: set the RectTransform anchor preset to bottom-left and set
///      the TextMeshPro alignment to Left.
///   4. Add this component to it. Leave Target empty and it uses the
///      TextMeshProUGUI on the same object.
///
/// Nothing else to wire - it finds the PlayerInputHandlers itself.
/// </summary>
public class ControlsHUD : MonoBehaviour
{
    [Tooltip("Leave empty to use the TextMeshProUGUI on this same GameObject.")]
    [SerializeField] TextMeshProUGUI target;

    [Tooltip("Show a MOVEMENT CONTROLS heading above the list.")]
    [SerializeField] bool showHeading = true;

    [Tooltip("Heading text, if shown.")]
    [SerializeField] string heading = "MOVEMENT CONTROLS";

    [Tooltip("How often to re-check for controllers being plugged in or removed. " +
             "0 disables the re-check and the text is built once on Start.")]
    [SerializeField] float refreshInterval = 2f;

    [Header("Sections")]
    [Tooltip("List each player's drive and boost controls.")]
    [SerializeField] bool showPlayerControls = true;

    [Tooltip("List the game hotkeys (restart, menu, quit, and the AI debug keys). " +
             "Turn this on for a second copy of this component placed bottom-right.")]
    [SerializeField] bool showSystemKeys = false;

    readonly List<PlayerInputHandler> handlers = new List<PlayerInputHandler>();
    readonly StringBuilder builder = new StringBuilder();

    string lastText;
    float timer;

    void Start()
    {
        if (target == null) target = GetComponent<TextMeshProUGUI>();

        if (target == null)
        {
            Debug.LogError("[ControlsHUD] No TextMeshProUGUI assigned or found on this object.", this);
            enabled = false;
            return;
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

    /// <summary>Rebuilds the legend. Safe to call any time.</summary>
    public void Rebuild()
    {
        handlers.Clear();
        handlers.AddRange(FindObjectsOfType<PlayerInputHandler>());

        // Stable, readable order: Player 1 before Player 2.
        handlers.Sort((a, b) => a.BotIndex.CompareTo(b.BotIndex));

        builder.Clear();

        if (showHeading && !string.IsNullOrEmpty(heading))
        {
            builder.AppendLine(heading);
        }

        bool any = false;

        if (showPlayerControls)
        {
            foreach (PlayerInputHandler handler in handlers)
            {
                // Bot 0 is the AI - it has no human controls to list.
                if (handler.BotIndex <= 0) continue;
                if (!handler.IsPaired) continue;

                if (any) builder.AppendLine();
                any = true;

                builder.AppendLine(BotName(handler.BotIndex));
                AppendDriveLines(builder, handler);
                builder.AppendLine("Boost: " + BoostLabel(handler));
            }

            if (!any)
            {
                builder.AppendLine("No controller paired");
                any = true;
            }
        }

        if (showSystemKeys)
        {
            if (any) builder.AppendLine();
            any = true;

            AppendSystemKeys();
        }

        string text = builder.ToString().TrimEnd();

        // Only touch the TMP object when something actually changed - setting
        // text forces a mesh rebuild, and this runs on a timer.
        if (text == lastText) return;

        lastText = text;
        target.text = text;
    }

    /// <summary>
    /// The game hotkeys, taken from the actual Input.GetKeyDown calls in the
    /// code rather than a hand-written list.
    ///
    /// The C and V keys only do anything when there is an AI in the scene
    /// (AI.cs owns the idle toggle, ChildCone.cs owns the cone view), so they
    /// are hidden in PVP where listing them would be misleading.
    ///
    /// NOTE: the old hand-written HUD text also listed "Reset Bot: B". Nothing
    /// implements that - B is bound to an unused "Join" action and no script
    /// reads KeyCode.B - so it is deliberately not listed here.
    /// </summary>
    void AppendSystemKeys()
    {
        builder.Append(SystemKeysText(FindObjectOfType<AI>() != null));
    }

    /// <summary>
    /// The game hotkeys as display text, adapted to what is actually available:
    /// the AI debug keys only when there is an AI, and the gamepad equivalents
    /// only when a pad is connected.
    ///
    /// Static so the exit hint in the arena corner can render the same text
    /// without duplicating the key names - if a binding changes, both follow.
    /// </summary>
    public static string SystemKeysText(bool includeAIKeys)
    {
        var sb = new StringBuilder();
        bool pad = Gamepad.current != null;

        // Named per brand rather than hardcoded: Select and Start are Minus
        // and Plus on a Switch Pro pad, View and Menu on an Xbox pad, and
        // Share and Options on a DualShock.
        GamepadFamily family = pad ? ControllerType.Detect(Gamepad.current) : GamepadFamily.None;

        if (includeAIKeys)
        {
            sb.AppendLine("Manual switch to Idle: C");
            sb.AppendLine("Toggle Cone View: V");
        }

        sb.AppendLine(pad
            ? $"Restart: L / {ControllerType.SelectButtonLabel(family)}"
            : "Restart: L");

        /*  The pad buttons go on their own continuation line rather than onto
         *  the end of the keyboard one.
         *
         *  Width is the reason. These blocks have word wrap switched off, and
         *  the left column in PVP has room for about 23 characters before it
         *  runs into the arena. "Back to Menu: O / Circle / Start" is 32 and
         *  would have overlapped on a PlayStation pad - the same failure as
         *  the old drive label. Split across two short lines, nothing has to
         *  be dropped to make it fit.                                        */
        string padBack = pad
            ? $"  or {ControllerType.EastButtonLabel(family)} / {ControllerType.StartButtonLabel(family)}"
            : null;

        // In a browser build Esc cannot quit - a page cannot close its own tab -
        // so GameOrganizer sends it to the menu instead. Advertising "Quit" there
        // would be telling the player something untrue.
#if UNITY_WEBGL && !UNITY_EDITOR
        sb.AppendLine("Back to Menu: O / Esc");
        if (padBack != null) sb.AppendLine(padBack);
#else
        sb.AppendLine("Back to Menu: O");
        if (padBack != null) sb.AppendLine(padBack);
        sb.AppendLine("Quit Game: Esc");
#endif

        return sb.ToString();
    }

    static string BotName(int botIndex) => botIndex switch
    {
        1 => "PLAYER 1",
        2 => "PLAYER 2",
        _ => $"BOT {botIndex}",
    };

    /// <summary>
    /// What to show for steering and throttle.
    ///
    /// Written as SHORT lines, one per line, rather than one descriptive
    /// sentence. Both controls blocks have word wrap switched off - the left
    /// one in PVP and the right-hand column in the arena - so a long line does
    /// not wrap, it runs off sideways across the arena. That is what was
    /// overlapping: "Drive: Left or Right Stick (up/down moves, left/right
    /// turns)" is about sixty characters and simply did not fit.
    ///
    /// The full explanation still exists, in the how-to-play panel, which has
    /// a whole screen to put it on.
    /// </summary>
    static void AppendDriveLines(StringBuilder sb, PlayerInputHandler handler)
    {
        foreach (string line in DriveLines(handler)) sb.AppendLine(line);
    }

    /// <summary>
    /// Falls back to the gamepad lines whenever a pad is detected, so
    /// Auto-profile players read correctly too.
    /// </summary>
    static string[] DriveLines(PlayerInputHandler handler)
    {
        switch (handler.Profile)
        {
            case PlayerInputHandler.InputProfile.KeyboardLeft:
                return new[] { "Drive: W A S D" };

            case PlayerInputHandler.InputProfile.KeyboardRight:
                return new[] { "Drive: Arrow Keys" };

            case PlayerInputHandler.InputProfile.Gamepad:
                return DriveScheme.GamepadDriveLines(DriveScheme.Current);

            default:
                // Auto: infer from whether a pad is actually paired.
                return handler.ControllerFamily == GamepadFamily.None
                    ? new[] { "Drive: W A S D" }
                    : DriveScheme.GamepadDriveLines(DriveScheme.Current);
        }
    }

    /// <summary>
    /// One player's controls as display lines. Shared so the arena corner panel
    /// and the bottom HUD cannot describe the controls differently.
    /// </summary>
    public static string PlayerControlsText(PlayerInputHandler handler)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BotName(handler.BotIndex));
        AppendDriveLines(sb, handler);
        sb.AppendLine("Boost: " + BoostLabel(handler));
        return sb.ToString();
    }

    static string BoostLabel(PlayerInputHandler handler)
    {
        switch (handler.Profile)
        {
            case PlayerInputHandler.InputProfile.KeyboardLeft:
                return "G";

            case PlayerInputHandler.InputProfile.KeyboardRight:
                // Not Right Ctrl: Mac laptop keyboards have no right Control
                // key at all. Right Shift exists on every layout and sits next
                // to the arrow keys this player is already using.
                return "Right Shift";

            case PlayerInputHandler.InputProfile.Gamepad:
                return ControllerType.SouthButtonLabel(handler.ControllerFamily);

            default:
                return handler.ControllerFamily == GamepadFamily.None
                    ? "G"
                    : ControllerType.SouthButtonLabel(handler.ControllerFamily);
        }
    }
}
