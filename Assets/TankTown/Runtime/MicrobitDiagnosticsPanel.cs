using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TankTown
{
    // Explicit hardware setup view; hidden during normal play.
    public sealed class MicrobitDiagnosticsPanel : MonoBehaviour
    {
        private MicrobitController controller;
        private Font font;
        private GUIStyle label, button, heading, field, panel;
        private Texture2D backdrop;
        private BaseInputModule blockedUi;
        private bool uiWasEnabled;
        private bool visible;
        private int threshold = 350, release = 180;
        private string port = "", message = "";
        private float loggedAt;
        private Vector2 scroll;
        internal bool Visible => visible;

        private void Awake()
        {
            controller = GetComponent<MicrobitController>();
            visible = Array.IndexOf(Environment.GetCommandLineArgs(), "--microbit-debug") >= 0;
        }
        private void Start() { if (visible) ReadSettings(); BlockMenuClicks(); }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            { if (!visible) GetComponent<TankControlTuningPanel>()?.Close(); visible = !visible; if (visible) ReadSettings(); }
            BlockMenuClicks();
            if (visible && Time.unscaledTime >= loggedAt)
            { loggedAt = Time.unscaledTime + 2; Debug.Log("MICROBIT DIAG: " + Report().Replace('\n', ' ')); }
        }
        private void BlockMenuClicks()
        {
            var module = visible && EventSystem.current != null ? EventSystem.current.GetComponent<BaseInputModule>() : null;
            if (blockedUi == module) return;
            if (blockedUi != null) blockedUi.enabled = uiWasEnabled;
            blockedUi = module;
            if (blockedUi != null) { uiWasEnabled = blockedUi.enabled; blockedUi.enabled = false; }
        }
        private void ReadSettings()
        {
            var config = controller.Configuration;
            if (config == null) return;
            threshold = config.piezoThreshold; release = config.piezoRelease; port = config.serialPort ?? "";
        }
        internal string Report()
        {
            var data = controller.DiagnosticState;
            var config = controller.Configuration;
            string hint = !controller.Connected ? "有効なデータを受信できていません" : controller.Armed ? "次の打撃を受付可能" : "再受付値以下へ戻るのを待っています";
            return $"状態: {data.Status}\n" +
                $"受信: {data.Bytes} bytes / 有効行 {data.Valid} / 不正行 {data.Invalid}\n" +
                $"BLE: {data.BleStage} / 通知 {data.BleCallbacks} / 通知エラー {data.BleCallbackErrors} {data.BleError}\n" +
                $"最後の受信片: {data.Chunk}\n最後の行: {data.Line}\n" +
                $"角度: {data.Angle:0.0} / ピエゾ: {data.Piezo} / 最小: {(data.Valid == 0 ? "--" : data.Minimum.ToString())} / 最大: {data.Maximum}\n" +
                $"砲撃閾値: {config?.piezoThreshold} / 再受付値: {config?.piezoRelease} / {hint}\n" +
                $"砲撃判定: {controller.DetectedShots} / Input Systemへ送信: {controller.SubmittedShots} / ゲーム入力: {GetComponent<TankInput>()?.MicrobitPresses ?? 0}\n" +
                $"ゲームが前面: {Application.isFocused} / エラー: {controller.LastError}\n設定: {controller.ConfigurationPath}";
        }
        private void OnGUI()
        {
            if (!visible || controller == null) return;
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 20);
                label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, wordWrap = true };
                heading = new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 19, padding = new RectOffset(12, 12, 6, 6) };
                field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 19 };
                label.normal.textColor = heading.normal.textColor = Color.white;
                backdrop = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                backdrop.SetPixel(0, 0, new Color(.025f, .045f, .075f, 1)); backdrop.Apply();
                panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(), padding = new RectOffset(12, 12, 10, 10) };
                panel.normal.background = backdrop;
            }
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .55f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(12, 12, 900, 680), panel);
            scroll = GUILayout.BeginScrollView(scroll);
            try
            {
                GUILayout.Label("コントローラー接続診断  [F8で閉じる]", heading);
                GUILayout.Label(Report(), label);
                GUILayout.Space(8);
                if (!controller.Connected)
                    GUILayout.Label("旋回もできない場合は接続・送信方式の確認が先です。受信が0なら閾値を変えても砲撃できません。", label);
                GUILayout.BeginHorizontal();
                GUILayout.Label("砲撃 " + threshold, label, GUILayout.Width(135));
                threshold = Mathf.RoundToInt(GUILayout.HorizontalSlider(threshold, 1, 1023, GUILayout.Width(380)));
                GUILayout.EndHorizontal();
                release = Mathf.Min(release, threshold - 1);
                GUILayout.BeginHorizontal();
                GUILayout.Label("再受付 " + release, label, GUILayout.Width(135));
                release = Mathf.RoundToInt(GUILayout.HorizontalSlider(release, 0, threshold - 1, GUILayout.Width(380)));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("COM指定（空欄は自動）", label, GUILayout.Width(250));
                port = GUILayout.TextField(port, 12, field, GUILayout.Width(150));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("設定を保存して再接続", button))
                {
                    try
                    {
                        bool selectPort = !string.Equals(port, controller.Configuration?.serialPort ?? "", StringComparison.OrdinalIgnoreCase);
                        controller.ApplyDiagnosticSettings(threshold, release, port, selectPort);
                        message = "保存しました。センサーを一度静かな状態に戻して試してください。";
                    }
                    catch (Exception e) { message = "保存できません: " + e.Message; }
                }
                if (GUILayout.Button("最小・最大をリセット", button)) controller.ResetDiagnosticPeaks();
                if (GUILayout.Button("診断をコピー", button)) { GUIUtility.systemCopyBuffer = Report(); message = "診断情報をコピーしました。"; }
                GUILayout.EndHorizontal();
                if (message.Length > 0) GUILayout.Label(message, label);
            }
            finally { GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.matrix = previous; }
        }
        private void OnDisable() { if (blockedUi != null) blockedUi.enabled = uiWasEnabled; blockedUi = null; }
        internal void Close() { visible = false; BlockMenuClicks(); }
        private void OnDestroy() { if (font != null) Destroy(font); if (backdrop != null) Destroy(backdrop); }
    }
}
