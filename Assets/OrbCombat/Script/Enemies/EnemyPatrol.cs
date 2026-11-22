using UnityEngine;

namespace OrbCombat
{
    // Scene-specific endpoint can be assigned on each prefab instance.
    public sealed class EnemyPatrol : MonoBehaviour
    {
        public Transform patrolEndpoint;
        [Min(0.1f)] public float moveSpeed = 1.4f;
        private bool forward = true;

        public void ResetState() => forward = true;

        public Vector3 DesiredMovement(float dt, Vector3 spawnPosition)
        {
            if (!patrolEndpoint) return Vector3.zero;
            Vector3 destination = forward ? patrolEndpoint.position : spawnPosition;
            Vector3 delta = CombatPhysics.Flat(destination - transform.position);
            if (delta.magnitude < 0.05f)
            {
                forward = !forward;
                return Vector3.zero;
            }
            return Vector3.ClampMagnitude(delta, moveSpeed * dt);
        }
    }
}
