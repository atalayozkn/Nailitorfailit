using System;
using UnityEngine;

// Content of an info pop-up (level intro, dog explanation ...). One asset per pop-up.
[CreateAssetMenu(
    fileName = "InfoPopup",
    menuName = "UI/Info Popup"
)]
public class InfoPopupData : ScriptableObject
{
    [Serializable]
    public class Page
    {
        public string title;
        [TextArea(3, 10)] public string body;
        [Tooltip("Optional picture shown above the text.")]
        public Sprite image;
    }

    [Tooltip("Saved once read, so the pop-up opens automatically only the first time.")]
    [SerializeField] private string popupId;
    [SerializeField] private Page[] pages;

    public string PopupId => popupId;
    public Page[] Pages => pages;
}
