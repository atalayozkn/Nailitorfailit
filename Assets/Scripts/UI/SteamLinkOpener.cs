using UnityEngine;

public class SteamLinkOpener : MonoBehaviour
{
    [SerializeField] private string storeUrl = "https://store.steampowered.com/app/4577740/Nail_It_Or_Fail_It/";

    [Tooltip("Open inside the Steam client if installed, otherwise the default browser.")]
    [SerializeField] private bool openInSteamClient = false;

    // Hook this up to your Wishlist button's OnClick
    public void OpenWishlist()
    {
        OpenStorePage();
    }

    public void OpenStorePage()
    {
#if UNITY_WEBGL
        Application.OpenURL(storeUrl);
#else
        if (openInSteamClient)
        {
            // Opens the same page inside the Steam client
            Application.OpenURL("steam://openurl/" + storeUrl);
        }
        else
        {
            Application.OpenURL(storeUrl);
        }
#endif
    }
}