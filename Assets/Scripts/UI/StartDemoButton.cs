using UnityEngine;

public class StartDemoButton : MonoBehaviour
{
    public void StartDemo()
    {
        GameManager.Instance.PlayLevel(2);
    }
}
