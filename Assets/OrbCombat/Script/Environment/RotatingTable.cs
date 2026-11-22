using System.Collections.Generic;
using UnityEngine;

namespace OrbCombat
{
    // Optional Meeting Room object. Rotation and actor sweeps are self-contained.
    public sealed class RotatingTable : MonoBehaviour
    {
        public BoxCollider tableCollider;
        [Min(1f)] public float rotationStep = 30f;
        [Min(0.1f)] public float sweepSpeed = 18f;
        [Min(0.1f)] public float rotationSpeed = 150f;
        private Quaternion initialRotation;
        private float turnRemaining;
        public BoxCollider TableCollider => tableCollider;

        private void Awake()
        {
            if (!tableCollider) tableCollider = GetComponent<BoxCollider>();
            initialRotation = transform.rotation;
        }

        public void ResetState()
        {
            transform.rotation = initialRotation;
            turnRemaining = 0f;
        }

        public void OnHeavyOrbHit(RaycastHit surface, Vector3 orbDirection)
        {
            float torque = Vector3.Cross(surface.point - transform.position, orbDirection).y;
            if (Mathf.Abs(torque) > 0.05f)
                turnRemaining = Mathf.Sign(torque) * rotationStep;
        }

        public void Tick(float dt, LevelManager level, PlayerController player, OrbController orb)
        {
            if (!tableCollider || Mathf.Abs(turnRemaining) < 0.001f) return;
            float degrees = Mathf.Min(Mathf.Abs(turnRemaining), rotationSpeed * dt);
            float tip = tableCollider.bounds.extents.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(degrees * Mathf.Deg2Rad * tip / 0.08f));
            float angle = Mathf.Sign(turnRemaining) * degrees / steps;

            // Build the exact actor set from the current level, not array indices.
            var actors = new List<Transform>();
            var radii = new List<float>();
            actors.Add(player.transform); radii.Add(player.collisionRadius);
            foreach (var enemy in level.Enemies)
            {
                if (!enemy || !enemy.IsAlive) continue;
                actors.Add(enemy.transform);
                radii.Add(enemy.Movement.collisionRadius);
            }
            if (orb.IsFlying && orb.Visual)
            {
                actors.Add(orb.Visual);
                radii.Add(orb.Radius);
            }

            Vector3[] displacements = new Vector3[actors.Count];
            for (int n = 0; n < steps; n++)
            {
                Quaternion previous = transform.rotation;
                transform.Rotate(Vector3.up, angle, Space.World);
                Physics.SyncTransforms();
                bool clear = true;
                for (int j = 0; j < actors.Count; j++)
                {
                    displacements[j] = Vector3.zero;
                    float radius = radii[j];
                    Vector3 center = CombatPhysics.At(actors[j].position, 0.55f);
                    Vector3 away = CombatPhysics.Flat(center - tableCollider.ClosestPoint(center));
                    if (away.magnitude >= radius + 0.002f) continue;
                    if (away.sqrMagnitude < 0.000001f) { clear = false; break; }
                    Vector3 offset = away.normalized * (radius + 0.004f - away.magnitude);
                    foreach (var solid in Physics.OverlapSphere(center + offset, radius,
                        level.Walls, QueryTriggerInteraction.Ignore))
                    {
                        if (solid != tableCollider) { clear = false; break; }
                    }
                    if (!clear) break;
                    displacements[j] = offset;
                }
                if (!clear)
                {
                    transform.rotation = previous;
                    Physics.SyncTransforms();
                    turnRemaining = 0f;
                    return;
                }
                turnRemaining -= angle;
                if (Mathf.Abs(turnRemaining) < 0.001f) turnRemaining = 0f;
                for (int j = 0; j < actors.Count; j++)
                {
                    Vector3 offset = displacements[j];
                    if (offset == Vector3.zero) continue;
                    actors[j].position += offset;
                    Vector3 kick = offset.normalized * sweepSpeed;
                    if (actors[j] == player.transform)
                        player.ApplyImpulse(kick);
                    else if (orb.Visual && actors[j] == orb.Visual)
                        orb.DeflectFromTable(offset.normalized);
                    else
                    {
                        var enemy = actors[j].GetComponent<EnemyBase>();
                        if (enemy) enemy.Movement.ApplyImpulse(kick);
                    }
                }
                Physics.SyncTransforms();
            }
        }
    }
}
