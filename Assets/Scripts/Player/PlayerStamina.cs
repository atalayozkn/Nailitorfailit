using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace PlayerScripts
{
    public class PlayerStamina : NetworkBehaviour
    {
        [Header("Energy Settings")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float currentEnergy = 100f;
        [SerializeField] private float energyDrainDuration = 8f;
        [SerializeField] private float energyRegenRate = 15f;
        [SerializeField] private float sprintUnlockThreshold = 40f;

        [Header("Timing")]
        [SerializeField] private float energyTickRate = 0.1f;
        [SerializeField] private float regenDelay = 0.75f;

        [Header("Energy UI")]
        [SerializeField] private GameObject energyCanvas;
        [SerializeField] private Slider energySlider;

        private bool canSprint = true;
        private bool isSprinting;
        private float lastEnergyUseTime = -999f;

        private Coroutine staminaRoutine;

        public float EnergyPercent => maxEnergy > 0f ? currentEnergy / maxEnergy : 0f;
        public bool CanSprint => canSprint && currentEnergy > 0f;

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isOwned)
            {
                if (energyCanvas != null)
                    energyCanvas.SetActive(false);

                return;
            }

            UpdateEnergyUI();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            StopStaminaRoutine();
        }

        public void SetSprinting(bool value)
        {
            if (!isOwned) return;

            isSprinting = value;

            if (isSprinting || currentEnergy < maxEnergy)
                StartStaminaRoutine();
        }

        public bool TryUseEnergy(float amount)
        {
            if (!isOwned) return false;
            if (amount <= 0f) return true;
            if (currentEnergy < amount) return false;

            currentEnergy -= amount;
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

            lastEnergyUseTime = Time.time;

            if (currentEnergy <= 0f)
                canSprint = false;

            UpdateEnergyUI();
            StartStaminaRoutine();

            return true;
        }

        public void UseEnergy(float amount)
        {
            if (!isOwned) return;
            if (amount <= 0f) return;

            currentEnergy -= amount;
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

            lastEnergyUseTime = Time.time;

            if (currentEnergy <= 0f)
                canSprint = false;

            UpdateEnergyUI();
            StartStaminaRoutine();
        }

        public void RefillEnergy()
        {
            currentEnergy = maxEnergy;
            canSprint = true;
            isSprinting = false;

            UpdateEnergyUI();
            StopStaminaRoutine();
        }

        private void StartStaminaRoutine()
        {
            if (staminaRoutine != null)
                return;

            staminaRoutine = StartCoroutine(StaminaRoutine());
        }

        private void StopStaminaRoutine()
        {
            if (staminaRoutine != null)
            {
                StopCoroutine(staminaRoutine);
                staminaRoutine = null;
            }
        }

        private IEnumerator StaminaRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(energyTickRate);

            while (true)
            {
                HandleStaminaTick();
                UpdateEnergyUI();

                if (!isSprinting && currentEnergy >= maxEnergy && canSprint)
                {
                    currentEnergy = maxEnergy;
                    UpdateEnergyUI();
                    staminaRoutine = null;
                    yield break;
                }

                yield return wait;
            }
        }

        private void HandleStaminaTick()
        {
            if (isSprinting && canSprint)
            {
                float drainPerSecond = maxEnergy / energyDrainDuration;

                currentEnergy -= drainPerSecond * energyTickRate;
                currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

                lastEnergyUseTime = Time.time;

                if (currentEnergy <= 0f)
                {
                    currentEnergy = 0f;
                    canSprint = false;
                    isSprinting = false;
                }

                return;
            }

            bool canRegenNow = Time.time - lastEnergyUseTime >= regenDelay;

            if (!canRegenNow)
                return;

            currentEnergy += energyRegenRate * energyTickRate;
            currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

            if (!canSprint && currentEnergy >= sprintUnlockThreshold)
                canSprint = true;
        }

        private void UpdateEnergyUI()
        {
            if (!isOwned) return;

            if (energySlider != null)
                energySlider.value = EnergyPercent;

            if (energyCanvas != null)
                energyCanvas.SetActive(currentEnergy < maxEnergy);
        }

        private void OnDisable()
        {
            StopStaminaRoutine();
        }
    }
}