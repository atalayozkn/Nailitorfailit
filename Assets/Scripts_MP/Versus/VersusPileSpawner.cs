using Mirror;
using UnityEngine;

// Versus resource depot: makes one item on a timer and lays it on a visible pile next to the depot.
// Production pauses while every slot of the pile is full. Anyone can carry the items away,
// which is the point: the other player sees the pile and can steal from it.
// Only the server (or a normal offline session) produces; the items are the usual carriables.
public class VersusPileSpawner : MonoBehaviour
{
    [SerializeField] private GameObject itemPrefab;
    [Tooltip("One waiting item per slot, so the slot count is the pile cap.")]
    [SerializeField] private Transform[] slots;
    [SerializeField] private float interval = 8f;
    [SerializeField] private float firstDelay = 3f;
    [Tooltip("An item further than this from its slot counts as taken.")]
    [SerializeField] private float takenDistance = 1.2f;

    private GameObject[] items;
    private float timer;
    private bool producedOnce;

    private void Awake()
    {
        items = new GameObject[slots != null ? slots.Length : 0];
    }

    private void Update()
    {
        if (itemPrefab == null || items.Length == 0) return;
        if (NetworkModeHelper.IsNetworked && !NetworkServer.active) return;
        if (VersusMatch.Instance != null && VersusMatch.Instance.Winner != VersusMatch.NoWinner) return;

        int free = FirstFreeSlot();
        if (free < 0)
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        if (timer < (producedOnce ? interval : firstDelay)) return;

        timer = 0f;
        producedOnce = true;
        items[free] = MpPrefabSpawn.Spawn(itemPrefab, slots[free].position, slots[free].rotation);
    }

    private int FirstFreeSlot()
    {
        for (int i = 0; i < items.Length; i++)
        {
            GameObject item = items[i];

            if (item != null && item.transform.parent == null &&
                (item.transform.position - slots[i].position).sqrMagnitude <= takenDistance * takenDistance) continue;

            items[i] = null;
            return i;
        }

        return -1;
    }
}
