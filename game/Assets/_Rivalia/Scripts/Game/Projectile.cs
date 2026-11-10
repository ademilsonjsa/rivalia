using UnityEngine;

namespace Rivalia.Game
{
    /// <summary>
    /// Projétil com colisão por SphereCast. Muros e estruturas bloqueiam; coberturas baixas (LowCover) não.
    /// Ao colidir, informa o diretor, que pede ao núcleo para validar e aplicar o dano.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private MatchDirector _director;
        private HeroActor _owner;
        private Vector3 _dir;
        private float _speed, _remaining, _radius;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public static void Spawn(MatchDirector director, HeroActor owner, Vector3 origin, Vector3 dir, float speed, float range, float radius, Color color)
        {
            var go = GameVisuals.Prim(PrimitiveType.Sphere, "Projetil", null, origin, Vector3.one * radius * 2f, color, keepCollider: false);
            var p = go.AddComponent<Projectile>();
            p._director = director; p._owner = owner; p._dir = dir.normalized;
            p._speed = speed; p._remaining = range; p._radius = radius;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f; trail.startWidth = radius * 1.6f; trail.endWidth = 0f;
            trail.material = GameVisuals.Mat(color);
        }

        private void Update()
        {
            if (_director == null || _director.Match.IsOver) { Destroy(gameObject); return; }
            float step = Mathf.Min(_speed * Time.deltaTime, _remaining);
            int n = Physics.SphereCastNonAlloc(transform.position, _radius, _dir, Hits, step, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(Hits, 0, n, HitComparer.Instance);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                if (col.GetComponentInParent<LowCover>() != null) continue;           // passa por cima
                var hero = col.GetComponentInParent<HeroActor>();
                if (hero == _owner) continue;
                if (hero != null)
                {
                    if (!hero.IsAlive) continue;
                    _director.OnProjectileHitHero(_owner, hero, Hits[i].point);
                    Destroy(gameObject);
                    return;
                }
                var structure = col.GetComponentInParent<StructureView>();
                if (structure != null) _director.OnProjectileHitStructure(_owner, structure, Hits[i].point);
                Destroy(gameObject); // muro, borda ou estrutura
                return;
            }
            transform.position += _dir * step;
            _remaining -= step;
            if (_remaining <= 0.001f) Destroy(gameObject);
        }

        private class HitComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new HitComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }

    /// <summary>Efeitos sonoros sintetizados (provisórios, sem arquivos de áudio de terceiros).</summary>
    public static class Sfx
    {
        public static AudioClip Shoot, Hit, Dash, Capture, Death, Coin;

        public static void Init()
        {
            if (Shoot != null) return;
            Shoot = Tone("tiro", 880f, 0.07f, 0.25f, 1.6f);
            Hit = Tone("acerto", 220f, 0.08f, 0.35f, 0.6f);
            Dash = Tone("avanco", 500f, 0.12f, 0.2f, 2.2f);
            Capture = Tone("captura", 660f, 0.35f, 0.3f, 1.5f);
            Death = Tone("morte", 160f, 0.4f, 0.35f, 0.4f);
            Coin = Tone("moeda", 1320f, 0.08f, 0.2f, 1.2f);
        }

        public static void Play(AudioClip clip, Vector3 pos)
        {
            if (clip != null && Camera.main != null)
                AudioSource.PlayClipAtPoint(clip, Vector3.Lerp(Camera.main.transform.position, pos, 0.3f));
        }

        private static AudioClip Tone(string name, float freq, float seconds, float volume, float sweep)
        {
            const int rate = 22050;
            int n = (int)(rate * seconds);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = freq * Mathf.Lerp(1f, sweep, t);
                phase += 2f * Mathf.PI * f / rate;
                data[i] = Mathf.Sin(phase) * volume * (1f - t);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
