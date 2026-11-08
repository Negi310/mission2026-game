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
        private readonly bool actionsOnly;
        internal int Selected => Mod(target);
        internal GameObject Root => menu.Root;
        internal static Color Ink => new Color(.09f, .13f, .25f);
        internal static Color Pink => new Color(1, .25f, .48f);
        internal TankTownCardCarousel(TankTownGame owner, Transform canvas, Font font, ToyHudStyle style, string name)
        {
            game = owner;
            actionsOnly = name.EndsWith("actions", StringComparison.Ordinal);
            menu = new TankTownMenuLayout(name, canvas, font, style, actionsOnly ? Color.clear : new Color(.05f, .18f, .35f, name == "Title screen" ? .14f : .7f));
            if (name == "Title screen" || name == "Pause screen")
            {
                var party = new GameObject("Menu colorful accents", typeof(RectTransform), typeof(TankTownResultArt));
                party.transform.SetParent(menu.Layout, false);
                var rect = (RectTransform)party.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(1440, 900);
                party.GetComponent<TankTownResultArt>().Initialize(ResultArtKind.TitleDecor, Pink);
                party.GetComponent<TankTownResultArt>().raycastTarget = false;
            }
            heading = menu.Label(menu.Layout, name + " heading", "", new Vector2(120, -55), new Vector2(1200, 125), 70, Color.white);
            heading.alignment = TextAnchor.MiddleCenter;
            heading.fontSize = name == "Pause screen" ? 72 : 92;
            var outline = heading.gameObject.AddComponent<Outline>(); outline.effectColor = Ink; outline.effectDistance = new Vector2(4,-4);
            var shadow = heading.gameObject.AddComponent<Shadow>(); shadow.effectColor = Pink; shadow.effectDistance = new Vector2(0,-9);
            subtitle = menu.Label(menu.Layout, name + " subtitle", "", new Vector2(160, -182), new Vector2(1120, 74), 28, Color.white);
            subtitle.alignment = TextAnchor.MiddleCenter;
            counter = menu.Label(menu.Layout, name + " page", "", new Vector2(590, -768), new Vector2(260, 40), 22, Color.white);
            counter.alignment = TextAnchor.MiddleCenter;
            menu.Label(menu.Layout, name + " controls", "ハンドルで えらぶ     ● ボタンで きめる", new Vector2(320, actionsOnly ? -760 : -826), new Vector2(800, 42), 25, actionsOnly ? Ink : Color.white).alignment = TextAnchor.MiddleCenter;
            heading.gameObject.SetActive(!actionsOnly); subtitle.gameObject.SetActive(!actionsOnly);
            counter.gameObject.SetActive(false);
            menu.Button(menu.Layout, name + " previous", "‹", new Vector2(52, -640), new Vector2(70, 90), Pink, () => Move(-1));
            menu.Button(menu.Layout, name + " next", "›", new Vector2(1318, -640), new Vector2(70, 90), Pink, () => Move(1));
        }
        private int Mod(int value) => choices.Count == 0 ? 0 : (value % choices.Count + choices.Count) % choices.Count;
        internal void SetHeadingFont(Font font) { heading.font = font; }
        internal void Set(string title, string detail, Choice[] items, int selected = 0)
        {
            foreach (var card in cards) UnityEngine.Object.Destroy(card.gameObject);
            choices.Clear(); cards.Clear(); groups.Clear(); choices.AddRange(items);
            target = selected + items.Length; position = target; armed = false; repeatAt = 0;
            heading.text = title; subtitle.text = detail;
            for (int i = 0; i < items.Length * 3; i++)
            {
                int index = i % items.Length; var item = items[index];
                var panel = menu.Panel(menu.Layout, "Carousel card " + i, Vector2.zero, new Vector2(460, 108), Color.clear);
                var rect = (RectTransform)panel.transform; rect.pivot = Vector2.one * .5f;
                var surface = new GameObject("Action surface", typeof(RectTransform), typeof(TankTownResultArt));
                surface.transform.SetParent(panel.transform, false);
                var surfaceRect = (RectTransform)surface.transform; surfaceRect.sizeDelta = new Vector2(460, 108);
                var graphic = surface.GetComponent<TankTownResultArt>(); graphic.Initialize(ResultArtKind.TitleAction, item.Color); graphic.raycastTarget = false;
                menu.Label(panel.transform, "Card title", item.Title, new Vector2(24, -6), new Vector2(412, 88), 42, Color.white).alignment = TextAnchor.MiddleCenter;
                panel.GetComponent<Image>().raycastTarget = true;
                var button = panel.AddComponent<Button>(); button.targetGraphic = graphic;
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
                var rect = cards[i]; rect.anchoredPosition = new Vector2(720 + delta * 415, -685 + Mathf.Min(2, distance) * 8);
                rect.localScale = Vector3.one * Mathf.Max(.65f, 1 - distance * .18f);
                rect.localRotation = Quaternion.identity;
                groups[i].alpha = Mathf.Clamp01((2 - distance) / .65f) * Mathf.Clamp01(1 - distance * .18f);
                groups[i].interactable = distance < 1.7f; groups[i].blocksRaycasts = distance < 1.7f;
            }
            if (cards.Count > 0) cards[(target % cards.Count + cards.Count) % cards.Count].SetAsLastSibling();
        }
    }
}
