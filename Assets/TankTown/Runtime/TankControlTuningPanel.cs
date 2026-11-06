using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TankTown
{
    public sealed class TankControlTuningPanel : MonoBehaviour
    {
        private TankTownGame game;
        private MicrobitController controller;
        private readonly MicrobitInputSettings fallback = new MicrobitInputSettings();
        private ControlTuning draft, defaults;
        private bool visible;
        private string message = "";
        private Font font;
        private Texture2D backdrop;
        private GUIStyle label, heading, button, panel;
        private Vector2 scroll;
        private BaseInputModule blockedUi;
        private bool uiWasEnabled;
        internal bool Visible => visible;
        private MicrobitInputSettings Hardware => controller.Configuration ?? fallback;

        internal void Initialize(TankTownGame owner, MicrobitController input)
        {
            game = owner; controller = input;
            var source = Resources.Load<TankTownSettings>("TankTownSettings");
            defaults = ControlTuning.Capture(source != null ? source : owner.Settings, new MicrobitInputSettings());
        }
        private void Update()
        {
            if (game == null || Keyboard.current == null || !Keyboard.current.f7Key.wasPressedThisFrame) return;
            if (visible) Close(); else Open();
        }
        internal void Open()
        {
            if (visible || game == null) return;
            GetComponent<MicrobitDiagnosticsPanel>()?.Close();
            draft = ControlTuning.Capture(game.Settings, Hardware); visible = true; message = "";
            var system = EventSystem.current;
            blockedUi = system != null ? system.GetComponent<BaseInputModule>() : null;
            if (blockedUi != null) { uiWasEnabled = blockedUi.enabled; blockedUi.enabled = false; }
        }
        internal void Close()
        {
            visible = false;
            if (blockedUi != null) blockedUi.enabled = uiWasEnabled;
            blockedUi = null;
        }
        private void Apply(bool save)
        {
            try
            {
                bool hardwareChanged = draft.center != Hardware.angleCenter || draft.sensitivity != Hardware.fullSteerAngle ||
                    draft.deadzone != Hardware.deadzoneDegrees || draft.invert != Hardware.invertSteering ||
                    draft.threshold != Hardware.piezoThreshold || draft.release != Hardware.piezoRelease || draft.strikeCooldown != Hardware.piezoCooldownSeconds;
                draft.Apply(game.Settings, Hardware);
                if (hardwareChanged && controller.Configuration != null) controller.ResetTuningInput();
                if (save) draft.Save();
                message = save ? "適用して保存しました。次回起動時も使用します。" : "適用しました。保存すると次回起動時も使用します。";
            }
            catch (System.Exception e) { message = "適用できません: " + e.Message; }
        }
        private float Slider(string title, float value, float min, float max, string format = "0.0")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, label, GUILayout.Width(250));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(340));
            GUILayout.Label(value.ToString(format), label, GUILayout.Width(85));
            GUILayout.EndHorizontal();
            return value;
        }
        private void OnGUI()
        {
            if (!visible || draft == null) return;
            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 20);
                label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, wordWrap = true };
                label.normal.textColor = Color.white;
                heading = new GUIStyle(label) { fontSize = 24, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 19, padding = new RectOffset(10, 10, 6, 6) };
                backdrop = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                backdrop.SetPixel(0, 0, new Color(.025f, .045f, .075f, 1)); backdrop.Apply();
                panel = new GUIStyle(GUI.skin.box) { border = new RectOffset(), padding = new RectOffset(12, 12, 10, 10) };
                panel.normal.background = backdrop;
            }
            var previous = GUI.matrix;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .55f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(12, 12, 850, 680), panel);
            scroll = GUILayout.BeginScrollView(scroll);
            try
            {
                GUILayout.Label("操作感の調整  [F7で閉じる]", heading);
                GUILayout.Label("スライダーを動かし、適用して走行を試せます。ゲームは停止しません。", label);
                GUILayout.Label("戦車の動き", heading);
                draft.speed = Slider("移動速度", draft.speed, 1, 50);
                draft.acceleration = Slider("加速", draft.acceleration, .1f, 50);
                draft.turn = Slider("旋回速度", draft.turn, 10, 240);
                draft.response = Slider("旋回の反応", draft.response, .1f, 40);
                draft.grip = Slider("横滑りの抑え", draft.grip, .1f, 30);
                draft.hullDelay = Slider("車体の旋回遅れ（秒）", draft.hullDelay, 0, .5f, "0.00");
                draft.turretYaw = Slider("砲塔の最大旋回角", draft.turretYaw, 0, 60);
                draft.turretSpeed = Slider("砲塔の追従速度", draft.turretSpeed, 30, 500);
                draft.cooldown = Slider("射撃間隔（秒）", draft.cooldown, .12f, 2, "0.00");
                GUILayout.Label("ハンドル・ピエゾ", heading);
                draft.center = Slider("ハンドルの中立", draft.center, -1023, 1023, "0");
                if (GUILayout.Button("現在のハンドル位置を中立にする", button))
                    message = controller.Connected ? (draft.center = Mathf.Clamp(controller.Angle, -1023, 1023)).ToString("0") + " を中立に設定しました。適用してください。" : "コントローラーから受信していません。";
                draft.sensitivity = Slider("最大旋回までの入力", draft.sensitivity, .1f, 1023, "0.0");
                draft.deadzone = Slider("中立付近の遊び", Mathf.Min(draft.deadzone, draft.sensitivity - .01f), 0, draft.sensitivity - .01f, "0.0");
                if (GUILayout.Button("左右反転: " + (draft.invert ? "ON" : "OFF"), button)) draft.invert = !draft.invert;
                draft.threshold = Mathf.RoundToInt(Slider("砲撃の閾値", draft.threshold, 1, 1023, "0"));
                draft.release = Mathf.RoundToInt(Slider("砲撃の再受付値", Mathf.Min(draft.release, draft.threshold - 1), 0, draft.threshold - 1, "0"));
                draft.strikeCooldown = Slider("打撃の連続受付間隔（秒）", draft.strikeCooldown, 0, 2, "0.00");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("適用", button)) Apply(false);
                if (GUILayout.Button("適用して保存", button)) Apply(true);
                if (GUILayout.Button("初期値に戻す", button)) { draft = JsonUtility.FromJson<ControlTuning>(JsonUtility.ToJson(defaults)); Apply(false); }
                if (GUILayout.Button("閉じる", button)) Close();
                GUILayout.EndHorizontal();
                if (message.Length > 0) GUILayout.Label(message, label);
            }
            finally { GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.matrix = previous; }
        }
        private void OnDisable() => Close();
        private void OnDestroy() { if (font != null) Destroy(font); if (backdrop != null) Destroy(backdrop); }
    }
}
