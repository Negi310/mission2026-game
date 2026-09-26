using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TankTown
{
    public sealed class TankTownHUD : MonoBehaviour
    {
        private TankTownGame game;
        private Font font;
        private Transform canvas;
        private Text timer, score, counter, notice, cardTitle, cardBody, buttonText;
        private Image fill;
        private GameObject card, noticePanel;
        private readonly Color ink = new Color(.08f, .17f, .22f);
        private readonly Color yellow = new Color(.86f, .97f, .32f);
        public void Initialize(TankTownGame owner)
        {
            game = owner;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 24);
            var root = new GameObject("Tank Town HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.transform;
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Panel("Brand", canvas, new Vector2(26, -24), new Vector2(310, 86), ink);
            Label(canvas, "TANK / TOWN", new Vector2(46, -34), new Vector2(270, 40), 29, yellow);
            Label(canvas, "こわして、街をあそぼう！", new Vector2(48, -77), new Vector2(270, 24), 15, Color.white);
            var clock = Panel("Clock", canvas, new Vector2(-26, -24), new Vector2(204, 86), ink, true);
            timer = Label(clock.transform, "03:00", new Vector2(20, -9), new Vector2(170, 48), 38, Color.white);
            Label(clock.transform, "のこり時間", new Vector2(24, -60), new Vector2(160, 22), 14, yellow);
            var progress = Panel("Destruction", canvas, new Vector2(26, -128), new Vector2(310, 113), new Color(.98f, .97f, .9f, .96f));
            score = Label(progress.transform, "破壊度  0.0%", new Vector2(20, -12), new Vector2(270, 37), 26, ink);
            var track = Panel("Progress track", progress.transform, new Vector2(20, -59), new Vector2(270, 10), new Color(.8f, .84f, .79f));
            fill = Panel("Progress fill", track.transform, Vector2.zero, new Vector2(0, 10), new Color(.22f, .66f, .51f)).GetComponent<Image>();
            counter = Label(progress.transform, "", new Vector2(20, -79), new Vector2(280, 23), 14, ink);
            noticePanel = Panel("Respawn notice", canvas, new Vector2(0, -132), new Vector2(720, 64), ink);
            var noticeRect = noticePanel.GetComponent<RectTransform>();
            noticeRect.anchorMin = noticeRect.anchorMax = new Vector2(.5f, 1);
            noticeRect.pivot = new Vector2(.5f, 1);
            notice = Label(noticePanel.transform, "", new Vector2(16, -7), new Vector2(688, 50), 22, Color.white);
            notice.fontStyle = FontStyle.Bold;
            notice.alignment = TextAnchor.MiddleCenter;
            var bottom = Panel("Controls", canvas, new Vector2(0, 24), new Vector2(900, 54), ink);
            var br = bottom.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(.5f, 0);
            br.pivot = new Vector2(.5f, 0);
            Label(bottom.transform, "ハンドル  A / D ・ ← / →       たたいて発射  SPACE / J / K       ひとやすみ  ESC", new Vector2(20, -14), new Vector2(870, 30), 17, Color.white);
            card = Panel("Round card", canvas, Vector2.zero, new Vector2(630, 380), new Color(.98f, .97f, .91f, .98f));
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(.5f, .5f);
            Label(card.transform, "3 MINUTE DESTRUCTION PLAYGROUND", new Vector2(38, -30), new Vector2(570, 27), 16, new Color(.22f, .55f, .43f));
            cardTitle = Label(card.transform, "", new Vector2(38, -78), new Vector2(560, 62), 42, ink);
            cardBody = Label(card.transform, "", new Vector2(40, -156), new Vector2(550, 110), 21, ink);
            var button = Panel("Start button", card.transform, new Vector2(40, -285), new Vector2(550, 60), yellow);
            button.AddComponent<Button>().onClick.AddListener(game.StartOrRestart);
            buttonText = Label(button.transform, "", new Vector2(0, -10), new Vector2(550, 42), 23, ink);
            buttonText.alignment = TextAnchor.MiddleCenter;
        }

        private GameObject Panel(string title, Transform parent, Vector2 position, Vector2 size, Color color, bool right = false)
        {
            var item = new GameObject(title, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1 : 0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            item.GetComponent<Image>().color = color;
            return item;
        }

        private Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var item = new GameObject("Label", typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        private void Update()
        {
            int seconds = Mathf.CeilToInt(game.Remaining);
            timer.text = $"{seconds / 60:00}:{seconds % 60:00}";
            timer.color = seconds <= 20 ? new Color(1, .55f, .35f) : Color.white;
            score.text = $"破壊度  {game.Destruction:0.0}%";
            counter.text = $"{game.BrokenBlocks} / {game.TotalBlocks} ブロック";
            fill.rectTransform.sizeDelta = new Vector2(270 * game.Destruction / 100, 10);
            notice.text = game.Tank.Respawning && game.IsPlaying ? $"ドカーン！ 安全な場所へもどるよ  −{game.Settings.respawnPenalty:0.#}秒" : "";
            noticePanel.SetActive(game.Tank.Respawning && game.IsPlaying);
            card.SetActive(game.State != RoundState.Playing);
            if (game.State == RoundState.Ready)
            {
                cardTitle.text = "街を、バラバラに！";
                cardBody.text = "ハンドルで曲がる。たたいて撃つ。\n小さいものは、そのまま体当たり！\n大きなビルは、砲撃でこわそう。";
                buttonText.text = "あそぶ   →   ENTER";
            }
            else if (game.State == RoundState.Paused)
            {
                cardTitle.text = "ひとやすみ";
                cardBody.text = "準備ができたら、もういちど出発！\n時間は止まっているよ。";
                buttonText.text = "つづける   →   ENTER";
            }
            else if (game.State == RoundState.Results)
            {
                cardTitle.text = $"{game.Destruction:0.0}%  こわせた！";
                cardBody.text = $"{game.BrokenBlocks} 個のブロックがバラバラ！\nリスポーン {game.Crashes} 回\n次はどの道を走ろう？";
                buttonText.text = "もういちど   →   ENTER";
            }
        }

        private void OnDestroy() { if (font != null) Destroy(font); }
    }
}
