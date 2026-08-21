using System.Collections;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnDuration;
    public void RespawnPlayer(GameObject playerObj)
    {
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
