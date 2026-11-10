using Rivalia.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Rivalia.Game
{
    public enum AimAssistMode { Off, TapToTarget, Magnet }

    /// <summary>
    /// Entrada do jogador humano: dual joystick no celular, WASD + mouse no PC, analógicos no gamepad.
    /// Todas geram os mesmos comandos para o HeroActor (mover, mirar, atacar), como os bots.
    /// Nunca dispara sozinho: a assistência só corrige a direção quando o jogador decide atacar.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public HeroActor Hero;
        public MatchDirector Director;
        public VirtualJoystick MoveStick, AttackStick, AbilityStick, SuperStick;
        public AimAssistMode AimAssist = AimAssistMode.TapToTarget;
        public float MagnetMaxDegrees = 8f;
        public float MagnetConeDegrees = 20f;

        private LineRenderer _aimLine, _superCircle;
        private bool _mouseAim;

        private void Start()
        {
            _aimLine = MakeLine("IndicadorMira", 2, new Color(1f, 1f, 1f));
            _superCircle = MakeLine("IndicadorSuper", 33, new Color(1f, 0.85f, 0.2f));
            _superCircle.loop = true;

            AttackStick.Released += (dir, tap) =>
            {
                if (tap) { if (AimAssist != AimAssistMode.Off && TryAutoTarget(out var t)) Hero.Fire(t - Hero.transform.position); else Hero.Fire(Hero.AimDirection); }
                else if (dir.sqrMagnitude > 0.06f) Hero.Fire(ApplyMagnet(StickToWorld(dir)));
            };
            AbilityStick.Released += (dir, tap) => Hero.UseAbility(tap ? Vector3.zero : StickToWorld(dir));
            SuperStick.Released += (dir, tap) =>
            {
                var p = Director.Match.GetPlayer(Hero.PlayerId);
                Vector3 point;
                if (tap) { if (!TryAutoTarget(out point, p.Stats.SuperRange)) return; }
                else point = Hero.transform.position + StickToWorld(dir).normalized * p.Stats.SuperRange * Mathf.Clamp01(dir.magnitude);
                Hero.UseSuper(point);
            };
        }

        private void Update()
        {
            if (Hero == null || Director?.Match == null) return;
            var state = Director.Match.GetPlayer(Hero.PlayerId);

            // ---------- movimento
            Vector2 move = MoveStick != null ? MoveStick.Value : Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 k = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1;
                if (k != Vector2.zero) move = k.normalized;
            }
            var pad = Gamepad.current;
            if (pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.02f) move = pad.leftStick.ReadValue();
            Hero.SetMove(move);

            // ---------- mira
            var mouse = Mouse.current;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.5f) _mouseAim = true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) _mouseAim = false;

            bool aimingWithStick = AttackStick != null && AttackStick.IsHeld && AttackStick.RawDirection.sqrMagnitude > 0.06f;
            if (aimingWithStick) Hero.SetAim(ApplyMagnet(StickToWorld(AttackStick.RawDirection)));
            else if (pad != null && pad.rightStick.ReadValue().sqrMagnitude > 0.1f) Hero.SetAim(StickToWorld(pad.rightStick.ReadValue()));
            else if (_mouseAim && mouse != null && MouseGroundPoint(out var mp)) Hero.SetAim(mp - Hero.transform.position);

            // ---------- ações por teclado/mouse/gamepad
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUi && _mouseAim) Hero.Fire(Hero.AimDirection);
            if (pad != null && pad.rightTrigger.wasPressedThisFrame) Hero.Fire(Hero.AimDirection);
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) Hero.UseAbility(Hero.AimDirection);
                if (kb.eKey.wasPressedThisFrame)
                {
                    float r = state.Stats.SuperRange;
                    Vector3 target = Hero.transform.position + Hero.AimDirection * r;
                    if (_mouseAim && mouse != null && MouseGroundPoint(out var mp2))
                        target = Hero.transform.position + Vector3.ClampMagnitude(mp2 - Hero.transform.position, r);
                    Hero.UseSuper(target);
                }
                if (kb.fKey.wasPressedThisFrame) Director.Match.TryUsePotion(Hero.PlayerId);
            }
            if (pad != null && pad.leftShoulder.wasPressedThisFrame) Hero.UseAbility(Hero.AimDirection);
            if (pad != null && pad.rightShoulder.wasPressedThisFrame) Hero.UseSuper(Hero.transform.position + Hero.AimDirection * state.Stats.SuperRange);

            UpdateIndicators(state);
        }

        private void UpdateIndicators(PlayerState state)
        {
            bool showAim = state.IsAlive && (AttackStick != null && AttackStick.IsHeld || _mouseAim);
            _aimLine.enabled = showAim;
            if (showAim)
            {
                var from = Hero.transform.position + Vector3.up * 0.1f;
                _aimLine.SetPosition(0, from);
                _aimLine.SetPosition(1, from + Hero.AimDirection * state.Stats.AttackRange);
            }

            bool showSuper = state.IsAlive && SuperStick != null && SuperStick.IsHeld && state.SuperCharge >= 1f;
            _superCircle.enabled = showSuper;
            if (showSuper)
            {
                var center = Hero.transform.position + StickToWorld(SuperStick.RawDirection).normalized * state.Stats.SuperRange * Mathf.Clamp01(SuperStick.RawDirection.magnitude);
                for (int i = 0; i < 33; i++)
                {
                    float a = i / 32f * Mathf.PI * 2f;
                    _superCircle.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * state.Stats.SuperRadius);
                }
            }
        }

        /// <summary>A câmera tem yaw fixo (norte no topo da tela): joystick (x, y) = mundo (x, z).</summary>
        public static Vector3 StickToWorld(Vector2 v) => new Vector3(v.x, 0f, v.y);

        private Vector3 ApplyMagnet(Vector3 dir)
        {
            if (AimAssist != AimAssistMode.Magnet || !TryAutoTarget(out var target, coneDegrees: MagnetConeDegrees, around: dir)) return dir;
            var to = target - Hero.transform.position; to.y = 0f;
            float angle = Vector3.SignedAngle(dir, to, Vector3.up);
            return Quaternion.Euler(0f, Mathf.Clamp(angle, -MagnetMaxDegrees, MagnetMaxDegrees), 0f) * dir;
        }

        /// <summary>Inimigo vivo e visível mais próximo dentro do alcance (e opcionalmente dentro de um cone).</summary>
        private bool TryAutoTarget(out Vector3 target, float range = -1f, float coneDegrees = 360f, Vector3 around = default)
        {
            var me = Director.Match.GetPlayer(Hero.PlayerId);
            if (range < 0f) range = me.Stats.AttackRange;
            target = default;
            float best = float.MaxValue;
            foreach (var h in Director.Heroes)
            {
                if (h == Hero || !h.IsAlive) continue;
                var to = h.transform.position - Hero.transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d > range || d >= best) continue;
                if (coneDegrees < 360f && Vector3.Angle(around, to) > coneDegrees) continue;
                if (BotPilot.Blocked(Hero.transform.position, h.transform.position)) continue;
                best = d; target = h.transform.position;
            }
            return best < float.MaxValue;
        }

        private static bool MouseGroundPoint(out Vector3 point)
        {
            point = default;
            var cam = Camera.main;
            if (cam == null || Mouse.current == null) return false;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            var plane = new Plane(Vector3.up, Vector3.up);
            if (!plane.Raycast(ray, out float t)) return false;
            point = ray.GetPoint(t);
            return true;
        }

        private static LineRenderer MakeLine(string name, int points, Color c)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = points;
            lr.startWidth = lr.endWidth = 0.15f;
            lr.material = GameVisuals.Mat(c);
            lr.enabled = false;
            return lr;
        }
    }
}
