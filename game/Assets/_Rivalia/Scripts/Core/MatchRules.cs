using System;

namespace Rivalia.Core
{
    /// <summary>Regras gerais da partida (seções 2.3 a 2.6 e 4.4 do GDD).</summary>
    [Serializable]
    public class MatchRules
    {
        public float TimeLimitSeconds = 480f;          // 8 minutos

        // Vitória por domínio: quantas torres (0 = todas) e se o centro é obrigatório.
        public int TowersRequiredForDomination = 0;
        public bool CenterRequiredForDomination = true;

        /// <summary>
        /// Encerramento por impossibilidade de vitória: no single-player, se todos os humanos forem
        /// eliminados nenhum deles pode mais vencer, então a partida termina e o vencedor sai do desempate.
        /// </summary>
        public bool EndWhenAllHumansEliminated = true;

        // Captura
        public float DecayPerSecondEmpty = 15f;        // % por segundo sem ninguém
        public float DecayPerSecondOwner = 30f;        // % por segundo com o dono sozinho

        // Morte e renascimento
        public float RespawnBaseSeconds = 3f;
        public float RespawnPerDeathSeconds = 1f;
        public float RespawnMaxSeconds = 8f;
        public float SpawnProtectionSeconds = 1.5f;

        // Regeneração
        public float RegenDelaySeconds = 3f;
        public float RegenFractionPerSecond = 0.10f;

        // Dano contínuo
        public float DotTickSeconds = 0.5f;

        /// <summary>Tolerância (m) ao validar se um acerto reportado é plausível.</summary>
        public float HitValidationTolerance = 1.5f;
    }
}
