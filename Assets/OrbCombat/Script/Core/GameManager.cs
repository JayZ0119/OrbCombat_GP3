using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbCombat
{
    // Coordinates the simulation only. Game rules live on the individual components.
    public sealed class GameManager : MonoBehaviour
    {
        public LevelManager level;
        public PlayerController player;
        public PlayerHealth playerHealth;
        public OrbController orb;
        public RotatingTable rotatingTable;

        public bool Started { get; private set; }

        private void Start()
        {
            if (!level || !player || !playerHealth || !orb)
            {
                Debug.LogError("GameManager: assign Level, Player, PlayerHealth and Orb in the Inspector.", this);
                enabled = false;
                return;
            }
            level.Bind(this);
            player.Bind(level);
            orb.Bind(this, level, player);
            level.ResetLevel();
        }

        public void BeginAction() => Started = true;
        public void ResetStartState() => Started = false;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                level.ResetLevel();
                return;
            }
            if (level.IsComplete)
            {
                if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                    level.TryLoadNextScene();
                return;
            }
            if (playerHealth.IsDead) return;

            player.ReadInput();
            if (player.HasMovementInput) BeginAction();
            orb.ReadInput();
            orb.SyncVisuals();
        }

        private void FixedUpdate()
        {
            if (!Started || playerHealth.IsDead || level.IsComplete) return;
            float dt = Time.fixedDeltaTime;

            playerHealth.Tick(dt);
            orb.TickRecovery(dt);
            if (rotatingTable) rotatingTable.Tick(dt, level, player, orb);
            player.Tick(dt);
            Physics.SyncTransforms();

            if (level.IsInBlackHole(player.transform.position))
            {
                playerHealth.Kill();
                return;
            }
            if (level.TryGetContactHazard(player.transform.position, 0.45f, true, out var playerHazard))
                playerHealth.TryDamage(playerHazard.damage);

            foreach (var enemy in level.Enemies)
                if (enemy && enemy.IsAlive)
                    enemy.Tick(dt, player, playerHealth, level);

            // Laser shots advance after enemies move, matching the old encounter order.
            foreach (var enemy in level.Enemies)
                if (enemy && enemy.IsAlive && enemy.LaserAttack)
                    enemy.LaserAttack.Tick(dt, player, playerHealth, level);

            orb.TickFlight(dt);
            orb.SyncVisuals();
            playerHealth.SyncVisuals();
        }
    }
}
