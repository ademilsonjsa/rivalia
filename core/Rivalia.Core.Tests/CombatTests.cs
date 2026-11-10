using Rivalia.Core;
using Xunit;

namespace Rivalia.Core.Tests
{
    public class CombatTests
    {
        [Fact]
        public void Basic_attack_consumes_ammo_respects_interval_and_reloads()
        {
            var m = TestUtil.NewMatch();
            var p = m.GetPlayer(0);
            Assert.Equal(3, p.Ammo);

            var shot = m.TryBasicAttack(0, new Vec2(1, 0));
            Assert.True(shot.Ok);
            Assert.Equal(new Vec2(1, 0), shot.Direction);       // ataque segue a direção escolhida
            Assert.Equal(2, p.Ammo);

            // intervalo mínimo entre disparos
            Assert.Equal(CommandResult.OnCooldown, m.TryBasicAttack(0, new Vec2(1, 0)).Result);
            TestUtil.Run(m, 0.5f);
            Assert.True(m.TryBasicAttack(0, new Vec2(0, 1)).Ok);
            TestUtil.Run(m, 0.47f);
            Assert.True(m.TryBasicAttack(0, new Vec2(-1, 0)).Ok);
            Assert.Equal(0, p.Ammo);
            p.AttackCooldown = 0f;                                   // isola a regra de munição
            Assert.Equal(CommandResult.NoAmmo, m.TryBasicAttack(0, new Vec2(1, 0)).Result);

            // recarga: 1 carga a cada 1,4 s, contada desde o primeiro disparo (t = 0)
            TestUtil.Run(m, 0.5f);                                   // t ~ 1,47 s
            Assert.Equal(1, p.Ammo);
            TestUtil.Run(m, 3.0f);
            Assert.Equal(3, p.Ammo);
        }

        [Fact]
        public void Attack_direction_is_independent_from_position_and_normalized()
        {
            var m = TestUtil.NewMatch();
            var shot = m.TryBasicAttack(0, new Vec2(-3, -4));
            Assert.True(shot.Ok);
            TestUtil.Approx(-0.6f, shot.Direction.X, 3);
            TestUtil.Approx(-0.8f, shot.Direction.Z, 3);
            Assert.Equal(m.GetPlayer(0).Position, shot.Origin);
        }

        [Fact]
        public void Hit_applies_damage_formula_and_charges_super()
        {
            var m = TestUtil.NewMatch();
            TestUtil.Run(m, 2f); // termina proteção de nascimento
            m.SetPosition(0, new Vec2(0, 0));
            m.SetPosition(1, new Vec2(5, 0));
            int dealt = TestUtil.ShootAndHit(m, 0, 1);
            Assert.Equal(640, dealt);
            Assert.Equal(3200 - 640, m.GetPlayer(1).Health);
            TestUtil.Approx(0.25f, m.GetPlayer(0).SuperCharge, 3);
        }

        [Fact]
        public void Damage_bonus_and_armor_follow_formula_with_caps()
        {
            var eco = new EconomyConfig();
            var m = TestUtil.NewMatch(eco: eco);
            var a = m.GetPlayer(0);
            var t = m.GetPlayer(1);
            a.Items[ShopItemId.SharpBlade] = 3;    // +45%
            t.Items[ShopItemId.BronzePlate] = 2;   // 20% resistência
            TestUtil.Run(m, 2f);
            m.SetPosition(0, new Vec2(0, 0));
            m.SetPosition(1, new Vec2(4, 0));
            int dealt = TestUtil.ShootAndHit(m, 0, 1);
            Assert.Equal((int)System.Math.Round(640 * 1.45f * 0.80f), dealt);

            t.Items[ShopItemId.BronzePlate] = 9;   // teto de 50%
            TestUtil.Approx(0.5f, m.ArmorOf(t), 3);
        }

        [Fact]
        public void Hit_without_a_fired_shot_or_out_of_range_is_rejected()
        {
            var m = TestUtil.NewMatch();
            TestUtil.Run(m, 2f);
            m.SetPosition(0, new Vec2(0, 0));
            m.SetPosition(1, new Vec2(5, 0));
            Assert.Equal(0, m.ReportBasicHitOnPlayer(0, 1));        // nenhum disparo autorizado

            m.SetPosition(1, new Vec2(20, 0));
            Assert.True(m.TryBasicAttack(0, new Vec2(1, 0)).Ok);
            Assert.Equal(0, m.ReportBasicHitOnPlayer(0, 1));        // fora do alcance (9 m)
            Assert.Equal(3200, m.GetPlayer(1).Health);
        }

        [Fact]
        public void Super_hits_area_and_applies_damage_over_time()
        {
            var m = TestUtil.NewMatch();
            TestUtil.Run(m, 2f);
            var a = m.GetPlayer(0);
            a.SuperCharge = 1f;
            m.SetPosition(0, new Vec2(0, 0));
            m.SetPosition(1, new Vec2(6, 0));
            m.SetPosition(2, new Vec2(7, 1));
            m.SetPosition(3, new Vec2(-30, -30));

            Assert.Equal(CommandResult.OutOfRange, m.TryUseSuper(0, new Vec2(20, 0)));
            Assert.Equal(CommandResult.Ok, m.TryUseSuper(0, new Vec2(6.5f, 0)));
            Assert.Equal(0f, a.SuperCharge);
            Assert.Equal(3200 - 1200, m.GetPlayer(1).Health);
            Assert.Equal(3200 - 1200, m.GetPlayer(2).Health);
            Assert.Equal(3200, m.GetPlayer(3).Health);

            TestUtil.Run(m, 3.2f); // DoT 200/s por 3 s
            Assert.Equal(3200 - 1200 - 600, m.GetPlayer(1).Health);
            Assert.Equal(CommandResult.NotReady, m.TryUseSuper(0, new Vec2(1, 0)));
        }

        [Fact]
        public void Ability_has_cooldown()
        {
            var m = TestUtil.NewMatch();
            Assert.Equal(CommandResult.Ok, m.TryUseAbility(0, new Vec2(1, 0), out float dist));
            Assert.Equal(5f, dist);
            Assert.Equal(CommandResult.OnCooldown, m.TryUseAbility(0, new Vec2(1, 0), out _));
            TestUtil.Run(m, 8.1f);
            Assert.Equal(CommandResult.Ok, m.TryUseAbility(0, new Vec2(1, 0), out _));
        }

        [Fact]
        public void Regeneration_starts_after_three_seconds_out_of_combat()
        {
            var m = TestUtil.NewMatch();
            TestUtil.Run(m, 2f);
            m.SetPosition(0, new Vec2(0, 0));
            m.SetPosition(1, new Vec2(5, 0));
            TestUtil.ShootAndHit(m, 0, 1);
            var t = m.GetPlayer(1);
            TestUtil.Run(m, 2.5f);
            Assert.Equal(3200 - 640, t.Health);
            TestUtil.Run(m, 3f);
            Assert.Equal(3200, t.Health);
        }

        [Fact]
        public void Positions_are_clamped_to_the_arena()
        {
            var m = TestUtil.NewMatch();
            m.SetPosition(0, new Vec2(500, -500));
            Assert.Equal(new Vec2(40, -40), m.GetPlayer(0).Position);
        }
    }
}
