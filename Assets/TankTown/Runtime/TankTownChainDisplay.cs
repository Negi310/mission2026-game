using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    [DefaultExecutionOrder(150)]
    public sealed class TankTownChainDisplay : MonoBehaviour
    {
        public int DisplayedDepth { get; private set; }
        public int DisplayedExplosions { get; private set; }
        public int TargetDepth { get; private set; }
        public bool Visible => group != null && group.alpha > .01f;
        internal bool ActiveSequence => Time.time < until;
        private TankTownGame game;
        private CanvasGroup group;
        private RectTransform rect;
        private Text number, addition;
        private Image progress;
        private readonly Image[] sparks = new Image[10];
        private int startDepth, startExplosions, targetExplosions, delta;
        private float until, changedAt, countAt;
        private bool obscured;
        private const float Lifetime = 2;
        private const float Top = 150;
        private Color accent;

        internal void Initialize(TankTownGame owner, Font font, Font displayFont, ToyHudStyle style)
        {
            game = owner; rect = (RectTransform)transform; group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0; group.blocksRaycasts = group.interactable = false;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -Top); rect.sizeDelta = new Vector2(270, 78);
            number = Label("Chain number", displayFont, new Vector2(10, -4), new Vector2(90, 65), 55, TextAnchor.MiddleRight);
            number.rectTransform.pivot = Vector2.one * .5f; number.rectTransform.anchoredPosition = new Vector2(55, -36.5f);
            var word = Label("Chain word", displayFont, new Vector2(110, -12), new Vector2(149, 52), 27, TextAnchor.MiddleLeft); word.text = "CHAIN!";
            addition = Label("Chain addition", displayFont, new Vector2(219, 20), new Vector2(56, 25), 19, TextAnchor.MiddleCenter);
            progress = Box("Chain remaining", new Vector2(15, -73), new Vector2(240, 3), Color.white, style);
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = Box("Chain spark " + i, Vector2.zero, Vector2.one * (i % 2 == 0 ? 9 : 5), Color.clear, style);
                sparks[i].rectTransform.anchorMin = sparks[i].rectTransform.anchorMax = sparks[i].rectTransform.pivot = Vector2.one * .5f;
                sparks[i].rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }
            game.ChainBurst += Show;
        }
        private Image Box(string name, Vector2 position, Vector2 size, Color color, ToyHudStyle style)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image)); root.transform.SetParent(transform, false);
            var image = root.GetComponent<Image>(); image.sprite = style.Rounded; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false;
            Layout(image.rectTransform, position, size); return image;
        }
        private Text Label(string name, Font font, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text)); root.transform.SetParent(transform, false);
            var text = root.GetComponent<Text>(); text.font = font; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold;
            text.color = Color.white; text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate;
            var outline = root.AddComponent<Outline>(); outline.effectColor = new Color(.025f, .07f, .11f, .95f);
            outline.effectDistance = new Vector2(2, -2);
            var shadow = root.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .35f);
            shadow.effectDistance = new Vector2(1, -3);
            Layout(text.rectTransform, position, size); return text;
        }
        private static void Layout(RectTransform target, Vector2 position, Vector2 size)
        { target.anchorMin = target.anchorMax = target.pivot = new Vector2(0, 1); target.anchoredPosition = position; target.sizeDelta = size; }
        internal void SetObscured(bool value) => obscured = value;
        private void Show(int depth)
        {
            bool fresh = Time.time >= until;
            int oldDepth = fresh ? 0 : TargetDepth;
            startDepth = fresh ? 1 : Mathf.Max(1, DisplayedDepth);
            TargetDepth = fresh ? depth : Mathf.Max(TargetDepth, depth);
            int gain = Mathf.Max(0, TargetDepth - oldDepth);
            startExplosions = DisplayedExplosions;
            targetExplosions = game.ChainExplosions + game.DebrisExplosions;
            countAt = Time.time;
            // Simultaneous branches may arrive out of order; keep the greatest depth visible.
            if (fresh || gain > 0) { changedAt = Time.time; delta = gain; }
            until = Time.time + Lifetime;
            accent = TargetDepth >= 5 ? new Color(1, .38f, .83f) : TargetDepth >= 3 ? new Color(1, .65f, .08f) : new Color(1, .87f, .22f);
        }
        private void Update()
        {
            if (game == null) return;
            bool active = game.IsPlaying && !obscured && Time.time < until;
            float remaining = until - Time.time;
            group.alpha = active ? Mathf.Clamp01(remaining / .3f) : 0;
            if (!active) return;
            float age = Time.time - changedAt;
            float count = 1 - Mathf.Pow(1 - Mathf.Clamp01((Time.time - countAt) / .2f), 3);
            DisplayedDepth = Mathf.RoundToInt(Mathf.Lerp(startDepth, TargetDepth, count));
            DisplayedExplosions = Mathf.RoundToInt(Mathf.Lerp(startExplosions, targetExplosions, count));
            number.text = Mathf.Max(1, DisplayedDepth).ToString(); number.color = accent;
            number.fontSize = TargetDepth >= 100 ? 38 : TargetDepth >= 10 ? 46 : 55;
            float punch = Mathf.Sin(Mathf.Clamp01(age / .3f) * Mathf.PI) * Mathf.Exp(-age * 5);
            number.rectTransform.localScale = Vector3.one * (1 + punch * .2f);
            rect.localScale = Vector3.one * (1 + punch * .045f);
            rect.anchoredPosition = new Vector2(0, -Top - (1 - Mathf.Clamp01(age / .18f)) * 8);
            addition.text = delta > 0 && age < .55f ? "+" + delta : "";
            addition.color = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(1 - age / .55f));
            addition.rectTransform.anchoredPosition = new Vector2(219, 20 + age * 18);
            progress.color = accent; progress.rectTransform.sizeDelta = new Vector2(240 * Mathf.Clamp01(remaining / Lifetime), 3);
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = (i + .5f) * Mathf.PI * 2 / sparks.Length;
                sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * (120 + age * 38), Mathf.Sin(angle) * (30 + age * 30));
                sparks[i].color = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(1 - age / .45f));
            }
        }
        private void OnDisable() { if (group != null) group.alpha = 0; }
        private void OnDestroy() { if (game != null) game.ChainBurst -= Show; }
    }
}
