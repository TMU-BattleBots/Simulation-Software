using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

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
                builder.AppendLine("Drive: " + DriveLabel(handler));
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
        bool aiPresent = FindObjectOfType<AI>() != null;

        if (aiPresent)
        {
            builder.AppendLine("Manual switch to Idle: C");
            builder.AppendLine("Toggle Cone View: V");
        }

        builder.AppendLine("Restart: L");
        builder.AppendLine("Back to Menu: O");
        builder.AppendLine("Quit Game: P");
    }

    static string BotName(int botIndex) => botIndex switch
    {
        1 => "PLAYER 1",
        2 => "PLAYER 2",
        _ => $"BOT {botIndex}",
    };

    /// <summary>
    /// What to show for steering and throttle. Falls back to the gamepad labels
    /// whenever a pad is detected, so Auto-profile players read correctly too.
    /// </summary>
    static string DriveLabel(PlayerInputHandler handler)
    {
        switch (handler.Profile)
        {
            case PlayerInputHandler.InputProfile.KeyboardLeft:
                return "W A S D";

            case PlayerInputHandler.InputProfile.KeyboardRight:
                return "Arrow Keys";

            case PlayerInputHandler.InputProfile.Gamepad:
                return GamepadDriveLabel(handler.ControllerFamily);

            default:
                // Auto: infer from whether a pad is actually paired.
                return handler.ControllerFamily == GamepadFamily.None
                    ? "W A S D"
                    : GamepadDriveLabel(handler.ControllerFamily);
        }
    }

    static string GamepadDriveLabel(GamepadFamily family)
    {
        string accel = ControllerType.AccelerateLabel(family);
        string reverse = ControllerType.ReverseLabel(family);

        return $"Stick, {accel} / {reverse}";
    }

    static string BoostLabel(PlayerInputHandler handler)
    {
        switch (handler.Profile)
        {
            case PlayerInputHandler.InputProfile.KeyboardLeft:
                return "G";

            case PlayerInputHandler.InputProfile.KeyboardRight:
                return "Right Ctrl";

            case PlayerInputHandler.InputProfile.Gamepad:
                return ControllerType.SouthButtonLabel(handler.ControllerFamily);

            default:
                return handler.ControllerFamily == GamepadFamily.None
                    ? "G"
                    : ControllerType.SouthButtonLabel(handler.ControllerFamily);
        }
    }
}
