using System;
using V2 = System.Numerics.Vector2;

namespace OrbCombat
{
    public sealed class CombatRound
    {
        public const float HalfWidth = 8, HalfDepth = 5;
        public V2 Player { get; private set; } = new V2(-4, 0);
        public V2 Enemy { get; private set; } = new V2(4, 0);
        public V2 Orb { get; private set; }
        public V2 Direction { get; private set; }
        public int Health { get; private set; } = 3;
        public static readonly V2 Spike = new V2(6, 0);
        public const float SpikeRadius = 0.75f;
        public static readonly V2 BlackHole = new V2(-1, 2.5f);
        public const float HoleRadius = 0.95f;
        public const int MaxEnemyHealth = 3;
        public int EnemyHealth { get; private set; } = MaxEnemyHealth;
        public bool EnemyDefeated => EnemyHealth == 0;
        public float ImpactTimer { get; private set; }
        private V2 knockback;
        public bool Flying { get; private set; }
        public bool Returning { get; private set; }
        public bool Over => Health == 0 || EnemyDefeated;
        public float HurtTimer { get; private set; }

        private bool touchingPlayer;

        public void Launch(V2 aim)
        {
            if (Over || Flying || aim.LengthSquared() < 0.001f) return;
            Direction = V2.Normalize(aim);
            Orb = Player;
            Flying = touchingPlayer = true;
            Returning = false;

        }

        public void Step(float dt, V2 movement, V2 aim, bool recall)
        {
            if (Over) return;
            if (movement.LengthSquared() > 1) movement = V2.Normalize(movement);
            V2 next = Player + movement * (5 * dt);
            Player = new V2(Math.Clamp(next.X, -7.55f, 7.55f), Math.Clamp(next.Y, -4.55f, 4.55f));
            V2 chase = Player - Enemy;
            ImpactTimer = Math.Max(0, ImpactTimer - dt);
            if (knockback.LengthSquared() > 0.25f)
            {
                V2 pushed = Enemy + knockback * dt;
                Enemy = V2.Clamp(pushed, new V2(-7.5f, -4.5f), new V2(7.5f, 4.5f));
                knockback *= (float)Math.Exp(-3.0f * dt);
                if (V2.Distance(Enemy, BlackHole) <= HoleRadius)
                {
                    EnemyHealth = 0;
                    return;
                }
                if (V2.Distance(Enemy, Spike) <= SpikeRadius + 0.5f)
                {
                    EnemyHealth--;
                    V2 outward = Enemy - Spike;
                    outward = outward.LengthSquared() > 0.0001f ? V2.Normalize(outward) : -V2.UnitX;
                    Enemy = Spike + outward * (SpikeRadius + 0.55f);
                    knockback = outward * 5;
                    ImpactTimer = 0.2f;
                    return;
                }
            }
            else if (chase.LengthSquared() > 0.01f)
                Enemy = ChaseStep(chase, dt);
            HurtTimer -= dt;
            if ((V2.Distance(Player, Enemy) < 0.9f || V2.Distance(Player, Spike) < SpikeRadius + 0.45f || V2.Distance(Player, BlackHole) < HoleRadius) && HurtTimer <= 0)
            {
                Health--;
                HurtTimer = 1.1f;
            }
            if (!Flying || Over) return;

            Returning |= recall;
            V2 toPlayer = Player - Orb;
            float travel = 10 * dt;
            if (Returning)
            {
                float distance = toPlayer.Length();
                if (distance > 0.0001f) Direction = toPlayer / distance;
                travel = Math.Min(distance, 15 * dt);
            }

            V2 point = Orb + Direction * travel;
            float dx = Direction.X, dy = Direction.Y;
            Bounce(ref point.X, ref dx, HalfWidth - 0.24f);
            Bounce(ref point.Y, ref dy, HalfDepth - 0.24f);
            V2 wallDirection = new V2(dx, dy);
            if (Direction != wallDirection) Returning = false;
            Direction = wallDirection;
            Orb = point;

            // The spike is solid. The black hole is a floor hazard.
            ReflectFrom(Spike, SpikeRadius + 0.24f, out _);
            if (ReflectFrom(Enemy, 0.78f, out V2 normal))
            {
                knockback = -normal * 14;
                ImpactTimer = 0.2f;
            }
            if (Returning && V2.Distance(Orb, Player) <= 0.35f)
            {
                Flying = Returning = false;
                return;
            }
            bool body = V2.Distance(Orb, Player) < 0.7f;
            if (!Returning && body && !touchingPlayer && aim.LengthSquared() > 0.001f)
                Direction = V2.Normalize(aim);
            touchingPlayer = body;
        }

        private bool ReflectFrom(V2 center, float radius, out V2 normal)
        {
            V2 offset = Orb - center;
            float distance = offset.Length();
            normal = distance > 0.0001f ? offset / distance : -Direction;
            if (distance >= radius) return false;
            Orb = center + normal * (radius + 0.001f);
            if (V2.Dot(Direction, normal) >= 0) return false;
            Direction = V2.Reflect(Direction, normal);
            Returning = false;
            return true;
        }

        private V2 ChaseStep(V2 chase, float dt)
        {
            V2 forward = V2.Normalize(chase);
            V2 direction = forward;
            float nearest = float.MaxValue;
            Avoid(Spike, SpikeRadius + 0.65f);
            Avoid(BlackHole, HoleRadius + 0.6f);
            V2 next = Enemy + direction * Math.Min(chase.Length(), 1.9f * dt);
            return V2.Clamp(next, new V2(-7.5f, -4.5f), new V2(7.5f, 4.5f));

            void Avoid(V2 center, float clearance)
            {
                V2 offset = center - Enemy;
                float ahead = V2.Dot(offset, forward);
                if (ahead <= 0 || ahead > chase.Length() + clearance || offset.Length() >= nearest) return;
                float lateral = forward.X * offset.Y - forward.Y * offset.X;
                if (Math.Abs(lateral) >= clearance || offset.Length() > clearance + 0.9f) return;
                nearest = offset.Length();
                V2 outward = -V2.Normalize(offset);
                V2 tangent = new V2(-outward.Y, outward.X);
                if (lateral < 0) tangent = -tangent;
                direction = V2.Normalize(tangent + outward * Math.Max(0, clearance - nearest));
            }
        }

        private static void Bounce(ref float coordinate, ref float speed, float limit)
        {
            if (Math.Abs(coordinate) <= limit) return;
            float edge = Math.Sign(coordinate) * limit;
            coordinate = 2 * edge - coordinate;
            speed = -speed;
        }
    }
}
