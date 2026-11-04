using System.Collections;
using Interactions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class MailBox : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractableType interactableType;
    [SerializeField] private float timeRewardAmount;
    [SerializeField] private UnityEvent onHoverOnEvent;
    [SerializeField] private UnityEvent onHoverOffEvent;
    [SerializeField] private UnityEvent onTriggerEvent;

    [Header("Mail Alert")]
    [SerializeField] private Color alertColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private float alertFontSize = 10f;
    [SerializeField] private float alertHeight = 0.6f;
    [SerializeField] private float minShakeInterval = 1f;
    [SerializeField] private float maxShakeInterval = 2f;
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeAngle = 8f;
    [SerializeField] private float shakeSpeed = 35f;

    private bool isMailPresent;
    private GameObject alertIcon;
    private Coroutine alertRoutine;
    private Quaternion restRotation;
    public InteractableType InteractableType => interactableType;

    private void LateUpdate()
    {
        if (alertIcon == null || !alertIcon.activeSelf || Camera.main == null) return;
        alertIcon.transform.rotation = Camera.main.transform.rotation;
    }

    #region Interactable
    public bool OnInteract()
    {
        if (!isMailPresent) return WarningManager.Reject("No mail yet");
        isMailPresent = false;
        StopAlert();
        GameTimeManager.Instance.IncreaseRoundTime(timeRewardAmount);
        return true;
    }
    public void OnHoverOn()
    {
        onHoverOnEvent?.Invoke();
    }
    public void OnHoverOff()
    {
        onHoverOffEvent?.Invoke();
    }
    #endregion

    public void OnTrigger()
    {
        isMailPresent = true;
        onTriggerEvent?.Invoke();
        if (!NetworkModeHelper.IsNetworked) StartAlert();
    }

    #region Mail Alert
    private void StartAlert()
    {
        if (alertRoutine != null) return;

        restRotation = transform.localRotation;
        if (alertIcon == null) CreateAlertIcon();
        alertIcon.SetActive(true);
        alertRoutine = StartCoroutine(AlertRoutine());
    }
    private void StopAlert()
    {
        if (alertRoutine == null) return;

        StopCoroutine(alertRoutine);
        alertRoutine = null;
        transform.localRotation = restRotation;
        alertIcon.SetActive(false);
    }
    private IEnumerator AlertRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minShakeInterval, maxShakeInterval));

            for (float elapsed = 0f; elapsed < shakeDuration; elapsed += Time.deltaTime)
            {
                float angle = Mathf.Sin(elapsed * shakeSpeed) * shakeAngle * (1f - elapsed / shakeDuration);
                transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            transform.localRotation = restRotation;
        }
    }
    private void CreateAlertIcon()
    {
        alertIcon = new GameObject("MailAlertIcon");
        alertIcon.transform.SetParent(transform);

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (Renderer meshRenderer in GetComponentsInChildren<Renderer>()) bounds.Encapsulate(meshRenderer.bounds);
        alertIcon.transform.position = new Vector3(bounds.center.x, bounds.max.y + alertHeight, bounds.center.z);

        TextMeshPro text = alertIcon.AddComponent<TextMeshPro>();
        text.text = "!";
        text.fontSize = alertFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = alertColor;
        text.rectTransform.sizeDelta = new Vector2(2f, 2f);
    }
    #endregion
}
