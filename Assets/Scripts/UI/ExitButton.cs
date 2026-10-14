using UnityEngine;

public class ExitButton : MonoBehaviour
{
    public void ExitRequest()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}