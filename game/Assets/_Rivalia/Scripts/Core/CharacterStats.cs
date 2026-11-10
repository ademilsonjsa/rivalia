using System;

namespace Rivalia.Core
{
    public enum CharacterRole { ShortRange, LongRange, Defense, Support, Mobility, AreaControl }
    public enum Rarity { Common, Rare, Epic, Mythic, Legendary }

    /// <summary>
    /// Atributos de combate de um herói (nível 1). No Unity vem de um CharacterDefinition (ScriptableObject);
    /// no servidor, das tabelas Characters/CharacterAttributes. Nenhum sistema depende de um herói específico.
    /// </summary>
    [Serializable]
    public class CharacterStats
    {
        public string Code = "tito_faisca";
        public string DisplayName = "Tito Faísca";
        public CharacterRole Role = CharacterRole.LongRange;
        public Rarity Rarity = Rarity.Common;

        public int MaxHealth = 3200;
        public float Armor = 0f;               // 0..1 (redução de dano)
        public float MoveSpeed = 6.0f;         // m/s

        // Ataque básico
        public int AttackDamage = 640;
        public float AttackRange = 9f;         // m
        public float AttackInterval = 0.45f;   // s mínimos entre disparos
        public int MaxAmmo = 3;
        public float ReloadPerAmmo = 1.4f;     // s por carga
        public float ProjectileSpeed = 18f;    // m/s
        public float ProjectileRadius = 0.35f; // m
        public float SuperChargePerHit = 0.25f;

        // Habilidade especial (avanço)
        public float AbilityCooldown = 8f;
        public float AbilityDashDistance = 5f;

        // Super (área com dano contínuo)
        public float SuperRange = 8f;
        public float SuperRadius = 3f;
        public int SuperImpactDamage = 1200;
        public int SuperDotDamagePerSecond = 200;
        public float SuperDotDuration = 3f;

        /// <summary>Progressão permanente: +5% vida e dano por nível acima de 1 (máx. 10).</summary>
        public CharacterStats AtLevel(int level)
        {
            level = Math.Max(1, Math.Min(10, level));
            var s = (CharacterStats)MemberwiseClone();
            float mult = 1f + 0.05f * (level - 1);
            s.MaxHealth = (int)Math.Round(MaxHealth * mult);
            s.AttackDamage = (int)Math.Round(AttackDamage * mult);
            s.SuperImpactDamage = (int)Math.Round(SuperImpactDamage * mult);
            s.SuperDotDamagePerSecond = (int)Math.Round(SuperDotDamagePerSecond * mult);
            return s;
        }

        public static CharacterStats TitoFaisca() => new CharacterStats();
    }
}
