using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopInfo : MonoBehaviour
{
    [SerializeField] private GameObject[] indexInfos;
    private int currentIndex = 0;
    private void Awake()
    {
        DisableInfo();
    }
    public void ActivateIndex(int index)
    {
        indexInfos[currentIndex].SetActive(false);
        currentIndex = index;
        indexInfos[currentIndex].SetActive(true);
    }
    public void DisableInfo()
    {
        foreach (var item in indexInfos) item.SetActive(false);
    }
}
