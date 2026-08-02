using System.Collections;
using Interactions;
using PlayerScripts;
using UnityEngine;
using UnityEngine.Events;

public class Shop : MonoBehaviour, IInteractable
{
    [Header("Interactable")]
    [SerializeField] private InteractableType interactableType = InteractableType.Shop;
    public InteractableType InteractableType => interactableType;

    [Header("Shop Menu")]
    [SerializeField] private ShopMenu shopMenu;

    [Header("Player References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerInteractionHandler playerInteractionHandler;
    [SerializeField] private PlayerStateMachine playerStateMachine;
    [SerializeField] private Rigidbody playerRigidbody;

    [Header("Camera Target Movement")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform playerCameraPoint;
    [SerializeField] private Transform shopCameraPoint;
    [SerializeField] private float cameraMoveDuration = 0.6f;

    [Header("Events")]
    [SerializeField] private UnityEvent onShopOpenedEvent;
    [SerializeField] private UnityEvent onShopClosedEvent;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;

    private bool isShopOpen;
    private Coroutine cameraMoveRoutine;

    private void Awake()
    {
        ResolveReferences();

        MoveCameraTargetInstant(playerCameraPoint);
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (shopMenu != null)
            shopMenu.OnShopClosed += HandleShopClosed;
    }

    private void OnDisable()
    {
        if (shopMenu != null)
            shopMenu.OnShopClosed -= HandleShopClosed;

        StopCameraMoveRoutine();
    }

    public void OnInteract()
    {
        if (isShopOpen)
            return;

        OpenShop();
    }

    public void OnHoverOn()
    {
        if (isShopOpen)
            return;

        onHoverOnEvent?.Invoke();
    }

    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }

    private void OpenShop()
    {
        ResolveReferences();

        if (shopMenu == null)
        {
            Debug.LogWarning("ShopMenu bulunamadý!");
            return;
        }

        if (cameraTarget == null)
        {
            Debug.LogWarning("CameraTarget atanmadý!");
            return;
        }

        if (shopCameraPoint == null)
        {
            Debug.LogWarning("ShopCameraPoint atanmadý!");
            return;
        }

        isShopOpen = true;

        LockPlayer();

        MoveCameraTargetSmooth(shopCameraPoint);

        shopMenu.OpenMenu();

        onShopOpenedEvent?.Invoke();
    }

    private void HandleShopClosed()
    {
        if (!isShopOpen)
            return;

        isShopOpen = false;

        MoveCameraTargetSmooth(playerCameraPoint);

        UnlockPlayer();

        onShopClosedEvent?.Invoke();
    }

    private void LockPlayer()
    {
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        if (playerStateMachine != null)
        {
            playerStateMachine.movementHandler.SetActivity(false);
            playerStateMachine.ChangeToIdleState();
        }

        if (playerMovement != null) playerMovement.enabled = false;

        if (playerInteractionHandler != null) playerInteractionHandler.enabled = false;
    }

    private void UnlockPlayer()
    {
        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerInteractionHandler != null)
            playerInteractionHandler.enabled = true;
    }

    private void MoveCameraTargetInstant(Transform targetPoint)
    {
        if (cameraTarget == null)
            return;

        if (targetPoint == null)
            return;

        cameraTarget.position = targetPoint.position;
        cameraTarget.rotation = targetPoint.rotation;
    }

    private void MoveCameraTargetSmooth(Transform targetPoint)
    {
        if (cameraTarget == null)
            return;

        if (targetPoint == null)
            return;

        StopCameraMoveRoutine();

        cameraMoveRoutine = StartCoroutine(CameraMoveRoutine(targetPoint));
    }

    private IEnumerator CameraMoveRoutine(Transform targetPoint)
    {
        Vector3 startPosition = cameraTarget.position;
        Quaternion startRotation = cameraTarget.rotation;

        Vector3 targetPosition = targetPoint.position;
        Quaternion targetRotation = targetPoint.rotation;

        float timer = 0f;

        while (timer < cameraMoveDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = timer / cameraMoveDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            cameraTarget.position = Vector3.Lerp(startPosition, targetPosition, t);
            cameraTarget.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            yield return null;
        }

        cameraTarget.position = targetPosition;
        cameraTarget.rotation = targetRotation;

        cameraMoveRoutine = null;
    }

    private void StopCameraMoveRoutine()
    {
        if (cameraMoveRoutine == null)
            return;

        StopCoroutine(cameraMoveRoutine);
        cameraMoveRoutine = null;
    }

    private void ResolveReferences()
    {
        if (shopMenu == null)
        {
            if (ShopMenu.Instance != null)
                shopMenu = ShopMenu.Instance;
            else
                shopMenu = FindFirstObjectByType<ShopMenu>();
        }

        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();

        if (playerInteractionHandler == null)
            playerInteractionHandler = FindFirstObjectByType<PlayerInteractionHandler>();

        if (playerStateMachine == null)
            playerStateMachine = FindFirstObjectByType<PlayerStateMachine>();

        if (playerRigidbody == null && playerMovement != null)
            playerRigidbody = playerMovement.GetComponent<Rigidbody>();
    }
}