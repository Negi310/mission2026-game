using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TankTown
{
    internal sealed class TankTownTitleScreen
    {
        public GameObject Root { get; }
        private readonly Font font;
        private readonly ToyHudStyle style;
        private readonly Color ink = new Color(.035f, .10f, .17f);
        private readonly Color lime = new Color(.81f, 1, .14f);
        private readonly Color cyan = new Color(.08f, .83f, 1);
        private readonly RectTransform layoutRect;

        public TankTownTitleScreen(TankTownGame game, Transform canvas, Font uiFont, ToyHudStyle uiStyle)
        {
            font = uiFont;
            style = uiStyle;
            Root = new GameObject("Title screen", typeof(RectTransform), typeof(Image));
            Root.transform.SetParent(canvas, false);
            var rect = Root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Root.GetComponent<Image>().color = ink;
            var layout = Panel(Root.transform, "Title layout", Vector2.zero, new Vector2(1440, 900), Color.clear);
            layoutRect = layout.GetComponent<RectTransform>();
            layout.GetComponent<RectTransform>().anchorMin = layout.GetComponent<RectTransform>().anchorMax = new Vector2(.5f, .5f);
            layout.GetComponent<RectTransform>().anchoredPosition = new Vector2(-720, 450);
            var parent = layout.transform;
            Panel(parent, "Sky accent", new Vector2(720, -52), new Vector2(648, 548), new Color(.065f, .19f, .27f));
            Text(parent, "1 MINUTE DESTRUCTION PLAYGROUND", new Vector2(72, -62), new Vector2(625, 35), 20, cyan);
            Text(parent, "TANK", new Vector2(66, -96), new Vector2(630, 140), 108, new Color(1, .75f, .08f));
            Text(parent, "TOWN", new Vector2(66, -208), new Vector2(630, 140), 108, cyan);
            Text(parent, "まちを、バラバラに！", new Vector2(74, -338), new Vector2(640, 54), 36, Color.white);
            Text(parent, "1分で、街をどこまでこわせる？", new Vector2(76, -399), new Vector2(610, 38), 24, new Color(.66f, .79f, .84f));
            var start = Panel(parent, "Title play button", new Vector2(72, -465), new Vector2(572, 82), lime);
            style.Card(start, new Color(.9f, 1, .65f));
            var button = start.AddComponent<Button>(); button.onClick.AddListener(game.StartOrRestart);
            var colors = button.colors; colors.highlightedColor = new Color(.9f, 1, .65f); colors.pressedColor = new Color(.6f, .85f, .2f); button.colors = colors;
            var label = Text(start.transform, "あそぶ  →", new Vector2(0, -11), new Vector2(572, 61), 34, ink);
            label.alignment = TextAnchor.MiddleCenter;
            Text(parent, "ENTER / SPACE / パッドをたたいてスタート", new Vector2(78, -564), new Vector2(620, 34), 19, Color.white);
            DrawTank(parent);
            Text(parent, "ビルの色が、スペシャルの目印！", new Vector2(744, -473), new Vector2(606, 40), 25, Color.white).alignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 6; i++)
            {
                var icon = Panel(parent, "Title special icon", new Vector2(779 + i * 91, -528), new Vector2(64, 64), Color.white).GetComponent<Image>();
                icon.sprite = game.World.SpecialIcons.Sprite((ItemKind)i);
                icon.type = Image.Type.Simple;
                Text(parent, ItemIcons.Label((ItemKind)i), new Vector2(766 + i * 91, -597), new Vector2(90, 24), 13, ItemIcons.Color((ItemKind)i)).alignment = TextAnchor.MiddleCenter;
            }
            Instruction(parent, new Vector2(72, -658), "01", "ハンドルで走る", "A / D  または  ← / →", cyan);
            Instruction(parent, new Vector2(510, -658), "02", "たたいて、発射！", "SPACE / J / K", lime);
            Instruction(parent, new Vector2(948, -658), "03", "こわして、集めよう", "赤いタンクで連鎖爆破！", new Color(1, .36f, .22f));
            Text(parent, "1分間の破壊度にチャレンジ！    ／    ESC：ひとやすみ", new Vector2(72, -845), new Vector2(1296, 30), 18, new Color(.6f, .74f, .8f)).alignment = TextAnchor.MiddleCenter;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(start);
            Fit();
        }

        public void Fit()
        {
            var bounds = ((RectTransform)Root.transform).rect.size;
            float scale = Mathf.Min(bounds.x / 1440, bounds.y / 900);
            if (scale <= 0) return;
            layoutRect.localScale = Vector3.one * scale;
            layoutRect.anchoredPosition = new Vector2(-720, 450) * scale;
        }

        private void DrawTank(Transform parent)
        {
            // A static, code-drawn illustration keeps the title legible and still.
            for (int i = 0; i < 5; i++)
            {
                var color = i % 2 == 0 ? cyan : new Color(1, .42f, .2f);
                Panel(parent, "Toy skyline", new Vector2(778 + i * 109, -220 + (i % 3) * 34), new Vector2(84, 176 - (i % 3) * 34), color * .52f);
                for (int n = 0; n < 3; n++)
                    Panel(parent, "Toy window", new Vector2(794 + i * 109, -238 - n * 34 + (i % 3) * 34), new Vector2(50, 15), new Color(.07f, .25f, .34f));
            }
            var tank = Panel(parent, "Title tank illustration", new Vector2(836, -291), new Vector2(368, 156), Color.clear);
            tank.transform.localRotation = Quaternion.Euler(0, 0, 8);
            Panel(tank.transform, "Tank shadow", new Vector2(-14, -117), new Vector2(398, 38), ink);
            Panel(tank.transform, "Track", new Vector2(0, -91), new Vector2(368, 52), new Color(.13f, .22f, .29f));
            for (int i = 0; i < 8; i++) Panel(tank.transform, "Track pad", new Vector2(12 + i * 43, -100), new Vector2(29, 32), new Color(.69f, .83f, .82f));
            Panel(tank.transform, "Hull", new Vector2(14, -30), new Vector2(332, 73), lime);
            Panel(tank.transform, "Hull highlight", new Vector2(14, -30), new Vector2(332, 12), new Color(.95f, 1, .65f));
            Panel(tank.transform, "Turret", new Vector2(83, 18), new Vector2(138, 53), lime);
            Panel(tank.transform, "Barrel", new Vector2(212, 3), new Vector2(178, 21), new Color(1, .94f, .77f));
            Panel(tank.transform, "Muzzle", new Vector2(376, 9), new Vector2(26, 33), cyan);
            Panel(tank.transform, "Turret hatch", new Vector2(113, 29), new Vector2(58, 11), cyan);
            Text(parent, "走る！  撃つ！  街がくずれる！", new Vector2(772, -96), new Vector2(552, 43), 25, lime).alignment = TextAnchor.MiddleCenter;
        }

        private void Instruction(Transform parent, Vector2 point, string number, string heading, string detail, Color color)
        {
            var panel = Panel(parent, "Title instruction " + number, point, new Vector2(420, 148), new Color(.08f, .18f, .24f));
            style.Card(panel, color);
            Panel(panel.transform, "Accent", Vector2.zero, new Vector2(5, 148), color);
            Text(panel.transform, number, new Vector2(22, -13), new Vector2(66, 44), 30, color);
            Text(panel.transform, heading, new Vector2(22, -60), new Vector2(381, 37), 25, Color.white);
            Text(panel.transform, detail, new Vector2(22, -105), new Vector2(381, 30), 19, new Color(.68f, .83f, .87f));
        }

        private GameObject Panel(Transform parent, string name, Vector2 point, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image)); item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = point; rect.sizeDelta = size;
            item.GetComponent<Image>().color = color; item.GetComponent<Image>().raycastTarget = name == "Title play button";
            item.GetComponent<Image>().sprite = style.Rounded; item.GetComponent<Image>().type = Image.Type.Sliced;
            return item;
        }

        private Text Text(Transform parent, string value, Vector2 point, Vector2 size, int fontSize, Color color)
        {
            var item = new GameObject("Title label", typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent, false);
            var label = item.GetComponent<Text>(); label.font = font; label.text = value; label.fontSize = fontSize;
            label.color = color; label.fontStyle = FontStyle.Bold; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0, 1);
            label.rectTransform.anchoredPosition = point; label.rectTransform.sizeDelta = size;
            return label;
        }
    }
}
