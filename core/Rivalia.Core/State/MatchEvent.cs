namespace Rivalia.Core
{
    public enum MatchEventType
    {
        Damaged, Killed, Respawned, Eliminated,
        CaptureStarted, CaptureContested, TowerCaptured,
        MineCollected, MineSabotaged, MineRepaired,
        BaseFired, BaseDisabled, BaseRestored,
        Upgraded, Purchased, PotionUsed, AbilityUsed, SuperUsed,
        MatchEnded,
    }

    /// <summary>
    /// Evento emitido pelo núcleo para a apresentação (VFX, som, HUD) e, no multiplayer, para a rede.
    /// </summary>
    public struct MatchEvent
    {
        public MatchEventType Type;
        public float Time;
        public int PlayerId;        // quem sofreu / quem agiu
        public int OtherId;         // atacante, novo dono, etc.
        public int RegionId;
        public int Amount;
        public Vec2 Position;

        public override string ToString() =>
            $"[{Time,6:0.0}s] {Type} player={PlayerId} other={OtherId} region={RegionId} amount={Amount}";
    }

    public enum MatchEndReason { None, Domination, LastStanding, TimeLimit, AllEliminated, NoHumanCanWin }

    public class MatchResult
    {
        public MatchEndReason Reason;
        public int WinnerId = Match.Neutral;   // Neutral = empate
        public float EndTime;
        public bool IsDraw => WinnerId == Match.Neutral;
        /// <summary>Ids na ordem de colocação (1º primeiro).</summary>
        public System.Collections.Generic.List<int> Placement = new System.Collections.Generic.List<int>();
    }
}
