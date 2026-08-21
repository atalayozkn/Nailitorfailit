using ItemScript;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MixerFormula",
    menuName = "Items/Mixer Formula"
)]
public class MixerFormulaSO : ScriptableObject
{
    [Header("Formula")]
    [Tooltip("Required ingredients. Order does not matter.")]
    [SerializeField] private CarriableType[] ingredients;

    [Header("Result")]
    [SerializeField] private GameObject productPrefab;

    public CarriableType[] Ingredients => ingredients;
    public GameObject ProductPrefab => productPrefab;

    public bool Matches(CarriableObject_SP[] objects)
    {
        if (objects == null || objects.Length != ingredients.Length)
            return false;

        bool[] used = new bool[objects.Length];

        for (int i = 0; i < ingredients.Length; i++)
        {
            bool foundMatch = false;

            for (int j = 0; j < objects.Length; j++)
            {
                if (used[j])
                    continue;

                if (objects[j] == null)
                    continue;

                if (objects[j].carriableType != ingredients[i])
                    continue;

                used[j] = true;
                foundMatch = true;
                break;
            }

            if (!foundMatch)
                return false;
        }

        return true;
    }
}