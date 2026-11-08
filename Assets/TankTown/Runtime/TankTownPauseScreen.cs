using UnityEngine;
namespace TankTown
{
 internal sealed class TankTownPauseScreen
 {
  private readonly TankTownGame game;
  private readonly TankTownCardCarousel carousel;
  private bool visible;
  internal TankTownPauseScreen(TankTownGame owner,Transform canvas,Font font,ToyHudStyle style)
  {game=owner;carousel=new TankTownCardCarousel(game,canvas,font,style,"Pause screen");carousel.Root.SetActive(false);}
  internal bool Confirm(){Tick(0);return carousel.Confirm();}
  internal void Tick(float dt)
  {
   bool next=game.State==RoundState.Paused;carousel.Root.SetActive(next);
   if(next&&!visible)carousel.Set("ひとやすみ","時間はとまっているよ",new[]{
    new TankTownCardCarousel.Choice("つづける","街の破壊に もどろう！","GO!",TankTownCardCarousel.Pink,game.TogglePause),
    new TankTownCardCarousel.Choice("タイトルへ","今回のプレイをおわって 街をえらぶ","HOME",new Color(.06f,.7f,.9f),game.ReturnToTitle)});
   visible=next;if(next)carousel.Tick(dt);
  }
 }
}
