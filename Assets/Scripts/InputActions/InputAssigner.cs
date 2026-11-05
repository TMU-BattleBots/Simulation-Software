using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Hands every player its own input device when a match starts, so no two
/// players ever share one.
///
/// THE PROBLEM THIS SOLVES
///   Unity will happily pair a single gamepad to several players and does not
///   treat that as an error. With nothing pinning each PlayerInput to a distinct
///   device, one stick ended up driving both bots - they moved together.
///
/// THE RULE
///   Players are served in bot-index order (Player 1, then Player 2):
///     1. Give each player its own gamepad, while unused gamepads remain.
///     2. When the gamepads run out, fall back to the two keyboard halves -
///        WASD + G first, then arrows + Right Ctrl.
///   A device is never handed out twice.
///
///   So with two pads, both players use pads. With one pad, Player 1 gets the
///   pad and Player 2 gets WASD. With no pads, it is WASD versus the arrow keys.
///   Plug a controller in mid-session and everyone is reassigned by the same
///   rule.
///
/// SETUP
///   None. This installs itself - see AutoInstall below - so there is nothing to
///   drag into a scene. Add the component by hand only if you want to change the
///   settings below; a hand-placed one takes over and no extra copy is created.
///
///   A PlayerInputHandler whose Input Profile is set to something other than
///   Auto is treated as a manual override and is left alone.
/// </summary>
public class InputAssigner : MonoBehaviour
{
    /*  SELF-INSTALL  -------------------------------------------------------
     *  A script does nothing until something in a scene references it, and
     *  remembering to drop this into every arena scene is exactly the kind of
     *  step that gets forgotten - leaving both bots sharing one gamepad with no
     *  obvious cause.
     *
     *  RuntimeInitializeOnLoadMethod runs automatically when the game starts, so
     *  this creates itself in whatever scene is loaded and again on every scene
     *  change. It never makes a second copy, so adding the component by hand
     *  still works and wins.
     * -------------------------------------------------------------------- */
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
        // A hand-placed instance always wins.
        if (FindObjectOfType<InputAssigner>() != null) return;

        var host = new GameObject("[InputAssigner]");
        host.AddComponent<InputAssigner>();
    }

    [Tooltip("Hand out gamepads before falling back to the keyboard. Turn off to " +
             "force keyboard play even when controllers are connected.")]
    [SerializeField] bool preferGamepads = true;

    [Tooltip("Reassign automatically when a controller is plugged in or removed.")]
    [SerializeField] bool reassignOnDeviceChange = true;

    [Tooltip("Log what each player was given. Useful the first time you set this up.")]
    [SerializeField] bool logAssignments = true;

    // The keyboard fallbacks, in the order they get handed out.
    static readonly PlayerInputHandler.InputProfile[] KeyboardProfiles =
    {
        PlayerInputHandler.InputProfile.KeyboardLeft,
        PlayerInputHandler.InputProfile.KeyboardRight,
    };

    readonly List<PlayerInputHandler> handlers = new List<PlayerInputHandler>();

    void OnEnable()
    {
        if (reassignOnDeviceChange) InputSystem.onDeviceChange += HandleDeviceChange;
    }

    void OnDisable()
    {
        if (reassignOnDeviceChange) InputSystem.onDeviceChange -= HandleDeviceChange;
    }

    IEnumerator Start()
    {
        // Wait one frame. PlayerInput assigns its playerIndex during OnEnable and
        // PlayerInputHandler resolves its bot in Start, so assigning any earlier
        // would race them.
        yield return null;

        Assign();
    }

    void HandleDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad) return;
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Removed) return;

        Assign();
    }

    /// <summary>Give every player a device of its own. Safe to call any time.</summary>
    public void Assign()
    {
        handlers.Clear();

        foreach (PlayerInputHandler h in FindObjectsOfType<PlayerInputHandler>())
        {
            // Bot 0 is the AI - it takes no input.
            if (h.BotIndex <= 0) continue;

            // An explicit Inspector choice wins; this is the manual escape hatch.
            if (h.PreferredProfile != PlayerInputHandler.InputProfile.Auto) continue;

            handlers.Add(h);
        }

        handlers.Sort((a, b) => a.BotIndex.CompareTo(b.BotIndex));

        var pads = Gamepad.all;
        int padCursor = 0;
        int keyboardCursor = 0;

        foreach (PlayerInputHandler handler in handlers)
        {
            if (preferGamepads && padCursor < pads.Count)
            {
                Gamepad pad = pads[padCursor++];
                handler.AssignProfile(PlayerInputHandler.InputProfile.Gamepad, pad);

                if (logAssignments)
                    Debug.Log($"[InputAssigner] Bot {handler.BotIndex} -> gamepad \"{pad.displayName}\".", handler);

                continue;
            }

            if (keyboardCursor < KeyboardProfiles.Length && Keyboard.current != null)
            {
                var profile = KeyboardProfiles[keyboardCursor++];
                handler.AssignProfile(profile, Keyboard.current);

                if (logAssignments)
                    Debug.Log($"[InputAssigner] Bot {handler.BotIndex} -> {profile}.", handler);

                continue;
            }

            // More players than devices. Only reachable with three-plus players
            // and no controllers, since the keyboard only splits two ways.
            Debug.LogWarning(
                $"[InputAssigner] No device left for bot {handler.BotIndex}. " +
                $"The keyboard only splits two ways - plug in a controller.", handler);
        }

        VerifyAssignments();
    }

    /// <summary>
    /// Reads back what each player is ACTUALLY paired to and complains if two
    /// players share a device.
    ///
    /// Worth doing because the failure is silent otherwise: Unity does not treat
    /// a shared device as an error, so the only visible symptom is two bots
    /// moving in lockstep, which looks like a movement bug rather than an input
    /// one. This reports the real state, not what we asked for.
    /// </summary>
    void VerifyAssignments()
    {
        var seen = new Dictionary<int, PlayerInputHandler>();

        foreach (PlayerInputHandler handler in handlers)
        {
            if (logAssignments)
                Debug.Log($"[InputAssigner] Bot {handler.BotIndex}: {handler.PairedDeviceSummary()}", handler);

            foreach (InputDevice device in handler.PairedDevices())
            {
                if (seen.TryGetValue(device.deviceId, out PlayerInputHandler other))
                {
                    Debug.LogError(
                        $"[InputAssigner] Bots {other.BotIndex} and {handler.BotIndex} are BOTH paired to " +
                        $"\"{device.displayName}\" - they will move together. " +
                        $"Connect another device, or set one player's Input Profile to " +
                        $"KeyboardLeft/KeyboardRight in the Inspector.", handler);
                }
                else
                {
                    seen[device.deviceId] = handler;
                }
            }
        }
    }
}
