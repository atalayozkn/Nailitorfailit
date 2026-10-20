using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject mainTab;
    [FormerlySerializedAs("menuTab")]
    [SerializeField] private GameObject levelSelectTab;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;

    public void SetActivity(bool condition)
    {
        menuRoot.SetActive(condition);
    }
    public void SwitchToMainTab()
    {
        if (mainTab != null) mainTab.SetActive(true);
        if (levelSelectTab != null) levelSelectTab.SetActive(false);
        RefreshContinueButton();
    }
    public void SwitchToLevelSelectTab()
    {
        if (mainTab != null) mainTab.SetActive(false);
        if (levelSelectTab != null) levelSelectTab.SetActive(true);
    }

    // Button hooks (assign in the Button OnClick events).
    public void NewGame()
    {
        GameManager.Instance.NewGame();
    }
    public void Continue()
    {
        GameManager.Instance.Continue();
    }

    private void RefreshContinueButton()
    {
        if (continueButton == null) return;
        continueButton.interactable = GameManager.Instance != null && GameManager.Instance.CanContinue();
    }
}
