using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MpLevelSelectUI : MonoBehaviour
{
    public static MpLevelSelectUI Instance { get; private set; }

    [Header("Sahnedeki level secici objeleri")]
    [SerializeField] private GameObject pickerRoot;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    private void Awake()
    {
        Instance = this;

        if (pickerRoot == null) pickerRoot = gameObject;

        if (levelLabel == null)
        {
            Debug.LogWarning($"[MP-LEVELS] '{pickerRoot.name}' uzerinde LevelLabel atanmamis, secici guncellenmeyecek. Tools > MP > Menu > 4 ile kurabilirsin.");
            return;
        }

        if (previousButton != null && previousButton.onClick.GetPersistentEventCount() == 0)
            previousButton.onClick.AddListener(HostPrevious);

        if (nextButton != null && nextButton.onClick.GetPersistentEventCount() == 0)
            nextButton.onClick.AddListener(HostNext);

        MpLevelNet.LevelChanged += OnLevelChanged;
    }

    private void OnDestroy()
    {
        MpLevelNet.LevelChanged -= OnLevelChanged;
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    public void HostPrevious()
    {
        MpLevelNet.Instance?.HostPrevious();
    }

    public void HostNext()
    {
        MpLevelNet.Instance?.HostNext();
    }

    private void Refresh()
    {
        if (levelLabel == null) return;

        MpLevelCatalog catalog = MpLevelCatalog.Instance;
        string text = catalog != null ? catalog.SelectedLabel() : string.Empty;

        if (levelLabel.text != text) levelLabel.text = text;

        bool isHost = NetworkServer.active;
        if (previousButton != null && previousButton.gameObject.activeSelf != isHost) previousButton.gameObject.SetActive(isHost);
        if (nextButton != null && nextButton.gameObject.activeSelf != isHost) nextButton.gameObject.SetActive(isHost);
    }

    private void OnLevelChanged(int index)
    {
        Refresh();
    }
}
