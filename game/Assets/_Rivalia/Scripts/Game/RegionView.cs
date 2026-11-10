using Rivalia.Core;
using UnityEngine;

namespace Rivalia.Game
{
    /// <summary>Apresentação de uma região: cor do dono, progresso de captura, estado da mina e da torreta.</summary>
    public class RegionView : MonoBehaviour
    {
        public int RegionId;
        public float CaptureRadius;
        public Renderer Floor, Flag, Crown, Ring;
        public Transform ProgressDisc, TurretHead, MineCrystal;
        public GameObject BaseBody, MineBody;

        private int _shownOwner = int.MinValue;
        private Renderer _progressRenderer;
        private float _ringPulse;

        public void Refresh(Match match, float dt)
        {
            var r = match.GetRegion(RegionId);
            if (r.OwnerId != _shownOwner)
            {
                _shownOwner = r.OwnerId;
                var c = GameVisuals.Team(r.OwnerId);
                GameVisuals.Tint(gameObject, c);
                Floor.material.color = Color.Lerp(c, Color.white, 0.55f);
            }

            // Disco de progresso cresce do centro até a borda da área de captura, na cor de quem captura.
            if (_progressRenderer == null) _progressRenderer = ProgressDisc.GetComponent<Renderer>();
            float t = Mathf.Clamp01(r.CaptureProgress / 100f);
            float d = Mathf.Max(0.01f, CaptureRadius * 2f * t);
            ProgressDisc.localScale = new Vector3(d, 0.01f, d);
            if (r.CapturingPlayerId != Match.Neutral) _progressRenderer.material.color = GameVisuals.Team(r.CapturingPlayerId);

            // Anel pisca em vermelho quando contestado
            _ringPulse += dt * 6f;
            Ring.material.color = r.IsContested ? Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), (Mathf.Sin(_ringPulse) + 1f) / 2f) : Color.white;

            if (MineCrystal != null)
            {
                bool sabotaged = r.Mine.IsSabotaged;
                MineCrystal.gameObject.SetActive(!sabotaged);
                MineCrystal.Rotate(0f, 60f * dt, 0f, Space.World);
                float fill = r.Mine.Stored / Mathf.Max(1f, match.MineCapacityOf(r));
                MineCrystal.localScale = new Vector3(0.25f + 0.2f * fill, 0.4f + 0.6f * fill, 0.25f + 0.2f * fill);
            }
            if (TurretHead != null)
                TurretHead.gameObject.SetActive(!r.Base.IsDisabled);
        }

        public void AimTurretAt(Vector3 worldPos)
        {
            if (TurretHead == null) return;
            var dir = worldPos - TurretHead.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) TurretHead.rotation = Quaternion.LookRotation(dir);
        }
    }
}
