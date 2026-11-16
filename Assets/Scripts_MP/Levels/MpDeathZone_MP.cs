using Mirror;
using PlayerScripts;
using UnityEngine;

// A death zone under the map of every level (server side): a player who falls out of the world dies and respawns at
// the level's spawn point like after any other death (items dropped, 5 s). Loose items that fall out are removed so
// they do not keep falling (and keep being synced) forever. No scene objects needed, so it covers every level.
public class MpDeathZone_MP : MonoBehaviour
{
    // Ground is around y = 0 in every level; basements and interiors stay well above these.
    private const float PlayerKillHeight = -15f;
    private const float ItemKillHeight = -30f;
    private const float CheckInterval = 0.25f;

    private float nextCheckAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpDeathZone_MP>(FindObjectsInactive.Include) != null) return;

        var host = new GameObject("MpDeathZone_MP");
        DontDestroyOnLoad(host);
        host.AddComponent<MpDeathZone_MP>();
    }

    private void Update()
    {
        if (!NetworkServer.active) return;
        if (Time.unscaledTime < nextCheckAt) return;
        nextCheckAt = Time.unscaledTime + CheckInterval;

        foreach (PlayerNetwork_MP player in FindObjectsByType<PlayerNetwork_MP>(FindObjectsSortMode.None))
        {
            if (player == null || player.isDead) continue;
            if (player.transform.position.y >= PlayerKillHeight) continue;

            InteractionDebug.Log($"[MP-DEATHZONE] '{player.name}' fell out of the map (y={player.transform.position.y:0}).");
            player.ServerKill(DeathReason.None);
        }

        foreach (CarriableObject_MP item in FindObjectsByType<CarriableObject_MP>(FindObjectsSortMode.None))
        {
            if (item == null || item.Carrier != null || item.transform.parent != null) continue;
            if (item.transform.position.y >= ItemKillHeight) continue;

            InteractionDebug.Log($"[MP-DEATHZONE] '{item.name}' fell out of the map and was removed.");
            NetworkServer.Destroy(item.gameObject);
        }
    }
}
