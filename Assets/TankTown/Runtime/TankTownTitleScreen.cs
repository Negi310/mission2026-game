using UnityEngine;
using UnityEngine.UI;
namespace TankTown
{
    internal sealed class TankTownTitleScreen
    {
        private readonly TankTownGame game;
        private readonly TankTownCardCarousel carousel;
        private readonly TankTownMenuLayout help;
        private readonly GameObject root;
        public GameObject Root => root;
        public TankTownTitleScreen(TankTownGame owner, Transform canvas, Font font, Font displayFont, ToyHudStyle style)
        {
            game = owner;
            root = new GameObject("Title screen group", typeof(RectTransform)); root.transform.SetParent(canvas, false);
            var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            carousel = new TankTownCardCarousel(owner, root.transform, font, style, "Title screen");
            carousel.SetHeadingFont(displayFont);
            carousel.Set("<color=#FFD43B>TANK</color> <color=#52DCFF>TOWN</color>", "街をえらんで、ドカーンと出発！", new[] {
                Stage("いつもの街", new Color(.02f,.7f,.92f), TownStage.Town),
                Stage("大きな街", new Color(.62f,.28f,.96f), TownStage.BigCity),
                Stage("お菓子の街", TankTownCardCarousel.Pink, TownStage.CandyTown),
                Stage("遊園地", new Color(1,.62f,.08f), TownStage.AmusementPark),
                new TankTownCardCarousel.Choice("あそびかた", "", "", new Color(.13f,.68f,.45f), game.ToggleHowTo)
            }, (int)game.Stage);
            help = new TankTownMenuLayout("How to overlay", root.transform, font, style, new Color(.08f,.35f,.7f,.96f));
            var tutorial = new TankTownHowToScreen(game, help, style);
            tutorial.Play.onClick.RemoveAllListeners(); tutorial.Play.onClick.AddListener(game.ToggleHowTo);
            tutorial.Play.GetComponentInChildren<Text>().text = "ボタンで もどる";
            help.Root.SetActive(false);
        }
        private TankTownCardCarousel.Choice Stage(string label, Color color, TownStage stage)
            => new TankTownCardCarousel.Choice(label, "", "", color, () => {game.SelectStage(stage); game.StartOrRestart();});
        public bool Confirm()
        {
            if(game.HowToOpen){game.ToggleHowTo(); return true;}
            return carousel.Confirm();
        }
        public void Fit()
        {
            carousel.Root.SetActive(!game.HowToOpen);
            help.Root.SetActive(game.HowToOpen);
            if(game.HowToOpen) help.Fit(); else carousel.Tick(Time.unscaledDeltaTime);
        }
        public void Dispose(){Object.Destroy(help.Root);}
    }
}
