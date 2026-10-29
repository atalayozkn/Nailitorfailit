using Mirror;
using UnityEngine;

public class OwnerOnlyHud_MP : NetworkBehaviour
{
    [Tooltip("Sadece bu oyuncunun sahibi oldugu makinede acik kalacak HUD kokleri.")]
    [SerializeField] private GameObject[] hudRoots;

    public override void OnStartClient()
    {
        base.OnStartClient();
        Apply();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Apply();
    }

    private void Apply()
    {
        if (hudRoots == null) return;

        foreach (GameObject root in hudRoots)
        {
            if (root == null) continue;
            if (root.activeSelf == isOwned) continue;

            root.SetActive(isOwned);

            InteractionDebug.Log($"owner-only HUD '{root.name}': {(isOwned ? "acildi (yerel oyuncu)" : "kapatildi (baska oyuncu)")}");
        }
    }
}
