using Rivalia.Core;
using Rivalia.Core.AI;
using UnityEngine;
using UnityEngine.AI;

namespace Rivalia.Game
{
    /// <summary>
    /// Camada tática do bot. Recebe o objetivo da camada estratégica (BotStrategist, no núcleo), navega
    /// pelo NavMesh, mira com erro e antecipação conforme a dificuldade, atira só com linha de visão,
    /// esquiva lateralmente e usa avanço, super, poção, loja e melhorias. Usa o MESMO HeroActor do jogador.
    /// </summary>
    public class BotPilot : MonoBehaviour
    {
        public HeroActor Hero;
        public MatchDirector Director;
        public BotStrategist Brain;

        public BotGoal CurrentGoal { get; private set; }

        private float _thinkTimer, _repathTimer, _reaction, _strafeTimer;
        private int _strafeSign = 1, _corner;
        private NavMeshPath _path;
        private Vector3 _lastDestination = new Vector3(9999, 0, 9999);
        private float _aimError, _reactionTime, _lead;
        private HeroActor _engaged;
        private static readonly RaycastHit[] LosHits = new RaycastHit[8];

        private void Start()
        {
            _path = new NavMeshPath();
            BotStrategist.AimParameters(Brain.Difficulty, out _aimError, out _reactionTime, out _lead);
            _thinkTimer = Random.value;
        }

        private void Update()
        {
            var match = Director?.Match;
            if (match == null || Hero == null) return;
            var me = match.GetPlayer(Hero.PlayerId);
            if (!me.IsAlive || match.IsOver) { Hero.SetMove(Vector2.zero); return; }
            float dt = Time.deltaTime;

            // ---------- estratégia (1x por segundo, ou logo após comprar/melhorar)
            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = 1f;
                CurrentGoal = Brain.Decide(match, me);
            }
            var goal = CurrentGoal;

            // ---------- destino
            Vector3 dest = GameVisuals.ToV3(goal.Destination);
            float stopAt = 0.4f;
            if (goal.Kind == BotGoalKind.HuntPlayer)
            {
                var t = Director.HeroOf(goal.TargetPlayerId);
                if (t != null && t.IsAlive) { dest = t.transform.position; stopAt = me.Stats.AttackRange * 0.7f; }
            }

            // ---------- combate (escolhe alvo visível mais próximo)
            HeroActor enemy = NearestVisibleEnemy(me.Stats.AttackRange + 2f, out float enemyDist);
            Vector2 moveDir = SteerAlongPath(dest, stopAt, dt);

            if (enemy != null)
            {
                if (_engaged != enemy) { _engaged = enemy; _reaction = _reactionTime; }
                _reaction -= dt;

                // esquiva lateral enquanto troca tiros
                _strafeTimer -= dt;
                if (_strafeTimer <= 0f) { _strafeTimer = Random.Range(0.4f, 1.1f); _strafeSign = Random.value < 0.5f ? -1 : 1; }
                var to = enemy.transform.position - Hero.transform.position; to.y = 0f;
                var side = Vector3.Cross(Vector3.up, to.normalized) * _strafeSign;
                if (goal.Kind == BotGoalKind.HuntPlayer || goal.Kind == BotGoalKind.DefendTower || enemyDist < me.Stats.AttackRange)
                    moveDir = Vector2.ClampMagnitude(moveDir * 0.6f + new Vector2(side.x, side.z) * 0.6f, 1f);

                if (_reaction <= 0f && enemyDist <= me.Stats.AttackRange)
                {
                    Vector3 aim = PredictAim(enemy, me.Stats.ProjectileSpeed);
                    aim = Quaternion.Euler(0f, Random.Range(-_aimError, _aimError), 0f) * aim;
                    Hero.Fire(aim);
                }
                else Hero.SetAim(to);

                if (me.SuperCharge >= 1f && enemyDist <= me.Stats.SuperRange) Hero.UseSuper(enemy.transform.position);

                float hp = me.Health / (float)match.MaxHealthOf(me);
                if (hp < 0.35f && me.AbilityCooldown <= 0f) Hero.UseAbility(-to); // foge com o avanço
                if (hp < 0.4f) match.TryUsePotion(me.Id);
            }
            else
            {
                _engaged = null;
                if (moveDir.sqrMagnitude > 0.01f) Hero.SetAim(new Vector3(moveDir.x, 0, moveDir.y));
                ShootEnemyStructureIfUseful(match, me, goal);
            }

            Hero.SetMove(moveDir);

            // ---------- interações econômicas
            float distToGoal = Vector3.Distance(Flat(Hero.transform.position), Flat(GameVisuals.ToV3(goal.Destination)));
            if (goal.Kind == BotGoalKind.Upgrade && distToGoal < 3.5f)
            {
                Director.RequestUpgrade(me.Id, goal.RegionId, goal.Structure);
                _thinkTimer = 0f;
            }
            else if (goal.Kind == BotGoalKind.Shop && distToGoal < 3.5f)
            {
                Director.RequestBuy(me.Id, goal.RegionId, goal.Item);
                _thinkTimer = 0f;
            }
        }

        private Vector2 SteerAlongPath(Vector3 dest, float stopAt, float dt)
        {
            var pos = Hero.transform.position;
            if (Vector3.Distance(Flat(pos), Flat(dest)) <= stopAt) return Vector2.zero;

            _repathTimer -= dt;
            if (_repathTimer <= 0f || (dest - _lastDestination).sqrMagnitude > 4f)
            {
                _repathTimer = 0.4f;
                _lastDestination = dest;
                bool ok = NavMesh.SamplePosition(pos, out var fromHit, 2f, NavMesh.AllAreas) &&
                          NavMesh.SamplePosition(dest, out var toHit, 3f, NavMesh.AllAreas) &&
                          NavMesh.CalculatePath(fromHit.position, toHit.position, NavMesh.AllAreas, _path);
                if (!ok) _path.ClearCorners();
                _corner = 1;
            }

            Vector3 next = dest;
            var corners = _path.corners;
            if (corners != null && corners.Length > 1)
            {
                while (_corner < corners.Length - 1 && Vector3.Distance(Flat(pos), Flat(corners[_corner])) < 0.6f) _corner++;
                next = corners[Mathf.Min(_corner, corners.Length - 1)];
            }
            var d = Flat(next) - Flat(pos);
            return d.sqrMagnitude < 1e-4f ? Vector2.zero : new Vector2(d.x, d.z).normalized;
        }

        private Vector3 PredictAim(HeroActor target, float projectileSpeed)
        {
            var to = target.transform.position - Hero.transform.position; to.y = 0f;
            float time = to.magnitude / Mathf.Max(projectileSpeed, 0.1f);
            var predicted = to + target.Velocity * time * _lead;
            return predicted;
        }

        private HeroActor NearestVisibleEnemy(float range, out float dist)
        {
            HeroActor best = null;
            dist = float.MaxValue;
            foreach (var h in Director.Heroes)
            {
                if (h == Hero || !h.IsAlive) continue;
                if (Director.Match.GetPlayer(h.PlayerId).SpawnProtection > 0f) continue;
                float d = Vector3.Distance(Flat(h.transform.position), Flat(Hero.transform.position));
                if (d > range || d >= dist) continue;
                if (Blocked(Hero.transform.position, h.transform.position)) continue;
                best = h; dist = d;
            }
            return best;
        }

        private void ShootEnemyStructureIfUseful(Match match, PlayerState me, BotGoal goal)
        {
            if (goal.Kind != BotGoalKind.CaptureTower && goal.Kind != BotGoalKind.HuntPlayer) return;
            foreach (var r in match.Regions)
            {
                if (r.IsCenter || r.OwnerId == me.Id || r.Base.IsDisabled) continue;
                var basePos = GameVisuals.ToV3(r.Def.BasePosition, 1f);
                if (Vector3.Distance(Flat(basePos), Flat(Hero.transform.position)) > me.Stats.AttackRange) continue;
                Hero.Fire(basePos - Hero.transform.position);
                return;
            }
        }

        /// <summary>Há muro ou estrutura entre os dois pontos? Coberturas baixas e heróis não bloqueiam a visão.</summary>
        public static bool Blocked(Vector3 from, Vector3 to)
        {
            from.y = 1f; to.y = 1f;
            var dir = to - from;
            int n = Physics.RaycastNonAlloc(from, dir.normalized, LosHits, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = LosHits[i].collider;
                if (c.GetComponentInParent<HeroActor>() != null || c.GetComponentInParent<LowCover>() != null) continue;
                return true;
            }
            return false;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
