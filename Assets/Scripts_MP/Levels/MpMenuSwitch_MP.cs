using Mirror;
using UnityEngine;

// The multiplayer menu lives in the main menu scene now (panel "MainMenu_MP" next to the singleplayer "MainMenu"):
// the Multiplayer button swaps the two panels instead of loading another scene. While a lobby/match connection is
// alive (host/client started, returning from a match, -host/-join test arguments) the multiplayer panel is shown
// by itself, so the lobby is never hidden behind the singleplayer buttons.
public class MpMenuSwitch_MP : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject mpPanel;

    // The social icons (shared by every menu panel) sit a little higher while the multiplayer panel is open.
    private const string SocialIconsName = "SocialMedia_Buttons (1)";
    private const string SocialPlateName = "Social_Backdrop";
    private const float MpSocialLift = 30f;

    private RectTransform socialIcons, socialPlate;
    private Vector2 socialIconsHome, socialPlateHome;
    private bool socialHomeSaved;

    private static MpMenuSwitch_MP instance;

    public bool IsMpOpen => mpPanel != null && mpPanel.activeSelf;

    private void Awake() => instance = this;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Start() => AutoOpenWhenConnected();

    private void Update() => AutoOpenWhenConnected();

    private void AutoOpenWhenConnected()
    {
        if (mpPanel == null || mpPanel.activeSelf) return;
        if (NetworkServer.active || NetworkClient.active) Open();
    }

    // Multiplayer button.
    public void Open()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (mpPanel != null) mpPanel.SetActive(true);
        MoveSocial(true);

        GameManager.Instance?.SwitchToMenuPhase();
        Debug.Log("[MP-MENU] multiplayer panel opened (same scene).");
    }

    // Back button of the multiplayer panel: leave the lobby/connection, show the singleplayer menu.
    public void Close()
    {
        MpMenuExit.ExitToMainMenu();

        if (mpPanel != null) mpPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        MoveSocial(false);
        Debug.Log("[MP-MENU] back to the main panel.");
    }

    private void MoveSocial(bool inMultiplayer)
    {
        if (mpPanel == null || mpPanel.transform.parent == null) return;
        Transform parent = mpPanel.transform.parent;
        if (socialIcons == null) socialIcons = parent.Find(SocialIconsName) as RectTransform;
        if (socialPlate == null) socialPlate = parent.Find(SocialPlateName) as RectTransform;
        if (socialIcons == null) return;

        if (!socialHomeSaved)
        {
            socialIconsHome = socialIcons.anchoredPosition;
            if (socialPlate != null) socialPlateHome = socialPlate.anchoredPosition;
            socialHomeSaved = true;
        }

        Vector2 lift = inMultiplayer ? new Vector2(0f, MpSocialLift) : Vector2.zero;
        socialIcons.anchoredPosition = socialIconsHome + lift;
        if (socialPlate != null) socialPlate.anchoredPosition = socialPlateHome + lift;
    }

    public static void CloseIfPresent()
    {
        if (instance != null) instance.Close();
    }
}
