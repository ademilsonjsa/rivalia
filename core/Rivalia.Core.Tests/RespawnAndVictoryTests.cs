using System.Linq;
using Rivalia.Core;
using Xunit;

namespace Rivalia.Core.Tests
{
    public class RespawnAndVictoryTests
    {
        private static void Kill(Match m, int victim)
        {
            // Atacante 3 (SE) vai até a vítima e a mata com acertos válidos.
            var v = m.GetPlayer(victim);
            m.SetPosition(3, v.Position + new Vec2(3, 0));
            TestUtil.KillWithShots(m, 3, victim);
        }

        [Fact]
        public void Dead_player_respawns_at_own_tower_with_growing_timer()
        {
            var m = TestUtil.NewMatch();
            TestUtil.Run(m, 2f);
            m.SetPosition(0, new Vec2(-5, 15));
            Kill(m, 0);
            var p = m.GetPlayer(0);
            Assert.Equal(LifeState.Dead, p.Life);
            TestUtil.Approx(3f, p.RespawnTimer, 1);

            TestUtil.Run(m, 3.1f);
            Assert.True(p.IsAlive);
            Assert.Equal(m.SpawnPoint(m.GetRegion(0)), p.Position);
            Assert.Equal(3200, p.Health);
            Assert.True(p.SpawnProtection > 0f);

            TestUtil.Run(m, 2f);
            m.SetPosition(0, new Vec2(-5, 15));
            Kill(m, 0);
            TestUtil.Approx(4f, p.RespawnTimer, 1);                 // 3 s + 1 s por morte anterior
        }

        [Fact]
        public void Player_without_home_tower_respawns_at_conquered_tower()
        {
            var m = TestUtil.NewMatch();
            var home = m.GetRegion(0);
            var ne = m.GetRegion(1);
            home.OwnerId = 2;   // perdeu a casa
            ne.OwnerId = 0;     // mas conquistou NE
            m.SetPosition(1, new Vec2(10, -10)); // antigo dono de NE longe da torre
            TestUtil.Run(m, 2f);
            m.SetPosition(0, new Vec2(-5, 15));
            Kill(m, 0);
            TestUtil.Run(m, 3.1f);
            var p = m.GetPlayer(0);
            Assert.True(p.IsAlive);
            Assert.Equal(m.SpawnPoint(ne), p.Position);
        }

        [Fact]
        public void Player_without_any_tower_is_eliminated_on_death()
        {
            var m = TestUtil.NewMatch();
            m.GetRegion(1).OwnerId = 2;   // bot A perdeu a única torre, mas segue vivo
            TestUtil.Run(m, 2f);
            Assert.True(m.GetPlayer(1).IsAlive);
            m.SetPosition(1, new Vec2(5, 15));
            Kill(m, 1);
            Assert.Equal(LifeState.Eliminated, m.GetPlayer(1).Life);
            Assert.Contains(m.Events, e => e.Type == MatchEventType.Eliminated && e.PlayerId == 1);
        }

        [Fact]
        public void Tower_lost_while_waiting_to_respawn_means_elimination()
        {
            var m = TestUtil.NewMatch();
            TestUtil.Run(m, 2f);
            m.SetPosition(1, new Vec2(5, 15));
            Kill(m, 1);
            Assert.Equal(LifeState.Dead, m.GetPlayer(1).Life);
            m.GetRegion(1).OwnerId = 3;   // a torre cai enquanto ele espera
            TestUtil.Run(m, 3.5f);
            Assert.Equal(LifeState.Eliminated, m.GetPlayer(1).Life);
        }

        [Fact]
        public void Owning_all_five_towers_wins_by_domination()
        {
            var m = TestUtil.NewMatch();
            for (int i = 1; i < 4; i++) m.GetRegion(i).OwnerId = 0;
            m.SetPosition(0, Vec2.Zero);
            foreach (var id in new[] { 1, 2, 3 }) m.SetPosition(id, new Vec2(-30 + id, -38));
            TestUtil.Run(m, 13f);
            Assert.True(m.IsOver);
            Assert.Equal(MatchEndReason.Domination, m.Result.Reason);
            Assert.Equal(0, m.Result.WinnerId);
            Assert.Equal(0, m.Result.Placement[0]);
            Assert.Equal(4, m.Result.Placement.Count);

            // Depois do fim, nada muda
            Assert.Equal(CommandResult.MatchOver, m.TryBasicAttack(0, new Vec2(1, 0)).Result);
        }

        [Fact]
        public void Four_towers_without_center_is_not_victory()
        {
            var m = TestUtil.NewMatch();
            for (int i = 1; i < 4; i++) m.GetRegion(i).OwnerId = 0;
            TestUtil.Run(m, 1f);
            Assert.False(m.IsOver);
        }

        [Fact]
        public void Last_player_standing_wins()
        {
            var m = TestUtil.NewMatch(new MatchRules { EndWhenAllHumansEliminated = false });
            foreach (var id in new[] { 1, 2 }) m.GetRegion(id).OwnerId = 3;
            m.GetRegion(0).OwnerId = 3;
            TestUtil.Run(m, 2f);
            foreach (var id in new[] { 0, 1, 2 })
            {
                m.SetPosition(id, new Vec2(0, 15));
                Kill(m, id);
            }
            Assert.True(m.IsOver);
            Assert.Equal(MatchEndReason.LastStanding, m.Result.Reason);
            Assert.Equal(3, m.Result.WinnerId);
        }

        [Fact]
        public void Match_ends_when_no_human_can_win_anymore()
        {
            var m = TestUtil.NewMatch();
            m.GetRegion(0).OwnerId = 2;
            TestUtil.Run(m, 2f);
            m.SetPosition(0, new Vec2(0, 15));
            Kill(m, 0);
            Assert.True(m.IsOver);
            Assert.Equal(MatchEndReason.NoHumanCanWin, m.Result.Reason);
            Assert.Equal(2, m.Result.WinnerId);               // 2 torres: vence o desempate
            Assert.Equal(0, m.Result.Placement.Last());
        }

        [Fact]
        public void Time_limit_uses_tiebreak_towers_then_center_then_kills()
        {
            var m = TestUtil.NewMatch(new MatchRules { TimeLimitSeconds = 20f });
            m.GetRegion(2).OwnerId = 1;     // Bot A tem 2 torres
            m.GetRegion(4).OwnerId = 3;     // Bot C tem 1 + centro = 2 torres e o centro
            foreach (var p in m.Players) m.SetPosition(p.Id, new Vec2(-38 + p.Id * 2, 38 - p.Id));
            TestUtil.Run(m, 21f);
            Assert.True(m.IsOver);
            Assert.Equal(MatchEndReason.TimeLimit, m.Result.Reason);
            Assert.Equal(3, m.Result.WinnerId);
        }

        [Fact]
        public void Perfect_tie_at_time_limit_is_a_draw()
        {
            var m = TestUtil.NewMatch(new MatchRules { TimeLimitSeconds = 5f });
            foreach (var p in m.Players) m.SetPosition(p.Id, new Vec2(-38 + p.Id * 2, 38));
            TestUtil.Run(m, 6f);
            Assert.True(m.IsOver);
            Assert.True(m.Result.IsDraw);
        }
    }
}
