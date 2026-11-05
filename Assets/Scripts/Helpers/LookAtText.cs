using System.Text;
using ItemScript;
using UnityEngine;

// Player-facing names for the look-at panel, derived from the data the objects already have.
public static class LookAtText
{
    public static string MaterialName(CarriableType type)
    {
        switch (type)
        {
            case CarriableType.EnergyDrink: return "Energy Drink";
            case CarriableType.PetFood: return "Pet Food";
            default: return type.ToString();
        }
    }

    public static string ItemName(CarriableObject_SP carriable)
    {
        switch (carriable.carriableType)
        {
            case CarriableType.Wood:
            case CarriableType.Brick:
            case CarriableType.Glass:
                string material = MaterialName(carriable.carriableType);
                return carriable.isRawMaterial ? material : material + " Tile";
            case CarriableType.Usable:
                return CleanObjectName(carriable.gameObject.name);
            default:
                return MaterialName(carriable.carriableType);
        }
    }

    public static string ItemName(GameObject prefab, string fallback)
    {
        if (prefab != null && prefab.TryGetComponent(out CarriableObject_SP carriable)) return ItemName(carriable);
        return fallback;
    }

    // "FireExtinguisher(Clone)" becomes "Fire Extinguisher".
    private static string CleanObjectName(string objectName)
    {
        string trimmed = objectName.Replace("(Clone)", string.Empty).Replace('_', ' ').Trim();
        StringBuilder builder = new StringBuilder(trimmed.Length + 4);

        for (int i = 0; i < trimmed.Length; i++)
        {
            if (i > 0 && char.IsUpper(trimmed[i]) && char.IsLower(trimmed[i - 1])) builder.Append(' ');
            builder.Append(trimmed[i]);
        }

        return builder.ToString();
    }
}
