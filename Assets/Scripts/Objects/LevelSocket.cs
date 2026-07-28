using UnityEngine;

public class LevelSocket : MonoBehaviour
{
    [SerializeField] private int levelIndex;
    [SerializeField] private GameObject canvasObject; //Information on the Level (Cost - Gain - Explanations etc.)

    private void OnEnable()
    {
        canvasObject.SetActive(false);
    }
    public void OnInteract()
    {
        SetActivity(false);
        GameSceneManager.Instance.LoadLevel(levelIndex);
    }
    public void SetActivity(bool condition)
    {
        canvasObject.SetActive(condition);
    }
    
}
