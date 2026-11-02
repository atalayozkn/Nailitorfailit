using UnityEngine;
using UnityEngine.EventSystems;

// Grows a UI element slightly while it is hovered or selected (mouse or gamepad).
// Uses unscaled time so it also works while the game is paused.
public class ButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float speed = 12f;

    private Vector3 baseScale;
    private bool hovered;

    private void Awake()
    {
        baseScale = transform.localScale;
    }
    private void OnDisable()
    {
        hovered = false;
        transform.localScale = baseScale;
    }
    private void Update()
    {
        Vector3 target = hovered ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnSelect(BaseEventData eventData) => hovered = true;
    public void OnDeselect(BaseEventData eventData) => hovered = false;
}
