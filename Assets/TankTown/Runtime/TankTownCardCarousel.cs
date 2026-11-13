using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
        private readonly List<Image> dots = new List<Image>();
        internal const float EnterThreshold = .72f;
        internal const float ReleaseThreshold = .32f;
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
            var controls = menu.Label(menu.Layout, name + " controls", "左右に回して えらぶ     ● ボタンで きめる", new Vector2(320, actionsOnly ? -770 : -800), new Vector2(800, 36), 22, actionsOnly ? Ink : Color.white);
            controls.alignment = TextAnchor.MiddleCenter;
            if (!actionsOnly)
            {
                var edge = controls.gameObject.AddComponent<Outline>(); edge.effectColor = new Color(Ink.r,Ink.g,Ink.b,.8f); edge.effectDistance = new Vector2(1,-1);
            }
            heading.gameObject.SetActive(!actionsOnly); subtitle.gameObject.SetActive(!actionsOnly);
            counter.gameObject.SetActive(false);
            var previous = menu.Button(menu.Layout, name + " previous", "‹", new Vector2(58, actionsOnly ? -626 : -641), new Vector2(58, 62), Ink, () => {Move(-1); ClearPointerFocus();});
            var next = menu.Button(menu.Layout, name + " next", "›", new Vector2(1324, actionsOnly ? -626 : -641), new Vector2(58, 62), Ink, () => {Move(1); ClearPointerFocus();});
            previous.navigation = next.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        private int Mod(int value) => choices.Count == 0 ? 0 : (value % choices.Count + choices.Count) % choices.Count;
        private static void ClearPointerFocus() { if(EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        internal void SetHeadingFont(Font font) { heading.font = font; }
        internal void Set(string title, string detail, Choice[] items, int selected = 0)
        {
            foreach (var card in cards) UnityEngine.Object.Destroy(card.gameObject);
            foreach (var dot in dots) UnityEngine.Object.Destroy(dot.gameObject);
            dots.Clear();
            choices.Clear(); cards.Clear(); groups.Clear(); choices.AddRange(items);
            target = selected + items.Length; position = target; armed = false; repeatAt = 0;
            heading.text = title; subtitle.text = detail;
            for (int i = 0; i < items.Length * 3; i++)
            {
                int index = i % items.Length; var item = items[index];
                var button = CreateAction(menu, menu.Layout, "Carousel card " + i, item.Title, Vector2.zero, new Vector2(460,108), item.Color, null);
                var panel = button.gameObject;
                var rect = (RectTransform)panel.transform; rect.pivot = Vector2.one * .5f;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => { if (Selected == index) Confirm(); else { int delta = index - Selected; if (delta > items.Length / 2) delta -= items.Length; if (delta < -items.Length / 2) delta += items.Length; target += delta; } });
                cards.Add(rect); groups.Add(panel.AddComponent<CanvasGroup>());
            }
            for (int i = 0; i < items.Length; i++)
                dots.Add(menu.Panel(menu.Layout, "Menu position " + i, new Vector2(720 + (i - (items.Length-1)*.5f)*24 - 5, actionsOnly ? -742 : -762), new Vector2(10,10), Color.white).GetComponent<Image>());
            Tick(0);
        }
        internal static Button CreateAction(TankTownMenuLayout layout, Transform parent, string name, string label, Vector2 point, Vector2 size, Color tint, Action action)
        {
            var panel = layout.Panel(parent, name, point, size, Color.clear);
            panel.GetComponent<Image>().raycastTarget = true;
            var surface = new GameObject("Action surface", typeof(RectTransform), typeof(TankTownResultArt));
            surface.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)surface.transform; rect.sizeDelta = size;
            var graphic = surface.GetComponent<TankTownResultArt>(); graphic.Initialize(ResultArtKind.MenuAction, tint);
            layout.Label(panel.transform, "Action label", label, new Vector2(24, -2), new Vector2(size.x-48,size.y-12), size.y > 90 ? 40 : 29, Color.white).alignment = TextAnchor.MiddleCenter;
            var button = panel.AddComponent<Button>(); button.targetGraphic = graphic;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(ClearPointerFocus);
            if(action != null) button.onClick.AddListener(() => action());
            return button;
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
            if (Mathf.Abs(steering) < ReleaseThreshold) { armed = true; repeatAt = 0; }
            if (armed && Mathf.Abs(steering) > EnterThreshold && Time.unscaledTime >= repeatAt)
            { Move(steering < 0 ? -1 : 1); repeatAt = Time.unscaledTime + (repeatAt == 0 ? .65f : .4f); }
            position = Mathf.Lerp(position, target, 1 - Mathf.Exp(-13 * dt));
            counter.text = (Selected + 1) + "  /  " + choices.Count;
            for (int i = 0; i < cards.Count; i++)
            {
                float delta = Mathf.Repeat(i - position + cards.Count * .5f, cards.Count) - cards.Count * .5f;
                float distance = Mathf.Abs(delta);
                var rect = cards[i]; rect.anchoredPosition = new Vector2(720 + delta * 415, actionsOnly ? -670 : -685);
                rect.localScale = Vector3.one * Mathf.Max(.65f, 1 - distance * .2f);
                rect.localRotation = Quaternion.identity;
                groups[i].alpha = Mathf.Clamp01((2 - distance) / .65f) * Mathf.Clamp01(1 - distance * .34f);
                groups[i].interactable = distance < 1.7f; groups[i].blocksRaycasts = distance < 1.7f;
            }
            for(int i=0;i<dots.Count;i++) {dots[i].color = i==Selected ? choices[i].Color : actionsOnly ? new Color(.2f,.15f,.35f,.2f) : new Color(1,1,1,.4f); dots[i].rectTransform.localScale = Vector3.one*(i==Selected?1.35f:1);}
            if (cards.Count > 0) cards[(target % cards.Count + cards.Count) % cards.Count].SetAsLastSibling();
        }
    }
}
