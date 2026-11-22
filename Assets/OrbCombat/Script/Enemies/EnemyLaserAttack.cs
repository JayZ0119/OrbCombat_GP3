using UnityEngine;

namespace OrbCombat
{
    // Optional ranged attack component. A stationary laser manager is EnemyBase +
    // EnemyMovement + EnemyLaserAttack, with no chase or patrol components.
    public sealed class EnemyLaserAttack : MonoBehaviour
    {
        [Min(0.1f)] public float lockSeconds = 1f;
        [Min(0.1f)] public float fireSeconds = 1.65f;
        [Min(0.1f)] public float projectileSpeed = 10f;
        [Min(0.01f)] public float projectileRadius = 0.12f;
        [Min(0.01f)] public float playerHitRadius = 0.57f;
        public Camera hudCamera;
        public PlayerController target;
        public LevelManager level;
        public GameManager game;

        private float laserClock;
        private Vector3 laserDirection;
        private Vector3 boltPosition;
        private bool boltActive;

        public void Bind(GameManager owner, LevelManager currentLevel, PlayerController currentTarget)
        {
            game = owner;
            level = currentLevel;
            target = currentTarget;
        }

        public void ResetState()
        {
            laserClock = 0f;
            laserDirection = boltPosition = Vector3.zero;
            boltActive = false;
        }

        public void Tick(float dt, PlayerController player, PlayerHealth health, LevelManager currentLevel)
        {
            if (!player || !health) return;
            if (boltActive)
            {
                float distance = projectileSpeed * dt;
                bool blocked = CombatPhysics.Wall(boltPosition, laserDirection, distance,
                    projectileRadius, currentLevel.Walls, out var hit);
                Vector3 travel = laserDirection * (blocked ? hit.distance : distance);
                Vector3 toPlayer = CombatPhysics.Flat(player.transform.position - boltPosition);
                float along = Mathf.Clamp(Vector3.Dot(toPlayer, laserDirection), 0f, travel.magnitude);
                bool touched = (toPlayer - laserDirection * along).sqrMagnitude <= playerHitRadius * playerHitRadius;
                boltPosition += travel;
                if (blocked || touched) { boltActive = false; laserClock = -1.2f; }
                if (touched) health.TryDamage();
                return;
            }
            if (laserClock <= lockSeconds)
                laserDirection = CombatPhysics.Flat(player.transform.position - transform.position).normalized;
            laserClock += dt;
            if (laserClock >= fireSeconds && laserDirection != Vector3.zero)
            {
                boltPosition = CombatPhysics.At(transform.position, 0.55f);
                boltActive = true;
            }
        }

        private Vector2 ScreenPoint(Camera view, Vector3 world)
        {
            Vector3 p = view.WorldToScreenPoint(world);
            return new Vector2(p.x, Screen.height - p.y);
        }

        private void OnGUI()
        {
            if (!game || !game.Started || !target || !level ||
                game.playerHealth.IsDead || level.IsComplete) return;
            Camera view = hudCamera ? hudCamera : Camera.main;
            if (!view) return;
            Color old = GUI.color;
            if (boltActive)
            {
                Vector2 p = ScreenPoint(view, boltPosition);
                GUI.color = Color.red;
                GUI.DrawTexture(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), Texture2D.whiteTexture);
            }
            else if (laserClock > 0f)
            {
                Vector3 origin = CombatPhysics.At(transform.position, 0.55f);
                float length = CombatPhysics.Wall(origin, laserDirection, 32f,
                    projectileRadius, level.Walls, out var hit) ? hit.distance : 32f;
                Vector2 p = ScreenPoint(view, origin);
                Vector2 line = ScreenPoint(view, origin + laserDirection * length) - p;
                Matrix4x4 originalMatrix = GUI.matrix;
                GUI.color = laserClock > lockSeconds ? Color.red : new Color(1f, 0.45f, 0.15f);
                GUIUtility.RotateAroundPivot(Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg, p);
                GUI.DrawTexture(new Rect(p.x, p.y, line.magnitude, 2f), Texture2D.whiteTexture);
                GUI.matrix = originalMatrix;
            }
            GUI.color = old;
        }
    }
}
