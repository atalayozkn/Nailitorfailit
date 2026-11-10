using PlayerScripts;
using UnityEngine;
using UnityEngine.UI;

// Shows the stamina mode on the bar itself (NailIt/UI/StaminaBar), replacing the old effects on the player:
// idle green, draining (green to amber when low, stripes sliding left), refilling (lighter green, stripes sliding
// right, glowing leading edge), energised (gold with a sweeping sheen) and empty (red pulse).
// Only colours and shader effects change here; the bar's length is always the real stamina, set by PlayerStaminaHandler.
public class StaminaBarVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStaminaHandler stamina;
    [SerializeField] private Image fill;
    [SerializeField] private Material barMaterial;

    [Header("Colours")]
    [SerializeField] private Color idleColor = new Color(0.424f, 0.820f, 0.243f);
    [SerializeField] private Color lowColor = new Color(0.95f, 0.60f, 0.15f);
    [SerializeField] private Color refillColor = new Color(0.56f, 0.90f, 0.40f);
    [SerializeField] private Color energisedColor = new Color(1f, 0.80f, 0.25f);
    [SerializeField] private Color emptyColor = new Color(0.90f, 0.26f, 0.18f);
    [Tooltip("Below this fraction the draining colour turns to amber.")]
    [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.35f;
    [SerializeField] private float blendSpeed = 6f;

    private static readonly int StripeSpeedId = Shader.PropertyToID("_StripeSpeed");
    private static readonly int StripeAmountId = Shader.PropertyToID("_StripeAmount");
    private static readonly int SheenAmountId = Shader.PropertyToID("_SheenAmount");
    private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");
    private static readonly int EdgeGlowId = Shader.PropertyToID("_EdgeGlow");

    private Material instance;
    private Color color;
    private float stripeSpeed, stripeAmount, sheen, pulse, edge;

    private void Awake()
    {
        if (fill == null || barMaterial == null || stamina == null) { enabled = false; return; }
        instance = new Material(barMaterial);
        fill.material = instance;
        color = idleColor;
    }
    private void OnDestroy()
    {
        if (instance != null) Destroy(instance);
    }

    private void LateUpdate()
    {
        Color targetColor = idleColor;
        float tStripeSpeed = 0f, tStripe = 0f, tSheen = 0f, tPulse = 0f, tEdge = 0f;

        if (stamina.IsEnergised)
        {
            targetColor = energisedColor;
            tSheen = 1f;
        }
        else if (stamina.IsExhausted)
        {
            targetColor = emptyColor;
            tPulse = 1f;
            tStripe = 0.6f; tStripeSpeed = 0.9f; tEdge = 0.6f; // it is refilling fast, so keep the refill motion
        }
        else if (stamina.IsDraining)
        {
            float low = Mathf.InverseLerp(lowThreshold, 0f, stamina.Energy01);
            targetColor = Color.Lerp(idleColor, lowColor, low);
            tStripe = 0.7f; tStripeSpeed = -0.8f;
        }
        else if (stamina.IsCharging)
        {
            targetColor = refillColor;
            tStripe = 0.6f; tStripeSpeed = 0.5f; tEdge = 1f;
        }

        float k = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
        color = Color.Lerp(color, targetColor, k);
        stripeAmount = Mathf.Lerp(stripeAmount, tStripe, k);
        // Direction flips instantly; easing through zero would freeze the stripes for a moment.
        stripeSpeed = tStripe > 0f ? tStripeSpeed : stripeSpeed;
        sheen = Mathf.Lerp(sheen, tSheen, k);
        pulse = Mathf.Lerp(pulse, tPulse, k);
        edge = Mathf.Lerp(edge, tEdge, k);

        fill.color = color;
        instance.SetFloat(StripeSpeedId, stripeSpeed);
        instance.SetFloat(StripeAmountId, stripeAmount);
        instance.SetFloat(SheenAmountId, sheen);
        instance.SetFloat(PulseAmountId, pulse);
        instance.SetFloat(EdgeGlowId, edge);
    }
}
