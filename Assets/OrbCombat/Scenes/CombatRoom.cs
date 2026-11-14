using UnityEngine;
using UnityEngine.InputSystem;
using V2 = System.Numerics.Vector2;

namespace OrbCombat
{
    public class CombatRoom : MonoBehaviour
    {
        private CombatRound round = new CombatRound();
        private Transform player, enemy, orb, marker;
        private Camera view;
        private V2 aim = V2.UnitX, movement;
        private bool recall;

        private void Start()
        {
            view = GetComponent<Camera>();

            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.03f, 0.045f, 0.08f);
            Shape("Arena", PrimitiveType.Cube, new Vector3(16, 0.2f, 10), new Color(0.13f, 0.19f, 0.26f)).position = Vector3.down * 0.11f;
            foreach (int side in new[] { -1, 1 })
            {
                Shape("Side wall", PrimitiveType.Cube, new Vector3(0.2f, 0.55f, 10.4f), Color.gray).position = Vector3.right * (8.1f * side);
                Shape("End wall", PrimitiveType.Cube, new Vector3(16, 0.55f, 0.2f), Color.gray).position = Vector3.forward * (5.1f * side);
            }
            float diameter = CombatRound.HoleRadius * 2;
            Shape("Gravity rim", PrimitiveType.Cylinder, new Vector3(diameter, 0.035f, diameter), new Color(0.6f, 0.25f, 1)).position = Position(CombatRound.BlackHole, 0.025f);
            Shape("Black hole", PrimitiveType.Cylinder, new Vector3(diameter * 0.84f, 0.04f, diameter * 0.84f), Color.black).position = Position(CombatRound.BlackHole, 0.04f);
            var spike = Shape("Spike trap", PrimitiveType.Sphere, Vector3.one * 1.05f, new Color(0.65f, 0.14f, 0.45f));
            spike.position = Position(CombatRound.Spike, 0.48f);
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
            orb = Shape("Basic Orb", PrimitiveType.Sphere, Vector3.one * 0.48f, Color.yellow);
            marker = Shape("Aim", PrimitiveType.Sphere, Vector3.one * 0.14f, Color.white);
            DrawActors();
        }

        private static Transform Shape(string label, PrimitiveType type, Vector3 size, Color color)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = label;
            obj.transform.localScale = size;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            obj.GetComponent<Renderer>().SetPropertyBlock(tint);
            return obj.transform;
        }

        private void Update()
        {

            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys == null || mouse == null) return;
            if (keys.rKey.wasPressedThisFrame)
            {
                round = new CombatRound();
                recall = false;
            }
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
            enemy.localScale = Vector3.one * (1 + round.ImpactTimer);
            enemy.gameObject.SetActive(!round.EnemyDefeated);
            marker.position = Position(round.Player + aim * 0.85f, 0.45f);
            orb.position = Position(round.Flying ? round.Orb : round.Player + aim * 0.48f, 0.7f);
            player.GetComponent<Renderer>().enabled = round.HurtTimer <= 0 || Mathf.Repeat(round.HurtTimer, 0.18f) < 0.11f;
        }

        private void OnGUI()
        {
            if (view != null && !round.EnemyDefeated)
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
            GUI.Box(new Rect(12, 10, 330, 28), $"Health  {round.Health}/3      Orb  {(round.Returning ? "returning" : round.Flying ? "in play" : "ready")}");
            GUI.Box(new Rect(12, Screen.height - 52, 410, 46), "WASD move   /   Aim + left click to launch   /   Right click recall\nSpikes wound the chaser. The black hole swallows it.");
            if (round.Over)
                GUI.Box(new Rect(Screen.width / 2 - 115, Screen.height / 2 - 26, 230, 52),
                    (round.Health > 0 ? "Room cleared" : "Defeated") + "\nPress R to play again");
        }
    }
}
