using UnityEngine;

public class DiscordLinkOpener : MonoBehaviour
{
    [SerializeField] private string inviteUrl = "https://discord.gg/YOUR_INVITE_CODE";

    // Hook this up to a UI Button's OnClick
    public void OpenDiscord()
    {
        Application.OpenURL(inviteUrl);
    }
}