using UnityEngine;

[CreateAssetMenu(fileName = "ConstructableObjectData", menuName = "Game/Constructable Object Data")]
public class ConstructableObjectData : ScriptableObject
{
    [Header("Stats")]
    public int objectHealth;
}