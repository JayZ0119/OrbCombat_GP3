using UnityEngine;

namespace OrbCombat
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Min(1)] public int maxHealth = 3;
        [Min(0f)] public float immunitySeconds = 1.1f;
        public Renderer playerRenderer;

        private float immunity;
        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        public bool IsDead => CurrentHealth <= 0;

        private void Awake()
        {
            if (!playerRenderer) playerRenderer = GetComponent<Renderer>();
            CurrentHealth = maxHealth;
        }

        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            immunity = 0f;
            SyncVisuals();
        }

        public bool TryDamage(int amount = 1)
        {
            if (IsDead || immunity > 0f) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            immunity = immunitySeconds;
            return true;
        }

        public void Kill() => CurrentHealth = 0;
        public void Tick(float dt) => immunity -= dt;
        public void SyncVisuals()
        {
            if (playerRenderer)
                playerRenderer.enabled = immunity <= 0f || Mathf.Repeat(immunity, 0.18f) < 0.11f;
        }
    }
}
