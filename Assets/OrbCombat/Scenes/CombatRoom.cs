using UnityEngine;
using UnityEngine.InputSystem;
using V2 = System.Numerics.Vector2;

namespace OrbCombat
{
    public partial class CombatRoom : MonoBehaviour
    {
        [Header("HR Office layout (world XZ)")]
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform chaserSpawn;
        [SerializeField] private Transform spikePosition;
        [SerializeField] private Transform[] additionalSpikes = new Transform[0];
        [SerializeField] private Transform holePosition;
        [SerializeField] private Sprite mouthSprite;
        [SerializeField] private Transform mouthVisual;
        [SerializeField] private Transform openingMark;
        [SerializeField] private OrbTuning orbTuning = new OrbTuning();

        private static readonly Color BasicColor = new Color(0.8f, 0.94f, 1);
        private static readonly Color BankColor = new Color(1, 0.68f, 0.08f);
        private static readonly Vector3 MarkSize = new Vector3(0.25f, 0.06f, 0.14f);
        private CombatRound round;
        [SerializeField] private Transform player, enemy, orb, marker;
        [SerializeField] private Transform bankMark, hole, spike;
        [SerializeField] private bool sceneObjectsBuilt;
        [SerializeField] private Transform leftWall, rightWall, bottomWall, topWall;
        [Min(0.01f)] public float spikeRadius = CombatRound.SpikeRadius;
        [Min(0.01f)] public float holeRadius = CombatRound.HoleRadius;
        private Vector3 enemyBaseScale;
        private MaterialPropertyBlock tint;
        private Camera view;
        private V2 aim = V2.UnitX, movement;
        private bool recall, showingBankColor;

        private void Start()
        {
            view = GetComponent<Camera>();
            tint = new MaterialPropertyBlock();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.03f, 0.045f, 0.08f);
            if (!sceneObjectsBuilt)
            {
                float width = CombatRound.HalfWidth * 2, depth = CombatRound.HalfDepth * 2;
                Shape("HR Office floor", PrimitiveType.Cube, new Vector3(width, 0.2f, depth), new Color(0.13f, 0.19f, 0.26f)).position = Vector3.down * 0.11f;
                foreach (int side in new[] { -1, 1 })
                {
                    Shape("Side wall", PrimitiveType.Cube, new Vector3(0.2f, 0.55f, depth + 0.4f), Color.gray).position = Vector3.right * ((CombatRound.HalfWidth + 0.1f) * side);
                    Shape("End wall", PrimitiveType.Cube, new Vector3(width, 0.55f, 0.2f), Color.gray).position = Vector3.forward * ((CombatRound.HalfDepth + 0.1f) * side);
                }
                bankMark = Shape("Opening bank mark", PrimitiveType.Cube, MarkSize, BankColor);
                float diameter = CombatRound.HoleRadius * 2;
                if (mouthVisual != null)
                {
                    hole = mouthVisual;
                }
                else if (mouthSprite != null)
                {
                    hole = new GameObject("CEO mouth").transform;
                    hole.rotation = Quaternion.Euler(90, 0, 0);
                    hole.gameObject.AddComponent<SpriteRenderer>().sprite = mouthSprite;
                }
                else
                {
                    hole = Shape("Black hole", PrimitiveType.Cylinder, new Vector3(diameter, 0.04f, diameter), Color.black);
                }
                spike = Shape("Spike trap", PrimitiveType.Sphere, Vector3.one * 1.05f, new Color(0.65f, 0.14f, 0.45f));
                for (int i = 0; i < 8; i++)
                {
                    Quaternion rotation = Quaternion.Euler(0, i * 45, 0);
                    var tooth = Shape("Spike tooth", PrimitiveType.Cube, new Vector3(0.18f, 0.18f, 0.32f), Color.white);
                    tooth.position = spike.position + rotation * Vector3.forward * 0.59f;
                    tooth.rotation = rotation * Quaternion.Euler(0, 0, 45);
                    tooth.SetParent(spike, true);
                }
                player = Shape("Player", PrimitiveType.Cylinder, new Vector3(0.9f, 0.35f, 0.9f), Color.cyan);
                enemy = Shape("Chaser", PrimitiveType.Cube, Vector3.one, new Color(1, 0.22f, 0.17f));
                orb = Shape("Coffee orb", PrimitiveType.Sphere, Vector3.one * (CombatRound.OrbRadius * 2), BasicColor);
                marker = Shape("Aim", PrimitiveType.Sphere, Vector3.one * 0.14f, Color.white);
                }
            enemyBaseScale = enemy.localScale;
            ResetRound();
        }

        private Transform Shape(string label, PrimitiveType type, Vector3 size, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = label;
            obj.transform.localScale = size;
            SetTint(obj.transform, color);
            return obj.transform;
        }

        private void SetTint(Transform target, Color color)
        {
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            target.GetComponent<Renderer>().SetPropertyBlock(tint);
        }

        private static V2? FloorPoint(Transform anchor) =>
            anchor == null ? (V2?)null : new V2(anchor.position.x, anchor.position.z);

        private V2 MarkPosition => FloorPoint(openingMark) ?? new V2(-1.21f, -5.1f);

        private void ResetRound()
        {
            var traps = new System.Collections.Generic.List<V2>();
            foreach (Transform trap in additionalSpikes)
                if (trap != null && trap.gameObject.activeInHierarchy) traps.Add(FloorPoint(trap).Value);
            round = new CombatRound(FloorPoint(playerSpawn), FloorPoint(chaserSpawn),
                FloorPoint(spikePosition), FloorPoint(holePosition), orbTuning, spikeRadius, holeRadius, RoomMinimum, RoomMaximum, traps.ToArray());
            recall = false;
            if (!sceneObjectsBuilt) bankMark.position = Position(MarkPosition, 0.30f);
            if (mouthVisual == null)
                hole.position = Position(round.HolePosition, 0.04f);
            if (!sceneObjectsBuilt) spike.position = Position(round.SpikePosition, 0.48f);
            DrawActors();
        }

        private void Update()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys == null || mouse == null) return;
            if (keys.rKey.wasPressedThisFrame) ResetRound();
            movement = new V2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            Ray ray = view.ScreenPointToRay(mouse.position.ReadValue());
            if (!new Plane(Vector3.up, Vector3.up * 0.7f).Raycast(ray, out float distance)) return;
            Vector3 hit = ray.GetPoint(distance);
            V2 target = new V2(hit.x, hit.z) - round.Player;
            if (target.LengthSquared() > 0.001f) aim = V2.Normalize(target);
            if (mouse.leftButton.wasPressedThisFrame) round.Launch(aim);
            recall |= mouse.rightButton.wasPressedThisFrame;
        }

        private void FixedUpdate()
        {
            round.Step(Time.fixedDeltaTime, movement, aim, recall);
            recall = false;
            DrawActors();
        }

        private static Vector3 Position(V2 p, float height) => new Vector3(p.X, height, p.Y);

        private void DrawActors()
        {
            player.position = Position(round.Player, 0.35f);
            enemy.position = Position(round.Enemy, 0.5f);
            enemy.localScale = enemyBaseScale * (1 + round.ImpactTimer);
            enemy.gameObject.SetActive(!round.EnemyDefeated);
            marker.position = Position(round.Player + aim * 0.85f, 0.45f);
            orb.gameObject.SetActive(round.RecoveryRemaining <= 0);
            orb.position = Position(round.Flying ? round.Orb : round.Player + aim * 0.48f, 0.7f);
            if (showingBankColor != round.Banked)
            {
                showingBankColor = round.Banked;
                SetTint(orb, showingBankColor ? BankColor : BasicColor);
            }
            player.GetComponent<Renderer>().enabled = round.HurtTimer <= 0 || Mathf.Repeat(round.HurtTimer, 0.18f) < 0.11f;
        }

        private void OnGUI()
        {
            if (round == null) return;
            if (!round.EnemyDefeated)
            {
                Vector3 anchor = view.WorldToScreenPoint(Position(round.Enemy, 1.35f));
                Rect bar = new Rect(anchor.x - 25, Screen.height - anchor.y - 5, 50, 7);
                Color original = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = new Color(1, 0.32f, 0.2f);
                GUI.DrawTexture(new Rect(bar.x + 1, bar.y + 1, 48f * round.EnemyHealth / CombatRound.MaxEnemyHealth, 5), Texture2D.whiteTexture);
                GUI.color = original;
            }
            string orbStatus = round.Returning ? "Returning" : round.Flying ? "In flight" : "Ready";
            if (round.Banked) orbStatus += "  /  Banked";
            if (round.RecoveryRemaining > 0)
                orbStatus = $"Lost in hole  /  {Mathf.Ceil(round.RecoveryRemaining * 10) / 10:0.0}s";
            GUI.Box(new Rect(12, 10, 340, 28), $"HR Office     Health  {round.Health}/3     Orb  {orbStatus}");
            GUI.Box(new Rect(12, Screen.height - 68, Mathf.Min(490, Screen.width - 24), 58),
                "WASD move   /   Left click throw   /   Right click recall   /   R restart\n" +
                "Push the chaser into the hole or spikes. Walls power up your next hit.\n" +
                "From your starting spot, try aiming at the gold mark.");
            if (round.Over)
                GUI.Box(new Rect(Screen.width / 2 - 115, Screen.height / 2 - 26, 230, 52),
                    (round.Health > 0 ? "Room cleared" : "Defeated") + "\nPress R to play again");
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(Position((RoomMinimum + RoomMaximum) * 0.5f, 0), new Vector3(RoomMaximum.X - RoomMinimum.X, 0.1f, RoomMaximum.Y - RoomMinimum.Y));
            DrawAnchor(playerSpawn, 0.45f, Color.cyan);
            DrawAnchor(chaserSpawn, 0.5f, Color.red);
            DrawAnchor(spikePosition, spikeRadius, Color.magenta);
            foreach (var trap in additionalSpikes) DrawAnchor(trap, spikeRadius, Color.magenta);
            DrawAnchor(holePosition, holeRadius, new Color(0.65f, 0.3f, 1));
            Gizmos.color = BankColor;
            Gizmos.DrawCube(Position(MarkPosition, 0.3f), MarkSize);
        }

        private static void DrawAnchor(Transform anchor, float radius, Color color)
        {
            if (anchor == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(anchor.position, radius);
        }
    }
}
