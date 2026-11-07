using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

/// <summary>
/// Converts Input System callbacks into the vectors the Player script needs, and
/// decides which bot this device drives.
///
/// HOW THE PAIRING WORKS
///   Each arena scene contains pre-placed PlayerInput objects - three in PVP,
///   two in PVE. Unity gives each a playerIndex in the order they enable, so the
///   first is 0, the second 1, and so on. By default this handler drives the
///   Player bot whose own index matches.
///
///   That default is why PVP needed two gamepads: its two bots carry indices 1
///   and 2, so the PlayerInput at index 0 (the keyboard) matched no bot and did
///   nothing. Set Bot Index Override below to point a device at any bot.
///
///   Bot indices: 0 = AI, 1 = Player 1, 2 = Player 2.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    /// <summary>Which control scheme this device should be locked to.</summary>
    public enum InputProfile
    {
        /// <summary>Let Unity choose. This is the original behaviour.</summary>
        Auto = 0,
        /// <summary>Keyboard, left-hand keys: WASD to drive, G to boost.</summary>
        KeyboardLeft = 1,
        /// <summary>Keyboard, right-hand keys: arrows to drive, Right Shift to boost.</summary>
        KeyboardRight = 2,
        /// <summary>Any gamepad - Xbox, PlayStation, Switch Pro or generic.</summary>
        Gamepad = 3,
    }

    [Header("Pairing")]
    [Tooltip("Which bot this device drives. Leave at -1 to use this PlayerInput's " +
             "own player index, which is the original behaviour. " +
             "Bot indices: 0 = AI, 1 = Player 1, 2 = Player 2.")]
    [SerializeField] int botIndexOverride = -1;

    [Tooltip("Auto leaves device selection to Unity. Pick KeyboardLeft and " +
             "KeyboardRight on two different PlayerInput objects to get two " +
             "players sharing one keyboard.")]
    [SerializeField] InputProfile profile = InputProfile.Auto;

    [Tooltip("Which connected gamepad to use when Profile is Gamepad. " +
             "0 = first pad, 1 = second pad. Leave at -1 to derive it from this " +
             "PlayerInput's own index. Two players MUST be on different slots, " +
             "or they will share a pad and both bots will move together.")]
    [SerializeField] int gamepadSlot = -1;

    [SerializeField] PlayerInputs playerInputs;

    private Player playerScript;
    private PlayerInput playerInput;
    private Vector2 boostVect;

    /// <summary>
    /// What kind of controller this player is holding, for on-screen prompts.
    /// See ControllerType - you do NOT need per-brand bindings, only per-brand
    /// labels.
    /// </summary>
    public GamepadFamily ControllerFamily { get; private set; } = GamepadFamily.None;

    /// <summary>Which bot this device ended up driving. -1 if it found none.</summary>
    public int BotIndex { get; private set; } = -1;

    /// <summary>
    /// The control scheme actually in use. This is whatever InputAssigner handed
    /// out, or the serialised profile if nothing assigned one.
    /// </summary>
    public InputProfile Profile { get; private set; } = InputProfile.Auto;

    /// <summary>The manual preference set in the Inspector, before assignment.</summary>
    public InputProfile PreferredProfile => profile;

    /// <summary>True once this device is actually driving a bot.</summary>
    public bool IsPaired => playerScript != null;

    void Start()
    {
        playerInput = GetComponent<PlayerInput>();

        ApplyControlScheme();

        int wantedBot = (botIndexOverride >= 0) ? botIndexOverride : playerInput.playerIndex;
        BotIndex = wantedBot;

        var players = FindObjectsOfType<Player>();
        playerScript = players.FirstOrDefault(m => m.GetPlayerIndex() == wantedBot);

        if (playerScript == null)
        {
            // This used to fail silently, which is why "my controller does
            // nothing" was the most common input complaint - the device was
            // fine, it just had no bot to drive.
            Debug.LogWarning(
                $"[PlayerInputHandler] Wanted bot index {wantedBot} " +
                $"(device index {playerInput.playerIndex}), but no Player in this scene has it. " +
                $"This device will do nothing. Set Bot Index Override, or check the bots' indices.", this);
            return;
        }

        // InputAssigner may already have set this from a specific device.
        if (ControllerFamily == GamepadFamily.None)
        {
            ControllerFamily = ControllerType.Detect(playerInput);
        }
    }

    /// <summary>
    /// Pins this device to one control scheme.
    ///
    /// This is what makes two players on ONE keyboard possible. Unity treats the
    /// whole keyboard as a single device and its automatic pairing will not hand
    /// the same device to two players, so the pairing has to be forced. Two
    /// schemes - KeyboardLeft and KeyboardRight - both require &lt;Keyboard&gt;
    /// and carry different bindings.
    ///
    /// Leaving the profile on Auto skips all of this, so nothing changes unless
    /// you opt in.
    /// </summary>
    void ApplyControlScheme()
    {
        if (profile == InputProfile.Auto || playerInput == null) return;

        InputDevice device = profile switch
        {
            InputProfile.KeyboardLeft => Keyboard.current,
            InputProfile.KeyboardRight => Keyboard.current,
            InputProfile.Gamepad => PickGamepad(),
            _ => null,
        };

        if (device == null)
        {
            Debug.LogWarning(
                $"[PlayerInputHandler] Profile is {profile} but no matching device is connected. " +
                $"Leaving device selection to InputAssigner.", this);
            return;
        }

        AssignProfile(profile, device);
    }

    /// <summary>
    /// Pin this player to a control scheme and a specific device.
    ///
    /// Called by InputAssigner, which is what guarantees two players never end
    /// up on the same device. Unity's own pairing will happily give one gamepad
    /// to several players, which is what made both bots move together.
    /// </summary>
    public void AssignProfile(InputProfile newProfile, InputDevice device)
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (playerInput == null || device == null || newProfile == InputProfile.Auto) return;

        /*  THE IMPORTANT LINE  ---------------------------------------------
         *  PlayerInput re-pairs a player to whatever device last sent input,
         *  which is sensible for one player and disastrous for two. Touching
         *  the gamepad made the keyboard player switch itself onto the gamepad,
         *  so both bots read the same stick and moved in lockstep. Pinning a
         *  control scheme does not survive that - it just re-pins on the next
         *  input event. This has to be off before anything else is worth doing.
         * ---------------------------------------------------------------- */
        playerInput.neverAutoSwitchControlSchemes = true;

        string scheme = newProfile.ToString();
        bool pinned = false;

        /*  WHY THIS IS GUARDED  ---------------------------------------------
         *  PlayerInput does not own an InputUser until it has successfully
         *  paired at least one device, and SwitchCurrentControlScheme reads
         *  user.index internally. Calling it before then throws
         *  "InvalidOperationException: Invalid user" from deep inside the Input
         *  System, which is what happened here - the pairing silently never
         *  took effect and both bots stayed on the same device.
         * ------------------------------------------------------------------ */
        if (playerInput.user.valid)
        {
            try
            {
                playerInput.SwitchCurrentControlScheme(scheme, device);
                pinned = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[PlayerInputHandler] Could not switch bot {BotIndex} to scheme " +
                    $"'{scheme}' on '{device.displayName}': {e.Message}. " +
                    $"Falling back to action-asset filtering.", this);
            }
        }

        /*  FALLBACK THAT NEEDS NO InputUser  ---------------------------------
         *  Restrict this player's own action asset to one device and one binding
         *  group. PlayerInput clones the asset per player, so this isolates two
         *  players just as effectively as device pairing does - and it works
         *  even before an InputUser exists.
         *
         *  It is also what makes two players share ONE keyboard: the device is
         *  the same for both, but the binding masks differ (KeyboardLeft vs
         *  KeyboardRight), so they respond to different keys.
         * ------------------------------------------------------------------ */
        if (!pinned && playerInput.actions != null)
        {
            playerInput.actions.devices = new InputDevice[] { device };
            playerInput.actions.bindingMask = InputBinding.MaskByGroup(scheme);
            pinned = true;
        }

        if (!pinned)
        {
            Debug.LogWarning(
                $"[PlayerInputHandler] Bot {BotIndex} could not be pinned to any device; " +
                $"it may share input with another player.", this);
            return;
        }

        /*  PlayerInput clones the action asset per player, so the clone this bot
         *  just got still has every gamepad drive composite live - it would read
         *  both stick layouts at once. Re-apply the chosen scheme to it.        */
        if (playerInput.actions != null) DriveScheme.ApplyTo(playerInput.actions);

        Profile = newProfile;
        ControllerFamily = ControllerType.Detect(device);
    }

    /// <summary>The devices this player is actually paired to right now.</summary>
    public IEnumerable<InputDevice> PairedDevices()
    {
        if (playerInput == null) return System.Array.Empty<InputDevice>();

        // playerInput.devices reads through the InputUser, which throws if there
        // is no valid user - so check first, and fall back to whatever the
        // action asset was restricted to.
        if (playerInput.user.valid) return playerInput.devices;

        var assetDevices = playerInput.actions?.devices;
        if (assetDevices.HasValue) return assetDevices.Value.ToArray();

        return System.Array.Empty<InputDevice>();
    }

    /// <summary>
    /// What this player is actually paired to right now, for logging. Reads the
    /// live state rather than what we asked for, so it can prove the assignment
    /// took effect instead of just reporting our intent.
    /// </summary>
    public string PairedDeviceSummary()
    {
        if (playerInput == null) return "no PlayerInput";

        var paired = PairedDevices().ToArray();

        string devices = paired.Length == 0
            ? "(no devices)"
            : string.Join(" + ", paired.Select(d => d.displayName));

        // currentControlScheme also goes through the InputUser, so only read it
        // when there is one; otherwise report the profile we pinned ourselves.
        string scheme = playerInput.user.valid && !string.IsNullOrEmpty(playerInput.currentControlScheme)
            ? playerInput.currentControlScheme
            : $"{Profile} (asset-level)";

        return $"{scheme} on {devices}";
    }

    /// <summary>
    /// Picks which physical gamepad this player uses.
    ///
    /// This is the usual cause of "both bots move together": two players end up
    /// on the SAME pad, so one stick drives both. Unity happily pairs one device
    /// to several players - it does not treat that as an error - so the clash is
    /// silent unless we say something.
    /// </summary>
    Gamepad PickGamepad()
    {
        var pads = Gamepad.all;
        if (pads.Count == 0) return null;

        int wanted = (gamepadSlot >= 0) ? gamepadSlot : playerInput.playerIndex;

        if (wanted >= pads.Count)
        {
            Debug.LogWarning(
                $"[PlayerInputHandler] Wanted gamepad slot {wanted} but only {pads.Count} " +
                $"gamepad(s) are connected, so this player falls back to pad 0 and will " +
                $"SHARE it with another player - both bots will move together. " +
                $"Plug in another pad, or set this player's Profile to KeyboardLeft/KeyboardRight.", this);
        }

        int slot = Mathf.Clamp(wanted, 0, pads.Count - 1);
        return pads[slot];
    }

    /// <summary>
    /// Called by the PlayerInput component via Send Messages, which finds this
    /// method by name. Renaming it silently unbinds the action.
    /// </summary>
    public void OnMove(InputValue context)
    {
        if (playerScript == null) return;

        playerScript.SetInputs(context.Get<Vector2>());
    }

    /// <summary>
    /// Boost is bound as a Vector2 rather than a button ON PURPOSE.
    ///
    /// A button action only fired on press and never reported being held, so
    /// hold-to-boost did not work. Vector2 values are polled continuously, so
    /// reading .y off one gives us the hold. It looks wrong; it is the fix. If
    /// you rework this, test hold-to-boost specifically.
    /// </summary>
    public void OnBoost(InputValue context)
    {
        if (playerScript == null) return;

        boostVect = context.Get<Vector2>();
        playerScript.Boost2(boostVect.y);
    }
}
