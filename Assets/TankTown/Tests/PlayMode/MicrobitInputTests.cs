using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TankTown.Tests
{
    public class MicrobitInputTests
    {
        [Test] public void InputSystemAxisActionsHornAndDisconnectReachTankInput()
        {
            bool previousBackground = Application.runInBackground;
            var previousBehavior = InputSystem.settings.backgroundBehavior;
            var previousEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var root = new GameObject("Microbit input integration test");
            MicrobitWheel device = null;
            try
            {
                var input = root.AddComponent<TankInput>();
                root.GetComponent<MicrobitController>().enabled = false;
                var gamepad = Gamepad.current;
                device = MicrobitWheel.Create();
                int shots = 0; input.PadPressed += _ => shots++;
                using (var steering = new InputAction(type: InputActionType.Value, binding: "<MicrobitWheel>/steering"))
                using (var horn = new InputAction(type: InputActionType.Button, binding: "<MicrobitWheel>/horn"))
                {
                    int actions = 0; horn.performed += _ => actions++;
                    steering.Enable(); horn.Enable();
                    // Prime ButtonControl's frame tracking before the first tap.
                    _ = device.horn.wasPressedThisFrame;
                    device.Submit(.37f, false); InputSystem.Update(); root.SendMessage("Update");
                    Assert.That(input.Steering, Is.EqualTo(.37f).Within(.0001f));
                    Assert.That(steering.ReadValue<float>(), Is.EqualTo(.37f).Within(.0001f));
                    Assert.That(shots, Is.Zero);
                    device.Submit(-.8f, true); InputSystem.Update(); root.SendMessage("Update");
                    Assert.That(shots, Is.EqualTo(1)); Assert.That(actions, Is.EqualTo(1));
                    Assert.That(device.horn.isPressed, Is.False);
                    device.Submit(0, true); InputSystem.Update(); root.SendMessage("Update");
                    Assert.That(shots, Is.EqualTo(2)); Assert.That(actions, Is.EqualTo(2));
                    device.Submit(0, false); InputSystem.Update(); root.SendMessage("Update");
                    Assert.That(shots, Is.EqualTo(2)); Assert.That(Gamepad.current, Is.SameAs(gamepad));
                    var session = new MicrobitSession();
                    session.Receive(Analog(45, 0), 0);
                    bool strike = session.Receive(Analog(45, 900), .02);
                    device.Submit(session.Steering, strike, session.Angle, session.Piezo);
                    InputSystem.Update(); root.SendMessage("Update");
                    Assert.That(shots, Is.EqualTo(3)); Assert.That(actions, Is.EqualTo(3));
                    Assert.That(input.Steering, Is.EqualTo((45f - 3) / 87).Within(.0001f));
                    Assert.That(device.angle.ReadValue(), Is.EqualTo(45));
                    Assert.That(device.piezo.ReadValue(), Is.EqualTo(900));
                    input.useHardware = true; input.SetWheel(.2f);
                    InputSystem.RemoveDevice(device); root.SendMessage("Update");
                    Assert.That(MicrobitWheel.current, Is.Null);
                    Assert.That(input.Steering, Is.EqualTo(.2f));
                }
            }
            finally
            {
                if (device != null && device.added) InputSystem.RemoveDevice(device);
                Object.DestroyImmediate(root);
                Application.runInBackground = previousBackground;
                InputSystem.settings.backgroundBehavior = previousBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditor;
            }
        }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [Test] public void WindowsDiscoveryAndMissingPortFailWithoutCrashing()
        {
            Assert.That(MicrobitSerial.FindPorts(), Is.Not.Null);
            Assert.Throws<System.ComponentModel.Win32Exception>(() => { using (var port = new MicrobitSerial("COM999999")) { } });
        }
#endif
        [TestCase("TT1,-1000,0", -1f)]
        [TestCase("TT1,1000,65535\r", 1f)]
        [TestCase("TT1,0,42", 0f)]
        public void ParsesSignedSteering(string text, float expected)
        { Assert.That(MicrobitFrame.TryParse(text, out var frame), Is.True); Assert.That(frame.Steering, Is.EqualTo(expected)); }
        [TestCase("TT1,1001,0")]
        [TestCase("TT1,NaN,0")]
        [TestCase("TT1,0,-1")]
        [TestCase("TT1,0,65536")]
        [TestCase("TT1,0,0,1")]
        [TestCase("hello")]
        public void RejectsBadOrUnrelatedInput(string text) => Assert.That(MicrobitFrame.TryParse(text, out _), Is.False);
        [Test] public void BaselinesThenTriggersOnceAndHandlesWrap()
        {
            var state = new MicrobitSession();
            Assert.That(state.Receive(new MicrobitFrame(.5f, 65535), 0), Is.False);
            Assert.That(state.Receive(new MicrobitFrame(.5f, 0), .02), Is.True);
            Assert.That(state.Receive(new MicrobitFrame(.5f, 0), .04), Is.False);
            Assert.That(state.Receive(new MicrobitFrame(1, 2), .06), Is.True);
        }
        [Test] public void StaleDataNeutralizesAndReconnectDoesNotShoot()
        {
            var state = new MicrobitSession(); state.Receive(new MicrobitFrame(1, 100), 0);
            Assert.That(state.Expire(.2), Is.False); Assert.That(state.Expire(.36), Is.True);
            Assert.That(state.Steering, Is.Zero); Assert.That(state.Connected, Is.False);
            Assert.That(state.Receive(new MicrobitFrame(-1, 105), .4), Is.False);
            Assert.That(state.Receive(new MicrobitFrame(-1, 106), .42), Is.True);
            Assert.That(state.Receive(new MicrobitFrame(1, 500), 1), Is.False);
        }
        [Test] public void BoardResetDoesNotReplayOldHornCount()
        {
            var state = new MicrobitSession(); state.Receive(new MicrobitFrame(0, 100), 0);
            Assert.That(state.Receive(new MicrobitFrame(0, 0), .02), Is.False);
            Assert.That(state.Receive(new MicrobitFrame(0, 1), .04), Is.True);
        }

        [TestCase("-90,0\n", -90f, 0)]
        [TestCase("90,1023\r\n", 90f, 1023)]
        [TestCase(" -22.5 , 512 \n", -22.5f, 512)]
        [TestCase("+0.5,350", .5f, 350)]
        public void ParsesAngleAndPiezo(string line, float angle, int piezo)
        {
            Assert.That(MicrobitFrame.TryParse(line, out var frame), Is.True);
            Assert.That(frame.IsAnalog, Is.True);
            Assert.That(frame.Angle, Is.EqualTo(angle)); Assert.That(frame.Piezo, Is.EqualTo(piezo));
        }
        [TestCase("91,0")][TestCase("-90.1,100")][TestCase("NaN,0")][TestCase("Infinity,0")]
        [TestCase("0,1024")][TestCase("0,-1")][TestCase("0,3.5")][TestCase("1,2,3")][TestCase("[1,2]")]
        public void RejectsInvalidAnalogFrames(string line) => Assert.That(MicrobitFrame.TryParse(line, out _), Is.False);

        private static MicrobitFrame Analog(float angle, int piezo)
        {
            Assert.That(MicrobitFrame.TryParse(angle.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + piezo, out var frame), Is.True);
            return frame;
        }
        [Test] public void AngleCalibrationDeadzoneAndInversion()
        {
            var config = new MicrobitInputSettings { angleCenter = 5, fullSteerAngle = 45, deadzoneDegrees = 5, invertSteering = true };
            config.Validate(); var session = new MicrobitSession(config);
            session.Receive(Analog(9, 0), 0); Assert.That(session.Steering, Is.Zero);
            session.Receive(Analog(30, 0), .02); Assert.That(session.Steering, Is.EqualTo(-.5f).Within(.0001f));
            session.Receive(Analog(-90, 0), .04); Assert.That(session.Steering, Is.EqualTo(1));
            session.Receive(Analog(90, 0), .06); Assert.That(session.Steering, Is.EqualTo(-1));
        }
        [Test] public void PiezoEdgesHysteresisAndCooldownPreventRingingAndHeldRepeat()
        {
            var session = new MicrobitSession();
            Assert.That(session.Receive(Analog(0, 700), 0), Is.False, "Connected while held: baseline only");
            Assert.That(session.Receive(Analog(0, 0), .02), Is.False);
            Assert.That(session.Receive(Analog(0, 350), .04), Is.True);
            Assert.That(session.Receive(Analog(0, 340), .06), Is.False);
            Assert.That(session.Receive(Analog(0, 700), .08), Is.False, "Not released below 180");
            Assert.That(session.Receive(Analog(0, 180), .09), Is.False);
            Assert.That(session.Receive(Analog(0, 700), .10), Is.False, "Cooldown consumes this new edge");
            Assert.That(session.Receive(Analog(0, 700), .20), Is.False, "Holding after cooldown cannot fire");
            Assert.That(session.Receive(Analog(0, 0), .22), Is.False);
            Assert.That(session.Receive(Analog(0, 1023), .24), Is.True);
            session.Expire(1); Assert.That(session.Piezo, Is.Zero);
            Assert.That(session.Receive(Analog(0, 1023), 1.1), Is.False, "Reconnect while held");
        }
        [Test] public void ChunkedNotificationsPreserveShortStrikeAndRecoverAfterOverflow()
        {
            var decoder = new MicrobitLineDecoder(); var inbox = new MicrobitReceiveBuffer(); var session = new MicrobitSession();
            double at = 0;
            System.Action<string> feed = text => { var bytes = System.Text.Encoding.ASCII.GetBytes(text); decoder.Feed(bytes, bytes.Length, frame => { inbox.Enqueue(frame, at); at += .02; }); };
            feed("-4,0\n45,"); feed("800\r"); feed("\n45,0\n");
            Assert.That(inbox.Process(session, .06, .35), Is.True, "Peak and release received in one rendered frame");
            Assert.That(session.Angle, Is.EqualTo(45)); Assert.That(session.Piezo, Is.Zero);
            Assert.That(inbox.Process(session, .08, .35), Is.False);
            feed(new string('x', 100) + "\n-90,0\n");
            Assert.That(inbox.Process(session, .09, .35), Is.False); Assert.That(session.Steering, Is.EqualTo(-1));
            feed("0,900\n"); Assert.That(inbox.Process(session, 2, .35), Is.False, "Stale queued shot discarded");
            Assert.That(session.Connected, Is.False);
        }
        [Test] public void ReceiveQueueIsBoundedAndResetsBaselineOnOverflow()
        {
            var inbox = new MicrobitReceiveBuffer(); var session = new MicrobitSession();
            session.Receive(Analog(0, 0), 0);
            for (int i = 0; i < 130; i++) inbox.Enqueue(Analog(0, 900), .1);
            Assert.That(inbox.Process(session, .2, .35), Is.False);
            Assert.That(session.Piezo, Is.EqualTo(900));
        }
        [Test] public void PartialJsonKeepsDefaultsAndRejectsInvalidThresholds()
        {
            var config = new MicrobitInputSettings();
            JsonUtility.FromJsonOverwrite("{\"piezoThreshold\":500}", config);
            config.Validate(); Assert.That(config.fullSteerAngle, Is.EqualTo(90));
            config.piezoRelease = 500; Assert.Throws<System.ArgumentException>(config.Validate);
        }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [Test] public void BleDiscoveryAndNativeLayoutsMatchWindowsSdk()
        {
            Assert.That(MicrobitBluetooth.FindServices(new MicrobitInputSettings().serviceUuid, ""), Is.Not.Null);
            Assert.Throws<System.ComponentModel.Win32Exception>(() => { using (var ble = new MicrobitBluetooth(@"\\?\BTHLEDevice#TankTownMissingDevice", "", (_, __) => { })) { } });
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf<MicrobitBluetooth.Uuid>(), Is.EqualTo(20));
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf<MicrobitBluetooth.Characteristic>(), Is.EqualTo(36));
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf<MicrobitBluetooth.Descriptor>(), Is.EqualTo(32));
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf<MicrobitBluetooth.DescriptorValue>(), Is.EqualTo(80));
            Assert.That(System.Runtime.InteropServices.Marshal.SizeOf<MicrobitBluetooth.Registration>(), Is.EqualTo(40));
            string path = @"\\?\BTHLEDevice#{6e400001-b5a3-f393-e0a9-e50e24dcca9e}_Dev_AABBCCDDEEFF";
            Assert.That(MicrobitBluetooth.MatchesService(path, new MicrobitInputSettings().serviceUuid, "AA:BB:CC:DD:EE:FF"), Is.True);
            Assert.That(MicrobitBluetooth.MatchesService(path, new MicrobitInputSettings().serviceUuid, "00:11:22:33:44:55"), Is.False);
        }
#endif
    }
}
