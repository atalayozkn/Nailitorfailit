using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class EmoteTab : MonoBehaviour
{
    [SerializeField] private UnityEvent onSelected;
    [SerializeField] private Image background;

    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;
    public void OnHoverOn()
    {
        background.sprite = selectedSprite;
    }
    public void OnHoverOff()
    {
        background.sprite = normalSprite;
    }
    public void Execute()
    {
        onSelected?.Invoke();
    }
}