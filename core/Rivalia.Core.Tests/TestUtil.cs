using Rivalia.Core;

namespace Rivalia.Core.Tests
{
    internal static class TestUtil
    {
        public const float Dt = 1f / 30f;

        /// <summary>Partida padrão: 1 humano (id 0, região NO) + 3 bots (NE, SO, SE).</summary>
        public static Match NewMatch(MatchRules? rules = null, EconomyConfig? eco = null)
        {
            var m = new Match(MapDefinition.ValeDasQuatroCoroas(), rules, eco);
            m.AddPlayer("Jogador", false);
            m.AddPlayer("Bot A", true);
            m.AddPlayer("Bot B", true);
            m.AddPlayer("Bot C", true);
            m.Start();
            return m;
        }

        public static void Approx(double expected, double actual, int digits) =>
            Xunit.Assert.Equal(expected, actual, digits);

        public static void Run(Match m, float seconds)
        {
            int steps = (int)System.Math.Ceiling(seconds / Dt);
            for (int i = 0; i < steps && !m.IsOver; i++) m.Tick(Dt);
        }

        /// <summary>Põe todos os outros jogadores longe (no canto da arena) para não interferirem.</summary>
        public static void ParkOthers(Match m, params int[] keep)
        {
            foreach (var p in m.Players)
            {
                if (System.Array.IndexOf(keep, p.Id) >= 0) continue;
                m.SetPosition(p.Id, m.SpawnPoint(m.GetRegion(p.HomeRegionId)));
            }
        }

        /// <summary>Dispara e reporta o acerto, como a engine faria quando o projétil colide.</summary>
        public static int ShootAndHit(Match m, int attacker, int target)
        {
            var a = m.GetPlayer(attacker);
            var t = m.GetPlayer(target);
            var shot = m.TryBasicAttack(attacker, t.Position - a.Position);
            if (!shot.Ok) return -1;
            return m.ReportBasicHitOnPlayer(attacker, target);
        }

        /// <summary>Mata o alvo à força com acertos válidos, esperando recargas.</summary>
        public static void KillWithShots(Match m, int attacker, int target)
        {
            var t = m.GetPlayer(target);
            int guard = 0;
            while (t.IsAlive && guard++ < 2000)
            {
                ShootAndHit(m, attacker, target);
                m.Tick(Dt);
            }
        }
    }
}
