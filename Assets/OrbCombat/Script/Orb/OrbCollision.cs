using UnityEngine;

namespace OrbCombat
{
    // Predictive circle hits and wall bounce, extracted from OfficeEncounter.StepOrb().
    public sealed class OrbCollision : MonoBehaviour
    {
        private OrbController orb;
        public void Bind(OrbController controller) => orb = controller;

        public void Tick(float dt)
        {
            if (!orb || !orb.Visual || !orb.Level || !orb.Player) return;
            var level = orb.Level;
            Transform visual = orb.Visual;
            orb.UpdateReturnSteering();

            for (int contacts = 0; contacts < 5 && dt > 0f; contacts++)
            {
                float speed = orb.IsReturning ? orb.recallSpeed : orb.throwSpeed;
                float travel = speed * dt;
                Vector3 direction = orb.Direction;

                float wall = CombatPhysics.Wall(visual.position, direction, travel,
                    orb.Radius, level.Walls, out var surface) ? surface.distance : float.PositiveInfinity;

                float mouth = float.PositiveInfinity;
                foreach (var hole in level.blackHoles)
                    if (hole) mouth = Mathf.Min(mouth,
                        CombatPhysics.CircleEntry(visual.position, hole.transform.position,
                            direction, hole.EffectiveRadius, true));

                float hand = orb.IsReturning
                    ? CombatPhysics.CircleEntry(visual.position, orb.Player.transform.position,
                        direction, 0.35f, true)
                    : float.PositiveInfinity;
                float nearest = Mathf.Min(wall, Mathf.Min(mouth, hand));
                EnemyBase struckEnemy = null;
                foreach (var enemy in level.Enemies)
                {
                    if (!enemy || !enemy.IsAlive) continue;
                    float distance = CombatPhysics.CircleEntry(visual.position,
                        enemy.transform.position, direction, 0.78f, false);
                    if (distance < nearest)
                    {
                        nearest = distance;
                        struckEnemy = enemy;
                    }
                }

                visual.position += direction * Mathf.Min(nearest, travel);
                if (nearest > travel) break;
                dt -= nearest / speed;
                orb.IsReturning = false;

                if (struckEnemy)
                {
                    Vector3 normal = CombatPhysics.Flat(visual.position - struckEnemy.transform.position).normalized;
                    if (normal.sqrMagnitude < 0.01f) normal = -direction;
                    float power = orb.Banked ? orb.BankKnockback : orb.DirectKnockback;
                    struckEnemy.Movement.ApplyImpulse(-normal * power);
                    if (struckEnemy.Chase) struckEnemy.Chase.OnOrbHit();
                    orb.Direction = Vector3.Reflect(direction, normal);
                    visual.position += normal * 0.002f;
                    orb.Banked = false;
                }
                else if (nearest == wall)
                {
                    Vector3 normal = CombatPhysics.Flat(surface.normal).normalized;
                    RotatingTable table = level.ActiveTable;
                    if (orb.Heavy && table && surface.collider == table.TableCollider)
                    {
                        table.OnHeavyOrbHit(surface, direction);
                        orb.Heavy = false;
                    }
                    orb.Direction = Vector3.Reflect(direction, normal);
                    visual.position += normal * 0.002f;
                    orb.Banked = true;
                }
                else
                {
                    orb.EndFlight(nearest == mouth);
                    return;
                }
            }
            orb.UpdateFlightSteeringAfterContacts();
        }
    }
}
