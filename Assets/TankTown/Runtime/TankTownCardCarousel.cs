using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    // Every menu uses the same steering thresholds, motion and confirm behaviour.
    internal sealed class TankTownCardCarousel
    {
        internal sealed class Choice
        {
            internal string Title, Detail, Badge;
            internal Color Color;
            internal Action Select;
            internal Choice(string title, string detail, string badge, Color color, Action select)
            { Title = title; Detail = detail; Badge = badge; Color = color; Select = select; }
        }
        private readonly TankTownGame game;
        private readonly TankTownMenuLayout menu;
        private readonly List<Choice> choices = new List<Choice>();
        private readonly List<RectTransform> cards = new List<RectTransform>();
        private readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        private readonly Text heading, subtitle, counter;
        private int target, confirmedFrame = -1;
        private float position, repeatAt;
        private bool armed;
        private readonly TankTownResultArt celebration;
        private float shownAt;
        internal int Selected => Mod(target);
        internal GameObject Root => menu.Root;
        internal static Color Ink => new Color(.09f, .13f, .25f);
        internal static Color Pink => new Color(1, .25f, .48f);
        internal TankTownCardCarousel(TankTownGame owner, Transform canvas, Font font, ToyHudStyle style, string name)
        {
            game = owner;
            menu = new TankTownMenuLayout(name, canvas, font, style, new Color(.04f, .10f, .20f, .73f));
            if (name == "Results screen")
            {
                var confetti = new GameObject("Result celebration", typeof(RectTransform), typeof(TankTownResultArt));
                confetti.transform.SetParent(menu.Layout, false);
                var rect = (RectTransform)confetti.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(1440, 900);
                celebration = confetti.GetComponent<TankTownResultArt>(); celebration.Initialize(ResultArtKind.Confetti, Pink); celebration.raycastTarget = false;
            }
            heading = menu.Label(menu.Layout, name + " heading", "", new Vector2(120, -55), new Vector2(1200, 125), 70, Color.white);
            heading.alignment = TextAnchor.MiddleCenter;
            subtitle = menu.Label(menu.Layout, name + " subtitle", "", new Vector2(160, -182), new Vector2(1120, 74), 28, Color.white);
            subtitle.alignment = TextAnchor.MiddleCenter;
            counter = menu.Label(menu.Layout, name + " page", "", new Vector2(590, -768), new Vector2(260, 40), 22, Color.white);
            counter.alignment = TextAnchor.MiddleCenter;
            menu.Label(menu.Layout, name + " controls", "ハンドルで えらぶ     ● ボタンで きめる", new Vector2(320, -826), new Vector2(800, 42), 25, Color.white).alignment = TextAnchor.MiddleCenter;
            menu.Button(menu.Layout, name + " previous", "‹", new Vector2(52, -470), new Vector2(70, 90), Ink, () => Move(-1));
            menu.Button(menu.Layout, name + " next", "›", new Vector2(1318, -470), new Vector2(70, 90), Ink, () => Move(1));
        }
        private int Mod(int value) => choices.Count == 0 ? 0 : (value % choices.Count + choices.Count) % choices.Count;
        internal void Set(string title, string detail, Choice[] items, int selected = 0)
        {
            foreach (var card in cards) UnityEngine.Object.Destroy(card.gameObject);
            choices.Clear(); cards.Clear(); groups.Clear(); choices.AddRange(items);
            target = selected + items.Length; position = target; armed = false; repeatAt = 0;
            shownAt = Time.unscaledTime;
            heading.text = title; subtitle.text = detail;
            for (int i = 0; i < items.Length * 3; i++)
            {
                int index = i % items.Length; var item = items[index];
                var panel = menu.Panel(menu.Layout, "Carousel card " + i, Vector2.zero, new Vector2(460, 420), Color.white);
                var rect = (RectTransform)panel.transform; rect.pivot = Vector2.one * .5f;
                var shadow = panel.AddComponent<Shadow>(); shadow.effectColor = new Color(.01f, .03f, .08f, .55f); shadow.effectDistance = new Vector2(0, -18);
                menu.Panel(panel.transform, "Card top", new Vector2(0, 0), new Vector2(460, 16), item.Color);
                var face = menu.Panel(panel.transform, "Card inset", new Vector2(28, -35), new Vector2(404, 185), Color.Lerp(item.Color, Color.white, .84f));
                var drawing = new GameObject("Card illustration", typeof(RectTransform), typeof(TankTownCardArt));
                drawing.transform.SetParent(face.transform, false);
                var drawingRect = (RectTransform)drawing.transform; drawingRect.sizeDelta = new Vector2(280, 130); drawingRect.anchoredPosition = new Vector2(0, 12);
                var art = drawing.GetComponent<TankTownCardArt>(); art.Symbol = item.Badge; art.color = item.Color; art.raycastTarget = false;
                var badge = menu.Label(face.transform, "Card emblem", item.Badge, new Vector2(20, -144), new Vector2(364, 34), 23, item.Color);
                badge.alignment = TextAnchor.MiddleCenter;
                menu.Label(panel.transform, "Card title", item.Title, new Vector2(24, -243), new Vector2(412, 64), 37, Ink).alignment = TextAnchor.MiddleCenter;
                menu.Label(panel.transform, "Card detail", item.Detail, new Vector2(36, -306), new Vector2(388, 100), item.Detail.Split('\n').Length > 3 ? 17 : 22, Ink).alignment = TextAnchor.MiddleCenter;
                panel.GetComponent<Image>().raycastTarget = true;
                var button = panel.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => { if (Selected == index) Confirm(); else { int delta = index - Selected; if (delta > items.Length / 2) delta -= items.Length; if (delta < -items.Length / 2) delta += items.Length; target += delta; } });
                cards.Add(rect); groups.Add(panel.AddComponent<CanvasGroup>());
            }
            Tick(0);
        }
        internal void Caption(string title, string detail) { heading.text = title; subtitle.text = detail; }
        internal bool Confirm()
        {
            if (choices.Count == 0 || confirmedFrame == Time.frameCount) return true;
            confirmedFrame = Time.frameCount; choices[Selected].Select?.Invoke(); return true;
        }
        internal void Move(int direction) { target += direction; game.PlayTone(560 + Selected * 65, .045f, .055f); }
        internal void Tick(float dt)
        {
            menu.Fit();
            celebration?.SetMotion(0, Time.unscaledTime - shownAt);
            float steering = game.Input.Steering;
            if (game.Input.GetComponent<TankControlTuningPanel>()?.Visible == true || game.Input.GetComponent<MicrobitDiagnosticsPanel>()?.Visible == true) steering = 0;
            if (Mathf.Abs(steering) < .24f) { armed = true; repeatAt = 0; }
            if (armed && Mathf.Abs(steering) > .48f && Time.unscaledTime >= repeatAt)
            { Move(steering < 0 ? -1 : 1); repeatAt = Time.unscaledTime + (repeatAt == 0 ? .55f : .32f); }
            position = Mathf.Lerp(position, target, 1 - Mathf.Exp(-13 * dt));
            counter.text = (Selected + 1) + "  /  " + choices.Count;
            for (int i = 0; i < cards.Count; i++)
            {
                float delta = Mathf.Repeat(i - position + cards.Count * .5f, cards.Count) - cards.Count * .5f;
                float distance = Mathf.Abs(delta);
                var rect = cards[i]; rect.anchoredPosition = new Vector2(720 + delta * 390, -510 - Mathf.Min(2, distance) * 30);
                rect.localScale = Vector3.one * Mathf.Max(.65f, 1 - distance * .18f);
                rect.localRotation = Quaternion.Euler(0, -Mathf.Clamp(delta, -2, 2) * 24, -Mathf.Clamp(delta, -2, 2) * 3);
                groups[i].alpha = Mathf.Clamp01((2 - distance) / .65f) * Mathf.Clamp01(1 - distance * .18f);
                groups[i].interactable = distance < 1.7f; groups[i].blocksRaycasts = distance < 1.7f;
            }
            if (cards.Count > 0) cards[(target % cards.Count + cards.Count) % cards.Count].SetAsLastSibling();
        }
    }
}
