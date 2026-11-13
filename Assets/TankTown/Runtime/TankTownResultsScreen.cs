using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TankTown
{
    internal sealed class TankTownResultsScreen
    {
        internal const float CountSeconds = .95f;
        private const float EntrySeconds = .38f;
        private readonly TankTownGame game;
        private readonly TankTownMenuLayout menu;
        private readonly TankTownCardCarousel actions;
        private readonly ResultConfirmGate confirmGate = new ResultConfirmGate();
        internal bool CanConfirm => showing && confirmGate.Ready;
        private int confirmFrame = -1;
        private readonly RectTransform card;
        private readonly CanvasGroup cardGroup;
        private readonly RectTransform emblem;
        private readonly CanvasGroup emblemGroup;
        private readonly TankTownResultArt panelArt, emblemArt, partyArt, confettiArt;
        private readonly GameObject details;
        private readonly Text score, recordInfo, best, finalRank, detailScore, heroRank;
        private readonly Text roundStage, recordStage;
        private readonly Text[] recordPoints = new Text[TankTownRanking.Capacity];
        private readonly Image[] recordRows = new Image[TankTownRanking.Capacity];
        private readonly Text[] metrics = new Text[4];
        private readonly Button replay, showDetails, closeDetails;
        private float elapsed, tickAt;
        private int inputFrame = -1, fanfareNote;
        private bool showing, settled;
        internal int DisplayedScore { get; private set; }
        internal bool Counting => showing && elapsed < CountSeconds;
        internal bool DetailsOpen { get; private set; }
        private static Color Ink => TankTownResultArt.Ink;
        private static Color Pink => TankTownResultArt.Pink;
        private static Color Purple => TankTownResultArt.Purple;

        public TankTownResultsScreen(TankTownGame owner, Transform canvas, Font font, Font displayFont, ToyHudStyle style)
        {
            game = owner;
            menu = new TankTownMenuLayout("Results screen", canvas, font, style, new Color(.13f, .64f, .94f, .97f));
            partyArt = Art(menu.Layout, "Result party backdrop", new Vector2(1440, 900), ResultArtKind.Party);
            var panel = menu.Panel(menu.Layout, "Result score card", new Vector2(64, -64), new Vector2(1312, 772), Color.clear);
            card = (RectTransform)panel.transform;
            card.pivot = Vector2.one * .5f; card.anchoredPosition = new Vector2(720, -450);
            cardGroup = panel.AddComponent<CanvasGroup>();
            panelArt = Art(card, "Result toy score board", card.sizeDelta, ResultArtKind.Panel);
            var heading = menu.Label(card, "Result heading", "けっか はっぴょう！", new Vector2(61, -31), new Vector2(712, 104), 62, Color.white);
            heading.alignment = TextAnchor.MiddleCenter;
            Outline(heading, new Color(.64f, .05f, .35f), 3);
            roundStage = menu.Label(card, "Result round label", game.StageLabel, new Vector2(880, -67), new Vector2(375, 40), 23, Purple); roundStage.alignment = TextAnchor.MiddleRight;
            menu.Label(card, "Result score caption", "SCORE", new Vector2(65, -190), new Vector2(760, 40), 27, Pink, displayFont);
            score = menu.Label(card, "Result score", "0", new Vector2(55, -235), new Vector2(819, 202), 156, Ink, displayFont);
            score.alignment = TextAnchor.MiddleCenter; score.fontStyle = FontStyle.Bold;
            score.rectTransform.pivot = Vector2.one * .5f; score.rectTransform.anchoredPosition = new Vector2(464.5f, -336);
            var scoreShadow = score.gameObject.AddComponent<Shadow>(); scoreShadow.effectColor = new Color(1, .32f, .55f, .65f); scoreShadow.effectDistance = new Vector2(0, -7);
            Outline(score, Color.white, 3);
            recordInfo = menu.Label(card, "Result record note", "", new Vector2(78, -512), new Vector2(730, 48), 28, Pink);
            var seal = menu.Panel(card, "Result rank emblem", new Vector2(946, -195), new Vector2(310, 310), Color.clear);
            emblem = (RectTransform)seal.transform;
            emblem.pivot = Vector2.one * .5f; emblem.anchoredPosition = new Vector2(1101, -350);
            emblemGroup = seal.AddComponent<CanvasGroup>(); emblemArt = Art(emblem, "Result rank medal", emblem.sizeDelta, ResultArtKind.Emblem);
            heroRank = menu.Label(emblem, "Result hero rank", "—", new Vector2(25, -103), new Vector2(260, 110), 86, Ink, displayFont);
            heroRank.alignment = TextAnchor.MiddleCenter;
            menu.Label(emblem, "Result rank caption", "RANK", new Vector2(35, -211), new Vector2(240, 36), 23, Ink, displayFont).alignment = TextAnchor.MiddleCenter;
            replay = Action(card, "Result replay button", "もういちど", new Vector2(54, -595), new Vector2(830, 84), true, game.StartOrRestart, "SPACE");
            showDetails = Action(card, "Result details button", "記録を見る", new Vector2(944, -605), new Vector2(312, 64), false, ToggleDetails);
            var title = menu.ActionButton(card, "Result title button", "タイトルへ", new Vector2(54, -704), new Vector2(240, 40), false, game.ReturnToTitle, quiet: true);
            title.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            title.GetComponentInChildren<Text>().color = Purple;
            replay.gameObject.SetActive(false); showDetails.gameObject.SetActive(false); title.gameObject.SetActive(false);

            details = menu.Panel(menu.Layout, "Result details card", new Vector2(64, -64), new Vector2(1312, 772), Color.clear);
            Art(details.transform, "Result record toy board", new Vector2(1312, 772), ResultArtKind.Details);
            menu.Label(details.transform, "Result details heading", "きろくを みよう！", new Vector2(61, -39), new Vector2(710, 62), 42, Color.white);
            recordStage = menu.Label(details.transform, "Result details stage", game.StageLabel, new Vector2(61, -108), new Vector2(710, 32), 20, Color.white);
            detailScore = menu.Label(details.transform, "Result detail score", "", new Vector2(65, -194), new Vector2(760, 68), 48, Ink, displayFont);
            best = menu.Label(details.transform, "Result best score", "", new Vector2(68, -280), new Vector2(730, 40), 25, Purple, displayFont);
            string[] headings = { "パーツ", "誘爆", "最高チェイン", "リスポーン" };
            Color[] metricColors = { new Color(1, .88f, .93f), new Color(1, .93f, .70f), new Color(.78f, .96f, 1), new Color(.9f, .84f, 1) };
            for (int i = 0; i < metrics.Length; i++)
            {
                var item = menu.Panel(details.transform, "Result metric " + i, new Vector2(54 + i * 202, -410), new Vector2(194, 112), metricColors[i]);
                menu.Label(item.transform, "Metric caption " + i, headings[i], new Vector2(8, -6), new Vector2(178, 30), 18, Ink).alignment = TextAnchor.MiddleCenter;
                metrics[i] = menu.Label(item.transform, "Result metric value " + i, "0", new Vector2(8, -42), new Vector2(178, 60), 32, Ink, displayFont);
                metrics[i].alignment = TextAnchor.MiddleCenter;
            }
            var records = menu.Panel(details.transform, "Local leaderboard", new Vector2(898, -66), new Vector2(360, 548), new Color(.83f, .96f, 1));
            menu.Label(records.transform, "Leaderboard heading", "TOP 10", new Vector2(24, -18), new Vector2(190, 36), 26, Purple, displayFont);
            finalRank = menu.Label(records.transform, "Result ranking position", "", new Vector2(234, -18), new Vector2(102, 36), 23, Pink);
            finalRank.alignment = TextAnchor.MiddleRight;
            for (int i = 0; i < TankTownRanking.Capacity; i++)
            {
                var row = menu.Panel(records.transform, "Ranking row " + (i + 1), new Vector2(20, -74 - i * 44), new Vector2(320, 38), Color.clear);
                recordRows[i] = row.GetComponent<Image>();
                menu.Label(row.transform, "Ranking number " + i, (i + 1).ToString(), new Vector2(20, 0), new Vector2(38, 38), 20, i < 3 ? Pink : Ink).alignment = TextAnchor.MiddleLeft;
                recordPoints[i] = menu.Label(row.transform, "Ranking score " + i, "—", new Vector2(72, 0), new Vector2(228, 38), 22, Ink, displayFont);
                recordPoints[i].alignment = TextAnchor.MiddleRight;
            }
            closeDetails = Action(details.transform, "Result details close button", "もどる", new Vector2(54, -666), new Vector2(1204, 72), false, ToggleDetails);
            confettiArt = Art(menu.Layout, "Result confetti", new Vector2(1440, 900), ResultArtKind.Confetti);
            details.SetActive(false); menu.Root.SetActive(false);
            actions = new TankTownCardCarousel(game, canvas, font, style, "Result actions");
            actions.Root.SetActive(false);
        }

        private void ResetActions()
        {
            actions.Set("", "", new[] {
                new TankTownCardCarousel.Choice("もういちど！", "", "", Pink, () => RunAction(game.StartOrRestart)),
                new TankTownCardCarousel.Choice("きろく", "", "", Purple, ToggleDetails),
                new TankTownCardCarousel.Choice("タイトルへ", "", "", new Color(.05f,.7f,.92f), () => RunAction(game.ReturnToTitle))
            });
        }
        internal bool Confirm()
        {
            if(confirmFrame == Time.frameCount) return true;
            confirmFrame = Time.frameCount;
            if (!AcceptInput()) return true;
            if(DetailsOpen){ToggleDetails();return true;}
            return actions.Confirm();
        }

        private bool AcceptInput()
        {
            if (game.State != RoundState.Results) return false;
            // Input can arrive before the HUD's first results update.
            if (!showing) Tick(0);
            bool accepted = confirmGate.TryConfirm(Time.unscaledTimeAsDouble);
            actions.ConfirmEnabled = accepted;
            return accepted;
        }

        private void RunAction(System.Action action)
        {
            if (AcceptInput()) action();
        }

        private static void Outline(Text label, Color tint, float width)
        {
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = tint; outline.effectDistance = new Vector2(width, -width);
        }

        private TankTownResultArt Art(Transform parent, string name, Vector2 size, ResultArtKind kind)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(TankTownResultArt)); item.transform.SetParent(parent, false);
            var rect = (RectTransform)item.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.sizeDelta = size;
            var art = item.GetComponent<TankTownResultArt>(); art.Initialize(kind, TankTownMenuLayout.Cyan); return art;
        }

        private Button Action(Transform parent, string name, string caption, Vector2 point, Vector2 size, bool primary, UnityEngine.Events.UnityAction action, string key = null)
        {
            return TankTownCardCarousel.CreateAction(menu, parent, name, caption, point, size, primary ? Pink : Purple, () => action());
        }

        internal void ToggleDetails()
        {
            if (!AcceptInput()) return;
            DetailsOpen = !DetailsOpen;
            card.gameObject.SetActive(!DetailsOpen); details.SetActive(DetailsOpen);
            confettiArt.gameObject.SetActive(!DetailsOpen);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void Tick(float dt)
        {
            bool visible = game.State == RoundState.Results || game.State == RoundState.Restarting;
            menu.Root.SetActive(visible);
            actions.Root.SetActive(visible && !DetailsOpen);
            if (!visible) { showing = false; return; }
            menu.Fit();
            if (!showing)
            {
                showing = true; elapsed = tickAt = 0; DetailsOpen = false; settled = false; fanfareNote = 0;
                confirmGate.Reset(Time.unscaledTimeAsDouble);
                ResetActions();
                roundStage.text = recordStage.text = game.StageLabel;
                card.gameObject.SetActive(true); details.SetActive(false);
                confettiArt.gameObject.SetActive(true);
                recordInfo.text = game.NewBest ? "ベスト こうしん！" : $"BEST {game.BestScore:N0} pt";
                var accent = game.NewBest ? Pink : Purple;
                recordInfo.color = accent;
                recordInfo.gameObject.SetActive(game.NewBest || game.BestScore > 0);
                panelArt.SetAccent(accent); emblemArt.SetAccent(accent);
                heroRank.text = game.FinalRank > 0 ? "#" + game.FinalRank.ToString("00") : "—";
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
                    recordPoints[i].color = current ? Pink : Ink;
                    recordRows[i].color = current ? new Color(1, .8f, .89f) : new Color(1, 1, 1, i % 2 == 0 ? .85f : .35f);
                }
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                game.PlayTone(523.25f, .12f, .10f);
            }
            confirmGate.Observe(Time.unscaledTimeAsDouble, game.Input.MenuConfirmHeld);
            actions.ConfirmEnabled = confirmGate.Ready && game.State == RoundState.Results;
            // Capture refreshes may tick several times in one frame; consume a key once.
            if (inputFrame != Time.frameCount && game.State == RoundState.Results)
            {
                inputFrame = Time.frameCount;
                var keys = Keyboard.current;
                if (keys != null && (keys.tabKey.wasPressedThisFrame || (DetailsOpen && keys.escapeKey.wasPressedThisFrame))) ToggleDetails();
            }
            elapsed += dt;
            float entry = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / EntrySeconds), 3);
            card.anchoredPosition = new Vector2(720, -450 - (1 - entry) * 26);
            card.localScale = Vector3.one * Mathf.LerpUnclamped(.88f, 1, Pop(Mathf.Clamp01(elapsed / .48f)));
            cardGroup.alpha = entry;
            float sealEntry = 1 - Mathf.Pow(1 - Mathf.Clamp01((elapsed - .12f) / .42f), 3);
            emblemGroup.alpha = sealEntry;
            emblem.localScale = Vector3.one * Mathf.LerpUnclamped(.55f, 1, Pop(Mathf.Clamp01((elapsed - .12f) / .58f)));
            float wiggle = Mathf.Clamp01(1 - (elapsed - .5f) / 1.5f);
            emblem.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(elapsed * 9) * 4 * wiggle);
            float progress = 1 - Mathf.Pow(1 - Mathf.Clamp01(elapsed / CountSeconds), 3);
            DisplayedScore = elapsed >= CountSeconds ? game.RoundScore : Mathf.RoundToInt(game.RoundScore * progress);
            score.text = $"{DisplayedScore:N0}<size=38> pt</size>";
            float impact = Mathf.Clamp01((elapsed - CountSeconds) / .34f);
            float scoreBounce = elapsed < CountSeconds ? Mathf.Sin(elapsed * 22) * .018f : Mathf.Sin(impact * Mathf.PI) * .055f;
            score.rectTransform.localScale = Vector3.one * (1 + scoreBounce);
            panelArt.SetMotion(DisplayedScore / 100000f, Mathf.Clamp01((elapsed - CountSeconds) / .48f));
            emblemArt.SetMotion(Mathf.Clamp01(elapsed / CountSeconds));
            partyArt.SetMotion(0, elapsed);
            confettiArt.SetMotion(0, elapsed - CountSeconds);
            if(!DetailsOpen) actions.Tick(dt);
            if (!settled && elapsed >= CountSeconds)
            {
                settled = true;
                if (game.RoundScore > 0) game.PlayTone(783.99f, .13f, .10f);
            }
            if (game.RoundScore > 0 && elapsed >= CountSeconds + .13f + fanfareNote * .14f && fanfareNote < 3)
            {
                float note = fanfareNote == 0 ? 987.77f : fanfareNote == 1 ? 1174.66f : 1567.98f;
                game.PlayTone(note, fanfareNote == 2 ? .24f : .12f, .085f); fanfareNote++;
            }
            if (elapsed < CountSeconds && elapsed >= tickAt) { tickAt = elapsed + .10f; game.PlayTone(Mathf.Lerp(330, 660, progress), .04f, .045f); }
        }

        private static float Pop(float t)
        {
            float x = t - 1;
            return 1 + 2.70158f * x * x * x + 1.70158f * x * x;
        }
    }

    // Require the score reveal and a quiet interval; continuous firing never arms it.
    internal sealed class ResultConfirmGate
    {
        internal const double MinimumDelay = 1.35;
        internal const double QuietSeconds = .65;
        private double earliest, lastObserved, quiet;
        internal bool Ready { get; private set; }
        internal void Reset(double now) { earliest = now + MinimumDelay; lastObserved = now; quiet = 0; Ready = false; }
        internal void Observe(double now, bool held)
        {
            if (Ready) return;
            if (held) { lastObserved = now; quiet = 0; }
            else AdvanceQuiet(now);
        }
        internal bool TryConfirm(double now)
        {
            if (!Ready) { AdvanceQuiet(now); if (!Ready) quiet = 0; }
            return Ready;
        }
        private void AdvanceQuiet(double now)
        {
            // A stalled frame is not evidence that the player stopped firing.
            quiet += System.Math.Min(.1, System.Math.Max(0, now - lastObserved));
            lastObserved = now;
            Ready = now >= earliest && quiet + 1e-9 >= QuietSeconds;
        }
    }
}
