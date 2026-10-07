using UnityEngine;

public class StartDemoButton : MonoBehaviour
{
    public void StartDemo()
    {
        GameManager.Instance.ChangeToLevelPhase(2);
    }
}
