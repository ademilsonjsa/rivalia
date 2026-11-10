using Rivalia.Core;
using UnityEngine;

namespace Rivalia.Game
{
    /// <summary>Conversões entre o núcleo (plano XZ) e o Unity, cores de time e fábrica de materiais.</summary>
    public static class GameVisuals
    {
        public static readonly Color Neutral = new Color(0.62f, 0.62f, 0.66f);
        public static readonly Color[] TeamColors =
        {
            new Color(0.16f, 0.55f, 1.00f), // azul
            new Color(1.00f, 0.28f, 0.30f), // vermelho
            new Color(0.25f, 0.85f, 0.35f), // verde
            new Color(1.00f, 0.80f, 0.15f), // amarelo
        };

        public static Color Team(int playerId) =>
            playerId >= 0 && playerId < TeamColors.Length ? TeamColors[playerId] : Neutral;

        public static Vector3 ToV3(Vec2 v, float y = 0f) => new Vector3(v.X, y, v.Z);
        public static Vec2 ToV2(Vector3 v) => new Vec2(v.x, v.z);
        public static Vec2 ToV2(Vector2 v) => new Vec2(v.x, v.y);

        private static Material _base;

        /// <summary>Material colorido que funciona tanto no pipeline padrão quanto no URP.</summary>
        public static Material Mat(Color c)
        {
            if (_base == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _base = new Material(tmp.GetComponent<Renderer>().sharedMaterial);
                Object.Destroy(tmp);
            }
            var m = new Material(_base) { color = c };
            return m;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        public static void Tint(GameObject go, Color c)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r.GetComponent<TeamTintable>() != null) r.material.color = c;
        }
    }

    /// <summary>Marca renderers que mudam de cor com o dono (bandeiras, faixas).</summary>
    public class TeamTintable : MonoBehaviour { }

    /// <summary>Obstáculo baixo: bloqueia movimento, mas projéteis passam por cima.</summary>
    public class LowCover : MonoBehaviour { }
}
