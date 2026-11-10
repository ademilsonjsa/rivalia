using System;
using System.Collections.Generic;
using Rivalia.Core;
using Rivalia.Core.AI;

namespace Rivalia.Core.Tests
{
    /// <summary>
    /// Simulação sem gráficos de uma partida inteira: substitui a física do Unity por movimento em linha reta
    /// e acertos probabilísticos, mas usa as MESMAS regras (Match) e a MESMA IA estratégica (BotStrategist)
    /// do jogo. Serve para provar que uma partida chega ao fim corretamente e para medir balanceamento.
    /// </summary>
    public class HeadlessSimulator
    {
        public readonly Match Match;
        private readonly BotStrategist[] _brains;
        private readonly BotGoal[] _goals;
        private readonly float[] _thinkTimer;
        private readonly Random _rng;
        private readonly float _hitChance;
        public readonly List<string> Log = new List<string>();

        public HeadlessSimulator(int seed, BotProfile[] profiles, BotDifficulty difficulty = BotDifficulty.Normal, bool firstIsHuman = true)
        {
            _rng = new Random(seed);
            Match = new Match(MapDefinition.ValeDasQuatroCoroas());
            _brains = new BotStrategist[profiles.Length];
            _goals = new BotGoal[profiles.Length];
            _thinkTimer = new float[profiles.Length];
            for (int i = 0; i < profiles.Length; i++)
            {
                bool human = firstIsHuman && i == 0;
                Match.AddPlayer(human ? "Jogador (roteirizado)" : $"Bot {profiles[i]}", !human);
                _brains[i] = new BotStrategist(profiles[i], difficulty);
                _thinkTimer[i] = (float)_rng.NextDouble(); // decisões dessincronizadas
            }
            _hitChance = difficulty == BotDifficulty.Hard ? 0.75f : difficulty == BotDifficulty.Easy ? 0.4f : 0.6f;
            Match.Start();
        }

        public MatchResult Run(float dt = 1f / 30f, float hardStopSeconds = 1000f)
        {
            while (!Match.IsOver && Match.Time < hardStopSeconds)
            {
                foreach (var p in Match.Players) if (p.IsAlive) Drive(p, dt);
                Match.Tick(dt);
                foreach (var e in Match.DrainEvents())
                    if (e.Type == MatchEventType.TowerCaptured || e.Type == MatchEventType.Eliminated || e.Type == MatchEventType.MatchEnded)
                        Log.Add(e.ToString());
            }
            return Match.Result;
        }

        private void Drive(PlayerState p, float dt)
        {
            var m = Match;
            int i = p.Id;

            _thinkTimer[i] -= dt;
            if (_thinkTimer[i] <= 0f)
            {
                _thinkTimer[i] = 1f;
                _goals[i] = _brains[i].Decide(m, p);
            }
            var goal = _goals[i];

            // --- movimento (substitui CharacterController + NavMesh)
            Vec2 dest = goal.Destination;
            float stopAt = 0.3f;
            if (goal.Kind == BotGoalKind.HuntPlayer)
            {
                var t = m.GetPlayer(goal.TargetPlayerId);
                if (t != null && t.IsAlive) { dest = t.Position; stopAt = p.Stats.AttackRange * 0.7f; }
            }
            if (Vec2.Distance(p.Position, dest) > stopAt)
                m.SetPosition(i, Vec2.MoveTowards(p.Position, dest, m.MoveSpeedOf(p) * dt));

            // --- interação com estruturas
            if (goal.Kind == BotGoalKind.Upgrade && Vec2.Distance(p.Position, goal.Destination) < 3.5f)
            {
                m.TryUpgrade(i, goal.RegionId, goal.Structure);
                _thinkTimer[i] = 0f;
            }
            else if (goal.Kind == BotGoalKind.Shop && Vec2.Distance(p.Position, goal.Destination) < 3.5f)
            {
                m.TryBuy(i, goal.RegionId, goal.Item);
                _thinkTimer[i] = 0f;
            }

            float hpFrac = p.Health / (float)m.MaxHealthOf(p);
            if (hpFrac < 0.4f) m.TryUsePotion(i);

            // --- combate (substitui projéteis com física)
            PlayerState enemy = null;
            float best = float.MaxValue;
            foreach (var o in m.Players)
            {
                if (o == p || !o.IsAlive || o.SpawnProtection > 0f) continue;
                float d = Vec2.Distance(o.Position, p.Position);
                if (d < best) { best = d; enemy = o; }
            }

            if (enemy != null && p.SuperCharge >= 1f && best <= p.Stats.SuperRange)
                m.TryUseSuper(i, enemy.Position);

            if (enemy != null && best <= p.Stats.AttackRange)
            {
                var shot = m.TryBasicAttack(i, enemy.Position - p.Position);
                if (shot.Ok && _rng.NextDouble() < _hitChance * (1f - 0.4f * best / p.Stats.AttackRange))
                    m.ReportBasicHitOnPlayer(i, enemy.Id);
                return;
            }

            // Sem herói por perto: derruba a Base Militar inimiga que protege o alvo
            foreach (var r in m.Regions)
            {
                if (r.IsCenter || r.OwnerId == i || r.Base.IsDisabled) continue;
                if (Vec2.Distance(p.Position, r.Def.BasePosition) > p.Stats.AttackRange) continue;
                if (m.TryBasicAttack(i, r.Def.BasePosition - p.Position).Ok && _rng.NextDouble() < 0.85)
                    m.ReportBasicHitOnStructure(i, r.Id, StructureKind.MilitaryBase);
                break;
            }
        }
    }
}
