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
        private static Color Ink => TankTownMenuLayout.Ink;
        private static Color Lime => TankTownMenuLayout.Lime;
        private static Color Cyan => TankTownMenuLayout.Cyan;

        public TankTownTitleScreen(TankTownGame owner, Transform canvas, Font font, Font displayFont, ToyHudStyle style)
        {
            game = owner;
            menu = new TankTownMenuLayout("Title screen", canvas, font, style, new Color(.02f, .08f, .13f, .08f));
            var parent = menu.Layout;
            title = menu.Panel(parent, "Title content", Vector2.zero, new Vector2(1440, 900), Color.clear);
            var card = menu.Panel(title.transform, "Title glass panel", new Vector2(52, -74), new Vector2(570, 742), new Color(Ink.r, Ink.g, Ink.b, .92f));
            style.Card(card, Cyan);
            menu.Label(card.transform, "Title eyebrow", "1 MINUTE / DESTRUCTION ACTION", new Vector2(34, -28), new Vector2(506, 31), 18, Cyan);
            menu.Label(card.transform, "Title tank logo", "TANK", new Vector2(24, -66), new Vector2(520, 133), 104, new Color(1, .75f, .08f), displayFont);
            menu.Label(card.transform, "Title town logo", "TOWN", new Vector2(24, -174), new Vector2(520, 133), 104, Cyan, displayFont);
            menu.Label(card.transform, "Title catchphrase", "まちを、バラバラに！", new Vector2(34, -325), new Vector2(506, 50), 33, Color.white);
            menu.Label(card.transform, "Title description", "走る！ 撃つ！ 街がくずれる！\n1分で、どこまでこわせる？", new Vector2(36, -387), new Vector2(500, 77), 23, new Color(.79f, .91f, .96f));
            start = menu.Button(card.transform, "Title play button", "あそぶ    →    SPACE", new Vector2(34, -491), new Vector2(502, 78), Lime, game.StartOrRestart);
            menu.Button(card.transform, "How to button", "あそびかた    H", new Vector2(34, -588), new Vector2(502, 61), new Color(.09f, .28f, .37f), game.ToggleHowTo);
            menu.Label(card.transform, "Title input hint", "ENTER / SPACE / パッドでもスタート", new Vector2(34, -680), new Vector2(502, 28), 17, new Color(.69f, .82f, .89f)).alignment = TextAnchor.MiddleCenter;
            var city = menu.Panel(title.transform, "Overview caption", new Vector2(918, -48), new Vector2(464, 86), new Color(Ink.r, Ink.g, Ink.b, .82f));
            menu.Label(city.transform, "City caption heading", "つぎは、どこからこわす？", new Vector2(20, -12), new Vector2(424, 32), 24, Color.white).alignment = TextAnchor.MiddleCenter;
            menu.Label(city.transform, "City caption detail", "あそぶたびに、新しい街！", new Vector2(20, -47), new Vector2(424, 27), 18, Lime).alignment = TextAnchor.MiddleCenter;
            var legend = menu.Panel(title.transform, "Title item legend", new Vector2(692, -709), new Vector2(688, 154), new Color(Ink.r, Ink.g, Ink.b, .87f));
            menu.Label(legend.transform, "Color legend heading", "ビルの色が、スペシャルの目印！", new Vector2(20, -12), new Vector2(648, 30), 22, Color.white).alignment = TextAnchor.MiddleCenter;
            AddItems(legend.transform, 37, 54, 104, 56);

            howTo = menu.Panel(parent, "How to screen", Vector2.zero, new Vector2(1440, 900), new Color(Ink.r, Ink.g, Ink.b, .86f));
            howTo.GetComponent<Image>().raycastTarget = true;
            var tutorial = menu.Panel(howTo.transform, "How to card", new Vector2(130, -68), new Vector2(1180, 764), new Color(.055f, .15f, .22f));
            style.Card(tutorial, Cyan);
            menu.Label(tutorial.transform, "Tutorial heading", "あそびかた", new Vector2(42, -25), new Vector2(850, 61), 42, Lime);
            menu.Label(tutorial.transform, "Tutorial subtitle", "自動で前進する戦車で、1分間の破壊チャレンジ！", new Vector2(46, -89), new Vector2(1090, 34), 24, Color.white);
            Step(tutorial.transform, 42, "01", "ハンドルで曲がる", "A / D  または  ← / →\n戦車は、ずっと前に進むよ。\n大きなビルへの激突に注意！", Cyan);
            Step(tutorial.transform, 421, "02", "たたいて、発射！", "SPACE / J / K で砲撃\n向いている方へ弾が飛ぶよ。\n赤い燃料タンクで連鎖爆破！", Lime);
            Step(tutorial.transform, 800, "03", "くずして、集める", "ビルをこわすとアイテム落下！\nビルの色とアイテムが同じ。\n小さなパワーアップも拾おう！", new Color(1, .54f, .24f));
            var specials = menu.Panel(tutorial.transform, "Tutorial specials", new Vector2(42, -386), new Vector2(1096, 187), new Color(.08f, .22f, .29f));
            menu.Label(specials.transform, "Tutorial item heading", "スペシャルの使いかた", new Vector2(24, -13), new Vector2(1048, 34), 25, Color.white);
            AddItems(specials.transform, 104, 58, 164, 59);
            menu.Label(specials.transform, "Tutorial weapon rules", "レーザー・ミサイル・爆弾 → 次の砲撃で発動   /   磁石・ブルドーザー・高速 → 拾うと発動", new Vector2(18, -144), new Vector2(1060, 30), 18, Lime).alignment = TextAnchor.MiddleCenter;
            menu.Label(tutorial.transform, "Tutorial score rules", "破壊度が高いほど高スコア！  爆発したら安全な場所へ戻るよ（時間ペナルティ）。", new Vector2(42, -598), new Vector2(1096, 33), 20, Color.white).alignment = TextAnchor.MiddleCenter;
            menu.Button(tutorial.transform, "How to close button", "もどる    ESC", new Vector2(42, -654), new Vector2(326, 65), new Color(.10f, .28f, .37f), game.ToggleHowTo);
            tutorialPlay = menu.Button(tutorial.transform, "Tutorial play button", "あそぶ    →    SPACE", new Vector2(396, -654), new Vector2(742, 65), Lime, game.StartOrRestart);
            howTo.SetActive(false);
            Select(start);
        }

        public void Fit()
        {
            menu.Fit();
            title.SetActive(!game.HowToOpen); howTo.SetActive(game.HowToOpen);
            if (helpWasOpen != game.HowToOpen) Select(game.HowToOpen ? tutorialPlay : start);
            helpWasOpen = game.HowToOpen;
        }

        private static void Select(Button button)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void Step(Transform parent, float x, string number, string heading, string detail, Color color)
        {
            var panel = menu.Panel(parent, "Tutorial step " + number, new Vector2(x, -151), new Vector2(338, 210), new Color(.08f, .22f, .29f));
            menu.Label(panel.transform, "Tutorial number " + number, number, new Vector2(20, -12), new Vector2(66, 42), 28, color);
            menu.Label(panel.transform, "Tutorial heading " + number, heading, new Vector2(20, -61), new Vector2(308, 40), 25, Color.white);
            menu.Label(panel.transform, "Tutorial body " + number, detail, new Vector2(20, -111), new Vector2(308, 85), 18, new Color(.79f, .91f, .96f));
        }

        private void AddItems(Transform parent, float x, float y, float spacing, float size)
        {
            for (int i = 0; i < 6; i++)
            {
                var kind = (ItemKind)i;
                var icon = menu.Panel(parent, "Menu special icon", new Vector2(x + spacing * i, -y), new Vector2(size, size), Color.white).GetComponent<Image>();
                icon.sprite = game.World.SpecialIcons.Sprite(kind); icon.type = Image.Type.Simple;
                menu.Label(parent, "Menu special label", ItemIcons.Label(kind), new Vector2(x - 26 + spacing * i, -y - size - 5), new Vector2(size + 52, 27), 14, ItemIcons.Color(kind)).alignment = TextAnchor.MiddleCenter;
            }
        }
    }
}
