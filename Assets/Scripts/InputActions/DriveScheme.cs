using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Which way the two sticks drive the bot.
/// </summary>
public enum DriveSchemeKind
{
    /// <summary>
    /// Either stick does the whole job: up/down moves, left/right turns. One
    /// hand is enough, and a player can use whichever stick they prefer.
    /// </summary>
    EitherStick,

    /// <summary>
    /// Left stick is the throttle, right stick is the steering - the layout a
    /// twin-stick driving game usually uses. Needs both hands, but separates
    /// "how fast" from "which way".
    /// </summary>
    SplitSticks,
}

/// <summary>
/// Chooses between the two gamepad driving layouts, and switches the bindings
/// to match.
///
/// HOW THE SWITCH WORKS
///   All three gamepad layouts are bound in the asset at once, as three
///   composites on the Move action:
///
///     Gamepad             left stick, full 2D     \  EitherStick
///     GamepadRightStick   right stick, full 2D    /
///     GamepadSplit        left stick Y + right stick X   -  SplitSticks
///
///   Switching does not rewrite the asset. It applies an empty binding
///   OVERRIDE to every part of the composites that are not wanted, which makes
///   them resolve to no control at all, and removes the override to bring them
///   back. The asset on disk is never touched, so this is safe to call as often
///   as you like and leaves nothing to clean up.
///
/// WHY EACH LAYOUT IS SHAPED THE WAY IT IS
///   EitherStick is two SEPARATE composites. Two composites on one action
///   compete - Unity's magnitude disambiguation gives the action to whichever
///   is pushed furthest from centre. That is correct here: each stick already
///   does the whole job, so "the stick you are actually pushing wins" is what
///   a player expects.
///
///   SplitSticks is a SINGLE composite. It has to be, because its two sticks
///   each supply only half of the drive vector and must be summed. As two
///   competing composites the throttle would cancel the steering mid-turn.
///
///   This is also why the triggers drive in neither layout: a held trigger sits
///   at magnitude 1 and would out-vote both sticks.
/// </summary>
public static class DriveScheme
{
    /// <summary>Composite names in the asset, per scheme.</summary>
    static readonly string[] EitherStickComposites = { "Gamepad", "GamepadRightStick" };
    static readonly string[] SplitStickComposites = { "GamepadSplit" };

    static readonly string[] AllComposites = { "Gamepad", "GamepadRightStick", "GamepadSplit" };

    static DriveSchemeKind current = DriveSchemeKind.EitherStick;

    /// <summary>
    /// Re-applies the chosen scheme whenever a scene loads.
    ///
    /// The choice is made in the menu, but the bots that read it are spawned by
    /// the arena scene afterwards - and a freshly loaded PlayerInput starts with
    /// every composite live, which would read both layouts at once.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        ApplyToAll();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToAll();

    /// <summary>
    /// The layout the player picked. Static so it survives the scene load - the
    /// choice is made in the menu and read by the arena, which does not exist
    /// yet at that point.
    /// </summary>
    public static DriveSchemeKind Current
    {
        get => current;
        set
        {
            current = value;
            ApplyToAll();
        }
    }

    /// <summary>Short name for a scheme, for menu buttons.</summary>
    public static string Title(DriveSchemeKind scheme) => scheme switch
    {
        DriveSchemeKind.SplitSticks => "Split Sticks",
        _ => "Either Stick",
    };

    /// <summary>One-line explanation, for the chooser and the how-to-play panel.</summary>
    public static string Description(DriveSchemeKind scheme) => scheme switch
    {
        DriveSchemeKind.SplitSticks => "Left stick moves, right stick turns",
        _ => "Either stick: up/down moves, left/right turns",
    };

    /// <summary>
    /// The drive lines for the on-screen controls block, already prefixed.
    ///
    /// Returned as separate SHORT lines rather than one descriptive sentence:
    /// the arena controls blocks have word wrap switched off, so a long line
    /// does not wrap, it just runs off sideways across the arena.
    /// </summary>
    public static string[] GamepadDriveLines(DriveSchemeKind scheme) => scheme switch
    {
        DriveSchemeKind.SplitSticks => new[] { "Move: Left Stick", "Turn: Right Stick" },
        _ => new[] { "Drive: Either Stick" },
    };

    /// <summary>
    /// Applies the current scheme to every input asset currently loaded.
    ///
    /// Called on a scene load and after a player is paired, because a
    /// PlayerInput that spawns later would otherwise still have every composite
    /// live and read both layouts at once.
    /// </summary>
    public static void ApplyToAll()
    {
        var seen = new HashSet<InputActionAsset>();

        foreach (PlayerInput playerInput in Object.FindObjectsOfType<PlayerInput>())
        {
            if (playerInput.actions == null) continue;
            if (!seen.Add(playerInput.actions)) continue;

            ApplyTo(playerInput.actions);
        }
    }

    /// <summary>Applies the current scheme to one asset.</summary>
    public static void ApplyTo(InputActionAsset asset)
    {
        if (asset == null) return;

        InputAction move = asset.FindAction("Move");
        if (move == null)
        {
            Debug.LogWarning("[DriveScheme] No 'Move' action in the asset; bindings unchanged.");
            return;
        }

        string[] wanted = current == DriveSchemeKind.SplitSticks
            ? SplitStickComposites
            : EitherStickComposites;

        foreach (string composite in AllComposites)
        {
            bool enable = System.Array.IndexOf(wanted, composite) >= 0;
            SetCompositeEnabled(move, composite, enable);
        }
    }

    /// <summary>
    /// Switches one composite on or off by overriding its parts.
    ///
    /// The composite is found by NAME rather than by a stored index - indices
    /// shift whenever a binding is added to the asset, and silently pointing at
    /// the wrong binding is a much nastier failure than not finding one.
    /// </summary>
    static void SetCompositeEnabled(InputAction move, string compositeName, bool enable)
    {
        bool inside = false;
        bool found = false;

        for (int i = 0; i < move.bindings.Count; i++)
        {
            InputBinding binding = move.bindings[i];

            if (binding.isComposite)
            {
                inside = binding.name == compositeName;
                if (inside) found = true;
                continue;
            }

            if (!inside || !binding.isPartOfComposite) continue;

            if (enable)
            {
                // Back to whatever the asset says.
                move.RemoveBindingOverride(i);
            }
            else
            {
                // An empty override path resolves to no control, which is how
                // the Input System disables a binding without editing the asset.
                move.ApplyBindingOverride(i, string.Empty);
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[DriveScheme] Composite '{compositeName}' not found on Move. " +
                             "The input asset may not have reimported since it was last changed.");
        }
    }
}
