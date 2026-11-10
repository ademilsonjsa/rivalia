using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rivalia.Core;
using Rivalia.Core.AI;
using Xunit;
using Xunit.Abstractions;

namespace Rivalia.Core.Tests
{
    public class FullMatchSimulationTests
    {
        private readonly ITestOutputHelper _out;
        public FullMatchSimulationTests(ITestOutputHelper output) { _out = output; }

        private static readonly BotProfile[] AllProfiles =
            { BotProfile.Economic, BotProfile.Defensive, BotProfile.Aggressive, BotProfile.Expansionist, BotProfile.Balanced };

        [Fact]
        public void Single_player_match_against_three_bots_always_ends_with_a_valid_result()
        {
            var sb = new StringBuilder();
            var reasons = new Dictionary<MatchEndReason, int>();
            for (int seed = 1; seed <= 60; seed++)
            {
                var profiles = new[] { BotProfile.Balanced, AllProfiles[seed % 5], AllProfiles[(seed + 1) % 5], AllProfiles[(seed + 3) % 5] };
                var sim = new HeadlessSimulator(seed, profiles);
                var result = sim.Run();

                Assert.NotNull(result);                                          // a partida terminou
                Assert.True(result.EndTime <= sim.Match.Rules.TimeLimitSeconds + 0.1f);
                Assert.Equal(4, result.Placement.Distinct().Count());            // colocação completa
                if (result.Reason == MatchEndReason.Domination)
                {
                    Assert.Equal(5, sim.Match.TowerCount(result.WinnerId));
                    Assert.True(sim.Match.OwnsCenter(result.WinnerId));
                }
                if (result.Reason == MatchEndReason.LastStanding)
                    Assert.Equal(1, sim.Match.Players.Count(p => !p.IsEliminated));
                if (result.Reason == MatchEndReason.NoHumanCanWin)
                    Assert.True(sim.Match.GetPlayer(0).IsEliminated);

                // Invariantes econômicas e de estado
                foreach (var p in sim.Match.Players)
                {
                    Assert.True(p.Crystals >= 0);
                    Assert.True(p.Health >= 0 && p.Health <= sim.Match.MaxHealthOf(p));
                    if (p.IsEliminated) Assert.Equal(0, sim.Match.TowerCount(p.Id));
                }

                reasons[result.Reason] = reasons.TryGetValue(result.Reason, out var n) ? n + 1 : 1;
                var w = result.IsDraw ? "empate" : sim.Match.GetPlayer(result.WinnerId).Name;
                sb.AppendLine($"seed {seed,2}: {result.Reason,-14} em {result.EndTime,5:0}s  vencedor: {w}  " +
                              $"capturas={sim.Match.Players.Sum(p => p.TowersCaptured)} mortes={sim.Match.Players.Sum(p => p.Deaths)}");
            }
            sb.AppendLine();
            sb.AppendLine("Motivos de fim: " + string.Join(", ", reasons.Select(kv => $"{kv.Key}={kv.Value}")));
            Emit("simulacao-single-player.txt", sb.ToString());
        }

        [Fact]
        public void Bots_move_fight_capture_and_spend_resources()
        {
            var sim = new HeadlessSimulator(7, new[] { BotProfile.Balanced, BotProfile.Aggressive, BotProfile.Expansionist, BotProfile.Economic });
            sim.Run();
            var m = sim.Match;
            for (int i = 1; i < 4; i++)
            {
                var bot = m.GetPlayer(i);
                Assert.True(bot.DamageDealt > 0, $"{bot.Name} não causou dano");
            }
            Assert.True(m.Players.Sum(p => p.TowersCaptured) > 0, "nenhuma torre foi capturada");
            Assert.True(m.Players.Sum(p => p.Deaths) > 0, "ninguém morreu");
            Assert.True(m.Players.Sum(p => p.CrystalsMined) > 0, "ninguém coletou da mina");
            bool spent = m.Players.Any(p => p.Items.Count > 0) ||
                         m.Regions.Any(r => !r.IsCenter && (r.Mine.Level > 1 || r.Base.Level > 1 || r.TowerLevel > 1));
            Assert.True(spent, "ninguém comprou nem melhorou nada");
            _out.WriteLine(string.Join("\n", sim.Log));
        }

        [Fact]
        public void Profile_balance_report_four_bots()
        {
            // Medição (não critério de aceite do MVP): vitórias por perfil em partidas só de bots.
            var wins = AllProfiles.ToDictionary(p => p, p => 0);
            var games = AllProfiles.ToDictionary(p => p, p => 0);
            int draws = 0, total = 200;
            for (int seed = 1; seed <= total; seed++)
            {
                var profiles = Enumerable.Range(0, 4).Select(k => AllProfiles[(seed + k * 2) % 5]).ToArray();
                var sim = new HeadlessSimulator(1000 + seed, profiles, firstIsHuman: false);
                var r = sim.Run();
                Assert.NotNull(r);
                foreach (var pr in profiles.Distinct()) games[pr]++;
                if (r.IsDraw) draws++; else wins[profiles[r.WinnerId]]++;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"{total} partidas só de bots (dificuldade Normal), empates: {draws}");
            foreach (var pr in AllProfiles)
                sb.AppendLine($"{pr,-13} venceu {wins[pr],3} de {games[pr],3} partidas em que jogou ({100.0 * wins[pr] / Math.Max(1, games[pr]),5:0.0}%)");
            Emit("balanceamento-perfis.txt", sb.ToString());
        }

        private void Emit(string file, string text)
        {
            _out.WriteLine(text);
            var dir = Environment.GetEnvironmentVariable("RIVALIA_EVIDENCE_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, file), text);
            }
        }
    }
}
