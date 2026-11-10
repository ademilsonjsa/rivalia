using System;
using System.Collections.Generic;
using Rivalia.Core;
using UnityEngine;

namespace Rivalia.Game
{
    /// <summary>
    /// Ponte entre o Unity e o núcleo de regras. Roda a simulação a 30 Hz fixos (mesmo tick do servidor
    /// dedicado da Fase 4), envia posições da física para o núcleo e aplica de volta os eventos
    /// (morte, renascimento, captura, tiros da torreta, fim de partida).
    /// </summary>
    public class MatchDirector : MonoBehaviour
    {
        public const float TickSeconds = 1f / 30f;

        public Match Match { get; private set; }
        public readonly List<HeroActor> Heroes = new List<HeroActor>();
        public readonly List<RegionView> RegionViews = new List<RegionView>();
        public int LocalPlayerId = 0;

        public event Action<MatchEvent> MatchEventRaised;

        private float _accumulator;
        private bool _endHandled;

        public void Init(Match match, List<RegionView> regionViews)
        {
            Match = match;
            RegionViews.AddRange(regionViews);
            Sfx.Init();
        }

        public HeroActor HeroOf(int playerId) => Heroes.Find(h => h.PlayerId == playerId);

        private void Update()
        {
            if (Match == null) return;
            _accumulator += Time.deltaTime;
            int guard = 0;
            while (_accumulator >= TickSeconds && guard++ < 8)
            {
                _accumulator -= TickSeconds;
                foreach (var h in Heroes) Match.SetPosition(h.PlayerId, GameVisuals.ToV2(h.transform.position));
                Match.Tick(TickSeconds);
                foreach (var e in Match.DrainEvents()) Handle(e);
            }
            foreach (var v in RegionViews) v.Refresh(Match, Time.deltaTime);
        }

        // ------------------------------------------------------------------ pedidos de ação (humano e bots)

        public bool RequestBasicAttack(HeroActor hero, Vector3 dir)
        {
            var res = Match.TryBasicAttack(hero.PlayerId, GameVisuals.ToV2(dir));
            if (!res.Ok) return false;
            Projectile.Spawn(this, hero, hero.Muzzle, dir, res.Speed, res.Range, res.Radius, Color.Lerp(GameVisuals.Team(hero.PlayerId), new Color(1f, 0.95f, 0.4f), 0.5f));
            Sfx.Play(Sfx.Shoot, hero.transform.position);
            return true;
        }

        public bool RequestSuper(HeroActor hero, Vector3 point)
        {
            if (Match.TryUseSuper(hero.PlayerId, GameVisuals.ToV2(point)) != CommandResult.Ok) return false;
            Vfx.Burst(new Vector3(point.x, 0.1f, point.z), Match.GetPlayer(hero.PlayerId).Stats.SuperRadius, new Color(1f, 0.85f, 0.2f), 3f);
            return true;
        }

        public void OnProjectileHitHero(HeroActor attacker, HeroActor target, Vector3 point)
        {
            Match.ReportBasicHitOnPlayer(attacker.PlayerId, target.PlayerId);
            Vfx.Spark(point, GameVisuals.Team(attacker.PlayerId));
        }

        public void OnProjectileHitStructure(HeroActor attacker, StructureView s, Vector3 point)
        {
            int dmg = Match.ReportBasicHitOnStructure(attacker.PlayerId, s.RegionId, s.Kind);
            Vfx.Spark(point, Color.white);
            if (dmg > 0) Vfx.Number(point + Vector3.up, dmg, Color.white);
        }

        public CommandResult RequestUpgrade(int playerId, int regionId, StructureKind kind)
        {
            var r = Match.TryUpgrade(playerId, regionId, kind);
            return r;
        }

        public CommandResult RequestBuy(int playerId, int regionId, ShopItemId item) => Match.TryBuy(playerId, regionId, item);

        // ------------------------------------------------------------------ eventos do núcleo

        private void Handle(MatchEvent e)
        {
            var hero = HeroOf(e.PlayerId);
            switch (e.Type)
            {
                case MatchEventType.Damaged:
                    if (hero != null)
                    {
                        hero.Flash();
                        Vfx.Number(hero.transform.position + Vector3.up * 3.2f, e.Amount, e.PlayerId == LocalPlayerId ? new Color(1f, 0.35f, 0.35f) : Color.white);
                        Sfx.Play(Sfx.Hit, hero.transform.position);
                    }
                    break;
                case MatchEventType.Killed:
                    if (hero != null) { Vfx.Burst(hero.transform.position, 1.2f, GameVisuals.Team(e.PlayerId), 0.6f); Sfx.Play(Sfx.Death, hero.transform.position); }
                    break;
                case MatchEventType.Respawned:
                    if (hero != null) hero.Teleport(GameVisuals.ToV3(e.Position));
                    break;
                case MatchEventType.TowerCaptured:
                    Sfx.Play(Sfx.Capture, GameVisuals.ToV3(e.Position));
                    Vfx.Burst(GameVisuals.ToV3(e.Position, 0.1f), 5f, GameVisuals.Team(e.PlayerId), 0.8f);
                    break;
                case MatchEventType.BaseFired:
                {
                    var rv = RegionViews.Find(v => v.RegionId == e.RegionId);
                    var targetPos = GameVisuals.ToV3(e.Position, 1f);
                    if (rv != null && rv.TurretHead != null)
                    {
                        rv.AimTurretAt(targetPos);
                        Vfx.Tracer(rv.TurretHead.position, targetPos, GameVisuals.Team(e.OtherId));
                    }
                    break;
                }
                case MatchEventType.MineCollected:
                    if (e.PlayerId == LocalPlayerId && hero != null) { Sfx.Play(Sfx.Coin, hero.transform.position); Vfx.Number(hero.transform.position + Vector3.up * 3.6f, e.Amount, new Color(0.4f, 0.95f, 1f), "+"); }
                    break;
                case MatchEventType.MatchEnded:
                    if (!_endHandled)
                    {
                        _endHandled = true;
                        var winner = HeroOf(Match.Result.WinnerId);
                        if (winner != null && Match.GetPlayer(winner.PlayerId).IsAlive) winner.PlayVictory();
                    }
                    break;
            }
            MatchEventRaised?.Invoke(e);
        }
    }

    /// <summary>Efeitos visuais simples e baratos (sem partículas de terceiros).</summary>
    public static class Vfx
    {
        private static Font _font;

        public static void Spark(Vector3 pos, Color c)
        {
            var go = GameVisuals.Prim(PrimitiveType.Sphere, "Faisca", null, pos, Vector3.one * 0.6f, c, keepCollider: false);
            go.AddComponent<FadeAndDie>().Setup(0.15f, 1.8f);
        }

        public static void Burst(Vector3 pos, float radius, Color c, float life)
        {
            var go = GameVisuals.Prim(PrimitiveType.Cylinder, "Explosao", null, pos, new Vector3(radius * 2f, 0.05f, radius * 2f), c, keepCollider: false);
            go.AddComponent<FadeAndDie>().Setup(life, 1.0f);
        }

        public static void Tracer(Vector3 from, Vector3 to, Color c)
        {
            var go = new GameObject("Rastro");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to);
            lr.startWidth = 0.25f; lr.endWidth = 0.1f;
            lr.material = GameVisuals.Mat(c);
            UnityEngine.Object.Destroy(go, 0.12f);
        }

        public static void Number(Vector3 pos, int value, Color c, string prefix = "")
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Numero");
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = prefix + value;
            tm.font = _font;
            tm.fontSize = 64; tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = c;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation;
            go.AddComponent<FloatUp>();
        }
    }

    public class FadeAndDie : MonoBehaviour
    {
        private float _life, _t, _grow;
        private Vector3 _scale0;
        public void Setup(float life, float grow) { _life = life; _grow = grow; _scale0 = transform.localScale; }
        private void Update()
        {
            _t += Time.deltaTime;
            float k = _t / _life;
            transform.localScale = new Vector3(_scale0.x, _scale0.y, _scale0.z) * Mathf.Lerp(1f, _grow, k);
            if (k >= 1f) Destroy(gameObject);
        }
    }

    public class FloatUp : MonoBehaviour
    {
        private float _t;
        private void Update()
        {
            _t += Time.deltaTime;
            transform.position += Vector3.up * 2f * Time.deltaTime;
            if (_t > 0.7f) Destroy(gameObject);
        }
    }
}
