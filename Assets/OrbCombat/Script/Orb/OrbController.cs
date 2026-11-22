using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbCombat
{
    // Input, shot state, return, cooldown, and visuals. OrbCollision handles collisions.
    public sealed class OrbController : MonoBehaviour
    {
        [Header("References")]
        public Transform orbVisual;
        public Transform aimMarker;
        public OrbCollision collision;
        public Renderer orbRenderer;
        [Header("Shot tuning - uses the existing project's OrbTuning")]
        public OrbTuning tuning = new OrbTuning();
        [Min(0.1f)] public float chargeSeconds = 0.65f;
        [Min(0.1f)] public float throwSpeed = 10f;
        [Min(0.1f)] public float recallSpeed = 15f;

        private GameManager game;
        private LevelManager level;
        private PlayerController player;
        private MaterialPropertyBlock tint;
        private float charge;
        private float recovery;
        private bool charging;
        private bool leftHand;

        public bool IsFlying { get; private set; }
        public bool IsReturning { get; set; }
        public bool Banked { get; set; }
        public bool Heavy { get; set; }
        public Vector3 Direction { get; set; } = Vector3.right;
        public Transform Visual => orbVisual;
        public PlayerController Player => player;
        public LevelManager Level => level;
        public float Radius => CombatRound.OrbRadius;
        public float DirectKnockback => tuning.directKnockback;
        public float BankKnockback => tuning.bankKnockback;

        private void Awake()
        {
            tint = new MaterialPropertyBlock();
            if (!orbRenderer && orbVisual) orbRenderer = orbVisual.GetComponent<Renderer>();
        }

        public void Bind(GameManager owner, LevelManager currentLevel, PlayerController currentPlayer)
        {
            game = owner;
            level = currentLevel;
            player = currentPlayer;
            if (!collision) collision = GetComponent<OrbCollision>();
            if (collision) collision.Bind(this);
        }

        public void ResetState()
        {
            charge = recovery = 0f;
            charging = leftHand = IsFlying = IsReturning = Banked = Heavy = false;
            Direction = Vector3.right;
            SyncVisuals();
        }

        public void ReadInput()
        {
            var mouse = Mouse.current;
            if (mouse == null || !orbVisual || !player) return;
            bool throwPressed = mouse.leftButton.wasPressedThisFrame && !IsFlying && recovery <= 0f;
            bool canCharge = level.HasRotatingTable;

            if (canCharge && throwPressed)
            {
                charging = true;
                charge = 0f;
                game.BeginAction();
            }
            if (charging && mouse.leftButton.isPressed) charge += Time.deltaTime;

            if (canCharge ? charging && mouse.leftButton.wasReleasedThisFrame : throwPressed)
            {
                Heavy = charging && charge >= chargeSeconds;
                charging = false;
                IsFlying = true;
                IsReturning = Banked = leftHand = false;
                orbVisual.position = CombatPhysics.At(player.transform.position, 0.7f);
                Direction = player.Aim;
                game.BeginAction();
            }
            if (IsFlying && mouse.rightButton.wasPressedThisFrame) IsReturning = true;
        }

        public void TickRecovery(float dt) => recovery -= dt;
        public void TickFlight(float dt)
        {
            if (IsFlying && collision) collision.Tick(dt);
        }

        public void EndFlight(bool enteredBlackHole)
        {
            if (enteredBlackHole) recovery = tuning.recoverySeconds;
            IsFlying = Banked = false;
        }

        public void DeflectFromTable(Vector3 direction)
        {
            if (!IsFlying) return;
            Direction = direction;
            IsReturning = false;
            Banked = true;
        }

        public void SyncVisuals()
        {
            if (!orbVisual || !player) return;
            if (!IsFlying)
                orbVisual.position = CombatPhysics.At(player.transform.position + player.Aim * 0.48f, 0.7f);
            if (aimMarker)
                aimMarker.position = CombatPhysics.At(player.transform.position + player.Aim * 0.85f, 0.45f);
            orbVisual.gameObject.SetActive(recovery <= 0f);
            if (orbRenderer)
            {
                Color color = Heavy && IsFlying ? Color.magenta
                    : Banked ? new Color(1f, 0.68f, 0.08f) : new Color(0.8f, 0.94f, 1f);
                tint.SetColor("_BaseColor", color);
                tint.SetColor("_Color", color);
                orbRenderer.SetPropertyBlock(tint);
            }
        }

        // Called by OrbCollision after each movement step (as in OfficeEncounter.StepOrb).
        public void UpdateReturnSteering()
        {
            if (IsReturning)
                Direction = CombatPhysics.Flat(player.transform.position - orbVisual.position).normalized;
        }

        public void UpdateFlightSteeringAfterContacts()
        {
            bool near = CombatPhysics.Flat(player.transform.position - orbVisual.position).magnitude < 0.7f;
            if (leftHand && near && !IsReturning) Direction = player.Aim;
            leftHand = !near;
        }

        private void OnGUI()
        {
            if (!level || !level.HasRotatingTable || !player || !game) return;
            Camera camera = player.aimCamera ? player.aimCamera : Camera.main;
            if (!camera) return;
            Vector3 screen = camera.WorldToScreenPoint(player.transform.position);
            Vector2 p = new Vector2(screen.x, Screen.height - screen.y) + new Vector2(8f, -16f);
            Color saved = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(p.x, p.y, 44f, 7f), Texture2D.whiteTexture);
            GUI.color = charge >= chargeSeconds ? Color.yellow : Color.cyan;
            GUI.DrawTexture(new Rect(p.x + 1f, p.y + 1f,
                charging ? 42f * Mathf.Clamp01(charge / chargeSeconds) : 0f, 5f), Texture2D.whiteTexture);
            GUI.color = saved;
        }
    }
}
