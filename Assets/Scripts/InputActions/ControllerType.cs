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

    /// <summary>Label for the south face button - the Boost control.</summary>
    public static string SouthButtonLabel(GamepadFamily family) => family switch
    {
        GamepadFamily.Xbox => "A",
        GamepadFamily.PlayStation => "Cross",
        GamepadFamily.Switch => "B",
        GamepadFamily.Generic => "South Button",
        _ => "G",                       // keyboard boost key
    };

    /// <summary>Label for the accelerate control.</summary>
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
