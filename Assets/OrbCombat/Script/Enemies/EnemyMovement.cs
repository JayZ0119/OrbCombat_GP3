using UnityEngine;

namespace OrbCombat
{
    // One authoritative movement component per enemy. No Rigidbody required.
    public sealed class EnemyMovement : MonoBehaviour
    {
        [Header("Movement")]
        [Min(0.01f)] public float collisionRadius = 0.5f;
        [Min(0f)] public float impulseDrag = 3f;
        [Range(0f, 1.5f)] public float bounceFactor = 0.88f;
        [Tooltip("Enable for Stationary Enemy so it bounces off ALL walls, not only the rebound screen.")]
        public bool bounceOffAllWalls = false;

        private Vector3 impulse;
        public void ResetState() => impulse = Vector3.zero;
        public void ApplyImpulse(Vector3 velocity) => impulse = velocity;

        public void Tick(float dt, EnemyBase enemy, PlayerController player, LevelManager level)
        {
            if (impulse.sqrMagnitude > 0.25f)
            {
                Vector3 velocity = impulse;
                float speed = velocity.magnitude;
                bool hitWall = CombatPhysics.Wall(transform.position, velocity / speed,
                    speed * dt, collisionRadius, level.Walls, out var hit);
                bool shouldBounce = hitWall && (bounceOffAllWalls ||
                    (level.ReboundScreen && hit.collider == level.ReboundScreen));

                if (shouldBounce)
                {
                    float travel = Mathf.Max(0f, hit.distance - 0.003f);
                    transform.position += velocity.normalized * travel;
                    Vector3 normal = CombatPhysics.Flat(hit.normal).normalized;
                    impulse = Vector3.Reflect(velocity, normal) * bounceFactor;
                    float remaining = Mathf.Max(0f, dt - travel / speed);
                    CombatPhysics.Move(transform, impulse * remaining, collisionRadius, level.Walls);
                }
                else
                {
                    CombatPhysics.Move(transform, velocity * dt, collisionRadius, level.Walls);
                }
                impulse *= Mathf.Exp(-impulseDrag * dt);
                return; // Being hit temporarily overrides autonomous movement.
            }

            if (enemy.Chase && enemy.Chase.IsChasing)
            {
                Vector3 next = enemy.Chase.NextWaypoint(transform.position, player.transform.position, level);
                Vector3 direction = CombatPhysics.Flat(next - transform.position).normalized;
                CombatPhysics.Move(transform, direction * (enemy.Chase.moveSpeed * dt), collisionRadius, level.Walls);
            }
            else if (enemy.Patrol)
            {
                Vector3 delta = enemy.Patrol.DesiredMovement(dt, enemy.SpawnPosition);
                CombatPhysics.Move(transform, delta, collisionRadius, level.Walls);
            }
            // No chase/patrol component means stationary until struck by the orb.
        }
    }
}
