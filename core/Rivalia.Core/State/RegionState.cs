namespace Rivalia.Core
{
    public class MineState
    {
        public int Level = 1;
        public float Stored;
        public int Health;
        public float SabotageTimer;     // > 0 = sabotada (não produz)
        public bool IsSabotaged => SabotageTimer > 0f;
    }

    public class MilitaryBaseState
    {
        public int Level = 1;
        public int Health;
        public float DisabledTimer;     // > 0 = inativa
        public float FireCooldown;
        public bool IsDisabled => DisabledTimer > 0f;
    }

    /// <summary>
    /// Estado de uma região. A propriedade fica aqui (na torre), nunca no personagem:
    /// sair da região não muda o dono.
    /// </summary>
    public class RegionState
    {
        public RegionDefinition Def;
        public int OwnerId = Match.Neutral;
        public int TowerLevel = 1;

        // Captura progressiva 0..100
        public float CaptureProgress;
        public int CapturingPlayerId = Match.Neutral;
        public bool IsContested;

        public MineState Mine;               // nulo na região central
        public MilitaryBaseState Base;       // nulo na região central

        public int Id => Def.Id;
        public bool IsCenter => Def.IsCenter;
    }
}
