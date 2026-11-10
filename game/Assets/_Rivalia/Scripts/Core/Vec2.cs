using System;

namespace Rivalia.Core
{
    /// <summary>
    /// Ponto/vetor no plano XZ do mundo (X = leste, Z = norte). O núcleo não depende de UnityEngine;
    /// a ponte Unity converte Vector3(x, 0, z) para Vec2(x, z).
    /// </summary>
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Z;

        public Vec2(float x, float z) { X = x; Z = z; }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float Length => (float)Math.Sqrt(X * X + Z * Z);
        public float SqrLength => X * X + Z * Z;

        public Vec2 Normalized
        {
            get
            {
                float len = Length;
                return len < 1e-5f ? Zero : new Vec2(X / len, Z / len);
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Z * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Z * s);

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static float SqrDistance(Vec2 a, Vec2 b) => (a - b).SqrLength;
        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Z * b.Z;

        /// <summary>Move de <paramref name="from"/> em direção a <paramref name="to"/> no máximo <paramref name="maxStep"/>.</summary>
        public static Vec2 MoveTowards(Vec2 from, Vec2 to, float maxStep)
        {
            Vec2 delta = to - from;
            float dist = delta.Length;
            if (dist <= maxStep || dist < 1e-5f) return to;
            return from + delta * (maxStep / dist);
        }

        public bool Equals(Vec2 other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Z.GetHashCode();
        public override string ToString() => $"({X:0.##}, {Z:0.##})";
    }
}
