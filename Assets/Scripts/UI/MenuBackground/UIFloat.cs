using UnityEngine;

/// <summary>
/// Idle sine motion for a UI element: gentle bob and sway (e.g. a hanging sign).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIFloat : MonoBehaviour
{
    [SerializeField] private Vector2 moveAmplitude = new Vector2(0f, 8f);
    [SerializeField] private float moveFrequency = 0.4f;
    [Tooltip("Degrees of rotation sway.")]
    [SerializeField] private float rotationAmplitude = 1.2f;
    [SerializeField] private float rotationFrequency = 0.3f;
    [SerializeField] private bool randomPhase = true;

    private RectTransform rect;
    private Vector2 basePosition;
    private float baseRotation;
    private float phase;

    private void Awake()
    {
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
        baseRotation = rect.localEulerAngles.z;
        phase = randomPhase ? Random.value * Mathf.PI * 2f : 0f;
    }

    // With zero move amplitude only rotation is driven, so another script (e.g. UIIntro)
    // can own the position on the same object.
    private bool DrivesPosition => moveAmplitude != Vector2.zero;

    private void OnDisable()
    {
        if (rect == null) return;
        if (DrivesPosition) rect.anchoredPosition = basePosition;
        rect.localRotation = Quaternion.Euler(0f, 0f, baseRotation);
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        float r = Mathf.Sin(t * rotationFrequency * Mathf.PI * 2f + phase * 0.7f);
        rect.localRotation = Quaternion.Euler(0f, 0f, baseRotation + rotationAmplitude * r);

        if (DrivesPosition)
        {
            float m = Mathf.Sin(t * moveFrequency * Mathf.PI * 2f + phase);
            rect.anchoredPosition = basePosition + moveAmplitude * m;
        }
    }
}
