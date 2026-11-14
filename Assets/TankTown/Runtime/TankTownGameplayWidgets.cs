using System;
using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    // Small fixed HUD widgets share the same icons as their field pickups.
    internal sealed class TankTownHudWidgets
    {
        private readonly Font font, numbers;
        private readonly ToyHudStyle style;
        internal static readonly Color Ink = new Color(.045f, .12f, .18f, .95f);
        internal static float BottomScale(Transform parent) => .94f * Mathf.Min(1, ((RectTransform)parent).rect.width / 1440);
        internal TankTownHudWidgets(Font font, Font numbers, ToyHudStyle style)
        { this.font = font; this.numbers = numbers; this.style = style; }
        internal RectTransform Root(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 point, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform; rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot; rect.anchoredPosition = point; rect.sizeDelta = size; item.layer = 5;
            return rect;
        }
        internal Image Image(Transform parent, string name, Vector2 point, Vector2 size, Color color, Sprite sprite = null)
        {
            var rect = Root(parent, name, new Vector2(0, 1), new Vector2(0, 1), point, size);
            var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.color = color;
            image.sprite = sprite ?? style.Rounded; image.type = sprite == null ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            return image;
        }
        internal RectTransform Card(Transform parent, string name, Vector2 point, Vector2 size, Color border)
        {
            var image = Image(parent, name, point, size, Ink); style.Card(image.gameObject, border); return image.rectTransform;
        }
        internal Text Text(Transform parent, string name, string value, Vector2 point, Vector2 size, int sizeInPoints, Color color, bool numeric = false)
        {
            var rect = Root(parent, name, new Vector2(0, 1), new Vector2(0, 1), point, size);
            var text = rect.gameObject.AddComponent<Text>(); text.font = numeric ? numbers : font; text.text = value;
            text.fontSize = sizeInPoints; text.fontStyle = FontStyle.Bold; text.color = color; text.raycastTarget = false;
            text.verticalOverflow = VerticalWrapMode.Overflow; return text;
        }
    }

    internal sealed class TankTownUpgradeHud : IDisposable
    {
        private sealed class Chip
        {
            internal RectTransform Root;
            internal Text From, To, Bonus;
            internal Image[] Pips;
            internal int Level, Target, Gains;
            internal float Step, Age = 10;
        }
        private const float DigitSeconds = .16f;
        private readonly TankTownGame game;
        private readonly Chip[] chips = new Chip[4];
        internal RectTransform Root { get; }
        internal int DisplayedLevel(UpgradeKind kind) => chips[(int)kind].Level;
        internal bool Counting(UpgradeKind kind) => chips[(int)kind].Level < chips[(int)kind].Target;
        internal TankTownUpgradeHud(TankTownGame owner, Transform parent, TankTownHudWidgets ui)
        {
            game = owner;
            Root = ui.Root(parent, "Upgrades", Vector2.zero, Vector2.zero, new Vector2(28, 24), new Vector2(558, 92));
            for (int i = 0; i < chips.Length; i++)
            {
                var kind = (UpgradeKind)i; var color = TankStats.Color(kind);
                var chip = new Chip { Level = game.Stats.Level(kind), Target = game.Stats.Level(kind) }; chips[i] = chip;
                chip.Root = ui.Card(Root, "Upgrade chip " + i, new Vector2(i * 142, 0), new Vector2(132, 92), color);
                ui.Image(chip.Root, "Upgrade field icon", new Vector2(8, -8), new Vector2(60, 60), Color.white, game.World.Icons.Sprite(kind));
                var clip = ui.Root(chip.Root, "Level digit mask", new Vector2(0, 1), new Vector2(0, 1), new Vector2(79, -13), new Vector2(44, 50));
                clip.gameObject.AddComponent<RectMask2D>();
                chip.From = ui.Text(clip, "Level digit", chip.Level.ToString(), Vector2.zero, new Vector2(44, 50), 44, Color.white, true);
                chip.To = ui.Text(clip, "Incoming level digit", "", new Vector2(0, -50), new Vector2(44, 50), 44, Color.white, true);
                chip.From.alignment = chip.To.alignment = TextAnchor.MiddleCenter;
                chip.Pips = new Image[Mathf.Max(1, game.Settings.maxUpgradeLevel)];
                float pitch = 116f / chip.Pips.Length;
                for (int p = 0; p < chip.Pips.Length; p++)
                    chip.Pips[p] = ui.Image(chip.Root, "Upgrade segment " + p, new Vector2(8 + p * pitch, -78), new Vector2(pitch - 2, 6), color);
                chip.Bonus = ui.Text(chip.Root, "Upgrade gain", "", new Vector2(86, 25), new Vector2(46, 25), 21, color, true);
            }
            game.Stats.Changed += Received; Tick(0);
        }
        private void Received(UpgradeKind kind)
        {
            var chip = chips[(int)kind]; chip.Target = game.Stats.Level(kind);
            chip.Gains = chip.Age < .55f ? chip.Gains + 1 : 1; chip.Age = 0;
        }
        internal void Tick(float dt)
        {
            Root.localScale = Vector3.one * TankTownHudWidgets.BottomScale(Root.parent);
            for (int i = 0; i < chips.Length; i++)
            {
                var chip = chips[i]; chip.Age += dt;
                if (chip.Level < chip.Target)
                {
                    chip.Step += dt;
                    while (chip.Step >= DigitSeconds && chip.Level < chip.Target) { chip.Step -= DigitSeconds; chip.Level++; }
                }
                if (chip.Level >= chip.Target) chip.Step = 0;
                float t = chip.Level < chip.Target ? chip.Step / DigitSeconds : 0;
                chip.From.text = chip.Level.ToString(); chip.To.text = chip.Level < chip.Target ? (chip.Level + 1).ToString() : "";
                chip.From.rectTransform.anchoredPosition = new Vector2(0, t * 50);
                chip.To.rectTransform.anchoredPosition = new Vector2(0, (t - 1) * 50);
                chip.Root.localScale = Vector3.one * (1 + .065f * Mathf.Sin(Mathf.Clamp01(chip.Age / .22f) * Mathf.PI));
                chip.Bonus.text = chip.Age < 1 ? "+" + chip.Gains : "";
                var color = TankStats.Color((UpgradeKind)i); color.a = Mathf.Clamp01((1 - chip.Age) * 4); chip.Bonus.color = color;
                for (int p = 0; p < chip.Pips.Length; p++) chip.Pips[p].color = p < chip.Level ? TankStats.Color((UpgradeKind)i) : new Color(.19f, .29f, .34f);
            }
        }
        public void Dispose() { game.Stats.Changed -= Received; }
    }

    internal sealed class TankTownSpecialHud : IDisposable
    {
        private sealed class Effect
        {
            internal RectTransform Root;
            internal Image Icon, Ring;
            internal Text Time, Bonus;
            internal float Age = 10, Added;
        }
        private readonly TankTownGame game;
        private readonly Effect[] effects = new Effect[6];
        private readonly Image heldIcon, usePlate;
        private readonly Text hint;
        private float heldAge = 10;
        internal RectTransform HeldRoot { get; }
        internal RectTransform AutomaticRoot { get; }
        internal RectTransform WeaponRoot { get; }
        internal TankTownSpecialHud(TankTownGame owner, Transform parent, TankTownHudWidgets ui, ToyHudStyle style)
        {
            game = owner;
            HeldRoot = ui.Card(parent, "Special items", Vector2.zero, new Vector2(104, 108), TankTownMenuLayout.Cyan);
            HeldRoot.anchorMin = HeldRoot.anchorMax = HeldRoot.pivot = new Vector2(.5f, 0); HeldRoot.anchoredPosition = new Vector2(8, 24);
            heldIcon = ui.Image(HeldRoot, "Held item icon", new Vector2(16, -6), new Vector2(72, 72), Color.white);
            usePlate = ui.Image(HeldRoot, "Special use prompt", new Vector2(14, -82), new Vector2(76, 20), TankTownMenuLayout.Lime);
            hint = ui.Text(usePlate.transform, "Held special hint", "SPACE", Vector2.zero, new Vector2(76, 20), 13, TankTownHudWidgets.Ink);
            hint.alignment = TextAnchor.MiddleCenter;
            var root = ui.Root(parent, "Active item timers", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            AutomaticRoot = ui.Root(root, "Automatic special effects", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 24), new Vector2(392, 108));
            WeaponRoot = ui.Root(root, "Weapon effect", new Vector2(.5f, 0), Vector2.zero, new Vector2(84, 24), new Vector2(92, 108));
            for (int i = 0; i < effects.Length; i++)
            {
                var kind = (ItemKind)i; var color = ItemIcons.Color(kind);
                var effect = new Effect(); effects[i] = effect;
                effect.Root = ui.Card(ItemIcons.Automatic(kind) ? AutomaticRoot : WeaponRoot, "Effect " + kind, Vector2.zero, new Vector2(92, 108), color);
                var back = ui.Image(effect.Root, "Effect ring track", new Vector2(12, -4), new Vector2(68, 68), new Color(color.r, color.g, color.b, .25f), style.Ring);
                effect.Ring = ui.Image(effect.Root, "Effect remaining ring", new Vector2(12, -4), new Vector2(68, 68), color, style.Ring);
                effect.Ring.type = UnityEngine.UI.Image.Type.Filled; effect.Ring.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                effect.Ring.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top; effect.Ring.fillClockwise = true;
                effect.Icon = ui.Image(effect.Root, "Active special field icon", new Vector2(16, -8), new Vector2(60, 60), Color.white, game.World.SpecialIcons.Sprite(kind));
                effect.Time = ui.Text(effect.Root, "Effect time", "", new Vector2(3, -76), new Vector2(86, 30), 26, Color.white, kind != ItemKind.GigantBomb);
                effect.Time.alignment = TextAnchor.MiddleCenter;
                effect.Bonus = ui.Text(effect.Root, "Effect extension", "", new Vector2(-6, 25), new Vector2(104, 25), 16, color);
                effect.Bonus.alignment = TextAnchor.MiddleCenter;
                if (kind == ItemKind.GigantBomb) { back.gameObject.SetActive(false); effect.Ring.gameObject.SetActive(false); }
            }
            game.Items.Received += Received; Tick(0);
        }
        private void Received(ItemKind kind, float previous)
        {
            if (!ItemIcons.Automatic(kind)) { heldAge = 0; return; }
            var effect = effects[(int)kind]; effect.Age = 0;
            effect.Added = previous > 0 ? Mathf.Max(0, game.Items.Remaining(kind) - previous) : 0;
        }
        internal void Tick(float dt)
        {
            heldAge += dt;
            float scale = TankTownHudWidgets.BottomScale(HeldRoot.parent);
            HeldRoot.localScale = Vector3.one * scale * (1 + .045f * Mathf.Sin(Mathf.Clamp01(heldAge / .22f) * Mathf.PI));
            AutomaticRoot.localScale = WeaponRoot.localScale = Vector3.one * scale;
            WeaponRoot.anchoredPosition = new Vector2(84 * scale, 24);
            var held = game.Items.Held; bool hasHeld = held != ItemKind.None;
            HeldRoot.gameObject.SetActive(hasHeld);
            if (hasHeld) heldIcon.sprite = game.World.SpecialIcons.Sprite(held);
            bool available = hasHeld && game.Items.Weapon == ItemKind.None && !game.Tank.Respawning;
            usePlate.color = available ? TankTownMenuLayout.Lime : new Color(.14f, .25f, .30f);
            hint.color = available ? TankTownHudWidgets.Ink : new Color(.70f, .83f, .90f);
            heldIcon.color = available ? Color.white : new Color(1, 1, 1, .55f);
            int count = 0;
            for (int i = 0; i < effects.Length; i++) if (ItemIcons.Automatic((ItemKind)i) && game.Items.Remaining((ItemKind)i) > 0) count++;
            AutomaticRoot.sizeDelta = new Vector2(Mathf.Max(1, count * 100 - 8), 108);
            int column = 0;
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i]; var kind = (ItemKind)i; float remaining = game.Items.Remaining(kind);
                bool active = remaining > 0; effect.Root.gameObject.SetActive(active); effect.Age += dt;
                if (!active) continue;
                if (ItemIcons.Automatic(kind)) effect.Root.anchoredPosition = new Vector2(column++ * 100, 0);
                effect.Ring.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(.01f, game.Items.Duration(kind)));
                effect.Time.text = kind == ItemKind.GigantBomb ? "↗" : $"{remaining:0.0}s";
                effect.Bonus.text = effect.Age < 1 && effect.Added >= .05f ? $"+{effect.Added:0.0}s" : "";
                var color = ItemIcons.Color(kind); color.a = Mathf.Clamp01((1 - effect.Age) * 4); effect.Bonus.color = color;
                float pop = .055f * Mathf.Sin(Mathf.Clamp01(effect.Age / .22f) * Mathf.PI);
                float pulse = kind != ItemKind.GigantBomb && remaining < game.Items.Duration(kind) * .25f ? .025f * Mathf.Sin(Time.time * Mathf.PI * 2) : 0;
                effect.Icon.rectTransform.localScale = Vector3.one * (1 + pop + pulse);
            }
        }
        public void Dispose() { game.Items.Received -= Received; }
    }
}
