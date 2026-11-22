using UnityEngine;

namespace OrbCombat
{
    // Detection and waypoint navigation only; EnemyMovement applies the displacement.
    public sealed class EnemyChase : MonoBehaviour
    {
        [Tooltip("Enable for the original enemy[0] and active Meeting Room chasers.")]
        public bool startsChasing = false;
        [Min(0.1f)] public float detectionRange = 7f;
        [Min(0.1f)] public float moveSpeed = 1.9f;
        public bool IsChasing { get; private set; }

        public void ResetState() => IsChasing = startsChasing;
        public void OnOrbHit() => IsChasing = true;

        public void Scan(Vector3 playerPosition, LevelManager level)
        {
            if (IsChasing) return;
            Vector3 toPlayer = CombatPhysics.Flat(playerPosition - transform.position);
            if (toPlayer.magnitude < detectionRange &&
                !Physics.Linecast(CombatPhysics.At(transform.position, 0.55f),
                                  CombatPhysics.At(playerPosition, 0.55f),
                                  level.Walls, QueryTriggerInteraction.Ignore))
                IsChasing = true;
        }

        private bool Clear(Vector3 a, Vector3 b, LevelManager level, bool approachPlayer = false)
        {
            Vector3 delta = CombatPhysics.Flat(b - a);
            if (approachPlayer && Physics.Linecast(CombatPhysics.At(a, 0.55f),
                CombatPhysics.At(b, 0.55f), level.Walls, QueryTriggerInteraction.Ignore))
                return false;
            float distance = Mathf.Max(0f, delta.magnitude - (approachPlayer ? 0.8f : 0f));
            return distance == 0f || !CombatPhysics.Wall(a, delta.normalized,
                distance, 0.5f, level.Walls, out _);
        }

        // Extracted visibility graph from OfficeEncounter.Next().
        public Vector3 NextWaypoint(Vector3 from, Vector3 goal, LevelManager level)
        {
            if (Clear(from, goal, level, true)) return goal;
            Transform[] points = level.routePoints;
            if (points == null || points.Length == 0) return from;
            int n = points.Length;
            float[] cost = new float[n];
            bool[] used = new bool[n];
            int[] first = new int[n];
            for (int i = 0; i < n; i++)
            {
                first[i] = i;
                cost[i] = points[i] && Clear(from, points[i].position, level)
                    ? CombatPhysics.Flat(from - points[i].position).magnitude
                    : float.PositiveInfinity;
            }
            float best = float.PositiveInfinity;
            Vector3 next = from;
            for (int pass = 0; pass < n; pass++)
            {
                int k = -1;
                for (int i = 0; i < n; i++)
                    if (!used[i] && cost[i] < best && (k < 0 || cost[i] < cost[k])) k = i;
                if (k < 0) break;
                used[k] = true;
                Vector3 p = points[k].position;
                float total = cost[k] + CombatPhysics.Flat(goal - p).magnitude;
                if (total < best && Clear(p, goal, level, true))
                {
                    best = total;
                    next = points[first[k]].position;
                }
                for (int i = 0; i < n; i++)
                {
                    if (!points[i]) continue;
                    float distance = cost[k] + CombatPhysics.Flat(p - points[i].position).magnitude;
                    if (!used[i] && distance < cost[i] && Clear(p, points[i].position, level))
                    {
                        cost[i] = distance;
                        first[i] = first[k];
                    }
                }
            }
            return next;
        }
    }
}
