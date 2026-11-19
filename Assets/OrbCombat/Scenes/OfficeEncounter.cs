using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbCombat
{
    // All positions, walls and patrol endpoints are authored in the scene.
    public sealed class OfficeEncounter : MonoBehaviour
    {
        public Transform player, orb, aimMarker;
        public Transform[] enemies, mouths, routePoints;
        public Transform patrolEnd;
        public Collider[] contactHazards;
        [Min(1)] public int enemyHealth = 3;
        [Min(0.1f)] public float enemyHitCooldown = 1.1f;
        [Header("Meeting room")]
        public BoxCollider rotatingTable;
        public Collider reboundScreen;
        public Transform manager;
        [Min(0.1f)] public float chargeSeconds = 0.65f;
        [Min(1)] public float rotationStep = 30f;
        [Min(1)] public float sweepSpeed = 18f;
        private Quaternion initialRotation;
        private float turnRemaining, charge, laserClock;
        private bool charging, heavy, boltActive;
        private Vector3 playerImpulse, laserDirection, boltPosition;
        private Vector3[] sweepOffsets;
        private int[] enemyHp;
        private float[] enemyImmunity;
        public LayerMask walls;
        public OrbTuning tuning = new OrbTuning();
        public float mouthRadius = 1.1f;
        private Vector3 start, aim = Vector3.right, movement, direction;
        private Vector3[] spawns, impulses;
        private bool[] chasing;
        private bool flying, returning, banked, leftHand, started, patrolForward;
        private float recovery, immunity;
        private int health, remaining;
        private Camera view;
        private MaterialPropertyBlock tint;
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        private static Vector3 At(Vector3 v, float height) => new Vector3(v.x, height, v.z);

        private void Start()
        {
            view = GetComponent<Camera>();
            tint = new MaterialPropertyBlock();
            start = player.position;
            enemyHp = new int[enemies.Length];
            enemyImmunity = new float[enemies.Length];
            spawns = System.Array.ConvertAll(enemies, e => e.position);
            impulses = new Vector3[enemies.Length];
            chasing = new bool[enemies.Length];
            if (rotatingTable) initialRotation = rotatingTable.transform.rotation;
            sweepOffsets = new Vector3[enemies.Length + 2];
            ResetEncounter();
        }

        private void ResetEncounter()
        {
            player.position = start;
            health = 3;
            if (rotatingTable) rotatingTable.transform.rotation = initialRotation;
            Physics.SyncTransforms();
            turnRemaining = charge = 0;
            laserClock = 0;
            charging = heavy = boltActive = false;
            playerImpulse = Vector3.zero;
            remaining = enemies.Length;
            for (int i = 0; i < enemies.Length; i++)
            {
                enemies[i].position = spawns[i];
                enemies[i].gameObject.SetActive(true);
                enemies[i].GetComponent<Renderer>().enabled = true;
                impulses[i] = Vector3.zero;
                enemyHp[i] = enemyHealth;
                enemyImmunity[i] = 0;
                chasing[i] = i == 0 || (rotatingTable && enemies[i] != manager);
            }
            aim = Vector3.right;
            started = flying = returning = banked = leftHand = false;
            patrolForward = true;
            immunity = recovery = 0;
            DrawOrb();
        }

        private void Update()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k == null || m == null) return;
            if (k.rKey.wasPressedThisFrame) ResetEncounter();
            if (remaining == 0 && !rotatingTable && k.enterKey.wasPressedThisFrame)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Level02_MeetingRoom");
                return;
            }
            if (health == 0 || remaining == 0) return;
            movement = Vector3.ClampMagnitude(new Vector3((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), 0,
                (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)), 1);
            var ray = view.ScreenPointToRay(m.position.ReadValue());
            if (new Plane(Vector3.up, Vector3.up * 0.7f).Raycast(ray, out float distance))
            {
                var delta = Flat(ray.GetPoint(distance) - player.position);
                if (delta.sqrMagnitude > 0.001f) aim = delta.normalized;
            }
            started |= movement.sqrMagnitude > 0;
            bool throwPressed = m.leftButton.wasPressedThisFrame && !flying && recovery <= 0;
            if (rotatingTable && throwPressed) { charging = started = true; charge = 0; }
            if (charging && m.leftButton.isPressed) charge += Time.deltaTime;
            if (rotatingTable ? charging && m.leftButton.wasReleasedThisFrame : throwPressed)
            {
                heavy = charging && charge >= chargeSeconds;
                charging = false;
                started = flying = true;
                leftHand = returning = banked = false;
                orb.position = At(player.position, 0.7f);
                direction = aim;
            }
            if (flying && m.rightButton.wasPressedThisFrame) returning = true;
        }

        private bool Wall(Vector3 from, Vector3 dir, float distance, float radius, out RaycastHit hit)
        {
            return Physics.SphereCast(At(from, 0.55f), radius, dir, out hit, distance, walls, QueryTriggerInteraction.Ignore);
        }

        private void Move(Transform actor, Vector3 delta, float radius)
        {
            for (int i = 0; i < 2 && delta.sqrMagnitude > 0.000001f; i++)
            {
                var dir = delta.normalized;
                if (!Wall(actor.position, dir, delta.magnitude, radius, out var hit))
                {
                    actor.position += delta;
                    return;
                }
                var step = dir * Mathf.Max(0, hit.distance - 0.002f);
                actor.position += step;
                delta = Flat(Vector3.ProjectOnPlane(delta - step, hit.normal));
            }
        }

        private bool Clear(Vector3 a, Vector3 b, bool approachPlayer = false)
        {
            var delta = Flat(b-a);
            // The enemy only needs to get within contact range of the player.
            if (approachPlayer && Physics.Linecast(At(a, 0.55f), At(b, 0.55f), walls, QueryTriggerInteraction.Ignore)) return false;
            float distance = Mathf.Max(0, delta.magnitude - (approachPlayer ? 0.8f : 0f));
            return distance == 0 || !Wall(a, delta.normalized, distance, 0.5f, out _);
        }

        // Visibility graph over editable doorway and desk-corner waypoints.
        private Vector3 Next(Vector3 from, Vector3 goal)
        {
            if (Clear(from, goal, true)) return goal;
            int n = routePoints.Length;
            var cost = new float[n];
            var used = new bool[n];
            var first = new int[n];
            for (int i = 0; i < n; i++)
            {
                first[i] = i;
                cost[i] = Clear(from, routePoints[i].position) ? Flat(from-routePoints[i].position).magnitude : float.PositiveInfinity;
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
                Vector3 p = routePoints[k].position;
                float total = cost[k] + Flat(goal-p).magnitude;
                if (total < best && Clear(p, goal, true)) { best = total; next = routePoints[first[k]].position; }
                for (int i = 0; i < n; i++)
                {
                    float d = cost[k] + Flat(p-routePoints[i].position).magnitude;
                    if (!used[i] && d < cost[i] && Clear(p, routePoints[i].position))
                    { cost[i] = d; first[i] = first[k]; }
                }
            }
            return next;
        }

        private bool InMouth(Vector3 p)
        {
            foreach (var mouth in mouths)
                if (Flat(mouth.position-p).magnitude <= mouthRadius * Mathf.Abs(mouth.lossyScale.x)) return true;
            return false;
        }

        private bool TouchesHazard(Vector3 position, float radius)
        {
            Vector3 center = At(position, 0.55f);
            // Movement stops just short of solid surfaces.
            float reach = radius + 0.01f;
            foreach (var hazard in contactHazards)
                if (hazard != null && hazard.enabled && hazard.gameObject.activeInHierarchy &&
                    (hazard.ClosestPoint(center) - center).sqrMagnitude <= reach * reach)
                    return true;
            return false;
        }

        private void FixedUpdate()
        {
            if (!started || health == 0 || remaining == 0) return;
            float dt = Time.fixedDeltaTime;
            immunity -= dt;
            recovery -= dt;
            if (rotatingTable) TurnTable(dt);
            Move(player, (movement * 5 + playerImpulse) * dt, 0.45f);
            playerImpulse *= Mathf.Exp(-3 * dt);
            if (InMouth(player.position))
            {
                health = 0;
                return;
            }
            bool hurt = TouchesHazard(player.position, 0.45f);
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (!e.gameObject.activeSelf) continue;
                var toPlayer = Flat(player.position-e.position);
                chasing[i] |= toPlayer.magnitude < 7 && !Physics.Linecast(At(e.position, 0.55f), At(player.position, 0.55f), walls, QueryTriggerInteraction.Ignore);
                if (impulses[i].sqrMagnitude > 0.25f)
                {
                    Vector3 velocity = impulses[i];
                    float speed = velocity.magnitude;
                    if (reboundScreen && Wall(e.position, velocity / speed, speed * dt, 0.5f, out var hit)
                        && hit.collider == reboundScreen)
                    {
                        float travel = Mathf.Max(0, hit.distance - 0.003f);
                        e.position += velocity.normalized * travel;
                        impulses[i] = Vector3.Reflect(velocity, Flat(hit.normal).normalized) * 0.88f;
                        Move(e, impulses[i] * (dt - travel / speed), 0.5f);
                    }
                    else Move(e, velocity * dt, 0.5f);
                    impulses[i] *= Mathf.Exp(-3 * dt);
                }
                else if (chasing[i] && e != manager)
                {
                    Vector3 step = Flat(Next(e.position, player.position)-e.position).normalized;
                    foreach (var mouth in mouths)
                    {
                        Vector3 away = Flat(e.position-mouth.position);
                        if (away.magnitude < mouthRadius * Mathf.Abs(mouth.lossyScale.x) + 0.8f && Vector3.Dot(step, away) < 0)
                        {
                            Vector3 tangent = Vector3.Cross(Vector3.up, away.normalized);
                            if (Vector3.Dot(tangent, step) < 0) tangent = -tangent;
                            step = (tangent + away.normalized * 0.3f).normalized;
                        }
                    }
                    Move(e, step * (1.9f * dt), 0.5f);
                }
                else if (i == 1 && patrolEnd != null)
                {
                    Vector3 delta = Flat((patrolForward ? patrolEnd.position : spawns[i])-e.position);
                    if (delta.magnitude < 0.05f) patrolForward = !patrolForward;
                    else Move(e, Vector3.ClampMagnitude(delta, 1.4f * dt), 0.5f);
                }
                if (InMouth(e.position))
                {
                    e.gameObject.SetActive(false);
                    remaining--;
                    continue;
                }
                enemyImmunity[i] -= dt;
                if (enemyImmunity[i] <= 0 && TouchesHazard(e.position, 0.5f))
                {
                    enemyImmunity[i] = enemyHitCooldown;
                    if (--enemyHp[i] <= 0)
                    {
                        e.gameObject.SetActive(false);
                        remaining--;
                        continue;
                    }
                }
                e.GetComponent<Renderer>().enabled = enemyImmunity[i] <= 0 || Mathf.Repeat(enemyImmunity[i], 0.18f) < 0.11f;
                hurt |= Flat(player.position-e.position).magnitude < 0.9f;
            }
            if (manager) hurt |= TickLaser(dt);
            if (remaining > 0 && hurt && immunity <= 0) { health--; immunity = 1.1f; }
            if (flying) StepOrb(dt);
            DrawOrb();
        }

        // Entry distance of a ray into a circular target in the gameplay plane.
        private float Entry(Vector3 center, float radius, bool capture)
        {
            Vector3 offset = Flat(orb.position-center);
            float along = Vector3.Dot(offset,direction);
            float outside = offset.sqrMagnitude-radius*radius;
            if (outside <= 0) return capture || along < 0 ? 0 : float.PositiveInfinity;
            float disc = along*along-outside;
            return along >= 0 || disc < 0 ? float.PositiveInfinity : -along-Mathf.Sqrt(disc);
        }

        private void StepOrb(float dt)
        {
            if (returning) direction = Flat(player.position-orb.position).normalized;
            for (int contact = 0; contact < 5 && dt > 0; contact++)
            {
                float speed = returning ? 15 : 10;
                float travel = speed * dt;
                float wall = Wall(orb.position,direction,travel,CombatRound.OrbRadius,out var surface) ? surface.distance : float.PositiveInfinity;
                float mouth = float.PositiveInfinity;
                foreach (var m in mouths) mouth = Mathf.Min(mouth, Entry(m.position,mouthRadius * Mathf.Abs(m.lossyScale.x),true));
                float hand = returning ? Entry(player.position,0.35f,true) : float.PositiveInfinity;
                float nearest = Mathf.Min(wall, Mathf.Min(mouth,hand));
                int enemy = -1;
                for (int i = 0; i < enemies.Length; i++)
                {
                    if (!enemies[i].gameObject.activeSelf) continue;
                    float d = Entry(enemies[i].position,0.78f,false);
                    if (d < nearest) { nearest = d; enemy = i; }
                }
                orb.position += direction * Mathf.Min(nearest,travel);
                if (nearest > travel) break;
                dt -= nearest / speed;
                returning = false;
                if (enemy >= 0)
                {
                    Vector3 normal = Flat(orb.position-enemies[enemy].position).normalized;
                    if (normal.sqrMagnitude < 0.01f) normal = -direction;
                    impulses[enemy] = -normal * (banked ? tuning.bankKnockback : tuning.directKnockback);
                    chasing[enemy] = true;
                    direction = Vector3.Reflect(direction,normal);
                    orb.position += normal * 0.002f;
                    banked = false;
                }
                else if (nearest == wall)
                {
                    Vector3 normal = Flat(surface.normal).normalized;
                    if (heavy && surface.collider == rotatingTable)
                    {
                        float torque = Vector3.Cross(surface.point - rotatingTable.transform.position, direction).y;
                        if (Mathf.Abs(torque) > 0.05f) turnRemaining = Mathf.Sign(torque) * rotationStep;
                        heavy = false;
                    }
                    direction = Vector3.Reflect(direction,normal);
                    orb.position += normal * 0.002f;
                    banked = true;
                }
                else
                {
                    if (nearest == mouth) recovery = tuning.recoverySeconds;
                    flying = banked = false;
                    return;
                }
            }
            bool near = Flat(player.position-orb.position).magnitude < 0.7f;
            if (leftHand && near && !returning) direction = aim;
            leftHand = !near;
        }

        // Advance in small arcs so even the far tip cannot pass through an actor in one tick.
        private void TurnTable(float dt)
        {
            Transform table = rotatingTable.transform;
            float degrees = Mathf.Min(Mathf.Abs(turnRemaining), 150f * dt);
            float tip = rotatingTable.bounds.extents.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(degrees * Mathf.Deg2Rad * tip / 0.08f));
            float step = Mathf.Sign(turnRemaining) * degrees / steps;
            for (int n = 0; n < steps && degrees > 0; n++)
            {
                Quaternion previous = table.rotation;
                table.Rotate(Vector3.up, step, Space.World);
                Physics.SyncTransforms();
                bool clear = true;
                for (int j = 0; j < sweepOffsets.Length; j++)
                {
                    Transform actor = SweepActor(j);
                    sweepOffsets[j] = Vector3.zero;
                    if (!actor) continue;
                    float radius = j == 0 ? 0.45f : actor == orb ? CombatRound.OrbRadius : 0.5f;
                    Vector3 center = At(actor.position, 0.55f);
                    Vector3 away = Flat(center - rotatingTable.ClosestPoint(center));
                    if (away.magnitude >= radius + 0.002f) continue;
                    if (away.sqrMagnitude < 0.000001f) { clear = false; break; }
                    Vector3 offset = away.normalized * (radius + 0.004f - away.magnitude);
                    foreach (var solid in Physics.OverlapSphere(center + offset, radius, walls, QueryTriggerInteraction.Ignore))
                        if (solid != rotatingTable) { clear = false; break; }
                    if (!clear) break;
                    sweepOffsets[j] = offset;
                }
                if (!clear)
                {
                    table.rotation = previous;
                    Physics.SyncTransforms();
                    turnRemaining = 0;
                    return;
                }
                turnRemaining -= step;
                if (Mathf.Abs(turnRemaining) < 0.001f) turnRemaining = 0;
                for (int j = 0; j < sweepOffsets.Length; j++)
                {
                    Vector3 offset = sweepOffsets[j];
                    if (offset == Vector3.zero) continue;
                    Transform actor = SweepActor(j);
                    actor.position += offset;
                    Vector3 kick = offset.normalized * sweepSpeed;
                    if (actor == player) playerImpulse = kick;
                    else if (actor == orb) { direction = offset.normalized; returning = false; banked = true; }
                    else impulses[j - 1] = kick;
                }
            }
        }

        private Transform SweepActor(int index)
        {
            if (index == 0) return player;
            if (index > enemies.Length) return flying ? orb : null;
            var enemy = enemies[index - 1];
            return enemy.gameObject.activeSelf ? enemy : null;
        }

        // Aim for one second, hold the direction for a readable warning, then fire.
        private bool TickLaser(float dt)
        {
            if (boltActive)
            {
                float distance = 10f * dt;
                bool blocked = Wall(boltPosition, laserDirection, distance, 0.12f, out var hit);
                Vector3 travel = laserDirection * (blocked ? hit.distance : distance);
                Vector3 toPlayer = Flat(player.position - boltPosition);
                float along = Mathf.Clamp(Vector3.Dot(toPlayer, laserDirection), 0, travel.magnitude);
                bool touched = (toPlayer - laserDirection * along).sqrMagnitude <= 0.57f * 0.57f;
                boltPosition += travel;
                if (blocked || touched) { boltActive = false; laserClock = -1.2f; }
                return touched;
            }
            if (!manager.gameObject.activeSelf) return false;
            if (laserClock <= 1f) laserDirection = Flat(player.position - manager.position).normalized;
            laserClock += dt;
            if (laserClock >= 1.65f && laserDirection != Vector3.zero)
            {
                boltPosition = At(manager.position, 0.55f);
                boltActive = true;
            }
            return false;
        }

        private Vector2 ScreenPoint(Vector3 world)
        {
            Vector3 p = view.WorldToScreenPoint(world);
            return new Vector2(p.x, Screen.height - p.y);
        }

        private void DrawMeetingHUD()
        {
            Color original = GUI.color;
            Vector2 p = ScreenPoint(player.position) + new Vector2(8, -16);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(p.x, p.y, 44, 7), Texture2D.whiteTexture);
            GUI.color = charge >= chargeSeconds ? Color.yellow : Color.cyan;
            GUI.DrawTexture(new Rect(p.x + 1, p.y + 1, charging ? 42 * Mathf.Clamp01(charge / chargeSeconds) : 0, 5), Texture2D.whiteTexture);
            if (started && health > 0 && remaining > 0)
            {
                if (boltActive)
                {
                    p = ScreenPoint(boltPosition);
                    GUI.color = Color.red;
                    GUI.DrawTexture(new Rect(p.x-5,p.y-5,10,10), Texture2D.whiteTexture);
                }
                else if (manager.gameObject.activeSelf && laserClock > 0)
                {
                    Vector3 origin = At(manager.position, 0.55f);
                    float length = Wall(origin, laserDirection, 32, 0.12f, out var hit) ? hit.distance : 32;
                    p = ScreenPoint(origin);
                    Vector2 line = ScreenPoint(origin + laserDirection * length) - p;
                    Matrix4x4 matrix = GUI.matrix;
                    GUI.color = laserClock > 1f ? Color.red : new Color(1, 0.45f, 0.15f);
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg, p);
                    GUI.DrawTexture(new Rect(p.x, p.y, line.magnitude, 2), Texture2D.whiteTexture);
                    GUI.matrix = matrix;
                }
            }
            GUI.color = original;
        }

        private void DrawOrb()
        {
            if (!flying) orb.position = At(player.position + aim * 0.48f, 0.7f);
            aimMarker.position = At(player.position + aim * 0.85f, 0.45f);
            orb.gameObject.SetActive(recovery <= 0);
            Color color = heavy && flying ? Color.magenta : banked ? new Color(1, 0.68f, 0.08f) : new Color(0.8f, 0.94f, 1);
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            orb.GetComponent<Renderer>().SetPropertyBlock(tint);
            player.GetComponent<Renderer>().enabled = immunity <= 0 || Mathf.Repeat(immunity, 0.18f) < 0.11f;
        }

        private void OnGUI()
        {
            string room = rotatingTable ? "Meeting Room" : "HR / Open Office";
            GUI.Box(new Rect(12,10,365,28), $"{room}     Health {health}/3     Enemies {remaining}/{enemies.Length}");
            if (rotatingTable) DrawMeetingHUD();
            GUI.Box(new Rect(12,Screen.height-60,430,50), rotatingTable ? "WASD move   /   Hold left to charge, release to throw   /   RMB recall\nHit either table end to spin it.   R restart" : "WASD move   /   Left click throw   /   Right click recall   /   R restart\nBank shots off walls and desks. Push enemies into a CEO mouth.");
            if (health == 0 || remaining == 0)
                GUI.Box(new Rect(Screen.width/2-100,Screen.height/2-25,200,50), (remaining == 0 && !rotatingTable ? "Heavy throw unlocked!\nEnter - Meeting Room" : (remaining == 0 ? "Meeting Room cleared" : "Defeated")+"\nPress R to restart"));
        }
    }
}
