using Mirror;
using UnityEngine;

// Multiplayer scene loads (host starts the match, client follows, back to the lobby) use the same paw loading screen
// as the singleplayer scene changes (LoadingScreen). This only decides WHEN: while Mirror is loading a scene.
public class MpLoadingScreen_MP : MonoBehaviour
{
    private static MpLoadingScreen_MP instance;

    private bool visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (instance != null) return;

        var host = new GameObject("MpLoadingScreen_MP");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<MpLoadingScreen_MP>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Kept for the callers; the reason text is not shown any more.
    public static void Show(string reason)
    {
        if (instance == null) return;
        instance.BeginVisible();
    }

    private void BeginVisible()
    {
        if (visible) return;
        visible = true;
        LoadingScreen.Show();
    }

    private void Update()
    {
        bool loading = IsLoading();

        if (loading) BeginVisible();

        // LoadingScreen keeps itself up for its minimum time so it never flashes.
        if (visible && !loading && LoadingScreen.MinTimeElapsed)
        {
            visible = false;
            LoadingScreen.Hide();
        }
    }

    private static bool IsLoading()
    {
        if (NetworkManager.loadingSceneAsync != null && !NetworkManager.loadingSceneAsync.isDone) return true;
        if (NetworkServer.isLoadingScene) return true;
        if (NetworkClient.isLoadingScene) return true;

        return false;
    }
}
