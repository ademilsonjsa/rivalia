using System.Linq;
using Rivalia.Core;
using Xunit;

namespace Rivalia.Core.Tests
{
    public class TerritoryTests
    {
        private static void DisableBase(Match m, int regionId) => m.GetRegion(regionId).Base.DisabledTimer = 9999f;

        [Fact]
        public void Each_player_starts_owning_one_region_and_center_is_neutral()
        {
            var m = TestUtil.NewMatch();
            for (int i = 0; i < 4; i++) Assert.Equal(i, m.GetRegion(i).OwnerId);
            Assert.Equal(Match.Neutral, m.CenterRegion.OwnerId);
        }

        [Fact]
        public void Player_captures_enemy_tower_progressively_and_ownership_changes()
        {
            var m = TestUtil.NewMatch();
            var ne = m.GetRegion(1);
            DisableBase(m, 1);
            m.SetPosition(1, new Vec2(10, -10));   // dono longe
            m.SetPosition(0, ne.Def.TowerPosition);
            TestUtil.Run(m, 4f);
            Assert.InRange(ne.CaptureProgress, 45f, 55f);         // 8 s no nível 1
            Assert.Equal(1, ne.OwnerId);

            TestUtil.Run(m, 4.2f);
            Assert.Equal(0, ne.OwnerId);                           // propriedade mudou
            Assert.Equal(2, m.TowerCount(0));
            Assert.Equal(1, m.GetPlayer(0).TowersCaptured);
            Assert.Contains(m.Events, e => e.Type == MatchEventType.TowerCaptured && e.RegionId == 1 && e.PlayerId == 0 && e.OtherId == 1);

            // A propriedade fica na torre: sair da região não devolve nada
            m.SetPosition(0, new Vec2(0, 20));
            TestUtil.Run(m, 5f);
            Assert.Equal(0, ne.OwnerId);
        }

        [Fact]
        public void Capture_is_contested_when_owner_is_present_and_decays_when_empty()
        {
            var m = TestUtil.NewMatch();
            var ne = m.GetRegion(1);
            DisableBase(m, 1);
            m.SetPosition(1, new Vec2(10, -10));   // dono longe
            m.SetPosition(0, ne.Def.TowerPosition);
            TestUtil.Run(m, 2f);
            float progress = ne.CaptureProgress;
            Assert.True(progress > 20f);

            m.SetPosition(1, ne.Def.TowerPosition + new Vec2(1, 0)); // dono volta
            TestUtil.Run(m, 2f);
            Assert.True(ne.IsContested);
            TestUtil.Approx(progress, ne.CaptureProgress, 2);              // congelado

            m.SetPosition(0, new Vec2(0, 20));                        // atacante sai, dono fica
            TestUtil.Run(m, 0.5f);
            Assert.InRange(ne.CaptureProgress, progress - 16f, progress - 14f); // cai 30%/s
            TestUtil.Run(m, 2f);
            Assert.Equal(0f, ne.CaptureProgress);
            Assert.Equal(1, ne.OwnerId);
        }

        [Fact]
        public void Upgraded_tower_takes_longer_to_capture()
        {
            var m = TestUtil.NewMatch();
            var ne = m.GetRegion(1);
            DisableBase(m, 1);
            ne.TowerLevel = 4; // 14 s
            m.SetPosition(1, new Vec2(10, -10));
            m.SetPosition(0, ne.Def.TowerPosition);
            TestUtil.Run(m, 10f);
            Assert.Equal(1, ne.OwnerId);
            TestUtil.Run(m, 4.2f);
            Assert.Equal(0, ne.OwnerId);
        }

        [Fact]
        public void Center_gives_production_and_damage_bonus_to_its_owner()
        {
            var m = TestUtil.NewMatch();
            m.SetPosition(0, Vec2.Zero);
            TestUtil.Run(m, 12.2f);
            Assert.Equal(0, m.CenterRegion.OwnerId);
            TestUtil.Approx(3.0f * 1.25f, m.MineProductionOf(m.GetRegion(0)), 3);
            TestUtil.Approx(0.10f, m.DamageBonusOf(m.GetPlayer(0)), 3);
            TestUtil.Approx(3.0f, m.MineProductionOf(m.GetRegion(1)), 3);
        }

        [Fact]
        public void Military_base_shoots_invaders_and_not_its_owner()
        {
            var m = TestUtil.NewMatch();
            var ne = m.GetRegion(1);
            TestUtil.Run(m, 2f);
            m.SetPosition(1, ne.Def.BasePosition + new Vec2(1, 0));   // dono perto da própria base
            m.SetPosition(0, ne.Def.BasePosition + new Vec2(0, -5));  // invasor a 5 m
            TestUtil.Run(m, 1.05f);
            Assert.True(m.GetPlayer(0).Health < 3200);
            Assert.Equal(3200, m.GetPlayer(1).Health);
            Assert.Contains(m.Events, e => e.Type == MatchEventType.BaseFired && e.PlayerId == 0 && e.RegionId == 1);
        }

        [Fact]
        public void Military_base_can_be_disabled_by_attacks_and_restores_later()
        {
            var m = TestUtil.NewMatch();
            var ne = m.GetRegion(1);
            var a = m.GetPlayer(0);
            a.Items[ShopItemId.Cuirass] = 3;
            a.Health = m.MaxHealthOf(a);
            m.SetPosition(0, ne.Def.BasePosition + new Vec2(0, -8.5f));
            int guard = 0;
            while (!ne.Base.IsDisabled && a.IsAlive && guard++ < 3000)
            {
                if (m.TryBasicAttack(0, ne.Def.BasePosition - a.Position).Ok)
                    m.ReportBasicHitOnStructure(0, 1, StructureKind.MilitaryBase);
                m.Tick(TestUtil.Dt);
            }
            Assert.True(ne.Base.IsDisabled);
            TestUtil.Run(m, 30.5f);
            Assert.False(ne.Base.IsDisabled);
            Assert.Equal(4000, ne.Base.Health);
        }
    }
}
