using UnityEngine;

namespace OrbCombat
{
    // Add to the dangerous object (or a parent of the relevant colliders).
    // This preserves the existing Transform-based collision system; no Rigidbody or trigger is required.
    [DisallowMultipleComponent]
    public sealed class ContactHazard : MonoBehaviour
    {
        [Header("Hazard Colliders")]
        [Tooltip("Only these colliders cause damage. Empty = auto-find colliders on this object and children.")]
        public Collider[] hazardColliders;

        [Header("Damage")]
        [Min(1)] public int damage = 1;
        public bool damagePlayer = true;
        public bool damageEnemies = true;

        private void Awake() => EnsureColliders();

        private void OnValidate() => EnsureColliders();

        private void EnsureColliders()
        {
            if (hazardColliders == null || hazardColliders.Length == 0)
                hazardColliders = GetComponentsInChildren<Collider>(true);
        }

        // Matches the old OfficeEncounter.TouchesHazard() proximity test.
        // forPlayer selects which target type can receive damage from this hazard.
        public bool Touches(Vector3 targetPosition, float targetRadius, bool forPlayer)
        {
            if (!isActiveAndEnabled || (forPlayer ? !damagePlayer : !damageEnemies))
                return false;

            Vector3 center = CombatPhysics.At(targetPosition, 0.55f);
            float reach = targetRadius + 0.01f;
            float reachSquared = reach * reach;

            if (hazardColliders == null) return false;
            foreach (var collider in hazardColliders)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    continue;
                if ((collider.ClosestPoint(center) - center).sqrMagnitude <= reachSquared)
                    return true;
            }
            return false;
        }
    }
}
