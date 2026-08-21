using Breakables;
using UnityEngine;

public class PlayerPressureHandler : MonoBehaviour
{
    private bool isHeavy;
    private void OnEnable()
    {
        isHeavy = false;
    }
    private void OnCollisionStay(Collision collision) 
    { 
        if (!isHeavy) return; 
        if (!collision.gameObject.CompareTag("Fragile")) return; 
        if (collision.gameObject.TryGetComponent<IBreakable>(out var breakable)) breakable.OnPressureApply();
    }

    public void SetHeavy(bool condition)
    {
        if (condition == isHeavy) return;
        isHeavy = condition;
    }
}
