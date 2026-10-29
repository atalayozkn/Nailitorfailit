using UnityEngine;
using UnityEngine.SceneManagement;

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

    private void OnEnable()
    {
        GameObject other = FindOtherMenuCanvas();
        if (other != null && other.activeSelf)
        {
            other.SetActive(false);
            if (logActions) Debug.Log($"[MP] MP prefab activated - hid the SP menu canvas '{other.name}'.");
        }
    }

    public void Open()
    {
        SetOwnActive(true);

        GameObject other = FindOtherMenuCanvas();
        if (other != null) other.SetActive(false);

        if (logActions) Debug.Log($"[MP] MP menu opened in place (hid '{(other != null ? other.name : "nothing")}').");
    }

    public void Close()
    {
        GameObject other = FindOtherMenuCanvas();
        if (other != null) other.SetActive(true);

        SetOwnActive(false);

        if (logActions) Debug.Log($"[MP] MP menu closed (restored '{(other != null ? other.name : "nothing")}').");
    }

    private void SetOwnActive(bool active)
    {

        if (ownCanvas != null && ownCanvas.gameObject != gameObject) ownCanvas.gameObject.SetActive(active);
        else gameObject.SetActive(active);
    }

    private GameObject FindOtherMenuCanvas()
    {
        Canvas own = ownCanvas != null ? ownCanvas : GetComponentInChildren<Canvas>(true);

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == own) continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (canvas.transform.IsChildOf(transform)) continue;
            if (!string.IsNullOrEmpty(otherMenuCanvasName) &&
                !canvas.gameObject.name.Contains(otherMenuCanvasName)) continue;

            return canvas.gameObject;
        }

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
