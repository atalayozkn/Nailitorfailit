using ItemScript;
using UnityEngine;

public class ScoreHelper : MonoBehaviour
{
    [SerializeField] private Constructor connectedConstructor;
    public bool IsObjectConstructed()
    {
        return connectedConstructor.IsBuilt();
    }
    public float GetObjectHealthPercent()
    {
        return connectedConstructor.GetCurrentHealthPercent();
    }
}
