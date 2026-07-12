using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PlayerScripts
{
    public class PlayerStamina_SP : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMove_SP playerMove;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private TextMeshProUGUI staminaText;

        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float rechargeDelay = 1f;
        [SerializeField] private float perChargeAmount = 1f;
        [SerializeField] private float chargeInterval = 0.1f;

        [Header("Mode Settings")]
        [SerializeField] private float energisedDuration = 10f;
        [SerializeField] private float drainedDuration = 5f;
        [SerializeField] private float drainedMultiplier = 5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onChargeStartEvent;
        [SerializeField] private UnityEvent onChargeStopEvent;

        [SerializeField] private UnityEvent onEnergisedEvent;
        [SerializeField] private UnityEvent onEnergiseReversedEvent;
        [SerializeField] private UnityEvent onDrainedEvent;
        [SerializeField] private UnityEvent onDrainReversedEvent;


        private float currentEnergy;
        private Coroutine chargeRoutine;
        private bool isEnergised;
        private bool isDrained;
        private float costMultiplier;
        private void OnEnable()
        {
            currentEnergy = maxEnergy;
            costMultiplier = 1.0f;
            staminaSlider.maxValue = maxEnergy;
            UpdateUI();
        }
        private void OnDisable()
        {
            if (chargeRoutine != null) StopCoroutine(chargeRoutine);
            onChargeStopEvent?.Invoke();
        }
        public void ConsumeEnergy(float value)
        {
            if (isEnergised) return;
            if (chargeRoutine != null) StopCoroutine(chargeRoutine);

            currentEnergy -= value;

            if (currentEnergy <= 0)
            {
                currentEnergy = 0f;
                BecomeDrained();
            }

            UpdateUI();
            CancelInvoke(nameof(StartCharge));
            Invoke(nameof(StartCharge), rechargeDelay);
        }
        public void GainEnergy(float value)
        {
            currentEnergy += value;
            if (currentEnergy > maxEnergy) currentEnergy = maxEnergy;
            BecomeEnergised();
        }
        private void StartCharge()
        {
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
            }

            chargeRoutine = StartCoroutine(ChargeRoutine());
            onChargeStartEvent?.Invoke();
        }
        private IEnumerator ChargeRoutine()
        {
            while (true)
            {
                currentEnergy += perChargeAmount * costMultiplier;
                UpdateUI();
                if (currentEnergy >= maxEnergy)
                {
                    currentEnergy = maxEnergy;
                    onChargeStopEvent?.Invoke();
                    yield break;
                }
                yield return new WaitForSeconds(chargeInterval);
            }
        }
        private void BecomeEnergised()
        {
            isEnergised = true;
            onEnergisedEvent?.Invoke();
            CancelInvoke(nameof(ReverseEnergised));
            Invoke(nameof(ReverseEnergised), energisedDuration);
        }
        private void BecomeDrained()
        {
            isDrained = true;
            costMultiplier = drainedMultiplier;
            onDrainedEvent?.Invoke();
            CancelInvoke(nameof(ReverseDrained));
            Invoke(nameof(ReverseDrained), drainedDuration);
        }

        private void UpdateUI()
        {
            staminaSlider.value = currentEnergy;
            staminaText.text = Mathf.FloorToInt(currentEnergy).ToString();
        }

        #region UTILITIES
        private void ReverseEnergised()
        {
            isEnergised = false;
            onEnergiseReversedEvent?.Invoke();
        }
        private void ReverseDrained()
        {
            isDrained = false;
            costMultiplier = 1.0f;
            onDrainReversedEvent?.Invoke();
        }
        public bool HasEnoughEnergy(float value)
        {
            float dif = currentEnergy - value;

            if (dif > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion
    }
}