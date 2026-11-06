using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Makes the menu usable without a mouse - arrow keys and Enter on a keyboard,
/// the stick and the south button on a pad.
///
/// TWO SEPARATE THINGS WERE WRONG
///
///   1. No starting point. The buttons already had Navigation set to Automatic,
///      so Unity could work out which button sits above which - but the
///      EventSystem's First Selected field was empty, so nothing was ever
///      selected and a navigation input had nothing to move away from. Every
///      key press was silently discarded. SelectFirstButton fixes that.
///
///   2. The wrong input module. The EventSystem in the menu scene carries the
///      LEGACY StandaloneInputModule, which reads the old Input Manager axes
///      ("Horizontal", "Vertical", "Submit") rather than the Input System asset
///      the rest of the game uses. Arrow keys happened to work because the old
///      Vertical axis defaults to the arrow keys. Sticks did not, because the
///      old Input Manager addresses a pad as a numbered axis, and which axis a
///      stick lands on differs between Xbox, PlayStation and everything else -
///      the exact brand problem the Input System exists to solve.
///
///      So this swaps the legacy module for InputSystemUIInputModule at
///      runtime. That routes Navigate, Submit and Cancel through the same
///      &lt;Gamepad&gt; bindings as the rest of the game, so every pad works
///      without naming axis numbers, and the mouse keeps working as before.
///
///      NOTE: this was NOT caused by the stick remap. Menu navigation never
///      went through the Input System at all.
///
/// SETUP
///   None. It installs itself the same way InputAssigner does.
/// </summary>
public class MenuNavigation : MonoBehaviour
{
    [Tooltip("Re-select a button whenever the selection is lost, so the keyboard " +
             "and pad never go dead after a stray mouse click.")]
    [SerializeField] bool keepSelectionAlive = true;

    [Tooltip("Replace the legacy StandaloneInputModule with the Input System's " +
             "UI module, so gamepad sticks can drive the menu. See the class note.")]
    [SerializeField] bool upgradeInputModule = true;

    [Tooltip("How often to check that something is still selected, in seconds.")]
    [SerializeField] float checkInterval = 0.25f;

    float timer;

    /// <summary>
    /// While a modal panel is up, navigation is confined to the buttons inside
    /// it. Without this the stick wanders onto the menu buttons hidden behind
    /// the backdrop, where the player cannot see what is selected.
    /// </summary>
    static Transform modalRoot;

    public static void SetModalRoot(Transform root)
    {
        modalRoot = root;

        // Moving in or out of a modal invalidates whatever was selected.
        var nav = FindObjectOfType<MenuNavigation>();
        if (nav != null) nav.SelectFirstButton();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        EnsureExists();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // A modal cannot survive the scene it was built in.
        modalRoot = null;

        EnsureExists();
    }

    static void EnsureExists()
    {
        if (FindObjectOfType<MenuNavigation>() != null) return;

        var host = new GameObject("[MenuNavigation]");
        host.AddComponent<MenuNavigation>();
    }

    IEnumerator Start()
    {
        if (upgradeInputModule) UpgradeInputModule();

        // Let the scene's own objects finish their Start before selecting.
        yield return null;

        SelectFirstButton();
    }

    /// <summary>
    /// Swaps the legacy StandaloneInputModule for the Input System's UI module.
    ///
    /// The replacement is fed a FRESH copy of the input asset, built from the
    /// generated PlayerInputs wrapper rather than shared with the players. That
    /// matters: PlayerInputHandler narrows the players' asset with a binding
    /// mask when it pins a device to a bot, and a mask of "KeyboardLeft" on a
    /// shared asset would switch off every gamepad binding in it - including
    /// the UI ones, killing menu navigation as a side effect of pairing.
    /// </summary>
    void UpgradeInputModule()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;

        if (es.GetComponent<InputSystemUIInputModule>() != null) return;   // already upgraded

        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy == null) return;   // some other module - leave it alone

        // Destroy the legacy module BEFORE adding the new one. Two active
        // modules would both raise navigation events and the selection would
        // jump two buttons per press.
        DestroyImmediate(legacy);

        var module = es.gameObject.AddComponent<InputSystemUIInputModule>();

        // Assigning the asset auto-wires Navigate/Submit/Cancel/Point/Click by
        // name, because the UI map in the asset uses the default action names.
        var fresh = new PlayerInputs();
        module.actionsAsset = fresh.asset;

        Debug.Log("[MenuNavigation] Swapped the legacy input module for " +
                  "InputSystemUIInputModule, so gamepad sticks can drive the menu.");
    }

    void Update()
    {
        if (!keepSelectionAlive) return;

        timer += Time.unscaledDeltaTime;
        if (timer < checkInterval) return;
        timer = 0f;

        EventSystem es = EventSystem.current;
        if (es == null) return;

        GameObject selected = es.currentSelectedGameObject;

        // A null or inactive selection means the keyboard and pad have nothing
        // to navigate from. So does a selection that has been left behind
        // outside an open modal.
        bool lost = selected == null
                    || !selected.activeInHierarchy
                    || (modalRoot != null && !selected.transform.IsChildOf(modalRoot));

        if (lost) SelectFirstButton();
    }

    /// <summary>Selects the topmost interactable button in the scene, or in the open modal.</summary>
    void SelectFirstButton()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;

        // Inside a modal, only its own buttons are reachable.
        var candidates = modalRoot != null
            ? modalRoot.GetComponentsInChildren<Button>(false)
            : FindObjectsOfType<Button>();

        // Topmost on screen = highest y in world space, which matches the
        // reading order of a vertical menu.
        Button first = candidates
            .Where(b => b.interactable && b.gameObject.activeInHierarchy)
            .Where(b => b.GetComponent<SkipAutoSelect>() == null)   // see SkipAutoSelect
            .OrderByDescending(b => b.transform.position.y)
            .FirstOrDefault();

        if (first == null) return;   // not a menu scene

        es.SetSelectedGameObject(first.gameObject);
    }
}
