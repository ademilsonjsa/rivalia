using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rivalia.Core;
using Rivalia.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rivalia.Tests
{
    /// <summary>
    /// Testes no próprio Unity (Window > General > Test Runner > PlayMode). Sobem a arena real com física,
    /// NavMesh e os 4 heróis controlados por bots, aceleram o tempo e verificam os critérios do MVP.
    /// </summary>
    public class MvpPlayModeTests
    {
        private RivaliaBootstrap _boot;
        private System.Collections.Generic.HashSet<GameObject> _before;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _before = new System.Collections.Generic.HashSet<GameObject>(Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None));
            _boot = new GameObject("Bootstrap").AddComponent<RivaliaBootstrap>();
            _boot.AutoPlayLocalPlayer = true;
            yield return null; // Start()
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            // destrói só o que o teste criou (o executor de testes também tem objetos na cena)
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.transform.parent == null && !_before.Contains(go)) Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hero_moves_in_any_direction_and_aims_independently()
        {
            var d = _boot.Director;
            var hero = d.HeroOf(0);
            Object.Destroy(hero.GetComponent<BotPilot>());
            yield return null;

            var start = hero.transform.position;
            hero.SetMove(new Vector2(0.7071f, -0.7071f)); // diagonal sudeste
            hero.SetAim(new Vector3(-1f, 0f, 0f));         // mira para oeste
            yield return new WaitForSeconds(0.5f);
            var moved = hero.transform.position - start;
            Assert.Greater(moved.x, 0.5f);
            Assert.Less(moved.z, -0.5f);
            Assert.AreEqual(-1f, hero.AimDirection.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator Projectile_follows_aim_and_walls_block_movement()
        {
            var d = _boot.Director;
            var hero = d.HeroOf(0);
            Object.Destroy(hero.GetComponent<BotPilot>());
            yield return null;
            hero.Teleport(new Vector3(-23f, 0f, 9f));      // a oeste do muro em x = -20,75 (z de 6 a 12)
            yield return new WaitForSeconds(0.6f);         // passa a proteção de renascimento dos outros

            Assert.IsTrue(hero.Fire(new Vector3(0f, 0f, -1f)));
            yield return null;
            // o projétil mais próximo do herói é o dele (os bots também atiram)
            var proj = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                .OrderBy(x => Vector3.Distance(x.transform.position, hero.transform.position)).FirstOrDefault();
            Assert.IsNotNull(proj);
            var p0 = proj.transform.position;
            yield return new WaitForSeconds(0.1f);
            if (proj != null) Assert.Less(proj.transform.position.z, p0.z);

            hero.SetMove(new Vector2(1f, 0f));             // empurra para leste, contra o muro
            yield return new WaitForSeconds(1.5f);
            hero.SetMove(Vector2.zero);
            Assert.Less(hero.transform.position.x, -21f);   // o CharacterController parou no muro
        }

        [UnityTest]
        public IEnumerator Full_match_with_four_bots_ends_with_valid_result()
        {
            var m = _boot.Director.Match;
            Time.timeScale = 8f;
            float realStart = Time.realtimeSinceStartup;
            while (!m.IsOver && Time.realtimeSinceStartup - realStart < 240f) yield return null;
            Time.timeScale = 1f;

            Assert.IsTrue(m.IsOver, "a partida não terminou");
            Assert.AreEqual(4, m.Result.Placement.Distinct().Count());
            Assert.Greater(m.Players.Sum(p => p.DamageDealt), 0, "ninguém causou dano");
            Debug.Log($"Fim: {m.Result.Reason}, vencedor {m.Result.WinnerId}, tempo {m.Result.EndTime:0}s, " +
                      $"capturas {m.Players.Sum(p => p.TowersCaptured)}, mortes {m.Players.Sum(p => p.Deaths)}");
        }
    }
}
