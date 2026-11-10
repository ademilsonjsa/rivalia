using System.Collections.Generic;
using System.Text;
using Rivalia.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rivalia.Game
{
    /// <summary>
    /// Interface da partida: joysticks, botões de habilidade, vida, Cristais, minimapa de domínio,
    /// progresso de captura, alertas, painel contextual de loja/melhorias, configurações e tela final.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public MatchDirector Director;
        public PlayerController Controller;
        public CameraRig CameraRig;

        private Text _crystals, _timer, _alert, _feed, _abilityLabel, _potionLabel, _endTitle, _endBody, _hpText;
        private Image _hpFill, _superFill;
        private readonly List<Image> _ammoPips = new List<Image>();
        private readonly List<Image> _regionChips = new List<Image>();
        private readonly List<Image> _regionProgress = new List<Image>();
        private readonly List<RectTransform> _miniDots = new List<RectTransform>();
        private RectTransform _context, _miniMap, _endScreen, _settings;
        private readonly List<string> _feedLines = new List<string>();
        private readonly Dictionary<string, Button> _contextButtons = new Dictionary<string, Button>();
        private Match M => Director.Match;
        private int Me => Director.LocalPlayerId;

        public static Hud Create(MatchDirector director, PlayerController controller, CameraRig rig, out VirtualJoystick move, out VirtualJoystick attack, out VirtualJoystick ability, out VirtualJoystick super)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = canvasGo.AddComponent<Hud>();
            hud.Director = director; hud.Controller = controller; hud.CameraRig = rig;
            var t = canvasGo.transform;

            // Joysticks: esquerdo flutuante ocupa a metade esquerda; direito fixo
            move = VirtualJoystick.Create(t, "JoystickMovimento", new Vector2(0.22f, 0.3f), Vector2.zero, new Vector2(840, 640), 110f, true, Color.white);
            attack = VirtualJoystick.Create(t, "JoystickAtaque", new Vector2(1, 0), new Vector2(-230, 210), new Vector2(260, 260), 110f, false, new Color(1f, 0.55f, 0.2f), "ATK");
            ability = VirtualJoystick.Create(t, "BotaoHabilidade", new Vector2(1, 0), new Vector2(-470, 140), new Vector2(150, 150), 62f, false, new Color(0.3f, 0.8f, 1f), "HAB");
            super = VirtualJoystick.Create(t, "BotaoSuper", new Vector2(1, 0), new Vector2(-440, 360), new Vector2(160, 160), 68f, false, new Color(1f, 0.85f, 0.2f), "SUPER");
            hud._abilityLabel = UiFactory.Text(ability.transform, "", 22, Color.white, TextAnchor.LowerCenter);
            hud._superFill = UiFactory.Fill(UiFactory.Panel(super.transform, "CargaSuper", new Vector2(0.5f, 0f), new Vector2(0, -10), new Vector2(130, 14), new Color(0, 0, 0, 0.5f)), "Carga", new Color(1f, 0.85f, 0.2f));
            for (int i = 0; i < 3; i++)
                hud._ammoPips.Add(UiFactory.Panel(attack.transform, "Municao" + i, new Vector2(0.5f, 0f), new Vector2(-50 + i * 50, -14), new Vector2(42, 12), Color.white).GetComponent<Image>());

            var potion = UiFactory.Button(t, "BotaoPocao", new Vector2(1, 0), new Vector2(-620, 110), new Vector2(110, 110), new Color(0.85f, 0.25f, 0.45f, 0.85f), "", () => director.Match.TryUsePotion(director.LocalPlayerId), round: true);
            hud._potionLabel = potion.GetComponentInChildren<Text>();

            // Vida
            var hp = UiFactory.Panel(t, "Vida", new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(520, 34), new Color(0, 0, 0, 0.55f));
            hud._hpFill = UiFactory.Fill(hp, "Preenchimento", GameVisuals.Team(director.LocalPlayerId));
            hud._hpText = UiFactory.Text(hp, "", 22, Color.white, TextAnchor.MiddleCenter);

            // Topo: Cristais, tempo, regiões
            var top = UiFactory.Panel(t, "Topo", new Vector2(0f, 1f), new Vector2(230, -50), new Vector2(420, 70), new Color(0, 0, 0, 0.45f));
            hud._crystals = UiFactory.Text(top, "", 30, new Color(0.5f, 0.95f, 1f), TextAnchor.MiddleCenter);
            var timer = UiFactory.Panel(t, "Tempo", new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(180, 56), new Color(0, 0, 0, 0.45f));
            hud._timer = UiFactory.Text(timer, "", 34, Color.white, TextAnchor.MiddleCenter);
            for (int i = 0; i < director.Match.Regions.Count; i++)
            {
                var chip = UiFactory.Panel(t, "Regiao" + i, new Vector2(0.5f, 1f), new Vector2(-220 + i * 110, -105), new Vector2(100, 40), GameVisuals.Neutral);
                UiFactory.Text(chip, director.Match.Regions[i].Def.Code, 20, Color.white, TextAnchor.MiddleCenter);
                hud._regionChips.Add(chip.GetComponent<Image>());
                var bar = UiFactory.Panel(chip, "Captura", new Vector2(0.5f, 0f), new Vector2(0, -6), new Vector2(100, 8), new Color(0, 0, 0, 0.5f));
                hud._regionProgress.Add(UiFactory.Fill(bar, "Progresso", Color.white));
            }

            // Minimapa (dono de cada região + posição dos heróis)
            hud._miniMap = UiFactory.Panel(t, "Minimapa", new Vector2(1f, 1f), new Vector2(-130, -130), new Vector2(220, 220), new Color(0, 0, 0, 0.45f));
            foreach (var r in director.Match.Regions)
            {
                float s = r.IsCenter ? 50 : 70;
                UiFactory.Panel(hud._miniMap, "Area" + r.Id, new Vector2(0.5f, 0.5f), hud.MiniPos(r.Def.Center), new Vector2(s, s), GameVisuals.Neutral);
            }
            foreach (var p in director.Match.Players)
            {
                var dot = UiFactory.Panel(hud._miniMap, "Ponto" + p.Id, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14), GameVisuals.Team(p.Id));
                dot.GetComponent<Image>().sprite = UiFactory.CircleSprite;
                hud._miniDots.Add(dot);
            }

            // Alertas e eventos
            var alert = UiFactory.Panel(t, "Alerta", new Vector2(0.5f, 0.75f), Vector2.zero, new Vector2(900, 60), new Color(0, 0, 0, 0f));
            hud._alert = UiFactory.Text(alert, "", 34, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter);
            var feed = UiFactory.Panel(t, "Eventos", new Vector2(1f, 1f), new Vector2(-200, -380), new Vector2(380, 220), new Color(0, 0, 0, 0f));
            hud._feed = UiFactory.Text(feed, "", 20, Color.white, TextAnchor.UpperRight);

            // Painel contextual (loja / melhorias)
            hud._context = UiFactory.Panel(t, "PainelContextual", new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1100, 90), new Color(0, 0, 0, 0f));
            var layout = hud._context.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false; layout.childControlHeight = false;
            hud.BuildContextButtons();

            // Configurações
            UiFactory.Button(t, "BotaoConfig", new Vector2(0f, 1f), new Vector2(490, -50), new Vector2(130, 60), new Color(0.2f, 0.2f, 0.25f, 0.8f), "Opções", () => hud._settings.gameObject.SetActive(!hud._settings.gameObject.activeSelf));
            hud.BuildSettings(t, move, attack, ability, super);

            // Tela final
            hud._endScreen = UiFactory.Panel(t, "TelaFinal", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 640), new Color(0.05f, 0.05f, 0.1f, 0.92f));
            var title = UiFactory.Panel(hud._endScreen, "Titulo", new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(900, 90), new Color(0, 0, 0, 0));
            hud._endTitle = UiFactory.Text(title, "", 64, Color.white, TextAnchor.MiddleCenter);
            var body = UiFactory.Panel(hud._endScreen, "Resumo", new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(900, 380), new Color(0, 0, 0, 0));
            hud._endBody = UiFactory.Text(body, "", 26, Color.white, TextAnchor.UpperCenter);
            hud._endBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Button(hud._endScreen, "JogarDeNovo", new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(360, 80), new Color(0.2f, 0.65f, 0.3f), "Jogar de novo", () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
            hud._endScreen.gameObject.SetActive(false);

            director.MatchEventRaised += hud.OnMatchEvent;
            return hud;
        }

        private Vector2 MiniPos(Vec2 world) => new Vector2(world.X, world.Z) * (100f / M.Map.HalfSize);

        private void BuildContextButtons()
        {
            AddContext("mina", () => Upgrade(StructureKind.Mine));
            AddContext("base", () => Upgrade(StructureKind.MilitaryBase));
            AddContext("torre", () => Upgrade(StructureKind.Tower));
            foreach (var item in M.Economy.ShopItems)
            {
                var id = item.Id;
                AddContext("item" + (int)id, () => Buy(id));
            }
        }

        private void AddContext(string key, System.Action onClick)
        {
            var b = UiFactory.Button(_context, key, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 80), new Color(0.15f, 0.45f, 0.85f, 0.9f), key, onClick);
            b.GetComponentInChildren<Text>().fontSize = 20;
            b.gameObject.SetActive(false);
            _contextButtons[key] = b;
        }

        private void BuildSettings(Transform t, params VirtualJoystick[] sticks)
        {
            _settings = UiFactory.Panel(t, "Configuracoes", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720, 440), new Color(0.05f, 0.05f, 0.1f, 0.94f));
            var title = UiFactory.Panel(_settings, "T", new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(600, 50), new Color(0, 0, 0, 0));
            UiFactory.Text(title, "Configurações", 34, Color.white, TextAnchor.MiddleCenter);
            float y = 110;
            Row("Zoom da câmera", () => { CameraRig.Distance = CameraRig.Distance >= 28f ? 16f : CameraRig.Distance + 3f; CameraRig.Apply(); }, () => $"{CameraRig.Distance:0} m", ref y);
            Row("Inclinação", () => { CameraRig.Pitch = CameraRig.Pitch >= 70f ? 55f : CameraRig.Pitch + 5f; CameraRig.Apply(); }, () => $"{CameraRig.Pitch:0}°", ref y);
            Row("Tamanho dos controles", () =>
            {
                float s = sticks[0].transform.localScale.x >= 1.3f ? 0.7f : sticks[0].transform.localScale.x + 0.15f;
                foreach (var js in sticks) js.transform.localScale = Vector3.one * s;
            }, () => $"{sticks[0].transform.localScale.x * 100:0}%", ref y);
            Row("Assistência de mira", () => Controller.AimAssist = (AimAssistMode)(((int)Controller.AimAssist + 1) % 3),
                () => Controller.AimAssist == AimAssistMode.Off ? "Desligada" : Controller.AimAssist == AimAssistMode.TapToTarget ? "Toque rápido" : "Magnetismo", ref y);
            _settings.gameObject.SetActive(false);
        }

        private void Row(string label, System.Action onClick, System.Func<string> value, ref float y)
        {
            var l = UiFactory.Panel(_settings, label, new Vector2(0.5f, 1f), new Vector2(-150, -y), new Vector2(340, 60), new Color(0, 0, 0, 0));
            UiFactory.Text(l, label, 26, Color.white, TextAnchor.MiddleLeft);
            Button b = null;
            b = UiFactory.Button(_settings, label + "Btn", new Vector2(0.5f, 1f), new Vector2(190, -y), new Vector2(240, 60), new Color(0.2f, 0.45f, 0.8f), value(), () => { onClick(); b.GetComponentInChildren<Text>().text = value(); });
            y += 80;
        }

        private void Upgrade(StructureKind kind)
        {
            var r = NearbyOwnedRegion(kind);
            if (r != null) Feedback(Director.RequestUpgrade(Me, r.Id, kind));
        }

        private void Buy(ShopItemId id)
        {
            var r = NearbyOwnedRegion(StructureKind.Shop);
            if (r != null) Feedback(Director.RequestBuy(Me, r.Id, id));
        }

        private void Feedback(CommandResult res)
        {
            if (res == CommandResult.Ok) Sfx.Play(Sfx.Coin, Director.HeroOf(Me).transform.position);
            else if (res == CommandResult.NotEnoughCrystals) Push("Cristais insuficientes");
            else if (res == CommandResult.MaxLevel) Push("Nível máximo");
            else if (res == CommandResult.MaxStacks) Push("Limite deste item atingido");
        }

        private RegionState NearbyOwnedRegion(StructureKind kind)
        {
            var me = M.GetPlayer(Me);
            foreach (var r in M.Regions)
            {
                if (r.IsCenter || r.OwnerId != Me) continue;
                if (Vec2.Distance(me.Position, M.StructurePosition(r, kind)) <= M.Economy.InteractRadius) return r;
            }
            return null;
        }

        private void Update()
        {
            if (Director?.Match == null) return;
            var me = M.GetPlayer(Me);

            _crystals.text = $"Cristais {me.Crystals}   Torres {M.TowerCount(Me)}/{M.Regions.Count}";
            float left = Mathf.Max(0f, M.Rules.TimeLimitSeconds - M.Time);
            _timer.text = $"{(int)left / 60}:{(int)left % 60:00}";

            int max = M.MaxHealthOf(me);
            _hpFill.fillAmount = me.Health / (float)max;
            _hpText.text = $"{me.Health} / {max}";
            for (int i = 0; i < _ammoPips.Count; i++) _ammoPips[i].color = i < me.Ammo ? Color.white : new Color(1, 1, 1, 0.2f);
            _superFill.fillAmount = me.SuperCharge;
            _abilityLabel.text = me.AbilityCooldown > 0f ? $"{me.AbilityCooldown:0.0}s" : "";
            _potionLabel.text = $"Poção\n{me.ItemCount(ShopItemId.Potion)}";

            for (int i = 0; i < M.Regions.Count; i++)
            {
                var r = M.Regions[i];
                _regionChips[i].color = GameVisuals.Team(r.OwnerId);
                _regionProgress[i].fillAmount = r.CaptureProgress / 100f;
                _regionProgress[i].color = GameVisuals.Team(r.CapturingPlayerId);
                _miniMap.GetChild(i).GetComponent<Image>().color = Color.Lerp(GameVisuals.Team(r.OwnerId), Color.black, 0.25f);
            }
            for (int i = 0; i < _miniDots.Count; i++)
            {
                var p = M.Players[i];
                _miniDots[i].gameObject.SetActive(p.IsAlive);
                _miniDots[i].anchoredPosition = MiniPos(p.Position);
            }

            _alert.text = AlertText(me);
            UpdateContext(me);

            if (M.IsOver && !_endScreen.gameObject.activeSelf) ShowEnd();
        }

        private string AlertText(PlayerState me)
        {
            if (M.IsOver) return "";
            if (me.IsEliminated) return "Você foi eliminado. Assistindo à partida.";
            if (me.Life == LifeState.Dead) return $"Renascendo em {Mathf.Max(0f, me.RespawnTimer):0.0}s";
            foreach (var r in M.Regions)
            {
                if (r.OwnerId == Me && r.CaptureProgress > 0f && r.CapturingPlayerId != Me)
                    return $"Sua torre {r.Def.Code} está sendo capturada! {r.CaptureProgress:0}%";
                if (r.OwnerId != Me && r.CapturingPlayerId == Me)
                    return r.IsContested ? $"Captura de {r.Def.Code} contestada!" : $"Capturando {r.Def.Code}: {r.CaptureProgress:0}%";
            }
            if (M.TowerCount(Me) == 0) return "Sem torres: se morrer, será eliminado. Conquiste uma torre!";
            return "";
        }

        private void UpdateContext(PlayerState me)
        {
            foreach (var b in _contextButtons.Values) b.gameObject.SetActive(false);
            if (!me.IsAlive || M.IsOver) return;
            Show("mina", StructureKind.Mine, "Mina");
            Show("base", StructureKind.MilitaryBase, "Base");
            Show("torre", StructureKind.Tower, "Torre");
            if (NearbyOwnedRegion(StructureKind.Shop) != null)
            {
                foreach (var item in M.Economy.ShopItems)
                {
                    var b = _contextButtons["item" + (int)item.Id];
                    b.gameObject.SetActive(true);
                    b.GetComponentInChildren<Text>().text = $"{item.Name}\n{item.Price} cristais ({me.ItemCount(item.Id)}/{item.MaxStacks})";
                    b.interactable = me.Crystals >= item.Price && me.ItemCount(item.Id) < item.MaxStacks;
                }
            }

            void Show(string key, StructureKind kind, string label)
            {
                var r = NearbyOwnedRegion(kind);
                if (r == null) return;
                int cost = M.UpgradeCost(r, kind);
                var b = _contextButtons[key];
                b.gameObject.SetActive(true);
                b.GetComponentInChildren<Text>().text = cost < 0 ? $"{label} nível máx." : $"Melhorar {label} → {M.StructureLevel(r, kind) + 1}\n{cost} cristais";
                b.interactable = cost >= 0 && me.Crystals >= cost;
            }
        }

        private void OnMatchEvent(MatchEvent e)
        {
            string Name(int id) => id == Match.Neutral ? "Base Militar" : M.GetPlayer(id).Name;
            switch (e.Type)
            {
                case MatchEventType.Killed: Push($"{Name(e.OtherId)} eliminou {Name(e.PlayerId)}"); break;
                case MatchEventType.TowerCaptured: Push($"{Name(e.PlayerId)} conquistou {M.GetRegion(e.RegionId).Def.Code}"); break;
                case MatchEventType.Eliminated: Push($"{Name(e.PlayerId)} está fora da partida"); break;
                case MatchEventType.MineSabotaged: Push($"Mina {M.GetRegion(e.RegionId).Def.Code} sabotada!"); break;
                case MatchEventType.BaseDisabled: Push($"Base {M.GetRegion(e.RegionId).Def.Code} destruída por 30s"); break;
            }
        }

        private void Push(string line)
        {
            _feedLines.Insert(0, line);
            if (_feedLines.Count > 6) _feedLines.RemoveAt(_feedLines.Count - 1);
            _feed.text = string.Join("\n", _feedLines);
        }

        private void ShowEnd()
        {
            var res = M.Result;
            _endScreen.gameObject.SetActive(true);
            _endTitle.text = res.IsDraw ? "Empate" : res.WinnerId == Me ? "Vitória!" : "Derrota";
            string reason;
            switch (res.Reason)
            {
                case MatchEndReason.Domination: reason = "Domínio de todas as torres"; break;
                case MatchEndReason.LastStanding: reason = "Último sobrevivente"; break;
                case MatchEndReason.TimeLimit: reason = "Tempo esgotado (desempate)"; break;
                case MatchEndReason.NoHumanCanWin: reason = "Você foi eliminado"; break;
                default: reason = "Todos eliminados"; break;
            }
            var sb = new StringBuilder();
            sb.AppendLine(reason).AppendLine();
            for (int i = 0; i < res.Placement.Count; i++)
            {
                var p = M.GetPlayer(res.Placement[i]);
                sb.AppendLine($"{i + 1}º  {p.Name}  ·  torres {M.TowerCount(p.Id)}  ·  eliminações {p.Kills}  ·  mortes {p.Deaths}  ·  dano {p.DamageDealt}");
            }
            _endBody.text = sb.ToString();
        }
    }
}
