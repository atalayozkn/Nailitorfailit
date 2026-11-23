using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// Pause menu in multiplayer: a light grey see-through layer behind it, and the in-game HUD (money, timer, dog, stamina,
// inventory, hints) is off while the menu is open and comes back when it closes.
public static class MpPauseStyle_MP
{
    private const string DimName = "MpDim";
    private static readonly Color DimColor = new Color(0.16f, 0.16f, 0.16f, 0.5f);

    private static readonly FieldInfo PanelField =
        typeof(PauseMenu).GetField("pauseMenu", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly List<GameObject> hidden = new List<GameObject>();

    public static void Update(PauseMenu menu)
    {
        if (menu == null || PanelField == null) return;

        var panel = PanelField.GetValue(menu) as GameObject;
        if (panel == null) return;

        hidden.RemoveAll(go => go == null); // a scene change while paused destroys them
        EnsureDim(panel.transform);

        bool paused = panel.activeInHierarchy;

        if (paused && hidden.Count == 0) HideHud();
        else if (!paused && hidden.Count > 0) RestoreHud();
    }

    private static void EnsureDim(Transform panel)
    {
        if (panel.Find(DimName) != null) return;

        var go = new GameObject(DimName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = panel.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(6000f, 4000f);
        rect.SetAsFirstSibling(); // behind the sign and the buttons

        var image = go.GetComponent<Image>();
        image.color = DimColor;
        image.raycastTarget = true; // nothing behind the menu can be clicked
    }

    private static void HideHud()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            foreach (Transform child in canvas.transform)
            {
                if (!child.gameObject.activeSelf) continue;
                if (System.Array.IndexOf(GameHUD_MP.InGameHudNames, child.name) < 0) continue;

                child.gameObject.SetActive(false);
                hidden.Add(child.gameObject);
            }
        }
    }

    private static void RestoreHud()
    {
        foreach (GameObject go in hidden)
            if (go != null) go.SetActive(true);

        hidden.Clear();
    }
}
