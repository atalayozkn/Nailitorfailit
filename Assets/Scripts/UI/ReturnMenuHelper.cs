using UnityEngine;

public class ReturnMenuHelper : MonoBehaviour
{
    public void RequestReturnToMenu()
    {
        GameSceneManager.Instance.LoadMenu();
    }
}
