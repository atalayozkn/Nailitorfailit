using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Plays hover and click sounds for any button or menu control it sits on. Added by SfxBootstrap.
public class UiSfxHook : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    private Selectable selectable;

    private void Awake() => selectable = GetComponent<Selectable>();

    private bool Interactable => selectable == null || selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData eventData) { if (Interactable) SfxBootstrap.PlayHover(); }

    public void OnPointerClick(PointerEventData eventData) { if (Interactable) SfxBootstrap.PlayClick(); }
}
