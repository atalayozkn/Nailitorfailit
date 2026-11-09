using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// One segment of the emote wheel. A tab with no emote hooked to onSelected is locked: it shows the
// locked plate and can't be picked, so empty slots are ready for future emotes.
public class EmoteTab : MonoBehaviour
{
    [SerializeField] private UnityEvent onSelected;
    [SerializeField] private Image background;

    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;
    [SerializeField] private Sprite lockedSprite;
    [Tooltip("Point the wheel measures this tab's direction from (its label). Defaults to this transform.")]
    [SerializeField] private RectTransform anchor;
    [SerializeField] private TMP_Text label;

    public bool IsLocked => onSelected == null || onSelected.GetPersistentEventCount() == 0;
    public RectTransform Anchor => anchor != null ? anchor : (RectTransform)transform;
    public string Label => label != null ? label.text : name;

    private void OnEnable() => OnHoverOff();

    public void OnHoverOn()
    {
        if (IsLocked) return;
        background.sprite = selectedSprite;
    }
    public void OnHoverOff()
    {
        background.sprite = IsLocked && lockedSprite != null ? lockedSprite : normalSprite;
    }
    public void Execute()
    {
        if (IsLocked) return;
        onSelected?.Invoke();
    }
}
