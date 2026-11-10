using Rivalia.Core;
using Xunit;

namespace Rivalia.Core.Tests
{
    public class EconomyTests
    {
        [Fact]
        public void Mine_produces_up_to_capacity_and_owner_collects_nearby()
        {
            var m = TestUtil.NewMatch();
            var home = m.GetRegion(0);
            var p = m.GetPlayer(0);
            m.SetPosition(0, new Vec2(-10, 10)); // longe da mina
            TestUtil.Run(m, 10f);
            Assert.InRange(home.Mine.Stored, 29f, 31f);           // 3/s
            TestUtil.Run(m, 60f);
            TestUtil.Approx(150f, home.Mine.Stored, 1);              // capacidade nível 1

            m.SetPosition(0, home.Def.MinePosition);
            m.Tick(TestUtil.Dt);
            Assert.Equal(100 + 150, p.Crystals);
            Assert.True(home.Mine.Stored < 1f);
            Assert.Equal(150, p.CrystalsMined);
        }

        [Fact]
        public void Enemy_cannot_collect_from_a_mine_it_does_not_own()
        {
            var m = TestUtil.NewMatch();
            var ne = m.GetRegion(1);
            ne.Base.DisabledTimer = 9999f;
            m.SetPosition(1, new Vec2(10, 10));
            TestUtil.Run(m, 10f);
            m.SetPosition(0, ne.Def.MinePosition);
            TestUtil.Run(m, 1f);
            Assert.Equal(100, m.GetPlayer(0).Crystals);
            Assert.True(ne.Mine.Stored > 25f);
        }

        [Fact]
        public void Upgrade_requires_owner_proximity_and_crystals_and_improves_stats()
        {
            var m = TestUtil.NewMatch();
            var home = m.GetRegion(0);
            var p = m.GetPlayer(0);
            m.SetPosition(0, new Vec2(-10, 10));
            Assert.Equal(CommandResult.TooFar, m.TryUpgrade(0, 0, StructureKind.Mine));
            m.SetPosition(0, home.Def.MinePosition);
            Assert.Equal(CommandResult.NotEnoughCrystals, m.TryUpgrade(0, 0, StructureKind.Mine));
            Assert.Equal(CommandResult.NotOwner, m.TryUpgrade(0, 1, StructureKind.Mine));

            p.Crystals = 1000;
            Assert.Equal(CommandResult.Ok, m.TryUpgrade(0, 0, StructureKind.Mine));
            Assert.Equal(850, p.Crystals);
            Assert.Equal(2, home.Mine.Level);
            TestUtil.Approx(4.5f, m.MineProductionOf(home), 3);
            Assert.Equal(250, m.MineCapacityOf(home));

            m.SetPosition(0, home.Def.BasePosition);
            Assert.Equal(CommandResult.Ok, m.TryUpgrade(0, 0, StructureKind.MilitaryBase));
            Assert.Equal(5000, home.Base.Health);
            m.SetPosition(0, home.Def.TowerPosition);
            Assert.Equal(CommandResult.Ok, m.TryUpgrade(0, 0, StructureKind.Tower));
            Assert.Equal(10f, m.CaptureSecondsOf(home));
            Assert.Equal(1000 - 150 - 120 - 100, p.Crystals);

            home.TowerLevel = 4;
            Assert.Equal(CommandResult.MaxLevel, m.TryUpgrade(0, 0, StructureKind.Tower));
        }

        [Fact]
        public void Shop_sells_items_with_limits_and_effects()
        {
            var m = TestUtil.NewMatch();
            var home = m.GetRegion(0);
            var p = m.GetPlayer(0);
            p.Crystals = 2000;
            m.SetPosition(0, home.Def.ShopPosition);

            Assert.Equal(CommandResult.Ok, m.TryBuy(0, 0, ShopItemId.Cuirass));
            Assert.Equal(3840, m.MaxHealthOf(p));
            Assert.Equal(3840, p.Health);                       // vida atual sobe junto
            Assert.Equal(CommandResult.Ok, m.TryBuy(0, 0, ShopItemId.SwiftBoots));
            TestUtil.Approx(6.0f * 1.08f, m.MoveSpeedOf(p), 3);
            Assert.Equal(CommandResult.Ok, m.TryBuy(0, 0, ShopItemId.SwiftBoots));
            Assert.Equal(CommandResult.MaxStacks, m.TryBuy(0, 0, ShopItemId.SwiftBoots));
            Assert.Equal(2000 - 120 - 100 - 100, p.Crystals);

            // loja de outra região não atende
            m.SetPosition(0, m.GetRegion(1).Def.ShopPosition);
            Assert.Equal(CommandResult.NotOwner, m.TryBuy(0, 1, ShopItemId.Potion));
        }

        [Fact]
        public void Potion_heals_forty_percent()
        {
            var m = TestUtil.NewMatch();
            var p = m.GetPlayer(0);
            p.Crystals = 500;
            m.SetPosition(0, m.GetRegion(0).Def.ShopPosition);
            Assert.Equal(CommandResult.Ok, m.TryBuy(0, 0, ShopItemId.Potion));
            Assert.Equal(CommandResult.NothingToDo, m.TryUsePotion(0));
            p.Health = 1000;
            Assert.Equal(CommandResult.Ok, m.TryUsePotion(0));
            Assert.Equal(1000 + 1280, p.Health);
            Assert.Equal(CommandResult.NotReady, m.TryUsePotion(0));
        }

        [Fact]
        public void Destroyed_mine_is_sabotaged_loses_stock_and_repairs()
        {
            var m = TestUtil.NewMatch();
            var ne = m.GetRegion(1);
            ne.Base.DisabledTimer = 9999f;
            m.SetPosition(1, new Vec2(10, 10));
            TestUtil.Run(m, 20f);
            Assert.True(ne.Mine.Stored > 50f);

            m.SetPosition(0, ne.Def.MinePosition + new Vec2(-6, 0));
            int guard = 0;
            while (!ne.Mine.IsSabotaged && guard++ < 3000)
            {
                if (m.TryBasicAttack(0, ne.Def.MinePosition - m.GetPlayer(0).Position).Ok)
                    m.ReportBasicHitOnStructure(0, 1, StructureKind.Mine);
                m.Tick(TestUtil.Dt);
            }
            Assert.True(ne.Mine.IsSabotaged);
            Assert.Equal(0f, ne.Mine.Stored);
            TestUtil.Run(m, 5f);
            Assert.Equal(0f, ne.Mine.Stored);                    // não produz sabotada
            TestUtil.Run(m, 10.5f);
            Assert.False(ne.Mine.IsSabotaged);
            Assert.True(ne.Mine.Stored > 0f);
        }

        [Fact]
        public void Kill_pays_bounty_and_steals_twenty_percent()
        {
            var m = TestUtil.NewMatch();
            TestUtil.Run(m, 2f);
            m.GetPlayer(1).Crystals = 200;
            m.SetPosition(0, new Vec2(0, 15));
            m.SetPosition(1, new Vec2(5, 15));
            TestUtil.KillWithShots(m, 0, 1);
            Assert.False(m.GetPlayer(1).IsAlive);
            Assert.Equal(100 + 40 + 40, m.GetPlayer(0).Crystals);
            Assert.Equal(160, m.GetPlayer(1).Crystals);
            Assert.Equal(1, m.GetPlayer(0).KillsBots);
        }
    }
}
