using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnPointRespawn : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float respawnDelay = 3.5f;
    [SerializeField] private float checkInterval = 0.1f;

    [SerializeField] private List<HazardBounceRespawn> players = new List<HazardBounceRespawn>();

    private void Start()
    {
        RefreshPlayers();
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(checkInterval);

        while (true)
        {
            RefreshPlayers();
            CheckRespawns();

            yield return wait;
        }
    }

    private void RefreshPlayers()
    {
        players.Clear();

        GameObject[] found = GameObject.FindGameObjectsWithTag(playerTag);

        for (int i = 0; i < found.Length; i++)
        {
            HazardBounceRespawn h = found[i].GetComponent<HazardBounceRespawn>();

            if (h != null)
                players.Add(h);
        }
    }

    private void CheckRespawns()
    {
        float now = Time.time;

        for (int i = 0; i < players.Count; i++)
        {
            HazardBounceRespawn p = players[i];
            if (p == null) continue;

            if (!p.IsInvisible) continue;
            if ((now - p.InvisibleStartTime) < respawnDelay) continue;

            RespawnPlayer(p);
        }
    }

    private void RespawnPlayer(HazardBounceRespawn player)
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.transform.position = transform.position;
        player.transform.rotation = transform.rotation;

        player.RespawnFinished();
    }
}