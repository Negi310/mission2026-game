using UnityEngine;
namespace TankTown
{
 internal sealed class TankTownResultsScreen
 {
  internal const float CountSeconds=.95f;
  private readonly TankTownGame game;
  private readonly TankTownCardCarousel carousel;
  private bool showing;
  private float elapsed,soundAt;
  internal int DisplayedScore{get;private set;}
  internal bool Counting=>showing&&elapsed<CountSeconds;
  internal bool DetailsOpen{get;private set;}
  public TankTownResultsScreen(TankTownGame owner,Transform canvas,Font font,Font displayFont,ToyHudStyle style)
  {game=owner;carousel=new TankTownCardCarousel(game,canvas,font,style,"Results screen");carousel.Root.SetActive(false);}
  private void Choices()
  {
   carousel.Set("","",new[]{
    new TankTownCardCarousel.Choice("もういちど！","同じ街に もう一度チャレンジ","GO!",TankTownCardCarousel.Pink,game.StartOrRestart),
    new TankTownCardCarousel.Choice("きろく","今回のスコアと ランキング","BEST",new Color(.62f,.32f,.96f),ToggleDetails),
    new TankTownCardCarousel.Choice("タイトルへ","ほかの街も 走ってみよう","HOME",new Color(.05f,.68f,.88f),game.ReturnToTitle)});
  }
  internal bool Confirm(){Tick(0);return carousel.Confirm();}
  internal void ToggleDetails()
  {
   DetailsOpen=!DetailsOpen;if(!DetailsOpen){Choices();return;}
   string Ranking(int start) { string value=""; for(int i=start;i<Mathf.Min(start+5,game.Ranking.Records.Count);i++) value+=(i+1)+"位  "+game.Ranking.Records[i].Score.ToString("N0")+" pt\n"; return value.Length==0?"まだ記録がありません":value.TrimEnd(); }
   carousel.Set("きろく","BEST  "+game.BestScore.ToString("N0")+" pt",new[]{
    new TankTownCardCarousel.Choice("今回のきろく","パーツ "+game.BrokenBlocks+"  /  最高チェイン "+game.LongestChain+"\nボタンでもどる",game.FinalRank>0?"#"+game.FinalRank:"RESULT",TankTownCardCarousel.Pink,ToggleDetails),
    new TankTownCardCarousel.Choice("ランキング 1～5",Ranking(0),"RANK",new Color(.55f,.3f,.9f),ToggleDetails),
    new TankTownCardCarousel.Choice("ランキング 6～10",Ranking(5),"RANK",new Color(.1f,.65f,.8f),ToggleDetails)});
  }
  public void Tick(float dt)
  {
   bool visible=game.State==RoundState.Results||game.State==RoundState.Restarting;
   carousel.Root.SetActive(visible);if(!visible){showing=false;return;}
   if(!showing){showing=true;elapsed=soundAt=0;DetailsOpen=false;Choices();game.PlayTone(523,.13f,.1f);}
   elapsed+=dt;
   DisplayedScore=Mathf.RoundToInt(game.RoundScore*(1-Mathf.Pow(1-Mathf.Clamp01(elapsed/CountSeconds),3)));
   if(!DetailsOpen)carousel.Caption(DisplayedScore.ToString("N0")+"<size=34> pt</size>",game.NewBest?"★ ベストこうしん！ ★":"ナイス破壊！   "+game.StageLabel);
   if(elapsed<CountSeconds&&elapsed>soundAt){soundAt=elapsed+.1f;game.PlayTone(420+elapsed*400,.035f,.04f);}
   carousel.Tick(dt);
  }
 }
}

