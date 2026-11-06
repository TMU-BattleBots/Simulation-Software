using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;

/// <summary>
/// Which family of gamepad a player is holding.
/// </summary>
public enum GamepadFamily
{
    None,           // no gamepad paired (keyboard player)
    Xbox,           // XInput: Xbox 360 / One / Series
    PlayStation,    // DualShock 3/4, DualSense
    Switch,         // Switch Pro controller
    Generic,        // recognised as a Gamepad but not a family we special-case
}

/// <summary>
/// Detects what kind of controller a player is using, and gives back the right
/// button label for it.
///
/// WHY THIS EXISTS, AND WHAT IT IS NOT FOR
///   You do NOT need separate bindings per controller brand. The Input Actions
///   asset binds to the abstract &lt;Gamepad&gt; layout, and Xbox, PlayStation and
///   Switch Pro controllers all inherit from it - so &lt;Gamepad&gt;/buttonSouth
///   already resolves to A on Xbox, Cross on PlayStation and B on a Switch Pro
///   pad, with no code. Duplicating the bindings per brand would be redundant
///   and risks the same action firing twice.
///
///   What is NOT automatic is knowing which brand is plugged in, which you need
///   for on-screen prompts ("Press A" vs "Press Cross") and for any case where
///   you genuinely want different behaviour per brand. That is this class's job.
///
/// USAGE
///   var family = ControllerType.Detect(playerInput);
///   string boost = ControllerType.SouthButtonLabel(family);   // "A", "Cross", ...
/// </summary>
public static class ControllerType
{
    /// <summary>Work out the family from the first gamepad paired to a PlayerInput.</summary>
    public static GamepadFamily Detect(PlayerInput playerInput)
    {
        if (playerInput == null) return GamepadFamily.None;

        foreach (InputDevice device in playerInput.devices)
        {
            GamepadFamily family = Detect(device);
            if (family != GamepadFamily.None) return family;
        }

        return GamepadFamily.None;
    }

    /// <summary>Work out the family from a single device.</summary>
    public static GamepadFamily Detect(InputDevice device)
    {
        if (device is not Gamepad) return GamepadFamily.None;

        // Concrete layouts first - these are the reliable checks.
        if (device is XInputController) return GamepadFamily.Xbox;
        if (device is DualShockGamepad) return GamepadFamily.PlayStation;

        // The Switch Pro layout is only present on some platforms, so match on
        // the layout name rather than taking a hard type dependency.
        string layout = device.layout ?? string.Empty;
        if (layout.Contains("SwitchPro")) return GamepadFamily.Switch;

        // Some third-party pads report only as a generic HID Gamepad. Fall back
        // to the product name, which is unreliable but better than nothing.
        string product = (device.description.product ?? string.Empty).ToLowerInvariant();
        if (product.Contains("xbox") || product.Contains("xinput")) return GamepadFamily.Xbox;
        if (product.Contains("dualshock") || product.Contains("dualsense") ||
            product.Contains("wireless controller") || product.Contains("playstation"))
            return GamepadFamily.PlayStation;

        return GamepadFamily.Generic;
    }

    /// <summary>
    /// Label for the east face button - the "back out" control.
    ///
    /// Note that east is NOT the same letter on every pad: it is B on Xbox,
    /// Circle on PlayStation, and A on a Switch Pro controller, whose A and B
    /// are swapped relative to Xbox. Naming the physical position rather than
    /// the letter is the whole reason this method exists.
    /// </summary>
    public static string EastButtonLabel(GamepadFamily family) => family switch
    {
        GamepadFamily.Xbox => "B",
        GamepadFamily.PlayStation => "Circle",
        GamepadFamily.Switch => "A",
        // Just "East", not "East Button": the HUD line has room for about 23
        // characters before it runs into the arena, and the longer form tipped
        // it over.
        GamepadFamily.Generic => "East",
        _ => "O",                       // keyboard back key
    };

    /// <summary>Label for the south face button - the Boost control.</summary>
    public static string SouthButtonLabel(GamepadFamily family) => family switch
    {
        GamepadFamily.Xbox => "A",
        GamepadFamily.PlayStation => "Cross",
        GamepadFamily.Switch => "B",
        GamepadFamily.Generic => "South Button",
        _ => "G",                       // keyboard boost key
    };

    /*  DRIVING LAYOUTS LIVE IN DriveScheme, NOT HERE
     *
     *  Which sticks drive the bot is now a player choice between two layouts,
     *  so the labels for it belong with the thing that switches the bindings.
     *  See DriveScheme for both layouts, why each is shaped the way it is, and
     *  why the triggers drive in neither.
     *
     *  This class stays responsible for what it is actually good at: naming
     *  buttons per controller brand. AccelerateLabel / ReverseLabel below are
     *  kept for text that wants to name a trigger.                             */

    /// <summary>Label for the accelerate control. Kept for callers that want the trigger name.</summary>
    public static string AccelerateLabel(GamepadFamily family) => family switch
    {
        GamepadFamily.Xbox => "RT",
        GamepadFamily.PlayStation => "R2",
        GamepadFamily.Switch => "ZR",
        GamepadFamily.Generic => "Right Trigger",
        _ => "W",
    };

    /// <summary>Label for the reverse control.</summary>
    public static string ReverseLabel(GamepadFamily family) => family switch
    {
        GamepadFamily.Xbox => "LT",
        GamepadFamily.PlayStation => "L2",
        GamepadFamily.Switch => "ZL",
        GamepadFamily.Generic => "Left Trigger",
        _ => "S",
    };
}
