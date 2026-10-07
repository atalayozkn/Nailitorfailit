using UnityEngine;

public class InstagramLinkOpener : MonoBehaviour
{
    [Header("Your Instagram username (without the @)")]
    [SerializeField] private string username = "YOUR_USERNAME";

    private string WebUrl => $"https://www.instagram.com/{username}/";
    private string AppUrl => $"instagram://user?username={username}";

    // Hook this up to a UI Button's OnClick
    public void OpenInstagram()
    {
#if UNITY_ANDROID || UNITY_IOS
        OpenOnMobile();
#else
        Application.OpenURL(WebUrl);
#endif
    }

#if UNITY_ANDROID || UNITY_IOS
    private void OpenOnMobile()
    {
        float startTime = Time.realtimeSinceStartup;

        // Try the Instagram app first
        Application.OpenURL(AppUrl);

        // If the app isn't installed, the call does nothing and we're still in the game,
        // so fall back to the web page after a short delay
        StartCoroutine(FallbackToWeb(startTime));
    }

    private System.Collections.IEnumerator FallbackToWeb(float startTime)
    {
        yield return new WaitForSecondsRealtime(0.5f);

        // If the game was backgrounded, Instagram opened. If the game is still
        // in focus, the app likely isn't installed.
        if (Application.isFocused)
        {
            Application.OpenURL(WebUrl);
        }
    }
#endif
}