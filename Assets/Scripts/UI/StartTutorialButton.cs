using UnityEngine;

public class StartTutorialButton : MonoBehaviour
{
    public void StartDemo()
    {
        GameManager.Instance.PlayLevel(1);
    }
}
