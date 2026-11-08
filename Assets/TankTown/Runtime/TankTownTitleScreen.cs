using UnityEngine;
namespace TankTown
{
 internal sealed class TankTownTitleScreen
 {
  private readonly TankTownGame game;
  private readonly TankTownCardCarousel carousel;
  private bool help, initialized;
  public GameObject Root => carousel.Root;
  public TankTownTitleScreen(TankTownGame owner, Transform canvas, Font font, Font displayFont, ToyHudStyle style)
  { game=owner; carousel=new TankTownCardCarousel(owner,canvas,font,style,"Title screen"); }
  private TankTownCardCarousel.Choice Stage(string title,string detail,string badge,Color color,TownStage stage)
   => new TankTownCardCarousel.Choice(title,detail,badge,color,()=>{game.SelectStage(stage);game.StartOrRestart();});
  public bool Confirm(){Fit();return carousel.Confirm();}
  public void Fit()
  {
   if(!initialized || help!=game.HowToOpen)
   {
    initialized=true;help=game.HowToOpen;
    if(help) carousel.Set("あそびかた","カードを回して あそびかたをチェック！",new[]{
     new TankTownCardCarousel.Choice("ハンドルで走ろう","前進はじどう！\n左右に回して 街を走ろう","↔",new Color(.05f,.7f,.9f),()=>carousel.Move(1)),
     new TankTownCardCarousel.Choice("たたいてドカーン","ボタンで砲撃！\n建物や車をねらおう","★",TankTownCardCarousel.Pink,()=>carousel.Move(1)),
     new TankTownCardCarousel.Choice("アイテムを集めよう","建物を全壊させて\n光るエリアでスペシャルGET","+",new Color(.6f,.3f,.95f),()=>carousel.Move(1)),
     new TankTownCardCarousel.Choice("街をえらぶ","80秒でどこまでこわせる？\nじゅんびができたら出発！","GO",new Color(.95f,.6f,.08f),game.ToggleHowTo)});
    else carousel.Set("<color=#FFD43B>TANK</color> <color=#52DCFF>TOWN</color>","街をえらんで ボタンで出発！",new[]{
     Stage("いつもの街","はじめては ここから！","CITY",new Color(.05f,.66f,.86f),TownStage.Town),
     Stage("大きな街","もっと広く！ もっとドカーン！","BIG",new Color(.58f,.29f,.94f),TownStage.BigCity),
     Stage("お菓子の街","あまい街を こわしちゃおう！","SWEET",TankTownCardCarousel.Pink,TownStage.CandyTown),
     Stage("遊園地","大きな遊具を たおそう！","PARK",new Color(.96f,.6f,.08f),TownStage.AmusementPark),
     new TankTownCardCarousel.Choice("あそびかた","ハンドルとボタンだけ！","?",new Color(.13f,.65f,.45f),game.ToggleHowTo)},(int)game.Stage);
   }
   carousel.Tick(Time.unscaledDeltaTime);
  }
  public void Dispose(){}
 }
}
