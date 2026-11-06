using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TankTown
{
    [DefaultExecutionOrder(-100)]
    public sealed class MicrobitController : MonoBehaviour
    {
        private MicrobitReceiveBuffer inbox = new MicrobitReceiveBuffer();
        private MicrobitSession session;
        private MicrobitInputSettings settings;
        private MicrobitWheel device;
        private Thread reader;
        private CancellationTokenSource cancellation;
        private volatile string connectionError;
        private string previousError;
        public bool Connected => session != null && session.Connected;
        public float Angle => session?.Angle ?? 0;
        public int Piezo => session?.Piezo ?? 0;
        public string ConfigurationPath { get; private set; }
        private static double Now => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;

        private void OnEnable()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "--microbit-off") >= 0 || GetComponent<TankInput>() == null) return;
            try
            {
                ConfigurationPath = Option(args, "--microbit-config") ?? Path.Combine(Path.GetDirectoryName(Application.dataPath), "microbit-controller.json");
                settings = new MicrobitInputSettings();
                if (File.Exists(ConfigurationPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(ConfigurationPath), settings);
                string port = Option(args, "--microbit-port");
                if (port != null) { settings.serialPort = port; settings.transport = "serial"; }
                if (Array.IndexOf(args, "--microbit-bluetooth") >= 0) settings.transport = "bluetooth";
                if (Array.IndexOf(args, "--microbit-invert") >= 0) settings.invertSteering = true;
                settings.Validate();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { Debug.LogWarning("MICROBIT CONFIG: " + e.Message + "; keyboard/gamepad available"); return; }
            if (settings.transport == "off") return;
            session = new MicrobitSession(settings);
            inbox = new MicrobitReceiveBuffer();
            inbox.Reset();
            InputSystem.onBeforeUpdate += ProcessInput;
            Debug.Log("MICROBIT WAITING: " + settings.transport + "; settings: " + ConfigurationPath);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            cancellation = new CancellationTokenSource();
            var source = cancellation;
            var readerInbox = inbox;
            var configuration = settings;
            reader = new Thread(() => { try { ReadLoop(source.Token, readerInbox, configuration); } finally { source.Dispose(); } })
                { IsBackground = true, Name = "TankTown Bluetooth/USB wheel" };
            reader.Start();
#endif
        }
        private static string Option(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void ReadLoop(CancellationToken token, MicrobitReceiveBuffer target, MicrobitInputSettings configuration)
        {
            while (!token.IsCancellationRequested)
            {
                if (configuration.transport == "auto" || configuration.transport == "bluetooth")
                {
                    try
                    {
                        foreach (string path in MicrobitBluetooth.FindServices(configuration.serviceUuid, configuration.bluetoothDevice))
                        {
                            if (token.IsCancellationRequested) return;
                            try { ReadBluetooth(path, token, target, configuration.receiveUuid, Math.Max(3000, (int)(configuration.timeoutSeconds * 2000))); }
                            catch (Exception e) when (TransportError(e)) { connectionError = "BLE: " + e.Message; }
                        }
                    }
                    catch (Exception e) when (TransportError(e)) { connectionError = "BLE discovery: " + e.Message; }
                }
                if (configuration.transport != "bluetooth")
                {
                    try
                    {
                        var ports = string.IsNullOrWhiteSpace(configuration.serialPort) ? MicrobitSerial.FindPorts() : new[] { configuration.serialPort };
                        foreach (string port in ports)
                        {
                            if (token.IsCancellationRequested) return;
                            try { ReadSerial(port, token, target, Math.Max(1500, (int)(configuration.timeoutSeconds * 2000))); }
                            catch (Exception e) when (TransportError(e)) { connectionError = "Serial: " + e.Message; }
                        }
                    }
                    catch (Exception e) when (TransportError(e)) { connectionError = "Serial discovery: " + e.Message; }
                }
                for (int i = 0; i < 20 && !token.IsCancellationRequested; i++) Thread.Sleep(100);
            }
        }
        private static bool TransportError(Exception e) => e is IOException || e is UnauthorizedAccessException ||
            e is System.Security.SecurityException || e is System.ComponentModel.Win32Exception || e is ArgumentException ||
            e is System.Runtime.InteropServices.COMException || e is DllNotFoundException || e is EntryPointNotFoundException;

        private void ReadSerial(string port, CancellationToken token, MicrobitReceiveBuffer target, int idleMillis)
        {
            using (var serial = new MicrobitSerial(port))
            {
                if (token.IsCancellationRequested) return;
                target.Reset();
                var decoder = new MicrobitLineDecoder();
                var buffer = new byte[256]; int validAt = Environment.TickCount;
                Action<MicrobitFrame> accept = frame =>
                {
                    if (token.IsCancellationRequested) return;
                    validAt = Environment.TickCount; target.Enqueue(frame, Now);
                };
                while (!token.IsCancellationRequested)
                {
                    int count = serial.Read(buffer);
                    decoder.Feed(buffer, count, accept);
                    if (unchecked(Environment.TickCount - validAt) > idleMillis) break;
                    Thread.Sleep(10);
                }
            }
            if (!token.IsCancellationRequested) target.Reset();
        }
        private void ReadBluetooth(string path, CancellationToken token, MicrobitReceiveBuffer target, string receiveUuid, int idleMillis)
        {
            target.Reset();
            var decoder = new MicrobitLineDecoder(); int validAt = Environment.TickCount;
            using (var bluetooth = new MicrobitBluetooth(path, receiveUuid, (bytes, count) =>
            {
                if (token.IsCancellationRequested) return;
                decoder.Feed(bytes, count, frame =>
                {
                    if (token.IsCancellationRequested) return;
                    Volatile.Write(ref validAt, Environment.TickCount); target.Enqueue(frame, Now);
                });
            }))
            {
                while (!token.IsCancellationRequested && unchecked(Environment.TickCount - Volatile.Read(ref validAt)) <= idleMillis) Thread.Sleep(20);
            }
            if (!token.IsCancellationRequested) target.Reset();
        }
#endif
        private void ProcessInput()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic || session == null) return;
            if (connectionError != null && connectionError != previousError)
            { previousError = connectionError; Debug.LogWarning("MICROBIT " + previousError); }
            bool fire = inbox.Process(session, Now, settings.timeoutSeconds);
            if (session.Connected)
            {
                if (device == null || !device.added) { device = MicrobitWheel.Create(); Debug.Log("MICROBIT CONNECTED: Input System wheel"); }
                device.Submit(session.Steering, fire && Application.isFocused, session.Angle, session.Piezo);
            }
            else ReleaseInput();
        }
        private void ReleaseInput()
        {
            if (device == null) return;
            if (device.added) InputSystem.RemoveDevice(device);
            device = null;
            Debug.Log("MICROBIT DISCONNECTED: keyboard/gamepad restored");
        }
        private void OnDisable()
        {
            InputSystem.onBeforeUpdate -= ProcessInput;
            try { cancellation?.Cancel(); } catch (ObjectDisposedException) { }
            reader?.Join(500); reader = null;
            // The worker disposes its source after any outstanding native BLE call returns.
            cancellation = null;
            inbox.Reset(); session?.Reset(); ReleaseInput();
        }
    }
}
