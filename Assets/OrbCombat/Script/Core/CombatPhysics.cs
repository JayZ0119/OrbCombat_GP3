using UnityEngine;

namespace OrbCombat
{
    // Shared non-Rigidbody collision math. Used by the player, orb, and enemies.
    public static class CombatPhysics
    {
        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        public static Vector3 At(Vector3 v, float height) => new Vector3(v.x, height, v.z);

        public static bool Wall(Vector3 from, Vector3 direction, float distance,
                                float radius, LayerMask walls, out RaycastHit hit)
        {
            if (direction.sqrMagnitude < 0.000001f || distance <= 0f)
            {
                hit = default(RaycastHit);
                return false;
            }
            return Physics.SphereCast(At(from, 0.55f), radius, direction.normalized,
                                      out hit, distance, walls, QueryTriggerInteraction.Ignore);
        }

        public static void Move(Transform actor, Vector3 delta, float radius, LayerMask walls)
        {
            for (int attempt = 0; attempt < 2 && delta.sqrMagnitude > 0.000001f; attempt++)
            {
                Vector3 direction = delta.normalized;
                if (!Wall(actor.position, direction, delta.magnitude, radius, walls, out var hit))
                {
                    actor.position += delta;
                    return;
                }
                Vector3 step = direction * Mathf.Max(0f, hit.distance - 0.002f);
                actor.position += step;
                delta = Flat(Vector3.ProjectOnPlane(delta - step, hit.normal));
            }
        }

        // Equivalent to the original OfficeEncounter.Entry() calculation.
        public static float CircleEntry(Vector3 from, Vector3 center, Vector3 direction,
                                        float radius, bool capture)
        {
            Vector3 offset = Flat(from - center);
            float along = Vector3.Dot(offset, direction);
            float outside = offset.sqrMagnitude - radius * radius;
            if (outside <= 0f)
                return (capture || along < 0f) ? 0f : float.PositiveInfinity;
            float discriminant = along * along - outside;
            return (along >= 0f || discriminant < 0f)
                ? float.PositiveInfinity : -along - Mathf.Sqrt(discriminant);
        }
    }
}
