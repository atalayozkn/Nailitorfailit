using System.Collections;
using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnDuration;
    public void RespawnPlayer(PlayerStateMachine playerStateMachine)
    {
        StartCoroutine(SpawnRoutine(playerStateMachine));
    }
    private IEnumerator SpawnRoutine(PlayerStateMachine playerStateMachine)
    {
        float elapsedTime = 0f;

        while (elapsedTime < spawnDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        playerStateMachine.transform.position = spawnPoint.position;
        playerStateMachine.ForceUpdateIdle();
    }
}
