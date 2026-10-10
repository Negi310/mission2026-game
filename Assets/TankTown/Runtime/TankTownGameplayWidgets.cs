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
            text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
    }

    internal sealed class TankTownUpgradeHud : IDisposable
    {
        private sealed class Chip
        {
            internal RectTransform Root;
            internal Text From, To, Value, Bonus;
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
            Root = ui.Root(parent, "Upgrades", Vector2.zero, Vector2.zero, new Vector2(28, 24), new Vector2(478, 108));
            for (int i = 0; i < chips.Length; i++)
            {
                var kind = (UpgradeKind)i; var color = TankStats.Color(kind);
                var chip = new Chip { Level = game.Stats.Level(kind), Target = game.Stats.Level(kind) }; chips[i] = chip;
                chip.Root = ui.Card(Root, "Upgrade chip " + i, new Vector2(i * 122, 0), new Vector2(112, 108), color);
                ui.Image(chip.Root, "Upgrade field icon", new Vector2(8, -9), new Vector2(44, 44), Color.white, game.World.Icons.Sprite(kind));
                ui.Text(chip.Root, "Upgrade kind", TankStats.Label(kind), new Vector2(56, -12), new Vector2(54, 22), 13, color);
                ui.Text(chip.Root, "Upgrade level prefix", "Lv.", new Vector2(56, -56), new Vector2(21, 16), 12, Color.white);
                var clip = ui.Root(chip.Root, "Level digit mask", new Vector2(0, 1), new Vector2(0, 1), new Vector2(74, -34), new Vector2(34, 38));
                clip.gameObject.AddComponent<RectMask2D>();
                chip.From = ui.Text(clip, "Level digit", chip.Level.ToString(), Vector2.zero, new Vector2(34, 38), 35, Color.white, true);
                chip.To = ui.Text(clip, "Incoming level digit", "", new Vector2(0, -38), new Vector2(34, 38), 35, Color.white, true);
                chip.From.alignment = chip.To.alignment = TextAnchor.MiddleCenter;
                chip.Pips = new Image[Mathf.Max(1, game.Settings.maxUpgradeLevel)];
                float pitch = 96f / chip.Pips.Length;
                for (int p = 0; p < chip.Pips.Length; p++)
                    chip.Pips[p] = ui.Image(chip.Root, "Upgrade segment " + p, new Vector2(8 + p * pitch, -77), new Vector2(pitch - 2, 7), color);
                chip.Value = ui.Text(chip.Root, "Upgrade actual value", "", new Vector2(4, -89), new Vector2(104, 18), 11, Color.white);
                chip.Value.alignment = TextAnchor.MiddleCenter;
                chip.Bonus = ui.Text(chip.Root, "Upgrade gain", "", new Vector2(66, 24), new Vector2(46, 24), 19, color, true);
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
                chip.From.rectTransform.anchoredPosition = new Vector2(0, t * 38);
                chip.To.rectTransform.anchoredPosition = new Vector2(0, (t - 1) * 38);
                chip.Root.localScale = Vector3.one * (1 + .065f * Mathf.Sin(Mathf.Clamp01(chip.Age / .22f) * Mathf.PI));
                chip.Bonus.text = chip.Age < 1 ? "+" + chip.Gains : "";
                var color = TankStats.Color((UpgradeKind)i); color.a = Mathf.Clamp01((1 - chip.Age) * 4); chip.Bonus.color = color;
                for (int p = 0; p < chip.Pips.Length; p++) chip.Pips[p].color = p < chip.Level ? TankStats.Color((UpgradeKind)i) : new Color(.19f, .29f, .34f);
                var kind = (UpgradeKind)i;
                chip.Value.text = kind == UpgradeKind.Speed ? $"速度 ×{game.Stats.Speed / game.Settings.driveSpeed:0.00}"
                    : kind == UpgradeKind.Power ? $"威力 ×{game.Stats.BlastImpulse / game.Settings.blastImpulse:0.00}"
                    : kind == UpgradeKind.Rapid ? $"間隔 {game.Stats.ShotCooldown:0.00}秒"
                    : $"旋回 ×{game.Stats.TurnSpeed / game.Settings.turnSpeed:0.00}";
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
        private readonly Text empty, name, hint, automaticHeading;
        private float heldAge = 10;
        internal RectTransform HeldRoot { get; }
        internal RectTransform AutomaticRoot { get; }
        internal RectTransform WeaponRoot { get; }
        internal TankTownSpecialHud(TankTownGame owner, Transform parent, TankTownHudWidgets ui, ToyHudStyle style)
        {
            game = owner;
            HeldRoot = ui.Card(parent, "Special items", Vector2.zero, new Vector2(272, 92), TankTownMenuLayout.Cyan);
            HeldRoot.anchorMin = HeldRoot.anchorMax = HeldRoot.pivot = new Vector2(.5f, 0); HeldRoot.anchoredPosition = new Vector2(8, 24);
            heldIcon = ui.Image(HeldRoot, "Held item icon", new Vector2(12, -17), new Vector2(60, 60), Color.white);
            empty = ui.Text(HeldRoot, "Empty special slot", "？", new Vector2(12, -17), new Vector2(60, 60), 40, new Color(.38f, .71f, .82f));
            empty.alignment = TextAnchor.MiddleCenter;
            ui.Text(HeldRoot, "Held special heading", "所持スペシャル", new Vector2(87, -10), new Vector2(174, 16), 10, TankTownMenuLayout.Cyan);
            name = ui.Text(HeldRoot, "Held special name", "", new Vector2(87, -31), new Vector2(174, 27), 18, Color.white);
            usePlate = ui.Image(HeldRoot, "Special use prompt", new Vector2(87, -61), new Vector2(173, 23), TankTownMenuLayout.Lime);
            hint = ui.Text(usePlate.transform, "Held special hint", "", new Vector2(3, 0), new Vector2(167, 23), 13, TankTownHudWidgets.Ink);
            hint.alignment = TextAnchor.MiddleCenter;
            var root = ui.Root(parent, "Active item timers", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            AutomaticRoot = ui.Root(root, "Automatic special effects", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 24), new Vector2(336, 112));
            automaticHeading = ui.Text(AutomaticRoot, "Automatic effects heading", "発動中", new Vector2(1, 44), new Vector2(200, 20), 11, Color.white);
            WeaponRoot = ui.Root(root, "Weapon effect", new Vector2(.5f, 0), Vector2.zero, new Vector2(164, 24), new Vector2(78, 112));
            for (int i = 0; i < effects.Length; i++)
            {
                var kind = (ItemKind)i; var color = ItemIcons.Color(kind);
                var effect = new Effect(); effects[i] = effect;
                effect.Root = ui.Card(ItemIcons.Automatic(kind) ? AutomaticRoot : WeaponRoot, "Effect " + kind, Vector2.zero, new Vector2(78, 112), color);
                var back = ui.Image(effect.Root, "Effect ring track", new Vector2(11, -4), new Vector2(56, 56), new Color(color.r, color.g, color.b, .25f), style.Ring);
                effect.Ring = ui.Image(effect.Root, "Effect remaining ring", new Vector2(11, -4), new Vector2(56, 56), color, style.Ring);
                effect.Ring.type = UnityEngine.UI.Image.Type.Filled; effect.Ring.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                effect.Ring.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top; effect.Ring.fillClockwise = true;
                effect.Icon = ui.Image(effect.Root, "Active special field icon", new Vector2(14, -7), new Vector2(50, 50), Color.white, game.World.SpecialIcons.Sprite(kind));
                effect.Time = ui.Text(effect.Root, "Effect time", "", new Vector2(3, -64), new Vector2(72, 26), 22, Color.white, kind != ItemKind.GigantBomb);
                effect.Time.alignment = TextAnchor.MiddleCenter;
                var label = ui.Text(effect.Root, "Effect name", ItemIcons.Label(kind), new Vector2(2, -94), new Vector2(74, 16), 11, Color.white);
                label.alignment = TextAnchor.MiddleCenter;
                effect.Bonus = ui.Text(effect.Root, "Effect extension", "", new Vector2(-8, 23), new Vector2(94, 22), 14, color);
                effect.Bonus.alignment = TextAnchor.MiddleCenter;
                if (kind == ItemKind.GigantBomb) { back.gameObject.SetActive(false); effect.Ring.gameObject.SetActive(false); effect.Time.fontSize = 15; }
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
            HeldRoot.localScale = Vector3.one * (1 + .045f * Mathf.Sin(Mathf.Clamp01(heldAge / .22f) * Mathf.PI));
            var held = game.Items.Held; bool hasHeld = held != ItemKind.None;
            heldIcon.gameObject.SetActive(hasHeld); empty.gameObject.SetActive(!hasHeld);
            if (hasHeld) heldIcon.sprite = game.World.SpecialIcons.Sprite(held);
            name.text = hasHeld ? ItemIcons.Label(held) : "ビルを全壊でゲット";
            name.fontSize = hasHeld ? 18 : 15;
            bool available = hasHeld && game.Items.Weapon == ItemKind.None && !game.Tank.Respawning;
            hint.text = !hasHeld ? "光る円に入ろう" : available ? "SPACE でつかう ▶" : "効果終了まで待機";
            usePlate.color = available ? TankTownMenuLayout.Lime : new Color(.14f, .25f, .30f);
            hint.color = available ? TankTownHudWidgets.Ink : new Color(.70f, .83f, .90f);
            int count = 0;
            for (int i = 0; i < effects.Length; i++) if (ItemIcons.Automatic((ItemKind)i) && game.Items.Remaining((ItemKind)i) > 0) count++;
            AutomaticRoot.sizeDelta = new Vector2(Mathf.Max(1, count * 86 - 8), 112);
            automaticHeading.gameObject.SetActive(count > 0);
            int column = 0;
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i]; var kind = (ItemKind)i; float remaining = game.Items.Remaining(kind);
                bool active = remaining > 0; effect.Root.gameObject.SetActive(active); effect.Age += dt;
                if (!active) continue;
                if (ItemIcons.Automatic(kind)) effect.Root.anchoredPosition = new Vector2(column++ * 86, 0);
                effect.Ring.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(.01f, game.Items.Duration(kind)));
                effect.Time.text = kind == ItemKind.GigantBomb ? "飛行中" : $"{remaining:0.0}s";
                effect.Bonus.text = effect.Age < 1 && effect.Added >= .05f ? $"+{effect.Added:0.0}秒" : "";
                var color = ItemIcons.Color(kind); color.a = Mathf.Clamp01((1 - effect.Age) * 4); effect.Bonus.color = color;
                float pop = .055f * Mathf.Sin(Mathf.Clamp01(effect.Age / .22f) * Mathf.PI);
                float pulse = kind != ItemKind.GigantBomb && remaining < game.Items.Duration(kind) * .25f ? .025f * Mathf.Sin(Time.time * Mathf.PI * 2) : 0;
                effect.Icon.rectTransform.localScale = Vector3.one * (1 + pop + pulse);
            }
        }
        public void Dispose() { game.Items.Received -= Received; }
    }
}
