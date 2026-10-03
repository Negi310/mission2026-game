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
        private Font displayFont;
        private Transform canvas;
        private GameObject gameplay, titleScreen;
        private TankTownTitleScreen titleView;
        private Text timer, score, notice, noticeCaption, cardTitle, cardBody, buttonText;
        private ToyHudStyle style;
        private Image fill;
        private Image viewFade;
        private Text aim;
        private readonly Text[] statRows = new Text[4];
        private Text pickupText;
        private GameObject pickupPanel;
        private Text heldName, heldHint, activeItems;
        private Text emptyItem;
        private GameObject activePanel;
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
            style = new ToyHudStyle();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 24);
            displayFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial Rounded MT Bold", "Arial Black", "Arial" }, 40);
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

            var tankWord = Label(hud, "TANK", new Vector2(28, -17), new Vector2(143, 65), 43, new Color(1, .75f, .08f));
            var townWord = Label(hud, "TOWN", new Vector2(172, -17), new Vector2(165, 65), 43, new Color(.08f, .83f, 1));
            tankWord.fontStyle = townWord.fontStyle = FontStyle.Bold;
            tankWord.font = townWord.font = displayFont;
            tankWord.fontSize = townWord.fontSize = 37;
            TextOutline(tankWord, ink, 3); TextOutline(townWord, ink, 3);
            var clock = Panel("Clock", hud, new Vector2(-28, -26), new Vector2(190, 83), ink, true);
            style.Card(clock, new Color(.83f, .93f, 1));
            timer = Label(clock.transform, "03:00", new Vector2(18, -4), new Vector2(162, 48), 38, Color.white);
            timer.fontStyle = FontStyle.Bold;
            Label(clock.transform, "のこり時間", new Vector2(26, -53), new Vector2(158, 24), 16, yellow);
            var progress = Panel("Destruction", hud, new Vector2(28, -89), new Vector2(310, 89), new Color(.98f, .97f, .9f, .98f));
            style.Card(progress, ink);
            var scoreLabel = Label(progress.transform, "破壊度", new Vector2(16, -14), new Vector2(98, 35), 25, ink);
            scoreLabel.fontStyle = FontStyle.Bold;
            score = Label(progress.transform, "0.0%", new Vector2(116, -3), new Vector2(178, 49), 37, ink);
            score.font = displayFont;
            score.fontStyle = FontStyle.Bold;
            var track = Panel("Progress track", progress.transform, new Vector2(16, -61), new Vector2(278, 14), new Color(.76f, .8f, .68f));
            fill = Panel("Progress fill", track.transform, Vector2.zero, new Vector2(0, 14), new Color(.59f, .9f, .06f)).GetComponent<Image>();
            var stats = new GameObject("Upgrades", typeof(RectTransform)); stats.transform.SetParent(hud, false);
            var statsRect = stats.GetComponent<RectTransform>();
            statsRect.anchorMin = statsRect.anchorMax = statsRect.pivot = new Vector2(0, 0);
            statsRect.anchoredPosition = new Vector2(28, 28); statsRect.sizeDelta = new Vector2(404, 115);
            for (int i = 0; i < 4; i++)
            {
                var chip = Panel("Upgrade chip " + i, stats.transform, new Vector2(i * 104, 0), new Vector2(92, 115), ink);
                style.Card(chip, TankStats.Color((UpgradeKind)i));
                var icon = Panel("Stat icon", chip.transform, new Vector2(22, -6), new Vector2(48, 48), Color.white).GetComponent<Image>();
                icon.sprite = game.World.Icons.Sprite((UpgradeKind)i);
                icon.type = Image.Type.Simple;
                var kindLabel = Label(chip.transform, TankStats.Label((UpgradeKind)i), new Vector2(3, -57), new Vector2(86, 23), 16, TankStats.Color((UpgradeKind)i));
                kindLabel.alignment = TextAnchor.MiddleCenter; kindLabel.fontStyle = FontStyle.Bold;
                statRows[i] = Label(chip.transform, "", new Vector2(3, -79), new Vector2(86, 29), 22, Color.white);
                statRows[i].alignment = TextAnchor.MiddleCenter;
                statRows[i].fontStyle = FontStyle.Bold;
            }
            pickupPanel = Panel("Pickup notice", hud, new Vector2(28, -195), new Vector2(310, 44), ink);
            pickupText = Label(pickupPanel.transform, "", new Vector2(10, -3), new Vector2(290, 37), 20, Color.white);
            pickupText.alignment = TextAnchor.MiddleCenter;
            game.Stats.Changed += ShowUpgrade;
            game.SpecialCollected += ShowSpecial;
            game.ChainBurst += ShowChain;
            var itemPanel = Panel("Special items", hud, new Vector2(-28, 28), new Vector2(288, 108), ink, true);
            Bottom(itemPanel, true);
            style.Card(itemPanel, new Color(.83f, .93f, 1));
            heldIcon = Panel("Held item icon", itemPanel.transform, new Vector2(12, -16), new Vector2(73, 73), Color.white).GetComponent<Image>();
            heldIcon.type = Image.Type.Simple;
            emptyItem = Label(itemPanel.transform, "？", new Vector2(16, -14), new Vector2(65, 79), 44, new Color(.38f, .71f, .82f));
            emptyItem.alignment = TextAnchor.MiddleCenter; emptyItem.fontStyle = FontStyle.Bold;
            heldName = Label(itemPanel.transform, "", new Vector2(97, -16), new Vector2(184, 32), 24, yellow);
            heldName.fontStyle = FontStyle.Bold;
            heldHint = Label(itemPanel.transform, "", new Vector2(97, -56), new Vector2(182, 41), 15, Color.white);
            activePanel = Panel("Active item timers", hud, new Vector2(-28, 153), new Vector2(288, 132), new Color(ink.r, ink.g, ink.b, .92f), true);
            Bottom(activePanel, true);
            activeItems = Label(activePanel.transform, "", new Vector2(14, -8), new Vector2(260, 116), 17, Color.white);
            noticePanel = Panel("Respawn notice", hud, new Vector2(0, -72), new Vector2(430, 92), Color.clear);
            var noticeRect = noticePanel.GetComponent<RectTransform>();
            noticeRect.anchorMin = noticeRect.anchorMax = new Vector2(.5f, 1);
            noticeRect.pivot = new Vector2(.5f, 1);
            notice = Label(noticePanel.transform, "", new Vector2(6, 0), new Vector2(418, 58), 35, Color.white);
            notice.fontStyle = FontStyle.Bold;
            notice.font = displayFont;
            notice.alignment = TextAnchor.MiddleCenter;
            notice.gameObject.name = "Action heading";
            notice.horizontalOverflow = HorizontalWrapMode.Overflow;
            TextOutline(notice, ink, 2);
            noticeCaption = Label(noticePanel.transform, "", new Vector2(15, -58), new Vector2(400, 32), 19, yellow);
            noticeCaption.gameObject.name = "Action caption";
            noticeCaption.alignment = TextAnchor.MiddleCenter;
            noticeCaption.fontStyle = FontStyle.Bold;
            noticeCaption.horizontalOverflow = HorizontalWrapMode.Overflow;
            TextOutline(noticeCaption, ink, 1);
            var bottom = Panel("Controls", hud, new Vector2(0, 28), new Vector2(440, 34), new Color(ink.r, ink.g, ink.b, .86f));
            var br = bottom.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(.5f, 0);
            br.pivot = new Vector2(.5f, 0);
            var controls = Label(bottom.transform, "A / D ハンドル   SPACE 砲撃   ESC 休憩", new Vector2(10, -3), new Vector2(420, 27), 16, Color.white);
            controls.alignment = TextAnchor.MiddleCenter;
            aim = Label(hud, "＋", Vector2.zero, new Vector2(40, 40), 32, Color.white);
            aim.rectTransform.anchorMin = aim.rectTransform.anchorMax = aim.rectTransform.pivot = Vector2.one * .5f;
            aim.alignment = TextAnchor.MiddleCenter;
            aim.fontStyle = FontStyle.Bold;
            aim.gameObject.AddComponent<Outline>().effectColor = ink;
            modalShade = Panel("Pause and results backdrop", hud, Vector2.zero, Vector2.zero, new Color(.025f, .07f, .12f, .62f));
            modalShade.GetComponent<Image>().sprite = null;
            var shadeRect = modalShade.GetComponent<RectTransform>();
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            card = Panel("Round card", hud, Vector2.zero, new Vector2(630, 440), new Color(.98f, .97f, .91f, .98f));
            style.Card(card, ink);
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(.5f, .5f);
            Label(card.transform, "3 MINUTE DESTRUCTION PLAYGROUND", new Vector2(38, -30), new Vector2(570, 27), 16, new Color(.22f, .55f, .43f));
            cardTitle = Label(card.transform, "", new Vector2(38, -78), new Vector2(560, 62), 42, ink);
            cardTitle.fontStyle = FontStyle.Bold;
            cardBody = Label(card.transform, "", new Vector2(40, -156), new Vector2(550, 110), 21, ink);
            cardBody.fontStyle = FontStyle.Bold;
            var button = Panel("Start button", card.transform, new Vector2(40, -285), new Vector2(550, 60), yellow);
            button.AddComponent<Button>().onClick.AddListener(game.StartOrRestart);
            buttonText = Label(button.transform, "", new Vector2(0, -10), new Vector2(550, 42), 23, ink);
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.fontStyle = FontStyle.Bold;
            var back = Panel("Title button", card.transform, new Vector2(40, -365), new Vector2(550, 44), ink);
            back.AddComponent<Button>().onClick.AddListener(game.ReturnToTitle);
            var backLabel = Label(back.transform, "タイトルへ", new Vector2(0, -5), new Vector2(550, 34), 19, Color.white);
            backLabel.alignment = TextAnchor.MiddleCenter;
            titleView = new TankTownTitleScreen(game, canvas, font, style);
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
            item.GetComponent<Image>().sprite = style.Rounded;
            item.GetComponent<Image>().type = Image.Type.Sliced;
            return item;
        }

        private static void Bottom(GameObject item, bool right)
        {
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1 : 0, 0);
        }
        private static void TextOutline(Text target, Color color, float width)
        {
            var outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = color; outline.effectDistance = new Vector2(width, -width);
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
            score.text = $"{game.Destruction:0.0}%";
            fill.rectTransform.sizeDelta = new Vector2(278 * game.Destruction / 100, 14);
            bool edge = Mathf.Abs(game.Tank.transform.position.x) > game.World.HalfExtent - 13 && game.Tank.transform.position.x * game.Tank.transform.forward.x > 0
                || Mathf.Abs(game.Tank.transform.position.z) > game.World.HalfExtent - 13 && game.Tank.transform.position.z * game.Tank.transform.forward.z > 0;
            bool respawning = game.Tank.Respawning;
            notice.text = respawning ? "ドカーン！"
                : edge ? "海が見えたら、街の内側へ曲がろう！"
                : Time.time < chainUntil ? $"{chainDepth} CHAIN!"
                : Time.time < goUntil ? "GO！" : "";
            bool chainVisible = Time.time < chainUntil && !respawning && !edge;
            notice.color = respawning || chainVisible ? new Color(1, .68f, .18f) : Color.white;
            bool celebrate = chainVisible || Time.time < goUntil && !edge && !respawning;
            notice.fontSize = respawning ? 34 : celebrate ? 35 : 22;
            notice.font = celebrate ? displayFont : font;
            noticeCaption.text = respawning ? $"安全な場所へもどるよ  −{game.Settings.respawnPenalty:0.#}秒" : "連鎖爆破！";
            noticeCaption.color = respawning ? Color.white : yellow;
            noticeCaption.gameObject.SetActive(respawning || chainVisible);
            noticePanel.SetActive(game.IsPlaying && notice.text.Length > 0);
            for (int i = 0; i < 4; i++)
            {
                var kind = (UpgradeKind)i;
                statRows[i].text = $"Lv.{game.Stats.Level(kind)}";
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
            emptyItem.gameObject.SetActive(shown == ItemKind.None);
            if (shown != ItemKind.None) heldIcon.sprite = game.World.SpecialIcons.Sprite(shown);
            heldName.text = shown != ItemKind.None ? ItemIcons.Label(shown) : "ビルをくずそう！";
            heldName.fontSize = shown == ItemKind.Bulldozer ? 19 : shown == ItemKind.None ? 18 : 22;
            heldHint.text = game.Items.Held != ItemKind.None ? game.Items.Weapon == ItemKind.None ? "SPACE でつかう" : "次の砲撃でつかう" : shown != ItemKind.None ? "発動中！" : "上のスペシャルをGET";
            activeItems.text = game.Items.ActiveSummary;
            activePanel.SetActive(activeItems.text.Length > 0);
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
            if (displayFont != null) Destroy(displayFont);
            style?.Dispose();
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
