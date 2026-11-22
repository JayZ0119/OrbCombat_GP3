using UnityEngine;

namespace OrbCombat
{
    // Shared enemy health, contact damage, hazard damage, and lifecycle.
    [RequireComponent(typeof(EnemyMovement))]
    public sealed class EnemyBase : MonoBehaviour
    {
        [Header("Stats")]
        [Min(1)] public int maxHealth = 3;
        [Min(0.1f)] public float hazardHitCooldown = 1.1f;
        public Renderer enemyRenderer;

        private Vector3 spawnPosition;
        private int health;
        private float immunity;
        private bool alive;

        public EnemyMovement Movement { get; private set; }
        public EnemyChase Chase { get; private set; }
        public EnemyPatrol Patrol { get; private set; }
        public EnemyLaserAttack LaserAttack { get; private set; }
        public Vector3 SpawnPosition => spawnPosition;
        public bool IsAlive => alive && gameObject.activeInHierarchy;

        private void Awake()
        {
            spawnPosition = transform.position;
            Movement = GetComponent<EnemyMovement>();
            Chase = GetComponent<EnemyChase>();
            Patrol = GetComponent<EnemyPatrol>();
            LaserAttack = GetComponent<EnemyLaserAttack>();
            if (!enemyRenderer) enemyRenderer = GetComponentInChildren<Renderer>();
            health = maxHealth;
            alive = true;
        }

        public void ResetState()
        {
            gameObject.SetActive(true);
            transform.position = spawnPosition;
            health = maxHealth;
            immunity = 0f;
            alive = true;
            if (Movement) Movement.ResetState();
            if (Chase) Chase.ResetState();
            if (Patrol) Patrol.ResetState();
            if (LaserAttack) LaserAttack.ResetState();
            if (enemyRenderer) enemyRenderer.enabled = true;
        }

        public void Tick(float dt, PlayerController player, PlayerHealth playerHealth,
                         LevelManager level)
        {
            if (!IsAlive) return;
            immunity -= dt;

            if (Chase) Chase.Scan(player.transform.position, level);
            Movement.Tick(dt, this, player, level);
            Physics.SyncTransforms();

            if (level.IsInBlackHole(transform.position))
            {
                Die();
                return;
            }
            if (immunity <= 0f && level.TryGetContactHazard(transform.position, 0.5f, false, out var enemyHazard))
            {
                immunity = hazardHitCooldown;
                health -= enemyHazard.damage;
                if (health <= 0)
                {
                    Die();
                    return;
                }
            }
            if (enemyRenderer)
                enemyRenderer.enabled = immunity <= 0f || Mathf.Repeat(immunity, 0.18f) < 0.11f;

            // Preserve the original distance-based enemy contact damage.
            if (CombatPhysics.Flat(player.transform.position - transform.position).magnitude < 0.9f)
                playerHealth.TryDamage();
        }

        public void Die()
        {
            if (!alive) return;
            alive = false;
            gameObject.SetActive(false);
        }
    }
}
