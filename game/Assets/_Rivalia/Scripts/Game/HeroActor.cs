using Rivalia.Core;
using UnityEngine;

namespace Rivalia.Game
{
    /// <summary>
    /// Personagem na arena (humano ou bot usam este mesmo componente). Movimento livre 360° no plano XZ com
    /// CharacterController; a mira gira só o tronco/arma, então dá para andar numa direção e atirar em outra.
    /// As regras (dano, munição, recarga) ficam no núcleo; aqui só há física e apresentação.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HeroActor : MonoBehaviour
    {
        public int PlayerId;
        public MatchDirector Director;

        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public Vector3 Velocity { get; private set; }
        public bool IsAlive => Director != null && Director.Match.GetPlayer(PlayerId).IsAlive;
        public Vector3 Muzzle => _aimPivot.position + AimDirection * 0.9f;

        private CharacterController _cc;
        private Transform _visual, _aimPivot, _hpFill, _hpRoot;
        private Renderer[] _renderers;
        private Color[] _baseColors;
        private Vector3 _moveInput;
        private float _verticalSpeed, _dashTime, _dashSpeed, _flash, _bob, _victoryTime = -1f;
        private Vector3 _dashDir;
        private bool _shownAlive = true;

        public static HeroActor Create(int playerId, Color teamColor, Vector3 position, MatchDirector director)
        {
            var go = new GameObject("Heroi " + playerId);
            go.transform.position = position;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f; cc.center = new Vector3(0, 1f, 0); cc.stepOffset = 0.3f;
            var h = go.AddComponent<HeroActor>();
            h.PlayerId = playerId;
            h.Director = director;
            h.BuildModel(teamColor);
            return h;
        }

        private void BuildModel(Color team)
        {
            // Proporções cartunescas: cabeça grande, corpo curto, silhueta fácil de ler de cima.
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            GameVisuals.Prim(PrimitiveType.Capsule, "Corpo", _visual, new Vector3(0, 0.65f, 0), new Vector3(0.9f, 0.6f, 0.9f), team, keepCollider: false);
            GameVisuals.Prim(PrimitiveType.Sphere, "Cabeca", _visual, new Vector3(0, 1.55f, 0), new Vector3(1.05f, 1.0f, 1.05f), new Color(1f, 0.85f, 0.7f), keepCollider: false);
            GameVisuals.Prim(PrimitiveType.Cube, "Bone", _visual, new Vector3(0, 2.0f, 0.1f), new Vector3(0.9f, 0.18f, 1.1f), team * 0.8f, keepCollider: false);
            GameVisuals.Prim(PrimitiveType.Sphere, "OlhoE", _visual, new Vector3(-0.2f, 1.6f, 0.45f), new Vector3(0.16f, 0.22f, 0.1f), Color.black, keepCollider: false);
            GameVisuals.Prim(PrimitiveType.Sphere, "OlhoD", _visual, new Vector3(0.2f, 1.6f, 0.45f), new Vector3(0.16f, 0.22f, 0.1f), Color.black, keepCollider: false);

            _aimPivot = new GameObject("Mira").transform;
            _aimPivot.SetParent(transform, false);
            _aimPivot.localPosition = new Vector3(0, 1.0f, 0);
            GameVisuals.Prim(PrimitiveType.Cube, "Estilingue", _aimPivot, new Vector3(0.35f, 0, 0.55f), new Vector3(0.18f, 0.18f, 0.8f), new Color(0.35f, 0.25f, 0.15f), keepCollider: false);
            GameVisuals.Prim(PrimitiveType.Sphere, "Faisca", _aimPivot, new Vector3(0.35f, 0.1f, 0.95f), new Vector3(0.25f, 0.25f, 0.25f), new Color(1f, 0.95f, 0.3f), keepCollider: false);

            // Barra de vida sobre a cabeça, na cor do time
            _hpRoot = new GameObject("BarraVida").transform;
            _hpRoot.SetParent(transform, false);
            _hpRoot.localPosition = new Vector3(0, 2.8f, 0);
            GameVisuals.Prim(PrimitiveType.Cube, "Fundo", _hpRoot, Vector3.zero, new Vector3(1.6f, 0.2f, 0.05f), new Color(0.1f, 0.1f, 0.1f), keepCollider: false);
            _hpFill = GameVisuals.Prim(PrimitiveType.Cube, "Vida", _hpRoot, new Vector3(0, 0, -0.03f), new Vector3(1.55f, 0.16f, 0.05f), team, keepCollider: false).transform;

            _renderers = _visual.GetComponentsInChildren<Renderer>();
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].material.color;
            _cc = GetComponent<CharacterController>();
        }

        /// <summary>Entrada de movimento já normalizada (magnitude 0..1 = fração da velocidade máxima).</summary>
        public void SetMove(Vector2 input)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            _moveInput = new Vector3(input.x, 0f, input.y);
        }

        public void SetAim(Vector3 worldDir)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude > 1e-4f) AimDirection = worldDir.normalized;
        }

        public bool Fire(Vector3 worldDir)
        {
            SetAim(worldDir);
            return Director.RequestBasicAttack(this, AimDirection);
        }

        public bool UseAbility(Vector3 worldDir)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-4f) worldDir = _moveInput.sqrMagnitude > 0.01f ? _moveInput : AimDirection;
            if (Director.Match.TryUseAbility(PlayerId, GameVisuals.ToV2(worldDir), out float dist) != CommandResult.Ok) return false;
            _dashDir = worldDir.normalized;
            _dashTime = 0.15f;
            _dashSpeed = dist / _dashTime;
            Sfx.Play(Sfx.Dash, transform.position);
            return true;
        }

        public bool UseSuper(Vector3 worldPoint) => Director.RequestSuper(this, worldPoint);

        public void Teleport(Vector3 pos)
        {
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
            _verticalSpeed = 0f;
        }

        public void Flash() => _flash = 0.08f;
        public void PlayVictory() => _victoryTime = 0f;

        private void Update()
        {
            if (Director == null || Director.Match == null) return;
            var state = Director.Match.GetPlayer(PlayerId);
            float dt = Time.deltaTime;

            if (state.IsAlive != _shownAlive)
            {
                _shownAlive = state.IsAlive;
                _visual.gameObject.SetActive(_shownAlive);
                _aimPivot.gameObject.SetActive(_shownAlive);
                _hpRoot.gameObject.SetActive(_shownAlive);
                _cc.enabled = _shownAlive;
            }

            if (_victoryTime >= 0f)
            {
                _victoryTime += dt;
                _visual.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_victoryTime * 6f)) * 0.8f, 0);
                _visual.Rotate(0, 240f * dt, 0);
                return;
            }
            if (!state.IsAlive || Director.Match.IsOver) { Velocity = Vector3.zero; return; }

            // Movimento independente da mira
            float speed = Director.Match.MoveSpeedOf(state);
            Vector3 v = _moveInput * speed;
            if (_dashTime > 0f)
            {
                _dashTime -= dt;
                v = _dashDir * _dashSpeed;
            }
            _verticalSpeed = _cc.isGrounded ? -1f : _verticalSpeed - 20f * dt;
            _cc.Move(new Vector3(v.x, _verticalSpeed, v.z) * dt);
            Velocity = new Vector3(v.x, 0f, v.z);

            // Pernas/corpo seguem o movimento; tronco/arma seguem a mira
            if (_moveInput.sqrMagnitude > 0.01f)
                _visual.rotation = Quaternion.Slerp(_visual.rotation, Quaternion.LookRotation(_moveInput), 1f - Mathf.Exp(-14f * dt));
            _aimPivot.rotation = Quaternion.LookRotation(AimDirection);

            // Animação procedural provisória (troca por Animator com camadas quando houver modelo final)
            _bob += dt * (_moveInput.magnitude * 14f);
            _visual.localPosition = new Vector3(0, _moveInput.sqrMagnitude > 0.01f ? Mathf.Abs(Mathf.Sin(_bob)) * 0.15f : 0f, 0);

            _flash -= dt;
            for (int i = 0; i < _renderers.Length; i++)
                _renderers[i].material.color = _flash > 0f ? Color.white : _baseColors[i];

            float frac = state.Health / (float)Director.Match.MaxHealthOf(state);
            _hpFill.localScale = new Vector3(1.55f * frac, 0.16f, 0.05f);
            _hpFill.localPosition = new Vector3(-0.775f * (1f - frac), 0, -0.03f);
            if (Camera.main != null) _hpRoot.rotation = Camera.main.transform.rotation;
        }
    }
}
