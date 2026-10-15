using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TankTown
{
    internal sealed class TankTownResultsScreen
    {
        internal const float CountSeconds = .95f;
        private const float EntrySeconds = .26f;
        private readonly TankTownGame game;
        private readonly TankTownMenuLayout menu;
        private readonly RectTransform card;
        private readonly CanvasGroup cardGroup;
        private readonly Text score, best, grade, finalRank;
        private readonly GameObject newBest;
        private readonly Text[] recordPoints = new Text[TankTownRanking.Capacity];
        private readonly Image[] recordRows = new Image[TankTownRanking.Capacity];
        private readonly Text[] metrics = new Text[4];
        private readonly CanvasGroup[] metricGroups = new CanvasGroup[4];
        private readonly RectTransform[] confetti = new RectTransform[24];
        private readonly Image[] confettiImages = new Image[24];
        private readonly Button replay;
        private readonly int[] values = new int[4];
        private float elapsed, tickAt;
        private bool showing;
        internal int DisplayedScore { get; private set; }
        internal bool Counting => showing && elapsed < CountSeconds;
        private static Color Ink => TankTownMenuLayout.Ink;
        private static Color Lime => TankTownMenuLayout.Lime;
        private static Color Cyan => TankTownMenuLayout.Cyan;

        public TankTownResultsScreen(TankTownGame owner, Transform canvas, Font font, Font displayFont, ToyHudStyle style)
        {
            game = owner;
            menu = new TankTownMenuLayout("Results screen", canvas, font, style, new Color(.02f, .06f, .11f, .65f));
            for (int i = 0; i < confetti.Length; i++)
            {
                var piece = menu.Panel(menu.Layout, "Result confetti", Vector2.zero, new Vector2(7 + i % 3 * 2, 12), i % 3 == 0 ? Lime : i % 3 == 1 ? Cyan : new Color(1, .66f, .16f));
                confetti[i] = (RectTransform)piece.transform; confettiImages[i] = piece.GetComponent<Image>();
            }
            var panel = menu.Panel(menu.Layout, "Result score card", new Vector2(64, -64), new Vector2(1312, 772), Color.white);
            style.TitleControl(panel, new Color(.025f, .09f, .16f, .99f)); card = (RectTransform)panel.transform;
            cardGroup = panel.AddComponent<CanvasGroup>();
            menu.Label(card, "Result heading", "RESULT", new Vector2(54, -24), new Vector2(770, 78), 58, Color.white, displayFont);
            var stage = menu.Panel(card, "Result stage pill", new Vector2(56, -112), new Vector2(240, 36), new Color(.25f, .46f, .60f, .28f));
            menu.Label(stage.transform, "Result round label", game.StageLabel, new Vector2(20, 0), new Vector2(200, 36), 19, Color.white).alignment = TextAnchor.MiddleCenter;
            newBest = menu.Panel(card, "Result new best badge", new Vector2(54, -194), new Vector2(198, 38), Lime);
            menu.Label(newBest.transform, "Result new best label", "NEW BEST", new Vector2(18, 0), new Vector2(162, 38), 20, Ink, displayFont).alignment = TextAnchor.MiddleCenter;
            score = menu.Label(card, "Result score", "0", new Vector2(50, -240), new Vector2(620, 156), 118, Color.white, displayFont);
            score.alignment = TextAnchor.MiddleLeft;
            best = menu.Label(card, "Result best score", "", new Vector2(58, -414), new Vector2(610, 40), 23, new Color(.69f, .83f, .93f), displayFont);
            menu.Label(card, "Rank caption", "RANK", new Vector2(680, -226), new Vector2(160, 34), 21, Lime, displayFont).alignment = TextAnchor.MiddleCenter;
            var ring = menu.Panel(card, "Result rank ring", new Vector2(696, -272), new Vector2(128, 128), Lime).GetComponent<Image>();
            ring.sprite = style.Ring; ring.type = Image.Type.Simple;
            grade = menu.Label(ring.transform, "Result rank grade", "C", Vector2.zero, new Vector2(128, 128), 76, Lime, displayFont); grade.alignment = TextAnchor.MiddleCenter;
            string[] headings = { "パーツ", "誘爆", "最高チェイン", "リスポーン" };
            for (int i = 0; i < metrics.Length; i++)
            {
                var item = menu.Panel(card, "Result metric " + i, new Vector2(54 + i * 202, -508), new Vector2(202, 112), Color.clear);
                metricGroups[i] = item.AddComponent<CanvasGroup>();
                menu.Label(item.transform, "Metric caption " + i, headings[i], new Vector2(12, -6), new Vector2(178, 30), 18, new Color(.69f, .83f, .93f)).alignment = TextAnchor.MiddleCenter;
                metrics[i] = menu.Label(item.transform, "Result metric value " + i, "0", new Vector2(12, -42), new Vector2(178, 60), 32, Color.white, displayFont);
                metrics[i].alignment = TextAnchor.MiddleCenter;
                if (i < 3)
                {
                    var line = menu.Panel(item.transform, "Metric divider", new Vector2(201, -8), new Vector2(1, 90), new Color(.65f, .84f, 1, .25f)).GetComponent<Image>(); line.sprite = null;
                }
            }
            var records = menu.Panel(card, "Local leaderboard", new Vector2(898, -46), new Vector2(360, 548), new Color(.02f, .06f, .11f, .85f));
            menu.Label(records.transform, "Leaderboard heading", "TOP 10", new Vector2(24, -18), new Vector2(190, 36), 26, Color.white, displayFont);
            finalRank = menu.Label(records.transform, "Result ranking position", "", new Vector2(234, -18), new Vector2(102, 36), 23, Lime);
            finalRank.alignment = TextAnchor.MiddleRight;
            for (int i = 0; i < TankTownRanking.Capacity; i++)
            {
                var row = menu.Panel(records.transform, "Ranking row " + (i + 1), new Vector2(20, -74 - i * 44), new Vector2(320, 38), Color.clear);
                recordRows[i] = row.GetComponent<Image>();
                menu.Label(row.transform, "Ranking number " + i, (i + 1).ToString(), new Vector2(20, 0), new Vector2(38, 38), 20, i < 3 ? Lime : Color.white).alignment = TextAnchor.MiddleLeft;
                recordPoints[i] = menu.Label(row.transform, "Ranking score " + i, "—", new Vector2(72, 0), new Vector2(228, 38), 22, Color.white, displayFont);
                recordPoints[i].alignment = TextAnchor.MiddleRight;
            }
            menu.ActionButton(card, "Result title button", "タイトルへ", new Vector2(54, -666), new Vector2(460, 72), false, game.ReturnToTitle);
            replay = menu.ActionButton(card, "Result replay button", "もういちど", new Vector2(534, -666), new Vector2(724, 72), true, game.StartOrRestart, "SPACE");
            menu.Root.SetActive(false);
        }

        public void Tick(float dt)
        {
            bool visible = game.State == RoundState.Results || game.State == RoundState.Restarting;
            menu.Root.SetActive(visible);
            if (!visible) { showing = false; return; }
            menu.Fit();
            if (!showing)
            {
                showing = true; elapsed = tickAt = 0;
                grade.text = game.Destruction >= 85 ? "S" : game.Destruction >= 60 ? "A" : game.Destruction >= 35 ? "B" : "C";
                newBest.SetActive(game.NewBest);
                best.text = "BEST  " + game.BestScore.ToString("N0") + " pt";
                finalRank.text = game.FinalRank == 0 ? "—" : game.FinalRank > TankTownRanking.Capacity ? "圏外" : $"{game.FinalRank}位";
                for (int i = 0; i < recordPoints.Length; i++)
                {
                    var record = i < game.Ranking.Records.Count ? game.Ranking.Records[i] : null;
                    bool current = record != null && record.Id == game.RoundRecord?.Id;
                    recordPoints[i].text = record == null ? "—" : $"{record.Score:N0} pt";
                    recordPoints[i].color = current ? Lime : Color.white;
                    recordRows[i].color = current ? new Color(.55f, .85f, .08f, .22f) : new Color(.54f, .72f, .88f, i % 2 == 0 ? .085f : .025f);
                }
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(replay.gameObject);
                game.PlayTone(220, .13f, .15f);
            }
            elapsed += dt;
            float entry = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / EntrySeconds), 3);
            card.anchoredPosition = new Vector2(64, -64 - (1 - entry) * 24);
            cardGroup.alpha = entry;
            float progress = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / CountSeconds), 3);
            DisplayedScore = elapsed >= CountSeconds ? game.RoundScore : Mathf.RoundToInt(game.RoundScore * progress);
            score.text = $"{DisplayedScore:N0}<size=30> pt</size>";
            if (elapsed < CountSeconds && elapsed >= tickAt) { tickAt = elapsed + .10f; game.PlayTone(330, .04f, .045f); }
            values[0] = game.BrokenBlocks; values[1] = game.ChainExplosions + game.DebrisExplosions; values[2] = game.LongestChain; values[3] = game.Crashes;
            for (int i = 0; i < metrics.Length; i++)
            {
                float t = Mathf.Clamp01((elapsed - .10f - i * .065f) / .46f);
                metricGroups[i].alpha = t;
                metrics[i].text = Mathf.RoundToInt(values[i] * t).ToString("N0") + (i == 2 ? " CHAIN" : "");
            }
            for (int i = 0; i < confetti.Length; i++)
            {
                float t = elapsed - i % 8 * .025f;
                confetti[i].anchoredPosition = new Vector2(28 + i * 139 % 1380 + Mathf.Sin(t * 2 + i) * 34, 30 - t * (280 + i % 7 * 40));
                confetti[i].localRotation = Quaternion.Euler(0, 0, i * 47 + t * (i % 2 == 0 ? 170 : -140));
                var color = confettiImages[i].color; color.a = Mathf.Clamp01(2.4f - elapsed); confettiImages[i].color = color;
            }
        }
    }
}
