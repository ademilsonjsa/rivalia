using System.Collections.Generic;
using Rivalia.Core;
using UnityEngine;

namespace Rivalia.Game
{
    /// <summary>Identifica uma estrutura atingível por projéteis.</summary>
    public class StructureView : MonoBehaviour
    {
        public int RegionId;
        public StructureKind Kind;
    }

    /// <summary>
    /// Gera a arena 3D a partir de um MapDefinition: chão, bordas, muros (bloqueiam tudo), coberturas baixas
    /// (bloqueiam movimento, não projéteis), vegetação decorativa e as estruturas de cada região.
    /// Arte provisória com primitivas; a troca por modelos finais é feita nos prefabs sem mudar as regras.
    /// </summary>
    public static class ArenaBuilder
    {
        public const float WallHeight = 2.6f;

        public static List<RegionView> Build(MapDefinition map, Transform root)
        {
            float h = map.HalfSize;
            // Chão
            GameVisuals.Prim(PrimitiveType.Cube, "Chao", root, new Vector3(0, -0.25f, 0), new Vector3(h * 2, 0.5f, h * 2), new Color(0.55f, 0.78f, 0.45f));

            // Bordas da arena
            var wallColor = new Color(0.55f, 0.45f, 0.38f);
            GameVisuals.Prim(PrimitiveType.Cube, "Borda N", root, new Vector3(0, WallHeight / 2, h + 0.75f), new Vector3(h * 2 + 3, WallHeight, 1.5f), wallColor);
            GameVisuals.Prim(PrimitiveType.Cube, "Borda S", root, new Vector3(0, WallHeight / 2, -h - 0.75f), new Vector3(h * 2 + 3, WallHeight, 1.5f), wallColor);
            GameVisuals.Prim(PrimitiveType.Cube, "Borda L", root, new Vector3(h + 0.75f, WallHeight / 2, 0), new Vector3(1.5f, WallHeight, h * 2), wallColor);
            GameVisuals.Prim(PrimitiveType.Cube, "Borda O", root, new Vector3(-h - 0.75f, WallHeight / 2, 0), new Vector3(1.5f, WallHeight, h * 2), wallColor);

            // Obstáculos espelhados nos 4 quadrantes (x, z, largura, profundidade, baixo?)
            var quadrant = new (float x, float z, float w, float d, bool low)[]
            {
                (9f, 20f, 6f, 1.5f, false),
                (20f, 9f, 1.5f, 6f, false),
                (5f, 32f, 1.5f, 8f, false),
                (32f, 5f, 8f, 1.5f, false),
                (16f, 16f, 3f, 3f, true),
                (24f, 14f, 2f, 2f, true),
                (14f, 24f, 2f, 2f, true),
                (6.5f, 6.5f, 1.5f, 1.5f, true),
            };
            var obstacles = new GameObject("Obstaculos").transform;
            obstacles.SetParent(root, false);
            int[] sx = { 1, -1, 1, -1 }, sz = { 1, 1, -1, -1 };
            for (int q = 0; q < 4; q++)
            {
                foreach (var o in quadrant)
                {
                    var pos = new Vector3(o.x * sx[q], 0, o.z * sz[q]);
                    if (o.low)
                    {
                        var c = GameVisuals.Prim(PrimitiveType.Cube, "Caixa", obstacles, pos + Vector3.up * 0.5f, new Vector3(o.w, 1f, o.d), new Color(0.85f, 0.6f, 0.3f));
                        c.AddComponent<LowCover>();
                    }
                    else
                    {
                        GameVisuals.Prim(PrimitiveType.Cube, "Muro", obstacles, pos + Vector3.up * (WallHeight / 2), new Vector3(o.w, WallHeight, o.d), wallColor);
                    }
                }
            }

            // Vegetação decorativa (sem colisão no MVP)
            var rng = new System.Random(42);
            var deco = new GameObject("Vegetacao").transform;
            deco.SetParent(root, false);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 2 - 1) * (h - 2), 0, (float)(rng.NextDouble() * 2 - 1) * (h - 2));
                if (NearAnyStructure(map, p)) continue;
                float s = 0.6f + (float)rng.NextDouble() * 0.8f;
                GameVisuals.Prim(PrimitiveType.Sphere, "Arbusto", deco, p + Vector3.up * s * 0.4f, new Vector3(s, s * 0.8f, s), new Color(0.2f, 0.6f, 0.25f), keepCollider: false);
            }

            var views = new List<RegionView>();
            foreach (var r in map.Regions) views.Add(BuildRegion(r, root));
            return views;
        }

        private static bool NearAnyStructure(MapDefinition map, Vector3 p)
        {
            foreach (var r in map.Regions)
            {
                var v = GameVisuals.ToV2(p);
                if (Vec2.Distance(v, r.TowerPosition) < 7f) return true;
                if (!r.IsCenter && (Vec2.Distance(v, r.MinePosition) < 3f || Vec2.Distance(v, r.ShopPosition) < 3f || Vec2.Distance(v, r.BasePosition) < 3f)) return true;
            }
            return false;
        }

        private static RegionView BuildRegion(RegionDefinition def, Transform root)
        {
            var go = new GameObject("Regiao " + def.Code);
            go.transform.SetParent(root, false);
            var view = go.AddComponent<RegionView>();
            view.RegionId = def.Id;

            // Piso da região (muda de cor com o dono)
            var floor = GameVisuals.Prim(PrimitiveType.Cylinder, "Piso", go.transform, GameVisuals.ToV3(def.Center, 0.01f), new Vector3(def.Radius * 2, 0.01f, def.Radius * 2), GameVisuals.Neutral, keepCollider: false);
            floor.AddComponent<TeamTintable>();
            view.Floor = floor.GetComponent<Renderer>();

            // Torre + área de captura
            var tower = GameVisuals.Prim(PrimitiveType.Cylinder, "Torre", go.transform, GameVisuals.ToV3(def.TowerPosition, 2f), new Vector3(2.6f, 2f, 2.6f), new Color(0.9f, 0.88f, 0.82f));
            tower.AddComponent<StructureView>().Kind = StructureKind.Tower;
            tower.GetComponent<StructureView>().RegionId = def.Id;
            var flag = GameVisuals.Prim(PrimitiveType.Cube, "Bandeira", tower.transform, new Vector3(0.3f, 1.6f, 0), new Vector3(0.5f, 0.6f, 0.08f), GameVisuals.Neutral, keepCollider: false);
            flag.AddComponent<TeamTintable>();
            view.Flag = flag.GetComponent<Renderer>();
            var crown = GameVisuals.Prim(PrimitiveType.Sphere, "Coroa", tower.transform, new Vector3(0, 1.3f, 0), new Vector3(0.7f, 0.5f, 0.7f), GameVisuals.Neutral, keepCollider: false);
            crown.AddComponent<TeamTintable>();
            view.Crown = crown.GetComponent<Renderer>();

            var ring = GameVisuals.Prim(PrimitiveType.Cylinder, "AreaCaptura", go.transform, GameVisuals.ToV3(def.TowerPosition, 0.03f), new Vector3(def.CaptureRadius * 2, 0.01f, def.CaptureRadius * 2), new Color(1, 1, 1), keepCollider: false);
            view.Ring = ring.GetComponent<Renderer>();
            var progress = GameVisuals.Prim(PrimitiveType.Cylinder, "ProgressoCaptura", go.transform, GameVisuals.ToV3(def.TowerPosition, 0.05f), new Vector3(0.01f, 0.01f, 0.01f), Color.white, keepCollider: false);
            view.ProgressDisc = progress.transform;
            view.CaptureRadius = def.CaptureRadius;

            if (!def.IsCenter)
            {
                // Mina
                var mine = GameVisuals.Prim(PrimitiveType.Cube, "Mina", go.transform, GameVisuals.ToV3(def.MinePosition, 0.9f), new Vector3(2.6f, 1.8f, 2.6f), new Color(0.45f, 0.35f, 0.3f));
                mine.AddComponent<StructureView>().Kind = StructureKind.Mine;
                mine.GetComponent<StructureView>().RegionId = def.Id;
                var crystal = GameVisuals.Prim(PrimitiveType.Cube, "Cristal", mine.transform, new Vector3(0, 0.9f, 0), new Vector3(0.35f, 0.6f, 0.35f), new Color(0.4f, 0.95f, 1f), keepCollider: false);
                crystal.transform.localRotation = Quaternion.Euler(0, 45, 20);
                view.MineCrystal = crystal.transform;

                // Loja
                var shop = GameVisuals.Prim(PrimitiveType.Cube, "Loja", go.transform, GameVisuals.ToV3(def.ShopPosition, 1.1f), new Vector3(2.8f, 2.2f, 2.8f), new Color(0.95f, 0.9f, 0.75f));
                shop.AddComponent<StructureView>().Kind = StructureKind.Shop;
                shop.GetComponent<StructureView>().RegionId = def.Id;
                var awning = GameVisuals.Prim(PrimitiveType.Cube, "Toldo", shop.transform, new Vector3(0, 0.6f, 0), new Vector3(1.15f, 0.12f, 1.15f), GameVisuals.Neutral, keepCollider: false);
                awning.AddComponent<TeamTintable>();

                // Base Militar
                var baseGo = GameVisuals.Prim(PrimitiveType.Cylinder, "BaseMilitar", go.transform, GameVisuals.ToV3(def.BasePosition, 0.8f), new Vector3(2.4f, 0.8f, 2.4f), new Color(0.5f, 0.52f, 0.58f));
                baseGo.AddComponent<StructureView>().Kind = StructureKind.MilitaryBase;
                baseGo.GetComponent<StructureView>().RegionId = def.Id;
                var head = GameVisuals.Prim(PrimitiveType.Cube, "Canhao", baseGo.transform, new Vector3(0, 1.3f, 0), new Vector3(0.5f, 0.6f, 0.5f), GameVisuals.Neutral, keepCollider: false);
                head.AddComponent<TeamTintable>();
                var barrel = GameVisuals.Prim(PrimitiveType.Cube, "Cano", head.transform, new Vector3(0, 0, 0.8f), new Vector3(0.3f, 0.3f, 1.2f), new Color(0.2f, 0.2f, 0.22f), keepCollider: false);
                view.TurretHead = head.transform;
                view.BaseBody = baseGo;
                view.MineBody = mine;
            }
            return view;
        }
    }
}
