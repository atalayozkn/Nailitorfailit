using UnityEngine;

public class PlayerTrapRespawn : MonoBehaviour
{
    public static PlayerTrapRespawn Instance { get; private set; }

    [Header("Player")]
    [SerializeField] private GameObject playerObject;

    [Header("Respawn Point")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float defaultRespawnDelay = 3f;

    private bool isRespawning;
    private bool isWaitingBeforeHide;
    private bool isWaitingRespawn;

    private float counter;
    private float beforeHideDelay;
    private float respawnDelay;

    public bool IsRespawning => isRespawning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Sahnede birden fazla PlayerTrapRespawn var!");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (respawnPoint == null)
            respawnPoint = transform;
    }

    private void Update()
    {
        if (!isRespawning)
            return;

        counter += Time.deltaTime;

        if (isWaitingBeforeHide)
        {
            if (counter < beforeHideDelay)
                return;

            counter = 0f;

            isWaitingBeforeHide = false;
            isWaitingRespawn = true;

            HidePlayer();

            return;
        }

        if (isWaitingRespawn)
        {
            if (counter < respawnDelay)
                return;

            FinishRespawn();
        }
    }

    public void RespawnPlayer(float delay)
    {
        StartRespawn(0f, delay);
    }

    public void RespawnPlayerAfterDelay(float delayBeforeHide, float delayAfterHide)
    {
        StartRespawn(delayBeforeHide, delayAfterHide);
    }

    private void StartRespawn(float delayBeforeHide, float delayAfterHide)
    {
        if (isRespawning)
            return;

        if (playerObject == null)
        {
            Debug.LogWarning("Player Object atanmadý!");
            return;
        }

        isRespawning = true;

        beforeHideDelay = Mathf.Max(0f, delayBeforeHide);
        respawnDelay = delayAfterHide > 0f ? delayAfterHide : defaultRespawnDelay;

        counter = 0f;

        if (beforeHideDelay <= 0f)
        {
            HidePlayer();

            isWaitingBeforeHide = false;
            isWaitingRespawn = true;
        }
        else
        {
            isWaitingBeforeHide = true;
            isWaitingRespawn = false;
        }
    }

    private void HidePlayer()
    {
        if (playerObject == null)
            return;

        playerObject.SetActive(false);
    }

    private void FinishRespawn()
    {
        if (playerObject == null)
        {
            ResetRespawnData();
            return;
        }

        if (respawnPoint != null)
        {
            playerObject.transform.position = respawnPoint.position;
            playerObject.transform.rotation = respawnPoint.rotation;
        }

        playerObject.SetActive(true);

        ResetRespawnData();
    }

    private void ResetRespawnData()
    {
        isRespawning = false;
        isWaitingBeforeHide = false;
        isWaitingRespawn = false;

        counter = 0f;
        beforeHideDelay = 0f;
        respawnDelay = 0f;
    }
}