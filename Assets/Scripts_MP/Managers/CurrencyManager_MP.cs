
using Mirror;

public class CurrencyManager_MP : NetworkBehaviour
{
    public static CurrencyManager_MP Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RequestGain(int amount)
    {
        if (isServer) RpcApplyGain(amount);
        else CmdRequestGain(amount);
    }

    public void RequestSpend(int amount)
    {
        if (isServer) RpcApplySpend(amount);
        else CmdRequestSpend(amount);
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestGain(int amount) => RpcApplyGain(amount);

    [Command(requiresAuthority = false)]
    private void CmdRequestSpend(int amount) => RpcApplySpend(amount);

    [ClientRpc]
    private void RpcApplyGain(int amount) => CurrencyManager.Instance.GainCurrency(amount);

    [ClientRpc]
    private void RpcApplySpend(int amount) => CurrencyManager.Instance.SpendCurrency(amount);
}
