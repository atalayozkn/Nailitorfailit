using TMPro;
using UnityEngine;

// Shows the dog's mood on the dog UI, next to the favor and hunger bars.
public class DogMoodUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DogStateMachine stateMachine;

    [Header("Layout")]
    [SerializeField] private Vector2 labelPosition = new Vector2(-711f, -290f);
    [SerializeField] private Vector2 labelSize = new Vector2(420f, 50f);
    [SerializeField] private float fontSize = 28f;

    [Header("Colors")]
    [SerializeField] private Color loyalColor = new Color(0.35f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color restlessColor = new Color(1f, 0.75f, 0.15f, 1f);
    [SerializeField] private Color rebelliousColor = new Color(1f, 0.25f, 0.2f, 1f);

    private TextMeshProUGUI moodText;
    private DogStateMachine.DogMood currentMood;
    private bool hasMood;

    private void Awake()
    {
        GameObject labelObject = new GameObject("DogMoodLabel", typeof(RectTransform));
        labelObject.layer = gameObject.layer;

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = labelPosition;
        rect.sizeDelta = labelSize;

        moodText = labelObject.AddComponent<TextMeshProUGUI>();
        moodText.alignment = TextAlignmentOptions.Center;
        moodText.fontStyle = FontStyles.Bold;
        moodText.fontSize = fontSize;
        moodText.raycastTarget = false;
        moodText.outlineWidth = 0.2f;
        moodText.outlineColor = Color.black;
    }
    private void Update()
    {
        // The tutorial keeps the dog inactive until its reveal, its stats aren't running yet.
        moodText.enabled = stateMachine.isActiveAndEnabled;
        if (!moodText.enabled) return;

        DogStateMachine.DogMood mood = stateMachine.GetMood();
        if (hasMood && mood == currentMood) return;

        hasMood = true;
        currentMood = mood;

        switch (mood)
        {
            case DogStateMachine.DogMood.Loyal:
                moodText.text = "Loyal: bringing you gifts";
                moodText.color = loyalColor;
                break;
            case DogStateMachine.DogMood.Restless:
                moodText.text = "Restless: watch your items";
                moodText.color = restlessColor;
                break;
            case DogStateMachine.DogMood.Rebellious:
                moodText.text = "Rebellious: up to no good";
                moodText.color = rebelliousColor;
                break;
        }
    }
}
