using UnityEngine;
using UnityEngine.UI;

namespace TankTown
{
    internal sealed class TankTownHowToScreen
    {
        internal GameObject Root { get; }
        internal Button Play { get; }
        private readonly TankTownGame game;
        private readonly TankTownMenuLayout menu;
        private readonly ToyHudStyle style;
        private static Color Ink => TankTownMenuLayout.Ink;
        private static Color Lime => TankTownMenuLayout.Lime;
        private static Color Cyan => TankTownMenuLayout.Cyan;

        internal TankTownHowToScreen(TankTownGame owner, TankTownMenuLayout layout, ToyHudStyle hudStyle)
        {
            game = owner; menu = layout; style = hudStyle;
            Root = menu.Panel(menu.Layout, "How to screen", Vector2.zero, new Vector2(1440, 900), Color.clear);
            Root.GetComponent<Image>().raycastTarget = true;
            Artwork(Root.transform, "Tutorial rays", Vector2.zero, new Vector2(1440,900), ResultArtKind.Party);
            var panel = menu.Panel(Root.transform, "How to card", new Vector2(64, -64), new Vector2(1312, 772), Color.clear);
            Artwork(panel.transform, "Tutorial colorful board", Vector2.zero, new Vector2(1312,772), ResultArtKind.Guide);
            menu.Label(panel.transform, "Tutorial heading", "あそびかた", new Vector2(58, -32), new Vector2(710, 72), 46, Color.white);
            menu.Label(panel.transform, "Tutorial subtitle", "1分20秒で、街をこわそう！", new Vector2(62, -105), new Vector2(700, 32), 21, Color.white);
            var turn = Step(panel.transform, 54, "01", "まがる", "自動で前進\nハンドルを左右に回そう", Cyan);
            var wheel = Shape(turn, "Tutorial steering wheel", new Vector2(138,-108), new Vector2(112,112), Cyan).GetComponent<Image>(); wheel.sprite=style.Ring;
            Shape(turn, "Wheel spoke", new Vector2(188,-120), new Vector2(12,86), Cyan);
            Shape(turn, "Wheel crossbar", new Vector2(151,-156), new Vector2(86,12), Cyan);
            menu.Label(turn, "Tutorial steering arrows", "←           →", new Vector2(24, -144), new Vector2(340, 50), 32, Ink).alignment = TextAnchor.MiddleCenter;
            var shoot = Step(panel.transform, 462, "02", "うつ", "ボタンで砲撃\n連打してOK！", TankTownResultArt.Pink);
            menu.Keycap(shoot, "Tutorial SPACE", "● ボタン", new Vector2(44, -122), new Vector2(216, 70), 29, Ink);
            Shape(shoot, "Tutorial cannonball", new Vector2(284, -143), new Vector2(30, 30), new Color(.28f, .36f, .46f)).GetComponent<Image>().sprite = style.Rounded;
            Shape(shoot, "Tutorial hit block 1", new Vector2(337, -112), new Vector2(24, 24), new Color(.87f, .9f, .93f), 16);
            Shape(shoot, "Tutorial hit block 2", new Vector2(326, -176), new Vector2(26, 26), new Color(1, .73f, .1f), -12);
            Shape(shoot, "Tutorial muzzle flash", new Vector2(265, -130), new Vector2(17, 5), new Color(1, .78f, .1f), 30);
            var collect = Step(panel.transform, 870, "03", "こわして、ひろう", "ビル全壊 → 光る円へ\nスペシャルをGET！", new Color(1, .76f, .15f));
            Shape(collect, "Tutorial building roof", new Vector2(51, -105), new Vector2(88, 7), new Color(.8f, .87f, .92f));
            for (int y = 0; y < 3; y++) for (int x = 0; x < 2; x++)
            {
                Shape(collect, "Tutorial building block", new Vector2(57 + x * 39, -113 - y * 29), new Vector2(34, 24), new Color(1, .75f, .1f));
                Shape(collect, "Tutorial building window", new Vector2(64 + x * 39, -118 - y * 29), new Vector2(20, 12), new Color(.05f, .35f, .6f));
            }
            Shape(collect, "Tutorial loose block", new Vector2(134, -137), new Vector2(24, 24), Color.white, 24);
            menu.Label(collect, "Tutorial reward arrow", "→", new Vector2(164, -133), new Vector2(55, 58), 42, Cyan).alignment = TextAnchor.MiddleCenter;
            var ring = Shape(collect, "Tutorial reward ring", new Vector2(229, -181), new Vector2(116, 34), Cyan).GetComponent<Image>();
            ring.sprite = style.Ring; ring.type = Image.Type.Simple;
            var reward = Shape(collect, "Tutorial reward icon", new Vector2(252, -106), new Vector2(72, 72), Color.white).GetComponent<Image>();
            reward.sprite = game.World.SpecialIcons.Sprite(ItemKind.MegaBeam); reward.type = Image.Type.Simple;

            var manual = Group(panel.transform, "Manual specials", new Vector2(54, -480), new Vector2(432, 164), "砲撃で使う", Cyan);
            Item(manual, ItemKind.MegaBeam, 124, "レーザー"); Item(manual, ItemKind.GigantBomb, 308, "爆弾");
            var automatic = Group(panel.transform, "Automatic specials", new Vector2(506, -480), new Vector2(752, 164), "拾うと発動", Lime);
            Item(automatic, ItemKind.Missiles, 99, "ミサイル"); Item(automatic, ItemKind.Magnet, 283, "マグネット");
            Item(automatic, ItemKind.Bulldozer, 467, "ブルドーザー"); Item(automatic, ItemKind.HighSpeed, 651, "ハイスピード");
            Play = TankTownCardCarousel.CreateAction(menu, panel.transform, "How to close button", "もどる", new Vector2(54, -666), new Vector2(1204, 78), TankTownResultArt.Pink, game.ToggleHowTo);
        }

        private void Artwork(Transform parent, string name, Vector2 point, Vector2 size, ResultArtKind kind)
        {
            var item=new GameObject(name,typeof(RectTransform),typeof(TankTownResultArt)); item.transform.SetParent(parent,false);
            var rect=(RectTransform)item.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=point;rect.sizeDelta=size;
            item.GetComponent<TankTownResultArt>().Initialize(kind,TankTownResultArt.Pink);
        }

        private Transform Step(Transform parent, float x, string number, string heading, string detail, Color accent)
        {
            var panel = menu.Panel(parent, "Tutorial step " + number, new Vector2(x, -160), new Vector2(388, 300), Color.Lerp(accent,Color.white,.87f));
            menu.Label(panel.transform, "Tutorial number " + number, number, new Vector2(24, -15), new Vector2(64, 38), 25, TankTownResultArt.Purple);
            menu.Label(panel.transform, "Tutorial heading " + number, heading, new Vector2(24, -58), new Vector2(340, 44), 30, TankTownResultArt.Ink).alignment = TextAnchor.MiddleCenter;
            menu.Label(panel.transform, "Tutorial body " + number, detail, new Vector2(24, -220), new Vector2(340, 70), 21, TankTownResultArt.Ink).alignment = TextAnchor.MiddleCenter;
            return panel.transform;
        }

        private Transform Group(Transform parent, string name, Vector2 position, Vector2 size, string heading, Color color)
        {
            var panel = menu.Panel(parent, name, position, size, Color.Lerp(color,Color.white,.88f));
            menu.Label(panel.transform, name + " heading", heading, new Vector2(24, -12), new Vector2(size.x - 48, 32), 21, TankTownResultArt.Ink);
            return panel.transform;
        }

        private void Item(Transform parent, ItemKind kind, float center, string name)
        {
            var icon = Shape(parent, "Tutorial special " + kind, new Vector2(center - 32, -50), new Vector2(64, 64), Color.white).GetComponent<Image>();
            icon.sprite = game.World.SpecialIcons.Sprite(kind); icon.type = Image.Type.Simple;
            menu.Label(parent, "Tutorial special label " + kind, name, new Vector2(center - 76, -120), new Vector2(152, 30), 17, TankTownResultArt.Ink).alignment = TextAnchor.MiddleCenter;
        }

        private GameObject Shape(Transform parent, string name, Vector2 point, Vector2 size, Color color, float angle = 0)
        {
            var item = menu.Panel(parent, name, point, size, color); item.GetComponent<Image>().sprite = null;
            item.transform.localRotation = Quaternion.Euler(0, 0, angle); return item;
        }
    }
}
