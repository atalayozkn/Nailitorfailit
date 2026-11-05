using UnityEngine;

// Floats an icon above the dog's head showing what it is doing, so the player can read its intentions.
// The art lives in Resources/DogIcons, overwrite a PNG there to replace an icon. Singleplayer only, like the mail alert.
public class DogStateIcon : MonoBehaviour
{
    private const string IconFolder = "DogIcons/";
    private const float HeadClearance = 0.3f;
    private const float WorldSize = 0.55f;

    private DogStateMachine stateMachine;
    private SpriteRenderer iconRenderer;
    private DogStateMachine.DogState shownState;
    private bool hasShownState;
    private bool hasHeight;
    private float heightAboveDog;

    public static void Create(DogStateMachine dog)
    {
        GameObject icon = new GameObject("DogStateIcon");
        icon.layer = dog.gameObject.layer;
        icon.transform.SetParent(dog.transform, false);

        DogStateIcon stateIcon = icon.AddComponent<DogStateIcon>();
        stateIcon.stateMachine = dog;
        stateIcon.iconRenderer = icon.AddComponent<SpriteRenderer>();
        stateIcon.iconRenderer.sortingOrder = 100;
    }

    private void LateUpdate()
    {
        if (NetworkModeHelper.IsNetworked)
        {
            iconRenderer.enabled = false;
            return;
        }

        // Measured here because the tutorial keeps the dog inactive until its reveal.
        if (!hasHeight) MeasureHeight();

        if (!hasShownState || stateMachine.currentDogState != shownState)
        {
            hasShownState = true;
            shownState = stateMachine.currentDogState;
            ApplyIcon(shownState);
        }

        transform.position = stateMachine.transform.position + Vector3.up * heightAboveDog;
        if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;
    }

    private void MeasureHeight()
    {
        hasHeight = true;
        float top = stateMachine.transform.position.y;

        foreach (Renderer meshRenderer in stateMachine.GetComponentsInChildren<Renderer>())
        {
            if (meshRenderer is SkinnedMeshRenderer || meshRenderer is MeshRenderer) top = Mathf.Max(top, meshRenderer.bounds.max.y);
        }

        heightAboveDog = top - stateMachine.transform.position.y + HeadClearance;
    }

    private void ApplyIcon(DogStateMachine.DogState state)
    {
        string iconName = GetIconName(state);
        Sprite sprite = iconName != null ? Resources.Load<Sprite>(IconFolder + iconName) : null;

        iconRenderer.sprite = sprite;
        iconRenderer.enabled = sprite != null;
        if (sprite == null) return;

        // Sized in world units, whatever the art's resolution or the dog's scale.
        float scale = WorldSize / (sprite.bounds.size.x * stateMachine.transform.lossyScale.x);
        transform.localScale = Vector3.one * scale;
    }

    private static string GetIconName(DogStateMachine.DogState state)
    {
        switch (state)
        {
            case DogStateMachine.DogState.Patrol: return "Patrol";
            case DogStateMachine.DogState.Inspect: return "Inspect";
            case DogStateMachine.DogState.Chase:
            case DogStateMachine.DogState.Bark:
            case DogStateMachine.DogState.Aggresive: return "Angry";
            case DogStateMachine.DogState.Play: return "Play";
            case DogStateMachine.DogState.Eat: return "Eat";
            case DogStateMachine.DogState.Sleep: return "Sleep";
            case DogStateMachine.DogState.Affection: return "Affection";
            default: return null;
        }
    }
}
