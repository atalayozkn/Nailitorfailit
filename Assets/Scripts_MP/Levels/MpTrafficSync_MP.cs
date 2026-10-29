using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MpTrafficSync_MP : MonoBehaviour
{
    [Tooltip("Tarama araligi (saniye).")]
    [SerializeField] private float scanInterval = 0.25f;

    [Tooltip("Log bas.")]
    [SerializeField] private bool logActions = true;

    private float nextScan;
    private int adoptedTotal;
    private int destroyedLocalTotal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        if (FindAnyObjectByType<MpTrafficSync_MP>(FindObjectsInactive.Include) != null) return;
        new GameObject("MpTrafficSync_MP").AddComponent<MpTrafficSync_MP>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + scanInterval;

        if (NetworkServer.active)
        {
            AdoptHostTraffic();
            return;
        }

        if (NetworkClient.active) FreezeClientTraffic();
    }

    private void AdoptHostTraffic()
    {
        foreach (CarController_Prototype controller in Object.FindObjectsByType<CarController_Prototype>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (controller == null) continue;
            if (!controller.gameObject.activeInHierarchy) continue;

            NetworkIdentity identity = controller.GetComponent<NetworkIdentity>();
            if (identity == null) continue;
            if (identity.netId != 0) continue;

            if (!NetworkServer.active) continue;

            NetworkServer.Spawn(controller.gameObject);
            adoptedTotal++;

            if (logActions)
                InteractionDebug.Log($"[MP-CAR] host trafigi aga alindi: '{controller.name}' netId={identity.netId} (toplam {adoptedTotal})");
        }
    }

    private void FreezeClientTraffic()
    {
        foreach (CarController_Prototype controller in Object.FindObjectsByType<CarController_Prototype>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (controller == null) continue;

            NetworkIdentity identity = controller.GetComponent<NetworkIdentity>();
            bool networked = identity != null && identity.netId != 0;

            if (controller.enabled) controller.enabled = false;

            Rigidbody body = controller.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic) body.isKinematic = true;

            if (networked) continue;

            if (logActions)
                InteractionDebug.Log($"[MP-CAR] client: yerel (agsiz) trafik silindi '{controller.name}' (toplam {destroyedLocalTotal + 1})");

            destroyedLocalTotal++;
            Destroy(controller.gameObject);
        }
    }
}
