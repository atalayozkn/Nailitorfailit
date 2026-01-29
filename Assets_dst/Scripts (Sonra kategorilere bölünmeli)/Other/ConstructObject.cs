using Interactions; // Added for Interface
using Unity.Netcode;
using UnityEngine;

namespace GameScripts
{
    public class ConstructObject : NetworkBehaviour, IItemReceiver
    {
        [Header("Settings")]
        [SerializeField] private string requiredItemType = "Brick";
        [SerializeField] private int requiredAmount = 1;

        [Header("Visuals")]
        [SerializeField] private GameObject incompleteModel;
        [SerializeField] private GameObject completeModel;

        private NetworkVariable<int> currentAmount = new NetworkVariable<int>(0);
        private NetworkVariable<bool> isCompleted = new NetworkVariable<bool>(false);

        public override void OnNetworkSpawn()
        {
            currentAmount.OnValueChanged += OnAmountChanged;
            isCompleted.OnValueChanged += OnStateChanged;
            UpdateVisuals(isCompleted.Value);
        }

        public override void OnNetworkDespawn()
        {
            currentAmount.OnValueChanged -= OnAmountChanged;
            isCompleted.OnValueChanged -= OnStateChanged;
        }

        // Interface Implementation
        public bool TryReceiveItem(IPickupable item)
        {
            // Logic handled on Server, checks state and type
            if (isCompleted.Value) return false;
            if (item.ItemType != requiredItemType) return false;

            // Logic logic logic
            currentAmount.Value++;

            if (currentAmount.Value >= requiredAmount)
            {
                isCompleted.Value = true;
                Debug.Log($"[ConstructObject] Construction Completed!");
            }

            // Despawn the item (Destroy it across network)
            if (item.NetworkObject != null && item.NetworkObject.IsSpawned)
            {
                item.NetworkObject.Despawn();
            }

            return true;
        }

        private void OnAmountChanged(int oldVal, int newVal)
        {
            Debug.Log($"[ConstructObject] Progress: {newVal}/{requiredAmount}");
        }

        private void OnStateChanged(bool oldVal, bool newVal)
        {
            UpdateVisuals(newVal);
        }

        private void UpdateVisuals(bool completed)
        {
            if (incompleteModel) incompleteModel.SetActive(!completed);
            if (completeModel) completeModel.SetActive(completed);
        }
    }
}