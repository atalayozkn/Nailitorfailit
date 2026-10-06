using UnityEngine;

public class ReturnMenuHelper : MonoBehaviour
{
    public void RequestReturnToMenu()
    {
        GameManager.Instance.ReturnToGameMenu();
    }
}
