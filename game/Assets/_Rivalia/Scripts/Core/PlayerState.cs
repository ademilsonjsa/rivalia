using System;
using System.Collections.Generic;

namespace Rivalia.Core
{
    public enum LifeState { Alive, Dead, Eliminated }

    public class DotEffect
    {
        public int SourcePlayerId;
        public int DamagePerTick;
        public float Remaining;
        public float TickTimer;
    }

    /// <summary>Estado de um participante (humano ou bot) dentro da partida.</summary>
    public class PlayerState
    {
        public int Id;
        public string Name;
        public bool IsBot;
        public int HomeRegionId;
        public CharacterStats Stats;

        public LifeState Life = LifeState.Alive;
        public Vec2 Position;
        public int Health;
        public float RespawnTimer;
        public float SpawnProtection;
        public float TimeSinceCombat;
        public Vec2 LastDeathPosition;

        // Combate
        public int Ammo;
        public float ReloadTimer;
        public float AttackCooldown;
        public float AbilityCooldown;
        public float SuperCharge;       // 0..1
        public readonly List<DotEffect> Dots = new List<DotEffect>();

        // Economia e itens
        public int Crystals;
        public readonly Dictionary<ShopItemId, int> Items = new Dictionary<ShopItemId, int>();

        // Estatísticas da partida (alimentam MatchStatistics no backend)
        public int Kills, KillsReal, KillsBots, Deaths, TowersCaptured;
        public long DamageDealt;
        public int CrystalsMined;
        public float SecondsAlive;
        public float EliminatedAtTime = -1f;

        public bool IsAlive => Life == LifeState.Alive;
        public bool IsEliminated => Life == LifeState.Eliminated;

        public int ItemCount(ShopItemId id) => Items.TryGetValue(id, out var n) ? n : 0;
    }
}
