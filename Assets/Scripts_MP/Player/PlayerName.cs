using TMPro;
using UnityEngine;

public class PlayerName : MonoBehaviour
{
    [SerializeField] TMP_InputField inputField;
    [SerializeField] int maxLength = 16;

    void Awake()
    {
        if (inputField == null) inputField = GetComponent<TMP_InputField>();

        if (inputField == null)
        {
            Debug.LogWarning($"[Name] '{name}' has no TMP_InputField, so nothing can be typed into it. " +
                             "Replace this object with: GameObject > UI (Canvas) > Input Field - TextMeshPro");
            return;
        }

        inputField.characterLimit = maxLength;

        inputField.onValueChanged.AddListener(Store);
        inputField.onEndEdit.AddListener(LogStored);
    }

    void OnDestroy()
    {
        if (inputField == null) return;

        inputField.onValueChanged.RemoveListener(Store);
        inputField.onEndEdit.RemoveListener(LogStored);
    }

    public void Store(string value)
    {
        LobbyNetworkManager.SetDesiredName(value);
    }

    void LogStored(string value)
    {
        string stored = LobbyNetworkManager.DesiredName;

        Debug.Log(string.IsNullOrEmpty(stored)
            ? "[Name] No name typed - I will join as 'Host' / 'Player N'."
            : $"[Name] I will join as '{stored}'");
    }

    public void StoreCurrent()
    {
        if (inputField != null) Store(inputField.text);
    }
}
