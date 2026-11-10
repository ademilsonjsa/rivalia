using Rivalia.Core;
using Rivalia.Core.AI;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Rivalia.Game
{
    /// <summary>
    /// Ponto de entrada do MVP: monta a arena, o NavMesh, a partida (1 jogador + 3 bots), a câmera e a HUD.
    /// A cena só precisa deste componente (o menu Rivalia > Criar cena MVP faz isso).
    /// </summary>
    public class RivaliaBootstrap : MonoBehaviour
    {
        public BotDifficulty Difficulty = BotDifficulty.Normal;
        public BotProfile[] BotProfiles = { BotProfile.Aggressive, BotProfile.Expansionist, BotProfile.Economic };
        [Tooltip("Demonstração/testes: o jogador também é controlado por um bot.")]
        public bool AutoPlayLocalPlayer = false;
        public int TargetFrameRate = 60;

        public MatchDirector Director { get; private set; }

        private void Start()
        {
            Application.targetFrameRate = TargetFrameRate;
            var map = MapDefinition.ValeDasQuatroCoroas();

            // 1) Arena e NavMesh
            var arena = new GameObject("Arena").transform;
            var regionViews = ArenaBuilder.Build(map, arena);
            var surface = arena.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            if (FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("Sol").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            }

            // 2) Partida
            var match = new Match(map, new MatchRules(), new EconomyConfig());
            match.AddPlayer("Você", AutoPlayLocalPlayer);
            string[] botNames = { "Brasa", "Pipo", "Nina" };
            for (int i = 0; i < 3; i++) match.AddPlayer($"{botNames[i]} (bot)", true);
            match.Start();

            Director = new GameObject("MatchDirector").AddComponent<MatchDirector>();
            Director.Init(match, regionViews);
            Director.LocalPlayerId = 0;

            // 3) Heróis (o mesmo HeroActor para humano e bots)
            foreach (var p in match.Players)
            {
                var hero = HeroActor.Create(p.Id, GameVisuals.Team(p.Id), GameVisuals.ToV3(p.Position), Director);
                Director.Heroes.Add(hero);
                bool bot = p.Id != 0 || AutoPlayLocalPlayer;
                if (bot)
                {
                    var pilot = hero.gameObject.AddComponent<BotPilot>();
                    pilot.Hero = hero;
                    pilot.Director = Director;
                    var profile = p.Id == 0 ? BotProfile.Balanced : BotProfiles[(p.Id - 1) % BotProfiles.Length];
                    pilot.Brain = new BotStrategist(profile, Difficulty);
                    hero.name = $"Heroi {p.Id} ({profile})";
                }
            }

            // 4) Câmera, entrada e HUD
            var local = Director.HeroOf(0);
            var rig = CameraRig.Create(local, map.HalfSize);
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var controller = new GameObject("PlayerController").AddComponent<PlayerController>();
            controller.Hero = local;
            controller.Director = Director;
            controller.enabled = !AutoPlayLocalPlayer;
            Hud.Create(Director, controller, rig, out controller.MoveStick, out controller.AttackStick, out controller.AbilityStick, out controller.SuperStick);
        }
    }
}
