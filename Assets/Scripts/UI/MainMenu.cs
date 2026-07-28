using UnityEngine;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject menuTab;
    [SerializeField] private GameObject characterTab;

    public void SetActivity(bool condition)
    {
        menuRoot.SetActive(condition);
    }
    public void SwitchToMenuTab()
    {
        menuTab.SetActive(true);
        characterTab.SetActive(false);
    }
    public void SwitchToCharacterTab()
    {
        menuTab.SetActive(false);
        characterTab.SetActive(true);
    }
}
