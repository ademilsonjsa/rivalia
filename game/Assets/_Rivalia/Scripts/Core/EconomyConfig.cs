using System;
using System.Collections.Generic;

namespace Rivalia.Core
{
    public enum StructureKind { Mine, Shop, Tower, MilitaryBase }

    public enum ShopItemId { SharpBlade, Cuirass, BronzePlate, SwiftBoots, Potion }

    [Serializable]
    public class MineLevel
    {
        public int UpgradeCost; public float ProductionPerSecond; public int Capacity; public int MaxHealth;
        public MineLevel(int cost, float prod, int cap, int hp) { UpgradeCost = cost; ProductionPerSecond = prod; Capacity = cap; MaxHealth = hp; }
    }

    [Serializable]
    public class BaseLevel
    {
        public int UpgradeCost; public int Damage; public float Range; public int MaxHealth;
        public BaseLevel(int cost, int dmg, float range, int hp) { UpgradeCost = cost; Damage = dmg; Range = range; MaxHealth = hp; }
    }

    [Serializable]
    public class TowerLevel
    {
        public int UpgradeCost; public float CaptureSeconds;
        public TowerLevel(int cost, float seconds) { UpgradeCost = cost; CaptureSeconds = seconds; }
    }

    [Serializable]
    public class ShopItem
    {
        public ShopItemId Id; public string Name; public int Price; public int MaxStacks;
        public ShopItem(ShopItemId id, string name, int price, int maxStacks) { Id = id; Name = name; Price = price; MaxStacks = maxStacks; }
    }

    /// <summary>
    /// Economia e balanceamento da partida (seção 6 do GDD). Tudo configurável: os níveis são listas,
    /// então árvores de melhoria maiores são só dados.
    /// </summary>
    [Serializable]
    public class EconomyConfig
    {
        public int StartingCrystals = 100;
        public float InteractRadius = 4f;          // coleta, loja e melhorias
        public int KillBounty = 40;
        public float KillStealFraction = 0.20f;
        public int TowerCaptureReward = 100;
        public float CenterMineBonus = 0.25f;      // +25% produção para o dono do centro
        public float CenterDamageBonus = 0.10f;    // +10% dano para o dono do centro

        public float MineSabotageSeconds = 15f;
        public float BaseDisabledSeconds = 30f;
        public float BaseFireInterval = 1.0f;

        // índice 0 = nível 1
        public List<MineLevel> MineLevels = new List<MineLevel>
        {
            new MineLevel(0,   3.0f, 150, 3000),
            new MineLevel(150, 4.5f, 250, 4000),
            new MineLevel(300, 6.5f, 400, 5000),
            new MineLevel(500, 9.0f, 600, 6000),
        };

        public List<BaseLevel> BaseLevels = new List<BaseLevel>
        {
            new BaseLevel(0,   400,  9f, 4000),
            new BaseLevel(120, 520, 10f, 5000),
            new BaseLevel(250, 680, 11f, 6000),
            new BaseLevel(450, 880, 12f, 7000),
        };

        public List<TowerLevel> TowerLevels = new List<TowerLevel>
        {
            new TowerLevel(0,    8f),
            new TowerLevel(100, 10f),
            new TowerLevel(220, 12f),
            new TowerLevel(400, 14f),
        };

        public float CenterCaptureSeconds = 12f;

        public List<ShopItem> ShopItems = new List<ShopItem>
        {
            new ShopItem(ShopItemId.SharpBlade,  "Lâmina Afiada", 120, 3),
            new ShopItem(ShopItemId.Cuirass,     "Couraça",       120, 3),
            new ShopItem(ShopItemId.BronzePlate, "Placa de Bronze", 150, 2),
            new ShopItem(ShopItemId.SwiftBoots,  "Botas Ligeiras", 100, 2),
            new ShopItem(ShopItemId.Potion,      "Poção",          50, 2),
        };

        // Efeitos dos itens
        public float BladeDamageBonus = 0.15f;
        public float CuirassHealthBonus = 0.20f;
        public float PlateArmorBonus = 0.10f;
        public float BootsSpeedBonus = 0.08f;
        public float PotionHealFraction = 0.40f;

        public float MaxDamageBonus = 0.45f;
        public float MaxArmor = 0.50f;

        public ShopItem GetItem(ShopItemId id)
        {
            foreach (var i in ShopItems) if (i.Id == id) return i;
            return null;
        }
    }
}
