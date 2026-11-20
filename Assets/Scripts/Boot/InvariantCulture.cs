using System.Globalization;
using UnityEngine;

// TextMeshPro's UpperCase style (and string.ToUpper / ToLower) follow the OS culture. On a Turkish system "i" becomes
// "İ" (dotted capital I), so "SINGLE PLAYER" is drawn as "SİNGLE PLAYER". The game's UI is English, so the main thread
// is pinned to the invariant culture, both in builds and in the Editor.
public static class InvariantCulture
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

#if UNITY_EDITOR
    // Runs after every domain reload, so edit-mode previews (Game/Scene view, screenshots) match the build.
    [UnityEditor.InitializeOnLoadMethod]
    private static void ApplyInEditor() => Apply();
#endif
}
