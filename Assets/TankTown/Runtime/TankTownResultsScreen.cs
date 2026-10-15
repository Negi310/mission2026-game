using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        private readonly GameObject details;
        private readonly Text score, recordInfo, best, finalRank, detailScore;
        private readonly Text[] recordPoints = new Text[TankTownRanking.Capacity];
        private readonly Image[] recordRows = new Image[TankTownRanking.Capacity];
        private readonly Text[] metrics = new Text[4];
        private readonly Button replay, showDetails, closeDetails;
        private float elapsed, tickAt;
        private int inputFrame = -1;
        private bool showing;
        internal int DisplayedScore { get; private set; }
        internal bool Counting => showing && elapsed < CountSeconds;
        internal bool DetailsOpen { get; private set; }
        private static Color Lime => TankTownMenuLayout.Lime;

        public TankTownResultsScreen(TankTownGame owner, Transform canvas, Font font, Font displayFont, ToyHudStyle style)
        {
            game = owner;
            menu = new TankTownMenuLayout("Results screen", canvas, font, style, new Color(.02f, .06f, .11f, .83f));
            var panel = menu.Panel(menu.Layout, "Result score card", new Vector2(320, -150), new Vector2(800, 600), Color.white);
            style.TitleControl(panel, new Color(.025f, .09f, .16f, .99f)); card = (RectTransform)panel.transform;
            cardGroup = panel.AddComponent<CanvasGroup>();
            menu.Label(card, "Result round label", game.StageLabel, new Vector2(64, -50), new Vector2(672, 40), 23, new Color(.7f, .82f, .9f)).alignment = TextAnchor.MiddleCenter;
            score = menu.Label(card, "Result score", "0", new Vector2(64, -132), new Vector2(672, 172), 136, Color.white, displayFont);
            score.alignment = TextAnchor.MiddleCenter;
            recordInfo = menu.Label(card, "Result record note", "", new Vector2(64, -328), new Vector2(672, 36), 22, Lime, displayFont);
            recordInfo.alignment = TextAnchor.MiddleCenter;
            replay = menu.ActionButton(card, "Result replay button", "もういちど", new Vector2(64, -408), new Vector2(672, 78), true, game.StartOrRestart, "SPACE");
            menu.ActionButton(card, "Result title button", "タイトルへ", new Vector2(64, -514), new Vector2(280, 42), false, game.ReturnToTitle, quiet: true);
            showDetails = menu.ActionButton(card, "Result details button", "記録を見る", new Vector2(456, -514), new Vector2(280, 42), false, ToggleDetails, quiet: true);

            details = menu.Panel(menu.Layout, "Result details card", new Vector2(64, -64), new Vector2(1312, 772), Color.white);
            style.TitleControl(details, new Color(.025f, .09f, .16f, .99f));
            menu.Label(details.transform, "Result details heading", "プレイ記録", new Vector2(54, -36), new Vector2(760, 58), 38, Color.white);
            menu.Label(details.transform, "Result details stage", game.StageLabel, new Vector2(58, -108), new Vector2(730, 36), 21, new Color(.7f, .82f, .9f));
            detailScore = menu.Label(details.transform, "Result detail score", "", new Vector2(54, -194), new Vector2(760, 68), 42, Color.white, displayFont);
            best = menu.Label(details.transform, "Result best score", "", new Vector2(58, -280), new Vector2(730, 40), 23, new Color(.69f, .83f, .93f), displayFont);
            string[] headings = { "パーツ", "誘爆", "最高チェイン", "リスポーン" };
            for (int i = 0; i < metrics.Length; i++)
            {
                var item = menu.Panel(details.transform, "Result metric " + i, new Vector2(54 + i * 202, -410), new Vector2(202, 112), Color.clear);
                menu.Label(item.transform, "Metric caption " + i, headings[i], new Vector2(12, -6), new Vector2(178, 30), 18, new Color(.69f, .83f, .93f)).alignment = TextAnchor.MiddleCenter;
                metrics[i] = menu.Label(item.transform, "Result metric value " + i, "0", new Vector2(12, -42), new Vector2(178, 60), 32, Color.white, displayFont);
                metrics[i].alignment = TextAnchor.MiddleCenter;
                if (i < 3) menu.Panel(item.transform, "Metric divider", new Vector2(201, -8), new Vector2(1, 90), new Color(.65f, .84f, 1, .25f)).GetComponent<Image>().sprite = null;
            }
            var records = menu.Panel(details.transform, "Local leaderboard", new Vector2(898, -46), new Vector2(360, 548), new Color(.02f, .06f, .11f, .85f));
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
            closeDetails = menu.ActionButton(details.transform, "Result details close button", "もどる", new Vector2(54, -666), new Vector2(320, 72), false, ToggleDetails, "ESC", quiet: true);
            menu.ActionButton(details.transform, "Result details replay button", "もういちど", new Vector2(394, -666), new Vector2(864, 72), true, game.StartOrRestart, "SPACE");
            details.SetActive(false); menu.Root.SetActive(false);
        }

        internal void ToggleDetails()
        {
            if (game.State != RoundState.Results) return;
            DetailsOpen = !DetailsOpen;
            card.gameObject.SetActive(!DetailsOpen); details.SetActive(DetailsOpen);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject((DetailsOpen ? closeDetails : showDetails).gameObject);
        }

        public void Tick(float dt)
        {
            bool visible = game.State == RoundState.Results || game.State == RoundState.Restarting;
            menu.Root.SetActive(visible);
            if (!visible) { showing = false; return; }
            menu.Fit();
            if (!showing)
            {
                showing = true; elapsed = tickAt = 0; DetailsOpen = false;
                card.gameObject.SetActive(true); details.SetActive(false);
                recordInfo.text = game.NewBest ? "NEW BEST" : $"BEST {game.BestScore:N0} pt";
                recordInfo.color = game.NewBest ? Lime : new Color(.7f, .82f, .9f);
                recordInfo.gameObject.SetActive(game.NewBest || game.BestScore > 0);
                detailScore.text = $"{game.RoundScore:N0} pt";
                best.text = "BEST  " + game.BestScore.ToString("N0") + " pt";
                finalRank.text = game.FinalRank == 0 ? "—" : game.FinalRank > TankTownRanking.Capacity ? "圏外" : $"{game.FinalRank}位";
                int[] values = { game.BrokenBlocks, game.ChainExplosions + game.DebrisExplosions, game.LongestChain, game.Crashes };
                for (int i = 0; i < metrics.Length; i++) metrics[i].text = values[i].ToString("N0") + (i == 2 ? " CHAIN" : "");
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
            // Capture refreshes may tick several times in one frame; consume a key once.
            if (inputFrame != Time.frameCount && game.State == RoundState.Results)
            {
                inputFrame = Time.frameCount;
                var keys = Keyboard.current;
                if (keys != null && (keys.tabKey.wasPressedThisFrame || (DetailsOpen && keys.escapeKey.wasPressedThisFrame))) ToggleDetails();
            }
            elapsed += dt;
            float entry = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / EntrySeconds), 3);
            card.anchoredPosition = new Vector2(320, -150 - (1 - entry) * 24);
            cardGroup.alpha = entry;
            float progress = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / CountSeconds), 3);
            DisplayedScore = elapsed >= CountSeconds ? game.RoundScore : Mathf.RoundToInt(game.RoundScore * progress);
            score.text = $"{DisplayedScore:N0}<size=30> pt</size>";
            if (elapsed < CountSeconds && elapsed >= tickAt) { tickAt = elapsed + .10f; game.PlayTone(330, .04f, .045f); }
        }
    }
}
