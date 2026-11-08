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
        private TankTownPauseScreen pauseView;
        internal bool ConfirmMenu()
        {
            if (game.Input.GetComponent<TankControlTuningPanel>()?.Visible == true || game.Input.GetComponent<MicrobitDiagnosticsPanel>()?.Visible == true) return true;
            if (game.State == RoundState.Ready) return titleView?.Confirm() ?? false;
            if (game.State == RoundState.Results) return ResultsView?.Confirm() ?? false;
            if (game.State == RoundState.Paused) return pauseView?.Confirm() ?? false;
            return false;
        }
        internal TankTownResultsScreen ResultsView { get; private set; }
        internal TankTownMinimap Minimap { get; private set; }
        private Text countdown;
        private CanvasGroup countdownGroup;
        private Text timer, score;
        private ToyHudStyle style;
        private Image viewFade;
        private Image pointsBacking;
        private readonly TextGenerator timerMetrics = new TextGenerator(), pointsMetrics = new TextGenerator();
        private TankTownReticle aim;
        internal TankTownUpgradeHud UpgradesView { get; private set; }
        internal TankTownSpecialHud SpecialsView { get; private set; }
        private Text pickupText;
        private float pickupUntil, lastPickupAt = -10;
        private int pickupCount;
        private TankTownChainDisplay chainDisplay;
        private readonly Color ink = new Color(.08f, .17f, .22f);
        private readonly Color yellow = new Color(.81f, 1, .14f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Retain the map only for explicit legacy map/performance diagnostics.
        internal TankTownMinimap EnableDiagnosticMinimap()
            => Minimap ??= new TankTownMinimap(game, gameplay.transform, font, style);

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
            font = Resources.Load<Font>("Fonts/NotoSansJP-Regular");
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 24);
            displayFont = font;
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

            var clock = new GameObject("Clock", typeof(RectTransform));
            clock.transform.SetParent(hud, false);
            var clockRect = (RectTransform)clock.transform;
            clockRect.anchorMin = clockRect.anchorMax = clockRect.pivot = new Vector2(.5f, 1);
            clockRect.anchoredPosition = new Vector2(0, -24); clockRect.sizeDelta = new Vector2(226, 72);
            style.Header(clock);
            timer = Label(clock.transform, "01:20", new Vector2(12, 0), new Vector2(202, 72), 58, Color.white);
            timer.name = "Round time";
            timer.font = displayFont; timer.alignment = TextAnchor.MiddleCenter;
            timer.fontStyle = FontStyle.Bold;
            timer.gameObject.AddComponent<Shadow>().effectColor = new Color(.01f, .03f, .05f, .45f);
            var pointsPlate = new GameObject("Points backing", typeof(RectTransform)); pointsPlate.transform.SetParent(hud, false);
            var pointsRect = (RectTransform)pointsPlate.transform;
            pointsRect.anchorMin = pointsRect.anchorMax = pointsRect.pivot = new Vector2(0, 1);
            pointsRect.anchoredPosition = new Vector2(24, -24); pointsRect.sizeDelta = new Vector2(140, 72);
            pointsBacking = style.Header(pointsPlate);
            score = Label(hud, "0 pt", new Vector2(38, -24), new Vector2(360, 72), 50, Color.white);
            score.name = "Points";
            score.font = displayFont;
            score.fontStyle = FontStyle.Bold;
            score.alignment = TextAnchor.MiddleLeft;
            score.resizeTextForBestFit = true; score.resizeTextMinSize = 40; score.resizeTextMaxSize = 50;
            score.gameObject.AddComponent<Shadow>().effectColor = new Color(.01f, .03f, .05f, .45f);
            var widgets = new TankTownHudWidgets(font, displayFont, style);
            UpgradesView = new TankTownUpgradeHud(game, hud, widgets);
            SpecialsView = new TankTownSpecialHud(game, hud, widgets, style);
            pickupText = Label(hud, "", Vector2.zero, new Vector2(270, 34), 24, Color.white);
            pickupText.name = "Pickup batch count";
            pickupText.rectTransform.anchorMin = pickupText.rectTransform.anchorMax = pickupText.rectTransform.pivot = Vector2.one * .5f;
            pickupText.alignment = TextAnchor.MiddleCenter;
            pickupText.fontStyle = FontStyle.Bold; TextOutline(pickupText, ink, 1.5f);
            game.Stats.Changed += ShowUpgrade;
            var chainRoot = new GameObject("Chain celebration", typeof(RectTransform)); chainRoot.transform.SetParent(hud, false);
            chainDisplay = chainRoot.AddComponent<TankTownChainDisplay>(); chainDisplay.Initialize(game, font, displayFont, style);
            var reticle = new GameObject("Cannon reticle", typeof(RectTransform), typeof(TankTownReticle));
            reticle.transform.SetParent(hud, false);
            aim = reticle.GetComponent<TankTownReticle>();
            aim.rectTransform.anchorMin = aim.rectTransform.anchorMax = aim.rectTransform.pivot = Vector2.one * .5f;
            aim.rectTransform.sizeDelta = new Vector2(80, 80);
            aim.raycastTarget = false;
            titleView = new TankTownTitleScreen(game, canvas, font, displayFont, style);
            titleScreen = titleView.Root;
            ResultsView = new TankTownResultsScreen(game, canvas, font, displayFont, style);
            pauseView = new TankTownPauseScreen(game, canvas, font, style);
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
            pauseView.Tick(Time.unscaledDeltaTime);
            countdown.text = game.Presentation.Countdown;
            float beat = Mathf.Clamp01(game.Presentation.BeatProgress);
            countdownGroup.alpha = countdown.text.Length == 0 ? 0 : Mathf.Min(1, (1 - beat) * 5);
            countdown.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.22f, 1, Mathf.Min(1, beat * 3));
            countdown.color = countdown.text == "GO!" ? yellow : Color.white;
            int seconds = Mathf.CeilToInt(game.Remaining);
            timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
            timer.color = seconds <= 20 ? new Color(1, .55f, .35f) : Color.white;
            score.text = $"{game.RoundScore:N0} pt";
            pointsBacking.rectTransform.sizeDelta = new Vector2(Mathf.Min(score.preferredWidth, score.rectTransform.rect.width) + 30, 72);
            bool respawning = game.Tank.Respawning;
            chainDisplay.SetObscured(respawning);
            float uiDelta = game.IsPlaying ? Time.deltaTime : 0;
            UpgradesView.Tick(uiDelta); SpecialsView.Tick(uiDelta);
            if (respawning || game.State == RoundState.Results) { pickupCount = 0; lastPickupAt = -10; }
            pickupText.text = pickupCount > 1 ? $"+{pickupCount}" : "";
            pickupText.gameObject.SetActive(game.IsPlaying && !respawning && Time.time < pickupUntil && pickupCount > 1);
            aim.gameObject.SetActive(game.IsPlaying && !game.Tank.Respawning);
        }

        private void ShowUpgrade(UpgradeKind kind)
        {
            pickupCount = Time.time - lastPickupAt < .45f ? pickupCount + 1 : 1;
            lastPickupAt = Time.time; pickupUntil = Time.time + 1.4f;
        }
        private void OnDestroy()
        {
            if (game != null && game.Stats != null) game.Stats.Changed -= ShowUpgrade;
            UpgradesView?.Dispose(); SpecialsView?.Dispose(); titleView?.Dispose();
            // The bundled font is a shared Resources asset.
            style?.Dispose();
            Minimap?.Dispose();
            ((System.IDisposable)timerMetrics).Dispose(); ((System.IDisposable)pointsMetrics).Dispose();
        }
        private static void CenterHeaderText(Text label, TextGenerator metrics, string reference, float baseY)
        {
            var rect = label.rectTransform;
            var settings = label.GetGenerationSettings(rect.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = label.resizeTextForBestFit ? label.resizeTextMaxSize : label.fontSize;
            if (!metrics.Populate(reference, settings)) return;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            var vertices = metrics.verts;
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                float low = vertices[i].position.y, high = low;
                for (int j = 1; j < 4; j++) { low = Mathf.Min(low, vertices[i + j].position.y); high = Mathf.Max(high, vertices[i + j].position.y); }
                if (high - low < .001f) continue; // Spaces and the generator's trailing empty quad.
                bottom = Mathf.Min(bottom, low); top = Mathf.Max(top, high);
            }
            if (float.IsInfinity(bottom)) return;
            // A fixed sample keeps changing digits on one baseline, including the "pt" descender.
            float offset = rect.rect.center.y - (bottom + top) * .5f / label.pixelsPerUnit;
            var position = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(position.x, baseY + offset);
        }
        private void LateUpdate()
        {
            CenterHeaderText(timer, timerMetrics, "00:00", 0);
            CenterHeaderText(score, pointsMetrics, "0 pt", -24);
            Minimap?.Tick();
            viewFade.color = new Color(.02f, .065f, .10f, Mathf.Max(game.CameraRig.FadeOpacity, game.Presentation.CoverOpacity));
            aim.enabled = game.IsPlaying && game.CameraRig.FadeOpacity <= .01f && !game.Tank.Relocating;
            // Project the actual muzzle direction after the comfort camera has moved.
            if (!game.IsPlaying || Camera.main == null) return;
            var viewportSize = Camera.main.targetTexture != null ? new Vector2(Camera.main.pixelWidth, Camera.main.pixelHeight) : new Vector2(Screen.width, Screen.height);
            float pickupHeight = game.Settings.pickupIconHeight * game.Tank.VehicleScale + game.Settings.pickupIconSize * .75f + .35f;
            Vector3 receipt = Camera.main.WorldToScreenPoint(game.Tank.transform.position + Vector3.up * pickupHeight);
            pickupText.enabled = receipt.z > 0 && game.CameraRig.FadeOpacity <= .01f && !game.Tank.Relocating;
            pickupText.rectTransform.anchoredPosition = ((Vector2)receipt - viewportSize * .5f) / canvas.GetComponent<Canvas>().scaleFactor;
            Vector3 projected = Camera.main.WorldToScreenPoint(game.Tank.Muzzle.position + game.Tank.Muzzle.forward * 70);
            aim.rectTransform.anchoredPosition = ((Vector2)projected - viewportSize * .5f) / canvas.GetComponent<Canvas>().scaleFactor;
            var solution = game.Tank.CannonAim.Resolve(Camera.main);
            var target = Camera.main.WorldToScreenPoint(solution.Point);
            bool hasTarget = solution.Target != null && target.z > 0;
            aim.Tick(game.Tank.Cooldown01, hasTarget, hasTarget ? ((Vector2)target - (Vector2)projected) / canvas.GetComponent<Canvas>().scaleFactor : Vector2.zero);
        }
    }
}

