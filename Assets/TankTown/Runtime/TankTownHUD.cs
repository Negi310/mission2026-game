using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TankTown
{
    [DefaultExecutionOrder(100)]
    public sealed class TankTownHUD : MonoBehaviour
    {
        private TankTownGame game;
        private Font font;
        private Transform canvas;
        private GameObject gameplay, titleScreen;
        private TankTownTitleScreen titleView;
        private Text timer, score, counter, notice, cardTitle, cardBody, buttonText;
        private Image fill;
        private Image viewFade;
        private Text aim;
        private readonly Text[] statRows = new Text[4];
        private Text pickupText;
        private GameObject pickupPanel;
        private Text heldName, heldHint, activeItems;
        private Image heldIcon;
        private float pickupUntil;
        private float chainUntil, goUntil;
        private int chainDepth;
        private RoundState previousState = RoundState.Ready;
        private GameObject card, noticePanel, modalShade;
        private readonly Color ink = new Color(.08f, .17f, .22f);
        private readonly Color yellow = new Color(.81f, 1, .14f);
        public void Initialize(TankTownGame owner)
        {
            game = owner;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 24);
            var root = new GameObject("Tank Town HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.transform;
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = .5f;
            var fade = new GameObject("View transition", typeof(RectTransform), typeof(Image));
            fade.transform.SetParent(canvas, false);
            var fadeRect = fade.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero; fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;
            viewFade = fade.GetComponent<Image>();
            viewFade.color = Color.clear; viewFade.raycastTarget = false;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
            gameplay = new GameObject("Gameplay HUD", typeof(RectTransform));
            gameplay.transform.SetParent(canvas, false);
            var gameplayRect = gameplay.GetComponent<RectTransform>();
            gameplayRect.anchorMin = Vector2.zero; gameplayRect.anchorMax = Vector2.one;
            gameplayRect.offsetMin = gameplayRect.offsetMax = Vector2.zero;
            var hud = gameplay.transform;

            Panel("Brand", hud, new Vector2(26, -24), new Vector2(310, 86), ink);
            Label(hud, "TANK / TOWN", new Vector2(46, -34), new Vector2(270, 40), 29, yellow);
            Label(hud, "こわして、街をあそぼう！", new Vector2(48, -77), new Vector2(270, 24), 15, Color.white);
            var clock = Panel("Clock", hud, new Vector2(-26, -24), new Vector2(204, 86), ink, true);
            timer = Label(clock.transform, "03:00", new Vector2(20, -9), new Vector2(170, 48), 38, Color.white);
            timer.fontStyle = FontStyle.Bold;
            Label(clock.transform, "のこり時間", new Vector2(24, -60), new Vector2(160, 22), 14, yellow);
            var progress = Panel("Destruction", hud, new Vector2(26, -128), new Vector2(310, 113), new Color(.98f, .97f, .9f, .96f));
            score = Label(progress.transform, "破壊度  0.0%", new Vector2(20, -12), new Vector2(270, 37), 26, ink);
            score.fontStyle = FontStyle.Bold;
            var track = Panel("Progress track", progress.transform, new Vector2(20, -59), new Vector2(270, 10), new Color(.8f, .84f, .79f));
            fill = Panel("Progress fill", track.transform, Vector2.zero, new Vector2(0, 10), new Color(.22f, .66f, .51f)).GetComponent<Image>();
            counter = Label(progress.transform, "", new Vector2(20, -79), new Vector2(280, 23), 14, ink);
            var stats = Panel("Upgrades", hud, new Vector2(-26, -128), new Vector2(318, 238), ink, true);
            Label(stats.transform, "パワーアップ", new Vector2(18, -12), new Vector2(280, 28), 18, Color.white);
            for (int i = 0; i < 4; i++)
            {
                var icon = Panel("Stat icon", stats.transform, new Vector2(16, -48 - i * 44), new Vector2(32, 32), Color.white).GetComponent<Image>();
                icon.sprite = game.World.Icons.Sprite((UpgradeKind)i);
                statRows[i] = Label(stats.transform, "", new Vector2(60, -49 - i * 44), new Vector2(250, 32), 17, TankStats.Color((UpgradeKind)i));
                statRows[i].fontStyle = FontStyle.Bold;
            }
            pickupPanel = Panel("Pickup notice", hud, new Vector2(0, -215), new Vector2(350, 54), ink);
            var pickupRect = pickupPanel.GetComponent<RectTransform>();
            pickupRect.anchorMin = pickupRect.anchorMax = new Vector2(.5f, 1);
            pickupRect.pivot = new Vector2(.5f, 1);
            pickupText = Label(pickupPanel.transform, "", new Vector2(12, -5), new Vector2(326, 44), 23, Color.white);
            pickupText.alignment = TextAnchor.MiddleCenter;
            game.Stats.Changed += ShowUpgrade;
            game.SpecialCollected += ShowSpecial;
            game.ChainBurst += ShowChain;
            var itemPanel = Panel("Special items", hud, new Vector2(-26, -385), new Vector2(318, 216), ink, true);
            Label(itemPanel.transform, "スペシャルアイテム", new Vector2(18, -10), new Vector2(285, 26), 17, Color.white);
            heldIcon = Panel("Held item icon", itemPanel.transform, new Vector2(16, -45), new Vector2(54, 54), Color.white).GetComponent<Image>();
            heldName = Label(itemPanel.transform, "", new Vector2(82, -43), new Vector2(220, 29), 20, yellow);
            heldName.fontStyle = FontStyle.Bold;
            heldHint = Label(itemPanel.transform, "", new Vector2(82, -77), new Vector2(222, 26), 14, Color.white);
            activeItems = Label(itemPanel.transform, "", new Vector2(18, -115), new Vector2(285, 92), 16, Color.white);
            noticePanel = Panel("Respawn notice", hud, new Vector2(0, -132), new Vector2(720, 64), ink);
            var noticeRect = noticePanel.GetComponent<RectTransform>();
            noticeRect.anchorMin = noticeRect.anchorMax = new Vector2(.5f, 1);
            noticeRect.pivot = new Vector2(.5f, 1);
            notice = Label(noticePanel.transform, "", new Vector2(16, -7), new Vector2(688, 50), 22, Color.white);
            notice.fontStyle = FontStyle.Bold;
            notice.alignment = TextAnchor.MiddleCenter;
            var bottom = Panel("Controls", hud, new Vector2(0, 24), new Vector2(900, 54), ink);
            var br = bottom.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(.5f, 0);
            br.pivot = new Vector2(.5f, 0);
            Label(bottom.transform, "ハンドル  A / D ・ ← / →       たたいて発射  SPACE / J / K       ひとやすみ  ESC", new Vector2(20, -14), new Vector2(870, 30), 17, Color.white);
            aim = Label(hud, "＋", Vector2.zero, new Vector2(40, 40), 32, Color.white);
            aim.rectTransform.anchorMin = aim.rectTransform.anchorMax = aim.rectTransform.pivot = Vector2.one * .5f;
            aim.alignment = TextAnchor.MiddleCenter;
            aim.fontStyle = FontStyle.Bold;
            aim.gameObject.AddComponent<Outline>().effectColor = ink;
            modalShade = Panel("Pause and results backdrop", hud, Vector2.zero, Vector2.zero, new Color(.025f, .07f, .12f, .62f));
            var shadeRect = modalShade.GetComponent<RectTransform>();
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            card = Panel("Round card", hud, Vector2.zero, new Vector2(630, 440), new Color(.98f, .97f, .91f, .98f));
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(.5f, .5f);
            Label(card.transform, "3 MINUTE DESTRUCTION PLAYGROUND", new Vector2(38, -30), new Vector2(570, 27), 16, new Color(.22f, .55f, .43f));
            cardTitle = Label(card.transform, "", new Vector2(38, -78), new Vector2(560, 62), 42, ink);
            cardTitle.fontStyle = FontStyle.Bold;
            cardBody = Label(card.transform, "", new Vector2(40, -156), new Vector2(550, 110), 21, ink);
            var button = Panel("Start button", card.transform, new Vector2(40, -285), new Vector2(550, 60), yellow);
            button.AddComponent<Button>().onClick.AddListener(game.StartOrRestart);
            buttonText = Label(button.transform, "", new Vector2(0, -10), new Vector2(550, 42), 23, ink);
            buttonText.alignment = TextAnchor.MiddleCenter;
            var back = Panel("Title button", card.transform, new Vector2(40, -365), new Vector2(550, 44), ink);
            back.AddComponent<Button>().onClick.AddListener(game.ReturnToTitle);
            var backLabel = Label(back.transform, "タイトルへ", new Vector2(0, -5), new Vector2(550, 34), 19, Color.white);
            backLabel.alignment = TextAnchor.MiddleCenter;
            titleView = new TankTownTitleScreen(game, canvas, font);
            titleScreen = titleView.Root;
            gameplay.SetActive(game.State != RoundState.Ready);
        }

        private GameObject Panel(string title, Transform parent, Vector2 position, Vector2 size, Color color, bool right = false)
        {
            var item = new GameObject(title, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1 : 0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = color;
            return item;
        }

        private Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var item = new GameObject("Label", typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        private void Update()
        {
            titleScreen.SetActive(game.State == RoundState.Ready);
            if (game.State == RoundState.Ready) titleView.Fit();
            gameplay.SetActive(game.State != RoundState.Ready);
            if (game.IsPlaying && previousState == RoundState.Ready) goUntil = Time.time + .85f;
            previousState = game.State;
            int seconds = Mathf.CeilToInt(game.Remaining);
            timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
            timer.color = seconds <= 20 ? new Color(1, .55f, .35f) : Color.white;
            score.text = $"破壊度  {game.Destruction:0.0}%";
            counter.text = $"{game.BrokenBlocks}/{game.TotalBlocks} パーツ ・ 誘爆 {game.ChainExplosions}回";
            fill.rectTransform.sizeDelta = new Vector2(270 * game.Destruction / 100, 10);
            bool edge = Mathf.Abs(game.Tank.transform.position.x) > game.World.HalfExtent - 13 && game.Tank.transform.position.x * game.Tank.transform.forward.x > 0
                || Mathf.Abs(game.Tank.transform.position.z) > game.World.HalfExtent - 13 && game.Tank.transform.position.z * game.Tank.transform.forward.z > 0;
            notice.text = game.Tank.Respawning ? $"ドカーン！ 安全な場所へもどるよ  −{game.Settings.respawnPenalty:0.#}秒"
                : edge ? "海が見えたら、街の内側へ曲がろう！"
                : Time.time < chainUntil ? $"連鎖爆破！  {chainDepth}段！"
                : Time.time < goUntil ? "GO！  街をこわそう！" : "";
            notice.color = Time.time < chainUntil && !game.Tank.Respawning && !edge ? new Color(1, .68f, .18f) : Color.white;
            noticePanel.SetActive(game.IsPlaying && notice.text.Length > 0);
            for (int i = 0; i < 4; i++)
            {
                var kind = (UpgradeKind)i;
                string value = kind == UpgradeKind.Speed ? $"{game.Stats.Speed:0.0}" : kind == UpgradeKind.Power ? $"x{game.Stats.BlastImpulse / game.Settings.blastImpulse:0.00}" : kind == UpgradeKind.Rapid ? $"{1 / game.Stats.ShotCooldown:0.0}/秒" : $"{game.Stats.TurnSpeed:0}°/秒";
                statRows[i].text = $"{TankStats.Label(kind)} Lv.{game.Stats.Level(kind)}  {value}";
            }
            pickupPanel.SetActive(game.IsPlaying && !game.Tank.Respawning && Time.time < pickupUntil);
            aim.gameObject.SetActive(game.IsPlaying && !game.Tank.Respawning);
            card.SetActive(game.State != RoundState.Playing);
            modalShade.SetActive(game.State != RoundState.Playing);
            if (game.State == RoundState.Ready)
            {
                cardTitle.text = "街を、バラバラに！";
                cardBody.text = "ハンドルで砲塔が向く。車体がついてくる。\nたたいて砲撃！ 武器アイテムも砲撃で発動！\n磁石・ブルドーザー・高速は取ると発動。\n街を走って、バラバラにしよう！";
                buttonText.text = "あそぶ   →   ENTER";
            }
            else if (game.State == RoundState.Paused)
            {
                cardTitle.text = "ひとやすみ";
                cardBody.text = "準備ができたら、もういちど出発！\n時間は止まっているよ。";
                buttonText.text = "つづける   →   ENTER";
            }
            else if (game.State == RoundState.Results)
            {
                cardTitle.text = $"{game.Destruction:0.0}%  こわせた！";
                string rank = game.Destruction >= 85 ? "まちのはかい王" : game.Destruction >= 60 ? "ビルクラッシャー" : game.Destruction >= 35 ? "はかいチャレンジャー" : "はかいルーキー";
                cardBody.text = $"称号：{rank}\n{game.BrokenBlocks} パーツ破壊 ・ 誘爆 {game.ChainExplosions}回\n最高 {game.LongestChain}段の連鎖 ・ リスポーン {game.Crashes}回\n次はどの道を走ろう？";
                buttonText.text = "もういちど   →   ENTER";
            }
            var shown = game.Items.Held != ItemKind.None ? game.Items.Held : game.Items.Active;
            heldIcon.gameObject.SetActive(shown != ItemKind.None);
            if (shown != ItemKind.None) heldIcon.sprite = game.World.SpecialIcons.Sprite(shown);
            heldName.text = game.Items.Held != ItemKind.None ? ItemIcons.Label(game.Items.Held) : shown != ItemKind.None ? "発動中！" : "ビルをくずそう！";
            heldHint.text = game.Items.Held != ItemKind.None ? game.Items.Weapon == ItemKind.None ? "次の砲撃でつかう！" : "発動後、砲撃でつかう" : shown != ItemKind.None ? shown == ItemKind.MegaBeam ? "ハンドルでねらおう！" : "効果は下の秒数まで！" : "上のスペシャルが落ちるよ";
            activeItems.text = game.Items.ActiveSummary;
        }

        private void ShowUpgrade(UpgradeKind kind)
        {
            pickupText.text = $"{TankStats.Label(kind)} UP!   Lv.{game.Stats.Level(kind)}";
            pickupText.color = TankStats.Color(kind);
            pickupUntil = Time.time + 1.7f;
        }
        private void ShowSpecial(ItemKind kind)
        {
            pickupText.text = $"{ItemIcons.Label(kind)} GET！";
            pickupText.color = ItemIcons.Color(kind); pickupUntil = Time.time + 1.7f;
        }
        private void ShowChain(int depth) { chainDepth = depth; chainUntil = Time.time + 1.3f; }
        private void OnDestroy()
        {
            if (game != null && game.Stats != null) game.Stats.Changed -= ShowUpgrade;
            if (game != null) { game.SpecialCollected -= ShowSpecial; game.ChainBurst -= ShowChain; }
            if (font != null) Destroy(font);
        }
        private void LateUpdate()
        {
            viewFade.color = new Color(0, 0, 0, game.CameraRig.FadeOpacity);
            aim.enabled = game.CameraRig.FadeOpacity <= .01f && !game.Tank.Relocating;
            // Project the actual muzzle direction after the comfort camera has moved.
            if (!game.IsPlaying || Camera.main == null) return;
            Vector3 projected = Camera.main.WorldToScreenPoint(game.Tank.Muzzle.position + game.Tank.Muzzle.forward * 70);
            aim.rectTransform.anchoredPosition = ((Vector2)projected - new Vector2(Screen.width, Screen.height) * .5f) / canvas.GetComponent<Canvas>().scaleFactor;
        }
    }
}
