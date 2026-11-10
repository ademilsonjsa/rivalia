using System;
using System.Collections.Generic;

namespace Rivalia.Core
{
    /// <summary>Uma região do mapa. A região central só tem torre.</summary>
    [Serializable]
    public class RegionDefinition
    {
        public int Id;
        public string Code;
        public bool IsCenter;
        public Vec2 Center;         // centro da região
        public float Radius;        // área da região (defesa da Base Militar)
        public Vec2 TowerPosition;
        public float CaptureRadius = 5f;
        public Vec2 MinePosition;
        public Vec2 ShopPosition;
        public Vec2 BasePosition;
    }

    /// <summary>
    /// Descrição de um mapa. Mapas para 6 ou 8 jogadores são outra instância desta classe
    /// (no Unity: um asset MapDefinition), sem mudar o código das regras.
    /// </summary>
    [Serializable]
    public class MapDefinition
    {
        public string Code;
        public string DisplayName;
        public float HalfSize;      // arena quadrada de lado 2*HalfSize centrada na origem
        public List<RegionDefinition> Regions = new List<RegionDefinition>();

        public int PlayerRegionCount
        {
            get { int n = 0; foreach (var r in Regions) if (!r.IsCenter) n++; return n; }
        }

        /// <summary>
        /// "Vale das Quatro Coroas": arena 80 x 80 m, quatro regiões nos cantos e centro neutro.
        /// As estruturas ficam dentro do platô de cada região, com a Base Militar voltada para o centro.
        /// </summary>
        public static MapDefinition ValeDasQuatroCoroas()
        {
            var map = new MapDefinition { Code = "vale_quatro_coroas", DisplayName = "Vale das Quatro Coroas", HalfSize = 40f };
            string[] codes = { "NO", "NE", "SO", "SE" };
            float[,] signs = { { -1, 1 }, { 1, 1 }, { -1, -1 }, { 1, -1 } };
            for (int i = 0; i < 4; i++)
            {
                float sx = signs[i, 0], sz = signs[i, 1];
                var c = new Vec2(28f * sx, 28f * sz);
                map.Regions.Add(new RegionDefinition
                {
                    Id = i,
                    Code = codes[i],
                    IsCenter = false,
                    Center = c,
                    Radius = 14f,
                    TowerPosition = c,
                    MinePosition = c + new Vec2(7f * sx, -1f * sz),     // canto externo, longe do centro
                    ShopPosition = c + new Vec2(-1f * sx, 7f * sz),
                    BasePosition = c + new Vec2(-6f * sx, -6f * sz),    // voltada para o centro do mapa
                });
            }
            map.Regions.Add(new RegionDefinition
            {
                Id = 4,
                Code = "CENTRO",
                IsCenter = true,
                Center = Vec2.Zero,
                Radius = 10f,
                TowerPosition = Vec2.Zero,
                CaptureRadius = 5f,
            });
            return map;
        }
    }
}
