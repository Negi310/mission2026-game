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
        private readonly Texture2D shadeTexture;
        private readonly Sprite shadeSprite;
        private TownStage paintedStage = (TownStage)(-1);
        private static Color Ink => TankTownMenuLayout.Ink;
        private static Color Lime => TankTownMenuLayout.Lime;
        private static Color Cyan => TankTownMenuLayout.Cyan;

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
                shadePixels[x] = new Color(.015f, .055f, .13f, .8f * fade);
            }
            shadeTexture.SetPixels(shadePixels); shadeTexture.Apply(false, true);
            shadeSprite = Sprite.Create(shadeTexture, new Rect(0, 0, 128, 1), Vector2.one * .5f);
            var shade = menu.Root.GetComponent<Image>(); shade.sprite = shadeSprite; shade.color = Color.white;
            var parent = menu.Layout;
            title = menu.Panel(parent, "Title content", Vector2.zero, new Vector2(1440, 900), Color.clear);
            var logo = menu.Label(title.transform, "Title wordmark", "<color=#FFC416>TANK</color> <color=#15D4FF>TOWN</color>", new Vector2(20, -180), new Vector2(640, 135), 94, Color.white, displayFont);
            logo.horizontalOverflow = HorizontalWrapMode.Overflow; logo.alignment = TextAnchor.MiddleLeft;
            var logoShadow = logo.gameObject.AddComponent<Shadow>(); logoShadow.effectColor = new Color(.01f, .025f, .05f, .65f); logoShadow.effectDistance = new Vector2(3, -5);
            var stages = menu.Panel(title.transform, "Title stage selector", new Vector2(20, -384), new Vector2(600, 80), Color.white);
            style.TitleControl(stages, new Color(.035f, .10f, .18f, .9f));
            StageButton(stages.transform, TownStage.Town, "Town stage button", "いつもの街");
            StageButton(stages.transform, TownStage.BigCity, "Large stage button", "大きな街");
            StageButton(stages.transform, TownStage.CandyTown, "Candy stage button", "お菓子の街");
            StageButton(stages.transform, TownStage.AmusementPark, "Amusement stage button", "遊園地");
            start = SoftButton(title.transform, "Title play button", new Vector2(20, -500), new Vector2(600, 100), Lime, game.StartOrRestart);
            var playVisual = start.targetGraphic.transform;
            style.TitleControl(playVisual.gameObject, Lime, true);
            var playLabel = menu.Label(playVisual, "Title play label", "あそぶ", new Vector2(98, -4), new Vector2(404, 92), 43, Ink);
            playLabel.alignment = TextAnchor.MiddleCenter;
            Keycap(playVisual, "Title space key", "SPACE", new Vector2(480, -31), new Vector2(88, 38), 17, Ink, style);
            var help = SoftButton(title.transform, "How to button", new Vector2(20, -634), new Vector2(250, 62), Color.clear, game.ToggleHowTo);
            var helpVisual = help.targetGraphic.transform;
            var helpLabel = menu.Label(helpVisual, "How to label", "あそびかた", Vector2.zero, new Vector2(174, 62), 28, new Color(1, .97f, .83f));
            helpLabel.alignment = TextAnchor.MiddleLeft;
            Keycap(helpVisual, "Title help key", "H", new Vector2(186, -10), new Vector2(44, 42), 23, Color.white, style);

            var tutorial = new TankTownHowToScreen(game, menu, style);
            howTo = tutorial.Root; tutorialPlay = tutorial.Play;
            howTo.SetActive(false);
            Fit();
            Select(start);
        }

        public void Fit()
        {
            menu.Fit();
            if (paintedStage != game.Stage)
            {
                paintedStage = game.Stage;
                for (int i = 0; i < stageLabels.Length; i++)
                {
                    bool selected = (TownStage)i == game.Stage;
                    stageMarks[i].enabled = selected;
                    stageLabels[i].color = selected ? Color.white : new Color(.73f, .83f, .91f);
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
            var visual = menu.Panel(panel.transform, name + " visual", Vector2.zero, size, tint);
            var visualRect = (RectTransform)visual.transform;
            visualRect.anchorMin = visualRect.anchorMax = visualRect.pivot = Vector2.one * .5f;
            visualRect.anchoredPosition = Vector2.zero;
            var image = visual.GetComponent<Image>();
            bool transparent = tint.a == 0;
            if (transparent) image.color = Color.white;
            var button = panel.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var colors = button.colors; colors.normalColor = transparent ? Color.clear : new Color(.98f, .98f, .98f);
            colors.highlightedColor = transparent ? new Color(1, 1, 1, .08f) : Color.white;
            colors.selectedColor = transparent ? colors.highlightedColor : colors.normalColor;
            colors.pressedColor = transparent ? new Color(1, 1, 1, .16f) : new Color(.83f, .89f, .94f);
            colors.fadeDuration = .09f; button.colors = colors;
            image.canvasRenderer.SetColor(colors.normalColor);
            panel.AddComponent<TankTownMenuHover>().Initialize(button, visualRect);
            return button;
        }

        private void StageButton(Transform parent, TownStage stage, string name, string label)
        {
            int index = (int)stage;
            var button = SoftButton(parent, name, new Vector2(index * 150, 0), new Vector2(150, 80), Color.clear, () => game.SelectStage(stage));
            var visual = button.targetGraphic.transform;
            var text = menu.Label(visual, name + " label", label, new Vector2(5, -5), new Vector2(140, 64), 21, Color.white);
            text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Overflow;
            stageLabels[index] = text;
            stageMarks[index] = menu.Panel(visual, name + " selected", new Vector2(15, -66), new Vector2(120, 5), Cyan).GetComponent<Image>();
            if (index > 0)
            {
                var divider = menu.Panel(button.transform, name + " divider", new Vector2(0, -16), new Vector2(1, 48), new Color(.47f, .78f, .94f, .22f)).GetComponent<Image>();
                divider.sprite = null;
            }
        }

        private void Keycap(Transform parent, string name, string label, Vector2 point, Vector2 size, int fontSize, Color color, ToyHudStyle style)
        {
            var key = menu.Panel(parent, name, point, size, Color.white);
            style.TitleControl(key, new Color(.64f, .77f, .86f, .13f));
            var text = menu.Label(key.transform, name + " label", label, Vector2.zero, size, fontSize, color);
            text.alignment = TextAnchor.MiddleCenter;
        }

    }
}
