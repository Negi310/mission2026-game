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
        internal TankTownResultsScreen ResultsView { get; private set; }
        internal TankTownMinimap Minimap { get; private set; }
        private Text liveRank, livePoints, nextRecord;
        private Text countdown;
        private CanvasGroup countdownGroup;
        private Text timer, score, notice, noticeCaption, cardTitle, cardBody, buttonText;
        private ToyHudStyle style;
        private Image fill;
        private Image viewFade;
        private Text aim;
        internal TankTownUpgradeHud UpgradesView { get; private set; }
        internal TankTownSpecialHud SpecialsView { get; private set; }
        private Text pickupText;
        private float pickupUntil, lastPickupAt = -10;
        private int pickupCount;
        private TankTownChainDisplay chainDisplay;
        private GameObject card, noticePanel, modalShade;
        private readonly Color ink = new Color(.08f, .17f, .22f);
        private readonly Color yellow = new Color(.81f, 1, .14f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void RefreshLayoutForCapture()
        {
            if (game.State == RoundState.Ready) titleView.Fit();
            if (game.State == RoundState.Results) ResultsView.Tick(0);
            UpgradesView.Tick(0); SpecialsView.Tick(0); LateUpdate();
        }
#endif
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

            var clock = Panel("Clock", hud, new Vector2(0, -24), new Vector2(230, 92), ink);
            var clockRect = (RectTransform)clock.transform;
            clockRect.anchorMin = clockRect.anchorMax = clockRect.pivot = new Vector2(.5f, 1);
            style.Card(clock, new Color(.83f, .93f, 1));
            timer = Label(clock.transform, "01:00", new Vector2(10, -27), new Vector2(210, 60), 48, Color.white);
            timer.font = displayFont; timer.alignment = TextAnchor.MiddleCenter;
            timer.fontStyle = FontStyle.Bold;
            var clockHeading = Label(clock.transform, "のこり時間", new Vector2(10, -9), new Vector2(210, 20), 12, new Color(.64f, .88f, 1));
            clockHeading.alignment = TextAnchor.MiddleCenter;
            var progress = Panel("Destruction", hud, new Vector2(28, -24), new Vector2(280, 140), ink);
            style.Card(progress, new Color(.83f, .93f, 1));
            var scoreLabel = Label(progress.transform, "破壊度", new Vector2(16, -15), new Vector2(98, 25), 17, Color.white);
            scoreLabel.fontStyle = FontStyle.Bold;
            score = Label(progress.transform, "0.0%", new Vector2(16, -44), new Vector2(154, 52), 42, Color.white);
            score.font = displayFont;
            score.fontStyle = FontStyle.Bold;
            score.resizeTextForBestFit = true; score.resizeTextMinSize = 30; score.resizeTextMaxSize = 42;
            var track = Panel("Progress track", progress.transform, new Vector2(16, -105), new Vector2(248, 8), new Color(.32f, .44f, .47f));
            fill = Panel("Progress fill", track.transform, Vector2.zero, new Vector2(0, 8), new Color(.59f, .9f, .06f)).GetComponent<Image>();
            var ranking = Panel("Live ranking", progress.transform, new Vector2(218, -13), new Vector2(46, 31), yellow);
            ranking.GetComponent<Image>().raycastTarget = false;
            liveRank = Label(ranking.transform, "", Vector2.zero, new Vector2(46, 31), 17, ink);
            liveRank.alignment = TextAnchor.MiddleCenter; liveRank.fontStyle = FontStyle.Bold;
            livePoints = Label(progress.transform, "", new Vector2(154, -56), new Vector2(110, 33), 18, new Color(.64f, .88f, 1));
            livePoints.alignment = TextAnchor.MiddleRight;
            livePoints.resizeTextForBestFit = true; livePoints.resizeTextMinSize = 12; livePoints.resizeTextMaxSize = 18;
            nextRecord = Label(progress.transform, "", new Vector2(16, -119), new Vector2(248, 18), 11, new Color(.64f, .88f, 1));
            Minimap = new TankTownMinimap(game, hud, font, style);
            var widgets = new TankTownHudWidgets(font, displayFont, style);
            UpgradesView = new TankTownUpgradeHud(game, hud, widgets);
            SpecialsView = new TankTownSpecialHud(game, hud, widgets, style);
            pickupText = Label(hud, "", Vector2.zero, new Vector2(270, 34), 24, Color.white);
            pickupText.name = "Pickup batch count";
            pickupText.rectTransform.anchorMin = pickupText.rectTransform.anchorMax = pickupText.rectTransform.pivot = Vector2.one * .5f;
            pickupText.alignment = TextAnchor.MiddleCenter;
            pickupText.fontStyle = FontStyle.Bold; TextOutline(pickupText, ink, 1.5f);
            game.Stats.Changed += ShowUpgrade;
            noticePanel = Panel("Respawn notice", hud, new Vector2(0, -140), new Vector2(430, 92), Color.clear);
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
            var chainRoot = new GameObject("Chain celebration", typeof(RectTransform)); chainRoot.transform.SetParent(hud, false);
            chainDisplay = chainRoot.AddComponent<TankTownChainDisplay>(); chainDisplay.Initialize(game, font, displayFont, style);
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
            Label(card.transform, "1 MINUTE DESTRUCTION PLAYGROUND", new Vector2(38, -30), new Vector2(570, 27), 16, new Color(.22f, .55f, .43f));
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
            titleView = new TankTownTitleScreen(game, canvas, font, displayFont, style);
            titleScreen = titleView.Root;
            ResultsView = new TankTownResultsScreen(game, canvas, font, displayFont, style);
            var countdownRoot = Panel("Start countdown", canvas, Vector2.zero, new Vector2(540, 232), Color.clear);
            var countdownRect = (RectTransform)countdownRoot.transform;
            countdownRect.anchorMin = countdownRect.anchorMax = countdownRect.pivot = Vector2.one * .5f;
            countdownRoot.GetComponent<Image>().raycastTarget = false;
            countdownGroup = countdownRoot.AddComponent<CanvasGroup>();
            countdownGroup.blocksRaycasts = false;
            countdown = Label(countdownRoot.transform, "", Vector2.zero, new Vector2(540, 232), 132, yellow);
            countdown.gameObject.name = "Countdown number";
            countdown.font = displayFont; countdown.fontStyle = FontStyle.Bold; countdown.alignment = TextAnchor.MiddleCenter;
            TextOutline(countdown, ink, 4);
            viewFade.transform.SetAsLastSibling();
            gameplay.SetActive(game.State != RoundState.Ready);
            // Explicitly include UI descendants in the title camera's restricted mask.
            foreach (var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 5;
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
            gameplay.SetActive(game.State != RoundState.Ready && game.State != RoundState.Results && game.State != RoundState.Restarting);
            ResultsView.Tick(Time.unscaledDeltaTime);
            countdown.text = game.Presentation.Countdown;
            float beat = Mathf.Clamp01(game.Presentation.BeatProgress);
            countdownGroup.alpha = countdown.text.Length == 0 ? 0 : Mathf.Min(1, (1 - beat) * 5);
            countdown.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.22f, 1, Mathf.Min(1, beat * 3));
            countdown.color = countdown.text == "GO!" ? yellow : Color.white;
            int seconds = Mathf.CeilToInt(game.Remaining);
            timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
            timer.color = seconds <= 20 ? new Color(1, .55f, .35f) : Color.white;
            score.text = $"{game.Destruction:0.0}%";
            int place = game.CurrentRank;
            liveRank.text = place == 0 ? "—" : place > TankTownRanking.Capacity ? "圏外" : $"{place}位";
            livePoints.text = $"{game.RoundScore:N0} pt";
            nextRecord.text = place > 1 ? $"次の順位まで  {game.Ranking.Records[Mathf.Min(place - 2, game.Ranking.Records.Count - 1)].Score - game.RoundScore + 1:N0} 点"
                : place == 1 ? "1位をめざそう！" : game.Ranking.Records.Count == 0 ? "街をこわして記録を作ろう！" : $"BEST  {game.BestScore:N0} 点";
            fill.rectTransform.sizeDelta = new Vector2(248 * game.Destruction / 100, 8);
            bool edge = Mathf.Abs(game.Tank.transform.position.x) > game.World.HalfExtent - 13 && game.Tank.transform.position.x * game.Tank.transform.forward.x > 0
                || Mathf.Abs(game.Tank.transform.position.z) > game.World.HalfExtent - 13 && game.Tank.transform.position.z * game.Tank.transform.forward.z > 0;
            bool respawning = game.Tank.Respawning;
            notice.text = !respawning && edge ? "海が見えたら、街の内側へ曲がろう！"
                : "";
            chainDisplay.SetObscured(respawning || edge);
            notice.color = Color.white;
            notice.fontSize = 22;
            notice.font = font;
            noticeCaption.text = "";
            noticeCaption.gameObject.SetActive(false);
            noticePanel.SetActive(game.IsPlaying && notice.text.Length > 0);
            float uiDelta = game.IsPlaying ? Time.deltaTime : 0;
            UpgradesView.Tick(uiDelta); SpecialsView.Tick(uiDelta);
            if (respawning || game.State == RoundState.Results) { pickupCount = 0; lastPickupAt = -10; }
            pickupText.text = pickupCount > 1 ? $"{pickupCount}個 GET!" : "";
            pickupText.gameObject.SetActive(game.IsPlaying && !respawning && Time.time < pickupUntil && pickupCount > 1);
            aim.gameObject.SetActive(game.IsPlaying && !game.Tank.Respawning);
            card.SetActive(game.State == RoundState.Paused);
            modalShade.SetActive(game.State == RoundState.Paused);
            if (game.State == RoundState.Paused)
            {
                cardTitle.text = "ひとやすみ";
                cardBody.text = "準備ができたら、もういちど出発！\n時間は止まっているよ。";
                buttonText.text = "つづける   →   SPACE";
            }
        }

        private void ShowUpgrade(UpgradeKind kind)
        {
            pickupCount = Time.time - lastPickupAt < .45f ? pickupCount + 1 : 1;
            lastPickupAt = Time.time; pickupUntil = Time.time + 1.4f;
        }
        private void OnDestroy()
        {
            if (game != null && game.Stats != null) game.Stats.Changed -= ShowUpgrade;
            UpgradesView?.Dispose(); SpecialsView?.Dispose();
            if (font != null) Destroy(font);
            if (displayFont != null) Destroy(displayFont);
            style?.Dispose();
            Minimap?.Dispose();
        }
        private void LateUpdate()
        {
            Minimap.Tick();
            viewFade.color = new Color(.02f, .065f, .10f, Mathf.Max(game.CameraRig.FadeOpacity, game.Presentation.CoverOpacity));
            aim.enabled = game.CameraRig.FadeOpacity <= .01f && !game.Tank.Relocating;
            // Project the actual muzzle direction after the comfort camera has moved.
            if (!game.IsPlaying || Camera.main == null) return;
            var viewportSize = Camera.main.targetTexture != null ? new Vector2(Camera.main.pixelWidth, Camera.main.pixelHeight) : new Vector2(Screen.width, Screen.height);
            float pickupHeight = game.Settings.pickupIconHeight * game.Tank.VehicleScale + game.Settings.pickupIconSize * .75f + .35f;
            Vector3 receipt = Camera.main.WorldToScreenPoint(game.Tank.transform.position + Vector3.up * pickupHeight);
            pickupText.enabled = receipt.z > 0 && game.CameraRig.FadeOpacity <= .01f && !game.Tank.Relocating;
            pickupText.rectTransform.anchoredPosition = ((Vector2)receipt - viewportSize * .5f) / canvas.GetComponent<Canvas>().scaleFactor;
            Vector3 projected = Camera.main.WorldToScreenPoint(game.Tank.Muzzle.position + game.Tank.Muzzle.forward * 70);
            aim.rectTransform.anchoredPosition = ((Vector2)projected - viewportSize * .5f) / canvas.GetComponent<Canvas>().scaleFactor;
        }
    }
}
