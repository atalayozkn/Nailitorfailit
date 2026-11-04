using TMPro;
using UnityEngine;

// Lives on the HUD's timer text and hands it to the level's GameTimeManager, so levels don't each need the
// reference assigned by hand (a missing one left the HUD frozen while the timer ran).
[RequireComponent(typeof(TextMeshProUGUI))]
public class GameTimerDisplay : MonoBehaviour
{
    private void Start()
    {
        if (GameTimeManager.Instance != null) GameTimeManager.Instance.BindTimerText(GetComponent<TextMeshProUGUI>());
    }
}
