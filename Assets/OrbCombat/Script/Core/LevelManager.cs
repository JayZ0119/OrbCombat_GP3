using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrbCombat
{
    // Scene composition, hazards, black holes, win/lose status and progression.
    public sealed class LevelManager : MonoBehaviour
    {
        [Header("Scene setup")]
        public string roomName = "HR / Open Office";
        public string nextSceneName = "Level02_MeetingRoom";
        public LayerMask walls;
        public EnemyBase[] enemies = new EnemyBase[0];
        public BlackHole[] blackHoles = new BlackHole[0];
        public Transform[] routePoints = new Transform[0];
        // Cached per-object hazard components, discovered when the level is bound.
        private ContactHazard[] contactHazards = new ContactHazard[0];
        public Collider reboundScreen;

        private GameManager game;
        public EnemyBase[] Enemies => enemies;
        public LayerMask Walls => walls;
        public Collider ReboundScreen => reboundScreen;
        public bool HasRotatingTable => game && game.rotatingTable;
        public RotatingTable ActiveTable => game ? game.rotatingTable : null;
        public int EnemyCount => enemies == null ? 0 : enemies.Length;
        public int Remaining
        {
            get
            {
                int count = 0;
                if (enemies != null)
                    foreach (var enemy in enemies)
                        if (enemy && enemy.IsAlive) count++;
                return count;
            }
        }
        public bool IsComplete => EnemyCount > 0 && Remaining == 0;

        public void Bind(GameManager gameManager)
        {
            game = gameManager;
            if (enemies == null) enemies = new EnemyBase[0];
            if (blackHoles == null) blackHoles = new BlackHole[0];
            RefreshContactHazards();
            foreach (var enemy in enemies)
                if (enemy && enemy.LaserAttack)
                    enemy.LaserAttack.Bind(gameManager, this, gameManager.player);
        }

        public void ResetLevel()
        {
            if (!game) return;
            game.player.ResetState();
            game.playerHealth.ResetHealth();
            if (game.rotatingTable) game.rotatingTable.ResetState();
            foreach (var enemy in enemies)
                if (enemy) enemy.ResetState();
            game.orb.ResetState();
            Physics.SyncTransforms();
            game.ResetStartState();
        }

        public bool IsInBlackHole(Vector3 position)
        {
            foreach (var hole in blackHoles)
                if (hole && hole.Contains(position)) return true;
            return false;
        }

        // Automatically discover hazards placed in this scene: no central Collider array to wire.
        // Call RefreshContactHazards() if hazards are spawned dynamically after the level starts.
        public void RefreshContactHazards()
        {
            var found = Object.FindObjectsByType<ContactHazard>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var inScene = new List<ContactHazard>();
            foreach (var hazard in found)
                if (hazard && hazard.gameObject.scene == gameObject.scene)
                    inScene.Add(hazard);
            contactHazards = inScene.ToArray();
        }

        public bool TryGetContactHazard(Vector3 position, float radius, bool forPlayer,
                                        out ContactHazard touchingHazard)
        {
            foreach (var hazard in contactHazards)
            {
                if (!hazard || !hazard.Touches(position, radius, forPlayer)) continue;
                touchingHazard = hazard;
                return true;
            }
            touchingHazard = null;
            return false;
        }

        public void TryLoadNextScene()
        {
            if (IsComplete && !HasRotatingTable && !string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }

        private void OnGUI()
        {
            if (!game || !game.playerHealth || enemies == null) return;
            GUI.Box(new Rect(12, 10, 365, 28),
                $"{roomName}     Health {game.playerHealth.CurrentHealth}/{game.playerHealth.MaxHealth}     Enemies {Remaining}/{EnemyCount}");
            string help = HasRotatingTable
                ? "WASD move   /   Hold left to charge, release to throw   /   RMB recall\nHit either table end to spin it.   R restart"
                : "WASD move   /   Left click throw   /   Right click recall   /   R restart\nBank shots off walls and desks. Push enemies into a CEO mouth.";
            GUI.Box(new Rect(12, Screen.height - 60, 430, 50), help);
            if (game.playerHealth.IsDead || IsComplete)
            {
                string message = IsComplete
                    ? (HasRotatingTable ? "Meeting Room cleared" : "Heavy throw unlocked!")
                    : "Defeated";
                string action = IsComplete && !HasRotatingTable ? "Enter - Meeting Room" : "Press R to restart";
                GUI.Box(new Rect(Screen.width / 2f - 100, Screen.height / 2f - 25, 200, 50),
                    message + "\n" + action);
            }
        }
    }
}
