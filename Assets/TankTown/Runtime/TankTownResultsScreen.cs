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
        private readonly Text score, destruction, best, rank, grade;
        private readonly Text finalRank;
        private readonly Text[] recordPoints = new Text[TankTownRanking.Capacity];
        private readonly Text[] recordPercents = new Text[TankTownRanking.Capacity];
        private readonly Image[] recordRows = new Image[TankTownRanking.Capacity];
        private readonly Text[] metrics = new Text[4];
        private readonly CanvasGroup[] metricGroups = new CanvasGroup[4];
        private readonly RectTransform[] confetti = new RectTransform[44];
        private readonly Image[] confettiImages = new Image[44];
        private readonly Image fill;
        private readonly Button replay;
        private readonly int[] values = new int[4];
        private readonly string[] units = { " パーツ", " 回", " CHAIN", " 回" };
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
            menu = new TankTownMenuLayout("Results screen", canvas, font, style, new Color(.02f, .06f, .11f, .77f));
            // A finite burst of lightweight UI confetti. It never moves the game camera.
            for (int i = 0; i < confetti.Length; i++)
            {
                var piece = menu.Panel(menu.Layout, "Result confetti", Vector2.zero, new Vector2(8 + i % 4 * 3, 15), i % 3 == 0 ? Lime : i % 3 == 1 ? Cyan : new Color(1, .66f, .16f));
                confetti[i] = (RectTransform)piece.transform; confettiImages[i] = piece.GetComponent<Image>();
            }
            var panel = menu.Panel(menu.Layout, "Result score card", new Vector2(140, -67), new Vector2(1160, 764), new Color(.98f, .97f, .91f));
            style.Card(panel, Ink); card = (RectTransform)panel.transform;
            cardGroup = panel.AddComponent<CanvasGroup>();
            menu.Label(card, "Result time up", "TIME UP!", new Vector2(40, -20), new Vector2(716, 80), 52, Ink, displayFont);
            menu.Label(card, "Result round label", "1 MINUTE / こわした街の記録", new Vector2(44, -101), new Vector2(734, 30), 20, new Color(.18f, .48f, .48f));
            menu.Label(card, "Score caption", "TOTAL SCORE", new Vector2(44, -157), new Vector2(700, 31), 22, new Color(.16f, .40f, .44f), displayFont);
            score = menu.Label(card, "Result score", "0", new Vector2(38, -190), new Vector2(726, 125), 92, Ink, displayFont);
            var badge = menu.Panel(card, "Result rank badge", new Vector2(786, -30), new Vector2(328, 85), Lime);
            style.Card(badge, Ink);
            menu.Label(badge.transform, "Rank caption", "DESTRUCTION RANK", new Vector2(87, -8), new Vector2(229, 26), 15, Ink, displayFont).alignment = TextAnchor.MiddleCenter;
            grade = menu.Label(badge.transform, "Result rank grade", "C", new Vector2(8, -7), new Vector2(72, 73), 57, Ink, displayFont); grade.alignment = TextAnchor.MiddleCenter;
            rank = menu.Label(badge.transform, "Result title", "", new Vector2(87, -39), new Vector2(229, 35), 20, Ink); rank.alignment = TextAnchor.MiddleCenter;
            finalRank = menu.Label(card, "Result ranking position", "", new Vector2(786, -121), new Vector2(328, 27), 18, Ink); finalRank.alignment = TextAnchor.MiddleCenter;
            var records = menu.Panel(card, "Local leaderboard", new Vector2(786, -157), new Vector2(328, 304), Ink);
            menu.Label(records.transform, "Leaderboard heading", "この端末の TOP 10", new Vector2(12, -8), new Vector2(304, 27), 19, Lime);
            for (int i = 0; i < TankTownRanking.Capacity; i++)
            {
                var row = menu.Panel(records.transform, "Ranking row " + (i + 1), new Vector2(9, -40 - i * 25), new Vector2(310, 24), Color.clear);
                recordRows[i] = row.GetComponent<Image>();
                menu.Label(row.transform, "Ranking number " + i, (i + 1).ToString(), new Vector2(7, 0), new Vector2(31, 24), 16, i < 3 ? Lime : Color.white);
                recordPoints[i] = menu.Label(row.transform, "Ranking score " + i, "—", new Vector2(42, 0), new Vector2(147, 24), 17, Color.white);
                recordPoints[i].alignment = TextAnchor.MiddleRight;
                recordPercents[i] = menu.Label(row.transform, "Ranking destruction " + i, "", new Vector2(200, 0), new Vector2(102, 24), 15, new Color(.64f, .88f, 1));
                recordPercents[i].alignment = TextAnchor.MiddleRight;
            }
            destruction = menu.Label(card, "Result destruction", "破壊度  0.0%", new Vector2(44, -322), new Vector2(640, 63), 39, Ink);
            var track = menu.Panel(card, "Result destruction track", new Vector2(44, -392), new Vector2(700, 18), new Color(.77f, .82f, .78f));
            fill = menu.Panel(track.transform, "Result destruction fill", Vector2.zero, new Vector2(0, 18), new Color(.37f, .78f, .11f)).GetComponent<Image>();
            menu.Label(card, "Result score formula", "破壊度 1% = 1,000点", new Vector2(44, -425), new Vector2(450, 28), 18, new Color(.23f, .43f, .43f));
            best = menu.Label(card, "Result best score", "", new Vector2(44, -454), new Vector2(700, 27), 18, Ink);
            string[] headings = { "こわしたパーツ", "連鎖爆破", "最高チェイン", "リスポーン" };
            for (int i = 0; i < metrics.Length; i++)
            {
                var chip = menu.Panel(card, "Result metric " + i, new Vector2(44 + i * 274, -483), new Vector2(252, 114), Ink);
                metricGroups[i] = chip.AddComponent<CanvasGroup>();
                menu.Label(chip.transform, "Metric caption " + i, headings[i], new Vector2(10, -12), new Vector2(232, 30), 19, i == 1 || i == 2 ? Cyan : Lime).alignment = TextAnchor.MiddleCenter;
                metrics[i] = menu.Label(chip.transform, "Result metric value " + i, "0", new Vector2(10, -44), new Vector2(232, 62), 37, Color.white); metrics[i].alignment = TextAnchor.MiddleCenter;
            }
            replay = menu.Button(card, "Result replay button", "もういちど！    →    SPACE", new Vector2(44, -643), new Vector2(740, 77), Lime, game.StartOrRestart);
            menu.Button(card, "Result title button", "タイトルへ", new Vector2(812, -643), new Vector2(302, 77), Ink, game.ReturnToTitle);
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
                rank.text = game.Destruction >= 85 ? "まちのはかい王" : game.Destruction >= 60 ? "ビルクラッシャー" : game.Destruction >= 35 ? "はかいチャレンジャー" : "はかいルーキー";
                best.text = (game.NewBest ? "NEW BEST!  " : "BEST  ") + game.BestScore.ToString("N0");
                finalRank.text = game.FinalRank == 0 ? "街をこわして記録を作ろう！" : game.FinalRank > TankTownRanking.Capacity ? "今回はランキング圏外" : $"今回の記録  {game.FinalRank}位！";
                for (int i = 0; i < recordPoints.Length; i++)
                {
                    var record = i < game.Ranking.Records.Count ? game.Ranking.Records[i] : null;
                    bool current = record != null && record.Id == game.RoundRecord?.Id;
                    recordPoints[i].text = record == null ? "—" : $"{record.Score:N0} 点";
                    recordPercents[i].text = record == null ? "" : $"{record.Destruction:0.0}%";
                    recordRows[i].color = current ? new Color(.35f, .61f, .15f, .75f) : new Color(1, 1, 1, i % 2 == 0 ? .055f : 0);
                }
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(replay.gameObject);
                game.PlayTone(220, .13f, .15f);
            }
            elapsed += dt;
            float entry = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / EntrySeconds), 3);
            card.anchoredPosition = new Vector2(140, -67 - (1 - entry) * 36);
            cardGroup.alpha = entry;
            float progress = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / CountSeconds), 3);
            DisplayedScore = elapsed >= CountSeconds ? game.RoundScore : Mathf.RoundToInt(game.RoundScore * progress);
            score.text = DisplayedScore.ToString("N0");
            destruction.text = $"破壊度  {game.Destruction * progress:0.0}%";
            fill.rectTransform.sizeDelta = new Vector2(700 * game.Destruction * progress / 100, 18);
            if (elapsed < CountSeconds && elapsed >= tickAt) { tickAt = elapsed + .10f; game.PlayTone(330, .04f, .045f); }
            values[0] = game.BrokenBlocks; values[1] = game.ChainExplosions + game.DebrisExplosions;
            values[2] = game.LongestChain; values[3] = game.Crashes;
            for (int i = 0; i < metrics.Length; i++)
            {
                float t = Mathf.Clamp01((elapsed - .10f - i * .065f) / .46f);
                metricGroups[i].alpha = t;
                metrics[i].text = Mathf.RoundToInt(values[i] * t).ToString("N0") + units[i];
            }
            for (int i = 0; i < confetti.Length; i++)
            {
                float t = elapsed - i % 8 * .025f;
                confetti[i].anchoredPosition = new Vector2(28 + i * 139 % 1380 + Mathf.Sin(t * 2 + i) * 34, 30 - t * (280 + i % 7 * 40));
                confetti[i].localRotation = Quaternion.Euler(0, 0, i * 47 + t * (i % 2 == 0 ? 170 : -140));
                var color = confettiImages[i].color; color.a = Mathf.Clamp01(3.2f - elapsed); confettiImages[i].color = color;
            }
        }
    }
}
