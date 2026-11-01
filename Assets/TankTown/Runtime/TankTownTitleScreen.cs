using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TankTown
{
    internal sealed class TankTownTitleScreen
    {
        public GameObject Root => menu.Root;
        private readonly TankTownGame game;
        private readonly TankTownMenuLayout menu;
        private readonly GameObject title, howTo;
        private readonly Button start, tutorialPlay;
        private bool helpWasOpen;
        private readonly Text[] stageLabels = new Text[4];
        private readonly Image[] stageMarks = new Image[4];
        private readonly TankTownResultArt[] stageArt = new TankTownResultArt[4];
        private readonly TankTownResultArt decorations;
        private static readonly Color[] StageColors = { TankTownResultArt.Aqua, TankTownResultArt.Purple, TankTownResultArt.Pink, new Color(1, .65f, .12f) };
        private readonly Texture2D shadeTexture;
        private readonly Sprite shadeSprite;
        private TownStage paintedStage = (TownStage)(-1);
        private static Color Ink => TankTownResultArt.Ink;
        private static Color Pink => TankTownResultArt.Pink;
        private static Color Purple => TankTownResultArt.Purple;

        public TankTownTitleScreen(TankTownGame owner, Transform canvas, Font font, Font displayFont, ToyHudStyle style)
        {
            game = owner;
            menu = new TankTownMenuLayout("Title screen", canvas, font, style, Color.clear);
            // The full-screen shade remains continuous at every aspect ratio.
            shadeTexture = new Texture2D(128, 1, TextureFormat.RGBA32, false)
            { name = "Title left shade", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var shadePixels = new Color[128];
            for (int x = 0; x < shadePixels.Length; x++)
            {
                float fade = 1 - Mathf.SmoothStep(0, .62f, x / 127f);
                shadePixels[x] = new Color(.12f, .62f, .94f, .40f * fade);
            }
            shadeTexture.SetPixels(shadePixels); shadeTexture.Apply(false, true);
            shadeSprite = Sprite.Create(shadeTexture, new Rect(0, 0, 128, 1), Vector2.one * .5f);
            var shade = menu.Root.GetComponent<Image>(); shade.sprite = shadeSprite; shade.color = Color.white;
            var parent = menu.Layout;
            title = menu.Panel(parent, "Title content", Vector2.zero, new Vector2(1440, 900), Color.clear);
            decorations = Art(title.transform, "Title party decorations", Vector2.zero, new Vector2(1440, 900), ResultArtKind.TitleDecor);
            var logo = menu.Label(title.transform, "Title wordmark", "<color=#FFD115>TANK</color> <color=#0DCDF2>TOWN</color>", new Vector2(48, -177), new Vector2(608, 135), 91, Color.white, displayFont);
            logo.horizontalOverflow = HorizontalWrapMode.Overflow; logo.alignment = TextAnchor.MiddleLeft;
            var logoShadow = logo.gameObject.AddComponent<Shadow>(); logoShadow.effectColor = Pink; logoShadow.effectDistance = new Vector2(0, -8);
            var outline = logo.gameObject.AddComponent<Outline>(); outline.effectColor = Ink; outline.effectDistance = new Vector2(4, -4);
            var stages = menu.Panel(title.transform, "Title stage selector", new Vector2(48, -350), new Vector2(608, 184), Color.clear);
            StageButton(stages.transform, TownStage.Town, "Town stage button", "いつもの街");
            StageButton(stages.transform, TownStage.BigCity, "Large stage button", "大きな街");
            StageButton(stages.transform, TownStage.CandyTown, "Candy stage button", "お菓子の街");
            StageButton(stages.transform, TownStage.AmusementPark, "Amusement stage button", "遊園地");
            start = SoftButton(title.transform, "Title play button", new Vector2(48, -565), new Vector2(608, 100), Pink, game.StartOrRestart);
            var playVisual = start.targetGraphic.transform;
            var playLabel = menu.Label(playVisual, "Title play label", "あそぶ", new Vector2(60, -4), new Vector2(396, 92), 43, Color.white);
            playLabel.alignment = TextAnchor.MiddleCenter;
            Keycap(playVisual, "Title space key", "SPACE", new Vector2(476, -29), new Vector2(88, 38), 17);
            var help = SoftButton(title.transform, "How to button", new Vector2(48, -692), new Vector2(294, 62), Purple, game.ToggleHowTo);
            var helpVisual = help.targetGraphic.transform;
            var helpLabel = menu.Label(helpVisual, "How to label", "あそびかた", new Vector2(22, 0), new Vector2(198, 56), 28, Color.white);
            helpLabel.alignment = TextAnchor.MiddleCenter;
            Keycap(helpVisual, "Title help key", "H", new Vector2(220, -9), new Vector2(38, 38), 23);

            var tutorial = new TankTownHowToScreen(game, menu, style);
            howTo = tutorial.Root; tutorialPlay = tutorial.Play;
            howTo.SetActive(false);
            Fit();
            Select(start);
        }

        public void Fit()
        {
            menu.Fit();
            decorations.SetMotion(0, Time.unscaledTime);
            if (paintedStage != game.Stage)
            {
                paintedStage = game.Stage;
                for (int i = 0; i < stageLabels.Length; i++)
                {
                    bool selected = (TownStage)i == game.Stage;
                    stageMarks[i].enabled = selected;
                    stageArt[i].SetAccent(selected ? StageColors[i] : Color.Lerp(StageColors[i], Color.white, .72f));
                    stageLabels[i].color = selected && (i == 1 || i == 2) ? Color.white : Ink;
                }
            }
            title.SetActive(!game.HowToOpen); howTo.SetActive(game.HowToOpen);
            if (helpWasOpen != game.HowToOpen) Select(game.HowToOpen ? tutorialPlay : start);
            helpWasOpen = game.HowToOpen;
        }

        private static void Select(Button button)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        public void Dispose()
        {
            Object.Destroy(shadeSprite); Object.Destroy(shadeTexture);
        }

        private Button SoftButton(Transform parent, string name, Vector2 point, Vector2 size, Color tint, UnityEngine.Events.UnityAction action)
        {
            var panel = menu.Panel(parent, name, point, size, Color.clear);
            panel.GetComponent<Image>().raycastTarget = true;
            // Animate only the artwork so the pointer's hit rectangle never moves.
            var graphic = Art(panel.transform, name + " visual", Vector2.zero, size, ResultArtKind.TitleAction);
            graphic.SetAccent(tint);
            var visualRect = (RectTransform)graphic.transform;
            visualRect.anchorMin = visualRect.anchorMax = visualRect.pivot = Vector2.one * .5f;
            visualRect.anchoredPosition = Vector2.zero;
            var button = panel.AddComponent<Button>(); button.targetGraphic = graphic; button.onClick.AddListener(action);
            var colors = button.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1, 1, .91f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(.86f, .81f, .94f);
            colors.fadeDuration = .09f; button.colors = colors;
            panel.AddComponent<TankTownMenuHover>().Initialize(button, visualRect);
            return button;
        }

        private void StageButton(Transform parent, TownStage stage, string name, string label)
        {
            int index = (int)stage;
            var button = SoftButton(parent, name, new Vector2(index % 2 * 314, -(index / 2) * 100), new Vector2(294, 84), StageColors[index], () => game.SelectStage(stage));
            var visual = button.targetGraphic.transform;
            var text = menu.Label(visual, name + " label", label, new Vector2(14, -4), new Vector2(250, 64), 25, Ink);
            text.alignment = TextAnchor.MiddleCenter;
            stageLabels[index] = text;
            stageArt[index] = (TankTownResultArt)button.targetGraphic;
            stageMarks[index] = menu.Panel(visual, name + " selected", new Vector2(28, -65), new Vector2(224, 5), Color.white).GetComponent<Image>();
        }

        private void Keycap(Transform parent, string name, string label, Vector2 point, Vector2 size, int fontSize)
        {
            var key = menu.Panel(parent, name, point, size, new Color(.18f, .08f, .36f, .43f));
            var text = menu.Label(key.transform, name + " label", label, Vector2.zero, size, fontSize, Color.white);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private TankTownResultArt Art(Transform parent, string name, Vector2 point, Vector2 size, ResultArtKind kind)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TankTownResultArt)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = point; rect.sizeDelta = size;
            var art = item.GetComponent<TankTownResultArt>(); art.Initialize(kind, Pink); return art;
        }

    }
}
