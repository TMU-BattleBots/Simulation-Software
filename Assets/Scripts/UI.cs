using System;
using TMPro;
using UnityEngine;

/// <summary>
/// The AI debug readout: which vision cone is active, which state the state
/// machine is in, and optionally the difficulty. The display strings themselves
/// are set in the Inspector.
/// </summary>
public class UI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI textCone;
    [SerializeField] TextMeshProUGUI textState;
    [SerializeField] TextMeshProUGUI textStateDesc;

    [Tooltip("Optional. Leave empty to not show the difficulty at all.")]
    [SerializeField] TextMeshProUGUI textDifficulty;

    [SerializeField] GameObject AI;

    // One entry per cone. There are TEN cones (0-9).
    //
    // Changing the size here does NOT resize the array already saved in a scene
    // or prefab - serialised values win. Grow it in the Inspector too.
    [SerializeField] String[] ConeNameID = new String[10];

    [SerializeField] String[] StateNameID = new String[4];
    [SerializeField] String[] StateDescID = new String[4];

    [Tooltip("Shown when no cone is detecting anything.")]
    [SerializeField] String noDetectionText = "No bot detected";

    /*  WHAT THE CONE READOUT SHOWS  ----------------------------------------
     *  On the easier difficulties the AI acts on a DELAYED copy of its vision -
     *  see AI.reactionDelay. This readout used to poll VisionCone directly, so
     *  it showed the raw truth while the AI was still acting on older
     *  information. The two visibly disagreed, which made the panel misleading
     *  exactly when you most wanted to trust it.
     *
     *  It now reports what the AI actually believes, so the readout always
     *  explains the behaviour you are watching. Untick to see the raw sensor
     *  state instead, which is the right choice when debugging the cones
     *  themselves rather than the AI.
     * -------------------------------------------------------------------- */
    [Header("Cone readout")]
    [Tooltip("Show what the AI believes (delayed by its reaction time) rather " +
             "than the raw sensor state.")]
    [SerializeField] bool showPerceivedCone = true;

    [Tooltip("Append a marker while the AI's perception is behind the raw sensors.")]
    [SerializeField] bool flagPerceptionLag = true;

    [SerializeField] String lagSuffix = "  (lag)";

    AI AIParentScript;

    void Start()
    {
        if (AI == null)
        {
            Debug.LogError("[UI] The AI reference is not set.", this);
            enabled = false;
            return;
        }

        AIParentScript = AI.GetComponent<AI>();

        if (AIParentScript == null)
        {
            Debug.LogError("[UI] The referenced object has no AI component.", this);
            enabled = false;
        }
    }

    void Update()
    {
        UpdateState();
        UpdateCone();
        UpdateDifficulty();
    }

    void UpdateCone()
    {
        if (AIParentScript == null || textCone == null) return;

        int cone = showPerceivedCone
            ? AIParentScript.GetConeInt()        // what the AI is acting on
            : AIParentScript.GetRawConeInt();    // what the colliders say right now

        string label = InRange(ConeNameID, cone) ? ConeNameID[cone] : noDetectionText;

        // While perception is lagging, the AI is knowingly a step behind. Saying
        // so turns a confusing mismatch into a visible, explainable feature.
        if (showPerceivedCone && flagPerceptionLag &&
            AIParentScript.GetRawConeInt() != cone)
        {
            label += lagSuffix;
        }

        textCone.text = label;
    }

    void UpdateState()
    {
        if (AIParentScript == null) return;

        int stateIndex = AIParentScript.GetStateName();

        // Bounds-checked instead of a switch with one case per state, so adding a
        // state no longer silently freezes the readout on the previous value.
        if (textState != null && InRange(StateNameID, stateIndex))
        {
            textState.text = StateNameID[stateIndex];
        }

        if (textStateDesc != null && InRange(StateDescID, stateIndex))
        {
            textStateDesc.text = StateDescID[stateIndex];
        }
    }

    void UpdateDifficulty()
    {
        if (textDifficulty == null) return;

        GameOrganizer organizer = GameOrganizer.instance;
        if (organizer == null) return;

        textDifficulty.text = organizer.Difficulty.ToString().ToUpperInvariant();
    }

    static bool InRange(String[] array, int index)
    {
        return array != null && index >= 0 && index < array.Length;
    }
}
