using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    internal sealed class TankTownMenuLayout
    {
        public readonly GameObject Root;
        public readonly RectTransform Layout;
        private readonly Font font;
        private readonly ToyHudStyle style;
        internal static readonly Color Ink = new Color(.035f, .10f, .17f);
        internal static readonly Color Lime = new Color(.81f, 1, .14f);
        internal static readonly Color Cyan = new Color(.08f, .83f, 1);

        public TankTownMenuLayout(string name, Transform canvas, Font uiFont, ToyHudStyle uiStyle, Color backdrop)
        {
            font = uiFont; style = uiStyle;
            Root = new GameObject(name, typeof(RectTransform), typeof(Image)); Root.transform.SetParent(canvas, false);
            var rect = (RectTransform)Root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Root.GetComponent<Image>().color = backdrop;
            Layout = Panel(Root.transform, name + " layout", Vector2.zero, new Vector2(1440, 900), Color.clear).GetComponent<RectTransform>();
            Layout.anchorMin = Layout.anchorMax = new Vector2(.5f, .5f);
            Fit();
        }

        public void Fit()
        {
            Vector2 bounds = ((RectTransform)Root.transform).rect.size;
            float scale = Mathf.Min(bounds.x / 1440, bounds.y / 900);
            if (scale <= 0) return;
            Layout.localScale = Vector3.one * scale;
            Layout.anchoredPosition = new Vector2(-720, 450) * scale;
        }

        public GameObject Panel(Transform parent, string name, Vector2 point, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = point; rect.sizeDelta = size;
            var graphic = item.GetComponent<Image>(); graphic.color = color; graphic.raycastTarget = false;
            graphic.sprite = style.Rounded; graphic.type = Image.Type.Sliced;
            return item;
        }

        public Text Label(Transform parent, string name, string value, Vector2 point, Vector2 size, int fontSize, Color color, Font customFont = null)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text)); item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>(); text.font = customFont != null ? customFont : font;
            text.text = value; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold; text.color = color; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = point; rect.sizeDelta = size;
            return text;
        }

        public Button Button(Transform parent, string name, string text, Vector2 point, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
        {
            var item = Panel(parent, name, point, size, color); item.GetComponent<Image>().raycastTarget = true;
            style.Card(item, new Color(1, 1, 1, .6f));
            var button = item.AddComponent<Button>(); button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = new Color(1, 1, .8f); colors.pressedColor = new Color(.75f, .85f, .6f); button.colors = colors;
            var label = Label(item.transform, name + " label", text, Vector2.zero, size, 26, color == Lime ? Ink : Color.white);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
