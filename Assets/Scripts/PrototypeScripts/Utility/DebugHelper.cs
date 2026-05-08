using UnityEngine;

public class DebugHelper : MonoBehaviour
{
    [SerializeField] private string ErrorMessage;
    public void DebugString()
    {
        Debug.Log(ErrorMessage);
    }
}
