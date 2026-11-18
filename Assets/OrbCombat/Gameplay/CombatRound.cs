using System;
using V2 = System.Numerics.Vector2;

namespace OrbCombat
{
    [Serializable]
    public sealed class OrbTuning
    {
        public float directKnockback = 3.5f;
        public float bankKnockback = 14f;
        public float recoverySeconds = 1.25f;
    }

    public sealed class CombatRound
    {
        public const float HalfWidth = 5.75f, HalfDepth = 5f;
        public const float OrbRadius = 0.24f, SpikeRadius = 0.75f, HoleRadius = 1.1f;
        public static readonly V2 Spike = new V2(4.45f, -2.35f);
        public static readonly V2 BlackHole = new V2(2.25f, 3.9f);
        private const float EnemyContactRadius = 0.78f;
        private readonly OrbTuning tuning;
        private readonly float spikeRadius, holeRadius;
        private readonly V2 roomMin, roomMax;
        private readonly V2[] spikes;

        public V2 Player { get; private set; }
        public V2 Enemy { get; private set; }
        public V2 SpikePosition { get; }
        public V2 HolePosition { get; }
        public V2 Orb { get; private set; }
        public V2 Direction { get; private set; }
        public int Health { get; private set; } = 3;
        public const int MaxEnemyHealth = 3;
        public int EnemyHealth { get; private set; } = MaxEnemyHealth;
        public bool EnemyDefeated => EnemyHealth == 0;
        public float ImpactTimer { get; private set; }
        private V2 knockback;
        public bool Flying { get; private set; }
        public bool Returning { get; private set; }
        public bool Banked { get; private set; }
        public float RecoveryRemaining { get; private set; }
        public bool Over => Health == 0 || EnemyDefeated;
        public float HurtTimer { get; private set; }

        private bool started, touchingPlayer;

        public CombatRound() : this(null, null) { }

        public CombatRound(V2? playerSpawn, V2? enemySpawn, V2? spike = null,
            V2? blackHole = null, OrbTuning tuning = null, float spikeRadius = SpikeRadius, float holeRadius = HoleRadius, V2? roomMin = null, V2? roomMax = null, V2[] extraSpikes = null)
        {
            Player = playerSpawn ?? new V2(-3.55f, 1.35f);
            Enemy = enemySpawn ?? new V2(3.1f, 1.15f);
            SpikePosition = spike ?? Spike;
            spikes = new V2[1 + (extraSpikes?.Length ?? 0)];
            spikes[0] = SpikePosition;
            if (extraSpikes != null) extraSpikes.CopyTo(spikes, 1);
            HolePosition = blackHole ?? BlackHole;
            this.tuning = tuning ?? new OrbTuning();
            this.spikeRadius = Math.Max(0.01f, spikeRadius);
            this.holeRadius = Math.Max(0.01f, holeRadius);
            this.roomMin = roomMin ?? new V2(-HalfWidth, -HalfDepth);
            this.roomMax = roomMax ?? new V2(HalfWidth, HalfDepth);
        }

        public void Launch(V2 aim)
        {
            if (Over || Flying || RecoveryRemaining > 0 || aim.LengthSquared() < 0.001f) return;
            Direction = V2.Normalize(aim);
            Orb = Player;
            started = Flying = touchingPlayer = true;
            Returning = Banked = false;
        }

        public void Step(float dt, V2 movement, V2 aim, bool recall)
        {
            if (Over || dt <= 0) return;
            started |= movement.LengthSquared() > 0;
            if (!started) return;
            RecoveryRemaining = Math.Max(0, RecoveryRemaining - dt);
            if (movement.LengthSquared() > 1) movement = V2.Normalize(movement);
            Player = ClampToRoom(Player + movement * (5 * dt), 0.45f);
            V2 chase = Player - Enemy;
            ImpactTimer = Math.Max(0, ImpactTimer - dt);
            if (knockback.LengthSquared() > 0.25f)
            {
                Enemy = ClampToRoom(Enemy + knockback * dt, 0.5f);
                knockback *= (float)Math.Exp(-3.0f * dt);
                if (V2.Distance(Enemy, HolePosition) <= holeRadius)
                {
                    EnemyHealth = 0;
                    return;
                }
                foreach (V2 center in spikes)
                {
                    if (V2.Distance(Enemy, center) > spikeRadius + 0.5f) continue;
                    EnemyHealth--;
                    V2 outward = Enemy - center;
                    outward = outward.LengthSquared() > 0.0001f ? V2.Normalize(outward) : -V2.UnitX;
                    Enemy = center + outward * (spikeRadius + 0.55f);
                    knockback = outward * 5;
                    ImpactTimer = 0.2f;
                    if (EnemyDefeated) return;
                    break;
                }
            }
            else if (chase.LengthSquared() > 0.01f)
                Enemy = ChaseStep(chase, dt);
            HurtTimer -= dt;
            if ((V2.Distance(Player, Enemy) < 0.9f || TouchesSpike(Player, spikeRadius + 0.45f) || V2.Distance(Player, HolePosition) < holeRadius) && HurtTimer <= 0)
            {
                Health--;
                HurtTimer = 1.1f;
            }
            if (!Flying || Over) return;

            Returning |= recall;
            V2 toPlayer = Player - Orb;
            if (Returning && toPlayer.LengthSquared() > 0.000001f) Direction = V2.Normalize(toPlayer);
            MoveOrb(dt);
            if (!Flying) return;
            bool body = V2.Distance(Orb, Player) < 0.7f;
            if (!Returning && body && !touchingPlayer && aim.LengthSquared() > 0.001f)
                Direction = V2.Normalize(aim);
            touchingPlayer = body;
        }

        private void MoveOrb(float dt)
        {
            // Resolve contacts in travel order, including a wall and a target in the same tick.
            // A bounded loop also handles an orb pinched between a chaser and a wall.
            for (int contact = 0; contact < 4 && dt > 0; contact++)
            {
                float speed = Returning ? 15f : 10f;
                float wallX = WallDistance(Orb.X, Direction.X, roomMin.X + OrbRadius, roomMax.X - OrbRadius);
                float wallY = WallDistance(Orb.Y, Direction.Y, roomMin.Y + OrbRadius, roomMax.Y - OrbRadius);
                float wall = Math.Min(wallX, wallY);
                float spike = float.PositiveInfinity;
                V2 spikeCenter = SpikePosition;
                foreach (var trap in spikes)
                {
                    float distance = CircleDistance(trap, spikeRadius + OrbRadius);
                    if (distance >= spike) continue;
                    spike = distance;
                    spikeCenter = trap;
                }
                float enemy = CircleDistance(Enemy, EnemyContactRadius);
                float hole = CircleDistance(HolePosition, holeRadius, true);
                float hand = Returning ? CircleDistance(Player, 0.35f, true) : float.PositiveInfinity;
                float hit = Math.Min(Math.Min(wall, spike), Math.Min(enemy, Math.Min(hole, hand)));

                if (hit > speed * dt)
                {
                    Orb += Direction * (speed * dt);
                    return;
                }
                Orb += Direction * hit;
                dt -= hit / speed;
                if (hit == hole || hit == hand)
                {
                    if (hit == hole) RecoveryRemaining = Math.Max(0, tuning.recoverySeconds);
                    Flying = Returning = Banked = false;
                    return;
                }
                Returning = false;
                if (hit == wall)
                {
                    Direction = new V2(wallX <= hit + 0.00001f ? -Direction.X : Direction.X,
                                       wallY <= hit + 0.00001f ? -Direction.Y : Direction.Y);
                    Banked = true;
                    continue;
                }

                V2 center = hit == enemy ? Enemy : spikeCenter;
                V2 offset = Orb - center;
                V2 normal = offset.LengthSquared() > 0.000001f ? V2.Normalize(offset) : -Direction;
                float radius = hit == enemy ? EnemyContactRadius : spikeRadius + OrbRadius;
                Orb = ClampToRoom(center + normal * (radius + 0.001f), OrbRadius);
                Direction = V2.Reflect(Direction, normal);
                if (hit == enemy)
                {
                    knockback = -normal * (Banked ? tuning.bankKnockback : tuning.directKnockback);
                    ImpactTimer = Banked ? 0.24f : 0.10f;
                    Banked = false;
                }
            }
        }

        private static float WallDistance(float position, float direction, float minimum, float maximum)
        {
            if (Math.Abs(direction) < 0.000001f) return float.PositiveInfinity;
            return Math.Max(0, ((direction > 0 ? maximum : minimum) - position) / direction);
        }

        private float CircleDistance(V2 center, float radius, bool collectInside = false)
        {
            V2 offset = Orb - center;
            float along = V2.Dot(offset, Direction);
            float outside = offset.LengthSquared() - radius * radius;
            if (outside <= 0) return collectInside || along < 0 ? 0 : float.PositiveInfinity;
            if (along >= 0) return float.PositiveInfinity;
            float discriminant = along * along - outside;
            return discriminant < 0 ? float.PositiveInfinity : -along - (float)Math.Sqrt(discriminant);
        }

        private bool TouchesSpike(V2 point, float radius)
        {
            foreach (var trap in spikes)
                if (V2.Distance(point, trap) < radius) return true;
            return false;
        }

        private V2 ChaseStep(V2 chase, float dt)
        {
            V2 forward = V2.Normalize(chase);
            V2 direction = forward;
            float nearest = float.MaxValue;
            foreach (var trap in spikes) Avoid(trap, spikeRadius + 0.65f);
            Avoid(HolePosition, holeRadius + 0.6f);
            V2 next = Enemy + direction * Math.Min(chase.Length(), 1.9f * dt);
            return ClampToRoom(next, 0.5f);

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

        private V2 ClampToRoom(V2 point, float radius)
        {
            V2 margin = new V2(radius, radius);
            return V2.Clamp(point, roomMin + margin, roomMax - margin);
        }
    }
}
