using System.Collections.Generic;
using UnityEngine;

public class SpawnPointRespawn_SP : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float respawnDelay = 3.5f;

    [SerializeField] private List<HazardBounceRespawn> players = new List<HazardBounceRespawn>();

    void FixedUpdate()
    {
        players.Clear();

        GameObject[] found = GameObject.FindGameObjectsWithTag(playerTag);
        for (int i = 0; i < found.Length; i++)
        {
            var h = found[i].GetComponent<HazardBounceRespawn>();
            if (h != null) players.Add(h);
        }

        float now = Time.time;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            if (!p.IsInvisible) continue; // ✅ visual kapanmadan saymaz
            if ((now - p.InvisibleStartTime) < respawnDelay) continue;

            Rigidbody rb = p.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            p.transform.position = transform.position;
            p.transform.rotation = transform.rotation;

            p.RespawnFinished();
        }
    }
}
