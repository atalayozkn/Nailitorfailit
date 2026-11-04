using UnityEngine;

public class ShopIndex : MonoBehaviour
{
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Transform spawnPosition;
    [SerializeField] private int itemCost;
    public void Buy()
    {
        if (!CurrencyManager.Instance.HasEnoughCurrency(itemCost))
        {
            WarningManager.Show("Not enough money");
            return;
        }
        CurrencyManager.Instance.SpendCurrency(itemCost);

        var obj = Instantiate(itemPrefab, spawnPosition.position, spawnPosition.rotation);
        obj.transform.SetParent(null);
    }
}
