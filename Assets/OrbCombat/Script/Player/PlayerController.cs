using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbCombat
{
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        public Camera aimCamera;
        [Header("Movement")]
        [Min(0.1f)] public float moveSpeed = 5f;
        [Min(0.01f)] public float collisionRadius = 0.45f;
        public float impulseDrag = 3f;

        private LevelManager level;
        private Vector3 spawnPosition;
        private Vector3 input;
        private Vector3 impulse;
        public Vector3 Aim { get; private set; } = Vector3.right;
        public bool HasMovementInput => input.sqrMagnitude > 0f;

        private void Awake() => spawnPosition = transform.position;
        public void Bind(LevelManager currentLevel) => level = currentLevel;

        public void ResetState()
        {
            transform.position = spawnPosition;
            input = impulse = Vector3.zero;
            Aim = Vector3.right;
        }

        public void ReadInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            input = Vector3.ClampMagnitude(new Vector3(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                0f,
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)), 1f);

            Camera view = aimCamera ? aimCamera : Camera.main;
            if (!view) return;
            Ray ray = view.ScreenPointToRay(mouse.position.ReadValue());
            if (new Plane(Vector3.up, Vector3.up * 0.7f).Raycast(ray, out float distance))
            {
                Vector3 delta = CombatPhysics.Flat(ray.GetPoint(distance) - transform.position);
                if (delta.sqrMagnitude > 0.001f) Aim = delta.normalized;
            }
        }

        public void ApplyImpulse(Vector3 velocity) => impulse = velocity;

        public void Tick(float dt)
        {
            CombatPhysics.Move(transform, (input * moveSpeed + impulse) * dt, collisionRadius, level.Walls);
            impulse *= Mathf.Exp(-impulseDrag * dt);
        }
    }
}
