using UnityEngine;
namespace TankTown
{
    internal sealed class TankTownPauseScreen
    {
        private readonly TankTownGame game;
        private readonly TankTownCardCarousel carousel;
        private readonly TankTownMenuLayout guide;
        private bool visible, guideOpen;
        internal TankTownPauseScreen(TankTownGame owner, Transform canvas, Font font, ToyHudStyle style)
        {
            game=owner;
            carousel=new TankTownCardCarousel(game,canvas,font,style,"Pause screen");
            carousel.Root.SetActive(false);
            guide=new TankTownMenuLayout("Pause guide",canvas,font,style,new Color(.08f,.35f,.7f,.96f));
            var tutorial=new TankTownHowToScreen(game,guide,style);
            tutorial.Play.onClick.RemoveAllListeners(); tutorial.Play.onClick.AddListener(()=>guideOpen=false);
            guide.Root.SetActive(false);
        }
        internal bool Confirm()
        {
            Tick(0);
            if(guideOpen){guideOpen=false;return true;}
            return carousel.Confirm();
        }
        internal void Tick(float dt)
        {
            bool next=game.State==RoundState.Paused;
            if(next&&!visible)
            {
                guideOpen=false;
                carousel.Set("ひとやすみ","",new[]{
                    new TankTownCardCarousel.Choice("つづける","","",TankTownCardCarousel.Pink,game.TogglePause),
                    new TankTownCardCarousel.Choice("あそびかた","","",new Color(.62f,.28f,.96f),()=>guideOpen=true),
                    new TankTownCardCarousel.Choice("タイトルへ","","",new Color(.06f,.7f,.9f),game.ReturnToTitle)});
            }
            visible=next;
            carousel.Root.SetActive(next&&!guideOpen); guide.Root.SetActive(next&&guideOpen);
            if(next){if(guideOpen)guide.Fit();else carousel.Tick(dt);}
        }
    }
}
