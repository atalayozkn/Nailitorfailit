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

    private void Awake()
    {
        instance = this;
        ScreenFader.QuietPanelSwitch = IsQuietSwitch;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (ScreenFader.QuietPanelSwitch == (System.Func<bool, bool>)IsQuietSwitch) ScreenFader.QuietPanelSwitch = null;
    }

    // No screen fade while the player walks through the multiplayer menu (Multiplayer -> mode -> Create / Join) until a lobby
    // opens. At press: the lobby was not showing. At release: the multiplayer panels are still up and the lobby is not.
    private bool IsQuietSwitch(bool atRelease)
    {
        if (lobbyPanel == null && mpPanel != null) lobbyPanel = mpPanel.transform.Find(LobbyPanelName);
        bool lobbyShowing = lobbyPanel != null && lobbyPanel.gameObject.activeSelf;

        return atRelease ? mpPanel != null && mpPanel.activeSelf && !lobbyShowing : !lobbyShowing;
    }

    private void Start() => AutoOpenWhenConnected();

    private void Update() => AutoOpenWhenConnected();

    private void LateUpdate()
    {
        UpdateTitle();
        UpdateSocialIcons();
    }

    // The lobby has no social media icons at the bottom (they are back in every other panel).
    private bool socialHiddenByLobby;

    private void UpdateSocialIcons()
    {
        if (mpPanel == null || mpPanel.transform.parent == null) return;

        if (socialIcons == null) socialIcons = mpPanel.transform.parent.Find(SocialIconsName) as RectTransform;
        if (socialIcons == null) return;

        if (lobbyPanel == null) lobbyPanel = mpPanel.transform.Find(LobbyPanelName);
        bool hide = mpPanel.activeSelf && lobbyPanel != null && lobbyPanel.gameObject.activeSelf;

        if (hide)
        {
            if (socialIcons.gameObject.activeSelf) socialIcons.gameObject.SetActive(false);
            socialHiddenByLobby = true;
        }
        else if (socialHiddenByLobby)
        {
            socialIcons.gameObject.SetActive(true);
            socialHiddenByLobby = false;
        }
    }

    // The game's title (the board with the hard hat and the "Nail It or Fail It" lettering) also stands on the multiplayer
    // panels: Multiplayer, Create and Join. The lobby does not show it. The main menu's own two title objects are copied
    // once into the multiplayer panel, so the art, animation and position stay exactly the same.
    private const string LobbyPanelName = "Panel_Lobby";
    private static readonly string[] TitleParts = { "Title_Sign", "Title_Image" };

    private GameObject titleRoot;
    private Transform lobbyPanel;

    private void UpdateTitle()
    {
        if (mpPanel == null || mainPanel == null) return;

        if (!mpPanel.activeSelf)
        {
            if (titleRoot != null && titleRoot.activeSelf) titleRoot.SetActive(false);
            return;
        }

        if (titleRoot == null) titleRoot = BuildTitle();
        if (titleRoot == null) return;

        if (lobbyPanel == null) lobbyPanel = mpPanel.transform.Find(LobbyPanelName);
        bool inLobby = lobbyPanel != null && lobbyPanel.gameObject.activeSelf;

        if (titleRoot.activeSelf == inLobby) titleRoot.SetActive(!inLobby);
    }

    private GameObject BuildTitle()
    {
        var holder = new GameObject("MpTitle", typeof(RectTransform));
        holder.SetActive(false); // the copies must not wake up (and start animating) before their animation scripts are removed
        var rect = (RectTransform)holder.transform;
        rect.SetParent(mpPanel.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetSiblingIndex(Mathf.Min(1, mpPanel.transform.childCount - 1)); // above the background, below the panels

        int copied = 0;
        foreach (string part in TitleParts)
        {
            Transform source = mainPanel.transform.Find(part);
            if (source == null) continue;

            GameObject copy = Instantiate(source.gameObject, rect, true);
            copy.name = part + "_MP";
            copy.SetActive(true);

            // Multiplayer shows the board still: no bobbing and no entrance animation.
            DestroyImmediate(copy.GetComponent<UIFloat>());
            DestroyImmediate(copy.GetComponent<UIIntro>());
            copied++;
        }

        if (copied == 0)
        {
            Destroy(holder);
            return null;
        }

        holder.SetActive(true);
        return holder;
    }

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
