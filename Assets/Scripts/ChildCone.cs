using UnityEngine;

// NOTE: this file used to have "using UnityEditor;" - see the note in
// VisionCone.cs. It breaks standalone player builds.

/// <summary>
/// One vision cone. Polls its own collider against the shared target and reports
/// the result up to the VisionCone on the parent.
///
/// Ten of these run every frame, so keep the per-frame work light. GetComponent
/// calls belong in Start.
/// </summary>
public class ChildCone : MonoBehaviour
{
    /// <summary>Index into VisionCone.coneState. Must be unique and contiguous.</summary>
    [SerializeField] int coneID;

    /// <summary>Keep this cone visible even when it is not detecting (V key toggles it).</summary>
    [SerializeField] bool toggleOn;

    VisionCone ParentScript;
    SpriteRenderer rend;
    PolygonCollider2D col;

    Color detectColor;
    Color normColor;

    void Start()
    {
        ParentScript = this.transform.parent.GetComponent<VisionCone>();
        rend = this.GetComponent<SpriteRenderer>();
        col = this.GetComponent<PolygonCollider2D>();

        detectColor = new Color(1f, 0f, 0.4f, 0.4f);
        normColor = new Color(0.3f, 1f, 1f, 0.3f);

        if (ParentScript == null)
        {
            Debug.LogError($"[ChildCone {coneID}] No VisionCone on the parent object.", this);
            enabled = false;
        }
    }

    void Update()
    {
        ReadToggleKey();
        ChildConeCheck();
    }

    void ChildConeCheck()
    {
        if (col == null || rend == null) return;

        BoxCollider2D target = ParentScript.GetTarget();
        bool detecting = target != null && col.IsTouching(target);

        if (detecting)
        {
            ParentScript.SetConeStateT(coneID);
        }
        else
        {
            ParentScript.SetConeStateF(coneID);
        }

        // Visibility and tint derive from one source of truth now. Previously
        // this method and the V-key handler both wrote rend.enabled from
        // separate bools that started out of sync, so the first press of V could
        // appear to do nothing.
        rend.color = detecting ? detectColor : normColor;
        rend.enabled = detecting || toggleOn;
    }

    void ReadToggleKey()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            toggleOn = !toggleOn;
        }
    }
}
