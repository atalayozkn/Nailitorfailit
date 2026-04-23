using UnityEngine;
using Interactions;
using ItemScript;
using GameData;
using System.Collections.Generic;

namespace PlayerScripts
{
    public class InteractionDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [SerializeField] private float interactRange = 4f;
        [SerializeField] private LayerMask interactionLayers;
        [SerializeField] private float angleThreshold = 0.5f;

        private PlayerCarry playerCarry;
        private Collider[] hitColliders = new Collider[15];

        private void Start()
        {
            playerCarry = GetComponent<PlayerCarry>();
        }

        public bool TryFindTarget(Vector3 origin, out IInteractable bestTarget, out Collider bestCollider)
        {
            bestTarget = null;
            bestCollider = null;

            // 1. First, check what we are looking DIRECTLY at (Raycast)
            // This ensures if you aim at something specific, it ALWAYS wins.
            Collider directHitCollider = null;
            if (Physics.Raycast(origin, transform.forward, out RaycastHit hitInfo, interactRange, interactionLayers))
            {
                directHitCollider = hitInfo.collider;
            }

            // 2. Find everything nearby
            int numFound = Physics.OverlapSphereNonAlloc(origin, interactRange, hitColliders, interactionLayers);

            // 3. Get Held Item
            CarriableObject heldItem = null;
            if (playerCarry.IsCarrying)
            {
                heldItem = GetComponentInChildren<CarriableObject>();
            }

            float bestScore = -9999f;

            for (int i = 0; i < numFound; i++)
            {
                var col = hitColliders[i];
                if (col == null) continue;

                // FIX: Verify component exists before calculating score
                var interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null) continue;

                // --- IMPROVED DECISION LOGIC ---
                float score = CalculateScore(col, interactable, heldItem, origin, directHitCollider);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = interactable;
                    bestCollider = col;
                }
            }

            return bestTarget != null;
        }

        private float CalculateScore(Collider col, IInteractable target, CarriableObject heldItem, Vector3 origin, Collider directHitCol)
        {
            float score = 0;

            // 1. Closest Point Logic
            // Find the spot on the collider closest to our origin (player)
            Vector3 closestPoint = col.ClosestPoint(origin);
            float realDistance = Vector3.Distance(origin, closestPoint);

            // --- NEW FIX: PROXIMITY OVERRIDE ---
            // If we are very close (e.g., under it, inside it, or touching it), 
            // we consider this a valid target regardless of where we are looking.
            bool isTouchingOrVeryClose = realDistance < 0.75f;

            // 2. Raycast Priority (Direct Hit)
            if (col == directHitCol)
            {
                score += 1000f; // Massive bonus for looking directly at it
            }
            // 3. Proximity Priority (Standing Under/Inside)
            else if (isTouchingOrVeryClose)
            {
                score += 500f; // Big bonus for standing right next to/under it
            }

            // 4. Angle Calculation
            Vector3 dirToTarget = (closestPoint - origin).normalized;
            float lookDot = Vector3.Dot(transform.forward, dirToTarget);

            // --- CRITICAL CHANGE ---
            // Only kill the score based on angle IF we are NOT touching/close and NOT looking at it directly.
            // This allows interaction when standing under the orange wall (Dot product ~0)
            if (lookDot < angleThreshold && col != directHitCol && !isTouchingOrVeryClose)
            {
                return -1000f;
            }

            // Add angle bonus only if it's positive (don't subtract score for being under)
            if (lookDot > 0) score += lookDot * 10f;

            // Distance Penalty (The closer, the better)
            score -= realDistance * 2f;

            // --- B. Context Logic (Same as before) ---

            if (target is WorkStation station)
            {
                if (heldItem != null)
                {
                    bool recipeMatch = false;
                    if (station.validRecipes != null)
                    {
                        foreach (var recipe in station.validRecipes)
                        {
                            if (recipe.inputMaterial == heldItem.Material)
                            {
                                recipeMatch = true;
                                break;
                            }
                        }
                    }
                    if (recipeMatch) score += 200f;
                    else score -= 50f;
                }
                else score += 20f;
            }
            else if (target is ConstructObject construction)
            {
                if (construction.IsBuilt) score -= 500f;
                else if (heldItem != null) score += 150f;
            }
            else if (target is CarriableObject itemOnGround)
            {
                if (itemOnGround == heldItem) return -10000f;
                if (heldItem != null) score -= 1000f;
                else score += 50f;
            }

            return score;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactRange);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, transform.forward * interactRange);
        }
    }
}