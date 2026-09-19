using System.Collections;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnDuration;

    public Transform SpawnPoint => spawnPoint;
    public float SpawnDuration => spawnDuration;

    public void RespawnPlayer(GameObject playerObj)
    {
        PlayerScripts.PlayerNetwork_MP netPlayer = playerObj.GetComponentInChildren<PlayerScripts.PlayerNetwork_MP>(true);

        if (netPlayer != null)
        {
            netPlayer.RequestDeath();
            return;
        }

        playerObj.SetActive(false);
        StartCoroutine(SpawnRoutine(playerObj));
    }

    private IEnumerator SpawnRoutine(GameObject playerObj)
    {
        float elapsedTime = 0f;

        while (elapsedTime < spawnDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        playerObj.transform.position = spawnPoint.position;
        playerObj.SetActive(true);
    }
}
