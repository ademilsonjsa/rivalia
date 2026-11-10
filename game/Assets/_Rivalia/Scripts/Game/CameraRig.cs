using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Rivalia.Game
{
    /// <summary>
    /// Câmera top-down elevada com Cinemachine 3: perspectiva, inclinação fixa (ajustável), yaw fixo
    /// (norte sempre no topo), seguimento suave com leve avanço na direção da mira, zoom configurável,
    /// limites da arena e esmaecimento de muros que escondem o herói.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Range(55f, 70f)] public float Pitch = 60f;
        [Range(16f, 28f)] public float Distance = 22f;
        public float FieldOfView = 40f;
        public float Damping = 0.15f;
        public float AimLead = 1.5f;
        public float ArenaHalfSize = 40f;
        public float BoundsMargin = 8f;

        public HeroActor Target;

        private Transform _followPoint;
        private CinemachineCamera _vcam;
        private CinemachineFollow _follow;
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static CameraRig Create(HeroActor target, float arenaHalfSize)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            if (cam.GetComponent<CinemachineBrain>() == null) cam.gameObject.AddComponent<CinemachineBrain>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.75f, 0.95f);

            var go = new GameObject("CameraRig");
            var rig = go.AddComponent<CameraRig>();
            rig.Target = target;
            rig.ArenaHalfSize = arenaHalfSize;
            rig.Setup();
            return rig;
        }

        private void Setup()
        {
            _followPoint = new GameObject("AlvoDaCamera").transform;
            _followPoint.position = Target.transform.position;

            var camGo = new GameObject("CM Top-Down");
            camGo.transform.SetParent(transform, false);
            _vcam = camGo.AddComponent<CinemachineCamera>();
            _vcam.Follow = _followPoint;
            _follow = camGo.AddComponent<CinemachineFollow>();
            // copiar-modificar-atribuir: funciona seja o membro um campo ou uma propriedade de struct
            var ts = _follow.TrackerSettings;
            ts.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            ts.PositionDamping = new Vector3(Damping, Damping, Damping);
            _follow.TrackerSettings = ts;
            Apply();
        }

        /// <summary>Aplica inclinação e zoom (chamado também pela tela de configurações).</summary>
        public void Apply()
        {
            float rad = Pitch * Mathf.Deg2Rad;
            _follow.FollowOffset = new Vector3(0f, Mathf.Sin(rad) * Distance, -Mathf.Cos(rad) * Distance);
            _vcam.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f); // sem componente de Aim: rotação fixa
            var lens = _vcam.Lens;
            lens.FieldOfView = FieldOfView;
            _vcam.Lens = lens;
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            // Zoom com a roda do mouse (no celular, pela tela de configurações)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) { Distance = Mathf.Clamp(Distance - Mathf.Sign(scroll), 16f, 28f); Apply(); }
            }

            // Ponto seguido = herói + avanço na direção da mira, preso aos limites da arena
            var p = Target.transform.position + Target.AimDirection * AimLead;
            float lim = ArenaHalfSize - BoundsMargin;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            p.z = Mathf.Clamp(p.z, -lim - 6f, lim - 10f); // a câmera inclinada vê mais ao norte
            p.y = 0f;
            _followPoint.position = p;

            FadeOccluders();
        }

        /// <summary>Muros e construções entre a câmera e o herói passam a só projetar sombra (ficam invisíveis).</summary>
        private void FadeOccluders()
        {
            foreach (var r in _hidden) if (r != null) r.shadowCastingMode = ShadowCastingMode.On;
            _hidden.Clear();
            var cam = Camera.main;
            if (cam == null) return;
            var heroPos = Target.transform.position + Vector3.up;
            var dir = heroPos - cam.transform.position;
            int n = Physics.SphereCastNonAlloc(cam.transform.position, 0.6f, dir.normalized, Hits, dir.magnitude - 1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                if (col.GetComponentInParent<HeroActor>() != null) continue;
                foreach (var r in col.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    _hidden.Add(r);
                }
            }
        }
    }
}
