using UnityEngine;

public class Oil_SP : MonoBehaviour
{
    [Header("Fuel Settings")]
    [SerializeField, Range(0f, 100f)]
    private float fuelPercent = 20f;

    public float FuelPercent => fuelPercent;
}