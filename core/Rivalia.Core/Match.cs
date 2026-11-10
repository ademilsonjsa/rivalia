using System;
using System.Collections.Generic;

namespace Rivalia.Core
{
    public enum CommandResult
    {
        Ok, MatchOver, NotAlive, NotOwner, TooFar, NotEnoughCrystals, MaxLevel, MaxStacks,
        OnCooldown, NoAmmo, OutOfRange, InvalidTarget, NotReady, NothingToDo,
    }

    /// <summary>Disparo autorizado pelo núcleo; a engine cria o projétil com estes parâmetros.</summary>
    public struct AttackResult
    {
        public CommandResult Result;
        public Vec2 Origin;
        public Vec2 Direction;
        public float Speed;
        public float Range;
        public float Radius;
        public bool Ok => Result == CommandResult.Ok;
    }

    /// <summary>
    /// Simulação autoritativa das regras da partida, independente de engine.
    /// A engine (Unity) move os personagens com física/NavMesh e informa posições e acertos;
    /// o núcleo valida, decide dano, economia, captura, renascimento e vitória.
    /// No multiplayer (Fase 4) a mesma classe roda no servidor dedicado.
    /// </summary>
    public class Match
    {
        public const int Neutral = -1;

        public readonly MapDefinition Map;
        public readonly MatchRules Rules;
        public readonly EconomyConfig Economy;
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly List<RegionState> Regions = new List<RegionState>();

        public float Time { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsOver => Result != null;
        public MatchResult Result { get; private set; }

        private readonly List<MatchEvent> _events = new List<MatchEvent>();
        public IReadOnlyList<MatchEvent> Events => _events;

        // Disparos autorizados ainda em voo: um acerto só vale se existir um disparo pendente (anti-trapaça).
        private struct PendingShot { public int PlayerId; public Vec2 Origin; public float Range; public float ExpiresAt; }
        private readonly List<PendingShot> _pendingShots = new List<PendingShot>();

        public Match(MapDefinition map, MatchRules rules = null, EconomyConfig economy = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Rules = rules ?? new MatchRules();
            Economy = economy ?? new EconomyConfig();
            foreach (var def in map.Regions)
            {
                var r = new RegionState { Def = def };
                if (!def.IsCenter)
                {
                    r.Mine = new MineState { Health = Economy.MineLevels[0].MaxHealth };
                    r.Base = new MilitaryBaseState { Health = Economy.BaseLevels[0].MaxHealth };
                }
                Regions.Add(r);
            }
        }

        // ------------------------------------------------------------------ montagem

        /// <summary>Adiciona um participante; ele recebe a próxima região livre como casa.</summary>
        public PlayerState AddPlayer(string name, bool isBot, CharacterStats stats = null)
        {
            if (IsStarted) throw new InvalidOperationException("A partida já começou.");
            RegionState home = null;
            foreach (var r in Regions)
                if (!r.IsCenter && r.OwnerId == Neutral) { home = r; break; }
            if (home == null) throw new InvalidOperationException("Não há região livre para mais um participante.");

            var p = new PlayerState
            {
                Id = Players.Count,
                Name = name,
                IsBot = isBot,
                HomeRegionId = home.Id,
                Stats = stats ?? CharacterStats.TitoFaisca(),
                Crystals = Economy.StartingCrystals,
            };
            p.Health = p.Stats.MaxHealth;
            p.Ammo = p.Stats.MaxAmmo;
            p.Position = SpawnPoint(home);
            home.OwnerId = p.Id;
            Players.Add(p);
            return p;
        }

        public void Start()
        {
            if (Players.Count < 2) throw new InvalidOperationException("São necessários pelo menos 2 participantes.");
            IsStarted = true;
        }

        // ------------------------------------------------------------------ consultas

        public PlayerState GetPlayer(int id) => id >= 0 && id < Players.Count ? Players[id] : null;
        public RegionState GetRegion(int id) => id >= 0 && id < Regions.Count ? Regions[id] : null;

        public RegionState CenterRegion
        {
            get { foreach (var r in Regions) if (r.IsCenter) return r; return null; }
        }

        public int TowerCount(int playerId)
        {
            int n = 0;
            foreach (var r in Regions) if (r.OwnerId == playerId) n++;
            return n;
        }

        public bool OwnsCenter(int playerId)
        {
            var c = CenterRegion;
            return c != null && c.OwnerId == playerId;
        }

        public int MaxHealthOf(PlayerState p) =>
            (int)Math.Round(p.Stats.MaxHealth * (1f + Economy.CuirassHealthBonus * p.ItemCount(ShopItemId.Cuirass)));

        public float DamageBonusOf(PlayerState p)
        {
            float b = Economy.BladeDamageBonus * p.ItemCount(ShopItemId.SharpBlade);
            if (OwnsCenter(p.Id)) b += Economy.CenterDamageBonus;
            return Math.Min(b, Economy.MaxDamageBonus);
        }

        public float ArmorOf(PlayerState p) =>
            Math.Min(p.Stats.Armor + Economy.PlateArmorBonus * p.ItemCount(ShopItemId.BronzePlate), Economy.MaxArmor);

        public float MoveSpeedOf(PlayerState p) =>
            p.Stats.MoveSpeed * (1f + Economy.BootsSpeedBonus * p.ItemCount(ShopItemId.SwiftBoots));

        public float MineProductionOf(RegionState r)
        {
            if (r.Mine == null || r.OwnerId == Neutral || r.Mine.IsSabotaged) return 0f;
            float rate = Economy.MineLevels[r.Mine.Level - 1].ProductionPerSecond;
            if (OwnsCenter(r.OwnerId)) rate *= 1f + Economy.CenterMineBonus;
            return rate;
        }

        public int MineCapacityOf(RegionState r) => r.Mine == null ? 0 : Economy.MineLevels[r.Mine.Level - 1].Capacity;

        public float CaptureSecondsOf(RegionState r) =>
            r.IsCenter ? Economy.CenterCaptureSeconds : Economy.TowerLevels[r.TowerLevel - 1].CaptureSeconds;

        public Vec2 StructurePosition(RegionState r, StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Mine: return r.Def.MinePosition;
                case StructureKind.Shop: return r.Def.ShopPosition;
                case StructureKind.MilitaryBase: return r.Def.BasePosition;
                default: return r.Def.TowerPosition;
            }
        }

        public int StructureLevel(RegionState r, StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Mine: return r.Mine?.Level ?? 0;
                case StructureKind.MilitaryBase: return r.Base?.Level ?? 0;
                case StructureKind.Tower: return r.TowerLevel;
                default: return 0;
            }
        }

        /// <summary>Custo da próxima melhoria, ou -1 se não houver.</summary>
        public int UpgradeCost(RegionState r, StructureKind kind)
        {
            if (r.IsCenter) return -1;
            switch (kind)
            {
                case StructureKind.Mine: return r.Mine.Level < Economy.MineLevels.Count ? Economy.MineLevels[r.Mine.Level].UpgradeCost : -1;
                case StructureKind.MilitaryBase: return r.Base.Level < Economy.BaseLevels.Count ? Economy.BaseLevels[r.Base.Level].UpgradeCost : -1;
                case StructureKind.Tower: return r.TowerLevel < Economy.TowerLevels.Count ? Economy.TowerLevels[r.TowerLevel].UpgradeCost : -1;
                default: return -1;
            }
        }

        public Vec2 SpawnPoint(RegionState r)
        {
            Vec2 toCenter = (Vec2.Zero - r.Def.TowerPosition).Normalized;
            if (toCenter.SqrLength < 0.01f) toCenter = new Vec2(0f, -1f);
            return r.Def.TowerPosition + toCenter * 2.5f;
        }

        // ------------------------------------------------------------------ eventos

        public List<MatchEvent> DrainEvents()
        {
            var copy = new List<MatchEvent>(_events);
            _events.Clear();
            return copy;
        }

        private void Emit(MatchEventType type, int playerId = Neutral, int otherId = Neutral, int regionId = -1, int amount = 0, Vec2 pos = default)
        {
            _events.Add(new MatchEvent { Type = type, Time = Time, PlayerId = playerId, OtherId = otherId, RegionId = regionId, Amount = amount, Position = pos });
        }

        // ------------------------------------------------------------------ comandos

        /// <summary>A engine informa a posição atual (após física/colisões). Limitada à arena.</summary>
        public void SetPosition(int playerId, Vec2 position)
        {
            var p = GetPlayer(playerId);
            if (p == null || !p.IsAlive) return;
            float h = Map.HalfSize;
            p.Position = new Vec2(Clamp(position.X, -h, h), Clamp(position.Z, -h, h));
        }

        public AttackResult TryBasicAttack(int playerId, Vec2 direction)
        {
            var res = new AttackResult();
            var p = GetPlayer(playerId);
            if (IsOver) { res.Result = CommandResult.MatchOver; return res; }
            if (p == null || !p.IsAlive) { res.Result = CommandResult.NotAlive; return res; }
            if (direction.SqrLength < 1e-6f) { res.Result = CommandResult.InvalidTarget; return res; }
            if (p.AttackCooldown > 0f) { res.Result = CommandResult.OnCooldown; return res; }
            if (p.Ammo <= 0) { res.Result = CommandResult.NoAmmo; return res; }

            if (p.Ammo == p.Stats.MaxAmmo) p.ReloadTimer = 0f;
            p.Ammo--;
            p.AttackCooldown = p.Stats.AttackInterval;
            p.TimeSinceCombat = 0f;
            float flight = p.Stats.AttackRange / Math.Max(p.Stats.ProjectileSpeed, 0.1f);
            _pendingShots.Add(new PendingShot { PlayerId = p.Id, Origin = p.Position, Range = p.Stats.AttackRange, ExpiresAt = Time + flight + 0.5f });

            res.Result = CommandResult.Ok;
            res.Origin = p.Position;
            res.Direction = direction.Normalized;
            res.Speed = p.Stats.ProjectileSpeed;
            res.Range = p.Stats.AttackRange;
            res.Radius = p.Stats.ProjectileRadius;
            return res;
        }

        /// <summary>A engine informa que um projétil do básico atingiu um herói. Retorna o dano aplicado (0 = rejeitado).</summary>
        public int ReportBasicHitOnPlayer(int attackerId, int targetId)
        {
            if (IsOver) return 0;
            var a = GetPlayer(attackerId);
            var t = GetPlayer(targetId);
            if (a == null || t == null || a == t || !t.IsAlive) return 0;
            // O alvo precisa estar ao alcance do PONTO DE ONDE o tiro saiu (o atacante pode ter andado durante o voo).
            if (!ConsumePendingShot(attackerId, t.Position, Rules.HitValidationTolerance)) return 0;
            if (t.SpawnProtection > 0f) return 0;

            int dealt = ApplyDamage(a, t, a.Stats.AttackDamage, true);
            if (dealt > 0 && a.IsAlive) a.SuperCharge = Math.Min(1f, a.SuperCharge + a.Stats.SuperChargePerHit);
            return dealt;
        }

        /// <summary>Acerto do básico numa Mina ou Base Militar inimiga.</summary>
        public int ReportBasicHitOnStructure(int attackerId, int regionId, StructureKind kind)
        {
            if (IsOver) return 0;
            var a = GetPlayer(attackerId);
            var r = GetRegion(regionId);
            if (a == null || r == null || r.IsCenter) return 0;
            if (kind != StructureKind.Mine && kind != StructureKind.MilitaryBase) return 0;
            if (r.OwnerId == attackerId) return 0;
            if (!ConsumePendingShot(attackerId, StructurePosition(r, kind), Rules.HitValidationTolerance + 1.5f)) return 0;

            int dmg = (int)Math.Round(a.Stats.AttackDamage * (1f + DamageBonusOf(a)));
            DamageStructure(a, r, kind, dmg);
            return dmg;
        }

        /// <summary>Habilidade especial (avanço). Retorna a distância autorizada; a engine move com colisão.</summary>
        public CommandResult TryUseAbility(int playerId, Vec2 direction, out float dashDistance)
        {
            dashDistance = 0f;
            var p = GetPlayer(playerId);
            if (IsOver) return CommandResult.MatchOver;
            if (p == null || !p.IsAlive) return CommandResult.NotAlive;
            if (p.AbilityCooldown > 0f) return CommandResult.OnCooldown;
            p.AbilityCooldown = p.Stats.AbilityCooldown;
            dashDistance = p.Stats.AbilityDashDistance;
            Emit(MatchEventType.AbilityUsed, p.Id, pos: p.Position);
            return CommandResult.Ok;
        }

        /// <summary>Super: área no ponto alvo com dano de impacto e dano contínuo.</summary>
        public CommandResult TryUseSuper(int playerId, Vec2 targetPoint)
        {
            var p = GetPlayer(playerId);
            if (IsOver) return CommandResult.MatchOver;
            if (p == null || !p.IsAlive) return CommandResult.NotAlive;
            if (p.SuperCharge < 1f) return CommandResult.NotReady;
            if (Vec2.Distance(p.Position, targetPoint) > p.Stats.SuperRange + Rules.HitValidationTolerance) return CommandResult.OutOfRange;

            p.SuperCharge = 0f;
            p.TimeSinceCombat = 0f;
            Emit(MatchEventType.SuperUsed, p.Id, pos: targetPoint);
            float r2 = p.Stats.SuperRadius * p.Stats.SuperRadius;
            int dotPerTick = (int)Math.Round(p.Stats.SuperDotDamagePerSecond * Rules.DotTickSeconds);

            foreach (var t in Players)
            {
                if (t == p || !t.IsAlive || t.SpawnProtection > 0f) continue;
                if (Vec2.SqrDistance(t.Position, targetPoint) > r2) continue;
                ApplyDamage(p, t, p.Stats.SuperImpactDamage, true);
                if (t.IsAlive)
                    t.Dots.Add(new DotEffect { SourcePlayerId = p.Id, DamagePerTick = dotPerTick, Remaining = p.Stats.SuperDotDuration, TickTimer = Rules.DotTickSeconds });
            }
            foreach (var r in Regions)
            {
                if (r.IsCenter || r.OwnerId == p.Id) continue;
                int dmg = (int)Math.Round(p.Stats.SuperImpactDamage * (1f + DamageBonusOf(p)));
                if (Vec2.SqrDistance(r.Def.MinePosition, targetPoint) <= r2) DamageStructure(p, r, StructureKind.Mine, dmg);
                if (Vec2.SqrDistance(r.Def.BasePosition, targetPoint) <= r2) DamageStructure(p, r, StructureKind.MilitaryBase, dmg);
            }
            return CommandResult.Ok;
        }

        public CommandResult TryUpgrade(int playerId, int regionId, StructureKind kind)
        {
            var p = GetPlayer(playerId);
            var r = GetRegion(regionId);
            if (IsOver) return CommandResult.MatchOver;
            if (p == null || !p.IsAlive) return CommandResult.NotAlive;
            if (r == null || r.IsCenter || kind == StructureKind.Shop) return CommandResult.InvalidTarget;
            if (r.OwnerId != p.Id) return CommandResult.NotOwner;
            if (Vec2.Distance(p.Position, StructurePosition(r, kind)) > Economy.InteractRadius) return CommandResult.TooFar;
            int cost = UpgradeCost(r, kind);
            if (cost < 0) return CommandResult.MaxLevel;
            if (p.Crystals < cost) return CommandResult.NotEnoughCrystals;

            p.Crystals -= cost;
            switch (kind)
            {
                case StructureKind.Mine:
                    r.Mine.Level++;
                    if (!r.Mine.IsSabotaged) r.Mine.Health = Economy.MineLevels[r.Mine.Level - 1].MaxHealth;
                    break;
                case StructureKind.MilitaryBase:
                    r.Base.Level++;
                    if (!r.Base.IsDisabled) r.Base.Health = Economy.BaseLevels[r.Base.Level - 1].MaxHealth;
                    break;
                case StructureKind.Tower:
                    r.TowerLevel++;
                    break;
            }
            Emit(MatchEventType.Upgraded, p.Id, regionId: r.Id, amount: (int)kind);
            return CommandResult.Ok;
        }

        public CommandResult TryBuy(int playerId, int regionId, ShopItemId itemId)
        {
            var p = GetPlayer(playerId);
            var r = GetRegion(regionId);
            if (IsOver) return CommandResult.MatchOver;
            if (p == null || !p.IsAlive) return CommandResult.NotAlive;
            if (r == null || r.IsCenter) return CommandResult.InvalidTarget;
            if (r.OwnerId != p.Id) return CommandResult.NotOwner;
            if (Vec2.Distance(p.Position, r.Def.ShopPosition) > Economy.InteractRadius) return CommandResult.TooFar;
            var item = Economy.GetItem(itemId);
            if (item == null) return CommandResult.InvalidTarget;
            if (p.ItemCount(itemId) >= item.MaxStacks) return CommandResult.MaxStacks;
            if (p.Crystals < item.Price) return CommandResult.NotEnoughCrystals;

            int oldMax = MaxHealthOf(p);
            p.Crystals -= item.Price;
            p.Items[itemId] = p.ItemCount(itemId) + 1;
            if (itemId == ShopItemId.Cuirass) p.Health += MaxHealthOf(p) - oldMax;
            Emit(MatchEventType.Purchased, p.Id, regionId: r.Id, amount: (int)itemId);
            return CommandResult.Ok;
        }

        public CommandResult TryUsePotion(int playerId)
        {
            var p = GetPlayer(playerId);
            if (IsOver) return CommandResult.MatchOver;
            if (p == null || !p.IsAlive) return CommandResult.NotAlive;
            if (p.ItemCount(ShopItemId.Potion) <= 0) return CommandResult.NotReady;
            int max = MaxHealthOf(p);
            if (p.Health >= max) return CommandResult.NothingToDo;
            p.Items[ShopItemId.Potion] = p.ItemCount(ShopItemId.Potion) - 1;
            int heal = (int)Math.Round(max * Economy.PotionHealFraction);
            p.Health = Math.Min(max, p.Health + heal);
            Emit(MatchEventType.PotionUsed, p.Id, amount: heal);
            return CommandResult.Ok;
        }

        // ------------------------------------------------------------------ simulação

        public void Tick(float dt)
        {
            if (!IsStarted || IsOver || dt <= 0f) return;
            Time += dt;

            for (int i = _pendingShots.Count - 1; i >= 0; i--)
                if (_pendingShots[i].ExpiresAt < Time) _pendingShots.RemoveAt(i);

            foreach (var p in Players) TickPlayer(p, dt);
            foreach (var r in Regions) TickMine(r, dt);
            foreach (var r in Regions) TickBase(r, dt);
            foreach (var r in Regions) TickCapture(r, dt);

            CheckEnd();
        }

        private void TickPlayer(PlayerState p, float dt)
        {
            if (p.Life == LifeState.Dead)
            {
                p.RespawnTimer -= dt;
                if (p.RespawnTimer <= 0f) Respawn(p);
                return;
            }
            if (!p.IsAlive) return;

            p.SecondsAlive += dt;
            p.SpawnProtection = Math.Max(0f, p.SpawnProtection - dt);
            p.AttackCooldown = Math.Max(0f, p.AttackCooldown - dt);
            p.AbilityCooldown = Math.Max(0f, p.AbilityCooldown - dt);
            p.TimeSinceCombat += dt;

            if (p.Ammo < p.Stats.MaxAmmo)
            {
                p.ReloadTimer += dt;
                while (p.ReloadTimer >= p.Stats.ReloadPerAmmo && p.Ammo < p.Stats.MaxAmmo)
                {
                    p.ReloadTimer -= p.Stats.ReloadPerAmmo;
                    p.Ammo++;
                }
                if (p.Ammo == p.Stats.MaxAmmo) p.ReloadTimer = 0f;
            }

            int max = MaxHealthOf(p);
            if (p.TimeSinceCombat >= Rules.RegenDelaySeconds && p.Health < max)
                p.Health = Math.Min(max, p.Health + (int)Math.Ceiling(max * Rules.RegenFractionPerSecond * dt));

            for (int i = p.Dots.Count - 1; i >= 0 && p.IsAlive; i--)
            {
                var d = p.Dots[i];
                d.Remaining -= dt;
                d.TickTimer -= dt;
                while (d.TickTimer <= 0f && p.IsAlive)
                {
                    d.TickTimer += Rules.DotTickSeconds;
                    var src = GetPlayer(d.SourcePlayerId);
                    ApplyDamage(src, p, d.DamagePerTick, false);
                }
                if (d.Remaining <= 0f && i < p.Dots.Count) p.Dots.RemoveAt(i);
            }
        }

        private void TickMine(RegionState r, float dt)
        {
            if (r.Mine == null) return;
            var m = r.Mine;
            if (m.IsSabotaged)
            {
                m.SabotageTimer -= dt;
                if (m.SabotageTimer <= 0f)
                {
                    m.SabotageTimer = 0f;
                    m.Health = Economy.MineLevels[m.Level - 1].MaxHealth;
                    Emit(MatchEventType.MineRepaired, r.OwnerId, regionId: r.Id);
                }
                return;
            }
            m.Stored = Math.Min(MineCapacityOf(r), m.Stored + MineProductionOf(r) * dt);

            var owner = GetPlayer(r.OwnerId);
            if (owner != null && owner.IsAlive && m.Stored >= 1f &&
                Vec2.Distance(owner.Position, r.Def.MinePosition) <= Economy.InteractRadius)
            {
                int amount = (int)m.Stored;
                m.Stored -= amount;
                owner.Crystals += amount;
                owner.CrystalsMined += amount;
                Emit(MatchEventType.MineCollected, owner.Id, regionId: r.Id, amount: amount);
            }
        }

        private void TickBase(RegionState r, float dt)
        {
            if (r.Base == null) return;
            var b = r.Base;
            if (b.IsDisabled)
            {
                b.DisabledTimer -= dt;
                if (b.DisabledTimer <= 0f)
                {
                    b.DisabledTimer = 0f;
                    b.Health = Economy.BaseLevels[b.Level - 1].MaxHealth;
                    Emit(MatchEventType.BaseRestored, r.OwnerId, regionId: r.Id);
                }
                return;
            }
            b.FireCooldown = Math.Max(0f, b.FireCooldown - dt);
            if (r.OwnerId == Neutral || b.FireCooldown > 0f) return;

            var lvl = Economy.BaseLevels[b.Level - 1];
            PlayerState target = null;
            float best = lvl.Range * lvl.Range;
            foreach (var p in Players)
            {
                if (p.Id == r.OwnerId || !p.IsAlive || p.SpawnProtection > 0f) continue;
                float d2 = Vec2.SqrDistance(p.Position, r.Def.BasePosition);
                if (d2 <= best) { best = d2; target = p; }
            }
            if (target == null) return;

            b.FireCooldown = Economy.BaseFireInterval;
            Emit(MatchEventType.BaseFired, target.Id, r.OwnerId, r.Id, lvl.Damage, target.Position);
            ApplyDamage(GetPlayer(r.OwnerId), target, lvl.Damage, false);
        }

        private void TickCapture(RegionState r, float dt)
        {
            int attacker = Neutral;
            int attackerCount = 0;
            bool ownerPresent = false;
            float cr2 = r.Def.CaptureRadius * r.Def.CaptureRadius;
            foreach (var p in Players)
            {
                if (!p.IsAlive || Vec2.SqrDistance(p.Position, r.Def.TowerPosition) > cr2) continue;
                if (p.Id == r.OwnerId) ownerPresent = true;
                else { attacker = p.Id; attackerCount++; }
            }

            bool contested = attackerCount >= 2 || (attackerCount == 1 && ownerPresent);
            if (contested)
            {
                if (!r.IsContested) Emit(MatchEventType.CaptureContested, attacker, r.OwnerId, r.Id);
                r.IsContested = true;
                return; // progresso congelado
            }
            r.IsContested = false;

            if (attackerCount == 1)
            {
                if (r.CapturingPlayerId != attacker && r.CaptureProgress > 0f)
                {
                    // Outro jogador havia começado: o progresso dele precisa zerar antes.
                    r.CaptureProgress = Math.Max(0f, r.CaptureProgress - Rules.DecayPerSecondOwner * dt);
                    if (r.CaptureProgress <= 0f) r.CapturingPlayerId = Neutral;
                    return;
                }
                if (r.CapturingPlayerId != attacker)
                {
                    r.CapturingPlayerId = attacker;
                    Emit(MatchEventType.CaptureStarted, attacker, r.OwnerId, r.Id);
                }
                r.CaptureProgress += 100f / CaptureSecondsOf(r) * dt;
                if (r.CaptureProgress >= 100f) CompleteCapture(r, attacker);
                return;
            }

            float decay = ownerPresent ? Rules.DecayPerSecondOwner : Rules.DecayPerSecondEmpty;
            r.CaptureProgress = Math.Max(0f, r.CaptureProgress - decay * dt);
            if (r.CaptureProgress <= 0f) r.CapturingPlayerId = Neutral;
        }

        private void CompleteCapture(RegionState r, int newOwnerId)
        {
            int previous = r.OwnerId;
            r.OwnerId = newOwnerId;
            r.CaptureProgress = 0f;
            r.CapturingPlayerId = Neutral;
            var p = GetPlayer(newOwnerId);
            p.TowersCaptured++;
            p.Crystals += Economy.TowerCaptureReward;
            Emit(MatchEventType.TowerCaptured, newOwnerId, previous, r.Id, Economy.TowerCaptureReward, r.Def.TowerPosition);
        }

        // ------------------------------------------------------------------ dano, morte, renascimento

        /// <summary>Aplica a fórmula da seção 4.2. Retorna o dano efetivo.</summary>
        private int ApplyDamage(PlayerState attacker, PlayerState target, int baseDamage, bool applyAttackerBonus)
        {
            if (!target.IsAlive || target.SpawnProtection > 0f) return 0;
            float bonus = applyAttackerBonus && attacker != null ? DamageBonusOf(attacker) : 0f;
            int dmg = (int)Math.Round(baseDamage * (1f + bonus) * (1f - ArmorOf(target)));
            if (dmg <= 0) return 0;
            dmg = Math.Min(dmg, target.Health);

            target.Health -= dmg;
            target.TimeSinceCombat = 0f;
            if (attacker != null)
            {
                attacker.DamageDealt += dmg;
                attacker.TimeSinceCombat = 0f;
            }
            Emit(MatchEventType.Damaged, target.Id, attacker?.Id ?? Neutral, amount: dmg, pos: target.Position);
            if (target.Health <= 0) Kill(target, attacker);
            return dmg;
        }

        private void DamageStructure(PlayerState attacker, RegionState r, StructureKind kind, int dmg)
        {
            attacker.TimeSinceCombat = 0f;
            if (kind == StructureKind.Mine && !r.Mine.IsSabotaged)
            {
                r.Mine.Health -= dmg;
                attacker.DamageDealt += dmg;
                if (r.Mine.Health <= 0)
                {
                    r.Mine.Health = 0;
                    r.Mine.Stored = 0f;
                    r.Mine.SabotageTimer = Economy.MineSabotageSeconds;
                    Emit(MatchEventType.MineSabotaged, r.OwnerId, attacker.Id, r.Id);
                }
            }
            else if (kind == StructureKind.MilitaryBase && !r.Base.IsDisabled)
            {
                r.Base.Health -= dmg;
                attacker.DamageDealt += dmg;
                if (r.Base.Health <= 0)
                {
                    r.Base.Health = 0;
                    r.Base.DisabledTimer = Economy.BaseDisabledSeconds;
                    Emit(MatchEventType.BaseDisabled, r.OwnerId, attacker.Id, r.Id);
                }
            }
        }

        private void Kill(PlayerState victim, PlayerState killer)
        {
            victim.Health = 0;
            victim.Life = LifeState.Dead;
            victim.Deaths++;
            victim.LastDeathPosition = victim.Position;
            victim.Dots.Clear();
            victim.SuperCharge = 0f;

            if (killer != null && killer != victim)
            {
                killer.Kills++;
                if (victim.IsBot) killer.KillsBots++; else killer.KillsReal++;
                int steal = (int)Math.Floor(victim.Crystals * Economy.KillStealFraction);
                victim.Crystals -= steal;
                killer.Crystals += Economy.KillBounty + steal;
            }
            Emit(MatchEventType.Killed, victim.Id, killer?.Id ?? Neutral, pos: victim.Position);

            if (TowerCount(victim.Id) == 0)
            {
                Eliminate(victim);
                return;
            }
            float t = Rules.RespawnBaseSeconds + Rules.RespawnPerDeathSeconds * (victim.Deaths - 1);
            victim.RespawnTimer = Math.Min(t, Rules.RespawnMaxSeconds);
        }

        private void Respawn(PlayerState p)
        {
            // A regra é verificada de novo no momento do renascimento: a torre pode ter caído enquanto ele esperava.
            RegionState spawn = null;
            var home = GetRegion(p.HomeRegionId);
            if (home.OwnerId == p.Id) spawn = home;
            else
            {
                float best = float.MaxValue;
                foreach (var r in Regions)
                {
                    if (r.OwnerId != p.Id) continue;
                    float d = Vec2.SqrDistance(r.Def.TowerPosition, p.LastDeathPosition);
                    if (d < best) { best = d; spawn = r; }
                }
            }
            if (spawn == null) { Eliminate(p); return; }

            p.Life = LifeState.Alive;
            p.Health = MaxHealthOf(p);
            p.Ammo = p.Stats.MaxAmmo;
            p.ReloadTimer = 0f;
            p.AttackCooldown = 0f;
            p.SpawnProtection = Rules.SpawnProtectionSeconds;
            p.TimeSinceCombat = 0f;
            p.Position = SpawnPoint(spawn);
            Emit(MatchEventType.Respawned, p.Id, regionId: spawn.Id, pos: p.Position);
        }

        private void Eliminate(PlayerState p)
        {
            p.Life = LifeState.Eliminated;
            p.Health = 0;
            p.EliminatedAtTime = Time;
            Emit(MatchEventType.Eliminated, p.Id);
        }

        private bool ConsumePendingShot(int playerId, Vec2 hitPoint, float tolerance)
        {
            for (int i = 0; i < _pendingShots.Count; i++)
            {
                var s = _pendingShots[i];
                if (s.PlayerId != playerId) continue;
                if (Vec2.Distance(s.Origin, hitPoint) > s.Range + tolerance) continue;
                _pendingShots.RemoveAt(i);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ fim de partida

        private void CheckEnd()
        {
            int required = Rules.TowersRequiredForDomination > 0 ? Rules.TowersRequiredForDomination : Regions.Count;
            foreach (var p in Players)
            {
                if (p.IsEliminated) continue;
                if (TowerCount(p.Id) >= required && (!Rules.CenterRequiredForDomination || OwnsCenter(p.Id)))
                {
                    End(MatchEndReason.Domination, p.Id);
                    return;
                }
            }

            var remaining = new List<PlayerState>();
            bool anyHuman = false, anyHumanStanding = false;
            foreach (var p in Players)
            {
                if (!p.IsBot) { anyHuman = true; if (!p.IsEliminated) anyHumanStanding = true; }
                if (!p.IsEliminated) remaining.Add(p);
            }

            if (remaining.Count == 0) { End(MatchEndReason.AllEliminated, Neutral); return; }
            if (remaining.Count == 1) { End(MatchEndReason.LastStanding, remaining[0].Id); return; }
            if (Rules.EndWhenAllHumansEliminated && anyHuman && !anyHumanStanding)
            {
                End(MatchEndReason.NoHumanCanWin, TiebreakWinner(remaining));
                return;
            }
            if (Time >= Rules.TimeLimitSeconds) End(MatchEndReason.TimeLimit, TiebreakWinner(remaining));
        }

        /// <summary>Compara para desempate: mais torres, dono do centro, mais eliminações, mais dano.</summary>
        public int CompareForTiebreak(PlayerState a, PlayerState b)
        {
            int c = TowerCount(b.Id).CompareTo(TowerCount(a.Id)); if (c != 0) return c;
            c = OwnsCenter(b.Id).CompareTo(OwnsCenter(a.Id)); if (c != 0) return c;
            c = b.Kills.CompareTo(a.Kills); if (c != 0) return c;
            return b.DamageDealt.CompareTo(a.DamageDealt);
        }

        private int TiebreakWinner(List<PlayerState> candidates)
        {
            var sorted = new List<PlayerState>(candidates);
            sorted.Sort(CompareForTiebreak);
            if (sorted.Count >= 2 && CompareForTiebreak(sorted[0], sorted[1]) == 0) return Neutral; // empate total
            return sorted[0].Id;
        }

        private void End(MatchEndReason reason, int winnerId)
        {
            var standing = new List<PlayerState>();
            var eliminated = new List<PlayerState>();
            foreach (var p in Players) (p.IsEliminated ? eliminated : standing).Add(p);
            standing.Sort(CompareForTiebreak);
            eliminated.Sort((a, b) => b.EliminatedAtTime.CompareTo(a.EliminatedAtTime));

            var result = new MatchResult { Reason = reason, WinnerId = winnerId, EndTime = Time };
            if (winnerId != Neutral) result.Placement.Add(winnerId);
            foreach (var p in standing) if (p.Id != winnerId) result.Placement.Add(p.Id);
            foreach (var p in eliminated) if (p.Id != winnerId) result.Placement.Add(p.Id);
            Result = result;
            Emit(MatchEventType.MatchEnded, winnerId, amount: (int)reason);
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
