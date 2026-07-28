using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CustomButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private UnityEvent onPointerEnterEvent;
    [SerializeField] private UnityEvent onPointerExitEvent;
    [SerializeField] private UnityEvent onPointerClickEvent;

    public void OnPointerEnter(PointerEventData eventData)
    {
        onPointerEnterEvent?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onPointerExitEvent?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onPointerClickEvent?.Invoke();
    }
}