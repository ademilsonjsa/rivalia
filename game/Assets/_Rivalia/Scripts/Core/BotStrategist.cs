using System;
using System.Collections.Generic;

namespace Rivalia.Core.AI
{
    public enum BotProfile { Economic, Defensive, Aggressive, Expansionist, Balanced }
    public enum BotDifficulty { Easy, Normal, Hard }

    public enum BotGoalKind { Idle, CollectMine, Upgrade, Shop, DefendTower, CaptureTower, ContestCenter, HuntPlayer, Retreat }

    /// <summary>Objetivo escolhido pela camada estratégica. A camada tática (Unity) navega até Destination e luta no caminho.</summary>
    public struct BotGoal
    {
        public BotGoalKind Kind;
        public Vec2 Destination;
        public int RegionId;
        public int TargetPlayerId;
        public StructureKind Structure;
        public ShopItemId Item;
        public float Score;
        public override string ToString() => $"{Kind} r={RegionId} t={TargetPlayerId} s={Score:0.00}";
    }

    /// <summary>Pesos de um perfil; cada um multiplica a pontuação base de um tipo de objetivo.</summary>
    public class BotWeights
    {
        public float Economy = 1f, Defense = 1f, Aggression = 1f, Expansion = 1f, Center = 1f;
        /// <summary>Fração da vida abaixo da qual o bot recua.</summary>
        public float RetreatHealth = 0.3f;

        public static BotWeights For(BotProfile profile)
        {
            switch (profile)
            {
                case BotProfile.Economic: return new BotWeights { Economy = 1.6f, Defense = 1.0f, Aggression = 0.6f, Expansion = 0.7f, Center = 0.8f, RetreatHealth = 0.4f };
                case BotProfile.Defensive: return new BotWeights { Economy = 1.0f, Defense = 1.7f, Aggression = 0.6f, Expansion = 0.6f, Center = 0.7f, RetreatHealth = 0.4f };
                case BotProfile.Aggressive: return new BotWeights { Economy = 0.7f, Defense = 0.7f, Aggression = 1.8f, Expansion = 1.1f, Center = 1.0f, RetreatHealth = 0.2f };
                case BotProfile.Expansionist: return new BotWeights { Economy = 0.8f, Defense = 0.8f, Aggression = 1.0f, Expansion = 1.7f, Center = 1.5f, RetreatHealth = 0.3f };
                default: return new BotWeights();
            }
        }
    }

    /// <summary>
    /// Camada de decisão estratégica dos bots (seção 2.8 do GDD). Pura e determinística: lê o estado da
    /// partida e devolve um objetivo. Roda a cada ~1 s; a mudança de estratégia acontece porque as
    /// pontuações dependem do estado (torre sob ataque, perda da torre, líder disparado, vida baixa).
    /// </summary>
    public class BotStrategist
    {
        public readonly BotProfile Profile;
        public readonly BotWeights Weights;
        public readonly BotDifficulty Difficulty;

        public BotStrategist(BotProfile profile, BotDifficulty difficulty = BotDifficulty.Normal)
        {
            Profile = profile;
            Weights = BotWeights.For(profile);
            Difficulty = difficulty;
        }

        public BotGoal Decide(Match match, PlayerState self)
        {
            var best = new BotGoal { Kind = BotGoalKind.Idle, Destination = self.Position, RegionId = -1, TargetPlayerId = Match.Neutral, Score = 0f };
            if (!self.IsAlive || match.IsOver) return best;

            var options = new List<BotGoal>();
            int myTowers = match.TowerCount(self.Id);
            float hpFrac = self.Health / (float)match.MaxHealthOf(self);

            // 1) Sobrevivência
            if (hpFrac < Weights.RetreatHealth && myTowers > 0)
            {
                var safe = NearestOwnedRegion(match, self);
                if (safe != null)
                    options.Add(new BotGoal { Kind = BotGoalKind.Retreat, Destination = safe.Def.TowerPosition, RegionId = safe.Id, TargetPlayerId = Match.Neutral, Score = 2.0f + (Weights.RetreatHealth - hpFrac) * 3f });
            }

            foreach (var r in match.Regions)
            {
                bool mine = r.OwnerId == self.Id;

                // 2) Defesa: torre própria sendo capturada ou contestada
                if (mine && (r.CaptureProgress > 0f || r.IsContested) && r.CapturingPlayerId != self.Id)
                {
                    float urgency = 1.0f + r.CaptureProgress / 50f + (r.Id == self.HomeRegionId ? 0.5f : 0f) + (myTowers == 1 ? 0.8f : 0f);
                    options.Add(new BotGoal { Kind = BotGoalKind.DefendTower, Destination = r.Def.TowerPosition, RegionId = r.Id, TargetPlayerId = r.CapturingPlayerId, Score = urgency * Weights.Defense });
                }

                // 3) Economia: coletar mina cheia
                if (mine && r.Mine != null && !r.Mine.IsSabotaged)
                {
                    float fill = r.Mine.Stored / Math.Max(1, match.MineCapacityOf(r));
                    if (fill > 0.35f)
                        options.Add(new BotGoal { Kind = BotGoalKind.CollectMine, Destination = r.Def.MinePosition, RegionId = r.Id, TargetPlayerId = Match.Neutral, Score = (0.4f + fill) * Weights.Economy - DistancePenalty(self, r.Def.MinePosition) });
                }

                // 4) Melhorias
                if (mine && !r.IsCenter)
                    AddUpgradeOptions(match, self, r, options);

                // 5) Expansão: capturar torres que não são minhas
                if (!mine)
                {
                    float score = CaptureScore(match, self, r, myTowers);
                    if (score > 0f)
                        options.Add(new BotGoal
                        {
                            Kind = r.IsCenter ? BotGoalKind.ContestCenter : BotGoalKind.CaptureTower,
                            Destination = r.Def.TowerPosition, RegionId = r.Id, TargetPlayerId = r.OwnerId, Score = score,
                        });
                }
            }

            // 6) Loja
            AddShopOption(match, self, options);

            // 7) Caça: inimigo próximo e fraco
            foreach (var p in match.Players)
            {
                if (p == self || !p.IsAlive) continue;
                float d = Vec2.Distance(p.Position, self.Position);
                if (d > 16f) continue;
                float theirHp = p.Health / (float)match.MaxHealthOf(p);
                float score = (0.6f + (1f - theirHp) + (hpFrac - theirHp) * 0.5f) * Weights.Aggression - d / 40f;
                if (match.TowerCount(p.Id) >= 3) score += 0.6f; // líder vira alvo
                options.Add(new BotGoal { Kind = BotGoalKind.HuntPlayer, Destination = p.Position, RegionId = -1, TargetPlayerId = p.Id, Score = score });
            }

            foreach (var o in options) if (o.Score > best.Score) best = o;
            return best;
        }

        private void AddUpgradeOptions(Match match, PlayerState self, RegionState r, List<BotGoal> options)
        {
            // Mantém reserva para a loja em perfis agressivos; dificuldade fácil gasta pior (mais tarde).
            int reserve = Difficulty == BotDifficulty.Easy ? 80 : 0;
            Consider(StructureKind.Mine, Weights.Economy * (match.Time < 240f ? 1.0f : 0.5f));
            Consider(StructureKind.MilitaryBase, Weights.Defense * 0.9f);
            Consider(StructureKind.Tower, Weights.Defense * 0.7f);

            void Consider(StructureKind kind, float weight)
            {
                int cost = match.UpgradeCost(r, kind);
                if (cost < 0 || self.Crystals < cost + reserve) return;
                var pos = match.StructurePosition(r, kind);
                options.Add(new BotGoal { Kind = BotGoalKind.Upgrade, Destination = pos, RegionId = r.Id, Structure = kind, TargetPlayerId = Match.Neutral, Score = 0.9f * weight - DistancePenalty(self, pos) });
            }
        }

        private void AddShopOption(Match match, PlayerState self, List<BotGoal> options)
        {
            var shop = NearestOwnedRegion(match, self, requireShop: true);
            if (shop == null) return;
            ShopItemId[] order;
            switch (Profile)
            {
                case BotProfile.Aggressive: order = new[] { ShopItemId.SharpBlade, ShopItemId.SwiftBoots, ShopItemId.Cuirass, ShopItemId.BronzePlate }; break;
                case BotProfile.Defensive: order = new[] { ShopItemId.Cuirass, ShopItemId.BronzePlate, ShopItemId.SharpBlade, ShopItemId.Potion }; break;
                default: order = new[] { ShopItemId.Cuirass, ShopItemId.SharpBlade, ShopItemId.BronzePlate, ShopItemId.SwiftBoots, ShopItemId.Potion }; break;
            }
            foreach (var id in order)
            {
                var item = match.Economy.GetItem(id);
                if (self.ItemCount(id) >= item.MaxStacks || self.Crystals < item.Price) continue;
                float w = id == ShopItemId.SharpBlade ? Weights.Aggression : (id == ShopItemId.Cuirass || id == ShopItemId.BronzePlate ? Weights.Defense : 1f);
                options.Add(new BotGoal { Kind = BotGoalKind.Shop, Destination = shop.Def.ShopPosition, RegionId = shop.Id, Item = id, TargetPlayerId = Match.Neutral, Score = 0.8f * w - DistancePenalty(self, shop.Def.ShopPosition) });
                return;
            }
        }

        private float CaptureScore(Match match, PlayerState self, RegionState r, int myTowers)
        {
            float score;
            if (myTowers == 0) score = 3.0f; // sem torre: reconquistar é questão de sobrevivência
            else score = (r.IsCenter ? 0.9f * Weights.Center : 0.7f) * Weights.Expansion;

            if (r.Id == self.HomeRegionId) score += 0.4f; // recuperar a casa
            var owner = match.GetPlayer(r.OwnerId);
            if (owner != null)
            {
                if (!owner.IsAlive) score += 0.5f;                       // dono morto: janela de invasão
                if (match.TowerCount(owner.Id) >= 3) score += 0.6f;      // conter o líder
                if (Vec2.Distance(owner.Position, r.Def.TowerPosition) > 20f) score += 0.2f;
            }
            if (r.Base != null)
            {
                if (r.Base.IsDisabled) score += 0.5f;
                else score -= 0.12f * r.Base.Level; // torreta forte desencoraja
            }
            // Cedo na partida só os expansionistas/agressivos saem de casa
            if (match.Time < 45f && myTowers > 0 && Weights.Expansion < 1.5f && Weights.Aggression < 1.5f) score -= 0.5f;
            return score - DistancePenalty(self, r.Def.TowerPosition);
        }

        private static float DistancePenalty(PlayerState self, Vec2 to) => Vec2.Distance(self.Position, to) / 120f;

        private static RegionState NearestOwnedRegion(Match match, PlayerState self, bool requireShop = false)
        {
            RegionState best = null;
            float bd = float.MaxValue;
            foreach (var r in match.Regions)
            {
                if (r.OwnerId != self.Id || (requireShop && r.IsCenter)) continue;
                float d = Vec2.SqrDistance(r.Def.TowerPosition, self.Position);
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        /// <summary>Erro de mira (graus) e tempo de reação (s) por dificuldade, usados pela camada tática.</summary>
        public static void AimParameters(BotDifficulty d, out float aimErrorDegrees, out float reactionSeconds, out float leadFactor)
        {
            switch (d)
            {
                case BotDifficulty.Easy: aimErrorDegrees = 14f; reactionSeconds = 0.6f; leadFactor = 0f; break;
                case BotDifficulty.Hard: aimErrorDegrees = 3f; reactionSeconds = 0.15f; leadFactor = 1f; break;
                default: aimErrorDegrees = 7f; reactionSeconds = 0.3f; leadFactor = 0.6f; break;
            }
        }
    }
}
