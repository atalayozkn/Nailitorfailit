using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// On the imported MP canvas: opens/closes the multiplayer menu IN PLACE, without
/// needing any prefab -> scene reference (a prefab cannot point at the SP menu).
///
/// Open()  - shows this canvas, hides the other menu canvas of the scene
/// Close() - hides this canvas, shows the other menu canvas again
///
/// The SP menu's Multiplayer button only has to call Open() (MpOpenButton does), and
/// the prefab's own Back button is wired to Close() by the setup tool.
/// </summary>
public class MpInPlaceToggle : MonoBehaviour
{
    [Tooltip("Optional: name of the menu canvas to hide. Empty = the first other overlay canvas.")]
    [SerializeField] private string otherMenuCanvasName = "Canvas_Overlay_MainMenu";

    [SerializeField] private bool logActions = true;

    private Canvas ownCanvas;

    private void Awake()
    {
        ownCanvas = GetComponent<Canvas>();
        if (ownCanvas == null) ownCanvas = GetComponentInChildren<Canvas>(true);
    }

    /// <summary>
    /// Just ACTIVATING this object is enough: the SP menu canvas is hidden
    /// automatically, so a button can simply call SetActive(true) on the prefab
    /// (no extra onClick entries or scripts needed).
    /// </summary>
    private void OnEnable()
    {
        GameObject other = FindOtherMenuCanvas();
        if (other != null && other.activeSelf)
        {
            other.SetActive(false);
            if (logActions) Debug.Log($"[MP] MP prefab activated - hid the SP menu canvas '{other.name}'.");
        }
    }

    /// <summary>Shows the multiplayer menu and hides the singleplayer one.</summary>
    public void Open()
    {
        SetOwnActive(true);

        GameObject other = FindOtherMenuCanvas();
        if (other != null) other.SetActive(false);

        if (logActions) Debug.Log($"[MP] MP menu opened in place (hid '{(other != null ? other.name : "nothing")}').");
    }

    /// <summary>Hides the multiplayer menu and restores the singleplayer one.</summary>
    public void Close()
    {
        GameObject other = FindOtherMenuCanvas();
        if (other != null) other.SetActive(true);

        SetOwnActive(false);

        if (logActions) Debug.Log($"[MP] MP menu closed (restored '{(other != null ? other.name : "nothing")}').");
    }

    private void SetOwnActive(bool active)
    {
        // the canvas usually sits on a child (or is this object)
        if (ownCanvas != null && ownCanvas.gameObject != gameObject) ownCanvas.gameObject.SetActive(active);
        else gameObject.SetActive(active);
    }

    /// <summary>
    /// The SP menu canvas is the OTHER overlay canvas with the menu name (this prefab
    /// carries a canvas with the same name, which is why we skip our own).
    /// </summary>
    private GameObject FindOtherMenuCanvas()
    {
        Canvas own = ownCanvas != null ? ownCanvas : GetComponentInChildren<Canvas>(true);

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == own) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (canvas.transform.IsChildOf(transform)) continue;      // part of this prefab
            if (!string.IsNullOrEmpty(otherMenuCanvasName) &&
                !canvas.gameObject.name.Contains(otherMenuCanvasName)) continue;

            return canvas.gameObject;
        }

        // fallback: any other overlay canvas with a GraphicRaycaster (i.e. a real menu)
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == own) continue;
            if (canvas.transform.IsChildOf(transform)) continue;
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) continue;

            return canvas.gameObject;
        }

        return null;
    }
}
