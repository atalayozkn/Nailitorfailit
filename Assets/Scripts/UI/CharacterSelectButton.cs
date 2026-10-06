using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
public class CharacterSelectButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private int characterIndex = 0;
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
        GameManager.Instance.StartGame(characterIndex);
        onPointerClickEvent?.Invoke();
    }
}
